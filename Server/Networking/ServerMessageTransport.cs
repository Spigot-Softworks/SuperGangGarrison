using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using OpenGarrison.Networking;
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
        int remotePort,
        bool isProtocol64 = false)
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
        IsProtocol64 = isProtocol64;
    }

    public ServerTransportKind Kind { get; }
    public ulong Id { get; }
    public string Description { get; }
    public IPEndPoint? UdpEndPoint { get; }
    public IPAddress? RemoteAddress { get; }
    public int RemotePort { get; }
    public bool IsProtocol64 { get; }

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

    public static ServerTransportPeer FromWebSocketSession(long sessionId, IPAddress? remoteAddress, int remotePort, bool protocol64 = false)
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
            remotePort,
            protocol64);
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
    void SendProtocol64(
        ServerTransportPeer remotePeer,
        byte[] payload,
        Protocol64DeliveryDescriptor delivery,
        string? replacementKey = null);
}

internal sealed class UdpServerMessageTransport : IServerMessageTransport
{
    private readonly UdpClient _udp;
    private readonly ServerTransportDiagnosticsAccumulator _diagnostics;
    private readonly UdpFragmentSendCache _fragmentSendCache = new();
    private readonly UdpFragmentReassembler _fragmentReassembler = new();
    private readonly UdpFragmentReassembler _preAdmissionFragmentReassembler = new(
        assemblyLifetimeMilliseconds: 1500,
        maxAssemblies: 512,
        maxAssembliesPerPeer: 4,
        maxBufferedBytes: 2 * 1024 * 1024,
        maxBufferedBytesPerPeer: 16 * 1024,
        maxCompletedIds: 512);
    private readonly UdpRepairPacer _repairPacer = new();
    private readonly UdpPathMtuDiscovery _pathMtu = new();
    private readonly ConcurrentDictionary<string, (IPEndPoint EndPoint, long LastSeen)> _knownPeers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (IPEndPoint EndPoint, long LastSeen)> _repairPeers = new(StringComparer.Ordinal);
    private readonly object _unadmittedProbeGate = new();
    private readonly Dictionary<string, long> _lastUnadmittedProbeAck = new(StringComparer.Ordinal);
    private const int MaxKnownUdpPeers = 256;
    private const long KnownUdpPeerLifetimeMilliseconds = 10000;
    private const int MaxUdpRepairPeers = 512;
    private const int MaxUnadmittedProbeSources = 256;
    private const long UnadmittedProbeAckIntervalMilliseconds = 1000;
    private const long UnadmittedProbeStateLifetimeMilliseconds = 10000;
    private const int MaxUnadmittedHelloBytes = 4096;

    public UdpServerMessageTransport(UdpClient udp, ServerTransportDiagnosticsAccumulator diagnostics)
    {
        _udp = udp;
        _diagnostics = diagnostics;
        EnableDontFragmentWhenSupported(udp.Client);
    }

    internal int KnownPeerCount => _knownPeers.Count;

    public bool HasPendingMessages
    {
        get
        {
            if (_udp.Available == 0)
            {
                ServiceDueFragmentNacks();
                ServiceDueRepairs();
                ServiceDuePathMtuProbes();
            }
            return _udp.Available > 0;
        }
    }

