using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server.LastToDie;

internal sealed record LastToDieCommandHandlingResult(
    LastToDieCommandResultMessage Result,
    bool StateChanged,
    bool Cached = false);

/// <summary>
/// Binds authenticated server slots to the authoritative LTD director. Transport
/// peers never supply a player identity; the dispatcher supplies the owning slot.
/// </summary>
internal sealed class LastToDieProtocolController
{
    private const int MaximumCachedCommandsPerPlayer = 128;

    private sealed record CachedCommand(
        LastToDieCommandMessage Command,
        LastToDieCommandResultMessage Result);

    private sealed class PlayerBinding(byte slot, Guid playerId, bool isHost)
    {
        public byte Slot { get; } = slot;

        public Guid PlayerId { get; } = playerId;

        public bool IsHost { get; set; } = isHost;

        public bool IsConnected { get; set; } = true;

        public ulong ContentReadyStageInstanceId { get; set; }

        public ulong BaselineReadyStageInstanceId { get; set; }

        public Dictionary<ulong, CachedCommand> CachedCommands { get; } = [];

        public Queue<ulong> CachedCommandOrder { get; } = [];

        public ulong LastAcknowledgedStructuralRevision { get; set; }
    }

    private readonly LastToDieDirector _director;
    private readonly Dictionary<byte, PlayerBinding> _playersBySlot = [];
    private readonly Dictionary<Guid, PlayerBinding> _playersById = [];
    private ulong _barrierStageInstanceId;
    private ulong _baselineStartFrame;

    public LastToDieProtocolController(LastToDieServerDirector serverDirector)
    {
        ArgumentNullException.ThrowIfNull(serverDirector);
        _director = serverDirector.Director;
    }

    public LastToDieDirector Director => _director;

    public bool TryRegisterPlayer(byte slot, Guid playerId, out string error)
    {
        if (slot is < 1 or > 2 || playerId == Guid.Empty)
        {
            return Fail("Last to Die requires player slots 1 or 2 and a non-zero player ID.", out error);
        }

        if (_playersBySlot.TryGetValue(slot, out var existingBySlot))
        {
            if (existingBySlot.PlayerId == playerId)
            {
                error = string.Empty;
                return true;
            }

            return Fail("Last to Die slot is already bound to another player.", out error);
        }

        if (_playersById.ContainsKey(playerId))
        {
            return Fail("Last to Die player ID is already bound to another slot.", out error);
        }

        if (!_director.TryAddPlayer(playerId, out error))
        {
            return false;
        }

        var binding = new PlayerBinding(slot, playerId, isHost: _playersBySlot.Count == 0);
        _playersBySlot.Add(slot, binding);
        _playersById.Add(playerId, binding);
        error = string.Empty;
        return true;
    }

    public LastToDieCommandHandlingResult HandleCommand(
        byte slot,
        LastToDieCommandMessage command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (TryGetCachedCommandResult(slot, command, out var cachedHandling))
        {
            return cachedHandling;
        }

        if (!_playersBySlot.TryGetValue(slot, out var player))
        {
            return Rejected(command.CommandId, "Player slot is not registered with this Last to Die run.");
        }

        LastToDieCommandResultMessage result;
        var revisionBefore = _director.StructuralRevision;
        if (command.CommandId == 0)
        {
            result = CreateRejectedResult(command.CommandId, "Command ID must be non-zero.");
        }
        else if (command.RunId != _director.RunId)
        {
            result = CreateRejectedResult(command.CommandId, "Command belongs to another Last to Die run.");
        }
        else if (!CanApplyAtRevision(command, revisionBefore))
        {
            result = CreateRejectedResult(
                command.CommandId,
                $"Expected structural revision {command.ExpectedStructuralRevision}, but the server is at {revisionBefore}.");
        }
        else
        {
            var accepted = TryApplyCommand(player, command, out var error);
            result = new LastToDieCommandResultMessage(
                command.CommandId,
                accepted ? LastToDieCommandResultKind.Accepted : LastToDieCommandResultKind.Rejected,
                _director.StructuralRevision,
                error);
        }

        Cache(player, command, result);
        return new LastToDieCommandHandlingResult(
            result,
            StateChanged: revisionBefore != _director.StructuralRevision
                || command.Kind == LastToDieCommandKind.StageContentReady
                    && result.Result == LastToDieCommandResultKind.Accepted);
    }

