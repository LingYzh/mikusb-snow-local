using MikuSB.TcpSharp;

namespace MikuSB.GameServer.Server;

public class Listener : SocketListener
{
    public static Connection? GetActiveConnection(int uid)
    {
        lock (Connections)
        {
            return Connections.Values.FirstOrDefault(c =>
                (c as Connection)?.Player?.Uid == uid && c.State == SessionStateEnum.ACTIVE) as Connection;
        }
    }
}