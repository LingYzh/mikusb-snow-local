using MikuSB.Data;
using MikuSB.Data.Excel;
using MikuSB.Database;
using MikuSB.Enums.Item;
using MikuSB.GameServer.Game.Player;
using MikuSB.GameServer.Game.Reward;
using MikuSB.GameServer.Server.Packet;
using MikuSB.Proto;
using MikuSB.Util;
using Newtonsoft.Json.Linq;
using System.Text.Json.Nodes;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Inventory;

internal static class ItemBoxService
{
    private static readonly Logger Logger = new("ItemBox");
    private static readonly Random Rng = new();
    private static readonly object BoxConfigLock = new();
    private static Dictionary<uint, List<BoxEntry>>? boxEntries;

    public static async Task OpenBoxAsync(Connection connection, string param)
    {
        var request = ParseObject(param);
        var itemId = ReadUInt(request?["Id"] ?? request?["nId"] ?? request?["ItemId"]);
        var count = Math.Max(1u, ReadUInt(request?["Count"] ?? request?["nCount"]));
        var player = connection.Player!;
        var item = player.InventoryManager.GetNormalItem(itemId);
        if (item == null || item.ItemType != ItemTypeEnum.TYPE_USEABLE ||
            !GameData.OtherItemData.TryGetValue(item.TemplateId, out var template) ||
            template.LuaType != "itembox")
        {
            await SendError(connection, "Item_OpenBox");
            return;
        }

        count = Math.Min(count, item.ItemCount);
        var awards = ResolveBoxAwards(template, count, request);
        if (awards.Count == 0)
        {
            await SendError(connection, "Item_OpenBox");
            return;
        }

        // Build one sync packet: consume the source first, then grant every
        // configured fixed/random/selected reward from item/box.json.
        var sync = new NtfSyncPlayer();
        if (!RewardGrant.TryConsumeItem(player, sync, item.TemplateId, count))
        {
            await SendError(connection, "Item_OpenBox");
            return;
        }
        await RewardGrant.GrantManyAsync(player, sync, awards);
        RewardGrant.Save(player);

        var response = new JsonObject { ["tbAward"] = RewardGrant.ToAwardArray(awards) };
        Logger.Info($"OpenBox item={itemId} template={item.TemplateId} count={count} awards={awards.Count}");
        await CallGSRouter.SendScript(connection, "Item_OpenBox", response.ToJsonString(), sync);
    }

    public static async Task UseCashBoxAsync(Connection connection, uint itemId, uint count)
    {
        var player = connection.Player!;
        var item = player.InventoryManager.GetNormalItem(itemId);
        if (item == null || item.ItemType != ItemTypeEnum.TYPE_USEABLE ||
            !GameData.OtherItemData.TryGetValue(item.TemplateId, out var template))
        {
            await connection.SendPacket(CmdIds.RspUseItem);
            return;
        }

        count = Math.Min(Math.Max(1u, count), item.ItemCount);
        uint moneyType;
        uint amountPerItem;
        string responseApi;
        switch (template.LuaType)
        {
            case "vigor_box":
                moneyType = 4;
                amountPerItem = template.Param1;
                responseApi = "VigorBox_Use";
                break;
            case "token_box":
            case "cashbox":
                moneyType = template.Param1;
                amountPerItem = template.Param2;
                responseApi = "TokenBox_Use";
                break;
            default:
                await connection.SendPacket(CmdIds.RspUseItem);
                return;
        }

        var sync = new NtfSyncPlayer();
        if (moneyType == 0 || amountPerItem == 0 ||
            !RewardGrant.TryConsumeItem(player, sync, item.TemplateId, count))
        {
            await connection.SendPacket(CmdIds.RspUseItem);
            return;
        }

        RewardGrant.GrantCash(player, sync, moneyType, checked(amountPerItem * count));
        RewardGrant.Save(player);
        var response = new JsonObject { ["tbAward"] = new JsonArray() };
        Logger.Info($"UseCashBox item={itemId} template={item.TemplateId} count={count}");
        await CallGSRouter.SendScript(connection, responseApi, response.ToJsonString(), sync);
        await connection.SendPacket(CmdIds.RspUseItem);
    }

    private static List<List<uint>> ResolveBoxAwards(OtherItemExcel template, uint count, JsonObject? request)
    {
        var entries = LoadBoxEntries().GetValueOrDefault(template.Param1) ?? [];
        var awards = new List<List<uint>>();
        AddAwards(entries.Where(x => x.ObtainType.Equals("Fix", StringComparison.OrdinalIgnoreCase)), count, awards);

        foreach (var group in entries.Where(x => x.ObtainType.Equals("Random", StringComparison.OrdinalIgnoreCase)).GroupBy(x => x.Group))
        {
            var chosen = PickWeighted(group.ToList());
            if (chosen != null)
                AddAward(chosen.Gdpln, count, awards);
        }

        foreach (var selected in ReadSelections(request))
        {
            var selectedEntry = entries.FirstOrDefault(x =>
                x.ObtainType.Equals("Select", StringComparison.OrdinalIgnoreCase) &&
                x.Gdpln.SequenceEqual(selected));
            if (selectedEntry != null)
                AddAward(selectedEntry.Gdpln, count, awards);
        }

        return awards;
    }

