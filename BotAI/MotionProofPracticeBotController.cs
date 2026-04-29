using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using System.IO.Compression;
using System.Text.Json;

namespace OpenGarrison.BotAI;

public sealed class MotionProofPracticeBotController : IPracticeBotController
{
    private const float GraphGoalRadius = 160f;
    private const float GraphCtfGoalRadius = 64f;
    private const float GraphAttachRadius = 192f;
    private const float StaticGoalQuantization = 64f;
    private const float DynamicGoalQuantization = 256f;
    private const float NoProgressDistance = 0.1f;
    private const int NoProgressTickLimit = 30;
    private const int EnemyGraphNoProgressTickLimit = 12;
    private const int EnemyGraphRescueJumpTicks = 8;
    private const int DynamicGraphGoalCandidateCount = 160;
    private const int DynamicGraphLineOfSightCandidateCount = 48;
    private const float DynamicGraphGoalDistanceCostScale = 6f;
    private const int DynamicGraphNoLineOfSightPenaltyTicks = 20_000;
    private const float ObjectiveTapeFastForwardSeconds = 0.3f;
    private const int ObjectiveNavigationRescueJumpTicks = NoProgressTickLimit - 2;
    private const float ObstacleProbeDistance = 10f;
    private const int MaxRouteMapCacheEntries = 128;
    private const int MaxGraphRouteActions = 2048;
    private const float DirectSeekHorizontalDeadZone = 14f;
    private const float DirectSeekVerticalJumpThreshold = 26f;
    private const float DirectSeekVerticalDropThreshold = 72f;
    private const float DirectSeekVerticalSearchThreshold = 64f;
    private const int DirectSeekRescueJumpTicks = 8;
    private const float DirectSeekDropProbeStep = 24f;
    private const float DirectSeekDropProbeMinDistance = 96f;
    private const float DirectSeekDropProbeMaxDistance = 420f;
    private const float DirectSeekDropProbeDepth = 56f;
    private const float DirectSeekObstacleProbeDistance = 38f;
    private const float DirectSeekEscapeProbeStep = 36f;
    private const float DirectSeekEscapeProbeMaxDistance = 720f;
    private const float DirectSeekLineOfSightSearchDistance = 720f;
    private const float DirectSeekClimbProbeMaxDistance = 128f;
    private const float DirectSeekClimbProbeStep = 16f;
    private const float DirectSeekClimbClearance = 84f;
    private const int DirectSeekDynamicRescueJumpPulseTicks = 10;
    private const int DirectSeekDynamicRescueJumpPulsePeriodTicks = 24;
    private const int DirectSeekBackupTicks = 14;
    private const int DirectSeekBackupJumpPulseTicks = 8;
    private const int DirectSeekStuckBackupTicks = 12;
    private const int DirectSeekVerticalFlipNoGoalProgressTicks = 90;
    private const int DirectSeekGraphEscalationTicks = 75;
    private const int DirectSeekGraphEscalationNoGoalProgressTicks = 45;
    private const float DirectSeekStateStaleDistance = 384f;
    private const float DirectSeekVerticalSearchHorizontalRadius = 560f;
    private const float SlotStateTeleportResetDistance = 640f;
    private const float LocalObjectiveDirectHorizontalRadius = 560f;
    private const float LocalObjectiveDirectVerticalRadius = 520f;
    private const float LocalObjectiveDirectReleaseHorizontalRadius = 1200f;
    private const float LocalObjectiveDirectReleaseVerticalRadius = 1000f;
    private const int ObjectiveGraphRecoveryTicks = 45;
    private const int ObjectiveTapeNoGoalProgressTicks = 90;
    private const float ObjectiveTerminalDirectRadius = 480f;
    private const float ObjectiveTerminalTightRadius = 96f;
    private const float CtfIntelPickupMarkerSize = 24f;
    private const float ObjectiveHoldStrafeHalfWidth = 44f;
    private const float ObjectiveCaptureStrafeHalfWidth = 18f;
    private const int ObjectiveHoldStrafePeriodTicks = 32;
    private const int ObjectiveHoldHopPeriodTicks = 54;
    private const int ObjectiveHoldHopPulseTicks = 4;
    private const float HealTargetSeekDistance = 360f;
    private const float MedicHealFireDistance = 300f;
    private const float MedicNeedleRange = 420f;
    private const float MedicUberEnemyThreatDistance = 220f;
    private const float LowHealthFraction = 0.3f;
    private const int HeavyIdleEatHealth = 100;
    private const int HeavyCombatEatHealth = 30;
    private const float MineThreatDistance = 400f;
    private const float MineDetonationRadius = 50f;
    private const float CombatSightDistance = 520f;
    private const float SniperScopeDistance = 150f;
    private const int SniperMinimumChargeTicks = 50;
    private const float SoldierOffhandDistance = 260f;
    private const float PyroAirblastDistance = 170f;
    private const float QuoteBladeMinimumDistance = 80f;
    private const float QuoteBladeMaximumDistance = 220f;
    private const string MotionProofContentRelativeDirectory = "Core/Content/MotionProof";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly Dictionary<byte, ReplayState> _statesBySlot = new();
    private readonly Dictionary<string, MotionProofArtifact?> _artifactsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PreparedMotionGraph?> _graphsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GraphRouteMap> _routeMapsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<byte, GraphReplayState> _graphStatesBySlot = new();
    private readonly Dictionary<byte, DirectSeekState> _directSeekStatesBySlot = new();
    private readonly Dictionary<byte, int> _objectiveHoldPointBySlot = new();
    private readonly Dictionary<byte, int> _localObjectiveDirectPointBySlot = new();
    private readonly Dictionary<byte, PlayerInputSnapshot> _inputs = new();
    private readonly Dictionary<byte, bool> _aliveBySlot = new();
    private readonly Random _random = new(1337);

    public bool CollectDiagnostics { get; set; }

    public BotControllerDiagnosticsSnapshot LastDiagnostics { get; private set; } = BotControllerDiagnosticsSnapshot.Empty;

    public void Reset()
    {
        _statesBySlot.Clear();
        _graphStatesBySlot.Clear();
        _directSeekStatesBySlot.Clear();
        _objectiveHoldPointBySlot.Clear();
        _localObjectiveDirectPointBySlot.Clear();
        _inputs.Clear();
        _aliveBySlot.Clear();
        _routeMapsByKey.Clear();
        LastDiagnostics = BotControllerDiagnosticsSnapshot.Empty;
    }

