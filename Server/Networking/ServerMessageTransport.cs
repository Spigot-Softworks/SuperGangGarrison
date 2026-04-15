using System;
using System.Collections.Concurrent;
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

internal interface IServerMessageTransport
{
    bool HasPendingMessages { get; }

    ServerMessagePacket Receive();
    void Send(ServerTransportPeer remotePeer, byte[] payload);
}

internal sealed class UdpServerMessageTransport(UdpClient udp) : IServerMessageTransport
{
    public bool HasPendingMessages => udp.Available > 0;

    public ServerMessagePacket Receive()
    {
        IPEndPoint remoteEndPoint = new(IPAddress.Any, 0);
        var payload = udp.Receive(ref remoteEndPoint);
        return new ServerMessagePacket(ServerTransportPeer.FromUdpEndPoint(remoteEndPoint), payload);
    }

    public void Send(ServerTransportPeer remotePeer, byte[] payload)
    {
        var remoteEndPoint = remotePeer.UdpEndPoint
            ?? throw new InvalidOperationException($"Peer {remotePeer} cannot be addressed by UDP.");
        udp.Send(payload, payload.Length, remoteEndPoint);
    }
}

internal sealed class CompositeServerMessageTransport(UdpClient udp) : IServerMessageTransport
{
    private const int MaxInboundWebSocketPayloadBytes = 64 * 1024;
    private const int MaxReliableQueueDepth = 64;
    private const int MaxReliableQueueBytes = 256 * 1024;

    private static long _nextWebSocketSessionId;

    private readonly UdpServerMessageTransport _udpTransport = new(udp);
    private readonly ConcurrentQueue<ServerMessagePacket> _inboundMessages = new();
    private readonly ConcurrentDictionary<ulong, WebSocketPeerConnection> _webSocketConnections = new();

    public bool HasPendingMessages => !_inboundMessages.IsEmpty || _udpTransport.HasPendingMessages;

    public ServerMessagePacket Receive()
    {
        return _inboundMessages.TryDequeue(out var message)
            ? message
            : _udpTransport.Receive();
    }

    public void Send(ServerTransportPeer remotePeer, byte[] payload)
    {
        if (remotePeer.Kind == ServerTransportKind.Udp)
        {
            _udpTransport.Send(remotePeer, payload);
            return;
        }

        if (_webSocketConnections.TryGetValue(remotePeer.Id, out var connection))
        {
            connection.QueueOutboundPayload(payload);
        }
    }

    public async Task RunWebSocketPeerAsync(WebSocket webSocket, IPAddress? remoteAddress, int remotePort, Action<string> log, CancellationToken cancellationToken)
    {
        var sessionId = Interlocked.Increment(ref _nextWebSocketSessionId);
        var peer = ServerTransportPeer.FromWebSocketSession(sessionId, remoteAddress, remotePort);
        var connection = new WebSocketPeerConnection(peer, webSocket, _inboundMessages, log);
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
        private readonly Action<string> _log;
        private readonly object _outboundGate = new();
        private byte[]? _latestSnapshotPayload;
        private int _queuedReliableBytes;
        private int _disposed;

        public WebSocketPeerConnection(
            ServerTransportPeer peer,
            WebSocket webSocket,
            ConcurrentQueue<ServerMessagePacket> inboundMessages,
            Action<string> log)
        {
            _peer = peer;
            _webSocket = webSocket;
            _inboundMessages = inboundMessages;
            _log = log;
        }

        public void QueueOutboundPayload(byte[] payload)
        {
            if (payload.Length == 0 || Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            if (payload[0] == (byte)MessageType.Snapshot)
            {
                lock (_outboundGate)
                {
                    _latestSnapshotPayload = payload;
                }
                return;
            }

            var queuedBytes = Interlocked.Add(ref _queuedReliableBytes, payload.Length);
            if (queuedBytes > MaxReliableQueueBytes)
            {
                Interlocked.Add(ref _queuedReliableBytes, -payload.Length);
                _log($"[server] dropping reliable WebSocket payload for {_peer}; outbound queue is over budget.");
                return;
            }

            if (!_reliableOutboundPayloads.Writer.TryWrite(payload))
            {
                Interlocked.Add(ref _queuedReliableBytes, -payload.Length);
            }
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
                while (_reliableOutboundPayloads.Reader.TryRead(out var reliablePayload))
                {
                    Interlocked.Add(ref _queuedReliableBytes, -reliablePayload.Length);
                    await SendBinaryMessageAsync(reliablePayload, cancellationToken).ConfigureAwait(false);
                }

                byte[]? snapshotPayload;
                lock (_outboundGate)
                {
                    snapshotPayload = _latestSnapshotPayload;
                    _latestSnapshotPayload = null;
                }

                if (snapshotPayload is not null)
                {
                    await SendBinaryMessageAsync(snapshotPayload, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var waitToReadTask = _reliableOutboundPayloads.Reader.WaitToReadAsync(cancellationToken).AsTask();
                var delayTask = Task.Delay(8, cancellationToken);
                await Task.WhenAny(waitToReadTask, delayTask).ConfigureAwait(false);
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
