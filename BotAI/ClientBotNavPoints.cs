using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

internal sealed class ClientBotNavPoints
{
    private const int GridStep = 6;
    private const int HeadroomStart = 6;
    private const int HeadroomEnd = 36;
    private const int FlatEdgeLookahead = 24;
    private const int FlatEdgeLookback = 19;
    private const int DropProbeDepth = 24;
    private const int InteriorCornerProbeNear = 13;
    private const int InteriorCornerProbeFar = 25;
    private const int InteriorCornerRightNear = 18;
    private const int InteriorCornerRightFar = 30;
    private const int InteriorCornerVerticalTolerance = 60;
    private const float ConnectionClearanceHeight = 12f;
    private const float MaximumSlope = 4f;
    private const float MaximumDirectedTraversalCost = 168f;
    private const float GapSupportProbeDepth = 54f;

    private readonly BotNavigationNode[] _points;
    private readonly BotNavigationNode[] _pointsByX;
    private readonly Dictionary<int, BotNavigationNode> _pointsById;
    private readonly Dictionary<int, List<BotNavigationEdge>> _outgoingByPointId = new();
    private readonly Dictionary<long, BotNavigationEdge> _edgeByPair = new();
    private readonly HashSet<long> _reverseBlockedPairs = new();
    private readonly Dictionary<string, int[]?> _weightCache = new(StringComparer.Ordinal);
    private readonly ObstacleGrid? _obstacles;

    private ClientBotNavPoints(SimpleLevel level, BotNavigationNode[] points, ObstacleGrid obstacles)
    {
        CacheKey = $"{level.Name}|{level.MapAreaIndex}|clientbot-navpoints|{level.Bounds.Width}x{level.Bounds.Height}|{points.Length}";
        _points = points;
        _pointsByX = points
            .OrderBy(static point => point.X)
            .ThenBy(static point => point.Y)
            .ThenBy(static point => point.Id)
            .ToArray();
        _pointsById = points.ToDictionary(static point => point.Id);
        _obstacles = obstacles;
        BuildConnections();
        ApplyStockMapReverseBlockPatches(level.Name);
    }

    private ClientBotNavPoints(BotNavigationAsset asset, ObstacleGrid? obstacles)
    {
        CacheKey = $"{asset.LevelName}|{asset.MapAreaIndex}|clientbot-navpoints-asset|{asset.LevelFingerprint}|{asset.Nodes.Count}|{asset.Edges.Count}";
        _points = asset.Nodes.ToArray();
        _pointsByX = _points
            .OrderBy(static point => point.X)
            .ThenBy(static point => point.Y)
            .ThenBy(static point => point.Id)
            .ToArray();
        _pointsById = _points.ToDictionary(static point => point.Id);
        _obstacles = obstacles;

        for (var nodeIndex = 0; nodeIndex < _points.Length; nodeIndex += 1)
        {
            var node = _points[nodeIndex];
            for (var blockedIndex = 0; blockedIndex < node.ReverseOnlyBlockedFromNodeIds.Count; blockedIndex += 1)
            {
                _reverseBlockedPairs.Add(GetPairKey(node.Id, node.ReverseOnlyBlockedFromNodeIds[blockedIndex]));
            }
        }

        for (var edgeIndex = 0; edgeIndex < asset.Edges.Count; edgeIndex += 1)
        {
            AddConnection(asset.Edges[edgeIndex]);
        }
    }

    public string CacheKey { get; }

    public IReadOnlyList<BotNavigationNode> Points => _points;

    public static ClientBotNavPoints Build(SimpleLevel level)
    {
        var obstacles = ObstacleGrid.Build(level.Bounds, ModernObstacleGeometry.BuildStaticObstacles(level));
        return new ClientBotNavPoints(level, DiscoverPoints(level, obstacles), obstacles);
    }

    public static ClientBotNavPoints Build(BotNavigationAsset asset)
    {
        return new ClientBotNavPoints(asset, obstacles: null);
    }

