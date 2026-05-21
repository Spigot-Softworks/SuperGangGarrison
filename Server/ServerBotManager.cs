using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.Core;

namespace OpenGarrison.Server;

/// <summary>
/// Manages server-authoritative bots that participate in the simulation.
/// Bot slots are distinct from client connections - bots are controlled by the server
/// and their PlayerInputSnapshots are fed into the world before simulation advances.
/// </summary>
internal sealed class ServerBotManager
{
    internal enum ServerBotSource
    {
        Manual,
        Autofill,
    }

    private static readonly PlayerClass[] DefaultFillClassCycle =
    [
        PlayerClass.Scout,
        PlayerClass.Pyro,
        PlayerClass.Soldier,
        PlayerClass.Heavy,
        PlayerClass.Demoman,
        PlayerClass.Medic,
        PlayerClass.Engineer,
        PlayerClass.Spy,
        PlayerClass.Sniper,
        PlayerClass.Quote,
    ];

    private readonly SimulationWorld _world;
    private readonly SimulationConfig _config;
    private readonly IPracticeBotController _botController;
    private readonly BotReactionController _reactionController;
    private readonly Func<byte, bool> _isSlotAvailableForBot;
    private readonly PracticeBotDisplayNamePool _botDisplayNamePool;
    
    private readonly Dictionary<byte, ServerBotSlotState> _botSlots = new();
    private readonly Dictionary<byte, ControlledBotSlot> _controlledSlotsBuffer = new();
    private readonly Dictionary<byte, ControlledBotSlot> _controllerConfigurationSlotsBuffer = new();
    private readonly Dictionary<byte, PlayerInputSnapshot> _inputCache = new();
    private readonly HashSet<byte> _staleSlotsBuffer = new();
    private int _perfSamples;
    private int _lastActiveInputCount;
    private int _lastZeroInputCount;
    private double _perfBuildInputLastMs;
    private double _perfBuildInputTotalMs;
    private double _perfBuildInputMaxMs;
    private double _perfApplyInputLastMs;
    private double _perfApplyInputTotalMs;
    private double _perfApplyInputMaxMs;

    public ServerBotManager(
        SimulationWorld world,
        SimulationConfig config,
        IPracticeBotController botController,
        Func<byte, bool>? isSlotAvailableForBot = null,
        PracticeBotDisplayNamePool? botDisplayNamePool = null)
    {
        _world = world;
        _config = config;
        _botController = botController;
        _reactionController = new BotReactionController(config.TicksPerSecond);
        _isSlotAvailableForBot = isSlotAvailableForBot ?? (_ => true);
        _botDisplayNamePool = botDisplayNamePool ?? new PracticeBotDisplayNamePool();
    }

    public IReadOnlyDictionary<byte, ServerBotSlotState> BotSlots => _botSlots;

    public ServerBotRuntimeMetrics Metrics
    {
        get
        {
            var botBrainSnapshot = _botController is BotBrainPracticeBotController botBrainController
                ? botBrainController.RuntimeSnapshot
                : default;
            return new ServerBotRuntimeMetrics(
                HasMeasurements: _perfSamples > 0,
                ControlledBotCount: _controlledSlotsBuffer.Count,
                ActiveInputCount: _lastActiveInputCount,
                ZeroInputCount: _lastZeroInputCount,
                BotBrainActiveControllerCount: botBrainSnapshot.ActiveControllerCount,
                BotBrainNavigationLoadedCount: botBrainSnapshot.NavigationLoadedCount,
                BotBrainNavigationMissingCount: botBrainSnapshot.NavigationMissingCount,
                BotBrainObjectiveTapeLoadedCount: botBrainSnapshot.ObjectiveTapeLoadedCount,
                BotBrainActivePathCount: botBrainSnapshot.ActivePathCount,
                SampleCount: _perfSamples,
                LastBuildInputMilliseconds: _perfBuildInputLastMs,
                AverageBuildInputMilliseconds: _perfSamples == 0 ? 0d : _perfBuildInputTotalMs / _perfSamples,
                MaxBuildInputMilliseconds: _perfBuildInputMaxMs,
                LastApplyInputMilliseconds: _perfApplyInputLastMs,
                AverageApplyInputMilliseconds: _perfSamples == 0 ? 0d : _perfApplyInputTotalMs / _perfSamples,
                MaxApplyInputMilliseconds: _perfApplyInputMaxMs);
        }
    }

