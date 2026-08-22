namespace MikuSB.GameServer.Server.CallGS.Handlers.SupporterCard;

[CallGSApi("SupporterCard_FixedResetInitialAffix")]
public class SupporterCard_FixedResetInitialAffix : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        await SupporterCard_ResetInitialAffix.Reset(connection, param, fixedMode: true);
    }
}
