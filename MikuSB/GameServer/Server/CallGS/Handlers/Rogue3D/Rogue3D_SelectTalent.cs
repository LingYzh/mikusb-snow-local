using MikuSB.Database.Player;
using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Rogue3D;

// Selects the Rogue3D talent and persists it as player attribute (GroupId=124, TalentId=7).
// param: {"nTalentId": int}
// Response: {} on success, {"sErr": "key"} on failure
[CallGSApi("Rogue3D_SelectTalent")]
public class Rogue3D_SelectTalent : ICallGSHandler
{
    private const uint GroupId = 124;
    private const uint TalentIdSid = 7;

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var req = JsonSerializer.Deserialize<SelectTalentParam>(param);
        if (req == null)
        {
            await CallGSRouter.SendScript(connection, "Rogue3D_SelectTalent", "{}");
            return;
        }

        var player = connection.Player!;
        var attr = player.Data.Attrs.FirstOrDefault(x => x.Gid == GroupId && x.Sid == TalentIdSid);
        if (attr == null)
        {
            attr = new PlayerAttr { Gid = GroupId, Sid = TalentIdSid };
            player.Data.Attrs.Add(attr);
        }
        attr.Val = req.TalentId;

        var sync = new NtfSyncPlayer();
        sync.Custom[player.ToPackedAttrKey(GroupId, TalentIdSid)] = attr.Val;
        sync.Custom[player.ToShiftedAttrKey(GroupId, TalentIdSid)] = attr.Val;

        await CallGSRouter.SendScript(connection, "Rogue3D_SelectTalent", "{}", sync);
    }
}

internal sealed class SelectTalentParam
{
    [JsonPropertyName("nTalentId")]
    public uint TalentId { get; set; }
}
