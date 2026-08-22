using MikuSB.Database.Player;
using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Scene;

// Response:{sErr:true or false}
[CallGSApi("ChangeMainScene")]
public class ChangeMainScene : ICallGSHandler
{
    private const int MainSceneGID = 132;
    private const int MainSceneSID = 1;

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        string rsp = $"{{\"sErr\":false}}";
        var req = JsonSerializer.Deserialize<ChangeMainSceneParam>(param);
        if (req == null) 
        {
            await CallGSRouter.SendScript(connection, "ChangeMainScene", rsp);
            return;
        } 

        var player = connection.Player!;
        var mainSceneAttr = player.Data.Attrs
            .FirstOrDefault(x => x.Gid == MainSceneGID && x.Sid == MainSceneSID);

        if (mainSceneAttr == null)
        {
            mainSceneAttr = new PlayerAttr
            {
                Gid = MainSceneGID,
                Sid = MainSceneSID
            };
            player.Data.Attrs.Add(mainSceneAttr);
        }
        var sync = new NtfSyncPlayer();
        mainSceneAttr.Val = req.Id;

        sync.Custom[player.ToPackedAttrKey(MainSceneGID, MainSceneSID)] = mainSceneAttr.Val;
        sync.Custom[player.ToShiftedAttrKey(MainSceneGID, MainSceneSID)] = mainSceneAttr.Val;
        await CallGSRouter.SendScript(connection, "ChangeMainScene", rsp, sync);
    }
}

internal sealed class ChangeMainSceneParam
{
    [JsonPropertyName("nId")]
    public uint Id { get; set; }
}