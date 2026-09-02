using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public enum Protocol64StateApplyStatus : byte
{
    Applied = 1,
    Stale = 2,
    Rejected = 3,
    RepairRequested = 4,
}

public sealed record Protocol64StateApplyResult(
    Protocol64StateApplyStatus Status,
    Protocol64StateResyncRequest? RepairRequest = null,
    string Reason = "")
{
    public bool Applied => Status == Protocol64StateApplyStatus.Applied;
}

/// <summary>
/// Atomic protocol-64 state view. The applier never resolves gameplay class or
/// projectile kind from a cache: those identities are validated from the event
/// itself and committed together with the rest of the domain record.
/// </summary>
public sealed class Protocol64StateApplier
{
    private readonly Dictionary<(ushort Slot, ulong PlayerId), Protocol64PlayerState> _players = [];
    private readonly Dictionary<(ushort Slot, ulong PlayerId), uint> _removedPlayerGenerations = [];
    private readonly Dictionary<ulong, Protocol64ProjectileState> _projectiles = [];
    private readonly Dictionary<ulong, Protocol64ProjectileIdentity> _removedProjectiles = [];
    private readonly Dictionary<(ushort Slot, ulong PlayerId), uint> _lastWorldPlayers = [];
    private readonly Dictionary<ulong, Protocol64ProjectileState> _lastWorldProjectiles = [];
    private ulong _lastWorldPlayerSequence;
    private ulong _lastWorldRosterSequence;
    private ulong _playerStateSequence;
    private ulong _rosterStateSequence;
    private ulong _projectileStateSequence;
    private uint _lastStateTick;
    private ulong _nextRepairRequestId = 1;
    private readonly HashSet<ulong> _outstandingResyncRequests = [];

    public IReadOnlyCollection<Protocol64PlayerState> Players => _players.Values;

    public IReadOnlyCollection<Protocol64ProjectileState> Projectiles => _projectiles.Values;

    public ulong PlayerStateSequence => _playerStateSequence;

    public ulong RosterStateSequence => _rosterStateSequence;

    public ulong ProjectileStateSequence => _projectileStateSequence;

    public uint LastStateTick => _lastStateTick;

    public bool TryGetPlayerState(byte slot, out Protocol64PlayerState state)
    {
        foreach (var pair in _players)
        {
            if (pair.Key.Slot == slot)
            {
                state = pair.Value;
                return true;
            }
        }

        state = null!;
        return false;
    }

    public void Reset()
    {
        _players.Clear();
        _removedPlayerGenerations.Clear();
        _projectiles.Clear();
        _removedProjectiles.Clear();
        _lastWorldPlayers.Clear();
        _lastWorldProjectiles.Clear();
        _lastWorldPlayerSequence = 0;
        _lastWorldRosterSequence = 0;
        _playerStateSequence = 0;
        _rosterStateSequence = 0;
        _projectileStateSequence = 0;
        _lastStateTick = 0;
        _nextRepairRequestId = 1;
        _outstandingResyncRequests.Clear();
    }

    public Protocol64StateResyncRequest CreateResyncRequest(Protocol64StateResyncReason reason)
    {
        var request = new Protocol64StateResyncRequest(
            _nextRepairRequestId++,
            _playerStateSequence,
            _projectileStateSequence,
            _lastStateTick,
            reason);
        _outstandingResyncRequests.Add(request.RequestId);
        return request;
    }

    public Protocol64StateApplyResult ApplyPlayerStateBatch(Protocol64PlayerStateBatch batch)
    {
        if (batch is null || batch.StateSequence == 0)
        {
            return Repair(Protocol64StateResyncReason.InvalidState, "Player state batch identity is invalid.");
        }

        if (batch.StateSequence <= _playerStateSequence)
        {
            return new(Protocol64StateApplyStatus.Stale, Reason: "Player state sequence is not newer.");
        }

        var replacement = new Dictionary<(ushort Slot, ulong PlayerId), Protocol64PlayerState>();
        foreach (var player in batch.Players)
        {
            if (!IsValidPlayer(player))
            {
                return Repair(Protocol64StateResyncReason.InvalidState, "Player state contains an invalid identity or class.");
            }

            var key = (player.Slot, player.PlayerId);
            if (_players.TryGetValue(key, out var previousPlayer)
                && player.Generation == previousPlayer.Generation
                && IsSequenceOlder(player.LastProcessedInputSequence, previousPlayer.LastProcessedInputSequence))
            {
                return new(Protocol64StateApplyStatus.Stale, Reason: "Player input watermark is older than the applied state.");
            }
            if (_removedPlayerGenerations.TryGetValue(key, out var removedGeneration))
            {
                if (player.Generation <= removedGeneration)
                {
                    continue;
                }

                _removedPlayerGenerations.Remove(key);
            }

            if (replacement.TryGetValue(key, out var existing)
                && player.Generation < existing.Generation)
            {
                return new(Protocol64StateApplyStatus.Stale, Reason: "Player generation is older than the applied state.");
            }

            if (replacement.TryGetValue(key, out existing)
                && player.Generation == existing.Generation
                && !string.Equals(player.GameplayClassId, existing.GameplayClassId, StringComparison.Ordinal))
            {
                return Repair(Protocol64StateResyncReason.InvalidState, "Player class identity changed without a generation change.");
            }

            replacement[key] = player;
        }

        _players.Clear();
        foreach (var pair in replacement)
        {
            _players.Add(pair.Key, pair.Value);
        }

        _playerStateSequence = batch.StateSequence;
        _lastStateTick = Math.Max(_lastStateTick, batch.StateTick);
        return new(Protocol64StateApplyStatus.Applied);
    }

