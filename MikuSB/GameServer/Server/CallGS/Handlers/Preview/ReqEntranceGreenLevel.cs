namespace MikuSB.GameServer.Server.CallGS.Handlers.Preview;

// Returns the green (unlocked) level for each skin manifestation.
// Response: [{skinId, greenLevel}, ...]
[CallGSApi("ReqEntranceGreenLevel")]
public class ReqEntranceGreenLevel : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        // TODO: return actual skin green levels from player data
        await CallGSRouter.SendScript(connection, "ReqEntranceGreenLevel", "[]");
    }
}
