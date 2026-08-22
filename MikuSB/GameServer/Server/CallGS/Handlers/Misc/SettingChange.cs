using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Misc;

[CallGSApi("SettingChange")]
public class SettingChange : ICallGSHandler
{
    private const uint PlayerSettingGid = 44;

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var changes = JsonSerializer.Deserialize<List<SettingChangeParam>>(param) ?? [];
        var player = connection.Player!;
        var sync = new NtfSyncPlayer();

        foreach (var change in changes)
        {
            var value = player.Data.StrAttrs
                .FirstOrDefault(x => x.Gid == PlayerSettingGid && x.Sid == change.Id)?
                .Val;

            if (value == null)
                continue;

            sync.CustomStr[player.ToShiftedAttrKey(PlayerSettingGid, change.Id)] = value;
        }

        if (sync.CustomStr.Count > 0)
            await connection.SendPacket(CmdIds.NtfSyncAttr, sync);
    }
}

internal sealed class SettingChangeParam
{
    [JsonPropertyName("id")]
    public uint Id { get; set; }
}
