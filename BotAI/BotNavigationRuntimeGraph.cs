namespace OpenGarrison.BotAI;

internal sealed class BotNavigationRuntimeGraph
{
    private readonly BotNavigationNode[] _nodesById;
    private readonly Dictionary<int, List<BotNavigationEdge>> _edgesByFromNodeId = new();
    private readonly Dictionary<int, List<BotNavigationEdge>> _edgesByToNodeId = new();
    private readonly Dictionary<long, BotNavigationEdge> _edgeByNodePair = new();
    private readonly Dictionary<int, (float MinX, float MaxX)> _surfaceExtentsBySurfaceId = new();
    private readonly Dictionary<long, int[]?> _routeCache = new();
    private readonly Dictionary<long, int[]?> _goalWeightCache = new();

    public BotNavigationRuntimeGraph(BotNavigationAsset asset)
    {
        Asset = asset;
        var scopeToken = asset.BuildStrategy == BotNavigationBuildStrategy.ModernClientBotPointGraph && !asset.ClassId.HasValue
            ? "modern"
            : asset.ClassId.HasValue
                ? BotNavigationClasses.GetFileToken(asset.ClassId.Value)
                : BotNavigationProfiles.GetFileToken(asset.Profile);
        CacheKey = $"{asset.LevelName}|{asset.MapAreaIndex}|{scopeToken}|{asset.LevelFingerprint}";

        var maxNodeId = asset.Nodes.Count == 0 ? -1 : asset.Nodes.Max(static node => node.Id);
        _nodesById = new BotNavigationNode[Math.Max(0, maxNodeId + 1)];
        for (var index = 0; index < asset.Nodes.Count; index += 1)
        {
            var node = asset.Nodes[index];
            _nodesById[node.Id] = node;

            if (_surfaceExtentsBySurfaceId.TryGetValue(node.SurfaceId, out var extents))
            {
                _surfaceExtentsBySurfaceId[node.SurfaceId] = (MathF.Min(extents.MinX, node.X), MathF.Max(extents.MaxX, node.X));
            }
            else
            {
                _surfaceExtentsBySurfaceId[node.SurfaceId] = (node.X, node.X);
            }
        }

        for (var index = 0; index < asset.Edges.Count; index += 1)
        {
            var edge = asset.Edges[index];
            if (!_edgesByFromNodeId.TryGetValue(edge.FromNodeId, out var edges))
            {
                edges = new List<BotNavigationEdge>();
                _edgesByFromNodeId[edge.FromNodeId] = edges;
            }

            edges.Add(edge);
            if (!_edgesByToNodeId.TryGetValue(edge.ToNodeId, out var incomingEdges))
            {
                incomingEdges = new List<BotNavigationEdge>();
                _edgesByToNodeId[edge.ToNodeId] = incomingEdges;
            }

            incomingEdges.Add(edge);
            _edgeByNodePair[GetPairKey(edge.FromNodeId, edge.ToNodeId)] = edge;
        }
    }

    public BotNavigationAsset Asset { get; }

    public string CacheKey { get; }

    public BotNavigationBuildStrategy BuildStrategy => Asset.BuildStrategy;

    public IReadOnlyList<BotNavigationNode> Nodes => Asset.Nodes;

    public bool TryFindNearestNode(float x, float y, float maxDistance, out BotNavigationNode node)
    {
        return TryFindNearestNode(x, y, maxDistance, requireGroundSupport: false, out node);
    }

