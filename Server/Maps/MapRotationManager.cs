using System;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Core;
using static ServerHelpers;

readonly record struct MapChangeTransition(
    string CurrentLevelName,
    int CurrentAreaIndex,
    int CurrentAreaCount,
    string NextLevelName,
    int NextAreaIndex,
    bool PreservePlayerStats,
    PlayerTeam? WinnerTeam,
    GameModeKind CurrentGameMode);

sealed class MapRotationManager
{
    private readonly SimulationWorld _world;
    private readonly Action<string> _log;
    private readonly List<string> _mapRotation;
    private readonly Random _shuffleRandom;
    private int _mapRotationIndex;
    private int _completedRoundsOnCurrentMap;
    private long _currentMapStartFrame;
    private QueuedNextRoundMap? _queuedNextRoundMap;

    public MapRotationManager(
        SimulationWorld world,
        string? requestedMap,
        string? mapRotationFile,
        IReadOnlyList<string> stockMapRotation,
        Action<string> log,
        bool mapRotationShuffleEnabled = false,
        Random? shuffleRandom = null)
    {
        _world = world;
        _log = log;
        _shuffleRandom = shuffleRandom ?? new Random();
        MapRotationShuffleEnabled = mapRotationShuffleEnabled;
        AdvanceMode = MapRotationAdvanceMode.RoundEnd;
        RotationRoundCount = 1;
        RotationTimeMinutes = 15;

        var rotation = LoadMapRotation(mapRotationFile, stockMapRotation);
        if (rotation.Count == 0)
        {
            rotation = SimpleLevelFactory.GetAvailableSourceLevels()
                .Select(entry => entry.Name)
                .ToList();
        }

        _mapRotation = rotation;
        InitializeWorldLevel(requestedMap);
    }

    public bool TryApplyPendingMapChange(out MapChangeTransition transition)
    {
        transition = default;
        if (!_world.IsMapChangeReady)
        {
            return false;
        }

        var winner = _world.MatchState.WinnerTeam;
        var currentLevelName = _world.Level.Name;
        var currentArea = _world.Level.MapAreaIndex;
        var totalAreas = _world.Level.MapAreaCount;
        var isVipStagedMap = IsVipStagedMap(currentLevelName, totalAreas);
        var isVipStageAdvance = isVipStagedMap
            && winner == PlayerTeam.Red
            && currentArea < totalAreas;
        var isVipFailedOffense = isVipStagedMap
            && winner == PlayerTeam.Blue
            && currentArea < totalAreas;
        var isVipFullCompletion = isVipStagedMap
            && winner == PlayerTeam.Red
            && currentArea >= totalAreas;
        if (!isVipStageAdvance)
        {
            _completedRoundsOnCurrentMap += 1;
        }

        var preserveStats = false;
        string nextMap;
        var nextArea = 1;
        var alignLoadedMapAsExternal = false;

        if (_queuedNextRoundMap is { } queuedNextRoundMap)
        {
            nextMap = queuedNextRoundMap.LevelName;
            nextArea = queuedNextRoundMap.AreaIndex;
            _queuedNextRoundMap = null;
            AlignExternalMapChange(nextMap);
            alignLoadedMapAsExternal = true;
            _log($"[server] advancing to voted next round map {nextMap} area {nextArea}");
        }
        else if (isVipFailedOffense && !ShouldAdvanceRotation())
        {
            transition = new MapChangeTransition(
                currentLevelName,
                currentArea,
                totalAreas,
                currentLevelName,
                currentArea,
                PreservePlayerStats: false,
                winner,
                _world.MatchRules.Mode);
            _log(BuildStagedRoundHoldLogMessage(currentLevelName, currentArea, totalAreas, "defender win"));
            _world.RestartCurrentRoundForMapRotation(preservePlayerStats: false);
            return true;
        }
        else if (isVipFullCompletion && !ShouldAdvanceRotation())
        {
            transition = new MapChangeTransition(
                currentLevelName,
                currentArea,
                totalAreas,
                currentLevelName,
                NextAreaIndex: 1,
                PreservePlayerStats: false,
                winner,
                _world.MatchRules.Mode);
            _log(BuildStagedRoundHoldLogMessage(currentLevelName, currentArea, totalAreas, "offense completion"));
            if (!_world.ApplyPendingMapChange(currentLevelName, mapAreaIndex: 1, preservePlayerStats: false))
            {
                _log($"[server] failed to restart {currentLevelName} area 1/{totalAreas}; restarting round.");
                transition = default;
                return false;
            }

            AlignCurrentMap(_world.Level.Name);
            _log($"[server] now running {_world.Level.Name} area {_world.Level.MapAreaIndex}/{_world.Level.MapAreaCount}");
            return true;
        }
        else if (winner == PlayerTeam.Red && currentArea < totalAreas)
        {
            nextMap = currentLevelName;
            nextArea = currentArea + 1;
            preserveStats = true;
            _log($"[server] advancing to {nextMap} area {nextArea}/{totalAreas} (winner red)");
        }
        else if (winner == PlayerTeam.Blue && currentArea < totalAreas && !isVipFailedOffense)
        {
            nextMap = currentLevelName;
            nextArea = currentArea;
            _log($"[server] restarting {nextMap} area {nextArea}/{totalAreas} for defender win");
        }
        else if (ShouldAdvanceRotation())
        {
            if (_mapRotation.Count > 0)
            {
                _mapRotationIndex = MapRotationShuffleEnabled
                    ? SelectShuffledRotationIndex(currentLevelName)
                    : (_mapRotationIndex + 1) % _mapRotation.Count;
                nextMap = _mapRotation[_mapRotationIndex];
                var rotationMode = MapRotationShuffleEnabled ? "random map" : "next map";
                _log($"[server] advancing to {rotationMode} {nextMap} (winner {(winner.HasValue ? winner.Value.ToString() : "tie")})");
            }
            else
            {
                nextMap = currentLevelName;
                _log("[server] map rotation empty; restarting current map.");
            }
        }
        else
        {
            nextMap = currentLevelName;
            _log(BuildRotationHoldLogMessage(currentLevelName));
            transition = new MapChangeTransition(
                currentLevelName,
                currentArea,
                totalAreas,
                currentLevelName,
                currentArea,
                PreservePlayerStats: false,
                winner,
                _world.MatchRules.Mode);
            _world.RestartCurrentRoundForMapRotation(preservePlayerStats: false);
            return true;
        }

        transition = new MapChangeTransition(
            currentLevelName,
            currentArea,
            totalAreas,
            nextMap,
            nextArea,
            preserveStats,
            winner,
            _world.MatchRules.Mode);

        if (!_world.ApplyPendingMapChange(nextMap, nextArea, preserveStats))
        {
            _log($"[server] failed to apply map change to {nextMap}; restarting round.");
            transition = default;
            return false;
        }

        if (!preserveStats)
        {
            _world.ResetPlayersToAwaitingJoinForFreshMap();
            ResetPolicyCountersForCurrentMap();
        }

        if (alignLoadedMapAsExternal)
        {
            AlignExternalMapChange(_world.Level.Name);
        }
        else
        {
            AlignCurrentMap(_world.Level.Name);
        }

        _log($"[server] now running {_world.Level.Name} area {_world.Level.MapAreaIndex}/{_world.Level.MapAreaCount}");
        return true;
    }

