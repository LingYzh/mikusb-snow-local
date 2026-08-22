using MikuSB.Data;
using MikuSB.Database.Inventory;
using MikuSB.Enums.Item;
using MikuSB.GameServer.Game.Player;

namespace MikuSB.GameServer.Game.Weapon;

public static class WeaponPartUnlockService
{
    private const uint UnlockLevel = 50;

    public static HashSet<ulong> GetLevelUnlockPartTemplateIds()
    {
        var templateIds = new HashSet<ulong>();
        foreach (var weapon in GameData.WeaponData.Values)
        {
            if (!weapon.TryGetWeaponPartsAward(out var genre, out var detail, out var particular, out var level) ||
                genre != (uint)ItemTypeEnum.TYPE_WEAPON_PART)
                continue;

            templateIds.Add(GameResourceTemplateId.FromGdpl(genre, detail, particular, level));
        }

        return templateIds;
    }

    public static async ValueTask<BaseGameItemInfo?> GrantForWeaponAsync(
        PlayerInstance player,
        GameWeaponInfo weapon)
    {
        if (weapon.Level < UnlockLevel)
            return null;

        var weaponTemplate = GameData.WeaponData.Values.FirstOrDefault(x =>
            GameResourceTemplateId.FromGdpl(x.Genre, x.Detail, x.Particular, x.Level) == weapon.TemplateId);
        if (weaponTemplate == null ||
            !weaponTemplate.TryGetWeaponPartsAward(out var genre, out var detail, out var particular, out var level) ||
            genre != (uint)ItemTypeEnum.TYPE_WEAPON_PART)
            return null;

        return await player.InventoryManager.AddWeaponPartItem(
            ItemTypeEnum.TYPE_WEAPON_PART,
            detail,
            particular,
            level,
            sendPacket: false);
    }

    public static async ValueTask<List<BaseGameItemInfo>> GrantForEligibleWeaponsAsync(PlayerInstance player)
    {
        var grants = new List<BaseGameItemInfo>();
        foreach (var weapon in player.InventoryManager.InventoryData.Weapons.Values)
        {
            var grant = await GrantForWeaponAsync(player, weapon);
            if (grant != null)
                grants.Add(grant);
        }

        return grants;
    }
}
