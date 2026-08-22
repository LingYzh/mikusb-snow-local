using MikuSB.Database.Player;
using MikuSB.GameServer.Server.CallGS.Handlers.Misc;
using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Preview;

[CallGSApi("RecordConfession")]
public class RecordConfession : ICallGSHandler
{
    private const int MainSceneGID = 132;
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var req = JsonSerializer.Deserialize<RecordConfessionParam>(param);
        if (req == null) return;
        var sid = req.Id + 10;
        var player = connection.Player!;
        var attr = player.Data.Attrs
            .FirstOrDefault(x => x.Gid == MainSceneGID && x.Sid == sid);
        if (attr == null)
        {
            attr = new PlayerAttr
            {
                Gid = MainSceneGID,
                Sid = sid,
                Val = 1
            };
            player.Data.Attrs.Add(attr);
        }
        var sync = new NtfSyncPlayer();
        sync.Custom[player.ToPackedAttrKey(MainSceneGID, sid)] = attr.Val;
        sync.Custom[player.ToShiftedAttrKey(MainSceneGID, sid)] = attr.Val;
        await CallGSRouter.SendScript(connection, "RecordConfession", "{}", sync);
    }
}

internal sealed class RecordConfessionParam
{
    [JsonPropertyName("nIdx")]
    public uint Id { get; set; }
}