using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Protocol64WebSocketConnectionTests
{
    [Fact]
    public async Task ReliableFramesAreOwnedAndNeverSilentlyDropped()
    {
        var registry = CreateRegistry();
        var socket = new FakeWebSocket();
        using var connection = CreateConnection(socket, registry, epoch: 8);

        var first = Encode(new TestEvent("first"), registry, epoch: 8, frameId: 41);
        var second = Encode(new TestEvent("second"), registry, epoch: 8, frameId: 42);
        connection.QueueReliableFrame(first);
        connection.QueueReliableFrame(second);

        // The caller no longer owns accepted reliable bytes. Mutating its input
        // cannot corrupt the queued frame.
        first[^1] ^= 0xFF;

        Assert.Equal(2, connection.ReliableQueueCount);
        Assert.True(await connection.SendNextAsync());
        Assert.True(await connection.SendNextAsync());
        Assert.False(await connection.SendNextAsync());
        Assert.Equal(2, socket.SentMessages.Count);
        Assert.All(socket.SentEndOfMessage, Assert.True);

        var received = Protocol64FrameCodec.Decode<TestEvent>(socket.SentMessages[0], registry);
        Assert.True(received.Succeeded, received.Fault?.Message);
        Assert.Equal("first", received.Event!.Value);
        Assert.Equal(41UL, received.Header!.FrameId);
    }

    [Fact]
    public async Task LastWinsReplacesOnlyTheSelectedMailboxKey()
    {
        var registry = CreateRegistry();
        var socket = new FakeWebSocket();
        using var connection = CreateConnection(socket, registry, epoch: 3);

        connection.PublishLastWins("player:7", new TestEvent("old"));
        connection.PublishLastWins("player:7", new TestEvent("new"));

        Assert.Equal(1, connection.PendingLastWinsCount);
        Assert.Equal(1, connection.LastWinsReplacementCount);
        Assert.True(await connection.SendNextAsync());
        Assert.False(await connection.SendNextAsync());

        var received = Protocol64FrameCodec.Decode<TestEvent>(socket.SentMessages.Single(), registry);
        Assert.True(received.Succeeded, received.Fault?.Message);
        Assert.Equal("new", received.Event!.Value);
    }

    [Fact]
    public void ReliableQueueOverflowIsExplicitBackpressure()
    {
        var registry = CreateRegistry();
        var socket = new FakeWebSocket();
        using var connection = CreateConnection(
            socket,
            registry,
            epoch: 4,
            options: new Protocol64WebSocketOptions
            {
                MaxReliableQueueFrames = 1,
                MaxReliableQueueBytes = 1024,
                WarningLogger = _ => { },
            });

        connection.QueueReliable(new TestEvent("accepted"));

        var exception = Assert.Throws<Protocol64WebSocketBackpressureException>(
            () => connection.QueueReliable(new TestEvent("rejected")));

        Assert.Equal(1, exception.PendingFrames);
        Assert.Equal(1, connection.ReliableQueueCount);
        Assert.Equal(1, connection.Telemetry.Snapshot().ReliableBackpressure);
    }

    [Fact]
    public void BackendOperationMustMatchTheFrameSchemaDeliveryAnnotation()
    {
        var registry = CreateRegistry();
        using var connection = CreateConnection(new FakeWebSocket(), registry, epoch: 13);
        var payload = Encode(new TestEvent("mismatch"), registry, epoch: 13, frameId: 1);

        Assert.Throws<Protocol64WebSocketException>(() => connection.QueueReliableFrame(
            payload,
            new Protocol64DeliveryDescriptor(Protocol64DeliveryKind.LastWins, ChannelType.State)));
    }

    [Fact]
    public async Task FirstMalformedCompleteMessageIsReportedAndIgnoredUntilAValidNextFrame()
    {
        var registry = CreateRegistry();
        var socket = new FakeWebSocket();
        var faults = new List<Protocol64Fault>();
        var warnings = new List<string>();
        using var connection = CreateConnection(
            socket,
            registry,
            epoch: 5,
            options: new Protocol64WebSocketOptions
            {
                FaultSink = new DelegateProtocol64FaultSink(faults.Add),
                WarningLogger = warnings.Add,
            });

        socket.QueueMessage(new byte[] { 0xFF, 0x00, 0x01 });
        socket.QueueMessage(Encode(new TestEvent("valid"), registry, epoch: 5, frameId: 1));

        var malformed = await connection.ReceiveAsync();
        Assert.Equal(Protocol64WebSocketReceiveStatus.IgnoredMalformedFrame, malformed.Status);
        Assert.Equal(Protocol64WebSocketRecoveryState.Recovering, connection.RecoveryState);
        Assert.Equal(Protocol64FaultKind.TruncatedFrame, malformed.Fault!.Kind);
        Assert.Single(faults);
        Assert.Single(warnings);

        var valid = await connection.ReceiveAsync();
        Assert.Equal(Protocol64WebSocketReceiveStatus.Frame, valid.Status);
        Assert.Equal("valid", valid.Decoded!.Event!.As<TestEvent>().Value);
        Assert.Equal(Protocol64WebSocketRecoveryState.Open, connection.RecoveryState);
        Assert.Equal(0, connection.ConsecutiveInvalidFrameCount);
    }

    [Fact]
    public async Task SecondConsecutiveInvalidMessageProducesProtocolErrorClose()
    {
        var registry = CreateRegistry();
        var socket = new FakeWebSocket();
        using var connection = CreateConnection(
            socket,
            registry,
            epoch: 6,
            options: new Protocol64WebSocketOptions { WarningLogger = _ => { } });

        socket.QueueMessage(new byte[] { 1 });
        socket.QueueMessage(new byte[] { 2 });

        var first = await connection.ReceiveAsync();
        var second = await connection.ReceiveAsync();

        Assert.Equal(Protocol64WebSocketReceiveStatus.IgnoredMalformedFrame, first.Status);
        Assert.Equal(Protocol64WebSocketReceiveStatus.ProtocolError, second.Status);
        Assert.Equal(Protocol64WebSocketRecoveryState.ProtocolError, connection.RecoveryState);
        Assert.Equal(WebSocketCloseStatus.ProtocolError, socket.CloseStatus);
    }

    [Fact]
    public async Task MalformedMessageUsesTheConfiguredReopenBudget()
    {
        var registry = CreateRegistry();
        var firstSocket = new FakeWebSocket();
        var replacementSocket = new FakeWebSocket();
        var reopenCount = 0;
        using var connection = CreateConnection(
            firstSocket,
            registry,
            epoch: 9,
            options: new Protocol64WebSocketOptions
            {
                MaxWsRetries = 2,
                ReopenAsync = _ =>
                {
                    reopenCount++;
                    return new ValueTask<WebSocket?>(replacementSocket);
                },
                WarningLogger = _ => { },
            });

        firstSocket.QueueMessage(new byte[] { 0x01 });
        replacementSocket.QueueMessage(Encode(new TestEvent("after-reopen"), registry, epoch: 9, frameId: 10));

        var malformed = await connection.ReceiveAsync();
        var valid = await connection.ReceiveAsync();

        Assert.Equal(Protocol64WebSocketReceiveStatus.IgnoredMalformedFrame, malformed.Status);
        Assert.Equal(1, reopenCount);
        Assert.Equal("after-reopen", valid.Decoded!.Event!.As<TestEvent>().Value);
        Assert.Equal(2, Protocol64WebSocketOptions.Default.MaxWsRetries);
    }

    [Fact]
    public async Task ReceiveAssemblesFragmentsAndRejectsMessagesOverTheInboundBound()
    {
        var registry = CreateRegistry();
        var socket = new FakeWebSocket();
        using var connection = CreateConnection(
            socket,
            registry,
            epoch: 12,
            options: new Protocol64WebSocketOptions
            {
                MaxInboundFrameBytes = 64,
                ReceiveBufferBytes = 128,
                WarningLogger = _ => { },
            });

        var encoded = Encode(new TestEvent("fragmented"), registry, epoch: 12, frameId: 1);
        socket.QueueFragment(encoded[..7], endOfMessage: false);
        socket.QueueFragment(encoded[7..], endOfMessage: true);

        var assembled = await connection.ReceiveAsync();
        Assert.Equal(Protocol64WebSocketReceiveStatus.Frame, assembled.Status);
        Assert.Equal("fragmented", assembled.Decoded!.Event!.As<TestEvent>().Value);

        socket.QueueMessage(new byte[65]);
        var oversized = await connection.ReceiveAsync();
        Assert.Equal(Protocol64WebSocketReceiveStatus.IgnoredMalformedFrame, oversized.Status);
        Assert.Equal(Protocol64FaultKind.OversizedLength, oversized.Fault!.Kind);
    }

    private static Protocol64WebSocketConnection CreateConnection(
        FakeWebSocket socket,
        Protocol64SchemaRegistry registry,
        ulong epoch,
        Protocol64WebSocketOptions? options = null)
        => new(socket, registry, epoch, options ?? new Protocol64WebSocketOptions { WarningLogger = _ => { } });

    private static Protocol64SchemaRegistry CreateRegistry()
    {
        var registry = new Protocol64SchemaRegistry();
        registry.Register(new TestSchema());
        return registry;
    }

    private static byte[] Encode(
        TestEvent value,
        Protocol64SchemaRegistry registry,
        ulong epoch,
        ulong frameId)
        => Protocol64FrameCodec.Encode(
            registry,
            value,
            epoch,
            frameId,
            new Protocol64FrameEncodeOptions { Compression = Protocol64Compression.None }).Payload!;

    private sealed record TestEvent(string Value);

    [ReliableUnordered(ChannelType.State)]
    private sealed class TestSchema : Protocol64EventSchema<TestEvent>
    {
        public TestSchema()
            : base(500, 1, Protocol64Direction.ServerToClient, maxBodyBytes: 256)
        {
        }

        public override void WriteBody(TestEvent eventValue, BinaryWriter writer)
            => writer.Write(eventValue.Value);

        public override TestEvent ReadBody(BinaryReader reader)
            => new(reader.ReadString());
    }

    private sealed class FakeWebSocket : WebSocket
    {
        private readonly Queue<InboundChunk> _inbound = new();
        private WebSocketState _state = WebSocketState.Open;
        private WebSocketCloseStatus? _closeStatus;
        private string? _closeStatusDescription;

        public List<byte[]> SentMessages { get; } = new();

        public List<bool> SentEndOfMessage { get; } = new();

        public override WebSocketCloseStatus? CloseStatus => _closeStatus;

        public override string? CloseStatusDescription => _closeStatusDescription;

        public override WebSocketState State => _state;

        public override string? SubProtocol => null;

        public void QueueMessage(byte[] payload, WebSocketMessageType messageType = WebSocketMessageType.Binary)
            => QueueFragment(payload, endOfMessage: true, messageType);

        public void QueueFragment(
            byte[] payload,
            bool endOfMessage,
            WebSocketMessageType messageType = WebSocketMessageType.Binary)
            => _inbound.Enqueue(new InboundChunk(payload, endOfMessage, messageType));

        public override void Abort()
            => _state = WebSocketState.Aborted;

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            _closeStatus = closeStatus;
            _closeStatusDescription = statusDescription;
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            _closeStatus = closeStatus;
            _closeStatusDescription = statusDescription;
            _state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_inbound.Count == 0)
            {
                throw new InvalidOperationException("The fake WebSocket has no queued inbound message.");
            }

            var chunk = _inbound.Dequeue();
            if (chunk.Payload.Length > buffer.Count)
            {
                throw new InvalidOperationException("The test chunk must fit in the receive buffer.");
            }

            chunk.Payload.CopyTo(buffer.Array!, buffer.Offset);
            return Task.FromResult(new WebSocketReceiveResult(
                chunk.Payload.Length,
                chunk.MessageType,
                chunk.EndOfMessage));
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SentMessages.Add(buffer.ToArray());
            SentEndOfMessage.Add(endOfMessage);
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _state = WebSocketState.Closed;
        }

        private sealed record InboundChunk(
            byte[] Payload,
            bool EndOfMessage,
            WebSocketMessageType MessageType);
    }
}

internal static class Protocol64WebSocketTestExtensions
{
    public static T As<T>(this object value)
        => Assert.IsType<T>(value);
}
