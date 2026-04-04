#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int LastToDieBotReactionFrameZ1 = 20;
    private const int LastToDieBotReactionFrameZ2 = 21;
    private const int LastToDieBotReactionFrameZ4 = 23;
    private const int LastToDieBotReactionFrameZ5 = 24;
    private const int LastToDieBotReactionFrameZ6 = 25;
    private const int LastToDieBotReactionFrameZ7 = 26;
    private const int LastToDieBotReactionFrameZ9 = 28;
    private const int LastToDieBotReactionFrameMedic = 45;
    private const float LastToDieBotReactionAwarenessDistance = 900f;
    private const float LastToDieBotReactionTeammateDeathDistance = 420f;
    private const float LastToDieBotReactionLowHealthFraction = 0.35f;
    private const float LastToDieBotReactionLowHealthRecoveryFraction = 0.55f;

    private sealed class LastToDieBotReactionState
    {
        public bool WasAlive { get; set; }

        public int LastKnownHealth { get; set; }

        public Vector2 LastKnownPosition { get; set; }

        public bool SawLocalPlayerLastTick { get; set; }

        public bool LowHealthCallIssued { get; set; }

        public long LastDamageTakenFrame { get; set; }

        public long LastReactionFrame { get; set; } = long.MinValue / 4;

        public long LastIdleReactionFrame { get; set; } = long.MinValue / 4;
    }

    private readonly Dictionary<byte, LastToDieBotReactionState> _lastToDieBotReactionStates = new();
    private PlayerClass? _lastToDieObservedBotReactionAcquiredWeaponClassId;
    private int _lastToDieObservedBotReactionLocalMultiKillCount;
    private bool _lastToDieObservedBotReactionLocalRaging;
    private bool _lastToDieObservedBotReactionCaptureCelebrated;

    private readonly record struct LastToDieObservedBotDeath(Vector2 Position, byte Slot);

    private void ResetLastToDieBotReactionState()
    {
        _lastToDieBotReactionStates.Clear();
        _lastToDieObservedBotReactionAcquiredWeaponClassId = null;
        _lastToDieObservedBotReactionLocalMultiKillCount = 0;
        _lastToDieObservedBotReactionLocalRaging = false;
        _lastToDieObservedBotReactionCaptureCelebrated = false;
    }

    private void ObserveLastToDieBotReactionState()
    {
        if (!IsLastToDieSessionActive || _world.LocalPlayerAwaitingJoin)
        {
            _lastToDieObservedBotReactionAcquiredWeaponClassId = null;
            _lastToDieObservedBotReactionLocalMultiKillCount = 0;
            _lastToDieObservedBotReactionLocalRaging = false;
            _lastToDieObservedBotReactionCaptureCelebrated = false;
            return;
        }

        _lastToDieObservedBotReactionAcquiredWeaponClassId = _world.LocalPlayer.AcquiredWeaponClassId;
        _lastToDieObservedBotReactionLocalMultiKillCount = _world.LocalPlayer.CurrentMultiKillCount;
        _lastToDieObservedBotReactionLocalRaging = _world.LocalPlayer.IsRaging;
        if (!_world.MatchState.IsEnded || _world.MatchState.WinnerTeam != PlayerTeam.Blue)
        {
            _lastToDieObservedBotReactionCaptureCelebrated = false;
        }
    }

    private void UpdateLastToDieBotReactions()
    {
        if (!IsLastToDieSessionActive || _lastToDieRun is null)
        {
            ResetLastToDieBotReactionState();
            return;
        }

        if (_world.LocalPlayerAwaitingJoin)
        {
            ObserveLastToDieBotReactionState();
            return;
        }

        var localPlayer = _world.LocalPlayer;
        var activeEnemyBots = BuildActiveLastToDieEnemyBotRoster(localPlayer.Team);
        PruneLastToDieBotReactionStates(activeEnemyBots.Keys);

        var localWeaponPickupObserved = localPlayer.IsAlive
            && localPlayer.AcquiredWeaponClassId.HasValue
            && localPlayer.AcquiredWeaponClassId != _lastToDieObservedBotReactionAcquiredWeaponClassId;
        var localMultiKillObserved = localPlayer.IsAlive
            && localPlayer.CurrentMultiKillCount >= 3
            && _lastToDieObservedBotReactionLocalMultiKillCount < 3;
        var localRageObserved = localPlayer.IsAlive
            && localPlayer.IsRaging
            && !_lastToDieObservedBotReactionLocalRaging;
        var captureCelebrationObserved = _world.MatchState.IsEnded
            && _world.MatchState.WinnerTeam == PlayerTeam.Blue
            && !_lastToDieObservedBotReactionCaptureCelebrated;

        var observedDeaths = CollectObservedLastToDieEnemyBotDeaths(activeEnemyBots);
        foreach (var entry in activeEnemyBots)
        {
            var slot = entry.Key;
            var bot = entry.Value;
            var state = GetOrCreateLastToDieBotReactionState(slot, bot);

            if (!bot.IsAlive)
            {
                state.WasAlive = false;
                state.SawLocalPlayerLastTick = false;
                state.LowHealthCallIssued = false;
                state.LastKnownHealth = bot.Health;
                state.LastKnownPosition = new Vector2(bot.X, bot.Y);
                continue;
            }

            if (!state.WasAlive)
            {
                state.LastDamageTakenFrame = _world.Frame;
                state.LastIdleReactionFrame = _world.Frame;
                state.LowHealthCallIssued = false;
            }

            if (state.LastKnownHealth > bot.Health)
            {
                state.LastDamageTakenFrame = _world.Frame;
                state.LastIdleReactionFrame = _world.Frame;
            }

            var seesLocalPlayer = localPlayer.IsAlive && CanLastToDieBotSeePlayer(bot, localPlayer);
            var sawTeammateDie = CanLastToDieBotSeeObservedDeath(bot, slot, observedDeaths);
            var lowHealthRequested = ShouldLastToDieBotCallMedic(bot, state);
            var idleReactionRequested = ShouldTriggerLastToDieBotIdleReaction(bot, state, seesLocalPlayer);

            var reactionFrame = captureCelebrationObserved
                ? LastToDieBotReactionFrameZ5
                : localRageObserved && seesLocalPlayer
                    ? LastToDieBotReactionFrameZ7
                    : localMultiKillObserved && seesLocalPlayer
                        ? LastToDieBotReactionFrameZ9
                        : localWeaponPickupObserved && seesLocalPlayer
                            ? LastToDieBotReactionFrameZ4
                            : seesLocalPlayer && !state.SawLocalPlayerLastTick
                                ? LastToDieBotReactionFrameZ6
                                : sawTeammateDie
                                    ? LastToDieBotReactionFrameZ1
                                    : lowHealthRequested
                                        ? LastToDieBotReactionFrameMedic
                                        : idleReactionRequested
                                            ? LastToDieBotReactionFrameZ2
                                            : -1;

            if (reactionFrame >= 0 && TryTriggerLastToDieBotReaction(slot, reactionFrame, state))
            {
                if (lowHealthRequested)
                {
                    state.LowHealthCallIssued = true;
                }

                if (idleReactionRequested)
                {
                    state.LastIdleReactionFrame = _world.Frame;
                }
            }

            if (GetHealthFraction(bot) >= LastToDieBotReactionLowHealthRecoveryFraction)
            {
                state.LowHealthCallIssued = false;
            }

            state.WasAlive = true;
            state.SawLocalPlayerLastTick = seesLocalPlayer;
            state.LastKnownHealth = bot.Health;
            state.LastKnownPosition = new Vector2(bot.X, bot.Y);
        }

        if (captureCelebrationObserved)
        {
            _lastToDieObservedBotReactionCaptureCelebrated = true;
        }

        ObserveLastToDieBotReactionState();
    }

    private Dictionary<byte, PlayerEntity> BuildActiveLastToDieEnemyBotRoster(PlayerTeam localTeam)
    {
        var bots = new Dictionary<byte, PlayerEntity>();
        foreach (var entry in _practiceBotSlots)
        {
            if (entry.Value.Team == localTeam || !_world.TryGetNetworkPlayer(entry.Key, out var player))
            {
                continue;
            }

            if (_world.IsNetworkPlayerAwaitingJoin(entry.Key))
            {
                continue;
            }

            bots[entry.Key] = player;
        }

        return bots;
    }

    private void PruneLastToDieBotReactionStates(Dictionary<byte, PlayerEntity>.KeyCollection activeSlots)
    {
        var staleSlots = new List<byte>();
        foreach (var slot in _lastToDieBotReactionStates.Keys)
        {
            if (!activeSlots.Contains(slot))
            {
                staleSlots.Add(slot);
            }
        }

        for (var index = 0; index < staleSlots.Count; index += 1)
        {
            _lastToDieBotReactionStates.Remove(staleSlots[index]);
        }
    }

    private List<LastToDieObservedBotDeath> CollectObservedLastToDieEnemyBotDeaths(
        IReadOnlyDictionary<byte, PlayerEntity> activeEnemyBots)
    {
        var observedDeaths = new List<LastToDieObservedBotDeath>();
        foreach (var entry in activeEnemyBots)
        {
            var slot = entry.Key;
            var bot = entry.Value;
            var state = GetOrCreateLastToDieBotReactionState(slot, bot);
            if (!state.WasAlive || bot.IsAlive)
            {
                continue;
            }

            observedDeaths.Add(new LastToDieObservedBotDeath(
                state.LastKnownPosition == Vector2.Zero ? new Vector2(bot.X, bot.Y) : state.LastKnownPosition,
                slot));
        }

        return observedDeaths;
    }

    private LastToDieBotReactionState GetOrCreateLastToDieBotReactionState(byte slot, PlayerEntity bot)
    {
        if (_lastToDieBotReactionStates.TryGetValue(slot, out var state))
        {
            return state;
        }

        state = new LastToDieBotReactionState
        {
            WasAlive = bot.IsAlive,
            LastKnownHealth = bot.Health,
            LastKnownPosition = new Vector2(bot.X, bot.Y),
            LastDamageTakenFrame = _world.Frame,
            LastIdleReactionFrame = _world.Frame,
        };
        _lastToDieBotReactionStates[slot] = state;
        return state;
    }

    private bool TryTriggerLastToDieBotReaction(byte slot, int bubbleFrame, LastToDieBotReactionState state)
    {
        if (_world.Frame - state.LastReactionFrame < GetLastToDieBotReactionCooldownTicks())
        {
            return false;
        }

        if (!_world.TryTriggerNetworkPlayerChatBubble(slot, bubbleFrame))
        {
            return false;
        }

        state.LastReactionFrame = _world.Frame;
        return true;
    }

    private bool CanLastToDieBotSeePlayer(PlayerEntity bot, PlayerEntity target)
    {
        if (!bot.IsAlive || !target.IsAlive)
        {
            return false;
        }

        var maxDistanceSquared = LastToDieBotReactionAwarenessDistance * LastToDieBotReactionAwarenessDistance;
        return Vector2.DistanceSquared(new Vector2(bot.X, bot.Y), new Vector2(target.X, target.Y)) <= maxDistanceSquared
            && _world.CombatTestHasLineOfSight(bot, target);
    }

    private bool CanLastToDieBotSeeObservedDeath(
        PlayerEntity bot,
        byte observingSlot,
        IReadOnlyList<LastToDieObservedBotDeath> observedDeaths)
    {
        if (!bot.IsAlive || observedDeaths.Count == 0)
        {
            return false;
        }

        var maxDistanceSquared = LastToDieBotReactionTeammateDeathDistance * LastToDieBotReactionTeammateDeathDistance;
        for (var index = 0; index < observedDeaths.Count; index += 1)
        {
            var death = observedDeaths[index];
            if (death.Slot == observingSlot)
            {
                continue;
            }

            if (Vector2.DistanceSquared(new Vector2(bot.X, bot.Y), death.Position) > maxDistanceSquared)
            {
                continue;
            }

            if (_world.CombatTestHasObstacleLineOfSight(bot.X, bot.Y, death.Position.X, death.Position.Y))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldLastToDieBotCallMedic(PlayerEntity bot, LastToDieBotReactionState state)
    {
        return bot.IsAlive
            && !state.LowHealthCallIssued
            && GetHealthFraction(bot) <= LastToDieBotReactionLowHealthFraction;
    }

    private bool ShouldTriggerLastToDieBotIdleReaction(PlayerEntity bot, LastToDieBotReactionState state, bool seesLocalPlayer)
    {
        if (!bot.IsAlive || seesLocalPlayer)
        {
            return false;
        }

        var idleThresholdTicks = GetLastToDieBotIdleReactionTicks();
        return _world.Frame - state.LastDamageTakenFrame >= idleThresholdTicks
            && _world.Frame - state.LastIdleReactionFrame >= idleThresholdTicks;
    }

    private long GetLastToDieBotReactionCooldownTicks()
    {
        return Math.Max(1, _config.TicksPerSecond * 3);
    }

    private long GetLastToDieBotIdleReactionTicks()
    {
        return Math.Max(1, _config.TicksPerSecond * 8L);
    }

    private static float GetHealthFraction(PlayerEntity player)
    {
        return player.MaxHealth <= 0
            ? 0f
            : Math.Clamp(player.Health / (float)player.MaxHealth, 0f, 1f);
    }
}
