using MikuSB.Data;
using MikuSB.Data.Excel;
using MikuSB.Database;
using MikuSB.GameServer.Game.Player;
using MikuSB.GameServer.Game.Reward;
using MikuSB.Proto;
using MikuSB.Util;
using System.Globalization;
using System.Text.Json.Nodes;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Shop;

internal static class ShopService
{
    public const uint BuyGroupId = 27;
    private const uint ShopStrGroupId = 1;
    private const uint GoodsStateStart = 1000;
    private static readonly Logger Logger = new("Shop");

    public static uint ReadShopId(JsonNode? node)
    {
        if (node is JsonValue value)
            return ToUInt(value);
        if (node is not JsonObject obj)
            return 0;

        foreach (var key in new[] { "nShopId", "nShopID", "nId", "nID", "shopId", "ShopId", "shopID" })
        {
            if (obj.TryGetPropertyValue(key, out var child))
            {
                var id = ToUInt(child);
                if (id > 0)
                    return id;
            }
        }

        if (obj.TryGetPropertyValue("tbParam", out var nested))
        {
            var nestedId = ReadShopId(nested);
            if (nestedId > 0)
                return nestedId;
        }

        return 0;
    }

    public static uint ReadGoodsId(JsonObject? obj)
    {
        if (obj == null)
            return 0;
        foreach (var key in new[] { "nGoodsId", "nGoodsID", "GoodsId", "nId", "nID" })
        {
            if (obj.TryGetPropertyValue(key, out var node))
            {
                var id = ToUInt(node);
                if (id > 0)
                    return id;
            }
        }

        return 0;
    }

    public static JsonObject BuildGoodsList(PlayerInstance player, uint shopId, NtfSyncPlayer sync, bool refresh = false)
    {
        IEnumerable<ShopGoodsExcel> goods;
        if (shopId > 0 && GameData.ShopGoodsByShop.TryGetValue(shopId, out var list))
            goods = list;
        else if (shopId == 0)
            goods = GameData.ShopGoodsData.Values;
        else
            goods = [];

        var refreshNumber = GetRefreshRollNumber(player, shopId);
        if (refresh)
            refreshNumber++;

        var availableGoods = goods.OrderBy(x => x.GoodsId).ToList();
        var displayLimit = availableGoods.Count;
        if (GameData.ShopTabData.TryGetValue(shopId, out var tab) && tab.Columns > 0)
            displayLimit = Math.Min(displayLimit, checked((int)tab.Columns));

        // Shops with Columns configured are rotating shops.  Keep the first
        // presentation deterministic, then use the persisted refresh counter
        // to select a different, reproducible set each manual refresh.
        if (refreshNumber > 0 && availableGoods.Count > 1)
        {
            var random = new Random(unchecked(
                player.Uid ^ (int)(shopId * 397u) ^ (int)(refreshNumber * 7919u)));
            for (var index = availableGoods.Count - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);
                (availableGoods[index], availableGoods[swapIndex]) =
                    (availableGoods[swapIndex], availableGoods[index]);
            }
        }

        var goodsList = new JsonArray();
        var count = 0;
        foreach (var item in availableGoods.Take(displayLimit))
        {
            // The client expects an ordered table of goods records, rather than
            // a goodsId-to-buyCount map.  In particular it reads `goodsid` from
            // every row before looking up the static ShopGoods configuration.
            goodsList.Add(new JsonObject
            {
                ["goodsid"] = (int)item.GoodsId,
                ["discountindex"] = 0
            });
            var state = new JsonObject
            {
                ["buynum"] = (int)GetBought(player, item.GoodsId),
                ["invalidtime"] = 0
            };
            SetStringAttr(player, sync, ShopStrGroupId, GoodsStateStart + item.GoodsId, state.ToJsonString());
            count++;
        }

