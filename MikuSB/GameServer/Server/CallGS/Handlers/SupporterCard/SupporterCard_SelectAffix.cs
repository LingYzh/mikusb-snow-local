using MikuSB.GameServer.Game.Support;
using MikuSB.Proto;
using System.Text.Json;

namespace MikuSB.GameServer.Server.CallGS.Handlers.SupporterCard;

[CallGSApi("SupporterCard_SelectAffix")]
public class SupporterCard_SelectAffix : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var req = JsonSerializer.Deserialize<SupporterCardSelectParam>(param);
        var card = req == null ? null : connection.Player!.InventoryManager.GetSupportCardItem((uint)req.SupportCardUid);
        if (card == null || !SupportAffixStateService.HasAffix(card, SupportAffixStateService.PendingMaxAffixSlot))
        {
            await SupporterCardAffixShared.SendSelectResponse(connection);
            return;
        }

        if (req!.SelectNew)
            SupportAffixStateService.CopyAffix(card, SupportAffixStateService.PendingMaxAffixSlot, SupportAffixStateService.ActiveThirdAffixSlot);

        SupportAffixStateService.ClearAffix(card, SupportAffixStateService.PendingMaxAffixSlot);

        var sync = new NtfSyncPlayer();
        sync.Items.Add(card.ToProto());
        SupporterCardAffixShared.Save(connection);
        await SupporterCardAffixShared.SendSelectResponse(connection, sync);
    }
}
