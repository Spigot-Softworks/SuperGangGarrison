namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// A lightweight waypoint graph built from level geometry.
/// Nodes are walkable positions; edges encode how to traverse between them.
/// </summary>
public sealed class NavGraph
{
    private const float SignificantWalkVerticalDelta = 24f;
    private const float SuspiciousRelayVerticalDelta = 24f;
    private const float SuspiciousRelayHorizontalReach = 260f;
    private const float SuspiciousRelayCostFloorMultiplier = 0.55f;

    private readonly NavNode[] _nodes;
    private readonly List<NavEdge>[] _adjacency;
    private readonly bool[] _spawnAdjacentNodes;
    private readonly string? _levelName;
    private readonly GameModeKind? _mode;

    public NavGraph(NavNode[] nodes, List<NavEdge>[] adjacency, string? levelName = null, GameModeKind? mode = null)
    {
        _nodes = nodes;
        _adjacency = adjacency;
        _spawnAdjacentNodes = ResolveSpawnAdjacentNodes(nodes);
        _levelName = levelName;
        _mode = mode;
    }

    public int NodeCount => _nodes.Length;

    public NavNode GetNode(int index) => _nodes[index];

    public ReadOnlySpan<NavEdge> GetEdges(int nodeIndex) =>
        _adjacency[nodeIndex] is { Count: > 0 } list
            ? System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list)
            : ReadOnlySpan<NavEdge>.Empty;

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

    public int FindNearestTraversalStartNode(float x, float y, float maxAboveDistance = float.PositiveInfinity)
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

        return bestIndex >= 0
            ? bestIndex
            : FindNearestTraversalStartNode(x, y);
    }

    public bool IsOnAcceptedCompletionSurface(float x, float y, NavEdgeCompletion completion)
    {
        if (completion.AcceptedSurfaceIds.Length == 0)
        {
            return true;
        }

        var nodeIndex = FindNearestTraversalStartNode(x, y, maxAboveDistance: 12f);
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
        completion.Contains(x, y) && IsOnAcceptedCompletionSurface(x, y, completion);

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
                if (playerClass.HasValue && !edge.Supports(playerClass.Value, team, carryingIntel))
                {
                    continue;
                }

                var neighbor = edge.ToNode;
                if (blockedEdges is not null && blockedEdges.Contains(new NavEdgeBlock(current, neighbor, edge.Kind)))
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
    /// A* shortest path from startNode to goalNode.
    /// Returns null if no path exists.
    /// </summary>
    public NavPath? FindPath(
        int startNode,
        int goalNode,
        PlayerClass? playerClass = null,
        IReadOnlySet<NavEdgeBlock>? blockedEdges = null,
        PlayerTeam? team = null,
        bool carryingIntel = false)
    {
        if (startNode < 0 || startNode >= _nodes.Length || goalNode < 0 || goalNode >= _nodes.Length)
        {
            return null;
        }

        if (startNode == goalNode)
        {
            return new NavPath([startNode], 0f);
        }

        var openSet = new PriorityQueue<int, float>();
        var cameFrom = new int[_nodes.Length];
        var edgeFrom = new NavEdge[_nodes.Length];
        var gScore = new float[_nodes.Length];
        var closed = new bool[_nodes.Length];
        Array.Fill(cameFrom, -1);
        Array.Fill(gScore, float.MaxValue);

        gScore[startNode] = 0f;
        openSet.Enqueue(startNode, Heuristic(startNode, goalNode));

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (current == goalNode)
            {
                return ReconstructPath(cameFrom, edgeFrom, current, gScore[current]);
            }

            if (closed[current])
            {
                continue;
            }

            closed[current] = true;

            var edges = GetEdges(current);
            for (var i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                if (playerClass.HasValue && !edge.Supports(playerClass.Value, team, carryingIntel))
                {
                    continue;
                }

                var neighbor = edge.ToNode;
                if (blockedEdges is not null && blockedEdges.Contains(new NavEdgeBlock(current, neighbor, edge.Kind)))
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

                cameFrom[neighbor] = current;
                edgeFrom[neighbor] = edge;
                gScore[neighbor] = tentativeG;
                openSet.Enqueue(neighbor, tentativeG + Heuristic(neighbor, goalNode));
            }
        }

        return null;
    }

    private float Heuristic(int fromNode, int toNode)
    {
        var dx = _nodes[toNode].X - _nodes[fromNode].X;
        var dy = _nodes[toNode].Y - _nodes[fromNode].Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private float ResolveTraversalCost(NavEdge edge, int fromNodeIndex, int toNodeIndex, PlayerClass? playerClass, bool carryingIntel, PlayerTeam? team)
    {
        var fromNode = _nodes[fromNodeIndex];
        var toNode = _nodes[toNodeIndex];
        var cost = MathF.Max(1f, edge.Cost);
        var verticalDelta = MathF.Abs(toNode.Y - fromNode.Y);
        var horizontalDelta = MathF.Abs(toNode.X - fromNode.X);
        var euclideanDistance = MathF.Sqrt((horizontalDelta * horizontalDelta) + (verticalDelta * verticalDelta));
        var isSuspiciousVerticalRelay = IsSuspiciousVerticalRelay(edge, cost, verticalDelta, horizontalDelta, euclideanDistance);
        if (ShouldReturnRawTraversalCost(edge, verticalDelta, isSuspiciousVerticalRelay, playerClass, carryingIntel, team))
        {
            return cost + ResolveCarrierSpawnAdjacencyPenalty(fromNodeIndex, toNodeIndex, carryingIntel, fromNode, toNode);
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
            + ResolveCarrierSpawnAdjacencyPenalty(fromNodeIndex, toNodeIndex, carryingIntel, fromNode, toNode)
            + ResolveMapSpecificTraversalPenalty(edge, fromNode, toNode, playerClass, carryingIntel, team);
    }

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
        NavNode toNode)
    {
        if (!carryingIntel
            || _mode != GameModeKind.CaptureTheFlag
            || !string.Equals(_levelName, "Eiger", StringComparison.OrdinalIgnoreCase)
            || fromNode.Kind == NavNodeKind.Objective
            || toNode.Kind == NavNodeKind.Objective)
        {
            return 0f;
        }

        return _spawnAdjacentNodes[fromNodeIndex] || _spawnAdjacentNodes[toNodeIndex]
            ? 3_000f
            : 0f;
    }

    private static bool[] ResolveSpawnAdjacentNodes(NavNode[] nodes)
    {
        const float spawnHorizontalRange = 170f;
        const float spawnVerticalRange = 80f;
        var spawnAdjacent = new bool[nodes.Length];
        for (var i = 0; i < nodes.Length; i += 1)
        {
            if (nodes[i].Kind is NavNodeKind.Spawn or NavNodeKind.Objective)
            {
                continue;
            }

            for (var j = 0; j < nodes.Length; j += 1)
            {
                if (nodes[j].Kind != NavNodeKind.Spawn)
                {
                    continue;
                }

                if (MathF.Abs(nodes[i].X - nodes[j].X) <= spawnHorizontalRange
                    && MathF.Abs(nodes[i].Y - nodes[j].Y) <= spawnVerticalRange)
                {
                    spawnAdjacent[i] = true;
                    break;
                }
            }
        }

        return spawnAdjacent;
    }

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
        if (_mode != GameModeKind.CaptureTheFlag || !carryingIntel)
        {
            return 0f;
        }

        if (edge.Kind == NavEdgeKind.Walk
            || playerClass != PlayerClass.Soldier
            || !team.HasValue
            || !string.Equals(_levelName, "Truefort", StringComparison.OrdinalIgnoreCase))
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
        if (!IsInBox(fromNode, minX: 4_180f, maxX: 4_680f, minY: 430f, maxY: 920f)
            || !IsInBox(toNode, minX: 4_180f, maxX: 4_680f, minY: 430f, maxY: 920f))
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
}

