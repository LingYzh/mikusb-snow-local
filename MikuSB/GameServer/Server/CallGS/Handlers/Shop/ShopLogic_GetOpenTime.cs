namespace MikuSB.GameServer.Server.CallGS.Handlers.Shop;

[CallGSApi("ShopLogic_GetOpenTime")]
public class ShopLogic_GetOpenTime : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var rsp = ShopService.BuildOpenTimes();
        await CallGSRouter.SendScript(connection, "ShopLogic_GetOpenTime", rsp.ToJsonString());
    }
}
