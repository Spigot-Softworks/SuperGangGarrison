using System.Net;
using System.Net.Sockets;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal sealed class ServerIncomingPacketPump(
    UdpClient udp,
    ServerIncomingMessageDispatcher messageDispatcher,
    int wsaConnReset,
    Action<string> log)
{
    public void PumpAvailablePackets()
    {
        while (udp.Available > 0)
        {
            try
            {
                IPEndPoint remoteEndPoint = new(IPAddress.Any, 0);
                var payload = udp.Receive(ref remoteEndPoint);
                if (!ProtocolCodec.TryDeserialize(payload, out var message) || message is null)
                {
                    continue;
                }

                messageDispatcher.Dispatch(message, remoteEndPoint);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset || ex.ErrorCode == wsaConnReset)
            {
                log("[server] ignoring UDP connection reset from disconnected client");
            }
        }
    }
}
