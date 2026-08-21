using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server.LastToDie;

internal sealed record LastToDieNetworkParticipant(
    byte Slot,
    Guid PlayerId,
    string SurvivorId,
    IReadOnlyList<string> OwnedPerkIds,
    bool IsConnected,
    bool IsAlive,
    int ConquistadorStacks);

/// <summary>
/// Server messaging boundary for one LTD run. GameServer owns its lifetime and
/// invokes it from authenticated dispatcher callbacks and the server tick loop.
/// </summary>
internal sealed class LastToDieNetworkSession(
    LastToDieProtocolController controller,
    Func<long> serverTick,
    Action<ServerTransportPeer, IProtocolMessage> sendMessage,
    int ticksPerSecond = SimulationConfig.DefaultTicksPerSecond,
    Func<byte, bool>? consumeAfterlifeDisconnectFailure = null,
    Action<ClientSession, string>? disconnectClient = null)
{
    private const int MaximumCommandsPerWindow = 32;
    private const int ReconnectGraceSeconds = 30;
    private const int LoadingTimeoutSeconds = 60;
    private readonly Dictionary<byte, ClientSession> _clientsBySlot = [];
    private readonly Dictionary<byte, Guid> _playerIdsBySlot = [];
    private readonly Dictionary<byte, Guid> _clientInstanceIdsBySlot = [];
    private readonly Dictionary<byte, Queue<long>> _commandTicksBySlot = [];
    private readonly Dictionary<byte, long> _reconnectGraceEndTicksBySlot = [];
    private readonly HashSet<byte> _aliveReconnectReservations = [];
    private readonly HashSet<byte> _rebindInputSuppressedSlots = [];
    private readonly Func<byte, bool> _consumeAfterlifeDisconnectFailure =
        consumeAfterlifeDisconnectFailure ?? (_ => false);
    private readonly int _commandWindowTicks = Math.Max(1, ticksPerSecond);
    private readonly int _snapshotHeartbeatTicks = Math.Max(1, ticksPerSecond);
    private readonly int _reconnectGraceTicks = Math.Max(1, ticksPerSecond * ReconnectGraceSeconds);
    private readonly int _loadingTimeoutTicks = Math.Max(1, ticksPerSecond * LoadingTimeoutSeconds);
    private readonly Action<ClientSession, string>? _disconnectClient = disconnectClient;
    private long _lastSnapshotHeartbeatTick = -1;
    private ulong _loadingStageInstanceId;
    private long _loadingDeadlineTick;

    public LastToDieProtocolController Controller { get; } =
        controller ?? throw new ArgumentNullException(nameof(controller));

    public bool TryRegisterClient(ClientSession client, Guid playerId, out string error)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (!client.IsAuthorized)
        {
            error = "Client must be authorized before joining Last to Die.";
            return false;
        }

        if (client.ClientInstanceId != Guid.Empty)
        {
            if (_clientInstanceIdsBySlot.TryGetValue(client.Slot, out var boundClientInstanceId)
                && boundClientInstanceId != client.ClientInstanceId)
            {
                error = "Last to Die reconnect identity does not own this slot.";
                return false;
            }

            if (_clientInstanceIdsBySlot.Any(entry =>
                    entry.Key != client.Slot
                    && entry.Value == client.ClientInstanceId))
            {
                error = "Last to Die reconnect identity is already bound to another slot.";
                return false;
            }
        }

        var isLogicalRebind = _playerIdsBySlot.TryGetValue(client.Slot, out var existingPlayerId)
            && existingPlayerId == playerId;
        if (!Controller.TryRegisterPlayer(client.Slot, playerId, out error))
        {
            return false;
        }

        _clientsBySlot[client.Slot] = client;
        _playerIdsBySlot[client.Slot] = playerId;
        if (client.ClientInstanceId != Guid.Empty)
        {
            _clientInstanceIdsBySlot[client.Slot] = client.ClientInstanceId;
        }
        Controller.TrySetPlayerConnected(client.Slot, isConnected: true, out _);
        RestoreReservedPlayerLife(client.Slot);
        if (isLogicalRebind && Controller.Director.Phase == LastToDiePhase.Playing)
        {
            // A replacement transport cannot drive the retained survivor until
            // it has both the current semantic run state and a post-map world
            // baseline. Delayed packets from the old peer cannot satisfy these
            // acknowledgements because dispatcher ownership is peer-bound.
            _rebindInputSuppressedSlots.Add(client.Slot);
        }

        SendSnapshot(client);
        error = string.Empty;
        return true;
    }

    public byte ResolveReconnectSlot(Guid clientInstanceId)
    {
        if (clientInstanceId == Guid.Empty)
        {
            return 0;
        }

        foreach (var entry in _clientInstanceIdsBySlot)
        {
            if (entry.Value == clientInstanceId)
            {
                return entry.Key;
            }
        }

        return 0;
    }

    public bool HasActiveReconnectGrace()
    {
        var tick = serverTick();
        return _aliveReconnectReservations.Any(slot =>
            _reconnectGraceEndTicksBySlot.TryGetValue(slot, out var deadline)
            && tick < deadline);
    }

    public IReadOnlyList<byte> SynchronizeAuthorizedClients(
        IEnumerable<ClientSession> clients,
        Action<ClientSession, string>? rejectClient = null)
    {
        ArgumentNullException.ThrowIfNull(clients);
        var eligible = clients
            .Where(client => client.IsAuthorized
                && !client.IsWatchOnly
                && SimulationWorld.IsPlayableNetworkPlayerSlot(client.Slot))
            .OrderByDescending(client => client.IsLoopbackConnection)
            .ThenBy(client => client.ConnectedAt)
            .ThenBy(client => client.Slot)
            .ToDictionary(client => client.Slot);

        var rosterChanged = false;
        var reboundSlots = new List<byte>();

        foreach (var slot in _clientsBySlot.Keys.Where(slot => !eligible.ContainsKey(slot)).ToArray())
        {
            DisconnectClientSlot(slot);
            rosterChanged = true;
        }

        foreach (var client in eligible.Values)
        {
            if (_clientsBySlot.TryGetValue(client.Slot, out var registered)
                && registered.Peer == client.Peer)
            {
                continue;
            }

            if (registered is not null)
            {
                DisconnectClientSlot(client.Slot);
            }

            var playerId = GetOrCreatePlayerId(client.Slot);
            if (TryRegisterClient(client, playerId, out var registrationError))
            {
                reboundSlots.Add(client.Slot);
                rosterChanged = true;
            }
            else
            {
                rejectClient?.Invoke(client, registrationError);
            }
        }

        if (rosterChanged)
        {
            BroadcastSnapshots();
        }

        return reboundSlots;
    }

    public bool ShouldRetainPlayableSlot(byte slot)
        => _playerIdsBySlot.ContainsKey(slot);

    public bool CanAcceptGameplayInput(byte slot)
    {
        if (Controller.Director.Phase != LastToDiePhase.Playing
            || !_clientsBySlot.TryGetValue(slot, out var client)
            || !client.IsAuthorized)
        {
            return false;
        }

        if (!_rebindInputSuppressedSlots.Contains(slot))
        {
            return true;
        }

        var baselineStartFrame = Controller.BaselineStartFrame;
        if (baselineStartFrame == 0
            || client.LastAcknowledgedSnapshotFrame < baselineStartFrame
            || Controller.GetLastAcknowledgedStructuralRevision(slot)
                < Controller.Director.StructuralRevision)
        {
            return false;
        }

        _rebindInputSuppressedSlots.Remove(slot);
        return true;
    }

    public IReadOnlyList<LastToDieNetworkParticipant> GetParticipants()
    {
        var playersById = Controller.Director.CreateSnapshot().Players
            .ToDictionary(player => player.PlayerId);
        return _playerIdsBySlot
            .OrderBy(entry => entry.Key)
            .Where(entry => playersById.ContainsKey(entry.Value))
            .Select(entry =>
            {
                var player = playersById[entry.Value];
                return new LastToDieNetworkParticipant(
                    entry.Key,
                    entry.Value,
                    player.SurvivorId?.Value ?? string.Empty,
                    player.OwnedPerks.Select(perk => perk.Value).ToArray(),
                    _clientsBySlot.ContainsKey(entry.Key),
                    player.IsAlive,
                    player.ConquistadorStacks);
            })
            .ToArray();
    }

    public void HandleCommand(ClientSession client, LastToDieCommandMessage command)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(command);
        if (Controller.TryGetCachedCommandResult(client.Slot, command, out var cached))
        {
            // Application retries are idempotent and do not consume command
            // rate budget or amplify into redundant snapshot sends.
            sendMessage(client.Peer, cached.Result);
            return;
        }

        if (!TryConsumeCommandBudget(client.Slot))
        {
            sendMessage(
                client.Peer,
                new LastToDieCommandResultMessage(
                    command.CommandId,
                    LastToDieCommandResultKind.Rejected,
                    Controller.Director.StructuralRevision,
                    "Last to Die command rate limit exceeded."));
            return;
        }

        var handling = Controller.HandleCommand(client.Slot, command);
        sendMessage(client.Peer, handling.Result);
        if (command.Kind == LastToDieCommandKind.Leave
            && handling.Result.Result == LastToDieCommandResultKind.Accepted)
        {
            PermanentlyReleaseClient(client, "left the Last to Die run");
            BroadcastSnapshots();
            return;
        }

        if (handling.StateChanged)
        {
            BroadcastSnapshots();
        }
        else if (_clientsBySlot.ContainsKey(client.Slot))
        {
            SendSnapshot(client);
        }
    }

    private void PermanentlyReleaseClient(ClientSession client, string reason)
    {
        var slot = client.Slot;
        _clientsBySlot.Remove(slot);
        _playerIdsBySlot.Remove(slot);
        _clientInstanceIdsBySlot.Remove(slot);
        _commandTicksBySlot.Remove(slot);
        _aliveReconnectReservations.Remove(slot);
        _reconnectGraceEndTicksBySlot.Remove(slot);
        _rebindInputSuppressedSlots.Remove(slot);
        _disconnectClient?.Invoke(client, reason);
    }

    public void HandleSnapshotAck(
        ClientSession client,
        LastToDieRunSnapshotAckMessage acknowledgement)
    {
        ArgumentNullException.ThrowIfNull(client);
        Controller.TryAcknowledgeSnapshot(client.Slot, acknowledgement, out _);
    }

    public void BroadcastSnapshots()
    {
        foreach (var client in _clientsBySlot.Values.OrderBy(client => client.Slot))
        {
            if (client.IsAuthorized)
            {
                SendSnapshot(client);
            }
        }
    }

    public bool TryTriggerStageVictoryForTesting(out string error)
    {
        if (!Controller.Director.TryAdvancePlayingState(
                serverTick(),
                redObjectiveWon: true,
                blueObjectiveWon: false,
                anyAfterlifeWindowActive: false,
                out error))
        {
            return false;
        }

        if (Controller.Director.Phase is not (LastToDiePhase.RewardChoice or LastToDiePhase.Won))
        {
            error = "The current Last to Die run state could not be converted into a stage victory.";
            return false;
        }

        BroadcastSnapshots();
        error = string.Empty;
        return true;
    }

    public void ResendUnacknowledgedSnapshots()
    {
        var revision = Controller.Director.StructuralRevision;
        foreach (var client in _clientsBySlot.Values.OrderBy(client => client.Slot))
        {
            if (client.IsAuthorized
                && Controller.GetLastAcknowledgedStructuralRevision(client.Slot) < revision)
            {
                SendSnapshot(client);
            }
        }
    }

    public bool TryOpenStageBarrier(ulong stageInstanceId, ulong baselineStartFrame, out string error)
    {
        if (!Controller.TryOpenStageBarrier(stageInstanceId, baselineStartFrame, out error))
        {
            return false;
        }

        BroadcastSnapshots();
        return true;
    }

    public bool Tick()
    {
        var tick = serverTick();
        var stateChanged = ExpireReconnectReservations(tick);
        if (Controller.Director.Phase == LastToDiePhase.LoadingStage)
        {
            var loadingSnapshot = Controller.Director.CreateSnapshot();
            if (_loadingStageInstanceId != loadingSnapshot.StageInstanceId)
            {
                _loadingStageInstanceId = loadingSnapshot.StageInstanceId;
                _loadingDeadlineTick = checked(tick + _loadingTimeoutTicks);
            }
        }
        else
        {
            _loadingStageInstanceId = 0;
            _loadingDeadlineTick = 0;
        }

        if (Controller.Director.Phase == LastToDiePhase.LoadingStage
            && Controller.BaselineStartFrame != 0)
        {
            foreach (var client in _clientsBySlot.Values)
            {
                if (client.IsAuthorized
                    && client.LastAcknowledgedSnapshotFrame >= Controller.BaselineStartFrame
                    && Controller.TryAcknowledgeWorldBaseline(
                        client.Slot,
                        client.LastAcknowledgedSnapshotFrame,
                        out var playerStateChanged,
                        out _))
                {
                    stateChanged |= playerStateChanged;
                }
            }

            var snapshot = Controller.Director.CreateSnapshot();
            var connectedPlayerIds = _clientsBySlot.Keys
                .Select(slot => snapshot.Players.FirstOrDefault(player =>
                    _playerIdsBySlot.TryGetValue(slot, out var playerId)
                    && player.PlayerId == playerId))
                .Where(player => player is not null)
                .Select(player => player!.PlayerId)
                .ToHashSet();
            if (connectedPlayerIds.Count > 0
                && snapshot.Players
                    .Where(player => connectedPlayerIds.Contains(player.PlayerId))
                    .All(player => player.IsReady)
                && Controller.Director.TryBeginStage(tick, out _))
            {
                MarkDisconnectedPlayersDeadForActiveStage(tick);
                stateChanged = true;
            }
        }

        if (Controller.Director.Phase == LastToDiePhase.LoadingStage
            && _loadingDeadlineTick > 0
            && tick >= _loadingDeadlineTick
            && ResolveLoadingStageTimeout(tick))
        {
            stateChanged = true;
        }

        if (stateChanged)
        {
            BroadcastSnapshots();
        }
        else if (_lastSnapshotHeartbeatTick < 0
            || tick - _lastSnapshotHeartbeatTick >= _snapshotHeartbeatTicks)
        {
            // The heartbeat also advances the replicated server clock for HUD
            // timers after the current structural revision has been ACKed.
            BroadcastSnapshots();
            _lastSnapshotHeartbeatTick = tick;
        }

        return stateChanged;
    }

    private bool ResolveLoadingStageTimeout(long tick)
    {
        var snapshot = Controller.Director.CreateSnapshot();
        var playersById = snapshot.Players.ToDictionary(player => player.PlayerId);
        var timedOutClients = _clientsBySlot
            .Where(entry =>
                _playerIdsBySlot.TryGetValue(entry.Key, out var playerId)
                && playersById.TryGetValue(playerId, out var player)
                && !player.IsReady)
            .Select(entry => entry.Value)
            .ToArray();
        foreach (var client in timedOutClients)
        {
            DisconnectClientSlot(client.Slot);
            _disconnectClient?.Invoke(client, "Last to Die stage synchronization timed out");
        }

        snapshot = Controller.Director.CreateSnapshot();
        var connectedPlayerIds = _clientsBySlot.Keys
            .Where(_playerIdsBySlot.ContainsKey)
            .Select(slot => _playerIdsBySlot[slot])
            .ToHashSet();
        if (connectedPlayerIds.Count > 0
            && snapshot.Players
                .Where(player => connectedPlayerIds.Contains(player.PlayerId))
                .All(player => player.IsReady)
            && Controller.Director.TryBeginStage(tick, out _))
        {
            MarkDisconnectedPlayersDeadForActiveStage(tick);
            return true;
        }

        return Controller.Director.TryFailRun(
            "Stage synchronization timed out.",
            out _);
    }

    private void DisconnectClientSlot(byte slot)
    {
        _clientsBySlot.Remove(slot);
        _commandTicksBySlot.Remove(slot);
        if (Controller.Director.Phase == LastToDiePhase.Playing)
        {
            _rebindInputSuppressedSlots.Add(slot);
        }

        var afterlifeDisconnectFailure = _consumeAfterlifeDisconnectFailure(slot);
        if (_playerIdsBySlot.TryGetValue(slot, out var playerId))
        {
            var player = Controller.Director.CreateSnapshot().Players
                .FirstOrDefault(candidate => candidate.PlayerId == playerId);
            if (player is { IsAlive: true } && !afterlifeDisconnectFailure)
            {
                _aliveReconnectReservations.Add(slot);
                _reconnectGraceEndTicksBySlot[slot] = checked(serverTick() + _reconnectGraceTicks);
            }
            else
            {
                _aliveReconnectReservations.Remove(slot);
                _reconnectGraceEndTicksBySlot.Remove(slot);
            }

            if (Controller.Director.Phase == LastToDiePhase.Playing)
            {
                Controller.Director.TrySetPlayerAlive(playerId, isAlive: false, out _);
            }
        }

        Controller.TrySetPlayerConnected(slot, isConnected: false, out _);
    }

    private void RestoreReservedPlayerLife(byte slot)
    {
        var tick = serverTick();
        var canRestore = _aliveReconnectReservations.Contains(slot)
            && _reconnectGraceEndTicksBySlot.TryGetValue(slot, out var deadline)
            && tick < deadline;
        _aliveReconnectReservations.Remove(slot);
        _reconnectGraceEndTicksBySlot.Remove(slot);
        if (!canRestore
            || Controller.Director.Phase != LastToDiePhase.Playing
            || !_playerIdsBySlot.TryGetValue(slot, out var playerId))
        {
            return;
        }

        Controller.Director.TrySetPlayerAlive(playerId, isAlive: true, out _);
    }

    private void MarkDisconnectedPlayersDeadForActiveStage(long tick)
    {
        var snapshot = Controller.Director.CreateSnapshot();
        foreach (var entry in _playerIdsBySlot)
        {
            if (_clientsBySlot.ContainsKey(entry.Key))
            {
                continue;
            }

            var player = snapshot.Players.FirstOrDefault(candidate => candidate.PlayerId == entry.Value);
            if (player is not { IsAlive: true })
            {
                continue;
            }

            _aliveReconnectReservations.Add(entry.Key);
            _reconnectGraceEndTicksBySlot[entry.Key] = checked(tick + _reconnectGraceTicks);
            Controller.Director.TrySetPlayerAlive(entry.Value, isAlive: false, out _);
        }
    }

    private bool ExpireReconnectReservations(long tick)
    {
        var stateChanged = false;
        foreach (var entry in _reconnectGraceEndTicksBySlot
                     .Where(entry => tick >= entry.Value)
                     .ToArray())
        {
            _reconnectGraceEndTicksBySlot.Remove(entry.Key);
            _aliveReconnectReservations.Remove(entry.Key);
            if (Controller.Director.Phase == LastToDiePhase.Lobby
                && !_clientsBySlot.ContainsKey(entry.Key)
                && Controller.TryRemoveDisconnectedLobbyPlayer(entry.Key, out _))
            {
                _playerIdsBySlot.Remove(entry.Key);
                _clientInstanceIdsBySlot.Remove(entry.Key);
                _commandTicksBySlot.Remove(entry.Key);
                _rebindInputSuppressedSlots.Remove(entry.Key);
                stateChanged = true;
            }
        }

        return stateChanged;
    }

    public LastToDieRunSnapshotMessage CreateSnapshot(byte recipientSlot)
    {
        var snapshot = Controller.CreateSnapshot(recipientSlot, serverTick());
        var players = snapshot.Players
            .Select(player => player with
            {
                ReconnectGraceEndServerTick = _reconnectGraceEndTicksBySlot
                    .GetValueOrDefault(player.Slot),
            })
            .ToArray();
        return snapshot with { Players = players };
    }

    private void SendSnapshot(ClientSession client)
        => sendMessage(client.Peer, CreateSnapshot(client.Slot));

    private Guid GetOrCreatePlayerId(byte slot)
    {
        if (_playerIdsBySlot.TryGetValue(slot, out var playerId))
        {
            return playerId;
        }

        Span<byte> bytes = stackalloc byte[16];
        Controller.Director.RunId.TryWriteBytes(bytes);
        bytes[0] ^= slot;
        bytes[15] ^= 0xA5;
        playerId = new Guid(bytes);
        _playerIdsBySlot.Add(slot, playerId);
        return playerId;
    }

    private bool TryConsumeCommandBudget(byte slot)
    {
        var tick = serverTick();
        if (!_commandTicksBySlot.TryGetValue(slot, out var commandTicks))
        {
            commandTicks = new Queue<long>();
            _commandTicksBySlot.Add(slot, commandTicks);
        }

        while (commandTicks.Count > 0 && tick - commandTicks.Peek() >= _commandWindowTicks)
        {
            commandTicks.Dequeue();
        }

        if (commandTicks.Count >= MaximumCommandsPerWindow)
        {
            return false;
        }

        commandTicks.Enqueue(tick);
        return true;
    }
}
