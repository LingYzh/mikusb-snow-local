namespace MikuSB.GameServer.Server.CallGS.Handlers.Activity;

// Client requests an activity state refresh. No s2c callback registered on client side.
// param: {nId}
[CallGSApi("Activity_Refresh")]
public class Activity_Refresh : ICallGSHandler
{
    public Task Handle(Connection connection, string param, ushort seqNo)
    {
        // TODO: refresh activity state for the given nId
        return Task.CompletedTask;
    }
}
