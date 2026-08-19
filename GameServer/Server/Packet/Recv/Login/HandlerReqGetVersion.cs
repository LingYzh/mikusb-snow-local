using MikuSB.GameServer.Server.Packet;
using MikuSB.Proto;
using MikuSB.TcpSharp;

namespace MikuSB.GameServer.Server.Packet.Recv.Login;

[Opcode(CmdIds.ReqGetVersion)]
public class HandlerReqGetVersion : Handler
{
    public override async Task OnHandle(Connection connection, byte[] data, ushort seqNo)
    {
        await connection.SendPacket(CmdIds.RspGetVersion);
    }
}
