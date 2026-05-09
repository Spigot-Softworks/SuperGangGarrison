namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// The result of an A* pathfind — an ordered sequence of waypoint node indices.
/// </summary>
public sealed class NavPath
{
    private readonly int[] _waypoints;
    private readonly NavEdge[] _incomingEdges;
    private int _currentIndex;

    public NavPath(int[] waypoints, float totalCost)
        : this(waypoints, new NavEdge[waypoints.Length], totalCost)
    {
    }

    public NavPath(int[] waypoints, NavEdge[] incomingEdges, float totalCost)
    {
        _waypoints = waypoints;
        _incomingEdges = incomingEdges.Length == waypoints.Length
            ? incomingEdges
            : new NavEdge[waypoints.Length];
        TotalCost = totalCost;
    }

    /// <summary>Total path cost from start to goal.</summary>
    public float TotalCost { get; }

    /// <summary>Number of waypoints in the path.</summary>
    public int Count => _waypoints.Length;

    /// <summary>Index of the waypoint currently being pursued.</summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>Whether all waypoints have been reached.</summary>
    public bool IsComplete => _currentIndex >= _waypoints.Length;

    /// <summary>The node index of the current waypoint, or -1 if complete.</summary>
    public int CurrentNode => _currentIndex < _waypoints.Length ? _waypoints[_currentIndex] : -1;

    /// <summary>The node index of the final waypoint.</summary>
    public int GoalNode => _waypoints.Length > 0 ? _waypoints[^1] : -1;

    /// <summary>How many waypoints remain including the current one.</summary>
    public int RemainingCount => Math.Max(0, _waypoints.Length - _currentIndex);

    /// <summary>
    /// Advance to the next waypoint. Call this when the bot has reached the current one.
    /// </summary>
    public void Advance()
    {
        if (_currentIndex < _waypoints.Length)
        {
            _currentIndex++;
        }
    }

    /// <summary>
    /// Get the node index at a specific position in the path.
    /// </summary>
    public int GetWaypoint(int index) => _waypoints[index];

    public bool TryGetCurrentEdge(out NavEdge edge)
    {
        if (_currentIndex <= 0 || _currentIndex >= _incomingEdges.Length)
        {
            edge = default;
            return false;
        }

        edge = _incomingEdges[_currentIndex];
        return true;
    }

    public bool TryGetIncomingEdge(int index, out NavEdge edge)
    {
        if (index <= 0 || index >= _incomingEdges.Length)
        {
            edge = default;
            return false;
        }

        edge = _incomingEdges[index];
        return true;
    }
}
