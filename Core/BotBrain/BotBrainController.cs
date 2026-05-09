namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// The per-bot tick driver. Each bot gets one BotBrainController instance.
/// Every tick, Think() reads the world state and returns a PlayerInputSnapshot.
/// 
/// This is the single entry point for the bot system. The server calls Think()
/// once per tick for each bot slot, then writes the result into the network
/// player input dictionary before the simulation advances.
/// </summary>
public sealed class BotBrainController
{
    private readonly SteeringMachine _steering = new();
    private readonly AimResolver _aimResolver = new();

    private NavGraph? _navGraph;
    private NavPath? _currentPath;
    private int _goalNodeIndex = -1;
    private int _repathCooldownTicks;
    private SimpleLevel? _lastLevel;
    private PlayerInputSnapshot _previousInput;
    private readonly Dictionary<NavEdgeBlock, int> _blockedEdges = [];

    /// <summary>
    /// How often (in ticks) the bot reconsiders its path.
    /// </summary>
    private const int RepathIntervalTicks = 30; // 1 second at 30 tps.

    private const int FailedEdgeBlockTicks = 900; // 30 seconds at 30 tps.

    /// <summary>
    /// How often (in ticks) the bot re-evaluates its objective.
    /// </summary>
    private const int ObjectiveReevalIntervalTicks = 60; // 2 seconds.

    private int _objectiveReevalCooldown;
    private (float X, float Y) _currentGoalPosition;

    public int CurrentPathNode => _currentPath?.CurrentNode ?? -1;

    public int CurrentPathIndex => _currentPath?.CurrentIndex ?? -1;

    public int CurrentPathCount => _currentPath?.Count ?? 0;

    public int CurrentGoalNode => _goalNodeIndex;

    public NavPath? CurrentPath => _currentPath;

    public SteeringOutput LastSteeringOutput { get; private set; }

    /// <summary>
    /// Produce a PlayerInputSnapshot for this bot for the current tick.
    /// </summary>
    public PlayerInputSnapshot Think(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam team)
    {
        // Rebuild nav graph if the level changed (map rotation).
        if (_lastLevel != world.Level)
        {
            _navGraph = BotNavigationAssetStore.LoadGraphOrBuild(world.Level);
            _lastLevel = world.Level;
            _currentPath = null;
            _goalNodeIndex = -1;
            _steering.Reset();
        }

        if (_navGraph is null || !self.IsAlive)
        {
            _previousInput = default;
            LastSteeringOutput = default;
            return default;
        }

        DecayBlockedEdges();

        // 1. Select combat target.
        var combatTarget = TargetSelector.SelectTarget(self, world, team);

        // 2. Evaluate objective (throttled).
        _objectiveReevalCooldown--;
        if (_objectiveReevalCooldown <= 0 || combatTarget is not null)
        {
            _currentGoalPosition = ObjectiveEvaluator.EvaluateGoal(self, world, team, combatTarget);
            _objectiveReevalCooldown = ObjectiveReevalIntervalTicks;
        }

        // 3. Find/update path.
        UpdatePath(self, team);

        // 4. Run steering.
        var steeringOutput = _steering.Update(self, _navGraph, _currentPath, world.Level, team);
        LastSteeringOutput = steeringOutput;

        // Handle repath requests from stuck detection.
        if (steeringOutput.RequestRepath)
        {
            if (steeringOutput.FailedEdge.HasFailure)
            {
                _blockedEdges[new NavEdgeBlock(
                    steeringOutput.FailedEdge.FromNode,
                    steeringOutput.FailedEdge.ToNode,
                    steeringOutput.FailedEdge.Kind)] = FailedEdgeBlockTicks;
            }

            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = 0;
        }

        // 5. Resolve aim.
        var (aimX, aimY) = _aimResolver.Resolve(self, combatTarget, _navGraph, _currentPath, steeringOutput);

        // 6. Synthesize input.
        var input = BotInputSynthesizer.Synthesize(self, steeringOutput, aimX, aimY, combatTarget, _previousInput);
        _previousInput = input;
        return input;
    }

