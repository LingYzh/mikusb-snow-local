using MikuSB.Data;
using MikuSB.GameServer.Game.Player;
using MikuSB.GameServer.Game.Reward;
using MikuSB.Proto;
using MikuSB.Util;
using System.Text.Json.Nodes;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Chapter;

internal static class StageSettlement
{
    private static readonly Logger Logger = new("Stage");
    private static readonly Random Rng = new();
    private const uint RewardClaimGroupId = 201;

    public static async Task<(JsonNode Payload, NtfSyncPlayer Sync)> GrantAsync(
        PlayerInstance player,
        JsonNode? tbParam,
        bool wrapShowAward)
    {
        var sync = new NtfSyncPlayer();
        var awards = new List<List<uint>>();
        var levelId = ReadLevelId(tbParam);
        if (levelId == 0)
        {
            Logger.Warn($"Settlement missing level id: {tbParam?.ToJsonString() ?? "null"}");
            JsonNode empty = wrapShowAward
                ? new JsonObject { ["tbShowAward"] = new JsonArray() }
                : new JsonArray();
            return (empty, sync);
        }

        if (IsExplicitFail(tbParam))
        {
            JsonNode empty = wrapShowAward
                ? new JsonObject { ["tbShowAward"] = new JsonArray() }
                : new JsonArray();
            return (empty, sync);
        }

        var firstClear = !IsRewardClaimed(player, levelId);
        CollectStageAwards(levelId, firstClear, awards);
        awards = MergeAwards(awards);
        await RewardGrant.GrantManyAsync(player, sync, awards);

        if (firstClear)
        {
            var attr = RewardGrant.GetOrCreateAttr(player, RewardClaimGroupId, levelId);
            attr.Val = 1;
            RewardGrant.SyncAttr(player, sync, attr);
        }

        RewardGrant.Save(player);
        Logger.Info($"Settlement level={levelId} first={firstClear} awards={awards.Count}");

        var awardArray = RewardGrant.ToAwardArray(awards);
        if (wrapShowAward)
        {
            var result = new JsonObject { ["tbShowAward"] = awardArray };
            if (tbParam is JsonObject source && source.TryGetPropertyValue("bWaitServer", out var wait))
                result["bWaitServer"] = wait?.DeepClone();
            return (result, sync);
        }

        // Settlement.ShowAwardItems iterates an outer list of award groups.
        // Each group is itself the normal GDPLN award array.  Returning the
        // inner list directly makes it interpret the five GDPLN numbers as
        // separate awards, hence the repeated "2" rows in direct battles.
        return (new JsonArray { awardArray }, sync);
    }

    public static async Task<(JsonArray Awards, NtfSyncPlayer Sync)> GrantMoppingUpAsync(
        PlayerInstance player,
        JsonNode? tbParam,
        uint multiple)
    {
        var sync = new NtfSyncPlayer();
        var levelId = ReadLevelId(tbParam);
        if (levelId == 0)
        {
            Logger.Warn($"Mopping-up missing level id: {tbParam?.ToJsonString() ?? "null"}");
            return (new JsonArray(), sync);
        }

        var allAwards = new List<List<uint>>();
        var results = new JsonArray();
        var actualMultiple = Math.Clamp(multiple, 1u, 20u);
        for (var i = 0u; i < actualMultiple; i++)
        {
            var attemptAwards = new List<List<uint>>();
            CollectStageAwards(levelId, firstClear: false, attemptAwards);
            attemptAwards = MergeAwards(attemptAwards);
            allAwards.AddRange(attemptAwards);

            // MopupResult renders one award-list per sweep.  Returning one
            // flattened list makes it treat each GDPL number as a separate row.
            results.Add(RewardGrant.ToAwardArray(attemptAwards));
        }

        await RewardGrant.GrantManyAsync(player, sync, allAwards);
        RewardGrant.Save(player);
        Logger.Info($"MoppingUp level={levelId} multiple={actualMultiple} awards={allAwards.Count}");
        return (results, sync);
    }

