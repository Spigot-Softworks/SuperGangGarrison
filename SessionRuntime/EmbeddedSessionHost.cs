using System.Collections.Concurrent;
using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;

namespace OpenGarrison.SessionRuntime;

/// <summary>One authority, stepped by its owning application's loop. Opens no listener or process.</summary>
public sealed class EmbeddedSessionHost : IDisposable
{
    private readonly GameServer _server;
    private readonly EmbeddedMessageTransport _transport = new();
    private bool _disposed;
    public EmbeddedSessionOptions Options { get; }
    public SimulationWorld World => _server.EmbeddedWorld;

    public EmbeddedSessionHost(EmbeddedSessionOptions options, bool requireRoomAdmission = false)
    {
        options.Validate();
        Options = options;
        _server = new GameServer(options);
        if (requireRoomAdmission) _server.ConfigureEmbeddedAdmissions(peer => _transport.GetAdmission(peer));
        _transport.PeerRemoved = peer => _server.RemoveEmbeddedPeer(peer);
        try { _server.StartEmbedded(_transport, options); }
        catch { _transport.Dispose(); _server.StopEmbedded(); throw; }
    }

    public EmbeddedSessionPeer CreatePeer(bool local = false, byte slot = 0, Guid clientId = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.CreatePeer(local, slot, clientId);
    }

    public void Advance(double elapsedSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _server.AdvanceEmbedded(Math.Clamp(double.IsFinite(elapsedSeconds) ? elapsedSeconds : 0, 0, 0.25));
    }

    public Task<IReadOnlyList<string>> ExecuteHostCommandAsync(string command)
        => _server.ExecuteAdminCommandAsync(command, false, CancellationToken.None);
    public IReadOnlyList<string> ExecuteJukeboxCommand(string command) => _server.EmbeddedJukeboxCommand(command);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _transport.Dispose();
        _server.StopEmbedded();
    }
}

/// <summary>A separate authenticated transport endpoint; queues are bounded even when a guest stalls.</summary>
public sealed class EmbeddedSessionPeer : IDisposable
{
    private const int MaximumPendingBytes = 8 * 1024 * 1024;
    private readonly Queue<byte[]> _outbound = new();
    private readonly EmbeddedMessageTransport _transport;
    private int _pendingBytes;
    internal ServerTransportPeer Identity { get; }
    public bool IsClosed { get; private set; }
    public string CloseReason { get; private set; } = "";
    public bool HasPendingMessages => _outbound.Count != 0;
    internal EmbeddedSessionPeer(EmbeddedMessageTransport transport, ServerTransportPeer identity)
        => (_transport, Identity) = (transport, identity);

    public void Send(byte[] payload)
    {
        if (IsClosed) return;
        if (payload.Length is < 1 or > 4 * 1024 * 1024)
        { Close("Invalid game packet."); return; }
        _transport.Enqueue(this, payload);
    }

    public bool TryReceive(out byte[] payload)
    {
        if (!_outbound.TryDequeue(out payload!)) { payload = []; return false; }
        _pendingBytes -= payload.Length;
        return true;
    }

    internal void Deliver(byte[] payload)
    {
        if (IsClosed) return;
        if (_pendingBytes + payload.Length > MaximumPendingBytes)
        { Close("The connection cannot keep up with the game."); return; }
        _outbound.Enqueue(payload);
        _pendingBytes += payload.Length;
    }

    public void Close(string reason)
    {
        if (IsClosed) return;
        IsClosed = true;
        CloseReason = reason;
        _outbound.Clear();
        _pendingBytes = 0;
        _transport.Remove(this);
    }
    public void Dispose() => Close("Connection closed.");
}

internal sealed class EmbeddedMessageTransport : IServerMessageTransport, IDisposable
{
    public Action<ServerTransportPeer>? PeerRemoved { get; set; }
    private readonly Dictionary<ulong, ManagedRoomRuntime.Participant> _admissions = [];
    public ManagedRoomRuntime.Participant? GetAdmission(ServerTransportPeer peer) => _admissions.GetValueOrDefault(peer.Id);
    private readonly Queue<ServerMessagePacket> _incoming = new();
    private readonly Dictionary<ulong, EmbeddedSessionPeer> _peers = [];
    private ulong _nextPeer = 1;
    private int _pendingBytes;
    public bool HasPendingMessages => _incoming.Count != 0;
    public EmbeddedSessionPeer CreatePeer(bool local, byte slot, Guid clientId)
    {
        if (_peers.Count >= 4) throw new InvalidOperationException("The room is full.");
        var id = _nextPeer++;
        var peer = new EmbeddedSessionPeer(this, new ServerTransportPeer(ServerTransportKind.WebSocket,
            id, $"session:{id}", null, local ? IPAddress.Loopback : null, 0, isProtocol64: true));
        _peers.Add(id, peer);
        if (slot != 0)
        {
            if (slot > 4 || clientId == Guid.Empty) { peer.Dispose(); throw new ArgumentException("Invalid room seat."); }
            _admissions.Add(id, new(clientId, slot));
        }
        return peer;
    }
    public void Enqueue(EmbeddedSessionPeer peer, byte[] payload)
    {
        if (_pendingBytes + payload.Length > 8 * 1024 * 1024)
        { peer.Close("Too many pending game packets."); return; }
        _incoming.Enqueue(new(peer.Identity, payload));
        _pendingBytes += payload.Length;
    }
    public ServerMessagePacket Receive()
    {
        var packet = _incoming.Dequeue();
        _pendingBytes -= packet.Payload.Length;
        return packet;
    }
    public void Send(ServerTransportPeer remotePeer, byte[] payload, MessageType? messageType = null)
    { if (_peers.TryGetValue(remotePeer.Id, out var peer)) peer.Deliver(payload); }
    public void SendProtocol64(ServerTransportPeer remotePeer, byte[] payload,
        Protocol64DeliveryDescriptor delivery, string? replacementKey = null) => Send(remotePeer, payload);
    public void Remove(EmbeddedSessionPeer peer)
    {
        _peers.Remove(peer.Identity.Id);
        _admissions.Remove(peer.Identity.Id);
        var remaining = _incoming.Where(packet => packet.RemotePeer.Id != peer.Identity.Id).ToArray();
        _incoming.Clear();
        _pendingBytes = 0;
        foreach (var packet in remaining) { _incoming.Enqueue(packet); _pendingBytes += packet.Payload.Length; }
        PeerRemoved?.Invoke(peer.Identity);
    }
    public void Dispose()
    {
        foreach (var peer in _peers.Values.ToArray()) peer.Dispose();
        _incoming.Clear();
        _pendingBytes = 0;
    }
}
