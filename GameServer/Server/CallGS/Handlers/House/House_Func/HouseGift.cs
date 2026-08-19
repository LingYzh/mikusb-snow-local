using MikuSB.Data;
using MikuSB.GameServer.Game.Reward;
using MikuSB.Proto;

namespace MikuSB.GameServer.Server.CallGS.Handlers.House;

[HouseFunc("GiveGiftToArea")]
[HouseFunc("GiftExchange")]
[HouseFunc("BuyGift")]
[HouseFunc("ExchangeGift")]
[HouseFunc("GiveGift")]
public class GiveGiftToArea : IHouseFuncHandler
{
    public async Task Handle(Connection connection, string param)
    {
        var root = HouseJson.ParseObject(param);
        if (root == null)
            return;

        var giftId = (uint)Math.Max(
            HouseJson.NumField(root, "GiftId"),
            Math.Max(HouseJson.NumField(root, "nGiftId"),
                Math.Max(HouseJson.NumField(root, "nGiftID"), HouseJson.NumField(root, "nId"))));
        var sync = new NtfSyncPlayer();
        var player = connection.Player!;

        if (giftId > 0 && GameData.GiftExchangeData.TryGetValue(giftId, out var gift) && gift.ShopBan == 0)
        {
            foreach (var cost in gift.NeedItems)
                RewardGrant.TryConsume(player, sync, cost);

            if (gift.NeedMoney > 0)
                RewardGrant.TryConsumeCash(player, sync, 1, gift.NeedMoney);

            var reward = gift.Gift.ToList();
            while (reward.Count < 5)
                reward.Add(1);
            await RewardGrant.GrantAsync(player, sync, reward);
            RewardGrant.Save(player);
        }

        await CallGSRouter.SendScript(connection, "House_Request", HouseRequestScript.Synthesize(root), sync);
    }
}