        // The visible list response is not enough for the Lua buy path.  It
        // re-reads this persistent string attribute before every purchase.
        var persistedShop = new JsonObject
        {
            ["shopid"] = (int)shopId,
            ["version"] = 1,
            ["listversion"] = 1,
            // `refreshnum` is exclusively the client-visible quota.  Keep it
            // at zero for offline testing so the UI always has its full set of
            // manual refreshes available; rollnum preserves varied shelves.
            ["refreshnum"] = 0,
            ["rollnum"] = (int)refreshNumber,
            ["displaynum"] = count,
            ["refreshtime"] = 0,
            ["tbgoods"] = goodsList.DeepClone()
        };
        SetStringAttr(player, sync, ShopStrGroupId, shopId, persistedShop.ToJsonString());
        Logger.Info($"GetGoodsList shopId={shopId} count={count}");
        return new JsonObject
        {
            ["shopId"] = (int)shopId,
            ["nShopId"] = (int)shopId,
            ["goodsList"] = goodsList
        };
    }

    public static JsonObject BuildOpenTimes()
    {
        var result = new JsonObject();
        foreach (var tab in GameData.ShopTabData.Values)
        {
            result[tab.ShopId.ToString()] = new JsonObject
            {
                ["nBegin"] = ParseCompactTime(tab.Begin, 202109010000),
                ["nEnd"] = ParseCompactTime(tab.End, 209912310400)
            };
        }

        return result;
    }

    public static uint GetBought(PlayerInstance player, uint goodsId)
    {
        return player.Data.Attrs.FirstOrDefault(x => x.Gid == BuyGroupId && x.Sid == goodsId)?.Val ?? 0;
    }

    public static void AddBought(PlayerInstance player, NtfSyncPlayer sync, uint goodsId, uint count)
    {
        var attr = RewardGrant.GetOrCreateAttr(player, BuyGroupId, goodsId);
        attr.Val += count;
        RewardGrant.SyncAttr(player, sync, attr);
    }

    // A manual shop refresh is a restock in the original UI: the newly rolled
    // shelf must not retain the previous purchase counters.  Keep the attrs
    // (rather than deleting them) so an open client receives explicit zeroes.
    public static void RestockShop(PlayerInstance player, uint shopId, NtfSyncPlayer sync)
    {
        if (!GameData.ShopGoodsByShop.TryGetValue(shopId, out var goods))
            return;

        var restocked = 0;
        foreach (var goodsId in goods.Select(x => x.GoodsId).Distinct())
        {
            var attr = player.Data.Attrs.FirstOrDefault(x =>
                x.Gid == BuyGroupId && x.Sid == goodsId);
            if (attr == null || attr.Val == 0)
                continue;

            attr.Val = 0;
            RewardGrant.SyncAttr(player, sync, attr);
            SyncGoodsPurchaseState(player, sync, goodsId);
            restocked++;
        }

        Logger.Info($"Restocked shopId={shopId} goods={restocked}");
    }

    private static void SetStringAttr(PlayerInstance player, NtfSyncPlayer sync, uint gid, uint sid, string value)
    {
        player.SetStrAttr(gid, sid, value);
        sync.CustomStr[player.ToShiftedAttrKey(gid, sid)] = value;
    }

    private static void SyncGoodsPurchaseState(PlayerInstance player, NtfSyncPlayer sync, uint goodsId)
    {
        var state = new JsonObject
        {
            ["buynum"] = (int)GetBought(player, goodsId),
            ["invalidtime"] = 0
        };
        SetStringAttr(player, sync, ShopStrGroupId, GoodsStateStart + goodsId, state.ToJsonString());
    }

    private static uint GetRefreshRollNumber(PlayerInstance player, uint shopId)
    {
        var raw = player.Data.StrAttrs
            .FirstOrDefault(x => x.Gid == ShopStrGroupId && x.Sid == shopId)?.Val;
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        try
        {
            var data = JsonNode.Parse(raw) as JsonObject;
            // Migrate a shelf generated by the previous implementation once,
            // then retain the independent server-side roll counter.
            return ToUInt(data?["rollnum"] ?? data?["refreshnum"]);
        }
        catch
        {
            return 0;
        }
    }

    public static void ConsumePrice(PlayerInstance player, NtfSyncPlayer sync, IReadOnlyList<uint> price, uint count)
    {
        if (price.Count >= 4)
        {
            var scaled = price.ToList();
            while (scaled.Count < 5)
                scaled.Add(1);
            scaled[4] = Math.Max(1u, scaled[4]) * count;
            RewardGrant.TryConsume(player, sync, scaled);
            return;
        }

        if (price.Count >= 2)
            RewardGrant.TryConsumeCash(player, sync, price[0], Math.Max(1u, price[1]) * count);
    }

    public static async Task HandleBuyAsync(Connection connection, string param, string api)
    {
        var node = ParseNode(param);
        var obj = node as JsonObject;
        var goodsId = ReadGoodsId(obj);
        var count = Math.Max(1u, ToUInt(obj?["nCount"] ?? obj?["Count"]));
        var player = connection.Player!;
        if (goodsId == 0 || !GameData.ShopGoodsData.TryGetValue(goodsId, out var goods))
        {
            await CallGSRouter.SendScript(connection, api, "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        if (goods.LimitNum > 0 && GetBought(player, goods.GoodsId) + count > goods.LimitNum)
        {
            await CallGSRouter.SendScript(connection, api, "{\"sErr\":\"tip.Mall_Limit_Buy\"}");
            return;
        }

        var sync = new NtfSyncPlayer();
        var price = goods.Price1.Count >= 2 ? goods.Price1 : goods.Price2;
        ConsumePrice(player, sync, price, count);

        var reward = goods.Gdpln.ToList();
        while (reward.Count < 5)
            reward.Add(1);
        reward[4] = Math.Max(1u, reward[4]) * count;
        await RewardGrant.GrantAsync(player, sync, reward);
        AddBought(player, sync, goods.GoodsId, count);
        SyncGoodsPurchaseState(player, sync, goods.GoodsId);
        RewardGrant.Save(player);

        var rsp = new JsonObject
        {
            ["nShopId"] = (int)goods.ShopId,
            ["nGoodsId"] = (int)goods.GoodsId,
            ["nCount"] = (int)count,
            ["nBuyCount"] = (int)GetBought(player, goods.GoodsId),
            // Shop.lua passes tbGoods directly to GetActualItemsUseMode(),
            // which expects one GDPLN row rather than an array of rows.
            ["tbGoods"] = ToJsonArray(reward)
        };
        await CallGSRouter.SendScript(connection, api, rsp.ToJsonString(), sync);
    }

    private static JsonArray ToJsonArray(IEnumerable<uint> values)
    {
        var result = new JsonArray();
        foreach (var value in values)
            result.Add((int)value);
        return result;
    }

    public static JsonNode? ParseNode(string? param)
    {
        if (string.IsNullOrWhiteSpace(param))
            return new JsonObject();
        try
        {
            return JsonNode.Parse(param);
        }
        catch
        {
            return new JsonObject();
        }
    }

    public static uint ToUInt(JsonNode? node)
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

    private static long ParseCompactTime(string raw, long fallback)
    {
        var normalized = (raw ?? "").Trim().Trim('[', ']');
        if (normalized.Length >= 12 && long.TryParse(normalized.AsSpan(0, 12), out var compact))
            return compact;
        if (long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            return value;
        return fallback;
    }
}

[CallGSApi("ShopLogic_GetGoodsList")]
public class ShopLogic_GetGoodsList : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var shopId = ShopService.ReadShopId(ShopService.ParseNode(param));
        var sync = new NtfSyncPlayer();
        var rsp = ShopService.BuildGoodsList(connection.Player!, shopId, sync);
        DatabaseHelper.SaveDatabaseType(connection.Player!.Data);
        await CallGSRouter.SendScript(connection, "ShopLogic_GetGoodsList", rsp.ToJsonString(), sync);
    }
}

