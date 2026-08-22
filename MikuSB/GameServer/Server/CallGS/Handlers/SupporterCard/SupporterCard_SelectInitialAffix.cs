using MikuSB.GameServer.Game.Support;
using MikuSB.Proto;
using System.Text.Json;

namespace MikuSB.GameServer.Server.CallGS.Handlers.SupporterCard;

[CallGSApi("SupporterCard_SelectInitialAffix")]
public class SupporterCard_SelectInitialAffix : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var req = JsonSerializer.Deserialize<SupporterCardSelectInitialParam>(param);
        var card = req == null ? null : connection.Player!.InventoryManager.GetSupportCardItem((uint)req.SupportCardUid);
        if (req == null || card == null || req.Index is < 1 or > 2 || card.AffixId != req.Index || !SupportAffixStateService.HasAffix(card, SupportAffixStateService.PendingInitialAffixSlot))
        {
            await SupporterCardAffixShared.SendSelectResponse(connection);
            return;
        }

        if (req.SelectNew)
            SupportAffixStateService.CopyAffix(card, SupportAffixStateService.PendingInitialAffixSlot, req.Index);

        SupportAffixStateService.ClearAffix(card, SupportAffixStateService.PendingInitialAffixSlot);
        card.AffixId = 0;

        var sync = new NtfSyncPlayer();
        sync.Items.Add(card.ToProto());
        SupporterCardAffixShared.Save(connection);
        await SupporterCardAffixShared.SendSelectResponse(connection, sync);
    }
}
