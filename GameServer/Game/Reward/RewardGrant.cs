using MikuSB.Data;
using MikuSB.Data.Excel;
using MikuSB.Database;
using MikuSB.Database.Inventory;
using MikuSB.Database.Player;
using MikuSB.Enums.Item;
using MikuSB.GameServer.Game.Player;
using MikuSB.Proto;
using System.Text.Json.Nodes;

namespace MikuSB.GameServer.Game.Reward;

public static class RewardGrant
{
    public const uint CashGroupId = 1;

    public static async Task GrantManyAsync(PlayerInstance player, NtfSyncPlayer sync, IEnumerable<IReadOnlyList<uint>> rewards)
    {
        foreach (var reward in rewards)
            await GrantAsync(player, sync, reward);
    }

    public static async Task GrantAsync(PlayerInstance player, NtfSyncPlayer sync, IReadOnlyList<uint> reward)
    {
        if (reward.Count < 4)
            return;

        var itemType = (ItemTypeEnum)reward[0];
        var detail = reward[1];
        var particular = reward[2];
        var level = reward[3];
        var count = reward.Count >= 5 ? Math.Max(1u, reward[4]) : 1u;

        switch (itemType)
        {
            case ItemTypeEnum.TYPE_CARD:
                for (var i = 0u; i < count; i++)
                {
                    var character = await player.CharacterManager.AddCharacter(itemType, detail, particular, level, sendPacket: false);
                    if (character != null)
                        sync.Items.Add(character.ToProto());
                }
                break;
            case ItemTypeEnum.TYPE_WEAPON:
                for (var i = 0u; i < count; i++)
                {
                    var weapon = await player.InventoryManager.AddWeaponItem(itemType, detail, particular, level, sendPacket: false);
                    if (weapon != null)
                        sync.Items.Add(weapon.ToProto());
                }
                break;
            case ItemTypeEnum.TYPE_SUPPORT:
                for (var i = 0u; i < count; i++)
                {
                    var support = await player.InventoryManager.AddSupportCardItem(detail, particular, level, sendPacket: false);
                    if (support != null)
                        sync.Items.Add(support.ToProto());
                }
                break;
            case ItemTypeEnum.TYPE_SUPPLIES:
            {
                var templateId = (uint)GameResourceTemplateId.FromGdpl(reward[0], detail, particular, level);
                if (!GameData.SuppliesData.TryGetValue(templateId, out var supplies))
                    break;
                var item = await player.InventoryManager.AddSuppliesItem(supplies, count, sendPacket: false);
                if (item != null)
                    sync.Items.Add(item.ToProto());
                break;
            }
            case ItemTypeEnum.TYPE_USEABLE:
            {
                if (!TryGrantCashBox(player, sync, detail, particular, level, count))
                {
                    var item = AddOtherItem(player.InventoryManager.InventoryData, reward[0], detail, particular, level, count);
                    if (item != null)
                        sync.Items.Add(item.ToProto());
                }
                break;
            }
            case ItemTypeEnum.TYPE_WEAPON_PART:
                for (var i = 0u; i < count; i++)
                {
                    var item = await player.InventoryManager.AddWeaponPartItem(itemType, detail, particular, level, sendPacket: false);
                    if (item != null)
                        sync.Items.Add(item.ToProto());
                }
                break;
            case ItemTypeEnum.TYPE_CARD_SKIN:
                for (var i = 0u; i < count; i++)
                {
                    var item = await player.InventoryManager.AddSkinItem(itemType, detail, particular, level, sendPacket: false);
                    if (item != null)
                        sync.Items.Add(item.ToProto());
                }
                break;
            case ItemTypeEnum.TYPE_HOUSE:
                for (var i = 0u; i < count; i++)
                {
                    var item = await player.InventoryManager.AddHouseFurnitureItem(itemType, detail, particular, level, sendPacket: false);
                    if (item != null)
                        sync.Items.Add(item.ToProto());
                }
                break;
            case ItemTypeEnum.TYPE_PROFILE:
            case ItemTypeEnum.TYPE_FRAME:
            case ItemTypeEnum.TYPE_BADGE:
            case ItemTypeEnum.TYPE_COVER:
            case ItemTypeEnum.TYPE_NAMECARD:
            case ItemTypeEnum.TYPE_EXPRESSION:
            case ItemTypeEnum.TYPE_BUBBLE:
            case ItemTypeEnum.TYPE_ANALYST:
                for (var i = 0u; i < count; i++)
                {
                    var item = await player.InventoryManager.AddProfileItem(itemType, detail, particular, level, sendPacket: false);
                    if (item != null)
                        sync.Items.Add(item.ToProto());
                }
                break;
            case ItemTypeEnum.TYPE_WEAPON_SKIN:
                for (var i = 0u; i < count; i++)
                {
                    var item = await player.InventoryManager.AddWeaponSkinItem(itemType, detail, particular, level, sendPacket: false);
                    if (item != null)
                        sync.Items.Add(item.ToProto());
                }
                break;
            case ItemTypeEnum.TYPE_MANIFESTATION:
                for (var i = 0u; i < count; i++)
                {
                    var item = await player.InventoryManager.AddManifestationItem(itemType, detail, particular, level, sendPacket: false);
                    if (item != null)
                        sync.Items.Add(item.ToProto());
                }
                break;
            case ItemTypeEnum.TYPE_CARD_SKIN_PART:
                for (var i = 0u; i < count; i++)
                {
                    var item = await player.InventoryManager.AddSkinPartItem(itemType, detail, particular, level, sendPacket: false);
                    if (item != null)
                        sync.Items.Add(item.ToProto());
                }
                break;
            case ItemTypeEnum.TYPE_AR:
                for (var i = 0u; i < count; i++)
                {
                    var item = await player.InventoryManager.AddArItem(itemType, detail, particular, level, sendPacket: false);
                    if (item != null)
                        sync.Items.Add(item.ToProto());
                }
                break;
            case ItemTypeEnum.TYPE_CALL:
                for (var i = 0u; i < count; i++)
                {
                    var item = await player.InventoryManager.AddCallItem(itemType, detail, particular, level, sendPacket: false);
                    if (item != null)
                        sync.Items.Add(item.ToProto());
                }
                break;
        }
    }