    /// <summary>
    /// Adds a bot to the specified slot with the given configuration.
    /// </summary>
    public bool TryAddBot(byte slot, PlayerTeam team, PlayerClass classId, string displayName)
    {
        return TryAddBot(slot, team, classId, displayName, ServerBotSource.Manual);
    }

    private bool TryAddBot(byte slot, PlayerTeam team, PlayerClass classId, string displayName, ServerBotSource source)
    {
        if (!IsValidServerBotSlot(slot) || !_isSlotAvailableForBot(slot))
        {
            return false;
        }

        if (_botSlots.ContainsKey(slot))
        {
            return false;
        }

        var resolvedDisplayName = ResolveBotDisplayName(slot, team, displayName);
        if (!_world.TryPrepareNetworkPlayerJoin(slot)
            || !_world.TrySetNetworkPlayerName(slot, resolvedDisplayName)
            || !_world.TrySetNetworkPlayerTeam(slot, team))
        {
            _world.TryReleaseNetworkPlayerSlot(slot);
            _botDisplayNamePool.ReleaseSlot(slot);
            return false;
        }

        ConfigureBotControllerSpawnOverrides(slot, team, classId);
        if (!_world.TryApplyNetworkPlayerClassSelection(slot, classId))
        {
            _world.TryReleaseNetworkPlayerSlot(slot);
            _botDisplayNamePool.ReleaseSlot(slot);
            ConfigureBotControllerSpawnOverrides();
            return false;
        }

        if (_world.TryGetNetworkPlayer(slot, out var player))
        {
            resolvedDisplayName = player.DisplayName;
        }

        _botDisplayNamePool.Reserve(resolvedDisplayName);
        _botSlots[slot] = new ServerBotSlotState(slot, team, classId, resolvedDisplayName, source);
        _inputCache.Remove(slot);
        return true;
    }

    /// <summary>
    /// Removes a bot from the specified slot.
    /// </summary>
    public bool TryRemoveBot(byte slot)
    {
        if (!_botSlots.Remove(slot))
        {
            return false;
        }

        _world.TryReleaseNetworkPlayerSlot(slot);
        _botDisplayNamePool.ReleaseSlot(slot);
        _inputCache.Remove(slot);
        ConfigureBotControllerSpawnOverrides();
        return true;
    }

    /// <summary>
    /// Updates the team for an existing bot slot.
    /// </summary>
    public bool TrySetBotTeam(byte slot, PlayerTeam team)
    {
        if (!_botSlots.TryGetValue(slot, out var state))
        {
            return false;
        }

        ConfigureBotControllerSpawnOverrides(slot, team, state.ClassId);
        if (!_world.TrySetNetworkPlayerTeam(slot, team))
        {
            ConfigureBotControllerSpawnOverrides();
            return false;
        }

        _botSlots[slot] = state.WithTeam(team);
        ConfigureBotControllerSpawnOverrides();
        return true;
    }

    /// <summary>
    /// Updates the class for an existing bot slot.
    /// </summary>
    public bool TrySetBotClass(byte slot, PlayerClass classId)
    {
        if (!_botSlots.TryGetValue(slot, out var state))
        {
            return false;
        }

        if (_world.TryGetNetworkPlayer(slot, out var player) && player.ClassId == classId)
        {
            _botSlots[slot] = state.WithClassId(classId);
            ConfigureBotControllerSpawnOverrides();
            return true;
        }

        ConfigureBotControllerSpawnOverrides(slot, state.Team, classId);
        if (!_world.TryApplyNetworkPlayerClassSelection(slot, classId))
        {
            ConfigureBotControllerSpawnOverrides();
            return false;
        }

        _botSlots[slot] = state.WithClassId(classId);
        ConfigureBotControllerSpawnOverrides();
        return true;
    }

