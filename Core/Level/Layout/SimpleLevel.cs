using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed class SimpleLevel
{
    private readonly Dictionary<RoomObjectType, RoomObjectMarker[]> _roomObjectsByType;
    private readonly SpatialSolidIndex _solidIndex;
    private bool _controlPointSetupGatesActive;
    private TeamGateLockMask _forcedBlockingTeamGates;
    private BlockingTeamGateCacheKey _blockingTeamGateCacheKey;
    private IReadOnlyList<RoomObjectMarker>? _blockingTeamGateCache;

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
        MapLogicGraph? logicGraph = null,
        MapLogicActivatorSet? logicActivators = null)
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
        ControlPointSettings = controlPointSettings ?? CustomMapControlPointSettings.Default;
        LogicGraph = logicGraph ?? MapLogicGraph.Empty;
        LogicActivators = logicActivators ?? MapLogicActivatorSet.Empty;
        RoomObjectLogicActiveMask = new bool[roomObjects.Count];
        Array.Fill(RoomObjectLogicActiveMask, true);
        _roomObjectsByType = RoomObjects
            .GroupBy(roomObject => roomObject.Type)
            .ToDictionary(group => group.Key, group => group.ToArray());
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

    public CustomMapControlPointSettings ControlPointSettings { get; }

    public MapLogicGraph LogicGraph { get; }

    public MapLogicActivatorSet LogicActivators { get; }

    public bool[] RoomObjectLogicActiveMask { get; private set; }

    public bool IsRoomObjectActive(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= RoomObjectLogicActiveMask.Length)
        {
            return true;
        }

        return RoomObjectLogicActiveMask[roomObjectIndex];
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
            _blockingTeamGateCache = null;
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
            _blockingTeamGateCache = null;
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

    public IReadOnlyList<RoomObjectMarker> GetBlockingTeamGates(PlayerTeam team, bool carryingIntel)
    {
        var cacheKey = new BlockingTeamGateCacheKey(team, carryingIntel, ControlPointSetupGatesActive, ForcedBlockingTeamGates);
        if (_blockingTeamGateCache is not null && _blockingTeamGateCacheKey.Equals(cacheKey))
        {
            return _blockingTeamGateCache;
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

        _blockingTeamGateCacheKey = cacheKey;
        _blockingTeamGateCache = blockingGates.Count == 0 ? Array.Empty<RoomObjectMarker>() : blockingGates.ToArray();
        return _blockingTeamGateCache;
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
