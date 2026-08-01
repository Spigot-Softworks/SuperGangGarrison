namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private CombatResolver Combat => _combatResolver ??= new CombatResolver(this);
    private CombatResolver? _combatResolver;

    private sealed partial class CombatResolver
    {
        private readonly SimulationWorld _world;
        private readonly List<LevelSolid> _solidRaycastCandidates = new();
        private int[] _solidRaycastCandidateMarks = [];
        private int _solidRaycastCandidateStamp;
        private readonly List<SimpleLevel.IndexedRoomObject> _roomObjectRaycastCandidates = new();
        private int[] _roomObjectRaycastCandidateMarks = [];
        private int _roomObjectRaycastCandidateStamp;
        private readonly Dictionary<ObstacleLineOfSightCacheKey, bool> _obstacleLineOfSightCache = new();
        private SimpleLevel? _solidRaycastIndexLevel;
        private SolidRaycastIndex? _solidRaycastIndex;
        private SimpleLevel? _roomObjectRaycastIndexLevel;
        private RoomObjectRaycastIndex? _roomObjectRaycastIndex;
        private SimpleLevel? _obstacleLineOfSightCacheLevel;
        private long _obstacleLineOfSightCacheFrame = long.MinValue;

        public CombatResolver(SimulationWorld world)
        {
            _world = world;
        }

        private SimpleLevel Level => _world.Level;

        private List<SentryEntity> _sentries => _world._sentries;

        private List<GeneratorState> _generators => _world._generators;

        private List<JumpPadEntity> _jumpPads => _world._jumpPads;

        private IEnumerable<PlayerEntity> EnumerateSimulatedPlayers()
        {
            return _world.EnumerateSimulatedPlayers();
        }

        private IReadOnlyList<LevelSolid> GetPotentialSolidRaycastCandidates(RectangleHitbox rayBounds)
        {
            if (Level.Solids.Count == 0)
            {
                return Array.Empty<LevelSolid>();
            }

            EnsureSolidRaycastIndex();

            _solidRaycastCandidates.Clear();
            _solidRaycastCandidateStamp += 1;
            if (_solidRaycastCandidateStamp == int.MaxValue)
            {
                Array.Clear(_solidRaycastCandidateMarks);
                _solidRaycastCandidateStamp = 1;
            }

            _solidRaycastIndex.AddCandidates(
                rayBounds,
                _solidRaycastCandidateMarks,
                _solidRaycastCandidateStamp,
                _solidRaycastCandidates);
            return _solidRaycastCandidates;
        }

        private IReadOnlyList<SimpleLevel.IndexedRoomObject> GetPotentialRoomObjectRaycastCandidates(RectangleHitbox rayBounds)
        {
            if (Level.RoomObjects.Count == 0)
            {
                return Array.Empty<SimpleLevel.IndexedRoomObject>();
            }

            EnsureRoomObjectRaycastIndex();

            _roomObjectRaycastCandidates.Clear();
            _roomObjectRaycastCandidateStamp += 1;
            if (_roomObjectRaycastCandidateStamp == int.MaxValue)
            {
                Array.Clear(_roomObjectRaycastCandidateMarks);
                _roomObjectRaycastCandidateStamp = 1;
            }

            _roomObjectRaycastIndex!.AddCandidates(
                rayBounds,
                _roomObjectRaycastCandidateMarks,
                _roomObjectRaycastCandidateStamp,
                _roomObjectRaycastCandidates);
            return _roomObjectRaycastCandidates;
        }

        public void WarmSpatialIndices()
        {
            EnsureSolidRaycastIndex();
            EnsureRoomObjectRaycastIndex();
        }

        private void EnsureSolidRaycastIndex()
        {
            if (_solidRaycastIndex is not null && ReferenceEquals(_solidRaycastIndexLevel, Level))
            {
                return;
            }

            _solidRaycastIndexLevel = Level;
            _solidRaycastIndex = SolidRaycastIndex.Build(Level);
            _solidRaycastCandidateMarks = new int[Level.Solids.Count];
            _solidRaycastCandidateStamp = 0;
        }

        private void EnsureRoomObjectRaycastIndex()
        {
            if (_roomObjectRaycastIndex is not null && ReferenceEquals(_roomObjectRaycastIndexLevel, Level))
            {
                return;
            }

            _roomObjectRaycastIndexLevel = Level;
            _roomObjectRaycastIndex = RoomObjectRaycastIndex.Build(Level);
            _roomObjectRaycastCandidateMarks = new int[Level.RoomObjects.Count];
            _roomObjectRaycastCandidateStamp = 0;
        }

        private bool TryGetCachedObstacleLineOfSight(
            float originX,
            float originY,
            float targetX,
            float targetY,
            out bool hasLineOfSight)
        {
            if (_obstacleLineOfSightCacheFrame != _world.Frame
                || !ReferenceEquals(_obstacleLineOfSightCacheLevel, Level))
            {
                _obstacleLineOfSightCache.Clear();
                _obstacleLineOfSightCacheFrame = _world.Frame;
                _obstacleLineOfSightCacheLevel = Level;
            }

            return _obstacleLineOfSightCache.TryGetValue(
                new ObstacleLineOfSightCacheKey(originX, originY, targetX, targetY),
                out hasLineOfSight);
        }

        private void CacheObstacleLineOfSight(
            float originX,
            float originY,
            float targetX,
            float targetY,
            bool hasLineOfSight)
        {
            _obstacleLineOfSightCache[
                new ObstacleLineOfSightCacheKey(originX, originY, targetX, targetY)] = hasLineOfSight;
        }

        private readonly record struct ObstacleLineOfSightCacheKey(
            float OriginX,
            float OriginY,
            float TargetX,
            float TargetY);

        private sealed class SolidRaycastIndex
        {
            private const float CellSize = 128f;
            private readonly IReadOnlyList<LevelSolid> _solids;
            private readonly Dictionary<CellKey, List<int>> _solidIndicesByCell;

            private SolidRaycastIndex(IReadOnlyList<LevelSolid> solids, Dictionary<CellKey, List<int>> solidIndicesByCell)
            {
                _solids = solids;
                _solidIndicesByCell = solidIndicesByCell;
            }

            public static SolidRaycastIndex Build(SimpleLevel level)
            {
                var solidIndicesByCell = new Dictionary<CellKey, List<int>>();
                for (var solidIndex = 0; solidIndex < level.Solids.Count; solidIndex += 1)
                {
                    var solid = level.Solids[solidIndex];
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

                return new SolidRaycastIndex(level.Solids, solidIndicesByCell);
            }

            public void AddCandidates(
                RectangleHitbox rayBounds,
                int[] seenMarks,
                int queryStamp,
                List<LevelSolid> candidates)
            {
                var minCellX = GetCellCoordinate(rayBounds.Left);
                var maxCellX = GetCellCoordinate(rayBounds.Right);
                var minCellY = GetCellCoordinate(rayBounds.Top);
                var maxCellY = GetCellCoordinate(rayBounds.Bottom);
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
                            var solidIndex = solidIndices[index];
                            if (seenMarks[solidIndex] != queryStamp)
                            {
                                seenMarks[solidIndex] = queryStamp;
                                candidates.Add(_solids[solidIndex]);
                            }
                        }
                    }
                }
            }

            private static int GetCellCoordinate(float value)
            {
                return (int)MathF.Floor(value / CellSize);
            }

            private readonly record struct CellKey(int X, int Y);
        }

        private sealed class RoomObjectRaycastIndex
        {
            private const float CellSize = 128f;
            private readonly IReadOnlyList<SimpleLevel.IndexedRoomObject> _roomObjects;
            private readonly Dictionary<CellKey, List<int>> _roomObjectIndicesByCell;

            private RoomObjectRaycastIndex(
                IReadOnlyList<SimpleLevel.IndexedRoomObject> roomObjects,
                Dictionary<CellKey, List<int>> roomObjectIndicesByCell)
            {
                _roomObjects = roomObjects;
                _roomObjectIndicesByCell = roomObjectIndicesByCell;
            }

            public static RoomObjectRaycastIndex Build(SimpleLevel level)
            {
                var roomObjects = level.RoomObjects
                    .Select((marker, index) => new SimpleLevel.IndexedRoomObject(index, marker))
                    .ToArray();
                var roomObjectIndicesByCell = new Dictionary<CellKey, List<int>>();
                for (var roomObjectIndex = 0; roomObjectIndex < roomObjects.Length; roomObjectIndex += 1)
                {
                    var marker = roomObjects[roomObjectIndex].Marker;
                    var minCellX = GetCellCoordinate(marker.Left);
                    var maxCellX = GetCellCoordinate(marker.Right);
                    var minCellY = GetCellCoordinate(marker.Top);
                    var maxCellY = GetCellCoordinate(marker.Bottom);
                    for (var cellY = minCellY; cellY <= maxCellY; cellY += 1)
                    {
                        for (var cellX = minCellX; cellX <= maxCellX; cellX += 1)
                        {
                            var key = new CellKey(cellX, cellY);
                            if (!roomObjectIndicesByCell.TryGetValue(key, out var indices))
                            {
                                indices = [];
                                roomObjectIndicesByCell[key] = indices;
                            }

                            indices.Add(roomObjectIndex);
                        }
                    }
                }

                return new RoomObjectRaycastIndex(roomObjects, roomObjectIndicesByCell);
            }

            public void AddCandidates(
                RectangleHitbox rayBounds,
                int[] seenMarks,
                int queryStamp,
                List<SimpleLevel.IndexedRoomObject> candidates)
            {
                var minCellX = GetCellCoordinate(rayBounds.Left);
                var maxCellX = GetCellCoordinate(rayBounds.Right);
                var minCellY = GetCellCoordinate(rayBounds.Top);
                var maxCellY = GetCellCoordinate(rayBounds.Bottom);
                for (var cellY = minCellY; cellY <= maxCellY; cellY += 1)
                {
                    for (var cellX = minCellX; cellX <= maxCellX; cellX += 1)
                    {
                        if (!_roomObjectIndicesByCell.TryGetValue(new CellKey(cellX, cellY), out var roomObjectIndices))
                        {
                            continue;
                        }

                        for (var index = 0; index < roomObjectIndices.Count; index += 1)
                        {
                            var roomObjectIndex = roomObjectIndices[index];
                            if (seenMarks[roomObjectIndex] != queryStamp)
                            {
                                seenMarks[roomObjectIndex] = queryStamp;
                                candidates.Add(_roomObjects[roomObjectIndex]);
                            }
                        }
                    }
                }
            }

            private static int GetCellCoordinate(float value) =>
                (int)MathF.Floor(value / CellSize);

            private readonly record struct CellKey(int X, int Y);
        }
    }
}
