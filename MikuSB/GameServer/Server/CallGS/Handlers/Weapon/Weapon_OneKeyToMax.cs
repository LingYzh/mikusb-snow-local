using MikuSB.Data;
using MikuSB.Database;
using MikuSB.Database.Inventory;
using MikuSB.GameServer.Game.Weapon;
using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Weapon;

[CallGSApi("Weapon_OneKeyToMax")]
public class Weapon_OneKeyToMax : ICallGSHandler
{
    private const uint MaxBreak = 6;

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var player = connection.Player!;
        var req = JsonSerializer.Deserialize<OneKeyToMaxParam>(param);
        if (req == null || req.Id <= 0 || req.TbBreakUpgradeMat == null || req.TbBreakUpgradeMat.Count == 0)
        {
            await CallGSRouter.SendScript(connection, "Weapon_OneKeyToMax", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        var weapon = player.InventoryManager.GetWeaponItem((uint)req.Id);
        if (weapon == null)
        {
            await CallGSRouter.SendScript(connection, "Weapon_OneKeyToMax", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        var config = WeaponUpgradeConfig.Load();
        if (!config.TryGetWeaponTemplate(weapon.TemplateId, out var targetTemplate))
        {
            await CallGSRouter.SendScript(connection, "Weapon_OneKeyToMax", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        var inventory = player.InventoryManager.InventoryData;
        var equippedWeaponIds = player.CharacterManager.CharacterData.Characters
            .Select(x => x.WeaponUniqueId)
            .Where(x => x != 0)
            .ToHashSet();

        // Validate all materials upfront before making any changes
        foreach (var stage in req.TbBreakUpgradeMat)
        {
            if (stage == null || stage.Count < 3) continue;
            var matList = stage[1].Deserialize<List<List<int>>>();
            if (matList == null) continue;

            foreach (var entry in matList)
            {
                if (entry == null || entry.Count < 2) continue;
                var itemId = (uint)Math.Max(0, entry[0]);
                var count = (uint)Math.Max(0, entry[1]);
                if (itemId == 0 || count == 0) continue;

                if (itemId == weapon.UniqueId)
                {
                    await CallGSRouter.SendScript(connection, "Weapon_OneKeyToMax", "{\"sErr\":\"tip.material_not_enough\"}");
                    return;
                }

                var material = FindInventoryItem(inventory, itemId);
                if (material == null || material.ItemCount < count)
                {
                    await CallGSRouter.SendScript(connection, "Weapon_OneKeyToMax", "{\"sErr\":\"tip.material_not_enough\"}");
                    return;
                }

                if (material is GameWeaponInfo materialWeapon &&
                    (materialWeapon.EquipAvatarId != 0 || equippedWeaponIds.Contains(materialWeapon.UniqueId)))
                {
                    await CallGSRouter.SendScript(connection, "Weapon_OneKeyToMax", "{\"sErr\":\"tip.material_not_enough\"}");
                    return;
                }
            }
        }

        var syncItems = new List<Item>();
        var weaponLevel = weapon.Level == 0 ? 1u : weapon.Level;
        weapon.Level = weaponLevel;

        // Process each break stage
        foreach (var stage in req.TbBreakUpgradeMat)
        {
            if (stage == null || stage.Count < 3) continue;

            var matList = stage[1].Deserialize<List<List<int>>>();
            var doBreak = stage[2].GetInt32() == 1;

            if (matList != null && matList.Count > 0)
            {
                // Aggregate materials, skip duplicates
                var materialsUsed = new Dictionary<uint, uint>();
                foreach (var entry in matList)
                {
                    if (entry == null || entry.Count < 2) continue;
                    var itemId = (uint)Math.Max(0, entry[0]);
                    var count = (uint)Math.Max(0, entry[1]);
                    if (itemId == 0 || count == 0) continue;
                    materialsUsed[itemId] = materialsUsed.GetValueOrDefault(itemId) + count;
                }

                ulong totalExp = 0;
                foreach (var (itemId, count) in materialsUsed)
                {
                    var material = FindInventoryItem(inventory, itemId)!;
                    if (config.TryGetMaterialGain(material, out var gainExp))
                        totalExp += gainExp * count;
                }

                // Consume materials
                foreach (var (itemId, count) in materialsUsed)
                {
                    var material = FindInventoryItem(inventory, itemId)!;
                    material.ItemCount -= count;
                    if (material.ItemCount == 0)
                    {
                        RemoveInventoryItem(inventory, itemId);
                        var proto = material.ToProto();
                        proto.Count = 0;
                        syncItems.Add(proto);
                    }
                    else
                    {
                        syncItems.Add(material.ToProto());
                    }
                }

                // Apply exp to weapon
                if (totalExp > 0)
                {
                    var maxLevel = config.GetWeaponMaxLevel(targetTemplate.BreakLimitId, weapon.Break);
                    var (newLevel, newExp) = config.ApplyWeaponExp(weapon.Level, weapon.Exp, totalExp, targetTemplate.Color, maxLevel);
                    weapon.Level = newLevel;
                    weapon.Exp = newExp;
                }
            }

            // Perform break
            if (doBreak && weapon.Break < MaxBreak)
                weapon.Break++;
        }

        var unlockedPart = await WeaponPartUnlockService.GrantForWeaponAsync(player, weapon);
        if (unlockedPart != null)
            syncItems.Add(unlockedPart.ToProto());
        syncItems.Add(weapon.ToProto());
        DatabaseHelper.SaveDatabaseType(inventory);

        var finalMaxLevel = config.GetWeaponMaxLevel(targetTemplate.BreakLimitId, weapon.Break);
        var bMaxUnlock = finalMaxLevel > 0 && weapon.Level >= finalMaxLevel;

        var sync = new NtfSyncPlayer();
        sync.Items.AddRange(syncItems);

        await CallGSRouter.SendScript(connection, "Weapon_OneKeyToMax",
            $"{{\"bMaxUnLock\":{(bMaxUnlock ? "true" : "false")}}}", sync);
    }

    private static BaseGameItemInfo? FindInventoryItem(InventoryData inventory, uint itemId)
    {
        if (inventory.Weapons.TryGetValue(itemId, out var weapon)) return weapon;
        if (inventory.Skins.TryGetValue(itemId, out var skin)) return skin;
        if (inventory.Items.TryGetValue(itemId, out var item)) return item;
        return null;
    }

    private static void RemoveInventoryItem(InventoryData inventory, uint itemId)
    {
        inventory.Weapons.Remove(itemId);
        inventory.Skins.Remove(itemId);
        inventory.Items.Remove(itemId);
    }
}

internal sealed class OneKeyToMaxParam
{
    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("tbBreakUpgradeMat")]
    public List<List<System.Text.Json.JsonElement>> TbBreakUpgradeMat { get; set; } = [];
}
