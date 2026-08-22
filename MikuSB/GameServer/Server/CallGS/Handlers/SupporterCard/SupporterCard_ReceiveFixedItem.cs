using MikuSB.Data;
using MikuSB.Proto;
using System.Text.Json.Nodes;

namespace MikuSB.GameServer.Server.CallGS.Handlers.SupporterCard;

[CallGSApi("SupporterCard_ReceiveFixedItem")]
public class SupporterCard_ReceiveFixedItem : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var player = connection.Player!;
        if (!GameData.SupportFixedData.TryGetValue(1, out var fixedCfg) || fixedCfg.Item.Count < 5 || fixedCfg.Num <= 0)
        {
            await CallGSRouter.SendScript(connection, "SupporterCard_ReceiveFixedItem", "{}");
            return;
        }

        var attr = SupporterCardAffixShared.GetOrCreateAttr(player.Data, SupporterCardAffixShared.BaseGid, SupporterCardAffixShared.FixedResetSid);
        var claimCount = attr.Val / (uint)fixedCfg.Num;
        if (claimCount == 0)
        {
            await CallGSRouter.SendScript(connection, "SupporterCard_ReceiveFixedItem", "{}");
            return;
        }

        attr.Val %= (uint)fixedCfg.Num;

        var rewardTemplateId = GameResourceTemplateId.FromGdpl(fixedCfg.Item);
        var rewardItem = GameData.SuppliesData.GetValueOrDefault(rewardTemplateId);
        if (rewardItem == null)
        {
            await CallGSRouter.SendScript(connection, "SupporterCard_ReceiveFixedItem", "{}");
            return;
        }

        var granted = await player.InventoryManager.AddSuppliesItem(rewardItem, claimCount * fixedCfg.Item[4], sendPacket: false);

        var sync = new NtfSyncPlayer();
        if (granted != null)
            sync.Items.Add(granted.ToProto());
        SupporterCardAffixShared.SetAttr(connection, sync, SupporterCardAffixShared.BaseGid, SupporterCardAffixShared.FixedResetSid, attr.Val);
        SupporterCardAffixShared.Save(connection);

        var arg = new JsonObject
        {
            ["tbRewards"] = new JsonArray(
                (int)fixedCfg.Item[0],
                (int)fixedCfg.Item[1],
                (int)fixedCfg.Item[2],
                (int)fixedCfg.Item[3],
                (int)(claimCount * fixedCfg.Item[4]))
        }.ToJsonString();

        await CallGSRouter.SendScript(connection, "SupporterCard_ReceiveFixedItem", arg, sync);
    }
}