    public static ClientBotNavPoints Build(BotNavigationAsset asset, SimpleLevel level)
    {
        var obstacles = ObstacleGrid.Build(level.Bounds, ModernObstacleGeometry.BuildStaticObstacles(level));
        return new ClientBotNavPoints(asset, obstacles);
    }

    public bool TryGetPoint(int pointId, out BotNavigationNode point)
    {
        return _pointsById.TryGetValue(pointId, out point!);
    }

    public bool TryFindNearestPoint(float x, float y, out BotNavigationNode point)
    {
        point = default!;
        var bestDistanceSquared = float.PositiveInfinity;
        for (var index = 0; index < _points.Length; index += 1)
        {
            var candidate = _points[index];
            var distanceSquared = DistanceSquared(candidate.X, candidate.Y, x, y);
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            point = candidate;
        }

        return bestDistanceSquared < float.PositiveInfinity;
    }

    public bool TryGetOutgoingConnections(int pointId, out IReadOnlyList<BotNavigationEdge> outgoing)
    {
        if (_outgoingByPointId.TryGetValue(pointId, out var connections))
        {
            outgoing = connections;
            return true;
        }

        outgoing = Array.Empty<BotNavigationEdge>();
        return false;
    }

    public bool TryGetConnection(int fromPointId, int toPointId, out BotNavigationEdge edge)
    {
        return _edgeByPair.TryGetValue(GetPairKey(fromPointId, toPointId), out edge!);
    }

    public bool IsReverseBlocked(int fromPointId, int toPointId)
    {
        return _reverseBlockedPairs.Contains(GetPairKey(fromPointId, toPointId));
    }

    public int GetDropDirection(int fromPointId, int toPointId)
    {
        if (!TryGetPoint(fromPointId, out var fromPoint) || !TryGetPoint(toPointId, out var toPoint))
        {
            return 0;
        }

        return Math.Sign(toPoint.X - fromPoint.X);
    }

