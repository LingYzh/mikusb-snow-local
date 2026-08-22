namespace MikuSB.GameServer.Server.CallGS.Handlers.Achievement;

// Client notifies the server that an achievement trigger condition was met. No response required.
// param: {nType}
[CallGSApi("Achievement_OnAddSomeOne")]
public class Achievement_OnAddSomeOne : ICallGSHandler
{
    public Task Handle(Connection connection, string param, ushort seqNo)
    {
        // TODO: process achievement progress for the given nType
        return Task.CompletedTask;
    }
}
