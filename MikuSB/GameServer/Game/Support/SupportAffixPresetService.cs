using MikuSB.Data;
using MikuSB.Data.Excel;
using MikuSB.Database.Inventory;
using Newtonsoft.Json.Linq;

namespace MikuSB.GameServer.Game.Support;

public static class SupportAffixPresetService
{
    private const string PrimaryStat = "SkillMastery";
    // The three name106-name108 cards omit this otherwise universal stat from
    // their first-slot pool.  Affix 21 is the locally defined, maximum-tier
    // Alignment Index row and the client can render it for every support card.
    private const uint UniversalSkillMasteryAffixId = 21;

    // The local static data only carries legal affix pools; it has no
    // per-card "recommended affix" mapping.  Prefer the offline fallback the
    // player requested and keep the remaining entries as legal fallbacks for
    // specialised pools that cannot roll any of the three primary stats.
    private static readonly string[] StatPriority =
    [
        PrimaryStat,
        "AttackPer",
        "CriticalDamageAddtion",
        "CriticalValue",
        "SkillMastery",
        "CharacterEnergyEfficiency",
        "NormalEnergySpeedPer",
        "SkillCDQuick",
        "Command",
        "EntityBulletDamageBonus",
        "SuperpowersDamageBonus",
        "FireDamageBonus",
        "ThunderDamageBonus",
        "IceDamageBonus",
        "ShieldDynamicAddtion",
        "HealBonus",
        "HealthPer",
        "DefencePer"
    ];

    public static void ApplyBestLegalAffixes(GameSupportCardInfo card, SupportCardExcel template)
    {
        for (var slot = 1; slot <= SupportAffixStateService.MaxLogicalSlots; slot++)
            SupportAffixStateService.ClearAffix(card, slot);
        card.AffixId = 0;

        var selected = new HashSet<uint>();
        // Never add an affix slot before that support card would normally
        // unlock it.  Newly issued offline cards are already at MaxLevel, but
        // this also makes the rebalance safe for a pre-existing low-level card.
        var unlockedAffixCount = card.Level >= template.MaxLevel
            ? template.TotalAffixCount
            : template.InitialAffixCount;
        var affixCount = Math.Min(unlockedAffixCount, template.AffixPool.Count);
        for (var slot = 1; slot <= affixCount; slot++)
        {
            if (!GameData.SupportAffixPoolData.TryGetValue(template.AffixPool[slot - 1], out var pool))
                continue;

            var candidates = pool.Groups
                .SelectMany(x => x.Affixs)
                .Select(x => (uint)x)
                .Distinct()
                .Where(x => GameData.SupportAffixData.ContainsKey((int)x))
                .ToList();

            // All current cards may use Alignment Index.  Three fully valid
            // card rows omit it from their first-slot source pool, so inject
            // the highest local definition for that first slot only.
            var affixId = slot == 1 &&
                          !candidates.Any(x => GetStat(x) == PrimaryStat) &&
                          GameData.SupportAffixData.ContainsKey((int)UniversalSkillMasteryAffixId)
                ? UniversalSkillMasteryAffixId
                : candidates
                .OrderBy(x => selected.Contains(x))
                .ThenBy(GetStatPriority)
                .ThenBy(x => x)
                .FirstOrDefault();
            if (affixId == 0)
                continue;

            var tierCount = GameData.SupportAffixData[(int)affixId].TierCount;
            SupportAffixStateService.SetAffix(card, slot, affixId, (uint)Math.Max(1, tierCount));
            selected.Add(affixId);
        }
    }

    private static int GetStatPriority(uint affixId)
    {
        if (!GameData.SupportAffixData.TryGetValue((int)affixId, out var affix))
            return int.MaxValue;

        var stat = GetStat(affix);
        var priority = Array.IndexOf(StatPriority, stat ?? string.Empty);
        return priority >= 0 ? priority : int.MaxValue - 1;
    }

    private static string? GetStat(uint affixId) =>
        GameData.SupportAffixData.TryGetValue((int)affixId, out var affix)
            ? GetStat(affix)
            : null;

    private static string? GetStat(SupportAffixExcel affix) =>
        affix.ExtraData
            .Where(x => x.Key is not "ID" and not "Sift" and not "Comment" &&
                        x.Value is JObject value && value.HasValues)
            .Select(x => x.Key)
            .FirstOrDefault();
}
