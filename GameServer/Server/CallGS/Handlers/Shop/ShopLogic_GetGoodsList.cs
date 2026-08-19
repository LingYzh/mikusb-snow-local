using MikuSB.Data;
using MikuSB.Data.Excel;
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

    public static JsonObject BuildGoodsList(PlayerInstance player, uint shopId)
    {
        IEnumerable<ShopGoodsExcel> goods;
        if (shopId > 0 && GameData.ShopGoodsByShop.TryGetValue(shopId, out var list))
            goods = list;
        else if (shopId == 0)
            goods = GameData.ShopGoodsData.Values;
        else
            goods = [];

        var result = new JsonObject();
        var count = 0;
        foreach (var item in goods)
        {
            result[item.GoodsId.ToString()] = (int)GetBought(player, item.GoodsId);
            count++;
        }

        Logger.Info($"GetGoodsList shopId={shopId} count={count}");
        return result;
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
        RewardGrant.Save(player);

        var rsp = new JsonObject
        {
            ["nGoodsId"] = (int)goods.GoodsId,
            ["nCount"] = (int)count,
            ["nBuyCount"] = (int)GetBought(player, goods.GoodsId),
            ["tbGoods"] = RewardGrant.ToAwardArray([reward])
        };
        await CallGSRouter.SendScript(connection, api, rsp.ToJsonString(), sync);
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
        var rsp = ShopService.BuildGoodsList(connection.Player!, shopId);
        await CallGSRouter.SendScript(connection, "ShopLogic_GetGoodsList", rsp.ToJsonString());
    }
}

[CallGSApi("ShopLogic_RefreshGoods")]
public class ShopLogic_RefreshGoods : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var shopId = ShopService.ReadShopId(ShopService.ParseNode(param));
        var rsp = ShopService.BuildGoodsList(connection.Player!, shopId);
        await CallGSRouter.SendScript(connection, "ShopLogic_RefreshGoods", rsp.ToJsonString());
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