    public ServerMessagePacket Receive()
    {
        IPEndPoint remoteEndPoint = new(IPAddress.Any, 0);
        var payload = _udp.Receive(ref remoteEndPoint);
        var peer = ServerTransportPeer.FromUdpEndPoint(remoteEndPoint);
        var peerKey = remoteEndPoint.ToString();
        if (!UdpFragmentation.IsFramed(payload))
            return new ServerMessagePacket(peer, payload);
        if (!UdpFragmentation.TryDecode(payload, out var frame))
            return new ServerMessagePacket(peer, []);
        var now = Environment.TickCount64;
        if (frame.IsMtuProbe)
        {
            if (TryGetKnownPeer(peerKey, now, out var knownEndpoint))
            {
                var ack = UdpFragmentation.CreateMtuAck(frame.MessageId, frame.TotalLength);
                SendDatagram(knownEndpoint, ack, snapshot: false, bestEffort: true);
            }
            else if ((frame.TotalLength is UdpPathMtuDiscovery.InitialDatagramBytes or UdpPathMtuDiscovery.SmallerConfirmationDatagramBytes)
                && TryAllowUnadmittedProbeAck(peerKey, now))
            {
                var ack = UdpFragmentation.CreateMtuAck(frame.MessageId, frame.TotalLength);
                SendDatagram(remoteEndPoint, ack, snapshot: false, bestEffort: true);
            }
            return new ServerMessagePacket(peer, []);
        }
        if (frame.IsMtuAck)
        {
            if (TryGetKnownPeer(peerKey, now, out _)) _pathMtu.TryAcceptAck(peerKey, frame, now, out _);
            return new ServerMessagePacket(peer, []);
        }
        if (frame.IsNack)
        {
            if (TryGetRepairPeer(peerKey, now, out var repairEndpoint))
            {
                var repairLimit = TryGetKnownPeer(peerKey, now, out _)
                    ? _pathMtu.GetDatagramLimit(peerKey, now)
                    : UdpPathMtuDiscovery.InitialDatagramBytes;
                var repairs = _fragmentSendCache.TryRepair(peerKey, payload, now, repairLimit);
                if (repairs.Count > 0) RememberRepairPeer(peerKey, repairEndpoint, now);
                _repairPacer.Enqueue(peerKey, repairs, now);
            }
            return new ServerMessagePacket(peer, []);
        }

        if (!TryGetKnownPeer(peerKey, now, out _))
        {
            if (frame.TotalLength > MaxUnadmittedHelloBytes
                || !_preAdmissionFragmentReassembler.TryAccept(peerKey, payload, now, out var preAdmissionPayload)
                || preAdmissionPayload is null)
                return new ServerMessagePacket(peer, []);
            if (!ProtocolCodec.TryDeserialize(preAdmissionPayload, out var preAdmissionMessage)
                || preAdmissionMessage is not HelloMessage)
            {
                _preAdmissionFragmentReassembler.ClearPeer(peerKey);
                return new ServerMessagePacket(peer, []);
            }
            _preAdmissionFragmentReassembler.ClearPeer(peerKey);
            return new ServerMessagePacket(peer, preAdmissionPayload);
        }
        if (_fragmentReassembler.TryAccept(peerKey, payload, now, out var completePayload))
            RememberPeer(peerKey, remoteEndPoint, now);
        return new ServerMessagePacket(peer, completePayload ?? []);
    }

    public void Send(ServerTransportPeer remotePeer, byte[] payload, MessageType? messageType = null)
    {
        var remoteEndPoint = remotePeer.UdpEndPoint
            ?? throw new InvalidOperationException($"Peer {remotePeer} cannot be addressed by UDP.");
        var peerKey = remoteEndPoint.ToString();
        var now = Environment.TickCount64;
        var activePeer = IsProbeActivatingMessage(messageType)
            || TryGetKnownPeer(peerKey, now, out _);
        if (activePeer) RememberPeer(peerKey, remoteEndPoint, now);
        var limit = activePeer
            ? _pathMtu.GetDatagramLimit(peerKey, now)
            : UdpPathMtuDiscovery.InitialDatagramBytes;
        if (!activePeer && payload.Length > limit)
        {
            activePeer = true;
            RememberPeer(peerKey, remoteEndPoint, now);
            limit = _pathMtu.GetDatagramLimit(peerKey, now);
        }
        if (payload.Length > limit) RememberRepairPeer(peerKey, remoteEndPoint, now);
        var datagrams = _fragmentSendCache.CacheAndFragment(
            peerKey, payload, now, limit, messageType == MessageType.Snapshot, cacheForRepairs: true);
        try
        {
            foreach (var datagram in datagrams) SendDatagram(remoteEndPoint, datagram, messageType == MessageType.Snapshot);
        }
        finally
        {
            ServiceDuePathMtuProbes();
            ServiceDueRepairs();
        }
    }

    public void SendProtocol64(
        ServerTransportPeer remotePeer,
        byte[] payload,
        Protocol64DeliveryDescriptor delivery,
        string? replacementKey = null)
        => Send(remotePeer, payload, messageType: null);

    private void ServiceDueFragmentNacks()
    {
        var now = Environment.TickCount64;
        foreach (var (peerKey, packet) in _fragmentReassembler.GetDueNacks(now))
            if (TryGetKnownPeer(peerKey, now, out _)) _repairPacer.Enqueue(peerKey, [packet], now);
    }