    public IReadOnlyList<string> MapRotation => _mapRotation;

    public int CurrentRotationIndex => _mapRotationIndex;

    public bool MapRotationShuffleEnabled { get; set; }

    public MapRotationAdvanceMode AdvanceMode { get; private set; }

    public int RotationRoundCount { get; private set; }

    public int RotationTimeMinutes { get; private set; }

    public int CompletedRoundsOnCurrentMap => _completedRoundsOnCurrentMap;

    public void ConfigureAdvancePolicy(MapRotationAdvanceMode advanceMode, int roundCount, int timeMinutes)
    {
        AdvanceMode = NormalizeAdvanceMode(advanceMode);
        RotationRoundCount = NormalizeRotationRoundCount(roundCount);
        RotationTimeMinutes = NormalizeRotationTimeMinutes(timeMinutes);
    }

    public static MapRotationAdvanceMode NormalizeAdvanceMode(MapRotationAdvanceMode mode)
    {
        return mode switch
        {
            MapRotationAdvanceMode.RoundEnd => MapRotationAdvanceMode.RoundEnd,
            MapRotationAdvanceMode.RoundCount => MapRotationAdvanceMode.RoundCount,
            MapRotationAdvanceMode.TimeElapsed => MapRotationAdvanceMode.TimeElapsed,
            _ => MapRotationAdvanceMode.RoundEnd,
        };
    }

    public static int NormalizeRotationRoundCount(int roundCount)
    {
        return Math.Clamp(roundCount, 1, 255);
    }

    public static int NormalizeRotationTimeMinutes(int timeMinutes)
    {
        return Math.Clamp(timeMinutes, 1, 255);
    }

