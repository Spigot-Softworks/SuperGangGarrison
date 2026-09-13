using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed class SimpleLevel
{
    private readonly RoomObjectMarker[] _roomObjectMarkers;
    private readonly Dictionary<RoomObjectType, RoomObjectMarker[]> _roomObjectsByType;
    private readonly IndexedRoomObject[] _playerWalls;
    private readonly IndexedRoomObject[] _barriers;
    private readonly IndexedRoomObject[] _directionalWalls;
    private readonly IndexedRoomObject[] _damageableZones;
    private readonly SpatialSolidIndex _solidIndex;
    private readonly Dictionary<RoomObjectType, int[]> _roomObjectIndicesByType;
    private readonly int[] _moveBoxIndices;
    private readonly int[] _hazardIndices;
    private readonly int[] _teleportCandidateIndices;
    private readonly int[] _gateIndices;
    private readonly int[] _hitscanObstacleIndices;
    private readonly int[] _projectileObstacleIndices;
    private readonly int[] _roomObjectParentIndices;
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
        IReadOnlyList<JumpPadSpawnMarker>? jumpPadSpawns = null,
        IReadOnlyList<BotSpawnMarker>? botSpawns = null,
        IReadOnlyList<GameplayMessageMarker>? gameplayMessages = null,
        IReadOnlyList<GameplaySoundMarker>? gameplaySounds = null,
        IReadOnlyList<SpawnClassBehaviorMarker>? spawnClassBehaviors = null,
        bool isTopDown = false)
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
        _roomObjectMarkers = roomObjects as RoomObjectMarker[] ?? roomObjects.ToArray();
        RoomObjects = _roomObjectMarkers;
        FloorY = floorY;
        Solids = solids;
        ImportedFromSource = importedFromSource;
        AreaTransitionMarkers = areaTransitionMarkers ?? Array.Empty<AreaTransitionMarker>();
        UnsupportedSourceEntities = unsupportedSourceEntities ?? Array.Empty<string>();
        CustomMapVisuals = customMapVisuals ?? CustomMapVisualMetadata.Empty;
        MovingPlatforms = movingPlatforms ?? Array.Empty<MovingPlatformMarker>();
        HealthPackSpawns = healthPackSpawns ?? Array.Empty<HealthPackSpawnMarker>();
        JumpPadSpawns = jumpPadSpawns ?? Array.Empty<JumpPadSpawnMarker>();
        BotSpawns = botSpawns ?? Array.Empty<BotSpawnMarker>();
        GameplayMessages = gameplayMessages ?? Array.Empty<GameplayMessageMarker>();
        GameplaySounds = gameplaySounds ?? Array.Empty<GameplaySoundMarker>();
        SpawnClassBehaviors = spawnClassBehaviors ?? Array.Empty<SpawnClassBehaviorMarker>();
        IsTopDown = isTopDown;
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
        _roomObjectIndicesByType = Enumerable.Range(0, RoomObjects.Count)
            .GroupBy(index => RoomObjects[index].Type)
            .ToDictionary(group => group.Key, group => group.ToArray());
        _roomObjectParentIndices = Enumerable.Repeat(-1, RoomObjects.Count).ToArray();
        foreach (var index in GetRoomObjectIndices(RoomObjectType.AreaExtension))
            _roomObjectParentIndices[index] = RoomObjects[index].AreaExtension.ParentRoomObjectIndex;
        _moveBoxIndices = BuildOrderedIndices(RoomObjectType.MoveBoxUp, RoomObjectType.MoveBoxDown,
            RoomObjectType.MoveBoxLeft, RoomObjectType.MoveBoxRight);
        _hazardIndices = BuildOrderedIndices(RoomObjectType.FragBox, RoomObjectType.KillBox, RoomObjectType.FireBox);
        _teleportCandidateIndices = BuildOrderedIndices(RoomObjectType.TeleportZone, RoomObjectType.AreaExtension);
        _gateIndices = BuildOrderedIndices(RoomObjectType.TeamGate, RoomObjectType.ControlPointSetupGate);
        _hitscanObstacleIndices = BuildOrderedIndices(RoomObjectType.TeamGate, RoomObjectType.ControlPointSetupGate,
            RoomObjectType.BulletWall, RoomObjectType.IntelGate);
        // A conservative union for all projectile profiles; flames also hit
        // cabinets. Each caller still applies its original live collision rules.
        _projectileObstacleIndices = BuildOrderedIndices(RoomObjectType.TeamGate, RoomObjectType.ControlPointSetupGate,
            RoomObjectType.BulletWall, RoomObjectType.Barrier, RoomObjectType.DirectionalWall,
            RoomObjectType.DamageableZone, RoomObjectType.HealingCabinet);
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

    public IReadOnlyList<JumpPadSpawnMarker> JumpPadSpawns { get; }

    public IReadOnlyList<BotSpawnMarker> BotSpawns { get; }

    public IReadOnlyList<GameplayMessageMarker> GameplayMessages { get; }

    public IReadOnlyList<GameplaySoundMarker> GameplaySounds { get; }

    public IReadOnlyList<SpawnClassBehaviorMarker> SpawnClassBehaviors { get; }

    public CustomMapControlPointSettings ControlPointSettings { get; }

    public CustomMapScrSettings ScrSettings { get; }

    public bool ShowControlPoints { get; }

    public bool IsTopDown { get; }

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

        // Activation is queried for every collision probe. The immutable parent
        // relation needs only an integer, not a copy of the full marker record.
        var parentIndex = _roomObjectParentIndices[roomObjectIndex];
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
    internal ReadOnlySpan<IndexedRoomObject> PlayerWalls => _playerWalls;

    internal ReadOnlySpan<IndexedRoomObject> Barriers => _barriers;

    internal ReadOnlySpan<IndexedRoomObject> DirectionalWalls => _directionalWalls;

    internal ReadOnlySpan<IndexedRoomObject> DamageableZones => _damageableZones;

    // RoomObjectMarker is a large value type. Scan only relevant indices on
    // each player tick, retaining source order and live activation checks.
    internal ReadOnlySpan<int> GetRoomObjectIndices(RoomObjectType type)
        => _roomObjectIndicesByType.TryGetValue(type, out var indices) ? indices : [];

    internal ReadOnlySpan<int> MoveBoxIndices => _moveBoxIndices;
    internal ReadOnlySpan<int> HazardIndices => _hazardIndices;
    internal ReadOnlySpan<int> TeleportCandidateIndices => _teleportCandidateIndices;
    internal ReadOnlySpan<int> GateIndices => _gateIndices;
    internal ReadOnlySpan<int> HitscanObstacleIndices => _hitscanObstacleIndices;
    internal ReadOnlySpan<int> ProjectileObstacleIndices => _projectileObstacleIndices;

    internal ref readonly RoomObjectMarker GetRoomObject(int index) => ref _roomObjectMarkers[index];

    private int[] BuildOrderedIndices(params RoomObjectType[] types)
        => types.SelectMany(type => _roomObjectIndicesByType.GetValueOrDefault(type) ?? [])
            .Order().ToArray();

    public bool ContainsSolidPoint(float x, float y) => _solidIndex.ContainsPoint(x, y);

    public IReadOnlyList<RoomObjectMarker> GetBlockingTeamGates(PlayerTeam team, bool carryingIntel)
        => GetBlockingTeamGateArray(team, carryingIntel);

    internal ReadOnlySpan<RoomObjectMarker> GetBlockingTeamGateSpan(PlayerTeam team, bool carryingIntel)
        => GetBlockingTeamGateArray(team, carryingIntel);

    private RoomObjectMarker[] GetBlockingTeamGateArray(PlayerTeam team, bool carryingIntel)
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
        => IntelGateCollision.BlocksPlayer(roomObject.Team, team, carryingIntel);

    private readonly record struct BlockingTeamGateCacheKey(
        PlayerTeam Team,
        bool CarryingIntel,
        bool ControlPointSetupGatesActive,
        TeamGateLockMask ForcedBlockingTeamGates);

    internal readonly struct IndexedRoomObject(int index, RoomObjectMarker marker)
    {
        public readonly int Index = index;
        public readonly RoomObjectMarker Marker = marker;
    }

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
        private readonly LevelSolid[] _solids;
        private readonly Dictionary<CellKey, List<int>> _solidIndicesByCell;

        private SpatialSolidIndex(IReadOnlyList<LevelSolid> solids, Dictionary<CellKey, List<int>> solidIndicesByCell)
        {
            _solids = solids as LevelSolid[] ?? solids.ToArray();
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
            if (_solids.Length == 0)
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

        public bool ContainsPoint(float x, float y)
        {
            if (!_solidIndicesByCell.TryGetValue(new CellKey(GetCellCoordinate(x), GetCellCoordinate(y)), out var indices))
                return false;
            foreach (var index in indices)
            {
                var solid = _solids[index];
                if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
                    return true;
            }
            return false;
        }

        public float? FindTop(float left, float top, float right, float bottom)
        {
            if (_solids.Length == 0)
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
