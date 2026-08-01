using System.Collections.Concurrent;

namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// A lightweight waypoint graph built from level geometry.
/// Nodes are walkable positions; edges encode how to traverse between them.
/// </summary>
public sealed class NavGraph
{
    // Contact probes and live PlayerEntity integration can differ by a few
    // sub-pixel/rounding units at a surface edge. Keep completion ownership
    // strict to the recorded window, but allow this small runtime handoff
    // margin when the player is already on an accepted destination surface.
    // The margin is bounded below the existing steering recovery tolerances;
    // it is not a free-standing route shortcut.
    private const float RuntimeCompletionHorizontalSlack = 16f;
    private const float RuntimeCompletionVerticalSlack = 8f;

    private const float SignificantWalkVerticalDelta = 24f;
    private const float SuspiciousRelayVerticalDelta = 24f;
    private const float SuspiciousRelayHorizontalReach = 260f;
    private const float SuspiciousRelayCostFloorMultiplier = 0.55f;

    private readonly NavNode[] _nodes;
    private readonly List<NavEdge>[] _adjacency;
    private readonly List<ReverseNavEdge>[] _reverseAdjacency;
    private readonly Dictionary<long, NavEdge[]> _certifiedJumpAlternatives;
    private readonly int[] _surfaceNodeIndices;
    private readonly Dictionary<int, int[]> _surfaceNodeIndicesById;
    private readonly int[] _objectiveNodeIndices;
    private readonly int[] _spawnAdjacentTeamMasks;
    private readonly string? _levelName;
    private readonly GameModeKind? _mode;
    private readonly bool _isOg2Alpha;
    private readonly ConcurrentDictionary<NavPathCacheKey, NavPath> _alphaPathCache = new();
    private readonly ConcurrentDictionary<NavPathCacheKey, byte> _alphaFailedPathCache = new();
    private readonly ConcurrentDictionary<AlphaBlockedPathCacheKey, NavPath> _alphaBlockedPathCache = new();
    private readonly ConcurrentDictionary<AlphaBlockedPathCacheKey, byte> _alphaFailedBlockedPathCache = new();
    private readonly ConcurrentDictionary<AlphaObjectiveResolutionCacheKey, int> _alphaObjectiveResolutionCache = new();
    private readonly ConcurrentDictionary<AlphaBlockedObjectiveReachabilityCacheKey, HashSet<int>> _alphaBlockedObjectiveReachabilityCache = new();
    private readonly ConcurrentDictionary<AlphaObjectiveReachabilityCacheKey, HashSet<int>> _alphaObjectiveReachabilityCache = new();
    private readonly ConcurrentDictionary<AlphaSearchProfileKey, ConcurrentDictionary<int, NavEdge[]>> _alphaSearchEdgesCache = new();
    private readonly ThreadLocal<NavPathSearchWorkspace> _pathSearchWorkspaces = new();

    public NavGraph(
        NavNode[] nodes,
        List<NavEdge>[] adjacency,
        string? levelName = null,
        GameModeKind? mode = null,
        IReadOnlyList<NavSpawnAnchor>? spawnAnchors = null,
        bool isOg2Alpha = false)
    {
        _nodes = nodes;
        _adjacency = adjacency;
        _reverseAdjacency = BuildReverseAdjacency(adjacency);
        _certifiedJumpAlternatives = BuildCertifiedJumpAlternatives(adjacency);
        (_surfaceNodeIndices, _surfaceNodeIndicesById) = BuildSurfaceNodeIndices(nodes);
        _objectiveNodeIndices = BuildObjectiveNodeIndices(nodes);
        _spawnAdjacentTeamMasks = ResolveSpawnAdjacentTeamMasks(nodes, spawnAnchors);
        _levelName = levelName;
        _mode = mode;
        _isOg2Alpha = isOg2Alpha;
    }

    public int NodeCount => _nodes.Length;

    public bool IsOg2Alpha => _isOg2Alpha;

    public int AlphaPathCacheCount => _alphaPathCache.Count;

    public NavNode GetNode(int index) => _nodes[index];