    public bool TryFindNearestNode(float x, float y, float maxDistance, bool requireGroundSupport, out BotNavigationNode node)
    {
        node = default!;
        var bestDistanceSquared = maxDistance <= 0f ? float.PositiveInfinity : maxDistance * maxDistance;
        var found = false;
        for (var index = 0; index < _nodesById.Length; index += 1)
        {
            var candidate = _nodesById[index];
            if (candidate is null)
            {
                continue;
            }

            if (requireGroundSupport && !candidate.RequiresGroundSupport)
            {
                continue;
            }

            var distanceSquared = DistanceSquared(candidate.X, candidate.Y, x, y);
            if (distanceSquared > bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            node = candidate;
            found = true;
        }

        return found;
    }

    public bool TryGetNode(int nodeId, out BotNavigationNode node)
    {
        node = default!;
        if (nodeId < 0 || nodeId >= _nodesById.Length || _nodesById[nodeId] is null)
        {
            return false;
        }

        node = _nodesById[nodeId];
        return true;
    }

    public bool TryGetEdge(int fromNodeId, int toNodeId, out BotNavigationEdge edge)
    {
        return _edgeByNodePair.TryGetValue(GetPairKey(fromNodeId, toNodeId), out edge!);
    }

    public bool TryGetOutgoingEdges(int fromNodeId, out IReadOnlyList<BotNavigationEdge> edges)
    {
        if (_edgesByFromNodeId.TryGetValue(fromNodeId, out var outgoingEdges))
        {
            edges = outgoingEdges;
            return true;
        }

        edges = Array.Empty<BotNavigationEdge>();
        return false;
    }

    public int[]? GetGoalWeights(int goalNodeId, int maximumDepth = 130)
    {
        var cacheKey = GetPairKey(goalNodeId, maximumDepth);
        if (_goalWeightCache.TryGetValue(cacheKey, out var cachedWeights))
        {
            return cachedWeights;
        }

        if (!TryGetNode(goalNodeId, out _))
        {
            _goalWeightCache[cacheKey] = null;
            return null;
        }

        var weights = new int[_nodesById.Length];
        var pendingNodeIds = new Queue<int>();
        weights[goalNodeId] = 1;
        pendingNodeIds.Enqueue(goalNodeId);

        while (pendingNodeIds.Count > 0)
        {
            var currentNodeId = pendingNodeIds.Dequeue();
            var currentWeight = weights[currentNodeId];
            if (currentWeight <= 0 || currentWeight >= maximumDepth)
            {
                continue;
            }

            if (!_edgesByToNodeId.TryGetValue(currentNodeId, out var incomingEdges))
            {
                continue;
            }

            for (var edgeIndex = 0; edgeIndex < incomingEdges.Count; edgeIndex += 1)
            {
                var incomingEdge = incomingEdges[edgeIndex];
                var predecessorNodeId = incomingEdge.FromNodeId;
                var predecessorWeight = currentWeight + 1;
                if (weights[predecessorNodeId] > 0 && weights[predecessorNodeId] <= predecessorWeight)
                {
                    continue;
                }

                weights[predecessorNodeId] = predecessorWeight;
                pendingNodeIds.Enqueue(predecessorNodeId);
            }
        }

        _goalWeightCache[cacheKey] = weights;
        return weights;
    }

    public int GetDropDirection(int fromNodeId, int toNodeId)
    {
        if (!TryGetNode(fromNodeId, out var fromNode))
        {
            return 0;
        }

        if (_surfaceExtentsBySurfaceId.TryGetValue(fromNode.SurfaceId, out var extents)
            && (extents.MaxX - extents.MinX) > 4f)
        {
            if (MathF.Abs(fromNode.X - extents.MinX) <= 2f)
            {
                return -1;
            }

            if (MathF.Abs(fromNode.X - extents.MaxX) <= 2f)
            {
                return 1;
            }
        }

        if (TryGetNode(toNodeId, out var toNode))
        {
            return toNode.X >= fromNode.X ? 1 : -1;
        }

        return 0;
    }

    public int[]? FindRoute(int startNodeId, int goalNodeId)
    {
        if (startNodeId == goalNodeId)
        {
            return [startNodeId];
        }

        var routeKey = GetPairKey(startNodeId, goalNodeId);
        if (_routeCache.TryGetValue(routeKey, out var cachedRoute))
        {
            return cachedRoute;
        }

        var route = ComputeRoute(startNodeId, goalNodeId);
        _routeCache[routeKey] = route;
        return route;
    }

    public bool TryFindRouteToGoalRadius(
        int startNodeId,
        float goalX,
        float goalY,
        float goalRadius,
        out int[] route,
        out int goalNodeId)
    {
        route = Array.Empty<int>();
        goalNodeId = -1;
        if (!TryGetNode(startNodeId, out _))
        {
            return false;
        }

        BuildShortestPathTree(startNodeId, out var costByNodeId, out var cameFromNodeId);

        var bestScore = float.PositiveInfinity;
        for (var index = 0; index < _nodesById.Length; index += 1)
        {
            var candidate = _nodesById[index];
            if (candidate is null || !float.IsFinite(costByNodeId[index]))
            {
                continue;
            }

            var goalDistance = DistanceBetween(candidate.X, candidate.Y, goalX, goalY);
            if (goalDistance > goalRadius)
            {
                continue;
            }

            var score = costByNodeId[index] + goalDistance;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            goalNodeId = index;
        }

        if (goalNodeId < 0)
        {
            return false;
        }

        route = ReconstructRoute(startNodeId, goalNodeId, cameFromNodeId);
        return route.Length > 1;
    }

    public bool TryFindBestPartialRoute(
        int startNodeId,
        float goalX,
        float goalY,
        float minimumImprovementDistance,
        out int[] route,
        out int goalNodeId)
    {
        route = Array.Empty<int>();
        goalNodeId = -1;
        if (!TryGetNode(startNodeId, out var startNode))
        {
            return false;
        }

        BuildShortestPathTree(startNodeId, out var costByNodeId, out var cameFromNodeId);

        var startGoalDistance = DistanceBetween(startNode.X, startNode.Y, goalX, goalY);
        var bestGoalDistance = startGoalDistance;
        var bestPathCost = float.PositiveInfinity;

        for (var index = 0; index < _nodesById.Length; index += 1)
        {
            var candidate = _nodesById[index];
            if (candidate is null
                || index == startNodeId
                || !float.IsFinite(costByNodeId[index]))
            {
                continue;
            }

            var goalDistance = DistanceBetween(candidate.X, candidate.Y, goalX, goalY);
            if ((startGoalDistance - goalDistance) < minimumImprovementDistance)
            {
                continue;
            }

            if (goalDistance > bestGoalDistance + 0.01f)
            {
                continue;
            }

            if (goalDistance >= bestGoalDistance - 0.01f && costByNodeId[index] >= bestPathCost)
            {
                continue;
            }

            bestGoalDistance = goalDistance;
            bestPathCost = costByNodeId[index];
            goalNodeId = index;
        }

        if (goalNodeId < 0)
        {
            return false;
        }

        route = ReconstructRoute(startNodeId, goalNodeId, cameFromNodeId);
        return route.Length > 1;
    }

    private int[]? ComputeRoute(int startNodeId, int goalNodeId)
    {
        if (!TryGetNode(startNodeId, out var startNode) || !TryGetNode(goalNodeId, out var goalNode))
        {
            return null;
        }

        var costByNodeId = new float[_nodesById.Length];
        var cameFromNodeId = new int[_nodesById.Length];
        Array.Fill(costByNodeId, float.PositiveInfinity);
        Array.Fill(cameFromNodeId, -1);

        var frontier = new PriorityQueue<int, float>();
        costByNodeId[startNodeId] = 0f;
        frontier.Enqueue(startNodeId, 0f);

        while (frontier.TryDequeue(out var currentNodeId, out _))
        {
            if (currentNodeId == goalNodeId)
            {
                break;
            }

            if (!_edgesByFromNodeId.TryGetValue(currentNodeId, out var edges))
            {
                continue;
            }

            for (var index = 0; index < edges.Count; index += 1)
            {
                var edge = edges[index];
                var newCost = costByNodeId[currentNodeId] + Math.Max(1f, edge.Cost);
                if (newCost >= costByNodeId[edge.ToNodeId])
                {
                    continue;
                }

                costByNodeId[edge.ToNodeId] = newCost;
                cameFromNodeId[edge.ToNodeId] = currentNodeId;
                var heuristic = TryGetNode(edge.ToNodeId, out var nextNode)
                    ? DistanceBetween(nextNode.X, nextNode.Y, goalNode.X, goalNode.Y)
                    : 0f;
                frontier.Enqueue(edge.ToNodeId, newCost + heuristic);
            }
        }

        if (cameFromNodeId[goalNodeId] < 0)
        {
            return null;
        }

        var route = new List<int> { goalNodeId };
        var current = goalNodeId;
        while (current != startNodeId)
        {
            current = cameFromNodeId[current];
            if (current < 0)
            {
                return null;
            }

            route.Add(current);
        }

        route.Reverse();
        return route.ToArray();
    }

    private void BuildShortestPathTree(int startNodeId, out float[] costByNodeId, out int[] cameFromNodeId)
    {
        costByNodeId = new float[_nodesById.Length];
        cameFromNodeId = new int[_nodesById.Length];
        Array.Fill(costByNodeId, float.PositiveInfinity);
        Array.Fill(cameFromNodeId, -1);

        var frontier = new PriorityQueue<int, float>();
        costByNodeId[startNodeId] = 0f;
        frontier.Enqueue(startNodeId, 0f);

        while (frontier.TryDequeue(out var currentNodeId, out _))
        {
            if (!_edgesByFromNodeId.TryGetValue(currentNodeId, out var edges))
            {
                continue;
            }

            for (var index = 0; index < edges.Count; index += 1)
            {
                var edge = edges[index];
                var newCost = costByNodeId[currentNodeId] + Math.Max(1f, edge.Cost);
                if (newCost >= costByNodeId[edge.ToNodeId])
                {
                    continue;
                }

                costByNodeId[edge.ToNodeId] = newCost;
                cameFromNodeId[edge.ToNodeId] = currentNodeId;
                frontier.Enqueue(edge.ToNodeId, newCost);
            }
        }
    }

    private static int[] ReconstructRoute(int startNodeId, int goalNodeId, int[] cameFromNodeId)
    {
        if (startNodeId == goalNodeId)
        {
            return [startNodeId];
        }

        if (goalNodeId < 0 || goalNodeId >= cameFromNodeId.Length || cameFromNodeId[goalNodeId] < 0)
        {
            return Array.Empty<int>();
        }

        var route = new List<int> { goalNodeId };
        var current = goalNodeId;
        while (current != startNodeId)
        {
            current = cameFromNodeId[current];
            if (current < 0)
            {
                return Array.Empty<int>();
            }

            route.Add(current);
        }

        route.Reverse();
        return route.ToArray();
    }

    private static long GetPairKey(int fromNodeId, int toNodeId)
    {
        return ((long)fromNodeId << 32) | (uint)toNodeId;
    }

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        return MathF.Sqrt(DistanceSquared(x1, y1, x2, y2));
    }
}