    public static bool TryConsume(PlayerInstance player, NtfSyncPlayer sync, IReadOnlyList<uint> cost)
    {
        if (cost.Count < 4)
            return true;

        var count = cost.Count >= 5 ? Math.Max(1u, cost[4]) : 1u;
        var genre = cost[0];
        if (genre == (uint)ItemTypeEnum.TYPE_USEABLE && TryConsumeCash(player, sync, cost[1], count))
            return true;

        var templateId = (uint)GameResourceTemplateId.FromGdpl(cost[0], cost[1], cost[2], cost[3]);
        var item = player.InventoryManager.InventoryData.Items.Values.FirstOrDefault(x => x.TemplateId == templateId);
        if (item == null || item.ItemCount < count)
            return false;

        item.ItemCount -= count;
        if (item.ItemCount == 0)
            player.InventoryManager.InventoryData.Items.Remove(item.UniqueId);
        sync.Items.Add(item.ToProto());
        return true;
    }

    public static bool TryConsumeCash(PlayerInstance player, NtfSyncPlayer sync, uint moneyId, uint amount)
    {
        if (moneyId == 0 || amount == 0)
            return true;

        var sid = moneyId * 2 + 1;
        var attr = GetOrCreateAttr(player, CashGroupId, sid);
        if (attr.Val < amount)
            return false;

        attr.Val -= amount;
        SyncAttr(player, sync, attr);
        if (moneyId == 1)
        {
            foreach (var (key, value) in player.BuildMoneySync())
                sync.Money[key] = value;
        }

        return true;
    }

    public static JsonArray ToAwardArray(IEnumerable<IReadOnlyList<uint>> rewards)
    {
        var array = new JsonArray();
        foreach (var reward in rewards)
        {
            if (reward.Count < 4)
                continue;
            var row = new JsonArray();
            var count = Math.Min(5, reward.Count);
            for (var i = 0; i < count; i++)
                row.Add((int)reward[i]);
            if (count < 5)
                row.Add(1);
            array.Add(row);
        }

        return array;
    }

    public static void Save(PlayerInstance player)
    {
        DatabaseHelper.SaveDatabaseType(player.Data);
        DatabaseHelper.SaveDatabaseType(player.InventoryManager.InventoryData);
        DatabaseHelper.SaveDatabaseType(player.CharacterManager.CharacterData);
    }

    public static PlayerAttr GetOrCreateAttr(PlayerInstance player, uint gid, uint sid)
    {
        var attr = player.Data.Attrs.FirstOrDefault(x => x.Gid == gid && x.Sid == sid);
        if (attr != null)
            return attr;

        attr = new PlayerAttr { Gid = gid, Sid = sid };
        player.Data.Attrs.Add(attr);
        return attr;
    }

    public static void SyncAttr(PlayerInstance player, NtfSyncPlayer sync, PlayerAttr attr)
    {
        sync.Custom[player.ToPackedAttrKey(attr.Gid, attr.Sid)] = attr.Val;
        sync.Custom[player.ToShiftedAttrKey(attr.Gid, attr.Sid)] = attr.Val;
    }

    private static bool TryGrantCashBox(PlayerInstance player, NtfSyncPlayer sync, uint detail, uint particular, uint level, uint count)
    {
        var templateId = (uint)GameResourceTemplateId.FromGdpl((uint)ItemTypeEnum.TYPE_USEABLE, detail, particular, level);
        if (!GameData.OtherItemData.TryGetValue(templateId, out var otherItem))
            return false;

        uint moneyType = otherItem.LuaType switch
        {
            "money_box" => 1,
            "gold_box" => 2,
            "silver_box" => 3,
            "vigor_box" => 4,
            _ => 0
        };

        if (moneyType == 0 || otherItem.Param1 == 0)
            return false;

        var amount = checked(otherItem.Param1 * count);
        var sid = moneyType * 2 + 1;
        var attr = GetOrCreateAttr(player, CashGroupId, sid);
        attr.Val += amount;
        SyncAttr(player, sync, attr);
        if (moneyType == 1)
        {
            foreach (var (key, value) in player.BuildMoneySync())
                sync.Money[key] = value;
        }

        return true;
    }

    private static BaseGameItemInfo? AddOtherItem(InventoryData inventory, uint genre, uint detail, uint particular, uint level, uint count)
    {
        var templateId = (uint)GameResourceTemplateId.FromGdpl(genre, detail, particular, level);
        if (!GameData.OtherItemData.TryGetValue(templateId, out var otherItem))
            return null;

        var maxCount = otherItem.GMnum > 0 ? otherItem.GMnum : 99999u;
        var existing = inventory.Items.Values.FirstOrDefault(x => x.TemplateId == templateId);
        if (existing != null)
        {
            existing.ItemCount = Math.Min(existing.ItemCount + count, maxCount);
            return existing;
        }

        var item = new BaseGameItemInfo
        {
            TemplateId = templateId,
            UniqueId = inventory.NextUniqueUid++,
            ItemType = ItemTypeEnum.TYPE_USEABLE,
            ItemCount = Math.Min(count, maxCount)
        };
        inventory.Items[item.UniqueId] = item;
        return item;
    }
}
