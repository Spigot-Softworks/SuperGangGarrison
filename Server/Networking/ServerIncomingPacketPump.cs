using System.Net.Sockets;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal sealed class ServerIncomingPacketPump(
    IServerDatagramTransport transport,
    ServerIncomingMessageDispatcher messageDispatcher,
    int wsaConnReset,
    Action<string> log)
{
    public void PumpAvailablePackets()
    {
        while (transport.HasPendingDatagrams)
        {
            try
            {
                var datagram = transport.Receive();
                if (!ProtocolCodec.TryDeserialize(datagram.Payload, out var message) || message is null)
                {
                    continue;
                }

                messageDispatcher.Dispatch(message, datagram.RemotePeer);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset || ex.ErrorCode == wsaConnReset)
            {
                log("[server] ignoring UDP connection reset from disconnected client");
            }
        }
    }
}