    private void ServiceDueRepairs()
    {
        var now = Environment.TickCount64;
        if (_repairPacer.TryDequeueDue(now, out var peerKey, out var packet)
            && packet is not null && TryGetRepairPeer(peerKey, now, out var endpoint)
            && UdpFragmentation.TryDecode(packet, out var frame))
        {
            var isCurrent = frame.IsNack
                ? _fragmentReassembler.IsCurrentNack(peerKey, packet, now)
                : _fragmentSendCache.IsCurrentFrame(peerKey, packet, now);
            if (isCurrent) SendDatagram(endpoint, packet, _fragmentSendCache.IsSnapshotFrame(peerKey, packet), bestEffort: true);
        }
    }

    private void ServiceDuePathMtuProbes()
    {
        var now = Environment.TickCount64;
        foreach (var peerKey in _knownPeers.Keys)
        {
            if (!TryGetKnownPeer(peerKey, now, out var endpoint)
                || !_pathMtu.TryCreateProbe(peerKey, now, out var packet) || packet is null) continue;
            SendDatagram(endpoint, packet, snapshot: false, bestEffort: true);
        }
    }

    private void RememberPeer(string peerKey, IPEndPoint endpoint, long nowMilliseconds)
    {
        _knownPeers[peerKey] = (endpoint, nowMilliseconds);
        RememberRepairPeer(peerKey, endpoint, nowMilliseconds);
        foreach (var pair in _knownPeers)
            if (nowMilliseconds - pair.Value.LastSeen >= KnownUdpPeerLifetimeMilliseconds)
            {
                _knownPeers.TryRemove(pair.Key, out _);
                ClearPeerState(pair.Key);
            }
        if (_knownPeers.Count <= MaxKnownUdpPeers) return;
        foreach (var pair in _knownPeers.OrderBy(pair => pair.Value.LastSeen).Take(_knownPeers.Count - MaxKnownUdpPeers))
        {
            _knownPeers.TryRemove(pair.Key, out _);
            ClearPeerState(pair.Key);
        }
    }

    private bool TryGetKnownPeer(string peerKey, long nowMilliseconds, out IPEndPoint endpoint)
    {
        if (_knownPeers.TryGetValue(peerKey, out var peer))
        {
            if (nowMilliseconds - peer.LastSeen < KnownUdpPeerLifetimeMilliseconds)
            {
                endpoint = peer.EndPoint;
                return true;
            }
            _knownPeers.TryRemove(peerKey, out _);
            ClearPeerState(peerKey);
        }
        endpoint = null!;
        return false;
    }

    private void ClearPeerState(string peerKey)
    {
        _pathMtu.ClearPeer(peerKey);
        _fragmentReassembler.ClearPeer(peerKey);
        _preAdmissionFragmentReassembler.ClearPeer(peerKey);
        _fragmentSendCache.ClearPeer(peerKey);
        _repairPacer.ClearPeer(peerKey);
        _repairPeers.TryRemove(peerKey, out _);
    }

    private void RememberRepairPeer(string peerKey, IPEndPoint endpoint, long nowMilliseconds)
    {
        _repairPeers[peerKey] = (endpoint, nowMilliseconds);
        foreach (var pair in _repairPeers)
            if (nowMilliseconds - pair.Value.LastSeen >= UdpFragmentSendCache.CacheLifetimeMilliseconds)
            {
                _repairPeers.TryRemove(pair.Key, out _);
                _fragmentSendCache.ClearPeer(pair.Key);
                _repairPacer.ClearPeer(pair.Key);
            }
        if (_repairPeers.Count <= MaxUdpRepairPeers) return;
        foreach (var pair in _repairPeers.OrderBy(pair => pair.Value.LastSeen).Take(_repairPeers.Count - MaxUdpRepairPeers))
        {
            _repairPeers.TryRemove(pair.Key, out _);
            _fragmentSendCache.ClearPeer(pair.Key);
            _repairPacer.ClearPeer(pair.Key);
        }
    }

    private bool TryGetRepairPeer(string peerKey, long nowMilliseconds, out IPEndPoint endpoint)
    {
        if (_repairPeers.TryGetValue(peerKey, out var peer)
            && nowMilliseconds - peer.LastSeen < UdpFragmentSendCache.CacheLifetimeMilliseconds)
        {
            endpoint = peer.EndPoint;
            return true;
        }
        _repairPeers.TryRemove(peerKey, out _);
        _fragmentSendCache.ClearPeer(peerKey);
        _repairPacer.ClearPeer(peerKey);
        endpoint = null!;
        return false;
    }