    /// <summary>
    /// Fills empty slots with bots to reach the target count per team.
    /// Returns the number of bots actually added.
    /// </summary>
    public int FillBots(int targetPerTeam, PlayerClass? requestedClass)
    {
        return FillBots(targetPerTeam, requestedClass, ServerBotSource.Manual);
    }

    private int FillBots(int targetPerTeam, PlayerClass? requestedClass, ServerBotSource source)
    {
        var addedCount = 0;
        
        // Count current bots per team
        var blueCount = 0;
        var redCount = 0;
        foreach (var state in _botSlots.Values)
        {
            if (state.Team == PlayerTeam.Blue) blueCount++;
            else if (state.Team == PlayerTeam.Red) redCount++;
        }

        foreach (var slot in EnumerateAvailableSlots())
        {
            if (blueCount < targetPerTeam)
            {
                var playerClass = ResolveFillClass(PlayerTeam.Blue, blueCount, requestedClass);
                if (TryAddBot(slot, PlayerTeam.Blue, playerClass, string.Empty, source))
                {
                    blueCount++;
                    addedCount++;
                }

                continue;
            }

            if (redCount < targetPerTeam)
            {
                var playerClass = ResolveFillClass(PlayerTeam.Red, redCount, requestedClass);
                if (TryAddBot(slot, PlayerTeam.Red, playerClass, string.Empty, source))
                {
                    redCount++;
                    addedCount++;
                }
            }

            if (blueCount >= targetPerTeam && redCount >= targetPerTeam)
            {
                break;
            }
        }

        return addedCount;
    }

    /// <summary>
    /// Fills a specific team with bots to reach the target count.
    /// Returns the number of bots actually added.
    /// </summary>
    public int TryFillTeam(PlayerTeam team, int targetCount, PlayerClass? requestedClass)
    {
        return TryFillTeam(team, targetCount, requestedClass, ServerBotSource.Manual);
    }

    private int TryFillTeam(PlayerTeam team, int targetCount, PlayerClass? requestedClass, ServerBotSource source)
    {
        if (team != PlayerTeam.Red && team != PlayerTeam.Blue)
        {
            return 0;
        }

        var currentCount = _botSlots.Values.Count(s => s.Team == team);
        var addedCount = 0;
        foreach (var slot in EnumerateAvailableSlots())
        {
            if (currentCount + addedCount >= targetCount)
            {
                break;
            }

            var playerClass = ResolveFillClass(team, currentCount + addedCount, requestedClass);
            if (TryAddBot(slot, team, playerClass, string.Empty, source))
            {
                addedCount++;
            }
        }

        return addedCount;
    }

    /// <summary>
    /// Removes bots from a team until the team has at most the requested bot count.
    /// Returns the number of bots actually removed.
    /// </summary>
    public int TrimTeam(PlayerTeam team, int targetCount)
    {
        if (team != PlayerTeam.Red && team != PlayerTeam.Blue)
        {
            return 0;
        }

        var clampedTargetCount = Math.Max(0, targetCount);
        var removableSlots = _botSlots.Values
            .Where(state => state.Team == team)
            .OrderByDescending(state => state.Slot)
            .Select(state => state.Slot)
            .ToArray();
        var currentCount = removableSlots.Length;
        if (currentCount <= clampedTargetCount)
        {
            return 0;
        }

        var removedCount = 0;
        for (var index = 0; index < removableSlots.Length && currentCount > clampedTargetCount; index += 1)
        {
            if (!TryRemoveBot(removableSlots[index]))
            {
                continue;
            }

            currentCount -= 1;
            removedCount += 1;
        }

        return removedCount;
    }

    public int FillAutofillTeam(PlayerTeam team, int targetCount, PlayerClass? requestedClass)
    {
        return TryFillTeam(team, targetCount, requestedClass, ServerBotSource.Autofill);
    }

