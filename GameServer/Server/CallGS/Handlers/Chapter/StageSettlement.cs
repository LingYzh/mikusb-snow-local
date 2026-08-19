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

        return (awardArray, sync);
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

        AddRows(awards, showAward);
        foreach (var dropId in baseDrop)
            AddRows(awards, RollDrop(dropId));
        foreach (var dropId in randomDrop)
            AddRows(awards, RollDrop(dropId));

        if (firstClear)
        {
            AddRows(awards, showFirst);
            foreach (var dropId in firstDrop)
                AddRows(awards, RollDrop(dropId));
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
