namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private CombatResolver Combat => _combatResolver ??= new CombatResolver(this);
    private CombatResolver? _combatResolver;

    private sealed partial class CombatResolver
    {
        private readonly SimulationWorld _world;
        private readonly List<LevelSolid> _solidRaycastCandidates = new();
        private readonly HashSet<int> _solidRaycastCandidateIndices = new();
        private SimpleLevel? _solidRaycastIndexLevel;
        private SolidRaycastIndex? _solidRaycastIndex;

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

            if (_solidRaycastIndex is null || !ReferenceEquals(_solidRaycastIndexLevel, Level))
            {
                _solidRaycastIndexLevel = Level;
                _solidRaycastIndex = SolidRaycastIndex.Build(Level);
            }

            _solidRaycastCandidates.Clear();
            _solidRaycastCandidateIndices.Clear();
            _solidRaycastIndex.AddCandidates(rayBounds, _solidRaycastCandidateIndices, _solidRaycastCandidates);
            return _solidRaycastCandidates;
        }

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

            public void AddCandidates(RectangleHitbox rayBounds, HashSet<int> seenIndices, List<LevelSolid> candidates)
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
                            if (seenIndices.Add(solidIndex))
                            {
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
    }
}
