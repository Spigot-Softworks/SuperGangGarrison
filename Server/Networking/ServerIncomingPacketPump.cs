using System.Buffers.Binary;
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

    private readonly Protocol64SchemaRegistry _protocol64Registry =
        Protocol64SchemaRegistryFactory.CreateDefault();

    public void PumpAvailablePackets()
    {
        var processedPackets = 0;
        while (processedPackets < MaxPacketsPerPump && transport.HasPendingMessages)
        {
            processedPackets += 1;
            try
            {
                var packet = transport.Receive();
                if (packet.Payload.Length >= sizeof(uint)
                    && BinaryPrimitives.ReadUInt32LittleEndian(packet.Payload) == Protocol64FrameHeader.Magic)
                {
                    var protocol64 = Protocol64FrameCodec.Decode(
                        packet.Payload,
                        _protocol64Registry,
                        new Protocol64FrameDecodeOptions
                        {
                            Backend = "server-inbound",
                            ExpectedDirection = Protocol64Direction.ClientToServer,
                            FaultSink = new LoggingProtocol64FaultSink(log),
                        });
                    if (protocol64.Succeeded &&
                        protocol64.Event is not null &&
                        protocol64.Schema?.Descriptor.Direction is
                            Protocol64Direction.ClientToServer or Protocol64Direction.Bidirectional)
                    {
                        messageDispatcher.DispatchProtocol64(protocol64.Event, packet.RemotePeer);
                    }
                    else if (protocol64.Succeeded && protocol64.Event is not null)
                    {
                        log($"[network] protocol-64 inbound S2C event rejected: schema={protocol64.Schema?.Descriptor.Key}");
                    }

                    continue;
                }

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

    private sealed class LoggingProtocol64FaultSink(Action<string> log) : IProtocol64FaultSink
    {
        public void Report(Protocol64Fault fault)
            => log($"[network] protocol-64 inbound frame ignored ({fault.Kind}): {fault.Message}");
    }
}
