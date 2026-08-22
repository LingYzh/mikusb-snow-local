using MikuSB.Data;
using MikuSB.Database;
using MikuSB.Database.Inventory;
using MikuSB.Database.Player;
using MikuSB.Enums.Item;
using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Girl;

[CallGSApi("GirlCard_UpdateLevel")]
public class GirlCard_UpdateLevel : ICallGSHandler
{
    private const uint CashGroupId = 1;
    private const uint SilverMoneyType = 3;
    private const uint SilverSid = SilverMoneyType * 2 + 1;
    private const uint RoleMaxLevel = 80;

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var player = connection.Player!;
        var req = JsonSerializer.Deserialize<GirlCardUpdateLevelParam>(param);
        if (req == null || req.Id == 0 || req.Materials == null || req.Materials.Count == 0)
        {
            await CallGSRouter.SendScript(connection, "GirlCard_UpdateLevel", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        var card = player.CharacterManager.GetCharacterByGUID((uint)req.Id);
        if (card == null)
        {
            await CallGSRouter.SendScript(connection, "GirlCard_UpdateLevel", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        var cardTemplate = GameData.CardData.Values.FirstOrDefault(x =>
            GameResourceTemplateId.FromGdpl(x.Genre, x.Detail, x.Particular, x.Level) == card.TemplateId);
        if (cardTemplate == null)
        {
            await CallGSRouter.SendScript(connection, "GirlCard_UpdateLevel", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        var levelCap = GetCardLevelCap(player.Data.Level, cardTemplate.LevelLimitID);
        if (levelCap == 0)
        {
            levelCap = card.Level;
        }

        if (card.Level >= RoleMaxLevel)
        {
            await CallGSRouter.SendScript(connection, "GirlCard_UpdateLevel", "{\"sErr\":\"tip.card_max_level\"}");
            return;
        }

        var requestedMaterials = new Dictionary<uint, uint>();
        foreach (var row in req.Materials)
        {
            if (row == null || row.Id == 0 || row.Num == 0)
                continue;

            requestedMaterials[(uint)row.Id] = requestedMaterials.GetValueOrDefault((uint)row.Id) + row.Num;
        }

        if (requestedMaterials.Count == 0)
        {
            await CallGSRouter.SendScript(connection, "GirlCard_UpdateLevel", "{\"sErr\":\"tip.material_not_enough\"}");
            return;
        }

        ulong totalExp = 0;
        ulong totalSilverCost = 0;
        foreach (var (itemId, count) in requestedMaterials)
        {
            var item = player.InventoryManager.GetNormalItem(itemId);
            if (item == null || item.ItemCount < count)
            {
                await CallGSRouter.SendScript(connection, "GirlCard_UpdateLevel", "{\"sErr\":\"tip.material_not_enough\"}");
                return;
            }

            if (!GameData.SuppliesData.TryGetValue(item.TemplateId, out var supplies) || supplies.ProvideExp == 0)
            {
                await CallGSRouter.SendScript(connection, "GirlCard_UpdateLevel", "{\"sErr\":\"error.BadParam\"}");
                return;
            }

            totalExp += (ulong)supplies.ProvideExp * count;
            totalSilverCost += (ulong)supplies.ConsumeGold * count;
        }

        var silverAttr = GetOrCreateAttr(player.Data, CashGroupId, SilverSid);
        if ((ulong)silverAttr.Val < totalSilverCost)
        {
            await CallGSRouter.SendScript(connection, "GirlCard_UpdateLevel", "{\"sErr\":\"tip.material_not_enough\"}");
            return;
        }

        var syncItems = new List<Item>();
        foreach (var (itemId, count) in requestedMaterials)
        {
            var item = player.InventoryManager.GetNormalItem(itemId)!;
            item.ItemCount -= count;

            if (item.ItemCount == 0)
            {
                player.InventoryManager.InventoryData.Items.Remove(item.UniqueId);
                syncItems.Add(BuildRemovedProto(item));
            }
            else
            {
                syncItems.Add(item.ToProto());
            }
        }

        silverAttr.Val -= checked((uint)totalSilverCost);

        var (newLevel, newExp) = ApplyCardExp(card.Level, card.Exp, totalExp, levelCap);
        card.Level = newLevel;
        card.Exp = checked((int)newExp);
        syncItems.Add(card.ToProto());

        DatabaseHelper.SaveDatabaseType(player.InventoryManager.InventoryData);
        DatabaseHelper.SaveDatabaseType(player.CharacterManager.CharacterData);
        DatabaseHelper.SaveDatabaseType(player.Data);

        var sync = new NtfSyncPlayer();
        sync.Items.AddRange(syncItems);
        sync.Custom[player.ToPackedAttrKey(CashGroupId, SilverSid)] = silverAttr.Val;
        sync.Custom[player.ToShiftedAttrKey(CashGroupId, SilverSid)] = silverAttr.Val;

        await CallGSRouter.SendScript(connection, "GirlCard_UpdateLevel", "null", sync);
    }

    private static PlayerAttr GetOrCreateAttr(PlayerGameData data, uint gid, uint sid)
    {
        var attr = data.Attrs.FirstOrDefault(x => x.Gid == gid && x.Sid == sid);
        if (attr != null)
            return attr;

        attr = new PlayerAttr
        {
            Gid = gid,
            Sid = sid,
            Val = 0
        };
        data.Attrs.Add(attr);
        return attr;
    }

    private static Item BuildRemovedProto(BaseGameItemInfo item)
    {
        var proto = item.ToProto();
        proto.Count = 0;
        return proto;
    }

    private static uint GetCardLevelCap(uint playerLevel, int levelLimitId)
    {
        var limits = LoadCardLevelLimit(levelLimitId);
        if (limits.Count == 0)
            return 0;

        uint nearestAccountLevel = 0;
        uint nearestCardLevel = 0;

        foreach (var (accountLevel, cardLevel) in limits)
        {
            if (accountLevel < playerLevel)
            {
                nearestAccountLevel = accountLevel;
                nearestCardLevel = cardLevel;
                continue;
            }

            if (accountLevel == playerLevel)
                return Math.Min(cardLevel, RoleMaxLevel);

            var distance = accountLevel - nearestAccountLevel;
            if (distance == 0)
                return Math.Min(cardLevel, RoleMaxLevel);

            var percent = (playerLevel - nearestAccountLevel) / (double)distance;
            var interpolated = (uint)Math.Floor(nearestCardLevel + ((cardLevel - nearestCardLevel) * percent));
            return Math.Min(interpolated, RoleMaxLevel);
        }

        return Math.Min(nearestCardLevel, RoleMaxLevel);
    }

    private static List<(uint AccountLevel, uint CardLevel)> LoadCardLevelLimit(int levelLimitId)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Resources",
            "item",
            "level_limit.json");

        if (!File.Exists(path))
            return [];

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var result = new List<(uint AccountLevel, uint CardLevel)>();

        foreach (var row in doc.RootElement.EnumerateArray())
        {
            if (!row.TryGetProperty("ID", out var idProp) || idProp.GetInt32() != levelLimitId)
                continue;

            if (!row.TryGetProperty("Type", out var typeProp) || typeProp.GetInt32() != 1)
                continue;

            if (!row.TryGetProperty("Limit", out var limitProp) || limitProp.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var property in limitProp.EnumerateObject())
            {
                if (!uint.TryParse(property.Name, out var accountLevel))
                    continue;

                uint cardLevel;
                if (property.Value.ValueKind == JsonValueKind.Number)
                {
                    cardLevel = property.Value.GetUInt32();
                }
                else if (property.Value.ValueKind == JsonValueKind.String &&
                         uint.TryParse(property.Value.GetString(), out var parsed))
                {
                    cardLevel = parsed;
                }
                else
                {
                    continue;
                }

                result.Add((accountLevel, cardLevel));
            }

            break;
        }

        result.Sort((a, b) => a.AccountLevel.CompareTo(b.AccountLevel));
        return result;
    }

    private static (uint Level, ulong Exp) ApplyCardExp(uint level, int currentExp, ulong addedExp, uint levelCap)
    {
        var destLevel = level == 0 ? 1u : level;
        var destExp = (ulong)Math.Max(0, currentExp) + addedExp;

        if (levelCap > 0 && destLevel >= levelCap)
            return (destLevel, destExp);

        while (destLevel < RoleMaxLevel)
        {
            var needExp = GetCardNeedExp(destLevel);
            if (needExp == 0 || destExp < needExp)
                break;

            destExp -= needExp;
            destLevel++;

            if (levelCap > 0 && destLevel >= levelCap)
                return (levelCap, destExp);
        }

        return (destLevel, destExp);
    }

    private static uint GetCardNeedExp(uint currentLevel)
    {
        if (GameData.UpgradeExpData.TryGetValue((int)currentLevel, out var row))
            return row.CardNeedExp;

        return 0;
    }
}

internal sealed class GirlCardUpdateLevelParam
{
    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("tbMaterials")]
    public List<GirlCardLevelMaterial> Materials { get; set; } = [];
}

internal sealed class GirlCardLevelMaterial
{
    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("Num")]
    public uint Num { get; set; }
}
