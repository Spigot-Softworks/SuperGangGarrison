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
    PlayerTeam? WinnerTeam);

sealed class MapRotationManager
{
    private readonly SimulationWorld _world;
    private readonly Action<string> _log;
    private readonly List<string> _mapRotation;
    private int _mapRotationIndex;
    private QueuedNextRoundMap? _queuedNextRoundMap;

    public MapRotationManager(SimulationWorld world, string? requestedMap, string? mapRotationFile, IReadOnlyList<string> stockMapRotation, Action<string> log)
    {
        _world = world;
        _log = log;

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
        var preserveStats = false;
        string nextMap;
        var nextArea = 1;

        if (_queuedNextRoundMap is { } queuedNextRoundMap)
        {
            nextMap = queuedNextRoundMap.LevelName;
            nextArea = queuedNextRoundMap.AreaIndex;
            _queuedNextRoundMap = null;
            _log($"[server] advancing to voted next round map {nextMap} area {nextArea}");
        }
        else if (winner == PlayerTeam.Red && currentArea < totalAreas)
        {
            nextMap = currentLevelName;
            nextArea = currentArea + 1;
            preserveStats = true;
            _log($"[server] advancing to {nextMap} area {nextArea}/{totalAreas} (winner red)");
        }
        else if (_mapRotation.Count > 0)
        {
            _mapRotationIndex = (_mapRotationIndex + 1) % _mapRotation.Count;
            nextMap = _mapRotation[_mapRotationIndex];
            _log($"[server] advancing to next map {nextMap} (winner {(winner.HasValue ? winner.Value.ToString() : "tie")})");
        }
        else
        {
            nextMap = currentLevelName;
            _log("[server] map rotation empty; restarting current map.");
        }

        transition = new MapChangeTransition(
            currentLevelName,
            currentArea,
            totalAreas,
            nextMap,
            nextArea,
            preserveStats,
            winner);

        if (!_world.ApplyPendingMapChange(nextMap, nextArea, preserveStats))
        {
            _log($"[server] failed to apply map change to {nextMap}; restarting round.");
            transition = default;
            return false;
        }

        AlignCurrentMap(_world.Level.Name);
        _log($"[server] now running {_world.Level.Name} area {_world.Level.MapAreaIndex}/{_world.Level.MapAreaCount}");
        return true;
    }

    public IReadOnlyList<string> MapRotation => _mapRotation;

    public int CurrentRotationIndex => _mapRotationIndex;

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
            return;
        }

        if (_mapRotation.Count == 0)
        {
            _mapRotationIndex = 0;
            return;
        }

        var requestedRotationMap = _mapRotation[0];
        if (!_world.TryLoadLevel(requestedRotationMap, mapAreaIndex: 1, preservePlayerStats: false))
        {
            _log($"[server] unknown map \"{requestedRotationMap}\"; falling back to {_world.Level.Name}.");
        }

        _mapRotationIndex = Math.Max(0, FindMapRotationIndex(_mapRotation, _world.Level.Name));
    }

    private readonly record struct QueuedNextRoundMap(string LevelName, int AreaIndex);
}
