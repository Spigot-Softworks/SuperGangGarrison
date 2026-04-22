using OpenGarrison.Core;
using System.Diagnostics;

namespace OpenGarrison.BotAI;

public static class BotNavigationModernPointGraphBuilder
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
    private const int GapSupportProbeDepth = 54;
    private const float MaximumDirectedTraversalCost = 168f;
    private const float MaximumSlope = 4f;
    private const float DropTraversalHorizontalTolerance = 18f;
    private const float DropTraversalMinimumDistance = 18f;
    private const float MaximumUniversalJumpRise = 120f;
    private static readonly PlayerClass[] TraversalValidationClasses =
    [
        PlayerClass.Scout,
        PlayerClass.Pyro,
        PlayerClass.Heavy,
    ];

    public static BotNavigationAsset Build(
        SimpleLevel level,
        string? levelFingerprint = null,
        bool validateTraversals = false)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (validateTraversals)
        {
            return BuildValidatedFromRuntimeNavPoints(level, levelFingerprint);
        }

        var stopwatch = Stopwatch.StartNew();
        var phaseStopwatch = Stopwatch.StartNew();
        var obstacleSurfaces = level.Solids;
        var grid = ObstacleGrid.Build(level.Bounds, obstacleSurfaces);
        var rawPoints = BuildRawPoints(obstacleSurfaces, grid);
        var surfaceSamplingMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;
        phaseStopwatch.Restart();

        var mutableNodes = new Dictionary<string, MutableNode>(StringComparer.Ordinal);
        var candidateNodeCount = 0;

        for (var index = 0; index < rawPoints.Count; index += 1)
        {
            var rawPoint = rawPoints[index];
            candidateNodeCount += 1;
            _ = TryAddNode(
                mutableNodes,
                rawPoint.SurfaceId,
                rawPoint.X,
                rawPoint.Y,
                BotNavigationNodeKind.Surface,
                null,
                string.Empty,
                requiresGroundSupport: true,
                rawPoint.Y);
        }

        // Preserve the original scan-order point IDs from clientbot_2020_update.
        var orderedNodes = new List<MutableNode>(rawPoints.Count);
        for (var index = 0; index < rawPoints.Count; index += 1)
        {
            var rawPoint = rawPoints[index];
            var key = BuildNodeKey(rawPoint.SurfaceId, rawPoint.X, rawPoint.Y);
            if (mutableNodes.TryGetValue(key, out var node))
            {
                orderedNodes.Add(node);
            }
        }

        for (var index = 0; index < orderedNodes.Count; index += 1)
        {
            orderedNodes[index].Id = index;
        }
        var autoAnchorMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;
        phaseStopwatch.Restart();

        var edges = new List<BotNavigationEdge>();
        var edgeKeys = new HashSet<long>();
        var walkEdgeCount = 0;
        var jumpEdgeCount = 0;
        var dropEdgeCount = 0;

        for (var fromIndex = 0; fromIndex < orderedNodes.Count; fromIndex += 1)
        {
            var from = orderedNodes[fromIndex];
            for (var toIndex = 0; toIndex < orderedNodes.Count; toIndex += 1)
            {
                var to = orderedNodes[toIndex];
                if (from.Id == to.Id)
                {
                    continue;
                }

                if (!TryEvaluateDirectedConnection(grid, from, to, out var reverseOnlyBlocked)
                    || IsConnectionRedundant(grid, from, to, orderedNodes))
                {
                    continue;
                }

                if (reverseOnlyBlocked)
                {
                    to.AddReverseOnlyBlockedFromNodeId(from.Id);
                }

                var traversalKind = ResolveTraversalKind(from, to);
                if (!TryCreateTraversalEdge(
                        level,
                        from,
                        to,
                        traversalKind,
                        validateTraversals,
                        out var edge))
                {
                    continue;
                }

                if (!TryAddEdge(
                        edgeKeys,
                        edges,
                        edge))
                {
                    continue;
                }

                switch (traversalKind)
                {
                    case BotNavigationTraversalKind.Drop:
                        dropEdgeCount += 1;
                        break;
                    case BotNavigationTraversalKind.Jump:
                        jumpEdgeCount += 1;
                        break;
                    default:
                        walkEdgeCount += 1;
                        break;
                }
            }
        }
        var automaticEdgeMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;

        stopwatch.Stop();

        var builtNodes = orderedNodes
            .Select(static node => new BotNavigationNode
            {
                Id = node.Id,
                X = node.X,
                Y = node.Y,
                SurfaceId = node.SurfaceId,
                Kind = node.Kind,
                Team = node.Team,
                Label = node.Label,
                RequiresGroundSupport = node.RequiresGroundSupport,
                ReverseOnlyBlockedFromNodeIds = node.ReverseOnlyBlockedFromNodeIds.ToArray(),
            })
            .ToArray();

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
                SurfaceCount = obstacleSurfaces.Count,
                CandidateNodeCount = candidateNodeCount,
                SurfaceSampleNodeCount = rawPoints.Count,
                AutoAnchorNodeCount = builtNodes.Count(static node => node.Kind != BotNavigationNodeKind.Surface),
                NodeCount = builtNodes.Length,
                EdgeCount = edges.Count,
                WalkEdgeCount = walkEdgeCount,
                JumpEdgeCount = jumpEdgeCount,
                DropEdgeCount = dropEdgeCount,
                BuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                SurfaceSamplingMilliseconds = surfaceSamplingMilliseconds,
                AutoAnchorMilliseconds = autoAnchorMilliseconds,
                AutomaticEdgeMilliseconds = automaticEdgeMilliseconds,
            },
            Nodes = builtNodes,
            Edges = edges.ToArray(),
        };
    }

    private static BotNavigationAsset BuildValidatedFromRuntimeNavPoints(
        SimpleLevel level,
        string? levelFingerprint)
    {
        var stopwatch = Stopwatch.StartNew();
        var phaseStopwatch = Stopwatch.StartNew();
        var runtimeNavPoints = ClientBotNavPoints.Build(level);
        var obstacleSurfaces = ModernObstacleGeometry.BuildStaticObstacles(level).ToList();
        var grid = ObstacleGrid.Build(level.Bounds, obstacleSurfaces);
        var surfaceSamplingMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;
        phaseStopwatch.Restart();

        var runtimePoints = runtimeNavPoints.Points
            .OrderBy(static point => point.Id)
            .ToArray();
        var orderedNodes = new List<MutableNode>(runtimePoints.Length);
        for (var index = 0; index < runtimePoints.Length; index += 1)
        {
            var sourcePoint = runtimePoints[index];
            var mutableNode = new MutableNode(
                sourcePoint.SurfaceId,
                sourcePoint.X,
                sourcePoint.Y,
                sourcePoint.Y,
                sourcePoint.Kind,
                sourcePoint.Team,
                sourcePoint.Label,
                sourcePoint.RequiresGroundSupport);
            mutableNode.Id = sourcePoint.Id;
            for (var blockedIndex = 0; blockedIndex < sourcePoint.ReverseOnlyBlockedFromNodeIds.Count; blockedIndex += 1)
            {
                mutableNode.AddReverseOnlyBlockedFromNodeId(sourcePoint.ReverseOnlyBlockedFromNodeIds[blockedIndex]);
            }

            orderedNodes.Add(mutableNode);
        }
        var autoAnchorMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;
        phaseStopwatch.Restart();

        var edges = new List<BotNavigationEdge>();
        var edgeKeys = new HashSet<long>();
        var walkEdgeCount = 0;
        var jumpEdgeCount = 0;
        var dropEdgeCount = 0;
        for (var fromIndex = 0; fromIndex < orderedNodes.Count; fromIndex += 1)
        {
            var fromNode = orderedNodes[fromIndex];
            for (var toIndex = 0; toIndex < orderedNodes.Count; toIndex += 1)
            {
                var toNode = orderedNodes[toIndex];
                if (fromNode.Id == toNode.Id)
                {
                    continue;
                }

                if (!TryEvaluateDirectedConnection(grid, fromNode, toNode, out var reverseOnlyBlocked)
                    || IsConnectionRedundant(grid, fromNode, toNode, orderedNodes))
                {
                    continue;
                }

                if (reverseOnlyBlocked || runtimeNavPoints.IsReverseBlocked(toNode.Id, fromNode.Id))
                {
                    toNode.AddReverseOnlyBlockedFromNodeId(fromNode.Id);
                }

                var traversalKind = ResolveTraversalKind(fromNode, toNode);
                if (!TryCreateTraversalEdge(
                        level,
                        fromNode,
                        toNode,
                        traversalKind,
                        validateTraversal: true,
                        out var builtEdge)
                    || !TryAddEdge(edgeKeys, edges, builtEdge))
                {
                    continue;
                }

                switch (traversalKind)
                {
                    case BotNavigationTraversalKind.Drop:
                        dropEdgeCount += 1;
                        break;
                    case BotNavigationTraversalKind.Jump:
                        jumpEdgeCount += 1;
                        break;
                    default:
                        walkEdgeCount += 1;
                        break;
                }
            }
        }
        var automaticEdgeMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;

        stopwatch.Stop();

        var builtNodes = orderedNodes
            .Select(static node => new BotNavigationNode
            {
                Id = node.Id,
                X = node.X,
                Y = node.Y,
                SurfaceId = node.SurfaceId,
                Kind = node.Kind,
                Team = node.Team,
                Label = node.Label,
                RequiresGroundSupport = node.RequiresGroundSupport,
                ReverseOnlyBlockedFromNodeIds = node.ReverseOnlyBlockedFromNodeIds.ToArray(),
            })
            .ToArray();

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
                SurfaceCount = obstacleSurfaces.Count,
                CandidateNodeCount = runtimePoints.Length,
                SurfaceSampleNodeCount = runtimePoints.Length,
                AutoAnchorNodeCount = builtNodes.Count(static node => node.Kind != BotNavigationNodeKind.Surface),
                NodeCount = builtNodes.Length,
                EdgeCount = edges.Count,
                WalkEdgeCount = walkEdgeCount,
                JumpEdgeCount = jumpEdgeCount,
                DropEdgeCount = dropEdgeCount,
                BuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                SurfaceSamplingMilliseconds = surfaceSamplingMilliseconds,
                AutoAnchorMilliseconds = autoAnchorMilliseconds,
                AutomaticEdgeMilliseconds = automaticEdgeMilliseconds,
            },
            Nodes = builtNodes,
            Edges = edges.ToArray(),
        };
    }

    private static List<RawPoint> BuildRawPoints(IReadOnlyList<LevelSolid> obstacleSurfaces, ObstacleGrid grid)
    {
        var pointsByKey = new Dictionary<string, RawPoint>(StringComparer.Ordinal);
        var rawPoints = new List<RawPoint>();

        for (var y = GridStep; y < grid.Height; y += GridStep)
        {
            for (var x = 0; x < grid.Width; x += GridStep)
            {
                if (!IsWalkableTop(grid, x, y))
                {
                    continue;
                }

                if (!grid.IsBlocked(x - 1, y)
                    && !grid.LineHitsObstacle(x, y - 1, x + FlatEdgeLookahead, y - 1))
                {
                    TryAddRawPoint(obstacleSurfaces, pointsByKey, rawPoints, x, y);
                }
                else if (!grid.IsBlocked(x - 1, y)
                    && !grid.LineHitsObstacle(x - 1, y, x - 1, y + DropProbeDepth))
                {
                    TryAddRawPoint(obstacleSurfaces, pointsByKey, rawPoints, x, y);
                }
                else if (grid.IsBlocked(x + GridStep, y - 1)
                    && grid.LineHitsObstacle(x - InteriorCornerProbeNear, y, x - InteriorCornerProbeFar, y))
                {
                    var candidateX = x + 5;
                    if (!HasNearbyVerticalPoint(rawPoints, candidateX + 1, y))
                    {
                        TryAddRawPoint(obstacleSurfaces, pointsByKey, rawPoints, candidateX, y);
                    }
                }
                else if (!grid.IsBlocked(x + GridStep, y)
                    && !grid.LineHitsObstacle(x, y - 1, x - FlatEdgeLookback, y - 1))
                {
                    TryAddRawPoint(obstacleSurfaces, pointsByKey, rawPoints, x + 5, y);
                }
                else if (!grid.IsBlocked(x + GridStep, y)
                    && !grid.LineHitsObstacle(x + GridStep, y, x + GridStep, y + DropProbeDepth))
                {
                    TryAddRawPoint(obstacleSurfaces, pointsByKey, rawPoints, x + 5, y);
                }
                else if (grid.IsBlocked(x - 1, y - 1)
                    && grid.LineHitsObstacle(x + InteriorCornerRightNear, y, x + InteriorCornerRightFar, y))
                {
                    var candidateX = x;
                    if (!HasNearbyVerticalPoint(rawPoints, candidateX - 1, y))
                    {
                        TryAddRawPoint(obstacleSurfaces, pointsByKey, rawPoints, candidateX, y);
                    }
                }
            }
        }

        return rawPoints;
    }

    private static bool TryAddRawPoint(
        IReadOnlyList<LevelSolid> obstacleSurfaces,
        IDictionary<string, RawPoint> pointsByKey,
        ICollection<RawPoint> rawPoints,
        int x,
        int y)
    {
        var surfaceId = FindSupportingSurfaceId(obstacleSurfaces, x, y);
        if (surfaceId < 0)
        {
            return false;
        }

        var key = $"{surfaceId}:{x}:{y}";
        if (pointsByKey.ContainsKey(key))
        {
            return false;
        }

        var point = new RawPoint(x, y, surfaceId);
        pointsByKey[key] = point;
        rawPoints.Add(point);
        return true;
    }

    private static bool HasNearbyVerticalPoint(IReadOnlyList<RawPoint> rawPoints, int x, int y)
    {
        for (var index = 0; index < rawPoints.Count; index += 1)
        {
            var point = rawPoints[index];
            if (point.X == x && (y - point.Y) <= InteriorCornerVerticalTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static int FindSupportingSurfaceId(IReadOnlyList<LevelSolid> obstacleSurfaces, float x, float y)
    {
        var bestSurfaceId = -1;
        var bestDistance = float.PositiveInfinity;
        for (var surfaceIndex = 0; surfaceIndex < obstacleSurfaces.Count; surfaceIndex += 1)
        {
            var solid = obstacleSurfaces[surfaceIndex];
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
            bestSurfaceId = surfaceIndex;
        }

        return bestSurfaceId;
    }

    private static bool IsWalkableTop(ObstacleGrid grid, float x, float y)
    {
        return grid.IsBlocked(x, y)
            && !grid.LineHitsObstacle(x, y - HeadroomStart, x, y - HeadroomEnd);
    }

    private static bool TryEvaluateDirectedConnection(ObstacleGrid grid, MutableNode from, MutableNode to, out bool reverseOnlyBlocked)
    {
        reverseOnlyBlocked = false;
        if (MathF.Abs(to.RawX - from.RawX) < GridStep)
        {
            return false;
        }

        // clientBot_2020_update never accepts rises above scout's total jump height,
        // so emitting those edges guarantees unusable routes for every class.
        if ((from.RawY - to.RawY) > MaximumUniversalJumpRise)
        {
            return false;
        }

        if (grid.LineHitsObstacle(from.RawX, from.RawY - 1f, from.RawX, from.RawY - ConnectionClearanceHeight)
            || grid.LineHitsObstacle(to.RawX, to.RawY - 1f, to.RawX, to.RawY - ConnectionClearanceHeight)
            || grid.LineHitsObstacle(from.RawX, from.RawY - ConnectionClearanceHeight, to.RawX, to.RawY - ConnectionClearanceHeight))
        {
            return false;
        }

        var slope = (from.RawY - to.RawY) / (to.RawX - from.RawX);
        if (slope > MaximumSlope || slope < -MaximumSlope)
        {
            return false;
        }

        if (ComputeDirectedTraversalCost(from.RawX, from.RawY, to.RawX, to.RawY) > MaximumDirectedTraversalCost)
        {
            var direction = MathF.Sign(to.RawX - from.RawX);
            for (var offset = 0f; MathF.Abs((from.RawX + (offset * direction)) - to.RawX) >= GridStep; offset += GridStep)
            {
                var sampleX = from.RawX + (offset * direction);
                var sampleY = from.RawY - (slope * (offset * direction));
                if (!grid.LineHitsObstacle(sampleX, sampleY, sampleX, sampleY + GapSupportProbeDepth))
                {
                    if (ComputeDirectedTraversalCost(to.RawX, to.RawY, from.RawX, from.RawY) > MaximumDirectedTraversalCost)
                    {
                        return false;
                    }

                    reverseOnlyBlocked = true;
                    break;
                }
            }
        }

        return true;
    }

    private static float ComputeDirectedTraversalCost(float fromX, float fromY, float toX, float toY)
    {
        return MathF.Abs(fromX - toX) + (MathF.Max(0f, fromY - toY - 18f) * 1.2f);
    }

    private static BotNavigationTraversalKind ResolveTraversalKind(MutableNode from, MutableNode to)
    {
        var horizontalDistance = MathF.Abs(to.RawX - from.RawX);
        var verticalDistance = to.RawY - from.RawY;
        if (from.SurfaceId == to.SurfaceId || MathF.Abs(verticalDistance) <= GridStep)
        {
            return BotNavigationTraversalKind.Walk;
        }

        if (verticalDistance >= DropTraversalMinimumDistance && horizontalDistance <= DropTraversalHorizontalTolerance)
        {
            return BotNavigationTraversalKind.Drop;
        }

        return BotNavigationTraversalKind.Jump;
    }

    private static bool IsConnectionRedundant(
        ObstacleGrid grid,
        MutableNode from,
        MutableNode to,
        IReadOnlyList<MutableNode> nodes)
    {
        var minimumX = MathF.Min(from.RawX, to.RawX);
        var maximumX = MathF.Max(from.RawX, to.RawX);
        var minimumY = MathF.Min(from.RawY, to.RawY);
        var maximumY = MathF.Max(from.RawY, to.RawY);

        for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex += 1)
        {
            var candidate = nodes[nodeIndex];
            if (candidate.Id == from.Id || candidate.Id == to.Id)
            {
                continue;
            }

            if (candidate.RawX <= minimumX || candidate.RawX >= maximumX)
            {
                continue;
            }

            if (candidate.RawY < minimumY || candidate.RawY > maximumY)
            {
                continue;
            }

            if (!grid.LineHitsObstacle(
                    from.RawX,
                    from.RawY - ConnectionClearanceHeight,
                    candidate.RawX,
                    candidate.RawY - ConnectionClearanceHeight)
                && !grid.LineHitsObstacle(
                    to.RawX,
                    to.RawY - ConnectionClearanceHeight,
                    candidate.RawX,
                    candidate.RawY - ConnectionClearanceHeight))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCreateTraversalEdge(
        SimpleLevel level,
        MutableNode from,
        MutableNode to,
        BotNavigationTraversalKind traversalKind,
        bool validateTraversal,
        out BotNavigationEdge edge)
    {
        edge = default!;
        var cost = Math.Max(GridStep, DistanceBetween(from.RawX, from.RawY, to.RawX, to.RawY));
        IReadOnlyList<BotNavigationInputFrame> inputTape = Array.Empty<BotNavigationInputFrame>();
        if (validateTraversal)
        {
            cost = EstimateGeneratedTraversalCost(from, to, traversalKind);
            if (traversalKind == BotNavigationTraversalKind.Jump
                && TryBuildGeneratedJumpTape(level, from, to, out var generatedJumpTape, out var generatedJumpCost))
            {
                inputTape = generatedJumpTape;
                cost = generatedJumpCost;
            }
            else if (!TryValidateGeneratedTraversal(level, from, to, traversalKind, out cost))
            {
                return false;
            }

            cost = Math.Max(GridStep, cost);
        }

        switch (traversalKind)
        {
            case BotNavigationTraversalKind.Jump:
                edge = new BotNavigationEdge
                {
                    FromNodeId = from.Id,
                    ToNodeId = to.Id,
                    Kind = BotNavigationTraversalKind.Jump,
                    Cost = cost,
                    InputTape = inputTape,
                };
                return true;

            case BotNavigationTraversalKind.Drop:
                edge = new BotNavigationEdge
                {
                    FromNodeId = from.Id,
                    ToNodeId = to.Id,
                    Kind = BotNavigationTraversalKind.Drop,
                    Cost = cost,
                };
                return true;

            default:
                edge = new BotNavigationEdge
                {
                    FromNodeId = from.Id,
                    ToNodeId = to.Id,
                    Kind = BotNavigationTraversalKind.Walk,
                    Cost = cost,
                };
                return true;
        }
    }

    private static bool TryBuildGeneratedJumpTape(
        SimpleLevel level,
        MutableNode from,
        MutableNode to,
        out IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost)
    {
        return BotNavigationMovementValidator.TryBuildSharedJumpTape(
            level,
            TraversalValidationClasses,
            from.RawX,
            from.RawY,
            to.RawX,
            to.RawY,
            out tape,
            out cost);
    }

    private static float EstimateGeneratedTraversalCost(MutableNode from, MutableNode to, BotNavigationTraversalKind traversalKind)
    {
        var cost = ComputeDirectedTraversalCost(from.RawX, from.RawY, to.RawX, to.RawY);
        return traversalKind switch
        {
            BotNavigationTraversalKind.Jump => cost * 1.15f,
            BotNavigationTraversalKind.Drop => MathF.Max(GridStep, DistanceBetween(from.RawX, from.RawY, to.RawX, to.RawY) * 0.8f),
            _ => cost,
        };
    }

    private static bool TryValidateGeneratedTraversal(
        SimpleLevel level,
        MutableNode from,
        MutableNode to,
        BotNavigationTraversalKind traversalKind,
        out float cost)
    {
        cost = DistanceBetween(from.RawX, from.RawY, to.RawX, to.RawY);
        if (traversalKind == BotNavigationTraversalKind.Drop)
        {
            return true;
        }

        var bestCost = float.PositiveInfinity;
        for (var classIndex = 0; classIndex < TraversalValidationClasses.Length; classIndex += 1)
        {
            var classId = TraversalValidationClasses[classIndex];
            var classDefinition = BotNavigationClasses.GetDefinition(classId);
            var sourceY = from.RawY - classDefinition.CollisionBottom;
            var targetY = to.RawY - classDefinition.CollisionBottom;
            var profile = BotNavigationProfiles.GetProfileForClass(classId);
            if (traversalKind == BotNavigationTraversalKind.Walk)
            {
                if (TryValidateGeneratedGroundTraversal(level, classDefinition, from.RawX, sourceY, to.RawX, targetY, out var groundCost))
                {
                    bestCost = MathF.Min(bestCost, groundCost);
                }

                continue;
            }

            if (TryValidateGeneratedJumpTraversal(level, classDefinition, profile, from.RawX, sourceY, to.RawX, targetY, out var jumpCost))
            {
                bestCost = MathF.Min(bestCost, jumpCost);
            }
        }

        if (bestCost < float.PositiveInfinity)
        {
            cost = bestCost;
            return true;
        }

        return false;
    }

    private static bool TryValidateGeneratedGroundTraversal(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        out float cost)
    {
        if (BotNavigationMovementValidator.TryBuildGroundTape(
                level,
                classDefinition,
                sourceX,
                sourceY,
                targetX,
                targetY,
                PlayerTeam.Red,
                out _,
                out cost)
            || BotNavigationMovementValidator.TryBuildGroundTape(
                level,
                classDefinition,
                sourceX,
                sourceY,
                targetX,
                targetY,
                PlayerTeam.Blue,
                out _,
                out cost))
        {
            return true;
        }

        cost = 0f;
        return false;
    }

    private static bool TryValidateGeneratedJumpTraversal(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        out float cost)
    {
        if (BotNavigationMovementValidator.TryBuildJumpTape(
                level,
                classDefinition,
                profile,
                sourceX,
                sourceY,
                targetX,
                targetY,
                PlayerTeam.Red,
                out _,
                out cost)
            || BotNavigationMovementValidator.TryBuildJumpTape(
                level,
                classDefinition,
                profile,
                sourceX,
                sourceY,
                targetX,
                targetY,
                PlayerTeam.Blue,
                out _,
                out cost))
        {
            return true;
        }

        cost = 0f;
        return false;
    }

    private static bool TryAddNode(
        IDictionary<string, MutableNode> mutableNodes,
        int surfaceId,
        float x,
        float y,
        BotNavigationNodeKind kind,
        PlayerTeam? team,
        string label,
        bool requiresGroundSupport,
        float rawY,
        bool preferLabel = false)
    {
        _ = requiresGroundSupport;

        var key = BuildNodeKey(surfaceId, x, y);
        if (mutableNodes.TryGetValue(key, out var existing))
        {
            existing.TryPromote(kind, team, label, requiresGroundSupport, rawY, preferLabel);
            return false;
        }

        var node = new MutableNode(surfaceId, x, y, rawY, kind, team, label, requiresGroundSupport);
        mutableNodes[key] = node;
        return true;
    }

    private static string BuildNodeKey(int surfaceId, float x, float y)
    {
        return $"{surfaceId}:{MathF.Round(x, 2):F2}:{MathF.Round(y, 2):F2}";
    }

    private static bool TryAddEdge(HashSet<long> edgeKeys, List<BotNavigationEdge> edges, BotNavigationEdge edge)
    {
        var edgeKey = GetEdgeKey(edge.FromNodeId, edge.ToNodeId);
        if (!edgeKeys.Add(edgeKey))
        {
            return false;
        }

        edges.Add(edge);
        return true;
    }

    private static long GetEdgeKey(int fromNodeId, int toNodeId)
    {
        return ((long)fromNodeId << 32) | (uint)toNodeId;
    }

    private static float DistanceBetween(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private sealed class MutableNode
    {
        public MutableNode(int surfaceId, float x, float y, float rawY, BotNavigationNodeKind kind, PlayerTeam? team, string label, bool requiresGroundSupport)
        {
            SurfaceId = surfaceId;
            X = x;
            Y = y;
            RawX = x;
            RawY = rawY;
            Kind = kind;
            Team = team;
            Label = label;
            RequiresGroundSupport = requiresGroundSupport;
        }

        public int Id { get; set; }

        public int SurfaceId { get; }

        public float X { get; }

        public float Y { get; }

        public float RawX { get; }

        public float RawY { get; private set; }

        public BotNavigationNodeKind Kind { get; private set; }

        public PlayerTeam? Team { get; private set; }

        public string Label { get; private set; }

        public bool RequiresGroundSupport { get; private set; }

        public HashSet<int> ReverseOnlyBlockedFromNodeIds { get; } = new();

        public void TryPromote(BotNavigationNodeKind kind, PlayerTeam? team, string label, bool requiresGroundSupport, float rawY, bool preferLabel = false)
        {
            if (kind > Kind)
            {
                Kind = kind;
            }

            if (!Team.HasValue && team.HasValue)
            {
                Team = team;
            }

            if (requiresGroundSupport)
            {
                RequiresGroundSupport = true;
            }

            RawY = MathF.Min(RawY, rawY);

            if (preferLabel && !string.IsNullOrWhiteSpace(label))
            {
                Label = label;
            }
            else if (label.Length > Label.Length)
            {
                Label = label;
            }
        }

        public void AddReverseOnlyBlockedFromNodeId(int fromNodeId)
        {
            if (fromNodeId >= 0)
            {
                ReverseOnlyBlockedFromNodeIds.Add(fromNodeId);
            }
        }
    }

    private readonly record struct RawPoint(int X, int Y, int SurfaceId);

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

            foreach (var solid in obstacleSurfaces)
            {
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
    }
}