    public ReadOnlySpan<NavEdge> GetEdges(int nodeIndex) =>
        _adjacency[nodeIndex] is { Count: > 0 } list
            ? System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list)
            : ReadOnlySpan<NavEdge>.Empty;

    /// <summary>
    /// Selects a nearby support node that has a valid incoming edge to an
    /// objective anchor. This is used when the live body is close enough to
    /// be attached to the anchor itself, but is still outside the gameplay
    /// marker. A one-node path cannot provide the final approach movement.
    /// </summary>
    public int FindNearestObjectiveApproachNode(
        int objectiveNode,
        float x,
        float y,
        PlayerClass? playerClass = null,
        IReadOnlySet<NavEdgeBlock>? blockedEdges = null,
        PlayerTeam? team = null,
        bool carryingIntel = false,
        float maxHorizontalDistance = 256f,
        float maxVerticalDistance = 128f)
    {
        if (objectiveNode < 0 || objectiveNode >= _nodes.Length)
        {
            return -1;
        }

        var bestNode = -1;
        var bestScore = float.PositiveInfinity;
        var nearbyIncomingCount = 0;
        var compatibleIncomingCount = 0;
        for (var fromNode = 0; fromNode < _adjacency.Length; fromNode += 1)
        {
            var candidate = _nodes[fromNode];
            var dx = candidate.X - x;
            var dy = candidate.Y - y;
            if (MathF.Abs(dx) > maxHorizontalDistance
                || MathF.Abs(dy) > maxVerticalDistance)
            {
                continue;
            }

            foreach (var edge in _adjacency[fromNode])
            {
                if (edge.ToNode == objectiveNode)
                {
                    nearbyIncomingCount += 1;
                }

                if (edge.ToNode != objectiveNode
                    || (blockedEdges is not null
                        && blockedEdges.Contains(new NavEdgeBlock(fromNode, objectiveNode, edge.Kind)))
                    || (playerClass.HasValue
                        && (!SupportsEdge(edge, playerClass.Value, team, carryingIntel)
                            || ShouldPreferCertifiedJump(fromNode, edge, playerClass.Value, team, carryingIntel))))
                {
                    continue;
                }

                compatibleIncomingCount += 1;

                var score = (dx * dx) + (dy * dy) + (edge.Cost * 0.01f);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestNode = fromNode;
                }
            }
        }

        if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_APPROACH_TRACE") is "1" or "true" or "TRUE")
        {
            Console.WriteLine(
                $"alphaObjectiveApproach objective={objectiveNode} pos=({x:0.0},{y:0.0}) " +
                $"nearby={nearbyIncomingCount} compatible={compatibleIncomingCount} result={bestNode}");
        }

        return bestNode;
    }

    public bool IsPathCompatible(
        NavPath path,
        PlayerClass playerClass,
        PlayerTeam? team,
        bool carryingIntel)
    {
        for (var index = 1; index < path.Count; index += 1)
        {
            if (!path.TryGetIncomingEdge(index, out var edge))
            {
                return false;
            }

            var fromNode = path.GetWaypoint(index - 1);
            if (!SupportsEdge(edge, playerClass, team, carryingIntel)
                || ShouldPreferCertifiedJump(fromNode, edge, playerClass, team, carryingIntel))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Populate the immutable alpha path cache for the first objective routes
    /// used by a practice roster. This is a load-time optimization only: it
    /// does not change edge eligibility, costs, or the path returned by A*.
    /// Keeping this work in the map warmup prevents the first live bot think
    /// from paying all class/team route allocations in one simulation frame.
    /// </summary>
    public int WarmAlphaObjectiveRoutes(
        SimpleLevel level,
        IReadOnlyList<PlayerClass> playerClasses)
    {
        if (!_isOg2Alpha
            || playerClasses.Count == 0)
        {
            return 0;
        }

        var objectiveTargets = _objectiveNodeIndices.Length > 0
            ? _objectiveNodeIndices
                .Select(index => (_nodes[index].X, _nodes[index].Y))
                .ToArray()
            : level.RoomObjects
                .Where(static marker => marker.Type is
                    RoomObjectType.CaptureZone or
                    RoomObjectType.ArenaControlPoint or
                    RoomObjectType.ControlPoint)
                .Select(static marker => (marker.CenterX, marker.CenterY))
                .Distinct()
                .ToArray();
        if (objectiveTargets.Length == 0)
        {
            return 0;
        }

        var warmPathCount = 0;
        WarmAlphaObjectiveRoutesForTeam(level.RedSpawns, PlayerTeam.Red, playerClasses, objectiveTargets, ref warmPathCount);
        WarmAlphaObjectiveRoutesForTeam(level.BlueSpawns, PlayerTeam.Blue, playerClasses, objectiveTargets, ref warmPathCount);
        return warmPathCount;
    }

    private void WarmAlphaObjectiveRoutesForTeam(
        IReadOnlyList<SpawnPoint> spawns,
        PlayerTeam team,
        IReadOnlyList<PlayerClass> playerClasses,
        IReadOnlyList<(float X, float Y)> objectiveTargets,
        ref int warmPathCount)
    {
        if (spawns.Count == 0)
        {
            return;
        }

        // Spawn reservation rotates through the standard spawn pool. Warm each
        // distinct traversal start that the live roster can actually receive;
        // warming only spawns[0] leaves the first bot assigned to another
        // spawn paying the full A* cost during the first gameplay frame.
        var startAnchors = new Dictionary<int, SpawnPoint>();
        foreach (var spawn in spawns)
        {
            var startNode = FindNearestTraversalStartNode(
                spawn.X,
                spawn.Y,
                maxAboveDistance: 48f,
                maxBelowDistance: 192f);
            if (startNode < 0)
            {
                startNode = FindNearestNode(spawn.X, spawn.Y);
            }

            if (startNode >= 0)
            {
                startAnchors.TryAdd(startNode, spawn);
            }
        }

        if (startAnchors.Count == 0)
        {
            return;
        }

        if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_WARM_TRACE") is "1" or "true" or "TRUE")
        {
            Console.WriteLine(
                $"[botbrain] alpha-warm team={team} starts={string.Join(',', startAnchors.Keys.Order())} " +
                $"objectives={_objectiveNodeIndices.Length} classes={playerClasses.Count}");
        }

        var distinctClasses = playerClasses.Distinct().ToArray();
        foreach (var (startNode, _) in startAnchors)
        {
            for (var objectiveIndex = 0; objectiveIndex < objectiveTargets.Count; objectiveIndex += 1)
            {
                var objective = objectiveTargets[objectiveIndex];
                for (var classIndex = 0; classIndex < distinctClasses.Length; classIndex += 1)
                {
                    var playerClass = distinctClasses[classIndex];
                    // Use the same goal-aware start-candidate search as live
                    // UpdatePath. A spawn can be nearest to a surface that is
                    // not itself routeable for a class; the live recovery
                    // selector then tries adjacent supports. Warming only the
                    // geometric start misses those fallback path keys.
                    var reachableObjectiveNode = FindNearestReachableObjectiveNode(
                        objective.X,
                        objective.Y,
                        startNode,
                        playerClass,
                        team: team,
                        carryingIntel: false);
                    if (reachableObjectiveNode < 0)
                    {
                        reachableObjectiveNode = FindNearestReachableNode(
                            objective.X,
                            objective.Y,
                            startNode,
                            playerClass,
                            team: team,
                            carryingIntel: false);
                    }
                    if (reachableObjectiveNode >= 0
                        && FindPath(
                            startNode,
                            reachableObjectiveNode,
                            playerClass,
                            team: team,
                            carryingIntel: false) is not null)
                    {
                        warmPathCount += 1;
                    }

                    if (_mode == GameModeKind.CaptureTheFlag)
                    {
                        var carrierObjectiveNode = FindNearestReachableObjectiveNode(
                            objective.X,
                            objective.Y,
                            startNode,
                            playerClass,
                            team: team,
                            carryingIntel: true);
                        if (carrierObjectiveNode < 0)
                        {
                            carrierObjectiveNode = FindNearestReachableNode(
                                objective.X,
                                objective.Y,
                                startNode,
                                playerClass,
                                team: team,
                                carryingIntel: true);
                        }

                        if (carrierObjectiveNode >= 0
                            && FindPath(
                                startNode,
                                carrierObjectiveNode,
                                playerClass,
                                team: team,
                                carryingIntel: true) is not null)
                        {
                            warmPathCount += 1;
                        }
                    }
                }
            }
        }

        if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_WARM_TRACE") is "1" or "true" or "TRUE")
        {
            Console.WriteLine($"[botbrain] alpha-warm team={team} paths={warmPathCount} cache={_alphaPathCache.Count}");
        }
    }

    /// <summary>
    /// Find the nearest node to a world position.
    /// </summary>
    public int FindNearestNode(float x, float y)
    {
        if (_nodes.Length == 0)
        {
            return -1;
        }

        var bestIndex = 0;
        var bestDistSq = float.MaxValue;
        for (var i = 0; i < _nodes.Length; i++)
        {
            var dx = _nodes[i].X - x;
            var dy = _nodes[i].Y - y;
            var distSq = (dx * dx) + (dy * dy);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    public int FindNearestTraversalStartNode(
        float x,
        float y,
        float maxAboveDistance = float.PositiveInfinity,
        float maxBelowDistance = float.PositiveInfinity)
    {
        if (_nodes.Length == 0)
        {
            return -1;
        }

        var bestIndex = -1;
        var bestScore = float.MaxValue;
        for (var i = 0; i < _nodes.Length; i++)
        {
            if (_nodes[i].Y < y - maxAboveDistance)
            {
                continue;
            }

            if (_nodes[i].Y > y + maxBelowDistance)
            {
                continue;
            }

            var dx = _nodes[i].X - x;
            var dy = _nodes[i].Y - y;
            var score = (dx * dx) + (dy * dy * 4f);
            if (_nodes[i].Y < y - 24f)
            {
                var above = y - _nodes[i].Y;
                score += 1_000_000f + (above * above * 16f);
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex >= 0 || float.IsPositiveInfinity(maxBelowDistance)
            ? bestIndex >= 0
                ? bestIndex
                : FindNearestTraversalStartNode(x, y)
            : -1;
    }

    /// <summary>
    /// Select the nearest traversal attachment that can actually reach the
    /// requested goal under the supplied navigation profile. This is used for
    /// recovery after a missed contact, where the geometrically nearest node
    /// can belong to a small local component while a slightly farther node is
    /// the valid continuation of the route.
    /// </summary>
    public int FindNearestTraversalStartNodeForGoal(
        float x,
        float y,
        float maxAboveDistance,
        float maxBelowDistance,
        int goalNode,
        PlayerClass? playerClass = null,
        IReadOnlySet<NavEdgeBlock>? blockedEdges = null,
        PlayerTeam? team = null,
        bool carryingIntel = false,
        int maxCandidates = 32,
        float maxHorizontalDistance = float.PositiveInfinity)
    {
        if (_nodes.Length == 0
            || goalNode < 0
            || goalNode >= _nodes.Length)
        {
            return -1;
        }

        var candidates = new List<(int NodeIndex, float Score)>();
        for (var i = 0; i < _nodes.Length; i += 1)
        {
            if (_nodes[i].Y < y - maxAboveDistance
                || _nodes[i].Y > y + maxBelowDistance)
            {
                continue;
            }

            var dx = _nodes[i].X - x;
            if (MathF.Abs(dx) > maxHorizontalDistance)
            {
                continue;
            }

            var dy = _nodes[i].Y - y;
            var score = (dx * dx) + (dy * dy * 4f);
            if (_nodes[i].Y < y - 24f)
            {
                var above = y - _nodes[i].Y;
                score += 1_000_000f + (above * above * 16f);
            }

            candidates.Add((i, score));
        }

        var orderedCandidates = candidates
            .OrderBy(static candidate => candidate.Score)
            .Take(Math.Max(1, maxCandidates))
            .ToArray();
        var reachableCandidates = _isOg2Alpha && blockedEdges is { Count: > 0 }
            ? FindAlphaBlockedReachableCandidateNodesToGoal(
                goalNode,
                orderedCandidates,
                playerClass,
                blockedEdges,
                team,
                carryingIntel)
            : FindReachableNodesFromGoal(
                goalNode,
                orderedCandidates,
                playerClass,
                blockedEdges,
                team,
                carryingIntel);
        foreach (var candidate in orderedCandidates)
        {
            if (reachableCandidates.Contains(candidate.NodeIndex))
            {
                return candidate.NodeIndex;
            }
        }

        return -1;
    }

    private HashSet<int> FindAlphaBlockedReachableCandidateNodesToGoal(
        int goalNode,
        IReadOnlyList<(int NodeIndex, float Score)> orderedCandidates,
        PlayerClass? playerClass,
        IReadOnlySet<NavEdgeBlock> blockedEdges,
        PlayerTeam? team,
        bool carryingIntel)
    {
        var candidateSet = orderedCandidates
            .Select(static candidate => candidate.NodeIndex)
            .ToHashSet();
        var reachableCandidates = new HashSet<int>();
        if (candidateSet.Count == 0)
        {
            return reachableCandidates;
        }

        var visited = new HashSet<int> { goalNode };
        var pending = new Queue<int>();
        pending.Enqueue(goalNode);
        while (pending.Count > 0 && reachableCandidates.Count < candidateSet.Count)
        {
            var current = pending.Dequeue();
            var predecessors = _reverseAdjacency[current];
            for (var index = 0; index < predecessors.Count; index += 1)
            {
                var predecessor = predecessors[index];
                var fromNode = predecessor.FromNode;
                var edge = predecessor.Edge;
                if (playerClass.HasValue
                    && (!SupportsEdge(edge, playerClass.Value, team, carryingIntel)
                        || ShouldPreferCertifiedJump(fromNode, edge, playerClass.Value, team, carryingIntel)))
                {
                    continue;
                }

                if (blockedEdges.Contains(new NavEdgeBlock(fromNode, current, edge.Kind))
                    || !visited.Add(fromNode))
                {
                    continue;
                }

                if (candidateSet.Contains(fromNode))
                {
                    reachableCandidates.Add(fromNode);
                }

                pending.Enqueue(fromNode);
            }
        }

        return reachableCandidates;
    }

    private HashSet<int> FindReachableNodesFromGoal(
        int goalNode,
        IReadOnlyList<(int NodeIndex, float Score)> candidates,
        PlayerClass? playerClass,
        IReadOnlySet<NavEdgeBlock>? blockedEdges,
        PlayerTeam? team,
        bool carryingIntel)
    {
        var reachableCandidates = new HashSet<int>();
        if (candidates.Count == 0)
        {
            return reachableCandidates;
        }

        var candidateSet = candidates
            .Select(static candidate => candidate.NodeIndex)
            .ToHashSet();
        var distance = new float[_nodes.Length];
        Array.Fill(distance, float.MaxValue);
        var openSet = new PriorityQueue<int, float>();
        distance[goalNode] = 0f;
        openSet.Enqueue(goalNode, 0f);

        // Walk the immutable graph backwards from the goal. The old recovery
        // path tried up to 32 independent A* searches, which was the source of
        // multi-second frames when several bots landed below the same point.
        // One reverse Dijkstra pass answers reachability for every nearby
        // attachment candidate, preserving the existing nearest-candidate
        // selection without repeating the expensive search.
        while (openSet.Count > 0 && candidateSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (candidateSet.Remove(current))
            {
                reachableCandidates.Add(current);
                if (candidateSet.Count == 0)
                {
                    break;
                }
            }

            var predecessors = _reverseAdjacency[current];
            for (var index = 0; index < predecessors.Count; index += 1)
            {
                var predecessor = predecessors[index];
                var fromNode = predecessor.FromNode;
                var edge = predecessor.Edge;
                if (playerClass.HasValue
                    && !SupportsEdge(edge, playerClass.Value, team, carryingIntel))
                {
                    continue;
                }

                if (playerClass.HasValue
                    && ShouldPreferCertifiedJump(fromNode, edge, playerClass.Value, team, carryingIntel))
                {
                    continue;
                }

                if (blockedEdges is not null
                    && blockedEdges.Contains(new NavEdgeBlock(fromNode, current, edge.Kind)))
                {
                    continue;
                }

                var tentativeDistance = distance[current]
                    + ResolveTraversalCost(edge, fromNode, current, playerClass, carryingIntel, team);
                if (tentativeDistance >= distance[fromNode])
                {
                    continue;
                }

                distance[fromNode] = tentativeDistance;
                openSet.Enqueue(fromNode, tentativeDistance);
            }
        }

        return reachableCandidates;
    }

    public bool IsOnAcceptedCompletionSurface(float x, float y, NavEdgeCompletion completion)
    {
        if (completion.AllowsAirborneObjective || completion.AcceptedSurfaceIds.Length == 0)
        {
            return true;
        }

        // The graph node is recorded at the nominal collision position while
        // the live entity can settle a few fractional units lower on a very
        // narrow riser. Keep this tolerance bounded to the completion
        // handoff; it must not turn arbitrary nearby geometry into a valid
        // landing surface.
        var nodeIndex = FindNearestTraversalStartNodeInCompletionBand(
            x,
            y,
            maxAboveDistance: 16f,
            acceptedSurfaceIds: completion.AcceptedSurfaceIds);
        var nodeSurfaceId = nodeIndex >= 0 ? _nodes[nodeIndex].SurfaceId : null;
        if (!nodeSurfaceId.HasValue)
        {
            return false;
        }

        var surfaceId = nodeSurfaceId.Value;
        for (var i = 0; i < completion.AcceptedSurfaceIds.Length; i += 1)
        {
            if (completion.AcceptedSurfaceIds[i] == surfaceId)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsEdgeCompletionSatisfied(float x, float y, NavEdgeCompletion completion) =>
        x >= completion.MinX - RuntimeCompletionHorizontalSlack
        && x <= completion.MaxX + RuntimeCompletionHorizontalSlack
        && y >= completion.MinY - RuntimeCompletionVerticalSlack
        && y <= completion.MaxY + RuntimeCompletionVerticalSlack
        && IsOnAcceptedCompletionSurface(x, y, completion);

    private static (int[] All, Dictionary<int, int[]> BySurfaceId) BuildSurfaceNodeIndices(NavNode[] nodes)
    {
        var all = new List<int>();
        var bySurfaceId = new Dictionary<int, List<int>>();
        for (var i = 0; i < nodes.Length; i += 1)
        {
            var node = nodes[i];
            if (!node.SurfaceId.HasValue
                || node.Kind is NavNodeKind.Objective or NavNodeKind.Spawn)
            {
                continue;
            }

            all.Add(i);
            if (!bySurfaceId.TryGetValue(node.SurfaceId.Value, out var surfaceNodes))
            {
                surfaceNodes = new List<int>();
                bySurfaceId.Add(node.SurfaceId.Value, surfaceNodes);
            }

            surfaceNodes.Add(i);
        }

        var compactBySurfaceId = new Dictionary<int, int[]>(bySurfaceId.Count);
        foreach (var entry in bySurfaceId)
        {
            compactBySurfaceId.Add(entry.Key, [.. entry.Value]);
        }

        return ([.. all], compactBySurfaceId);
    }

    private static List<ReverseNavEdge>[] BuildReverseAdjacency(List<NavEdge>[] adjacency)
    {
        var reverse = new List<ReverseNavEdge>[adjacency.Length];
        for (var nodeIndex = 0; nodeIndex < reverse.Length; nodeIndex += 1)
        {
            reverse[nodeIndex] = [];
        }

        for (var fromNode = 0; fromNode < adjacency.Length; fromNode += 1)
        {
            var edges = adjacency[fromNode];
            for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex += 1)
            {
                var edge = edges[edgeIndex];
                if (edge.ToNode >= 0 && edge.ToNode < reverse.Length)
                {
                    reverse[edge.ToNode].Add(new ReverseNavEdge(fromNode, edge));
                }
            }
        }

        return reverse;
    }

    private static Dictionary<long, NavEdge[]> BuildCertifiedJumpAlternatives(List<NavEdge>[] adjacency)
    {
        var alternatives = new Dictionary<long, List<NavEdge>>();
        for (var fromNode = 0; fromNode < adjacency.Length; fromNode += 1)
        {
            var edges = adjacency[fromNode];
            for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex += 1)
            {
                var edge = edges[edgeIndex];
                if (!edge.IsOg2Contact || edge.Kind != NavEdgeKind.Jump)
                {
                    continue;
                }

                var key = ComposeTransitionKey(fromNode, edge.ToNode);
                if (!alternatives.TryGetValue(key, out var transitionAlternatives))
                {
                    transitionAlternatives = [];
                    alternatives.Add(key, transitionAlternatives);
                }

                transitionAlternatives.Add(edge);
            }
        }

        return alternatives.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value.ToArray());
    }

    private static long ComposeTransitionKey(int fromNode, int toNode) =>
        ((long)fromNode << 32) | (uint)toNode;

    private static int[] BuildObjectiveNodeIndices(NavNode[] nodes)
    {
        var objectiveNodes = new List<int>();
        for (var index = 0; index < nodes.Length; index += 1)
        {
            if (nodes[index].Kind == NavNodeKind.Objective)
            {
                objectiveNodes.Add(index);
            }
        }

        return [.. objectiveNodes];
    }

    private int FindNearestTraversalStartNodeInCompletionBand(
        float x,
        float y,
        float maxAboveDistance,
        IReadOnlyList<int>? acceptedSurfaceIds = null)
    {
        var bestIndex = -1;
        var bestScore = float.MaxValue;
        var candidateIndices = _surfaceNodeIndices;
        if (acceptedSurfaceIds is { Count: 1 }
            && _surfaceNodeIndicesById.TryGetValue(acceptedSurfaceIds[0], out var acceptedSurfaceNodeIndices))
        {
            // A single accepted surface is the common case. The per-surface
            // index retains the original node order while avoiding a scan of
            // unrelated geometry during every runtime contact probe tick.
            candidateIndices = acceptedSurfaceNodeIndices;
        }

        for (var candidateIndex = 0; candidateIndex < candidateIndices.Length; candidateIndex += 1)
        {
            var i = candidateIndices[candidateIndex];

            if (acceptedSurfaceIds is not null
                && !acceptedSurfaceIds.Contains(_nodes[i].SurfaceId.Value))
            {
                continue;
            }

            if (_nodes[i].Y < y - maxAboveDistance)
            {
                continue;
            }

            var dx = _nodes[i].X - x;
            var dy = _nodes[i].Y - y;
            var score = (dx * dx) + (dy * dy * 4f);
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    public int FindNearestReachableNode(
        float x,
        float y,
        int startNode,
        PlayerClass? playerClass = null,
        IReadOnlySet<NavEdgeBlock>? blockedEdges = null,
        PlayerTeam? team = null,
        bool carryingIntel = false,
        float verticalWeight = 2f,
        bool penalizeLowerCandidate = false)
    {
        if (startNode < 0 || startNode >= _nodes.Length)
        {
            return -1;
        }

        if (_isOg2Alpha && blockedEdges is null)
        {
            // Moving combat/support targets are resolved every think. In the
            // common case the globally best geometric candidate is reachable,
            // so verify that candidate with the existing class/team-filtered
            // path solver instead of allocating a full graph flood search and
            // then running A* again for the same target. This is exact when
            // the best candidate is reachable; the original search remains the
            // fallback for disconnected or class-filtered candidates.
            var bestCandidate = startNode;
            var bestCandidateScore = ScoreReachableGoalCandidate(
                startNode,
                x,
                y,
                verticalWeight,
                penalizeLowerCandidate);
            for (var nodeIndex = 0; nodeIndex < _nodes.Length; nodeIndex += 1)
            {
                var candidateScore = ScoreReachableGoalCandidate(
                    nodeIndex,
                    x,
                    y,
                    verticalWeight,
                    penalizeLowerCandidate);
                if (candidateScore < bestCandidateScore)
                {
                    bestCandidate = nodeIndex;
                    bestCandidateScore = candidateScore;
                }
            }

            if (bestCandidate == startNode
                || FindPath(
                    startNode,
                    bestCandidate,
                    playerClass,
                    team: team,
                    carryingIntel: carryingIntel) is not null)
            {
                return bestCandidate;
            }
        }

        var openSet = new PriorityQueue<int, float>();
        var gScore = new float[_nodes.Length];
        var closed = new bool[_nodes.Length];
        Array.Fill(gScore, float.MaxValue);

        gScore[startNode] = 0f;
        openSet.Enqueue(startNode, 0f);

        var bestIndex = startNode;
        var bestScore = ScoreReachableGoalCandidate(startNode, x, y, verticalWeight, penalizeLowerCandidate);

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (closed[current])
            {
                continue;
            }

            closed[current] = true;
            var candidateScore = ScoreReachableGoalCandidate(current, x, y, verticalWeight, penalizeLowerCandidate);
            if (candidateScore < bestScore)
            {
                bestScore = candidateScore;
                bestIndex = current;
            }

            var edges = GetEdges(current);
            for (var i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                if (playerClass.HasValue && !SupportsEdge(edge, playerClass.Value, team, carryingIntel))
                {
                    continue;
                }

                if (playerClass.HasValue
                    && ShouldPreferCertifiedJump(current, edge, playerClass.Value, team, carryingIntel))
                {
                    continue;
                }

                var neighbor = edge.ToNode;
                if (blockedEdges is not null && blockedEdges.Contains(new NavEdgeBlock(current, neighbor, edge.Kind)))
                {
                    continue;
                }

                if (!_isOg2Alpha && ShouldBlockSuspiciousVerticalRelayForExperiment(edge, current, neighbor))
                {
                    continue;
                }

                if (closed[neighbor])
                {
                    continue;
                }

                var tentativeG = gScore[current] + ResolveTraversalCost(edge, current, neighbor, playerClass, carryingIntel, team);
                if (tentativeG >= gScore[neighbor])
                {
                    continue;
                }

                gScore[neighbor] = tentativeG;
                openSet.Enqueue(neighbor, tentativeG);
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// Finds the nearest reachable objective anchor near a logical objective
    /// coordinate. Capture points may expose several assigned zone anchors;
    /// a class/team-filtered route can legitimately use any one of them, but
    /// must never degrade to a nearby walkable proxy outside the capture
    /// volume while an objective anchor is reachable.
    /// </summary>
    public int FindNearestReachableObjectiveNode(
        float x,
        float y,
        int startNode,
        PlayerClass? playerClass = null,
        IReadOnlySet<NavEdgeBlock>? blockedEdges = null,
        PlayerTeam? team = null,
        bool carryingIntel = false,
        float maxDistance = 96f)
    {
        if (startNode < 0 || startNode >= _nodes.Length)
        {
            return -1;
        }

        // Objective anchors are static for the lifetime of a graph. Recovery
        // can invalidate the current path, but it does not change which
        // objective anchor is reachable from a given attachment for a class
        // and traversal profile. Cache this resolution separately from the
        // full path so a missed contact does not rerun a graph flood search
        // before the cached A* path is consulted. Blocked-edge searches stay
        // uncached because their answer is intentionally transient.
        var cacheableResolution = _isOg2Alpha && blockedEdges is null;
        var cacheKey = cacheableResolution
            ? new AlphaObjectiveResolutionCacheKey(
                x,
                y,
                startNode,
                playerClass,
                team,
                carryingIntel,
                maxDistance)
            : default;
        if (cacheableResolution
            && _alphaObjectiveResolutionCache.TryGetValue(cacheKey, out var cachedObjectiveNode))
        {
            return cachedObjectiveNode;
        }

        var maxDistanceSquared = maxDistance * maxDistance;
        var candidates = new List<(int NodeIndex, float DistanceSquared)>();
        for (var nodeIndex = 0; nodeIndex < _nodes.Length; nodeIndex += 1)
        {
            if (_nodes[nodeIndex].Kind != NavNodeKind.Objective)
            {
                continue;
            }

            var dx = _nodes[nodeIndex].X - x;
            var dy = _nodes[nodeIndex].Y - y;
            var distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared <= maxDistanceSquared)
            {
                candidates.Add((nodeIndex, distanceSquared));
            }
        }

        var startCandidates = new List<(int NodeIndex, float DistanceSquared)>
        {
            (startNode, 0f),
        };
        var start = _nodes[startNode];
        for (var nodeIndex = 0; nodeIndex < _nodes.Length; nodeIndex += 1)
        {
            if (nodeIndex == startNode || !_nodes[nodeIndex].SurfaceId.HasValue)
            {
                continue;
            }

            var dx = _nodes[nodeIndex].X - start.X;
            var dy = _nodes[nodeIndex].Y - start.Y;
            if (MathF.Abs(dx) > 128f || MathF.Abs(dy) > 96f)
            {
                continue;
            }

            startCandidates.Add((nodeIndex, (dx * dx) + (dy * dy)));
        }

        var orderedCandidates = candidates
            .OrderBy(static candidate => candidate.DistanceSquared)
            .ToArray();
        var orderedStartCandidates = startCandidates
            .OrderBy(static candidate => candidate.DistanceSquared)
            .Take(12)
            .ToArray();
        var candidateNodeSet = orderedCandidates
            .Select(static candidate => candidate.NodeIndex)
            .ToHashSet();
        var reachableCandidateNodes = new HashSet<int>();

        if (cacheableResolution)
        {
            // Objective anchors are immutable for the lifetime of an alpha
            // graph. Cache reverse reachability per anchor/profile instead of
            // rerunning a forward flood from up to twelve nearby attachment
            // candidates on every recovery or spawn. This keeps the exact
            // directed/profile-filtered answer while making subsequent live
            // attachments a few set lookups.
            foreach (var candidate in orderedCandidates)
            {
                var reachableStartNodes = GetAlphaReachableNodesToObjective(
                    candidate.NodeIndex,
                    playerClass,
                    team,
                    carryingIntel);
                for (var startIndex = 0; startIndex < orderedStartCandidates.Length; startIndex += 1)
                {
                    if (!reachableStartNodes.Contains(orderedStartCandidates[startIndex].NodeIndex))
                    {
                        continue;
                    }

                    if (_alphaObjectiveResolutionCache.Count < 8_192)
                    {
                        _alphaObjectiveResolutionCache.TryAdd(cacheKey, candidate.NodeIndex);
                    }

                    return candidate.NodeIndex;
                }
            }

            if (_alphaObjectiveResolutionCache.Count < 8_192)
            {
                _alphaObjectiveResolutionCache.TryAdd(cacheKey, -1);
            }

            return -1;
        }

        // The old implementation ran one A* search for every objective/start
        // pair. A missed contact can leave several nearby supports eligible,
        // so that multiplied into hundreds of thousands of edge visits during
        // one bot think and was the source of the 500 ms Corinth freezes. A
        // single forward reachability pass answers all objective candidates
        // for an attachment start; only the final selected route needs A*.
        foreach (var startCandidate in orderedStartCandidates)
        {
            reachableCandidateNodes.UnionWith(FindReachableNodesFromStart(
                startCandidate.NodeIndex,
                candidateNodeSet,
                playerClass,
                blockedEdges,
                team,
                carryingIntel));
            if (reachableCandidateNodes.Count == candidateNodeSet.Count)
            {
                break;
            }
        }

        foreach (var candidate in orderedCandidates)
        {
            if (reachableCandidateNodes.Contains(candidate.NodeIndex))
            {
                if (cacheableResolution && _alphaObjectiveResolutionCache.Count < 8_192)
                {
                    _alphaObjectiveResolutionCache.TryAdd(cacheKey, candidate.NodeIndex);
                }

                return candidate.NodeIndex;
            }
        }

        if (cacheableResolution && _alphaObjectiveResolutionCache.Count < 8_192)
        {
            _alphaObjectiveResolutionCache.TryAdd(cacheKey, -1);
        }

        return -1;
    }

    private HashSet<int> GetAlphaBlockedReachableNodesToGoal(
        int goalNode,
        PlayerClass? playerClass,
        IReadOnlySet<NavEdgeBlock> blockedEdges,
        PlayerTeam? team,
        bool carryingIntel)
    {
        var cacheKey = new AlphaBlockedObjectiveReachabilityCacheKey(
            goalNode,
            playerClass,
            team,
            carryingIntel,
            ComputeBlockedEdgesFingerprint(blockedEdges));
        if (_alphaBlockedObjectiveReachabilityCache.TryGetValue(cacheKey, out var cachedReachableNodes))
        {
            return cachedReachableNodes;
        }

        var reachableNodes = FindAllReachableNodesFromGoal(
            goalNode,
            playerClass,
            blockedEdges,
            team,
            carryingIntel);
        if (_alphaBlockedObjectiveReachabilityCache.Count < 1_024)
        {
            _alphaBlockedObjectiveReachabilityCache.TryAdd(cacheKey, reachableNodes);
        }

        return reachableNodes;
    }

    private HashSet<int> FindAllReachableNodesFromGoal(
        int goalNode,
        PlayerClass? playerClass,
        IReadOnlySet<NavEdgeBlock> blockedEdges,
        PlayerTeam? team,
        bool carryingIntel)
    {
        var reachableNodes = new HashSet<int>();
        var distance = new float[_nodes.Length];
        Array.Fill(distance, float.MaxValue);
        var openSet = new PriorityQueue<int, float>();
        distance[goalNode] = 0f;
        openSet.Enqueue(goalNode, 0f);

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (!reachableNodes.Add(current))
            {
                continue;
            }

            var predecessors = _reverseAdjacency[current];
            for (var index = 0; index < predecessors.Count; index += 1)
            {
                var predecessor = predecessors[index];
                var fromNode = predecessor.FromNode;
                var edge = predecessor.Edge;
                if (playerClass.HasValue
                    && (!SupportsEdge(edge, playerClass.Value, team, carryingIntel)
                        || ShouldPreferCertifiedJump(fromNode, edge, playerClass.Value, team, carryingIntel)))
                {
                    continue;
                }

                if (blockedEdges.Contains(new NavEdgeBlock(fromNode, current, edge.Kind)))
                {
                    continue;
                }

                var tentativeDistance = distance[current]
                    + ResolveTraversalCost(edge, fromNode, current, playerClass, carryingIntel, team);
                if (tentativeDistance >= distance[fromNode])
                {
                    continue;
                }

                distance[fromNode] = tentativeDistance;
                openSet.Enqueue(fromNode, tentativeDistance);
            }
        }

        return reachableNodes;
    }

    private HashSet<int> GetAlphaReachableNodesToObjective(
        int goalNode,
        PlayerClass? playerClass,
        PlayerTeam? team,
        bool carryingIntel)
    {
        var traceReachability = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_REACH_TRACE") is "1" or "true" or "TRUE";
        var reachabilityStart = traceReachability ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
        var cacheKey = new AlphaObjectiveReachabilityCacheKey(
            goalNode,
            playerClass,
            team,
            carryingIntel);
        if (_alphaObjectiveReachabilityCache.TryGetValue(cacheKey, out var cachedReachableNodes))
        {
            return cachedReachableNodes;
        }

        var reachableNodes = new HashSet<int> { goalNode };
        var pending = new Queue<int>();
        pending.Enqueue(goalNode);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            var predecessors = _reverseAdjacency[current];
            for (var index = 0; index < predecessors.Count; index += 1)
            {
                var predecessor = predecessors[index];
                var fromNode = predecessor.FromNode;
                var edge = predecessor.Edge;
                if (playerClass.HasValue
                    && !SupportsEdge(edge, playerClass.Value, team, carryingIntel))
                {
                    continue;
                }

                if (playerClass.HasValue
                    && ShouldPreferCertifiedJump(fromNode, edge, playerClass.Value, team, carryingIntel))
                {
                    continue;
                }

                if (reachableNodes.Add(fromNode))
                {
                    pending.Enqueue(fromNode);
                }
            }
        }

        if (_alphaObjectiveReachabilityCache.Count < 8_192)
        {
            _alphaObjectiveReachabilityCache.TryAdd(cacheKey, reachableNodes);
        }

        if (traceReachability)
        {
            var elapsedMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - reachabilityStart)
                * 1000d
                / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds >= 20d)
            {
                Console.WriteLine(
                    $"[botbrain] alpha-reach slowMs:{elapsedMilliseconds:0.0} goal:{goalNode} " +
                    $"class:{playerClass?.ToString() ?? "any"} team:{team?.ToString() ?? "any"} " +
                    $"carry:{(carryingIntel ? 1 : 0)} nodes:{reachableNodes.Count}");
            }
        }

        return reachableNodes;
    }

    private HashSet<int> FindReachableNodesFromStart(
        int startNode,
        IReadOnlySet<int> candidateNodes,
        PlayerClass? playerClass,
        IReadOnlySet<NavEdgeBlock>? blockedEdges,
        PlayerTeam? team,
        bool carryingIntel)
    {
        var reachableCandidates = new HashSet<int>();
        if (startNode < 0 || startNode >= _nodes.Length || candidateNodes.Count == 0)
        {
            return reachableCandidates;
        }

        var distance = new float[_nodes.Length];
        var closed = new bool[_nodes.Length];
        Array.Fill(distance, float.MaxValue);
        var openSet = new PriorityQueue<int, float>();
        distance[startNode] = 0f;
        openSet.Enqueue(startNode, 0f);

        while (openSet.Count > 0 && reachableCandidates.Count < candidateNodes.Count)
        {
            var current = openSet.Dequeue();
            if (closed[current])
            {
                continue;
            }

            closed[current] = true;
            if (candidateNodes.Contains(current))
            {
                reachableCandidates.Add(current);
            }

            var edges = GetEdges(current);
            for (var index = 0; index < edges.Length; index += 1)
            {
                var edge = edges[index];
                if (playerClass.HasValue
                    && !SupportsEdge(edge, playerClass.Value, team, carryingIntel))
                {
                    continue;
                }

                if (playerClass.HasValue
                    && ShouldPreferCertifiedJump(current, edge, playerClass.Value, team, carryingIntel))
                {
                    continue;
                }

                var neighbor = edge.ToNode;
                if (blockedEdges is not null
                    && blockedEdges.Contains(new NavEdgeBlock(current, neighbor, edge.Kind)))
                {
                    continue;
                }

                if (closed[neighbor])
                {
                    continue;
                }

                var tentativeDistance = distance[current]
                    + ResolveTraversalCost(edge, current, neighbor, playerClass, carryingIntel, team);
                if (tentativeDistance >= distance[neighbor])
                {
                    continue;
                }

                distance[neighbor] = tentativeDistance;
                openSet.Enqueue(neighbor, tentativeDistance);
            }
        }

        return reachableCandidates;
    }

    /// <summary>
    /// A* shortest path from startNode to goalNode.
    /// Returns null if no path exists.
    /// </summary>
    public NavPath? FindPath(
        int startNode,
        int goalNode,
        PlayerClass? playerClass = null,
        IReadOnlySet<NavEdgeBlock>? blockedEdges = null,
        PlayerTeam? team = null,
        bool carryingIntel = false,
        double maxSearchMilliseconds = 0d,
        string? traceContext = null,
        int routeVariant = 0)
    {
        if (!_isOg2Alpha)
        {
            return FindPathCore(
                startNode,
                goalNode,
                playerClass,
                blockedEdges,
                team,
                carryingIntel,
                maxSearchMilliseconds,
                traceContext,
                routeVariant);
        }

        if (blockedEdges is { Count: > 0 })
        {
            // Failed-edge recovery can ask for the same dynamic route again
            // on consecutive thinks while the bot is still beside the same
            // obstruction. A blocked search is deterministic for an
            // immutable graph, so retain a small bounded cache for the
            // transient edge-set variant as well as the normal path cache.
            // This removes repeated 100ms+ A* work without weakening the
            // failed-edge exclusion or changing route selection.
            var blockedKey = new AlphaBlockedPathCacheKey(
                startNode,
                goalNode,
                playerClass,
                team,
                carryingIntel,
                ComputeBlockedEdgesFingerprint(blockedEdges),
                routeVariant);
            if (_alphaFailedBlockedPathCache.ContainsKey(blockedKey))
            {
                return null;
            }

            if (_alphaBlockedPathCache.TryGetValue(blockedKey, out var cachedBlockedPath))
            {
                return cachedBlockedPath.Clone();
            }

            var blockedPath = FindPathCore(
                startNode,
                goalNode,
                playerClass,
                blockedEdges,
                team,
                carryingIntel,
                maxSearchMilliseconds,
                traceContext,
                routeVariant);
            if (blockedPath is not null && _alphaBlockedPathCache.Count < 8_192)
            {
                _alphaBlockedPathCache.TryAdd(blockedKey, blockedPath.Clone());
            }
            else if (blockedPath is null
                && maxSearchMilliseconds <= 0d
                && _alphaFailedBlockedPathCache.Count < 8_192)
            {
                _alphaFailedBlockedPathCache.TryAdd(blockedKey, 0);
            }

            return blockedPath;
        }

        var key = new NavPathCacheKey(startNode, goalNode, playerClass, team, carryingIntel, routeVariant);
        if (_alphaFailedPathCache.ContainsKey(key))
        {
            return null;
        }

        if (_alphaPathCache.TryGetValue(key, out var cachedPath))
        {
            return cachedPath.Clone();
        }

        var path = FindPathCore(
            startNode,
            goalNode,
            playerClass,
            blockedEdges,
            team,
            carryingIntel,
            maxSearchMilliseconds,
            traceContext,
            routeVariant);
        if (path is not null && _alphaPathCache.Count < 8_192)
        {
            _alphaPathCache.TryAdd(key, path.Clone());
        }
        else if (path is null
            && maxSearchMilliseconds <= 0d
            && _alphaFailedPathCache.Count < 8_192)
        {
            _alphaFailedPathCache.TryAdd(key, 0);
        }

        return path;
    }

    private static int ComputeBlockedEdgesFingerprint(IReadOnlySet<NavEdgeBlock> blockedEdges)
    {
        var hash = new HashCode();
        hash.Add(blockedEdges.Count);
        foreach (var edge in blockedEdges
                     .OrderBy(static edge => edge.FromNode)
                     .ThenBy(static edge => edge.ToNode)
                     .ThenBy(static edge => edge.Kind))
        {
            hash.Add(edge.FromNode);
            hash.Add(edge.ToNode);
            hash.Add(edge.Kind);
        }

        return hash.ToHashCode();
    }

    private NavPath? FindPathCore(
        int startNode,
        int goalNode,
        PlayerClass? playerClass = null,
        IReadOnlySet<NavEdgeBlock>? blockedEdges = null,
        PlayerTeam? team = null,
        bool carryingIntel = false,
        double maxSearchMilliseconds = 0d,
        string? traceContext = null,
        int routeVariant = 0)
    {
        var traceSearch = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_PATH_TRACE") is "1" or "true" or "TRUE";
        var traceStart = traceSearch || maxSearchMilliseconds > 0d
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        var searchDeadline = maxSearchMilliseconds > 0d && double.IsFinite(maxSearchMilliseconds)
            ? traceStart + (long)(System.Diagnostics.Stopwatch.Frequency * maxSearchMilliseconds / 1000d)
            : 0L;
        var expandedNodes = 0;
        var consideredEdges = 0;
        if (startNode < 0 || startNode >= _nodes.Length || goalNode < 0 || goalNode >= _nodes.Length)
        {
            return null;
        }

        if (startNode == goalNode)
        {
            return new NavPath([startNode], 0f);
        }

        var workspace = _pathSearchWorkspaces.Value;
        if (workspace is null)
        {
            workspace = new NavPathSearchWorkspace(_nodes.Length);
            _pathSearchWorkspaces.Value = workspace;
        }

        workspace.Reset();
        var openSet = workspace.OpenSet;
        var cameFrom = workspace.CameFrom;
        var edgeFrom = workspace.EdgeFrom;
        var gScore = workspace.GScore;
        var closed = workspace.Closed;
        var filteredProfile = _isOg2Alpha && playerClass.HasValue
            ? new AlphaSearchProfileKey(playerClass.Value, team, carryingIntel)
            : (AlphaSearchProfileKey?)null;

        gScore[startNode] = 0f;
        openSet.Enqueue(startNode, Heuristic(startNode, goalNode));

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (searchDeadline > 0L
                && System.Diagnostics.Stopwatch.GetTimestamp() >= searchDeadline)
            {
                TracePathSearch(
                    null,
                    traceSearch,
                    traceStart,
                    expandedNodes,
                    consideredEdges,
                    startNode,
                    goalNode,
                    playerClass,
                    team,
                    carryingIntel,
                    traceContext,
                    searchAborted: true);
                return null;
            }

            if (current == goalNode)
            {
                var result = ReconstructPath(cameFrom, edgeFrom, current, gScore[current]);
                TracePathSearch(
                    result,
                    traceSearch,
                    traceStart,
                    expandedNodes,
                    consideredEdges,
                    startNode,
                    goalNode,
                    playerClass,
                    team,
                    carryingIntel,
                    traceContext);
                return result;
            }

            if (closed[current])
            {
                continue;
            }

            closed[current] = true;
            expandedNodes += 1;

            // Dynamic target routes run on the simulation thread. Checking
            // only every 32 expansions lets a supposedly bounded search run
            // far past its budget on dense OG2 graphs, because one expansion
            // can enumerate a large filtered edge set. Keep the check cheap
            // but frequent enough that the runtime budget is an actual upper
            // bound rather than a best-effort hint.
            if (searchDeadline > 0L
                && (expandedNodes & 7) == 0
                && System.Diagnostics.Stopwatch.GetTimestamp() >= searchDeadline)
            {
                TracePathSearch(
                    null,
                    traceSearch,
                    traceStart,
                    expandedNodes,
                    consideredEdges,
                    startNode,
                    goalNode,
                    playerClass,
                    team,
                    carryingIntel,
                    traceContext,
                    searchAborted: true);
                return null;
            }

            var profileSearchAborted = false;
            ReadOnlySpan<NavEdge> edges;
            if (filteredProfile is null)
            {
                edges = GetEdges(current);
            }
            else
            {
                edges = GetAlphaSearchEdges(
                    filteredProfile.Value,
                    current,
                    searchDeadline,
                    out profileSearchAborted);
            }
            if (profileSearchAborted)
            {
                TracePathSearch(
                    null,
                    traceSearch,
                    traceStart,
                    expandedNodes,
                    consideredEdges,
                    startNode,
                    goalNode,
                    playerClass,
                    team,
                    carryingIntel,
                    traceContext,
                    searchAborted: true);
                return null;
            }

            for (var i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                consideredEdges += 1;
                if (searchDeadline > 0L
                    && (consideredEdges & 31) == 0
                    && System.Diagnostics.Stopwatch.GetTimestamp() >= searchDeadline)
                {
                    TracePathSearch(
                        null,
                        traceSearch,
                        traceStart,
                        expandedNodes,
                        consideredEdges,
                        startNode,
                        goalNode,
                        playerClass,
                        team,
                        carryingIntel,
                        traceContext,
                        searchAborted: true);
                    return null;
                }

                if (filteredProfile is null
                    && playerClass.HasValue
                    && (!SupportsEdge(edge, playerClass.Value, team, carryingIntel)
                        || ShouldPreferCertifiedJump(current, edge, playerClass.Value, team, carryingIntel)))
                {
                    continue;
                }

                var neighbor = edge.ToNode;
                if (blockedEdges is not null && blockedEdges.Contains(new NavEdgeBlock(current, neighbor, edge.Kind)))
                {
                    continue;
                }

                if (!_isOg2Alpha && ShouldBlockSuspiciousVerticalRelayForExperiment(edge, current, neighbor))
                {
                    continue;
                }

                if (closed[neighbor])
                {
                    continue;
                }

                var tentativeG = gScore[current]
                    + ResolveTraversalCost(edge, current, neighbor, playerClass, carryingIntel, team, routeVariant);
                if (tentativeG >= gScore[neighbor])
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                edgeFrom[neighbor] = edge;
                gScore[neighbor] = tentativeG;
                openSet.Enqueue(neighbor, tentativeG + Heuristic(neighbor, goalNode));
            }
        }

        TracePathSearch(
            null,
            traceSearch,
            traceStart,
            expandedNodes,
            consideredEdges,
            startNode,
            goalNode,
            playerClass,
            team,
            carryingIntel,
            traceContext);
        return null;
    }

    private NavEdge[] GetAlphaSearchEdges(
        AlphaSearchProfileKey profile,
        int nodeIndex,
        long searchDeadline,
        out bool searchAborted)
    {
        searchAborted = false;
        var profileCache = _alphaSearchEdgesCache.GetOrAdd(
            profile,
            static _ => new ConcurrentDictionary<int, NavEdge[]>());
        if (profileCache.TryGetValue(nodeIndex, out var cachedEdges))
        {
            return cachedEdges;
        }

        var source = _adjacency[nodeIndex];
        var compatible = new List<NavEdge>(source.Count);
        for (var edgeIndex = 0; edgeIndex < source.Count; edgeIndex += 1)
        {
            if (searchDeadline > 0L
                && (edgeIndex & 31) == 0
                && System.Diagnostics.Stopwatch.GetTimestamp() >= searchDeadline)
            {
                searchAborted = true;
                return [];
            }

            var edge = source[edgeIndex];
            if (!SupportsEdge(edge, profile.PlayerClass, profile.Team, profile.CarryingIntel)
                || ShouldPreferCertifiedJump(
                    nodeIndex,
                    edge,
                    profile.PlayerClass,
                    profile.Team,
                    profile.CarryingIntel))
            {
                continue;
            }

            compatible.Add(edge);
        }

        var result = compatible.ToArray();
        profileCache.TryAdd(nodeIndex, result);
        return result;
    }

    private static void TracePathSearch(
        NavPath? path,
        bool traceSearch,
        long traceStart,
        int expandedNodes,
        int consideredEdges,
        int startNode,
        int goalNode,
        PlayerClass? playerClass,
        PlayerTeam? team,
        bool carryingIntel,
        string? traceContext,
        bool searchAborted = false)
    {
        if (!traceSearch)
        {
            return;
        }

        var elapsedMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - traceStart)
            * 1000d
            / System.Diagnostics.Stopwatch.Frequency;
        if (elapsedMilliseconds < 20d)
        {
            return;
        }

        Console.WriteLine(
            $"[botbrain] alpha-path slowMs:{elapsedMilliseconds:0.0} " +
            $"nodes:{expandedNodes} edges:{consideredEdges} result:{path?.Count ?? 0} " +
            $"start:{startNode} goal:{goalNode} class:{playerClass?.ToString() ?? "any"} " +
            $"team:{team?.ToString() ?? "any"} carry:{(carryingIntel ? 1 : 0)} " +
            $"aborted:{(searchAborted ? 1 : 0)} context:{traceContext ?? "unknown"}");
    }

    private readonly record struct AlphaSearchProfileKey(
        PlayerClass PlayerClass,
        PlayerTeam? Team,
        bool CarryingIntel);

    private float Heuristic(int fromNode, int toNode)
    {
        var dx = _nodes[toNode].X - _nodes[fromNode].X;
        var dy = _nodes[toNode].Y - _nodes[fromNode].Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private bool SupportsEdge(
        NavEdge edge,
        PlayerClass playerClass,
        PlayerTeam? team,
        bool carryingIntel)
    {
        if (!_isOg2Alpha)
        {
            return edge.Supports(playerClass, team, carryingIntel);
        }

        // Alpha contacts are certified against the conservative Heavy envelope
        // and the separate Scout air-jump envelope. Use the same certified
        // movement-profile substitution as graph generation; jump contacts are
        // still re-proved from the live class before steering, so this broadens
        // topology eligibility without reusing another class's physics recipe.
        return BotBrainClassMask.Contains(edge.SupportedClassMask, playerClass)
            && (!team.HasValue || BotBrainTeamMask.Contains(edge.SupportedTeamMask, team.Value))
            && (!edge.RequiresCarryingIntel || carryingIntel)
            && (!edge.CarryingIntelRequirement.HasValue
                || edge.CarryingIntelRequirement.Value == carryingIntel);
    }

    private bool ShouldPreferCertifiedJump(
        int fromNode,
        NavEdge edge,
        PlayerClass playerClass,
        PlayerTeam? team,
        bool carryingIntel)
    {
        if (!_isOg2Alpha
            || !edge.IsOg2Contact
            || edge.Kind is not (NavEdgeKind.Walk or NavEdgeKind.Fall))
        {
            return false;
        }

        // A measured alpha Walk is already a grounded traversal proof for a
        // flat corridor. Some maps also expose a nominal Walk between the
        // bottom and top of a long stair relay; that link reaches the lower
        // landing but cannot climb the vertical gap without the paired Jump
        // contact. Keep the certified Jump only for those materially vertical
        // relays, so ordinary routes remain grounded and directional.
        if (edge.Kind == NavEdgeKind.Walk
            && MathF.Abs(_nodes[edge.ToNode].Y - _nodes[fromNode].Y) <= 48f)
        {
            return false;
        }

        // A contact-first sweep can observe a nominal walk/fall landing that
        // is valid from a clean probe but fragile when composed after a prior
        // jump. If the same directed transition has a class/team/carrying
        // compatible certified jump, choose that explicit recipe instead of
        // the less constrained landing.
        if (!_certifiedJumpAlternatives.TryGetValue(ComposeTransitionKey(fromNode, edge.ToNode), out var alternatives))
        {
            return false;
        }

        for (var index = 0; index < alternatives.Length; index += 1)
        {
            if (SupportsEdge(alternatives[index], playerClass, team, carryingIntel))
            {
                return true;
            }
        }

        return false;
    }

    private float ResolveTraversalCost(
        NavEdge edge,
        int fromNodeIndex,
        int toNodeIndex,
        PlayerClass? playerClass,
        bool carryingIntel,
        PlayerTeam? team,
        int routeVariant = 0)
    {
        if (_isOg2Alpha)
        {
            var alphaCost = MathF.Max(1f, edge.Cost);
            alphaCost += edge.Kind switch
            {
                NavEdgeKind.Jump => 36f,
                NavEdgeKind.Fall => 28f,
                NavEdgeKind.Dropdown => 18f,
                _ => 0f,
            };

            // A contact sweep can record a composite walk-off/air-jump as a
            // single Jump edge. Its launch window is materially below the
            // source node, so it is more sensitive to live handoff momentum
            // than an ordinary jump from the source surface. Prefer the
            // explicit fall/relay chain when one exists by making this
            // shortcut a route cost, not a topology requirement. The edge
            // remains available for maps where no safer relay exists.
            if (edge.Kind == NavEdgeKind.Jump
                && edge.LaunchRecipe.HasRecipe
                && edge.LaunchRecipe.LaunchMinY > _nodes[fromNodeIndex].Y + 24f)
            {
                alphaCost += 180f;
            }
            if (edge.RequiresGroundedContinuation)
            {
                alphaCost += 24f;
            }

            if (carryingIntel && edge.Kind is NavEdgeKind.Jump or NavEdgeKind.Fall)
            {
                alphaCost += 20f;
            }

            // The graph remains class/team agnostic, but a full roster should
            // not deterministically queue every bot through the same equal-
            // cost contact chain. A small stable per-bot tie-break spreads
            // otherwise equivalent routes without changing the topology or
            // making a materially longer route preferable.
            alphaCost += ResolveRouteVariantTieCost(
                fromNodeIndex,
                toNodeIndex,
                edge.Kind,
                routeVariant);

            return alphaCost;
        }

        var fromNode = _nodes[fromNodeIndex];
        var toNode = _nodes[toNodeIndex];
        var cost = MathF.Max(1f, edge.Cost);
        var verticalDelta = MathF.Abs(toNode.Y - fromNode.Y);
        var horizontalDelta = MathF.Abs(toNode.X - fromNode.X);
        var euclideanDistance = MathF.Sqrt((horizontalDelta * horizontalDelta) + (verticalDelta * verticalDelta));
        var isSuspiciousVerticalRelay = IsSuspiciousVerticalRelay(edge, cost, verticalDelta, horizontalDelta, euclideanDistance);
        if (ShouldReturnRawTraversalCost(edge, verticalDelta, isSuspiciousVerticalRelay, playerClass, carryingIntel, team))
        {
            return cost + ResolveCarrierSpawnAdjacencyPenalty(fromNodeIndex, toNodeIndex, carryingIntel, fromNode, toNode, team);
        }

        var difficultyPenalty = edge.Kind switch
        {
            NavEdgeKind.Jump => 36f,
            NavEdgeKind.Fall => 28f,
            NavEdgeKind.Dropdown => 18f,
            _ => 0f,
        };

        if (verticalDelta > 48f)
        {
            difficultyPenalty += MathF.Min(96f, (verticalDelta - 48f) * 0.45f);
        }

        if (isSuspiciousVerticalRelay)
        {
            var stableRelayFloor = euclideanDistance * SuspiciousRelayCostFloorMultiplier;
            if (cost < stableRelayFloor)
            {
                difficultyPenalty += MathF.Min(260f, (stableRelayFloor - cost) * 1.35f);
            }
        }

        var hasCertifiedProof = edge.ProbeTicks > 0 || edge.ProbeVariantAttempts > 0;
        if (!hasCertifiedProof)
        {
            difficultyPenalty += edge.Kind switch
            {
                NavEdgeKind.Jump => 150f,
                NavEdgeKind.Fall => 120f,
                NavEdgeKind.Dropdown => 80f,
                NavEdgeKind.Walk when isSuspiciousVerticalRelay => 110f,
                _ => 0f,
            };

            if (edge.Kind == NavEdgeKind.Jump && toNode.Y < fromNode.Y - 48f)
            {
                difficultyPenalty += 1_200f;
            }
        }
        else
        {
            if (!edge.Completion.HasWindow)
            {
                difficultyPenalty += 45f;
            }

            if (edge.ProbeTicks > 0)
            {
                difficultyPenalty += MathF.Min(90f, edge.ProbeTicks * 0.35f);
            }

            if (edge.ProbeVariantAttempts > 0)
            {
                var successRate = edge.ProbeVariantSuccesses / (float)edge.ProbeVariantAttempts;
                difficultyPenalty += MathF.Max(0f, 1f - successRate) * 120f;
            }
        }

        if (edge.RequiresGroundedContinuation)
        {
            difficultyPenalty += 24f;
        }

        if (carryingIntel && (edge.Kind is NavEdgeKind.Jump or NavEdgeKind.Fall || isSuspiciousVerticalRelay))
        {
            difficultyPenalty += 20f;
        }

        return cost
            + difficultyPenalty
            + ResolveCarrierSpawnAdjacencyPenalty(fromNodeIndex, toNodeIndex, carryingIntel, fromNode, toNode, team)
            + ResolveMapSpecificTraversalPenalty(edge, fromNode, toNode, playerClass, carryingIntel, team);
    }

    private static float ResolveRouteVariantTieCost(
        int fromNodeIndex,
        int toNodeIndex,
        NavEdgeKind edgeKind,
        int routeVariant)
    {
        if (routeVariant == 0)
        {
            return 0f;
        }

        unchecked
        {
            var hash = (uint)routeVariant * 2_654_435_761u;
            hash ^= (uint)fromNodeIndex * 2_246_822_519u;
            hash ^= (uint)toNodeIndex * 3_266_489_917u;
            hash ^= (uint)edgeKind * 374_761_393u;
            return (hash & 0x0Fu) * 0.35f;
        }
    }

    private bool ShouldBlockSuspiciousVerticalRelayForExperiment(NavEdge edge, int fromNodeIndex, int toNodeIndex)
    {
        if (!IsSuspiciousVerticalRelayBlockEnabled())
        {
            return false;
        }

        var fromNode = _nodes[fromNodeIndex];
        var toNode = _nodes[toNodeIndex];
        var cost = MathF.Max(1f, edge.Cost);
        var verticalDelta = MathF.Abs(toNode.Y - fromNode.Y);
        var horizontalDelta = MathF.Abs(toNode.X - fromNode.X);
        var euclideanDistance = MathF.Sqrt((horizontalDelta * horizontalDelta) + (verticalDelta * verticalDelta));
        return IsSuspiciousVerticalRelay(edge, cost, verticalDelta, horizontalDelta, euclideanDistance);
    }

    private static bool IsSuspiciousVerticalRelayBlockEnabled() =>
        Environment.GetEnvironmentVariable("BOTBRAIN_BLOCK_SUSPICIOUS_VERTICAL_RELAYS") is "1" or "true" or "TRUE";

    private bool ShouldReturnRawTraversalCost(
        NavEdge edge,
        float verticalDelta,
        bool isSuspiciousVerticalRelay,
        PlayerClass? playerClass,
        bool carryingIntel,
        PlayerTeam? team)
    {
        if (edge.Kind == NavEdgeKind.Walk
            && verticalDelta <= SignificantWalkVerticalDelta)
        {
            return true;
        }

        return ShouldUseRawTraversalCost(playerClass, carryingIntel, team)
            && !isSuspiciousVerticalRelay;
    }

    private static bool IsSuspiciousVerticalRelay(
        NavEdge edge,
        float cost,
        float verticalDelta,
        float horizontalDelta,
        float euclideanDistance)
    {
        if (edge.Kind is not (NavEdgeKind.Walk or NavEdgeKind.Jump or NavEdgeKind.Fall)
            || verticalDelta < SuspiciousRelayVerticalDelta
            || horizontalDelta > SuspiciousRelayHorizontalReach)
        {
            return false;
        }

        return cost < euclideanDistance * SuspiciousRelayCostFloorMultiplier;
    }

    private float ResolveCarrierSpawnAdjacencyPenalty(
        int fromNodeIndex,
        int toNodeIndex,
        bool carryingIntel,
        NavNode fromNode,
        NavNode toNode,
        PlayerTeam? team)
    {
        if (!carryingIntel
            || _mode != GameModeKind.CaptureTheFlag
            || !ShouldApplyCarrierSpawnAdjacencyPenalty()
            || fromNode.Kind == NavNodeKind.Objective
            || toNode.Kind == NavNodeKind.Objective)
        {
            return 0f;
        }

        return IsCarrierBlockedSpawnAdjacentNode(fromNodeIndex, team)
            || IsCarrierBlockedSpawnAdjacentNode(toNodeIndex, team)
            ? 3_000f
            : 0f;
    }

    private bool ShouldApplyCarrierSpawnAdjacencyPenalty()
    {
        return string.Equals(_levelName, "Conflict", StringComparison.OrdinalIgnoreCase)
            || string.Equals(_levelName, "Eiger", StringComparison.OrdinalIgnoreCase)
            || string.Equals(_levelName, "Waterway", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsCarrierBlockedSpawnAdjacentNode(int nodeIndex, PlayerTeam? team)
    {
        var mask = _spawnAdjacentTeamMasks[nodeIndex];
        if (mask == 0)
        {
            return false;
        }

        if (!ShouldUseTeamAwareCarrierSpawnAdjacencyPenalty() || !team.HasValue)
        {
            return true;
        }

        return BotBrainTeamMask.Contains(mask, GetOpposingTeam(team.Value));
    }

    private bool ShouldUseTeamAwareCarrierSpawnAdjacencyPenalty() =>
        string.Equals(_levelName, "Waterway", StringComparison.OrdinalIgnoreCase);

    private static int[] ResolveSpawnAdjacentTeamMasks(NavNode[] nodes, IReadOnlyList<NavSpawnAnchor>? spawnAnchors)
    {
        const float spawnHorizontalRange = 170f;
        const float spawnVerticalRange = 80f;
        var spawnAdjacentTeamMasks = new int[nodes.Length];
        if (spawnAnchors is null || spawnAnchors.Count == 0)
        {
            spawnAnchors = ResolveLegacySpawnAnchors(nodes);
        }

        for (var i = 0; i < nodes.Length; i += 1)
        {
            if (nodes[i].Kind is NavNodeKind.Spawn or NavNodeKind.Objective)
            {
                continue;
            }

            for (var j = 0; j < spawnAnchors.Count; j += 1)
            {
                var spawn = spawnAnchors[j];
                if (MathF.Abs(nodes[i].X - spawn.X) <= spawnHorizontalRange
                    && MathF.Abs(nodes[i].Y - spawn.Y) <= spawnVerticalRange)
                {
                    spawnAdjacentTeamMasks[i] |= BotBrainTeamMask.For(spawn.Team);
                }
            }
        }

        return spawnAdjacentTeamMasks;
    }

    private static NavSpawnAnchor[] ResolveLegacySpawnAnchors(NavNode[] nodes)
    {
        var anchors = new List<NavSpawnAnchor>();
        foreach (var node in nodes)
        {
            if (node.Kind == NavNodeKind.Spawn)
            {
                anchors.Add(new NavSpawnAnchor(node.X, node.Y, PlayerTeam.Red));
                anchors.Add(new NavSpawnAnchor(node.X, node.Y, PlayerTeam.Blue));
            }
        }

        return anchors.ToArray();
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team) =>
        team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;

    private bool ShouldUseRawTraversalCost(PlayerClass? playerClass, bool carryingIntel, PlayerTeam? team)
    {
        return _mode == GameModeKind.CaptureTheFlag
            && string.Equals(_levelName, "Orange", StringComparison.OrdinalIgnoreCase)
            && carryingIntel;
    }

    private float ResolveMapSpecificTraversalPenalty(
        NavEdge edge,
        NavNode fromNode,
        NavNode toNode,
        PlayerClass? playerClass,
        bool carryingIntel,
        PlayerTeam? team)
    {
        if (_mode != GameModeKind.CaptureTheFlag)
        {
            return 0f;
        }

        if (edge.Kind == NavEdgeKind.Walk
            || playerClass == PlayerClass.Scout
            || !team.HasValue
            || !string.Equals(_levelName, "Truefort", StringComparison.OrdinalIgnoreCase))
        {
            return 0f;
        }

        if (!carryingIntel)
        {
            return 0f;
        }

        if (team.Value == PlayerTeam.Blue && IsTruefortBlueReturnChurnEdge(edge, fromNode, toNode))
        {
            return 1_600f;
        }

        return team.Value == PlayerTeam.Red && IsTruefortRedReturnChurnEdge(edge, fromNode, toNode)
            ? 1_600f
            : 0f;
    }

    private static bool IsTruefortBlueReturnChurnEdge(NavEdge edge, NavNode fromNode, NavNode toNode)
    {
        if (!IsInBox(fromNode, minX: 680f, maxX: 1_160f, minY: 430f, maxY: 920f)
            || !IsInBox(toNode, minX: 680f, maxX: 1_160f, minY: 430f, maxY: 920f))
        {
            return false;
        }

        if (IsKnownTruefortBlueChurnEdge(edge, fromNode, toNode))
        {
            return true;
        }

        return edge.Kind is NavEdgeKind.Jump or NavEdgeKind.Fall
            && MathF.Abs(toNode.Y - fromNode.Y) >= 48f
            && MathF.Abs(toNode.X - fromNode.X) <= 180f;
    }

    private static bool IsTruefortRedReturnChurnEdge(NavEdge edge, NavNode fromNode, NavNode toNode)
    {
        if (!IsInBox(fromNode, minX: 4_140f, maxX: 4_680f, minY: 430f, maxY: 920f)
            || !IsInBox(toNode, minX: 4_140f, maxX: 4_680f, minY: 430f, maxY: 920f))
        {
            return false;
        }

        if (IsKnownTruefortRedChurnEdge(edge, fromNode, toNode))
        {
            return true;
        }

        return edge.Kind is NavEdgeKind.Jump or NavEdgeKind.Fall
            && MathF.Abs(toNode.Y - fromNode.Y) >= 48f
            && MathF.Abs(toNode.X - fromNode.X) <= 180f;
    }

    private static bool IsKnownTruefortBlueChurnEdge(NavEdge edge, NavNode fromNode, NavNode toNode)
    {
        return edge.Kind is NavEdgeKind.Jump or NavEdgeKind.Fall
            && ((IsNear(fromNode, 820f, 888f) && IsNear(toNode, 860f, 760f))
                || (IsNear(fromNode, 1_075f, 640f) && IsNear(toNode, 1_105f, 760f))
                || (IsNear(fromNode, 1_115f, 760f) && IsNear(toNode, 1_105f, 888f))
                || (IsNear(fromNode, 735f, 504f) && IsNear(toNode, 735f, 632f))
                || (IsNear(fromNode, 1_150f, 504f) && IsNear(toNode, 1_105f, 632f))
                || (IsNear(fromNode, 950f, 760f) && IsNear(toNode, 950f, 632f)));
    }

    private static bool IsKnownTruefortRedChurnEdge(NavEdge edge, NavNode fromNode, NavNode toNode)
    {
        return edge.Kind is NavEdgeKind.Jump or NavEdgeKind.Fall
            && ((IsNear(fromNode, 4_575f, 888f) && IsNear(toNode, 4_540f, 760f))
                || (IsNear(fromNode, 4_325f, 640f) && IsNear(toNode, 4_290f, 760f))
                || (IsNear(fromNode, 4_290f, 760f) && IsNear(toNode, 4_290f, 888f))
                || (IsNear(fromNode, 4_660f, 504f) && IsNear(toNode, 4_660f, 632f))
                || (IsNear(fromNode, 4_245f, 504f) && IsNear(toNode, 4_290f, 632f))
                || (IsNear(fromNode, 4_450f, 760f) && IsNear(toNode, 4_450f, 632f)));
    }

    private static bool IsInBox(NavNode node, float minX, float maxX, float minY, float maxY) =>
        node.X >= minX && node.X <= maxX && node.Y >= minY && node.Y <= maxY;

    private static bool IsNear(NavNode node, float x, float y) =>
        MathF.Abs(node.X - x) <= 56f && MathF.Abs(node.Y - y) <= 56f;

    private float ScoreReachableGoalCandidate(int nodeIndex, float x, float y, float verticalWeight, bool penalizeLowerCandidate)
    {
        var node = _nodes[nodeIndex];
        var dx = node.X - x;
        var dy = node.Y - y;
        var kindPenalty = node.Kind == NavNodeKind.Spawn ? 10_000f : 0f;
        var lowerPenalty = penalizeLowerCandidate && dy > 36f
            ? dy * dy * 6f
            : 0f;
        return (dx * dx) + (dy * dy * MathF.Max(1f, verticalWeight)) + lowerPenalty + kindPenalty;
    }

    private static NavPath ReconstructPath(int[] cameFrom, NavEdge[] edgeFrom, int current, float totalCost)
    {
        var reverseWaypoints = new List<int>();
        var reverseIncomingEdges = new List<NavEdge>();
        while (cameFrom[current] >= 0)
        {
            reverseWaypoints.Add(current);
            reverseIncomingEdges.Add(edgeFrom[current]);
            current = cameFrom[current];
        }

        reverseWaypoints.Add(current);
        reverseWaypoints.Reverse();
        reverseIncomingEdges.Reverse();

        var waypoints = reverseWaypoints.ToArray();
        var incomingEdges = new NavEdge[waypoints.Length];
        for (var i = 1; i < incomingEdges.Length; i += 1)
        {
            incomingEdges[i] = reverseIncomingEdges[i - 1];
        }

        return new NavPath(waypoints, incomingEdges, totalCost);
    }

    private readonly record struct ReverseNavEdge(int FromNode, NavEdge Edge);
}

internal readonly record struct NavPathCacheKey(
    int StartNode,
    int GoalNode,
    PlayerClass? PlayerClass,
    PlayerTeam? Team,
    bool CarryingIntel,
    int RouteVariant);

internal readonly record struct AlphaBlockedPathCacheKey(
    int StartNode,
    int GoalNode,
    PlayerClass? PlayerClass,
    PlayerTeam? Team,
    bool CarryingIntel,
    int BlockedEdgesFingerprint,
    int RouteVariant);

internal readonly record struct AlphaObjectiveResolutionCacheKey(
    float X,
    float Y,
    int StartNode,
    PlayerClass? PlayerClass,
    PlayerTeam? Team,
    bool CarryingIntel,
    float MaxDistance);

internal readonly record struct AlphaBlockedObjectiveReachabilityCacheKey(
    int GoalNode,
    PlayerClass? PlayerClass,
    PlayerTeam? Team,
    bool CarryingIntel,
    int BlockedEdgesFingerprint);

internal readonly record struct AlphaObjectiveReachabilityCacheKey(
    int GoalNode,
    PlayerClass? PlayerClass,
    PlayerTeam? Team,
    bool CarryingIntel);

/// <summary>
/// Per-worker scratch storage for A*. Bot brains may search concurrently, so
/// this is thread-local rather than shared. Reusing it avoids allocating the
/// node-sized arrays and priority queue on every recovery or dynamic route.
/// </summary>
internal sealed class NavPathSearchWorkspace
{
    public NavPathSearchWorkspace(int nodeCount)
    {
        CameFrom = new int[nodeCount];
        EdgeFrom = new NavEdge[nodeCount];
        GScore = new float[nodeCount];
        Closed = new bool[nodeCount];
        OpenSet = new PriorityQueue<int, float>();
    }

    public int[] CameFrom { get; }

    public NavEdge[] EdgeFrom { get; }

    public float[] GScore { get; }

    public bool[] Closed { get; }

    public PriorityQueue<int, float> OpenSet { get; }

    public void Reset()
    {
        Array.Fill(CameFrom, -1);
        Array.Fill(GScore, float.MaxValue);
        Array.Clear(Closed);
        OpenSet.Clear();
    }
}

/// <summary>
/// A node in the navigation graph — a walkable position in world space.
/// </summary>
public readonly record struct NavNode(float X, float Y, NavNodeKind Kind, int? SurfaceId = null);

public readonly record struct NavSpawnAnchor(float X, float Y, PlayerTeam Team);

public readonly record struct NavEdgeBlock(int FromNode, int ToNode, NavEdgeKind Kind);

public enum NavNodeKind : byte
{
    /// <summary>Surface endpoint on a solid or platform.</summary>
    Ledge = 0,
    /// <summary>Spawn point.</summary>
    Spawn = 1,
    /// <summary>Objective location (intel base, control point, etc.).</summary>
    Objective = 2,
    /// <summary>Mid-surface waypoint for long platforms.</summary>
    Surface = 3,
}

/// <summary>
/// An edge connecting two nodes with a traversal type and cost.
/// </summary>
public readonly record struct NavEdge(
    int ToNode,
    NavEdgeKind Kind,
    float Cost,
    NavEdgeCompletion Completion,
    int JumpTriggerTick,
    int ProbeTicks,
    float ProbeMoveDirectionX,
    int ProbeVariantAttempts,
    int ProbeVariantSuccesses,
    int SupportedClassMask,
    int SupportedTeamMask,
    bool RequiresGroundedContinuation,
    bool RequiresCarryingIntel,
    NavEdgeLaunchRecipe LaunchRecipe,
    bool? CarryingIntelRequirement = null,
    bool IsOg2Contact = false,
    bool IsRuntimeResolved = false,
    bool RuntimeResolutionExhausted = false)
{
    public NavEdge(int toNode, NavEdgeKind kind, float cost)
        : this(toNode, kind, cost, NavEdgeCompletion.None, 0, 0, 0f, 0, 0, BotBrainClassMask.All, BotBrainTeamMask.All, false, false, NavEdgeLaunchRecipe.None)
    {
    }

    public bool Supports(PlayerClass playerClass) => BotBrainClassMask.Contains(SupportedClassMask, playerClass);

    public bool Supports(PlayerClass playerClass, PlayerTeam? team, bool carryingIntel = false) =>
        BotBrainClassMask.Contains(SupportedClassMask, playerClass)
        && (!team.HasValue || BotBrainTeamMask.Contains(SupportedTeamMask, team.Value))
        && (!RequiresCarryingIntel || carryingIntel)
        && (!CarryingIntelRequirement.HasValue || CarryingIntelRequirement.Value == carryingIntel);
}

public readonly record struct NavEdgeCompletion(
    float MinX,
    float MaxX,
    float MinY,
    float MaxY,
    int[] AcceptedSurfaceIds,
    bool AllowsAirborneObjective = false)
{
    public static NavEdgeCompletion None { get; } = new(0f, 0f, 0f, 0f, []);

    public bool HasWindow => MaxX > MinX && MaxY > MinY;

    public bool Contains(float x, float y) =>
        HasWindow
        && x >= MinX
        && x <= MaxX
        && y >= MinY
        && y <= MaxY;
}

public readonly record struct NavEdgeLaunchRecipe(
    bool StartGrounded,
    int LaunchTick,
    float LaunchMinX,
    float LaunchMaxX,
    float LaunchMinY,
    float LaunchMaxY,
    float LaunchMinHorizontalSpeed,
    float LaunchMaxHorizontalSpeed,
    float ExpectedMoveDirectionX,
    bool JumpStartsGrounded = true,
    NavEdgeAirControlMode AirControlMode = NavEdgeAirControlMode.HoldDirection,
    int AirControlHoldTicks = 0,
    int PreLaunchBrakeTicks = 0)
{
    public static NavEdgeLaunchRecipe None { get; } = new(
        StartGrounded: false,
        LaunchTick: -1,
        LaunchMinX: 0f,
        LaunchMaxX: 0f,
        LaunchMinY: 0f,
        LaunchMaxY: 0f,
        LaunchMinHorizontalSpeed: 0f,
        LaunchMaxHorizontalSpeed: 0f,
        ExpectedMoveDirectionX: 0f,
        JumpStartsGrounded: false,
        AirControlMode: NavEdgeAirControlMode.HoldDirection,
        AirControlHoldTicks: 0,
        PreLaunchBrakeTicks: 0);

    public bool HasRecipe =>
        LaunchTick >= 0
        && LaunchMaxX > LaunchMinX
        && LaunchMaxY > LaunchMinY
        && LaunchMaxHorizontalSpeed >= LaunchMinHorizontalSpeed;

    public bool ContainsLaunchState(PlayerEntity player) =>
        HasRecipe
        && (!StartGrounded || player.IsGrounded)
        && player.X >= LaunchMinX
        && player.X <= LaunchMaxX
        && player.Y >= LaunchMinY
        && player.Y <= LaunchMaxY
        && player.HorizontalSpeed >= LaunchMinHorizontalSpeed
        && player.HorizontalSpeed <= LaunchMaxHorizontalSpeed;

}

/// <summary>
/// Horizontal input schedule after a certified jump has consumed its launch
/// input. The schedule is part of the OG2 contact proof because faster classes
/// can otherwise cross a narrow landing window while holding the launch key.
/// </summary>
public enum NavEdgeAirControlMode : byte
{
    HoldDirection = 0,
    ReleaseDirection = 1,
    CounterSteer = 2,
}

public enum NavEdgeKind : byte
{
    /// <summary>Horizontal walk on the same surface.</summary>
    Walk = 0,
    /// <summary>Jump required (edge-trigger Up).</summary>
    Jump = 1,
    /// <summary>Fall off a ledge (no input needed beyond walking off).</summary>
    Fall = 2,
    /// <summary>Drop through a dropdown platform (hold Down).</summary>
    Dropdown = 3,
}
