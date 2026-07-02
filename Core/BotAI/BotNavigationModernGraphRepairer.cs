using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public sealed record BotNavigationModernGraphRepairResult(
    BotNavigationAsset Asset,
    int AddedEdges);

public static class BotNavigationModernGraphRepairer
{
    private const int GridStep = 6;
    private const int ConnectionClearanceHeight = 12;
    private const float MaximumDirectedTraversalCost = 168f;
    private const float MaximumBridgeTraversalCost = 720f;
    private const float MaximumSlope = 4f;
    private const float DropTraversalHorizontalTolerance = 18f;
    private const float DropTraversalMinimumDistance = 18f;
    private const float StartNodeSearchDistance = 220f;
    private const float GoalNodeSearchDistance = 220f;
    private const float GapSupportProbeDepth = 54f;

    public static BotNavigationAsset AddReverseOnlyTraversalBlocks(SimpleLevel level, BotNavigationAsset asset)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(asset);

        if (asset.BuildStrategy != BotNavigationBuildStrategy.ModernClientBotPointGraph
            || asset.Nodes.Count == 0
            || asset.Edges.Count == 0)
        {
            return asset;
        }

        var blockedFromNodeIdsByNodeId = new Dictionary<int, HashSet<int>>();
        for (var nodeIndex = 0; nodeIndex < asset.Nodes.Count; nodeIndex += 1)
        {
            var node = asset.Nodes[nodeIndex];
            if (node.ReverseOnlyBlockedFromNodeIds.Count == 0)
            {
                continue;
            }

            blockedFromNodeIdsByNodeId[node.Id] = new HashSet<int>(node.ReverseOnlyBlockedFromNodeIds);
        }

        var addedBlock = false;
        for (var edgeIndex = 0; edgeIndex < asset.Edges.Count; edgeIndex += 1)
        {
            var edge = asset.Edges[edgeIndex];
            if (!TryGetNode(asset.Nodes, edge.FromNodeId, out var from)
                || !TryGetNode(asset.Nodes, edge.ToNodeId, out var to)
                || !IsReverseOnlyDirectedConnection(level, from, to))
            {
                continue;
            }

            if (!blockedFromNodeIdsByNodeId.TryGetValue(to.Id, out var blockedFromNodeIds))
            {
                blockedFromNodeIds = new HashSet<int>();
                blockedFromNodeIdsByNodeId[to.Id] = blockedFromNodeIds;
            }

            addedBlock |= blockedFromNodeIds.Add(from.Id);
        }

        if (!addedBlock)
        {
            return asset;
        }

        var nodes = asset.Nodes
            .Select(node => new BotNavigationNode
            {
                Id = node.Id,
                X = node.X,
                Y = node.Y,
                SurfaceId = node.SurfaceId,
                Kind = node.Kind,
                Team = node.Team,
                Label = node.Label,
                RequiresGroundSupport = node.RequiresGroundSupport,
                ReverseOnlyBlockedFromNodeIds = blockedFromNodeIdsByNodeId.TryGetValue(node.Id, out var blockedFromNodeIds)
                    ? blockedFromNodeIds.Order().ToArray()
                    : Array.Empty<int>(),
            })
            .ToArray();