    public Protocol64StateApplyResult ApplyRosterState(Protocol64RosterState roster)
    {
        if (roster is null || roster.StateSequence == 0)
        {
            return Repair(Protocol64StateResyncReason.InvalidState, "Roster state identity is invalid.");
        }

        if (roster.StateSequence <= _rosterStateSequence)
        {
            return new(Protocol64StateApplyStatus.Stale, Reason: "Roster sequence is not newer.");
        }

        var replacement = new Dictionary<(ushort Slot, ulong PlayerId), Protocol64PlayerState>(_players);
        var seenPlayers = new HashSet<(ushort Slot, ulong PlayerId)>();
        var seenSlots = new HashSet<ushort>();
        foreach (var player in roster.Players)
        {
            if (!IsValidIdentity(player)
                || !seenPlayers.Add((player.Slot, player.PlayerId))
                || !seenSlots.Add(player.Slot))
            {
                return Repair(Protocol64StateResyncReason.InvalidState, "Roster contains an invalid or duplicate player slot or identity.");
            }

            var playerKey = (player.Slot, player.PlayerId);
            if (_removedPlayerGenerations.TryGetValue(playerKey, out var removedGeneration)
                && player.Generation > removedGeneration)
            {
                _removedPlayerGenerations.Remove(playerKey);
            }
        }

        var seenRemoved = new HashSet<(ushort Slot, ulong PlayerId)>();
        foreach (var removed in roster.RemovedPlayers)
        {
            if (!IsValidIdentity(removed)
                || !seenRemoved.Add((removed.Slot, removed.PlayerId))
                || !seenSlots.Add(removed.Slot))
            {
                return Repair(Protocol64StateResyncReason.InvalidState, "Roster contains an invalid or duplicate removal slot or identity.");
            }

            var key = (removed.Slot, removed.PlayerId);
            if (!_removedPlayerGenerations.TryGetValue(key, out var knownRemovedGeneration)
                || removed.Generation > knownRemovedGeneration)
            {
                _removedPlayerGenerations[key] = removed.Generation;
            }

            if (replacement.TryGetValue(key, out var existing)
                && existing.Generation == removed.Generation)
            {
                replacement.Remove(key);
            }
        }

        _players.Clear();
        foreach (var pair in replacement)
        {
            _players.Add(pair.Key, pair.Value);
        }

        _rosterStateSequence = roster.StateSequence;
        _lastStateTick = Math.Max(_lastStateTick, roster.StateTick);
        return new(Protocol64StateApplyStatus.Applied);
    }

    public Protocol64StateApplyResult ApplyProjectileState(Protocol64ProjectileState state)
    {
        if (state is null || state.EntityId == 0 || state.Generation == 0 || !Enum.IsDefined(state.EntityKind))
        {
            return Repair(Protocol64StateResyncReason.InvalidState, "Projectile state identity is invalid.");
        }

        if (_projectiles.TryGetValue(state.EntityId, out var existing))
        {
            if (state.Generation < existing.Generation || state.StateTick < existing.StateTick)
            {
                return new(Protocol64StateApplyStatus.Stale, Reason: "Projectile state is older than the applied generation/tick.");
            }

            if (state.Generation == existing.Generation && state.EntityKind != existing.EntityKind)
            {
                return Repair(Protocol64StateResyncReason.InvalidState, "Projectile kind changed without a generation change.");
            }
        }

        _projectiles[state.EntityId] = state;
        _removedProjectiles.Remove(state.EntityId);
        _projectileStateSequence = Math.Max(_projectileStateSequence, state.StateTick);
        _lastStateTick = Math.Max(_lastStateTick, state.StateTick);
        return new(Protocol64StateApplyStatus.Applied);
    }