/// <summary>
/// A node in the navigation graph — a walkable position in world space.
/// </summary>
public readonly record struct NavNode(float X, float Y, NavNodeKind Kind, int? SurfaceId = null);

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
    NavEdgeLaunchRecipe LaunchRecipe)
{
    public NavEdge(int toNode, NavEdgeKind kind, float cost)
        : this(toNode, kind, cost, NavEdgeCompletion.None, 0, 0, 0f, 0, 0, BotBrainClassMask.All, BotBrainTeamMask.All, false, false, NavEdgeLaunchRecipe.None)
    {
    }

    public bool Supports(PlayerClass playerClass) => BotBrainClassMask.Contains(SupportedClassMask, playerClass);

    public bool Supports(PlayerClass playerClass, PlayerTeam? team, bool carryingIntel = false) =>
        BotBrainClassMask.Contains(SupportedClassMask, playerClass)
        && (!team.HasValue || BotBrainTeamMask.Contains(SupportedTeamMask, team.Value))
        && (!RequiresCarryingIntel || carryingIntel);
}

public readonly record struct NavEdgeCompletion(
    float MinX,
    float MaxX,
    float MinY,
    float MaxY,
    int[] AcceptedSurfaceIds)
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
    float ExpectedMoveDirectionX)
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
        ExpectedMoveDirectionX: 0f);

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
