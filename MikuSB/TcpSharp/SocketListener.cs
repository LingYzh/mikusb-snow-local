using System.Net.Sockets;
using System.Net;
using MikuSB.Util;
using MikuSB.Internationalization;

namespace MikuSB.TcpSharp;

public class SocketListener
{
    private static readonly Logger Logger = new("GameServer");
    private static readonly List<Socket> ServerSockets = [];

    public static readonly SortedList<long, SocketConnection> Connections = [];

    public static Type BaseConnection { get; set; } = typeof(SocketConnection);

    private static int PORT => ConfigManager.Config.GameServer.Port;

    private static long _nextId = 0;

    private static IEnumerable<int> GetListenPorts()
    {
        var ports = new HashSet<int> { PORT, 5200, 5100, 5201, 21000, 21001 };
        foreach (var port in ports)
            yield return port;
    }

    public static void StartListener()
    {
        if (ServerSockets.Count > 0)
            throw new InvalidOperationException("SocketListener already started.");

        var bindAddress = IPAddress.Parse(ConfigManager.Config.GameServer.BindAddress);
        foreach (var port in GetListenPorts())
        {
            try
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Bind(new IPEndPoint(bindAddress, port));
                socket.Listen(100);
                ServerSockets.Add(socket);
                Logger.Info(I18NManager.Translate("Server.ServerInfo.ServerRunning",
                    I18NManager.Translate("Word.Game"),
                    $"{ConfigManager.Config.GameServer.PublicAddress}:{port}"));
                var captured = socket;
                _ = Task.Run(() => AcceptLoop(captured));
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to listen on {bindAddress}:{port}: {ex.Message}");
            }
        }

        if (ServerSockets.Count == 0)
            throw new InvalidOperationException("GameServer failed to bind any TCP port.");
    }

    private static async Task AcceptLoop(Socket serverSocket)
    {
        try
        {
            while (true)
            {
                Socket clientSocket = await serverSocket.AcceptAsync();
                try
                {
                    clientSocket.NoDelay = true;
                }
                catch
                {
                }

                var remote = clientSocket.RemoteEndPoint as IPEndPoint;
                if (remote == null)
                {
                    clientSocket.Close();
                    continue;
                }

                try
                {
                    var connection = (SocketConnection?)Activator.CreateInstance(BaseConnection, clientSocket, remote);

                    if (connection == null)
                    {
                        Logger.Error($"Failed to create connection instance from {BaseConnection.Name}");
                        clientSocket.Close();
                        continue;
                    }

                    var id = Interlocked.Increment(ref _nextId);
                    connection.ConnectionId = id;

                    lock (Connections)
                        Connections[id] = connection;
                    Logger.Info($"Accepted connection #{id} from {remote} on {(serverSocket.LocalEndPoint as IPEndPoint)?.Port}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error creating connection: {ex}");
                    clientSocket.Close();
                }
            }
        }
        catch (ObjectDisposedException)
        {
            Logger.Info("Server stopped listening.");
        }
        catch (Exception ex)
        {
            Logger.Error($"AcceptLoop crashed: {ex}");
        }
    }

    public static SocketConnection? GetConnectionByEndPoint(IPEndPoint ep)
    {
        lock (Connections)
            return Connections.Values.FirstOrDefault(c => c.RemoteEndPoint.Equals(ep));
    }

    public static void UnregisterConnection(SocketConnection socket)
    {
        if (socket == null) return;

        lock (Connections)
        {
            if (Connections.Remove(socket.ConnectionId))
                Logger.Info($"Connection #{socket.ConnectionId} with {socket.RemoteEndPoint} has been closed");
        }
    }
}
