namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// A lightweight waypoint graph built from level geometry.
/// Nodes are walkable positions; edges encode how to traverse between them.
/// </summary>
public sealed class NavGraph
{
    private readonly NavNode[] _nodes;
    private readonly List<NavEdge>[] _adjacency;

    public NavGraph(NavNode[] nodes, List<NavEdge>[] adjacency)
    {
        _nodes = nodes;
        _adjacency = adjacency;
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
        bool carryingIntel = false)
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
        var bestScore = ScoreReachableGoalCandidate(startNode, x, y);

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (closed[current])
            {
                continue;
            }

            closed[current] = true;
            var candidateScore = ScoreReachableGoalCandidate(current, x, y);
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

                var tentativeG = gScore[current] + ResolveTraversalCost(edge);
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

                var tentativeG = gScore[current] + ResolveTraversalCost(edge);
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

    private static float ResolveTraversalCost(NavEdge edge)
    {
        return edge.Cost;
    }

    private float ScoreReachableGoalCandidate(int nodeIndex, float x, float y)
    {
        var node = _nodes[nodeIndex];
        var dx = node.X - x;
        var dy = node.Y - y;
        var kindPenalty = node.Kind == NavNodeKind.Spawn ? 10_000f : 0f;
        return (dx * dx) + (dy * dy * 2f) + kindPenalty;
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
