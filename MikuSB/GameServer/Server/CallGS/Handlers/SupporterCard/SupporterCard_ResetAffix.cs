using MikuSB.GameServer.Game.Support;
using MikuSB.Proto;
using System.Text.Json;

namespace MikuSB.GameServer.Server.CallGS.Handlers.SupporterCard;

[CallGSApi("SupporterCard_ResetAffix")]
public class SupporterCard_ResetAffix : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var req = JsonSerializer.Deserialize<SupporterCardIdParam>(param);
        var card = req == null ? null : connection.Player!.InventoryManager.GetSupportCardItem((uint)req.SupportCardUid);
        var excel = card == null ? null : SupporterCardAffixShared.GetExcel(card);
        if (card == null || excel == null || excel.AffixCost.Count < 5 || !SupportAffixStateService.HasAffix(card, SupportAffixStateService.ActiveThirdAffixSlot))
        {
            await SupporterCardAffixShared.SendResetResponse(connection);
            return;
        }

        var costs = new[] { excel.AffixCost };
        if (!SupporterCardAffixShared.HasEnoughItems(connection, costs))
        {
            await SupporterCardAffixShared.SendResetResponse(connection);
            return;
        }

        var sync = new NtfSyncPlayer();
        sync.Items.AddRange(SupporterCardAffixShared.ConsumeCostItems(connection, costs));
        var excluded = SupporterCardAffixShared.GetActiveAffixIds(card, SupportAffixStateService.ActiveThirdAffixSlot);
        var (affixId, tier) = SupportAffixService.GenerateRandomAffix(excel.AffixPool[SupportAffixStateService.ActiveThirdAffixSlot - 1], excluded);
        SupportAffixStateService.SetAffix(card, SupportAffixStateService.PendingMaxAffixSlot, affixId, tier);
        sync.Items.Add(card.ToProto());

        SupporterCardAffixShared.Save(connection);
        await SupporterCardAffixShared.SendResetResponse(connection, sync);
    }
}
