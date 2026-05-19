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
    private static readonly object NavigationDiagnosticSync = new();
    private static readonly HashSet<string> ReportedNavigationDiagnostics = new(StringComparer.OrdinalIgnoreCase);

    private readonly SteeringMachine _steering = new();
    private readonly AimResolver _aimResolver = new();
    private readonly CombatDecisionMemory _combatMemory = new();
    private readonly ObjectiveTapeExecutor _objectiveTapeExecutor = new();
    private readonly VerifiedNavProofRouteExecutor _proofRouteExecutor = new();
    private readonly LocalMotionController _localMotionController = new();
    private readonly StochasticLocalMotionPlanner _stochasticLocalMotionPlanner = new();
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
    private int _carrierReturnDirectEscapeTicks;
    private int _carrierReturnDirectStuckTicks;
    private float _carrierReturnDirectEscapeDirection;
    private float _carrierReturnDirectCheckX;
    private float _carrierReturnDirectCheckY;
    private BotBrainObjectiveTapeAsset? _objectiveTapeAsset;
    private VerifiedNavProofGraphAsset? _verifiedProofGraphAsset;
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
    private const float DynamicEscortCarrierDirectSeekDistance = 2400f;
    private const int CaptureStrafeHopCycleTicks = 32;
    private const int CaptureStrafeHopSideTicks = 14;
    private const int CaptureStrafeTapTicks = 5;
    private const int CaptureStrafeHopWindowTicks = 4;
    private const float CaptureStrafeCenterBand = 0.08f;
    private const float CaptureStrafeBrakeSpeed = 36f;
    private const float CapturePointLaneSpacing = 18f;
    private const float CapturePointLaneTargetDeadZone = 6f;
    private const float CapturePointLaneBoundaryPadding = 4f;
    private const float CapturePointClusterMinimumDistance = 24f;
    private const float CapturePointClusterVerticalRange = 40f;
    private const float CapturePointDirectSeekDistance = 360f;
    private const float CapturePointDirectSeekVerticalRange = 300f;
    private const float ArenaCaptureDirectDriveHorizontalRange = 420f;
    private const float ArenaCaptureDirectDriveVerticalMin = -320f;
    private const float ArenaCaptureDirectDriveVerticalMax = 80f;
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
    private const float PlatformLadderInitialRunupJumpRange = 36f;
    private const float PlatformLadderInitialRunupSpeed = 60f;
    private const int PlatformLadderDefaultFinalStage = 4;
    private const int PlatformLadderArenaFinalStage = 5;
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
    private const float SoldierCarrierCapFinishRunupDistance = 100f;
    private const float SoldierCarrierCapFinishStuckSpeed = 8f;
    private const int SoldierCarrierCapFinishRunupTicks = 14;
    private const int SoldierCarrierCapFinishAttackTicks = 36;
    private const float DirectSeekRouteVerticalThreshold = 80f;
    private const float DirectSeekRouteGoalVerticalSlack = 72f;
    private const float DirectSeekRouteGoalHorizontalSlack = 220f;
    private const float CarrierReturnRouteGoalProxyMaxDistance = 520f;
    private const float CarrierReturnRouteGoalProxyMaxHorizontalDistance = 420f;
    private const float OrangeCarrierFinishHorizontalRange = 380f;
    private const float OrangeCarrierFinishBottomRange = 280f;
    private const float DroppedIntelPrimitiveDirectSeekDistance = 220f;
    private const float DroppedIntelPrimitiveDirectSeekVerticalRange = 96f;
    private const float DroppedIntelNearHoldDistance = 96f;
    private const float DroppedIntelNearHorizontalDeadZone = 4f;
    private const float ProofRouteAttachmentMaxDistance = 1800f;
    private const float ProofRouteAttachmentEgressBelowThreshold = 48f;
    private const float ProofRouteAttachmentEgressSurfaceTolerance = 18f;
    private const float ProofRouteAttachmentEgressOvershoot = 48f;
    private const int CarrierReturnDirectStuckWindowTicks = 30;
    private const int CarrierReturnDirectEscapeTicks = 42;
    private const float CarrierReturnDirectStuckMovement = 10f;
    private const float CarrierReturnDirectEscapeMaxHorizontalDistance = 900f;
    private const float StaleFirstWaypointHorizontalDistance = 220f;
    private const float StaleFirstWaypointVerticalDistance = 160f;
    private const float EngineerCtfDefenseHoldRadius = 96f;
    private const float EngineerCtfSentryBuildRadius = 88f;
    private const float EngineerCtfSentryDefendedRadius = 220f;
    private const int EngineerCtfBuildRetryIntervalTicks = 30;
    private const float EngineerControlPointSentryBuildRadius = 88f;
    private const float EngineerControlPointSentryDefendedRadius = 220f;
    private const int EngineerControlPointBuildRetryIntervalTicks = 30;
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
    private bool _lastCarryingIntel;
    private int _lastObservedDeaths = -1;
    private bool _wasAliveLastThink;

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

    public bool PreferEnemyPlayerObjective { get; set; }

    public bool HasNavigationGraph => _navGraph is not null;

    public bool HasObjectiveTapeAsset => _objectiveTapeAsset is not null;

    public bool HasActivePath => _currentPath is not null && !_currentPath.IsComplete;

    public SteeringOutput LastSteeringOutput { get; private set; }

    public int? LastMedicHealTargetId { get; private set; }

    public bool LastMedicHealTargetIsPocket { get; private set; }

    public string LastSemanticRecoveryTrace { get; private set; } = string.Empty;

    public string LastDirectDriveTrace { get; private set; } = string.Empty;

    public string LastObjectiveTapeTrace { get; private set; } = string.Empty;

    public string LastProofGraphTrace { get; private set; } = string.Empty;

    public string LastTraversalTrace
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LastObjectiveTapeTrace))
            {
                return LastProofGraphTrace;
            }

            if (string.IsNullOrWhiteSpace(LastProofGraphTrace))
            {
                return LastObjectiveTapeTrace;
            }

            return $"{LastObjectiveTapeTrace} {LastProofGraphTrace}";
        }
    }

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
        LastProofGraphTrace = string.Empty;
        _thinkTicks += 1;
        var proofGraphRequired = !PreferEnemyPlayerObjective && VerifiedNavProofGraphAssetStore.IsRequired();

        // Rebuild nav graph if the level changed (map rotation).
        if (_lastLevel != world.Level)
        {
            _navGraph = _graphOverride ?? (BotNavigationAssetStore.TryLoadCachedGraph(world.Level, out var graph)
                ? graph
                : null);
            _objectiveTapeAsset = BotBrainObjectiveTapeStore.TryLoad(world.Level, out var tapeAsset)
                ? tapeAsset
                : null;
            _verifiedProofGraphAsset = VerifiedNavProofGraphAssetStore.TryLoad(world.Level, team, self.ClassId, out var proofGraphAsset)
                ? proofGraphAsset
                : null;
            _lastLevel = world.Level;
            _currentPath = null;
            _goalNodeIndex = -1;
            _steering.Reset();
            _objectiveTapeExecutor.Reset();
            _proofRouteExecutor.Reset();
            _stochasticLocalMotionPlanner.Reset();
            _lastCarryingIntel = self.IsCarryingIntel;

            ReportNavigationLoadDiagnostic(world.Level, _navGraph is not null);
        }

        if (_navGraph is null || !self.IsAlive)
        {
            if (!self.IsAlive && (_wasAliveLastThink || _lastObservedDeaths != self.Deaths))
            {
                ResetTransientNavigationStateForNewLife();
            }

            _lastObservedDeaths = self.Deaths;
            _wasAliveLastThink = false;
            _previousInput = default;
            LastSteeringOutput = default;
            return default;
        }

        if (!_wasAliveLastThink || _lastObservedDeaths != self.Deaths)
        {
            ResetTransientNavigationStateForNewLife();
        }

        _lastObservedDeaths = self.Deaths;
        _wasAliveLastThink = true;

        DecayBlockedEdges();
        var engineerCtfDefender = IsCaptureTheFlagEngineerDefender(world, self);
        if (engineerCtfDefender)
        {
            _objectiveTapeExecutor.Reset();
        }

        // 1. Select combat/heal targets.
        var combatTarget = TargetSelector.SelectCombatTarget(self, world, team);
        PlayerEntity? preferredEnemyObjectiveTarget = null;
        if (PreferEnemyPlayerObjective
            && TryFindNearestEnemyPlayer(world, self, team, float.PositiveInfinity, out var preferredEnemy))
        {
            preferredEnemyObjectiveTarget = preferredEnemy;
        }

        var healTargetSelection = CombatDecisionResolver.FindBestMedicHealTargetSelection(world, self, team);
        var healTarget = healTargetSelection.Target;
        LastMedicHealTargetId = healTarget?.Id;
        LastMedicHealTargetIsPocket = healTargetSelection.Kind == MedicHealTargetSelectionKind.Pocket;

        // 2. Evaluate objective (throttled).
        if (self.IsCarryingIntel != _lastCarryingIntel)
        {
            _objectiveReevalCooldown = 0;
            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = 0;
            _steering.Reset();
            _stochasticLocalMotionPlanner.Reset();
        }

        _objectiveReevalCooldown--;
        if (_objectiveReevalCooldown <= 0 || combatTarget is not null)
        {
            var previousGoalPosition = _currentGoalPosition;
            _currentGoalPosition = preferredEnemyObjectiveTarget is not null
                ? (preferredEnemyObjectiveTarget.X, preferredEnemyObjectiveTarget.Y)
                : ObjectiveEvaluator.EvaluateGoal(self, world, team, combatTarget?.Player);
            if (_proofRouteExecutor.IsActive
                && DistanceBetween(previousGoalPosition.X, previousGoalPosition.Y, _currentGoalPosition.X, _currentGoalPosition.Y) > 96f)
            {
                _proofRouteExecutor.Reset();
            }

            _objectiveReevalCooldown = ObjectiveReevalIntervalTicks;
        }

        _lastCarryingIntel = self.IsCarryingIntel;

        // 3. Find/update path.
        var graphSuspendedForPointCapture = ShouldSuspendGraphRoutingForControlPointCapture(world, self, team);
        var bypassCarrierReturnProofGraph = ShouldBypassCarrierReturnProofGraph(world, self, proofGraphRequired);
        if (bypassCarrierReturnProofGraph)
        {
            _proofRouteExecutor.Reset();
        }

        var proofGraphOwnsMovement = _proofRouteExecutor.IsActive;
        var tapeOwnsMovement = _objectiveTapeExecutor.IsActive;
        if (graphSuspendedForPointCapture)
        {
            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = RepathIntervalTicks;
            _steering.Reset();
        }
        else if ((!proofGraphRequired || engineerCtfDefender) && !proofGraphOwnsMovement && !tapeOwnsMovement)
        {
            UpdatePath(self, team);
        }

        var routeMissingAfterUpdate = _currentPath is null || _currentPath.IsComplete;
        var steeringOutput = new SteeringOutput();
        PlayerInputSnapshot? inputOverride = null;
        var dynamicCtfSteering = steeringOutput;
        var dynamicCtfTrace = string.Empty;
        var dynamicCtfResolved = !PreferEnemyPlayerObjective
            && !engineerCtfDefender
            && TryResolveCaptureTheFlagDynamicObjectiveSeek(
            world,
            self,
            team,
            steeringOutput,
            out dynamicCtfSteering,
            out dynamicCtfTrace);
        if (dynamicCtfResolved)
        {
            steeringOutput = dynamicCtfSteering;
            LastDirectDriveTrace = dynamicCtfTrace;
            _repathCooldownTicks = 0;
            _proofRouteExecutor.Reset();
        }

        var proofSteering = steeringOutput;
        var proofResolved = !engineerCtfDefender
            && !dynamicCtfResolved
            && !bypassCarrierReturnProofGraph
            && _proofRouteExecutor.TryResolve(
            _verifiedProofGraphAsset,
            self,
            team,
            _thinkTicks,
            steeringOutput,
            out proofSteering);
        if (proofResolved)
        {
            steeringOutput = proofSteering;
            LastProofGraphTrace = _proofRouteExecutor.LastTrace;
            if (TryResolveProofTerminalObjectiveFinish(world, self, team, steeringOutput, out var terminalFinishSteering, out var terminalFinishTrace))
            {
                steeringOutput = terminalFinishSteering;
                LastDirectDriveTrace = terminalFinishTrace;
            }

            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = 0;
            _steering.Reset();
            ResetCarrierReturnDirectEscape();
        }
        else if (IsProofGraphHandoffTrace(_proofRouteExecutor.LastTrace))
        {
            LastProofGraphTrace = _proofRouteExecutor.LastTrace;
            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = 0;
            _steering.Reset();
            if (!proofGraphRequired)
            {
                UpdatePath(self, team);
            }
        }
        else if (_proofRouteExecutor.LastTrace.StartsWith("proofGraph=idle", StringComparison.Ordinal)
            || _proofRouteExecutor.LastTrace.StartsWith("proofGraph=selected", StringComparison.Ordinal))
        {
            LastProofGraphTrace = _proofRouteExecutor.LastTrace;
        }

        if (!proofResolved
            && !PreferEnemyPlayerObjective
            && !engineerCtfDefender
            && self.IsCarryingIntel
            && TryResolveCaptureTheFlagCarrierReturnSeek(
                world,
                self,
                team,
                steeringOutput,
                out var carrierReturnSteering,
                out var carrierReturnTrace,
                out var carrierReturnInputOverride))
        {
            dynamicCtfResolved = true;
            steeringOutput = carrierReturnSteering;
            inputOverride = carrierReturnInputOverride;
            LastDirectDriveTrace = carrierReturnTrace;
            _repathCooldownTicks = 0;
        }

        var tapeResolved = false;
        if (!PreferEnemyPlayerObjective && !engineerCtfDefender && !proofResolved && !proofGraphRequired)
        {
            tapeResolved = _objectiveTapeExecutor.TryResolve(
                _objectiveTapeAsset,
                _navGraph,
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
                _currentPath = null;
                _goalNodeIndex = -1;
                _repathCooldownTicks = 0;
                _steering.Reset();
            }
            else if (IsObjectiveTapeHandoffTrace(_objectiveTapeExecutor.LastTrace))
            {
                LastObjectiveTapeTrace = _objectiveTapeExecutor.LastTrace;
                _currentPath = null;
                _goalNodeIndex = -1;
                _repathCooldownTicks = 0;
                _steering.Reset();
                UpdatePath(self, team);
            }
            else if (_objectiveTapeExecutor.LastTrace.StartsWith("objectiveTape=idle", StringComparison.Ordinal))
            {
                LastObjectiveTapeTrace = _objectiveTapeExecutor.LastTrace;
            }
        }

        if (!dynamicCtfResolved && !proofResolved && !tapeResolved)
        {
            if (!proofGraphRequired || engineerCtfDefender)
            {
                // 4. Run graph steering only when the objective tape is not actively driving.
                // Otherwise the graph can time out stale path edges while tape input is correctly moving the bot.
                steeringOutput = _steering.Update(self, _navGraph, _currentPath, world.Level, team);
            }
            else if (string.IsNullOrWhiteSpace(LastProofGraphTrace))
            {
                LastProofGraphTrace = _verifiedProofGraphAsset is null
                    ? "proofGraph=idle reason:not_loaded strict:1"
                    : "proofGraph=idle reason:not_active strict:1";
            }
        }

        var routeRecoveryRequested = routeMissingAfterUpdate || steeringOutput.RequestRepath;
        if (!dynamicCtfResolved
            && (!proofGraphRequired || engineerCtfDefender)
            && !proofResolved
            && !tapeResolved
            && TryResolveAtaliaUpperMidJumpDrive(world, self, steeringOutput, out var ataliaEdgeSteering, out var ataliaEdgeTrace))
        {
            steeringOutput = ataliaEdgeSteering;
            LastDirectDriveTrace = ataliaEdgeTrace;
        }

        // Handle repath requests from stuck detection.
        if (!dynamicCtfResolved && (!proofGraphRequired || engineerCtfDefender) && !proofResolved && !tapeResolved && steeringOutput.RequestRepath)
        {
            HandleSteeringRepathRequest(self, team, steeringOutput);
        }

        if (!dynamicCtfResolved
            && !proofGraphRequired
            && !proofResolved
            && tapeResolved
            && self.IsCarryingIntel
            && TryResolveCarrierCapFinishDirectSeek(world, self, team, steeringOutput, out var tapeFinishSteering, out var tapeFinishTrace))
        {
            steeringOutput = tapeFinishSteering;
            LastDirectDriveTrace = tapeFinishTrace;
            tapeResolved = false;
        }

        if (!dynamicCtfResolved
            && (!proofGraphRequired || engineerCtfDefender)
            && (TryResolveSpyRetreat(world, self, team, combatTarget, steeringOutput, out var directSteering, out var directTrace)
                || TryResolveSpyBackstabDrive(world, self, combatTarget, steeringOutput, out directSteering, out directTrace)
                || TryResolveSniperCombatDrive(world, self, combatTarget, steeringOutput, out directSteering, out directTrace)
                || (!proofResolved && !tapeResolved && TryResolveDirectSeek(world, self, team, combatTarget, routeRecoveryRequested, steeringOutput, out directSteering, out directTrace))))
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
        var input = inputOverride.HasValue
            ? ApplyCombatToInputOverride(self, inputOverride.Value, combat)
            : BotInputSynthesizer.Synthesize(self, steeringOutput, aimX, aimY, combat, _previousInput);
        input = ApplyEngineerCaptureTheFlagDefenseInput(world, self, team, input);
        input = ApplyEngineerControlPointInput(world, self, team, input);
        _previousInput = input;
        return input;
    }

    private static void ReportNavigationLoadDiagnostic(SimpleLevel level, bool loaded)
    {
        var key = $"{level.Name}:{level.MapAreaIndex}:{loaded}";
        lock (NavigationDiagnosticSync)
        {
            if (!ReportedNavigationDiagnostics.Add(key))
            {
                return;
            }
        }

        var diagnostic = BotNavigationAssetStore.GetLoadDiagnostic(level);
        var fingerprint = diagnostic.ExpectedFingerprint;
        if (fingerprint.Length > 12)
        {
            fingerprint = fingerprint[..12];
        }

        Console.WriteLine(
            "[botbrain] nav " +
            $"level={level.Name} area={level.MapAreaIndex} loaded={loaded} " +
            $"expectedFingerprint={fingerprint} " +
            $"shipped={diagnostic.ShippedStatus} shippedPath=\"{diagnostic.ShippedPath}\" " +
            $"runtimeCache={diagnostic.RuntimeCacheStatus} runtimeCachePath=\"{diagnostic.RuntimeCachePath}\"");
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
        var exactGoalNode = ShouldPreserveExactControlObjective()
            ? _navGraph.FindNearestReachableNode(
                _currentGoalPosition.X,
                _currentGoalPosition.Y,
                startNode,
                self.ClassId,
                team: team,
                carryingIntel: self.IsCarryingIntel)
            : _navGraph.FindNearestNode(_currentGoalPosition.X, _currentGoalPosition.Y);
        if (startNode < 0 || exactGoalNode < 0)
        {
            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = RepathIntervalTicks;
            return;
        }

        if (exactGoalNode == _goalNodeIndex
            && _currentPath is not null
            && !_currentPath.IsComplete
            && !ShouldReplaceStalePathFromCurrentPosition(self, _navGraph, _currentPath))
        {
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
        var rejectDistantGoalProxy = ShouldRejectCarrierReturnDistantGoalProxy(_lastLevel, self, team, _currentGoalPosition);
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
            refreshedPath = !IsRejectedDistantDirectSeekRouteGoalProxy(_navGraph, goalNode, _currentGoalPosition.X, _currentGoalPosition.Y, rejectDistantGoalProxy)
                && (goalNode != startNode || exactGoalNode == startNode)
                ? _navGraph.FindPath(startNode, goalNode, self.ClassId, activeBlockedEdges, team, self.IsCarryingIntel)
                : null;
        }

        if (refreshedPath is null
            && activeBlockedEdges is not null
            && !preserveExactControlObjective
            && (!self.IsCarryingIntel || !ShouldPreserveCarrierFailedEdgeBlocks(_lastLevel, self)))
        {
            _blockedEdges.Clear();
            activeBlockedEdges = null;
            goalNode = exactGoalNode;
            refreshedPath = _navGraph.FindPath(startNode, goalNode, self.ClassId, team: team, carryingIntel: self.IsCarryingIntel);
        }

        // A fallback reachable goal can still resolve to the current goal even when
        // the exact nearest node changed, so keep the post-fallback reuse guard too.
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

    private static bool IsProofGraphHandoffTrace(string trace)
    {
        return trace.StartsWith("proofGraph=complete", StringComparison.Ordinal)
            || trace.StartsWith("proofGraph=abort", StringComparison.Ordinal);
    }

    private static bool IsRecoverableProofGraphAttachmentTrace(string trace)
    {
        return trace.StartsWith("proofGraph=idle reason:start_outside_tolerance", StringComparison.Ordinal)
            || trace.StartsWith("proofGraph=abort route:Return reason:route_action_no_movement", StringComparison.Ordinal)
            || trace.StartsWith("proofGraph=abort route:Pickup reason:route_action_no_movement", StringComparison.Ordinal);
    }

    private static bool IsRecoverableProofGraphReturnFailureTrace(string trace)
    {
        return trace.StartsWith("proofGraph=idle reason:suppressed route:Return", StringComparison.Ordinal)
            || trace.StartsWith("proofGraph=abort route:Return reason:no_progress", StringComparison.Ordinal)
            || trace.StartsWith("proofGraph=abort route:Return reason:route_action_no_movement", StringComparison.Ordinal);
    }

    private static bool IsProofGraphTerminalTrace(string trace, VerifiedNavProofRouteKind kind) =>
        trace.StartsWith($"proofGraph=terminal route:{kind}", StringComparison.Ordinal);

    private bool ShouldPreserveExactControlObjective()
    {
        return _lastLevel?.Mode is GameModeKind.ControlPoint
            or GameModeKind.Arena
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
        var targetDx = MathF.Abs(targetNode.X - self.X);
        var targetDy = MathF.Abs(targetNode.Y - self.Y);
        if (targetDx > StaleFirstWaypointHorizontalDistance
            && targetDy <= StaleFirstWaypointVerticalDistance)
        {
            return true;
        }

        return targetNode.Y <= self.Y - 40f
            && targetDx < 128f;
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
        _navGraph = null;
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
        ResetCarrierReturnDirectEscape();
        _objectiveReevalCooldown = 0;
        _lastLevel = null;
        _objectiveTapeAsset = null;
        _verifiedProofGraphAsset = null;
        _currentGoalPosition = default;
        _lastCarryingIntel = false;
        _steering.Reset();
        _stochasticLocalMotionPlanner.Reset();
        _previousInput = default;
        LastSteeringOutput = default;
        LastSemanticRecoveryTrace = string.Empty;
        LastDirectDriveTrace = string.Empty;
        LastObjectiveTapeTrace = string.Empty;
        LastProofGraphTrace = string.Empty;
        _blockedEdges.Clear();
        _objectiveTapeExecutor.Reset();
        _proofRouteExecutor.Reset();
        _combatMemory.BeenHealingTicks = 0;
        _combatMemory.ReloadCounterTicks = 0;
        _combatMemory.ZoomToShootTicks = 50;
        _lastObservedDeaths = -1;
        _wasAliveLastThink = false;
        LastMedicHealTargetId = null;
        LastMedicHealTargetIsPocket = false;
    }

    private void ResetTransientNavigationStateForNewLife()
    {
        _currentPath = null;
        _goalNodeIndex = -1;
        _repathCooldownTicks = 0;
        _carrierCapFinishRunupUntilTick = 0;
        _carrierCapFinishAttackUntilTick = 0;
        _platformLadderStage = 0;
        _platformLadderSide = 0f;
        _ataliaPointClimbStage = 0;
        _ataliaPointClimbSide = 0f;
        _ataliaCentralRecoveryStage = 0;
        ResetCarrierReturnDirectEscape();
        _objectiveReevalCooldown = 0;
        _currentGoalPosition = default;
        _lastCarryingIntel = false;
        _steering.Reset();
        _stochasticLocalMotionPlanner.Reset();
        _previousInput = default;
        LastSteeringOutput = default;
        LastSemanticRecoveryTrace = string.Empty;
        LastDirectDriveTrace = string.Empty;
        LastObjectiveTapeTrace = string.Empty;
        LastProofGraphTrace = string.Empty;
        _blockedEdges.Clear();
        _objectiveTapeExecutor.Reset();
        _proofRouteExecutor.Reset();
        _combatMemory.BeenHealingTicks = 0;
        _combatMemory.ReloadCounterTicks = 0;
        _combatMemory.ZoomToShootTicks = 50;
    }

    private PlayerInputSnapshot ApplyCombatToInputOverride(
        PlayerEntity self,
        PlayerInputSnapshot input,
        CombatFireDecision combat) =>
        BotInputSynthesizer.ApplyCombat(self, input, combat, _previousInput);

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

    private void HandleSteeringRepathRequest(PlayerEntity self, PlayerTeam team, SteeringOutput steeringOutput)
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
        if (PreferEnemyPlayerObjective)
        {
            return TryResolvePreferredEnemyPlayerSeek(
                world,
                self,
                team,
                combatTarget,
                steeringOutput,
                out directSteering,
                out directTrace);
        }

        if (TryResolveArenaCaptureZoneDirectDrive(world, self, routeRecoveryRequested, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (TryResolveCaptureTheFlagEngineerDefenseSeek(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

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
                || TryResolveLocalMotionRecovery(
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
                || TryResolveLocalMotionRecovery(
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

        var objectiveRouteRejectTrace = string.Empty;
        if (IsObjectiveRouteRecoveryEnabled()
            && world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            && self.IsCarryingIntel)
        {
            if (TryRouteToDirectSeekTarget(
                    world,
                    self,
                    team,
                    _currentGoalPosition.X,
                    _currentGoalPosition.Y,
                    "recoveryObjectiveRoute",
                    steeringOutput,
                    out directSteering,
                    out directTrace,
                    requireVerticalSeparation: false,
                    traceFailure: true))
            {
                return true;
            }

            objectiveRouteRejectTrace = directTrace;
        }

        var primitiveResolved = TryResolveLocalMotionRecovery(
            world,
            self,
            new DirectDriveTarget(DirectDriveTargetKind.Objective, _currentGoalPosition.X, _currentGoalPosition.Y, "recoveryObjective"),
            steeringOutput,
            out directSteering,
            out directTrace);
        if (primitiveResolved
            && !string.IsNullOrWhiteSpace(objectiveRouteRejectTrace))
        {
            directTrace = $"{directTrace} {objectiveRouteRejectTrace}";
        }

        return primitiveResolved;
    }

    private bool TryResolvePreferredEnemyPlayerSeek(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        BotBrainCombatTarget? combatTarget,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        PlayerEntity? target = combatTarget is { Kind: BotBrainCombatTargetKind.Player, Player: { } combatPlayer }
            ? combatPlayer
            : null;
        if (target is null
            && TryFindNearestEnemyPlayer(world, self, team, float.PositiveInfinity, out var nearestEnemy))
        {
            target = nearestEnemy;
        }

        if (target is null)
        {
            directSteering = steeringOutput;
            directTrace = string.Empty;
            return false;
        }

        return TryRouteToDirectSeekTarget(
                world,
                self,
                team,
                target.X,
                target.Y,
                $"preferredEnemy player:{target.Id}",
                steeringOutput,
                out directSteering,
                out directTrace,
                requireVerticalSeparation: false,
                rejectDistantGoalProxy: false)
            || TryResolveLocalMotionRecovery(
                world,
                self,
                new DirectDriveTarget(DirectDriveTargetKind.Enemy, target.X, target.Y, $"preferredEnemy player:{target.Id}"),
                steeringOutput,
                out directSteering,
                out directTrace);
    }

    private static bool IsObjectiveRouteRecoveryEnabled() =>
        Environment.GetEnvironmentVariable("BOTBRAIN_ROUTE_RECOVERY_OBJECTIVE") is "1" or "true" or "TRUE";

    private bool TryResolveLocalMotionRecovery(
        SimulationWorld world,
        PlayerEntity self,
        DirectDriveTarget target,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace) =>
        _localMotionController.TryResolveRecovery(
            world,
            self,
            target,
            steeringOutput,
            _thinkTicks,
            out directSteering,
            out directTrace);

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

    private bool TryResolveArenaCaptureZoneDirectDrive(
        SimulationWorld world,
        PlayerEntity self,
        bool routeRecoveryRequested,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (world.MatchRules.Mode != GameModeKind.Arena
            || !TryResolveCaptureZoneUnion(world, out var centerX, out var centerY, out _, out _))
        {
            return false;
        }

        var dx = centerX - self.X;
        var dy = centerY - self.Y;
        var inCaptureZone = IsPlayerInArenaCaptureZone(world, self);
        if (!inCaptureZone
            && !routeRecoveryRequested
            && _currentPath is { IsComplete: false, RemainingCount: > 2 })
        {
            _platformLadderStage = 0;
            _platformLadderSide = 0f;
            return false;
        }

        if (!inCaptureZone
            && (MathF.Abs(dx) > ArenaCaptureDirectDriveHorizontalRange
                || dy < ArenaCaptureDirectDriveVerticalMin
                || dy > ArenaCaptureDirectDriveVerticalMax))
        {
            return false;
        }

        if (inCaptureZone)
        {
            directSteering.MoveDirection = MathF.Abs(dx) <= CapturePointHoldCenterDeadZone
                ? 0
                : dx > 0f ? 1 : -1;
            directSteering.Jump = false;
            directSteering.DropDown = false;
            directTrace = $"arenaCaptureHold dx:{dx:0.0} dy:{dy:0.0} move:{directSteering.MoveDirection:0} jump:{(directSteering.Jump ? 1 : 0)}";
            return true;
        }

        if (_platformLadderStage <= 0 || _platformLadderSide == 0f)
        {
            _platformLadderStage = ResolveInitialPlatformLadderStage(self.Y, centerY);
            _platformLadderSide = self.X <= centerX ? -1f : 1f;
        }

        const bool isArenaLadder = true;
        const int finalStage = PlatformLadderArenaFinalStage;
        var target = ResolvePlatformLadderTarget(centerX, centerY, _platformLadderSide, _platformLadderStage, isArenaLadder);
        if (HasReachedPlatformLadderTarget(self, target, isArenaLadder)
            && _platformLadderStage < finalStage)
        {
            _platformLadderStage += 1;
            target = ResolvePlatformLadderTarget(centerX, centerY, _platformLadderSide, _platformLadderStage, isArenaLadder);
        }

        var targetDx = target.X - self.X;
        var targetDy = target.Y - self.Y;
        directSteering.MoveDirection = ResolvePlatformLadderMoveDirection(targetDx, targetDy, _platformLadderSide, _platformLadderStage);
        directSteering.Jump = self.IsGrounded
            && targetDy < -18f
            && MathF.Abs(targetDx) <= PlatformLadderJumpHorizontalRange
            && IsPlatformLadderJumpReady(world, self, directSteering.MoveDirection, targetDx, _platformLadderStage);
        directSteering.DropDown = false;
        directTrace = $"arenaCaptureLadder stage:{_platformLadderStage}/{finalStage} target:({target.X:0.0},{target.Y:0.0}) dx:{targetDx:0.0} dy:{targetDy:0.0} move:{directSteering.MoveDirection:0} jump:{(directSteering.Jump ? 1 : 0)}";
        return true;
    }

    private bool TryResolveCaptureTheFlagEngineerDefenseSeek(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (!IsCaptureTheFlagEngineerDefender(world, self))
        {
            return false;
        }

        if (self.IsCarryingIntel
            && TryResolveCarrierCapFinishDirectSeek(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            ApplyEngineerCaptureTheFlagDefenseAim(world, team, ResolveEngineerCaptureTheFlagDefenseAnchor(world, team), ref directSteering);
            return true;
        }

        var anchor = ResolveEngineerCaptureTheFlagDefenseAnchor(world, team);
        var dx = anchor.X - self.X;
        var dy = anchor.Y - self.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        var defendedSentry = FindOwnedSentryNear(world, self, anchor.X, anchor.Y, EngineerCtfSentryDefendedRadius);
        if (distance <= EngineerCtfDefenseHoldRadius)
        {
            _proofRouteExecutor.Reset();
            directSteering = steeringOutput;
            directSteering.MoveDirection = MathF.Abs(dx) <= DroppedIntelNearHorizontalDeadZone
                ? 0f
                : dx > 0f ? 1f : -1f;
            directSteering.Jump = self.IsGrounded && dy <= -24f;
            directSteering.DropDown = false;
            ApplyEngineerCaptureTheFlagDefenseAim(world, team, anchor, ref directSteering);
            directTrace = $"engineerIntelDefense=hold team:{team} dx:{dx:0.0} dy:{dy:0.0} dist:{distance:0.0} sentry:{(defendedSentry is null ? 0 : 1)} buildReady:{(ShouldBuildEngineerCaptureTheFlagDefenseSentry(world, self, team) ? 1 : 0)}";
            return true;
        }

        var defenseTarget = new DirectDriveTarget(DirectDriveTargetKind.Intel, anchor.X, anchor.Y, $"engineerIntelDefense team:{team}");
        if (IsEngineerCaptureTheFlagDefenseBaseAnchor(world, team, anchor)
            && _proofRouteExecutor.TryResolve(
                _verifiedProofGraphAsset,
                self,
                team,
                _thinkTicks,
                steeringOutput,
                out directSteering,
                forcedKind: VerifiedNavProofRouteKind.Return))
        {
            ApplyEngineerCaptureTheFlagDefenseAim(world, team, anchor, ref directSteering);
            LastProofGraphTrace = _proofRouteExecutor.LastTrace;
            directTrace = $"engineerIntelDefense=proofReturn team:{team} {_proofRouteExecutor.LastTrace}";
            return true;
        }

        if (ShouldPreferTruefortEngineerCaptureTheFlagDefensePocketRecovery(world, self)
            && TryResolveLocalMotionRecovery(
                world,
                self,
                ResolveTruefortEngineerCaptureTheFlagDefensePocketRecoveryTarget(self, defenseTarget),
                steeringOutput,
                out directSteering,
                out directTrace))
        {
            ApplyEngineerCaptureTheFlagDefenseAim(world, team, anchor, ref directSteering);
            return true;
        }

        if (TryRouteToDirectSeekTarget(
                world,
                self,
                team,
                anchor.X,
                anchor.Y,
                $"engineerIntelDefense team:{team}",
                steeringOutput,
                out directSteering,
                out directTrace,
                requireVerticalSeparation: false))
        {
            ApplyEngineerCaptureTheFlagDefenseAim(world, team, anchor, ref directSteering);
            return true;
        }

        if (TryResolveLocalMotionRecovery(
                world,
                self,
                defenseTarget,
                steeringOutput,
                out directSteering,
                out directTrace))
        {
            ApplyEngineerCaptureTheFlagDefenseAim(world, team, anchor, ref directSteering);
            return true;
        }

        return false;
    }

    private PlayerInputSnapshot ApplyEngineerCaptureTheFlagDefenseInput(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        PlayerInputSnapshot input)
    {
        if (!IsCaptureTheFlagEngineerDefender(world, self))
        {
            return input;
        }

        var buildPulse = ShouldBuildEngineerCaptureTheFlagDefenseSentry(world, self, team)
            && _thinkTicks % EngineerCtfBuildRetryIntervalTicks == 1;
        var destroyPulse = !buildPulse
            && ShouldDestroyMisplacedEngineerCaptureTheFlagSentry(world, self, team)
            && _thinkTicks % EngineerCtfBuildRetryIntervalTicks == 1;
        if (!buildPulse && !destroyPulse)
        {
            return input;
        }

        return input with
        {
            BuildSentry = buildPulse,
            DestroySentry = destroyPulse,
        };
    }

    private PlayerInputSnapshot ApplyEngineerControlPointInput(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        PlayerInputSnapshot input)
    {
        if (!IsControlPointEngineerBuilder(world, self))
        {
            return input;
        }

        var buildPulse = ShouldBuildEngineerControlPointSentry(world, self, team)
            && _thinkTicks % EngineerControlPointBuildRetryIntervalTicks == 1;
        var destroyPulse = !buildPulse
            && ShouldDestroyMisplacedEngineerControlPointSentry(world, self, team)
            && _thinkTicks % EngineerControlPointBuildRetryIntervalTicks == 1;
        if (!buildPulse && !destroyPulse)
        {
            return input;
        }

        return input with
        {
            BuildSentry = input.BuildSentry || buildPulse,
            DestroySentry = input.DestroySentry || destroyPulse,
        };
    }

    private static bool IsCaptureTheFlagEngineerDefender(SimulationWorld world, PlayerEntity self) =>
        world.MatchRules.Mode == GameModeKind.CaptureTheFlag
        && self.ClassId == PlayerClass.Engineer;

    private static bool IsControlPointEngineerBuilder(SimulationWorld world, PlayerEntity self) =>
        world.MatchRules.Mode is GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
        && self.ClassId == PlayerClass.Engineer;

    private static bool ShouldPreferTruefortEngineerCaptureTheFlagDefensePocketRecovery(
        SimulationWorld world,
        PlayerEntity self)
    {
        if (!string.Equals(world.Level.Name, "Truefort", StringComparison.OrdinalIgnoreCase)
            || !IsCaptureTheFlagEngineerDefender(world, self)
            || self.IsCarryingIntel)
        {
            return false;
        }

        return self.Team == PlayerTeam.Blue
            ? IsInTruefortBlueEngineerDefenseWallPocket(self)
            : IsInTruefortEngineerDefenseStairwellPocket(self);
    }

    private static bool IsInTruefortEngineerDefenseStairwellPocket(PlayerEntity self)
    {
        if (self.Team == PlayerTeam.Red)
        {
            return self.X >= 920f && self.X <= 1_170f && self.Y >= 485f && self.Y <= 635f;
        }

        return self.Team == PlayerTeam.Blue
            && self.X >= 4_175f && self.X <= 4_425f
            && self.Y >= 485f
            && self.Y <= 635f;
    }

    private static DirectDriveTarget ResolveTruefortEngineerCaptureTheFlagDefensePocketRecoveryTarget(
        PlayerEntity self,
        DirectDriveTarget defenseTarget)
    {
        if (IsInTruefortBlueEngineerDefenseWallPocket(self))
        {
            return new DirectDriveTarget(
                DirectDriveTargetKind.Objective,
                4_216f,
                504f,
                $"engineerIntelDefensePocketUpper team:{self.Team}");
        }

        return defenseTarget;
    }

    private static bool IsInTruefortBlueEngineerDefenseWallPocket(PlayerEntity self) =>
        self.Team == PlayerTeam.Blue
        && self.X >= 4_210f
        && self.X <= 4_425f
        && self.Y >= 480f
        && self.Y <= 590f;

    private static (float X, float Y) ResolveEngineerCaptureTheFlagDefenseAnchor(SimulationWorld world, PlayerTeam team)
    {
        var ownIntel = GetOwnIntelState(world, team);
        if (ownIntel.IsDropped)
        {
            return (ownIntel.X, ownIntel.Y);
        }

        var ownBase = world.Level.GetIntelBase(team);
        return ownBase.HasValue
            ? (ownBase.Value.X, ownBase.Value.Y)
            : (ownIntel.X, ownIntel.Y);
    }

    private static bool IsEngineerCaptureTheFlagDefenseBaseAnchor(
        SimulationWorld world,
        PlayerTeam team,
        (float X, float Y) anchor)
    {
        var ownBase = world.Level.GetIntelBase(team);
        return ownBase.HasValue
            && DistanceBetween(anchor.X, anchor.Y, ownBase.Value.X, ownBase.Value.Y) <= 8f;
    }

    private static void ApplyEngineerCaptureTheFlagDefenseAim(
        SimulationWorld world,
        PlayerTeam team,
        (float X, float Y) anchor,
        ref SteeringOutput steering)
    {
        var opposingTeam = team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        var enemyBase = world.Level.GetIntelBase(opposingTeam);
        var aimX = enemyBase.HasValue
            ? enemyBase.Value.X
            : anchor.X + (team == PlayerTeam.Red ? 120f : -120f);
        var aimY = enemyBase.HasValue ? enemyBase.Value.Y : anchor.Y;
        steering.HasAimOverride = true;
        steering.AimOverrideX = aimX;
        steering.AimOverrideY = aimY;
    }

    private static bool ShouldBuildEngineerCaptureTheFlagDefenseSentry(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team)
    {
        if (!IsCaptureTheFlagEngineerDefender(world, self)
            || !self.IsAlive
            || !self.IsGrounded
            || !self.CanAffordSentry()
            || self.IsInSpawnRoom)
        {
            return false;
        }

        var anchor = ResolveEngineerCaptureTheFlagDefenseAnchor(world, team);
        if (DistanceBetween(self.X, self.Y, anchor.X, anchor.Y) > EngineerCtfSentryBuildRadius)
        {
            return false;
        }

        return FindOwnedSentryNear(world, self, anchor.X, anchor.Y, EngineerCtfSentryDefendedRadius) is null
            && !HasOwnedSentry(world, self);
    }

    private static bool ShouldDestroyMisplacedEngineerCaptureTheFlagSentry(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team)
    {
        if (!IsCaptureTheFlagEngineerDefender(world, self)
            || !self.IsAlive)
        {
            return false;
        }

        var anchor = ResolveEngineerCaptureTheFlagDefenseAnchor(world, team);
        if (DistanceBetween(self.X, self.Y, anchor.X, anchor.Y) > EngineerCtfSentryBuildRadius)
        {
            return false;
        }

        var ownedSentry = FindOwnedSentry(world, self);
        return ownedSentry is not null
            && DistanceBetween(ownedSentry.X, ownedSentry.Y, anchor.X, anchor.Y) > EngineerCtfSentryDefendedRadius;
    }

    private static bool ShouldBuildEngineerControlPointSentry(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team)
    {
        if (!IsControlPointEngineerBuilder(world, self)
            || !self.IsAlive
            || !self.IsGrounded
            || !self.CanAffordSentry()
            || self.IsInSpawnRoom
            || !TryFindEngineerControlPointSentryAnchor(world, self, team, out var point)
            || !IsEngineerOnControlPointBuildAnchor(world, self, point))
        {
            return false;
        }

        return FindOwnedSentryNear(
                world,
                self,
                point.HealingAuraCenterX,
                point.HealingAuraCenterY,
                EngineerControlPointSentryDefendedRadius) is null
            && !HasOwnedSentry(world, self);
    }

    private static bool ShouldDestroyMisplacedEngineerControlPointSentry(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team)
    {
        if (!IsControlPointEngineerBuilder(world, self)
            || !self.IsAlive
            || !TryFindEngineerControlPointSentryAnchor(world, self, team, out var point)
            || !IsEngineerOnControlPointBuildAnchor(world, self, point))
        {
            return false;
        }

        var ownedSentry = FindOwnedSentry(world, self);
        return ownedSentry is not null
            && DistanceBetween(
                ownedSentry.X,
                ownedSentry.Y,
                point.HealingAuraCenterX,
                point.HealingAuraCenterY) > EngineerControlPointSentryDefendedRadius;
    }

    private static bool IsEngineerOnControlPointBuildAnchor(
        SimulationWorld world,
        PlayerEntity self,
        ControlPointState point) =>
        world.IsPlayerInControlPointCaptureZone(self, point.Index)
        && DistanceBetween(
            self.X,
            self.Y,
            point.HealingAuraCenterX,
            point.HealingAuraCenterY) <= EngineerControlPointSentryBuildRadius;

    private static bool TryFindEngineerControlPointSentryAnchor(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        out ControlPointState point)
    {
        point = null!;
        var bestDistanceSq = float.MaxValue;
        var bestIsCurrentZone = false;
        var allowLockedPointStaging = world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill;
        foreach (var candidate in world.ControlPoints)
        {
            if ((!allowLockedPointStaging && candidate.IsLocked)
                || !IsEngineerControlPointSentryCandidate(world, candidate, team))
            {
                continue;
            }

            var isCurrentZone = world.IsPlayerInControlPointCaptureZone(self, candidate.Index);
            var dx = candidate.HealingAuraCenterX - self.X;
            var dy = candidate.HealingAuraCenterY - self.Y;
            var distanceSq = (dx * dx) + (dy * dy);
            if (point is not null
                && ((bestIsCurrentZone && !isCurrentZone)
                    || (bestIsCurrentZone == isCurrentZone && distanceSq >= bestDistanceSq)))
            {
                continue;
            }

            point = candidate;
            bestDistanceSq = distanceSq;
            bestIsCurrentZone = isCurrentZone;
        }

        return point is not null;
    }

    private static bool IsEngineerControlPointSentryCandidate(
        SimulationWorld world,
        ControlPointState point,
        PlayerTeam team)
    {
        if (point.Team != team)
        {
            return true;
        }

        if (point.CappingTeam.HasValue && point.CappingTeam != team)
        {
            return true;
        }

        return world.MatchRules.Mode == GameModeKind.KingOfTheHill;
    }

    private static bool HasOwnedSentry(SimulationWorld world, PlayerEntity self) =>
        FindOwnedSentry(world, self) is not null;

    private static SentryEntity? FindOwnedSentryNear(
        SimulationWorld world,
        PlayerEntity self,
        float x,
        float y,
        float radius)
    {
        var radiusSquared = radius * radius;
        foreach (var sentry in world.Sentries)
        {
            if (sentry.OwnerPlayerId != self.Id || sentry.Team != self.Team)
            {
                continue;
            }

            var dx = sentry.X - x;
            var dy = sentry.Y - y;
            if ((dx * dx) + (dy * dy) <= radiusSquared)
            {
                return sentry;
            }
        }

        return null;
    }

    private static SentryEntity? FindOwnedSentry(SimulationWorld world, PlayerEntity self)
    {
        foreach (var sentry in world.Sentries)
        {
            if (sentry.OwnerPlayerId == self.Id && sentry.Team == self.Team)
            {
                return sentry;
            }
        }

        return null;
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

        if (self.IsCarryingIntel)
        {
            return false;
        }

        if (TryFindNearestIntelCarrier(world, self, team, opposingCarrier: true, IntelCarrierDirectSeekDistance, out var enemyCarrier))
        {
            var enemyCarrierDistance = DistanceBetween(self.X, self.Y, enemyCarrier.X, enemyCarrier.Y);
            if (enemyCarrierDistance <= EscortCarrierDirectSeekDistance
                && (TryResolveLocalMotionRecovery(
                        world,
                        self,
                        new DirectDriveTarget(DirectDriveTargetKind.Carrier, enemyCarrier.X, enemyCarrier.Y, $"dynamicEnemyCarrier player:{enemyCarrier.Id}"),
                        steeringOutput,
                        out directSteering,
                        out directTrace)
                    || PrimitiveDirectDrive.TryResolveRecovery(
                        world,
                        self,
                        new DirectDriveTarget(DirectDriveTargetKind.Carrier, enemyCarrier.X, enemyCarrier.Y, $"dynamicEnemyCarrierPrimitive player:{enemyCarrier.Id}"),
                        steeringOutput,
                        out directSteering,
                        out directTrace)))
            {
                return true;
            }

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
                || TryResolveLocalMotionRecovery(
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
        if (CanOwnCaptureTheFlagEnemyObjective()
            && enemyIntel.IsDropped
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
                    out directTrace,
                    requireVerticalSeparation: false)
                || TryResolveLocalMotionRecovery(
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
                    out directTrace,
                    requireVerticalSeparation: false)
                || TryResolveLocalMotionRecovery(
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
                || TryResolveLocalMotionRecovery(
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

    private bool TryResolveCaptureTheFlagDynamicObjectiveSeek(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag || self.IsCarryingIntel)
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
                    $"dynamicEnemyCarrier player:{enemyCarrier.Id}",
                    steeringOutput,
                    out directSteering,
                    out directTrace)
                || TryResolveLocalMotionRecovery(
                    world,
                    self,
                    new DirectDriveTarget(DirectDriveTargetKind.Carrier, enemyCarrier.X, enemyCarrier.Y, $"dynamicEnemyCarrier player:{enemyCarrier.Id}"),
                    steeringOutput,
                    out directSteering,
                    out directTrace))
            {
                return true;
            }
        }

        var enemyIntel = GetEnemyIntelState(world, team);
        if (CanOwnCaptureTheFlagEnemyObjective()
            && enemyIntel.IsDropped
            && ShouldDirectSeekDroppedIntel(self, world, team, enemyIntel))
        {
            if (TryResolveDroppedIntelDynamicSeek(
                    world,
                    self,
                    team,
                    enemyIntel,
                    $"dynamicDroppedEnemyIntel team:{enemyIntel.Team}",
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
            if (TryResolveDroppedIntelDynamicSeek(
                    world,
                    self,
                    team,
                    ownIntel,
                    $"dynamicOwnDroppedIntel team:{ownIntel.Team}",
                    steeringOutput,
                    out directSteering,
                    out directTrace))
            {
                return true;
            }
        }

        if (enemyIntel.IsCarried
            && TryFindNearestIntelCarrier(world, self, team, opposingCarrier: false, DynamicEscortCarrierDirectSeekDistance, out var friendlyCarrier))
        {
            var carrierDx = friendlyCarrier.X - self.X;
            var carrierDy = friendlyCarrier.Y - self.Y;
            var carrierDistance = MathF.Sqrt((carrierDx * carrierDx) + (carrierDy * carrierDy));
            if (carrierDistance <= EscortCarrierDirectSeekDistance
                && (TryResolveLocalMotionRecovery(
                        world,
                        self,
                        new DirectDriveTarget(DirectDriveTargetKind.Escort, friendlyCarrier.X, friendlyCarrier.Y, $"dynamicEscortCarrier player:{friendlyCarrier.Id}"),
                        steeringOutput,
                        out directSteering,
                        out directTrace)
                    || PrimitiveDirectDrive.TryResolveRecovery(
                        world,
                        self,
                        new DirectDriveTarget(DirectDriveTargetKind.Escort, friendlyCarrier.X, friendlyCarrier.Y, $"dynamicEscortCarrierPrimitive player:{friendlyCarrier.Id}"),
                        steeringOutput,
                        out directSteering,
                        out directTrace)))
            {
                return true;
            }

            if (TryRouteToDirectSeekTarget(
                    world,
                    self,
                    team,
                    friendlyCarrier.X,
                    friendlyCarrier.Y,
                    $"dynamicEscortCarrier player:{friendlyCarrier.Id}",
                    steeringOutput,
                    out directSteering,
                    out directTrace,
                    requireVerticalSeparation: false)
                || TryResolveLocalMotionRecovery(
                    world,
                    self,
                    new DirectDriveTarget(DirectDriveTargetKind.Escort, friendlyCarrier.X, friendlyCarrier.Y, $"dynamicEscortCarrier player:{friendlyCarrier.Id}"),
                    steeringOutput,
                    out directSteering,
                    out directTrace))
            {
                return true;
            }

            if (carrierDistance <= DroppedIntelNearHoldDistance)
            {
                directSteering = steeringOutput;
                directSteering.MoveDirection = MathF.Abs(carrierDx) > DroppedIntelNearHorizontalDeadZone
                    ? carrierDx > 0f ? 1 : -1
                    : 0;
                directSteering.Jump = carrierDy < -DroppedIntelNearHorizontalDeadZone || steeringOutput.Jump;
                directSteering.DropDown = false;
                directTrace = $"directDrive=dynamicEscortCarrier player:{friendlyCarrier.Id} near dx:{carrierDx:0.0} dy:{carrierDy:0.0} dist:{carrierDistance:0.0} move:{directSteering.MoveDirection:0} jump:{(directSteering.Jump ? 1 : 0)}";
                return true;
            }
        }

        return false;
    }

    private bool TryResolveCaptureTheFlagCarrierReturnSeek(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace,
        out PlayerInputSnapshot? inputOverride)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        inputOverride = null;
        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag || !self.IsCarryingIntel)
        {
            _stochasticLocalMotionPlanner.Reset();
            return false;
        }

        if (TryResolveOrangeCarrierReturnFinish(world, self, team, steeringOutput, out directSteering, out directTrace, out var orangeInputOverride))
        {
            inputOverride = orangeInputOverride;
            return true;
        }

        if (TryResolveCarrierCapFinishDirectSeek(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            _stochasticLocalMotionPlanner.Reset();
            return true;
        }

        var ownBase = world.Level.GetIntelBase(team);
        if (!ownBase.HasValue)
        {
            _stochasticLocalMotionPlanner.Reset();
            return false;
        }

        if (TryResolveTruefortCarrierReturnSpawnLipRecovery(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            _stochasticLocalMotionPlanner.Reset();
            return true;
        }

        var recoverableReturnFailure = IsRecoverableProofGraphReturnFailureTrace(_proofRouteExecutor.LastTrace);
        var preferReturnGraph = ShouldPreferCarrierReturnGraph(world, self);

        if (recoverableReturnFailure
            && !preferReturnGraph
            && TryResolveLocalMotionRecovery(
                world,
                self,
                new DirectDriveTarget(DirectDriveTargetKind.Intel, ownBase.Value.X, ownBase.Value.Y, $"dynamicCarrierReturnBaseAfterProofFailure team:{team}"),
                steeringOutput,
                out directSteering,
                out directTrace))
        {
            ApplyCarrierReturnDirectEscape(self, ownBase.Value.X, ref directSteering, ref directTrace);
            return true;
        }

        if (!preferReturnGraph
            && TryResolveProofRouteAttachmentSeek(
                world,
                self,
                team,
                VerifiedNavProofRouteKind.Return,
                "dynamicCarrierReturnAttach",
                steeringOutput,
                out directSteering,
                out directTrace))
        {
            ResetCarrierReturnDirectEscape();
            return true;
        }

        if (preferReturnGraph
            && ShouldTryTruefortCarrierReturnMirrorTeamRoute(world, self)
            && TryRouteToDirectSeekTarget(
                world,
                self,
                team,
                ownBase.Value.X,
                ownBase.Value.Y,
                $"dynamicCarrierReturnBaseMirrorRoute team:{team}",
                steeringOutput,
                out directSteering,
                out directTrace,
                requireVerticalSeparation: false,
                rejectDistantGoalProxy: preferReturnGraph,
                routeTeamOverride: GetOpposingTeam(team)))
        {
            return true;
        }

        if (preferReturnGraph
            && TryRouteToDirectSeekTarget(
                world,
                self,
                team,
                ownBase.Value.X,
                ownBase.Value.Y,
                recoverableReturnFailure
                    ? $"dynamicCarrierReturnBaseAfterProofFailureRoute team:{team}"
                    : $"dynamicCarrierReturnBaseRoute team:{team}",
                steeringOutput,
                out directSteering,
                out directTrace,
                requireVerticalSeparation: false,
                rejectDistantGoalProxy: preferReturnGraph))
        {
            return true;
        }

        if (TryResolveTruefortCarrierReturnPocketRecovery(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            _stochasticLocalMotionPlanner.Reset();
            return true;
        }

        if (!preferReturnGraph
            && TryResolveLocalMotionRecovery(
                world,
                self,
                new DirectDriveTarget(DirectDriveTargetKind.Intel, ownBase.Value.X, ownBase.Value.Y, $"dynamicCarrierReturnBasePrimitive team:{team}"),
                steeringOutput,
                out directSteering,
                out directTrace))
        {
            ApplyCarrierReturnDirectEscape(self, ownBase.Value.X, ref directSteering, ref directTrace);
            return true;
        }

        if (TryRouteToDirectSeekTarget(
                world,
                self,
                team,
                ownBase.Value.X,
                ownBase.Value.Y,
                $"dynamicCarrierReturnBase team:{team}",
                steeringOutput,
                out directSteering,
                out directTrace,
                requireVerticalSeparation: false,
                rejectDistantGoalProxy: preferReturnGraph))
        {
            return true;
        }

        if (TryResolveLocalMotionRecovery(
                world,
                self,
                new DirectDriveTarget(DirectDriveTargetKind.Intel, ownBase.Value.X, ownBase.Value.Y, $"dynamicCarrierReturnBase team:{team}"),
                steeringOutput,
                out directSteering,
                out directTrace))
        {
            ApplyCarrierReturnDirectEscape(self, ownBase.Value.X, ref directSteering, ref directTrace);
            return true;
        }

        return false;
    }

    private static bool ShouldTryTruefortCarrierReturnMirrorTeamRoute(SimulationWorld world, PlayerEntity self) =>
        string.Equals(world.Level.Name, "Truefort", StringComparison.OrdinalIgnoreCase)
        && world.MatchRules.Mode == GameModeKind.CaptureTheFlag
        && self.IsCarryingIntel;

    private static bool TryResolveTruefortCarrierReturnSpawnLipRecovery(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (!ShouldTryTruefortCarrierReturnMirrorTeamRoute(world, self)
            || team != PlayerTeam.Red)
        {
            return false;
        }

        if (self.X < 3_768f
            || self.X > 3_870f
            || self.Y < 450f
            || self.Y > 482f)
        {
            return false;
        }

        directSteering.MoveDirection = 1;
        directSteering.Jump = self.IsGrounded && self.X < 3_830f;
        directSteering.DropDown = false;
        directSteering.RequestRepath = false;
        directTrace = $"truefortCarrierReturnSpawnLip side:right move:1 jump:{(directSteering.Jump ? 1 : 0)}";
        return true;
    }

    private static bool TryResolveTruefortCarrierReturnPocketRecovery(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (!string.Equals(world.Level.Name, "Truefort", StringComparison.OrdinalIgnoreCase)
            || world.MatchRules.Mode != GameModeKind.CaptureTheFlag
            || !self.IsCarryingIntel)
        {
            return false;
        }

        if (team == PlayerTeam.Red
            && self.X >= 1_320f
            && self.X <= 1_495f
            && self.Y >= 740f
            && self.Y <= 790f)
        {
            directSteering.MoveDirection = 1;
            directSteering.Jump = self.IsGrounded && self.X >= 1_455f;
            directSteering.DropDown = false;
            directSteering.RequestRepath = false;
            directTrace = $"truefortCarrierReturnPocket side:left move:1 jump:{(directSteering.Jump ? 1 : 0)}";
            return true;
        }

        return false;
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team) =>
        team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;

    private bool TryResolveOrangeCarrierReturnFinish(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace,
        out PlayerInputSnapshot inputOverride)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        inputOverride = default;
        if (!string.Equals(world.Level.Name, "Orange", StringComparison.OrdinalIgnoreCase))
        {
            _stochasticLocalMotionPlanner.Reset();
            return false;
        }

        if (self.ClassId == PlayerClass.Soldier)
        {
            _stochasticLocalMotionPlanner.Reset();
            return false;
        }

        var ownBase = world.Level.GetIntelBase(team);
        if (!ownBase.HasValue)
        {
            _stochasticLocalMotionPlanner.Reset();
            return false;
        }

        var targetBottom = ownBase.Value.Y + self.CollisionBottomOffset;
        var dx = ownBase.Value.X - self.X;
        var db = targetBottom - self.Bottom;
        if (MathF.Abs(dx) > OrangeCarrierFinishHorizontalRange
            || MathF.Abs(db) > OrangeCarrierFinishBottomRange)
        {
            _stochasticLocalMotionPlanner.Reset();
            return false;
        }

        var goal = StochasticLocalMotionGoal.FromPoint(
            ownBase.Value.X,
            targetBottom,
            $"orangeCarrierReturnBase team:{team}",
            acceptanceX: 24f,
            acceptanceBottom: 24f);
        if (!_stochasticLocalMotionPlanner.TryResolve(world, self, goal, _thinkTicks, out inputOverride, out var stochasticTrace))
        {
            directTrace =
                $"orangeCarrierCapFinish reject:{stochasticTrace.RejectedReason} dx:{dx:0.0} db:{db:0.0} " +
                $"metric:{stochasticTrace.StartMetric:0.0}->{stochasticTrace.BestMetric:0.0}/{stochasticTrace.FinalMetric:0.0}";
            return false;
        }

        directSteering.MoveDirection = inputOverride.Left == inputOverride.Right
            ? 0f
            : inputOverride.Right ? 1f : -1f;
        directSteering.Jump = inputOverride.Up;
        directSteering.DropDown = inputOverride.Down;
        directSteering.RequestRepath = false;
        directTrace =
            $"orangeCarrierCapFinish source:{stochasticTrace.Source} macro:{stochasticTrace.MacroLabel} " +
            $"dx:{dx:0.0} db:{db:0.0} metric:{stochasticTrace.StartMetric:0.0}->{stochasticTrace.BestMetric:0.0}/{stochasticTrace.FinalMetric:0.0} " +
            $"input:L{(inputOverride.Left ? 1 : 0)}R{(inputOverride.Right ? 1 : 0)}U{(inputOverride.Up ? 1 : 0)}D{(inputOverride.Down ? 1 : 0)}";
        return true;
    }

    private static bool ShouldPreferCarrierReturnGraph(
        SimulationWorld world,
        PlayerEntity self) =>
        ShouldPreferCarrierReturnGraph(world.Level, self);

    private static bool ShouldPreferCarrierReturnGraph(
        SimpleLevel level,
        PlayerEntity self)
    {
        if (string.Equals(level.Name, "Waterway", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(level.Name, "Truefort", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(level.Name, "Conflict", StringComparison.OrdinalIgnoreCase)
            && self.ClassId != PlayerClass.Scout;
    }

    private static bool ShouldBypassCarrierReturnProofGraph(
        SimulationWorld world,
        PlayerEntity self,
        bool proofGraphRequired)
    {
        if (string.Equals(world.Level.Name, "Truefort", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !proofGraphRequired
            && self.IsCarryingIntel
            && world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            && ShouldPreferCarrierReturnGraph(world, self);
    }

    private void ApplyCarrierReturnDirectEscape(
        PlayerEntity self,
        float targetX,
        ref SteeringOutput directSteering,
        ref string directTrace)
    {
        if (MathF.Abs(self.X - targetX) > CarrierReturnDirectEscapeMaxHorizontalDistance)
        {
            ResetCarrierReturnDirectEscape();
            return;
        }

        var moved = DistanceBetween(self.X, self.Y, _carrierReturnDirectCheckX, _carrierReturnDirectCheckY);
        if (_carrierReturnDirectCheckX == 0f && _carrierReturnDirectCheckY == 0f || moved > CarrierReturnDirectStuckMovement)
        {
            _carrierReturnDirectCheckX = self.X;
            _carrierReturnDirectCheckY = self.Y;
            _carrierReturnDirectStuckTicks = 0;
        }
        else
        {
            _carrierReturnDirectStuckTicks += 1;
        }

        if (_carrierReturnDirectEscapeTicks <= 0 && _carrierReturnDirectStuckTicks >= CarrierReturnDirectStuckWindowTicks)
        {
            _carrierReturnDirectEscapeTicks = CarrierReturnDirectEscapeTicks;
            _carrierReturnDirectEscapeDirection = MathF.Sign(self.X - targetX);
            if (_carrierReturnDirectEscapeDirection == 0f)
            {
                _carrierReturnDirectEscapeDirection = directSteering.MoveDirection == 0f
                    ? 1f
                    : -MathF.Sign(directSteering.MoveDirection);
            }

            _carrierReturnDirectStuckTicks = 0;
        }

        if (_carrierReturnDirectEscapeTicks <= 0)
        {
            return;
        }

        _carrierReturnDirectEscapeTicks -= 1;
        directSteering.MoveDirection = _carrierReturnDirectEscapeDirection;
        directSteering.Jump = self.IsGrounded || directSteering.Jump;
        directSteering.DropDown = false;
        directTrace =
            $"{directTrace} escape:carrierReturn ticks:{_carrierReturnDirectEscapeTicks} " +
            $"dir:{_carrierReturnDirectEscapeDirection:0} stuck:{_carrierReturnDirectStuckTicks}";
    }

    private void ResetCarrierReturnDirectEscape()
    {
        _carrierReturnDirectEscapeTicks = 0;
        _carrierReturnDirectStuckTicks = 0;
        _carrierReturnDirectEscapeDirection = 0f;
        _carrierReturnDirectCheckX = 0f;
        _carrierReturnDirectCheckY = 0f;
    }

    private bool TryResolveProofTerminalObjectiveFinish(
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

        if (!self.IsCarryingIntel && IsProofGraphTerminalTrace(_proofRouteExecutor.LastTrace, VerifiedNavProofRouteKind.Pickup))
        {
            var enemyIntel = GetEnemyIntelState(world, team);
            if (enemyIntel.IsCarried)
            {
                return false;
            }

            var distance = DistanceBetween(self.X, self.Y, enemyIntel.X, enemyIntel.Y);
            if (distance > DroppedIntelPrimitiveDirectSeekDistance)
            {
                return false;
            }

            return TryResolveLocalMotionRecovery(
                world,
                self,
                new DirectDriveTarget(DirectDriveTargetKind.Intel, enemyIntel.X, enemyIntel.Y, $"proofTerminalPickupFinish team:{enemyIntel.Team}"),
                steeringOutput,
                out directSteering,
                out directTrace);
        }

        if (self.IsCarryingIntel && IsProofGraphTerminalTrace(_proofRouteExecutor.LastTrace, VerifiedNavProofRouteKind.Return))
        {
            var ownBase = world.Level.GetIntelBase(team);
            if (!ownBase.HasValue)
            {
                return false;
            }

            var distance = DistanceBetween(self.X, self.Y, ownBase.Value.X, ownBase.Value.Y);
            if (distance > DroppedIntelPrimitiveDirectSeekDistance)
            {
                return false;
            }

            return TryResolveLocalMotionRecovery(
                world,
                self,
                new DirectDriveTarget(DirectDriveTargetKind.Intel, ownBase.Value.X, ownBase.Value.Y, $"proofTerminalReturnFinish team:{team}"),
                steeringOutput,
                out directSteering,
                out directTrace);
        }

        return false;
    }

    private bool TryResolveProofRouteAttachmentSeek(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        VerifiedNavProofRouteKind routeKind,
        string label,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (_verifiedProofGraphAsset is null
            || !TryFindNearestProofRouteAttachment(
                _verifiedProofGraphAsset,
                self,
                routeKind,
                out var targetX,
                out var targetBottom,
                out var routeIndex,
                out var elementIndex,
                out var edgeId,
                out var attachmentKind,
                out var attachmentDistance))
        {
            return false;
        }

        var selfBottomOffset = self.Bottom - self.Y;
        if (TryAdjustProofRouteAttachmentForSurfaceEgress(
                _verifiedProofGraphAsset,
                self,
                ref targetX,
                ref targetBottom,
                out var egressTrace))
        {
            attachmentKind = $"{attachmentKind} egress:{egressTrace}";
        }

        var targetY = targetBottom - selfBottomOffset;
        var targetLabel = $"{label} {attachmentKind}:{edgeId} route:{routeIndex} index:{elementIndex}";
        var verticalDelta = targetBottom - self.Bottom;
        if (TryResolveLocalMotionRecovery(
                world,
                self,
                new DirectDriveTarget(DirectDriveTargetKind.Objective, targetX, targetY, targetLabel),
                steeringOutput,
                out directSteering,
                out directTrace))
        {
            return true;
        }

        directTrace = $"directDrive={targetLabel} reject:attachment_entry dx:{targetX - self.X:0.0} dy:{targetY - self.Y:0.0} dist:{attachmentDistance:0.0} vertical:{verticalDelta:0.0}";
        return false;
    }

    private static bool TryAdjustProofRouteAttachmentForSurfaceEgress(
        VerifiedNavProofGraphAsset asset,
        PlayerEntity self,
        ref float targetX,
        ref float targetBottom,
        out string trace)
    {
        trace = string.Empty;
        var surface = asset.Surfaces.FirstOrDefault(surface =>
            self.X >= surface.Left - ProofRouteAttachmentEgressSurfaceTolerance
            && self.X <= surface.Right + ProofRouteAttachmentEgressSurfaceTolerance
            && MathF.Abs(self.Bottom - surface.Top) <= ProofRouteAttachmentEgressSurfaceTolerance);
        if (surface is null)
        {
            return false;
        }

        var targetBelowSurface = targetBottom - surface.Top;
        if (targetBelowSurface < ProofRouteAttachmentEgressBelowThreshold
            || targetX <= surface.Left
            || targetX >= surface.Right)
        {
            return false;
        }

        var distanceToLeft = MathF.Abs(targetX - surface.Left);
        var distanceToRight = MathF.Abs(surface.Right - targetX);
        if (distanceToLeft <= distanceToRight)
        {
            targetX = surface.Left - ProofRouteAttachmentEgressOvershoot;
            targetBottom = surface.Top;
            trace = $"surface:{surface.Id} side:left";
            return true;
        }

        targetX = surface.Right + ProofRouteAttachmentEgressOvershoot;
        targetBottom = surface.Top;
        trace = $"surface:{surface.Id} side:right";
        return true;
    }

    private static bool TryFindNearestProofRouteAttachment(
        VerifiedNavProofGraphAsset asset,
        PlayerEntity self,
        VerifiedNavProofRouteKind routeKind,
        out float targetX,
        out float targetBottom,
        out int routeIndex,
        out int elementIndex,
        out int edgeId,
        out string attachmentKind,
        out float attachmentDistance)
    {
        targetX = 0f;
        targetBottom = 0f;
        routeIndex = -1;
        elementIndex = -1;
        edgeId = -1;
        attachmentKind = string.Empty;
        attachmentDistance = float.PositiveInfinity;

        for (var candidateRouteIndex = 0; candidateRouteIndex < asset.Routes.Count; candidateRouteIndex += 1)
        {
            var route = asset.Routes[candidateRouteIndex];
            if (route.Kind != routeKind)
            {
                continue;
            }

            if (route.Actions.Count > 0
                && (route.LaneSegmentIds is null || route.LaneSegmentIds.Count == 0)
                && route.StartX != 0f
                && route.StartBottom != 0f)
            {
                ConsiderProofRouteAttachment(
                    self,
                    route.StartX,
                    route.StartBottom,
                    candidateRouteIndex,
                    0,
                    route.EdgeIds.Count > 0 ? route.EdgeIds[0] : -1,
                    "routeStart",
                    ref targetX,
                    ref targetBottom,
                    ref routeIndex,
                    ref elementIndex,
                    ref edgeId,
                    ref attachmentKind,
                    ref attachmentDistance);
            }

            for (var candidateEdgeIndex = 0; candidateEdgeIndex < route.EdgeIds.Count; candidateEdgeIndex += 1)
            {
                var edge = asset.Edges.FirstOrDefault(edge => edge.Id == route.EdgeIds[candidateEdgeIndex]);
                if (edge is null)
                {
                    continue;
                }

                ConsiderProofRouteAttachment(
                    self,
                    edge.EntryX,
                    edge.EntryBottom,
                    candidateRouteIndex,
                    candidateEdgeIndex,
                    edge.Id,
                    "edge",
                    ref targetX,
                    ref targetBottom,
                    ref routeIndex,
                    ref elementIndex,
                    ref edgeId,
                    ref attachmentKind,
                    ref attachmentDistance);
            }

            if (route.LaneSegmentIds is null || route.LaneSegmentIds.Count == 0)
            {
                continue;
            }

            for (var candidateSegmentIndex = 0; candidateSegmentIndex < route.LaneSegmentIds.Count; candidateSegmentIndex += 1)
            {
                var segment = asset.LaneSegments.FirstOrDefault(segment => segment.Id == route.LaneSegmentIds[candidateSegmentIndex]);
                if (segment is null)
                {
                    continue;
                }

                ConsiderProofRouteAttachment(
                    self,
                    segment.StartX,
                    segment.StartBottom,
                    candidateRouteIndex,
                    candidateSegmentIndex,
                    segment.EdgeId,
                    "lane",
                    ref targetX,
                    ref targetBottom,
                    ref routeIndex,
                    ref elementIndex,
                    ref edgeId,
                    ref attachmentKind,
                    ref attachmentDistance);
            }
        }

        return attachmentDistance <= ProofRouteAttachmentMaxDistance;
    }

    private static void ConsiderProofRouteAttachment(
        PlayerEntity self,
        float candidateX,
        float candidateBottom,
        int candidateRouteIndex,
        int candidateElementIndex,
        int candidateEdgeId,
        string candidateKind,
        ref float targetX,
        ref float targetBottom,
        ref int routeIndex,
        ref int elementIndex,
        ref int edgeId,
        ref string attachmentKind,
        ref float attachmentDistance)
    {
        var distance = DistanceBetween(self.X, self.Bottom, candidateX, candidateBottom);
        if (distance >= attachmentDistance)
        {
            return;
        }

        targetX = candidateX;
        targetBottom = candidateBottom;
        routeIndex = candidateRouteIndex;
        elementIndex = candidateElementIndex;
        edgeId = candidateEdgeId;
        attachmentKind = candidateKind;
        attachmentDistance = distance;
    }

    private bool TryResolveDroppedIntelDynamicSeek(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        TeamIntelligenceState intel,
        string label,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;

        var dx = intel.X - self.X;
        var dy = intel.Y - self.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        var target = new DirectDriveTarget(DirectDriveTargetKind.Intel, intel.X, intel.Y, label);
        if (label.StartsWith("dynamicDroppedEnemyIntel", StringComparison.Ordinal)
            && distance > DroppedIntelPrimitiveDirectSeekDistance
            && HasProofRoute(VerifiedNavProofRouteKind.Pickup))
        {
            return false;
        }

        if (distance <= DroppedIntelPrimitiveDirectSeekDistance
            && MathF.Abs(dy) <= DroppedIntelPrimitiveDirectSeekVerticalRange
            && TryResolveLocalMotionRecovery(world, self, target, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (distance <= DroppedIntelNearHoldDistance)
        {
            directSteering = steeringOutput;
            directSteering.MoveDirection = MathF.Abs(dx) > DroppedIntelNearHorizontalDeadZone
                ? dx > 0f ? 1 : -1
                : 0;
            directSteering.Jump = dy < -DroppedIntelNearHorizontalDeadZone || steeringOutput.Jump;
            directSteering.DropDown = false;
            directSteering.RequestRepath = false;
            directTrace = $"directDrive={label} near dx:{dx:0.0} dy:{dy:0.0} dist:{distance:0.0} move:{directSteering.MoveDirection:0} jump:{(directSteering.Jump ? 1 : 0)}";
            return true;
        }

        if (TryRouteToDirectSeekTarget(
                world,
                self,
                team,
                intel.X,
                intel.Y,
                label,
                steeringOutput,
                out directSteering,
                out directTrace,
                requireVerticalSeparation: false))
        {
            return true;
        }

        return TryResolveLocalMotionRecovery(world, self, target, steeringOutput, out directSteering, out directTrace);
    }

    private bool HasProofRoute(VerifiedNavProofRouteKind routeKind)
        => _verifiedProofGraphAsset is not null
            && _verifiedProofGraphAsset.Routes.Any(route => route.Kind == routeKind && route.EdgeIds.Count > 0);

    private bool CanOwnCaptureTheFlagEnemyObjective()
        => HasProofRoute(VerifiedNavProofRouteKind.Pickup)
            && HasProofRoute(VerifiedNavProofRouteKind.Return);

    private (float X, float Y) ApplyCaptureTheFlagObjectiveAuthority(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam team,
        (float X, float Y) goal)
    {
        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag
            || self.IsCarryingIntel
            || CanOwnCaptureTheFlagEnemyObjective())
        {
            return goal;
        }

        var ownIntel = GetOwnIntelState(world, team);
        if (ownIntel.IsDropped)
        {
            return (ownIntel.X, ownIntel.Y);
        }

        if (TryFindNearestIntelCarrier(world, self, team, opposingCarrier: true, IntelCarrierDirectSeekDistance, out var enemyCarrier))
        {
            return (enemyCarrier.X, enemyCarrier.Y);
        }

        if (TryFindNearestIntelCarrier(world, self, team, opposingCarrier: false, DynamicEscortCarrierDirectSeekDistance, out var friendlyCarrier))
        {
            return (friendlyCarrier.X, friendlyCarrier.Y);
        }

        var ownBase = world.Level.GetIntelBase(team);
        return ownBase.HasValue
            ? (ownBase.Value.X, ownBase.Value.Y)
            : goal;
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
        bool requireVerticalSeparation = true,
        bool traceFailure = false,
        bool rejectDistantGoalProxy = false,
        PlayerTeam? routeTeamOverride = null)
    {
        routedSteering = currentSteering;
        trace = string.Empty;
        if (_navGraph is null)
        {
            if (traceFailure)
            {
                trace = $"directRoute={label} reject:no_graph";
            }

            return false;
        }

        var dx = targetX - self.X;
        var dy = targetY - self.Y;
        var routeTeam = routeTeamOverride ?? team;
        var routeTeamTrace = routeTeamOverride.HasValue && routeTeamOverride.Value != team
            ? $" routeTeam:{routeTeamOverride.Value}"
            : string.Empty;
        if (requireVerticalSeparation && MathF.Abs(dy) < DirectSeekRouteVerticalThreshold)
        {
            if (traceFailure)
            {
                trace = $"directRoute={label}{routeTeamTrace} reject:vertical dx:{dx:0.0} dy:{dy:0.0}";
            }

            return false;
        }

        if (_currentPath is not null
            && !_currentPath.IsComplete
            && DistanceBetween(_currentGoalPosition.X, _currentGoalPosition.Y, targetX, targetY) <= 8f)
        {
            if (IsRejectedDistantDirectSeekRouteGoalProxy(_navGraph, _goalNodeIndex, targetX, targetY, rejectDistantGoalProxy))
            {
                _currentPath = null;
                _goalNodeIndex = -1;
                _repathCooldownTicks = 0;
                trace = $"directRoute={label}{routeTeamTrace} reject:distant_proxy_reuse dx:{dx:0.0} dy:{dy:0.0}";
                return false;
            }

            routedSteering = _steering.Update(self, _navGraph, _currentPath, world.Level, team);
            if (routedSteering.RequestRepath)
            {
                HandleSteeringRepathRequest(self, team, routedSteering);
                trace = $"directRoute={label}{routeTeamTrace} reject:repath dx:{dx:0.0} dy:{dy:0.0}";
                return false;
            }

            trace = $"directRoute={label}{routeTeamTrace} reuse dx:{dx:0.0} dy:{dy:0.0} path:{_currentPath.Count}";
            return true;
        }

        var startNode = _navGraph.FindNearestTraversalStartNode(
            self.X,
            self.Y,
            ResolveTraversalStartMaxAboveDistance(self, _currentPath is null));
        if (startNode < 0)
        {
            if (traceFailure)
            {
                trace = $"directRoute={label}{routeTeamTrace} reject:no_start dx:{dx:0.0} dy:{dy:0.0}";
            }

            return false;
        }

        var activeBlockedEdges = _blockedEdges.Count > 0
            ? _blockedEdges.Keys.ToHashSet()
            : null;
        var goalNode = _navGraph.FindNearestReachableNode(
            targetX,
            targetY,
            startNode,
            self.ClassId,
            activeBlockedEdges,
            team: routeTeam,
            carryingIntel: self.IsCarryingIntel,
            verticalWeight: 8f,
            penalizeLowerCandidate: true);
        if (goalNode < 0)
        {
            goalNode = _navGraph.FindNearestNode(targetX, targetY);
        }

        if (rejectDistantGoalProxy
            && goalNode >= 0
            && IsDistantDirectSeekRouteGoalProxy(_navGraph.GetNode(goalNode), targetX, targetY))
        {
            if (traceFailure)
            {
                var rejectedGoal = _navGraph.GetNode(goalNode);
                trace = $"directRoute={label}{routeTeamTrace} reject:distant_proxy start:{startNode} goal:{goalNode}@({rejectedGoal.X:0.0},{rejectedGoal.Y:0.0}) target:({targetX:0.0},{targetY:0.0}) dx:{dx:0.0} dy:{dy:0.0}";
            }

            return false;
        }

        if (requireVerticalSeparation
            && goalNode >= 0
            && !IsDirectSeekRouteGoalAcceptable(_navGraph.GetNode(goalNode), self, targetX, targetY))
        {
            if (traceFailure)
            {
                var rejectedGoal = _navGraph.GetNode(goalNode);
                trace = $"directRoute={label}{routeTeamTrace} reject:goal_unacceptable start:{startNode} goal:{goalNode}@({rejectedGoal.X:0.0},{rejectedGoal.Y:0.0}) dx:{dx:0.0} dy:{dy:0.0}";
            }

            return false;
        }

        if (_currentPath is not null
            && !_currentPath.IsComplete
            && goalNode >= 0
            && goalNode == _goalNodeIndex
            && !ShouldReplaceStalePathFromCurrentPosition(self, _navGraph, _currentPath))
        {
            _currentGoalPosition = (targetX, targetY);
            _repathCooldownTicks = RepathIntervalTicks;
            routedSteering = _steering.Update(self, _navGraph, _currentPath, world.Level, team);
            if (routedSteering.RequestRepath)
            {
                HandleSteeringRepathRequest(self, team, routedSteering);
                trace = $"directRoute={label}{routeTeamTrace} reject:repath dx:{dx:0.0} dy:{dy:0.0}";
                return false;
            }

            trace = $"directRoute={label}{routeTeamTrace} reuseGoal dx:{dx:0.0} dy:{dy:0.0} goal:{goalNode} path:{_currentPath.Count}";
            return true;
        }

        var path = _navGraph.FindPath(startNode, goalNode, self.ClassId, activeBlockedEdges, routeTeam, self.IsCarryingIntel);
        if (path is null
            && (ShouldPreferCarrierReturnGraph(world, self)
                || !self.IsCarryingIntel
                || !ShouldPreserveCarrierFailedEdgeBlocks(world.Level, self)))
        {
            path = _navGraph.FindPath(startNode, goalNode, self.ClassId, team: routeTeam, carryingIntel: self.IsCarryingIntel);
        }
        if (path is null || path.Count < 2)
        {
            if (traceFailure)
            {
                trace = $"directRoute={label}{routeTeamTrace} reject:no_path start:{startNode} goal:{goalNode} dx:{dx:0.0} dy:{dy:0.0}";
            }

            return false;
        }

        _currentGoalPosition = (targetX, targetY);
        _currentPath = path;
        _goalNodeIndex = goalNode;
        _repathCooldownTicks = RepathIntervalTicks;
        _steering.Reset();
        routedSteering = _steering.Update(self, _navGraph, _currentPath, world.Level, team);
        if (routedSteering.RequestRepath)
        {
            HandleSteeringRepathRequest(self, team, routedSteering);
            trace = $"directRoute={label}{routeTeamTrace} reject:repath dx:{dx:0.0} dy:{dy:0.0} path:{path.Count}";
            return false;
        }

        trace = $"directRoute={label}{routeTeamTrace} dx:{dx:0.0} dy:{dy:0.0} path:{path.Count}";
        return true;
    }

    private static bool ShouldRejectCarrierReturnDistantGoalProxy(
        SimpleLevel? level,
        PlayerEntity self,
        PlayerTeam team,
        (float X, float Y) target)
    {
        if (level is null
            || !self.IsCarryingIntel
            || !ShouldPreferCarrierReturnGraph(level, self))
        {
            return false;
        }

        var ownBase = level.GetIntelBase(team);
        return ownBase.HasValue
            && DistanceBetween(target.X, target.Y, ownBase.Value.X, ownBase.Value.Y) <= 8f;
    }

    private static bool IsRejectedDistantDirectSeekRouteGoalProxy(
        NavGraph graph,
        int goalNode,
        float targetX,
        float targetY,
        bool rejectDistantGoalProxy)
    {
        return rejectDistantGoalProxy
            && goalNode >= 0
            && IsDistantDirectSeekRouteGoalProxy(graph.GetNode(goalNode), targetX, targetY);
    }

    private static bool IsDistantDirectSeekRouteGoalProxy(NavNode goalNode, float targetX, float targetY)
    {
        var dx = MathF.Abs(goalNode.X - targetX);
        var dy = MathF.Abs(goalNode.Y - targetY);
        return dx > CarrierReturnRouteGoalProxyMaxHorizontalDistance
            && MathF.Sqrt((dx * dx) + (dy * dy)) > CarrierReturnRouteGoalProxyMaxDistance;
    }

    private static bool IsDirectSeekRouteGoalAcceptable(NavNode goalNode, PlayerEntity self, float targetX, float targetY)
    {
        var targetIsAbove = targetY < self.Y - DirectSeekRouteVerticalThreshold;
        var targetIsBelow = targetY > self.Y + DirectSeekRouteVerticalThreshold;
        if (targetIsAbove && goalNode.Y > targetY + DirectSeekRouteGoalVerticalSlack)
        {
            return false;
        }

        if (targetIsBelow && goalNode.Y < targetY - DirectSeekRouteGoalVerticalSlack)
        {
            return false;
        }

        return MathF.Abs(goalNode.X - targetX) <= DirectSeekRouteGoalHorizontalSlack
            || MathF.Abs(goalNode.Y - targetY) <= DirectSeekRouteGoalVerticalSlack;
    }

    private static bool ShouldPreserveCarrierFailedEdgeBlocks() =>
        Environment.GetEnvironmentVariable("BOTBRAIN_PRESERVE_CARRIER_FAILED_EDGE_BLOCKS") is "1" or "true" or "TRUE";

    private static bool ShouldPreserveCarrierFailedEdgeBlocks(SimpleLevel? level, PlayerEntity self) =>
        ShouldPreserveCarrierFailedEdgeBlocks()
        || (self.IsCarryingIntel
            && self.ClassId != PlayerClass.Scout
            && level?.Mode == GameModeKind.CaptureTheFlag
            && string.Equals(level.Name, "Conflict", StringComparison.OrdinalIgnoreCase));

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
        if (distance > CarrierCapFinishDirectSeekDistance
            || MathF.Abs(dy) > CarrierCapFinishDirectSeekVerticalRange)
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

        return TryResolveLocalMotionRecovery(
            world,
            self,
            new DirectDriveTarget(DirectDriveTargetKind.Objective, ownBase.Value.X, ownBase.Value.Y, "carrierCapFinish"),
            steeringOutput,
            out directSteering,
            out directTrace);
    }

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
        if (world.MatchRules.Mode is not (GameModeKind.Arena or GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill))
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

            if (TryResolveControlPointHold(world, self, team, point, steeringOutput, out directSteering, out directTrace))
            {
                return true;
            }

            if (ShouldKeepGraphControlForBelowPointClimb(self, point))
            {
                return false;
            }

            if (TryRouteToDirectSeekTarget(
                    world,
                    self,
                    team,
                    point.HealingAuraCenterX,
                    point.HealingAuraCenterY,
                    $"capturePoint point:{point.Index}",
                    steeringOutput,
                    out directSteering,
                    out directTrace)
                || TryResolveLocalMotionRecovery(
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
        if (!IsPlatformLadderCandidate(world, self, point))
        {
            _platformLadderStage = 0;
            _platformLadderSide = 0f;
            return false;
        }

        if (_platformLadderStage <= 0 || _platformLadderSide == 0f)
        {
            _platformLadderStage = ResolveInitialPlatformLadderStage(self.Y, point.HealingAuraCenterY);
            _platformLadderSide = self.X <= point.HealingAuraCenterX ? -1f : 1f;
        }

        var isArenaLadder = world.MatchRules.Mode == GameModeKind.Arena;
        var finalStage = isArenaLadder ? PlatformLadderArenaFinalStage : PlatformLadderDefaultFinalStage;
        var target = ResolvePlatformLadderTarget(point.HealingAuraCenterX, point.HealingAuraCenterY, _platformLadderSide, _platformLadderStage, isArenaLadder);
        if (HasReachedPlatformLadderTarget(self, target, isArenaLadder)
            && _platformLadderStage < finalStage)
        {
            _platformLadderStage += 1;
            target = ResolvePlatformLadderTarget(point.HealingAuraCenterX, point.HealingAuraCenterY, _platformLadderSide, _platformLadderStage, isArenaLadder);
        }

        var dx = target.X - self.X;
        var dy = target.Y - self.Y;
        ladderSteering.MoveDirection = ResolvePlatformLadderMoveDirection(dx, dy, _platformLadderSide, _platformLadderStage);
        ladderSteering.Jump = self.IsGrounded
            && dy < -18f
            && MathF.Abs(dx) <= PlatformLadderJumpHorizontalRange
            && IsPlatformLadderJumpReady(world, self, ladderSteering.MoveDirection, dx, _platformLadderStage);
        ladderSteering.DropDown = false;
        trace = $"platformLadder point:{point.Index} stage:{_platformLadderStage}/{finalStage} dx:{dx:0.0} dy:{dy:0.0} move:{ladderSteering.MoveDirection:0} jump:{(ladderSteering.Jump ? 1 : 0)}";
        return true;
    }

    private static (float X, float Y) ResolvePlatformLadderTarget(float centerX, float centerY, float side, int stage, bool isArenaLadder)
    {
        if (isArenaLadder)
        {
            return stage switch
            {
                1 => (centerX + (side * 37f), centerY + 186f),
                2 => (centerX + (side * 249f), centerY + 138f),
                3 => (centerX + (side * 193f), centerY + 36f),
                4 => (centerX + (side * 31f), centerY + 12f),
                _ => (centerX, centerY),
            };
        }

        return stage switch
        {
            1 => (centerX + (side * 84f), centerY + 180f),
            2 => (centerX + (side * 141f), centerY + 126f),
            3 => (centerX + (side * 99f), centerY + 72f),
            _ => (centerX + (side * 70f), centerY + 18f),
        };
    }

    private static int ResolveInitialPlatformLadderStage(float playerY, float centerY)
    {
        var dy = playerY - centerY;
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

    private static bool IsPlatformLadderJumpReady(SimulationWorld world, PlayerEntity self, float moveDirection, float targetDx, int stage)
    {
        if (stage == 1
            && world.MatchRules.Mode != GameModeKind.Arena
            && world.Level.Name.Contains("Valley", StringComparison.OrdinalIgnoreCase)
            && MathF.Abs(targetDx) > PlatformLadderInitialRunupJumpRange)
        {
            return moveDirection != 0f
                && self.HorizontalSpeed * moveDirection >= PlatformLadderInitialRunupSpeed;
        }

        if (stage > 1
            && stage < PlatformLadderDefaultFinalStage
            && world.MatchRules.Mode != GameModeKind.Arena
            && world.Level.Name.Contains("Valley", StringComparison.OrdinalIgnoreCase))
        {
            return moveDirection != 0f
                && self.HorizontalSpeed * moveDirection >= PlatformLadderInitialRunupSpeed;
        }

        if (self.ClassId != PlayerClass.Heavy || stage <= 1 || stage >= 4)
        {
            return true;
        }

        return moveDirection != 0f
            && self.HorizontalSpeed * moveDirection >= 60f;
    }

    private static bool HasReachedPlatformLadderTarget(PlayerEntity self, (float X, float Y) target, bool isArenaLadder)
    {
        if (MathF.Abs(self.X - target.X) > PlatformLadderArrivalHorizontal)
        {
            return false;
        }

        if (self.IsGrounded)
        {
            return MathF.Abs(self.Y - target.Y) <= PlatformLadderArrivalVertical;
        }

        return isArenaLadder
            && MathF.Abs(self.Y - target.Y) <= PlatformLadderArrivalVertical * 2f;
    }

    private static bool IsPlatformLadderCandidate(SimulationWorld world, PlayerEntity self, ControlPointState point)
    {
        if (world.MatchRules.Mode != GameModeKind.Arena
            && !world.Level.Name.Contains("Valley", StringComparison.OrdinalIgnoreCase))
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

    private static bool TryResolveCaptureZoneUnion(
        SimulationWorld world,
        out float centerX,
        out float centerY,
        out float width,
        out float height)
    {
        centerX = 0f;
        centerY = 0f;
        width = 0f;
        height = 0f;
        var captureZones = world.Level.GetRoomObjects(RoomObjectType.CaptureZone);
        if (captureZones.Count == 0)
        {
            return false;
        }

        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        foreach (var zone in captureZones)
        {
            minX = MathF.Min(minX, zone.CenterX - zone.Width * 0.5f);
            minY = MathF.Min(minY, zone.CenterY - zone.Height * 0.5f);
            maxX = MathF.Max(maxX, zone.CenterX + zone.Width * 0.5f);
            maxY = MathF.Max(maxY, zone.CenterY + zone.Height * 0.5f);
        }

        if (!float.IsFinite(minX)
            || !float.IsFinite(minY)
            || !float.IsFinite(maxX)
            || !float.IsFinite(maxY))
        {
            return false;
        }

        centerX = (minX + maxX) * 0.5f;
        centerY = (minY + maxY) * 0.5f;
        width = maxX - minX;
        height = maxY - minY;
        return width > 0f && height > 0f;
    }

    private static bool IsPlayerInArenaCaptureZone(SimulationWorld world, PlayerEntity self)
    {
        var captureZones = world.Level.GetRoomObjects(RoomObjectType.CaptureZone);
        for (var index = 0; index < captureZones.Count; index += 1)
        {
            var zone = captureZones[index];
            if (self.IntersectsMarker(zone.CenterX, zone.CenterY, zone.Width, zone.Height))
            {
                return true;
            }
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
        PlayerTeam team,
        ControlPointState point,
        SteeringOutput steeringOutput,
        out SteeringOutput holdSteering,
        out string trace)
    {
        holdSteering = steeringOutput;
        trace = string.Empty;

        var holdTargetX = world.IsPlayerInControlPointCaptureZone(self, point.Index)
            ? ResolveCapturePointLaneTargetX(world, self, team, point)
            : point.HealingAuraCenterX;
        var dx = holdTargetX - self.X;
        var dy = point.HealingAuraCenterY - self.Y;
        var inCaptureZone = world.IsPlayerInControlPointCaptureZone(self, point.Index);
        if (!inCaptureZone
            && (MathF.Abs(point.HealingAuraCenterX - self.X) > CapturePointHoldHorizontalRange || MathF.Abs(dy) > CapturePointHoldVerticalRange))
        {
            return false;
        }

        var spacingTrace = string.Empty;
        if (inCaptureZone && TryResolveCapturePointSpacingMove(world, self, team, point, out var spacingMoveDirection, out spacingTrace))
        {
            holdSteering.MoveDirection = spacingMoveDirection;
        }
        else
        {
            holdSteering.MoveDirection = MathF.Abs(dx) <= CapturePointHoldCenterDeadZone
                ? 0
                : dx > 0f ? 1 : -1;
        }

        holdSteering.Jump = inCaptureZone
            ? false
            : self.IsGrounded && dy < -24f;
        holdSteering.DropDown = false;
        var spacingSuffix = string.IsNullOrWhiteSpace(spacingTrace)
            ? string.Empty
            : $" spacing:{spacingTrace}";
        trace = $"capturePointHold point:{point.Index} dx:{dx:0.0} dy:{dy:0.0} inZone:{(inCaptureZone ? 1 : 0)} move:{holdSteering.MoveDirection:0} jump:{(holdSteering.Jump ? 1 : 0)}{spacingSuffix}";
        return true;
    }

    private void ApplyCaptureStrafeHop(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        ref SteeringOutput steeringOutput)
    {
        if (!TryFindControlPointStrafeTarget(world, self, team, out var point, out var reason))
        {
            return;
        }

        var centerBand = Math.Clamp(point.Marker.Width * CaptureStrafeCenterBand, CapturePointLaneTargetDeadZone, 32f);
        var laneTargetX = ResolveCapturePointLaneTargetX(world, self, team, point);
        var dxFromTarget = self.X - laneTargetX;
        var phase = Math.Abs((_thinkTicks + (point.Index * 7)) % CaptureStrafeHopCycleTicks);
        var inHopWindow = phase >= CaptureStrafeHopSideTicks * 2
            && phase < (CaptureStrafeHopSideTicks * 2) + CaptureStrafeHopWindowTicks;
        var tapDirection = ResolveCaptureStrafeTapDirection(phase);
        var inTapWindow = tapDirection != 0
            && phase % CaptureStrafeHopSideTicks < CaptureStrafeTapTicks;
        var spacingTrace = string.Empty;
        var hasSpacingMove = TryResolveCapturePointSpacingMove(world, self, team, point, out var spacingMoveDirection, out spacingTrace);
        var moveDirection = hasSpacingMove
            ? spacingMoveDirection
            : (MathF.Abs(dxFromTarget) > centerBand
                ? (dxFromTarget > 0f ? -1 : 1)
                : inTapWindow ? tapDirection : 0);
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

        var spacingSuffix = string.IsNullOrWhiteSpace(spacingTrace)
            ? string.Empty
            : $" spacing:{spacingTrace}";
        LastDirectDriveTrace = string.IsNullOrWhiteSpace(LastDirectDriveTrace)
            ? $"captureStrafeHop point:{point.Index} reason:{reason} move:{moveDirection} phase:{phase}{spacingSuffix}"
            : $"{LastDirectDriveTrace} captureStrafeHop point:{point.Index} reason:{reason} move:{moveDirection} phase:{phase}{spacingSuffix}";
    }

    private static bool TryResolveCapturePointSpacingMove(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        ControlPointState point,
        out int moveDirection,
        out string trace)
    {
        if (TryResolveCapturePointClusterMove(world, self, point, out moveDirection, out trace))
        {
            return true;
        }

        var targetX = ResolveCapturePointLaneTargetX(world, self, team, point);
        var dx = targetX - self.X;
        if (MathF.Abs(dx) <= CapturePointLaneTargetDeadZone)
        {
            moveDirection = 0;
            trace = string.Empty;
            return false;
        }

        var laneMoveDirection = dx > 0f ? 1 : -1;
        if (!CanMoveWithinCapturePointLane(world, self, point, laneMoveDirection))
        {
            moveDirection = 0;
            trace = string.Empty;
            return false;
        }

        moveDirection = laneMoveDirection;
        trace = $"lane target:{targetX:0.0} dx:{dx:0.0}";
        return true;
    }

    private static bool TryResolveCapturePointClusterMove(
        SimulationWorld world,
        PlayerEntity self,
        ControlPointState point,
        out int moveDirection,
        out string trace)
    {
        moveDirection = 0;
        trace = string.Empty;
        PlayerEntity? closestPlayer = null;
        var closestDistance = CapturePointClusterMinimumDistance;
        var closestDx = 0f;
        var closestDy = 0f;

        foreach (var candidate in CombatDecisionResolver.EnumeratePlayers(world))
        {
            if (!candidate.IsAlive
                || candidate.Id == self.Id
                || !world.IsPlayerInControlPointCaptureZone(candidate, point.Index))
            {
                continue;
            }

            var dx = self.X - candidate.X;
            var dy = self.Y - candidate.Y;
            if (MathF.Abs(dy) > CapturePointClusterVerticalRange)
            {
                continue;
            }

            var horizontalDistance = MathF.Abs(dx);
            if (horizontalDistance >= closestDistance)
            {
                continue;
            }

            var awayDirection = ResolveCapturePointClusterAwayDirection(self, candidate, dx);
            if (!CanMoveWithinCapturePointLane(world, self, point, awayDirection))
            {
                var oppositeDirection = -awayDirection;
                if (horizontalDistance > 1f || !CanMoveWithinCapturePointLane(world, self, point, oppositeDirection))
                {
                    continue;
                }

                awayDirection = oppositeDirection;
            }

            closestPlayer = candidate;
            closestDistance = horizontalDistance;
            closestDx = dx;
            closestDy = dy;
            moveDirection = awayDirection;
        }

        if (closestPlayer is null || moveDirection == 0)
        {
            return false;
        }

        trace = $"cluster player:{closestPlayer.Id} dx:{closestDx:0.0} dy:{closestDy:0.0}";
        return true;
    }

    private static int ResolveCapturePointClusterAwayDirection(PlayerEntity self, PlayerEntity candidate, float dx)
    {
        if (MathF.Abs(dx) > 1f)
        {
            return dx > 0f ? 1 : -1;
        }

        return self.Id < candidate.Id ? -1 : 1;
    }

    private static float ResolveCapturePointLaneTargetX(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        ControlPointState point)
    {
        var (centerX, width) = ResolveCapturePointActiveZone(world, self, point);
        var (minX, maxX) = ResolveCapturePointLaneBounds(centerX, width);
        if (minX >= maxX)
        {
            return centerX;
        }

        return Math.Clamp(
            centerX + ResolveCapturePointPreferredOffset(self, team, point),
            minX,
            maxX);
    }

    private static float ResolveCapturePointPreferredOffset(PlayerEntity self, PlayerTeam team, ControlPointState point)
    {
        var hash = unchecked((uint)((self.Id * 397) ^ (point.Index * 97) ^ ((int)team * 53)));
        var lane = (int)(hash % 5u) - 2;
        return lane * CapturePointLaneSpacing;
    }

    private static (float CenterX, float Width) ResolveCapturePointActiveZone(
        SimulationWorld world,
        PlayerEntity self,
        ControlPointState point)
    {
        RoomObjectMarker? bestZone = null;
        var bestContainsSelf = false;
        var bestArea = -1f;
        foreach (var zone in world.Level.GetRoomObjects(RoomObjectType.CaptureZone))
        {
            if (!IsCaptureZoneAssignedToPoint(world, zone, point))
            {
                continue;
            }

            var containsSelf = self.IntersectsMarker(zone.CenterX, zone.CenterY, zone.Width, zone.Height);
            var area = zone.Width * zone.Height;
            if (bestZone.HasValue
                && ((bestContainsSelf && !containsSelf)
                    || (bestContainsSelf == containsSelf && area <= bestArea)))
            {
                continue;
            }

            bestZone = zone;
            bestContainsSelf = containsSelf;
            bestArea = area;
        }

        return bestZone.HasValue
            ? (bestZone.Value.CenterX, bestZone.Value.Width)
            : (point.Marker.CenterX, point.Marker.Width);
    }

    private static bool IsCaptureZoneAssignedToPoint(
        SimulationWorld world,
        RoomObjectMarker zone,
        ControlPointState point)
    {
        var closestIndex = 0;
        var closestDistance = float.MaxValue;
        foreach (var candidate in world.ControlPoints)
        {
            var distance = DistanceBetween(zone.CenterX, zone.CenterY, candidate.Marker.CenterX, candidate.Marker.CenterY);
            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closestIndex = candidate.Index;
        }

        return closestIndex == point.Index;
    }

    private static (float MinX, float MaxX) ResolveCapturePointLaneBounds(float centerX, float width)
    {
        var halfWidth = width * 0.5f;
        var padding = MathF.Min(CapturePointLaneBoundaryPadding, MathF.Max(0f, halfWidth - 1f));
        return (centerX - halfWidth + padding, centerX + halfWidth - padding);
    }

    private static bool CanMoveWithinCapturePointLane(
        SimulationWorld world,
        PlayerEntity self,
        ControlPointState point,
        int moveDirection)
    {
        var (centerX, width) = ResolveCapturePointActiveZone(world, self, point);
        var (minX, maxX) = ResolveCapturePointLaneBounds(centerX, width);
        return moveDirection < 0
            ? self.X > minX
            : moveDirection > 0 && self.X < maxX;
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
        if (world.MatchRules.Mode is not (GameModeKind.Arena or GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill))
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

    private bool TryFindControlPointStrafeTarget(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        out ControlPointState point,
        out string reason)
    {
        point = null!;
        reason = string.Empty;
        if (world.MatchRules.Mode is not (GameModeKind.Arena or GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill))
        {
            return false;
        }

        var bestDistanceSq = float.PositiveInfinity;
        string? bestReason = null;
        ControlPointState? bestPoint = null;
        foreach (var candidate in world.ControlPoints)
        {
            if (!world.IsPlayerInControlPointCaptureZone(self, candidate.Index))
            {
                continue;
            }

            var currentGoalPoint = IsCurrentGoalControlPoint(candidate);
            var candidateReason = ResolveControlPointStrafeReason(candidate, team, currentGoalPoint);
            if (candidateReason is null)
            {
                continue;
            }

            var dx = candidate.HealingAuraCenterX - self.X;
            var dy = candidate.HealingAuraCenterY - self.Y;
            var distanceSq = (dx * dx) + (dy * dy);
            if (distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            bestReason = candidateReason;
            bestPoint = candidate;
        }

        if (bestPoint is null || bestReason is null)
        {
            return false;
        }

        point = bestPoint;
        reason = bestReason;
        return true;
    }

    private bool IsCurrentGoalControlPoint(ControlPointState point) =>
        DistanceBetween(
            _currentGoalPosition.X,
            _currentGoalPosition.Y,
            point.HealingAuraCenterX,
            point.HealingAuraCenterY) <= CapturePointHoldHorizontalRange;

    private static string? ResolveControlPointStrafeReason(
        ControlPointState point,
        PlayerTeam team,
        bool currentGoalPoint)
    {
        if (point.IsLocked)
        {
            if (point.Team != team)
            {
                return "lockedCapture";
            }

            return currentGoalPoint ? "lockedHold" : null;
        }

        if (point.Team != team)
        {
            return point.CappingTeam == team ? "capture" : "captureStaging";
        }

        return point.CappingTeam.HasValue ? "defend" : null;
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
        var allowLockedPointStaging = world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill;
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

            if (!CombatDecisionResolver.IsPlayerVisibleToBot(self, candidate))
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

            if (!CombatDecisionResolver.IsPlayerVisibleToBot(self, candidate))
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