    private static void CollectStageAwards(uint levelId, bool firstClear, List<List<uint>> awards)
    {
        List<uint> firstDrop = [];
        List<uint> baseDrop = [];
        List<uint> randomDrop = [];
        List<List<uint>> showAward = [];
        List<List<uint>> showFirst = [];

        if (GameData.ChapterLevelData.TryGetValue(levelId, out var chapter))
        {
            firstDrop = chapter.FirstDropIds;
            baseDrop = chapter.BaseDropIds;
            randomDrop = chapter.RandomDropIds;
            showAward = chapter.ShowAward;
            showFirst = chapter.ShowFirstAward;
        }
        else if (GameData.DailyLevelData.TryGetValue(levelId, out var daily))
        {
            firstDrop = daily.FirstDropIds;
            baseDrop = daily.BaseDropIds;
            randomDrop = daily.RandomDropIds;
            showAward = daily.ShowAward;
            showFirst = daily.ShowFirstAward;
        }
        else if (GameData.RoleLevelData.TryGetValue(levelId, out var role))
        {
            firstDrop = role.FirstDropIds;
            baseDrop = role.BaseDropIds;
            randomDrop = role.RandomDropIds;
            showAward = role.ShowAward;
            showFirst = role.ShowFirstAward;
        }

        var beforeRegularDrops = awards.Count;
        foreach (var dropId in baseDrop)
            AddRows(awards, RollDrop(dropId));
        foreach (var dropId in randomDrop)
            AddRows(awards, RollDrop(dropId));

        // ShowAward is UI metadata.  The base/random drop tables are the actual
        // rewards and usually contain the same rows, so never grant both.
        if (awards.Count == beforeRegularDrops)
            AddRows(awards, showAward);

        if (firstClear)
        {
            var beforeFirstDrops = awards.Count;
            foreach (var dropId in firstDrop)
                AddRows(awards, RollDrop(dropId));
            if (awards.Count == beforeFirstDrops)
                AddRows(awards, showFirst);
        }
    }

    private static void AddRows(List<List<uint>> dest, IEnumerable<List<uint>> rows)
    {
        foreach (var row in rows)
        {
            if (row.Count >= 4)
                dest.Add(row);
        }
    }

    private static List<List<uint>> RollDrop(uint dropId)
    {
        var result = new List<List<uint>>();
        if (!GameData.DropData.TryGetValue(dropId, out var drop))
            return result;

        foreach (var groupId in drop.GroupIds())
        {
            if (!GameData.DropGroupData.TryGetValue(groupId, out var group))
                continue;
            result.AddRange(group.Roll(Rng));
        }

        return result;
    }

    private static List<List<uint>> MergeAwards(IEnumerable<List<uint>> awards)
    {
        var merged = new Dictionary<(uint Genre, uint Detail, uint Particular, uint Level), uint>();
        foreach (var row in awards)
        {
            if (row.Count < 4)
                continue;

            var key = (row[0], row[1], row[2], row[3]);
            var count = row.Count >= 5 ? Math.Max(1u, row[4]) : 1u;
            merged[key] = merged.GetValueOrDefault(key) + count;
        }

        return merged.Select(x => new List<uint>
        {
            x.Key.Genre,
            x.Key.Detail,
            x.Key.Particular,
            x.Key.Level,
            x.Value
        }).ToList();
    }

    private static bool IsRewardClaimed(PlayerInstance player, uint levelId) =>
        player.Data.Attrs.Any(x => x.Gid == RewardClaimGroupId && x.Sid == levelId && x.Val > 0);

    private static bool IsExplicitFail(JsonNode? tbParam)
    {
        if (tbParam is not JsonObject obj)
            return false;
        if (obj.TryGetPropertyValue("bWin", out var win) && win is JsonValue winVal && winVal.TryGetValue<bool>(out var bWin))
            return !bWin;
        if (obj.TryGetPropertyValue("bSuccess", out var success) && success is JsonValue successVal && successVal.TryGetValue<bool>(out var bSuccess))
            return !bSuccess;
        return false;
    }

    private static uint ReadLevelId(JsonNode? tbParam)
    {
        if (tbParam is JsonValue direct)
            return ShopLikeUInt(direct);
        if (tbParam is not JsonObject obj)
            return 0;

        foreach (var key in new[] { "nId", "nID", "nLevelID", "nLevelId", "nLevel", "nStageId", "nStageID" })
        {
            if (obj.TryGetPropertyValue(key, out var node))
            {
                var id = ShopLikeUInt(node);
                if (id > 0)
                    return id;
            }
        }

        return 0;
    }

    private static uint ShopLikeUInt(JsonNode? node)
    {
        if (node is not JsonValue value)
            return 0;
        if (value.TryGetValue<uint>(out var u))
            return u;
        if (value.TryGetValue<int>(out var i) && i > 0)
            return (uint)i;
        if (value.TryGetValue<long>(out var l) && l > 0)
            return (uint)l;
        if (value.TryGetValue<string>(out var s) && uint.TryParse(s, out var parsed))
            return parsed;
        return 0;
    }
}