    private static void AddAwards(IEnumerable<BoxEntry> entries, uint count, List<List<uint>> awards)
    {
        foreach (var entry in entries)
            AddAward(entry.Gdpln, count, awards);
    }

    private static void AddAward(IReadOnlyList<uint> reward, uint multiplier, List<List<uint>> awards)
    {
        if (reward.Count < 4)
            return;
        var row = reward.Take(5).ToList();
        while (row.Count < 5)
            row.Add(1);
        row[4] = checked(Math.Max(1u, row[4]) * multiplier);
        awards.Add(row);
    }

    private static BoxEntry? PickWeighted(List<BoxEntry> entries)
    {
        if (entries.Count == 0)
            return null;
        var total = entries.Sum(x => Math.Max(1, x.Weight));
        var roll = Rng.Next(total);
        foreach (var entry in entries)
        {
            roll -= Math.Max(1, entry.Weight);
            if (roll < 0)
                return entry;
        }
        return entries[^1];
    }

    private static Dictionary<uint, List<BoxEntry>> LoadBoxEntries()
    {
        lock (BoxConfigLock)
        {
            if (boxEntries != null)
                return boxEntries;

            boxEntries = [];
            var path = Path.Combine(ConfigManager.Config.Path.ResourcePath, "item", "box.json");
            if (!File.Exists(path))
                return boxEntries;

            foreach (var row in JArray.Parse(File.ReadAllText(path)).OfType<JObject>())
            {
                var index = ReadUInt(row["Index"]);
                var reward = row["GDPLN"] is JArray rewardTokens
                    ? rewardTokens.Select(ReadUInt).ToList()
                    : [];
                if (index == 0 || reward.Count < 4)
                    continue;
                var entry = new BoxEntry(
                    row.Value<string>("ObtainType") ?? "Fix",
                    ReadInt(row["Group"], 1),
                    ReadInt(row["Weight"], 1),
                    reward);
                if (!boxEntries.TryGetValue(index, out var entries))
                    boxEntries[index] = entries = [];
                entries.Add(entry);
            }

            return boxEntries;
        }
    }

    private static IEnumerable<IReadOnlyList<uint>> ReadSelections(JsonObject? request)
    {
        if (request?["tbSelect"] is not JsonArray selections)
            yield break;
        foreach (var node in selections.OfType<JsonObject>())
        {
            if (node["tbGDPLN"] is not JsonArray reward)
                continue;
            var values = reward.Select(ReadUInt).ToList();
            if (values.Count >= 4)
                yield return values;
        }
    }

    private static JsonObject? ParseObject(string raw)
    {
        try { return JsonNode.Parse(raw) as JsonObject; }
        catch { return null; }
    }

    private static uint ReadUInt(JsonNode? node)
    {
        if (node is not JsonValue value)
            return 0;
        if (value.TryGetValue<uint>(out var u)) return u;
        if (value.TryGetValue<int>(out var i) && i > 0) return (uint)i;
        if (value.TryGetValue<long>(out var l) && l > 0) return (uint)l;
        return value.TryGetValue<string>(out var s) && uint.TryParse(s, out var parsed) ? parsed : 0;
    }

    private static uint ReadUInt(JToken? token)
    {
        if (token == null)
            return 0;
        return token.Type switch
        {
            JTokenType.Integer => token.Value<uint>(),
            JTokenType.Float => (uint)Math.Max(0, token.Value<decimal>()),
            JTokenType.String when uint.TryParse(token.Value<string>(), out var value) => value,
            _ => 0
        };
    }

    private static int ReadInt(JToken? token, int fallback)
    {
        var value = ReadUInt(token);
        return value == 0 ? fallback : checked((int)value);
    }

    private static Task SendError(Connection connection, string api) =>
        CallGSRouter.SendScript(connection, api, "{\"sErr\":\"error.BadParam\"}");

    private sealed record BoxEntry(string ObtainType, int Group, int Weight, List<uint> Gdpln);
}

[CallGSApi("Item_OpenBox")]
public class Item_OpenBox : ICallGSHandler
{
    public Task Handle(Connection connection, string param, ushort seqNo) => ItemBoxService.OpenBoxAsync(connection, param);
}

[Opcode(CmdIds.ReqUseItem)]
public class HandlerReqUseItem : Handler
{
    public override Task OnHandle(Connection connection, byte[] data, ushort seqNo)
    {
        var request = ReqUseItem.Parser.ParseFrom(data);
        return ItemBoxService.UseCashBoxAsync(connection, (uint)request.Id, request.Count);
    }
}