    public Protocol64StateApplyResult ApplyProjectileLifecycle(Protocol64ProjectileLifecycle lifecycle)
    {
        if (lifecycle is null || lifecycle.EntityId == 0 || lifecycle.Generation == 0 || !Enum.IsDefined(lifecycle.EntityKind))
        {
            return Repair(Protocol64StateResyncReason.InvalidState, "Projectile lifecycle identity is invalid.");
        }

        if (lifecycle.Lifecycle == Protocol64ProjectileLifecycleKind.Despawn)
        {
            _removedProjectiles[lifecycle.EntityId] = new(
                lifecycle.EntityId,
                lifecycle.Generation,
                lifecycle.EntityKind);
            if (_projectiles.TryGetValue(lifecycle.EntityId, out var existing)
                && existing.Generation == lifecycle.Generation
                && existing.EntityKind == lifecycle.EntityKind)
            {
                _projectiles.Remove(lifecycle.EntityId);
            }

            _lastStateTick = Math.Max(_lastStateTick, lifecycle.StateTick);
            return new(Protocol64StateApplyStatus.Applied);
        }

        return ApplyProjectileState(new Protocol64ProjectileState(
            lifecycle.EntityId,
            lifecycle.Generation,
            lifecycle.EntityKind,
            lifecycle.StateTick,
            lifecycle.OwnerSlot,
            lifecycle.OwnerGeneration,
            lifecycle.X,
            lifecycle.Y,
            lifecycle.VelocityX,
            lifecycle.VelocityY,
            lifecycle.Rotation,
            lifecycle.IsActive,
            lifecycle.RemainingLifetimeTicks,
            lifecycle.Damage,
            lifecycle.IsCritical,
            lifecycle.LastToDieSpyRevolverProfile,
            lifecycle.AppliesLastToDieLuckyStrikeStun,
            lifecycle.ArrowFakeSpeedMultiplier,
            lifecycle.IsArrowLanded,
            lifecycle.AppliesLastToDieGuardian,
            lifecycle.PiercesPlayers,
            lifecycle.AppliesLastToDieTranqDarts,
            lifecycle.LastToDiePoisonDamagePerSecond,
            lifecycle.LastToDieGhostDamageMultiplier,
            lifecycle.AppliesLastToDieDecapitator,
            lifecycle.IsLastToDieDecapitatorFullyCharged,
            lifecycle.LastToDieAttachedHeadClassId,
            lifecycle.LastToDieAttachedHeadTeam,
            lifecycle.AppliesLastToDieExplosiveTip,
            lifecycle.LastToDieMedicKritzM2Payload,
            lifecycle.LastToDieMedicJavelinOwnerPlayerId,
            lifecycle.LastToDieMedicJavelinTeam,
            lifecycle.IsLastToDieMedicJavelinAnchored,
            lifecycle.LastToDieMedicJavelinFuseTicksRemaining,
            lifecycle.HasLastToDieMedicJavelinExploded,
            lifecycle.CriticalDamageMultiplier,
            lifecycle.PlayerKnockbackImpulse,
            lifecycle.PlayerKnockbackAirborneVerticalScale,
            lifecycle.PlayerKnockbackGroundedVerticalScale));
    }

    public Protocol64StateApplyResult ApplyResyncResponse(Protocol64StateResyncResponse response)
    {
        if (response is null || response.RequestId == 0 || response.StateSequence == 0)
        {
            return Repair(Protocol64StateResyncReason.InvalidState, "State resync response identity is invalid.");
        }

        if (!_outstandingResyncRequests.Contains(response.RequestId))
        {
            return Repair(Protocol64StateResyncReason.InvalidState, "State resync response does not match an outstanding request.");
        }

        if (response.StateTick < _lastStateTick || response.StateSequence < _playerStateSequence)
        {
            return new(Protocol64StateApplyStatus.Stale, Reason: "State resync response would roll back a newer state view.");
        }

        var nextPlayers = new Dictionary<(ushort Slot, ulong PlayerId), Protocol64PlayerState>();
        var nextPlayerSlots = new HashSet<ushort>();
        foreach (var player in response.Players)
        {
            if (!IsValidPlayer(player))
            {
                return Repair(Protocol64StateResyncReason.InvalidState, "State resync contains an invalid player record.");
            }

            var key = (player.Slot, player.PlayerId);
            if (_players.TryGetValue(key, out var previousPlayer)
                && player.Generation == previousPlayer.Generation
                && IsSequenceOlder(player.LastProcessedInputSequence, previousPlayer.LastProcessedInputSequence))
            {
                return new(Protocol64StateApplyStatus.Stale, Reason: "State resync would roll back a player input watermark.");
            }
            if (!nextPlayers.TryAdd(key, player) || !nextPlayerSlots.Add(player.Slot))
            {
                return Repair(Protocol64StateResyncReason.InvalidState, "State resync contains duplicate player slot or identity.");
            }
        }

        var nextProjectiles = new Dictionary<ulong, Protocol64ProjectileState>();
        foreach (var projectile in response.Projectiles)
        {
            if (projectile.EntityId == 0 || projectile.Generation == 0 || !Enum.IsDefined(projectile.EntityKind))
            {
                return Repair(Protocol64StateResyncReason.InvalidState, "State resync contains an invalid projectile record.");
            }

            if (!nextProjectiles.TryAdd(projectile.EntityId, projectile))
            {
                return Repair(Protocol64StateResyncReason.InvalidState, "State resync contains duplicate projectile identity.");
            }
        }

        _players.Clear();
        _removedPlayerGenerations.Clear();
        foreach (var pair in nextPlayers)
        {
            _players.Add(pair.Key, pair.Value);
        }

        _projectiles.Clear();
        _removedProjectiles.Clear();
        foreach (var pair in nextProjectiles)
        {
            _projectiles.Add(pair.Key, pair.Value);
        }

        _playerStateSequence = response.StateSequence;
        _rosterStateSequence = response.StateSequence;
        _projectileStateSequence = response.StateSequence;
        _lastStateTick = response.StateTick;
        _outstandingResyncRequests.Remove(response.RequestId);
        _lastWorldPlayerSequence = 0;
        _lastWorldRosterSequence = 0;
        _lastWorldProjectiles.Clear();
        return new(Protocol64StateApplyStatus.Applied);
    }

