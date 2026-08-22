namespace MikuSB.GameServer.Server.CallGS.Handlers.Daily;

// Success response shape expected by Lua:
// { nSeed = random_number }
[CallGSApi("Daily_EnterLevel")]
public class Daily_EnterLevel : ICallGSHandler
{
    private static readonly Random Random = new();

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var rsp = $"{{\"nSeed\":{Random.Next(1, 1000000000)}}}";
        await CallGSRouter.SendScript(connection, "Daily_EnterLevel", rsp);
    }
}
