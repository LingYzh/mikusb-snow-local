namespace MikuSB.GameServer.Server.CallGS.Handlers.Role;

// Response:{tbRet:{nSeed:random_number}}
[CallGSApi("Role_EnterLevel")]
public class Role_EnterLevel : ICallGSHandler
{
    private static readonly Random _random = new Random();

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        string rsp = $"{{\"tbRet\":{{\"nSeed\":{_random.Next(1, 1000000000)}}}}}";
        await CallGSRouter.SendScript(connection, "Role_EnterLevel", rsp);
    }
}