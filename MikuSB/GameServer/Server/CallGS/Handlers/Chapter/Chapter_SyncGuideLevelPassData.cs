namespace MikuSB.GameServer.Server.CallGS.Handlers.Chapter;

// Client syncs completed guide level data to the server. No response required.
// param: {tbData = [{nLevelID, passTime}, ...]}
[CallGSApi("Chapter_SyncGuideLevelPassData")]
public class Chapter_SyncGuideLevelPassData : ICallGSHandler
{
    public Task Handle(Connection connection, string param, ushort seqNo)
    {
        // TODO: persist guide level pass data to player save
        return Task.CompletedTask;
    }
}
