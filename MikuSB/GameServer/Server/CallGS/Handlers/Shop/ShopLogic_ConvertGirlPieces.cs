using MikuSB.Data;
using MikuSB.Database;
using MikuSB.Enums.Item;
using MikuSB.GameServer.Game.Player;
using MikuSB.GameServer.Game.Reward;
using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Shop;

[CallGSApi("ShopLogic_ConvertGirlPieces")]
public class ShopLogic_ConvertGirlPieces : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var requests = JsonSerializer.Deserialize<List<GirlPiecesConvertParam>>(param);
        if (requests == null || requests.Count == 0)
        {
            await CallGSRouter.SendScript(connection, "ShopLogic_ConvertGirlPieces", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        var player = connection.Player!;
        var consumes = new Dictionary<ulong, uint>();
        var rewards = new List<IReadOnlyList<uint>>();

        foreach (var request in requests)
        {
            if (!TryParseGdpl(request.GirlGdpl, 4, out var girl) ||
                !TryParseGdpl(request.PieceGdpln, 5, out var pieces) ||
                girl[0] != (uint)ItemTypeEnum.TYPE_CARD ||
                pieces[0] != (uint)ItemTypeEnum.TYPE_SUPPLIES || pieces[1] != 4 || pieces[4] == 0)
            {
                await SendBadParam(connection);
                return;
            }

            var cardCfg = GameData.CardData.Values.FirstOrDefault(x =>
                x.Genre == girl[0] && x.Detail == girl[1] && x.Particular == girl[2] && x.Level == girl[3]);
            var character = player.CharacterManager.GetCharacterGDPL(
                ItemTypeEnum.TYPE_CARD, (int)girl[1], (int)girl[2]);
            if (cardCfg == null || character == null || character.Break < 4 ||
                !cardCfg.Pieces.Any(x => x.Count >= 4 &&
                    x[0] == pieces[0] && x[1] == pieces[1] && x[2] == pieces[2] && x[3] == pieces[3]))
            {
                await SendBadParam(connection);
                return;
            }

            var templateId = GameResourceTemplateId.FromGdpl(pieces[0], pieces[1], pieces[2], pieces[3]);
            consumes[templateId] = checked(consumes.GetValueOrDefault(templateId) + pieces[4]);

            // item/pieces_convert.json: colour 5 produces the purple extract;
            // colours 3/4 produce the blue extract, ten for each fragment.
            var extractLevel = cardCfg.Color == 5 ? 2u : 1u;
            rewards.Add([5, 19, 1, extractLevel, checked(pieces[4] * 10)]);
        }

        foreach (var (templateId, count) in consumes)
        {
            var item = player.InventoryManager.InventoryData.Items.Values
                .FirstOrDefault(x => x.TemplateId == templateId);
            if (item == null || item.ItemCount < count)
            {
                await SendBadParam(connection);
                return;
            }
        }

        var sync = new NtfSyncPlayer();
        foreach (var (templateId, count) in consumes)
            RewardGrant.TryConsumeItem(player, sync, templateId, count);
        await RewardGrant.GrantManyAsync(player, sync, rewards);
        RewardGrant.Save(player);

        await CallGSRouter.SendScript(connection, "ShopLogic_ConvertGirlPieces", "{}", sync);
    }

    private static async Task SendBadParam(Connection connection) =>
        await CallGSRouter.SendScript(connection, "ShopLogic_ConvertGirlPieces", "{\"sErr\":\"error.BadParam\"}");

    private static bool TryParseGdpl(string? raw, int expectedLength, out uint[] values)
    {
        values = [];
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var parts = raw.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != expectedLength)
            return false;

        values = new uint[expectedLength];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!uint.TryParse(parts[i], out values[i]) || values[i] == 0)
                return false;
        }
        return true;
    }
}

internal sealed class GirlPiecesConvertParam
{
    [JsonPropertyName("sGirlGDPL")]
    public string? GirlGdpl { get; set; }

    [JsonPropertyName("sPieceGDPLN")]
    public string? PieceGdpln { get; set; }
}
