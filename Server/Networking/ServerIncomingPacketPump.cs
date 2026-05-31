using System.Net.Sockets;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal sealed class ServerIncomingPacketPump(
    IServerMessageTransport transport,
    ServerIncomingMessageDispatcher messageDispatcher,
    int wsaConnReset,
    Action<string> log)
{
    internal const int MaxPacketsPerPump = 256;

    public void PumpAvailablePackets()
    {
        var processedPackets = 0;
        while (processedPackets < MaxPacketsPerPump && transport.HasPendingMessages)
        {
            processedPackets += 1;
            try
            {
                var packet = transport.Receive();
                if (!ProtocolCodec.TryDeserialize(packet.Payload, out var message) || message is null)
                {
                    continue;
                }

                messageDispatcher.Dispatch(message, packet.RemotePeer);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset || ex.ErrorCode == wsaConnReset)
            {
                log("[server] ignoring UDP connection reset from disconnected client");
            }
            catch (Exception ex)
            {
                log($"[server] unhandled exception processing incoming packet: {ex}");
            }
        }
    }
}