    public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        _inputs.Clear();
        var diagnostics = CollectDiagnostics ? new List<BotControllerDiagnosticsEntry>(controlledSlots.Count) : null;
        var allPlayers = world.EnumerateActiveNetworkPlayers()
            .Select(entry => entry.Item2)
            .ToArray();
        foreach (var entry in controlledSlots)
        {
            var slot = entry.Key;
            var controlledSlot = entry.Value;
            if (!world.TryGetNetworkPlayer(slot, out var player))
            {
                ResetSlotState(slot);
                _inputs[slot] = default;
                continue;
            }

            if (!player.IsAlive)
            {
                ResetSlotState(slot);
                _aliveBySlot[slot] = false;
                var idleInput = BuildIdleInput(player);
                _inputs[slot] = idleInput;
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, new GraphGoal("respawning", player.X, player.Bottom, GraphGoalRadius), idleInput));
                continue;
            }

            if (_aliveBySlot.TryGetValue(slot, out var wasAlive) && !wasAlive)
            {
                ResetSlotState(slot);
            }
            else if (ShouldResetSlotStateForTeleport(slot, player))
            {
                ResetSlotState(slot);
            }

            _aliveBySlot[slot] = true;
            if (TryGetDirectEnemyGoal(world, player, out var enemyGoal))
            {
                _statesBySlot.Remove(slot);
                if (ShouldEscalateEnemyChaseToGraph(slot, world, player, enemyGoal))
                {
                    var enemyGraph = GetGraph(world, controlledSlot);
                    if (enemyGraph is not null
                        && TryBuildGraphInput(slot, world, controlledSlot, enemyGraph, enemyGoal, player, out var enemyGraphInput))
                    {
                        _inputs[slot] = ApplyCombatInput(world, player, enemyGraphInput, allPlayers);
                        diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, enemyGoal, _inputs[slot]));
                        continue;
                    }
                }

                _graphStatesBySlot.Remove(slot);
                var directInput = BuildDirectSeekInput(slot, world, controlledSlot.Team, player, enemyGoal);
                _inputs[slot] = ApplyCombatInput(world, player, directInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, enemyGoal, _inputs[slot]));
                continue;
            }

            var artifact = GetArtifact(world, controlledSlot);
            var graph = GetGraph(world, controlledSlot);
            GraphGoal graphGoal = default;
            var hasGraphGoal = graph is not null && TryGetGraphGoal(world, controlledSlot.Team, player, out graphGoal);

            if (hasGraphGoal
                && TryBuildObjectiveTerminalInput(slot, world, controlledSlot.Team, player, graphGoal, out var terminalInput))
            {
                _localObjectiveDirectPointBySlot.Remove(slot);
                _graphStatesBySlot.Remove(slot);
                _inputs[slot] = ApplyCombatInput(world, player, terminalInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                continue;
            }

            if (hasGraphGoal && ShouldUseLocalObjectiveDirectSeek(slot, world, graphGoal, player))
            {
                _statesBySlot.Remove(slot);
                _graphStatesBySlot.Remove(slot);
                var localObjectiveInput = BuildDirectSeekInput(slot, world, controlledSlot.Team, player, graphGoal);
                _inputs[slot] = ApplyCombatInput(world, player, localObjectiveInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                continue;
            }

            if (hasGraphGoal && IsDynamicGraphGoal(graphGoal.Label))
            {
                _statesBySlot.Remove(slot);
                _graphStatesBySlot.Remove(slot);
                var directGoalInput = BuildDirectSeekInput(slot, world, controlledSlot.Team, player, graphGoal);
                _inputs[slot] = ApplyCombatInput(world, player, directGoalInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                continue;
            }

            if (hasGraphGoal
                && ShouldPreferGraphNavigation(world, graphGoal, artifact is not null)
                && TryBuildGraphInput(slot, world, controlledSlot, graph!, graphGoal, player, out var preferredGraphInput))
            {
                _inputs[slot] = ApplyCombatInput(world, player, preferredGraphInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                continue;
            }

            if (artifact is { } objectiveArtifact && ShouldUseObjectiveTape(world))
            {
                var tapeInput = BuildTapeInput(slot, world, controlledSlot, objectiveArtifact, player, out var tapeGoal);
                if (hasGraphGoal
                    && ShouldRecoverObjectiveTapeWithGraph(slot, world, player)
                    && TryBuildGraphInput(slot, world, controlledSlot, graph!, graphGoal, player, out var recoveryGraphInput))
                {
                    _inputs[slot] = ApplyCombatInput(world, player, recoveryGraphInput, allPlayers);
                    diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                    continue;
                }

                _inputs[slot] = ApplyCombatInput(world, player, tapeInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, tapeGoal, _inputs[slot]));
                continue;
            }

            if (hasGraphGoal
                && TryBuildGraphInput(slot, world, controlledSlot, graph!, graphGoal, player, out var graphInput))
            {
                _inputs[slot] = ApplyCombatInput(world, player, graphInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                continue;
            }

            if (hasGraphGoal && !ShouldUseObjectiveTape(world))
            {
                var directGoalInput = BuildDirectSeekInput(slot, world, controlledSlot.Team, player, graphGoal);
                _inputs[slot] = ApplyCombatInput(world, player, directGoalInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                continue;
            }

            if (artifact is null)
            {
                _inputs[slot] = BuildIdleInput(player);
                continue;
            }

            var fallbackInput = BuildTapeInput(slot, world, controlledSlot, artifact, player, out var fallbackGoal);
            _inputs[slot] = ApplyCombatInput(world, player, fallbackInput, allPlayers);
            diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, fallbackGoal, _inputs[slot]));
        }

        PruneStates(controlledSlots);
        LastDiagnostics = diagnostics is null
            ? BotControllerDiagnosticsSnapshot.Empty
            : new BotControllerDiagnosticsSnapshot(
                diagnostics,
                AliveBotCount: diagnostics.Count,
                VisibleEnemyCount: 0,
                HealFocusCount: 0,
                CabinetSeekCount: 0,
                UnstickCount: 0);
        return _inputs;
    }

    private PlayerInputSnapshot BuildTapeInput(
        byte slot,
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        MotionProofArtifact artifact,
        PlayerEntity player,
        out GraphGoal diagnosticGoal)
    {
        var phase = SelectPhase(world, player);
        var tape = phase == ReplayPhase.Return ? artifact.Return : artifact.Attack;
        diagnosticGoal = new GraphGoal($"tape_{phase}", player.X, player.Bottom, GraphGoalRadius);
        if (tape.Count == 0)
        {
            return BuildIdleInput(player);
        }

        var state = GetReplayState(slot, world, controlledSlot, artifact, phase);
        if (state.Phase != phase)
        {
            state = state with
            {
                Phase = phase,
                ActionIndex = 0,
                ActionTick = 0,
                LastX = player.X,
                LastBottom = player.Bottom,
                NoProgressTicks = 0,
                BestGoalDistance = float.NaN,
                NoGoalProgressTicks = 0,
            };
        }

        if (state.ActionIndex >= tape.Count)
        {
            if (HasCompletedTapePhase(world, controlledSlot.Team, player, phase))
            {
                _statesBySlot[slot] = state;
                if (TryBuildObjectiveHoldInput(world, slot, controlledSlot.Team, player, out var holdInput))
                {
                    return holdInput;
                }

                return BuildIdleInput(player);
            }

            _statesBySlot[slot] = state;
            if (TryGetGraphGoal(world, controlledSlot.Team, player, out var recoveryGoal))
            {
                return BuildDirectSeekInput(slot, world, controlledSlot.Team, player, recoveryGoal);
            }

            state = state with { ActionIndex = 0, ActionTick = 0 };
        }

        if (HasCompletedTapePhase(world, controlledSlot.Team, player, phase))
        {
            _statesBySlot[slot] = state;
            if (TryBuildObjectiveHoldInput(world, slot, controlledSlot.Team, player, out var holdInput))
            {
                return holdInput;
            }

            return BuildIdleInput(player);
        }

        var hasObjectiveGoal = TryGetGraphGoal(world, controlledSlot.Team, player, out var objectiveGoal);
        var objectiveDistance = hasObjectiveGoal
            ? Distance(player.X, player.Bottom, objectiveGoal.X, objectiveGoal.Bottom)
            : float.NaN;
        state = TrackReplayProgress(state, player, objectiveDistance);
        if (state.NoProgressTicks >= GetObjectiveTapeFastForwardTicks(world)
            && CanFastForwardInertTapeAction(tape, state.ActionIndex))
        {
            state = state with
            {
                ActionIndex = FindNextActiveTapeActionIndex(tape, state.ActionIndex),
                ActionTick = 0,
                LastX = player.X,
                LastBottom = player.Bottom,
                NoProgressTicks = 0,
                BestGoalDistance = objectiveDistance,
                NoGoalProgressTicks = 0,
            };
        }

        var action = tape[state.ActionIndex];
        if (hasObjectiveGoal
            && state.NoGoalProgressTicks >= ObjectiveTapeNoGoalProgressTicks
            && TryFindBetterObjectiveTapeAction(tape, state.ActionIndex, objectiveGoal.X - player.X, out var betterActionIndex))
        {
            state = state with
            {
                ActionIndex = betterActionIndex,
                ActionTick = 0,
                LastX = player.X,
                LastBottom = player.Bottom,
                BestGoalDistance = objectiveDistance,
                NoGoalProgressTicks = 0,
            };
            action = tape[state.ActionIndex];
        }

        if (IsGroundedIdleAction(action, player))
        {
            state = state with
            {
                ActionIndex = FindNextActiveTapeActionIndex(tape, state.ActionIndex),
                ActionTick = 0,
                LastX = player.X,
                LastBottom = player.Bottom,
                NoProgressTicks = 0,
                BestGoalDistance = objectiveDistance,
                NoGoalProgressTicks = 0,
            };
            if (state.ActionIndex < tape.Count)
            {
                action = tape[state.ActionIndex];
            }
        }

        var input = BuildActionInput(action, state.ActionTick, player);
        input = ApplyNavigationRescueInput(
            world,
            controlledSlot.Team,
            player,
            action,
            input,
            state.NoProgressTicks,
            allowObstacleJump: false,
            rescueJumpTicks: ObjectiveNavigationRescueJumpTicks);
        _statesBySlot[slot] = AdvanceReplayState(state, tape) with { LastX = player.X, LastBottom = player.Bottom };
        return input;
    }

    private static bool HasCompletedTapePhase(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        ReplayPhase phase)
    {
        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            return phase == ReplayPhase.Attack
                ? player.IsCarryingIntel
                : !player.IsCarryingIntel;
        }

        if (world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
            && TryGetGraphGoal(world, team, player, out _)
            && SelectKothTargetPoint(world, team) is { } point)
        {
            return world.IsPlayerInControlPointCaptureZone(player, point.Index)
                && !player.IsSpyCloaked;
        }

        return true;
    }

    private bool TryBuildObjectiveHoldInput(
        SimulationWorld world,
        byte slot,
        PlayerTeam team,
        PlayerEntity player,
        out PlayerInputSnapshot input)
    {
        input = default;
        if (world.MatchRules.Mode is not (GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
            || SelectKothTargetPoint(world, team) is not { } point)
        {
            _objectiveHoldPointBySlot.Remove(slot);
            return false;
        }

        var inCaptureZone = world.IsPlayerInControlPointCaptureZone(player, point.Index)
            && !player.IsSpyCloaked;
        var enemyCappers = team == PlayerTeam.Blue ? point.RedCappers : point.BlueCappers;
        var contested = enemyCappers > 0
            || point.CappingTeam.HasValue && point.CappingTeam.Value != team && point.CappingTicks > 0f;
        var ownedAndStable = point.Team == team && !contested;
        var activelyCapturing = point.CappingTeam == team && point.CappingTicks > 0f;
        var latched = _objectiveHoldPointBySlot.TryGetValue(slot, out var heldPointIndex)
            && heldPointIndex == point.Index;

        if (ownedAndStable)
        {
            _objectiveHoldPointBySlot.Remove(slot);
            if (!inCaptureZone)
            {
                return false;
            }
        }
        else if (inCaptureZone || activelyCapturing || latched)
        {
            _objectiveHoldPointBySlot[slot] = point.Index;
        }
        else
        {
            return false;
        }

        if (!inCaptureZone && !ownedAndStable)
        {
            var goal = new GraphGoal($"control_point_{point.Index}", point.Marker.CenterX, point.Marker.CenterY, GraphGoalRadius);
            input = BuildDirectSeekInput(slot, world, team, player, goal);
            return true;
        }

        var centerX = point.Marker.CenterX;
        var halfWidth = ownedAndStable ? ObjectiveHoldStrafeHalfWidth : ObjectiveCaptureStrafeHalfWidth;
        var leftBound = centerX - halfWidth;
        var rightBound = centerX + halfWidth;
        var phase = (int)((world.Frame + slot * 11) / ObjectiveHoldStrafePeriodTicks);
        var direction = phase % 2 == 0 ? -1 : 1;
        if (player.X <= leftBound)
        {
            direction = 1;
        }
        else if (player.X >= rightBound)
        {
            direction = -1;
        }

        if (WouldMoveIntoObstacle(world, team, player, direction, probeDistance: 16f))
        {
            var reversed = -direction;
            direction = WouldMoveIntoObstacle(world, team, player, reversed, probeDistance: 16f) ? 0 : reversed;
        }

        var hopFrame = (int)((world.Frame + slot * 7) % ObjectiveHoldHopPeriodTicks);
        var shouldHop = player.IsGrounded && hopFrame < ObjectiveHoldHopPulseTicks;
        input = new PlayerInputSnapshot(
            Left: direction < 0,
            Right: direction > 0,
            Up: shouldHop,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + (direction == 0 ? player.FacingDirectionX : direction) * 160f,
            AimWorldY: player.Y,
            DebugKill: false);
        return true;
    }

    private BotControllerDiagnosticsEntry CreateDiagnostics(
        byte slot,
        ControlledBotSlot controlledSlot,
        PlayerEntity player,
        GraphGoal goal,
        PlayerInputSnapshot input)
    {
        var hasGraphState = _graphStatesBySlot.TryGetValue(slot, out var graphState);
        var hasDirectState = _directSeekStatesBySlot.TryGetValue(slot, out var directState)
            && directState.GoalKey.Equals(BuildDirectSeekGoalKey(goal), StringComparison.OrdinalIgnoreCase);
        var actionCount = graphState.Actions?.Count ?? 0;
        var actionTicks = graphState.Actions is not null && graphState.ActionIndex < actionCount
            ? graphState.Actions[graphState.ActionIndex].Ticks
            : -1;
        var routeLabel = hasDirectState
            ? $"motion_proof:{goal.Label}:direct:{directState.NoProgressTicks}:{directState.BackupTicksRemaining}"
            : actionCount > 0
            ? $"motion_proof:{goal.Label}:s{graphState.StartNodeIndex}:g{graphState.GoalNodeIndex}:{graphState.ActionIndex}/{actionCount}:{graphState.ActionTick}"
            : $"motion_proof:{goal.Label}:idle";
        return new BotControllerDiagnosticsEntry(
            slot,
            player.DisplayName,
            controlledSlot.Team,
            controlledSlot.ClassId,
            BotRole.AttackObjective,
            BotStateKind.TravelObjective,
            BotFocusKind.Objective,
            goal.Label,
            routeLabel,
            HasVisibleEnemy: false,
            player.Health,
            player.MaxHealth,
            StuckTicks: 0,
            ModernStuckTicks: 0,
            UnstickTicks: 0,
            CurrentPointId: graphState.ActionIndex,
            NextPointId: actionTicks,
            NextPoint2Id: actionCount,
            MovementTargetX: hasGraphState && graphState.GoalNodeIndex >= 0 ? graphState.GoalNodeX : goal.X,
            MovementTargetY: hasGraphState && graphState.GoalNodeIndex >= 0 ? graphState.GoalNodeBottom : goal.Bottom,
            RequestedHorizontal: input.Right ? 1 : input.Left ? -1 : 0,
            MoveDebug: input.Left ? "left" : input.Right ? "right" : "none",
            RequestedJump: input.Up,
            JumpDebug: input.Up ? "jump" : "none",
            RouteGoalNodeId: -1,
            RouteGoalX: goal.X,
            RouteGoalY: goal.Bottom,
            PreviousCurrentPointId: -1,
            PreviousNextPointId: -1,
            player.IsGrounded,
            ProbeGrounded: player.IsGrounded,
            SecondAnchorBlockPointId: -1,
            SecondAnchorBlockTicksRemaining: 0,
            NoNextPointTicks: 0,
            FallbackRouteLabel: string.Empty,
            FallbackTriggerLabel: string.Empty,
            NavigationIssueLabel: string.Empty,
            BranchFromPointId: -1,
            BranchToPointId: -1,
            BranchTicks: 0,
            BranchNoProgressTicks: 0,
            DirectTargetTicks: hasDirectState ? directState.TotalTicks : 0,
            DirectTargetNoProgressTicks: hasDirectState ? directState.NoProgressTicks : 0);
    }

    private MotionProofArtifact? GetArtifact(SimulationWorld world, ControlledBotSlot controlledSlot)
    {
        var key = $"{world.Level.Name}.a{world.Level.MapAreaIndex}.{controlledSlot.Team}.{controlledSlot.ClassId}";
        if (_artifactsByKey.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var root = ResolveMotionProofContentRoot();

        var path = Path.Combine(root, $"{world.Level.Name}.{controlledSlot.Team}.{controlledSlot.ClassId}.json");
        if (!File.Exists(path))
        {
            _artifactsByKey[key] = null;
            return null;
        }

        var artifact = ReadJsonArtifact<MotionProofArtifact>(path);
        _artifactsByKey[key] = artifact;
        return artifact;
    }

    private static string ResolveMotionProofContentRoot()
    {
        var root = Environment.GetEnvironmentVariable("OG_MOTION_PROOF_DIR");
        if (!string.IsNullOrWhiteSpace(root))
        {
            return root;
        }

        root = Path.Combine(ContentRoot.Path, "MotionProof");
        if (OperatingSystem.IsBrowser() || Directory.Exists(root))
        {
            return root;
        }

        return ProjectSourceLocator.FindDirectory(MotionProofContentRelativeDirectory) ?? root;
    }

    private static bool ShouldUseObjectiveTape(SimulationWorld world)
    {
        return world.MatchRules.Mode is GameModeKind.CaptureTheFlag or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
            && !HasExplicitGraphGoalOverride();
    }

    private bool ShouldRecoverObjectiveTapeWithGraph(byte slot, SimulationWorld world, PlayerEntity player)
    {
        if (!_statesBySlot.TryGetValue(slot, out var state))
        {
            return false;
        }

        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            return state.NoProgressTicks >= ObjectiveGraphRecoveryTicks;
        }

        return state.NoProgressTicks >= ObjectiveGraphRecoveryTicks
            || state.NoGoalProgressTicks >= ObjectiveGraphRecoveryTicks;
    }

    private static bool ShouldPreferGraphNavigation(SimulationWorld world, GraphGoal goal, bool hasObjectiveArtifact)
    {
        if (IsDynamicGraphGoal(goal.Label))
        {
            return true;
        }

        if (hasObjectiveArtifact && ShouldUseObjectiveTape(world))
        {
            return false;
        }

        return !IsEnabled(Environment.GetEnvironmentVariable("OG_MOTION_PROOF_FORCE_OBJECTIVE_TAPES"))
            && (world.MatchRules.Mode is GameModeKind.CaptureTheFlag or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill);
    }

    private bool ShouldUseLocalObjectiveDirectSeek(byte slot, SimulationWorld world, GraphGoal goal, PlayerEntity player)
    {
        if (world.MatchRules.Mode is not (GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
            || !goal.Label.StartsWith("control_point_", StringComparison.OrdinalIgnoreCase))
        {
            _localObjectiveDirectPointBySlot.Remove(slot);
            return false;
        }

        var targetPoint = SelectKothTargetPoint(world, player.Team);
        if (targetPoint is null
            || player.IsSpyCloaked
            || world.IsPlayerInControlPointCaptureZone(player, targetPoint.Index))
        {
            _localObjectiveDirectPointBySlot.Remove(slot);
            return false;
        }

        var deltaX = MathF.Abs(goal.X - player.X);
        var deltaBottom = MathF.Abs(goal.Bottom - player.Bottom);
        var isLatched = _localObjectiveDirectPointBySlot.TryGetValue(slot, out var latchedPointIndex)
            && latchedPointIndex == targetPoint.Index;
        if (isLatched)
        {
            if (deltaX <= LocalObjectiveDirectReleaseHorizontalRadius
                && deltaBottom <= LocalObjectiveDirectReleaseVerticalRadius)
            {
                return true;
            }

            _localObjectiveDirectPointBySlot.Remove(slot);
            return false;
        }

        if (deltaX <= LocalObjectiveDirectHorizontalRadius
            && deltaBottom <= LocalObjectiveDirectVerticalRadius)
        {
            _localObjectiveDirectPointBySlot[slot] = targetPoint.Index;
            return true;
        }

        return false;
    }

    private static bool HasExplicitGraphGoalOverride()
    {
        var envX = Environment.GetEnvironmentVariable("OG_MOTION_PROOF_GOAL_X");
        var envY = Environment.GetEnvironmentVariable("OG_MOTION_PROOF_GOAL_Y");
        return float.TryParse(envX, out _) && float.TryParse(envY, out _);
    }

    private PreparedMotionGraph? GetGraph(SimulationWorld world, ControlledBotSlot controlledSlot)
    {
        var movementProfile = GetMovementProfileId(controlledSlot.ClassId);
        var key = $"{world.Level.Name}.a{world.Level.MapAreaIndex}.{movementProfile}";
        if (_graphsByKey.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var root = ResolveMotionProofContentRoot();

        var candidatePaths = new[]
        {
            Path.Combine(root, $"{world.Level.Name}.{movementProfile}.graph.json.gz"),
            Path.Combine(root, $"{world.Level.Name}.{movementProfile}.graph.json"),
            Path.Combine(root, $"{world.Level.Name}.{controlledSlot.Team}.{controlledSlot.ClassId}.graph.json.gz"),
            Path.Combine(root, $"{world.Level.Name}.{controlledSlot.Team}.{controlledSlot.ClassId}.graph.json"),
        };
        var path = candidatePaths.FirstOrDefault(File.Exists);
        if (!File.Exists(path))
        {
            _graphsByKey[key] = null;
            return null;
        }

        var artifact = ReadJsonArtifact<MotionGraphArtifact>(path);
        var graph = artifact is null ? null : PreparedMotionGraph.From(artifact, Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path)));
        _graphsByKey[key] = graph;
        return graph;
    }

    private bool TryBuildGraphInput(
        byte slot,
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PreparedMotionGraph graph,
        GraphGoal goal,
        PlayerEntity player,
        out PlayerInputSnapshot input)
    {
        input = default;
        var mapKey = $"{world.Level.Name}.a{world.Level.MapAreaIndex}.{graph.Identity}.{controlledSlot.Team}.{graph.Version}";
        var goalKey = BuildGraphGoalKey(goal);
        var hasExistingState = _graphStatesBySlot.TryGetValue(slot, out var state);
        var shouldFinishPickupTape = hasExistingState
            && state.MapKey == mapKey
            && state.ActionIndex < state.Actions.Count
            && player.IsCarryingIntel
            && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
            && state.GoalKey.StartsWith("enemy_intel:", StringComparison.OrdinalIgnoreCase);
        if (!hasExistingState
            || state.MapKey != mapKey
            || (!shouldFinishPickupTape && state.GoalKey != goalKey)
            || state.ActionIndex >= state.Actions.Count)
        {
            if (!TryBuildGraphPlan(
                    graph,
                    controlledSlot.Team,
                    player.IsCarryingIntel,
                    player.X,
                    player.Bottom,
                    player.IsGrounded,
                    player.HorizontalSpeed,
                    player.VerticalSpeed,
                    player.Height,
                    world,
                    goal.X,
                    goal.Bottom,
                    goal.Radius,
                    goal.Label,
                    out var plan))
            {
                _graphStatesBySlot.Remove(slot);
                return false;
            }

            state = new GraphReplayState(
                mapKey,
                goalKey,
                plan.Actions,
                ActionIndex: 0,
                ActionTick: 0,
                plan.StartNodeIndex,
                plan.GoalNodeIndex,
                plan.GoalNodeX,
                plan.GoalNodeBottom,
                plan.PathNodeIndices,
                LastX: player.X,
                LastBottom: player.Bottom,
                NoProgressTicks: 0);
        }
        else
        {
            state = TrackGraphProgress(state, player);
            if (state.NoProgressTicks >= GetGraphNoProgressTickLimit(goal.Label))
            {
                _graphStatesBySlot.Remove(slot);
                return false;
            }
        }

        if (state.ActionIndex >= state.Actions.Count)
        {
            _graphStatesBySlot[slot] = state;
            return false;
        }

        if (IsDynamicGraphGoal(goal.Label)
            && TryBuildDynamicGraphWaypointInput(slot, world, controlledSlot.Team, graph, state, player, goal, out input, out state))
        {
            _graphStatesBySlot[slot] = state with { LastX = player.X, LastBottom = player.Bottom };
            return true;
        }

        var action = state.Actions[state.ActionIndex];
        if (IsGroundedIdleAction(action, player))
        {
            state = AdvanceGraphReplayState(state) with { LastX = player.X, LastBottom = player.Bottom, NoProgressTicks = 0 };
            if (state.ActionIndex < state.Actions.Count)
            {
                action = state.Actions[state.ActionIndex];
            }
        }

        input = BuildActionInput(action, state.ActionTick, player);
        input = ApplyNavigationRescueInput(
            world,
            controlledSlot.Team,
            player,
            action,
            input,
            state.NoProgressTicks,
            allowObstacleJump: true,
            rescueJumpTicks: GetGraphRescueJumpTicks(goal.Label));
        _graphStatesBySlot[slot] = AdvanceGraphReplayState(state) with { LastX = player.X, LastBottom = player.Bottom };
        return true;
    }

    private static bool TryGetGraphGoal(SimulationWorld world, PlayerTeam team, PlayerEntity player, out GraphGoal goal)
    {
        var envX = Environment.GetEnvironmentVariable("OG_MOTION_PROOF_GOAL_X");
        var envY = Environment.GetEnvironmentVariable("OG_MOTION_PROOF_GOAL_Y");
        if (float.TryParse(envX, out var overrideX) && float.TryParse(envY, out var overrideY))
        {
            goal = new GraphGoal("override", overrideX, overrideY, GraphGoalRadius);
            return true;
        }

        if (ShouldPrioritizeEnemyGoals(world)
            && TrySelectEnemyGoal(world, player, out goal))
        {
            return true;
        }

        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            return TryGetCtfGraphGoal(world, team, player, out goal);
        }

        if (world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
        {
            return TryGetKothGraphGoal(world, team, player, out goal);
        }

        goal = default;
        return false;
    }

    private static bool TryGetKothGraphGoal(SimulationWorld world, PlayerTeam team, PlayerEntity player, out GraphGoal goal)
    {
        var point = SelectKothTargetPoint(world, team);
        if (point is null)
        {
            goal = default;
            return false;
        }

        var enemyCappers = team == PlayerTeam.Blue ? point.RedCappers : point.BlueCappers;
        var contested = enemyCappers > 0
            || point.CappingTeam.HasValue && point.CappingTeam.Value != team && point.CappingTicks > 0f;
        if (point.Team == team && !contested && TrySelectEnemyGoal(world, player, out goal))
        {
            return true;
        }

        goal = new GraphGoal($"control_point_{point.Index}", point.Marker.CenterX, point.Marker.CenterY, GraphGoalRadius);
        return true;
    }

    private static bool TryGetCtfGraphGoal(SimulationWorld world, PlayerTeam team, PlayerEntity player, out GraphGoal goal)
    {
        var ownIntel = GetIntelState(world, team);
        var enemyTeam = GetOpposingTeam(team);
        var enemyIntel = GetIntelState(world, enemyTeam);
        if (player.IsCarryingIntel)
        {
            goal = new GraphGoal("own_intel", ownIntel.HomeX, ownIntel.HomeY, GraphCtfGoalRadius);
            return true;
        }

        if (TryFindIntelCarrier(world, enemyTeam, out var enemyCarrier))
        {
            goal = new GraphGoal("enemy_carrier", enemyCarrier.X, enemyCarrier.Bottom, GraphGoalRadius);
            return true;
        }

        if (TryFindIntelCarrier(world, team, out var allyCarrier))
        {
            goal = new GraphGoal("ally_carrier", allyCarrier.X, allyCarrier.Bottom, GraphGoalRadius);
            return true;
        }

        if (enemyIntel.IsDropped)
        {
            goal = new GraphGoal("dropped_enemy_intel", enemyIntel.X, enemyIntel.Y, GraphCtfGoalRadius);
            return true;
        }

        if (ownIntel.IsDropped)
        {
            goal = new GraphGoal("dropped_own_intel", ownIntel.X, ownIntel.Y, GraphCtfGoalRadius);
            return true;
        }

        goal = new GraphGoal("enemy_intel", enemyIntel.HomeX, enemyIntel.HomeY, GraphCtfGoalRadius);
        return true;
    }

    private bool TryBuildObjectiveTerminalInput(
        byte slot,
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        GraphGoal goal,
        out PlayerInputSnapshot input)
    {
        input = default;
        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            if (IsCtfTerminalGoal(goal.Label)
                && player.IntersectsMarker(goal.X, goal.Bottom, CtfIntelPickupMarkerSize, CtfIntelPickupMarkerSize))
            {
                input = BuildCtfTerminalInput(slot, world, team, player, goal);
                return true;
            }

            return false;
        }

        if (world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
            && goal.Label.StartsWith("control_point_", StringComparison.OrdinalIgnoreCase)
            && SelectKothTargetPoint(world, team) is { } point)
        {
            if (world.IsPlayerInControlPointCaptureZone(player, point.Index))
            {
                return TryBuildObjectiveHoldInput(world, slot, team, player, out input);
            }
        }

        return false;
    }

    private PlayerInputSnapshot BuildCtfTerminalInput(
        byte slot,
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        GraphGoal goal)
    {
        if (player.IntersectsMarker(goal.X, goal.Bottom, CtfIntelPickupMarkerSize, CtfIntelPickupMarkerSize))
        {
            return BuildIntelMarkerHoldInput(world, slot, team, player, goal);
        }

        var terminalGoal = new GraphGoal($"terminal_{goal.Label}", goal.X, goal.Bottom, ObjectiveTerminalTightRadius);
        var input = BuildDirectSeekInput(slot, world, team, player, terminalGoal);
        if (MathF.Abs(goal.X - player.X) <= DirectSeekHorizontalDeadZone
            && !player.IntersectsMarker(goal.X, goal.Bottom, CtfIntelPickupMarkerSize, CtfIntelPickupMarkerSize))
        {
            return BuildIntelMarkerHoldInput(world, slot, team, player, goal) with
            {
                Down = goal.Bottom > player.Bottom + DirectSeekVerticalDropThreshold,
            };
        }

        return input;
    }

    private static PlayerInputSnapshot BuildIntelMarkerHoldInput(
        SimulationWorld world,
        byte slot,
        PlayerTeam team,
        PlayerEntity player,
        GraphGoal goal)
    {
        var direction = ((world.Frame + slot * 13) / ObjectiveHoldStrafePeriodTicks) % 2 == 0 ? -1 : 1;
        if (WouldMoveIntoObstacle(world, team, player, direction, probeDistance: 16f))
        {
            var reversed = -direction;
            direction = WouldMoveIntoObstacle(world, team, player, reversed, probeDistance: 16f) ? 0 : reversed;
        }

        var hopFrame = (int)((world.Frame + slot * 5) % ObjectiveHoldHopPeriodTicks);
        return new PlayerInputSnapshot(
            Left: direction < 0,
            Right: direction > 0,
            Up: player.IsGrounded && hopFrame < ObjectiveHoldHopPulseTicks,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: goal.X,
            AimWorldY: goal.Bottom,
            DebugKill: false);
    }

    private static bool IsCtfTerminalGoal(string label)
    {
        return label.Equals("enemy_intel", StringComparison.OrdinalIgnoreCase)
            || label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
            || label.Equals("dropped_enemy_intel", StringComparison.OrdinalIgnoreCase)
            || label.Equals("dropped_own_intel", StringComparison.OrdinalIgnoreCase);
    }

    private static TeamIntelligenceState GetIntelState(SimulationWorld world, PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? world.BlueIntel : world.RedIntel;
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }

    private static bool TryFindIntelCarrier(SimulationWorld world, PlayerTeam team, out PlayerEntity carrier)
    {
        foreach (var (_, candidate) in world.EnumerateActiveNetworkPlayers())
        {
            if (candidate.Team == team
                && candidate.IsAlive
                && candidate.IsCarryingIntel)
            {
                carrier = candidate;
                return true;
            }
        }

        carrier = null!;
        return false;
    }

    private static bool TryGetDirectEnemyGoal(SimulationWorld world, PlayerEntity player, out GraphGoal goal)
    {
        if (!ShouldPrioritizeEnemyGoals(world))
        {
            goal = default;
            return false;
        }

        return TrySelectEnemyGoal(world, player, out goal);
    }

    private PlayerInputSnapshot BuildDirectSeekInput(
        byte slot,
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        GraphGoal goal)
    {
        var goalKey = BuildDirectSeekGoalKey(goal);
        var currentGoalDistance = Distance(player.X, player.Bottom, goal.X, goal.Bottom);
        var hasState = _directSeekStatesBySlot.TryGetValue(slot, out var state)
            && state.GoalKey.Equals(goalKey, StringComparison.OrdinalIgnoreCase);
        if (!hasState)
        {
            state = new DirectSeekState(goalKey, player.X, player.Bottom, currentGoalDistance, NoProgressTicks: 0, NoHorizontalProgressTicks: 0, NoGoalProgressTicks: 0, TotalTicks: 0, BackupTicksRemaining: 0, BackupDirection: 0, VerticalSearchDirection: 0);
        }
        else if (ShouldResetDirectSeekState(state, currentGoalDistance))
        {
            state = new DirectSeekState(goalKey, player.X, player.Bottom, currentGoalDistance, NoProgressTicks: 0, NoHorizontalProgressTicks: 0, NoGoalProgressTicks: 0, TotalTicks: 0, BackupTicksRemaining: 0, BackupDirection: 0, VerticalSearchDirection: 0);
        }
        else
        {
            state = TrackDirectSeekProgress(state, player, currentGoalDistance);
        }

        var deltaX = goal.X - player.X;
        var deltaBottom = goal.Bottom - player.Bottom;
        var targetAbove = deltaBottom < -DirectSeekVerticalJumpThreshold;
        var targetBelow = deltaBottom > DirectSeekVerticalDropThreshold;
        var goalCenterY = goal.Bottom - (player.Height * 0.5f);
        var isDynamicEnemyGoal = IsDynamicGraphGoal(goal.Label);
        var hasGoalLineOfSight = !isDynamicEnemyGoal
            || HasLineOfSight(world, player.X, player.Y, goal.X, goalCenterY, team, player.IsCarryingIntel);
        var targetDirection = MathF.Abs(deltaX) > DirectSeekHorizontalDeadZone
            ? Math.Sign(deltaX)
            : 0;
        var verticalSearchDirection = state.VerticalSearchDirection;
        var shouldUseWideVerticalSearch = targetAbove
            && (goal.Label.StartsWith("control_point_", StringComparison.OrdinalIgnoreCase)
                || isDynamicEnemyGoal)
            && MathF.Abs(deltaX) <= DirectSeekVerticalSearchHorizontalRadius;
        var shouldSearchForLineOfSight = isDynamicEnemyGoal
            && !hasGoalLineOfSight
            && (currentGoalDistance <= DirectSeekLineOfSightSearchDistance
                || state.NoGoalProgressTicks >= DirectSeekRescueJumpTicks);
        var shouldSearchVertically = (MathF.Abs(deltaBottom) >= DirectSeekVerticalSearchThreshold
                && (MathF.Abs(deltaX) <= GraphAttachRadius || shouldUseWideVerticalSearch || verticalSearchDirection != 0))
            || shouldSearchForLineOfSight;
        if (shouldSearchVertically)
        {
            if (verticalSearchDirection == 0
                || (shouldSearchForLineOfSight
                    && state.NoHorizontalProgressTicks >= DirectSeekStuckBackupTicks * 2
                    && state.NoGoalProgressTicks >= DirectSeekVerticalFlipNoGoalProgressTicks))
            {
                verticalSearchDirection = SelectVerticalSearchDirection(world, team, player, goal, state.VerticalSearchDirection);
            }

            targetDirection = verticalSearchDirection;
        }
        else
        {
            verticalSearchDirection = 0;
        }

        var direction = targetDirection;
        var backupEscape = false;
        var horizontalStall = direction != 0
            && player.IsGrounded
            && state.NoHorizontalProgressTicks >= DirectSeekRescueJumpTicks;
        if (state.BackupTicksRemaining > 0 && state.BackupDirection != 0)
        {
            direction = state.BackupDirection;
            backupEscape = true;
            state = state with { BackupTicksRemaining = state.BackupTicksRemaining - 1 };
        }
        else if (targetDirection != 0
            && ShouldReverseDirectSeek(state, shouldSearchVertically, targetAbove)
            && (WouldMoveIntoObstacle(world, team, player, targetDirection)
                || MathF.Abs(deltaBottom) >= DirectSeekVerticalSearchThreshold
                || state.NoHorizontalProgressTicks >= DirectSeekStuckBackupTicks * 2))
        {
            direction = -targetDirection;
            backupEscape = true;
            if (shouldSearchVertically)
            {
                verticalSearchDirection = direction;
            }

            state = state with
            {
                BackupTicksRemaining = DirectSeekBackupTicks,
                BackupDirection = direction,
                NoProgressTicks = 0,
            };
        }

        var obstacleAhead = direction != 0 && WouldMoveIntoObstacle(world, team, player, direction, DirectSeekObstacleProbeDistance);
        if (targetBelow
            && shouldSearchVertically
            && player.IsGrounded
            && TryFindNearestDropDirection(world, team, player, out var dropDirection))
        {
            direction = dropDirection;
            obstacleAhead = WouldMoveIntoObstacle(world, team, player, direction);
            verticalSearchDirection = direction;
        }

        var verticalClimbJump = targetAbove
            && direction != 0
            && ShouldJumpForVerticalChase(
                world,
                team,
                player,
                direction,
                state.NoProgressTicks,
                state.NoHorizontalProgressTicks,
                state.NoGoalProgressTicks,
                state.TotalTicks);
        var dynamicRescueJumpPulse = isDynamicEnemyGoal
            && !targetBelow
            && direction != 0
            && state.NoGoalProgressTicks >= DirectSeekRescueJumpTicks
            && ((state.NoGoalProgressTicks - DirectSeekRescueJumpTicks) % DirectSeekDynamicRescueJumpPulsePeriodTicks) < DirectSeekDynamicRescueJumpPulseTicks
            && (!targetAbove || hasGoalLineOfSight || obstacleAhead);
        var backupEscapeJump = backupEscape
            && !targetBelow
            && direction != 0
            && state.BackupTicksRemaining >= DirectSeekBackupTicks - DirectSeekBackupJumpPulseTicks;
        var groundJump = (!targetBelow && obstacleAhead)
            || verticalClimbJump
            || (targetAbove && hasGoalLineOfSight && MathF.Abs(deltaX) <= 260f)
            || (targetBelow && shouldSearchVertically && obstacleAhead)
            || dynamicRescueJumpPulse
            || backupEscapeJump
            || (horizontalStall && !targetBelow)
            || (!targetBelow && state.NoProgressTicks >= DirectSeekRescueJumpTicks && targetDirection != 0);
        var airRescueJump = !targetBelow
            && !player.IsGrounded
            && direction != 0
            && state.NoProgressTicks >= DirectSeekRescueJumpTicks;
        var shouldJump = (player.IsGrounded && groundJump) || dynamicRescueJumpPulse || airRescueJump;
        var shouldDrop = !shouldJump
            && targetBelow
            && (MathF.Abs(deltaX) <= 96f || shouldSearchVertically);

        var aimDirection = targetDirection != 0 ? targetDirection : player.FacingDirectionX;
        var input = new PlayerInputSnapshot(
            Left: direction < 0,
            Right: direction > 0,
            Up: shouldJump,
            Down: shouldDrop,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: goal.X + (aimDirection * 24f),
            AimWorldY: goalCenterY,
            DebugKill: false);

        _directSeekStatesBySlot[slot] = state with
        {
            LastX = player.X,
            LastBottom = player.Bottom,
            BestGoalDistance = state.BestGoalDistance,
            TotalTicks = state.TotalTicks + 1,
            VerticalSearchDirection = verticalSearchDirection,
        };
        return input;
    }

    private static bool ShouldEscalateEnemyChaseToGraph(byte slot, SimulationWorld world, PlayerEntity player, GraphGoal goal)
    {
        // Enemy chasing needs continuous retargeting. The baked graph is still useful for static goals, but
        // its momentum-specific edges are too brittle for live moving targets, so dynamic combat stays on
        // the adaptive direct seeker.
        return false;
    }

    private static bool ShouldResetDirectSeekState(DirectSeekState state, float currentGoalDistance)
    {
        return state.NoGoalProgressTicks >= DirectSeekRescueJumpTicks
            && currentGoalDistance > state.BestGoalDistance + DirectSeekStateStaleDistance;
    }

    private static bool ShouldReverseDirectSeek(DirectSeekState state, bool shouldSearchVertically, bool targetAbove)
    {
        if (state.NoProgressTicks >= DirectSeekStuckBackupTicks)
        {
            return true;
        }

        if (!shouldSearchVertically || state.NoHorizontalProgressTicks < DirectSeekStuckBackupTicks)
        {
            return false;
        }

        return !targetAbove || state.NoGoalProgressTicks >= DirectSeekVerticalFlipNoGoalProgressTicks;
    }

    private static int SelectVerticalSearchDirection(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        GraphGoal goal,
        int fallbackDirection)
    {
        var leftScore = ScoreVerticalSearchDirection(world, team, player, goal, -1, fallbackDirection);
        var rightScore = ScoreVerticalSearchDirection(world, team, player, goal, 1, fallbackDirection);
        if (MathF.Abs(leftScore - rightScore) <= 0.01f)
        {
            if (fallbackDirection != 0)
            {
                return Math.Sign(fallbackDirection);
            }

            return player.FacingDirectionX >= 0f ? 1 : -1;
        }

        return rightScore > leftScore ? 1 : -1;
    }

    private static float ScoreVerticalSearchDirection(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        GraphGoal goal,
        int direction,
        int fallbackDirection)
    {
        var clearDistance = 0f;
        var bestScore = fallbackDirection == direction ? 24f : 0f;
        var foundJumpableAccess = false;
        var targetBelow = goal.Bottom > player.Bottom + DirectSeekVerticalDropThreshold;
        var targetAbove = goal.Bottom < player.Bottom - DirectSeekVerticalJumpThreshold;
        var dynamicEnemyGoal = IsDynamicGraphGoal(goal.Label);
        var goalCenterY = goal.Bottom - (player.Height * 0.5f);
        for (var distance = DirectSeekEscapeProbeStep; distance <= DirectSeekEscapeProbeMaxDistance; distance += DirectSeekEscapeProbeStep)
        {
            var offsetX = direction * distance;
            if (!IsHorizontallyClearProbe(world, team, player, offsetX))
            {
                if (targetAbove
                    && IsJumpableVerticalAccessProbe(world, team, player, direction, distance))
                {
                    foundJumpableAccess = true;
                    bestScore = MathF.Max(bestScore, 12_000f - distance);
                }

                break;
            }

            clearDistance = distance;
            var probeX = player.X + offsetX;
            var distanceFromProbe = Distance(probeX, player.Bottom, goal.X, goal.Bottom);
            var horizontalDistanceFromProbe = MathF.Abs(goal.X - probeX);
            var score = dynamicEnemyGoal
                ? -distanceFromProbe + (distance * 0.1f)
                : -horizontalDistanceFromProbe - (distanceFromProbe * 0.25f) + (distance * 0.05f);
            if (HasLineOfSight(world, probeX, player.Y, goal.X, goalCenterY, team, player.IsCarryingIntel))
            {
                score += 20_000f - distanceFromProbe;
            }

            if (targetBelow && IsViableDropProbe(world, team, player, offsetX))
            {
                score += 8_000f - distance;
            }

            if (targetBelow)
            {
                score += MathF.Max(0f, EstimateDropClearance(world, team, player, offsetX));
            }
            else if (targetAbove && WouldMoveIntoObstacle(world, team, player, direction, distance))
            {
                score += 120f;
            }

            if (score > bestScore)
            {
                bestScore = score;
            }
        }

        if (clearDistance <= 0f && !foundJumpableAccess)
        {
            return -100_000f;
        }

        return bestScore + clearDistance;
    }

    private static bool ShouldJumpForVerticalChase(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        int direction,
        int noProgressTicks,
        int noHorizontalProgressTicks,
        int noGoalProgressTicks,
        int totalTicks)
    {
        if (!player.IsGrounded || direction == 0)
        {
            return false;
        }

        if (HasJumpableVerticalAccessAhead(world, team, player, direction))
        {
            return true;
        }

        return noProgressTicks >= 4
            || noHorizontalProgressTicks >= 4
            || noGoalProgressTicks >= 8
            || totalTicks % 10 == 0;
    }

    private static bool HasJumpableVerticalAccessAhead(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        int direction)
    {
        for (var distance = DirectSeekClimbProbeStep; distance <= DirectSeekClimbProbeMaxDistance; distance += DirectSeekClimbProbeStep)
        {
            if (IsJumpableVerticalAccessProbe(world, team, player, direction, distance))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsJumpableVerticalAccessProbe(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        int direction,
        float distance)
    {
        var probeX = player.X + (Math.Sign(direction) * distance);
        if (IsCollisionClearAt(world, team, player, probeX, player.Y))
        {
            return false;
        }

        for (var clearance = 24f; clearance <= DirectSeekClimbClearance; clearance += 20f)
        {
            if (IsCollisionClearAt(world, team, player, probeX, player.Y - clearance))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCollisionClearAt(SimulationWorld world, PlayerTeam team, PlayerEntity player, float x, float y)
    {
        player.GetCollisionBoundsAt(x, y, out var left, out var top, out var right, out var bottom);
        if (IsOutsideLevelBounds(world, left, top, right, bottom))
        {
            return false;
        }

        if (world.Level.IntersectsSolid(left, top, right, bottom))
        {
            return false;
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, player.IsCarryingIntel))
        {
            if (RectanglesOverlap(left, top, right, bottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                return false;
            }
        }

        foreach (var wall in world.Level.RoomObjects)
        {
            if (wall.Type != RoomObjectType.PlayerWall)
            {
                continue;
            }

            if (RectanglesOverlap(left, top, right, bottom, wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                return false;
            }
        }

        return true;
    }

    private static float EstimateDropClearance(SimulationWorld world, PlayerTeam team, PlayerEntity player, float offsetX)
    {
        var probeX = player.X + offsetX;
        player.GetCollisionBoundsAt(probeX, player.Y, out var left, out _, out var right, out var bottom);
        foreach (var gate in world.Level.GetBlockingTeamGates(team, player.IsCarryingIntel))
        {
            if (RectanglesOverlap(left, bottom + 1f, right, bottom + DirectSeekDropProbeDepth, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                return 0f;
            }
        }

        var clearDepth = 0f;
        for (var depth = DirectSeekDropProbeStep; depth <= DirectSeekDropProbeMaxDistance; depth += DirectSeekDropProbeStep)
        {
            if (world.Level.IntersectsSolid(left, bottom + 1f, right, bottom + depth))
            {
                break;
            }

            clearDepth = depth;
        }

        return clearDepth;
    }

    private static bool TryFindNearestDropDirection(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        out int direction)
    {
        var left = FindDropSearchCandidate(world, team, player, -1);
        var right = FindDropSearchCandidate(world, team, player, 1);
        if (!left.HasValue && !right.HasValue)
        {
            direction = 0;
            return false;
        }

        if (!left.HasValue)
        {
            direction = 1;
            return true;
        }

        if (!right.HasValue)
        {
            direction = -1;
            return true;
        }

        direction = right.Value.ClearRunDistance > left.Value.ClearRunDistance + DirectSeekDropProbeStep
            ? 1
            : left.Value.ClearRunDistance > right.Value.ClearRunDistance + DirectSeekDropProbeStep
            ? -1
            : right.Value.DropDistance <= left.Value.DropDistance ? 1 : -1;
        return true;
    }

    private static DropSearchCandidate? FindDropSearchCandidate(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        int direction)
    {
        var clearRunDistance = 0f;
        float? dropDistance = null;
        for (var distance = DirectSeekDropProbeStep; distance <= DirectSeekDropProbeMaxDistance; distance += DirectSeekDropProbeStep)
        {
            var offsetX = direction * distance;
            if (!IsHorizontallyClearProbe(world, team, player, offsetX))
            {
                break;
            }

            clearRunDistance = distance;
            if (distance >= DirectSeekDropProbeMinDistance
                && dropDistance is null
                && IsViableDropProbe(world, team, player, offsetX))
            {
                dropDistance = distance;
            }
        }

        return dropDistance.HasValue ? new DropSearchCandidate(dropDistance.Value, clearRunDistance) : null;
    }

    private static bool IsViableDropProbe(SimulationWorld world, PlayerTeam team, PlayerEntity player, float offsetX)
    {
        var probeX = player.X + offsetX;
        player.GetCollisionBoundsAt(probeX, player.Y, out var left, out var top, out var right, out var bottom);
        if (IsOutsideLevelBounds(world, left, top, right, bottom))
        {
            return false;
        }

        if (world.Level.IntersectsSolid(left, top, right, bottom))
        {
            return false;
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, player.IsCarryingIntel))
        {
            if (RectanglesOverlap(left, top, right, bottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                return false;
            }
        }

        return !world.Level.IntersectsSolid(
            left,
            bottom + 1f,
            right,
            bottom + DirectSeekDropProbeDepth);
    }

    private static bool IsHorizontallyClearProbe(SimulationWorld world, PlayerTeam team, PlayerEntity player, float offsetX)
    {
        var probeX = player.X + offsetX;
        player.GetCollisionBoundsAt(probeX, player.Y, out var left, out var top, out var right, out var bottom);
        if (IsOutsideLevelBounds(world, left, top, right, bottom))
        {
            return false;
        }

        if (world.Level.IntersectsSolid(left, top, right, bottom))
        {
            return false;
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, player.IsCarryingIntel))
        {
            if (RectanglesOverlap(left, top, right, bottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                return false;
            }
        }

        foreach (var wall in world.Level.RoomObjects)
        {
            if (wall.Type != RoomObjectType.PlayerWall)
            {
                continue;
            }

            if (RectanglesOverlap(left, top, right, bottom, wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldPrioritizeEnemyGoals(SimulationWorld world)
    {
        return world.ExperimentalGameplaySettings.EnablePracticeBotsPrioritizeKills
            || IsEnabled(Environment.GetEnvironmentVariable("OG_MOTION_PROOF_CHASE_ENEMIES"));
    }

    private static bool TrySelectEnemyGoal(SimulationWorld world, PlayerEntity player, out GraphGoal goal)
    {
        var bestDistance = float.MaxValue;
        byte bestSlot = 0;
        PlayerEntity? bestEnemy = null;
        foreach (var (slot, candidate) in world.EnumerateActiveNetworkPlayers())
        {
            if (candidate.Id == player.Id
                || !candidate.IsAlive
                || candidate.Team == player.Team)
            {
                continue;
            }

            var distance = Distance(player.X, player.Bottom, candidate.X, candidate.Bottom);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestSlot = slot;
            bestEnemy = candidate;
        }

        if (bestEnemy is null)
        {
            goal = default;
            return false;
        }

        goal = new GraphGoal($"enemy_{bestSlot}", bestEnemy.X, bestEnemy.Bottom, GraphGoalRadius);
        return true;
    }

    private static bool IsEnabled(string? value)
    {
        return value is not null
            && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetMovementProfileId(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Scout => "Scout",
            PlayerClass.Heavy => "Heavy",
            PlayerClass.Soldier or PlayerClass.Sniper => "Soldier",
            PlayerClass.Engineer or PlayerClass.Demoman => "Demoman",
            PlayerClass.Pyro or PlayerClass.Medic or PlayerClass.Spy => "Fast",
            PlayerClass.Quote => "Quote",
            _ => classId.ToString(),
        };
    }

    private static string BuildGraphGoalKey(GraphGoal goal)
    {
        var quantum = IsDynamicGraphGoal(goal.Label) ? DynamicGoalQuantization : StaticGoalQuantization;
        return $"{goal.Label}:{QuantizeGoalCoordinate(goal.X, quantum)}:{QuantizeGoalCoordinate(goal.Bottom, quantum)}";
    }

    private static string BuildDirectSeekGoalKey(GraphGoal goal)
    {
        return BuildGraphGoalKey(goal);
    }

    private static string BuildRouteMapKey(
        PreparedMotionGraph graph,
        PlayerTeam team,
        bool carryingIntel,
        float goalX,
        float goalBottom,
        float goalRadius,
        string goalLabel)
    {
        var quantum = IsDynamicGraphGoal(goalLabel) ? DynamicGoalQuantization : StaticGoalQuantization;
        return $"{graph.Identity}:{graph.Version}:{team}:{carryingIntel}:{QuantizeGoalCoordinate(goalX, quantum)}:{QuantizeGoalCoordinate(goalBottom, quantum)}:{QuantizeGoalCoordinate(goalRadius, StaticGoalQuantization)}";
    }

    private static bool IsDynamicGraphGoal(string label)
    {
        return (label.StartsWith("enemy_", StringComparison.OrdinalIgnoreCase)
                && !label.Equals("enemy_intel", StringComparison.OrdinalIgnoreCase))
            || label.StartsWith("ally_carrier", StringComparison.OrdinalIgnoreCase)
            || label.StartsWith("dropped_", StringComparison.OrdinalIgnoreCase)
            || label.Equals("override", StringComparison.OrdinalIgnoreCase);
    }

    private static int QuantizeGoalCoordinate(float value, float quantum)
    {
        return (int)MathF.Round(value / MathF.Max(1f, quantum));
    }

    private static ControlPointState? SelectKothTargetPoint(SimulationWorld world, PlayerTeam team)
    {
        if (world.MatchRules.Mode == GameModeKind.DoubleKingOfTheHill)
        {
            return world.ControlPoints.FirstOrDefault(point =>
                team == PlayerTeam.Blue
                    ? point.Marker.IsRedKothControlPoint()
                    : point.Marker.IsBlueKothControlPoint());
        }

        return world.ControlPoints.FirstOrDefault(point => point.Marker.IsSingleKothControlPoint())
            ?? (world.ControlPoints.Count > 0 ? world.ControlPoints[0] : null);
    }

    private static GraphReplayState AdvanceGraphReplayState(GraphReplayState state)
    {
        var nextTick = state.ActionTick + 1;
        if (nextTick < state.Actions[state.ActionIndex].Ticks)
        {
            return state with { ActionTick = nextTick };
        }

        return state with { ActionIndex = state.ActionIndex + 1, ActionTick = 0 };
    }

    private bool TryBuildGraphPlan(
        PreparedMotionGraph graph,
        PlayerTeam team,
        bool carryingIntel,
        float startX,
        float startBottom,
        bool startGrounded,
        float startHorizontalSpeed,
        float startVerticalSpeed,
        float playerHeight,
        SimulationWorld world,
        float goalX,
        float goalBottom,
        float goalRadius,
        string goalLabel,
        out GraphPlan plan)
    {
        plan = default;
        var startCandidates = FindStartGraphNodesWithinRadius(graph.Nodes, startX, startBottom, startGrounded, startHorizontalSpeed, startVerticalSpeed, GraphAttachRadius, 64);
        if (startCandidates.Length == 0)
        {
            return false;
        }

        var isDynamicGoal = IsDynamicGraphGoal(goalLabel);
        var goalCandidates = FindGraphNodesWithinRadius(graph.Nodes, goalX, goalBottom, goalRadius, 32);
        if (goalCandidates.Length == 0)
        {
            if (!isDynamicGoal)
            {
                return false;
            }

            goalCandidates = FindNearestGraphNodes(graph.Nodes, goalX, goalBottom, 4, preferStable: false);
        }

        if (isDynamicGoal)
        {
            var lineOfSightGoalCandidates = SelectDynamicGraphGoalCandidates(
                graph,
                world,
                team,
                carryingIntel,
                goalCandidates,
                goalX,
                goalBottom,
                playerHeight,
                requireLineOfSight: true,
                DynamicGraphLineOfSightCandidateCount);
            if (TrySelectGraphPlan(lineOfSightGoalCandidates, useCache: false, out plan))
            {
                return true;
            }
        }

        if (TrySelectGraphPlan(goalCandidates, useCache: !isDynamicGoal, out plan))
        {
            return true;
        }

        if (!isDynamicGoal)
        {
            return false;
        }

        var fallbackGoalCandidates = FindNearestGraphNodes(graph.Nodes, goalX, goalBottom, DynamicGraphGoalCandidateCount, preferStable: false);
        var lineOfSightFallbackCandidates = SelectDynamicGraphGoalCandidates(
            graph,
            world,
            team,
            carryingIntel,
            fallbackGoalCandidates,
            goalX,
            goalBottom,
            playerHeight,
            requireLineOfSight: true,
            DynamicGraphLineOfSightCandidateCount);
        if (TrySelectGraphPlan(lineOfSightFallbackCandidates, useCache: false, out plan))
        {
            return true;
        }

        fallbackGoalCandidates = SelectDynamicGraphGoalCandidates(
            graph,
            world,
            team,
            carryingIntel,
            fallbackGoalCandidates,
            goalX,
            goalBottom,
            playerHeight,
            requireLineOfSight: false,
            DynamicGraphLineOfSightCandidateCount);
        return TrySelectGraphPlan(fallbackGoalCandidates, useCache: false, out plan);

        bool TrySelectGraphPlan(int[] candidateGoalNodes, bool useCache, out GraphPlan selectedPlan)
        {
            selectedPlan = default;
            if (candidateGoalNodes.Length == 0)
            {
                return false;
            }

            var routeMap = useCache
                ? GetOrBuildRouteMap(graph, team, carryingIntel, candidateGoalNodes, goalX, goalBottom, goalRadius, goalLabel)
                : BuildReverseRouteMap(
                    graph,
                    team,
                    carryingIntel,
                    candidateGoalNodes,
                    goalX,
                    goalBottom,
                    isDynamicGoal
                        ? BuildDynamicGraphGoalSeedCosts(graph, world, team, carryingIntel, candidateGoalNodes, goalX, goalBottom, playerHeight)
                        : null);
            var bestScore = float.MaxValue;
            var bestTicks = int.MaxValue;
            foreach (var startIndex in startCandidates)
            {
                if (!TryBuildGraphPlanFromRouteMap(graph, routeMap, startIndex, out var candidateActions, out var reachedGoalIndex, out var pathNodeIndices))
                {
                    continue;
                }

                var candidateNode = graph.Nodes[reachedGoalIndex];
                var candidateDistance = Distance(candidateNode.X, candidateNode.Bottom, goalX, goalBottom);
                var candidateTicks = routeMap.CostByNode[startIndex];
                var candidateLineOfSight = !isDynamicGoal
                    || HasLineOfSight(
                        world,
                        candidateNode.X,
                        candidateNode.Y,
                        goalX,
                        goalBottom - (playerHeight * 0.5f),
                        team,
                        carryingIntel);
                var candidateScore = candidateTicks
                    + (candidateDistance * (isDynamicGoal ? DynamicGraphGoalDistanceCostScale : 1f))
                    + (isDynamicGoal && !candidateLineOfSight ? DynamicGraphNoLineOfSightPenaltyTicks : 0);
                if (candidateScore > bestScore
                    || (MathF.Abs(candidateScore - bestScore) <= 0.1f && candidateTicks >= bestTicks))
                {
                    continue;
                }

                bestScore = candidateScore;
                bestTicks = candidateTicks;
                selectedPlan = new GraphPlan(candidateActions, startIndex, reachedGoalIndex, candidateNode.X, candidateNode.Bottom, pathNodeIndices);
            }

            return selectedPlan.Actions is { Count: > 0 };
        }
    }

    private static int[] SelectDynamicGraphGoalCandidates(
        PreparedMotionGraph graph,
        SimulationWorld world,
        PlayerTeam team,
        bool carryingIntel,
        int[] candidates,
        float goalX,
        float goalBottom,
        float playerHeight,
        bool requireLineOfSight,
        int maxCount)
    {
        if (candidates.Length == 0)
        {
            return Array.Empty<int>();
        }

        var goalCenterY = goalBottom - (playerHeight * 0.5f);
        return candidates
            .Distinct()
            .Select(index =>
            {
                var node = graph.Nodes[index];
                var hasLineOfSight = HasLineOfSight(world, node.X, node.Y, goalX, goalCenterY, team, carryingIntel);
                var distance = Distance(node.X, node.Bottom, goalX, goalBottom);
                return new
                {
                    Index = index,
                    Distance = distance,
                    HasLineOfSight = hasLineOfSight,
                    Score = distance
                        + (hasLineOfSight ? 0f : 10_000f)
                        + (node.IsGrounded ? 0f : 96f),
                };
            })
            .Where(entry => !requireLineOfSight || entry.HasLineOfSight)
            .OrderBy(entry => entry.Score)
            .ThenBy(entry => entry.Distance)
            .Take(maxCount)
            .Select(entry => entry.Index)
            .ToArray();
    }

    private static Dictionary<int, int> BuildDynamicGraphGoalSeedCosts(
        PreparedMotionGraph graph,
        SimulationWorld world,
        PlayerTeam team,
        bool carryingIntel,
        int[] goalCandidates,
        float goalX,
        float goalBottom,
        float playerHeight)
    {
        var goalCenterY = goalBottom - (playerHeight * 0.5f);
        var seedCosts = new Dictionary<int, int>();
        foreach (var goalIndex in goalCandidates.Distinct())
        {
            if (goalIndex < 0 || goalIndex >= graph.Nodes.Count)
            {
                continue;
            }

            var node = graph.Nodes[goalIndex];
            var distance = Distance(node.X, node.Bottom, goalX, goalBottom);
            var hasLineOfSight = HasLineOfSight(world, node.X, node.Y, goalX, goalCenterY, team, carryingIntel);
            var seedCost = (int)MathF.Round(distance * DynamicGraphGoalDistanceCostScale)
                + (hasLineOfSight ? 0 : DynamicGraphNoLineOfSightPenaltyTicks)
                + (node.IsGrounded ? 0 : 256);
            seedCosts[goalIndex] = Math.Max(0, seedCost);
        }

        return seedCosts;
    }

    private GraphRouteMap GetOrBuildRouteMap(
        PreparedMotionGraph graph,
        PlayerTeam team,
        bool carryingIntel,
        int[] goalCandidates,
        float goalX,
        float goalBottom,
        float goalRadius,
        string goalLabel)
    {
        var key = BuildRouteMapKey(graph, team, carryingIntel, goalX, goalBottom, goalRadius, goalLabel);
        if (_routeMapsByKey.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_routeMapsByKey.Count >= MaxRouteMapCacheEntries)
        {
            _routeMapsByKey.Clear();
        }

        var routeMap = BuildReverseRouteMap(graph, team, carryingIntel, goalCandidates, goalX, goalBottom, goalSeedCosts: null);
        _routeMapsByKey[key] = routeMap;
        return routeMap;
    }

    private static GraphRouteMap BuildReverseRouteMap(
        PreparedMotionGraph graph,
        PlayerTeam team,
        bool carryingIntel,
        int[] goalCandidates,
        float goalX,
        float goalBottom,
        Dictionary<int, int>? goalSeedCosts)
    {
        var goalSet = goalCandidates.ToHashSet();
        var queue = new PriorityQueue<int, float>();
        var costByNode = new int[graph.Nodes.Count];
        var nextEdgeByNode = new MotionGraphEdge?[graph.Nodes.Count];
        var settled = new bool[graph.Nodes.Count];
        Array.Fill(costByNode, int.MaxValue);
        foreach (var goalIndex in goalCandidates)
        {
            if (goalIndex < 0 || goalIndex >= costByNode.Length)
            {
                continue;
            }

            var seedCost = goalSeedCosts is not null && goalSeedCosts.TryGetValue(goalIndex, out var seeded)
                ? Math.Max(0, seeded)
                : 0;
            costByNode[goalIndex] = seedCost;
            queue.Enqueue(goalIndex, seedCost);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (settled[current])
            {
                continue;
            }

            settled[current] = true;

            if (!graph.ReverseEdgesByNode.TryGetValue(current, out var incomingEdges))
            {
                continue;
            }

            var currentCost = costByNode[current];
            foreach (var edge in incomingEdges)
            {
                if (!IsEdgeAllowedForTeam(edge, team) || !IsEdgeAllowedForCarry(edge, carryingIntel))
                {
                    continue;
                }

                var nextCost = currentCost + edge.CostTicks;
                if (nextCost >= costByNode[edge.From])
                {
                    continue;
                }

                costByNode[edge.From] = nextCost;
                nextEdgeByNode[edge.From] = edge;
                queue.Enqueue(edge.From, nextCost);
            }
        }

        return new GraphRouteMap(goalSet, costByNode, nextEdgeByNode);
    }

    private bool TryBuildDynamicGraphWaypointInput(
        byte slot,
        SimulationWorld world,
        PlayerTeam team,
        PreparedMotionGraph graph,
        GraphReplayState state,
        PlayerEntity player,
        GraphGoal finalGoal,
        out PlayerInputSnapshot input,
        out GraphReplayState updatedState)
    {
        input = default;
        updatedState = state;
        if (state.PathNodeIndices.Count < 2)
        {
            return false;
        }

        var actionIndex = Math.Clamp(state.ActionIndex, 0, state.PathNodeIndices.Count - 1);
        while (actionIndex < state.PathNodeIndices.Count - 1)
        {
            var node = graph.Nodes[state.PathNodeIndices[actionIndex + 1]];
            if (Distance(player.X, player.Bottom, node.X, node.Bottom) > 72f)
            {
                break;
            }

            actionIndex += 1;
        }

        var lookaheadIndex = Math.Min(state.PathNodeIndices.Count - 1, actionIndex + 3);
        for (var candidateIndex = lookaheadIndex; candidateIndex > actionIndex + 1; candidateIndex -= 1)
        {
            var candidate = graph.Nodes[state.PathNodeIndices[candidateIndex]];
            if (Distance(player.X, player.Bottom, candidate.X, candidate.Bottom) <= 220f
                && HasLineOfSight(world, player.X, player.Y, candidate.X, candidate.Y, team, player.IsCarryingIntel))
            {
                lookaheadIndex = candidateIndex;
                break;
            }
        }

        var waypointNode = graph.Nodes[state.PathNodeIndices[lookaheadIndex]];
        if (lookaheadIndex >= state.PathNodeIndices.Count - 1
            && HasLineOfSight(
                world,
                player.X,
                player.Y,
                finalGoal.X,
                finalGoal.Bottom - (player.Height * 0.5f),
                team,
                player.IsCarryingIntel))
        {
            input = BuildDirectSeekInput(slot, world, team, player, finalGoal);
            updatedState = state with { ActionIndex = actionIndex };
            return true;
        }

        var waypointGoal = new GraphGoal(
            $"{finalGoal.Label}_waypoint",
            waypointNode.X,
            waypointNode.Bottom,
            MathF.Min(finalGoal.Radius, 96f));
        input = BuildDirectSeekInput(slot, world, team, player, waypointGoal);
        updatedState = state with { ActionIndex = actionIndex };
        return true;
    }

    private static bool TryBuildGraphPlanFromRouteMap(
        PreparedMotionGraph graph,
        GraphRouteMap routeMap,
        int startIndex,
        out IReadOnlyList<MotionProofAction> actions,
        out int reachedGoalIndex,
        out IReadOnlyList<int> pathNodeIndices)
    {
        actions = Array.Empty<MotionProofAction>();
        reachedGoalIndex = -1;
        pathNodeIndices = Array.Empty<int>();
        if (startIndex < 0
            || startIndex >= routeMap.CostByNode.Length
            || routeMap.CostByNode[startIndex] == int.MaxValue)
        {
            return false;
        }

        var planned = new List<MotionProofAction>();
        var pathNodes = new List<int> { startIndex };
        var pathNode = startIndex;
        for (var steps = 0; steps < MaxGraphRouteActions; steps += 1)
        {
            if (routeMap.GoalNodes.Contains(pathNode))
            {
                actions = planned;
                reachedGoalIndex = pathNode;
                pathNodeIndices = pathNodes;
                return actions.Count > 0;
            }

            var edge = routeMap.NextEdgeByNode[pathNode];
            if (edge is null)
            {
                return false;
            }

            planned.Add(edge.Action);
            pathNode = edge.To;
            pathNodes.Add(pathNode);
        }

        return false;
    }

    private static bool IsEdgeAllowedForTeam(MotionGraphEdge edge, PlayerTeam team)
    {
        return edge.TeamMask.Equals("Any", StringComparison.OrdinalIgnoreCase)
            || edge.TeamMask.Equals(team.ToString(), StringComparison.OrdinalIgnoreCase)
            || edge.TeamMask.Equals("Both", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEdgeAllowedForCarry(MotionGraphEdge edge, bool carryingIntel)
    {
        return edge.CarryMask.Equals("Any", StringComparison.OrdinalIgnoreCase)
            || edge.CarryMask.Equals(carryingIntel ? "Carrying" : "Free", StringComparison.OrdinalIgnoreCase)
            || edge.CarryMask.Equals("Both", StringComparison.OrdinalIgnoreCase);
    }

    private static int FindNearestGraphNode(IReadOnlyList<MotionGraphNode> nodes, float x, float bottom)
    {
        var bestIndex = 0;
        var bestDistance = float.MaxValue;
        for (var index = 0; index < nodes.Count; index += 1)
        {
            var node = nodes[index];
            var distance = Distance(node.X, node.Bottom, x, bottom);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static int[] FindNearestGraphNodes(
        IReadOnlyList<MotionGraphNode> nodes,
        float x,
        float bottom,
        int count,
        bool preferStable = false)
    {
        return nodes
            .Select((node, index) => new
            {
                Index = index,
                Distance = Distance(node.X, node.Bottom, x, bottom),
                Score = Distance(node.X, node.Bottom, x, bottom) + (preferStable && !IsStableGraphNode(node) ? 10000f : 0f),
            })
            .OrderBy(entry => entry.Score)
            .ThenBy(entry => entry.Distance)
            .Take(Math.Min(count, nodes.Count))
            .Select(entry => entry.Index)
            .ToArray();
    }

    private static int[] FindGraphNodesWithinRadius(
        IReadOnlyList<MotionGraphNode> nodes,
        float x,
        float bottom,
        float radius,
        int maxCount,
        bool preferStable = false)
    {
        return nodes
            .Select((node, index) => new
            {
                Index = index,
                Distance = Distance(node.X, node.Bottom, x, bottom),
                Score = Distance(node.X, node.Bottom, x, bottom) + (preferStable && !IsStableGraphNode(node) ? radius : 0f),
            })
            .Where(entry => entry.Distance <= radius)
            .OrderBy(entry => entry.Score)
            .ThenBy(entry => entry.Distance)
            .Take(Math.Min(maxCount, nodes.Count))
            .Select(entry => entry.Index)
            .ToArray();
    }

    private static int[] FindStartGraphNodesWithinRadius(
        IReadOnlyList<MotionGraphNode> nodes,
        float x,
        float bottom,
        bool grounded,
        float horizontalSpeed,
        float verticalSpeed,
        float radius,
        int maxCount)
    {
        var entries = nodes
            .Select((node, index) =>
            {
                var distance = Distance(node.X, node.Bottom, x, bottom);
                var horizontalMismatch = MathF.Abs(node.HorizontalSpeed - horizontalSpeed);
                var verticalMismatch = MathF.Abs(node.VerticalSpeed - verticalSpeed);
                var speedMismatch = horizontalMismatch + verticalMismatch;
                var stationaryPenalty = MathF.Abs(horizontalSpeed) <= 5f && MathF.Abs(verticalSpeed) <= 5f && speedMismatch > 20f
                    ? 128f
                    : 0f;
                var groundedPenalty = node.IsGrounded == grounded ? 0f : 32f;
                return new
                {
                    Index = index,
                    Distance = distance,
                    Score = distance + speedMismatch + stationaryPenalty + groundedPenalty,
                };
            })
            .Where(entry => entry.Distance <= radius)
            .ToArray();
        if (MathF.Abs(horizontalSpeed) <= 5f && MathF.Abs(verticalSpeed) <= 5f)
        {
            var restAnchors = entries
                .Where(entry => MathF.Abs(nodes[entry.Index].HorizontalSpeed) <= 5f && MathF.Abs(nodes[entry.Index].VerticalSpeed) <= 5f)
                .ToArray();
            if (restAnchors.Length > 0)
            {
                entries = restAnchors;
            }
        }

        return entries
            .OrderBy(entry => entry.Score)
            .ThenBy(entry => entry.Distance)
            .Take(Math.Min(maxCount, nodes.Count))
            .Select(entry => entry.Index)
            .ToArray();
    }

    private static bool IsStableGraphNode(MotionGraphNode node)
    {
        return node.IsGrounded
            && MathF.Abs(node.HorizontalSpeed) <= 5f
            && MathF.Abs(node.VerticalSpeed) <= 5f;
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static T? ReadJsonArtifact<T>(string path)
    {
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            using var file = File.OpenRead(path);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            return JsonSerializer.Deserialize<T>(gzip, JsonOptions);
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions);
    }

    private ReplayState GetReplayState(
        byte slot,
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        MotionProofArtifact artifact,
        ReplayPhase phase)
    {
        var mapKey = $"{world.Level.Name}.a{world.Level.MapAreaIndex}.{controlledSlot.Team}.{controlledSlot.ClassId}.{artifact.Version}";
        if (_statesBySlot.TryGetValue(slot, out var state) && state.MapKey == mapKey)
        {
            return state;
        }

        return new ReplayState(
            mapKey,
            phase,
            ActionIndex: 0,
            ActionTick: 0,
            LastX: float.NaN,
            LastBottom: float.NaN,
            NoProgressTicks: 0,
            BestGoalDistance: float.NaN,
            NoGoalProgressTicks: 0);
    }

    private static ReplayPhase SelectPhase(SimulationWorld world, PlayerEntity player)
    {
        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag && player.IsCarryingIntel)
        {
            return ReplayPhase.Return;
        }

        return ReplayPhase.Attack;
    }

    private static ReplayState AdvanceReplayState(ReplayState state, IReadOnlyList<MotionProofAction> tape)
    {
        if (state.ActionIndex >= tape.Count)
        {
            return state;
        }

        var nextTick = state.ActionTick + 1;
        if (nextTick < tape[state.ActionIndex].Ticks)
        {
            return state with { ActionTick = nextTick };
        }

        return state with { ActionIndex = state.ActionIndex + 1, ActionTick = 0 };
    }

    private static ReplayState TrackReplayProgress(ReplayState state, PlayerEntity player, float objectiveDistance)
    {
        var goalState = state;
        if (!float.IsNaN(objectiveDistance))
        {
            if (float.IsNaN(state.BestGoalDistance) || objectiveDistance + 8f < state.BestGoalDistance)
            {
                goalState = state with { BestGoalDistance = objectiveDistance, NoGoalProgressTicks = 0 };
            }
            else
            {
                goalState = state with { NoGoalProgressTicks = state.NoGoalProgressTicks + 1 };
            }
        }

        if (float.IsNaN(state.LastX) || float.IsNaN(state.LastBottom))
        {
            return goalState with { LastX = player.X, LastBottom = player.Bottom, NoProgressTicks = 0 };
        }

        var moved = Distance(player.X, player.Bottom, state.LastX, state.LastBottom);
        return moved >= NoProgressDistance
            ? goalState with { NoProgressTicks = 0 }
            : goalState with { NoProgressTicks = state.NoProgressTicks + 1 };
    }

    private static GraphReplayState TrackGraphProgress(GraphReplayState state, PlayerEntity player)
    {
        var moved = Distance(player.X, player.Bottom, state.LastX, state.LastBottom);
        return moved >= NoProgressDistance
            ? state with { NoProgressTicks = 0 }
            : state with { NoProgressTicks = state.NoProgressTicks + 1 };
    }

    private static DirectSeekState TrackDirectSeekProgress(DirectSeekState state, PlayerEntity player, float currentGoalDistance)
    {
        var moved = Distance(player.X, player.Bottom, state.LastX, state.LastBottom);
        var movedHorizontally = MathF.Abs(player.X - state.LastX);
        var improvedGoalDistance = currentGoalDistance + 8f < state.BestGoalDistance;
        return state with
        {
            NoProgressTicks = moved >= NoProgressDistance ? 0 : state.NoProgressTicks + 1,
            NoHorizontalProgressTicks = movedHorizontally >= NoProgressDistance ? 0 : state.NoHorizontalProgressTicks + 1,
            BestGoalDistance = improvedGoalDistance ? currentGoalDistance : state.BestGoalDistance,
            NoGoalProgressTicks = improvedGoalDistance ? 0 : state.NoGoalProgressTicks + 1,
        };
    }

    private bool ShouldResetSlotStateForTeleport(byte slot, PlayerEntity player)
    {
        if (_directSeekStatesBySlot.TryGetValue(slot, out var directState)
            && ShouldResetSlotStateForTeleport(player, directState.LastX, directState.LastBottom))
        {
            return true;
        }

        if (_graphStatesBySlot.TryGetValue(slot, out var graphState)
            && ShouldResetSlotStateForTeleport(player, graphState.LastX, graphState.LastBottom))
        {
            return true;
        }

        return _statesBySlot.TryGetValue(slot, out var replayState)
            && ShouldResetSlotStateForTeleport(player, replayState.LastX, replayState.LastBottom);
    }

    private static bool ShouldResetSlotStateForTeleport(PlayerEntity player, float lastX, float lastBottom)
    {
        if (float.IsNaN(lastX) || float.IsNaN(lastBottom))
        {
            return false;
        }

        return Distance(player.X, player.Bottom, lastX, lastBottom) >= SlotStateTeleportResetDistance;
    }

    private static int GetObjectiveTapeFastForwardTicks(SimulationWorld world)
    {
        return Math.Max(1, (int)MathF.Ceiling(world.Config.TicksPerSecond * ObjectiveTapeFastForwardSeconds));
    }

    private static int GetGraphNoProgressTickLimit(string goalLabel)
    {
        return IsDynamicGraphGoal(goalLabel) ? EnemyGraphNoProgressTickLimit : NoProgressTickLimit;
    }

    private static int GetGraphRescueJumpTicks(string goalLabel)
    {
        return IsDynamicGraphGoal(goalLabel) ? EnemyGraphRescueJumpTicks : ObjectiveNavigationRescueJumpTicks;
    }

    private static bool CanFastForwardInertTapeAction(IReadOnlyList<MotionProofAction> actions, int currentIndex)
    {
        return currentIndex >= 0
            && currentIndex < actions.Count
            && IsInertTapeAction(actions[currentIndex]);
    }

    private static int FindNextActiveTapeActionIndex(IReadOnlyList<MotionProofAction> actions, int currentIndex)
    {
        for (var index = Math.Min(currentIndex + 1, actions.Count - 1); index < actions.Count; index += 1)
        {
            if (!IsInertTapeAction(actions[index]))
            {
                return index;
            }
        }

        return Math.Min(currentIndex + 1, actions.Count - 1);
    }

    private static bool TryFindBetterObjectiveTapeAction(
        IReadOnlyList<MotionProofAction> actions,
        int currentIndex,
        float goalDeltaX,
        out int actionIndex)
    {
        var desiredDirection = Math.Sign(goalDeltaX);
        if (desiredDirection == 0)
        {
            actionIndex = currentIndex;
            return false;
        }

        for (var index = Math.Min(currentIndex + 1, actions.Count - 1); index < actions.Count; index += 1)
        {
            var action = actions[index];
            if (IsInertTapeAction(action))
            {
                continue;
            }

            if (action.Direction == desiredDirection || IsVerticalRecoveryTapeAction(action))
            {
                actionIndex = index;
                return index != currentIndex;
            }
        }

        actionIndex = currentIndex;
        return false;
    }

    private static bool IsVerticalRecoveryTapeAction(MotionProofAction action)
    {
        return action.Kind.Equals("Jump", StringComparison.OrdinalIgnoreCase)
            || action.Kind.Equals("Drop", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInertTapeAction(MotionProofAction action)
    {
        return action.Kind.Equals("Idle", StringComparison.OrdinalIgnoreCase)
            || (action.Direction == 0
                && !action.Kind.Equals("Jump", StringComparison.OrdinalIgnoreCase)
                && !action.Kind.Equals("Drop", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGroundedIdleAction(MotionProofAction action, PlayerEntity player)
    {
        return player.IsGrounded
            && action.Kind.Equals("Idle", StringComparison.OrdinalIgnoreCase);
    }

    private static PlayerInputSnapshot BuildActionInput(MotionProofAction action, int tick, PlayerEntity player)
    {
        var left = action.Direction < 0;
        var right = action.Direction > 0;
        var up = string.Equals(action.Kind, "Jump", StringComparison.OrdinalIgnoreCase) && tick == 0;
        var down = string.Equals(action.Kind, "Drop", StringComparison.OrdinalIgnoreCase);
        var aimDirection = action.Direction == 0 ? 1 : action.Direction;
        return new PlayerInputSnapshot(
            Left: left,
            Right: right,
            Up: up,
            Down: down,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + (aimDirection * 160f),
            AimWorldY: player.Y,
            DebugKill: false);
    }

    private static PlayerInputSnapshot ApplyNavigationRescueInput(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        MotionProofAction action,
        PlayerInputSnapshot input,
        int noProgressTicks,
        bool allowObstacleJump,
        int rescueJumpTicks)
    {
        var direction = input.Right ? 1 : input.Left ? -1 : action.Direction;
        if (direction == 0)
        {
            return input;
        }

        var blockedAhead = player.IsGrounded
            && WouldMoveIntoObstacle(world, team, player, direction);
        var obstacleAhead = allowObstacleJump && blockedAhead;
        var shouldJump = noProgressTicks >= rescueJumpTicks
            || (obstacleAhead && action.Kind.Equals("Run", StringComparison.OrdinalIgnoreCase));
        if (!shouldJump)
        {
            return input;
        }

        if (noProgressTicks >= rescueJumpTicks
            && blockedAhead
            && !WouldMoveIntoObstacle(world, team, player, -direction, probeDistance: DirectSeekObstacleProbeDistance))
        {
            direction = -direction;
        }

        return input with
        {
            Up = player.IsGrounded || noProgressTicks >= rescueJumpTicks,
            Left = direction < 0,
            Right = direction > 0,
        };
    }

    private static bool WouldMoveIntoObstacle(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        int direction,
        float probeDistance = ObstacleProbeDistance)
    {
        var probeX = player.X + (Math.Sign(direction) * probeDistance);
        player.GetCollisionBoundsAt(probeX, player.Y, out var left, out var top, out var right, out var bottom);
        if (IsOutsideLevelBounds(world, left, top, right, bottom))
        {
            return true;
        }

        if (world.Level.IntersectsSolid(left, top, right, bottom))
        {
            return true;
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, player.IsCarryingIntel))
        {
            if (RectanglesOverlap(left, top, right, bottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                return true;
            }
        }

        foreach (var wall in world.Level.RoomObjects)
        {
            if (wall.Type != RoomObjectType.PlayerWall)
            {
                continue;
            }

            if (RectanglesOverlap(left, top, right, bottom, wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOutsideLevelBounds(SimulationWorld world, float left, float top, float right, float bottom)
    {
        return left < 0f
            || top < 0f
            || right > world.Level.Bounds.Width
            || bottom > world.Level.Bounds.Height;
    }

    private static PlayerInputSnapshot BuildIdleInput(PlayerEntity player)
    {
        return new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + 160f,
            AimWorldY: player.Y,
            DebugKill: false);
    }

    private PlayerInputSnapshot ApplyCombatInput(
        SimulationWorld world,
        PlayerEntity player,
        PlayerInputSnapshot movementInput,
        IReadOnlyList<PlayerEntity> allPlayers)
    {
        if (!player.IsAlive)
        {
            return movementInput;
        }

        var hasCombatTarget = TrySelectCombatTarget(world, player, allPlayers, out var target);
        var targetPlayer = target.Player;
        var healTarget = player.ClassId == PlayerClass.Medic
            ? FindBestMedicHealTarget(world, player, allPlayers)
            : null;
        var isBeingHealed = IsPlayerBeingHealed(allPlayers, player);
        var firePrimary = false;
        var fireSecondary = false;
        var fireSecondaryWeapon = false;
        var aimX = movementInput.AimWorldX;
        var aimY = movementInput.AimWorldY;
        if (targetPlayer is not null)
        {
            movementInput = ApplyCombatSpacingInput(movementInput, player, targetPlayer);
        }

        if (player.ClassId == PlayerClass.Medic)
        {
            if (healTarget is not null)
            {
                aimX = healTarget.X;
                aimY = GetCombatAimTargetY(healTarget);
                firePrimary = NeedsHealing(healTarget)
                    && Distance(player.X, player.Y, healTarget.X, healTarget.Y) <= MedicHealFireDistance
                    && HasCombatLineOfSight(world, player, healTarget);
                fireSecondary = player.IsMedicUberReady
                    && (healTarget.Health < healTarget.MaxHealth / 2
                        || GetHealthFraction(player) < LowHealthFraction
                        || (hasCombatTarget
                            && Distance(player.X, player.Y, target.X, target.Y) <= MedicUberEnemyThreatDistance));
            }
            else if (hasCombatTarget && Distance(player.X, player.Y, target.X, target.Y) <= MedicNeedleRange)
            {
                aimX = target.X;
                aimY = target.Y;
                fireSecondary = true;
            }

            fireSecondaryWeapon = player.IsMedicUberReady
                && (player.Health < 40 || healTarget is { Health: < 50 });

            return movementInput with
            {
                FirePrimary = firePrimary,
                FireSecondary = fireSecondary,
                AimWorldX = aimX,
                AimWorldY = aimY,
                FireSecondaryWeapon = fireSecondaryWeapon,
            };
        }

        if (player.ClassId == PlayerClass.Heavy)
        {
            var shouldEat = ShouldHeavyEat(player, isBeingHealed, hasCombatTarget ? HeavyCombatEatHealth : HeavyIdleEatHealth);
            if (hasCombatTarget)
            {
                aimX = target.X;
                aimY = target.Y;
            }

            return movementInput with
            {
                FirePrimary = hasCombatTarget && !shouldEat && ShouldFirePrimaryAtTarget(player, target),
                FireSecondary = shouldEat,
                AimWorldX = aimX,
                AimWorldY = aimY,
            };
        }

        if (player.ClassId == PlayerClass.Demoman)
        {
            if (hasCombatTarget)
            {
                aimX = target.X;
                aimY = target.Y;
                firePrimary = ShouldFirePrimaryAtTarget(player, target);
            }

            fireSecondary = ShouldDetonateMines(world, player, allPlayers);
            return movementInput with
            {
                FirePrimary = firePrimary,
                FireSecondary = fireSecondary,
                AimWorldX = aimX,
                AimWorldY = aimY,
            };
        }

        if (player.ClassId == PlayerClass.Sniper)
        {
            if (!hasCombatTarget)
            {
                return movementInput with
                {
                    FireSecondary = player.IsSniperScoped,
                };
            }

            var sniperDistance = Distance(player.X, player.Y, target.X, target.Y);
            aimX = target.X;
            aimY = target.Y;
            fireSecondary = player.IsSniperScoped
                ? sniperDistance < SniperScopeDistance
                : sniperDistance >= SniperScopeDistance;
            firePrimary = player.IsSniperScoped && player.SniperChargeTicks >= SniperMinimumChargeTicks;
            return movementInput with
            {
                FirePrimary = firePrimary,
                FireSecondary = fireSecondary,
                AimWorldX = aimX,
                AimWorldY = aimY,
            };
        }

        if (player.ClassId == PlayerClass.Spy)
        {
            if (!hasCombatTarget)
            {
                return movementInput with
                {
                    FireSecondary = player.IsSpyCloaked,
                };
            }

            aimX = target.X;
            aimY = target.Y;
            var spyDistance = Distance(player.X, player.Y, target.X, target.Y);
            fireSecondary = player.IsCarryingIntel
                ? false
                : player.IsSpyCloaked
                    ? spyDistance > 96f && !player.IsSpyBackstabAnimating
                    : false;
            firePrimary = target.Kind == CombatTargetKind.Player
                && player.IsSpyCloaked
                && spyDistance <= 64f;
            return movementInput with
            {
                FirePrimary = firePrimary,
                FireSecondary = fireSecondary,
                AimWorldX = aimX,
                AimWorldY = aimY,
            };
        }

        if (!hasCombatTarget)
        {
            return movementInput;
        }

        var distance = Distance(player.X, player.Y, target.X, target.Y);
        firePrimary = ShouldFirePrimaryAtTarget(player, target);
        fireSecondary = player.ClassId == PlayerClass.Quote
            && distance is > QuoteBladeMinimumDistance and <= QuoteBladeMaximumDistance;
        fireSecondaryWeapon = ShouldFireSecondaryWeaponAtTarget(player, target, firePrimary, fireSecondary);
        return movementInput with
        {
            FirePrimary = firePrimary,
            FireSecondary = fireSecondary,
            AimWorldX = target.X,
            AimWorldY = target.Y,
            FireSecondaryWeapon = fireSecondaryWeapon,
        };
    }

    private static PlayerInputSnapshot ApplyCombatSpacingInput(PlayerInputSnapshot movementInput, PlayerEntity player, PlayerEntity target)
    {
        var minimumSpacing = GetMinimumCombatSpacing(player.ClassId);
        if (MathF.Abs(target.Bottom - player.Bottom) > 48f
            || MathF.Abs(target.X - player.X) >= minimumSpacing)
        {
            return movementInput;
        }

        var awayDirection = Math.Sign(player.X - target.X);
        if (awayDirection == 0)
        {
            awayDirection = player.FacingDirectionX >= 0f ? -1 : 1;
        }

        return movementInput with
        {
            Left = awayDirection < 0,
            Right = awayDirection > 0,
        };
    }

    private static float GetMinimumCombatSpacing(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Pyro => 72f,
            PlayerClass.Scout => 96f,
            PlayerClass.Spy => 72f,
            PlayerClass.Heavy => 160f,
            PlayerClass.Soldier => 180f,
            PlayerClass.Demoman => 180f,
            PlayerClass.Sniper => 180f,
            _ => 110f,
        };
    }

    private static bool TrySelectCombatTarget(
        SimulationWorld world,
        PlayerEntity player,
        IReadOnlyList<PlayerEntity> allPlayers,
        out CombatTarget target)
    {
        target = default;
        var found = false;
        var bestDistance = MathF.Max(CombatSightDistance, GetMaximumFireRange(player.ClassId));

        for (var index = 0; index < world.Generators.Count; index += 1)
        {
            var candidate = world.Generators[index];
            if (candidate.Team == player.Team || candidate.IsDestroyed)
            {
                continue;
            }

            var candidateX = candidate.Marker.CenterX;
            var candidateY = candidate.Marker.CenterY;
            var distance = Distance(player.X, player.Y, candidateX, candidateY);
            if (distance >= bestDistance
                || !HasLineOfSight(world, player.X, player.Y, candidateX, candidateY, player.Team, player.IsCarryingIntel))
            {
                continue;
            }

            found = true;
            bestDistance = distance;
            target = new CombatTarget(CombatTargetKind.Generator, candidate.Team, candidateX, candidateY, Generator: candidate);
        }

        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (candidate.Id == player.Id
                || !candidate.IsAlive
                || candidate.Team == player.Team)
            {
                continue;
            }

            var distance = Distance(player.X, player.Y, candidate.X, candidate.Y);
            if (distance >= bestDistance || !HasCombatLineOfSight(world, player, candidate))
            {
                continue;
            }

            found = true;
            bestDistance = distance;
            target = new CombatTarget(CombatTargetKind.Player, candidate.Team, candidate.X, GetCombatAimTargetY(candidate), Player: candidate);
        }

        for (var index = 0; index < world.Sentries.Count; index += 1)
        {
            var candidate = world.Sentries[index];
            if (candidate.Team == player.Team || candidate.Health <= 0)
            {
                continue;
            }

            var candidateY = candidate.Y - (SentryEntity.Height * 0.5f);
            var distance = Distance(player.X, player.Y, candidate.X, candidateY);
            if (distance >= bestDistance
                || !HasLineOfSight(world, player.X, player.Y, candidate.X, candidateY, player.Team, player.IsCarryingIntel))
            {
                continue;
            }

            found = true;
            bestDistance = distance;
            target = new CombatTarget(CombatTargetKind.Sentry, candidate.Team, candidate.X, candidateY, Sentry: candidate);
        }

        return found;
    }

    private static bool ShouldFirePrimaryAtTarget(PlayerEntity player, CombatTarget target)
    {
        if (target.Kind == CombatTargetKind.Player
            && target.Player is { IsSpyCloaked: true }
            && player.ClassId != PlayerClass.Pyro)
        {
            return false;
        }

        var distance = Distance(player.X, player.Y, target.X, target.Y);
        return player.PrimaryWeapon.Kind switch
        {
            PrimaryWeaponKind.PelletGun => distance <= 280f,
            PrimaryWeaponKind.Minigun => distance <= 340f,
            PrimaryWeaponKind.Rifle => distance <= 900f,
            PrimaryWeaponKind.Revolver => distance <= 480f,
            PrimaryWeaponKind.FlameThrower => distance <= 150f,
            PrimaryWeaponKind.RocketLauncher => distance is >= 120f and <= 520f,
            PrimaryWeaponKind.MineLauncher => distance is >= 90f and <= 260f,
            PrimaryWeaponKind.Blade => distance <= 64f,
            PrimaryWeaponKind.Medigun => false,
            _ => distance <= GetMaximumFireRange(player.ClassId),
        };
    }

    private static bool ShouldFireSecondaryWeaponAtTarget(
        PlayerEntity player,
        CombatTarget target,
        bool firePrimary,
        bool fireSecondary)
    {
        var distance = Distance(player.X, player.Y, target.X, target.Y);
        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.PyroUtility))
        {
            return target.Kind == CombatTargetKind.Player
                && player.CanFirePyroAirblast()
                && distance <= PyroAirblastDistance;
        }

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.SoldierSecondaryWeapon))
        {
            return !fireSecondary
                && (!firePrimary || player.CurrentShells <= 0 || player.PrimaryCooldownTicks > 0)
                && player.ExperimentalOffhandCurrentShells > 0
                && player.ExperimentalOffhandCooldownTicks <= 0
                && distance <= SoldierOffhandDistance;
        }

        return false;
    }

    private static PlayerEntity? FindBestMedicHealTarget(
        SimulationWorld world,
        PlayerEntity player,
        IReadOnlyList<PlayerEntity> allPlayers)
    {
        PlayerEntity? bestTarget = null;
        var bestScore = float.MaxValue;
        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (!candidate.IsAlive
                || candidate.Team != player.Team
                || candidate.Id == player.Id
                || !NeedsHealing(candidate))
            {
                continue;
            }

            var distanceSquared = DistanceSquared(player.X, player.Y, candidate.X, candidate.Y);
            if (distanceSquared > HealTargetSeekDistance * HealTargetSeekDistance)
            {
                continue;
            }

            var score = distanceSquared;
            score -= (1f - GetHealthFraction(candidate)) * 60_000f;
            if (candidate.IsCarryingIntel)
            {
                score -= 90_000f;
            }

            if (!HasCombatLineOfSight(world, player, candidate))
            {
                score += 40_000f;
            }

            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    private bool ShouldDetonateMines(
        SimulationWorld world,
        PlayerEntity player,
        IReadOnlyList<PlayerEntity> allPlayers)
    {
        var ownedMineCount = 0;
        for (var mineIndex = 0; mineIndex < world.Mines.Count; mineIndex += 1)
        {
            var mine = world.Mines[mineIndex];
            if (mine.OwnerId != player.Id || mine.IsDestroyed)
            {
                continue;
            }

            ownedMineCount += 1;
            if (Distance(player.X, player.Y, mine.X, mine.Y) >= MineThreatDistance)
            {
                continue;
            }

            for (var playerIndex = 0; playerIndex < allPlayers.Count; playerIndex += 1)
            {
                var candidate = allPlayers[playerIndex];
                if (!candidate.IsAlive
                    || candidate.Id == player.Id
                    || candidate.Team == player.Team)
                {
                    continue;
                }

                if (Distance(candidate.X, candidate.Y, mine.X, mine.Y) <= MineDetonationRadius)
                {
                    return true;
                }
            }
        }

        if (ownedMineCount >= player.PrimaryWeapon.MaxAmmo)
        {
            var roll = _random.Next(0, 101);
            return roll is >= 70 and <= 71;
        }

        return false;
    }

    private static bool IsPlayerBeingHealed(IReadOnlyList<PlayerEntity> allPlayers, PlayerEntity player)
    {
        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (!candidate.IsAlive
                || candidate.Team != player.Team
                || candidate.ClassId != PlayerClass.Medic
                || !candidate.IsMedicHealing
                || candidate.MedicHealTargetId != player.Id)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool ShouldHeavyEat(PlayerEntity player, bool isBeingHealed, int healthThreshold)
    {
        return player.ClassId == PlayerClass.Heavy
            && !isBeingHealed
            && !player.IsHeavyEating
            && player.HeavyEatCooldownTicksRemaining <= 0
            && player.Health <= healthThreshold;
    }

    private static bool NeedsHealing(PlayerEntity player)
    {
        return player.Health < player.MaxHealth
            || player.IsBurning
            || player.IsCarryingIntel
            || GetHealthFraction(player) < LowHealthFraction;
    }

    private static float GetHealthFraction(PlayerEntity player)
    {
        return player.MaxHealth <= 0 ? 0f : player.Health / (float)player.MaxHealth;
    }

    private static float DistanceSquared(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return (dx * dx) + (dy * dy);
    }

    private static float GetMaximumFireRange(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Pyro => 230f,
            PlayerClass.Heavy => 520f,
            PlayerClass.Soldier => 700f,
            PlayerClass.Sniper => 900f,
            PlayerClass.Spy => 480f,
            PlayerClass.Medic => MedicNeedleRange,
            _ => 520f,
        };
    }

    private static bool HasCombatLineOfSight(SimulationWorld world, PlayerEntity origin, PlayerEntity target)
    {
        return HasLineOfSight(
            world,
            origin.X,
            origin.Y,
            target.X,
            GetCombatAimTargetY(target),
            origin.Team,
            origin.IsCarryingIntel);
    }

    private static bool HasLineOfSight(
        SimulationWorld world,
        float originX,
        float originY,
        float targetX,
        float targetY,
        PlayerTeam team,
        bool carryingIntel)
    {
        var distance = Distance(originX, originY, targetX, targetY);
        if (distance <= 0.0001f)
        {
            return true;
        }

        var directionX = (targetX - originX) / distance;
        var directionY = (targetY - originY) / distance;
        var lineLeft = MathF.Min(originX, targetX);
        var lineTop = MathF.Min(originY, targetY);
        var lineRight = MathF.Max(originX, targetX);
        var lineBottom = MathF.Max(originY, targetY);
        foreach (var solid in world.Level.Solids)
        {
            if (!RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, solid.Left, solid.Top, solid.Right, solid.Bottom))
            {
                continue;
            }

            if (GetRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, solid.Left, solid.Top, solid.Right, solid.Bottom, distance).HasValue)
            {
                return false;
            }
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, carryingIntel))
        {
            if (!RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                continue;
            }

            if (GetRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, gate.Left, gate.Top, gate.Right, gate.Bottom, distance).HasValue)
            {
                return false;
            }
        }

        foreach (var wall in world.Level.RoomObjects)
        {
            if (wall.Type != RoomObjectType.PlayerWall && wall.Type != RoomObjectType.BulletWall)
            {
                continue;
            }

            if (!RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                continue;
            }

            if (GetRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, wall.Left, wall.Top, wall.Right, wall.Bottom, distance).HasValue)
            {
                return false;
            }
        }

        return true;
    }

    private static float GetCombatAimTargetY(PlayerEntity target)
    {
        return (target.Top + target.Bottom) * 0.5f;
    }

    private static bool RectanglesOverlap(
        float leftA,
        float topA,
        float rightA,
        float bottomA,
        float leftB,
        float topB,
        float rightB,
        float bottomB)
    {
        return leftA < rightB && rightA > leftB && topA < bottomB && bottomA > topB;
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
        var tMin = 0f;
        var tMax = maxDistance;
        if (!ClipRaySlab(originX, directionX, left, right, ref tMin, ref tMax)
            || !ClipRaySlab(originY, directionY, top, bottom, ref tMin, ref tMax))
        {
            return null;
        }

        return tMin >= 0f && tMin <= maxDistance ? tMin : null;
    }

    private static bool ClipRaySlab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(direction) < 0.0001f)
        {
            return origin >= min && origin <= max;
        }

        var inverse = 1f / direction;
        var t1 = (min - origin) * inverse;
        var t2 = (max - origin) * inverse;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = MathF.Max(tMin, t1);
        tMax = MathF.Min(tMax, t2);
        return tMin <= tMax;
    }

    private void PruneStates(IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        var staleSlots = new List<byte>();
        foreach (var slot in _statesBySlot.Keys)
        {
            if (!controlledSlots.ContainsKey(slot))
            {
                staleSlots.Add(slot);
            }
        }

        foreach (var slot in staleSlots)
        {
            ResetSlotState(slot);
        }

        staleSlots.Clear();
        foreach (var slot in _aliveBySlot.Keys)
        {
            if (!controlledSlots.ContainsKey(slot))
            {
                staleSlots.Add(slot);
            }
        }

        foreach (var slot in staleSlots)
        {
            ResetSlotState(slot);
        }
    }

    private void ResetSlotState(byte slot)
    {
        _statesBySlot.Remove(slot);
        _graphStatesBySlot.Remove(slot);
        _directSeekStatesBySlot.Remove(slot);
        _objectiveHoldPointBySlot.Remove(slot);
        _localObjectiveDirectPointBySlot.Remove(slot);
        _aliveBySlot.Remove(slot);
    }

    private enum ReplayPhase
    {
        Attack,
        Return,
    }

    private readonly record struct ReplayState(
        string MapKey,
        ReplayPhase Phase,
        int ActionIndex,
        int ActionTick,
        float LastX,
        float LastBottom,
        int NoProgressTicks,
        float BestGoalDistance,
        int NoGoalProgressTicks);

    private readonly record struct GraphGoal(string Label, float X, float Bottom, float Radius);

    private enum CombatTargetKind
    {
        Player,
        Sentry,
        Generator,
    }

    private readonly record struct CombatTarget(
        CombatTargetKind Kind,
        PlayerTeam Team,
        float X,
        float Y,
        PlayerEntity? Player = null,
        SentryEntity? Sentry = null,
        GeneratorState? Generator = null);

    private readonly record struct DirectSeekState(
        string GoalKey,
        float LastX,
        float LastBottom,
        float BestGoalDistance,
        int NoProgressTicks,
        int NoHorizontalProgressTicks,
        int NoGoalProgressTicks,
        int TotalTicks,
        int BackupTicksRemaining,
        int BackupDirection,
        int VerticalSearchDirection);

    private readonly record struct DropSearchCandidate(float DropDistance, float ClearRunDistance);

    private readonly record struct GraphReplayState(
        string MapKey,
        string GoalKey,
        IReadOnlyList<MotionProofAction> Actions,
        int ActionIndex,
        int ActionTick,
        int StartNodeIndex,
        int GoalNodeIndex,
        float GoalNodeX,
        float GoalNodeBottom,
        IReadOnlyList<int> PathNodeIndices,
        float LastX,
        float LastBottom,
        int NoProgressTicks);

    private readonly record struct GraphPlan(
        IReadOnlyList<MotionProofAction> Actions,
        int StartNodeIndex,
        int GoalNodeIndex,
        float GoalNodeX,
        float GoalNodeBottom,
        IReadOnlyList<int> PathNodeIndices);

    private sealed record GraphRouteMap(
        HashSet<int> GoalNodes,
        int[] CostByNode,
        MotionGraphEdge?[] NextEdgeByNode);

    private sealed class PreparedMotionGraph
    {
        private PreparedMotionGraph(
            int version,
            string identity,
            IReadOnlyList<MotionGraphNode> nodes,
            IReadOnlyDictionary<int, MotionGraphEdge[]> edgesByNode,
            IReadOnlyDictionary<int, MotionGraphEdge[]> reverseEdgesByNode)
        {
            Version = version;
            Identity = identity;
            Nodes = nodes;
            EdgesByNode = edgesByNode;
            ReverseEdgesByNode = reverseEdgesByNode;
        }

        public int Version { get; }

        public string Identity { get; }

        public IReadOnlyList<MotionGraphNode> Nodes { get; }

        public IReadOnlyDictionary<int, MotionGraphEdge[]> EdgesByNode { get; }

        public IReadOnlyDictionary<int, MotionGraphEdge[]> ReverseEdgesByNode { get; }

        public static PreparedMotionGraph From(MotionGraphArtifact artifact, string identity)
        {
            var edgesByNode = artifact.Edges
                .GroupBy(edge => edge.From)
                .ToDictionary(group => group.Key, group => group.ToArray());
            var reverseEdgesByNode = artifact.Edges
                .GroupBy(edge => edge.To)
                .ToDictionary(group => group.Key, group => group.ToArray());
            return new PreparedMotionGraph(artifact.Version, identity, artifact.Nodes, edgesByNode, reverseEdgesByNode);
        }
    }

    private sealed record MotionGraphArtifact(
        int Version,
        string Map,
        int Area,
        string Mode,
        string Team,
        string Class,
        IReadOnlyList<MotionGraphNode> Nodes,
        IReadOnlyList<MotionGraphEdge> Edges);

    private sealed record MotionGraphNode(
        int Id,
        float X,
        float Y,
        float Bottom,
        bool IsGrounded,
        float HorizontalSpeed = 0f,
        float VerticalSpeed = 0f,
        int RemainingAirJumps = 0);

    private sealed record MotionGraphEdge(
        int From,
        int To,
        int CostTicks,
        MotionProofAction Action,
        string TeamMask = "Any",
        string CarryMask = "Any");

    private sealed record MotionProofArtifact(
        int Version,
        string Map,
        int Area,
        string Mode,
        string Team,
        string Class,
        int AttackTicks,
        int ReturnTicks,
        IReadOnlyList<MotionProofAction> Attack,
        IReadOnlyList<MotionProofAction> Return);

    private sealed record MotionProofAction(string Kind, int Direction, int Ticks);
}
