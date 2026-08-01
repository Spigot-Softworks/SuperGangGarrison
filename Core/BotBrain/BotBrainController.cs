using System.Diagnostics;
using System.Globalization;
using System.IO;

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
    private static readonly bool NavigationEventTracingEnabled =
        Environment.GetEnvironmentVariable("OG_CLIENT_PERF_BOT_TRACE") is "1" or "true" or "TRUE";
    private static readonly string? NavigationEventTracePath = NavigationEventTracingEnabled
        ? RuntimePaths.GetLogPath($"bot-navigation-events-{DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)}.log")
        : null;
    private static bool NavigationStageTracingEnabled =>
        Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_STAGE_TRACE") is "1" or "true" or "TRUE";
    private static readonly double ThinkTimingTraceThresholdMilliseconds = ResolveThinkTimingTraceThresholdMilliseconds();

    private static double ResolveThinkTimingTraceThresholdMilliseconds()
    {
        var configured = Environment.GetEnvironmentVariable("OG_CLIENT_PERF_BOT_TRACE_THRESHOLD_MS");
        return double.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold)
            ? Math.Max(0d, threshold)
            : 8d;
    }

    private readonly SteeringMachine _steering = new();
    private readonly AimResolver _aimResolver = new();
    private readonly CombatDecisionMemory _combatMemory = new();
    private readonly ObjectiveTapeExecutor _objectiveTapeExecutor = new();
    private readonly VerifiedNavProofRouteExecutor _proofRouteExecutor = new();
    private readonly LocalMotionController _localMotionController = new();
    private readonly StochasticLocalMotionPlanner _stochasticLocalMotionPlanner = new();
    private readonly NavGraph? _graphOverride;
    private readonly bool _forceAlphaNavigation;

    private NavGraph? _navGraph;
    private bool _alphaNavigation;
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
    private int _harvestRightSpoolLowMotionTicks;
    private SimpleLevel? _lastLevel;
    private int _carrierReturnDirectEscapeTicks;
    private int _carrierReturnDirectStuckTicks;
    private float _carrierReturnDirectEscapeDirection;
    private float _carrierReturnDirectCheckX;
    private float _carrierReturnDirectCheckY;
    private BotBrainObjectiveTapeAsset? _objectiveTapeAsset;
    private VerifiedNavProofGraphAsset? _verifiedProofGraphAsset;
    private PlayerInputSnapshot _previousInput;
    private BotBrainCombatTarget? _graphlessCombatTarget;
    private int _graphlessCombatTargetRefreshCooldown;
    private bool _graphlessCombatTargetRefreshInitialized;
    private MedicHealTargetSelection _graphlessHealTargetSelection;
    private int _graphlessMedicHealTargetRefreshCooldown;
    private bool _graphlessMedicHealTargetRefreshInitialized;
    private readonly Dictionary<NavEdgeBlock, int> _blockedEdges = [];
    private int _blockedEdgesVersion;
    private AlphaRecoverySearchFailure? _alphaRecoverySearchFailure;
    private int _runtimeContactPathIndex = -1;
    private int _runtimeContactFromNode = -1;
    private int _runtimeContactToNode = -1;
    private bool _runtimeContactProbeAttempted;
    private int _runtimeContactRetryThinkTick;
    private int _runtimeContactFailureCount;
    private int _pathObjectiveStateSignature;
    private bool _alphaRecoveryPending;
    private int _alphaRecoveryNextAttemptThinkTick;
    private int _dynamicRouteRetryCooldownTicks;
    private bool _hasDynamicRouteTarget;
    private (float X, float Y) _dynamicRouteTargetPosition;
    private int _alphaPathlessEscapeDirection;
    private int _alphaPathlessEscapeUntilThinkTick;
    private string? _alphaDynamicRecoveryLabel;
    private float _alphaDynamicRecoveryLastX;
    private float _alphaDynamicRecoveryLastY;
    private int _alphaDynamicRecoveryStagnantTicks;
    private float _dynamicRouteProgressLastX;
    private float _dynamicRouteProgressLastY;
    private int _dynamicRouteProgressStagnantTicks;
    private int _dynamicRouteProgressLowSpeedFlips;
    private int _dynamicRouteProgressLastMoveDirection;
    private NavPath? _dynamicRouteProgressPath;
    private string? _combatRouteProgressOwner;
    private float _combatRouteProgressLastX;
    private float _combatRouteProgressLastY;
    private int _combatRouteProgressStagnantTicks;

    /// <summary>
    /// How often (in ticks) the bot reconsiders its path.
    /// </summary>
    private const int RepathIntervalTicks = 30; // 1 second at 30 tps.

    // A failed edge must be absent from the next route search, but it also
    // cannot be a permanent ban: a transient landing/attachment failure may
    // be recoverable once the bot has escaped the local obstruction. Six
    // 1.5 seconds is long enough to prevent an immediate same-edge hot loop
    // while still allowing a later retry without stranding the bot for a match.
    // Dynamic crates and bodies are not part of the static OG2 graph. Keep a
    // transition that just failed out of the route search long enough for the
    // replacement path to clear the local support region; 45 ticks was short
    // enough for Waterway to re-select the same invalid central contact.
    private const int FailedEdgeBlockTicks = 1_200; // 40 seconds at 30 tps; long enough to avoid reselecting a failed contact during recovery.
    private const float DirectSeekPlayerDistance = 900f;
    private const float MedicSupportDirectSeekDistance = 900f;
    private const float MedicSupportHoldMinDistance = 78f;
    private const float MedicSupportHoldMaxDistance = 230f;
    private const float MedicSupportHealRange = 300f;
    private const float IntelCarrierDirectSeekDistance = 1200f;
    private const float EscortCarrierDirectSeekDistance = 900f;
    private const float DynamicEscortCarrierDirectSeekDistance = 2400f;
    private const float DirectRouteGoalReuseDistance = 8f;
    // A graph route is a coarse traversal plan, not a pixel-perfect chase
    // line. Keep the active traversal edge while a carrier moves through the
    // surrounding region; rebuilding as soon as the target crosses a single
    // graph relay restarts the jump schedule and can strand a bot on the same
    // launch surface indefinitely. A completed/failed edge still forces a
    // fresh route immediately.
    private const float MovingCarrierRouteReuseDistance = 2_048f;
    private const int CaptureStrafeHopCycleTicks = 32;
    private const int CaptureStrafeHopSideTicks = 14;
    private const int CaptureStrafeTapTicks = 5;
    private const int CaptureStrafeHopWindowTicks = 4;
    private const float CaptureStrafeCenterBand = 0.08f;
    private const float CaptureStrafeBrakeSpeed = 36f;
    private const float CapturePointLaneSpacing = 18f;
    private const float CapturePointLaneTargetDeadZone = 6f;
    private const float CapturePointLaneBoundaryPadding = 4f;
    private const int CapturePointDefensePatrolCycleTicks = 180;
    private const int CapturePointDefensePatrolLegTicks = 72;
    private const float CapturePointDefensePatrolOffset = 32f;
    private const float CapturePointClusterMinimumDistance = 24f;
    private const float CapturePointClusterVerticalRange = 40f;
    private const float CapturePointDirectSeekDistance = 360f;
    private const float CapturePointDirectSeekVerticalRange = 300f;
    private const float CapturePointClearEnemyDistance = 260f;
    private const float CapturePointClearEnemyVerticalRange = 120f;
    private const float CapturePointClearSelfInterestDistance = 420f;
    private const float AlphaCapturePointCombatEngagementDistance = 280f;
    private const float AlphaObjectiveCombatOverlayDistance = 192f;
    private const float CapturePointObstacleProbeDistance = 36f;
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
    private const float EngineerCtfDefenseCombatChaseDistance = 520f;
    private const float EngineerCtfPatrolOffset = 64f;
    private const float EngineerCtfPatrolTargetDeadZone = 8f;
    private const int EngineerCtfPatrolCycleTicks = 180;
    private const int EngineerCtfPatrolLegTicks = 70;
    private const int EngineerCtfPatrolPauseTicks = 20;
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
    private const float HarvestRightSpoolPocketMinX = 2984f;
    private const float HarvestRightSpoolPocketMaxX = 3080f;
    private const float HarvestRightSpoolPocketCenterX = 3030f;
    private const float HarvestRightSpoolPocketMinBottom = 768f;
    private const float HarvestRightSpoolPocketMaxBottom = 828f;
    private const float HarvestRightSpoolPocketCenterDeadZone = 8f;

    private const float GroundedStartNodeMaxAboveDistance = 12f;
    private const float AlphaGroundedStartNodeMaxAboveDistance = 48f;
    private const float FallingStartNodeMaxAboveDistance = 8f;
    // A failed contact can leave the bot grounded on the next lower support
    // surface rather than exactly on the failed edge's source surface. The
    // alpha graph must be able to reattach to that valid landing support.
    // A missed jump can leave the live body several supports below the
    // certified launch surface. Corinth's point well is a concrete example:
    // the lower recovery floor is more than one platform height below the
    // point. The goal-aware path check still rejects a disconnected candidate,
    // so this wider attachment is not a blind seek.
    private const float AlphaTraversalStartMaxBelowDistance = 512f;
    private const float AlphaRecoveryMaxHorizontalAttachmentDistance = 192f;
    // Runtime jump proof is only valid when the live grounded body is still
    // attached to the edge's source surface. A lower recovery floor must
    // reattach to its own graph node instead of repeatedly probing an upper
    // stair edge that can no longer be launched from the current Y.
    private const float AlphaRuntimeContactSourceVerticalTolerance = 42f;

    /// <summary>
    /// How often (in ticks) the bot re-evaluates its objective.
    /// </summary>
    private const int ObjectiveReevalIntervalTicks = 60; // 2 seconds.
    // Three probes can all occur before SteeringMachine's shared stagnation
    // detector reaches its first recovery phase. Keep the resolver bounded,
    // but leave enough attempts for a real body to escape a transient crate
    // or body-block before declaring the contact unavailable and hot-looping
    // through equivalent graph edges.
    // A runtime proof is an adaptive refinement of the shipped OG2 contact,
    // not a prerequisite for using that contact. Keep the retry count small:
    // repeating the probe while the player is moving replays the expensive
    // physics sandbox and can freeze a live frame for 100 ms or more.
    private const int DefaultMaximumRuntimeContactProbeFailures = 6;
    private static int MaximumRuntimeContactProbeFailures
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_RUNTIME_CONTACT_FAILURES");
            return int.TryParse(configured, out var value)
                ? Math.Clamp(value, 1, 6)
                : DefaultMaximumRuntimeContactProbeFailures;
        }
    }
    private const int AlphaRecoveryRetryTicks = 6;
    private const int AlphaRecoveryNegativeCacheTicks = 18;
    // A failed alpha route must never leave the bot with a neutral movement
    // owner while the graph is reattaching. Hold a deterministic escape
    // direction briefly so a box/body-block does not turn into left/right
    // churn while the next graph search is pending.
    private const int AlphaPathlessEscapeCommitTicks = 12;
    private const float AlphaPathlessEscapeTargetDeadZone = 8f;
    // A dynamic target route that just failed is temporarily handed to the
    // local recovery owner. Re-running the same synchronous A* every think
    // produces visible frame stalls while the bot is still beside the failed
    // contact.
    private const int DynamicRouteRetryCooldownTicks = 12;
    private const int DynamicObjectiveRecoveryStagnationTicks = 18;
    private const float DynamicObjectiveRecoveryProgressDistance = 4f;
    private const int DynamicRouteProgressStagnationTicks = 18;
    private const int CombatRouteProgressStagnationTicks = 30;
    private const float CombatRouteProgressDistance = 3f;
    private const int GraphlessCombatTargetRefreshTicks = 4;
    private const int GraphlessMedicHealTargetRefreshTicks = 6;
    private const float GraphlessCombatTargetMaxRange = 375f;
    private const float GraphlessSniperCombatTargetMaxRange = 760f;
    private const float GraphlessMedicHealTargetMaxDistance = 300f;
    private const float GraphlessMedicHumanCallTargetMaxDistance = 900f;

    private int _objectiveReevalCooldown;
    private (float X, float Y) _currentGoalPosition;
    private bool _lastCarryingIntel;
    private int _lastObservedDeaths = -1;
    private bool _wasAliveLastThink;

    public BotBrainController()
    {
    }

    public BotBrainController(NavGraph graphOverride)
        : this(graphOverride, forceAlphaNavigation: false)
    {
    }

    public BotBrainController(NavGraph graphOverride, bool forceAlphaNavigation)
    {
        _graphOverride = graphOverride ?? throw new ArgumentNullException(nameof(graphOverride));
        _forceAlphaNavigation = forceAlphaNavigation;
    }

    public int CurrentPathNode => _currentPath?.CurrentNode ?? -1;

    public int CurrentPathIndex => _currentPath?.CurrentIndex ?? -1;

    public int CurrentPathCount => _currentPath?.Count ?? 0;

    public bool IsAlphaNavigation => _alphaNavigation;

    public int CurrentGoalNode => _goalNodeIndex;

    public NavPath? CurrentPath => _currentPath;

    public (float X, float Y) CurrentGoalPosition => _currentGoalPosition;

    public bool PreferEnemyPlayerObjective { get; set; }

    /// <summary>
    /// Test-only seam for validating navigation with role-specific objective
    /// overrides disabled. Production callers leave this false so Engineer
    /// defense and Medic support behavior remain unchanged.
    /// </summary>
    public bool ForceObjectiveNavigationForDiagnostics { get; set; }

    /// <summary>
    /// Test-only seam for separating navigation/body-traffic failures from
    /// combat targeting. Production callers leave this false; it does not
    /// alter shipped combat behavior.
    /// </summary>
    public bool DisableCombatForDiagnostics { get; set; }

    public bool HasNavigationGraph => IsNavigationGraphUsable(_navGraph);

    public bool HasObjectiveTapeAsset => _objectiveTapeAsset is not null;

    public bool HasActivePath => _currentPath is not null && !_currentPath.IsComplete;

    public SteeringOutput LastSteeringOutput { get; private set; }

    /// <summary>
    /// Runtime-certified contacts are schedules measured one simulation tick at a
    /// time. The client may batch ordinary bot thinking for performance, but must
    /// advance the entire contact edge one simulation tick at a time. In
    /// particular, stopping the fast path while airborne leaves the cached input
    /// running ahead of the edge state and hides the landing handoff from the
    /// controller.
    /// </summary>
    public bool RequiresPerTickNavigationThink
    {
        get
        {
            if (!_alphaNavigation
                || _navGraph is null
                || _currentPath is null
                || !_currentPath.TryGetCurrentEdge(out var edge)
                || !edge.IsOg2Contact)
            {
                return false;
            }

            return LastSteeringOutput.RecipeTrace.HasRecipe;
        }
    }

    /// <summary>
    /// A batched client must promote a bot whose alpha route was cleared back
    /// into the next full brain pass. Cached steering cannot reconstruct the
    /// objective target or choose a new graph attachment on its own.
    /// </summary>
    public bool RequiresImmediateNavigationThink =>
        _alphaNavigation
        && (_currentPath is null || _alphaRecoveryPending);

    public BotBrainCombatTarget? LastCombatTarget { get; private set; }

    public int? LastMedicHealTargetId { get; private set; }

    public bool LastMedicHealTargetIsPocket { get; private set; }

    public string LastSemanticRecoveryTrace { get; private set; } = string.Empty;

    public string LastDirectDriveTrace { get; private set; } = string.Empty;

    public string LastThinkTimingTrace { get; private set; } = string.Empty;

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
        PlayerTeam team,
        IReadOnlyDictionary<byte, PlayerTeam>? controlledTeamsBySlot = null)
    {
        var thinkStartTimestamp = Stopwatch.GetTimestamp();
        var graphReadyTimestamp = thinkStartTimestamp;
        var targetSelectionTimestamp = thinkStartTimestamp;
        var pathUpdatedTimestamp = thinkStartTimestamp;
        var routeSteeringResolvedTimestamp = thinkStartTimestamp;
        var directSteeringResolvedTimestamp = thinkStartTimestamp;
        var steeringResolvedTimestamp = thinkStartTimestamp;
        LastSemanticRecoveryTrace = string.Empty;
        LastDirectDriveTrace = string.Empty;
        LastObjectiveTapeTrace = string.Empty;
        LastProofGraphTrace = string.Empty;
        LastThinkTimingTrace = string.Empty;
        LastCombatTarget = null;
        _thinkTicks += 1;
        var proofGraphRequired = false;

        // Rebuild nav graph if the level changed (map rotation).
        if (_lastLevel != world.Level)
        {
            _alphaNavigation = _forceAlphaNavigation
                || (_graphOverride is null && !IsLegacyNavigationOptIn());
            _navGraph = _graphOverride ?? (_alphaNavigation
                ? Og2NavigationGraphStore.GetOrBuild(world.Level)
                : BotNavigationAssetStore.TryLoadCachedGraph(world.Level, out var graph)
                    ? graph
                    : null);
            _objectiveTapeAsset = !_alphaNavigation
                && BotBrainObjectiveTapeStore.TryLoad(world.Level, out var tapeAsset)
                    ? tapeAsset
                    : null;
            _verifiedProofGraphAsset = !_alphaNavigation
                && VerifiedNavProofGraphAssetStore.TryLoad(world.Level, team, self.BotGraphClassId, out var proofGraphAsset)
                    ? proofGraphAsset
                    : null;
            _lastLevel = world.Level;
            _currentPath = null;
            _hasDynamicRouteTarget = false;
            _goalNodeIndex = -1;
            _steering.Reset();
            _objectiveTapeExecutor.Reset();
            _proofRouteExecutor.Reset();
            _stochasticLocalMotionPlanner.Reset();
            ResetGraphlessTargetSelection();
            _lastCarryingIntel = self.IsCarryingIntel;

            if (_alphaNavigation)
            {
                ReportAlphaNavigationLoadDiagnostic(world.Level, _navGraph);
            }
            else
            {
                ReportNavigationLoadDiagnostic(world.Level, IsNavigationGraphUsable(_navGraph));
            }
        }

        graphReadyTimestamp = Stopwatch.GetTimestamp();

        proofGraphRequired = !_alphaNavigation
            && !PreferEnemyPlayerObjective
            && VerifiedNavProofGraphAssetStore.IsRequired();

        if (!self.IsAlive)
        {
            if (_wasAliveLastThink || _lastObservedDeaths != self.Deaths)
            {
                ResetTransientNavigationStateForNewLife();
            }

            _lastObservedDeaths = self.Deaths;
            _wasAliveLastThink = false;
            _previousInput = default;
            LastSteeringOutput = default;
            return default;
        }

        if (!IsNavigationGraphUsable(_navGraph))
        {
            return ThinkWithoutNavigationGraph(self, world, team, controlledTeamsBySlot);
        }

        if (!_wasAliveLastThink || _lastObservedDeaths != self.Deaths)
        {
            ResetTransientNavigationStateForNewLife();
        }

        _lastObservedDeaths = self.Deaths;
        _wasAliveLastThink = true;

        DecayBlockedEdges();
        var engineerCtfDefender = !ForceObjectiveNavigationForDiagnostics
            && IsCaptureTheFlagEngineerDefender(world, self);
        if (engineerCtfDefender)
        {
            _objectiveTapeExecutor.Reset();
        }

        // 1. Select combat/heal targets.
        var combatTarget = DisableCombatForDiagnostics
            ? null
            : TargetSelector.SelectCombatTarget(self, world, team);
        LastCombatTarget = combatTarget;
        PlayerEntity? preferredEnemyObjectiveTarget = null;
        if (PreferEnemyPlayerObjective
            && TryFindNearestEnemyPlayer(world, self, team, float.PositiveInfinity, out var preferredEnemy))
        {
            preferredEnemyObjectiveTarget = preferredEnemy;
        }

        var healTargetSelection = DisableCombatForDiagnostics
            ? new MedicHealTargetSelection(null, MedicHealTargetSelectionKind.None)
            : CombatDecisionResolver.FindBestMedicHealTargetSelection(world, self, team, controlledTeamsBySlot);
        var healTarget = healTargetSelection.Target;
        LastMedicHealTargetId = healTarget?.Id;
        LastMedicHealTargetIsPocket = healTargetSelection.Kind == MedicHealTargetSelectionKind.Pocket;
        targetSelectionTimestamp = Stopwatch.GetTimestamp();

        // 2. Evaluate objective (throttled).
        var objectiveNavigationMustRefresh = self.IsCarryingIntel != _lastCarryingIntel;
        if (self.IsCarryingIntel != _lastCarryingIntel)
        {
            _objectiveReevalCooldown = 0;
            _currentPath = null;
            _hasDynamicRouteTarget = false;
            _goalNodeIndex = -1;
            _repathCooldownTicks = 0;
            _alphaRecoveryPending = false;
            _alphaRecoveryNextAttemptThinkTick = 0;
            _alphaPathlessEscapeDirection = 0;
            _alphaPathlessEscapeUntilThinkTick = 0;
            ResetAlphaDynamicRecoveryProgress();
            _steering.Reset();
            _stochasticLocalMotionPlanner.Reset();
        }

        _objectiveReevalCooldown--;
        if (_alphaNavigation
            && _pathObjectiveStateSignature != 0
            && _pathObjectiveStateSignature != ComputeObjectiveStateSignature(world))
        {
            // Capture/CTF state is a navigation input, not a cosmetic event.
            // Do not wait for the normal two-second objective cadence when a
            // point changes owner or intel changes state.
            _objectiveReevalCooldown = 0;
            objectiveNavigationMustRefresh = true;
        }

        if (_objectiveReevalCooldown <= 0 || combatTarget is not null)
        {
            var previousGoalPosition = _currentGoalPosition;
            var evaluatedGoal = preferredEnemyObjectiveTarget is not null
                ? (preferredEnemyObjectiveTarget.X, preferredEnemyObjectiveTarget.Y)
                : ForceObjectiveNavigationForDiagnostics
                    ? ResolveDiagnosticObjectiveGoal(self, world, team)
                    : ObjectiveEvaluator.EvaluateGoal(self, world, team, combatTarget?.Player);
            _currentGoalPosition = _alphaNavigation
                ? ResolveAlphaObjectiveGoal(world, self, team, evaluatedGoal)
                : evaluatedGoal;
            if (_proofRouteExecutor.IsActive
                && DistanceBetween(previousGoalPosition.X, previousGoalPosition.Y, _currentGoalPosition.X, _currentGoalPosition.Y) > 96f)
            {
                _proofRouteExecutor.Reset();
            }

            _objectiveReevalCooldown = ObjectiveReevalIntervalTicks;
        }

        _lastCarryingIntel = self.IsCarryingIntel;

        // 3. Find/update path.
        var graphSuspendedForPointCapture = ShouldSuspendGraphRoutingForControlPointCapture(world, self, team, _alphaNavigation);
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
        else if ((!proofGraphRequired || engineerCtfDefender)
            && !proofGraphOwnsMovement
            && !tapeOwnsMovement
            // Combat can override an already-valid objective route, but a
            // capture/drop/carry transition must rebuild that route first.
            // Otherwise scoring clears the old path, the dynamic CTF owner
            // correctly returns no target, and this optimization leaves the
            // bot with no movement owner while an enemy remains visible.
            // The same rule applies after any failed edge: once steering has
            // cleared the route, combat must not suppress the replacement
            // graph route on the next think.
            && (combatTarget is null
                || objectiveNavigationMustRefresh
                || _currentPath is null
                || _currentPath.IsComplete
                || _currentPath.Count < 2))
        {
            UpdatePath(world, self, team);
        }

        // A moving CTF carrier can leave the bot attached to a still-valid
        // graph edge that is no longer useful from the live body position.
        // The steering machine cannot report that as a failed static edge,
        // so bound the dynamic route's own progress and reattach from the
        // current surface when it has made no measurable progress.
        BreakStalledDynamicRoute(self, world);
        BreakStalledCombatRoute(self);

        pathUpdatedTimestamp = Stopwatch.GetTimestamp();

        var routeMissingAfterUpdate = _currentPath is null || _currentPath.IsComplete || _currentPath.Count < 2;
        var steeringOutput = new SteeringOutput();
        PlayerInputSnapshot? inputOverride = null;
        var dynamicCtfSteering = steeringOutput;
        var dynamicCtfTrace = string.Empty;
        // An Engineer normally owns the static intel-defense behavior. Once
        // our intel is being carried, that defense owner intentionally exits;
        // allow the dynamic CTF resolver to take over so the Engineer chases
        // the live enemy carrier instead of falling through to pathless
        // objective recovery with no movement owner.
        var engineerNeedsDynamicCtfResponse = engineerCtfDefender
            && GetOwnIntelState(world, team).IsCarried;
        var dynamicCtfResolved = !ForceObjectiveNavigationForDiagnostics
            && !PreferEnemyPlayerObjective
            && (!engineerCtfDefender || engineerNeedsDynamicCtfResponse)
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
        var proofResolved = !_alphaNavigation
            && !engineerCtfDefender
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
                UpdatePath(world, self, team);
            }
        }
        else if (_proofRouteExecutor.LastTrace.StartsWith("proofGraph=idle", StringComparison.Ordinal)
            || _proofRouteExecutor.LastTrace.StartsWith("proofGraph=selected", StringComparison.Ordinal))
        {
            LastProofGraphTrace = _proofRouteExecutor.LastTrace;
        }

        if (!_alphaNavigation
            && !proofResolved
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
        if (!_alphaNavigation
            && !PreferEnemyPlayerObjective
            && !engineerCtfDefender
            && !proofResolved
            && !proofGraphRequired)
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
                UpdatePath(world, self, team);
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
                var waitingForAlphaRuntimeContact = PrepareAlphaRuntimeContact(self, team, world.Level);
                steeringOutput = waitingForAlphaRuntimeContact
                    ? new SteeringOutput()
                    : _steering.Update(self, _navGraph!, _currentPath, world.Level, team);
            }
            else if (string.IsNullOrWhiteSpace(LastProofGraphTrace))
            {
                LastProofGraphTrace = _verifiedProofGraphAsset is null
                    ? "proofGraph=idle reason:not_loaded strict:1"
                    : "proofGraph=idle reason:not_active strict:1";
            }
        }

        if (_alphaNavigation
            && _currentPath is { IsComplete: true }
            && !IsAlphaCompletedRouteAtLiveTarget(world, self))
        {
            TraceNavigationEvent(
                self,
                team,
                $"event=completed_route_not_at_target path={_currentPath.Count} index={_currentPath.CurrentIndex} " +
                $"goalNode={_goalNodeIndex} dynamic={(_hasDynamicRouteTarget ? 1 : 0)} " +
                $"pos=({self.X:0.0},{self.Y:0.0}) goalPos=({_currentGoalPosition.X:0.0},{_currentGoalPosition.Y:0.0})");
            // Steering can satisfy a graph completion window and advance the
            // final waypoint during this same think even though the live body
            // is still outside the gameplay marker (or a moving dynamic
            // target). Do not expose that stale completed route to the rest
            // of the controller; leave the objective active and let the
            // immediate recovery/repath lane own this tick.
            _currentPath = null;
            _goalNodeIndex = -1;
            // Do not rebuild the same terminal path every think while a body,
            // pickup contact, or collision rounding keeps it just outside the
            // live intel marker. The pathless recovery lane remains active,
            // and the graph gets a fresh attachment attempt on the normal
            // one-second repath cadence.
            _repathCooldownTicks = RepathIntervalTicks;
            _hasDynamicRouteTarget = false;
            _steering.Reset();
            MarkAlphaRecoveryPending();
            if (dynamicCtfResolved)
            {
                // The dynamic owner just completed a stale graph terminal.
                // Give it one fresh target-resolution pass now that the old
                // path has been discarded; otherwise the later recovery lane
                // would only know about the macro objective, not the live
                // carrier/intel position that requested this route.
                dynamicCtfResolved = false;
                steeringOutput = new SteeringOutput();
                if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag
                    && !ForceObjectiveNavigationForDiagnostics
                    && !PreferEnemyPlayerObjective
                    && (!engineerCtfDefender || engineerNeedsDynamicCtfResponse)
                    && TryResolveCaptureTheFlagDynamicObjectiveSeek(
                        world,
                        self,
                        team,
                        steeringOutput,
                        out var refreshedDynamicSteering,
                        out var refreshedDynamicTrace))
                {
                    dynamicCtfResolved = true;
                    steeringOutput = refreshedDynamicSteering;
                    LastDirectDriveTrace = refreshedDynamicTrace;
                    _repathCooldownTicks = 0;
                }
            }
        }

        routeSteeringResolvedTimestamp = Stopwatch.GetTimestamp();

        // A dynamic CTF resolver can invalidate the active graph edge while
        // it is trying to reuse it (body collision, failed contact, or a
        // moving target). Recompute this after all route owners have had a
        // chance to touch the path; the pre-dynamic snapshot otherwise
        // mislabels the newly pathless controller as healthy and suppresses
        // the recovery lane for the rest of the tick.
        routeMissingAfterUpdate = routeMissingAfterUpdate
            || _currentPath is null
            || _currentPath.IsComplete
            || _currentPath.Count < 2;
        var routeRecoveryRequested = routeMissingAfterUpdate || steeringOutput.RequestRepath;
        if (!dynamicCtfResolved
            && (!proofGraphRequired || engineerCtfDefender)
            && !proofResolved
            && !tapeResolved
            && !_alphaNavigation
            && TryResolveAtaliaUpperMidJumpDrive(world, self, steeringOutput, out var ataliaEdgeSteering, out var ataliaEdgeTrace))
        {
            steeringOutput = ataliaEdgeSteering;
            LastDirectDriveTrace = ataliaEdgeTrace;
        }

        // Handle repath requests from stuck detection.
        if (!dynamicCtfResolved && (!proofGraphRequired || engineerCtfDefender) && !proofResolved && !tapeResolved && steeringOutput.RequestRepath)
        {
            var repathStartTimestamp = NavigationStageTracingEnabled
                ? Stopwatch.GetTimestamp()
                : 0L;
            HandleSteeringRepathRequest(self, team, steeringOutput);
            TraceSlowNavigationStage(self, "HandleSteeringRepathRequest", repathStartTimestamp);

            // Steering can invalidate the path after routeMissingAfterUpdate
            // was first sampled above. Re-enter alpha recovery immediately on
            // a grounded failed edge so the bot does not spend this tick (or
            // the next local-motion suppression window) with an empty input.
            // This is deliberately limited to failed navigation recovery; the
            // combat resolver and its movement ownership are unchanged.
            if (_alphaNavigation
                && self.IsGrounded
                && (_currentPath is null
                    || _currentPath.IsComplete
                    || _currentPath.Count < 2))
            {
                UpdatePath(world, self, team);
                if (_currentPath is { IsComplete: false, Count: >= 2 })
                {
                    var waitingForReplacementRuntimeContact = PrepareAlphaRuntimeContact(self, team, world.Level);
                    steeringOutput = waitingForReplacementRuntimeContact
                        ? new SteeringOutput()
                        : _steering.Update(self, _navGraph!, _currentPath, world.Level, team);
                }
            }

            routeMissingAfterUpdate = _currentPath is null
                || _currentPath.IsComplete
                || _currentPath.Count < 2;
            routeRecoveryRequested = routeMissingAfterUpdate || steeringOutput.RequestRepath;
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

        var directResolved = false;
        if (!dynamicCtfResolved
            && !_alphaNavigation
            && TryResolveHarvestRightSpoolRecovery(world, self, routeRecoveryRequested, steeringOutput, out var harvestSpoolSteering, out var harvestSpoolTrace))
        {
            steeringOutput = harvestSpoolSteering;
            LastDirectDriveTrace = harvestSpoolTrace;
            directResolved = true;
            proofResolved = false;
            tapeResolved = false;
            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = 0;
            _steering.Reset();
            _objectiveTapeExecutor.Reset();
            _proofRouteExecutor.Reset();
        }

        if (!dynamicCtfResolved
            && !directResolved
            && (!proofGraphRequired || engineerCtfDefender)
            && (!ForceObjectiveNavigationForDiagnostics
                && (TryResolveSpyRetreat(world, self, team, combatTarget, steeringOutput, out var directSteering, out var directTrace)
                    || TryResolveSpyBackstabDrive(world, self, combatTarget, steeringOutput, out directSteering, out directTrace)
                    || TryResolveSniperCombatDrive(world, self, combatTarget, steeringOutput, out directSteering, out directTrace)
                    || TryResolveMedicSupportDrive(world, self, team, healTarget, healTargetSelection.Kind, steeringOutput, out directSteering, out directTrace)
                    || (!proofResolved && !tapeResolved && TryResolveDirectSeek(world, self, team, combatTarget, routeRecoveryRequested, steeringOutput, out directSteering, out directTrace)))))
        {
            steeringOutput = directSteering;
            LastDirectDriveTrace = directTrace;
            directResolved = true;
        }
        if (!dynamicCtfResolved
            && _alphaNavigation
            && world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            && combatTarget is null
            && routeMissingAfterUpdate)
        {
            // A carrier can disappear after the dynamic route owner has
            // invalidated its path. Refresh the macro objective before the
            // pathless recovery lane starts steering toward that stale
            // carrier position.
            var refreshedObjectiveGoal = ForceObjectiveNavigationForDiagnostics
                ? ResolveDiagnosticObjectiveGoal(self, world, team)
                : ObjectiveEvaluator.EvaluateGoal(self, world, team, combatTarget: null);
            var refreshedAlphaGoal = ResolveAlphaObjectiveGoal(world, self, team, refreshedObjectiveGoal);
            if (DistanceBetween(
                    _currentGoalPosition.X,
                    _currentGoalPosition.Y,
                    refreshedAlphaGoal.X,
                    refreshedAlphaGoal.Y) > 96f)
            {
                _currentGoalPosition = refreshedAlphaGoal;
                _currentPath = null;
                _goalNodeIndex = -1;
                _hasDynamicRouteTarget = false;
                _repathCooldownTicks = 0;
            }
        }
        if (!dynamicCtfResolved
            && !directResolved
            && _alphaNavigation
            && routeRecoveryRequested
            && !self.IsGrounded
            && TryResolveAlphaAirborneRecoverySteering(
                self,
                steeringOutput,
                out var alphaAirborneRecoverySteering,
                out var alphaAirborneRecoveryTrace))
        {
            steeringOutput = alphaAirborneRecoverySteering;
            LastDirectDriveTrace = alphaAirborneRecoveryTrace;
            directResolved = true;
        }
        if (!dynamicCtfResolved
            && !directResolved
            && _alphaNavigation
            && routeRecoveryRequested
            && combatTarget is null
            && TryResolveAlphaObjectiveArrivalCorrection(
                world,
                self,
                steeringOutput,
                out var alphaObjectiveCorrectionSteering,
                out var alphaObjectiveCorrectionTrace))
        {
            steeringOutput = alphaObjectiveCorrectionSteering;
            LastDirectDriveTrace = alphaObjectiveCorrectionTrace;
            directResolved = true;
        }
        if (!dynamicCtfResolved
            && !proofResolved
            && !tapeResolved
            && !directResolved
            && routeMissingAfterUpdate
            && TryResolveNoGraphObjectiveSeek(world, self, team, combatTarget, steeringOutput, out var routeFallbackSteering, out var routeFallbackTrace))
        {
            steeringOutput = routeFallbackSteering;
            LastDirectDriveTrace = $"routeFallback {routeFallbackTrace}";
        }
        // A route owner can invalidate the path after the initial missing-path
        // snapshot (for example when a dynamic Intel route reaches a stale
        // terminal node). Keep the alpha CTF recovery contract total: a
        // pathless, combat-free bot must never leave this phase with neutral
        // navigation input. This is deliberately after all combat resolution
        // and is limited to objective traversal, so combat behavior remains
        // unchanged.
        if (_alphaNavigation
            && !proofResolved
            && !tapeResolved
            && (_currentPath is null || _currentPath.IsComplete || _currentPath.Count < 2)
            && IsNeutralNavigationOutput(steeringOutput))
        {
            // A moving carrier can disappear between objective refreshes. If
            // the route is already pathless, do one cheap objective
            // re-evaluation before committing local recovery to the stale
            // carrier position; otherwise the bot can oscillate around the
            // last carrier location for the remainder of the refresh window.
            // This also covers a combat resolver that yielded no movement
            // after invalidating its direct route. Combat target selection and
            // fire/aim synthesis remain unchanged; this guard only restores a
            // movement owner when the route owner is pathless and neutral.
            var refreshedObjectiveGoal = ForceObjectiveNavigationForDiagnostics
                ? ResolveDiagnosticObjectiveGoal(self, world, team)
                : ObjectiveEvaluator.EvaluateGoal(self, world, team, combatTarget: null);
            var refreshedAlphaGoal = ResolveAlphaObjectiveGoal(world, self, team, refreshedObjectiveGoal);
            if (DistanceBetween(
                    _currentGoalPosition.X,
                    _currentGoalPosition.Y,
                    refreshedAlphaGoal.X,
                    refreshedAlphaGoal.Y) > 96f)
            {
                _currentGoalPosition = refreshedAlphaGoal;
                _currentPath = null;
                _goalNodeIndex = -1;
                _hasDynamicRouteTarget = false;
                _repathCooldownTicks = 0;
            }

            var alphaObjectiveTarget = new DirectDriveTarget(
                DirectDriveTargetKind.Objective,
                _currentGoalPosition.X,
                _currentGoalPosition.Y,
                "alphaPathlessObjectiveFinal");
            if (TryResolveAlphaPathlessEscape(
                    world,
                    self,
                    alphaObjectiveTarget,
                    steeringOutput,
                    out var finalRecoverySteering,
                    out var finalRecoveryTrace))
            {
                steeringOutput = finalRecoverySteering;
                LastDirectDriveTrace = $"routeFallback {finalRecoveryTrace}";
            }
        }
        directSteeringResolvedTimestamp = Stopwatch.GetTimestamp();
        ApplyCaptureStrafeHop(world, self, team, ref steeringOutput);
        LastSteeringOutput = steeringOutput;
        TraceRuntimeRecipeExecution(self, team, steeringOutput);
        steeringResolvedTimestamp = Stopwatch.GetTimestamp();

        // 5. Resolve aim.
        var aimHealTarget = ResolveMedicAimHealTarget(world, self, healTarget);
        var (aimX, aimY) = _aimResolver.Resolve(self, combatTarget, aimHealTarget, _navGraph, _currentPath, steeringOutput);

        // 6. Synthesize input.
        var combat = CombatDecisionResolver.Resolve(world, self, combatTarget, healTarget, _combatMemory);
        var synthesisPreviousInput = ResolveNavigationJumpPulsePreviousInput(steeringOutput, _previousInput);
        var input = inputOverride.HasValue
            ? ApplyCombatToInputOverride(self, inputOverride.Value, combat)
            : BotInputSynthesizer.Synthesize(self, steeringOutput, aimX, aimY, combat, synthesisPreviousInput);
        input = ApplyEngineerCaptureTheFlagDefenseInput(world, self, team, input);
        input = ApplyEngineerControlPointInput(world, self, team, input);
        _previousInput = input;
        var thinkElapsedStopwatchTicks = Stopwatch.GetTimestamp() - thinkStartTimestamp;
        var thinkElapsedMilliseconds = thinkElapsedStopwatchTicks * 1000d / Stopwatch.Frequency;
        if (thinkElapsedMilliseconds >= 50d
            || (NavigationEventTracingEnabled && thinkElapsedMilliseconds >= ThinkTimingTraceThresholdMilliseconds))
        {
            var graphMilliseconds = (graphReadyTimestamp - thinkStartTimestamp) * 1000d / Stopwatch.Frequency;
            var targetMilliseconds = (targetSelectionTimestamp - graphReadyTimestamp) * 1000d / Stopwatch.Frequency;
            var pathMilliseconds = (pathUpdatedTimestamp - targetSelectionTimestamp) * 1000d / Stopwatch.Frequency;
            var steeringMilliseconds = (steeringResolvedTimestamp - pathUpdatedTimestamp) * 1000d / Stopwatch.Frequency;
            var routeSteeringMilliseconds = (routeSteeringResolvedTimestamp - pathUpdatedTimestamp) * 1000d / Stopwatch.Frequency;
            var directSteeringMilliseconds = (directSteeringResolvedTimestamp - routeSteeringResolvedTimestamp) * 1000d / Stopwatch.Frequency;
            var postRouteMilliseconds = (steeringResolvedTimestamp - directSteeringResolvedTimestamp) * 1000d / Stopwatch.Frequency;
            LastThinkTimingTrace =
                $"thinkTiming totalMs:{thinkElapsedMilliseconds:0.000} graphMs:{graphMilliseconds:0.000} " +
                $"targetMs:{targetMilliseconds:0.000} pathMs:{pathMilliseconds:0.000} "+
                $"steeringMs:{steeringMilliseconds:0.000} routeMs:{routeSteeringMilliseconds:0.000} " +
                $"directMs:{directSteeringMilliseconds:0.000} postDirectMs:{postRouteMilliseconds:0.000}";
        }
        return input;
    }

    private static PlayerInputSnapshot ResolveNavigationJumpPulsePreviousInput(
        SteeringOutput steeringOutput,
        PlayerInputSnapshot previousInput)
    {
        // SteeringMachine owns the jump cooldown and emits a one-tick recipe
        // pulse. The input synthesizer also edge-detects Up, so a route edge
        // entered immediately after another jump could otherwise inherit the
        // previous Up=true state and silently lose a valid jump-at-tick-zero
        // request. Reset only that synthesis edge for certified navigation;
        // combat/weapon inputs remain unchanged.
        return steeringOutput.RecipeTrace.HasRecipe
            && steeringOutput.Jump
            && previousInput.Up
            ? previousInput with { Up = false }
            : previousInput;
    }

    /// <summary>
    /// Advance only an already-certified OG2 contact. This is intentionally
    /// separate from Think: high-frequency contact ticks exist to keep a measured
    /// jump pulse in the physics time domain, not to rerun objective selection,
    /// combat targeting, or graph search every simulation tick.
    /// </summary>
    public PlayerInputSnapshot ThinkRuntimeContact(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam team)
    {
        if (!_alphaNavigation
            || _navGraph is null
            || _currentPath is null
            || !_currentPath.TryGetCurrentEdge(out var edge)
            || !edge.IsOg2Contact)
        {
            return _previousInput;
        }

        PrepareAlphaRuntimeContact(self, team, world.Level);
        var steeringOutput = _steering.Update(
            self,
            _navGraph,
            _currentPath,
            world.Level,
            team);
        if (steeringOutput.RequestRepath)
        {
            HandleSteeringRepathRequest(self, team, steeringOutput);
        }

        LastSteeringOutput = steeringOutput;
        TraceRuntimeRecipeExecution(self, team, steeringOutput);

        var contactPreviousInput = ResolveNavigationJumpPulsePreviousInput(steeringOutput, _previousInput);
        var input = _previousInput with
        {
            Left = steeringOutput.MoveDirection < 0f,
            Right = steeringOutput.MoveDirection > 0f,
            Up = steeringOutput.Jump && !contactPreviousInput.Up,
            Down = steeringOutput.DropDown,
        };
        _previousInput = input;
        return input;
    }

    /// <summary>
    /// Advance only the already-selected graph edge for a bot whose expensive
    /// objective/target think is intentionally in another scheduler batch.
    /// This keeps edge timers, stuck detection, and jump pulses in physics-tick
    /// time without rerunning objective evaluation or A*.
    /// </summary>
    public bool TryAdvanceCachedNavigation(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam team,
        PlayerInputSnapshot cachedInput,
        out PlayerInputSnapshot input)
    {
        input = cachedInput;
        if (!_alphaNavigation
            || _navGraph is null
            || !self.IsAlive)
        {
            return false;
        }

        // The full brain may be in another scheduler batch when a point is
        // captured or an intel state changes. Refresh the cheap objective
        // contract and route immediately so a completed path cannot leave a
        // bot holding neutral input until its next expensive think.
        RefreshAlphaNavigationStateIfNeeded(world, self, team);
        if (_currentPath is null
            || _currentPath.IsComplete
            || !_currentPath.TryGetCurrentEdge(out _))
        {
            return false;
        }

        PrepareAlphaRuntimeContact(self, team, world.Level);
        var steeringOutput = _steering.Update(
            self,
            _navGraph,
            _currentPath,
            world.Level,
            team);
        if (steeringOutput.RequestRepath)
        {
            HandleSteeringRepathRequest(self, team, steeringOutput);
        }

        LastSteeringOutput = steeringOutput;
        TraceRuntimeRecipeExecution(self, team, steeringOutput);
        var cachedPreviousInput = ResolveNavigationJumpPulsePreviousInput(steeringOutput, cachedInput);
        input = cachedInput with
        {
            Left = steeringOutput.MoveDirection < 0f,
            Right = steeringOutput.MoveDirection > 0f,
            Up = steeringOutput.Jump && !cachedPreviousInput.Up,
            Down = steeringOutput.DropDown,
        };
        _previousInput = input;
        return true;
    }

    private PlayerInputSnapshot ThinkWithoutNavigationGraph(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam team,
        IReadOnlyDictionary<byte, PlayerTeam>? controlledTeamsBySlot)
    {
        if (!_wasAliveLastThink || _lastObservedDeaths != self.Deaths)
        {
            ResetTransientNavigationStateForNewLife();
        }

        _lastObservedDeaths = self.Deaths;
        _wasAliveLastThink = true;

        var engineerCtfDefender = !ForceObjectiveNavigationForDiagnostics
            && IsCaptureTheFlagEngineerDefender(world, self);
        var engineerNeedsDynamicCtfResponse = engineerCtfDefender
            && GetOwnIntelState(world, team).IsCarried;
        if (engineerCtfDefender)
        {
            _objectiveTapeExecutor.Reset();
        }

        var combatTarget = DisableCombatForDiagnostics
            ? null
            : SelectGraphlessCombatTarget(self, world, team);
        LastCombatTarget = combatTarget;
        PlayerEntity? preferredEnemyObjectiveTarget = null;
        if (PreferEnemyPlayerObjective
            && TryFindNearestEnemyPlayer(world, self, team, float.PositiveInfinity, out var preferredEnemy))
        {
            preferredEnemyObjectiveTarget = preferredEnemy;
        }

        var healTargetSelection = DisableCombatForDiagnostics
            ? new MedicHealTargetSelection(null, MedicHealTargetSelectionKind.None)
            : SelectGraphlessMedicHealTargetSelection(world, self, team, controlledTeamsBySlot);
        var healTarget = healTargetSelection.Target;
        LastMedicHealTargetId = healTarget?.Id;
        LastMedicHealTargetIsPocket = healTargetSelection.Kind == MedicHealTargetSelectionKind.Pocket;

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
            var evaluatedGoal = preferredEnemyObjectiveTarget is not null
                ? (preferredEnemyObjectiveTarget.X, preferredEnemyObjectiveTarget.Y)
                : ForceObjectiveNavigationForDiagnostics
                    ? ResolveDiagnosticObjectiveGoal(self, world, team)
                    : ObjectiveEvaluator.EvaluateGoal(self, world, team, combatTarget?.Player);
            _currentGoalPosition = _alphaNavigation
                ? ResolveAlphaObjectiveGoal(world, self, team, evaluatedGoal)
                : evaluatedGoal;
            _objectiveReevalCooldown = ObjectiveReevalIntervalTicks;
        }

        _lastCarryingIntel = self.IsCarryingIntel;
        _currentPath = null;
        _goalNodeIndex = -1;
        _repathCooldownTicks = RepathIntervalTicks;

        var steeringOutput = new SteeringOutput();
        PlayerInputSnapshot? inputOverride = null;
        var directResolved = false;

        if (!ForceObjectiveNavigationForDiagnostics
            && !PreferEnemyPlayerObjective
            && (!engineerCtfDefender || engineerNeedsDynamicCtfResponse)
            && TryResolveCaptureTheFlagDynamicObjectiveSeek(
                world,
                self,
                team,
                steeringOutput,
                out var dynamicCtfSteering,
                out var dynamicCtfTrace))
        {
            steeringOutput = dynamicCtfSteering;
            LastDirectDriveTrace = $"noGraph {dynamicCtfTrace}";
            directResolved = true;
        }

        if (!directResolved
            && !PreferEnemyPlayerObjective
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
            steeringOutput = carrierReturnSteering;
            inputOverride = carrierReturnInputOverride;
            LastDirectDriveTrace = $"noGraph {carrierReturnTrace}";
            directResolved = true;
        }

        if (!directResolved
            && (!ForceObjectiveNavigationForDiagnostics
                && (TryResolveSpyRetreat(world, self, team, combatTarget, steeringOutput, out var directSteering, out var directTrace)
                    || TryResolveSpyBackstabDrive(world, self, combatTarget, steeringOutput, out directSteering, out directTrace)
                    || TryResolveSniperCombatDrive(world, self, combatTarget, steeringOutput, out directSteering, out directTrace)
                    || TryResolveMedicSupportDrive(world, self, team, healTarget, healTargetSelection.Kind, steeringOutput, out directSteering, out directTrace)
                    || TryResolveNoGraphObjectiveSeek(world, self, team, combatTarget, steeringOutput, out directSteering, out directTrace))))
        {
            steeringOutput = directSteering;
            LastDirectDriveTrace = $"noGraph {directTrace}";
            directResolved = true;
        }

        if (!directResolved)
        {
            LastDirectDriveTrace =
                $"noGraphObjective=idle target:({_currentGoalPosition.X:0.0},{_currentGoalPosition.Y:0.0})";
        }

        ApplyCaptureStrafeHop(world, self, team, ref steeringOutput);
        LastSteeringOutput = steeringOutput;

        var aimHealTarget = ResolveMedicAimHealTarget(world, self, healTarget);
        var (aimX, aimY) = _aimResolver.Resolve(self, combatTarget, aimHealTarget, graph: null, path: null, steeringOutput);
        var combat = CombatDecisionResolver.Resolve(world, self, combatTarget, healTarget, _combatMemory);
        var input = inputOverride.HasValue
            ? ApplyCombatToInputOverride(self, inputOverride.Value, combat)
            : BotInputSynthesizer.Synthesize(self, steeringOutput, aimX, aimY, combat, _previousInput);
        input = ApplyEngineerCaptureTheFlagDefenseInput(world, self, team, input);
        input = ApplyEngineerControlPointInput(world, self, team, input);
        _previousInput = input;
        return input;
    }

    private bool PrepareAlphaRuntimeContact(
        PlayerEntity self,
        PlayerTeam team,
        SimpleLevel level)
    {
        if (!_alphaNavigation
            || _navGraph is null
            || _currentPath is null
            || !_currentPath.TryGetCurrentEdge(out var edge)
            || !edge.IsOg2Contact
            || edge.Kind != NavEdgeKind.Jump
            || _currentPath.CurrentIndex <= 0)
        {
            _runtimeContactPathIndex = -1;
            _runtimeContactFromNode = -1;
            _runtimeContactToNode = -1;
            _runtimeContactProbeAttempted = false;
            _runtimeContactRetryThinkTick = 0;
            _runtimeContactFailureCount = 0;
            return false;
        }

        var fromNode = _currentPath.GetWaypoint(_currentPath.CurrentIndex - 1);
        var toNode = _currentPath.CurrentNode;
        if (edge.RuntimeResolutionExhausted)
        {
            return false;
        }

        // A grounded-start contact cannot be re-proved from a support surface
        // that is materially below its graph source. This is the common
        // knock-off state under Corinth's point: spending three probe retries
        // there only burns CPU because the edge's launch contract is on the
        // upper platform. Mark it unavailable immediately and let recovery
        // attach to the actual lower support.
        if (self.IsGrounded
            && edge.LaunchRecipe.StartGrounded
            && !edge.IsRuntimeResolved
            && MathF.Abs(self.Y - _navGraph.GetNode(fromNode).Y) > AlphaRuntimeContactSourceVerticalTolerance)
        {
            _currentPath.ReplaceIncomingEdge(
                _currentPath.CurrentIndex,
                // This recipe is certified for the upper source surface, not
                // for the lower support where the live body actually landed.
                // Remove it as a fallback as well: retaining it makes
                // SteeringMachine replay an impossible jump until its long
                // watchdog expires instead of immediately reattaching the
                // graph from the real support.
                edge with
                {
                    RuntimeResolutionExhausted = true,
                    LaunchRecipe = default,
                    JumpTriggerTick = -1,
                    ProbeTicks = 0,
                });
            _runtimeContactRetryThinkTick = int.MaxValue;
            return false;
        }

        var sameContact = _runtimeContactProbeAttempted
            && _runtimeContactPathIndex == _currentPath.CurrentIndex
            && _runtimeContactFromNode == fromNode
            && _runtimeContactToNode == toNode;
        if (!sameContact)
        {
            _runtimeContactFailureCount = 0;
            _runtimeContactRetryThinkTick = 0;
        }

        // A grounded-start contact cannot be proved from an airborne handoff.
        // The next edge can become active on the same fixed update that the
        // previous jump is still settling, so the first runtime-contact pass
        // may see an airborne body even though the edge is valid. Do not spend
        // a probe budget or count a guaranteed failure here; retain the edge
        // identity and let the first grounded sample perform the proof.
        if (edge.LaunchRecipe.StartGrounded
            && !self.IsGrounded)
        {
            _runtimeContactPathIndex = _currentPath.CurrentIndex;
            _runtimeContactFromNode = fromNode;
            _runtimeContactToNode = toNode;
            _runtimeContactProbeAttempted = true;
            return false;
        }

        if (sameContact)
        {
            if (edge.IsRuntimeResolved
                || !self.IsGrounded)
            {
                // Keep steering an unresolved grounded-start edge while the
                // body settles from an airborne handoff. The steering layer
                // supplies horizontal recovery and suppresses the jump
                // pulse; returning true here would freeze the carrier.
                return false;
            }

            // Entry momentum can be outside the certified launch window even
            // though this edge becomes executable after a few settling ticks.
            // Retry at a bounded cadence instead of probing every tick or
            // leaving the bot oscillating at the launch band forever.
            if (_thinkTicks < _runtimeContactRetryThinkTick)
            {
                return false;
            }
        }

        _runtimeContactPathIndex = _currentPath.CurrentIndex;
        _runtimeContactFromNode = fromNode;
        _runtimeContactToNode = toNode;
        _runtimeContactProbeAttempted = true;

        // Most contacts are entered directly inside the graph's certified
        // launch window. Re-simulating those clean entries is both redundant
        // and a source of frame spikes; the runtime probe exists for composed
        // handoffs and recovery states that are outside the canonical window.
        if (edge.LaunchRecipe.StartGrounded
            && self.IsGrounded
            && edge.LaunchRecipe.ContainsLaunchState(self))
        {
            var immediateRecipe = edge.LaunchRecipe with
            {
                LaunchTick = 0,
                PreLaunchBrakeTicks = 0,
            };
            var immediateEdge = edge with
            {
                JumpTriggerTick = 0,
                IsRuntimeResolved = true,
                RequiresGroundedContinuation = true,
                LaunchRecipe = immediateRecipe,
            };
            _currentPath.ReplaceIncomingEdge(_currentPath.CurrentIndex, immediateEdge);
            _runtimeContactRetryThinkTick = int.MaxValue;
            _runtimeContactFailureCount = 0;
            return true;
        }

        var resolved = Og2RuntimeContactPlanner.TryResolve(
                level,
                _navGraph,
                self,
                team,
                fromNode,
                edge,
                out var resolvedEdge);
        if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_TRACE_RUNTIME_CONTACTS") is "1" or "true" or "TRUE")
        {
            Console.WriteLine(
                $"alphaRuntimeContact map={level.Name} team={team} class={self.ClassId} " +
                $"edge={fromNode}->{toNode} kind={edge.Kind} carry={(self.IsCarryingIntel ? 1 : 0)} " +
                $"status={(resolved ? "success" : "fail")} " +
                $"start=({self.X:0.0},{self.Y:0.0}) grounded={(self.IsGrounded ? 1 : 0)} " +
                $"speed={self.HorizontalSpeed:0.0} " +
                (resolved
                    ? $"jump={resolvedEdge.JumpTriggerTick} probe={resolvedEdge.ProbeTicks} " +
                      $"launch=({resolvedEdge.LaunchRecipe.LaunchMinX:0.0},{resolvedEdge.LaunchRecipe.LaunchMaxX:0.0}," +
                      $"{resolvedEdge.LaunchRecipe.LaunchMinY:0.0},{resolvedEdge.LaunchRecipe.LaunchMaxY:0.0})"
                    : string.Empty));
        }

        if (resolved)
        {
            _currentPath.ReplaceIncomingEdge(_currentPath.CurrentIndex, resolvedEdge);
            _runtimeContactRetryThinkTick = int.MaxValue;
            _runtimeContactFailureCount = 0;
        }
        else
        {
            _runtimeContactFailureCount += 1;
            if (_runtimeContactFailureCount >= MaximumRuntimeContactProbeFailures)
            {
                // The live body may be on a lower recovery support from which
                // this edge cannot be re-proved. Do not hold the bot in the
                // unresolved gate forever: mark the canonical edge unavailable
                // so SteeringMachine immediately repaths from the actual
                // support surface.
                _currentPath.ReplaceIncomingEdge(
                    _currentPath.CurrentIndex,
                    edge with { RuntimeResolutionExhausted = true });
                _runtimeContactRetryThinkTick = int.MaxValue;
            }
            else
            {
                _runtimeContactRetryThinkTick = _thinkTicks + 6;
            }
        }

        // A grounded-start contact cannot be certified from an airborne handoff.
        // Keep the edge unresolved, but let SteeringMachine provide the
        // contact's horizontal recovery direction while the body settles.
        // Suppressing all input here can strand a carrier in mid-air when a
        // preceding jump hands off one tick before collision refresh marks the
        // body grounded. SteeringMachine separately suppresses the jump pulse
        // for this unresolved grounded-start edge.
        return false;
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

    private static void ReportAlphaNavigationLoadDiagnostic(SimpleLevel level, NavGraph? graph)
    {
        var loaded = IsNavigationGraphUsable(graph);
        var key = $"alpha:{level.Name}:{level.MapAreaIndex}:{loaded}:{graph?.NodeCount ?? 0}";
        lock (NavigationDiagnosticSync)
        {
            if (!ReportedNavigationDiagnostics.Add(key))
            {
                return;
            }
        }

        Console.WriteLine(
            $"[botbrain] alpha-nav level={level.Name} area={level.MapAreaIndex} " +
            $"loaded={loaded} nodes={graph?.NodeCount ?? 0}");
    }

    private static (float X, float Y) ResolveDiagnosticObjectiveGoal(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam team)
    {
        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            var targetTeam = self.IsCarryingIntel
                ? team
                : team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
            if (world.Level.GetIntelBase(targetTeam) is { } intel)
            {
                return (intel.X, intel.Y);
            }
        }

        return ObjectiveEvaluator.EvaluateGoal(self, world, team, combatTarget: null);
    }

    private static (float X, float Y) ResolveAlphaObjectiveGoal(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        (float X, float Y) evaluatedGoal)
    {
        if (world.MatchRules.Mode is not (GameModeKind.Arena
            or GameModeKind.ControlPoint
            or GameModeKind.KingOfTheHill
            or GameModeKind.DoubleKingOfTheHill)
            || world.ControlPoints.Count == 0)
        {
            return evaluatedGoal;
        }

        // The control-point marker is often a logical/visual object above the
        // floor. The capture zone is the actual gameplay objective and the
        // only objective coordinate that should become a navigation goal.
        // Keep this translation alpha-only so legacy/direct behavior retains
        // its existing objective semantics.
        var point = world.ControlPoints
            .OrderBy(candidate => DistanceBetween(
                candidate.HealingAuraCenterX,
                candidate.HealingAuraCenterY,
                evaluatedGoal.X,
                evaluatedGoal.Y))
            .FirstOrDefault();
        if (point is null)
        {
            return evaluatedGoal;
        }

        RoomObjectMarker? bestZone = null;
        var bestContainsSelf = false;
        var bestDistance = float.PositiveInfinity;
        foreach (var zone in world.Level.GetRoomObjects(RoomObjectType.CaptureZone))
        {
            if (!IsCaptureZoneAssignedToPoint(world, zone, point))
            {
                continue;
            }

            var containsSelf = self.IntersectsMarker(zone.CenterX, zone.CenterY, zone.Width, zone.Height);
            var distance = DistanceBetween(self.X, self.Y, zone.CenterX, zone.CenterY);
            if (bestZone.HasValue
                && ((bestContainsSelf && !containsSelf)
                    || (bestContainsSelf == containsSelf && distance >= bestDistance)))
            {
                continue;
            }

            bestContainsSelf = containsSelf;
            bestDistance = distance;
            bestZone = zone;
        }

        return bestZone.HasValue
            ? (bestZone.Value.CenterX, bestZone.Value.CenterY)
            : evaluatedGoal;
    }

    private static bool IsLegacyNavigationOptIn()
    {
        var mode = Environment.GetEnvironmentVariable("BOTBRAIN_NAVIGATION_MODE");
        return string.Equals(mode, "legacy", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "asset", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdatePath(SimulationWorld world, PlayerEntity self, PlayerTeam team)
    {
        if (_navGraph is null)
        {
            return;
        }

        if (_alphaNavigation
            && _pathObjectiveStateSignature != 0
            && _pathObjectiveStateSignature != ComputeObjectiveStateSignature(world))
        {
            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = 0;
            _alphaRecoveryPending = false;
            _alphaRecoveryNextAttemptThinkTick = 0;
            _alphaRecoverySearchFailure = null;
            _steering.Reset();
            _hasDynamicRouteTarget = false;
        }

        _repathCooldownTicks--;
        if (_alphaNavigation
            && _currentPath is { IsComplete: true }
            && _repathCooldownTicks > 0)
        {
            // A completed route is normally stable for a short period so we
            // do not rerun A* every high-frequency steering update.  A
            // control-point capture is different: it changes the objective
            // for both teams immediately.  Retaining the completed path after
            // that transition leaves the opposing team with neutral steering
            // because alpha navigation has no direct-seek fallback.
            if (_pathObjectiveStateSignature != ComputeObjectiveStateSignature(world))
            {
                _currentPath = null;
                _goalNodeIndex = -1;
                _repathCooldownTicks = 0;
                _steering.Reset();
            }
            else if (IsAtAlphaObjectiveArrival(world, self))
            {
                // Completion of an objective edge is a stable state, not a
                // reason to run A* again on every high-frequency contact
                // update. Keep the completed route for the normal repath
                // interval; objective changes and carrier-state changes still
                // clear this cooldown explicitly.
                return;
            }
            else
            {
                // A graph terminal node is only an attachment hint. The
                // live body may still be outside the gameplay marker because
                // a crate, a teammate, or collision rounding stopped the
                // final approach. Do not let the cooldown turn that partial
                // arrival into a pathless local-motion loop; reattach from
                // the body's actual support surface now.
                _currentPath = null;
                _goalNodeIndex = -1;
                _repathCooldownTicks = RepathIntervalTicks;
                _steering.Reset();
            }
        }

        if (_alphaNavigation
            && _currentPath is { IsComplete: true }
            && _pathObjectiveStateSignature == ComputeObjectiveStateSignature(world)
            && IsAtAlphaObjectiveArrival(world, self))
        {
            // A completed route at the live objective is a stable arrival
            // state, not a request to attach to a nearby node and run A* again
            // every repath interval. Rebuilding here creates tiny corrective
            // paths around a capture zone, which shows up as visible
            // left/right oscillation and needless CPU work. Objective-state
            // changes still invalidate this path above, so an ownership,
            // capping, or intel transition immediately leaves the hold state.
            _repathCooldownTicks = RepathIntervalTicks;
            return;
        }

        var alphaRecoveryReady = _alphaNavigation
            && _alphaRecoveryPending
            && self.IsGrounded
            && _thinkTicks >= _alphaRecoveryNextAttemptThinkTick;
        var needsRepath = _currentPath is null
            ? _repathCooldownTicks <= 0
            : _currentPath.IsComplete
                || (!_alphaNavigation && _repathCooldownTicks <= 0);
        needsRepath |= alphaRecoveryReady;

        if (alphaRecoveryReady)
        {
            // A failed attachment must get a grounded retry, but not an A*
            // hot loop when the body is still inside the same obstruction.
            _alphaRecoveryNextAttemptThinkTick = _thinkTicks + AlphaRecoveryRetryTicks;
        }

        if (_alphaNavigation
            && _currentPath is { IsComplete: false } activePath
            && self.IsGrounded
            && ShouldReplaceStalePathFromCurrentPosition(self, _navGraph, activePath, alphaNavigation: true))
        {
            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = 0;
            _steering.Reset();
            needsRepath = true;
        }

        if (!needsRepath)
        {
            return;
        }

        // A jump/fall is an in-flight movement transaction. Re-attaching to a
        // fresh nearest node while airborne discards its launch edge and can
        // leave the bot pursuing a walk node on the wrong side of a ledge.
        // If a valid path is still active, finish its airborne transaction
        // before attaching to a new node. When a failed edge cleared the path,
        // recovery below is allowed to reattach, but only through the
        // goal-aware start-node selection so it cannot choose a disconnected
        // local component.
        if (_alphaNavigation
            && !self.IsGrounded
            && (_currentPath is { IsComplete: false }
                || self.VerticalSpeed > 0.1f))
        {
            _repathCooldownTicks = RepathIntervalTicks;
            return;
        }

        var pathMissing = _currentPath is null;
        var startNode = _navGraph.FindNearestTraversalStartNode(
            self.X,
            self.Y,
            ResolveTraversalStartMaxAboveDistance(self, pathMissing, _alphaNavigation),
            _alphaNavigation ? AlphaTraversalStartMaxBelowDistance : float.PositiveInfinity);
        var preserveExactControlObjective = ShouldPreserveExactControlObjective();
        // A failed edge invalidates the current path, not the objective. Keep
        // the previously selected alpha goal node as the fast recovery target;
        // the blocked-edge A* below will prove whether the current support can
        // still reach it. Re-running the multi-start goal-aware reachability
        // search for every knockback/missed landing was a major source of
        // recovery-frame spikes on dense maps.
        var canReuseAlphaGoalNode = _alphaNavigation
            && _goalNodeIndex >= 0
            && _pathObjectiveStateSignature == ComputeObjectiveStateSignature(world);
        if (canReuseAlphaGoalNode
            && preserveExactControlObjective
            && startNode == _goalNodeIndex
            && !IsAtAlphaObjectiveArrival(world, self))
        {
            // A recovery attachment can land on the same graph node that was
            // retained as the control-point goal. Reusing that node produces
            // a complete one-node path even though the body is outside the
            // live capture volume; alpha navigation then has no route input
            // and the bot appears inert. Re-resolve the objective anchor from
            // the current support instead of treating the support as arrival.
            canReuseAlphaGoalNode = false;
        }
        var exactGoalNode = preserveExactControlObjective
            ? canReuseAlphaGoalNode
                ? _goalNodeIndex
                : _alphaNavigation
                    ? _navGraph.FindNearestReachableObjectiveNode(
                        _currentGoalPosition.X,
                        _currentGoalPosition.Y,
                        startNode,
                        self.BotGraphClassId,
                        team: team,
                        carryingIntel: self.IsCarryingIntel)
                    : -1
            : _navGraph.FindNearestNode(_currentGoalPosition.X, _currentGoalPosition.Y);
        if (exactGoalNode < 0 && preserveExactControlObjective)
        {
            exactGoalNode = _navGraph.FindNearestReachableNode(
                _currentGoalPosition.X,
                _currentGoalPosition.Y,
                startNode,
                self.BotGraphClassId,
                team: team,
                carryingIntel: self.IsCarryingIntel);
        }
        if (startNode < 0 || exactGoalNode < 0)
        {
            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = RepathIntervalTicks;
            if (alphaRecoveryReady)
            {
                _alphaRecoveryNextAttemptThinkTick = _thinkTicks + AlphaRecoveryRetryTicks;
            }
            return;
        }

        if (alphaRecoveryReady
            && _alphaRecoverySearchFailure is { } cachedFailure
            && cachedFailure.Matches(
                startNode,
                exactGoalNode,
                _pathObjectiveStateSignature,
                _blockedEdgesVersion,
                self.BotGraphClassId,
                team,
                self.IsCarryingIntel)
            && _thinkTicks < cachedFailure.ExpiresThinkTick)
        {
            // A failed alpha recovery search is deterministic while the
            // attachment node, objective, blocked-edge set, and movement
            // profile are unchanged. Do not rerun the dense graph search
            // every six ticks while the failed contact is still blocked.
            _alphaRecoveryNextAttemptThinkTick = cachedFailure.ExpiresThinkTick;
            _repathCooldownTicks = RepathIntervalTicks;
            return;
        }

        if (exactGoalNode == _goalNodeIndex
            && _currentPath is not null
            && !_currentPath.IsComplete
            && !ShouldReplaceStalePathFromCurrentPosition(self, _navGraph, _currentPath, _alphaNavigation))
        {
            _repathCooldownTicks = RepathIntervalTicks;
            return;
        }

        // Don't repath if goal hasn't changed and we have a valid path.
        _repathCooldownTicks = RepathIntervalTicks;

        var activeBlockedEdges = _blockedEdges.Count > 0
            ? _blockedEdges.Keys.ToHashSet()
            : null;
        var objectiveApproachReattach = false;

        if (_alphaNavigation
            && world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            && startNode == exactGoalNode
            && !IsAtAlphaObjectiveArrival(world, self))
        {
            // The nearest traversal attachment can be the objective anchor
            // itself while the live body is still outside the intel marker.
            // A* then returns a legal one-node path, which has no movement
            // edge for the final approach and sends the controller into local
            // recovery. Attach to the closest valid predecessor instead so
            // the graph owns the approach all the way to the marker.
            var approachNode = _navGraph.FindNearestObjectiveApproachNode(
                exactGoalNode,
                self.X,
                self.Y,
                self.BotGraphClassId,
                activeBlockedEdges,
                team,
                self.IsCarryingIntel);
            if (approachNode < 0)
            {
                approachNode = _navGraph.FindNearestObjectiveApproachNode(
                    exactGoalNode,
                    self.X,
                    self.Y,
                    self.BotGraphClassId,
                    team: team,
                    carryingIntel: self.IsCarryingIntel);
            }

            if (approachNode >= 0)
            {
                objectiveApproachReattach = true;
                TraceNavigationEvent(
                    self,
                    team,
                    $"event=objective_approach_reattach fromNode={startNode} approachNode={approachNode} " +
                    $"goalNode={exactGoalNode} pos=({self.X:0.0},{self.Y:0.0})");
                startNode = approachNode;
            }
        }

        var goalNode = exactGoalNode;
        var rejectDistantGoalProxy = ShouldRejectCarrierReturnDistantGoalProxy(_lastLevel, self, team, _currentGoalPosition);
        var goalAwareBlockedStartSearched = false;
        if (_alphaNavigation
            && activeBlockedEdges is { Count: > 0 }
            && exactGoalNode != startNode
            && !objectiveApproachReattach)
        {
            // A blocked-edge recovery search is already asking the graph to
            // find a support that can reach the exact objective. Do that
            // attachment selection before attempting A* from the known
            // blocked support; otherwise the first search can exhaust the
            // entire directed graph just to prove that this start is dead.
            var reachableStartNode = _navGraph.FindNearestTraversalStartNodeForGoal(
                self.X,
                self.Y,
                ResolveTraversalStartMaxAboveDistance(self, pathMissing, alphaNavigation: true),
                AlphaTraversalStartMaxBelowDistance,
                exactGoalNode,
                self.BotGraphClassId,
                activeBlockedEdges,
                team,
                self.IsCarryingIntel,
                maxHorizontalDistance: AlphaRecoveryMaxHorizontalAttachmentDistance);
            goalAwareBlockedStartSearched = true;
            if (reachableStartNode >= 0)
            {
                startNode = reachableStartNode;
            }
        }

        var refreshedPath = _navGraph.FindPath(
            startNode,
            goalNode,
            self.BotGraphClassId,
            activeBlockedEdges,
            team,
            self.IsCarryingIntel,
            traceContext: "UpdatePath",
            routeVariant: ResolveRouteVariant(self));

        if (refreshedPath is null
            && _alphaNavigation
            && exactGoalNode != startNode
            && !objectiveApproachReattach
            && !goalAwareBlockedStartSearched)
        {
            var reachableStartNode = _navGraph.FindNearestTraversalStartNodeForGoal(
                self.X,
                self.Y,
                ResolveTraversalStartMaxAboveDistance(self, pathMissing, alphaNavigation: true),
                AlphaTraversalStartMaxBelowDistance,
                exactGoalNode,
                self.BotGraphClassId,
                activeBlockedEdges,
                team,
                self.IsCarryingIntel,
                maxHorizontalDistance: AlphaRecoveryMaxHorizontalAttachmentDistance);
            if (reachableStartNode >= 0)
            {
                startNode = reachableStartNode;
                goalNode = exactGoalNode;
                if (startNode == exactGoalNode
                    && !IsAtAlphaObjectiveArrival(world, self))
                {
                    var approachNode = _navGraph.FindNearestObjectiveApproachNode(
                        exactGoalNode,
                        self.X,
                        self.Y,
                        self.BotGraphClassId,
                        activeBlockedEdges,
                        team,
                        self.IsCarryingIntel);
                    if (approachNode < 0)
                    {
                        approachNode = _navGraph.FindNearestObjectiveApproachNode(
                            exactGoalNode,
                            self.X,
                            self.Y,
                            self.BotGraphClassId,
                            team: team,
                            carryingIntel: self.IsCarryingIntel);
                    }

                    if (approachNode >= 0)
                    {
                        objectiveApproachReattach = true;
                        TraceNavigationEvent(
                            self,
                            team,
                            $"event=objective_approach_reattach_after_reachable_start " +
                            $"fromNode={startNode} approachNode={approachNode} goalNode={exactGoalNode} " +
                            $"pos=({self.X:0.0},{self.Y:0.0})");
                        startNode = approachNode;
                    }
                }
                refreshedPath = _navGraph.FindPath(
                    startNode,
                    goalNode,
                    self.BotGraphClassId,
                    activeBlockedEdges,
                    team,
                    self.IsCarryingIntel,
                    traceContext: "UpdatePath:reachableStart",
                    routeVariant: ResolveRouteVariant(self));
            }
        }

        var alphaCaptureTheFlag = _alphaNavigation
            && _lastLevel?.Mode == GameModeKind.CaptureTheFlag;
        if (refreshedPath is null
            && !alphaCaptureTheFlag
            && !(preserveExactControlObjective && activeBlockedEdges is not null))
        {
            goalNode = _navGraph.FindNearestReachableNode(
                _currentGoalPosition.X,
                _currentGoalPosition.Y,
                startNode,
                self.BotGraphClassId,
                activeBlockedEdges,
                team,
                self.IsCarryingIntel);
            refreshedPath = !IsRejectedDistantDirectSeekRouteGoalProxy(_navGraph, goalNode, _currentGoalPosition.X, _currentGoalPosition.Y, rejectDistantGoalProxy)
                && (goalNode != startNode || exactGoalNode == startNode)
                ? _navGraph.FindPath(
                    startNode,
                    goalNode,
                    self.BotGraphClassId,
                    activeBlockedEdges,
                    team,
                    self.IsCarryingIntel,
                    traceContext: "UpdatePath:reachableGoal",
                    routeVariant: ResolveRouteVariant(self))
                : null;
        }

        if (refreshedPath is null
            && activeBlockedEdges is not null
            && !_alphaNavigation
            && !preserveExactControlObjective
            && (!self.IsCarryingIntel || !ShouldPreserveCarrierFailedEdgeBlocks(_lastLevel, self)))
        {
            _blockedEdges.Clear();
            activeBlockedEdges = null;
            goalNode = exactGoalNode;
            if (_alphaNavigation)
            {
                var reachableStartNode = _navGraph.FindNearestTraversalStartNodeForGoal(
                    self.X,
                    self.Y,
                    ResolveTraversalStartMaxAboveDistance(self, pathMissing, alphaNavigation: true),
                    AlphaTraversalStartMaxBelowDistance,
                    exactGoalNode,
                    self.BotGraphClassId,
                    team: team,
                    carryingIntel: self.IsCarryingIntel,
                    maxHorizontalDistance: AlphaRecoveryMaxHorizontalAttachmentDistance);
                if (reachableStartNode >= 0)
                {
                    startNode = reachableStartNode;
                }
            }
            refreshedPath = _navGraph.FindPath(
                startNode,
                goalNode,
                self.BotGraphClassId,
                team: team,
                carryingIntel: self.IsCarryingIntel,
                traceContext: "UpdatePath:unblockedLegacy",
                routeVariant: ResolveRouteVariant(self));
        }

        if (refreshedPath is null
            && activeBlockedEdges is not null
            && _alphaNavigation
            && alphaCaptureTheFlag)
        {
            // Failed-edge blocks are a temporary live-collision memory, not a
            // permanent topology change. If every currently unblocked route
            // has been exhausted, retaining the empty result strands the bot
            // in pathless local motion even though the static graph still has
            // a route to the objective. Prefer that route over inertness; a
            // repeated failure will reinsert the edge into the block set after
            // the bot has had a bounded recovery attempt.
            var unblockedPath = _navGraph.FindPath(
                startNode,
                exactGoalNode,
                self.BotGraphClassId,
                team: team,
                carryingIntel: self.IsCarryingIntel,
                traceContext: "UpdatePath:unblockedAlpha",
                routeVariant: ResolveRouteVariant(self));
            if (unblockedPath is not null)
            {
                refreshedPath = unblockedPath;
                _blockedEdges.Clear();
                _blockedEdgesVersion += 1;
                activeBlockedEdges = null;
                TraceNavigationEvent(
                    self,
                    team,
                    $"event=blocked_route_fallback startNode={startNode} goalNode={exactGoalNode} " +
                    $"waypoints={unblockedPath.Count}");
            }
        }

        // A fallback reachable goal can still resolve to the current goal even when
        // the exact nearest node changed, so keep the post-fallback reuse guard too.
        if (goalNode == _goalNodeIndex
            && _currentPath is not null
            && !_currentPath.IsComplete
            && !ShouldReplaceStalePathFromCurrentPosition(self, _navGraph, _currentPath, _alphaNavigation))
        {
            _repathCooldownTicks = RepathIntervalTicks;
            return;
        }

        if (refreshedPath is null)
        {
            TraceNavigationEvent(
                self,
                team,
                $"event=path_search_failed startNode={startNode} goalNode={goalNode} " +
                $"exactGoalNode={exactGoalNode} blockedCount={_blockedEdges.Count} " +
                $"recoveryReady={(alphaRecoveryReady ? 1 : 0)} " +
                $"pos=({self.X:0.0},{self.Y:0.0}) goalPos=({_currentGoalPosition.X:0.0},{_currentGoalPosition.Y:0.0})");
            if (_alphaNavigation && _currentPath is { IsComplete: true })
            {
                // The completed path was already proven to be short of the
                // live objective marker. If the fresh attachment search cannot
                // connect from the body's current support, retaining that
                // completed path makes every subsequent tick look like a
                // successful terminal route and suppresses clean recovery.
                _currentPath = null;
                _steering.Reset();
            }

            if (alphaRecoveryReady)
            {
                _alphaRecoverySearchFailure = new AlphaRecoverySearchFailure(
                    startNode,
                    goalNode,
                    _pathObjectiveStateSignature,
                    _blockedEdgesVersion,
                    self.BotGraphClassId,
                    team,
                    self.IsCarryingIntel,
                    _thinkTicks + AlphaRecoveryNegativeCacheTicks);
                _alphaRecoveryNextAttemptThinkTick = _thinkTicks + AlphaRecoveryNegativeCacheTicks;
            }

            return;
        }

        if (_alphaNavigation
            && refreshedPath.Count < 2
            && !IsAtAlphaObjectiveArrival(world, self))
        {
            TraceNavigationEvent(
                self,
                team,
                $"event=one_node_route_not_at_target node={goalNode} startNode={startNode} " +
                $"pos=({self.X:0.0},{self.Y:0.0}) goalPos=({_currentGoalPosition.X:0.0},{_currentGoalPosition.Y:0.0})");
            // The graph objective anchor is the correct terminal support, but
            // the live gameplay objective can still reject the body by a few
            // pixels at the edge of its capture volume. Keep the graph goal
            // for the terminal correction phase without exposing a completed
            // one-node route to the steering state machine.
            _currentPath = null;
            _goalNodeIndex = goalNode;
            _pathObjectiveStateSignature = ComputeObjectiveStateSignature(world);
            _repathCooldownTicks = RepathIntervalTicks;
            _steering.Reset();
            if (alphaRecoveryReady)
            {
                _alphaRecoveryNextAttemptThinkTick = _thinkTicks + AlphaRecoveryRetryTicks;
            }
            return;
        }

        _currentPath = refreshedPath;
        _hasDynamicRouteTarget = false;
        _goalNodeIndex = goalNode;
        _pathObjectiveStateSignature = ComputeObjectiveStateSignature(world);
        _alphaRecoveryPending = false;
        _alphaRecoveryNextAttemptThinkTick = 0;
        _alphaRecoverySearchFailure = null;
        if (_currentPath is not null)
        {
            _steering.Reset();
            TraceNavigationEvent(
                self,
                team,
                $"event=path_assigned start=({self.X:0.0},{self.Y:0.0}) startNode={startNode} goalNode={goalNode} " +
                $"waypoints={_currentPath.Count} path={FormatPath(_currentPath, _navGraph)} " +
                $"goalPos=({_currentGoalPosition.X:0.0},{_currentGoalPosition.Y:0.0}) " +
                $"objectiveArrival={(IsAtAlphaObjectiveArrival(world, self) ? 1 : 0)}");
        }
    }

    private bool IsAtAlphaObjectiveArrival(SimulationWorld world, PlayerEntity self)
    {
        var isControlPointMode = world.MatchRules.Mode is GameModeKind.Arena
            or GameModeKind.ControlPoint
            or GameModeKind.KingOfTheHill
            or GameModeKind.DoubleKingOfTheHill;
        if (isControlPointMode)
        {
            foreach (var point in world.ControlPoints)
            {
                if (DistanceBetween(
                        _currentGoalPosition.X,
                        _currentGoalPosition.Y,
                        point.HealingAuraCenterX,
                        point.HealingAuraCenterY) <= 128f
                    && world.IsPlayerInControlPointCaptureZone(self, point.Index))
                {
                    return true;
                }
            }

            // Being near the logical marker is not enough for a control-point
            // arrival. Some stock maps place the marker above the actual
            // capture volume, and the bot must keep routing until the runtime
            // capture-zone test is true.
            return false;
        }

        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            && self.IsCarryingIntel)
        {
            // A graph terminal near home is not a completed return. The
            // gameplay scorer uses the actual 24x24 intel marker, so use that
            // same contact test for carrier arrival instead of the broad
            // graph-distance tolerance. Otherwise a carrier can keep a
            // completed path while standing just outside the score marker and
            // never enter the terminal recovery lane.
            var ownBase = world.Level.GetIntelBase(self.Team);
            return ownBase.HasValue
                && self.IntersectsMarker(ownBase.Value.X, ownBase.Value.Y, 24f, 24f);
        }

        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            && !self.IsCarryingIntel
            && IsAlphaCtfEnemyIntelGoal(world, self.Team))
        {
            // Reaching an intel anchor is not the same as completing the CTF
            // objective. An outbound bot can be within the graph terminal's
            // tolerance while a crate, teammate, pickup cooldown, or marker
            // edge still prevents the actual pickup. Only a carrier arriving
            // at its own base has completed the CTF navigation leg. If the
            // enemy intel is already carried, however, this static base goal
            // is no longer actionable; the dynamic carrier resolver owns the
            // next leg and the completed anchor must not hot-loop.
            if (GetEnemyIntelState(world, self.Team).IsCarried)
            {
                return true;
            }

            // Outbound bots must retain the final approach until the world
            // flips their carrying state.
            return false;
        }

        return DistanceBetween(
                self.X,
                self.Y,
                _currentGoalPosition.X,
                _currentGoalPosition.Y) <= 48f;
    }

    private bool IsAlphaCompletedRouteAtLiveTarget(SimulationWorld world, PlayerEntity self)
    {
        if (_hasDynamicRouteTarget)
        {
            var dy = _dynamicRouteTargetPosition.Y - self.Y;
            return DistanceBetween(
                    self.X,
                    self.Y,
                    _dynamicRouteTargetPosition.X,
                    _dynamicRouteTargetPosition.Y) <= DroppedIntelNearHoldDistance
                && MathF.Abs(dy) <= DroppedIntelNearHorizontalDeadZone;
        }

        return IsAtAlphaObjectiveArrival(world, self);
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

    private static bool ShouldReplaceStalePathFromCurrentPosition(
        PlayerEntity self,
        NavGraph graph,
        NavPath path,
        bool alphaNavigation = false)
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

        if (alphaNavigation
            && self.IsGrounded
            && targetNode.Y > self.Y + AlphaTraversalStartMaxBelowDistance)
        {
            return true;
        }

        return targetNode.Y <= self.Y - 40f
            && targetDx < 128f;
    }

    private static float ResolveTraversalStartMaxAboveDistance(
        PlayerEntity self,
        bool pathMissing = false,
        bool alphaNavigation = false)
    {
        if (self.IsGrounded)
        {
            return alphaNavigation
                ? AlphaGroundedStartNodeMaxAboveDistance
                : GroundedStartNodeMaxAboveDistance;
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
        _alphaRecoveryPending = false;
        _alphaRecoveryNextAttemptThinkTick = 0;
        _dynamicRouteRetryCooldownTicks = 0;
        _hasDynamicRouteTarget = false;
        _dynamicRouteTargetPosition = default;
        _platformLadderStage = 0;
        _platformLadderSide = 0f;
        _ataliaPointClimbStage = 0;
        _ataliaPointClimbSide = 0f;
        _ataliaCentralRecoveryStage = 0;
        _harvestRightSpoolLowMotionTicks = 0;
        _alphaPathlessEscapeDirection = 0;
        _alphaPathlessEscapeUntilThinkTick = 0;
        ResetAlphaDynamicRecoveryProgress();
        ResetDynamicRouteProgress();
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
        _runtimeContactPathIndex = -1;
        _runtimeContactFromNode = -1;
        _runtimeContactToNode = -1;
        _runtimeContactProbeAttempted = false;
        _runtimeContactRetryThinkTick = 0;
        _runtimeContactFailureCount = 0;
        _pathObjectiveStateSignature = 0;
        _alphaRecoverySearchFailure = null;
        _objectiveTapeExecutor.Reset();
        _proofRouteExecutor.Reset();
        _combatMemory.BeenHealingTicks = 0;
        _combatMemory.ReloadCounterTicks = 0;
        _combatMemory.ZoomToShootTicks = 50;
        _lastObservedDeaths = -1;
        _wasAliveLastThink = false;
        ResetGraphlessTargetSelection();
        LastMedicHealTargetId = null;
        LastMedicHealTargetIsPocket = false;
    }

    private static int ComputeObjectiveStateSignature(SimulationWorld world)
    {
        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            unchecked
            {
                var signature = 17;
                signature = (signature * 31) + (int)world.MatchRules.Mode;
                signature = AppendIntelStateSignature(signature, world.RedIntel);
                signature = AppendIntelStateSignature(signature, world.BlueIntel);
                return signature == 0 ? 1 : signature;
            }
        }

        if (world.MatchRules.Mode is not (GameModeKind.Arena
            or GameModeKind.ControlPoint
            or GameModeKind.KingOfTheHill
            or GameModeKind.DoubleKingOfTheHill))
        {
            return 1;
        }

        unchecked
        {
            var signature = 17;
            signature = (signature * 31) + (int)world.MatchRules.Mode;
            foreach (var point in world.ControlPoints)
            {
                signature = (signature * 31) + point.Index;
                signature = (signature * 31) + (point.Team.HasValue ? (int)point.Team.Value + 1 : 0);
                signature = (signature * 31) + (point.CappingTeam.HasValue ? (int)point.CappingTeam.Value + 1 : 0);
                signature = (signature * 31) + (point.IsLocked ? 1 : 0);
            }

            return signature == 0 ? 1 : signature;
        }
    }

    private static int AppendIntelStateSignature(int signature, TeamIntelligenceState intel)
    {
        signature = (signature * 31) + (intel.IsAtBase ? 1 : 0);
        signature = (signature * 31) + (intel.IsDropped ? 1 : 0);
        if (intel.IsDropped)
        {
            signature = (signature * 31) + (int)MathF.Round(intel.X / 16f);
            signature = (signature * 31) + (int)MathF.Round(intel.Y / 16f);
        }

        return signature;
    }

    private void RefreshAlphaNavigationStateIfNeeded(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team)
    {
        var objectiveStateChanged = _pathObjectiveStateSignature != 0
            && _pathObjectiveStateSignature != ComputeObjectiveStateSignature(world);
        var carryingStateChanged = self.IsCarryingIntel != _lastCarryingIntel;
        if (!objectiveStateChanged && !carryingStateChanged)
        {
            return;
        }

        _currentPath = null;
        _hasDynamicRouteTarget = false;
        _goalNodeIndex = -1;
        _repathCooldownTicks = 0;
        _alphaRecoveryPending = false;
        _alphaRecoveryNextAttemptThinkTick = 0;
        _objectiveReevalCooldown = 0;
        _steering.Reset();
        _stochasticLocalMotionPlanner.Reset();

        var evaluatedGoal = ObjectiveEvaluator.EvaluateGoal(self, world, team, combatTarget: null);
        _currentGoalPosition = ResolveAlphaObjectiveGoal(world, self, team, evaluatedGoal);
        _lastCarryingIntel = self.IsCarryingIntel;
        UpdatePath(world, self, team);
    }

    private static bool IsNavigationGraphUsable(NavGraph? graph) =>
        graph is { NodeCount: > 0 };

    private static int ResolveRouteVariant(PlayerEntity self)
    {
        unchecked
        {
            var hash = (uint)self.Id;
            hash ^= hash >> 16;
            hash *= 2_246_822_519u;
            hash ^= hash >> 13;
            return (int)(hash % 31u) + 1;
        }
    }

    private void ResetTransientNavigationStateForNewLife()
    {
        _currentPath = null;
        _hasDynamicRouteTarget = false;
        _dynamicRouteTargetPosition = default;
        _goalNodeIndex = -1;
        _repathCooldownTicks = 0;
        _carrierCapFinishRunupUntilTick = 0;
        _carrierCapFinishAttackUntilTick = 0;
        _alphaRecoveryPending = false;
        _alphaRecoveryNextAttemptThinkTick = 0;
        _platformLadderStage = 0;
        _platformLadderSide = 0f;
        _ataliaPointClimbStage = 0;
        _ataliaPointClimbSide = 0f;
        _ataliaCentralRecoveryStage = 0;
        _harvestRightSpoolLowMotionTicks = 0;
        _alphaPathlessEscapeDirection = 0;
        _alphaPathlessEscapeUntilThinkTick = 0;
        ResetDynamicRouteProgress();
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
        _runtimeContactPathIndex = -1;
        _runtimeContactFromNode = -1;
        _runtimeContactToNode = -1;
        _runtimeContactProbeAttempted = false;
        _runtimeContactRetryThinkTick = 0;
        _runtimeContactFailureCount = 0;
        _pathObjectiveStateSignature = 0;
        _alphaRecoverySearchFailure = null;
        _objectiveTapeExecutor.Reset();
        _proofRouteExecutor.Reset();
        _combatMemory.BeenHealingTicks = 0;
        _combatMemory.ReloadCounterTicks = 0;
        _combatMemory.ZoomToShootTicks = 50;
        ResetGraphlessTargetSelection();
    }

    private BotBrainCombatTarget? SelectGraphlessCombatTarget(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam team)
    {
        if (!_graphlessCombatTargetRefreshInitialized)
        {
            _graphlessCombatTargetRefreshInitialized = true;
            _graphlessCombatTargetRefreshCooldown = PositiveModulo(self.Id, GraphlessCombatTargetRefreshTicks);
        }

        if (_graphlessCombatTargetRefreshCooldown > 0)
        {
            _graphlessCombatTargetRefreshCooldown -= 1;
            if (_graphlessCombatTarget is { } cachedTarget
                && TryRefreshReusableGraphlessCombatTarget(cachedTarget, self, team, out var refreshedTarget))
            {
                _graphlessCombatTarget = refreshedTarget;
                return refreshedTarget;
            }

            _graphlessCombatTarget = null;
            return null;
        }

        _graphlessCombatTarget = TargetSelector.SelectCombatTarget(self, world, team);
        _graphlessCombatTargetRefreshCooldown = GraphlessCombatTargetRefreshTicks - 1;
        return _graphlessCombatTarget;
    }

    private MedicHealTargetSelection SelectGraphlessMedicHealTargetSelection(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        IReadOnlyDictionary<byte, PlayerTeam>? controlledTeamsBySlot)
    {
        if (self.ClassId != PlayerClass.Medic)
        {
            ResetGraphlessMedicHealTargetSelection();
            return default;
        }

        if (!_graphlessMedicHealTargetRefreshInitialized)
        {
            _graphlessMedicHealTargetRefreshInitialized = true;
            _graphlessMedicHealTargetRefreshCooldown = PositiveModulo(self.Id + 2, GraphlessMedicHealTargetRefreshTicks);
        }

        if (_graphlessMedicHealTargetRefreshCooldown > 0)
        {
            _graphlessMedicHealTargetRefreshCooldown -= 1;
            if (IsReusableGraphlessMedicHealTarget(_graphlessHealTargetSelection, self, team))
            {
                return _graphlessHealTargetSelection;
            }

            _graphlessHealTargetSelection = default;
            return default;
        }

        _graphlessHealTargetSelection = CombatDecisionResolver.FindBestMedicHealTargetSelection(
            world,
            self,
            team,
            controlledTeamsBySlot);
        _graphlessMedicHealTargetRefreshCooldown = GraphlessMedicHealTargetRefreshTicks - 1;
        return _graphlessHealTargetSelection;
    }

    private void ResetGraphlessTargetSelection()
    {
        _graphlessCombatTarget = null;
        _graphlessCombatTargetRefreshCooldown = 0;
        _graphlessCombatTargetRefreshInitialized = false;
        ResetGraphlessMedicHealTargetSelection();
    }

    private void ResetGraphlessMedicHealTargetSelection()
    {
        _graphlessHealTargetSelection = default;
        _graphlessMedicHealTargetRefreshCooldown = 0;
        _graphlessMedicHealTargetRefreshInitialized = false;
    }

    private static bool TryRefreshReusableGraphlessCombatTarget(
        BotBrainCombatTarget target,
        PlayerEntity self,
        PlayerTeam team,
        out BotBrainCombatTarget refreshedTarget)
    {
        refreshedTarget = default;
        if (!self.IsAlive)
        {
            return false;
        }

        var maxEngagementRange = ResolveGraphlessCombatTargetMaxRange(self);
        var maxEngagementDistanceSquared = maxEngagementRange * maxEngagementRange;
        switch (target.Kind)
        {
            case BotBrainCombatTargetKind.Player:
                if (target.Player is not { } player
                    || !player.IsAlive
                    || player.Id == self.Id)
                {
                    return false;
                }

                var opposingTeam = team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
                var treatAsFriendlyFireTarget = SimulationWorld.ShouldTreatPlayerAsExperimentalFriendlyFireTarget(self, player);
                if (player.Team != opposingTeam && !treatAsFriendlyFireTarget)
                {
                    return false;
                }

                if (!CombatDecisionResolver.IsPlayerVisibleToBot(self, player)
                    || DistanceSquared(self.X, self.Y, player.X, player.Y) >= maxEngagementDistanceSquared)
                {
                    return false;
                }

                refreshedTarget = new BotBrainCombatTarget(
                    BotBrainCombatTargetKind.Player,
                    player.Team,
                    player.X,
                    player.Y,
                    Player: player);
                return true;

            case BotBrainCombatTargetKind.Sentry:
                if (target.Sentry is not { } sentry
                    || sentry.Team == team
                    || sentry.Health <= 0
                    || DistanceSquared(self.X, self.Y, sentry.X, sentry.Y) >= maxEngagementDistanceSquared)
                {
                    return false;
                }

                refreshedTarget = new BotBrainCombatTarget(
                    BotBrainCombatTargetKind.Sentry,
                    sentry.Team,
                    sentry.X,
                    sentry.Y,
                    Sentry: sentry);
                return true;

            case BotBrainCombatTargetKind.Generator:
                if (target.Generator is not { } generator
                    || generator.Team == team
                    || generator.IsDestroyed)
                {
                    return false;
                }

                var generatorX = generator.Marker.CenterX;
                var generatorY = generator.Marker.CenterY;
                if (DistanceSquared(self.X, self.Y, generatorX, generatorY) >= maxEngagementDistanceSquared)
                {
                    return false;
                }

                refreshedTarget = new BotBrainCombatTarget(
                    BotBrainCombatTargetKind.Generator,
                    generator.Team,
                    generatorX,
                    generatorY,
                    Generator: generator);
                return true;

            default:
                return false;
        }
    }

    private static bool IsReusableGraphlessMedicHealTarget(
        MedicHealTargetSelection selection,
        PlayerEntity self,
        PlayerTeam team)
    {
        if (selection.Kind == MedicHealTargetSelectionKind.None
            || self.ClassId != PlayerClass.Medic
            || !self.IsAlive
            || selection.Target is not { } target
            || !target.IsAlive
            || target.Team != team
            || target.Id == self.Id
            || IsGraphlessCloakedSpy(target))
        {
            return false;
        }

        var maxDistance = selection.Kind == MedicHealTargetSelectionKind.HumanMedicCall
            ? GraphlessMedicHumanCallTargetMaxDistance
            : GraphlessMedicHealTargetMaxDistance;
        return DistanceSquared(self.X, self.Y, target.X, target.Y) <= maxDistance * maxDistance;
    }

    private static bool IsGraphlessCloakedSpy(PlayerEntity target) =>
        target.ClassId == PlayerClass.Spy && target.IsSpyCloaked;

    private static float ResolveGraphlessCombatTargetMaxRange(PlayerEntity self) =>
        self.ClassId == PlayerClass.Sniper
            ? GraphlessSniperCombatTargetMaxRange
            : GraphlessCombatTargetMaxRange;

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
        out string trace,
        out NavPath? continuationPath)
    {
        trace = string.Empty;
        continuationPath = null;
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
            ResolveTraversalStartMaxAboveDistance(self, alphaNavigation: _alphaNavigation),
            _alphaNavigation ? AlphaTraversalStartMaxBelowDistance : float.PositiveInfinity);
        if (startNode < 0)
        {
            return false;
        }

        var activeBlockedEdges = _blockedEdges.Count > 0
            ? _blockedEdges.Keys.ToHashSet()
            : [];
        activeBlockedEdges.Add(failedBlock);
        var candidatePath = _navGraph.FindPath(
            startNode,
            _goalNodeIndex,
            self.BotGraphClassId,
            activeBlockedEdges,
            team,
            self.IsCarryingIntel,
            routeVariant: ResolveRouteVariant(self));
        if (candidatePath is null)
        {
            return false;
        }

        if (PathStartsWithFailedEdge(candidatePath, failedBlock))
        {
            return false;
        }

        var start = _navGraph.GetNode(startNode);
        trace =
            $"semanticRecovery=continuation reason:{failedEdge.Reason} failed:{failedEdge.FromNode}->{failedEdge.ToNode}/{failedEdge.Kind} " +
            $"startNode:{startNode}@({start.X:0.0},{start.Y:0.0}) goalNode:{_goalNodeIndex} pathWaypoints:{candidatePath.Count} " +
            $"pos:({self.X:0.0},{self.Y:0.0}) grounded:{(self.IsGrounded ? 1 : 0)} edgeTicks:{failedEdge.EdgeTicks}";
        continuationPath = candidatePath;
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
            _blockedEdgesVersion += 1;
            _alphaRecoverySearchFailure = null;
            var failedEdgeTrace = _currentPath is not null
                && _currentPath.TryGetCurrentEdge(out var failedEdge)
                ? $" og2={(failedEdge.IsOg2Contact ? 1 : 0)} runtime={(failedEdge.IsRuntimeResolved ? 1 : 0)} " +
                  $"recipe={(failedEdge.LaunchRecipe.HasRecipe ? 1 : 0)} jump={failedEdge.JumpTriggerTick} probe={failedEdge.ProbeTicks}"
                : string.Empty;
            TraceNavigationEvent(
                self,
                team,
                $"event=edge_failed edge={failedBlock.FromNode}->{failedBlock.ToNode}/{failedBlock.Kind} " +
                $"ticks={steeringOutput.FailedEdge.EdgeTicks} reason={steeringOutput.FailedEdge.Reason} " +
                $"pos=({self.X:0.0},{self.Y:0.0}) grounded={(self.IsGrounded ? 1 : 0)} " +
                $"speed={self.HorizontalSpeed:0.0} pathIndex={_currentPath?.CurrentIndex ?? -1} " +
                $"goalNode={_goalNodeIndex} blockedCount={_blockedEdges.Count}{failedEdgeTrace}");
            if (TrySemanticContinuationAfterFailedEdge(
                    self,
                    team,
                    steeringOutput.FailedEdge,
                    failedBlock,
                    out var recoveryTrace,
                    out var continuationPath))
            {
                LastSemanticRecoveryTrace = recoveryTrace;
                _currentPath = continuationPath;
                _repathCooldownTicks = RepathIntervalTicks;
                _steering.Reset();
                return;
            }
        }

        _currentPath = null;
        _hasDynamicRouteTarget = false;
        // Preserve the alpha goal across a failed contact. UpdatePath clears
        // it when the objective-state signature changes, and the fallback
        // reachability search still replaces it when this goal cannot be
        // reached from the new support attachment.
        if (!_alphaNavigation)
        {
            _goalNodeIndex = -1;
        }
        _repathCooldownTicks = 0;
        MarkAlphaRecoveryPending();
        _dynamicRouteRetryCooldownTicks = _alphaNavigation
            ? DynamicRouteRetryCooldownTicks
            : 0;
        _steering.Reset();
    }

    private void MarkAlphaRecoveryPending()
    {
        if (!_alphaNavigation)
        {
            return;
        }

        _alphaRecoveryPending = true;
        _alphaRecoveryNextAttemptThinkTick = 0;
    }

    private static string FormatPath(NavPath path, NavGraph graph)
    {
        var parts = new string[Math.Max(0, path.Count - 1)];
        for (var index = 1; index < path.Count; index += 1)
        {
            var fromNode = path.GetWaypoint(index - 1);
            var toNode = path.GetWaypoint(index);
            var edgeKind = path.TryGetIncomingEdge(index, out var edge)
                ? edge.Kind.ToString()
                : "Unknown";
            parts[index - 1] = $"{fromNode}>{toNode}/{edgeKind}";
        }

        return string.Join(',', parts);
    }

    private static void TraceNavigationEvent(PlayerEntity self, PlayerTeam team, string message)
    {
        if (!NavigationEventTracingEnabled || string.IsNullOrWhiteSpace(NavigationEventTracePath))
        {
            return;
        }

        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.Now:O} bot={self.Id} team={team} class={self.ClassId} {message}{Environment.NewLine}");
        lock (NavigationDiagnosticSync)
        {
            File.AppendAllText(NavigationEventTracePath, line);
        }
    }

    private void TraceRuntimeRecipeExecution(PlayerEntity self, PlayerTeam team, SteeringOutput steeringOutput)
    {
        var trace = steeringOutput.RecipeTrace;
        var traceAllRuntimeRecipeEdges =
            Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_TRACE_RUNTIME_EXECUTION") is "1" or "true" or "TRUE";
        if (!NavigationEventTracingEnabled
            || !trace.HasRecipe
            || (!traceAllRuntimeRecipeEdges && trace.ToNode is not (305 or 306 or 307 or 538))
            || (trace.EdgeTicks % 5 != 0 && !trace.FinalJump))
        {
            return;
        }

        TraceNavigationEvent(
            self,
            team,
            $"event=contact_execution edge={trace.FromNode}->{trace.ToNode} edgeTicks={trace.EdgeTicks} " +
            $"pos=({trace.CurrentX:0.0},{trace.CurrentY:0.0}) grounded={(trace.CurrentGrounded ? 1 : 0)} " +
            $"speed={trace.CurrentHorizontalSpeed:0.0} " +
            $"launchX={trace.RecipeLaunchMinX:0.0}..{trace.RecipeLaunchMaxX:0.0} " +
            $"launchSpeed={trace.RecipeLaunchMinHorizontalSpeed:0.0}..{trace.RecipeLaunchMaxHorizontalSpeed:0.0} " +
            $"inX={(trace.InLaunchXWindow ? 1 : 0)} inY={(trace.InLaunchYWindow ? 1 : 0)} " +
            $"inSpeed={(trace.InLaunchSpeedWindow ? 1 : 0)} ready={(trace.RecipeReady ? 1 : 0)} " +
            $"suppress={(trace.SuppressJumpUntilLaunch ? 1 : 0)} " +
            $"move={trace.FinalMoveDirection:0} jump={(trace.FinalJump ? 1 : 0)} prevUp={(_previousInput.Up ? 1 : 0)}");
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
                _blockedEdgesVersion += 1;
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
        var stageStartTimestamp = NavigationStageTracingEnabled
            ? Stopwatch.GetTimestamp()
            : 0L;
        var resolved = TryResolveDirectSeekCore(
            world,
            self,
            team,
            combatTarget,
            routeRecoveryRequested,
            steeringOutput,
            out directSteering,
            out directTrace);
        TraceSlowNavigationStage(self, "TryResolveDirectSeek", stageStartTimestamp);
        return resolved;
    }

    private bool TryResolveDirectSeekCore(
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

        if (!_alphaNavigation
            && TryResolveArenaCaptureZoneDirectDrive(world, self, routeRecoveryRequested, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (!ForceObjectiveNavigationForDiagnostics
            && TryResolveCaptureTheFlagEngineerDefenseSeek(world, self, team, combatTarget, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (!_alphaNavigation
            && TryResolveCaptureTheFlagDirectSeek(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (!DisableCombatForDiagnostics
            && TryResolveControlPointEnemyClearSeek(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (!_alphaNavigation
            && TryResolveControlPointDirectSeek(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (!_alphaNavigation
            && ShouldDirectSeekEnemiesAfterKothCapture(world, team)
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
            if (!_alphaNavigation
                && combatTarget is { Kind: BotBrainCombatTargetKind.Player, Player: { } directCombatTarget }
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

            return TryResolveAlphaAwarePrimitiveCombatDrive(
                world,
                self,
                combatTarget,
                steeringOutput,
                out directSteering,
                out directTrace);
        }

        // A failed/repathing alpha graph route must not turn into a route to
        // whichever enemy happened to be selected that tick.  That creates a
        // second route owner during recovery and makes the bot alternate
        // between the objective and a moving combat target.  Immediate local
        // combat remains available; the next graph update owns traversal.
        if (_alphaNavigation)
        {
            return TryResolveAlphaAwarePrimitiveCombatDrive(
                world,
                self,
                combatTarget,
                steeringOutput,
                out directSteering,
                out directTrace);
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

        if (!_alphaNavigation
            && TryResolveAtaliaCentralRecoveryDrive(
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

        if (_alphaNavigation)
        {
            directSteering = steeringOutput;
            directTrace = string.Empty;
            return false;
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

    private bool TryResolveAlphaAwarePrimitiveCombatDrive(
        SimulationWorld world,
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        var stageStartTimestamp = NavigationStageTracingEnabled
            ? Stopwatch.GetTimestamp()
            : 0L;
        var resolved = TryResolveAlphaAwarePrimitiveCombatDriveCore(
            world,
            self,
            combatTarget,
            steeringOutput,
            out directSteering,
            out directTrace);
        TraceSlowNavigationStage(self, "TryResolveAlphaAwarePrimitiveCombatDrive", stageStartTimestamp);
        return resolved;
    }

    private bool TryResolveAlphaAwarePrimitiveCombatDriveCore(
        SimulationWorld world,
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (_alphaNavigation
            && _currentPath is { IsComplete: false }
            && combatTarget is { Kind: BotBrainCombatTargetKind.Player, Player: { } target }
            && DistanceBetween(self.X, self.Y, target.X, target.Y) > AlphaObjectiveCombatOverlayDistance)
        {
            // Keep the graph moving toward the live objective while a merely
            // visible enemy is still outside the immediate combat pocket.
            // Point-clear and dynamic CTF resolvers run earlier and remain
            // allowed to take ownership when the objective requires it.
            return false;
        }

        return TryResolvePrimitiveCombatDrive(
            world,
            self,
            combatTarget,
            steeringOutput,
            out directSteering,
            out directTrace);
    }

    private bool TryResolveNoGraphObjectiveSeek(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        BotBrainCombatTarget? combatTarget,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        if (_alphaNavigation)
        {
            // Alpha still owns long-range traversal. This is only the
            // grounded emergency lane after the graph has no executable path
            // for this tick (for example immediately after a missed landing
            // or a dynamic body-block). It keeps the bot moving out of the
            // invalid attachment region while UpdatePath retries the graph;
            // it is deliberately not a replacement for graph routing.
            if (_navGraph is not null)
            {
                var alphaObjectiveTarget = new DirectDriveTarget(
                    DirectDriveTargetKind.Objective,
                    _currentGoalPosition.X,
                    _currentGoalPosition.Y,
                    "alphaPathlessObjective");
                var forceCtfIntelApproach = IsAlphaCtfTerminalIntelApproach(world, self, alphaObjectiveTarget);
                var objectiveDistance = DistanceBetween(self.X, self.Y, alphaObjectiveTarget.X, alphaObjectiveTarget.Y);

                if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag
                    && self.IsCarryingIntel
                    && TryResolveAlphaCarrierTerminalReturn(
                        world,
                        self,
                        team,
                        steeringOutput,
                        out directSteering,
                        out directTrace))
                {
                    return true;
                }

                if (!forceCtfIntelApproach
                    && self.IsGrounded
                    && TryResolveLocalMotionRecovery(
                            world,
                            self,
                            alphaObjectiveTarget,
                            steeringOutput,
                            out directSteering,
                            out directTrace)
                        && !IsSuppressedObjectiveProbeTrace(directTrace)
                        && (!IsNeutralNavigationOutput(directSteering)
                            || objectiveDistance <= DroppedIntelNearHoldDistance))
                {
                    directTrace = $"alphaPathlessRecovery {directTrace}";
                    return true;
                }

                if (forceCtfIntelApproach
                    && TryResolveAlphaPathlessEscape(
                        world,
                        self,
                        alphaObjectiveTarget,
                        steeringOutput,
                        out directSteering,
                        out directTrace))
                {
                    directTrace = $"alphaCtfIntelApproach {directTrace}";
                    return true;
                }

                // A stale/neutral local plan must not become the movement
                // owner while the graph is reattaching. Give the body one
                // cheap deterministic objective drive instead. This also
                // covers an airborne failed-edge handoff; waiting for the
                // next grounded think with an empty SteeringOutput is what
                // made bots visibly freeze after a missed landing.
                if (PrimitiveDirectDrive.TryResolveRecovery(
                        world,
                        self,
                        alphaObjectiveTarget,
                        steeringOutput,
                        out directSteering,
                        out directTrace)
                    && !IsNeutralNavigationOutput(directSteering))
                {
                    directTrace = $"alphaPathlessPrimitive {directTrace}";
                    return true;
                }

                if (combatTarget is null
                    && TryResolveAlphaPathlessEscape(
                        world,
                        self,
                        alphaObjectiveTarget,
                        steeringOutput,
                        out directSteering,
                        out directTrace))
                {
                    return true;
                }
            }

            directSteering = steeringOutput;
            directTrace = string.Empty;
            return false;
        }

        if (TryResolveArenaCaptureZoneDirectDrive(world, self, routeRecoveryRequested: true, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (!ForceObjectiveNavigationForDiagnostics
            && TryResolveCaptureTheFlagEngineerDefenseSeek(world, self, team, combatTarget, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (TryResolveCaptureTheFlagDirectSeek(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (!DisableCombatForDiagnostics
            && TryResolveControlPointEnemyClearSeek(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (TryResolveControlPointDirectSeek(world, self, team, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        var objectiveTarget = new DirectDriveTarget(
            DirectDriveTargetKind.Objective,
            _currentGoalPosition.X,
            _currentGoalPosition.Y,
            "noGraphObjective");
        if (TryResolveLocalMotionRecovery(
            world,
            self,
            objectiveTarget,
            steeringOutput,
            out directSteering,
            out directTrace))
        {
            return true;
        }

        if (!IsLocalMotionSuppressionTrace(directTrace)
            && PrimitiveDirectDrive.TryResolveRecovery(
                world,
                self,
                objectiveTarget,
                steeringOutput,
                out directSteering,
                out directTrace))
        {
            directTrace = $"primitiveFallback {directTrace}";
            return true;
        }

        return false;
    }

    private static bool TryResolveAlphaCarrierTerminalReturn(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        var ownBase = world.Level.GetIntelBase(team);
        if (!ownBase.HasValue)
        {
            return false;
        }

        var dx = ownBase.Value.X - self.X;
        var dy = ownBase.Value.Y - self.Y;
        var distance = DistanceBetween(self.X, self.Y, ownBase.Value.X, ownBase.Value.Y);
        if (distance > 220f || MathF.Abs(dy) > 128f)
        {
            return false;
        }

        var moveDirection = Math.Sign(dx);
        directSteering.MoveDirection = moveDirection;
        // A grounded player can always initiate the normal jump, even when
        // the class has no mid-air jumps. RemainingAirJumps only gates a
        // second jump after takeoff.
        directSteering.Jump = self.IsGrounded && dy < -4f;
        directSteering.DropDown = false;
        directSteering.RequestRepath = false;
        directTrace = string.Create(
            CultureInfo.InvariantCulture,
            $"alphaCarrierTerminal team:{team} dx:{dx:0.0} dy:{dy:0.0} " +
            $"dist:{distance:0.0} move:{moveDirection:0} jump:{(directSteering.Jump ? 1 : 0)}");
        return true;
    }

    private bool TryResolveAlphaPathlessEscape(
        SimulationWorld world,
        PlayerEntity self,
        DirectDriveTarget target,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (!self.IsAlive)
        {
            return false;
        }

        var dx = target.X - self.X;
        var dy = target.Y - self.Y;
        var distance = DistanceBetween(self.X, self.Y, target.X, target.Y);
        if (distance <= DroppedIntelNearHoldDistance
            && MathF.Abs(dy) <= DroppedIntelNearHorizontalDeadZone
            && !IsAlphaCtfTerminalIntelApproach(world, self, target))
        {
            // The bot is already in the terminal horizontal objective band;
            // a neutral hold here is intentional (capture/scoring owns it).
            return false;
        }

        var terminalIntelApproach = IsAlphaCtfTerminalIntelApproach(world, self, target);
        if (terminalIntelApproach
            && self.IntersectsMarker(target.X, target.Y, 24f, 24f))
        {
            return false;
        }
        var desiredObjectiveDirection = MathF.Sign(dx);
        var targetCrossedCommittedDirection = terminalIntelApproach
            && desiredObjectiveDirection != 0f
            && desiredObjectiveDirection != _alphaPathlessEscapeDirection
            // Once the body has crossed the marker, reverse immediately. The
            // old 8px dead zone could preserve the stale direction while the
            // bot was already on the far side of the intel, making it walk
            // away from the pickup/score volume until the commit expired.
            && MathF.Abs(dx) > 1f;
        if (_alphaPathlessEscapeUntilThinkTick <= _thinkTicks
            || _alphaPathlessEscapeDirection == 0
            || targetCrossedCommittedDirection)
        {
            var objectiveDirection = desiredObjectiveDirection;
            if (objectiveDirection == 0f)
            {
                objectiveDirection = terminalIntelApproach
                    ? 0
                    : MathF.Sign(self.FacingDirectionX);
            }

            if (objectiveDirection == 0f && !terminalIntelApproach)
            {
                objectiveDirection = self.Id % 2 == 0 ? 1 : -1;
            }

            // If the objective-facing side is blocked, move away from the
            // obstruction long enough to leave the stale attachment region.
            // If both sides are blocked, retain the objective direction and
            // use a jump pulse as the final cheap escape attempt.
            var objectiveDirectionBlocked = PrimitiveDirectDrive.WouldMoveIntoObstacle(
                world,
                self,
                objectiveDirection);
            _alphaPathlessEscapeDirection = terminalIntelApproach
                ? objectiveDirection
                : objectiveDirectionBlocked
                    ? -objectiveDirection
                    : objectiveDirection;
            _alphaPathlessEscapeUntilThinkTick = _thinkTicks + AlphaPathlessEscapeCommitTicks;
        }

        var escapeDirection = _alphaPathlessEscapeDirection;
        var escapeBlocked = escapeDirection != 0
            && PrimitiveDirectDrive.WouldMoveIntoObstacle(world, self, escapeDirection);
        // RemainingAirJumps is zero for every non-Scout class while grounded;
        // it must not suppress the ordinary ground jump used to recover onto
        // an upper support.
        var jump = self.IsGrounded
            && (escapeBlocked
                || dy < (terminalIntelApproach
                    ? -DroppedIntelNearHorizontalDeadZone
                    : -AlphaPathlessEscapeTargetDeadZone));
        directSteering = steeringOutput;
        directSteering.MoveDirection = escapeDirection;
        directSteering.Jump = jump;
        directSteering.DropDown = false;
        directSteering.RequestRepath = false;
        MarkAlphaRecoveryPending();
        directTrace =
            $"alphaPathlessEscape target:{target.Label} dx:{dx:0.0} dy:{dy:0.0} " +
            $"dist:{distance:0.0} move:{escapeDirection:0} blocked:{(escapeBlocked ? 1 : 0)} " +
            $"jump:{(jump ? 1 : 0)} remaining:{Math.Max(0, _alphaPathlessEscapeUntilThinkTick - _thinkTicks)}";
        return true;
    }

    private bool IsAlphaCtfEnemyIntelGoal(SimulationWorld world, PlayerTeam team)
    {
        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag)
        {
            return false;
        }

        var enemyIntel = GetEnemyIntelState(world, team);
        var targetX = enemyIntel.IsAtBase ? enemyIntel.HomeX : enemyIntel.X;
        var targetY = enemyIntel.IsAtBase ? enemyIntel.HomeY : enemyIntel.Y;
        return DistanceBetween(_currentGoalPosition.X, _currentGoalPosition.Y, targetX, targetY)
            <= DroppedIntelNearHoldDistance;
    }

    private static bool IsAlphaCtfTerminalIntelApproach(
        SimulationWorld world,
        PlayerEntity self,
        DirectDriveTarget target)
    {
        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag
            || self.IsCarryingIntel
            || target.Kind != DirectDriveTargetKind.Objective)
        {
            return false;
        }

        var enemyIntel = GetEnemyIntelState(world, self.Team);
        var ownIntel = GetOwnIntelState(world, self.Team);
        var enemyTarget = (enemyIntel.IsAtBase || enemyIntel.IsDropped)
            && DistanceBetween(target.X, target.Y, enemyIntel.X, enemyIntel.Y) <= DroppedIntelNearHoldDistance;
        var ownDroppedTarget = ownIntel.IsDropped
            && DistanceBetween(target.X, target.Y, ownIntel.X, ownIntel.Y) <= DroppedIntelNearHoldDistance;
        return enemyTarget || ownDroppedTarget;
    }

    private bool TryResolveAlphaObjectiveArrivalCorrection(
        SimulationWorld world,
        PlayerEntity self,
        SteeringOutput steeringOutput,
        out SteeringOutput correctionSteering,
        out string trace)
    {
        correctionSteering = steeringOutput;
        trace = string.Empty;
        if (world.MatchRules.Mode is not (GameModeKind.Arena
            or GameModeKind.ControlPoint
            or GameModeKind.KingOfTheHill
            or GameModeKind.DoubleKingOfTheHill)
            || world.ControlPoints.Count == 0
            || IsAtAlphaObjectiveArrival(world, self))
        {
            return false;
        }

        var point = world.ControlPoints
            .OrderBy(candidate => DistanceBetween(
                candidate.HealingAuraCenterX,
                candidate.HealingAuraCenterY,
                _currentGoalPosition.X,
                _currentGoalPosition.Y))
            .First();
        if (point.IsLocked
            || DistanceBetween(
                point.HealingAuraCenterX,
                point.HealingAuraCenterY,
                _currentGoalPosition.X,
                _currentGoalPosition.Y) > 160f)
        {
            return false;
        }

        var dx = _currentGoalPosition.X - self.X;
        var dy = _currentGoalPosition.Y - self.Y;
        if (MathF.Abs(dx) > 128f || MathF.Abs(dy) > 96f)
        {
            return false;
        }

        // The graph has already attached the body to the objective node, but
        // the live capture volume can end a few pixels short of that node's
        // completion bounds (especially on the upper Corinth platform). Use
        // one deterministic horizontal correction to enter the real volume;
        // this is a terminal approach correction, not a second combat or
        // long-range navigation owner.
        var moveDirection = MathF.Sign(dx);
        if (moveDirection == 0f)
        {
            return false;
        }

        correctionSteering.MoveDirection = moveDirection;
        correctionSteering.Jump = false;
        correctionSteering.DropDown = false;
        trace = string.Create(
            CultureInfo.InvariantCulture,
            $"alphaObjectiveCorrection point:{point.Index} dx:{dx:0.0} dy:{dy:0.0} move:{moveDirection:0}");
        return true;
    }

    private bool TryResolveAlphaAirborneRecoverySteering(
        PlayerEntity self,
        SteeringOutput steeringOutput,
        out SteeringOutput recoverySteering,
        out string trace)
    {
        recoverySteering = steeringOutput;
        trace = string.Empty;
        if (_currentPath is { IsComplete: false })
        {
            return false;
        }

        var dx = _currentGoalPosition.X - self.X;
        if (MathF.Abs(dx) <= 8f)
        {
            return false;
        }

        var moveDirection = MathF.Sign(dx);
        recoverySteering.MoveDirection = moveDirection;
        recoverySteering.Jump = false;
        recoverySteering.DropDown = false;
        trace = string.Create(
            CultureInfo.InvariantCulture,
            $"alphaAirborneRecovery dx:{dx:0.0} move:{moveDirection:0} " +
            $"pos:({self.X:0.0},{self.Y:0.0}) goal:({_currentGoalPosition.X:0.0},{_currentGoalPosition.Y:0.0})");
        return true;
    }

    private static bool IsLocalMotionSuppressionTrace(string trace) =>
        trace.StartsWith("localMotion=suppressed", StringComparison.Ordinal)
        || trace.StartsWith("localMotion=failed", StringComparison.Ordinal);

    private static bool IsSuppressedObjectiveProbeTrace(string trace) =>
        trace.StartsWith("localMotion=suppressedProbe", StringComparison.Ordinal);

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

    private bool TryResolveDynamicObjectiveLocalMotionRecovery(
        SimulationWorld world,
        PlayerEntity self,
        DirectDriveTarget target,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        if (!TryResolveLocalMotionRecovery(
                world,
                self,
                target,
                steeringOutput,
                out directSteering,
                out directTrace))
        {
            return false;
        }

        // An alpha dynamic target is a movement owner only while it emits a
        // real input. A neutral local plan at world-scale distance otherwise
        // masks the graph route and leaves the bot standing still until the
        // moving target changes enough to force another search. Near-target
        // neutral output is only a terminal hold when the bot is also in the
        // target's horizontal band; a vertically offset neutral plan still
        // needs graph/local recovery to reach the gameplay contact.
        var dx = target.X - self.X;
        var dy = target.Y - self.Y;
        var distance = DistanceBetween(self.X, self.Y, target.X, target.Y);
        var nearTerminal = distance <= DroppedIntelNearHoldDistance
            && MathF.Abs(dy) <= DroppedIntelNearHorizontalDeadZone;
        if (nearTerminal)
        {
            ResetAlphaDynamicRecoveryProgress();
        }
        else if (IsAlphaDynamicRecoveryStagnant(self, target))
        {
            _localMotionController.AbortActivePlan();
            MarkAlphaRecoveryPending();
            directTrace = $"{directTrace} reject:stagnant_dynamic ticks:{_alphaDynamicRecoveryStagnantTicks} " +
                $"distance:{distance:0.0} dx:{dx:0.0} dy:{dy:0.0}";
            return false;
        }
        if (_alphaNavigation
            && ((target.Kind == DirectDriveTargetKind.Intel
                    && IsSuppressedObjectiveProbeTrace(directTrace))
                || (!nearTerminal
                    && (IsNeutralNavigationOutput(directSteering)
                        || IsSuppressedObjectiveProbeTrace(directTrace)))))
        {
            directTrace = $"{directTrace} reject:nonmoving_dynamic distance:{distance:0.0} dx:{dx:0.0} dy:{dy:0.0}";
            return false;
        }

        return true;
    }

    private bool IsAlphaDynamicRecoveryStagnant(PlayerEntity self, DirectDriveTarget target)
    {
        if (!string.Equals(_alphaDynamicRecoveryLabel, target.Label, StringComparison.Ordinal)
            || DistanceBetween(
                _alphaDynamicRecoveryLastX,
                _alphaDynamicRecoveryLastY,
                target.X,
                target.Y) > DroppedIntelNearHoldDistance)
        {
            _alphaDynamicRecoveryLabel = target.Label;
            _alphaDynamicRecoveryLastX = self.X;
            _alphaDynamicRecoveryLastY = self.Y;
            _alphaDynamicRecoveryStagnantTicks = 0;
            return false;
        }

        var moved = DistanceBetween(
            _alphaDynamicRecoveryLastX,
            _alphaDynamicRecoveryLastY,
            self.X,
            self.Y);
        _alphaDynamicRecoveryLastX = self.X;
        _alphaDynamicRecoveryLastY = self.Y;
        if (moved < DynamicObjectiveRecoveryProgressDistance)
        {
            _alphaDynamicRecoveryStagnantTicks += 1;
        }
        else
        {
            _alphaDynamicRecoveryStagnantTicks = 0;
        }

        return _alphaDynamicRecoveryStagnantTicks >= DynamicObjectiveRecoveryStagnationTicks;
    }

    private void ResetAlphaDynamicRecoveryProgress()
    {
        _alphaDynamicRecoveryLabel = null;
        _alphaDynamicRecoveryLastX = 0f;
        _alphaDynamicRecoveryLastY = 0f;
        _alphaDynamicRecoveryStagnantTicks = 0;
    }

    private bool BreakStalledDynamicRoute(PlayerEntity self, SimulationWorld world)
    {
        if (!_alphaNavigation
            || world.MatchRules.Mode != GameModeKind.CaptureTheFlag
            || !_hasDynamicRouteTarget
            || _currentPath is null
            || _currentPath.IsComplete
            || DistanceBetween(
                self.X,
                self.Y,
                _dynamicRouteTargetPosition.X,
                _dynamicRouteTargetPosition.Y) <= DroppedIntelNearHoldDistance)
        {
            ResetDynamicRouteProgress();
            return false;
        }

        if (!ReferenceEquals(_dynamicRouteProgressPath, _currentPath))
        {
            _dynamicRouteProgressPath = _currentPath;
            _dynamicRouteProgressLastX = self.X;
            _dynamicRouteProgressLastY = self.Y;
            _dynamicRouteProgressStagnantTicks = 0;
            _dynamicRouteProgressLowSpeedFlips = 0;
            _dynamicRouteProgressLastMoveDirection = 0;
            return false;
        }

        _dynamicRouteProgressStagnantTicks += 1;
        var moveDirection = Math.Sign(LastSteeringOutput.MoveDirection);
        if (moveDirection != 0
            && _dynamicRouteProgressLastMoveDirection != 0
            && moveDirection != _dynamicRouteProgressLastMoveDirection)
        {
            _dynamicRouteProgressLowSpeedFlips += 1;
        }

        if (moveDirection != 0)
        {
            _dynamicRouteProgressLastMoveDirection = moveDirection;
        }

        if (_dynamicRouteProgressStagnantTicks < DynamicRouteProgressStagnationTicks)
        {
            return false;
        }

        var moved = DistanceBetween(
            _dynamicRouteProgressLastX,
            _dynamicRouteProgressLastY,
            self.X,
            self.Y);
        var oscillating = self.IsGrounded
            && _dynamicRouteProgressLowSpeedFlips >= 4
            && moved < 32f;
        if (!oscillating)
        {
            _dynamicRouteProgressLastX = self.X;
            _dynamicRouteProgressLastY = self.Y;
            _dynamicRouteProgressStagnantTicks = 0;
            _dynamicRouteProgressLowSpeedFlips = 0;
            _dynamicRouteProgressLastMoveDirection = 0;
            return false;
        }

        var stalledTicks = _dynamicRouteProgressStagnantTicks;
        var stalledFlips = _dynamicRouteProgressLowSpeedFlips;
        _currentPath = null;
        _goalNodeIndex = -1;
        _hasDynamicRouteTarget = false;
        _repathCooldownTicks = 0;
        _dynamicRouteRetryCooldownTicks = 0;
        _steering.Reset();
        MarkAlphaRecoveryPending();
        ResetDynamicRouteProgress();
        TraceNavigationEvent(
            self,
            self.Team,
            $"event=dynamic_route_stall_recovery ticks={stalledTicks} flips={stalledFlips} " +
            $"pos=({self.X:0.0},{self.Y:0.0}) " +
            $"target=({_dynamicRouteTargetPosition.X:0.0},{_dynamicRouteTargetPosition.Y:0.0})");
        LastSemanticRecoveryTrace = $"dynamicRouteRecovery=stalled ticks:{stalledTicks} flips:{stalledFlips}";
        return true;
    }

    private bool BreakStalledCombatRoute(PlayerEntity self)
    {
        var directTrace = LastDirectDriveTrace;
        var directOwner = directTrace.Contains("controlPointClearEnemy", StringComparison.Ordinal)
            ? "controlPointClearEnemy"
            : directTrace.Contains("spyRetreat", StringComparison.Ordinal)
                ? "spyRetreat"
                : directTrace.Contains("directRoute=", StringComparison.Ordinal)
                    ? "directRoute"
                    : directTrace.Contains("directDrive=", StringComparison.Ordinal)
                        ? "directDrive"
                        : null;
        if (!_alphaNavigation
            || LastCombatTarget is null
            || directOwner is null
            || directTrace.Contains("alphaCapture", StringComparison.Ordinal)
            || directTrace.Contains("medicSupport:", StringComparison.Ordinal)
            || MathF.Abs(LastSteeringOutput.MoveDirection) <= 0.01f)
        {
            ResetCombatRouteProgress();
            return false;
        }

        if (!string.Equals(_combatRouteProgressOwner, directOwner, StringComparison.Ordinal))
        {
            _combatRouteProgressOwner = directOwner;
            _combatRouteProgressLastX = self.X;
            _combatRouteProgressLastY = self.Y;
            _combatRouteProgressStagnantTicks = 0;
            return false;
        }

        var moved = DistanceBetween(
            _combatRouteProgressLastX,
            _combatRouteProgressLastY,
            self.X,
            self.Y);
        _combatRouteProgressLastX = self.X;
        _combatRouteProgressLastY = self.Y;
        if (moved >= CombatRouteProgressDistance)
        {
            _combatRouteProgressStagnantTicks = 0;
            return false;
        }

        _combatRouteProgressStagnantTicks += 1;
        if (_combatRouteProgressStagnantTicks < CombatRouteProgressStagnationTicks)
        {
            return false;
        }

        var stalledTicks = _combatRouteProgressStagnantTicks;
        _currentPath = null;
        _goalNodeIndex = -1;
        _hasDynamicRouteTarget = false;
        _repathCooldownTicks = 0;
        _steering.Reset();
        MarkAlphaRecoveryPending();
        LastSemanticRecoveryTrace = $"combatRouteRecovery=stalled ticks:{stalledTicks}";
        ResetCombatRouteProgress();
        return true;
    }

    private void ResetCombatRouteProgress()
    {
        _combatRouteProgressOwner = null;
        _combatRouteProgressLastX = 0f;
        _combatRouteProgressLastY = 0f;
        _combatRouteProgressStagnantTicks = 0;
    }

    private void ResetDynamicRouteProgress()
    {
        _dynamicRouteProgressLastX = 0f;
        _dynamicRouteProgressLastY = 0f;
        _dynamicRouteProgressStagnantTicks = 0;
        _dynamicRouteProgressLowSpeedFlips = 0;
        _dynamicRouteProgressLastMoveDirection = 0;
        _dynamicRouteProgressPath = null;
    }

    private bool TryResolvePrimitiveCombatDrive(
        SimulationWorld world,
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (combatTarget is not { Kind: BotBrainCombatTargetKind.Player, Player: { } target }
            || !PrimitiveDirectDrive.TryResolve(world, self, combatTarget, steeringOutput, out var primitiveSteering, out var primitiveTrace))
        {
            return false;
        }

        if (MathF.Abs(primitiveSteering.MoveDirection) <= 0.01f
            || !PrimitiveDirectDrive.WouldMoveIntoObstacle(world, self, MathF.Sign(primitiveSteering.MoveDirection)))
        {
            directSteering = primitiveSteering;
            directTrace = primitiveTrace;
            return true;
        }

        var localTarget = new DirectDriveTarget(
            DirectDriveTargetKind.Enemy,
            target.X,
            target.Y,
            $"combatObstacleEnemy player:{target.Id}");
        if (TryResolveLocalMotionRecovery(world, self, localTarget, steeringOutput, out var localSteering, out var localTrace))
        {
            directSteering = localSteering;
            directTrace = $"combatObstacle {localTrace} primitive:{primitiveTrace}";
            return true;
        }

        if (IsLocalMotionSuppressionTrace(localTrace))
        {
            directSteering = steeringOutput;
            directTrace = $"combatObstacle hold {localTrace} primitive:{primitiveTrace}";
            return true;
        }

        directSteering = primitiveSteering;
        directTrace = primitiveTrace;
        return true;
    }

    private bool TryResolveMedicSupportDrive(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        PlayerEntity? healTarget,
        MedicHealTargetSelectionKind selectionKind,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        var stageStartTimestamp = NavigationStageTracingEnabled
            ? Stopwatch.GetTimestamp()
            : 0L;
        var resolved = TryResolveMedicSupportDriveCore(
            world,
            self,
            team,
            healTarget,
            selectionKind,
            steeringOutput,
            out directSteering,
            out directTrace);
        TraceSlowNavigationStage(self, "TryResolveMedicSupportDrive", stageStartTimestamp);
        return resolved;
    }

    private bool TryResolveMedicSupportDriveCore(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        PlayerEntity? healTarget,
        MedicHealTargetSelectionKind selectionKind,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (self.ClassId != PlayerClass.Medic
            || self.IsCarryingIntel
            || healTarget is null
            || !healTarget.IsAlive
            || healTarget.Team != team
            || healTarget.Id == self.Id)
        {
            return false;
        }

        var dx = healTarget.X - self.X;
        var dy = healTarget.Y - self.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        if (distance > MedicSupportDirectSeekDistance)
        {
            return false;
        }

        var healLinkStartTimestamp = Stopwatch.GetTimestamp();
        var hasHealLink = distance <= MedicSupportHealRange
            && CombatDecisionResolver.HasLineOfSight(world, self.X, self.Y, healTarget.X, healTarget.Y, self.Team, self.IsCarryingIntel);
        var healLinkMilliseconds = (Stopwatch.GetTimestamp() - healLinkStartTimestamp) * 1000d / Stopwatch.Frequency;
        var healLinkTiming = healLinkMilliseconds >= 8d
            ? $" losMs:{healLinkMilliseconds:0.0}"
            : string.Empty;
        var label = $"medicSupport:{selectionKind} player:{healTarget.Id}";
        if (distance < MedicSupportHoldMinDistance)
        {
            directSteering.MoveDirection = ResolveMedicSupportAwayDirection(self, dx);
            directSteering.Jump = false;
            directSteering.DropDown = false;
            directTrace = $"{label} space dx:{dx:0.0} dy:{dy:0.0} dist:{distance:0.0} move:{directSteering.MoveDirection:0}{healLinkTiming}";
            return true;
        }

        if (hasHealLink && distance <= MedicSupportHoldMaxDistance)
        {
            directSteering.MoveDirection = 0;
            directSteering.Jump = false;
            directSteering.DropDown = false;
            directTrace = $"{label} hold dx:{dx:0.0} dy:{dy:0.0} dist:{distance:0.0}{healLinkTiming}";
            return true;
        }

        if (TryRouteToDirectSeekTarget(
                world,
                self,
                team,
                healTarget.X,
                healTarget.Y,
                label,
                steeringOutput,
                out directSteering,
                out directTrace,
                requireVerticalSeparation: false,
                activePathReuseDistance: MovingCarrierRouteReuseDistance))
        {
            return true;
        }

        if (PrimitiveDirectDrive.TryResolveSupport(
                world,
                self,
                new DirectDriveTarget(DirectDriveTargetKind.Escort, healTarget.X, healTarget.Y, label),
                steeringOutput,
                MedicSupportDirectSeekDistance,
                out directSteering,
                out directTrace))
        {
            return true;
        }

        if (!hasHealLink)
        {
            return false;
        }

        directSteering.MoveDirection = 0;
        directSteering.Jump = false;
        directSteering.DropDown = false;
        directTrace = $"{label} holdFallback dx:{dx:0.0} dy:{dy:0.0} dist:{distance:0.0}{healLinkTiming}";
        return true;
    }

    private static PlayerEntity? ResolveMedicAimHealTarget(
        SimulationWorld world,
        PlayerEntity self,
        PlayerEntity? desiredHealTarget)
    {
        if (self.ClassId != PlayerClass.Medic
            || !self.MedicHealTargetId.HasValue)
        {
            return desiredHealTarget;
        }

        var currentHealTarget = FindBotBrainPlayerById(world, self.MedicHealTargetId.Value);
        if (currentHealTarget is null
            || !currentHealTarget.IsAlive
            || currentHealTarget.Team != self.Team)
        {
            return desiredHealTarget;
        }

        if (desiredHealTarget is null)
        {
            return currentHealTarget.ClassId == PlayerClass.Medic
                ? null
                : currentHealTarget;
        }

        if (desiredHealTarget.Id == currentHealTarget.Id)
        {
            return desiredHealTarget;
        }

        return currentHealTarget.ClassId == PlayerClass.Medic && desiredHealTarget.ClassId != PlayerClass.Medic
            ? desiredHealTarget
            : currentHealTarget;
    }

    private static int ResolveMedicSupportAwayDirection(PlayerEntity self, float targetDeltaX)
    {
        if (MathF.Abs(targetDeltaX) > 1f)
        {
            return targetDeltaX > 0f ? -1 : 1;
        }

        return self.Id % 2 == 0 ? 1 : -1;
    }

    private static PlayerEntity? FindBotBrainPlayerById(SimulationWorld world, int playerId)
    {
        foreach (var player in CombatDecisionResolver.EnumeratePlayers(world))
        {
            if (player.Id == playerId)
            {
                return player;
            }
        }

        return null;
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
        if (_alphaNavigation
            || world.MatchRules.Mode != GameModeKind.Arena
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
        if (TryResolveBlockedLocalMotion(
                world,
                self,
                new DirectDriveTarget(
                    DirectDriveTargetKind.Objective,
                    target.X,
                    target.Y,
                    $"arenaCaptureLadder stage:{_platformLadderStage}"),
                directSteering.MoveDirection,
                steeringOutput,
                out var recoverySteering,
                out var recoveryTrace))
        {
            directSteering = recoverySteering;
            directTrace = $"{directTrace} blockedRecovery:{recoveryTrace}";
        }

        return true;
    }

    private bool TryResolveCaptureTheFlagEngineerDefenseSeek(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        BotBrainCombatTarget? combatTarget,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        var stageStartTimestamp = NavigationStageTracingEnabled
            ? Stopwatch.GetTimestamp()
            : 0L;
        var resolved = TryResolveCaptureTheFlagEngineerDefenseSeekCore(
            world,
            self,
            team,
            combatTarget,
            steeringOutput,
            out directSteering,
            out directTrace);
        TraceSlowNavigationStage(self, "TryResolveCaptureTheFlagEngineerDefenseSeek", stageStartTimestamp);
        return resolved;
    }

    private bool TryResolveCaptureTheFlagEngineerDefenseSeekCore(
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
        if (ForceObjectiveNavigationForDiagnostics
            || !IsCaptureTheFlagEngineerDefender(world, self))
        {
            return false;
        }

        var ownIntel = GetOwnIntelState(world, team);
        if (ownIntel.IsCarried)
        {
            return false;
        }

        if (combatTarget is { Kind: BotBrainCombatTargetKind.Player, Player: { } target }
            && DistanceBetween(self.X, self.Y, target.X, target.Y) <= EngineerCtfDefenseCombatChaseDistance)
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
            if (defendedSentry is not null && !ownIntel.IsDropped)
            {
                directSteering = ResolveEngineerCaptureTheFlagBasePatrol(self, anchor, steeringOutput, out var patrolTrace);
                ApplyEngineerCaptureTheFlagDefenseAim(world, team, anchor, ref directSteering);
                directTrace = $"engineerIntelDefense=patrol team:{team} dx:{dx:0.0} dy:{dy:0.0} dist:{distance:0.0} sentry:1 {patrolTrace}";
                return true;
            }

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

    private SteeringOutput ResolveEngineerCaptureTheFlagBasePatrol(
        PlayerEntity self,
        (float X, float Y) anchor,
        SteeringOutput steeringOutput,
        out string trace)
    {
        var steering = steeringOutput;
        var phase = PositiveModulo(
            _thinkTicks + (self.Id * 19) + ((int)self.Team * 29),
            EngineerCtfPatrolCycleTicks);
        var targetOffset = phase switch
        {
            < EngineerCtfPatrolLegTicks => -EngineerCtfPatrolOffset,
            < EngineerCtfPatrolLegTicks + EngineerCtfPatrolPauseTicks => 0f,
            < (EngineerCtfPatrolLegTicks * 2) + EngineerCtfPatrolPauseTicks => EngineerCtfPatrolOffset,
            _ => 0f,
        };
        var targetX = anchor.X + targetOffset;
        var dx = targetX - self.X;
        steering.MoveDirection = MathF.Abs(dx) <= EngineerCtfPatrolTargetDeadZone
            ? 0f
            : dx > 0f ? 1f : -1f;
        steering.Jump = false;
        steering.DropDown = false;
        trace = $"target:{targetX:0.0} phase:{phase} move:{steering.MoveDirection:0}";
        return steering;
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

        if (GetOwnIntelState(world, team).IsCarried)
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
        if (ForceObjectiveNavigationForDiagnostics
            || !IsControlPointEngineerBuilder(world, self))
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

        if (GetOwnIntelState(world, team).IsCarried)
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

        if (GetOwnIntelState(world, team).IsCarried)
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
                    out directTrace,
                    activePathReuseDistance: MovingCarrierRouteReuseDistance)
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
                    out directTrace,
                    requireVerticalSeparation: !_alphaNavigation,
                    activePathReuseDistance: MovingCarrierRouteReuseDistance)
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
                    out directTrace,
                    requireVerticalSeparation: !_alphaNavigation,
                    traceFailure: true,
                    activePathReuseDistance: MovingCarrierRouteReuseDistance)
                || TryResolveDynamicObjectiveLocalMotionRecovery(
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
        if ((_alphaNavigation || CanOwnCaptureTheFlagEnemyObjective())
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
            var escortTarget = new DirectDriveTarget(
                DirectDriveTargetKind.Escort,
                friendlyCarrier.X,
                friendlyCarrier.Y,
                $"dynamicEscortCarrier player:{friendlyCarrier.Id}");
            var escortNeedsTerminalLocalMotion = carrierDistance <= DroppedIntelNearHoldDistance
                && MathF.Abs(carrierDy) <= DroppedIntelNearHorizontalDeadZone;

            // A moving carrier is still a world-scale objective. Let the
            // alpha graph choose the next traversal edge until the bot is in
            // the final local-contact radius. Starting with primitive local
            // motion made body collisions and carrier movement look like
            // route indecision, and it could keep the bot oscillating without
            // ever giving the graph a chance to reattach around the blocker.
            if (!escortNeedsTerminalLocalMotion
                && TryRouteToDirectSeekTarget(
                    world,
                    self,
                    team,
                    friendlyCarrier.X,
                    friendlyCarrier.Y,
                    $"dynamicEscortCarrier player:{friendlyCarrier.Id}",
                    steeringOutput,
                    out directSteering,
                    out directTrace,
                    requireVerticalSeparation: false,
                    traceFailure: true,
                    activePathReuseDistance: MovingCarrierRouteReuseDistance))
            {
                return true;
            }

            if (TryResolveDynamicObjectiveLocalMotionRecovery(
                    world,
                    self,
                    escortTarget,
                    steeringOutput,
                    out directSteering,
                    out directTrace)
                || PrimitiveDirectDrive.TryResolveRecovery(
                    world,
                    self,
                    new DirectDriveTarget(
                        DirectDriveTargetKind.Escort,
                        friendlyCarrier.X,
                        friendlyCarrier.Y,
                        $"dynamicEscortCarrierPrimitive player:{friendlyCarrier.Id}"),
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

    private bool TryResolveHarvestRightSpoolRecovery(
        SimulationWorld world,
        PlayerEntity self,
        bool routeRecoveryRequested,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (self.ClassId != PlayerClass.Pyro
            || !string.Equals(world.Level.Name, "Harvest", StringComparison.OrdinalIgnoreCase)
            || self.X < HarvestRightSpoolPocketMinX
            || self.X > HarvestRightSpoolPocketMaxX
            || self.Bottom < HarvestRightSpoolPocketMinBottom
            || self.Bottom > HarvestRightSpoolPocketMaxBottom)
        {
            _harvestRightSpoolLowMotionTicks = 0;
            return false;
        }

        var lowMotion = MathF.Abs(self.HorizontalSpeed) <= 6f;
        var tryingToMove = routeRecoveryRequested
            || steeringOutput.RequestRepath
            || MathF.Abs(steeringOutput.MoveDirection) > 0.1f;
        if (!lowMotion)
        {
            _harvestRightSpoolLowMotionTicks = 0;
            return false;
        }

        _harvestRightSpoolLowMotionTicks += 1;
        if (!tryingToMove && _harvestRightSpoolLowMotionTicks < 12)
        {
            return false;
        }

        var escapeDirection = self.X < HarvestRightSpoolPocketCenterX ? -1f : 1f;
        if (MathF.Abs(self.X - HarvestRightSpoolPocketCenterX) <= HarvestRightSpoolPocketCenterDeadZone)
        {
            escapeDirection = self.FacingDirectionX < 0f ? -1f : 1f;
        }

        directSteering.MoveDirection = escapeDirection;
        directSteering.Jump = true;
        directSteering.DropDown = false;
        directSteering.RequestRepath = false;
        directTrace =
            $"harvestRightSpoolEscape x:{self.X:0.0} bottom:{self.Bottom:0.0} ticks:{_harvestRightSpoolLowMotionTicks} move:{escapeDirection:0}";
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

        var preferGraphRoute = label.StartsWith("dynamic", StringComparison.Ordinal)
            && (distance > DroppedIntelNearHoldDistance
                // Being close in Euclidean distance is not terminal when the
                // intel is on the platform above/below the bot. A direct
                // move:0/jump:1 fallback leaves bots under Waterway's point
                // hopping in place instead of using the graph's stair route.
                || MathF.Abs(dy) > DroppedIntelNearHorizontalDeadZone);
        if (preferGraphRoute
            && TryRouteToDirectSeekTarget(
                world,
                self,
                team,
                intel.X,
                intel.Y,
                label,
                steeringOutput,
                out directSteering,
                out directTrace,
                requireVerticalSeparation: false,
                traceFailure: true))
        {
            return true;
        }

        if (distance <= DroppedIntelPrimitiveDirectSeekDistance
            && MathF.Abs(dy) <= DroppedIntelPrimitiveDirectSeekVerticalRange
            && TryResolveDynamicObjectiveLocalMotionRecovery(world, self, target, steeringOutput, out directSteering, out directTrace))
        {
            return true;
        }

        if (distance <= DroppedIntelNearHoldDistance)
        {
            var nearMoveDirection = MathF.Abs(dx) > DroppedIntelNearHorizontalDeadZone
                ? dx > 0f ? 1 : -1
                : 0;
            if (nearMoveDirection == 0
                && label.Contains("DroppedIntel", StringComparison.Ordinal))
            {
                // A failed local probe can leave the bot exactly on the
                // dropped-intel marker with neutral steering forever. Give
                // only this dynamic pickup target a short deterministic
                // nudge; control-point holds and combat spacing retain their
                // intentional neutral/strafe behavior.
                nearMoveDirection = MathF.Sign(self.FacingDirectionX);
                if (nearMoveDirection == 0)
                {
                    nearMoveDirection = self.Id % 2 == 0 ? 1 : -1;
                }
            }

            directSteering = steeringOutput;
            directSteering.MoveDirection = nearMoveDirection;
            directSteering.Jump = dy < -DroppedIntelNearHorizontalDeadZone || steeringOutput.Jump;
            directSteering.DropDown = false;
            directSteering.RequestRepath = false;
            directTrace = $"directDrive={label} near dx:{dx:0.0} dy:{dy:0.0} dist:{distance:0.0} move:{directSteering.MoveDirection:0} jump:{(directSteering.Jump ? 1 : 0)}";
            return true;
        }

        if (!preferGraphRoute
            && TryRouteToDirectSeekTarget(
                world,
                self,
                team,
                intel.X,
                intel.Y,
                label,
                steeringOutput,
                out directSteering,
                out directTrace,
                requireVerticalSeparation: false,
                traceFailure: label.StartsWith("dynamic", StringComparison.Ordinal)))
        {
            return true;
        }

        return TryResolveDynamicObjectiveLocalMotionRecovery(world, self, target, steeringOutput, out directSteering, out directTrace);
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
        PlayerTeam? routeTeamOverride = null,
        float activePathReuseDistance = DirectRouteGoalReuseDistance)
    {
        routedSteering = currentSteering;
        trace = string.Empty;
        var routeStartTimestamp = Stopwatch.GetTimestamp();
        var goalSelectionStartTimestamp = routeStartTimestamp;
        var pathSearchStartTimestamp = routeStartTimestamp;
        string StampRouteTiming(string routeTrace)
        {
            var elapsedMilliseconds = (Stopwatch.GetTimestamp() - routeStartTimestamp) * 1000d / Stopwatch.Frequency;
            return elapsedMilliseconds >= 8d
                ? $"{routeTrace} searchMs:{elapsedMilliseconds:0.0}"
                : routeTrace;
        }
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
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        var routeTeam = routeTeamOverride ?? team;
        var routeTeamTrace = routeTeamOverride.HasValue && routeTeamOverride.Value != team
            ? $" routeTeam:{routeTeamOverride.Value}"
            : string.Empty;
        var isDynamicRoute = label.StartsWith("dynamic", StringComparison.Ordinal)
            || label.StartsWith("medicSupport:", StringComparison.Ordinal);
        var isMovingTargetRoute = isDynamicRoute
            || label.StartsWith("controlPointClearEnemy", StringComparison.Ordinal)
            || label.StartsWith("enemy player:", StringComparison.Ordinal)
            || label.StartsWith("ownedKothEnemy", StringComparison.Ordinal)
            || label.StartsWith("recoveryEnemy", StringComparison.Ordinal);
        if (isDynamicRoute
            && _dynamicRouteRetryCooldownTicks > 0)
        {
            _dynamicRouteRetryCooldownTicks -= 1;
            if (traceFailure)
            {
                trace = $"directRoute={label}{routeTeamTrace} reject:retry_cooldown remaining:{_dynamicRouteRetryCooldownTicks}";
            }

            return false;
        }
        if (requireVerticalSeparation && MathF.Abs(dy) < DirectSeekRouteVerticalThreshold)
        {
            if (traceFailure)
            {
                trace = $"directRoute={label}{routeTeamTrace} reject:vertical dx:{dx:0.0} dy:{dy:0.0}";
            }

            return false;
        }

        var activeRouteReuseGoal = isDynamicRoute && _hasDynamicRouteTarget
            ? _dynamicRouteTargetPosition
            : _currentGoalPosition;
        if (_currentPath is not null
            && !_currentPath.IsComplete
            && DistanceBetween(activeRouteReuseGoal.X, activeRouteReuseGoal.Y, targetX, targetY) <= activePathReuseDistance)
        {
            if (IsRejectedDistantDirectSeekRouteGoalProxy(_navGraph, _goalNodeIndex, targetX, targetY, rejectDistantGoalProxy))
            {
                _currentPath = null;
                _goalNodeIndex = -1;
                _repathCooldownTicks = 0;
                trace = $"directRoute={label}{routeTeamTrace} reject:distant_proxy_reuse dx:{dx:0.0} dy:{dy:0.0}";
                return false;
            }

            _currentGoalPosition = (targetX, targetY);
            if (_alphaNavigation)
            {
                _pathObjectiveStateSignature = ComputeObjectiveStateSignature(world);
            }
            _repathCooldownTicks = RepathIntervalTicks;

            // The normal route phase has already advanced this same path
            // through SteeringMachine.Update for this think. Re-running it
            // here for a direct-seek reuse performs collision/contact scans,
            // stuck accounting, and edge-timer advancement twice in one
            // simulation tick. Apart from wasting 20-40 ms on busy Corinth
            // frames, that duplicate state advance can make a bot appear to
            // change its mind. Reuse the output that was just computed; a
            // newly-built direct path below still receives one steering pass.
            routedSteering = currentSteering;
            if (routedSteering.RequestRepath)
            {
                trace = $"directRoute={label}{routeTeamTrace} reject:repath dx:{dx:0.0} dy:{dy:0.0}";
                return false;
            }

            // A moving CTF target can keep a previously selected path alive
            // after that path's current edge has become neutral: for example,
            // the route may be waiting on a runtime contact or may have
            // reached a stale terminal surface while the carrier moved on.
            // Treating that neutral output as a successful dynamic route
            // suppresses LocalMotionController recovery and leaves the bot
            // inert while the trace continues to say "reuseMoving". Combat
            // and fixed-target direct routes keep their existing behavior;
            // only dynamic objective targets are allowed to fall through to
            // their cheap local recovery path here.
            if (label.StartsWith("dynamic", StringComparison.Ordinal)
                && IsNeutralNavigationOutput(routedSteering)
                && (distance > DroppedIntelNearHoldDistance
                    || MathF.Abs(dy) > DroppedIntelNearHorizontalDeadZone))
            {
                // Dynamic CTF routing is resolved before the normal graph
                // steering phase in Think().  A reused path therefore has
                // not been advanced yet; returning the caller's empty
                // SteeringOutput here made every moving-carrier route look
                // valid while emitting neutral input. Advance the existing
                // path once in the dynamic owner, matching the normal route
                // phase contract. Only fall through when that real steering
                // pass is still neutral or requests a repath.
                if (_alphaNavigation)
                {
                    PrepareAlphaRuntimeContact(self, team, world.Level);
                }

                routedSteering = _steering.Update(
                    self,
                    _navGraph,
                    _currentPath,
                    world.Level,
                    team);
                if (routedSteering.RequestRepath)
                {
                    HandleSteeringRepathRequest(self, team, routedSteering);
                    trace = $"directRoute={label}{routeTeamTrace} reject:repath_reuse dx:{dx:0.0} dy:{dy:0.0}";
                    return false;
                }

                if (IsNeutralNavigationOutput(routedSteering))
                {
                    _currentPath = null;
                    _goalNodeIndex = -1;
                    _repathCooldownTicks = 0;
                    MarkAlphaRecoveryPending();
                    _steering.Reset();
                    trace = $"directRoute={label}{routeTeamTrace} reject:neutral_reuse dx:{dx:0.0} dy:{dy:0.0}";
                    return false;
                }
            }

            var reuseKind = activePathReuseDistance > DirectRouteGoalReuseDistance
                ? "reuseMoving"
                : "reuse";
            trace = StampRouteTiming($"directRoute={label}{routeTeamTrace} {reuseKind} dx:{dx:0.0} dy:{dy:0.0} path:{_currentPath.Count}");
            return true;
        }

        var startNode = _navGraph.FindNearestTraversalStartNode(
            self.X,
            self.Y,
            ResolveTraversalStartMaxAboveDistance(self, _currentPath is null),
            _alphaNavigation ? AlphaTraversalStartMaxBelowDistance : float.PositiveInfinity);
        goalSelectionStartTimestamp = Stopwatch.GetTimestamp();
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
        var preferNearestGoalFastPath = _alphaNavigation
            && (isMovingTargetRoute
                || label.StartsWith("engineerIntelDefense", StringComparison.Ordinal));
        var isMedicSupportRoute = label.StartsWith("medicSupport:", StringComparison.Ordinal);
        // Moving targets do not justify an unbounded A* on the simulation
        // thread. Keep the class-specific retry smaller for Medic support,
        // while allowing dynamic CTF targets enough room to retain a useful
        // route before the local recovery lane takes over.
        var movingTargetPathSearchBudgetMilliseconds = isMedicSupportRoute
            ? 8d
            : isMovingTargetRoute
                ? 8d
                : 24d;
        var allowExpensiveDynamicGoalFallback =
            !isMedicSupportRoute
            && !isMovingTargetRoute;
        var goalNode = preferNearestGoalFastPath
            ? _navGraph.FindNearestNode(targetX, targetY)
            : _navGraph.FindNearestReachableNode(
                targetX,
                targetY,
                startNode,
                self.BotGraphClassId,
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
            && !ShouldReplaceStalePathFromCurrentPosition(self, _navGraph, _currentPath, _alphaNavigation))
        {
            _currentGoalPosition = (targetX, targetY);
            if (isDynamicRoute)
            {
                _hasDynamicRouteTarget = true;
                _dynamicRouteTargetPosition = (targetX, targetY);
            }
            _repathCooldownTicks = RepathIntervalTicks;

            // As above, the route phase already evaluated this unchanged
            // path. Keeping the same SteeringOutput avoids a second mutation
            // of the stateful edge executor in the same think.
            routedSteering = currentSteering;
            if (routedSteering.RequestRepath)
            {
                trace = $"directRoute={label}{routeTeamTrace} reject:repath dx:{dx:0.0} dy:{dy:0.0}";
                return false;
            }

            if (label.StartsWith("dynamic", StringComparison.Ordinal)
                && IsNeutralNavigationOutput(routedSteering)
                && (distance > DroppedIntelNearHoldDistance
                    || MathF.Abs(dy) > DroppedIntelNearHorizontalDeadZone))
            {
                // This branch is also reached by the pre-route dynamic CTF
                // resolver. The moving target may have changed its precise
                // position while the graph goal node stayed the same; do not
                // mistake that goal-node reuse for an already-advanced route.
                if (_alphaNavigation)
                {
                    PrepareAlphaRuntimeContact(self, team, world.Level);
                }

                routedSteering = _steering.Update(
                    self,
                    _navGraph,
                    _currentPath,
                    world.Level,
                    team);
                if (routedSteering.RequestRepath)
                {
                    HandleSteeringRepathRequest(self, team, routedSteering);
                    trace = $"directRoute={label}{routeTeamTrace} reject:repath_reuseGoal dx:{dx:0.0} dy:{dy:0.0}";
                    return false;
                }

                if (IsNeutralNavigationOutput(routedSteering))
                {
                    _currentPath = null;
                    _goalNodeIndex = -1;
                    _repathCooldownTicks = 0;
                    MarkAlphaRecoveryPending();
                    _steering.Reset();
                    trace = $"directRoute={label}{routeTeamTrace} reject:neutral_reuseGoal dx:{dx:0.0} dy:{dy:0.0}";
                    return false;
                }
            }

            trace = StampRouteTiming($"directRoute={label}{routeTeamTrace} reuseGoal dx:{dx:0.0} dy:{dy:0.0} goal:{goalNode} path:{_currentPath.Count}");
            return true;
        }

        pathSearchStartTimestamp = Stopwatch.GetTimestamp();
        var path = preferNearestGoalFastPath
            ? _navGraph.FindPath(
                startNode,
                goalNode,
                playerClass: null,
                // Dynamic/support targets are resolved frequently. Reuse the
                // immutable alpha route cache even while an unrelated edge
                // is blocked, then validate the returned route below. Only
                // fall back to a blocked-edge search when this specific path
                // actually uses a blocked transition.
                blockedEdges: null,
                team: null,
                carryingIntel: self.IsCarryingIntel,
                maxSearchMilliseconds: movingTargetPathSearchBudgetMilliseconds,
                traceContext: label,
                routeVariant: ResolveRouteVariant(self))
            : _navGraph.FindPath(
                startNode,
                goalNode,
                self.BotGraphClassId,
                activeBlockedEdges,
                routeTeam,
                self.IsCarryingIntel,
                maxSearchMilliseconds: isMovingTargetRoute
                    ? movingTargetPathSearchBudgetMilliseconds
                    : 0d,
                traceContext: label,
                routeVariant: ResolveRouteVariant(self));
        if (preferNearestGoalFastPath
            && path is not null
            && (!_navGraph.IsPathCompatible(path, self.BotGraphClassId, routeTeam, self.IsCarryingIntel)
                || ContainsBlockedNavigationEdge(path, activeBlockedEdges)))
        {
            path = _navGraph.FindPath(
                startNode,
                goalNode,
                self.BotGraphClassId,
                activeBlockedEdges,
                routeTeam,
                self.IsCarryingIntel,
                maxSearchMilliseconds: movingTargetPathSearchBudgetMilliseconds,
                traceContext: $"{label}:compatible",
                routeVariant: ResolveRouteVariant(self));
        }
        if (path is null
            && preferNearestGoalFastPath
            && allowExpensiveDynamicGoalFallback)
        {
            // Dynamic targets are usually standing on the same connected
            // surface as the bot. The nearest-node path above avoids a full
            // reachable-goal flood in that common case. If class/team filters
            // reject that node, retain the exact reachable-goal behavior as a
            // fallback rather than accepting a proxy or abandoning the route.
            goalNode = _navGraph.FindNearestReachableNode(
                targetX,
                targetY,
                startNode,
                self.BotGraphClassId,
                activeBlockedEdges,
                team: routeTeam,
                carryingIntel: self.IsCarryingIntel,
                verticalWeight: 8f,
                penalizeLowerCandidate: true);
            path = goalNode >= 0
                ? _navGraph.FindPath(
                    startNode,
                    goalNode,
                    self.BotGraphClassId,
                    activeBlockedEdges,
                    routeTeam,
                    self.IsCarryingIntel,
                    maxSearchMilliseconds: movingTargetPathSearchBudgetMilliseconds,
                    traceContext: $"{label}:reachable",
                    routeVariant: ResolveRouteVariant(self))
                : null;
        }
        // Once an edge has been marked failed, an unblocked fallback is not a
        // fallback at all: it can immediately select the same transition and
        // put the bot back into the obstruction that just rejected it. This
        // was especially harmful for dynamic CTF targets, where the route
        // resolver runs frequently and could reintroduce the failed edge on
        // the very next think. Let local recovery own the interval until the
        // failed-edge block expires or a blocked search proves an alternative.
        if (path is null
            && activeBlockedEdges is not { Count: > 0 }
            && allowExpensiveDynamicGoalFallback
            && (ShouldPreferCarrierReturnGraph(world, self)
                || !self.IsCarryingIntel
                || !ShouldPreserveCarrierFailedEdgeBlocks(world.Level, self)))
        {
            path = _navGraph.FindPath(
                startNode,
                goalNode,
                self.BotGraphClassId,
                team: routeTeam,
                carryingIntel: self.IsCarryingIntel,
                maxSearchMilliseconds: movingTargetPathSearchBudgetMilliseconds,
                traceContext: $"{label}:unblocked",
                routeVariant: ResolveRouteVariant(self));
        }
        if (path is null || path.Count < 2)
        {
            if (isDynamicRoute && _alphaNavigation)
            {
                // A failed dynamic search must not become a synchronous
                // search hot loop. The caller still gets the normal local
                // recovery lane while this short cooldown expires.
                _dynamicRouteRetryCooldownTicks = DynamicRouteRetryCooldownTicks;
            }

            if (traceFailure)
            {
                trace = $"directRoute={label}{routeTeamTrace} reject:no_path start:{startNode} goal:{goalNode} dx:{dx:0.0} dy:{dy:0.0}";
            }

            return false;
        }

        var pathSearchCompletedTimestamp = Stopwatch.GetTimestamp();
        _currentGoalPosition = (targetX, targetY);
        _currentPath = path;
        _goalNodeIndex = goalNode;
        if (isDynamicRoute)
        {
            _hasDynamicRouteTarget = true;
            _dynamicRouteTargetPosition = (targetX, targetY);
            _dynamicRouteRetryCooldownTicks = 0;
        }
        else
        {
            _hasDynamicRouteTarget = false;
        }
        if (_alphaNavigation)
        {
            _pathObjectiveStateSignature = ComputeObjectiveStateSignature(world);
        }
        _repathCooldownTicks = RepathIntervalTicks;
        _steering.Reset();
        var steeringStartTimestamp = Stopwatch.GetTimestamp();
        routedSteering = _steering.Update(self, _navGraph, _currentPath, world.Level, team);
        var steeringElapsedMilliseconds = (Stopwatch.GetTimestamp() - steeringStartTimestamp) * 1000d / Stopwatch.Frequency;
        if (routedSteering.RequestRepath)
        {
            HandleSteeringRepathRequest(self, team, routedSteering);
            trace = $"directRoute={label}{routeTeamTrace} reject:repath dx:{dx:0.0} dy:{dy:0.0} path:{path.Count}";
            return false;
        }

        if (_alphaNavigation
            && isDynamicRoute
            && IsNeutralNavigationOutput(routedSteering)
            && (distance > DroppedIntelNearHoldDistance
                || MathF.Abs(dy) > DroppedIntelNearHorizontalDeadZone))
        {
            // A newly-created dynamic route can land on a graph node whose
            // first edge is already complete/neutral even though the live
            // carrier or dropped-intel marker is still offset from the bot.
            // Do not report that path as the movement owner: clear only this
            // dynamic attachment and let the bounded local recovery lane
            // choose a real input for the live target.
            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = 0;
            _hasDynamicRouteTarget = false;
            _steering.Reset();
            MarkAlphaRecoveryPending();
            trace = $"directRoute={label}{routeTeamTrace} reject:neutral_new dx:{dx:0.0} dy:{dy:0.0} path:{path.Count}";
            return false;
        }

        var totalSearchMilliseconds = (Stopwatch.GetTimestamp() - routeStartTimestamp) * 1000d / Stopwatch.Frequency;
        if (totalSearchMilliseconds >= 8d)
        {
            var goalMilliseconds = (pathSearchStartTimestamp - goalSelectionStartTimestamp) * 1000d / Stopwatch.Frequency;
            var pathMilliseconds = (pathSearchCompletedTimestamp - pathSearchStartTimestamp) * 1000d / Stopwatch.Frequency;
            trace = $"directRoute={label}{routeTeamTrace} dx:{dx:0.0} dy:{dy:0.0} path:{path.Count} " +
                $"searchMs:{totalSearchMilliseconds:0.0} goalMs:{goalMilliseconds:0.0} pathMs:{pathMilliseconds:0.0} steeringMs:{steeringElapsedMilliseconds:0.0}";
        }
        else
        {
            trace = $"directRoute={label}{routeTeamTrace} dx:{dx:0.0} dy:{dy:0.0} path:{path.Count}";
        }
        return true;
    }

    private static bool IsNeutralNavigationOutput(SteeringOutput steering) =>
        MathF.Abs(steering.MoveDirection) <= 0.01f
        && !steering.Jump
        && !steering.DropDown;

    private static bool ContainsBlockedNavigationEdge(
        NavPath path,
        IReadOnlySet<NavEdgeBlock>? blockedEdges)
    {
        if (blockedEdges is null || blockedEdges.Count == 0)
        {
            return false;
        }

        for (var index = 1; index < path.Count; index += 1)
        {
            if (!path.TryGetIncomingEdge(index, out var edge))
            {
                continue;
            }

            var fromNode = path.GetWaypoint(index - 1);
            var toNode = path.GetWaypoint(index);
            if (blockedEdges.Contains(new NavEdgeBlock(fromNode, toNode, edge.Kind)))
            {
                return true;
            }
        }

        return false;
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
        if (_alphaNavigation
            || world.MatchRules.Mode is not (GameModeKind.Arena or GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill))
        {
            return false;
        }

        if (TryFindNearestUnownedControlPoint(world, self, team, CapturePointDirectSeekDistance, out var point))
        {
            var inCaptureZone = world.IsPlayerInControlPointCaptureZone(self, point.Index);
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

            if (inCaptureZone
                && TryResolveControlPointHold(world, self, team, point, steeringOutput, out directSteering, out directTrace))
            {
                return true;
            }

            if (ShouldKeepGraphControlForBelowPointClimb(self, point))
            {
                return false;
            }

            if (_navGraph is null
                && TryResolveNoGraphControlPointObstacleNudge(world, self, team, point, steeringOutput, out directSteering, out directTrace))
            {
                return true;
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
                ApplyCapturePointObstacleJumpIfNeeded(world, self, team, point, ref directSteering, ref directTrace);
                return true;
            }

            if (!inCaptureZone
                && TryResolveControlPointHold(world, self, team, point, steeringOutput, out directSteering, out directTrace))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveNoGraphControlPointObstacleNudge(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        ControlPointState point,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (world.IsPlayerInControlPointCaptureZone(self, point.Index)
            || !self.IsGrounded
            || steeringOutput.Jump
            || steeringOutput.DropDown)
        {
            return false;
        }

        var pointDx = point.HealingAuraCenterX - self.X;
        var laneTargetX = ResolveCapturePointLaneTargetX(world, self, team, point);
        var laneDx = laneTargetX - self.X;
        if (MathF.Abs(pointDx) > CapturePointHoldCenterDeadZone
            && MathF.Abs(laneDx) > CapturePointHoldHorizontalRange)
        {
            return false;
        }

        var direction = MathF.Abs(laneDx) > CapturePointLaneTargetDeadZone
            ? MathF.Sign(laneDx)
            : MathF.Sign(pointDx);
        if (direction == 0)
        {
            direction = point.HealingAuraCenterX >= self.X ? 1 : -1;
        }

        if (!IsJumpableCapturePointObstacleAhead(self, world.Level, team, direction))
        {
            return false;
        }

        directSteering.MoveDirection = direction;
        directSteering.Jump = true;
        directSteering.DropDown = false;
        directSteering.RequestRepath = false;
        directTrace = $"capturePointObstacleJump point:{point.Index} noGraphNudge move:{direction:0}";
        return true;
    }

    private static void ApplyCapturePointObstacleJumpIfNeeded(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        ControlPointState point,
        ref SteeringOutput steering,
        ref string trace)
    {
        if (world.IsPlayerInControlPointCaptureZone(self, point.Index)
            || !self.IsGrounded
            || steering.Jump
            || steering.DropDown
            || steering.MoveDirection == 0f
            || !IsJumpableCapturePointObstacleAhead(self, world.Level, team, steering.MoveDirection))
        {
            return;
        }

        steering.Jump = true;
        trace = string.IsNullOrWhiteSpace(trace)
            ? $"capturePointObstacleJump point:{point.Index}"
            : $"{trace} capturePointObstacleJump point:{point.Index}";
    }

    private bool TryResolveControlPointEnemyClearSeek(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        var stageStartTimestamp = NavigationStageTracingEnabled
            ? Stopwatch.GetTimestamp()
            : 0L;
        var resolved = TryResolveControlPointEnemyClearSeekCore(
            world,
            self,
            team,
            steeringOutput,
            out directSteering,
            out directTrace);
        TraceSlowNavigationStage(self, "TryResolveControlPointEnemyClearSeek", stageStartTimestamp);
        return resolved;
    }

    private bool TryResolveControlPointEnemyClearSeekCore(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        SteeringOutput steeringOutput,
        out SteeringOutput directSteering,
        out string directTrace)
    {
        directSteering = steeringOutput;
        directTrace = string.Empty;
        if (!TryFindControlPointEnemyClearTarget(world, self, team, out var target, out var point))
        {
            return false;
        }

        // Alpha navigation owns the long-range objective route.  A visible
        // enemy near a point must not replace that route while the bot is
        // still approaching from the rest of the map: doing so makes every
        // small enemy movement rebuild/retarget a route and produces the
        // characteristic forward/backward oscillation.  Once the bot is in
        // the capture zone or in the point's immediate engagement pocket,
        // combat is allowed to take ownership and clear the point.
        if (_alphaNavigation
            && !world.IsPlayerInControlPointCaptureZone(self, point.Index)
            && DistanceBetween(self.X, self.Y, point.HealingAuraCenterX, point.HealingAuraCenterY)
                > AlphaCapturePointCombatEngagementDistance)
        {
            return false;
        }

        var dx = target.X - self.X;
        var dy = target.Y - self.Y;
        if (MathF.Abs(dy) <= CapturePointClearEnemyVerticalRange
            && MathF.Abs(dx) <= CapturePointClearEnemyDistance)
        {
            var distance = MathF.Sqrt((dx * dx) + (dy * dy));
            directSteering.MoveDirection = PrimitiveDirectDrive.ResolveCombatMoveDirection(self, distance, dx);
            directSteering.Jump = steeringOutput.Jump || dy < -24f;
            directSteering.DropDown = false;
            directTrace = $"controlPointClearEnemy point:{point.Index} player:{target.Id} direct combat:spacing dx:{dx:0.0} dy:{dy:0.0} move:{directSteering.MoveDirection:0}";
            if (TryResolveBlockedLocalMotion(
                    world,
                    self,
                    new DirectDriveTarget(
                        DirectDriveTargetKind.Enemy,
                        target.X,
                        target.Y,
                        $"controlPointClearEnemy point:{point.Index} player:{target.Id}"),
                    directSteering.MoveDirection,
                    steeringOutput,
                    out var recoverySteering,
                    out var recoveryTrace))
            {
                directSteering = recoverySteering;
                directTrace = $"{directTrace} blockedRecovery:{recoveryTrace}";
            }

            return true;
        }

        var directRouteResolved = TryRouteToDirectSeekTarget(
                world,
                self,
                team,
                target.X,
                target.Y,
                $"controlPointClearEnemy point:{point.Index} player:{target.Id}",
                steeringOutput,
                out directSteering,
                out directTrace,
                requireVerticalSeparation: false);
        if (directRouteResolved && IsNeutralNavigationOutput(directSteering))
        {
            // A direct combat route is an optional movement owner. If its
            // stateful graph edge returns neutral, do not report success and
            // suppress the objective route for the rest of the think. Keep
            // combat targeting intact, but hand movement back to the alpha
            // objective recovery lane below.
            _currentPath = null;
            _goalNodeIndex = -1;
            _repathCooldownTicks = 0;
            _hasDynamicRouteTarget = false;
            _steering.Reset();
            MarkAlphaRecoveryPending();
            directTrace = $"{directTrace} reject:neutral_combat_route";
            directRouteResolved = false;
        }

        if (directRouteResolved
            || TryResolveLocalMotionRecovery(
                world,
                self,
                new DirectDriveTarget(
                    DirectDriveTargetKind.Enemy,
                    target.X,
                    target.Y,
                    $"controlPointClearEnemy point:{point.Index} player:{target.Id}"),
                steeringOutput,
                out directSteering,
                out directTrace))
        {
            return true;
        }

        return false;
    }

    private bool TryResolveBlockedLocalMotion(
        SimulationWorld world,
        PlayerEntity self,
        DirectDriveTarget target,
        float moveDirection,
        SteeringOutput steeringOutput,
        out SteeringOutput recoverySteering,
        out string recoveryTrace)
    {
        recoverySteering = steeringOutput;
        recoveryTrace = string.Empty;
        if (MathF.Abs(moveDirection) <= 0.01f
            || !PrimitiveDirectDrive.WouldMoveIntoObstacle(world, self, MathF.Sign(moveDirection)))
        {
            return false;
        }

        if (TryResolveLocalMotionRecovery(world, self, target, steeringOutput, out recoverySteering, out recoveryTrace))
        {
            return true;
        }

        if (IsLocalMotionSuppressionTrace(recoveryTrace))
        {
            recoverySteering = steeringOutput;
            recoveryTrace = $"hold {recoveryTrace}";
            return true;
        }

        return false;
    }

    private static bool TryFindControlPointEnemyClearTarget(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        out PlayerEntity target,
        out ControlPointState point)
    {
        target = null!;
        point = null!;
        if (world.MatchRules.Mode is not (GameModeKind.Arena or GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill))
        {
            return false;
        }

        var opposingTeam = team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        var bestScore = float.PositiveInfinity;
        foreach (var candidatePoint in world.ControlPoints)
        {
            var selfDx = candidatePoint.HealingAuraCenterX - self.X;
            var selfDy = candidatePoint.HealingAuraCenterY - self.Y;
            var selfDistance = MathF.Sqrt((selfDx * selfDx) + (selfDy * selfDy));
            if (selfDistance > CapturePointClearSelfInterestDistance
                && !world.IsPlayerInControlPointCaptureZone(self, candidatePoint.Index))
            {
                continue;
            }

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

                if (!CombatDecisionResolver.IsPlayerVisibleToBot(self, candidate)
                    || !CombatDecisionResolver.HasLineOfSight(world, self.X, self.Y, candidate.X, candidate.Y, self.Team, self.IsCarryingIntel))
                {
                    continue;
                }

                var enemyDx = candidate.X - candidatePoint.HealingAuraCenterX;
                var enemyDy = candidate.Y - candidatePoint.HealingAuraCenterY;
                var enemyNearPoint = world.IsPlayerInControlPointCaptureZone(candidate, candidatePoint.Index)
                    || (MathF.Sqrt((enemyDx * enemyDx) + (enemyDy * enemyDy)) <= CapturePointClearEnemyDistance
                        && MathF.Abs(enemyDy) <= CapturePointClearEnemyVerticalRange);
                if (!enemyNearPoint)
                {
                    continue;
                }

                var dx = candidate.X - self.X;
                var dy = candidate.Y - self.Y;
                var distanceSq = (dx * dx) + (dy * dy);
                var score = distanceSq + (world.IsPlayerInControlPointCaptureZone(candidate, candidatePoint.Index) ? 0f : 10_000f);
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                target = candidate;
                point = candidatePoint;
            }
        }

        return target is not null;
    }

    private static bool ShouldSuspendGraphRoutingForControlPointCapture(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        bool alphaNavigation = false)
    {
        if (TryFindActivePointBeingCaptured(world, self, team, out _))
        {
            return true;
        }

        if (alphaNavigation
            || !TryFindNearestUnownedControlPoint(world, self, team, CapturePointHoldHorizontalRange, out var point))
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

        var obstacleJump = !inCaptureZone
            && self.IsGrounded
            && holdSteering.MoveDirection != 0f
            && IsJumpableCapturePointObstacleAhead(self, world.Level, team, holdSteering.MoveDirection);
        holdSteering.Jump = inCaptureZone
            ? false
            : self.IsGrounded && (dy < -24f || obstacleJump);
        holdSteering.DropDown = false;
        var spacingSuffix = string.IsNullOrWhiteSpace(spacingTrace)
            ? string.Empty
            : $" spacing:{spacingTrace}";
        trace = $"capturePointHold point:{point.Index} dx:{dx:0.0} dy:{dy:0.0} inZone:{(inCaptureZone ? 1 : 0)} move:{holdSteering.MoveDirection:0} jump:{(holdSteering.Jump ? 1 : 0)} obstacle:{(obstacleJump ? 1 : 0)}{spacingSuffix}";
        return true;
    }

    private static bool IsJumpableCapturePointObstacleAhead(PlayerEntity player, SimpleLevel level, PlayerTeam team, float direction)
    {
        if (direction == 0f)
        {
            return false;
        }

        var blockedOffset = FindCapturePointObstacleOffsetAhead(player, level, team, MathF.Sign(direction));
        if (!blockedOffset.HasValue)
        {
            return false;
        }

        return CanClearCapturePointObstacleAtLift(player, level, team, direction, blockedOffset.Value, 16f)
            || CanClearCapturePointObstacleAtLift(player, level, team, direction, blockedOffset.Value, 32f)
            || CanClearCapturePointObstacleAtLift(player, level, team, direction, blockedOffset.Value, 48f)
            || CanClearCapturePointObstacleAtLift(player, level, team, direction, blockedOffset.Value, 64f);
    }

    private static float? FindCapturePointObstacleOffsetAhead(PlayerEntity player, SimpleLevel level, PlayerTeam team, float direction)
    {
        for (var offset = 4f; offset <= CapturePointObstacleProbeDistance; offset += 4f)
        {
            if (!player.CanOccupy(level, team, player.X + (direction * offset), player.Y))
            {
                return offset;
            }
        }

        return null;
    }

    private static bool CanClearCapturePointObstacleAtLift(
        PlayerEntity player,
        SimpleLevel level,
        PlayerTeam team,
        float direction,
        float blockedOffset,
        float lift)
    {
        var liftedY = player.Y - lift;
        var clearProbeOffset = MathF.Max(CapturePointObstacleProbeDistance, blockedOffset + 8f);
        return player.CanOccupy(level, team, player.X, liftedY)
            && player.CanOccupy(level, team, player.X + (direction * blockedOffset), liftedY)
            && player.CanOccupy(level, team, player.X + (direction * clearProbeOffset), liftedY);
    }

    private void ApplyCaptureStrafeHop(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        ref SteeringOutput steeringOutput)
    {
        if (_alphaNavigation)
        {
            ApplyAlphaCaptureArrivalHold(world, self, team, ref steeringOutput);
            return;
        }

        if (!TryFindControlPointStrafeTarget(world, self, team, out var point, out var reason))
        {
            return;
        }

        if (TryFindControlPointEnemyClearTarget(world, self, team, out _, out _))
        {
            return;
        }

        var style = ResolveCapturePointHoldStyle(self, team, point);
        if (style == CapturePointHoldStyle.Hold)
        {
            steeringOutput.MoveDirection = 0f;
            steeringOutput.Jump = false;
            steeringOutput.DropDown = false;
            LastDirectDriveTrace = string.IsNullOrWhiteSpace(LastDirectDriveTrace)
                ? $"captureHold point:{point.Index} reason:{reason} style:hold"
                : $"{LastDirectDriveTrace} captureHold point:{point.Index} reason:{reason} style:hold";
            return;
        }

        var centerBand = Math.Clamp(point.Marker.Width * CaptureStrafeCenterBand, CapturePointLaneTargetDeadZone, 32f);
        var laneTargetX = ResolveCapturePointLaneTargetX(world, self, team, point);
        var dxFromTarget = self.X - laneTargetX;
        var phase = PositiveModulo(
            _thinkTicks + (self.Id * 11) + ((int)team * 17) + (point.Index * 7),
            CaptureStrafeHopCycleTicks);
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
        if (style == CapturePointHoldStyle.StrafeHop && self.IsGrounded && inHopWindow)
        {
            steeringOutput.Jump = true;
        }
        else
        {
            steeringOutput.Jump = false;
        }

        var spacingSuffix = string.IsNullOrWhiteSpace(spacingTrace)
            ? string.Empty
            : $" spacing:{spacingTrace}";
        LastDirectDriveTrace = string.IsNullOrWhiteSpace(LastDirectDriveTrace)
            ? $"captureStrafeHop point:{point.Index} reason:{reason} style:{style} move:{moveDirection} phase:{phase}{spacingSuffix}"
            : $"{LastDirectDriveTrace} captureStrafeHop point:{point.Index} reason:{reason} style:{style} move:{moveDirection} phase:{phase}{spacingSuffix}";
    }

    private void ApplyAlphaCaptureArrivalHold(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        ref SteeringOutput steeringOutput)
    {
        if (world.MatchRules.Mode is not (GameModeKind.Arena
            or GameModeKind.ControlPoint
            or GameModeKind.KingOfTheHill
            or GameModeKind.DoubleKingOfTheHill))
        {
            return;
        }

        var pathComplete = _currentPath is null || _currentPath.IsComplete;
        ControlPointState? bestPoint = null;
        var bestDistance = float.PositiveInfinity;
        var bestInZone = false;
        foreach (var point in world.ControlPoints)
        {
            var (activeZoneCenterX, activeZoneCenterY, _, _) = ResolveCapturePointActiveZone(world, self, point);
            var goalDistance = DistanceBetween(
                _currentGoalPosition.X,
                _currentGoalPosition.Y,
                activeZoneCenterX,
                activeZoneCenterY);
            if (goalDistance > 128f)
            {
                continue;
            }

            var inZone = world.IsPlayerInControlPointCaptureZone(self, point.Index);
            if (!inZone && !pathComplete)
            {
                continue;
            }

            var horizontalDistance = MathF.Abs(activeZoneCenterX - self.X);
            var verticalDistance = MathF.Abs(activeZoneCenterY - self.Y);
            if (!inZone && (horizontalDistance > 112f || verticalDistance > 96f))
            {
                continue;
            }

            if (bestPoint is not null
                && ((bestInZone && !inZone)
                    || (bestInZone == inZone && goalDistance >= bestDistance)))
            {
                continue;
            }

            bestPoint = point;
            bestDistance = goalDistance;
            bestInZone = inZone;
        }

        if (bestPoint is null)
        {
            return;
        }

        // An enemy already inside the point must remain the combat objective;
        // the control-point enemy-clear resolver ran earlier in Think and its
        // steering must not be overwritten by this arrival helper.
        var botOwnsPoint = bestPoint.Team == team;
        var botIsCapturingPoint = bestPoint.CappingTeam == team;
        var enemyIsCapturingPoint = bestPoint.CappingTeam.HasValue
            && bestPoint.CappingTeam != team;
        if (enemyIsCapturingPoint
            || (bestInZone
                && !DisableCombatForDiagnostics
                && TryFindControlPointEnemyClearTarget(world, self, team, out _, out _)))
        {
            return;
        }

        // A completed alpha route can leave a bot in an enemy-owned point
        // while the simulation has not yet exposed a capping team (for
        // example while the point is contested or during the ownership
        // transition). Keep it engaged in the live capture volume rather than
        // returning neutral input. Once the point is no longer enemy-owned,
        // this is the normal owned-point arrival hold below.
        var contestingEnemyPoint = bestInZone && !botOwnsPoint && !botIsCapturingPoint;

        var targetX = ResolveCapturePointLaneTargetX(world, self, team, bestPoint);
        if (bestInZone && botOwnsPoint && !botIsCapturingPoint)
        {
            var patrolPhase = PositiveModulo(
                _thinkTicks + (self.Id * 29) + (bestPoint.Index * 17),
                CapturePointDefensePatrolCycleTicks);
            var patrolOffset = patrolPhase < CapturePointDefensePatrolLegTicks
                ? -CapturePointDefensePatrolOffset
                : patrolPhase < CapturePointDefensePatrolLegTicks * 2
                    ? CapturePointDefensePatrolOffset
                    : 0f;
            var (zoneCenterX, _, zoneWidth, _) = ResolveCapturePointActiveZone(world, self, bestPoint);
            var (zoneMinX, zoneMaxX) = ResolveCapturePointLaneBounds(zoneCenterX, zoneWidth);
            targetX = Math.Clamp(targetX + patrolOffset, zoneMinX, zoneMaxX);
        }
        var dx = targetX - self.X;
        var moveDirection = MathF.Abs(dx) > CapturePointLaneTargetDeadZone
            ? dx > 0f ? 1 : -1
            : MathF.Abs(self.HorizontalSpeed) > CaptureStrafeBrakeSpeed
                ? self.HorizontalSpeed > 0f ? -1 : 1
                : 0;

        steeringOutput.MoveDirection = moveDirection;
        steeringOutput.Jump = false;
        steeringOutput.DropDown = false;
        LastDirectDriveTrace = string.IsNullOrWhiteSpace(LastDirectDriveTrace)
            ? $"alphaCapture{(contestingEnemyPoint ? "Contest" : "Arrival")}Hold point:{bestPoint.Index} inZone:{(bestInZone ? 1 : 0)} targetX:{targetX:0.0} dx:{dx:0.0} move:{moveDirection}"
            : $"{LastDirectDriveTrace} alphaCapture{(contestingEnemyPoint ? "Contest" : "Arrival")}Hold point:{bestPoint.Index} inZone:{(bestInZone ? 1 : 0)} targetX:{targetX:0.0} dx:{dx:0.0} move:{moveDirection}";
    }

    private CapturePointHoldStyle ResolveCapturePointHoldStyle(
        PlayerEntity self,
        PlayerTeam team,
        ControlPointState point)
    {
        var styleEpoch = _thinkTicks / 150;
        var style = PositiveModulo(
            (self.Id * 5) + (point.Index * 3) + ((int)team * 2) + styleEpoch,
            3);
        return style switch
        {
            0 => CapturePointHoldStyle.Hold,
            1 => CapturePointHoldStyle.Strafe,
            _ => CapturePointHoldStyle.StrafeHop,
        };
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
        var (centerX, _, width, _) = ResolveCapturePointActiveZone(world, self, point);
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

    private static (float CenterX, float CenterY, float Width, float Height) ResolveCapturePointActiveZone(
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
            ? (bestZone.Value.CenterX, bestZone.Value.CenterY, bestZone.Value.Width, bestZone.Value.Height)
            : (point.Marker.CenterX, point.Marker.CenterY, point.Marker.Width, point.Marker.Height);
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
        var (centerX, _, width, _) = ResolveCapturePointActiveZone(world, self, point);
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
        if (point.Team == team)
        {
            return null;
        }

        if (point.IsLocked)
        {
            return currentGoalPoint ? "lockedCapture" : null;
        }

        return point.CappingTeam == team ? "capture" : "captureStaging";
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

            if (!CombatDecisionResolver.HasCombatLineOfSight(world, self.X, self.Y, candidate.X, candidate.Y))
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

    private static int PositiveModulo(int value, int modulo)
    {
        var result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private enum CapturePointHoldStyle
    {
        Hold,
        Strafe,
        StrafeHop,
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

    private static float DistanceSquared(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return (dx * dx) + (dy * dy);
    }

    private static float DistanceBetween(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static void TraceSlowNavigationStage(
        PlayerEntity self,
        string stage,
        long startTimestamp,
        long? endTimestamp = null)
    {
        if (startTimestamp == 0L)
        {
            return;
        }

        var elapsedMilliseconds = ((endTimestamp ?? Stopwatch.GetTimestamp()) - startTimestamp)
            * 1000d
            / Stopwatch.Frequency;
        if (elapsedMilliseconds < 20d)
        {
            return;
        }

        Console.WriteLine(
            $"[botbrain] alpha-stage slowMs:{elapsedMilliseconds:0.0} stage:{stage} " +
            $"player:{self.Id} class:{self.ClassId} pos:({self.X:0.0},{self.Y:0.0})");
    }

    private readonly record struct AlphaRecoverySearchFailure(
        int StartNode,
        int GoalNode,
        int ObjectiveSignature,
        int BlockedEdgesVersion,
        PlayerClass GraphClass,
        PlayerTeam Team,
        bool CarryingIntel,
        int ExpiresThinkTick)
    {
        public bool Matches(
            int startNode,
            int goalNode,
            int objectiveSignature,
            int blockedEdgesVersion,
            PlayerClass graphClass,
            PlayerTeam team,
            bool carryingIntel) =>
            StartNode == startNode
            && GoalNode == goalNode
            && ObjectiveSignature == objectiveSignature
            && BlockedEdgesVersion == blockedEdgesVersion
            && GraphClass == graphClass
            && Team == team
            && CarryingIntel == carryingIntel;
    }

}