    public bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1)
    {
        var nextLevel = SimpleLevelFactory.CreateImportedLevel(levelName, mapAreaIndex);
        if (nextLevel is null)
        {
            return false;
        }

        _queuedNextRoundMap = new QueuedNextRoundMap(nextLevel.Name, nextLevel.MapAreaIndex);
        _log($"[server] queued next round map {nextLevel.Name} area {nextLevel.MapAreaIndex}");
        return true;
    }

    public void ClearQueuedNextRoundMap()
    {
        _queuedNextRoundMap = null;
    }

    public void AlignCurrentMap(string levelName)
    {
        var index = FindMapRotationIndex(_mapRotation, levelName);
        if (index >= 0)
        {
            _mapRotationIndex = index;
            return;
        }

        _mapRotationIndex = EnsureMapRotationIndex(_mapRotation, levelName, levelName);
    }

    public void AlignExternalMapChange(string levelName)
    {
        var index = FindMapRotationIndex(_mapRotation, levelName);
        if (index >= 0)
        {
            _mapRotationIndex = index;
        }
    }

    private void InitializeWorldLevel(string? requestedMap)
    {
        _queuedNextRoundMap = null;
        if (!string.IsNullOrWhiteSpace(requestedMap))
        {
            var loadedRequestedMap = _world.TryLoadLevel(requestedMap, mapAreaIndex: 1, preservePlayerStats: false);
            if (!loadedRequestedMap)
            {
                _log($"[server] unknown map \"{requestedMap}\"; falling back to {_world.Level.Name}.");
            }

            _mapRotationIndex = EnsureMapRotationIndex(
                _mapRotation,
                loadedRequestedMap ? requestedMap : _world.Level.Name,
                _world.Level.Name);
            ResetPolicyCountersForCurrentMap();
            return;
        }

        if (_mapRotation.Count == 0)
        {
            _mapRotationIndex = 0;
            ResetPolicyCountersForCurrentMap();
            return;
        }

        var requestedRotationMap = _mapRotation[0];
        if (!_world.TryLoadLevel(requestedRotationMap, mapAreaIndex: 1, preservePlayerStats: false))
        {
            _log($"[server] unknown map \"{requestedRotationMap}\"; falling back to {_world.Level.Name}.");
        }

        _mapRotationIndex = Math.Max(0, FindMapRotationIndex(_mapRotation, _world.Level.Name));
        ResetPolicyCountersForCurrentMap();
    }

    private bool ShouldAdvanceRotation()
    {
        return AdvanceMode switch
        {
            MapRotationAdvanceMode.RoundCount => _completedRoundsOnCurrentMap >= RotationRoundCount,
            MapRotationAdvanceMode.TimeElapsed => GetCurrentMapElapsedTicks() >= RotationTimeMinutes * _world.Config.TicksPerSecond * 60,
            _ => true,
        };
    }

    private long GetCurrentMapElapsedTicks()
    {
        return Math.Max(0, _world.Frame - _currentMapStartFrame);
    }

    private string BuildRotationHoldLogMessage(string currentLevelName)
    {
        return AdvanceMode switch
        {
            MapRotationAdvanceMode.RoundCount =>
                $"[server] restarting {currentLevelName}; map rotation waits for round {Math.Min(_completedRoundsOnCurrentMap, RotationRoundCount)}/{RotationRoundCount}.",
            MapRotationAdvanceMode.TimeElapsed =>
                $"[server] restarting {currentLevelName}; map rotation waits for {RotationTimeMinutes} minute(s) elapsed.",
            _ => $"[server] restarting {currentLevelName}.",
        };
    }

    private string BuildStagedRoundHoldLogMessage(string currentLevelName, int currentArea, int totalAreas, string reason)
    {
        return AdvanceMode switch
        {
            MapRotationAdvanceMode.RoundCount =>
                $"[server] restarting {currentLevelName} area {currentArea}/{totalAreas} after {reason}; map rotation waits for round {Math.Min(_completedRoundsOnCurrentMap, RotationRoundCount)}/{RotationRoundCount}.",
            MapRotationAdvanceMode.TimeElapsed =>
                $"[server] restarting {currentLevelName} area {currentArea}/{totalAreas} after {reason}; map rotation waits for {RotationTimeMinutes} minute(s) elapsed.",
            _ => $"[server] restarting {currentLevelName} area {currentArea}/{totalAreas} after {reason}.",
        };
    }

    private bool IsVipStagedMap(string levelName, int totalAreas)
    {
        return totalAreas > 1
            && (_world.MatchRules.Mode == GameModeKind.Vip
                || levelName.StartsWith("vip_", StringComparison.OrdinalIgnoreCase));
    }

    private void ResetPolicyCountersForCurrentMap()
    {
        _completedRoundsOnCurrentMap = 0;
        _currentMapStartFrame = _world.Frame;
    }

    private int SelectShuffledRotationIndex(string currentLevelName)
    {
        if (_mapRotation.Count <= 1)
        {
            return 0;
        }

        var currentMap = NormalizeMapName(currentLevelName);
        var candidates = new List<int>(_mapRotation.Count - 1);
        for (var index = 0; index < _mapRotation.Count; index += 1)
        {
            if (!string.Equals(NormalizeMapName(_mapRotation[index]), currentMap, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(index);
            }
        }

        if (candidates.Count == 0)
        {
            return _shuffleRandom.Next(_mapRotation.Count);
        }

        return candidates[_shuffleRandom.Next(candidates.Count)];
    }

    private readonly record struct QueuedNextRoundMap(string LevelName, int AreaIndex);
}