    private void UpdatePath(PlayerEntity self, PlayerTeam team)
    {
        if (_navGraph is null)
        {
            return;
        }

        _repathCooldownTicks--;
        var needsRepath = _currentPath is null
            || _currentPath.IsComplete
            || _repathCooldownTicks <= 0;

        if (!needsRepath)
        {
            return;
        }

        var startNode = _navGraph.FindNearestTraversalStartNode(self.X, self.Y);
        var exactGoalNode = _navGraph.FindNearestNode(_currentGoalPosition.X, _currentGoalPosition.Y);
        if (startNode < 0 || exactGoalNode < 0)
        {
            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = RepathIntervalTicks;
            return;
        }

        // Don't repath if goal hasn't changed and we have a valid path.
        _repathCooldownTicks = RepathIntervalTicks;

        var activeBlockedEdges = _blockedEdges.Count > 0
            ? _blockedEdges.Keys.ToHashSet()
            : null;
        var goalNode = exactGoalNode;
        var refreshedPath = _navGraph.FindPath(startNode, goalNode, self.ClassId, activeBlockedEdges, team);
        if (refreshedPath is null)
        {
            goalNode = _navGraph.FindNearestReachableNode(
                _currentGoalPosition.X,
                _currentGoalPosition.Y,
                startNode,
                self.ClassId,
                activeBlockedEdges,
                team);
            refreshedPath = _navGraph.FindPath(startNode, goalNode, self.ClassId, activeBlockedEdges, team);
        }

        if (refreshedPath is null && activeBlockedEdges is not null)
        {
            _blockedEdges.Clear();
            goalNode = exactGoalNode;
            refreshedPath = _navGraph.FindPath(startNode, goalNode, self.ClassId, team: team);
            if (refreshedPath is null)
            {
                goalNode = _navGraph.FindNearestReachableNode(
                    _currentGoalPosition.X,
                    _currentGoalPosition.Y,
                    startNode,
                    self.ClassId,
                    team: team);
                refreshedPath = _navGraph.FindPath(startNode, goalNode, self.ClassId, team: team);
            }
        }

        // Don't repath if goal hasn't changed and we have a valid path.
        if (goalNode == _goalNodeIndex
            && _currentPath is not null
            && !_currentPath.IsComplete
            && !ShouldReplaceStalePathFromCurrentPosition(self, _navGraph, _currentPath))
        {
            _repathCooldownTicks = RepathIntervalTicks;
            return;
        }

        if (refreshedPath is null)
        {
            return;
        }

        _currentPath = refreshedPath;
        _goalNodeIndex = goalNode;
        if (_currentPath is not null)
        {
            _steering.Reset();
        }
    }

    private static bool ShouldReplaceStalePathFromCurrentPosition(PlayerEntity self, NavGraph graph, NavPath path)
    {
        if (path.CurrentIndex != 0 || !self.IsGrounded)
        {
            return false;
        }

        var targetNode = graph.GetNode(path.CurrentNode);
        return targetNode.Y < self.Y - 48f
            && MathF.Abs(targetNode.X - self.X) < 128f;
    }

    /// <summary>
    /// Reset the bot brain state. Call on respawn or team change.
    /// </summary>
    public void Reset()
    {
        _currentPath = null;
        _goalNodeIndex = -1;
        _repathCooldownTicks = 0;
        _objectiveReevalCooldown = 0;
        _steering.Reset();
        _previousInput = default;
        LastSteeringOutput = default;
        _blockedEdges.Clear();
    }

    private void DecayBlockedEdges()
    {
        if (_blockedEdges.Count == 0)
        {
            return;
        }

        foreach (var key in _blockedEdges.Keys.ToArray())
        {
            var remaining = _blockedEdges[key] - 1;
            if (remaining <= 0)
            {
                _blockedEdges.Remove(key);
            }
            else
            {
                _blockedEdges[key] = remaining;
            }
        }
    }

}