    /// <summary>
    /// Commits the validated protocol-64 view into the live simulation world.
    /// Parsing and freshness stay in this class; this is the explicit bridge
    /// that prevents a decoded state from remaining stranded in a network-only
    /// cache.
    /// </summary>
    public void ApplyToWorld(SimulationWorld world)
    {
        if (world is null)
        {
            throw new ArgumentNullException(nameof(world));
        }

        if (_lastWorldRosterSequence != _rosterStateSequence)
        {
            foreach (var removed in _removedPlayerGenerations)
            {
                world.RemoveProtocol64Player(new Protocol64PlayerIdentity(
                    removed.Key.Slot,
                    removed.Key.PlayerId,
                    removed.Value));
            }

            _lastWorldRosterSequence = _rosterStateSequence;
        }

        if (_lastWorldPlayerSequence != _playerStateSequence)
        {
            foreach (var previous in _lastWorldPlayers)
            {
                if (!_players.ContainsKey(previous.Key))
                {
                    world.RemoveProtocol64Player(new Protocol64PlayerIdentity(
                        previous.Key.Slot,
                        previous.Key.PlayerId,
                        previous.Value));
                }
            }

            foreach (var player in _players.Values)
            {
                world.ApplyProtocol64PlayerState(player);
            }

            _lastWorldPlayers.Clear();
            foreach (var player in _players)
            {
                _lastWorldPlayers[player.Key] = player.Value.Generation;
            }

            _lastWorldPlayerSequence = _playerStateSequence;
        }

        foreach (var removed in _removedProjectiles.Keys)
        {
            world.RemoveProtocol64Projectile(removed);
            _lastWorldProjectiles.Remove(removed);
        }

        foreach (var projectile in _projectiles.Values)
        {
            if (_lastWorldProjectiles.TryGetValue(projectile.EntityId, out var applied)
                && applied.Equals(projectile))
            {
                continue;
            }

            if (world.ApplyProtocol64ProjectileState(projectile))
            {
                _lastWorldProjectiles[projectile.EntityId] = projectile;
            }
        }
    }

    private Protocol64StateApplyResult Repair(Protocol64StateResyncReason reason, string message)
        => new(Protocol64StateApplyStatus.RepairRequested, CreateResyncRequest(reason), message);

    private static bool IsValidPlayer(Protocol64PlayerState? player)
        => player is not null
            && player.PlayerId != 0
            && player.Generation != 0
            && player.Slot < 64
            && !string.IsNullOrWhiteSpace(player.GameplayClassId)
            && player.Health >= 0
            && player.MaxHealth > 0
            && player.Health <= player.MaxHealth
            && player.RemainingAirJumps >= 0
            && float.IsFinite(player.X)
            && float.IsFinite(player.Y)
            && float.IsFinite(player.VelocityX)
            && float.IsFinite(player.VelocityY);

    private static bool IsValidIdentity(Protocol64PlayerIdentity? identity)
        => identity is not null
            && identity.PlayerId != 0
            && identity.Generation != 0
            && identity.Slot < 64;

    private static bool IsSequenceOlder(uint candidate, uint baseline)
    {
        if (candidate == baseline || baseline == 0)
        {
            return false;
        }

        if (candidate == 0)
        {
            return true;
        }

        var difference = unchecked(candidate - baseline);
        return difference >= 0x80000000u;
    }
}
