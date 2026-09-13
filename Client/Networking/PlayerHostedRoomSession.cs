using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.ClientShared;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.SessionRuntime;

namespace OpenGarrison.Client;

/// <summary>Owns one room. All gameplay work runs on Update; network callbacks only enqueue bounded packets.</summary>
public sealed class PlayerHostedRoomSession : IDisposable
{
    private PeerRoomConnection _room;
    private readonly Dictionary<byte, Link> _links = [];
    private int _generation, _nextEpoch;
    private long _nextPing;
    private bool _disposed;
    public PeerRoomMessage? State { get; private set; }
    public EmbeddedSessionHost? Host { get; private set; }
    public PeerRoomConnection Connection => _room;
    public bool IsOwner => _room.Grant.Slot == 1;
    public event Action<INetworkClientMessageTransport>? ConnectLocal;
    public event Action? ReturnedToLobby;
    public event Action<string>? Failed;
    public event Action<string>? Notice;
    public PlayerHostedRoomSession(PeerRoomConnection room) => _room = room;

    public void ReplaceConnection(PeerRoomConnection room)
    {
        _room.Dispose(); _room = room;
        foreach (var link in _links.Values) link.Dispose();
        _links.Clear();
    }

    public void Update(double elapsed)
    {
        if (_disposed) return;
        try
        {
            while (_room.TryReadMessage(out var message))
            {
                if (message.Type == "closed") { Failed?.Invoke(message.Reason); return; }
                if (message.Type == "state") ApplyState(message);
                else if (message.Type == "signal") ApplySignal(message.Source, message.Data ?? "");
                else if (message.Type == "error") Notice?.Invoke(message.Reason);
                if (_disposed) return;
            }
            while (_room.TryReadPacket(out var source, out var packet))
                if (_links.TryGetValue(source, out var link)) link.Receive(packet);
            FlushReconnectRequest();
            if (Environment.TickCount64 >= _nextPing)
            { _room.Send(new("ping")); _nextPing = Environment.TickCount64 + 10000; }
            foreach (var link in _links.Values.ToArray())
            {
                if (IsOwner && State?.Phase == "Playing" && Host is not null && link.Epoch == 0
                    && (link.Direct?.IsOpen == true || Environment.TickCount64 - link.CreatedAt > 5000))
                    OpenGameLink(link, relay: link.Direct?.IsOpen != true);
                if (link.Epoch > 0 && !link.Relay && (link.Direct?.IsOpen != true || link.SendFailed))
                {
                    if (IsOwner) OpenGameLink(link, relay: true);
                    else RequestReconnect();
                }
                if (link.Peer is not null)
                    while (link.TryReceive(out var payload)) link.Peer.Send(payload);
            }
            Host?.Advance(elapsed);
            foreach (var link in _links.Values)
                if (link.Peer is not null)
                    while (link.Peer.TryReceive(out var payload)) link.Send(payload);
        }
        catch (Exception ex) { Failed?.Invoke("The room stopped: " + ex.Message); }
    }

    private void ApplyState(PeerRoomMessage state)
    {
        if (state.Players is null || state.Settings is null || state.MaximumPlayers != 4) return;
        if (State is not null && state.Revision < State.Revision) return;
        State = state;
        if (_generation != state.Generation)
        {
            Host?.Dispose(); Host = null;
            foreach (var link in _links.Values) link.Reset(0, true);
            _generation = state.Generation;
            _reconnectPending = false; _nextReconnect = 0;
            if (state.Phase == "Lobby") ReturnedToLobby?.Invoke();
            else if (IsOwner)
            {
                var s = state.Settings;
                Host = new EmbeddedSessionHost(new EmbeddedSessionOptions
                {
                    LastToDie = state.Kind == "LastToDie", MaximumPlayers = 4, Name = state.Kind == "Practice" ? "Practice Co-op" : "Last To Die Co-op",
                    PreferClassicMaps = ClientDistribution.IsRestricted,
                    Map = s.Map, MapArea = s.MapArea, TickRate = s.TickRate, TimeLimitMinutes = s.TimeLimitMinutes,
                    CaptureLimit = s.CaptureLimit, RespawnSeconds = s.RespawnSeconds, RedBots = s.RedBots, BlueBots = s.BlueBots,
                    SpecialAbilities = s.SpecialAbilities, Difficulty = s.Difficulty == "hardcore" ? LastToDieDifficulty.Hardcore : LastToDieDifficulty.Standard
                }, requireRoomAdmission: true);
                var owner = state.Players.Single(player => player.Slot == 1);
                ConnectLocal?.Invoke(new EmbeddedSessionClientTransport(Host.CreatePeer(true, 1, Guid.Parse(owner.ClientId))));
            }
        }
        var peers = state.Players.Where(player => player.Connected && player.Slot != _room.Grant.Slot
            && (IsOwner || player.Slot == 1)).ToArray();
        foreach (var slot in _links.Keys.Where(slot => peers.All(player => player.Slot != slot)).ToArray())
        { _links[slot].Dispose(); _links.Remove(slot); }
        foreach (var peer in peers)
        {
            if (_links.TryGetValue(peer.Slot, out var previous) && previous.ClientId != peer.ClientId)
            { previous.Dispose(); _links.Remove(peer.Slot); }
            if (!_links.ContainsKey(peer.Slot))
            {
                var link = new Link(_room, peer.Slot, peer.ClientId);
                _links.Add(peer.Slot, link);
                try { link.Direct = PeerDataConnectionFactory.Create(IsOwner, _room.Grant.IceServers,
                    signal => _room.Send(new("signal", peer.Slot, signal)), link.Receive); }
                catch (Exception) { /* The authenticated room relay remains available. */ }
                if (!IsOwner && state.Phase == "Playing") RequestReconnect();
            }
        }
    }

