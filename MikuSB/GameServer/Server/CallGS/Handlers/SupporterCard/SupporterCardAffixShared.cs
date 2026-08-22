using MikuSB.Data;
using MikuSB.Data.Excel;
using MikuSB.Database;
using MikuSB.Database.Inventory;
using MikuSB.Database.Player;
using MikuSB.GameServer.Game.Support;
using MikuSB.Proto;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.SupporterCard;

internal static class SupporterCardAffixShared
{
    public const uint BaseGid = 150;
    public const uint FixedResetSid = 1;

    public static SupportCardExcel? GetExcel(GameSupportCardInfo card)
    {
        return GameData.SupportCardData.FirstOrDefault(x => x.TemplateId == card.TemplateId);
    }

    public static async Task SendResetResponse(Connection connection, NtfSyncPlayer? sync = null)
    {
        await CallGSRouter.SendScript(connection, "SupporterCard_ResetAffix", "null", sync!);
    }

    public static async Task SendSelectResponse(Connection connection, NtfSyncPlayer? sync = null)
    {
        await CallGSRouter.SendScript(connection, "SupporterCard_SelectAffix", "null", sync!);
    }

    public static List<Item> ConsumeCostItems(Connection connection, IEnumerable<IReadOnlyList<uint>> costs)
    {
        var player = connection.Player!;
        var syncItems = new List<Item>();

        foreach (var cost in costs)
        {
            if (cost.Count < 5)
                continue;

            var templateId = GameResourceTemplateId.FromGdpl(cost);
            var item = player.InventoryManager.InventoryData.Items.Values.FirstOrDefault(x => x.TemplateId == templateId);
            if (item == null || item.ItemCount < cost[4])
                throw new InvalidOperationException("support affix material not enough");

            item.ItemCount -= cost[4];
            var proto = item.ToProto();
            if (item.ItemCount == 0)
            {
                player.InventoryManager.InventoryData.Items.Remove(item.UniqueId);
                proto.Count = 0;
            }
            syncItems.Add(proto);
        }

        return syncItems;
    }

    public static bool HasEnoughItems(Connection connection, IEnumerable<IReadOnlyList<uint>> costs)
    {
        var items = connection.Player!.InventoryManager.InventoryData.Items.Values;
        return costs.All(cost =>
        {
            if (cost.Count < 5)
                return false;

            var templateId = GameResourceTemplateId.FromGdpl(cost);
            var item = items.FirstOrDefault(x => x.TemplateId == templateId);
            return item != null && item.ItemCount >= cost[4];
        });
    }

    public static PlayerAttr GetOrCreateAttr(PlayerGameData data, uint gid, uint sid)
    {
        var attr = data.Attrs.FirstOrDefault(x => x.Gid == gid && x.Sid == sid);
        if (attr != null)
            return attr;

        attr = new PlayerAttr { Gid = gid, Sid = sid, Val = 0 };
        data.Attrs.Add(attr);
        return attr;
    }

    public static void SetAttr(Connection connection, NtfSyncPlayer sync, uint gid, uint sid, uint value)
    {
        var player = connection.Player!;
        var attr = GetOrCreateAttr(player.Data, gid, sid);
        attr.Val = value;
        sync.Custom[player.ToPackedAttrKey(gid, sid)] = value;
        sync.Custom[player.ToShiftedAttrKey(gid, sid)] = value;
    }

    public static IEnumerable<uint> GetActiveAffixIds(GameSupportCardInfo card, params int[] ignoreSlots)
    {
        var ignored = ignoreSlots.ToHashSet();
        for (var slot = 1; slot <= SupportAffixStateService.ActiveThirdAffixSlot; slot++)
        {
            if (ignored.Contains(slot))
                continue;

            var (affixId, _) = SupportAffixStateService.GetAffix(card, slot);
            if (affixId > 0)
                yield return affixId;
        }
    }

    public static void Save(Connection connection)
    {
        var player = connection.Player!;
        DatabaseHelper.SaveDatabaseType(player.InventoryManager.InventoryData);
        DatabaseHelper.SaveDatabaseType(player.Data);
    }
}

internal sealed class SupporterCardIdParam
{
    [JsonPropertyName("Id")]
    public int SupportCardUid { get; set; }
}

internal sealed class SupporterCardSelectParam
{
    [JsonPropertyName("Id")]
    public int SupportCardUid { get; set; }

    [JsonPropertyName("SelectNew")]
    public bool SelectNew { get; set; }
}

internal sealed class SupporterCardResetInitialParam
{
    [JsonPropertyName("Id")]
    public int SupportCardUid { get; set; }

    [JsonPropertyName("Index")]
    public int Index { get; set; }

    [JsonPropertyName("FixedId")]
    public uint FixedId { get; set; }
}

internal sealed class SupporterCardSelectInitialParam
{
    [JsonPropertyName("Id")]
    public int SupportCardUid { get; set; }

    [JsonPropertyName("Index")]
    public int Index { get; set; }

    [JsonPropertyName("SelectNew")]
    public bool SelectNew { get; set; }
}