    public int[]? GetGoalWeights(int goalPointId, int maximumDepth = 130, PlayerClass? classId = null)
    {
        var cacheKey = $"{goalPointId}|{maximumDepth}|{(classId.HasValue ? (int)classId.Value : -1)}";
        if (_weightCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        if (!TryGetPoint(goalPointId, out _))
        {
            _weightCache[cacheKey] = null;
            return null;
        }

        var weights = new int[_points.Length];
        ProcessPoints([goalPointId], weights, branchDepth: 1, sourcePointId: -1, maximumDepth, classId);
        _weightCache[cacheKey] = weights;
        return weights;
    }

    private void ProcessPoints(IReadOnlyList<int> branches, int[] weights, int branchDepth, int sourcePointId, int maximumDepth, PlayerClass? classId)
    {
        for (var branchIndex = 0; branchIndex < branches.Count; branchIndex += 1)
        {
            var branch = branches[branchIndex];
            if (branch < 0 || branch >= weights.Length)
            {
                continue;
            }

            if (weights[branch] != 0 && weights[branch] <= branchDepth)
            {
                continue;
            }

            if (sourcePointId >= 0 && IsReverseBlocked(branch, sourcePointId))
            {
                continue;
            }

            if (sourcePointId >= 0
                && classId.HasValue
                && !IsClassTraversalUsable(sourcePointId, branch, classId.Value))
            {
                continue;
            }

            weights[branch] = branchDepth;
            if (branchDepth > maximumDepth || !TryGetOutgoingConnections(branch, out var outgoing))
            {
                continue;
            }

            var nextBranches = new int[outgoing.Count];
            for (var index = 0; index < outgoing.Count; index += 1)
            {
                nextBranches[index] = outgoing[index].ToNodeId;
            }

            ProcessPoints(nextBranches, weights, branchDepth + 1, branch, maximumDepth, classId);
        }
    }

    private bool IsClassTraversalUsable(int fromPointId, int toPointId, PlayerClass classId)
    {
        if (!TryGetConnection(fromPointId, toPointId, out _)
            || !TryGetPoint(fromPointId, out var fromPoint)
            || !TryGetPoint(toPointId, out var toPoint))
        {
            return false;
        }

        GetClassJumpProfile(classId, out var classJumpRange, out var classJumpHeightTotal);
        var jumpRiseNeeded = MathF.Max(0f, fromPoint.Y - toPoint.Y);
        if (jumpRiseNeeded > classJumpHeightTotal)
        {
            return false;
        }

        var jumpDistanceNeeded = MathF.Abs(toPoint.X - fromPoint.X);
        if (jumpDistanceNeeded <= classJumpRange)
        {
            return true;
        }

        return _obstacles is not null
            && _obstacles.LineHitsObstacle(fromPoint.X, fromPoint.Y + 2f, toPoint.X, toPoint.Y + 2f);
    }

    private static void GetClassJumpProfile(PlayerClass classId, out float jumpRange, out float jumpHeightTotal)
    {
        jumpRange = 120f;
        jumpHeightTotal = 60f;
        switch (classId)
        {
            case PlayerClass.Scout:
                jumpRange = 168f;
                jumpHeightTotal = 120f;
                break;

            case PlayerClass.Heavy:
                jumpRange = 96f;
                jumpHeightTotal = 60f;
                break;
        }
    }

    private static BotNavigationNode[] DiscoverPoints(SimpleLevel level, ObstacleGrid obstacles)
    {
        var points = new List<BotNavigationNode>();
        var nextPointId = 0;
        for (var y = GridStep; y < obstacles.Height; y += GridStep)
        {
            for (var x = 0; x < obstacles.Width; x += GridStep)
            {
                if (!IsWalkableTop(obstacles, x, y))
                {
                    continue;
                }

                if (!obstacles.IsBlocked(x - 1f, y) && !obstacles.LineHitsObstacle(x, y - 1f, x + FlatEdgeLookahead, y - 1f))
                {
                    AddPoint(points, ref nextPointId, x, y, level.Solids);
                }
                else if (!obstacles.IsBlocked(x - 1f, y) && !obstacles.LineHitsObstacle(x - 1f, y, x - 1f, y + DropProbeDepth))
                {
                    AddPoint(points, ref nextPointId, x, y, level.Solids);
                }
                else if (obstacles.IsBlocked(x + GridStep, y - 1f)
                         && obstacles.LineHitsObstacle(x - InteriorCornerProbeNear, y, x - InteriorCornerProbeFar, y)
                         && !HasNearbyVerticalPoint(points, x + 6f, y))
                {
                    AddPoint(points, ref nextPointId, x + 5f, y, level.Solids);
                }
                else if (!obstacles.IsBlocked(x + GridStep, y) && !obstacles.LineHitsObstacle(x, y - 1f, x - FlatEdgeLookback, y - 1f))
                {
                    AddPoint(points, ref nextPointId, x + 5f, y, level.Solids);
                }
                else if (!obstacles.IsBlocked(x + GridStep, y) && !obstacles.LineHitsObstacle(x + GridStep, y, x + GridStep, y + DropProbeDepth))
                {
                    AddPoint(points, ref nextPointId, x + 5f, y, level.Solids);
                }
                else if (obstacles.IsBlocked(x - 1f, y - 1f)
                         && obstacles.LineHitsObstacle(x + InteriorCornerRightNear, y, x + InteriorCornerRightFar, y)
                         && !HasNearbyVerticalPoint(points, x - 1f, y))
                {
                    AddPoint(points, ref nextPointId, x, y, level.Solids);
                }
            }
        }

        return points.ToArray();
    }

    private void BuildConnections()
    {
        var clearOffsetLineCache = new Dictionary<long, bool>();
        for (var fromIndex = 0; fromIndex < _points.Length; fromIndex += 1)
        {
            var fromPoint = _points[fromIndex];
            for (var toIndex = 0; toIndex < _points.Length; toIndex += 1)
            {
                if (fromIndex == toIndex)
                {
                    continue;
                }

                var toPoint = _points[toIndex];
                if (!TryEvaluateConnection(fromPoint, toPoint, out var reverseOnlyBlocked)
                    || IsConnectionRedundant(fromPoint, toPoint, clearOffsetLineCache))
                {
                    continue;
                }

                if (reverseOnlyBlocked)
                {
                    // Source stores this on navPoints[to][3] as "from is blocked when expanding from to".
                    _reverseBlockedPairs.Add(GetPairKey(toPoint.Id, fromPoint.Id));
                }

                var edge = new BotNavigationEdge
                {
                    FromNodeId = fromPoint.Id,
                    ToNodeId = toPoint.Id,
                    Kind = BotNavigationTraversalKind.Walk,
                    Cost = Math.Max(GridStep, DistanceBetween(fromPoint.X, fromPoint.Y, toPoint.X, toPoint.Y)),
                };

                AddConnection(edge);
            }
        }
    }

    private void AddConnection(BotNavigationEdge edge)
    {
        if (!_outgoingByPointId.TryGetValue(edge.FromNodeId, out var outgoing))
        {
            outgoing = new List<BotNavigationEdge>();
            _outgoingByPointId[edge.FromNodeId] = outgoing;
        }

        outgoing.Add(edge);
        _edgeByPair[GetPairKey(edge.FromNodeId, edge.ToNodeId)] = edge;
    }

    private void ApplyStockMapReverseBlockPatches(string levelName)
    {
        IReadOnlyDictionary<int, int[]>? reverseBlocks = null;
        if (string.Equals(levelName, "Corinth", StringComparison.OrdinalIgnoreCase))
        {
            reverseBlocks = CorinthStockReverseBlocks;
        }
        else if (string.Equals(levelName, "Waterway", StringComparison.OrdinalIgnoreCase))
        {
            reverseBlocks = WaterwayStockReverseBlocks;
        }

        if (reverseBlocks is null)
        {
            return;
        }

        foreach (var pair in reverseBlocks)
        {
            var toPointId = pair.Key;
            var fromPointIds = pair.Value;
            for (var index = 0; index < fromPointIds.Length; index += 1)
            {
                _reverseBlockedPairs.Add(GetPairKey(toPointId, fromPointIds[index]));
            }
        }
    }

    private bool TryEvaluateConnection(BotNavigationNode fromPoint, BotNavigationNode toPoint, out bool reverseOnlyBlocked)
    {
        var obstacles = _obstacles ?? throw new InvalidOperationException("Generated clientbot navpoints require obstacle geometry.");
        reverseOnlyBlocked = false;
        var dx = toPoint.X - fromPoint.X;
        if (dx == 0f)
        {
            return false;
        }

        if (obstacles.LineHitsObstacle(fromPoint.X, fromPoint.Y - 1f, fromPoint.X, fromPoint.Y - ConnectionClearanceHeight)
            || obstacles.LineHitsObstacle(toPoint.X, toPoint.Y - 1f, toPoint.X, toPoint.Y - ConnectionClearanceHeight)
            || obstacles.LineHitsObstacle(fromPoint.X, fromPoint.Y - ConnectionClearanceHeight, toPoint.X, toPoint.Y - ConnectionClearanceHeight))
        {
            return false;
        }

        var slope = (fromPoint.Y - toPoint.Y) / dx;
        if (slope > MaximumSlope || slope < -MaximumSlope)
        {
            return false;
        }

        if (ComputeDirectedTraversalCost(fromPoint.X, fromPoint.Y, toPoint.X, toPoint.Y) > MaximumDirectedTraversalCost)
        {
            var direction = MathF.Sign(dx);
            for (var offset = 0f; MathF.Abs(fromPoint.X + (offset * direction) - toPoint.X) >= GridStep; offset += GridStep)
            {
                var sampleX = fromPoint.X + (offset * direction);
                var sampleY = fromPoint.Y - (slope * (offset * direction));
                if (!obstacles.LineHitsObstacle(sampleX, sampleY, sampleX, sampleY + GapSupportProbeDepth))
                {
                    if (ComputeDirectedTraversalCost(toPoint.X, toPoint.Y, fromPoint.X, fromPoint.Y) > MaximumDirectedTraversalCost)
                    {
                        return false;
                    }

                    reverseOnlyBlocked = true;
                    break;
                }
            }
        }

        return MathF.Abs(dx) >= GridStep;
    }

    private bool IsConnectionRedundant(BotNavigationNode fromPoint, BotNavigationNode toPoint, Dictionary<long, bool> clearOffsetLineCache)
    {
        var minimumX = MathF.Min(fromPoint.X, toPoint.X);
        var maximumX = MathF.Max(fromPoint.X, toPoint.X);
        var minimumY = MathF.Min(fromPoint.Y, toPoint.Y);
        var maximumY = MathF.Max(fromPoint.Y, toPoint.Y);
        for (var index = 0; index < _pointsByX.Length; index += 1)
        {
            var intermediate = _pointsByX[index];
            if (intermediate.X <= minimumX)
            {
                continue;
            }

            if (intermediate.X >= maximumX)
            {
                break;
            }

            if (intermediate.Id == fromPoint.Id || intermediate.Id == toPoint.Id)
            {
                continue;
            }

            if (intermediate.Y < minimumY || intermediate.Y > maximumY)
            {
                continue;
            }

            if (HasClearOffsetLine(fromPoint, intermediate, clearOffsetLineCache)
                && HasClearOffsetLine(toPoint, intermediate, clearOffsetLineCache))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasClearOffsetLine(BotNavigationNode first, BotNavigationNode second, Dictionary<long, bool> cache)
    {
        var obstacles = _obstacles ?? throw new InvalidOperationException("Generated clientbot navpoints require obstacle geometry.");
        var key = GetPairKey(first.Id, second.Id);
        if (cache.TryGetValue(key, out var clear))
        {
            return clear;
        }

        clear = !obstacles.LineHitsObstacle(first.X, first.Y - ConnectionClearanceHeight, second.X, second.Y - ConnectionClearanceHeight);
        cache[key] = clear;
        cache[GetPairKey(second.Id, first.Id)] = clear;
        return clear;
    }

    private static void AddPoint(List<BotNavigationNode> points, ref int nextPointId, float x, float y, IReadOnlyList<LevelSolid> solids)
    {
        points.Add(new BotNavigationNode
        {
            Id = nextPointId,
            X = x,
            Y = y,
            SurfaceId = ResolveSurfaceId(x, y, solids),
            Kind = BotNavigationNodeKind.Surface,
        });
        nextPointId += 1;
    }

    private static bool HasNearbyVerticalPoint(IReadOnlyList<BotNavigationNode> points, float x, float y)
    {
        for (var index = 0; index < points.Count; index += 1)
        {
            var point = points[index];
            if (point.X == x && y - point.Y <= InteriorCornerVerticalTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static int ResolveSurfaceId(float x, float y, IReadOnlyList<LevelSolid> solids)
    {
        var bestSurfaceId = -1;
        var bestDistance = float.PositiveInfinity;
        for (var index = 0; index < solids.Count; index += 1)
        {
            var solid = solids[index];
            if (x >= solid.Left && x <= solid.Right && y >= solid.Top && y <= solid.Bottom)
            {
                var distance = MathF.Abs(y - solid.Top);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestSurfaceId = index;
            }
        }

        return bestSurfaceId;
    }

    private static float ComputeDirectedTraversalCost(float fromX, float fromY, float toX, float toY)
    {
        return MathF.Abs(fromX - toX) + (MathF.Max(0f, fromY - toY - 18f) * 1.2f);
    }

    private static readonly IReadOnlyDictionary<int, int[]> CorinthStockReverseBlocks = new Dictionary<int, int[]>
    {
        [5] = [18, 26],
        [6] = [17, 25],
        [8] = [18, 26],
        [9] = [17, 25],
        [10] = [21, 37],
        [13] = [30, 42],
        [14] = [20],
        [15] = [31],
        [24] = [55],
        [27] = [56],
        [39] = [88],
        [40] = [89],
        [54] = [70],
        [55] = [88],
        [56] = [89],
        [57] = [73],
        [58] = [76, 91, 98],
        [59] = [90],
        [60] = [97],
        [61] = [81, 96, 105],
        [68] = [92, 99],
        [75] = [95, 104],
        [76] = [90],
        [78] = [101],
        [79] = [102],
        [81] = [97],
    };

    private static readonly IReadOnlyDictionary<int, int[]> WaterwayStockReverseBlocks = new Dictionary<int, int[]>
    {
        [6] = [28],
        [9] = [31],
        [10] = [38, 63],
        [11] = [45, 66],
        [13] = [29],
        [16] = [30],
        [21] = [94],
        [22] = [95],
        [28] = [34],
        [31] = [37],
        [39] = [83],
        [41] = [86],
        [42] = [83],
        [44] = [86],
        [48] = [80],
        [50] = [94],
        [51] = [95],
        [53] = [89],
        [60] = [70, 76, 94],
        [61] = [73, 79, 95],
        [68] = [104],
        [69] = [116],
        [71] = [106],
        [72] = [109],
        [74] = [119],
        [75] = [111],
        [77] = [106],
        [78] = [109],
        [90] = [103],
        [99] = [112],
        [114] = [126],
        [115] = [125],
    };

    private static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return (dx * dx) + (dy * dy);
    }

    private static long GetPairKey(int fromPointId, int toPointId)
    {
        return ((long)fromPointId << 32) | (uint)toPointId;
    }

    private static bool IsWalkableTop(ObstacleGrid grid, float x, float y)
    {
        return grid.IsBlocked(x, y)
            && !grid.LineHitsObstacle(x, y - HeadroomStart, x, y - HeadroomEnd);
    }

    private sealed class ObstacleGrid
    {
        private readonly bool[] _blocked;

        private ObstacleGrid(int width, int height, bool[] blocked)
        {
            Width = width;
            Height = height;
            _blocked = blocked;
        }

        public int Width { get; }

        public int Height { get; }

        public static ObstacleGrid Build(WorldBounds bounds, IReadOnlyList<LevelSolid> solids)
        {
            var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
            var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
            var blocked = new bool[(width + 1) * (height + 1)];

            void FillRect(float left, float top, float right, float bottom)
            {
                var minX = Math.Max(0, (int)Math.Floor(left));
                var maxX = Math.Min(width - 1, (int)Math.Ceiling(right) - 1);
                var minY = Math.Max(0, (int)Math.Floor(top));
                var maxY = Math.Min(height - 1, (int)Math.Ceiling(bottom) - 1);
                if (maxX < minX || maxY < minY)
                {
                    return;
                }

                for (var y = minY; y <= maxY; y += 1)
                {
                    var rowStart = y * (width + 1);
                    for (var x = minX; x <= maxX; x += 1)
                    {
                        blocked[rowStart + x] = true;
                    }
                }
            }

            for (var index = 0; index < solids.Count; index += 1)
            {
                var solid = solids[index];
                FillRect(solid.Left, solid.Top, solid.Right, solid.Bottom);
            }

            return new ObstacleGrid(width, height, blocked);
        }

        public bool IsBlocked(float x, float y)
        {
            var ix = (int)MathF.Floor(x);
            var iy = (int)MathF.Floor(y);
            if (ix < 0 || iy < 0 || ix >= Width || iy >= Height)
            {
                return false;
            }

            return _blocked[(iy * (Width + 1)) + ix];
        }

        public bool LineHitsObstacle(float originX, float originY, float targetX, float targetY)
        {
            var deltaX = targetX - originX;
            var deltaY = targetY - originY;
            var steps = (int)Math.Ceiling(Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)));
            if (steps <= 0)
            {
                return IsBlocked(originX, originY);
            }

            for (var step = 0; step <= steps; step += 1)
            {
                var t = step / (float)steps;
                if (IsBlocked(originX + (deltaX * t), originY + (deltaY * t)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