    private bool TryAllowUnadmittedProbeAck(string peerKey, long nowMilliseconds)
    {
        lock (_unadmittedProbeGate)
        {
            var expired = new List<string>();
            foreach (var pair in _lastUnadmittedProbeAck)
                if (nowMilliseconds - pair.Value >= UnadmittedProbeStateLifetimeMilliseconds) expired.Add(pair.Key);
            foreach (var key in expired) _lastUnadmittedProbeAck.Remove(key);

            if (_lastUnadmittedProbeAck.TryGetValue(peerKey, out var lastAck)
                && nowMilliseconds - lastAck < UnadmittedProbeAckIntervalMilliseconds)
                return false;
            if (_lastUnadmittedProbeAck.Count >= MaxUnadmittedProbeSources)
            {
                string? oldestPeer = null;
                var oldestTime = long.MaxValue;
                foreach (var pair in _lastUnadmittedProbeAck)
                    if (pair.Value < oldestTime) { oldestPeer = pair.Key; oldestTime = pair.Value; }
                if (oldestPeer is not null) _lastUnadmittedProbeAck.Remove(oldestPeer);
            }
            _lastUnadmittedProbeAck[peerKey] = nowMilliseconds;
            return true;
        }
    }

    private static bool IsProbeActivatingMessage(MessageType? messageType)
        => messageType is MessageType.Welcome or MessageType.PasswordRequest or MessageType.Snapshot;