    public int TrimAutofillTeam(PlayerTeam team, int targetCount)
    {
        if (team != PlayerTeam.Red && team != PlayerTeam.Blue)
        {
            return 0;
        }

        var clampedTargetCount = Math.Max(0, targetCount);
        var currentCount = _botSlots.Values.Count(state => state.Team == team);
        if (currentCount <= clampedTargetCount)
        {
            return 0;
        }

        var removableSlots = _botSlots.Values
            .Where(state => state.Team == team && state.Source == ServerBotSource.Autofill)
            .OrderByDescending(state => state.Slot)
            .Select(state => state.Slot)
            .ToArray();
        if (removableSlots.Length == 0)
        {
            return 0;
        }

        var removedCount = 0;
        for (var index = 0; index < removableSlots.Length && currentCount > clampedTargetCount; index += 1)
        {
            if (!TryRemoveBot(removableSlots[index]))
            {
                continue;
            }

            currentCount -= 1;
            removedCount += 1;
        }

        return removedCount;
    }

    /// <summary>
    /// Removes all bots from the server.
    /// </summary>
    public void ClearAllBots()
    {
        foreach (var slot in _botSlots.Keys.ToArray())
        {
            _world.TryReleaseNetworkPlayerSlot(slot);
        }
        _botSlots.Clear();
        _inputCache.Clear();
        _controllerConfigurationSlotsBuffer.Clear();
        _controlledSlotsBuffer.Clear();
        _botController.Reset();
        _reactionController.Reset();
        _botDisplayNamePool.Reset();
    }

    /// <summary>
    /// Resets reaction state when a new session starts.
    /// </summary>
    public void ResetReactions()
    {
        _reactionController.Reset();
    }

    /// <summary>
    /// Reapplies remembered bot join state after a dedicated map change pushed playable slots back to awaiting-join.
    /// Returns the number of bots successfully restored into active play.
    /// </summary>
    public int ReactivateBotsAfterMapChange()
    {
        if (_botSlots.Count == 0)
        {
            return 0;
        }

        _botController.Reset();
        ConfigureBotControllerSpawnOverrides();

        var restoredCount = 0;
        foreach (var entry in _botSlots)
        {
            var slot = entry.Key;
            var state = entry.Value;

            if (!_world.TrySetNetworkPlayerName(slot, state.DisplayName)
                || !_world.TrySetNetworkPlayerTeam(slot, state.Team)
                || !_world.TryApplyNetworkPlayerClassSelection(slot, state.ClassId))
            {
                continue;
            }

            if (_world.IsNetworkPlayerAwaitingJoin(slot))
            {
                continue;
            }

            restoredCount += 1;
        }

        return restoredCount;
    }

    /// <summary>
    /// Called each simulation tick AFTER AdvanceSimulationTick to update bot reactions and emotes.
    /// </summary>
    public void AdvanceBotReactions()
    {
        if (_botSlots.Count == 0)
        {
            return;
        }

        // Build controlled slots buffer if not already built this tick
        if (_controlledSlotsBuffer.Count == 0)
        {
            BuildControlledSlotsBuffer();
        }

        if (_controlledSlotsBuffer.Count == 0)
        {
            return;
        }

        // Build observed events from the local player (for Last-to-Die style reactions)
        BotObservedEvents? observedEvents = null;
        if (_world.TryGetNetworkPlayer(SimulationWorld.LocalPlayerSlot, out var localPlayer) && localPlayer != null)
        {
            var observedDeaths = CollectObservedBotDeaths();
            observedEvents = new BotObservedEvents
            {
                LocalPlayerAcquiredWeapon = localPlayer.AcquiredWeaponClassId.HasValue,
                LocalPlayerMultiKill = localPlayer.CurrentMultiKillCount >= 3,
                LocalPlayerStartedRaging = localPlayer.IsRaging,
                CaptureCelebration = _world.MatchState.IsEnded && _world.MatchState.WinnerTeam == PlayerTeam.Blue,
                ObservedDeaths = observedDeaths,
            };
        }

        _reactionController.UpdateReactions(_world, _controlledSlotsBuffer, observedEvents);
    }

    private List<BotObservedDeath> CollectObservedBotDeaths()
    {
        var deaths = new List<BotObservedDeath>();
        foreach (var entry in _botSlots)
        {
            var slot = entry.Key;
            if (!_world.TryGetNetworkPlayer(slot, out var bot) || bot is null)
            {
                continue;
            }

            // Bot was alive last tick but is now dead - this is an observed death
            // We track this through the controlled slots buffer state
            if (!bot.IsAlive)
            {
                // Record the death position for teammate reactions
                deaths.Add(new BotObservedDeath(bot.X, bot.Y, slot));
            }
        }
        return deaths;
    }

