using MikuSB.GameServer.Game.Support;
using MikuSB.Proto;
using System.Text.Json;

namespace MikuSB.GameServer.Server.CallGS.Handlers.SupporterCard;

[CallGSApi("SupporterCard_ResetInitialAffix")]
public class SupporterCard_ResetInitialAffix : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        await Reset(connection, param, fixedMode: false);
    }

    internal static async Task Reset(Connection connection, string param, bool fixedMode)
    {
        var req = JsonSerializer.Deserialize<SupporterCardResetInitialParam>(param);
        var card = req == null ? null : connection.Player!.InventoryManager.GetSupportCardItem((uint)req.SupportCardUid);
        var excel = card == null ? null : SupporterCardAffixShared.GetExcel(card);
        if (req == null || card == null || excel == null || req.Index is < 1 or > 2 || excel.AffixPool.Count < req.Index)
        {
            await SupporterCardAffixShared.SendResetResponse(connection);
            return;
        }

        var costs = fixedMode ? new[] { excel.FixedAffixCost } : excel.InitialAffixCost;
        if (!costs.Any() || !SupporterCardAffixShared.HasEnoughItems(connection, costs))
        {
            await SupporterCardAffixShared.SendResetResponse(connection);
            return;
        }

        var sync = new NtfSyncPlayer();
        sync.Items.AddRange(SupporterCardAffixShared.ConsumeCostItems(connection, costs));

        uint affixId;
        uint tier;
        if (fixedMode && req.FixedId > 0)
        {
            affixId = req.FixedId;
            tier = SupportAffixService.GenerateTier(affixId);
        }
        else
        {
            var excluded = SupporterCardAffixShared.GetActiveAffixIds(card, req.Index);
            (affixId, tier) = SupportAffixService.GenerateRandomAffix(excel.AffixPool[req.Index - 1], excluded);
        }

        SupportAffixStateService.SetAffix(card, SupportAffixStateService.PendingInitialAffixSlot, affixId, tier);
        card.AffixId = (uint)req.Index;

        var attr = SupporterCardAffixShared.GetOrCreateAttr(connection.Player!.Data, SupporterCardAffixShared.BaseGid, SupporterCardAffixShared.FixedResetSid);
        attr.Val += 1;
        SupporterCardAffixShared.SetAttr(connection, sync, SupporterCardAffixShared.BaseGid, SupporterCardAffixShared.FixedResetSid, attr.Val);

        sync.Items.Add(card.ToProto());
        SupporterCardAffixShared.Save(connection);
        await SupporterCardAffixShared.SendResetResponse(connection, sync);
    }
}
