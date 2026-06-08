using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal enum ServerTransportKind
{
    Udp = 1,
    WebSocket = 2,
}

internal readonly struct ServerTransportPeer : IEquatable<ServerTransportPeer>
{
    public ServerTransportPeer(
        ServerTransportKind kind,
        ulong id,
        string description,
        IPEndPoint? udpEndPoint,
        IPAddress? remoteAddress,
        int remotePort)
    {
        if (id == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Server transport peer id must not be zero.");
        }

        Kind = kind;
        Id = id;
        Description = string.IsNullOrWhiteSpace(description) ? $"peer:{id}" : description;
        UdpEndPoint = udpEndPoint;
        RemoteAddress = NormalizeAddress(remoteAddress ?? udpEndPoint?.Address);
        RemotePort = remotePort > 0 ? remotePort : udpEndPoint?.Port ?? 0;
    }

    public ServerTransportKind Kind { get; }
    public ulong Id { get; }
    public string Description { get; }
    public IPEndPoint? UdpEndPoint { get; }
    public IPAddress? RemoteAddress { get; }
    public int RemotePort { get; }

    public bool IsLoopback
    {
        get
        {
            var address = RemoteAddress;
            return address is not null && IPAddress.IsLoopback(address);
        }
    }

    public bool Equals(ServerTransportPeer other)
    {
        return Kind == other.Kind && Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is ServerTransportPeer other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Kind, Id);
    }

    public override string ToString()
    {
        return Description;
    }

    public static ServerTransportPeer FromUdpEndPoint(IPEndPoint endPoint)
    {
        ArgumentNullException.ThrowIfNull(endPoint);
        return new ServerTransportPeer(
            ServerTransportKind.Udp,
            CreateUdpPeerId(endPoint),
            endPoint.ToString(),
            endPoint,
            endPoint.Address,
            endPoint.Port);
    }

    public static ServerTransportPeer FromWebSocketSession(long sessionId, IPAddress? remoteAddress, int remotePort)
    {
        var normalizedAddress = NormalizeAddress(remoteAddress);
        var description = normalizedAddress is null
            ? $"ws:unknown#{sessionId}"
            : $"ws:{normalizedAddress}:{remotePort}#{sessionId}";
        return new ServerTransportPeer(
            ServerTransportKind.WebSocket,
            unchecked((ulong)sessionId),
            description,
            udpEndPoint: null,
            normalizedAddress,
            remotePort);
    }

    public static bool operator ==(ServerTransportPeer left, ServerTransportPeer right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ServerTransportPeer left, ServerTransportPeer right)
    {
        return !left.Equals(right);
    }

    private static ulong CreateUdpPeerId(IPEndPoint endPoint)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offsetBasis;
        var addressBytes = endPoint.Address.GetAddressBytes();
        for (var index = 0; index < addressBytes.Length; index += 1)
        {
            hash = (hash ^ addressBytes[index]) * prime;
        }

        hash = (hash ^ (byte)(endPoint.Port & 0xff)) * prime;
        hash = (hash ^ (byte)((endPoint.Port >> 8) & 0xff)) * prime;
        return hash == 0 ? 1 : hash;
    }

    private static IPAddress? NormalizeAddress(IPAddress? address)
    {
        return address?.IsIPv4MappedToIPv6 == true ? address.MapToIPv4() : address;
    }
}

internal readonly record struct ServerMessagePacket(ServerTransportPeer RemotePeer, byte[] Payload);

internal readonly record struct ServerTransportDiagnostics(
    long UdpSendPackets,
    long UdpSendBytes,
    long UdpSnapshotPackets,
    long UdpSnapshotBytes,
    long UdpSendErrors,
    double UdpSendTotalMilliseconds,
    double UdpSendMaxMilliseconds,
    long WebSocketSnapshotQueued,
    long WebSocketSnapshotSent,
    long WebSocketSnapshotOverwritten,
    int WebSocketSnapshotLatestSlotPending,
    double WebSocketSnapshotEnqueueToSendTotalMilliseconds,
    double WebSocketSnapshotEnqueueToSendMaxMilliseconds,
    long WebSocketReliableSent,
    long WebSocketReliableDropped,
    int WebSocketQueuedReliableBytes);

