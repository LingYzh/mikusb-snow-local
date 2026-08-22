namespace MikuSB.GameServer.Server.CallGS.Handlers.Misc;

// Client requests server time to calculate timezone offset.
// In the client, ZoneTime.lua hardcodes sTime1/sTime2; if nTime1/nTime2 are false, the client ignores this update.
// Otherwise, offset = nTimeX - ParseTimeNative(sTimeX).
[CallGSApi("ZoneTime_ReqTime")]
public class ZoneTime_ReqTime : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var arg = $"{{\"nTime1\":false,\"nTime2\":false}}";
        await CallGSRouter.SendScript(connection, "ZoneTime_ChangeTime", arg);
    }
}