        return new BotNavigationAsset
        {
            FormatVersion = asset.FormatVersion,
            LevelName = asset.LevelName,
            MapAreaIndex = asset.MapAreaIndex,
            ClassId = asset.ClassId,
            Profile = asset.Profile,
            LevelFingerprint = asset.LevelFingerprint,
            BuildStrategy = asset.BuildStrategy,
            BuiltUtc = asset.BuiltUtc,
            Stats = asset.Stats,
            Nodes = nodes,
            Edges = asset.Edges,
        };
    }

    public static BotNavigationAsset RemoveRedundantClientBotEdges(SimpleLevel level, BotNavigationAsset asset)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(asset);

        if (asset.BuildStrategy != BotNavigationBuildStrategy.ModernClientBotPointGraph
            || asset.Nodes.Count == 0
            || asset.Edges.Count == 0)
        {
            return asset;
        }

        var nodesById = asset.Nodes.ToDictionary(static node => node.Id);
        var nodesByX = asset.Nodes.OrderBy(static node => node.X).ToArray();
        var prunedEdges = new List<BotNavigationEdge>(asset.Edges.Count);
        var removedEdges = 0;
        for (var edgeIndex = 0; edgeIndex < asset.Edges.Count; edgeIndex += 1)
        {
            var edge = asset.Edges[edgeIndex];
            if (!nodesById.TryGetValue(edge.FromNodeId, out var from)
                || !nodesById.TryGetValue(edge.ToNodeId, out var to)
                || IsRedundantClientBotConnection(level, nodesByX, from, to))
            {
                removedEdges += 1;
                continue;
            }

            prunedEdges.Add(edge);
        }

        if (removedEdges == 0)
        {
            return asset;
        }

        var stats = asset.Stats;
        return new BotNavigationAsset
        {
            FormatVersion = asset.FormatVersion,
            LevelName = asset.LevelName,
            MapAreaIndex = asset.MapAreaIndex,
            ClassId = asset.ClassId,
            Profile = asset.Profile,
            LevelFingerprint = asset.LevelFingerprint,
            BuildStrategy = asset.BuildStrategy,
            BuiltUtc = asset.BuiltUtc,
            Stats = new BotNavigationBuildStats
            {
                SurfaceCount = stats.SurfaceCount,
                CandidateNodeCount = stats.CandidateNodeCount,
                SurfaceSampleNodeCount = stats.SurfaceSampleNodeCount,
                AutoAnchorNodeCount = stats.AutoAnchorNodeCount,
                HintNodeCount = stats.HintNodeCount,
                NodeCount = asset.Nodes.Count,
                EdgeCount = prunedEdges.Count,
                WalkEdgeCount = prunedEdges.Count(static edge => edge.Kind == BotNavigationTraversalKind.Walk),
                JumpEdgeCount = prunedEdges.Count(static edge => edge.Kind == BotNavigationTraversalKind.Jump),
                DropEdgeCount = prunedEdges.Count(static edge => edge.Kind == BotNavigationTraversalKind.Drop),
                HintEdgeCount = stats.HintEdgeCount,
                BuildMilliseconds = stats.BuildMilliseconds,
                SurfaceSamplingMilliseconds = stats.SurfaceSamplingMilliseconds,
                AutoAnchorMilliseconds = stats.AutoAnchorMilliseconds,
                HintNodeMilliseconds = stats.HintNodeMilliseconds,
                AutomaticEdgeMilliseconds = stats.AutomaticEdgeMilliseconds,
                HintEdgeMilliseconds = stats.HintEdgeMilliseconds,
                DropEdgeMilliseconds = stats.DropEdgeMilliseconds,
            },
            Nodes = asset.Nodes,
            Edges = prunedEdges,
        };
    }

    public static BotNavigationModernGraphRepairResult AddMissingClientBotEdges(SimpleLevel level, BotNavigationAsset asset)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(asset);

        if (asset.BuildStrategy != BotNavigationBuildStrategy.ModernClientBotPointGraph || asset.Nodes.Count == 0)
        {
            return new BotNavigationModernGraphRepairResult(asset, AddedEdges: 0);
        }

        var edges = asset.Edges.ToList();
        var edgeKeys = new HashSet<long>(edges.Select(static edge => GetEdgeKey(edge.FromNodeId, edge.ToNodeId)));
        var nodesByX = asset.Nodes
            .OrderBy(static node => node.X)
            .ToArray();
        var addedEdges = 0;

        for (var fromIndex = 0; fromIndex < asset.Nodes.Count; fromIndex += 1)
        {
            var from = asset.Nodes[fromIndex];
            var firstCandidateIndex = FindFirstNodeAtOrAfterX(nodesByX, from.X - MaximumDirectedTraversalCost);
            for (var toIndex = firstCandidateIndex; toIndex < nodesByX.Length; toIndex += 1)
            {
                var to = nodesByX[toIndex];
                if (to.X > from.X + MaximumDirectedTraversalCost)
                {
                    break;
                }

                if (from.Id == to.Id)
                {
                    continue;
                }

                var edgeKey = GetEdgeKey(from.Id, to.Id);
                if (edgeKeys.Contains(edgeKey) || !CanConnectDirected(level, from, to))
                {
                    continue;
                }

                var traversalKind = ResolveTraversalKind(from, to);
                edges.Add(new BotNavigationEdge
                {
                    FromNodeId = from.Id,
                    ToNodeId = to.Id,
                    Kind = traversalKind,
                    Cost = Math.Max(GridStep, DistanceBetween(from.X, from.Y, to.X, to.Y)),
                });
                edgeKeys.Add(edgeKey);
                addedEdges += 1;
            }
        }

        if (addedEdges == 0)
        {
            return new BotNavigationModernGraphRepairResult(asset, AddedEdges: 0);
        }

        var stats = asset.Stats;
        var repairedAsset = new BotNavigationAsset
        {
            FormatVersion = asset.FormatVersion,
            LevelName = asset.LevelName,
            MapAreaIndex = asset.MapAreaIndex,
            ClassId = asset.ClassId,
            Profile = asset.Profile,
            LevelFingerprint = asset.LevelFingerprint,
            BuildStrategy = asset.BuildStrategy,
            BuiltUtc = DateTime.UtcNow,
            Stats = new BotNavigationBuildStats
            {
                SurfaceCount = stats.SurfaceCount,
                CandidateNodeCount = stats.CandidateNodeCount,
                SurfaceSampleNodeCount = stats.SurfaceSampleNodeCount,
                AutoAnchorNodeCount = stats.AutoAnchorNodeCount,
                HintNodeCount = stats.HintNodeCount,
                NodeCount = asset.Nodes.Count,
                EdgeCount = edges.Count,
                WalkEdgeCount = edges.Count(static edge => edge.Kind == BotNavigationTraversalKind.Walk),
                JumpEdgeCount = edges.Count(static edge => edge.Kind == BotNavigationTraversalKind.Jump),
                DropEdgeCount = edges.Count(static edge => edge.Kind == BotNavigationTraversalKind.Drop),
                HintEdgeCount = stats.HintEdgeCount,
                BuildMilliseconds = stats.BuildMilliseconds,
                SurfaceSamplingMilliseconds = stats.SurfaceSamplingMilliseconds,
                AutoAnchorMilliseconds = stats.AutoAnchorMilliseconds,
                HintNodeMilliseconds = stats.HintNodeMilliseconds,
                AutomaticEdgeMilliseconds = stats.AutomaticEdgeMilliseconds,
                HintEdgeMilliseconds = stats.HintEdgeMilliseconds,
                DropEdgeMilliseconds = stats.DropEdgeMilliseconds,
            },
            Nodes = asset.Nodes,
            Edges = edges,
        };

        return new BotNavigationModernGraphRepairResult(repairedAsset, addedEdges);
    }

    private static int AddReachabilityBridgeEdges(
        SimpleLevel level,
        IReadOnlyList<BotNavigationNode> nodes,
        List<BotNavigationEdge> edges,
        HashSet<long> edgeKeys)
    {
        if (!SupportsAttackReachabilityAudit(level.Mode))
        {
            return 0;
        }

        var addedEdges = 0;
        for (var teamIndex = 0; teamIndex < 2; teamIndex += 1)
        {
            var team = teamIndex == 0 ? PlayerTeam.Red : PlayerTeam.Blue;
            var spawns = GetTeamSpawns(level, nodes, team);
            var objectives = GetAttackObjectives(level, nodes, team);
            for (var spawnIndex = 0; spawnIndex < spawns.Count; spawnIndex += 1)
            {
                var spawn = spawns[spawnIndex];
                var nearestStartCandidates = FindNearestNodes(nodes, spawn.X, spawn.Y, StartNodeSearchDistance);
                if (nearestStartCandidates.Count == 0)
                {
                    continue;
                }

                var startCandidates = nearestStartCandidates.Take(1).ToArray();
                for (var bridgeAttempt = 0; bridgeAttempt < 8; bridgeAttempt += 1)
                {
                    var adjacency = BuildAdjacency(edges);
                    if (CanReachAnyObjective(nodes, adjacency, startCandidates, objectives))
                    {
                        break;
                    }

                    if (!TryAddShortestBridgeToAnyObjective(
                            level,
                            nodes,
                            edges,
                            edgeKeys,
                            adjacency,
                            startCandidates,
                            objectives))
                    {
                        break;
                    }

                    addedEdges += 1;
                }
            }
        }

        return addedEdges;
    }

    private static bool CanReachAnyObjective(
        IReadOnlyList<BotNavigationNode> nodes,
        Dictionary<int, List<int>> adjacency,
        IReadOnlyList<BotNavigationNode> startCandidates,
        IReadOnlyList<NavMarkerPoint> objectives)
    {
        for (var objectiveIndex = 0; objectiveIndex < objectives.Count; objectiveIndex += 1)
        {
            var objective = objectives[objectiveIndex];
            var goalCandidate = FindNearestNode(nodes, objective.X, objective.Y);
            if (goalCandidate is null)
            {
                continue;
            }

            if (HasRoute(adjacency, startCandidates.Select(static node => node.Id), [goalCandidate.Id]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryAddShortestBridgeToAnyObjective(
        SimpleLevel level,
        IReadOnlyList<BotNavigationNode> nodes,
        List<BotNavigationEdge> edges,
        HashSet<long> edgeKeys,
        Dictionary<int, List<int>> adjacency,
        IReadOnlyList<BotNavigationNode> startCandidates,
        IReadOnlyList<NavMarkerPoint> objectives)
    {
        var reachable = FindReachable(adjacency, startCandidates.Select(static node => node.Id));
        var nodesByX = nodes
            .OrderBy(static node => node.X)
            .ToArray();
        BotNavigationNode? bestFrom = null;
        BotNavigationNode? bestTo = null;
        var bestScore = float.PositiveInfinity;
        var found = false;

        for (var objectiveIndex = 0; objectiveIndex < objectives.Count; objectiveIndex += 1)
        {
            var objective = objectives[objectiveIndex];
            var goalCandidate = FindNearestNode(nodes, objective.X, objective.Y);
            if (goalCandidate is null)
            {
                continue;
            }

            var reverseReachable = FindReverseReachable(edges, [goalCandidate.Id]);
            for (var fromIndex = 0; fromIndex < nodes.Count; fromIndex += 1)
            {
                var from = nodes[fromIndex];
                if (!reachable.Contains(from.Id))
                {
                    continue;
                }

                var firstCandidateIndex = FindFirstNodeAtOrAfterX(nodesByX, from.X - MaximumBridgeTraversalCost);
                for (var toIndex = firstCandidateIndex; toIndex < nodesByX.Length; toIndex += 1)
                {
                    var to = nodesByX[toIndex];
                    if (to.X > from.X + MaximumBridgeTraversalCost)
                    {
                        break;
                    }

                    if (!reverseReachable.Contains(to.Id) || edgeKeys.Contains(GetEdgeKey(from.Id, to.Id)))
                    {
                        continue;
                    }

                    var bridgeCost = ComputeDirectedTraversalCost(from.X, from.Y, to.X, to.Y);
                    if (bridgeCost > MaximumBridgeTraversalCost || !CanBridgeDirected(from, to))
                    {
                        continue;
                    }

                    var hasClearTraversal = CanConnectDirected(level, from, to, MaximumBridgeTraversalCost);
                    var score = bridgeCost
                        + DistanceBetween(to.X, to.Y, objective.X, objective.Y)
                        + (MathF.Abs(to.X - from.X) * 0.1f)
                        + (hasClearTraversal ? 0f : 100_000f);
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestFrom = from;
                    bestTo = to;
                    found = true;
                }
            }
        }

        if (!found || bestFrom is null || bestTo is null)
        {
            return false;
        }

        var edge = new BotNavigationEdge
        {
            FromNodeId = bestFrom.Id,
            ToNodeId = bestTo.Id,
            Kind = ResolveTraversalKind(bestFrom, bestTo),
            Cost = Math.Max(GridStep, DistanceBetween(bestFrom.X, bestFrom.Y, bestTo.X, bestTo.Y)),
        };
        edges.Add(edge);
        edgeKeys.Add(GetEdgeKey(edge.FromNodeId, edge.ToNodeId));
        return true;
    }

    private static bool CanConnectDirected(SimpleLevel level, BotNavigationNode from, BotNavigationNode to)
    {
        return CanConnectDirected(level, from, to, MaximumDirectedTraversalCost);
    }

    private static bool CanConnectDirected(SimpleLevel level, BotNavigationNode from, BotNavigationNode to, float maximumCost)
    {
        if (!CanBridgeDirected(from, to) || ComputeDirectedTraversalCost(from.X, from.Y, to.X, to.Y) > maximumCost)
        {
            return false;
        }

        return !LineHitsSolid(level, from.X, from.Y - 1f, from.X, from.Y - ConnectionClearanceHeight)
            && !LineHitsSolid(level, to.X, to.Y - 1f, to.X, to.Y - ConnectionClearanceHeight)
            && !LineHitsSolid(level, from.X, from.Y - ConnectionClearanceHeight, to.X, to.Y - ConnectionClearanceHeight);
    }

    private static bool CanBridgeDirected(BotNavigationNode from, BotNavigationNode to)
    {
        var horizontalDistance = MathF.Abs(to.X - from.X);
        if (horizontalDistance < GridStep)
        {
            return false;
        }

        var slope = (from.Y - to.Y) / (to.X - from.X);
        return slope <= MaximumSlope && slope >= -MaximumSlope;
    }

    private static bool IsReverseOnlyDirectedConnection(SimpleLevel level, BotNavigationNode from, BotNavigationNode to)
    {
        if (ComputeDirectedTraversalCost(from.X, from.Y, to.X, to.Y) <= MaximumDirectedTraversalCost
            || ComputeDirectedTraversalCost(to.X, to.Y, from.X, from.Y) > MaximumDirectedTraversalCost)
        {
            return false;
        }

        var deltaX = to.X - from.X;
        if (MathF.Abs(deltaX) < GridStep)
        {
            return false;
        }

        var direction = MathF.Sign(deltaX);
        var slope = (from.Y - to.Y) / deltaX;
        for (var offset = 0f; MathF.Abs((from.X + (offset * direction)) - to.X) >= GridStep; offset += GridStep)
        {
            var sampleX = from.X + (offset * direction);
            var sampleY = from.Y - (slope * (offset * direction));
            if (!LineHitsSolid(level, sampleX, sampleY, sampleX, sampleY + GapSupportProbeDepth))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRedundantClientBotConnection(
        SimpleLevel level,
        BotNavigationNode[] nodesByX,
        BotNavigationNode from,
        BotNavigationNode to)
    {
        var minimumX = MathF.Min(from.X, to.X);
        var maximumX = MathF.Max(from.X, to.X);
        var minimumY = MathF.Min(from.Y, to.Y);
        var maximumY = MathF.Max(from.Y, to.Y);
        var firstCandidateIndex = FindFirstNodeAtOrAfterX(nodesByX, minimumX);

        for (var nodeIndex = firstCandidateIndex; nodeIndex < nodesByX.Length; nodeIndex += 1)
        {
            var candidate = nodesByX[nodeIndex];
            if (candidate.X >= maximumX)
            {
                break;
            }

            if (candidate.Id == from.Id || candidate.Id == to.Id)
            {
                continue;
            }

            if (candidate.X <= minimumX)
            {
                continue;
            }

            if (candidate.Y < minimumY || candidate.Y > maximumY)
            {
                continue;
            }

            if (!LineHitsSolid(level, from.X, from.Y - ConnectionClearanceHeight, candidate.X, candidate.Y - ConnectionClearanceHeight)
                && !LineHitsSolid(level, to.X, to.Y - ConnectionClearanceHeight, candidate.X, candidate.Y - ConnectionClearanceHeight))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetNode(IReadOnlyList<BotNavigationNode> nodes, int nodeId, out BotNavigationNode node)
    {
        for (var index = 0; index < nodes.Count; index += 1)
        {
            if (nodes[index].Id == nodeId)
            {
                node = nodes[index];
                return true;
            }
        }

        node = default!;
        return false;
    }

    private static float ComputeDirectedTraversalCost(float fromX, float fromY, float toX, float toY)
    {
        return MathF.Abs(fromX - toX) + (MathF.Max(0f, fromY - toY - 18f) * 1.2f);
    }

    private static BotNavigationTraversalKind ResolveTraversalKind(BotNavigationNode from, BotNavigationNode to)
    {
        var horizontalDistance = MathF.Abs(to.X - from.X);
        var verticalDistance = to.Y - from.Y;
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

    private static bool LineHitsSolid(SimpleLevel level, float startX, float startY, float endX, float endY)
    {
        var deltaX = endX - startX;
        var deltaY = endY - startY;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= 0.001f)
        {
            return false;
        }

        var directionX = deltaX / distance;
        var directionY = deltaY / distance;
        for (var index = 0; index < level.Solids.Count; index += 1)
        {
            var solid = level.Solids[index];
            if (GetRayIntersectionDistanceWithRectangle(
                    startX,
                    startY,
                    directionX,
                    directionY,
                    solid.Left,
                    solid.Top,
                    solid.Right,
                    solid.Bottom,
                    distance).HasValue)
            {
                return true;
            }
        }

        return false;
    }

    private static float? GetRayIntersectionDistanceWithRectangle(
        float originX,
        float originY,
        float directionX,
        float directionY,
        float left,
        float top,
        float right,
        float bottom,
        float maxDistance)
    {
        var inverseX = MathF.Abs(directionX) < 0.0001f ? float.PositiveInfinity : 1f / directionX;
        var inverseY = MathF.Abs(directionY) < 0.0001f ? float.PositiveInfinity : 1f / directionY;
        var tx1 = (left - originX) * inverseX;
        var tx2 = (right - originX) * inverseX;
        var ty1 = (top - originY) * inverseY;
        var ty2 = (bottom - originY) * inverseY;
        var tMin = MathF.Max(MathF.Min(tx1, tx2), MathF.Min(ty1, ty2));
        var tMax = MathF.Min(MathF.Max(tx1, tx2), MathF.Max(ty1, ty2));
        if (tMax < 0f || tMin > tMax)
        {
            return null;
        }

        var distance = tMin < 0f ? 0f : tMin;
        return distance <= maxDistance ? distance : null;
    }

    private static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static long GetEdgeKey(int fromNodeId, int toNodeId)
    {
        return ((long)fromNodeId << 32) | (uint)toNodeId;
    }

    private static Dictionary<int, List<int>> BuildAdjacency(IReadOnlyList<BotNavigationEdge> edges)
    {
        var adjacency = new Dictionary<int, List<int>>();
        for (var index = 0; index < edges.Count; index += 1)
        {
            var edge = edges[index];
            if (!adjacency.TryGetValue(edge.FromNodeId, out var outgoing))
            {
                outgoing = new List<int>();
                adjacency[edge.FromNodeId] = outgoing;
            }

            outgoing.Add(edge.ToNodeId);
        }

        return adjacency;
    }

    private static bool HasRoute(
        Dictionary<int, List<int>> adjacency,
        IEnumerable<int> startNodeIds,
        IEnumerable<int> goalNodeIds)
    {
        var goals = goalNodeIds.ToHashSet();
        if (goals.Count == 0)
        {
            return false;
        }

        var reachable = FindReachable(adjacency, startNodeIds);
        return reachable.Overlaps(goals);
    }

    private static HashSet<int> FindReachable(Dictionary<int, List<int>> adjacency, IEnumerable<int> startNodeIds)
    {
        var seen = new HashSet<int>();
        var pending = new Queue<int>();
        foreach (var startNodeId in startNodeIds)
        {
            if (seen.Add(startNodeId))
            {
                pending.Enqueue(startNodeId);
            }
        }

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!adjacency.TryGetValue(current, out var outgoing))
            {
                continue;
            }

            for (var index = 0; index < outgoing.Count; index += 1)
            {
                if (seen.Add(outgoing[index]))
                {
                    pending.Enqueue(outgoing[index]);
                }
            }
        }

        return seen;
    }

    private static HashSet<int> FindReverseReachable(IReadOnlyList<BotNavigationEdge> edges, IEnumerable<int> goalNodeIds)
    {
        var reverseAdjacency = new Dictionary<int, List<int>>();
        for (var index = 0; index < edges.Count; index += 1)
        {
            var edge = edges[index];
            if (!reverseAdjacency.TryGetValue(edge.ToNodeId, out var incoming))
            {
                incoming = new List<int>();
                reverseAdjacency[edge.ToNodeId] = incoming;
            }

            incoming.Add(edge.FromNodeId);
        }

        return FindReachable(reverseAdjacency, goalNodeIds);
    }

    private static IReadOnlyList<BotNavigationNode> FindNearestNodes(
        IReadOnlyList<BotNavigationNode> nodes,
        float x,
        float y,
        float maxDistance)
    {
        var maxDistanceSquared = maxDistance * maxDistance;
        return nodes
            .Where(node => DistanceSquared(node.X, node.Y, x, y) <= maxDistanceSquared)
            .OrderBy(node => DistanceSquared(node.X, node.Y, x, y))
            .Take(12)
            .ToArray();
    }

    private static BotNavigationNode? FindNearestNode(IReadOnlyList<BotNavigationNode> nodes, float x, float y)
    {
        BotNavigationNode? bestNode = null;
        var bestDistanceSquared = float.PositiveInfinity;
        for (var index = 0; index < nodes.Count; index += 1)
        {
            var node = nodes[index];
            var distanceSquared = DistanceSquared(node.X, node.Y, x, y);
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestNode = node;
        }

        return bestNode;
    }

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private static bool SupportsAttackReachabilityAudit(GameModeKind mode)
    {
        return mode is GameModeKind.CaptureTheFlag
            or GameModeKind.Generator
            or GameModeKind.ControlPoint
            or GameModeKind.Arena
            or GameModeKind.KingOfTheHill
            or GameModeKind.DoubleKingOfTheHill;
    }

    private static IReadOnlyList<NavMarkerPoint> GetTeamSpawns(SimpleLevel level, IReadOnlyList<BotNavigationNode> nodes, PlayerTeam team)
    {
        var authoredSpawns = nodes
            .Where(node => node.Kind == BotNavigationNodeKind.Spawn && GetNodeTeam(node) == team)
            .Select(node => new NavMarkerPoint(node.X, node.Y))
            .ToArray();
        if (authoredSpawns.Length > 0)
        {
            return authoredSpawns;
        }

        var spawns = team == PlayerTeam.Red ? level.RedSpawns : level.BlueSpawns;
        return spawns.Count == 0
            ? [new NavMarkerPoint(level.LocalSpawn.X, level.LocalSpawn.Y)]
            : spawns.Select(spawn => new NavMarkerPoint(spawn.X, spawn.Y)).ToArray();
    }

    private static IReadOnlyList<NavMarkerPoint> GetAttackObjectives(SimpleLevel level, IReadOnlyList<BotNavigationNode> nodes, PlayerTeam team)
    {
        var enemyTeam = GetOpposingTeam(team);
        var authoredObjectives = nodes
            .Where(node => node.Kind == BotNavigationNodeKind.Objective && GetNodeTeam(node) == enemyTeam)
            .Select(node => new NavMarkerPoint(node.X, node.Y))
            .ToArray();
        if (authoredObjectives.Length > 0)
        {
            return authoredObjectives;
        }

        return level.Mode switch
        {
            GameModeKind.CaptureTheFlag => level.GetIntelBase(enemyTeam) is { } intelBase
                ? [new NavMarkerPoint(intelBase.X, intelBase.Y)]
                : [],
            GameModeKind.Generator => level.GetRoomObjects(RoomObjectType.Generator)
                .Where(generator => !generator.Team.HasValue || generator.Team.Value == enemyTeam)
                .Select(generator => new NavMarkerPoint(generator.CenterX, generator.CenterY))
                .ToArray(),
            GameModeKind.Arena => level.GetRoomObjects(RoomObjectType.ArenaControlPoint)
                .Select(point => new NavMarkerPoint(point.CenterX, point.CenterY))
                .ToArray(),
            GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill => GetControlPointObjectives(level),
            _ => [],
        };
    }

    private static IReadOnlyList<NavMarkerPoint> GetControlPointObjectives(SimpleLevel level)
    {
        var captureZones = level.GetRoomObjects(RoomObjectType.CaptureZone)
            .Select(point => new NavMarkerPoint(point.CenterX, point.CenterY))
            .ToArray();
        return captureZones.Length > 0
            ? captureZones
            : level.GetRoomObjects(RoomObjectType.ControlPoint)
                .Select(point => new NavMarkerPoint(point.CenterX, point.CenterY))
                .ToArray();
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }

    private static PlayerTeam? GetNodeTeam(BotNavigationNode node)
    {
        if (node.Team.HasValue)
        {
            return node.Team.Value;
        }

        if (node.Label.StartsWith("red", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerTeam.Red;
        }

        if (node.Label.StartsWith("blue", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerTeam.Blue;
        }

        return null;
    }

    private readonly record struct NavMarkerPoint(float X, float Y);

    private static int FindFirstNodeAtOrAfterX(BotNavigationNode[] nodesByX, float x)
    {
        var low = 0;
        var high = nodesByX.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (nodesByX[middle].X < x)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
