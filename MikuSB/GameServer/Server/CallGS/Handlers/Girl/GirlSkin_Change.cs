using MikuSB.Proto;
using System.Text.Json;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Girl;

[CallGSApi("GirlSkin_Change")]
public class GirlSkin_Change : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var player = connection.Player!;
        var girlSkinData = JsonSerializer.Deserialize<ChangeSkinParam>(param);
        var cardData = player.CharacterManager.GetCharacterByGUID((uint)girlSkinData!.CardId);
        if (cardData == null) return;

        cardData.SkinId = (uint)girlSkinData.Id;

        var sync = new NtfSyncPlayer
        {
            Items = { cardData.ToProto() }
        };
        await CallGSRouter.SendScript(connection, "GirlSkin_Change", "{}", sync);
    }
}
