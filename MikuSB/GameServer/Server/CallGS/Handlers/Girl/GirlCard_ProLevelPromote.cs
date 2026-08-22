using MikuSB.Database;
using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Girl;

[CallGSApi("GirlCard_ProLevelPromote")]
public class GirlCard_ProLevelPromote : ICallGSHandler
{
    private const uint MaxProLevel = 3;

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var player = connection.Player!;
        var req = JsonSerializer.Deserialize<ProLevelPromoteParam>(param);
        if (req == null || req.CardId == 0)
        {
            await CallGSRouter.SendScript(connection, "GirlCard_ProLevelPromote", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        var card = player.CharacterManager.GetCharacterByGUID((uint)req.CardId);
        if (card == null)
        {
            await CallGSRouter.SendScript(connection, "GirlCard_ProLevelPromote", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        if (card.ProLevel >= MaxProLevel)
        {
            await CallGSRouter.SendScript(connection, "GirlCard_ProLevelPromote", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        card.ProLevel++;

        DatabaseHelper.SaveDatabaseType(player.CharacterManager.CharacterData);

        var sync = new NtfSyncPlayer();
        sync.Items.Add(card.ToProto());

        // s2c callback takes no params — return empty arg
        await CallGSRouter.SendScript(connection, "GirlCard_ProLevelPromote", "{}", sync);
    }
}

internal sealed class ProLevelPromoteParam
{
    [JsonPropertyName("nID")]
    public int CardId { get; set; }
}