    private void OpenGameLink(Link link, bool relay)
    {
        if (Host is null || State?.Phase != "Playing") return;
        link.Reset(++_nextEpoch, relay);
        link.Peer = Host.CreatePeer(false, link.Slot, Guid.Parse(link.ClientId));
        _room.Send(new("signal", link.Slot, $"@open:{_generation}:{link.Epoch}:{(relay ? 1 : 0)}"));
    }

    private void ApplySignal(byte source, string signal)
    {
        if (!_links.TryGetValue(source, out var link)) return;
        if (signal.StartsWith("@open:", StringComparison.Ordinal) && !IsOwner && source == 1)
        {
            var fields = signal.Split(':');
            if (fields.Length != 4 || !int.TryParse(fields[1], out var generation) || generation != _generation
                || State?.Phase != "Playing" || !int.TryParse(fields[2], out var epoch) || epoch <= link.Epoch) return;
            link.Reset(epoch, fields[3] == "1");
            _reconnectPending = false;
            ConnectLocal?.Invoke(new RoomGameTransport(link));
        }
        else if (signal == "@reconnect" && IsOwner && State?.Phase == "Playing") OpenGameLink(link, true);
        else if (!signal.StartsWith('@')) link.Direct?.ApplySignal(signal);
    }

    private long _nextReconnect;
    private bool _reconnectPending;
    public void RequestReconnect()
    {
        if (_disposed || IsOwner) return;
        _reconnectPending = true;
        FlushReconnectRequest();
    }
    private void FlushReconnectRequest()
    {
        if (_disposed || IsOwner || !_reconnectPending || State?.Phase != "Playing"
            || Environment.TickCount64 < _nextReconnect) return;
        _nextReconnect = Environment.TickCount64 + 2000;
        _room.Send(new("signal", 1, "@reconnect"));
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var link in _links.Values) link.Dispose();
        _links.Clear(); Host?.Dispose(); Host = null;
        _ = _room.LeaveAsync();
    }

    private sealed class Link(PeerRoomConnection room, byte slot, string clientId) : IDisposable
    {
        private readonly ConcurrentQueue<byte[]> _incoming = new();
        private int _pendingBytes;
        public byte Slot => slot;
        public string ClientId => clientId;
        public long CreatedAt { get; } = Environment.TickCount64;
        public int Epoch { get; private set; }
        public bool Relay { get; private set; } = true;
        public bool SendFailed { get; private set; }
        public bool Closed { get; private set; }
        public bool HasPending => !_incoming.IsEmpty;
        public IPeerDataConnection? Direct;
        public EmbeddedSessionPeer? Peer;
        public void Reset(int epoch, bool relay)
        {
            Epoch = epoch; Relay = relay; SendFailed = false;
            Peer?.Dispose(); Peer = null;
            while (TryReceive(out _)) { }
        }
        public void Receive(byte[] packet)
        {
            if (Closed || packet.Length < 5 || BinaryPrimitives.ReadInt32LittleEndian(packet) != Epoch || Epoch == 0) return;
            if (System.Threading.Interlocked.Add(ref _pendingBytes, packet.Length) > 8 * 1024 * 1024)
            { System.Threading.Interlocked.Add(ref _pendingBytes, -packet.Length); SendFailed = true; return; }
            _incoming.Enqueue(packet);
        }
        public bool TryReceive(out byte[] payload)
        {
            while (_incoming.TryDequeue(out var packet))
            {
                System.Threading.Interlocked.Add(ref _pendingBytes, -packet.Length);
                if (Closed || Epoch == 0 || BinaryPrimitives.ReadInt32LittleEndian(packet) != Epoch) continue;
                payload = packet[4..]; return true;
            }
            payload = []; return false;
        }
        public void Send(byte[] payload)
        {
            if (Closed || Epoch == 0) return;
            var packet = new byte[payload.Length + 4];
            BinaryPrimitives.WriteInt32LittleEndian(packet, Epoch); payload.CopyTo(packet, 4);
            if (Relay) room.SendPacket(slot, packet);
            else if (Direct?.TrySend(packet) != true) SendFailed = true;
        }
        public void Dispose() { Closed = true; Reset(0, true); Direct?.Dispose(); }
    }
    private sealed class RoomGameTransport(Link link) : INetworkClientMessageTransport
    {
        private bool _closed;
        private readonly int _epoch = link.Epoch;
        public bool HasPendingMessages => !_closed && link.Epoch == _epoch && link.HasPending;
        public bool IsLoopbackConnection => false;
        public int ReceiveTimeoutMilliseconds => 30000;
        public string RemoteDescription => "ws64://private-room/game";
        public bool TryReceive(out byte[] payload)
        {
            if (!_closed && link.Epoch == _epoch) return link.TryReceive(out payload);
            payload = []; return false;
        }
        public void Send(byte[] payload) { if (!_closed && link.Epoch == _epoch) link.Send(payload); }
        public bool TryConsumeDisconnectReason(out string reason)
        { reason = "The room connection changed."; return link.Closed || link.Epoch != _epoch; }
        public void Dispose() => _closed = true;
    }
}
