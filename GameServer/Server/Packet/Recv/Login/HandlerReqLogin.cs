using MikuSB.Data;
using MikuSB.Database;
using MikuSB.Database.Account;
using MikuSB.Database.Player;
using MikuSB.GameServer.Game.Player;
using MikuSB.GameServer.Server.CallGS;
using MikuSB.GameServer.Server.CallGS.Handlers.Girl;
using MikuSB.GameServer.Server.Packet.Send.Friend;
using MikuSB.GameServer.Server.Packet.Send.Login;
using MikuSB.GameServer.Server.Packet.Send.Misc;
using MikuSB.Proto;
using MikuSB.TcpSharp;
using MikuSB.Util;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MikuSB.GameServer.Server.Packet.Recv.Login;

[Opcode(CmdIds.ReqLogin)]
public class HandlerReqLogin : Handler
{
    private static readonly Logger Logger = new("ReqLogin");
    private const int SupportCardLoginSplitThreshold = 2000;

    private static readonly string[] TokenJsonKeys =
    [
        "authToken", "token", "Token", "session", "sessionId", "comboToken", "dispatchToken"
    ];

    private static string? ExtractJsonField(string? token, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var normalized = Uri.UnescapeDataString(token).Trim();
            var padding = normalized.Length % 4;
            if (padding > 0)
                normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
            using var document = JsonDocument.Parse(json);
            foreach (var key in keys)
            {
                if (document.RootElement.TryGetProperty(key, out var value))
                {
                    if (value.ValueKind == JsonValueKind.String)
                        return value.GetString();
                    if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                        return value.ToString();
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? ExtractSdkAuthToken(string? token)
        => ExtractJsonField(token, TokenJsonKeys);

    private static AccountData? ResolveLoginAccount(ReqLogin req, string? sdkAuthToken)
    {
        var wrappedUid = ExtractJsonField(req.Token, "uid", "Uid", "passportId", "pid");
        return AccountData.GetAccountByComboToken(req.Token)
               ?? AccountData.GetAccountByDispatchToken(req.Token)
               ?? AccountData.GetAccountByComboToken(sdkAuthToken ?? "")
               ?? AccountData.GetAccountByDispatchToken(sdkAuthToken ?? "")
               ?? AccountData.GetAccountByUserName(req.Token)
               ?? AccountData.GetAccountByUserName(req.Provider)
               ?? (int.TryParse(wrappedUid, out var wrappedUidValue) ? AccountData.GetAccountByUid(wrappedUidValue) : null)
               ?? AccountData.GetAccountByUid(10001)
               ?? AccountData.GetAccountByUserName("player")
               ?? AccountData.GetFirstAccount();
    }

    public override async Task OnHandle(Connection connection, byte[] data, ushort seqNo)
    {
        var req = ReqLogin.Parser.ParseFrom(data);
        var sdkAuthToken = ExtractSdkAuthToken(req.Token);
        Logger.Info($"ReqLogin provider={req.Provider}, tokenLen={req.Token?.Length ?? 0}, authToken={sdkAuthToken ?? "<none>"}");

        var account = ResolveLoginAccount(req, sdkAuthToken);
        if (account == null && ConfigManager.Config.ServerOption.AutoCreateUser)
        {
            var fallbackName = string.IsNullOrWhiteSpace(req.Provider) ? "player" : req.Provider;
            if (AccountData.GetAccountByUserName(fallbackName) == null)
                AccountData.CreateAccount(fallbackName, 0, "123456");
            account = AccountData.GetAccountByUserName(fallbackName)
                      ?? AccountData.GetAccountByUserName("player")
                      ?? AccountData.GetFirstAccount();
        }

        if (account == null)
        {
            Logger.Warn($"Rejected login: provider={req.Provider}, token={req.Token}, authToken={sdkAuthToken}");
            await connection.SendPacket(CmdIds.NtfLogout);
            return;
        }

        connection.SessionId = Guid.NewGuid().ToString("N");
        Logger.Info($"Player ReqLogin authenticated: Uid={account.Uid}, Username={account.Username}, session={connection.SessionId}");
        if (!ResourceManager.IsLoaded)
        {
            Logger.Warn("Resource manager is not loaded yet, delaying login is not supported; returning without RspLogin");
            return;
        }
        var prev = Listener.GetActiveConnection(account.Uid);
        if (prev != null && !ReferenceEquals(prev, connection))
        {
            try
            {
                await prev.SendPacket(CmdIds.NtfLogout);
            }
            catch
            {
            }
            prev.Stop();
        }

        connection.State = SessionStateEnum.WAITING_FOR_LOGIN;
        var pd = DatabaseHelper.GetInstance<PlayerGameData>(account.Uid);
        connection.Player = pd == null ? new PlayerInstance(account.Uid) : new PlayerInstance(pd);
        if (connection.Player.Data.EnsureDisplayName())
            DatabaseHelper.UpdateInstance(connection.Player.Data);

        connection.DebugFile = Path.Combine(ConfigManager.Config.Path.LogPath, "Debug/", $"{account.Uid}/",
            $"Debug-{DateTime.Now:yyyy-MM-dd HH-mm-ss}.log");
        await connection.Player.OnEnterGame();
        connection.Player.Connection = connection;
        var splitSupportCards = connection.Player.InventoryManager.InventoryData.SupportCards.Count > SupportCardLoginSplitThreshold;
        await connection.SendPacket(new PacketRspLogin(connection.Player!, !splitSupportCards));
        connection.State = SessionStateEnum.ACTIVE;
        if (splitSupportCards)
            await SendSupportCardsOnLogin(connection);
        await connection.SendPacket(new PacketNtfCallScript(connection.Player!));
        await SendDebugLoginState(connection);

        await connection.Player.OnHeartBeat();
        await connection.SendPacket(new PacketNtfUpdateFriend(connection.Player!));
        ApplySavedGirlSkinTypes(connection.Player!);
        await SendGirlSkinTypeOnLogin(connection);
    }

    private static async Task SendSupportCardsOnLogin(Connection connection)
    {
        var player = connection.Player;
        if (player == null)
            return;

        var supportCards = player.InventoryManager.InventoryData.SupportCards.Values.ToList();
        Logger.Info($"Split support card sync on login: total={supportCards.Count}, chunkSize={SupportCardLoginSplitThreshold}");

        foreach (var chunk in supportCards.Chunk(SupportCardLoginSplitThreshold))
        {
            var packet = new PacketNtfCallScript(chunk.ToList());
            await connection.SendPacket(packet);
        }
    }

    private static void ApplySavedGirlSkinTypes(PlayerInstance player)
    {
        var inventoryData = player.InventoryManager.InventoryData;
        inventoryData.SkinTypesBySkinId ??= [];
        var changed = false;

        foreach (var (skinId, skinType) in inventoryData.SkinTypesBySkinId.ToArray())
        {
            var clamped = GirlSkin_ChangeSkinType.ClampClientSkinType(skinType);
            if (clamped != skinType)
            {
                inventoryData.SkinTypesBySkinId[skinId] = clamped;
                changed = true;
            }

            var skinData = GirlSkin_ChangeSkinType.GetOrCreateSkinItem(player, skinId);
            if (skinData != null && skinData.SkinType != clamped)
            {
                skinData.SkinType = clamped;
                changed = true;
            }
        }

        if (changed)
            DatabaseHelper.SaveDatabaseType(inventoryData);
    }

    private static async Task SendGirlSkinTypeOnLogin(Connection connection)
    {
        var player = connection.Player;
        if (player == null)
            return;

        var inventoryData = player.InventoryManager.InventoryData;
        inventoryData.SkinTypesBySkinId ??= [];
        foreach (var (skinId, skinType) in inventoryData.SkinTypesBySkinId)
        {
            var clamped = GirlSkin_ChangeSkinType.ClampClientSkinType(skinType);
            var skinData = GirlSkin_ChangeSkinType.GetOrCreateSkinItem(player, skinId);
            var response = new JsonObject
            {
                ["nType"] = clamped,
                ["nSkinId"] = skinId
            };

            if (skinData == null)
            {
                await CallGSRouter.SendScript(connection, "GirlSkin_ChangeSkinType", response.ToJsonString());
                continue;
            }

            await CallGSRouter.SendScript(connection, "GirlSkin_ChangeSkinType", response.ToJsonString(), new NtfSyncPlayer
            {
                Items = { skinData.ToProto() }
            });
        }
    }

    private static async Task SendDebugLoginState(Connection connection)
    {
        var response = new JsonObject
        {
            ["IsDebug"] = ConfigManager.Config.ServerOption.EnableGmMenu
        };

        await CallGSRouter.SendScript(connection, "gm.notifylogin", response.ToJsonString());
    }
}