    public bool TryGetCachedCommandResult(
        byte slot,
        LastToDieCommandMessage command,
        out LastToDieCommandHandlingResult handling)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_playersBySlot.TryGetValue(slot, out var player)
            || !player.CachedCommands.TryGetValue(command.CommandId, out var cached))
        {
            handling = null!;
            return false;
        }

        handling = cached.Command == command
            ? new LastToDieCommandHandlingResult(
                cached.Result,
                StateChanged: false,
                Cached: true)
            : new LastToDieCommandHandlingResult(
                new LastToDieCommandResultMessage(
                    command.CommandId,
                    LastToDieCommandResultKind.Duplicate,
                    _director.StructuralRevision,
                    "Command ID was already used for a different Last to Die command."),
                StateChanged: false,
                Cached: true);
        return true;
    }

    public bool TrySetPlayerConnected(byte slot, bool isConnected, out string error)
    {
        if (!_playersBySlot.TryGetValue(slot, out var player))
        {
            return Fail("Player slot is not registered with this Last to Die run.", out error);
        }

        var wasConnected = player.IsConnected;
        player.IsConnected = isConnected;
        if (wasConnected != isConnected && _director.Phase == LastToDiePhase.Lobby)
        {
            _director.TrySetLobbyReady(player.PlayerId, isReady: false, out _);
        }
        if (isConnected
            && !wasConnected
            && _director.Phase == LastToDiePhase.LoadingStage
            && _barrierStageInstanceId != 0)
        {
            player.ContentReadyStageInstanceId = 0;
            player.BaselineReadyStageInstanceId = 0;
            _director.TrySetStageUnready(player.PlayerId, out _);
        }
        if (!isConnected
            && _director.Phase == LastToDiePhase.LoadingStage
            && _barrierStageInstanceId != 0)
        {
            player.ContentReadyStageInstanceId = _barrierStageInstanceId;
            player.BaselineReadyStageInstanceId = _barrierStageInstanceId;
            _director.TrySetStageReady(player.PlayerId, out _);
        }

        error = string.Empty;
        return true;
    }

    public bool TryRemoveDisconnectedLobbyPlayer(byte slot, out string error)
    {
        if (_director.Phase != LastToDiePhase.Lobby)
        {
            return Fail("Disconnected lobby seats can only expire before the run starts.", out error);
        }

        if (!_playersBySlot.TryGetValue(slot, out var player))
        {
            return Fail("Player slot is not registered with this Last to Die run.", out error);
        }

        if (player.IsConnected)
        {
            return Fail("A connected Last to Die lobby player cannot be expired.", out error);
        }

        return TryLeavePlayer(player, out error);
    }

    public bool TryOpenStageBarrier(
        ulong stageInstanceId,
        ulong baselineStartFrame,
        out string error)
    {
        var snapshot = _director.CreateSnapshot();
        if (_director.Phase != LastToDiePhase.LoadingStage
            || stageInstanceId == 0
            || stageInstanceId != snapshot.StageInstanceId
            || baselineStartFrame == 0)
        {
            return Fail("Stage barrier identity and baseline frame must match the loading stage.", out error);
        }

        if (_barrierStageInstanceId == stageInstanceId
            && _baselineStartFrame == baselineStartFrame)
        {
            error = string.Empty;
            return true;
        }

        _barrierStageInstanceId = stageInstanceId;
        _baselineStartFrame = baselineStartFrame;
        foreach (var player in _playersBySlot.Values)
        {
            player.ContentReadyStageInstanceId = 0;
            player.BaselineReadyStageInstanceId = 0;
            if (!player.IsConnected)
            {
                player.ContentReadyStageInstanceId = stageInstanceId;
                player.BaselineReadyStageInstanceId = stageInstanceId;
                _director.TrySetStageReady(player.PlayerId, out _);
            }
        }

        error = string.Empty;
        return true;
    }

    public bool TryAcknowledgeWorldBaseline(
        byte slot,
        ulong snapshotFrame,
        out bool stateChanged,
        out string error)
    {
        stateChanged = false;
        if (!_playersBySlot.TryGetValue(slot, out var player))
        {
            return Fail("Player slot is not registered with this Last to Die run.", out error);
        }

        if (_director.Phase != LastToDiePhase.LoadingStage
            || _barrierStageInstanceId == 0
            || _baselineStartFrame == 0
            || snapshotFrame < _baselineStartFrame)
        {
            return Fail("World snapshot acknowledgement does not satisfy the active stage baseline.", out error);
        }

        if (player.BaselineReadyStageInstanceId == _barrierStageInstanceId)
        {
            error = string.Empty;
            return true;
        }

        player.BaselineReadyStageInstanceId = _barrierStageInstanceId;
        stateChanged = true;
        PromoteStageReady(player);
        error = string.Empty;
        return true;
    }

    public ulong BarrierStageInstanceId => _barrierStageInstanceId;

    public ulong BaselineStartFrame => _baselineStartFrame;

    public bool TryAcknowledgeSnapshot(
        byte slot,
        LastToDieRunSnapshotAckMessage acknowledgement,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        if (!_playersBySlot.TryGetValue(slot, out var player))
        {
            return Fail("Player slot is not registered with this Last to Die run.", out error);
        }

        if (acknowledgement.RunId != _director.RunId)
        {
            return Fail("Snapshot acknowledgement belongs to another Last to Die run.", out error);
        }

        if (acknowledgement.StructuralRevision == 0
            || acknowledgement.StructuralRevision > _director.StructuralRevision)
        {
            return Fail("Snapshot acknowledgement revision is invalid.", out error);
        }

        player.LastAcknowledgedStructuralRevision = Math.Max(
            player.LastAcknowledgedStructuralRevision,
            acknowledgement.StructuralRevision);
        error = string.Empty;
        return true;
    }

    public ulong GetLastAcknowledgedStructuralRevision(byte slot)
        => _playersBySlot.TryGetValue(slot, out var player)
            ? player.LastAcknowledgedStructuralRevision
            : 0;

    public LastToDieRunSnapshotMessage CreateSnapshot(byte recipientSlot, long serverTick)
    {
        if (serverTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(serverTick));
        }

        if (!_playersBySlot.ContainsKey(recipientSlot))
        {
            throw new InvalidOperationException(
                "Cannot create a Last to Die snapshot for an unregistered player slot.");
        }

        var snapshot = _director.CreateSnapshot();
        var players = snapshot.Players
            .Select(player => CreatePlayerSnapshot(recipientSlot, player))
            .OrderBy(player => player.Slot)
            .ToArray();
        return new LastToDieRunSnapshotMessage(
            snapshot.RunId,
            snapshot.StructuralRevision,
            snapshot.Seed,
            snapshot.RulesetVersion,
            (LastToDieWireDifficulty)snapshot.Difficulty,
            (LastToDieWirePhase)snapshot.Phase,
            serverTick,
            snapshot.StageNumber,
            snapshot.StageInstanceId,
            snapshot.CurrentMap,
            snapshot.EnemyCount,
            snapshot.StageEndServerTick,
            snapshot.RunEndServerTick,
            players,
            snapshot.TerminalReason,
            _baselineStartFrame,
            (byte)_director.MaximumPlayers);
    }

    private bool TryApplyCommand(
        PlayerBinding player,
        LastToDieCommandMessage command,
        out string error)
    {
        switch (command.Kind)
        {
            case LastToDieCommandKind.RequestStart:
                if (!player.IsHost)
                {
                    return Fail("Only the Last to Die host can start the run.", out error);
                }

                if (_playersBySlot.Count != _director.MaximumPlayers
                    || _playersBySlot.Values.Any(candidate => !candidate.IsConnected))
                {
                    return Fail("Every lobby seat must be connected before the host can start.", out error);
                }

                return _director.TryStart(out error, requireReadyRoster: true);

            case LastToDieCommandKind.ChooseSurvivor:
                return _director.TrySelectSurvivor(
                    player.PlayerId,
                    new LastToDieSurvivorId(command.SelectedId),
                    out error);

            case LastToDieCommandKind.SelectReward:
                return _director.TrySelectReward(
                    player.PlayerId,
                    command.OfferId,
                    new LastToDiePerkId(command.SelectedId),
                    out error);

            case LastToDieCommandKind.StageContentReady:
                var snapshot = _director.CreateSnapshot();
                if (command.StageInstanceId == 0
                    || command.StageInstanceId != snapshot.StageInstanceId
                    || command.StageInstanceId != _barrierStageInstanceId
                    || _baselineStartFrame == 0)
                {
                    return Fail("Stage-ready command belongs to another stage instance.", out error);
                }

                if (!string.Equals(command.SelectedId, snapshot.CurrentMap, StringComparison.OrdinalIgnoreCase))
                {
                    return Fail("Stage-ready command does not match the loaded map.", out error);
                }

                player.ContentReadyStageInstanceId = command.StageInstanceId;
                PromoteStageReady(player);
                error = string.Empty;
                return true;

            case LastToDieCommandKind.Ready:
                return _director.Phase == LastToDiePhase.Lobby
                    ? _director.TrySetLobbyReady(player.PlayerId, isReady: true, out error)
                    : Fail("Stage readiness requires the content-ready command and world baseline acknowledgement.", out error);

            case LastToDieCommandKind.Unready:
                if (_director.Phase == LastToDiePhase.Lobby)
                {
                    return _director.TrySetLobbyReady(player.PlayerId, isReady: false, out error);
                }

                return _director.Phase == LastToDiePhase.SurvivorChoice
                    ? _director.TryClearSurvivor(player.PlayerId, out error)
                    : Fail("Unready is not supported after the stage-load barrier begins.", out error);

            case LastToDieCommandKind.Leave:
                return TryLeavePlayer(player, out error);

            case LastToDieCommandKind.Retry:
                if (_playersBySlot.Count != _director.MaximumPlayers
                    || _playersBySlot.Values.Any(candidate => !candidate.IsConnected))
                {
                    return Fail("Every Last to Die player must be connected before retrying.", out error);
                }

                return _director.TrySetRetryReady(player.PlayerId, out error);

            case LastToDieCommandKind.ReturnToLobby:
                return _director.TryReturnToLobby(out error);

            default:
                return Fail("Unknown Last to Die command kind.", out error);
        }
    }

    private bool CanApplyAtRevision(LastToDieCommandMessage command, ulong currentRevision)
    {
        if (command.ExpectedStructuralRevision == currentRevision)
        {
            return true;
        }

        if (command.ExpectedStructuralRevision > currentRevision)
        {
            return false;
        }

        return command.Kind switch
        {
            LastToDieCommandKind.ChooseSurvivor
                => _director.Phase == LastToDiePhase.SurvivorChoice,
            LastToDieCommandKind.SelectReward
                => _director.Phase == LastToDiePhase.RewardChoice,
            LastToDieCommandKind.Ready
                => _director.Phase == LastToDiePhase.Lobby,
            LastToDieCommandKind.Unready
                => _director.Phase is LastToDiePhase.Lobby or LastToDiePhase.SurvivorChoice,
            LastToDieCommandKind.StageContentReady
                => _director.Phase == LastToDiePhase.LoadingStage
                    && command.StageInstanceId != 0
                    && command.StageInstanceId == _director.CreateSnapshot().StageInstanceId
                    && command.StageInstanceId == _barrierStageInstanceId,
            LastToDieCommandKind.Retry
                => _director.Phase == LastToDiePhase.Lost,
            LastToDieCommandKind.ReturnToLobby
                => _director.Phase is LastToDiePhase.Won or LastToDiePhase.Lost,
            _ => false,
        };
    }

    private LastToDiePlayerSnapshotMessage CreatePlayerSnapshot(
        byte recipientSlot,
        LastToDiePlayerSnapshot player)
    {
        var binding = _playersById[player.PlayerId];
        var offer = binding.Slot == recipientSlot ? player.ActiveOffer : null;
        return new LastToDiePlayerSnapshotMessage(
            binding.Slot,
            player.PlayerId,
            binding.IsConnected,
            player.SurvivorId?.Value ?? string.Empty,
            player.OwnedPerks.Select(perk => perk.Value).ToArray(),
            offer?.OfferId ?? 0,
            offer?.DraftOrdinal ?? 0,
            offer?.Choices.Select(perk => perk.Value).ToArray() ?? [],
            player.IsReady,
            player.IsAlive,
            player.Kills,
            binding.IsHost,
            player.ConquistadorStacks);
    }

    private void PromoteStageReady(PlayerBinding player)
    {
        if (player.ContentReadyStageInstanceId == _barrierStageInstanceId
            && player.BaselineReadyStageInstanceId == _barrierStageInstanceId)
        {
            _director.TrySetStageReady(player.PlayerId, out _);
        }
    }

    private bool TryLeavePlayer(PlayerBinding player, out string error)
    {
        if (!_director.TryRemovePlayer(player.PlayerId, out error))
        {
            return false;
        }

        _playersBySlot.Remove(player.Slot);
        _playersById.Remove(player.PlayerId);
        if (player.IsHost && _playersBySlot.Count > 0)
        {
            _playersBySlot.Values.OrderBy(candidate => candidate.Slot).First().IsHost = true;
        }

        error = string.Empty;
        return true;
    }

    private LastToDieCommandHandlingResult Rejected(ulong commandId, string reason)
        => new(CreateRejectedResult(commandId, reason), StateChanged: false);

    private LastToDieCommandResultMessage CreateRejectedResult(ulong commandId, string reason)
        => new(
            commandId,
            LastToDieCommandResultKind.Rejected,
            _director.StructuralRevision,
            reason);

    private static void Cache(
        PlayerBinding player,
        LastToDieCommandMessage command,
        LastToDieCommandResultMessage result)
    {
        player.CachedCommands.Add(command.CommandId, new CachedCommand(command, result));
        player.CachedCommandOrder.Enqueue(command.CommandId);
        while (player.CachedCommandOrder.Count > MaximumCachedCommandsPerPlayer)
        {
            player.CachedCommands.Remove(player.CachedCommandOrder.Dequeue());
        }
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
