namespace MikuSB.GameServer.Server.CallGS.Handlers.Achievement;

// Client requests a reward for a completed achievement.
// param: {nId}
// Response: {}
[CallGSApi("Achievement_GetReward")]
public class Achievement_GetReward : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        // TODO: validate achievement completion and grant reward items
        await CallGSRouter.SendScript(connection, "Achievement_GetReward", "{}");
    }
}