    private void SendDatagram(IPEndPoint endpoint, byte[] payload, bool snapshot, bool bestEffort = false)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var failed = false;
        try
        {
            _udp.Send(payload, payload.Length, endpoint);
        }
        catch (SocketException) when (bestEffort)
        {
            failed = true;
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            _diagnostics.RecordUdpSend(payload.Length, snapshot, Stopwatch.GetTimestamp() - startTimestamp, failed);
        }
    }

    private static void EnableDontFragmentWhenSupported(Socket socket)
    {
        try
        {
            if (socket.AddressFamily == AddressFamily.InterNetwork) socket.DontFragment = true;
            else if (socket.AddressFamily == AddressFamily.InterNetworkV6)
                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.DontFragment, true);
        }
        catch (SocketException)
        {
            // Some IPv6 stacks do not expose a per-socket no-fragment option.
        }
        catch (NotSupportedException)
        {
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
    private readonly ConcurrentDictionary<ulong, Protocol64WebSocketConnection> _protocol64WebSocketConnections = new();
    private readonly Protocol64SchemaRegistry _protocol64Registry = Protocol64SchemaRegistryFactory.CreateDefault();
    private readonly Action<string> _log;

    public CompositeServerMessageTransport(UdpClient udp, Action<string>? log = null)
    {
        _udpTransport = new UdpServerMessageTransport(udp, _diagnostics);
        _log = log ?? (message => Console.Error.WriteLine(message));
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

    public void SendProtocol64(
        ServerTransportPeer remotePeer,
        byte[] payload,
        Protocol64DeliveryDescriptor delivery,
        string? replacementKey = null)
    {
        if (remotePeer.Kind == ServerTransportKind.Udp)
        {
            _log($"[server] refusing protocol-64 delivery to legacy UDP peer {remotePeer}; the canonical backend is WebSocket.");
            return;
        }

        if (_protocol64WebSocketConnections.TryGetValue(remotePeer.Id, out var protocol64Connection))
        {
            try
            {
                if (delivery.IsLastWins)
                {
                    protocol64Connection.PublishLastWinsFrame(
                        replacementKey ?? "protocol64-state",
                        payload,
                        delivery);
                }
                else
                {
                    protocol64Connection.QueueReliableFrame(payload, delivery);
                }
            }
            catch (Protocol64WebSocketBackpressureException exception)
            {
                _log($"[server] closing protocol-64 WebSocket peer {remotePeer} after explicit backpressure: {exception.Message}");
                _protocol64WebSocketConnections.TryRemove(remotePeer.Id, out _);
                protocol64Connection.Dispose();
            }

            return;
        }

        // During migration, a protocol-64 frame can still be carried by the
        // legacy WebSocket adapter. It remains a complete binary message, but
        // it does not get the canonical container's recovery semantics.
        Send(remotePeer, payload);
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

    public async Task RunProtocol64WebSocketPeerAsync(
        WebSocket webSocket,
        IPAddress? remoteAddress,
        int remotePort,
        Action<string> log,
        CancellationToken cancellationToken,
        ManagedRoomRuntime.Participant? managedParticipant = null)
    {
        var sessionId = Interlocked.Increment(ref _nextWebSocketSessionId);
        var peer = ServerTransportPeer.FromWebSocketSession(sessionId, remoteAddress, remotePort, protocol64: true);
        if (managedParticipant is not null) ManagedRoomRuntime.Participants[peer.Id] = managedParticipant;
        var connection = new Protocol64WebSocketConnection(
            webSocket,
            _protocol64Registry,
            connectionEpoch: peer.Id,
            options: new Protocol64WebSocketOptions
            {
                WarningLogger = log,
                FaultSink = new DelegateProtocol64FaultSink(fault =>
                    log($"[server] protocol-64 WebSocket fault peer={peer}: {fault.Kind} {fault.Message}")),
            });
        if (!_protocol64WebSocketConnections.TryAdd(peer.Id, connection))
        {
            connection.Dispose();
            throw new InvalidOperationException($"A protocol-64 WebSocket peer with id {peer.Id} is already connected.");
        }

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var linkedToken = linkedCts.Token;
            var receiveTask = Task.Run(async () =>
            {
                while (!linkedToken.IsCancellationRequested && !connection.IsDisposed)
                {
                    var result = await connection.ReceiveAsync(linkedToken).ConfigureAwait(false);
                    if (result.Status is Protocol64WebSocketReceiveStatus.Closed
                        or Protocol64WebSocketReceiveStatus.ProtocolError)
                    {
                        break;
                    }

                    if (result.HasFrame && result.Decoded?.Event is not null)
                    {
                        _inboundMessages.Enqueue(new ServerMessagePacket(peer, result.EncodedPayload!));
                    }
                }
            }, linkedToken);
            var sendTask = connection.RunSendLoopAsync(linkedToken);
            await Task.WhenAny(receiveTask, sendTask).ConfigureAwait(false);
            linkedCts.Cancel();
            try
            {
                await Task.WhenAll(receiveTask, sendTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
            {
            }
        }
        finally
        {
            _protocol64WebSocketConnections.TryRemove(peer.Id, out _);
            ManagedRoomRuntime.Participants.TryRemove(peer.Id, out _);
            await connection.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Protocol-64 WebSocket session ended.",
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task RunOutboundProtocol64RelayAsync(
        Uri relayEndpoint,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relayEndpoint);
        if (relayEndpoint.Scheme is not ("ws" or "wss"))
        {
            throw new ArgumentException("The relay endpoint must use ws or wss.", nameof(relayEndpoint));
        }

        var retryDelay = TimeSpan.FromSeconds(1);
        while (!cancellationToken.IsCancellationRequested)
        {
            using var socket = new ClientWebSocket();
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            try
            {
                log($"[server] connecting outbound protocol-64 relay at {RedactRelayEndpoint(relayEndpoint)}");
                await socket.ConnectAsync(relayEndpoint, cancellationToken).ConfigureAwait(false);
                log("[server] outbound protocol-64 relay connected.");
                retryDelay = TimeSpan.FromSeconds(1);
                await RunProtocol64WebSocketPeerAsync(
                    socket,
                    remoteAddress: null,
                    remotePort: relayEndpoint.IsDefaultPort ? 0 : relayEndpoint.Port,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // ClientWebSocket exception messages may echo the requested
                // URI, whose query contains the host bearer token.
                log($"[server] outbound relay unavailable ({exception.GetType().Name}); retrying.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            retryDelay = TimeSpan.FromSeconds(Math.Min(10d, retryDelay.TotalSeconds * 2d));
        }
    }

    internal static string RedactRelayEndpoint(Uri endpoint)
    {
        var builder = new UriBuilder(endpoint)
        {
            Query = string.IsNullOrWhiteSpace(endpoint.Query) ? string.Empty : "token=REDACTED",
            Fragment = string.Empty,
        };
        return builder.Uri.ToString();
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