    /// <summary>
    /// Called each simulation tick BEFORE AdvanceSimulationTick to feed bot inputs into the world.
    /// </summary>
    public void FeedBotInputsBeforeSimulationAdvance()
    {
        if (_botSlots.Count == 0)
        {
            return;
        }

        var buildStartTimestamp = Stopwatch.GetTimestamp();
        
        // Build controlled slot list
        BuildControlledSlotsBuffer();
        
        if (_controlledSlotsBuffer.Count == 0)
        {
            return;
        }

        // Build fresh authoritative inputs every simulation tick.
        var inputs = GetBotInputs();
        RecordInputActivity(inputs);
        
        var buildMs = GetElapsedMilliseconds(buildStartTimestamp);
        RecordBuildInputPerformance(buildMs);

        // Apply inputs to world
        var applyStartTimestamp = Stopwatch.GetTimestamp();
        foreach (var entry in _controlledSlotsBuffer)
        {
            if (inputs.TryGetValue(entry.Key, out var input))
            {
                _world.TrySetNetworkPlayerInput(entry.Key, input);
            }
        }
        var applyMs = GetElapsedMilliseconds(applyStartTimestamp);
        RecordApplyInputPerformance(applyMs);
    }

    private void BuildControlledSlotsBuffer()
    {
        _controlledSlotsBuffer.Clear();
        foreach (var entry in _botSlots)
        {
            if (_world.IsNetworkPlayerAwaitingJoin(entry.Key))
            {
                continue;
            }

            if (!_world.TryGetNetworkPlayer(entry.Key, out var player))
            {
                continue;
            }

            _controlledSlotsBuffer[entry.Key] = new ControlledBotSlot(
                entry.Key,
                player.Team,
                player.ClassId);
        }
    }

    private void ConfigureBotControllerSpawnOverrides(
        byte? pendingSlot = null,
        PlayerTeam pendingTeam = default,
        PlayerClass pendingClassId = default)
    {
        _controllerConfigurationSlotsBuffer.Clear();
        foreach (var entry in _botSlots)
        {
            var state = entry.Value;
            _controllerConfigurationSlotsBuffer[entry.Key] = new ControlledBotSlot(
                entry.Key,
                state.Team,
                state.ClassId);
        }

        if (pendingSlot.HasValue)
        {
            _controllerConfigurationSlotsBuffer[pendingSlot.Value] = new ControlledBotSlot(
                pendingSlot.Value,
                pendingTeam,
                pendingClassId);
        }

        _botController.ConfigureSpawnOverrides(_world, _controllerConfigurationSlotsBuffer);
    }

    private Dictionary<byte, PlayerInputSnapshot> GetBotInputs()
    {
        var refreshedInputs = _botController.BuildInputs(
            _world,
            _controlledSlotsBuffer);

        SyncInputCache(refreshedInputs);
        return _inputCache;
    }

    private void SyncInputCache(IReadOnlyDictionary<byte, PlayerInputSnapshot> refreshedInputs)
    {
        // Remove stale slots
        _staleSlotsBuffer.Clear();
        foreach (var slot in _inputCache.Keys)
        {
            if (!_controlledSlotsBuffer.ContainsKey(slot))
            {
                _staleSlotsBuffer.Add(slot);
            }
        }
        foreach (var slot in _staleSlotsBuffer)
        {
            _inputCache.Remove(slot);
        }

        // Update with new inputs
        foreach (var slot in _controlledSlotsBuffer.Keys)
        {
            _inputCache[slot] = refreshedInputs.GetValueOrDefault(slot);
        }
    }

    private void RecordInputActivity(IReadOnlyDictionary<byte, PlayerInputSnapshot> inputs)
    {
        var active = 0;
        var zero = 0;
        foreach (var slot in _controlledSlotsBuffer.Keys)
        {
            if (inputs.TryGetValue(slot, out var input) && IsActiveInput(input))
            {
                active += 1;
            }
            else
            {
                zero += 1;
            }
        }

        _lastActiveInputCount = active;
        _lastZeroInputCount = zero;
    }

