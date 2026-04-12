using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenGarrison.BotAI;
using OpenGarrison.Core;

namespace OpenGarrison.Server;

/// <summary>
/// Manages server-authoritative bots that participate in the simulation.
/// Bot slots are distinct from client connections - bots are controlled by the server
/// and their PlayerInputSnapshots are fed into the world before simulation advances.
/// </summary>
internal sealed class ServerBotManager
{
    private readonly SimulationWorld _world;
    private readonly SimulationConfig _config;
    private readonly IPracticeBotController _botController;
    private readonly BotReactionController _reactionController;
    
    private readonly Dictionary<byte, ServerBotSlotState> _botSlots = new();
    private readonly Dictionary<byte, ControlledBotSlot> _controlledSlotsBuffer = new();
    private readonly Dictionary<byte, PlayerInputSnapshot> _inputCache = new();
    private readonly HashSet<byte> _staleSlotsBuffer = new();
    
    private IReadOnlyDictionary<PlayerClass, BotNavigationAsset>? _navigationAssets;
    private string? _currentLevelName;
    private int _currentMapAreaIndex;
    
    private int _thinkTick;
    private int _perfSamples;
    private double _perfBuildInputTotalMs;
    private double _perfBuildInputMaxMs;
    private double _perfApplyInputTotalMs;
    private double _perfApplyInputMaxMs;
    
    private const int ThinkIntervalTicksSmallRoster = 2;
    private const int ThinkIntervalTicksMediumRoster = 3;
    private const int ThinkIntervalTicksLargeRoster = 4;

    public ServerBotManager(
        SimulationWorld world,
        SimulationConfig config,
        IPracticeBotController botController)
    {
        _world = world;
        _config = config;
        _botController = botController;
        _reactionController = new BotReactionController(config.TicksPerSecond);
    }

    public IReadOnlyDictionary<byte, ServerBotSlotState> BotSlots => _botSlots;

    /// <summary>
    /// Adds a bot to the specified slot with the given configuration.
    /// </summary>
    public bool TryAddBot(byte slot, PlayerTeam team, PlayerClass classId, string displayName)
    {
        if (!SimulationWorld.IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        if (_botSlots.ContainsKey(slot))
        {
            return false;
        }

        _world.TryPrepareNetworkPlayerJoin(slot);
        _world.TrySetNetworkPlayerName(slot, displayName);
        _world.TrySetNetworkPlayerTeam(slot, team);
        _world.TryApplyNetworkPlayerClassSelection(slot, classId);

        _botSlots[slot] = new ServerBotSlotState(slot, team, classId, displayName);
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
        _inputCache.Remove(slot);
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

        _world.TrySetNetworkPlayerTeam(slot, team);
        _botSlots[slot] = state.WithTeam(team);
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

        _world.TryApplyNetworkPlayerClassSelection(slot, classId);
        _botSlots[slot] = state.WithClassId(classId);
        return true;
    }

    /// <summary>
    /// Fills empty slots with bots to reach the target count per team.
    /// Returns the number of bots actually added.
    /// </summary>
    public int FillBots(int targetPerTeam, PlayerClass defaultClass)
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

        // Add blue bots
        var nextSlot = FindNextAvailableSlot();
        while (blueCount < targetPerTeam && nextSlot.HasValue)
        {
            var displayName = GenerateBotDisplayName(PlayerTeam.Blue, blueCount + 1);
            if (TryAddBot(nextSlot.Value, PlayerTeam.Blue, defaultClass, displayName))
            {
                blueCount++;
                addedCount++;
            }
            nextSlot = FindNextAvailableSlot();
        }

        // Add red bots
        nextSlot = FindNextAvailableSlot();
        while (redCount < targetPerTeam && nextSlot.HasValue)
        {
            var displayName = GenerateBotDisplayName(PlayerTeam.Red, redCount + 1);
            if (TryAddBot(nextSlot.Value, PlayerTeam.Red, defaultClass, displayName))
            {
                redCount++;
                addedCount++;
            }
            nextSlot = FindNextAvailableSlot();
        }

        return addedCount;
    }

    /// <summary>
    /// Fills a specific team with bots to reach the target count.
    /// Returns the number of bots actually added.
    /// </summary>
    public int TryFillTeam(PlayerTeam team, int targetCount, PlayerClass defaultClass)
    {
        if (team != PlayerTeam.Red && team != PlayerTeam.Blue)
        {
            return 0;
        }

        var currentCount = _botSlots.Values.Count(s => s.Team == team);
        var addedCount = 0;
        var slot = FindNextAvailableSlot();

        while (currentCount + addedCount < targetCount && slot.HasValue)
        {
            var displayName = GenerateBotDisplayName(team, currentCount + addedCount + 1);
            if (TryAddBot(slot.Value, team, defaultClass, displayName))
            {
                addedCount++;
            }
            slot = FindNextAvailableSlot();
        }

        return addedCount;
    }

