using MikuSB.Enums.Item;
using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Girl;

[CallGSApi("GirlCard_SetRoleLikeFlag")]
public class GirlCard_SetRoleLikeFlag : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var player = connection.Player!;
        var girlData = JsonSerializer.Deserialize<SetRoleLikeFlagParam>(param);
        if (girlData == null) return;

        var cardData = player.CharacterManager.GetCharacterByGUID(girlData.CardId);
        if (cardData == null) return;

        cardData.Flag = girlData.Flag == 1
            ? ItemFlagEnum.FLAG_ROLE_LIKE
            : ItemFlagEnum.FLAG_READED;

        var sync = new NtfSyncPlayer
        {
            Items = { cardData.ToProto() }
        };

        await CallGSRouter.SendScript(connection, "GirlCard_SetRoleLikeFlag", "{}", sync);
    }
}

internal sealed class SetRoleLikeFlagParam
{
    [JsonPropertyName("nFlag")]
    public int Flag { get; set; }

    [JsonPropertyName("nCardID")]
    public uint CardId { get; set; }
}