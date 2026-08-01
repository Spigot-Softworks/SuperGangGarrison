using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed class SimpleLevel
{
    private readonly Dictionary<RoomObjectType, RoomObjectMarker[]> _roomObjectsByType;
    private readonly IndexedRoomObject[] _playerWalls;
    private readonly IndexedRoomObject[] _barriers;
    private readonly IndexedRoomObject[] _directionalWalls;
    private readonly IndexedRoomObject[] _damageableZones;
    private readonly SpatialSolidIndex _solidIndex;
    private bool _controlPointSetupGatesActive;
    private TeamGateLockMask _forcedBlockingTeamGates;
    private readonly ConcurrentDictionary<BlockingTeamGateCacheKey, RoomObjectMarker[]> _blockingTeamGateCaches = [];

    public SimpleLevel(
        string name,
        GameModeKind mode,
        WorldBounds bounds,
        float mapScale,
        string? backgroundAssetName,
        int mapAreaIndex,
        int mapAreaCount,
        SpawnPoint localSpawn,
        IReadOnlyList<SpawnPoint> redSpawns,
        IReadOnlyList<SpawnPoint> blueSpawns,
        IReadOnlyList<IntelBaseMarker> intelBases,
        IReadOnlyList<RoomObjectMarker> roomObjects,
        float floorY,
        IReadOnlyList<LevelSolid> solids,
        bool importedFromSource,
        IReadOnlyList<AreaTransitionMarker>? areaTransitionMarkers = null,
        IReadOnlyList<string>? unsupportedSourceEntities = null,
        CustomMapVisualMetadata? customMapVisuals = null,
        IReadOnlyList<MovingPlatformMarker>? movingPlatforms = null,
        CustomMapControlPointSettings? controlPointSettings = null,
        CustomMapScrSettings? scrSettings = null,
        bool showControlPoints = false,
        MapLogicGraph? logicGraph = null,
        MapLogicActivatorSet? logicActivators = null,
        MapLogicScoreTriggerSet? logicScoreTriggers = null,
        SpritesheetPlaybackSet? spritesheetPlaybackSet = null,
        IReadOnlyList<HealthPackSpawnMarker>? healthPackSpawns = null,
        IReadOnlyList<BotSpawnMarker>? botSpawns = null,
        IReadOnlyList<GameplayMessageMarker>? gameplayMessages = null,
        IReadOnlyList<GameplaySoundMarker>? gameplaySounds = null,
        IReadOnlyList<SpawnClassBehaviorMarker>? spawnClassBehaviors = null)
    {
        Name = name;
        Mode = mode;
        Bounds = bounds;
        MapScale = mapScale;
        BackgroundAssetName = backgroundAssetName;
        MapAreaIndex = mapAreaIndex;
        MapAreaCount = mapAreaCount;
        LocalSpawn = localSpawn;
        RedSpawns = redSpawns;
        BlueSpawns = blueSpawns;
        IntelBases = intelBases;
        RoomObjects = roomObjects;
        FloorY = floorY;
        Solids = solids;
        ImportedFromSource = importedFromSource;
        AreaTransitionMarkers = areaTransitionMarkers ?? Array.Empty<AreaTransitionMarker>();
        UnsupportedSourceEntities = unsupportedSourceEntities ?? Array.Empty<string>();
        CustomMapVisuals = customMapVisuals ?? CustomMapVisualMetadata.Empty;
        MovingPlatforms = movingPlatforms ?? Array.Empty<MovingPlatformMarker>();
        HealthPackSpawns = healthPackSpawns ?? Array.Empty<HealthPackSpawnMarker>();
        BotSpawns = botSpawns ?? Array.Empty<BotSpawnMarker>();
        GameplayMessages = gameplayMessages ?? Array.Empty<GameplayMessageMarker>();
        GameplaySounds = gameplaySounds ?? Array.Empty<GameplaySoundMarker>();
        SpawnClassBehaviors = spawnClassBehaviors ?? Array.Empty<SpawnClassBehaviorMarker>();
        ControlPointSettings = controlPointSettings ?? CustomMapControlPointSettings.Default;
        ScrSettings = scrSettings ?? CustomMapScrSettings.Default;
        ShowControlPoints = showControlPoints;
        LogicGraph = logicGraph ?? MapLogicGraph.Empty;
        LogicActivators = logicActivators ?? MapLogicActivatorSet.Empty;
        LogicScoreTriggers = logicScoreTriggers ?? MapLogicScoreTriggerSet.Empty;
        SpritesheetPlaybackSet = spritesheetPlaybackSet ?? SpritesheetPlaybackSet.Empty;
        SpritesheetPlaybackState = new SpritesheetPlaybackState(roomObjects.Count);
        SpritesheetPlaybackState.ResetFromConfiguration(SpritesheetPlaybackSet);
        RoomObjectLogicActiveMask = new bool[roomObjects.Count];
        Array.Fill(RoomObjectLogicActiveMask, true);
        _roomObjectsByType = RoomObjects
            .GroupBy(roomObject => roomObject.Type)
            .ToDictionary(group => group.Key, group => group.ToArray());
        _playerWalls = BuildIndexedRoomObjects(RoomObjectType.PlayerWall);
        _barriers = BuildIndexedRoomObjects(RoomObjectType.Barrier);
        _directionalWalls = BuildIndexedRoomObjects(RoomObjectType.DirectionalWall);
        _damageableZones = BuildIndexedRoomObjects(RoomObjectType.DamageableZone);
        _solidIndex = SpatialSolidIndex.Build(Solids);
    }

    public string Name { get; }

    public GameModeKind Mode { get; }

    public WorldBounds Bounds { get; }

    public float MapScale { get; }

    public string? BackgroundAssetName { get; }

    public int MapAreaIndex { get; }

    public int MapAreaCount { get; }

    public SpawnPoint LocalSpawn { get; }

    public IReadOnlyList<SpawnPoint> RedSpawns { get; }

    public IReadOnlyList<SpawnPoint> BlueSpawns { get; }

    public IReadOnlyList<IntelBaseMarker> IntelBases { get; }

    public IReadOnlyList<RoomObjectMarker> RoomObjects { get; }

    public float FloorY { get; }

    public IReadOnlyList<LevelSolid> Solids { get; }

    public bool ImportedFromSource { get; }

    public IReadOnlyList<AreaTransitionMarker> AreaTransitionMarkers { get; }

    public IReadOnlyList<string> UnsupportedSourceEntities { get; }

    public CustomMapVisualMetadata CustomMapVisuals { get; }

    public IReadOnlyList<MovingPlatformMarker> MovingPlatforms { get; }

    public IReadOnlyList<HealthPackSpawnMarker> HealthPackSpawns { get; }

    public IReadOnlyList<BotSpawnMarker> BotSpawns { get; }

    public IReadOnlyList<GameplayMessageMarker> GameplayMessages { get; }

    public IReadOnlyList<GameplaySoundMarker> GameplaySounds { get; }

    public IReadOnlyList<SpawnClassBehaviorMarker> SpawnClassBehaviors { get; }

    public CustomMapControlPointSettings ControlPointSettings { get; }

    public CustomMapScrSettings ScrSettings { get; }

    public bool ShowControlPoints { get; }

    public MapLogicGraph LogicGraph { get; }

    public MapLogicActivatorSet LogicActivators { get; }

    public MapLogicScoreTriggerSet LogicScoreTriggers { get; }

    public SpritesheetPlaybackSet SpritesheetPlaybackSet { get; }

    public SpritesheetPlaybackState SpritesheetPlaybackState { get; }

    public bool ShouldSimulateControlPoints =>
        ShowControlPoints
        || Mode is GameModeKind.ControlPoint or GameModeKind.Scr
        || Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill;

    public bool[] RoomObjectLogicActiveMask { get; private set; }

    public float[]? DamageableZoneCurrentHealth { get; internal set; }

    public float GetDamageableZoneCurrentHealth(int roomObjectIndex, in RoomObjectMarker marker)
    {
        if (marker.Type != RoomObjectType.DamageableZone)
        {
            return 0f;
        }

        if (DamageableZoneCurrentHealth is not null
            && roomObjectIndex >= 0
            && roomObjectIndex < DamageableZoneCurrentHealth.Length)
        {
            return DamageableZoneCurrentHealth[roomObjectIndex];
        }

        return marker.DamageableZone.MaxHealth;
    }

    public bool IsRoomObjectActive(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= RoomObjectLogicActiveMask.Length)
        {
            return true;
        }

        if (!RoomObjectLogicActiveMask[roomObjectIndex])
        {
            return false;
        }

        if (roomObjectIndex >= RoomObjects.Count)
        {
            return true;
        }

        var marker = RoomObjects[roomObjectIndex];
        if (marker.Type != RoomObjectType.AreaExtension)
        {
            return true;
        }

        var parentIndex = marker.AreaExtension.ParentRoomObjectIndex;
        return parentIndex < 0 || IsRoomObjectActive(parentIndex);
    }

    public bool ControlPointSetupGatesActive
    {
        get => _controlPointSetupGatesActive;
        set
        {
            if (_controlPointSetupGatesActive == value)
            {
                return;
            }

            _controlPointSetupGatesActive = value;
            _blockingTeamGateCaches.Clear();
        }
    }

    public TeamGateLockMask ForcedBlockingTeamGates
    {
        get => _forcedBlockingTeamGates;
        set
        {
            if (_forcedBlockingTeamGates == value)
            {
                return;
            }

            _forcedBlockingTeamGates = value;
            _blockingTeamGateCaches.Clear();
        }
    }

    public SpawnPoint GetSpawn(PlayerTeam team, int spawnIndex)
    {
        var teamSpawns = team == PlayerTeam.Blue ? BlueSpawns : RedSpawns;
        if (teamSpawns.Count == 0)
        {
            return LocalSpawn;
        }

        return teamSpawns[spawnIndex % teamSpawns.Count];
    }

    public IntelBaseMarker? GetIntelBase(PlayerTeam team)
    {
        return IntelBases
            .Where(intelBase => intelBase.Team == team)
            .Cast<IntelBaseMarker?>()
            .FirstOrDefault();
    }

    public RoomObjectMarker? GetFirstRoomObject(RoomObjectType type)
    {
        return RoomObjects
            .Where(roomObject => roomObject.Type == type)
            .Cast<RoomObjectMarker?>()
            .FirstOrDefault();
    }

    public IReadOnlyList<RoomObjectMarker> GetRoomObjects(RoomObjectType type)
    {
        return _roomObjectsByType.TryGetValue(type, out var roomObjects)
            ? roomObjects
            : Array.Empty<RoomObjectMarker>();
    }

    // Collision probes run once or more per movement tick. Keep the hot path
    // indexed by collision-relevant marker type while retaining the original
    // room-object index for logic-activation checks. This is behaviorally
    // identical to scanning RoomObjects and skipping unrelated markers.
    internal IReadOnlyList<IndexedRoomObject> PlayerWalls => _playerWalls;

    internal IReadOnlyList<IndexedRoomObject> Barriers => _barriers;

    internal IReadOnlyList<IndexedRoomObject> DirectionalWalls => _directionalWalls;

    internal IReadOnlyList<IndexedRoomObject> DamageableZones => _damageableZones;

    public IReadOnlyList<RoomObjectMarker> GetBlockingTeamGates(PlayerTeam team, bool carryingIntel)
    {
        var cacheKey = new BlockingTeamGateCacheKey(team, carryingIntel, ControlPointSetupGatesActive, ForcedBlockingTeamGates);
        if (_blockingTeamGateCaches.TryGetValue(cacheKey, out var cachedGates))
        {
            return cachedGates;
        }

        var blockingGates = new List<RoomObjectMarker>();
        for (var index = 0; index < RoomObjects.Count; index += 1)
        {
            var roomObject = RoomObjects[index];
            if (!IsRoomObjectActive(index))
            {
                continue;
            }

            switch (roomObject.Type)
            {
                case RoomObjectType.ControlPointSetupGate:
                    if (ControlPointSetupGatesActive)
                    {
                        blockingGates.Add(roomObject);
                    }
                    break;
                case RoomObjectType.TeamGate:
                    if (roomObject.Team.HasValue && IsForcedBlockingTeamGate(roomObject.Team.Value))
                    {
                        blockingGates.Add(roomObject);
                        break;
                    }

                    if (carryingIntel || (roomObject.Team.HasValue && roomObject.Team.Value != team))
                    {
                        blockingGates.Add(roomObject);
                    }
                    break;
                case RoomObjectType.IntelGate:
                    if (IsIntelGateBlocking(roomObject, team, carryingIntel))
                    {
                        blockingGates.Add(roomObject);
                    }
                    break;
            }
        }

        cachedGates = blockingGates.Count == 0 ? Array.Empty<RoomObjectMarker>() : blockingGates.ToArray();
        return _blockingTeamGateCaches.GetOrAdd(cacheKey, cachedGates);
    }

    public bool IntersectsSolid(float left, float top, float right, float bottom)
    {
        return _solidIndex.Intersects(left, top, right, bottom);
    }

    public float? FindBlockingSolidTop(float left, float top, float right, float bottom)
    {
        return _solidIndex.FindTop(left, top, right, bottom);
    }

    private bool IsForcedBlockingTeamGate(PlayerTeam team)
    {
        return team switch
        {
            PlayerTeam.Red => (ForcedBlockingTeamGates & TeamGateLockMask.Red) != 0,
            PlayerTeam.Blue => (ForcedBlockingTeamGates & TeamGateLockMask.Blue) != 0,
            _ => false,
        };
    }

    private static bool IsIntelGateBlocking(RoomObjectMarker roomObject, PlayerTeam team, bool carryingIntel)
    {
        if (carryingIntel)
        {
            return false;
        }

        if (roomObject.Team.HasValue)
        {
            return roomObject.Team.Value != team;
        }

        return true;
    }

    private readonly record struct BlockingTeamGateCacheKey(
        PlayerTeam Team,
        bool CarryingIntel,
        bool ControlPointSetupGatesActive,
        TeamGateLockMask ForcedBlockingTeamGates);

    internal readonly record struct IndexedRoomObject(int Index, RoomObjectMarker Marker);

    private IndexedRoomObject[] BuildIndexedRoomObjects(RoomObjectType type)
    {
        return RoomObjects
            .Select((marker, index) => new IndexedRoomObject(index, marker))
            .Where(entry => entry.Marker.Type == type)
            .ToArray();
    }

    private sealed class SpatialSolidIndex
    {
        private const float CellSize = 128f;
        private readonly IReadOnlyList<LevelSolid> _solids;
        private readonly Dictionary<CellKey, List<int>> _solidIndicesByCell;

        private SpatialSolidIndex(IReadOnlyList<LevelSolid> solids, Dictionary<CellKey, List<int>> solidIndicesByCell)
        {
            _solids = solids;
            _solidIndicesByCell = solidIndicesByCell;
        }

        public static SpatialSolidIndex Build(IReadOnlyList<LevelSolid> solids)
        {
            var solidIndicesByCell = new Dictionary<CellKey, List<int>>();
            for (var solidIndex = 0; solidIndex < solids.Count; solidIndex += 1)
            {
                var solid = solids[solidIndex];
                var minCellX = GetCellCoordinate(solid.Left);
                var maxCellX = GetCellCoordinate(solid.Right);
                var minCellY = GetCellCoordinate(solid.Top);
                var maxCellY = GetCellCoordinate(solid.Bottom);
                for (var cellY = minCellY; cellY <= maxCellY; cellY += 1)
                {
                    for (var cellX = minCellX; cellX <= maxCellX; cellX += 1)
                    {
                        var key = new CellKey(cellX, cellY);
                        if (!solidIndicesByCell.TryGetValue(key, out var indices))
                        {
                            indices = [];
                            solidIndicesByCell[key] = indices;
                        }

                        indices.Add(solidIndex);
                    }
                }
            }

            return new SpatialSolidIndex(solids, solidIndicesByCell);
        }

        public bool Intersects(float left, float top, float right, float bottom)
        {
            if (_solids.Count == 0)
            {
                return false;
            }

            var minCellX = GetCellCoordinate(left);
            var maxCellX = GetCellCoordinate(right);
            var minCellY = GetCellCoordinate(top);
            var maxCellY = GetCellCoordinate(bottom);
            for (var cellY = minCellY; cellY <= maxCellY; cellY += 1)
            {
                for (var cellX = minCellX; cellX <= maxCellX; cellX += 1)
                {
                    if (!_solidIndicesByCell.TryGetValue(new CellKey(cellX, cellY), out var solidIndices))
                    {
                        continue;
                    }

                    for (var index = 0; index < solidIndices.Count; index += 1)
                    {
                        var solid = _solids[solidIndices[index]];
                        if (left < solid.Right && right > solid.Left && top < solid.Bottom && bottom > solid.Top)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public float? FindTop(float left, float top, float right, float bottom)
        {
            if (_solids.Count == 0)
            {
                return null;
            }

            float? obstacleTop = null;
            var minCellX = GetCellCoordinate(left);
            var maxCellX = GetCellCoordinate(right);
            var minCellY = GetCellCoordinate(top);
            var maxCellY = GetCellCoordinate(bottom);
            for (var cellY = minCellY; cellY <= maxCellY; cellY += 1)
            {
                for (var cellX = minCellX; cellX <= maxCellX; cellX += 1)
                {
                    if (!_solidIndicesByCell.TryGetValue(new CellKey(cellX, cellY), out var solidIndices))
                    {
                        continue;
                    }

                    for (var index = 0; index < solidIndices.Count; index += 1)
                    {
                        var solid = _solids[solidIndices[index]];
                        if (left < solid.Right && right > solid.Left && top < solid.Bottom && bottom > solid.Top)
                        {
                            obstacleTop = obstacleTop.HasValue ? MathF.Min(obstacleTop.Value, solid.Top) : solid.Top;
                        }
                    }
                }
            }

            return obstacleTop;
        }

        private static int GetCellCoordinate(float value)
        {
            return (int)MathF.Floor(value / CellSize);
        }

        private readonly record struct CellKey(int X, int Y);
    }
}
