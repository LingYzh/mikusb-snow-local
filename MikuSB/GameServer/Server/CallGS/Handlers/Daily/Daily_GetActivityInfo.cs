namespace MikuSB.GameServer.Server.CallGS.Handlers.Daily;

// Returns daily activity info for each activity type.
// Response: {activityId: tbActivity}
[CallGSApi("Daily_GetActivityInfo")]
public class Daily_GetActivityInfo : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        // TODO: return actual daily activity data
        await CallGSRouter.SendScript(connection, "Daily_GetActivityInfo", "{}");
    }
}