internal sealed class ServerTransportDiagnosticsAccumulator
{
    private long _udpSendPackets;
    private long _udpSendBytes;
    private long _udpSnapshotPackets;
    private long _udpSnapshotBytes;
    private long _udpSendErrors;
    private long _udpSendTotalTicks;
    private long _udpSendMaxTicks;
    private long _webSocketSnapshotQueued;
    private long _webSocketSnapshotSent;
    private long _webSocketSnapshotOverwritten;
    private long _webSocketSnapshotEnqueueToSendTotalTicks;
    private long _webSocketSnapshotEnqueueToSendMaxTicks;
    private long _webSocketReliableSent;
    private long _webSocketReliableDropped;

    public void RecordUdpSend(int payloadBytes, bool snapshot, long elapsedTicks, bool failed)
    {
        Interlocked.Increment(ref _udpSendPackets);
        Interlocked.Add(ref _udpSendBytes, Math.Max(0, payloadBytes));
        Interlocked.Add(ref _udpSendTotalTicks, Math.Max(0L, elapsedTicks));
        UpdateMax(ref _udpSendMaxTicks, Math.Max(0L, elapsedTicks));
        if (snapshot)
        {
            Interlocked.Increment(ref _udpSnapshotPackets);
            Interlocked.Add(ref _udpSnapshotBytes, Math.Max(0, payloadBytes));
        }

        if (failed)
        {
            Interlocked.Increment(ref _udpSendErrors);
        }
    }

    public void RecordWebSocketSnapshotQueued(bool overwrotePendingSnapshot)
    {
        Interlocked.Increment(ref _webSocketSnapshotQueued);
        if (overwrotePendingSnapshot)
        {
            Interlocked.Increment(ref _webSocketSnapshotOverwritten);
        }
    }

    public void RecordWebSocketSnapshotSent(long enqueueToSendTicks)
    {
        var elapsedTicks = Math.Max(0L, enqueueToSendTicks);
        Interlocked.Increment(ref _webSocketSnapshotSent);
        Interlocked.Add(ref _webSocketSnapshotEnqueueToSendTotalTicks, elapsedTicks);
        UpdateMax(ref _webSocketSnapshotEnqueueToSendMaxTicks, elapsedTicks);
    }

    public void RecordWebSocketReliableSent()
    {
        Interlocked.Increment(ref _webSocketReliableSent);
    }

    public void RecordWebSocketReliableDropped()
    {
        Interlocked.Increment(ref _webSocketReliableDropped);
    }

    public ServerTransportDiagnostics Snapshot(int webSocketSnapshotLatestSlotPending, int webSocketQueuedReliableBytes)
    {
        return new ServerTransportDiagnostics(
            UdpSendPackets: Interlocked.Read(ref _udpSendPackets),
            UdpSendBytes: Interlocked.Read(ref _udpSendBytes),
            UdpSnapshotPackets: Interlocked.Read(ref _udpSnapshotPackets),
            UdpSnapshotBytes: Interlocked.Read(ref _udpSnapshotBytes),
            UdpSendErrors: Interlocked.Read(ref _udpSendErrors),
            UdpSendTotalMilliseconds: TicksToMilliseconds(Interlocked.Read(ref _udpSendTotalTicks)),
            UdpSendMaxMilliseconds: TicksToMilliseconds(Interlocked.Read(ref _udpSendMaxTicks)),
            WebSocketSnapshotQueued: Interlocked.Read(ref _webSocketSnapshotQueued),
            WebSocketSnapshotSent: Interlocked.Read(ref _webSocketSnapshotSent),
            WebSocketSnapshotOverwritten: Interlocked.Read(ref _webSocketSnapshotOverwritten),
            WebSocketSnapshotLatestSlotPending: Math.Max(0, webSocketSnapshotLatestSlotPending),
            WebSocketSnapshotEnqueueToSendTotalMilliseconds: TicksToMilliseconds(Interlocked.Read(ref _webSocketSnapshotEnqueueToSendTotalTicks)),
            WebSocketSnapshotEnqueueToSendMaxMilliseconds: TicksToMilliseconds(Interlocked.Read(ref _webSocketSnapshotEnqueueToSendMaxTicks)),
            WebSocketReliableSent: Interlocked.Read(ref _webSocketReliableSent),
            WebSocketReliableDropped: Interlocked.Read(ref _webSocketReliableDropped),
            WebSocketQueuedReliableBytes: Math.Max(0, webSocketQueuedReliableBytes));
    }

