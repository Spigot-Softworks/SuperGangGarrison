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
    private readonly CombatDecisionMemory _combatMemory = new();
    private readonly ObjectiveTapeExecutor _objectiveTapeExecutor = new();
    private readonly NavGraph? _graphOverride;

    private NavGraph? _navGraph;
    private NavPath? _currentPath;
    private int _goalNodeIndex = -1;
    private int _repathCooldownTicks;
    private int _thinkTicks;
    private int _carrierCapFinishRunupUntilTick;
    private int _carrierCapFinishAttackUntilTick;
    private int _platformLadderStage;
    private float _platformLadderSide;
    private int _ataliaPointClimbStage;
    private float _ataliaPointClimbSide;
    private int _ataliaCentralRecoveryStage;
    private SimpleLevel? _lastLevel;
    private BotBrainObjectiveTapeAsset? _objectiveTapeAsset;
    private PlayerInputSnapshot _previousInput;
    private readonly Dictionary<NavEdgeBlock, int> _blockedEdges = [];

    /// <summary>
    /// How often (in ticks) the bot reconsiders its path.
    /// </summary>
    private const int RepathIntervalTicks = 30; // 1 second at 30 tps.

    private const int FailedEdgeBlockTicks = 9000; // 5 minutes at 30 tps.
    private const float DirectSeekPlayerDistance = 900f;
    private const float IntelCarrierDirectSeekDistance = 1200f;
    private const float EscortCarrierDirectSeekDistance = 900f;
    private const int CaptureStrafeHopCycleTicks = 32;
    private const int CaptureStrafeHopSideTicks = 14;
    private const int CaptureStrafeHopWindowTicks = 4;
    private const float CaptureStrafeCenterBand = 0.08f;
    private const float CaptureStrafeBrakeSpeed = 36f;
    private const float CapturePointDirectSeekDistance = 360f;
    private const float CapturePointDirectSeekVerticalRange = 300f;
    private const float CapturePointHoldHorizontalRange = 72f;
    private const float CapturePointHoldVerticalRange = 72f;
    private const float CapturePointHoldCenterDeadZone = 12f;
    private const float PlatformLadderHorizontalRange = 360f;
    private const float PlatformLadderVerticalMin = 48f;
    private const float PlatformLadderVerticalMax = 280f;
    private const float PlatformLadderTargetDeadZone = 12f;
    private const float PlatformLadderArrivalHorizontal = 36f;
    private const float PlatformLadderArrivalVertical = 18f;
    private const float PlatformLadderJumpHorizontalRange = 96f;
    private const float AtaliaPointClimbHorizontalRange = 140f;
    private const float AtaliaPointClimbVerticalMin = 24f;
    private const float AtaliaPointClimbVerticalMax = 96f;
    private const float AtaliaPointClimbRunupSpeed = 80f;
    private const float AtaliaPointClimbLaunchArrivalHorizontal = 8f;
    private const float AtaliaPointClimbLandingArrivalHorizontal = 48f;
    private const float AtaliaPointClimbArrivalVertical = 24f;
    private const float AtaliaCentralRecoveryRunupSpeed = 80f;
    private const float CarrierCapFinishDirectSeekDistance = 620f;
    private const float CarrierCapFinishDirectSeekVerticalRange = 96f;
    private const float EigerCarrierCapFinishDirectSeekVerticalRange = 180f;
    private const float SoldierCarrierCapFinishRunupDistance = 100f;
    private const float SoldierCarrierCapFinishStuckSpeed = 8f;
    private const int SoldierCarrierCapFinishRunupTicks = 14;
    private const int SoldierCarrierCapFinishAttackTicks = 36;
    private const float DirectSeekRouteVerticalThreshold = 80f;
    private const float SpyRetreatEnemyDistance = 460f;
    private const float SpyBackstabPositionTolerance = 10f;
    private const float SniperRetreatDistance = 300f;
    private const float SniperPreferredMaxDistance = 680f;
    private const float SniperDroppedIntelDirectSeekDistance = 520f;
    private const float ObjectiveAllyIntelPressureDistance = 640f;

    private const float GroundedStartNodeMaxAboveDistance = 12f;
    private const float FallingStartNodeMaxAboveDistance = 8f;

    /// <summary>
    /// How often (in ticks) the bot re-evaluates its objective.
    /// </summary>
    private const int ObjectiveReevalIntervalTicks = 60; // 2 seconds.

    private int _objectiveReevalCooldown;
    private (float X, float Y) _currentGoalPosition;

    public BotBrainController()
    {
    }

    public BotBrainController(NavGraph graphOverride)
    {
        _graphOverride = graphOverride ?? throw new ArgumentNullException(nameof(graphOverride));
    }

    public int CurrentPathNode => _currentPath?.CurrentNode ?? -1;

    public int CurrentPathIndex => _currentPath?.CurrentIndex ?? -1;

    public int CurrentPathCount => _currentPath?.Count ?? 0;

    public int CurrentGoalNode => _goalNodeIndex;

    public NavPath? CurrentPath => _currentPath;

    public SteeringOutput LastSteeringOutput { get; private set; }

    public int? LastMedicHealTargetId { get; private set; }

    public bool LastMedicHealTargetIsPocket { get; private set; }

    public string LastSemanticRecoveryTrace { get; private set; } = string.Empty;

    public string LastDirectDriveTrace { get; private set; } = string.Empty;

    public string LastObjectiveTapeTrace { get; private set; } = string.Empty;

    /// <summary>
    /// Produce a PlayerInputSnapshot for this bot for the current tick.
    /// </summary>
    public PlayerInputSnapshot Think(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam team)
    {
        LastSemanticRecoveryTrace = string.Empty;
        LastDirectDriveTrace = string.Empty;
        LastObjectiveTapeTrace = string.Empty;
        _thinkTicks += 1;

        // Rebuild nav graph if the level changed (map rotation).
        if (_lastLevel != world.Level)
        {
            _navGraph = _graphOverride ?? (BotNavigationAssetStore.TryLoadCachedGraph(world.Level, out var graph)
                ? graph
                : null);
            _objectiveTapeAsset = BotBrainObjectiveTapeStore.TryLoad(world.Level, out var tapeAsset)
                ? tapeAsset
                : null;
            _lastLevel = world.Level;
            _currentPath = null;
            _goalNodeIndex = -1;
            _steering.Reset();
            _objectiveTapeExecutor.Reset();
        }

        if (_navGraph is null || !self.IsAlive)
        {
            _previousInput = default;
            LastSteeringOutput = default;
            return default;
        }

        DecayBlockedEdges();

        // 1. Select combat/heal targets.
        var combatTarget = TargetSelector.SelectCombatTarget(self, world, team);
        var healTargetSelection = CombatDecisionResolver.FindBestMedicHealTargetSelection(world, self, team);
        var healTarget = healTargetSelection.Target;
        LastMedicHealTargetId = healTarget?.Id;
        LastMedicHealTargetIsPocket = healTargetSelection.Kind == MedicHealTargetSelectionKind.Pocket;

        // 2. Evaluate objective (throttled).
        _objectiveReevalCooldown--;
        if (_objectiveReevalCooldown <= 0 || combatTarget is not null)
        {
            _currentGoalPosition = ObjectiveEvaluator.EvaluateGoal(self, world, team, combatTarget?.Player);
            _objectiveReevalCooldown = ObjectiveReevalIntervalTicks;
        }

        // 3. Find/update path.
        var graphSuspendedForPointCapture = ShouldSuspendGraphRoutingForControlPointCapture(world, self, team);
        if (graphSuspendedForPointCapture)
        {
            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = RepathIntervalTicks;
            _steering.Reset();
        }
        else
        {
            UpdatePath(self, team);
        }

        var routeMissingAfterUpdate = _currentPath is null;

        // 4. Run steering.
        var steeringOutput = _steering.Update(self, _navGraph, _currentPath, world.Level, team);
        var routeRecoveryRequested = routeMissingAfterUpdate || steeringOutput.RequestRepath;
        if (TryResolveAtaliaUpperMidJumpDrive(world, self, steeringOutput, out var ataliaEdgeSteering, out var ataliaEdgeTrace))
        {
            steeringOutput = ataliaEdgeSteering;
            LastDirectDriveTrace = ataliaEdgeTrace;
        }

        // Handle repath requests from stuck detection.
        if (steeringOutput.RequestRepath)
        {
            if (steeringOutput.FailedEdge.HasFailure)
            {
                var failedBlock = new NavEdgeBlock(
                    steeringOutput.FailedEdge.FromNode,
                    steeringOutput.FailedEdge.ToNode,
                    steeringOutput.FailedEdge.Kind);
                _blockedEdges[failedBlock] = FailedEdgeBlockTicks;
                if (TrySemanticContinuationAfterFailedEdge(self, team, steeringOutput.FailedEdge, failedBlock, out var recoveryTrace))
                {
                    LastSemanticRecoveryTrace = recoveryTrace;
                }
            }

            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = 0;
        }

        var tapeResolved = _objectiveTapeExecutor.TryResolve(
            _objectiveTapeAsset,
            self,
            team,
            _currentGoalPosition,
            _thinkTicks,
            steeringOutput,
            out var tapeSteering);
        if (tapeResolved)
        {
            steeringOutput = tapeSteering;
            LastObjectiveTapeTrace = _objectiveTapeExecutor.LastTrace;
        }
        else if (IsObjectiveTapeHandoffTrace(_objectiveTapeExecutor.LastTrace))
        {
            LastObjectiveTapeTrace = _objectiveTapeExecutor.LastTrace;
            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = 0;
            _steering.Reset();
            UpdatePath(self, team);
            steeringOutput = _steering.Update(self, _navGraph, _currentPath, world.Level, team);
            routeRecoveryRequested = _currentPath is null || steeringOutput.RequestRepath;
        }
        else if (_objectiveTapeExecutor.LastTrace.StartsWith("objectiveTape=idle", StringComparison.Ordinal))
        {
            LastObjectiveTapeTrace = _objectiveTapeExecutor.LastTrace;
        }

        if (tapeResolved
            && self.IsCarryingIntel
            && TryResolveCarrierCapFinishDirectSeek(world, self, team, steeringOutput, out var tapeFinishSteering, out var tapeFinishTrace))
        {
            steeringOutput = tapeFinishSteering;
            LastDirectDriveTrace = tapeFinishTrace;
            tapeResolved = false;
        }

        if (TryResolveSpyRetreat(world, self, team, combatTarget, steeringOutput, out var directSteering, out var directTrace)
            || TryResolveSpyBackstabDrive(world, self, combatTarget, steeringOutput, out directSteering, out directTrace)
            || TryResolveSniperCombatDrive(world, self, combatTarget, steeringOutput, out directSteering, out directTrace)
            || (!tapeResolved && TryResolveDirectSeek(world, self, team, combatTarget, routeRecoveryRequested, steeringOutput, out directSteering, out directTrace)))
        {
            steeringOutput = directSteering;
            LastDirectDriveTrace = directTrace;
        }

        ApplyCaptureStrafeHop(world, self, team, ref steeringOutput);
        LastSteeringOutput = steeringOutput;

        // 5. Resolve aim.
        var (aimX, aimY) = _aimResolver.Resolve(self, combatTarget, healTarget, _navGraph, _currentPath, steeringOutput);

        // 6. Synthesize input.
        var combat = CombatDecisionResolver.Resolve(world, self, combatTarget, healTarget, _combatMemory);
        var input = BotInputSynthesizer.Synthesize(self, steeringOutput, aimX, aimY, combat, _previousInput);
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

        var pathMissing = _currentPath is null;
        var startNode = _navGraph.FindNearestTraversalStartNode(
            self.X,
            self.Y,
            ResolveTraversalStartMaxAboveDistance(self, pathMissing));
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
        var preserveExactControlObjective = ShouldPreserveExactControlObjective();
        var refreshedPath = _navGraph.FindPath(startNode, goalNode, self.ClassId, activeBlockedEdges, team, self.IsCarryingIntel);

        if (refreshedPath is null && !(preserveExactControlObjective && activeBlockedEdges is not null))
        {
            goalNode = _navGraph.FindNearestReachableNode(
                _currentGoalPosition.X,
                _currentGoalPosition.Y,
                startNode,
                self.ClassId,
                activeBlockedEdges,
                team,
                self.IsCarryingIntel);
            refreshedPath = goalNode != startNode || exactGoalNode == startNode
                ? _navGraph.FindPath(startNode, goalNode, self.ClassId, activeBlockedEdges, team, self.IsCarryingIntel)
                : null;
        }

        if (refreshedPath is null && activeBlockedEdges is not null && !preserveExactControlObjective)
        {
            _blockedEdges.Clear();
            activeBlockedEdges = null;
            goalNode = exactGoalNode;
            refreshedPath = _navGraph.FindPath(startNode, goalNode, self.ClassId, team: team, carryingIntel: self.IsCarryingIntel);
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

    private static bool IsObjectiveTapeHandoffTrace(string trace)
    {
        return trace.StartsWith("objectiveTape=complete", StringComparison.Ordinal)
            || trace.StartsWith("objectiveTape=abort", StringComparison.Ordinal);
    }

    private bool ShouldPreserveExactControlObjective()
    {
        return _lastLevel?.Mode is GameModeKind.ControlPoint
            or GameModeKind.KingOfTheHill
            or GameModeKind.DoubleKingOfTheHill;
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

    private static float ResolveTraversalStartMaxAboveDistance(PlayerEntity self, bool pathMissing = false)
    {
        if (self.IsGrounded)
        {
            return GroundedStartNodeMaxAboveDistance;
        }

        if (pathMissing)
        {
            return FallingStartNodeMaxAboveDistance;
        }

        return self.ClassId != PlayerClass.Heavy && self.VerticalSpeed > 0f
            ? FallingStartNodeMaxAboveDistance
            : float.PositiveInfinity;
    }

    /// <summary>
    /// Reset the bot brain state. Call on respawn or team change.
    /// </summary>
    public void Reset()
    {
        _currentPath = null;
        _goalNodeIndex = -1;
        _repathCooldownTicks = 0;
        _thinkTicks = 0;
        _carrierCapFinishRunupUntilTick = 0;
        _carrierCapFinishAttackUntilTick = 0;
        _platformLadderStage = 0;
        _platformLadderSide = 0f;
        _ataliaPointClimbStage = 0;
        _ataliaPointClimbSide = 0f;
        _ataliaCentralRecoveryStage = 0;
        _objectiveReevalCooldown = 0;
        _steering.Reset();
        _previousInput = default;
        LastSteeringOutput = default;
        LastSemanticRecoveryTrace = string.Empty;
        LastDirectDriveTrace = string.Empty;
        LastObjectiveTapeTrace = string.Empty;
        _blockedEdges.Clear();
        _objectiveTapeExecutor.Reset();
        _combatMemory.BeenHealingTicks = 0;
        _combatMemory.ReloadCounterTicks = 0;
        _combatMemory.ZoomToShootTicks = 50;
        LastMedicHealTargetId = null;
        LastMedicHealTargetIsPocket = false;
    }

    private bool TrySemanticContinuationAfterFailedEdge(
        PlayerEntity self,
        PlayerTeam team,
        SteeringFailedEdge failedEdge,
        NavEdgeBlock failedBlock,
        out string trace)
    {
        trace = string.Empty;
        if (_navGraph is null || _currentPath is null || _goalNodeIndex < 0)
        {
            return false;
        }

        if (!IsSemanticContinuationCandidate(failedEdge) || !self.IsGrounded)
        {
            return false;
        }

        var startNode = _navGraph.FindNearestTraversalStartNode(
            self.X,
            self.Y,
            ResolveTraversalStartMaxAboveDistance(self));
        if (startNode < 0)
        {
            return false;
        }

        var activeBlockedEdges = _blockedEdges.Count > 0
            ? _blockedEdges.Keys.ToHashSet()
            : [];
        activeBlockedEdges.Add(failedBlock);
        var continuationPath = _navGraph.FindPath(startNode, _goalNodeIndex, self.ClassId, activeBlockedEdges, team, self.IsCarryingIntel);
        if (continuationPath is null)
        {
            return false;
        }

        if (PathStartsWithFailedEdge(continuationPath, failedBlock))
        {
            return false;
        }

        var start = _navGraph.GetNode(startNode);
        trace =
            $"semanticRecovery=continuation reason:{failedEdge.Reason} failed:{failedEdge.FromNode}->{failedEdge.ToNode}/{failedEdge.Kind} " +
            $"startNode:{startNode}@({start.X:0.0},{start.Y:0.0}) goalNode:{_goalNodeIndex} pathWaypoints:{continuationPath.Count} " +
            $"pos:({self.X:0.0},{self.Y:0.0}) grounded:{(self.IsGrounded ? 1 : 0)} edgeTicks:{failedEdge.EdgeTicks}";
        return true;
    }

    private static bool IsSemanticContinuationCandidate(SteeringFailedEdge failedEdge) =>
        failedEdge.Reason is "walk_airborne_timeout"
            or "walk_timeout"
            or "landed_below_completion"
            or "missed_completion"
            or "wrong_fall_landing"
            or "fall_not_completing"
            or "edge_timeout_near";

    private static bool PathStartsWithFailedEdge(NavPath path, NavEdgeBlock failedBlock)
    {
        if (path.Count < 2)
        {
            return false;
        }

        if (!path.TryGetIncomingEdge(1, out var firstEdge))
        {
            return false;
        }

        return path.GetWaypoint(0) == failedBlock.FromNode
            && path.GetWaypoint(1) == failedBlock.ToNode
            && firstEdge.Kind == failedBlock.Kind;
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

    private bool TryResolveDirectSeek(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        BotBrainCombatTarget? combatTarget,
        bool routeRecoveryRequested,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        if (TryResolveCaptureTheFlagDirectSeek(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (TryResolveControlPointDirectSeek(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (ShouldDirectSeekEnemiesAfterKothCapture(world, team)
            && TryFindNearestEnemyPlayer(world, self, team, DirectSeekPlayerDistance, out var ownedKothTarget))
        {
            if (TryRouteToDirectSeekTarget(
                    world,
                    self,
                    team,
                    ownedKothTarget.X,
                    ownedKothTarget.Y,
                    $"ownedKothEnemy player:{ownedKothTarget.Id}",
                    steeringOutput,
                    out directSteering,
                    out directTrace)
                || PrimitiveDirectDrive.TryResolveRecovery(
                    world,
                    self,
                    new DirectDriveTarget(DirectDriveTargetKind.Enemy, ownedKothTarget.X, ownedKothTarget.Y, $"ownedKothEnemy player:{ownedKothTarget.Id}"),
                    steeringOutput,
                    out directSteering,
                    out directTrace))
            {
                return true;
            }
        }

        if (!routeRecoveryRequested)
        {
            if (combatTarget is { Kind: BotBrainCombatTargetKind.Player, Player: { } directCombatTarget }
                && TryRouteToDirectSeekTarget(
                    world,
                    self,
                    team,
                    directCombatTarget.X,
                    directCombatTarget.Y,
                    $"enemy player:{directCombatTarget.Id}",
                    steeringOutput,
                    out directSteering,
                    out directTrace))
            {
                return true;
            }

            return PrimitiveDirectDrive.TryResolve(world, self, combatTarget, steeringOutput, out directSteering, out directTrace);
        }

        if (TryFindNearestEnemyPlayer(world, self, team, DirectSeekPlayerDistance, out var recoveryTarget))
        {
            if (TryRouteToDirectSeekTarget(
                    world,
                    self,
                    team,
                    recoveryTarget.X,
                    recoveryTarget.Y,
                    $"recoveryEnemy player:{recoveryTarget.Id}",
                    steeringOutput,
                    out directSteering,
                    out directTrace)
                || PrimitiveDirectDrive.TryResolveRecovery(
                    world,
                    self,
                    new DirectDriveTarget(DirectDriveTargetKind.Enemy, recoveryTarget.X, recoveryTarget.Y, $"recoveryEnemy player:{recoveryTarget.Id}"),
                    steeringOutput,
                    out directSteering,
                    out directTrace))
            {
                return true;
            }
        }

        if (TryResolveAtaliaCentralRecoveryDrive(
                world,
                self,
                _currentGoalPosition.X,
                _currentGoalPosition.Y,
                steeringOutput,
                out directSteering,
                out directTrace))
        {
            return true;
        }

        if (IsAtaliaObjectiveRecovery(world, _currentGoalPosition.X)
            && TryRouteToDirectSeekTarget(
                world,
                self,
                team,
                _currentGoalPosition.X,
                _currentGoalPosition.Y,
                "ataliaRecoveryObjective",
                steeringOutput,
                out directSteering,
                out directTrace,
                requireVerticalSeparation: false))
        {
            return true;
        }

        return PrimitiveDirectDrive.TryResolveRecovery(
            world,
            self,
            new DirectDriveTarget(DirectDriveTargetKind.Objective, _currentGoalPosition.X, _currentGoalPosition.Y, "recoveryObjective"),
            steeringOutput,
            out directSteering,
            out directTrace);
    }

    private static bool TryResolveSpyRetreat(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        BotBrainCombatTarget? combatTarget,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (self.ClassId != PlayerClass.Spy
            || !CombatDecisionResolver.IsSpyCompromised(self)
            || self.Health < MathF.Ceiling(self.MaxHealth * 0.25f)
            || combatTarget is not { Kind: BotBrainCombatTargetKind.Player, Player: { } target }
            || DistanceBetween(self.X, self.Y, target.X, target.Y) > SpyRetreatEnemyDistance)
        {
            return false;
        }

        directSteering.MoveDirection = self.X <= target.X ? -1 : 1;
        directSteering.Jump = steeringOutput.Jump;
        directSteering.DropDown = false;
        directTrace = $"spyRetreat enemy:{target.Id} visibleAlpha:{self.SpyCloakAlpha:0.00} move:{directSteering.MoveDirection:0}";
        return true;
    }

    private static bool TryResolveSpyBackstabDrive(
        SimulationWorld world,
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        var plan = CombatDecisionResolver.ResolveSpyBackstabPlan(world, self, combatTarget);
        if (!plan.ShouldAttempt || plan.Target is null)
        {
            return false;
        }

        var dx = plan.ApproachX - self.X;
        directSteering.MoveDirection = MathF.Abs(dx) <= SpyBackstabPositionTolerance
            ? 0
            : dx > 0f ? 1 : -1;
        directSteering.Jump = steeringOutput.Jump || plan.ApproachY < self.Y - 24f;
        directSteering.DropDown = false;
        directTrace = $"spyBackstab target:{plan.Target.Id} dx:{dx:0.0} ready:{(plan.ReadyToStab ? 1 : 0)}";
        return directSteering.MoveDirection != 0 || directSteering.Jump || plan.ReadyToStab;
    }

    private static bool TryResolveSniperCombatDrive(
        SimulationWorld world,
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (self.ClassId != PlayerClass.Sniper
            || combatTarget is not { Kind: BotBrainCombatTargetKind.Player, Player: { } target }
            || !target.IsAlive
            || !HasOtherAllyAvailableForObjective(self, world, self.Team))
        {
            return false;
        }

        var dx = target.X - self.X;
        var dy = target.Y - self.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        if (distance < SniperRetreatDistance)
        {
            directSteering.MoveDirection = dx >= 0f ? -1 : 1;
            directSteering.Jump = steeringOutput.Jump;
            directSteering.DropDown = false;
            directTrace = $"sniperRetreat target:{target.Id} dist:{distance:0.0} move:{directSteering.MoveDirection:0}";
            return true;
        }

        if (distance > SniperPreferredMaxDistance)
        {
            directSteering.MoveDirection = dx >= 0f ? 1 : -1;
            directSteering.Jump = steeringOutput.Jump || dy < -24f;
            directSteering.DropDown = false;
            directTrace = $"sniperSightlineClose target:{target.Id} dist:{distance:0.0} move:{directSteering.MoveDirection:0}";
            return true;
        }

        return false;
    }

    private bool TryResolveCaptureTheFlagDirectSeek(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag)
        {
            return false;
        }

        if (self.IsCarryingIntel
            && TryResolveCarrierCapFinishDirectSeek(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (self.IsCarryingIntel
            && TryResolveTruefortBlueSoldierCarrierPocketEscape(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (self.IsCarryingIntel)
        {
            return false;
        }

        if (TryFindNearestIntelCarrier(world, self, team, opposingCarrier: true, IntelCarrierDirectSeekDistance, out var enemyCarrier))
        {
            if (TryRouteToDirectSeekTarget(
                    world,
                    self,
                    team,
                    enemyCarrier.X,
                    enemyCarrier.Y,
                    $"enemyCarrier player:{enemyCarrier.Id}",
                    steeringOutput,
                    out directSteering,
                    out directTrace)
                || PrimitiveDirectDrive.TryResolveRecovery(
                    world,
                    self,
                    new DirectDriveTarget(DirectDriveTargetKind.Carrier, enemyCarrier.X, enemyCarrier.Y, $"enemyCarrier player:{enemyCarrier.Id}"),
                    steeringOutput,
                    out directSteering,
                    out directTrace))
            {
                return true;
            }
        }

        var enemyIntel = GetEnemyIntelState(world, team);
        if (enemyIntel.IsDropped
            && ShouldDirectSeekDroppedIntel(self, world, team, enemyIntel))
        {
            if (TryRouteToDirectSeekTarget(
                    world,
                    self,
                    team,
                    enemyIntel.X,
                    enemyIntel.Y,
                    $"droppedEnemyIntel team:{enemyIntel.Team}",
                    steeringOutput,
                    out directSteering,
                    out directTrace)
                || PrimitiveDirectDrive.TryResolveRecovery(
                    world,
                    self,
                    new DirectDriveTarget(DirectDriveTargetKind.Intel, enemyIntel.X, enemyIntel.Y, $"droppedEnemyIntel team:{enemyIntel.Team}"),
                    steeringOutput,
                    out directSteering,
                    out directTrace))
            {
                return true;
            }
        }

        var ownIntel = GetOwnIntelState(world, team);
        if (ownIntel.IsDropped
            && ShouldDirectSeekDroppedIntel(self, world, team, ownIntel))
        {
            if (TryRouteToDirectSeekTarget(
                    world,
                    self,
                    team,
                    ownIntel.X,
                    ownIntel.Y,
                    $"ownDroppedIntel team:{ownIntel.Team}",
                    steeringOutput,
                    out directSteering,
                    out directTrace)
                || PrimitiveDirectDrive.TryResolveRecovery(
                    world,
                    self,
                    new DirectDriveTarget(DirectDriveTargetKind.Intel, ownIntel.X, ownIntel.Y, $"ownDroppedIntel team:{ownIntel.Team}"),
                    steeringOutput,
                    out directSteering,
                    out directTrace))
            {
                return true;
            }
        }

        if (TryFindNearestIntelCarrier(world, self, team, opposingCarrier: false, EscortCarrierDirectSeekDistance, out var friendlyCarrier))
        {
            if (TryRouteToDirectSeekTarget(
                    world,
                    self,
                    team,
                    friendlyCarrier.X,
                    friendlyCarrier.Y,
                    $"escortCarrier player:{friendlyCarrier.Id}",
                    steeringOutput,
                    out directSteering,
                    out directTrace)
                || PrimitiveDirectDrive.TryResolveRecovery(
                    world,
                    self,
                    new DirectDriveTarget(DirectDriveTargetKind.Escort, friendlyCarrier.X, friendlyCarrier.Y, $"escortCarrier player:{friendlyCarrier.Id}"),
                    steeringOutput,
                    out directSteering,
                    out directTrace))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveTruefortBlueSoldierCarrierPocketEscape(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag
            || !world.Level.Name.Equals("Truefort", StringComparison.OrdinalIgnoreCase)
            || team != PlayerTeam.Blue
            || self.ClassId != PlayerClass.Soldier
            || self.X < 700f
            || self.X > 1_150f
            || self.Y < 600f
            || self.Y > 900f)
        {
            return false;
        }

        return PrimitiveDirectDrive.TryResolveRecovery(
            world,
            self,
            new DirectDriveTarget(DirectDriveTargetKind.Objective, 1_206f, 612f, "truefortBlueCarrierPocketEscape"),
            steeringOutput,
            out directSteering,
            out directTrace);
    }

    private bool TryRouteToDirectSeekTarget(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        float targetX,
        float targetY,
        string label,
        SteeringOutput currentSteering,
        out SteeringOutput routedSteering,
        out string trace,
        bool requireVerticalSeparation = true)
    {
        routedSteering = currentSteering;
        trace = string.Empty;
        if (_navGraph is null)
        {
            return false;
        }

        var dx = targetX - self.X;
        var dy = targetY - self.Y;
        if (requireVerticalSeparation && MathF.Abs(dy) < DirectSeekRouteVerticalThreshold)
        {
            return false;
        }

        var startNode = _navGraph.FindNearestTraversalStartNode(
            self.X,
            self.Y,
            ResolveTraversalStartMaxAboveDistance(self, _currentPath is null));
        var goalNode = _navGraph.FindNearestNode(targetX, targetY);
        if (startNode < 0 || goalNode < 0)
        {
            return false;
        }

        var path = _navGraph.FindPath(startNode, goalNode, self.ClassId, team: team, carryingIntel: self.IsCarryingIntel);
        if (path is null || path.Count < 2)
        {
            return false;
        }

        _currentGoalPosition = (targetX, targetY);
        _currentPath = path;
        _goalNodeIndex = goalNode;
        _repathCooldownTicks = RepathIntervalTicks;
        _steering.Reset();
        routedSteering = _steering.Update(self, _navGraph, _currentPath, world.Level, team);
        trace = $"directRoute={label} dx:{dx:0.0} dy:{dy:0.0} path:{path.Count}";
        return true;
    }

    private static bool ShouldDirectSeekDroppedIntel(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam team,
        TeamIntelligenceState intel)
    {
        return self.ClassId != PlayerClass.Sniper
            || DistanceBetween(self.X, self.Y, intel.X, intel.Y) <= SniperDroppedIntelDirectSeekDistance
            || !HasOtherAllyAvailableForObjective(self, world, team);
    }

    private bool TryResolveCarrierCapFinishDirectSeek(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (world.Level.Name.Equals("Orange", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var ownBase = world.Level.GetIntelBase(team);
        if (!ownBase.HasValue)
        {
            return false;
        }

        var dx = ownBase.Value.X - self.X;
        var dy = ownBase.Value.Y - self.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        var verticalRange = ResolveCarrierCapFinishDirectSeekVerticalRange(world.Level);
        if (distance > CarrierCapFinishDirectSeekDistance
            || MathF.Abs(dy) > verticalRange)
        {
            _carrierCapFinishRunupUntilTick = 0;
            _carrierCapFinishAttackUntilTick = 0;
            return false;
        }

        if (!ShouldAllowCarrierCapFinishDirectDrive(self, ownBase.Value.X, ownBase.Value.Y))
        {
            return false;
        }

        if (TryResolveSoldierCarrierCapFinishRunup(
                self,
                dx,
                dy,
                distance,
                steeringOutput,
                out directSteering,
                out directTrace))
        {
            return true;
        }

        return PrimitiveDirectDrive.TryResolveRecovery(
            world,
            self,
            new DirectDriveTarget(DirectDriveTargetKind.Objective, ownBase.Value.X, ownBase.Value.Y, "carrierCapFinish"),
            steeringOutput,
            out directSteering,
            out directTrace);
    }

    private static float ResolveCarrierCapFinishDirectSeekVerticalRange(SimpleLevel level) =>
        level.Name.Equals("Eiger", StringComparison.OrdinalIgnoreCase)
            ? EigerCarrierCapFinishDirectSeekVerticalRange
            : CarrierCapFinishDirectSeekVerticalRange;

    private bool ShouldAllowCarrierCapFinishDirectDrive(PlayerEntity self, float ownBaseX, float ownBaseY)
    {
        if (DistanceBetween(self.X, self.Y, ownBaseX, ownBaseY) <= 260f)
        {
            return true;
        }

        if (_navGraph is null || _currentPath is null || _currentPath.IsComplete)
        {
            return false;
        }

        var remainingWaypoints = _currentPath.Count - _currentPath.CurrentIndex;
        if (remainingWaypoints <= 3)
        {
            return true;
        }

        var currentNode = _navGraph.GetNode(_currentPath.CurrentNode);
        return DistanceBetween(currentNode.X, currentNode.Y, ownBaseX, ownBaseY) <= 96f;
    }

    private bool TryResolveSoldierCarrierCapFinishRunup(
        PlayerEntity self,
        float dx,
        float dy,
        float distance,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (self.ClassId != PlayerClass.Soldier
            || distance > SoldierCarrierCapFinishRunupDistance
            || MathF.Abs(dy) > 48f)
        {
            return false;
        }

        var attackDirection = dx < 0f ? -1 : 1;
        if (_thinkTicks < _carrierCapFinishRunupUntilTick)
        {
            directSteering.MoveDirection = -attackDirection;
            directSteering.Jump = false;
            directSteering.DropDown = false;
            directTrace = $"carrierCapFinishRunup phase:backoff dx:{dx:0.0} dy:{dy:0.0} move:{directSteering.MoveDirection:0}";
            return true;
        }

        if (_thinkTicks < _carrierCapFinishAttackUntilTick)
        {
            directSteering.MoveDirection = attackDirection;
            directSteering.Jump = self.IsGrounded;
            directSteering.DropDown = false;
            directTrace = $"carrierCapFinishRunup phase:attack dx:{dx:0.0} dy:{dy:0.0} move:{directSteering.MoveDirection:0} jump:{(directSteering.Jump ? 1 : 0)}";
            return true;
        }

        if (!self.IsGrounded || MathF.Abs(self.HorizontalSpeed) > SoldierCarrierCapFinishStuckSpeed)
        {
            return false;
        }

        _carrierCapFinishRunupUntilTick = _thinkTicks + SoldierCarrierCapFinishRunupTicks;
        _carrierCapFinishAttackUntilTick = _carrierCapFinishRunupUntilTick + SoldierCarrierCapFinishAttackTicks;
        directSteering.MoveDirection = -attackDirection;
        directSteering.Jump = false;
        directSteering.DropDown = false;
        directTrace = $"carrierCapFinishRunup phase:start dx:{dx:0.0} dy:{dy:0.0} move:{directSteering.MoveDirection:0}";
        return true;
    }

    private bool TryResolveControlPointDirectSeek(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (world.MatchRules.Mode is not (GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill))
        {
            return false;
        }

        if (TryFindNearestUnownedControlPoint(world, self, team, CapturePointDirectSeekDistance, out var point))
        {
            if (TryResolveControlPointPlatformLadderDrive(world, self, point, steeringOutput, out directSteering, out directTrace))
            {
                return true;
            }

            if (TryResolveAtaliaControlPointClimbDrive(world, self, point, steeringOutput, out directSteering, out directTrace))
            {
                return true;
            }

            if (TryResolveAtaliaCentralRecoveryDrive(world, self, point, steeringOutput, out directSteering, out directTrace))
            {
                return true;
            }

            if (ShouldKeepGraphControlForBelowPointClimb(self, point))
            {
                return false;
            }

            if (TryResolveControlPointHold(world, self, point, steeringOutput, out directSteering, out directTrace)
                || TryRouteToDirectSeekTarget(
                    world,
                    self,
                    team,
                    point.HealingAuraCenterX,
                    point.HealingAuraCenterY,
                    $"capturePoint point:{point.Index}",
                    steeringOutput,
                    out directSteering,
                    out directTrace)
                || PrimitiveDirectDrive.TryResolveRecovery(
                    world,
                    self,
                    new DirectDriveTarget(
                        DirectDriveTargetKind.Objective,
                        point.HealingAuraCenterX,
                        point.HealingAuraCenterY,
                        $"capturePoint point:{point.Index}"),
                    steeringOutput,
                    out directSteering,
                    out directTrace))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldSuspendGraphRoutingForControlPointCapture(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team)
    {
        if (TryFindActivePointBeingCaptured(world, self, team, out _))
        {
            return true;
        }

        if (!TryFindNearestUnownedControlPoint(world, self, team, CapturePointHoldHorizontalRange, out var point))
        {
            return false;
        }

        var dx = point.HealingAuraCenterX - self.X;
        var dy = point.HealingAuraCenterY - self.Y;
        return MathF.Abs(dx) <= CapturePointHoldHorizontalRange
            && MathF.Abs(dy) <= CapturePointHoldVerticalRange
            && dy >= -24f;
    }

    private bool TryResolveControlPointPlatformLadderDrive(
        SimulationWorld world,
        PlayerEntity self,
        ControlPointState point,
        SteeringOutput steeringOutput,
        out SteeringOutput ladderSteering,
        out string trace)
    {
        ladderSteering = steeringOutput;
        trace = string.Empty;
        if (!IsValleyPlatformLadderCandidate(world, self, point))
        {
            _platformLadderStage = 0;
            _platformLadderSide = 0f;
            return false;
        }

        if (_platformLadderStage <= 0 || _platformLadderSide == 0f)
        {
            _platformLadderStage = ResolveInitialPlatformLadderStage(self, point);
            _platformLadderSide = self.X <= point.HealingAuraCenterX ? -1f : 1f;
        }

        var target = ResolvePlatformLadderTarget(point, _platformLadderSide, _platformLadderStage);
        if (self.IsGrounded
            && MathF.Abs(self.X - target.X) <= PlatformLadderArrivalHorizontal
            && MathF.Abs(self.Y - target.Y) <= PlatformLadderArrivalVertical
            && _platformLadderStage < 4)
        {
            _platformLadderStage += 1;
            target = ResolvePlatformLadderTarget(point, _platformLadderSide, _platformLadderStage);
        }

        var dx = target.X - self.X;
        var dy = target.Y - self.Y;
        ladderSteering.MoveDirection = ResolvePlatformLadderMoveDirection(dx, dy, _platformLadderSide, _platformLadderStage);
        ladderSteering.Jump = self.IsGrounded
            && dy < -18f
            && MathF.Abs(dx) <= PlatformLadderJumpHorizontalRange
            && IsPlatformLadderJumpReady(self, ladderSteering.MoveDirection, _platformLadderStage);
        ladderSteering.DropDown = false;
        trace = $"platformLadder point:{point.Index} stage:{_platformLadderStage} dx:{dx:0.0} dy:{dy:0.0} move:{ladderSteering.MoveDirection:0} jump:{(ladderSteering.Jump ? 1 : 0)}";
        return true;
    }

    private static (float X, float Y) ResolvePlatformLadderTarget(ControlPointState point, float side, int stage) =>
        stage switch
        {
            1 => (point.HealingAuraCenterX + (side * 84f), point.HealingAuraCenterY + 180f),
            2 => (point.HealingAuraCenterX + (side * 141f), point.HealingAuraCenterY + 126f),
            3 => (point.HealingAuraCenterX + (side * 99f), point.HealingAuraCenterY + 72f),
            _ => (point.HealingAuraCenterX + (side * 70f), point.HealingAuraCenterY + 18f),
        };

    private static int ResolveInitialPlatformLadderStage(PlayerEntity self, ControlPointState point)
    {
        var dy = self.Y - point.HealingAuraCenterY;
        if (dy <= 84f)
        {
            return 3;
        }

        if (dy <= 144f)
        {
            return 2;
        }

        return 1;
    }

    private static float ResolvePlatformLadderMoveDirection(float dx, float dy, float side, int stage)
    {
        if (dy < -18f)
        {
            if (MathF.Abs(dx) > 3f)
            {
                return dx > 0f ? 1 : -1;
            }

            return stage == 2 ? side : -side;
        }

        return MathF.Abs(dx) <= PlatformLadderTargetDeadZone
            ? 0
            : dx > 0f ? 1 : -1;
    }

    private static bool IsPlatformLadderJumpReady(PlayerEntity self, float moveDirection, int stage)
    {
        if (self.ClassId != PlayerClass.Heavy || stage <= 1 || stage >= 4)
        {
            return true;
        }

        return moveDirection != 0f
            && self.HorizontalSpeed * moveDirection >= 60f;
    }

    private static bool IsValleyPlatformLadderCandidate(SimulationWorld world, PlayerEntity self, ControlPointState point)
    {
        if (!world.Level.Name.Contains("Valley", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var dx = MathF.Abs(self.X - point.HealingAuraCenterX);
        var dy = self.Y - point.HealingAuraCenterY;
        return dx <= PlatformLadderHorizontalRange
            && dy >= PlatformLadderVerticalMin
            && dy <= PlatformLadderVerticalMax
            && !world.IsPlayerInControlPointCaptureZone(self, point.Index);
    }

    private bool TryResolveAtaliaControlPointClimbDrive(
        SimulationWorld world,
        PlayerEntity self,
        ControlPointState point,
        SteeringOutput steeringOutput,
        out SteeringOutput climbSteering,
        out string trace)
    {
        climbSteering = steeringOutput;
        trace = string.Empty;
        if (!IsAtaliaPointClimbCandidate(world, self, point))
        {
            _ataliaPointClimbStage = 0;
            _ataliaPointClimbSide = 0f;
            return false;
        }

        if (_ataliaPointClimbStage <= 0 || _ataliaPointClimbSide == 0f)
        {
            _ataliaPointClimbStage = 1;
            _ataliaPointClimbSide = ResolveAtaliaPointClimbSide(point);
        }

        var target = ResolveAtaliaPointClimbTarget(point, _ataliaPointClimbSide, _ataliaPointClimbStage);
        var arrivalHorizontal = _ataliaPointClimbStage == 1
            ? AtaliaPointClimbLaunchArrivalHorizontal
            : AtaliaPointClimbLandingArrivalHorizontal;
        if (self.IsGrounded
            && MathF.Abs(self.X - target.X) <= arrivalHorizontal
            && MathF.Abs(self.Y - target.Y) <= AtaliaPointClimbArrivalVertical
            && _ataliaPointClimbStage < 2)
        {
            _ataliaPointClimbStage = 2;
            target = ResolveAtaliaPointClimbTarget(point, _ataliaPointClimbSide, _ataliaPointClimbStage);
        }

        var dx = target.X - self.X;
        var dy = target.Y - self.Y;
        var moveDirection = MathF.Abs(dx) <= 6f
            ? (int)-_ataliaPointClimbSide
            : dx > 0f ? 1 : -1;
        climbSteering.MoveDirection = moveDirection;
        climbSteering.Jump = self.IsGrounded
            && dy < -18f
            && MathF.Abs(dx) <= 72f
            && _ataliaPointClimbStage >= 2
            && IsAtaliaPointClimbJumpReady(self, point, _ataliaPointClimbSide, moveDirection);
        climbSteering.DropDown = false;
        trace = $"ataliaPointClimb point:{point.Index} stage:{_ataliaPointClimbStage} dx:{dx:0.0} dy:{dy:0.0} move:{climbSteering.MoveDirection:0} jump:{(climbSteering.Jump ? 1 : 0)}";
        return true;
    }

    private static (float X, float Y) ResolveAtaliaPointClimbTarget(ControlPointState point, float side, int stage) =>
        stage switch
        {
            1 => (point.HealingAuraCenterX + (side * 94f), point.HealingAuraCenterY + 57f),
            _ => (point.HealingAuraCenterX + (side * 34f), point.HealingAuraCenterY - 9f),
        };

    private static float ResolveAtaliaPointClimbSide(ControlPointState point) =>
        point.HealingAuraCenterX >= 2500f ? 1f : -1f;

    private static bool IsAtaliaPointClimbJumpReady(PlayerEntity self, ControlPointState point, float side, int moveDirection)
    {
        if (moveDirection == 0 || self.HorizontalSpeed * moveDirection < AtaliaPointClimbRunupSpeed)
        {
            return false;
        }

        var innerX = point.HealingAuraCenterX + (side * 68f);
        var outerX = point.HealingAuraCenterX + (side * 108f);
        return side > 0f
            ? self.X >= innerX && self.X <= outerX
            : self.X <= innerX && self.X >= outerX;
    }

    private static bool IsAtaliaPointClimbCandidate(SimulationWorld world, PlayerEntity self, ControlPointState point)
    {
        if (!world.Level.Name.Contains("Atalia", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var dx = MathF.Abs(self.X - point.HealingAuraCenterX);
        var dy = self.Y - point.HealingAuraCenterY;
        return dx <= AtaliaPointClimbHorizontalRange
            && dy >= AtaliaPointClimbVerticalMin
            && dy <= AtaliaPointClimbVerticalMax
            && !world.IsPlayerInControlPointCaptureZone(self, point.Index);
    }

    private bool TryResolveAtaliaCentralRecoveryDrive(
        SimulationWorld world,
        PlayerEntity self,
        ControlPointState point,
        SteeringOutput steeringOutput,
        out SteeringOutput recoverySteering,
        out string trace)
    {
        return TryResolveAtaliaCentralRecoveryDrive(
            world,
            self,
            point.HealingAuraCenterX,
            point.HealingAuraCenterY,
            steeringOutput,
            out recoverySteering,
            out trace);
    }

    private bool TryResolveAtaliaCentralRecoveryDrive(
        SimulationWorld world,
        PlayerEntity self,
        float goalX,
        float goalY,
        SteeringOutput steeringOutput,
        out SteeringOutput recoverySteering,
        out string trace)
    {
        recoverySteering = steeringOutput;
        trace = string.Empty;
        if (!IsAtaliaCentralRecoveryCandidate(world, self, goalX))
        {
            _ataliaCentralRecoveryStage = 0;
            return false;
        }

        if (goalX >= 2500f)
        {
            ApplyAtaliaRightObjectiveRecoverySteering(self, goalX, goalY, ref recoverySteering, out trace);
            return true;
        }

        if (_ataliaCentralRecoveryStage <= 0)
        {
            _ataliaCentralRecoveryStage = 1;
        }

        if (_ataliaCentralRecoveryStage == 1 && self.IsGrounded && self.X <= 2552f)
        {
            _ataliaCentralRecoveryStage = 2;
        }

        var moveDirection = _ataliaCentralRecoveryStage == 1 ? -1 : 1;
        recoverySteering.MoveDirection = moveDirection;
        recoverySteering.DropDown = false;
        recoverySteering.Jump = self.IsGrounded
            && _ataliaCentralRecoveryStage >= 2
            && self.X >= 2539f
            && self.X <= 2586f
            && self.HorizontalSpeed >= AtaliaCentralRecoveryRunupSpeed;
        trace = $"ataliaCentralRecovery stage:{_ataliaCentralRecoveryStage} dx:{goalX - self.X:0.0} dy:{goalY - self.Y:0.0} move:{moveDirection} jump:{(recoverySteering.Jump ? 1 : 0)}";
        return true;
    }

    private static bool IsAtaliaCentralRecoveryCandidate(SimulationWorld world, PlayerEntity self, float goalX)
    {
        if (!IsAtaliaObjectiveRecovery(world, goalX))
        {
            return false;
        }

        if (goalX < 2500f)
        {
            return self.X >= 2528f
                && self.X <= 2640f
                && self.Y >= 1180f
                && self.Y <= 1278f;
        }

        return self.X >= 2290f
            && self.X <= 2392f
            && self.Y >= 1180f
            && self.Y <= 1278f;
    }

    private static bool IsAtaliaObjectiveRecovery(SimulationWorld world, float goalX) =>
        world.Level.Name.Contains("Atalia", StringComparison.OrdinalIgnoreCase);

    private static void ApplyAtaliaRightObjectiveRecoverySteering(
        PlayerEntity self,
        float goalX,
        float goalY,
        ref SteeringOutput recoverySteering,
        out string trace)
    {
        recoverySteering.MoveDirection = -1;
        recoverySteering.DropDown = false;
        recoverySteering.Jump = self.IsGrounded
            && self.X >= 2334f
            && self.X <= 2380f
            && self.HorizontalSpeed <= -80f;
        trace = $"ataliaCentralRecovery stage:right dx:{goalX - self.X:0.0} dy:{goalY - self.Y:0.0} move:-1 jump:{(recoverySteering.Jump ? 1 : 0)}";
    }

    private bool TryResolveAtaliaUpperMidJumpDrive(
        SimulationWorld world,
        PlayerEntity self,
        SteeringOutput steeringOutput,
        out SteeringOutput edgeSteering,
        out string trace)
    {
        edgeSteering = steeringOutput;
        trace = string.Empty;
        if (self.ClassId == PlayerClass.Scout
            || !world.Level.Name.Contains("Atalia", StringComparison.OrdinalIgnoreCase)
            || _navGraph is null
            || _currentPath is null
            || !_currentPath.TryGetCurrentEdge(out var edge)
            || _currentPath.CurrentIndex <= 0)
        {
            return false;
        }

        var fromNode = _currentPath.GetWaypoint(_currentPath.CurrentIndex - 1);
        var from = _navGraph.GetNode(fromNode);
        var to = _navGraph.GetNode(edge.ToNode);
        if (IsAtaliaUpperMidRelay(from, to, fromX: 2597f, fromY: 1194f, toX: 2554f, toY: 1122f)
            && self.X >= 2528f
            && self.X <= 2630f
            && self.Y >= 1180f
            && self.Y <= 1232f)
        {
            if (self.X < 2608f)
            {
                edgeSteering.MoveDirection = 1;
                edgeSteering.Jump = false;
                edgeSteering.DropDown = false;
                trace = $"ataliaUpperMidJump edge:{fromNode}->{edge.ToNode} stage:right dx:{2554f - self.X:0.0} dy:{1122f - self.Y:0.0} move:1";
                return true;
            }

            edgeSteering.MoveDirection = -1;
            edgeSteering.DropDown = false;
            edgeSteering.Jump = self.IsGrounded
                && self.X <= 2624f
                && self.HorizontalSpeed <= -52f;
            trace = $"ataliaUpperMidJump edge:{fromNode}->{edge.ToNode} dx:{2554f - self.X:0.0} dy:{1122f - self.Y:0.0} move:-1 jump:{(edgeSteering.Jump ? 1 : 0)} speed:{self.HorizontalSpeed:0.0}";
            return true;
        }

        if (IsAtaliaUpperMidRelay(from, to, fromX: 2284f, fromY: 1158f, toX: 2366f, toY: 1122f)
            && self.X >= 2248f
            && self.X <= 2384f
            && self.Y >= 1132f
            && self.Y <= 1232f)
        {
            if (self.X > 2276f)
            {
                edgeSteering.MoveDirection = -1;
                edgeSteering.Jump = false;
                edgeSteering.DropDown = false;
                trace = $"ataliaUpperMidJump edge:{fromNode}->{edge.ToNode} stage:left dx:{2366f - self.X:0.0} dy:{1122f - self.Y:0.0} move:-1";
                return true;
            }

            edgeSteering.MoveDirection = 1;
            edgeSteering.DropDown = false;
            edgeSteering.Jump = self.IsGrounded
                && self.HorizontalSpeed >= 52f;
            trace = $"ataliaUpperMidJump edge:{fromNode}->{edge.ToNode} dx:{2366f - self.X:0.0} dy:{1122f - self.Y:0.0} move:1 jump:{(edgeSteering.Jump ? 1 : 0)} speed:{self.HorizontalSpeed:0.0}";
            return true;
        }

        if (fromNode == 70
            && edge.ToNode == 115
            && edge.Kind == NavEdgeKind.Fall
            && self.X >= 1668f
            && self.X <= 1724f
            && self.Y >= 620f
            && self.Y <= 650f)
        {
            edgeSteering.MoveDirection = 1;
            edgeSteering.Jump = false;
            edgeSteering.DropDown = false;
            trace = $"ataliaUpperMidJump edge:70->115 dx:{1696f - self.X:0.0} dy:{882f - self.Y:0.0} move:1 drop:0";
            return true;
        }

        if (fromNode == 219
            && edge.ToNode == 188
            && edge.Kind == NavEdgeKind.Jump
            && self.X >= 2588f
            && self.X <= 2630f
            && self.Y >= 1168f
            && self.Y <= 1192f)
        {
            edgeSteering.MoveDirection = -1;
            edgeSteering.DropDown = false;
            edgeSteering.Jump = self.IsGrounded
                && self.X >= 2608f
                && self.X <= 2622f
                && self.HorizontalSpeed <= -52f;
            trace = $"ataliaUpperMidJump edge:219->188 dx:{2554f - self.X:0.0} dy:{1122f - self.Y:0.0} move:-1 jump:{(edgeSteering.Jump ? 1 : 0)} speed:{self.HorizontalSpeed:0.0}";
            return true;
        }

        if (fromNode == 196
            && edge.ToNode == 185
            && edge.Kind == NavEdgeKind.Jump
            && self.X >= 2248f
            && self.X <= 2284f
            && self.Y >= 1132f
            && self.Y <= 1150f)
        {
            edgeSteering.MoveDirection = 1;
            edgeSteering.DropDown = false;
            edgeSteering.Jump = self.IsGrounded;
            trace = $"ataliaUpperMidJump edge:196->185 dx:{2366f - self.X:0.0} dy:{1122f - self.Y:0.0} move:1 jump:{(edgeSteering.Jump ? 1 : 0)} speed:{self.HorizontalSpeed:0.0}";
            return true;
        }

        if (fromNode == 232
            && edge.ToNode == 206
            && edge.Kind == NavEdgeKind.Jump
            && self.X >= 1238f
            && self.X <= 1290f
            && self.Y >= 1216f
            && self.Y <= 1232f)
        {
            edgeSteering.MoveDirection = 1;
            edgeSteering.DropDown = false;
            edgeSteering.Jump = self.IsGrounded
                && self.X >= 1260f
                && self.X <= 1276f
                && self.HorizontalSpeed >= 72f;
            trace = $"ataliaUpperMidJump edge:232->206 dx:{1281f - self.X:0.0} dy:{1164f - self.Y:0.0} move:1 jump:{(edgeSteering.Jump ? 1 : 0)} speed:{self.HorizontalSpeed:0.0}";
            return true;
        }

        return false;
    }

    private static bool IsAtaliaUpperMidRelay(
        NavNode from,
        NavNode to,
        float fromX,
        float fromY,
        float toX,
        float toY) =>
        MathF.Abs(from.X - fromX) <= 8f
        && MathF.Abs(from.Y - fromY) <= 8f
        && MathF.Abs(to.X - toX) <= 8f
        && MathF.Abs(to.Y - toY) <= 8f;

    private bool ShouldKeepGraphControlForBelowPointClimb(PlayerEntity self, ControlPointState point)
    {
        if (_currentPath is null || _currentPath.IsComplete)
        {
            return false;
        }

        return point.HealingAuraCenterY - self.Y < -24f;
    }

    private static bool TryResolveControlPointHold(
        SimulationWorld world,
        PlayerEntity self,
        ControlPointState point,
        SteeringOutput steeringOutput,
        out SteeringOutput holdSteering,
        out string trace)
    {
        holdSteering = steeringOutput;
        trace = string.Empty;

        var dx = point.HealingAuraCenterX - self.X;
        var dy = point.HealingAuraCenterY - self.Y;
        var inCaptureZone = world.IsPlayerInControlPointCaptureZone(self, point.Index);
        if (!inCaptureZone
            && (MathF.Abs(dx) > CapturePointHoldHorizontalRange || MathF.Abs(dy) > CapturePointHoldVerticalRange))
        {
            return false;
        }

        if (!inCaptureZone && dy < -24f)
        {
            return false;
        }

        holdSteering.MoveDirection = MathF.Abs(dx) <= CapturePointHoldCenterDeadZone
            ? 0
            : dx > 0f ? 1 : -1;
        holdSteering.Jump = steeringOutput.Jump || (self.IsGrounded && dy < -24f);
        holdSteering.DropDown = false;
        trace = $"capturePointHold point:{point.Index} dx:{dx:0.0} dy:{dy:0.0} inZone:{(inCaptureZone ? 1 : 0)} move:{holdSteering.MoveDirection:0} jump:{(holdSteering.Jump ? 1 : 0)}";
        return true;
    }

    private void ApplyCaptureStrafeHop(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        ref SteeringOutput steeringOutput)
    {
        if (!TryFindActivePointBeingCaptured(world, self, team, out var point))
        {
            return;
        }

        var centerBand = Math.Clamp(point.Marker.Width * CaptureStrafeCenterBand, 12f, 24f);
        var dxFromCenter = self.X - point.Marker.CenterX;
        var phase = Math.Abs((_thinkTicks + (point.Index * 7)) % CaptureStrafeHopCycleTicks);
        var inHopWindow = phase >= CaptureStrafeHopSideTicks * 2
            && phase < (CaptureStrafeHopSideTicks * 2) + CaptureStrafeHopWindowTicks;
        var moveDirection = MathF.Abs(dxFromCenter) > centerBand
            ? (dxFromCenter > 0f ? -1 : 1)
            : ResolveCaptureStrafeTapDirection(phase);
        if (moveDirection == 0 && MathF.Abs(self.HorizontalSpeed) > CaptureStrafeBrakeSpeed)
        {
            moveDirection = self.HorizontalSpeed > 0f ? -1 : 1;
        }

        steeringOutput.MoveDirection = moveDirection;
        steeringOutput.DropDown = false;
        if (self.IsGrounded && inHopWindow)
        {
            steeringOutput.Jump = true;
        }

        LastDirectDriveTrace = string.IsNullOrWhiteSpace(LastDirectDriveTrace)
            ? $"captureStrafeHop point:{point.Index} move:{moveDirection} phase:{phase}"
            : $"{LastDirectDriveTrace} captureStrafeHop point:{point.Index} move:{moveDirection} phase:{phase}";
    }

    private static int ResolveCaptureStrafeTapDirection(int phase)
    {
        if (phase < CaptureStrafeHopSideTicks)
        {
            return -1;
        }

        if (phase < CaptureStrafeHopSideTicks * 2)
        {
            return 1;
        }

        return 0;
    }

    private static bool TryFindActivePointBeingCaptured(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        out ControlPointState point)
    {
        point = null!;
        if (world.MatchRules.Mode is not (GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill))
        {
            return false;
        }

        foreach (var candidate in world.ControlPoints)
        {
            if (candidate.IsLocked
                || candidate.Team == team
                || !world.IsPlayerInControlPointCaptureZone(self, candidate.Index))
            {
                continue;
            }

            point = candidate;
            return true;
        }

        return false;
    }

    private static bool ShouldDirectSeekEnemiesAfterKothCapture(SimulationWorld world, PlayerTeam team)
    {
        if (world.MatchRules.Mode != GameModeKind.KingOfTheHill)
        {
            return false;
        }

        foreach (var point in world.ControlPoints)
        {
            if (point.Team == team && point.CappingTeam is null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindNearestUnownedControlPoint(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        float maxDistance,
        out ControlPointState point)
    {
        point = null!;
        var bestDistanceSq = maxDistance * maxDistance;
        var allowLockedPointStaging = world.MatchRules.Mode == GameModeKind.KingOfTheHill;
        foreach (var candidate in world.ControlPoints)
        {
            if ((!allowLockedPointStaging && candidate.IsLocked) || candidate.Team == team)
            {
                continue;
            }

            var dx = candidate.Marker.CenterX - self.X;
            var dy = candidate.Marker.CenterY - self.Y;
            if (MathF.Abs(dy) > CapturePointDirectSeekVerticalRange)
            {
                continue;
            }

            var distanceSq = (dx * dx) + (dy * dy);
            if (distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            point = candidate;
        }

        return point is not null;
    }

    private static bool TryFindNearestEnemyPlayer(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        float maxDistance,
        out PlayerEntity target)
    {
        target = null!;
        var opposingTeam = team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        var bestDistanceSq = maxDistance * maxDistance;
        foreach (var candidate in CombatDecisionResolver.EnumeratePlayers(world))
        {
            if (!candidate.IsAlive || candidate.Id == self.Id)
            {
                continue;
            }

            var treatAsFriendlyFireTarget = SimulationWorld.ShouldTreatPlayerAsExperimentalFriendlyFireTarget(self, candidate);
            if (candidate.Team != opposingTeam && !treatAsFriendlyFireTarget)
            {
                continue;
            }

            if (candidate.ClassId == PlayerClass.Spy && candidate.IsSpyCloaked && !candidate.IsSpyVisibleToEnemies)
            {
                continue;
            }

            var dx = candidate.X - self.X;
            var dy = candidate.Y - self.Y;
            var distanceSq = (dx * dx) + (dy * dy);
            if (distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            target = candidate;
        }

        return target is not null;
    }

    private static bool TryFindNearestIntelCarrier(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        bool opposingCarrier,
        float maxDistance,
        out PlayerEntity target)
    {
        target = null!;
        var bestDistanceSq = maxDistance * maxDistance;
        foreach (var candidate in CombatDecisionResolver.EnumeratePlayers(world))
        {
            if (!candidate.IsAlive
                || candidate.Id == self.Id
                || !candidate.IsCarryingIntel
                || (opposingCarrier ? candidate.Team == team : candidate.Team != team))
            {
                continue;
            }

            if (candidate.ClassId == PlayerClass.Spy && candidate.IsSpyCloaked && !candidate.IsSpyVisibleToEnemies)
            {
                continue;
            }

            var dx = candidate.X - self.X;
            var dy = candidate.Y - self.Y;
            var distanceSq = (dx * dx) + (dy * dy);
            if (distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            target = candidate;
        }

        return target is not null;
    }

    private static TeamIntelligenceState GetEnemyIntelState(SimulationWorld world, PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? world.RedIntel : world.BlueIntel;
    }

    private static TeamIntelligenceState GetOwnIntelState(SimulationWorld world, PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? world.BlueIntel : world.RedIntel;
    }

    private static bool HasOtherAllyAvailableForObjective(PlayerEntity self, SimulationWorld world, PlayerTeam team)
    {
        foreach (var candidate in CombatDecisionResolver.EnumeratePlayers(world))
        {
            if (candidate.IsAlive
                && candidate.Id != self.Id
                && candidate.Team == team
                && candidate.ClassId != PlayerClass.Sniper
                && IsAllyApplyingObjectivePressure(candidate, world, team))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAllyApplyingObjectivePressure(PlayerEntity candidate, SimulationWorld world, PlayerTeam team)
    {
        if (candidate.IsCarryingIntel)
        {
            return true;
        }

        var enemyIntel = GetEnemyIntelState(world, team);
        if (!enemyIntel.IsCarried
            && DistanceBetween(candidate.X, candidate.Y, enemyIntel.X, enemyIntel.Y) <= ObjectiveAllyIntelPressureDistance)
        {
            return true;
        }

        var ownIntel = GetOwnIntelState(world, team);
        return ownIntel.IsDropped
            && DistanceBetween(candidate.X, candidate.Y, ownIntel.X, ownIntel.Y) <= ObjectiveAllyIntelPressureDistance;
    }

    private static float DistanceBetween(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

}