[CallGSApi("ShopLogic_RefreshGoods")]
public class ShopLogic_RefreshGoods : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var shopId = ShopService.ReadShopId(ShopService.ParseNode(param));
        var sync = new NtfSyncPlayer();
        ShopService.RestockShop(connection.Player!, shopId, sync);
        var rsp = ShopService.BuildGoodsList(connection.Player!, shopId, sync, refresh: true);
        DatabaseHelper.SaveDatabaseType(connection.Player!.Data);
        await CallGSRouter.SendScript(connection, "ShopLogic_GetGoodsList", rsp.ToJsonString(), sync);
    }
}

[CallGSApi("ShopLogic_UpdateShop")]
public class ShopLogic_UpdateShop : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var shopId = ShopService.ReadShopId(ShopService.ParseNode(param));
        var sync = new NtfSyncPlayer();
        ShopService.RestockShop(connection.Player!, shopId, sync);
        var rsp = ShopService.BuildGoodsList(connection.Player!, shopId, sync, refresh: true);
        DatabaseHelper.SaveDatabaseType(connection.Player!.Data);
        // Lua only registers this callback name and closes its loading mask
        // there, including for a manual refresh request.
        await CallGSRouter.SendScript(connection, "ShopLogic_GetGoodsList", rsp.ToJsonString(), sync);
    }
}

[CallGSApi("ShopLogic_BuyGoods")]
public class ShopLogic_BuyGoods : ICallGSHandler
{
    public Task Handle(Connection connection, string param, ushort seqNo)
        => ShopService.HandleBuyAsync(connection, param, "ShopLogic_BuyGoods");
}

[CallGSApi("ShopLogic_Buy")]
public class ShopLogic_Buy : ICallGSHandler
{
    public Task Handle(Connection connection, string param, ushort seqNo)
        => ShopService.HandleBuyAsync(connection, param, "ShopLogic_Buy");
}

[CallGSApi("ShopLogic_BuyItem")]
public class ShopLogic_BuyItem : ICallGSHandler
{
    public Task Handle(Connection connection, string param, ushort seqNo)
        => ShopService.HandleBuyAsync(connection, param, "ShopLogic_BuyItem");
}

[CallGSApi("ShopLogic_ExchangeGoods")]
public class ShopLogic_ExchangeGoods : ICallGSHandler
{
    public Task Handle(Connection connection, string param, ushort seqNo)
        => ShopService.HandleBuyAsync(connection, param, "ShopLogic_ExchangeGoods");
}