    private static bool IsActiveInput(PlayerInputSnapshot input)
    {
        return input.Left
            || input.Right
            || input.Up
            || input.Down
            || input.BuildSentry
            || input.DestroySentry
            || input.Taunt
            || input.FirePrimary
            || input.FireSecondary
            || input.DebugKill
            || input.DropIntel
            || input.UseAbility
            || input.InteractWeapon
            || input.SwapWeapon
            || input.IsUsingBinoculars;
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
    }

    private void RecordBuildInputPerformance(double milliseconds)
    {
        _perfSamples++;
        _perfBuildInputLastMs = milliseconds;
        _perfBuildInputTotalMs += milliseconds;
        _perfBuildInputMaxMs = Math.Max(_perfBuildInputMaxMs, milliseconds);
    }

    private void RecordApplyInputPerformance(double milliseconds)
    {
        _perfApplyInputLastMs = milliseconds;
        _perfApplyInputTotalMs += milliseconds;
        _perfApplyInputMaxMs = Math.Max(_perfApplyInputMaxMs, milliseconds);
    }

    private byte? FindNextAvailableSlot()
    {
        for (var slotNumber = SimulationWorld.LocalPlayerSlot + 1; slotNumber <= SimulationWorld.MaxPlayableNetworkPlayers; slotNumber++)
        {
            var slot = (byte)slotNumber;
            if (!_botSlots.ContainsKey(slot) && _isSlotAvailableForBot(slot))
            {
                return slot;
            }
        }
        return null;
    }

    private IEnumerable<byte> EnumerateAvailableSlots()
    {
        for (var slotNumber = SimulationWorld.LocalPlayerSlot + 1; slotNumber <= SimulationWorld.MaxPlayableNetworkPlayers; slotNumber++)
        {
            var slot = (byte)slotNumber;
            if (!_botSlots.ContainsKey(slot) && _isSlotAvailableForBot(slot))
            {
                yield return slot;
            }
        }
    }

    private static bool IsValidServerBotSlot(byte slot)
    {
        return slot != SimulationWorld.LocalPlayerSlot
            && SimulationWorld.IsPlayableNetworkPlayerSlot(slot);
    }

    private static PlayerClass ResolveFillClass(PlayerTeam team, int teamClassIndex, PlayerClass? requestedClass)
    {
        if (requestedClass.HasValue)
        {
            return requestedClass.Value;
        }

        var classOffset = team == PlayerTeam.Red ? 3 : 0;
        return DefaultFillClassCycle[(teamClassIndex + classOffset) % DefaultFillClassCycle.Length];
    }

    private string ResolveBotDisplayName(byte slot, PlayerTeam team, string displayName)
    {
        var trimmed = displayName.Trim();
        if (trimmed.Length > 0)
        {
            return trimmed;
        }

        var teamBotNumber = _botSlots.Values.Count(state => state.Team == team) + 1;
        return _botDisplayNamePool.GetOrAssign(slot, team, teamBotNumber);
    }
}

/// <summary>
/// Represents the configuration state for a server bot slot.
/// </summary>
internal readonly struct ServerBotSlotState
{
    public ServerBotSlotState(byte slot, PlayerTeam team, PlayerClass classId, string displayName, ServerBotManager.ServerBotSource source)
    {
        Slot = slot;
        Team = team;
        ClassId = classId;
        DisplayName = displayName;
        Source = source;
    }

    public byte Slot { get; }
    public PlayerTeam Team { get; }
    public PlayerClass ClassId { get; }
    public string DisplayName { get; }
    public ServerBotManager.ServerBotSource Source { get; }

    public ServerBotSlotState WithTeam(PlayerTeam newTeam) => new(Slot, newTeam, ClassId, DisplayName, Source);
    public ServerBotSlotState WithClassId(PlayerClass newClassId) => new(Slot, Team, newClassId, DisplayName, Source);
    public ServerBotSlotState WithDisplayName(string newDisplayName) => new(Slot, Team, ClassId, newDisplayName, Source);
}
