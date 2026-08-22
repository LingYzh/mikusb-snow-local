namespace MikuSB.GameServer.Server.CallGS.Handlers.Misc;

// Client notifies the server of its language setting. No response required.
// param: {nType, sLan, sEx}
[CallGSApi("LanguageChange")]
public class LanguageChange : ICallGSHandler
{
    public Task Handle(Connection connection, string param, ushort seqNo)
        => Task.CompletedTask;
}
