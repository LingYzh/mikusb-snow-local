namespace MikuSB.GameServer.Server.CallGS.Handlers.Rogue3D;

// Enters the Rogue3D level. Returns a random seed used by the client for map generation.
// param: {"nDiffId", "nTeamID", "tbTeam", "tbBuffList", "tbLog"}
// Response: {"nSeed": int}
[CallGSApi("Rogue3D_EnterLevel")]
public class Rogue3D_EnterLevel : ICallGSHandler
{
    private static readonly Random Random = new();

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var seed = Random.Next(1, 1_000_000_000);
        await CallGSRouter.SendScript(connection, "Rogue3D_EnterLevel", $"{{\"nSeed\":{seed}}}");
    }
}
