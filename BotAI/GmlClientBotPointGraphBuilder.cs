using OpenGarrison.Core;
using System.Diagnostics;

namespace OpenGarrison.BotAI;

internal static class GmlClientBotPointGraphBuilder
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
    private const int ConnectionClearanceHeight = 12;
    private const int ConnectionClearanceRelaxEndpointMargin = GridStep * 4;
    private const int GapSupportProbeDepth = 54;
    private const float MaximumDirectedTraversalCost = 168f;
    private const float MaximumSlope = 4f;

    public static BotNavigationAsset Build(SimpleLevel level, string? levelFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(level);

        var stopwatch = Stopwatch.StartNew();
        var obstacleSurfaces = ModernObstacleGeometry.BuildStaticObstacles(level);
        var obstacles = ObstacleGrid.Build(level.Bounds, obstacleSurfaces);
        var points = DiscoverPoints(level, obstacles);
        var graph = ConnectPoints(points, obstacles, obstacleSurfaces);
        stopwatch.Stop();

        var nodes = new BotNavigationNode[points.Count];
        for (var index = 0; index < points.Count; index += 1)
        {
            var point = points[index];
            graph.ReverseBlockedByPointId.TryGetValue(point.Id, out var reverseBlocked);
            nodes[index] = new BotNavigationNode
            {
                Id = point.Id,
                X = point.X,
                Y = point.Y,
                SurfaceId = point.SurfaceId,
                Kind = BotNavigationNodeKind.Surface,
                RequiresGroundSupport = true,
                ReverseOnlyBlockedFromNodeIds = reverseBlocked is null ? Array.Empty<int>() : reverseBlocked.ToArray(),
            };
        }

        var edges = new List<BotNavigationEdge>();
        for (var pointIndex = 0; pointIndex < points.Count; pointIndex += 1)
        {
            var point = points[pointIndex];
            if (!graph.OutgoingByPointId.TryGetValue(point.Id, out var outgoing))
            {
                continue;
            }

            for (var outgoingIndex = 0; outgoingIndex < outgoing.Count; outgoingIndex += 1)
            {
                var toPointId = outgoing[outgoingIndex];
                var toPoint = points[toPointId];
                edges.Add(new BotNavigationEdge
                {
                    FromNodeId = point.Id,
                    ToNodeId = toPointId,
                    Kind = BotNavigationTraversalKind.Walk,
                    Cost = Math.Max(GridStep, DistanceBetween(point.X, point.Y, toPoint.X, toPoint.Y)),
                });
            }
        }

        return new BotNavigationAsset
        {
            FormatVersion = BotNavigationAssetStore.CurrentFormatVersion,
            LevelName = level.Name,
            MapAreaIndex = level.MapAreaIndex,
            ClassId = null,
            Profile = BotNavigationProfile.Standard,
            LevelFingerprint = levelFingerprint ?? BotNavigationLevelFingerprint.Compute(level),
            BuildStrategy = BotNavigationBuildStrategy.ModernClientBotPointGraph,
            BuiltUtc = DateTime.UtcNow,
            Stats = new BotNavigationBuildStats
            {
                SurfaceCount = level.Solids.Count,
                CandidateNodeCount = nodes.Length,
                SurfaceSampleNodeCount = nodes.Length,
                NodeCount = nodes.Length,
                EdgeCount = edges.Count,
                WalkEdgeCount = edges.Count,
                BuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                SurfaceSamplingMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            },
            Nodes = nodes,
            Edges = edges.ToArray(),
        };
    }

    public static string DescribeConnection(SimpleLevel level, BotNavigationAsset asset, int fromNodeId, int toNodeId)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(asset);

        var fromNode = asset.Nodes.FirstOrDefault(node => node.Id == fromNodeId);
        var toNode = asset.Nodes.FirstOrDefault(node => node.Id == toNodeId);
        if (fromNode is null || toNode is null)
        {
            return $"gml_link {fromNodeId}->{toNodeId} missing_node";
        }

        var obstacleSurfaces = ModernObstacleGeometry.BuildStaticObstacles(level);
        var obstacles = ObstacleGrid.Build(level.Bounds, obstacleSurfaces);
        var fromPoint = new GmlPoint(fromNode.Id, fromNode.X, fromNode.Y, fromNode.SurfaceId);
        var toPoint = new GmlPoint(toNode.Id, toNode.X, toNode.Y, toNode.SurfaceId);
        var points = asset.Nodes
            .Select(static node => new GmlPoint(node.Id, node.X, node.Y, node.SurfaceId))
            .ToArray();

        var existing = asset.Edges.Any(edge => edge.FromNodeId == fromNodeId && edge.ToNodeId == toNodeId);
        var reverseExisting = asset.Edges.Any(edge => edge.FromNodeId == toNodeId && edge.ToNodeId == fromNodeId);
        var clearanceFrom = obstacles.LineHitsObstacle(fromPoint.X, fromPoint.Y - 1f, fromPoint.X, fromPoint.Y - ConnectionClearanceHeight);
        var clearanceTo = obstacles.LineHitsObstacle(toPoint.X, toPoint.Y - 1f, toPoint.X, toPoint.Y - ConnectionClearanceHeight);
        var clearanceLine = obstacles.LineHitsObstacle(fromPoint.X, fromPoint.Y - ConnectionClearanceHeight, toPoint.X, toPoint.Y - ConnectionClearanceHeight);
        var clearanceLineHit = obstacles.FindFirstObstacleHit(fromPoint.X, fromPoint.Y - ConnectionClearanceHeight, toPoint.X, toPoint.Y - ConnectionClearanceHeight);
        var clearanceLineCanRelax = ClearanceLineHitCanBeRelaxed(obstacleSurfaces, obstacles, fromPoint, toPoint);
        var clearanceLineHitSurface = DescribeFirstSurfaceHit(
            obstacleSurfaces,
            fromPoint.X,
            fromPoint.Y - ConnectionClearanceHeight,
            toPoint.X,
            toPoint.Y - ConnectionClearanceHeight);
        var deltaX = toPoint.X - fromPoint.X;
        var slope = deltaX == 0f ? float.PositiveInfinity : (fromPoint.Y - toPoint.Y) / deltaX;
        var forwardCost = ComputeDirectedTraversalCost(fromPoint.X, fromPoint.Y, toPoint.X, toPoint.Y);
        var reverseCost = ComputeDirectedTraversalCost(toPoint.X, toPoint.Y, fromPoint.X, fromPoint.Y);
        var supportOk = true;
        var supportFailedAt = string.Empty;
        if (deltaX != 0f && (forwardCost > MaximumDirectedTraversalCost || clearanceLine))
        {
            var direction = MathF.Sign(deltaX);
            for (var testingXPoint = 0f; MathF.Abs(fromPoint.X + (testingXPoint * direction) - toPoint.X) >= GridStep; testingXPoint += GridStep)
            {
                var sampleX = fromPoint.X + (testingXPoint * direction);
                var sampleY = fromPoint.Y - (slope * (testingXPoint * direction));
                if (!obstacles.LineHitsObstacle(sampleX, sampleY, sampleX, sampleY + GapSupportProbeDepth))
                {
                    supportOk = false;
                    supportFailedAt = $"@({sampleX:0.0},{sampleY:0.0})";
                    break;
                }
            }
        }

        GmlPoint? redundantBy = null;
        for (var candidateIndex = 0; candidateIndex < points.Length; candidateIndex += 1)
        {
            var candidate = points[candidateIndex];
            if (!IsBetweenForGmlRedundancy(fromPoint, toPoint, candidate))
            {
                continue;
            }

            if (!obstacles.LineHitsObstacle(fromPoint.X, fromPoint.Y - ConnectionClearanceHeight, candidate.X, candidate.Y - ConnectionClearanceHeight)
                && !obstacles.LineHitsObstacle(toPoint.X, toPoint.Y - ConnectionClearanceHeight, candidate.X, candidate.Y - ConnectionClearanceHeight))
            {
                redundantBy = candidate;
                break;
            }
        }

        var reason = "ok";
        if (clearanceFrom || clearanceTo || (clearanceLine && (!supportOk || !clearanceLineCanRelax)))
        {
            reason = "clearance";
        }
        else if (deltaX == 0f)
        {
            reason = "vertical";
        }
        else if (MaximumSlope < slope || slope < -MaximumSlope)
        {
            reason = "slope";
        }
        else if (!supportOk && reverseCost > MaximumDirectedTraversalCost)
        {
            reason = "support";
        }
        else if (redundantBy.HasValue)
        {
            reason = $"redundant:{redundantBy.Value.Id}";
        }
        else if (MathF.Abs(deltaX) < GridStep)
        {
            reason = "short_dx";
        }

        return $"gml_link {fromNodeId}->{toNodeId} existing={existing} reverseExisting={reverseExisting} reason={reason} from=({fromPoint.X:0.0},{fromPoint.Y:0.0}) to=({toPoint.X:0.0},{toPoint.Y:0.0}) dx={deltaX:0.0} slope={slope:0.00} cost={forwardCost:0.0}/{reverseCost:0.0} clearance=from:{clearanceFrom}/to:{clearanceTo}/line:{clearanceLine}{clearanceLineHit}{clearanceLineHitSurface}/relax:{clearanceLineCanRelax} support={supportOk}{supportFailedAt}";
    }

    private static string DescribeFirstSurfaceHit(IReadOnlyList<LevelSolid> obstacleSurfaces, float startX, float startY, float endX, float endY)
    {
        var deltaX = endX - startX;
        var deltaY = endY - startY;
        var steps = (int)Math.Ceiling(Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)));
        if (steps <= 0)
        {
            return DescribeSurfaceAt(obstacleSurfaces, startX, startY);
        }

        for (var step = 0; step <= steps; step += 1)
        {
            var t = step / (float)steps;
            var x = startX + (deltaX * t);
            var y = startY + (deltaY * t);
            var surface = DescribeSurfaceAt(obstacleSurfaces, x, y);
            if (!string.IsNullOrEmpty(surface))
            {
                return surface;
            }
        }

        return string.Empty;
    }

    private static string DescribeSurfaceAt(IReadOnlyList<LevelSolid> obstacleSurfaces, float x, float y)
    {
        for (var index = 0; index < obstacleSurfaces.Count; index += 1)
        {
            var solid = obstacleSurfaces[index];
            if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
            {
                return $" surface#{index}[{solid.Left:0},{solid.Top:0},{solid.Right:0},{solid.Bottom:0}]";
            }
        }

        return string.Empty;
    }

    private static List<GmlPoint> DiscoverPoints(SimpleLevel level, ObstacleGrid obstacles)
    {
        var points = new List<GmlPoint>();
        for (var y = GridStep; y < obstacles.Height; y += GridStep)
        {
            for (var x = 0; x < obstacles.Width; x += GridStep)
            {
                if (!obstacles.IsBlocked(x, y)
                    || obstacles.LineHitsObstacle(x, y - HeadroomStart, x, y - HeadroomEnd))
                {
                    continue;
                }

                if (!obstacles.IsBlocked(x - 1f, y)
                    && !obstacles.LineHitsObstacle(x, y - 1f, x + FlatEdgeLookahead, y - 1f))
                {
                    AddPoint(points, level, x, y);
                }
                else if (!obstacles.IsBlocked(x - 1f, y)
                         && !obstacles.LineHitsObstacle(x - 1f, y, x - 1f, y + DropProbeDepth))
                {
                    AddPoint(points, level, x, y);
                }
                else if (obstacles.IsBlocked(x + GridStep, y - 1f)
                         && obstacles.LineHitsObstacle(x - InteriorCornerProbeNear, y, x - InteriorCornerProbeFar, y))
                {
                    if (!HasNearbyVerticalPoint(points, x + GridStep, y))
                    {
                        AddPoint(points, level, x + 5f, y);
                    }
                }
                else if (!obstacles.IsBlocked(x + GridStep, y)
                         && !obstacles.LineHitsObstacle(x, y - 1f, x - FlatEdgeLookback, y - 1f))
                {
                    AddPoint(points, level, x + 5f, y);
                }
                else if (!obstacles.IsBlocked(x + GridStep, y)
                         && !obstacles.LineHitsObstacle(x + GridStep, y, x + GridStep, y + DropProbeDepth))
                {
                    AddPoint(points, level, x + 5f, y);
                }
                else if (obstacles.IsBlocked(x - 1f, y - 1f)
                         && obstacles.LineHitsObstacle(x + InteriorCornerRightNear, y, x + InteriorCornerRightFar, y))
                {
                    if (!HasNearbyVerticalPoint(points, x - 1f, y))
                    {
                        AddPoint(points, level, x, y);
                    }
                }
            }
        }

        return points;
    }

    private static GmlGraph ConnectPoints(
        IReadOnlyList<GmlPoint> points,
        ObstacleGrid obstacles,
        IReadOnlyList<LevelSolid> obstacleSurfaces)
    {
        var graph = new GmlGraph();
        for (var fromIndex = 0; fromIndex < points.Count; fromIndex += 1)
        {
            var fromPoint = points[fromIndex];
            for (var toIndex = 0; toIndex < points.Count; toIndex += 1)
            {
                if (fromIndex == toIndex)
                {
                    continue;
                }

                var toPoint = points[toIndex];
                if (!ShouldConnect(points, obstacles, obstacleSurfaces, fromPoint, toPoint, out var reverseOnlyBlocked))
                {
                    continue;
                }

                if (reverseOnlyBlocked)
                {
                    AddReverseBlock(graph, toPoint.Id, fromPoint.Id);
                }

                AddOutgoing(graph, fromPoint.Id, toPoint.Id);
            }
        }

        return graph;
    }

    private static bool ShouldConnect(
        IReadOnlyList<GmlPoint> points,
        ObstacleGrid obstacles,
        IReadOnlyList<LevelSolid> obstacleSurfaces,
        GmlPoint fromPoint,
        GmlPoint toPoint,
        out bool reverseOnlyBlocked)
    {
        reverseOnlyBlocked = false;
        if (obstacles.LineHitsObstacle(fromPoint.X, fromPoint.Y - 1f, fromPoint.X, fromPoint.Y - ConnectionClearanceHeight)
            || obstacles.LineHitsObstacle(toPoint.X, toPoint.Y - 1f, toPoint.X, toPoint.Y - ConnectionClearanceHeight))
        {
            return false;
        }

        var deltaX = toPoint.X - fromPoint.X;
        if (deltaX == 0f)
        {
            return false;
        }

        var slope = (fromPoint.Y - toPoint.Y) / deltaX;
        if (MaximumSlope < slope || slope < -MaximumSlope)
        {
            return false;
        }

        if (obstacles.LineHitsObstacle(fromPoint.X, fromPoint.Y - ConnectionClearanceHeight, toPoint.X, toPoint.Y - ConnectionClearanceHeight)
            && (!HasContinuousSupportUnderConnection(obstacles, fromPoint, toPoint, slope)
                || !ClearanceLineHitCanBeRelaxed(obstacleSurfaces, obstacles, fromPoint, toPoint)))
        {
            return false;
        }

        var connectPoints = true;
        if (ComputeDirectedTraversalCost(fromPoint.X, fromPoint.Y, toPoint.X, toPoint.Y) > MaximumDirectedTraversalCost)
        {
            connectPoints = HasContinuousSupportUnderConnection(obstacles, fromPoint, toPoint, slope);

            if (!connectPoints)
            {
                if (ComputeDirectedTraversalCost(toPoint.X, toPoint.Y, fromPoint.X, fromPoint.Y) <= MaximumDirectedTraversalCost)
                {
                    reverseOnlyBlocked = true;
                    connectPoints = true;
                }
            }
        }

        if (!connectPoints)
        {
            return false;
        }

        for (var candidateIndex = 0; candidateIndex < points.Count; candidateIndex += 1)
        {
            var candidate = points[candidateIndex];
            if (!IsBetweenForGmlRedundancy(fromPoint, toPoint, candidate))
            {
                continue;
            }

            if (!obstacles.LineHitsObstacle(fromPoint.X, fromPoint.Y - ConnectionClearanceHeight, candidate.X, candidate.Y - ConnectionClearanceHeight)
                && !obstacles.LineHitsObstacle(toPoint.X, toPoint.Y - ConnectionClearanceHeight, candidate.X, candidate.Y - ConnectionClearanceHeight))
            {
                return false;
            }
        }

        return MathF.Abs(deltaX) >= GridStep;
    }

    private static bool HasContinuousSupportUnderConnection(ObstacleGrid obstacles, GmlPoint fromPoint, GmlPoint toPoint, float slope)
    {
        var direction = MathF.Sign(toPoint.X - fromPoint.X);
        for (var testingXPoint = 0f; MathF.Abs(fromPoint.X + (testingXPoint * direction) - toPoint.X) >= GridStep; testingXPoint += GridStep)
        {
            var sampleX = fromPoint.X + (testingXPoint * direction);
            var sampleY = fromPoint.Y - (slope * (testingXPoint * direction));
            if (!obstacles.LineHitsObstacle(sampleX, sampleY, sampleX, sampleY + GapSupportProbeDepth))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ClearanceLineHitIsNearEndpoint(ObstacleGrid obstacles, GmlPoint fromPoint, GmlPoint toPoint)
    {
        var fromClearanceY = fromPoint.Y - ConnectionClearanceHeight;
        var toClearanceY = toPoint.Y - ConnectionClearanceHeight;
        return obstacles.TryFindFirstObstacleHit(fromPoint.X, fromClearanceY, toPoint.X, toClearanceY, out var hitX, out var hitY)
            && DistanceBetween(fromPoint.X, fromClearanceY, hitX, hitY) <= ConnectionClearanceRelaxEndpointMargin
            && hitY < fromPoint.Y - GridStep;
    }

    private static bool ClearanceLineHitCanBeRelaxed(
        IReadOnlyList<LevelSolid> obstacleSurfaces,
        ObstacleGrid obstacles,
        GmlPoint fromPoint,
        GmlPoint toPoint)
    {
        if (ClearanceLineHitIsNearEndpoint(obstacles, fromPoint, toPoint))
        {
            return false;
        }

        var fromClearanceY = fromPoint.Y - ConnectionClearanceHeight;
        var toClearanceY = toPoint.Y - ConnectionClearanceHeight;
        if (!TryFindFirstSurfaceHit(obstacleSurfaces, fromPoint.X, fromClearanceY, toPoint.X, toClearanceY, out var surface))
        {
            return false;
        }

        return surface.Height <= GridStep
            && surface.Width <= MaximumDirectedTraversalCost;
    }

    private static bool TryFindFirstSurfaceHit(
        IReadOnlyList<LevelSolid> obstacleSurfaces,
        float startX,
        float startY,
        float endX,
        float endY,
        out LevelSolid surface)
    {
        var deltaX = endX - startX;
        var deltaY = endY - startY;
        var steps = (int)Math.Ceiling(Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)));
        if (steps <= 0)
        {
            return TryFindSurfaceAt(obstacleSurfaces, startX, startY, out surface);
        }

        for (var step = 0; step <= steps; step += 1)
        {
            var t = step / (float)steps;
            if (TryFindSurfaceAt(obstacleSurfaces, startX + (deltaX * t), startY + (deltaY * t), out surface))
            {
                return true;
            }
        }

        surface = default;
        return false;
    }

    private static bool TryFindSurfaceAt(
        IReadOnlyList<LevelSolid> obstacleSurfaces,
        float x,
        float y,
        out LevelSolid surface)
    {
        for (var index = 0; index < obstacleSurfaces.Count; index += 1)
        {
            var candidate = obstacleSurfaces[index];
            if (x >= candidate.Left && x < candidate.Right && y >= candidate.Top && y < candidate.Bottom)
            {
                surface = candidate;
                return true;
            }
        }

        surface = default;
        return false;
    }

    private static bool IsBetweenForGmlRedundancy(GmlPoint fromPoint, GmlPoint toPoint, GmlPoint candidate)
    {
        if (candidate.Id == fromPoint.Id || candidate.Id == toPoint.Id)
        {
            return false;
        }

        var betweenX = (fromPoint.X < candidate.X && candidate.X < toPoint.X)
            || (toPoint.X < candidate.X && candidate.X < fromPoint.X);
        var betweenY = (fromPoint.Y >= candidate.Y && candidate.Y >= toPoint.Y)
            || (toPoint.Y >= candidate.Y && candidate.Y >= fromPoint.Y);
        return betweenX && betweenY;
    }

    private static void AddOutgoing(GmlGraph graph, int fromPointId, int toPointId)
    {
        if (!graph.OutgoingByPointId.TryGetValue(fromPointId, out var outgoing))
        {
            outgoing = new List<int>();
            graph.OutgoingByPointId[fromPointId] = outgoing;
        }

        outgoing.Add(toPointId);
    }

    private static void AddReverseBlock(GmlGraph graph, int toPointId, int fromPointId)
    {
        if (!graph.ReverseBlockedByPointId.TryGetValue(toPointId, out var blocked))
        {
            blocked = new List<int>();
            graph.ReverseBlockedByPointId[toPointId] = blocked;
        }

        blocked.Add(fromPointId);
    }

    private static void AddPoint(List<GmlPoint> points, SimpleLevel level, float x, float y)
    {
        points.Add(new GmlPoint(points.Count, x, y, ResolveSurfaceId(level.Solids, x, y)));
    }

    private static bool HasNearbyVerticalPoint(IReadOnlyList<GmlPoint> points, float x, float y)
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

    private static int ResolveSurfaceId(IReadOnlyList<LevelSolid> solids, float x, float y)
    {
        var bestSurfaceId = -1;
        var bestDistance = float.PositiveInfinity;
        for (var index = 0; index < solids.Count; index += 1)
        {
            var solid = solids[index];
            if (x < solid.Left || x > solid.Right || y < solid.Top || y > solid.Bottom)
            {
                continue;
            }

            var distance = MathF.Abs(y - solid.Top);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestSurfaceId = index;
        }

        return bestSurfaceId;
    }

    private static float ComputeDirectedTraversalCost(float fromX, float fromY, float toX, float toY)
    {
        return MathF.Abs(fromX - toX) + (MathF.Max(0f, fromY - toY - 18f) * 1.2f);
    }

    private static float DistanceBetween(float fromX, float fromY, float toX, float toY)
    {
        var deltaX = toX - fromX;
        var deltaY = toY - fromY;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private readonly record struct GmlPoint(int Id, float X, float Y, int SurfaceId);

    private sealed class GmlGraph
    {
        public Dictionary<int, List<int>> OutgoingByPointId { get; } = new();

        public Dictionary<int, List<int>> ReverseBlockedByPointId { get; } = new();
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

        public static ObstacleGrid Build(WorldBounds bounds, IReadOnlyList<LevelSolid> obstacleSurfaces)
        {
            var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
            var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
            var blocked = new bool[(width + 1) * (height + 1)];

            for (var index = 0; index < obstacleSurfaces.Count; index += 1)
            {
                var solid = obstacleSurfaces[index];
                var minX = Math.Max(0, (int)Math.Floor(solid.Left));
                var maxX = Math.Min(width - 1, (int)Math.Ceiling(solid.Right) - 1);
                var minY = Math.Max(0, (int)Math.Floor(solid.Top));
                var maxY = Math.Min(height - 1, (int)Math.Ceiling(solid.Bottom) - 1);
                if (maxX < minX || maxY < minY)
                {
                    continue;
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

        public bool LineHitsObstacle(float startX, float startY, float endX, float endY)
        {
            var deltaX = endX - startX;
            var deltaY = endY - startY;
            var steps = (int)Math.Ceiling(Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)));
            if (steps <= 0)
            {
                return IsBlocked(startX, startY);
            }

            for (var step = 0; step <= steps; step += 1)
            {
                var t = step / (float)steps;
                if (IsBlocked(startX + (deltaX * t), startY + (deltaY * t)))
                {
                    return true;
                }
            }

            return false;
        }

        public string FindFirstObstacleHit(float startX, float startY, float endX, float endY)
        {
            return TryFindFirstObstacleHit(startX, startY, endX, endY, out var x, out var y)
                ? $"@({x:0.0},{y:0.0})"
                : string.Empty;
        }

        public bool TryFindFirstObstacleHit(float startX, float startY, float endX, float endY, out float hitX, out float hitY)
        {
            var deltaX = endX - startX;
            var deltaY = endY - startY;
            var steps = (int)Math.Ceiling(Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)));
            if (steps <= 0)
            {
                hitX = startX;
                hitY = startY;
                return IsBlocked(startX, startY);
            }

            for (var step = 0; step <= steps; step += 1)
            {
                var t = step / (float)steps;
                var x = startX + (deltaX * t);
                var y = startY + (deltaY * t);
                if (IsBlocked(x, y))
                {
                    hitX = x;
                    hitY = y;
                    return true;
                }
            }

            hitX = 0f;
            hitY = 0f;
            return false;
        }
    }
}