    /// <summary>
    /// Removes all bots from the server.
    /// </summary>
    public void ClearAllBots()
    {
        foreach (var slot in _botSlots.Keys)
        {
            _world.TryReleaseNetworkPlayerSlot(slot);
        }
        _botSlots.Clear();
        _inputCache.Clear();
        _reactionController.Reset();
    }

    /// <summary>
    /// Resets reaction state when a new session starts.
    /// </summary>
    public void ResetReactions()
    {
        _reactionController.Reset();
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

        // Get inputs (with browser-style throttling)
        var inputs = GetBotInputs();
        
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

            _controlledSlotsBuffer[entry.Key] = new ControlledBotSlot(
                entry.Key,
                entry.Value.Team,
                entry.Value.ClassId);
        }
    }

    private Dictionary<byte, PlayerInputSnapshot> GetBotInputs()
    {
        // Browser-style throttling for performance
        _thinkTick++;
        var thinkInterval = GetThinkIntervalTicks(_controlledSlotsBuffer.Count);
        var shouldRefresh = _inputCache.Count == 0
            || (_thinkTick % thinkInterval) == 0
            || !DoInputsCoverRoster()
            || _currentLevelName != _world.Level.Name
            || _currentMapAreaIndex != _world.Level.MapAreaIndex;

        if (!shouldRefresh)
        {
            return _inputCache;
        }

        // Get navigation assets for current level
        var navigationResult = BotNavigationAssetStore.LoadForLevel(
            _world.Level,
            classes: null,
            useModernRuntimeGeneration: true,
            allowSynchronousGeneration: true);

        _currentLevelName = _world.Level.Name;
        _currentMapAreaIndex = _world.Level.MapAreaIndex;
        _navigationAssets = navigationResult.Assets;

        // Build inputs using the bot controller
        var refreshedInputs = _botController.BuildInputs(
            _world,
            _controlledSlotsBuffer,
            _navigationAssets);

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

    private bool DoInputsCoverRoster()
    {
        if (_inputCache.Count != _controlledSlotsBuffer.Count)
        {
            return false;
        }

        foreach (var slot in _controlledSlotsBuffer.Keys)
        {
            if (!_inputCache.ContainsKey(slot))
            {
                return false;
            }
        }

        return true;
    }

    private static int GetThinkIntervalTicks(int botCount)
    {
        return botCount switch
        {
            >= 8 => ThinkIntervalTicksLargeRoster,
            >= 4 => ThinkIntervalTicksMediumRoster,
            _ => ThinkIntervalTicksSmallRoster,
        };
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
    }

    private void RecordBuildInputPerformance(double milliseconds)
    {
        _perfSamples++;
        _perfBuildInputTotalMs += milliseconds;
        _perfBuildInputMaxMs = Math.Max(_perfBuildInputMaxMs, milliseconds);

        if ((_perfSamples % 30) != 0)
        {
            return;
        }

        var averageMs = _perfBuildInputTotalMs / _perfSamples;
        Console.WriteLine(
            $"ServerBotManager slots={_controlledSlotsBuffer.Count} " +
            $"build(last/avg/max)={milliseconds:0.0}/{averageMs:0.0}/{_perfBuildInputMaxMs:0.0}ms " +
            $"apply(last/avg/max)={_perfApplyInputTotalMs / _perfSamples:0.0}/{_perfApplyInputTotalMs / _perfSamples:0.0}/{_perfApplyInputMaxMs:0.0}ms");
    }

    private void RecordApplyInputPerformance(double milliseconds)
    {
        _perfApplyInputTotalMs += milliseconds;
        _perfApplyInputMaxMs = Math.Max(_perfApplyInputMaxMs, milliseconds);
    }

    private byte? FindNextAvailableSlot()
    {
        for (byte slot = (byte)(SimulationWorld.LocalPlayerSlot + 1); slot <= SimulationWorld.MaxPlayableNetworkPlayers; slot++)
        {
            if (!_botSlots.ContainsKey(slot))
            {
                // Check if slot is not occupied by a client
                return slot;
            }
        }
        return null;
    }

    private static string GenerateBotDisplayName(PlayerTeam team, int number)
    {
        var teamLabel = team == PlayerTeam.Blue ? "BLU" : "RED";
        return $"{teamLabel} Bot {number}";
    }
}

/// <summary>
/// Represents the configuration state for a server bot slot.
/// </summary>
internal readonly struct ServerBotSlotState
{
    public ServerBotSlotState(byte slot, PlayerTeam team, PlayerClass classId, string displayName)
    {
        Slot = slot;
        Team = team;
        ClassId = classId;
        DisplayName = displayName;
    }

    public byte Slot { get; }
    public PlayerTeam Team { get; }
    public PlayerClass ClassId { get; }
    public string DisplayName { get; }

    public ServerBotSlotState WithTeam(PlayerTeam newTeam) => new(Slot, newTeam, ClassId, DisplayName);
    public ServerBotSlotState WithClassId(PlayerClass newClassId) => new(Slot, Team, newClassId, DisplayName);
    public ServerBotSlotState WithDisplayName(string newDisplayName) => new(Slot, Team, ClassId, newDisplayName);
}