    private static void UpdateMax(ref long target, long value)
    {
        var current = Volatile.Read(ref target);
        while (value > current)
        {
            var previous = Interlocked.CompareExchange(ref target, value, current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks <= 0
            ? 0d
            : ticks * 1000d / Stopwatch.Frequency;
    }
}

internal interface IServerMessageTransport
{
    bool HasPendingMessages { get; }

    ServerMessagePacket Receive();
    void Send(ServerTransportPeer remotePeer, byte[] payload, MessageType? messageType = null);
}

internal sealed class UdpServerMessageTransport : IServerMessageTransport
{
    private readonly UdpClient _udp;
    private readonly ServerTransportDiagnosticsAccumulator _diagnostics;

    public UdpServerMessageTransport(UdpClient udp, ServerTransportDiagnosticsAccumulator diagnostics)
    {
        _udp = udp;
        _diagnostics = diagnostics;
    }

    public bool HasPendingMessages => _udp.Available > 0;

    public ServerMessagePacket Receive()
    {
        IPEndPoint remoteEndPoint = new(IPAddress.Any, 0);
        var payload = _udp.Receive(ref remoteEndPoint);
        return new ServerMessagePacket(ServerTransportPeer.FromUdpEndPoint(remoteEndPoint), payload);
    }

    public void Send(ServerTransportPeer remotePeer, byte[] payload, MessageType? messageType = null)
    {
        var remoteEndPoint = remotePeer.UdpEndPoint
            ?? throw new InvalidOperationException($"Peer {remotePeer} cannot be addressed by UDP.");
        var startTimestamp = Stopwatch.GetTimestamp();
        var failed = false;
        try
        {
            _udp.Send(payload, payload.Length, remoteEndPoint);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            _diagnostics.RecordUdpSend(
                payload.Length,
                messageType == MessageType.Snapshot,
                Stopwatch.GetTimestamp() - startTimestamp,
                failed);
        }
    }
}

internal sealed class CompositeServerMessageTransport : IServerMessageTransport
{
    private const int MaxInboundWebSocketPayloadBytes = 64 * 1024;
    private const int MaxReliableQueueDepth = 64;
    private const int MaxReliableQueueBytes = 256 * 1024;
    private const int MaxReliableMessagesBeforeSnapshotCheck = 8;

    private static long _nextWebSocketSessionId;

    private readonly ServerTransportDiagnosticsAccumulator _diagnostics = new();
    private readonly UdpServerMessageTransport _udpTransport;
    private readonly ConcurrentQueue<ServerMessagePacket> _inboundMessages = new();
    private readonly ConcurrentDictionary<ulong, WebSocketPeerConnection> _webSocketConnections = new();

    public CompositeServerMessageTransport(UdpClient udp)
    {
        _udpTransport = new UdpServerMessageTransport(udp, _diagnostics);
    }

    public bool HasPendingMessages => !_inboundMessages.IsEmpty || _udpTransport.HasPendingMessages;

    public ServerTransportDiagnostics Diagnostics
    {
        get
        {
            var latestSnapshotPending = 0;
            var queuedReliableBytes = 0;
            foreach (var connection in _webSocketConnections.Values)
            {
                latestSnapshotPending += connection.HasPendingSnapshot ? 1 : 0;
                queuedReliableBytes += connection.QueuedReliableBytes;
            }

            return _diagnostics.Snapshot(latestSnapshotPending, queuedReliableBytes);
        }
    }

    public ServerMessagePacket Receive()
    {
        return _inboundMessages.TryDequeue(out var message)
            ? message
            : _udpTransport.Receive();
    }

    public void Send(ServerTransportPeer remotePeer, byte[] payload, MessageType? messageType = null)
    {
        if (remotePeer.Kind == ServerTransportKind.Udp)
        {
            _udpTransport.Send(remotePeer, payload, messageType);
            return;
        }

        if (_webSocketConnections.TryGetValue(remotePeer.Id, out var connection))
        {
            connection.QueueOutboundPayload(payload, messageType);
        }
    }

    public async Task RunWebSocketPeerAsync(WebSocket webSocket, IPAddress? remoteAddress, int remotePort, Action<string> log, CancellationToken cancellationToken)
    {
        var sessionId = Interlocked.Increment(ref _nextWebSocketSessionId);
        var peer = ServerTransportPeer.FromWebSocketSession(sessionId, remoteAddress, remotePort);
        var connection = new WebSocketPeerConnection(peer, webSocket, _inboundMessages, _diagnostics, log);
        if (!_webSocketConnections.TryAdd(peer.Id, connection))
        {
            throw new InvalidOperationException($"A WebSocket peer with id {peer.Id} is already connected.");
        }

        try
        {
            await connection.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _webSocketConnections.TryRemove(peer.Id, out _);
            connection.Dispose();
        }
    }

    private sealed class WebSocketPeerConnection : IDisposable
    {
        private readonly ServerTransportPeer _peer;
        private readonly WebSocket _webSocket;
        private readonly ConcurrentQueue<ServerMessagePacket> _inboundMessages;
        private readonly Channel<byte[]> _reliableOutboundPayloads = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(MaxReliableQueueDepth)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
        private readonly Channel<bool> _outboundSignals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });
        private readonly Action<string> _log;
        private readonly ServerTransportDiagnosticsAccumulator _diagnostics;
        private readonly object _outboundGate = new();
        private QueuedSnapshotPayload? _latestSnapshotPayload;
        private int _queuedReliableBytes;
        private int _disposed;

        private readonly record struct QueuedSnapshotPayload(byte[] Payload, long EnqueuedTimestamp);

        public WebSocketPeerConnection(
            ServerTransportPeer peer,
            WebSocket webSocket,
            ConcurrentQueue<ServerMessagePacket> inboundMessages,
            ServerTransportDiagnosticsAccumulator diagnostics,
            Action<string> log)
        {
            _peer = peer;
            _webSocket = webSocket;
            _inboundMessages = inboundMessages;
            _diagnostics = diagnostics;
            _log = log;
        }

        public bool HasPendingSnapshot
        {
            get
            {
                lock (_outboundGate)
                {
                    return _latestSnapshotPayload.HasValue;
                }
            }
        }

        public int QueuedReliableBytes => Math.Max(0, Volatile.Read(ref _queuedReliableBytes));

        public void QueueOutboundPayload(byte[] payload, MessageType? messageType)
        {
            if (payload.Length == 0 || Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            if (messageType == MessageType.Snapshot)
            {
                var overwrotePendingSnapshot = false;
                lock (_outboundGate)
                {
                    overwrotePendingSnapshot = _latestSnapshotPayload.HasValue;
                    _latestSnapshotPayload = new QueuedSnapshotPayload(payload, Stopwatch.GetTimestamp());
                }
                _diagnostics.RecordWebSocketSnapshotQueued(overwrotePendingSnapshot);
                SignalOutboundWriter();
                return;
            }

            var queuedBytes = Interlocked.Add(ref _queuedReliableBytes, payload.Length);
            if (queuedBytes > MaxReliableQueueBytes)
            {
                Interlocked.Add(ref _queuedReliableBytes, -payload.Length);
                _diagnostics.RecordWebSocketReliableDropped();
                _log($"[server] dropping reliable WebSocket payload for {_peer}; outbound queue is over budget.");
                return;
            }

            if (!_reliableOutboundPayloads.Writer.TryWrite(payload))
            {
                Interlocked.Add(ref _queuedReliableBytes, -payload.Length);
                _diagnostics.RecordWebSocketReliableDropped();
                return;
            }

            SignalOutboundWriter();
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var linkedToken = linkedCts.Token;
            var readerTask = RunReaderLoopAsync(linkedToken);
            var writerTask = RunWriterLoopAsync(linkedToken);
            var completedTask = await Task.WhenAny(readerTask, writerTask).ConfigureAwait(false);
            linkedCts.Cancel();

            try
            {
                await completedTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _log($"[server] WebSocket peer {_peer} closed with error: {ex.Message}");
            }

            try
            {
                await Task.WhenAll(readerTask, writerTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
            {
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _reliableOutboundPayloads.Writer.TryComplete();
            _outboundSignals.Writer.TryComplete();
            _webSocket.Dispose();
        }

        private async Task RunReaderLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[16 * 1024];
            while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                using var messageStream = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    if (result.MessageType != WebSocketMessageType.Binary)
                    {
                        throw new InvalidOperationException($"Non-binary WebSocket message from peer {_peer}.");
                    }

                    if (messageStream.Length + result.Count > MaxInboundWebSocketPayloadBytes)
                    {
                        throw new InvalidOperationException($"WebSocket payload from peer {_peer} exceeded {MaxInboundWebSocketPayloadBytes} bytes.");
                    }

                    messageStream.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                var payload = messageStream.ToArray();
                if (payload.Length > 0)
                {
                    _inboundMessages.Enqueue(new ServerMessagePacket(_peer, payload));
                }
            }
        }

        private async Task RunWriterLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                if (TryTakeLatestSnapshotPayload() is { } pendingSnapshotPayload)
                {
                    await SendBinaryMessageAsync(pendingSnapshotPayload.Payload, cancellationToken).ConfigureAwait(false);
                    _diagnostics.RecordWebSocketSnapshotSent(Stopwatch.GetTimestamp() - pendingSnapshotPayload.EnqueuedTimestamp);
                    continue;
                }

                var reliableMessagesSent = 0;
                while (reliableMessagesSent < MaxReliableMessagesBeforeSnapshotCheck
                    && _reliableOutboundPayloads.Reader.TryRead(out var reliablePayload))
                {
                    Interlocked.Add(ref _queuedReliableBytes, -reliablePayload.Length);
                    await SendBinaryMessageAsync(reliablePayload, cancellationToken).ConfigureAwait(false);
                    _diagnostics.RecordWebSocketReliableSent();
                    reliableMessagesSent += 1;

                    if (TryTakeLatestSnapshotPayload() is { } snapshotPayloadAfterReliable)
                    {
                        await SendBinaryMessageAsync(snapshotPayloadAfterReliable.Payload, cancellationToken).ConfigureAwait(false);
                        _diagnostics.RecordWebSocketSnapshotSent(Stopwatch.GetTimestamp() - snapshotPayloadAfterReliable.EnqueuedTimestamp);
                        break;
                    }
                }

                if (reliableMessagesSent > 0)
                {
                    continue;
                }

                await WaitForOutboundSignalAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private void SignalOutboundWriter()
        {
            _outboundSignals.Writer.TryWrite(true);
        }

        private async ValueTask WaitForOutboundSignalAsync(CancellationToken cancellationToken)
        {
            if (!await _outboundSignals.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            while (_outboundSignals.Reader.TryRead(out _))
            {
            }
        }

        private QueuedSnapshotPayload? TryTakeLatestSnapshotPayload()
        {
            lock (_outboundGate)
            {
                var snapshotPayload = _latestSnapshotPayload;
                _latestSnapshotPayload = null;
                return snapshotPayload;
            }
        }

        private Task SendBinaryMessageAsync(byte[] payload, CancellationToken cancellationToken)
        {
            return _webSocket.SendAsync(
                payload,
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken);
        }
    }
}
