using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public sealed class ObjectiveTraversalBotController : IPracticeBotController
{
    private const double DeltaSeconds = 1d / 30d;
    private const int SurfaceSampleStep = 56;
    private const int MaxBakeNodes = 760;
    private const float NodeMergeDistance = 28f;
    private const float EdgeSnapMaxDistance = 118f;
    private const float StrictEdgeSnapMaxDistance = 86f;
    private const float StrictEdgeSnapMaxVerticalDistance = 48f;
    private const float StrictJumpEdgeSnapMaxDistance = 54f;
    private const float StrictJumpEdgeSnapMaxVerticalDistance = 28f;
    private const float EdgeReachedDistance = 42f;
    private const int ReplanDistanceThreshold = 96;
    private const float IntelMarkerSize = 24f;

    private static readonly MotionPrimitive[] PrimitiveLibrary =
    [
        new("left", -1, JumpMode.None, false, 28),
        new("right", 1, JumpMode.None, false, 28),
        new("left_long", -1, JumpMode.None, false, 54),
        new("right_long", 1, JumpMode.None, false, 54),
        new("left_jump", -1, JumpMode.Takeoff, false, 48),
        new("right_jump", 1, JumpMode.Takeoff, false, 48),
        new("left_late_jump", -1, JumpMode.LateTakeoff, false, 68),
        new("right_late_jump", 1, JumpMode.LateTakeoff, false, 68),
        new("left_climb", -1, JumpMode.Climb, false, 72),
        new("right_climb", 1, JumpMode.Climb, false, 72),
        new("left_pulse", -1, JumpMode.Pulse, false, 64),
        new("right_pulse", 1, JumpMode.Pulse, false, 64),
        new("left_drop", -1, JumpMode.None, true, 44),
        new("right_drop", 1, JumpMode.None, true, 44),
        new("jump", 0, JumpMode.Takeoff, false, 42),
    ];

    private static readonly object GraphLock = new();
    private static readonly Dictionary<GraphKey, MotionGraph> GraphCache = new();

    private readonly Dictionary<byte, BotState> _states = new();
    private readonly Dictionary<byte, PlayerInputSnapshot> _inputs = new();

    public bool CollectDiagnostics { get; set; }

    public BotControllerDiagnosticsSnapshot LastDiagnostics { get; private set; } = BotControllerDiagnosticsSnapshot.Empty;

    public void Reset()
    {
        _states.Clear();
        _inputs.Clear();
        LastDiagnostics = BotControllerDiagnosticsSnapshot.Empty;
    }

    public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        _inputs.Clear();
        if (controlledSlots.Count == 0)
        {
            LastDiagnostics = BotControllerDiagnosticsSnapshot.Empty;
            return _inputs;
        }

        PruneStates(controlledSlots);
        var diagnostics = CollectDiagnostics ? new List<BotControllerDiagnosticsEntry>(controlledSlots.Count) : null;
        foreach (var controlledSlot in controlledSlots.Values)
        {
            if (!world.TryGetNetworkPlayer(controlledSlot.Slot, out var player) || !player.IsAlive)
            {
                var deadState = GetState(controlledSlot.Slot);
                _inputs[controlledSlot.Slot] = default;
                diagnostics?.Add(CreateDiagnostics(controlledSlot, player, deadState, "primitive_respawn", (0f, 0f), 0, false, "dead"));
                continue;
            }

            var state = GetState(controlledSlot.Slot);
            var objective = ResolveObjective(world, player, controlledSlot.Team);
            var input = BuildInput(world, controlledSlot, player, objective, state, out var label, out var horizontal, out var jump, out var issue);
            _inputs[controlledSlot.Slot] = input;
            diagnostics?.Add(CreateDiagnostics(controlledSlot, player, state, label, objective.Target, horizontal, jump, issue));
        }

        LastDiagnostics = diagnostics is null
            ? BotControllerDiagnosticsSnapshot.Empty
            : new BotControllerDiagnosticsSnapshot(diagnostics, diagnostics.Count, 0, 0, 0, 0);
        return _inputs;
    }

    private static PlayerInputSnapshot BuildInput(
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PlayerEntity player,
        ObjectivePoint objective,
        BotState state,
        out string label,
        out int horizontal,
        out bool jump,
        out string issue)
    {
        if (objective.IsSatisfied(world, player))
        {
            state.ResetRoute();
            horizontal = 0;
            jump = false;
            issue = string.Empty;
            label = "primitive_hold";
            return CreateInput(objective.Target, horizontal, jump, down: false);
        }

        var graph = GetOrBuildGraph(world.Level, controlledSlot.Team, controlledSlot.ClassId, player.IsCarryingIntel, strictEdges: !objective.IsCtfObjective);
        var objectiveChanged = state.ObjectiveKind != objective.Kind
            || Distance(state.ObjectiveX, state.ObjectiveY, objective.Target.X, objective.Target.Y) > ReplanDistanceThreshold
            || state.CarryingIntel != player.IsCarryingIntel;
        if (objectiveChanged)
        {
            state.Route.Clear();
            state.RouteStepIndex = 0;
            state.ActiveFromNodeId = -1;
            state.ActiveToNodeId = -1;
        }

        if (objectiveChanged || state.NoProgressTicks > 150)
        {
            state.RouteTicksRemaining = 0;
            state.ActivePrimitiveTicksRemaining = 0;
            state.ObjectiveKind = objective.Kind;
            state.ObjectiveX = objective.Target.X;
            state.ObjectiveY = objective.Target.Y;
            state.CarryingIntel = player.IsCarryingIntel;
            state.NoProgressTicks = 0;
            state.BestDistance = float.PositiveInfinity;
        }

        if (!objective.IsCtfObjective
            && MathF.Abs(objective.Target.X - player.X) <= 900f
            && MathF.Abs(objective.Target.Y - player.Bottom) <= 260f)
        {
            horizontal = Math.Sign(objective.Target.X - player.X);
            jump = objective.Target.Y < player.Bottom - 42f && player.IsGrounded;
            label = "primitive_local_objective";
            issue = string.Empty;
            return CreateInput(objective.Target, horizontal, ResolveJumpOutput(state, jump, objective.IsCtfObjective), down: false);
        }

        var distance = Distance(player.X, player.Bottom, objective.Target.X, objective.Target.Y);
        if (distance + 8f < state.BestDistance)
        {
            state.BestDistance = distance;
            state.NoProgressTicks = 0;
        }
        else
        {
            state.NoProgressTicks += 1;
        }

        if (objective.Kind == "ctf_home" && state.NoProgressTicks > 45)
        {
            horizontal = Math.Sign(objective.Target.X - player.X);
            var drop = MathF.Abs(objective.Target.X - player.X) < 900f && objective.Target.Y > player.Bottom + 96f;
            if (drop && player.IsGrounded && MathF.Abs(player.HorizontalSpeed) < 1f && horizontal != 0)
            {
                horizontal = -horizontal;
            }

            jump = !drop
                && player.IsGrounded
                && (objective.Target.Y < player.Bottom - 36f
                    || MathF.Abs(player.HorizontalSpeed) < 1f
                    || state.NoProgressTicks % 24 < 8);
            label = "primitive_return_recover";
            issue = state.NoProgressTicks > 90 ? "primitive_no_progress" : string.Empty;
            return CreateInput(objective.Target, horizontal, ResolveJumpOutput(state, jump, objective.IsCtfObjective), drop);
        }

        if ((!objective.IsCtfObjective || objective.Kind == "ctf_home")
            && state.RouteTicksRemaining > 0
            && state.ActiveToNodeId >= 0
            && player.IsGrounded
            && IsAtActiveRouteTarget(objective, player, state))
        {
            state.RouteTicksRemaining = 0;
        }

        if (state.RouteTicksRemaining <= 0)
        {
            if (state.Route.Count > 0
                && state.RouteStepIndex + 1 < state.Route.Count
                && CanAdvanceRouteStep(objective, player, state))
            {
                state.RouteStepIndex += 1;
                ActivateRouteEdge(graph, state, state.Route[state.RouteStepIndex]);
            }
            else
            {
                state.Route = graph.FindRoute(player.X, player.Bottom, objective);
                state.RouteStepIndex = 0;
                if (state.Route.Count > 0)
                {
                    ActivateRouteEdge(graph, state, state.Route[0]);
                }
                else
                {
                    state.ActiveFromNodeId = -1;
                    state.ActiveToNodeId = -1;
                    state.ActiveTargetX = objective.Target.X;
                    state.ActiveTargetY = objective.Target.Y;
                    state.EdgeSettleTicks = 0;
                }
            }
        }

        if (state.RouteTicksRemaining > 0)
        {
            var primitive = state.ActivePrimitive;
            if ((!objective.IsCtfObjective || objective.Kind == "ctf_home")
                && player.IsGrounded
                && state.ActivePrimitiveTicksRemaining == primitive.Ticks
                && TryBuildEdgePrealignInput(
                    graph,
                    player,
                    state,
                    primitive,
                    objective.Target,
                    allowOppositeHorizontalAlign: !objective.IsCtfObjective,
                    out var prealignInput,
                    out var prealignLabel,
                    out var prealignHorizontal))
            {
                horizontal = prealignHorizontal;
                jump = false;
                label = prealignLabel;
                issue = string.Empty;
                return prealignInput;
            }

            horizontal = primitive.Horizontal;
            var isPrimitivePhase = state.ActivePrimitiveTicksRemaining > 0;
            jump = isPrimitivePhase && WantsJump(primitive.JumpMode, state.ActivePrimitiveTicksRemaining);
            var down = primitive.Down;
            state.RouteTicksRemaining -= 1;
            if (state.ActivePrimitiveTicksRemaining > 0)
            {
                state.ActivePrimitiveTicksRemaining -= 1;
            }

            state.EdgeSettleTicks = 0;
            label = isPrimitivePhase
                ? $"primitive_{primitive.Name}"
                : $"primitive_{primitive.Name}_settle";
            issue = state.NoProgressTicks > 90 ? "primitive_no_progress" : string.Empty;
            return CreateInput(objective.Target, horizontal, ResolveJumpOutput(state, jump, objective.IsCtfObjective), down);
        }

        horizontal = Math.Sign(objective.Target.X - player.X);
        jump = objective.Target.Y < player.Bottom - 48f && (player.IsGrounded || state.NoProgressTicks > 30);
        label = "primitive_direct";
        issue = "no_cached_route";
        return CreateInput(objective.Target, horizontal, ResolveJumpOutput(state, jump, objective.IsCtfObjective), down: false);
    }

    private static void ActivateRouteEdge(MotionGraph graph, BotState state, GraphEdge activeEdge)
    {
        state.ActivePrimitive = activeEdge.Primitive;
        state.ActiveFromNodeId = activeEdge.From;
        state.ActiveToNodeId = activeEdge.To;
        state.ActiveTargetX = graph.GetNodeX(activeEdge.To);
        state.ActiveTargetY = graph.GetNodeBottom(activeEdge.To);
        state.RouteTicksRemaining = activeEdge.Ticks;
        state.ActivePrimitiveTicksRemaining = state.ActivePrimitive.Ticks;
        state.EdgeSettleTicks = 0;
    }

    private static bool CanAdvanceRouteStep(ObjectivePoint objective, PlayerEntity player, BotState state)
    {
        if (!player.IsGrounded)
        {
            return false;
        }

        var distance = Distance(player.X, player.Bottom, state.ActiveTargetX, state.ActiveTargetY);
        if (!objective.IsCtfObjective)
        {
            return distance <= EdgeSnapMaxDistance;
        }

        return distance <= 96f && MathF.Abs(player.Bottom - state.ActiveTargetY) <= 64f;
    }

    private static bool IsAtActiveRouteTarget(ObjectivePoint objective, PlayerEntity player, BotState state)
    {
        var distance = Distance(player.X, player.Bottom, state.ActiveTargetX, state.ActiveTargetY);
        if (!objective.IsCtfObjective)
        {
            return distance <= EdgeReachedDistance;
        }

        return distance <= 56f && MathF.Abs(player.Bottom - state.ActiveTargetY) <= 36f;
    }

    private static bool TryBuildEdgePrealignInput(
        MotionGraph graph,
        PlayerEntity player,
        BotState state,
        MotionPrimitive primitive,
        (float X, float Y) target,
        bool allowOppositeHorizontalAlign,
        out PlayerInputSnapshot input,
        out string label,
        out int horizontal)
    {
        input = default;
        label = string.Empty;
        horizontal = 0;
        if (state.EdgeSettleTicks >= 60 || state.ActiveFromNodeId < 0)
        {
            return false;
        }

        var fromX = graph.GetNodeX(state.ActiveFromNodeId);
        var fromY = graph.GetNodeBottom(state.ActiveFromNodeId);
        var targetDistance = Distance(player.X, player.Bottom, state.ActiveTargetX, state.ActiveTargetY);
        var fromDistance = Distance(player.X, player.Bottom, fromX, fromY);
        if (primitive.JumpMode != JumpMode.None && targetDistance > EdgeReachedDistance && fromDistance > 34f)
        {
            horizontal = Math.Sign(fromX - player.X);
            if (!allowOppositeHorizontalAlign && primitive.Horizontal != 0 && horizontal != primitive.Horizontal)
            {
                return false;
            }

            state.EdgeSettleTicks += 1;
            label = "primitive_align";
            input = CreateInput(target, horizontal, jump: false, down: false);
            return horizontal != 0;
        }

        if (primitive.JumpMode != JumpMode.None && state.EdgeSettleTicks < 8)
        {
            state.EdgeSettleTicks += 1;
            label = "primitive_settle";
            input = CreateInput(target, horizontal: 0, jump: false, down: false);
            return true;
        }

        if (primitive.JumpMode == JumpMode.None && primitive.Horizontal != 0 && (player.HorizontalSpeed * primitive.Horizontal) < -6f)
        {
            horizontal = primitive.Horizontal;
            state.EdgeSettleTicks += 1;
            label = "primitive_prime";
            input = CreateInput(target, horizontal, jump: false, down: false);
            return true;
        }

        return false;
    }

    private static MotionGraph GetOrBuildGraph(SimpleLevel level, PlayerTeam team, PlayerClass classId, bool carryingIntel, bool strictEdges)
    {
        var key = new GraphKey(level.Name, level.Bounds.Width, level.Bounds.Height, level.Solids.Count, team, classId, carryingIntel, strictEdges);
        lock (GraphLock)
        {
            if (!GraphCache.TryGetValue(key, out var graph))
            {
                graph = MotionGraph.Bake(level, team, classId, carryingIntel, strictEdges);
                GraphCache[key] = graph;
            }

            return graph;
        }
    }

    private static bool WantsJump(JumpMode mode, int ticksRemaining)
    {
        return mode switch
        {
            JumpMode.None => false,
            JumpMode.Takeoff => ticksRemaining >= 40,
            JumpMode.LateTakeoff => ticksRemaining is <= 50 and >= 43,
            JumpMode.Pulse => ticksRemaining % 14 is >= 10,
            JumpMode.Climb => ticksRemaining >= 58 || ticksRemaining % 18 is >= 10,
            _ => false,
        };
    }

    private static bool ResolveJumpOutput(BotState state, bool wantsJump, bool pulseJump)
    {
        if (!pulseJump)
        {
            state.JumpReleaseTicks = 0;
            state.LastJumpOutput = wantsJump;
            return wantsJump;
        }

        if (!wantsJump)
        {
            state.JumpReleaseTicks = 0;
            state.LastJumpOutput = false;
            return false;
        }

        if (state.JumpReleaseTicks > 0)
        {
            state.JumpReleaseTicks -= 1;
            state.LastJumpOutput = false;
            return false;
        }

        if (state.LastJumpOutput)
        {
            state.JumpReleaseTicks = 1;
            state.LastJumpOutput = false;
            return false;
        }

        state.LastJumpOutput = true;
        return true;
    }

    private static ObjectivePoint ResolveObjective(SimulationWorld world, PlayerEntity player, PlayerTeam team)
    {
        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            if (player.IsCarryingIntel && world.Level.GetIntelBase(team) is { } ownBase)
            {
                return ObjectivePoint.IntelHome(ownBase.X, ownBase.Y);
            }

            var enemyIntel = team == PlayerTeam.Blue ? world.RedIntel : world.BlueIntel;
            return ObjectivePoint.IntelAttack(enemyIntel.X, enemyIntel.Y);
        }

        var point = ResolveCapturePoint(world, team);
        if (point is null)
        {
            return ObjectivePoint.Point(world.Level.Bounds.Width * 0.5f, world.Level.Bounds.Height * 0.5f, "center", -1, []);
        }

        var zones = ResolveCaptureZones(world.Level, point.Marker);
        (float X, float Y) target = zones.Length > 0
            ? SelectNearestZone(player, zones)
            : (point.Marker.CenterX, point.Marker.CenterY);
        return ObjectivePoint.Point(target.X, target.Y, $"cp_{point.Index}", point.Index, zones);
    }

    private static ControlPointState? ResolveCapturePoint(SimulationWorld world, PlayerTeam team)
    {
        if (world.ControlPoints.Count == 0)
        {
            return null;
        }

        if (world.MatchRules.Mode == GameModeKind.KingOfTheHill)
        {
            return world.ControlPoints.FirstOrDefault(point => point.Marker.IsSingleKothControlPoint()) ?? world.ControlPoints[0];
        }

        if (world.MatchRules.Mode == GameModeKind.DoubleKingOfTheHill)
        {
            var enemyHome = team == PlayerTeam.Blue
                ? world.ControlPoints.FirstOrDefault(point => point.Marker.IsRedKothControlPoint())
                : world.ControlPoints.FirstOrDefault(point => point.Marker.IsBlueKothControlPoint());
            return enemyHome ?? world.ControlPoints[0];
        }

        return world.ControlPoints
            .Where(point => !point.IsLocked && point.Team != team)
            .OrderBy(point => point.Index)
            .FirstOrDefault()
            ?? world.ControlPoints[0];
    }

    private static CaptureZone[] ResolveCaptureZones(SimpleLevel level, RoomObjectMarker controlPointMarker)
    {
        var zones = level.GetRoomObjects(RoomObjectType.CaptureZone);
        if (zones.Count == 0)
        {
            return [];
        }

        var result = new List<CaptureZone>();
        for (var index = 0; index < zones.Count; index += 1)
        {
            var zone = zones[index];
            if (Distance(zone.CenterX, zone.CenterY, controlPointMarker.CenterX, controlPointMarker.CenterY) <= 420f)
            {
                result.Add(new CaptureZone(zone.CenterX, zone.CenterY, zone.Width, zone.Height));
            }
        }

        if (result.Count == 0)
        {
            var bestZone = zones
                .OrderBy(zone => Distance(zone.CenterX, zone.CenterY, controlPointMarker.CenterX, controlPointMarker.CenterY))
                .First();
            result.Add(new CaptureZone(bestZone.CenterX, bestZone.CenterY, bestZone.Width, bestZone.Height));
        }

        return result.ToArray();
    }

    private static (float X, float Y) SelectNearestZone(PlayerEntity player, IReadOnlyList<CaptureZone> zones)
    {
        var best = zones[0];
        var bestDistance = float.PositiveInfinity;
        for (var index = 0; index < zones.Count; index += 1)
        {
            var zone = zones[index];
            var distance = Distance(player.X, player.Bottom, zone.X, zone.Y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = zone;
            }
        }

        return (best.X, best.Y);
    }

    private static PlayerInputSnapshot CreateInput((float X, float Y) target, int horizontal, bool jump, bool down)
    {
        return new PlayerInputSnapshot(
            Left: horizontal < 0,
            Right: horizontal > 0,
            Up: jump,
            Down: down,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: target.X,
            AimWorldY: target.Y,
            DebugKill: false);
    }

    private static PlayerEntity CreateProbe(
        PlayerTeam team,
        PlayerClass classId,
        float x,
        float y,
        bool grounded,
        bool carryingIntel,
        string name)
    {
        var definition = CharacterClassCatalog.GetDefinition(classId);
        var player = new PlayerEntity(-4000, definition, name);
        player.ApplyNetworkState(
            team,
            definition,
            isAlive: true,
            x,
            y,
            horizontalSpeed: 0f,
            verticalSpeed: 0f,
            health: definition.MaxHealth,
            currentShells: 0,
            kills: 0,
            deaths: 0,
            caps: 0,
            points: 0,
            healPoints: 0,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: 100,
            isGrounded: grounded,
            remainingAirJumps: definition.MaxAirJumps,
            isCarryingIntel: carryingIntel,
            intelRechargeTicks: 0,
            isSpyCloaked: false,
            spyCloakAlpha: 1f,
            isUbered: false,
            isHeavyEating: false,
            heavyEatTicksRemaining: 0,
            isSniperScoped: false,
            sniperChargeTicks: 0,
            facingDirectionX: 1,
            aimDirectionDegrees: 0f,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            playerScale: 1f);
        return player;
    }

    private static PlayerEntity CloneProbe(PlayerEntity source, PlayerClass classId, string name)
    {
        var definition = CharacterClassCatalog.GetDefinition(classId);
        var clone = new PlayerEntity(-4000, definition, name);
        clone.ApplyNetworkState(
            source.Team,
            definition,
            source.IsAlive,
            source.X,
            source.Y,
            source.HorizontalSpeed,
            source.VerticalSpeed,
            source.Health,
            source.CurrentShells,
            source.Kills,
            source.Deaths,
            source.Caps,
            source.Points,
            source.HealPoints,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            source.Metal,
            source.IsGrounded,
            source.RemainingAirJumps,
            source.IsCarryingIntel,
            source.IntelRechargeTicks,
            source.IsSpyCloaked,
            source.SpyCloakAlpha,
            source.IsUbered,
            source.IsHeavyEating,
            source.HeavyEatTicksRemaining,
            source.IsSniperScoped,
            source.SniperChargeTicks,
            source.FacingDirectionX,
            source.AimDirectionDegrees,
            source.IsTaunting,
            tauntFrameIndex: 0f,
            source.IsChatBubbleVisible,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f,
            playerScale: source.PlayerScale);
        return clone;
    }

    private static BotControllerDiagnosticsEntry CreateDiagnostics(
        ControlledBotSlot controlledSlot,
        PlayerEntity player,
        BotState state,
        string label,
        (float X, float Y) target,
        int horizontal,
        bool jump,
        string issue)
    {
        return new BotControllerDiagnosticsEntry(
            controlledSlot.Slot,
            DisplayName: string.Empty,
            controlledSlot.Team,
            controlledSlot.ClassId,
            BotRole.AttackObjective,
            BotStateKind.TravelObjective,
            BotFocusKind.Objective,
            "objective",
            label,
            HasVisibleEnemy: false,
            player?.Health ?? 0,
            player?.MaxHealth ?? 0,
            StuckTicks: 0,
            ModernStuckTicks: 0,
            UnstickTicks: 0,
            CurrentPointId: state.ActiveFromNodeId,
            NextPointId: state.ActiveToNodeId,
            NextPoint2Id: -1,
            target.X,
            target.Y,
            horizontal,
            horizontal < 0 ? "primitive_left" : horizontal > 0 ? "primitive_right" : "primitive_stop",
            jump,
            jump ? "primitive_jump" : "primitive_no_jump",
            RouteGoalNodeId: state.ActiveToNodeId,
            RouteGoalX: state.ActiveTargetX,
            RouteGoalY: state.ActiveTargetY,
            PreviousCurrentPointId: -1,
            PreviousNextPointId: -1,
            IsGrounded: player?.IsGrounded ?? false,
            ProbeGrounded: player?.IsGrounded ?? false,
            SecondAnchorBlockPointId: -1,
            SecondAnchorBlockTicksRemaining: 0,
            NoNextPointTicks: 0,
            FallbackRouteLabel: string.Empty,
            FallbackTriggerLabel: string.Empty,
            NavigationIssueLabel: issue,
            BranchFromPointId: -1,
            BranchToPointId: -1,
            BranchTicks: 0,
            BranchNoProgressTicks: 0,
            DirectTargetTicks: 0,
            DirectTargetNoProgressTicks: 0);
    }

    private BotState GetState(byte slot)
    {
        if (_states.TryGetValue(slot, out var state))
        {
            return state;
        }

        state = new BotState();
        _states[slot] = state;
        return state;
    }

    private void PruneStates(IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        foreach (var slot in _states.Keys.ToArray())
        {
            if (!controlledSlots.ContainsKey(slot))
            {
                _states.Remove(slot);
            }
        }
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private sealed class MotionGraph
    {
        private readonly PlayerTeam _team;
        private readonly PlayerClass _classId;
        private readonly bool _carryingIntel;
        private readonly bool _strictEdges;
        private readonly List<GraphNode> _nodes = [];
        private readonly Dictionary<int, List<GraphEdge>> _edgesByNode = new();

        private MotionGraph(PlayerTeam team, PlayerClass classId, bool carryingIntel, bool strictEdges)
        {
            _team = team;
            _classId = classId;
            _carryingIntel = carryingIntel;
            _strictEdges = strictEdges;
        }

        public static MotionGraph Bake(SimpleLevel level, PlayerTeam team, PlayerClass classId, bool carryingIntel, bool strictEdges)
        {
            var graph = new MotionGraph(team, classId, carryingIntel, strictEdges);
            graph.SampleNodes(level);
            graph.BuildEdges(level);
            graph.DumpDebug(level);
            return graph;
        }

        public List<GraphEdge> FindRoute(float startX, float startBottom, ObjectivePoint objective)
        {
            if (_nodes.Count == 0)
            {
                return [];
            }

            var goals = FindGoalNodes(objective);
            if (goals.Count == 0)
            {
                return [];
            }

            var goalSet = goals.ToHashSet();
            var startCandidates = FindStartCandidates(startX, startBottom, objective.IsCtfObjective);
            RouteSearchResult? bestComplete = null;
            RouteSearchResult? bestPartial = null;
            for (var candidateIndex = 0; candidateIndex < startCandidates.Count; candidateIndex += 1)
            {
                var start = startCandidates[candidateIndex];
                var result = FindRouteFromStart(start, objective, goalSet);
                if (result.Route.Count == 0)
                {
                    continue;
                }

                var startPenalty = Distance(startX, startBottom, _nodes[start].X, _nodes[start].Bottom) * 4f;
                result = result with { Cost = result.Cost + startPenalty };
                if (result.Complete)
                {
                    if (bestComplete is null || result.Cost < bestComplete.Value.Cost)
                    {
                        bestComplete = result;
                    }
                }
                else if (!objective.IsCtfObjective
                    && (bestPartial is null || result.Heuristic < bestPartial.Value.Heuristic))
                {
                    bestPartial = result;
                }
            }

            if (bestComplete is { } complete)
            {
                return complete.Route;
            }

            if (!objective.IsCtfObjective && bestPartial is { } partial)
            {
                return partial.Route;
            }

            return [];
        }

        private RouteSearchResult FindRouteFromStart(int start, ObjectivePoint objective, HashSet<int> goalSet)
        {
            var open = new PriorityQueue<int, float>();
            var cameFrom = new Dictionary<int, (int Previous, GraphEdge Edge)>();
            var gScore = new Dictionary<int, float> { [start] = 0f };
            var bestPartial = start;
            var bestPartialHeuristic = GoalHeuristic(_nodes[start], objective);
            open.Enqueue(start, 0f);

            while (open.Count > 0)
            {
                var current = open.Dequeue();
                var currentHeuristic = GoalHeuristic(_nodes[current], objective);
                if (currentHeuristic < bestPartialHeuristic)
                {
                    bestPartialHeuristic = currentHeuristic;
                    bestPartial = current;
                }

                if (goalSet.Contains(current))
                {
                    return new RouteSearchResult(ReconstructRoute(current, cameFrom), Complete: true, gScore[current], currentHeuristic);
                }

                if (!_edgesByNode.TryGetValue(current, out var edges))
                {
                    continue;
                }

                for (var index = 0; index < edges.Count; index += 1)
                {
                    var edge = edges[index];
                    var nextScore = gScore[current] + edge.Cost + EdgeObjectivePenalty(_nodes[current], _nodes[edge.To], objective);
                    if (gScore.TryGetValue(edge.To, out var knownScore) && nextScore >= knownScore)
                    {
                        continue;
                    }

                    gScore[edge.To] = nextScore;
                    cameFrom[edge.To] = (current, edge);
                    var priority = nextScore + GoalHeuristic(_nodes[edge.To], objective);
                    open.Enqueue(edge.To, priority);
                }
            }

            if (!objective.IsCtfObjective && bestPartial != start)
            {
                return new RouteSearchResult(ReconstructRoute(bestPartial, cameFrom), Complete: false, gScore.GetValueOrDefault(bestPartial, 0f), bestPartialHeuristic);
            }

            return new RouteSearchResult([], Complete: false, 0f, bestPartialHeuristic);
        }

        public float GetNodeX(int id)
        {
            return id >= 0 && id < _nodes.Count ? _nodes[id].X : 0f;
        }

        public float GetNodeBottom(int id)
        {
            return id >= 0 && id < _nodes.Count ? _nodes[id].Bottom : 0f;
        }

        private void SampleNodes(SimpleLevel level)
        {
            var definition = CharacterClassCatalog.GetDefinition(_classId);
            foreach (var spawn in level.BlueSpawns.Concat(level.RedSpawns))
            {
                TryAddStandNear(level, spawn.X, spawn.Y, definition);
            }

            foreach (var intel in level.IntelBases)
            {
                TryAddObjectiveMarkerNodes(level, intel.X, intel.Y, IntelMarkerSize, IntelMarkerSize, definition);
            }

            foreach (var marker in level.RoomObjects)
            {
                if (marker.Type is RoomObjectType.ControlPoint or RoomObjectType.CaptureZone)
                {
                    TryAddObjectiveMarkerNodes(level, marker.CenterX, marker.CenterY, marker.Width, marker.Height, definition);
                }
            }

            foreach (var solid in level.Solids.OrderBy(solid => solid.Top).ThenBy(solid => solid.Left))
            {
                SampleSurface(level, definition, solid.Left + 12f, solid.Right - 12f, solid.Top);
            }

            foreach (var platform in level.GetRoomObjects(RoomObjectType.DropdownPlatform))
            {
                SampleSurface(level, definition, platform.Left + 12f, platform.Right - 12f, platform.Top);
            }
        }

        private void TryAddObjectiveMarkerNodes(
            SimpleLevel level,
            float markerX,
            float markerY,
            float markerWidth,
            float markerHeight,
            CharacterClassDefinition definition)
        {
            var markerLeft = markerX - (markerWidth * 0.5f);
            var markerRight = markerX + (markerWidth * 0.5f);
            var minIntersectingX = markerLeft - definition.CollisionRight + 2f;
            var maxIntersectingX = markerRight - definition.CollisionLeft - 2f;
            var centerX = (minIntersectingX + maxIntersectingX) * 0.5f;

            for (var x = minIntersectingX; x <= maxIntersectingX; x += 8f)
            {
                TryAddStandNear(level, x, markerY, definition);
            }

            TryAddStandNear(level, centerX, markerY, definition);
            TryAddStandNear(level, markerX, markerY, definition);
        }

        private void SampleSurface(SimpleLevel level, CharacterClassDefinition definition, float left, float right, float surfaceTop)
        {
            if (_nodes.Count >= MaxBakeNodes || right < left)
            {
                return;
            }

            for (var x = left; x <= right && _nodes.Count < MaxBakeNodes; x += SurfaceSampleStep)
            {
                TryAddNode(level, x, surfaceTop - definition.CollisionBottom, definition);
            }

            TryAddNode(level, right, surfaceTop - definition.CollisionBottom, definition);
        }

        private void TryAddStandNear(SimpleLevel level, float x, float y, CharacterClassDefinition definition)
        {
            for (var offset = -180f; offset <= 280f; offset += 8f)
            {
                if (TryAddNode(level, x, y + offset - definition.CollisionBottom, definition))
                {
                    return;
                }
            }
        }

        private bool TryAddNode(SimpleLevel level, float x, float y, CharacterClassDefinition definition)
        {
            if (_nodes.Count >= MaxBakeNodes)
            {
                return false;
            }

            if (x < -definition.CollisionLeft || x > level.Bounds.Width - definition.CollisionRight)
            {
                return false;
            }

            var probe = CreateProbe(_team, _classId, x, y, grounded: true, _carryingIntel, "primitive-node");
            if (!probe.CanOccupy(level, _team, x, y) || probe.CanOccupy(level, _team, x, y + 1f))
            {
                return false;
            }

            for (var index = 0; index < _nodes.Count; index += 1)
            {
                var node = _nodes[index];
                if (Distance(node.X, node.Bottom, x, probe.Bottom) <= NodeMergeDistance)
                {
                    return true;
                }
            }

            _nodes.Add(new GraphNode(_nodes.Count, x, probe.Bottom));
            return true;
        }

        private void BuildEdges(SimpleLevel level)
        {
            for (var nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex += 1)
            {
                var edges = new List<GraphEdge>();
                for (var primitiveIndex = 0; primitiveIndex < PrimitiveLibrary.Length; primitiveIndex += 1)
                {
                    var primitive = PrimitiveLibrary[primitiveIndex];
                    if (!_strictEdges && primitive.JumpMode == JumpMode.LateTakeoff)
                    {
                        continue;
                    }

                    if (TrySimulateEdge(level, _nodes[nodeIndex], primitive, out var edge))
                    {
                        edges.Add(edge);
                    }
                }

                if (edges.Count > 0)
                {
                    _edgesByNode[nodeIndex] = edges;
                }
            }
        }

        private void DumpDebug(SimpleLevel level)
        {
            var debugMap = Environment.GetEnvironmentVariable("OG_OBJECTIVE_TRAVERSAL_DUMP");
            if (string.IsNullOrWhiteSpace(debugMap) || !level.Name.Equals(debugMap, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var edgeCount = _edgesByNode.Values.Sum(static edges => edges.Count);
            Console.WriteLine($"objective-traversal-dump map={level.Name} team={_team} class={_classId} carrying={_carryingIntel} nodes={_nodes.Count} edges={edgeCount} budget={MaxBakeNodes}");
            foreach (var node in _nodes.Where(static node => node.X >= 1200f && node.X <= 3700f && node.Bottom >= 900f && node.Bottom <= 1700f).OrderBy(static node => node.Bottom).ThenBy(static node => node.X))
            {
                var edgeSummary = _edgesByNode.TryGetValue(node.Id, out var edges)
                    ? string.Join(",", edges.Select(edge => $"{edge.Primitive.Name}->{edge.To}@({_nodes[edge.To].X:0},{_nodes[edge.To].Bottom:0})"))
                    : string.Empty;
                Console.WriteLine($"  node {node.Id}@({node.X:0},{node.Bottom:0}) edges=[{edgeSummary}]");
            }
        }

        private bool TrySimulateEdge(SimpleLevel level, GraphNode source, MotionPrimitive primitive, out GraphEdge edge)
        {
            edge = default;
            var definition = CharacterClassCatalog.GetDefinition(_classId);
            var player = CreateProbe(_team, _classId, source.X, source.Bottom - definition.CollisionBottom, grounded: true, _carryingIntel, "primitive-edge");
            var previousUp = false;
            for (var tick = 0; tick < primitive.Ticks; tick += 1)
            {
                var jump = WantsJump(primitive.JumpMode, primitive.Ticks - tick);
                var input = CreateInput((source.X, source.Bottom), primitive.Horizontal, jump, primitive.Down);
                player.Advance(input, input.Up && !previousUp, level, _team, DeltaSeconds);
                previousUp = input.Up;
            }

            var settleTicks = 0;
            for (var settle = 0; settle < 38 && !player.IsGrounded; settle += 1)
            {
                var input = CreateInput((source.X, source.Bottom), primitive.Horizontal, jump: false, primitive.Down);
                player.Advance(input, jumpPressed: false, level, _team, DeltaSeconds);
                settleTicks += 1;
            }

            if (!player.IsGrounded)
            {
                return false;
            }

            var isJumpEdge = primitive.JumpMode != JumpMode.None;
            var target = _strictEdges && isJumpEdge
                ? FindNearestNode(player.X, player.Bottom, StrictJumpEdgeSnapMaxDistance, StrictJumpEdgeSnapMaxVerticalDistance)
                : _strictEdges
                    ? FindNearestNode(player.X, player.Bottom, StrictEdgeSnapMaxDistance, StrictEdgeSnapMaxVerticalDistance)
                    : FindNearestNode(player.X, player.Bottom, EdgeSnapMaxDistance);
            if (target < 0 || target == source.Id)
            {
                return false;
            }

            var distance = Distance(source.X, source.Bottom, _nodes[target].X, _nodes[target].Bottom);
            if (_strictEdges
                && primitive.JumpMode == JumpMode.Takeoff
                && _nodes[target].Bottom < source.Bottom - 80f)
            {
                return false;
            }

            if (distance < 18f)
            {
                return false;
            }

            var totalTicks = _strictEdges ? primitive.Ticks + settleTicks : primitive.Ticks;
            var cost = totalTicks + (distance * 0.12f);
            if (primitive.JumpMode == JumpMode.LateTakeoff)
            {
                cost += 32f;
            }

            edge = new GraphEdge(source.Id, target, primitive, totalTicks, cost);
            return true;
        }

        private int FindNearestNode(float x, float bottom, float maxDistance = float.PositiveInfinity, float maxVerticalDistance = float.PositiveInfinity)
        {
            var best = -1;
            var bestDistance = maxDistance;
            for (var index = 0; index < _nodes.Count; index += 1)
            {
                var node = _nodes[index];
                if (MathF.Abs(node.Bottom - bottom) > maxVerticalDistance)
                {
                    continue;
                }

                var distance = Distance(x, bottom, node.X, node.Bottom);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = index;
                }
            }

            return best;
        }

        private List<int> FindStartCandidates(float x, float bottom, bool strictStart)
        {
            var maxDistance = strictStart ? 92f : 160f;
            var maxVertical = strictStart ? 64f : 112f;
            var maxCandidates = strictStart ? 6 : 10;
            var candidates = _nodes
                .Select(node => (node.Id, Distance: Distance(x, bottom, node.X, node.Bottom), Vertical: MathF.Abs(node.Bottom - bottom)))
                .Where(candidate => candidate.Distance <= maxDistance && candidate.Vertical <= maxVertical)
                .OrderBy(candidate => candidate.Distance)
                .Take(maxCandidates)
                .Select(candidate => candidate.Id)
                .ToList();

            if (!strictStart && candidates.Count == 0)
            {
                var nearest = FindNearestNode(x, bottom);
                if (nearest >= 0)
                {
                    candidates.Add(nearest);
                }
            }

            return candidates;
        }

        private List<int> FindGoalNodes(ObjectivePoint objective)
        {
            var goals = new List<int>();
            for (var index = 0; index < _nodes.Count; index += 1)
            {
                var node = _nodes[index];
                if (objective.IsCtfObjective
                    ? NodeIntersectsMarker(node, objective.Target.X, objective.Target.Y, IntelMarkerSize, IntelMarkerSize)
                    : objective.ContainsNode(node.X, node.Bottom)
                        || Distance(node.X, node.Bottom, objective.Target.X, objective.Target.Y) <= 92f)
                {
                    goals.Add(index);
                }
            }

            if (goals.Count == 0 && !objective.IsCtfObjective)
            {
                var nearest = FindNearestNode(objective.Target.X, objective.Target.Y);
                if (nearest >= 0)
                {
                    goals.Add(nearest);
                }
            }

            return goals;
        }

        private bool NodeIntersectsMarker(GraphNode node, float markerX, float markerY, float markerWidth, float markerHeight)
        {
            var definition = CharacterClassCatalog.GetDefinition(_classId);
            var y = node.Bottom - definition.CollisionBottom;
            var left = node.X + definition.CollisionLeft;
            var right = node.X + definition.CollisionRight;
            var top = y + definition.CollisionTop;
            var bottom = node.Bottom;
            var markerLeft = markerX - (markerWidth * 0.5f);
            var markerRight = markerX + (markerWidth * 0.5f);
            var markerTop = markerY - (markerHeight * 0.5f);
            var markerBottom = markerY + (markerHeight * 0.5f);

            return left < markerRight
                && right > markerLeft
                && top < markerBottom
                && bottom > markerTop;
        }

        private static float GoalHeuristic(GraphNode node, ObjectivePoint objective)
        {
            return Distance(node.X, node.Bottom, objective.Target.X, objective.Target.Y) * 0.18f;
        }

        private static float EdgeObjectivePenalty(GraphNode from, GraphNode to, ObjectivePoint objective)
        {
            var fromXDistance = MathF.Abs(objective.Target.X - from.X);
            var toXDistance = MathF.Abs(objective.Target.X - to.X);
            var awayX = MathF.Max(0f, toXDistance - fromXDistance);
            if (awayX <= 0f)
            {
                return 0f;
            }

            if (objective.Kind == "ctf_home")
            {
                return awayX * 6f;
            }

            return objective.IsCtfObjective ? 0f : awayX * 0.25f;
        }

        private static List<GraphEdge> ReconstructRoute(int goal, Dictionary<int, (int Previous, GraphEdge Edge)> cameFrom)
        {
            var route = new List<GraphEdge>();
            var current = goal;
            while (cameFrom.TryGetValue(current, out var step))
            {
                route.Add(step.Edge);
                current = step.Previous;
            }

            route.Reverse();
            return route;
        }
    }

    private sealed class BotState
    {
        public string ObjectiveKind { get; set; } = string.Empty;
        public float ObjectiveX { get; set; }
        public float ObjectiveY { get; set; }
        public bool CarryingIntel { get; set; }
        public List<GraphEdge> Route { get; set; } = [];
        public int RouteStepIndex { get; set; }
        public int RouteTicksRemaining { get; set; }
        public int ActivePrimitiveTicksRemaining { get; set; }
        public MotionPrimitive ActivePrimitive { get; set; } = PrimitiveLibrary[0];
        public int ActiveFromNodeId { get; set; } = -1;
        public int ActiveToNodeId { get; set; } = -1;
        public float ActiveTargetX { get; set; }
        public float ActiveTargetY { get; set; }
        public float BestDistance { get; set; } = float.PositiveInfinity;
        public int NoProgressTicks { get; set; }
        public bool LastJumpOutput { get; set; }
        public int JumpReleaseTicks { get; set; }
        public int EdgeSettleTicks { get; set; }

        public void ResetRoute()
        {
            ObjectiveKind = string.Empty;
            Route.Clear();
            RouteStepIndex = 0;
            RouteTicksRemaining = 0;
            ActivePrimitiveTicksRemaining = 0;
            ActiveFromNodeId = -1;
            ActiveToNodeId = -1;
            ActiveTargetX = 0f;
            ActiveTargetY = 0f;
            BestDistance = float.PositiveInfinity;
            NoProgressTicks = 0;
            LastJumpOutput = false;
            JumpReleaseTicks = 0;
            EdgeSettleTicks = 0;
        }
    }

    private sealed record ObjectivePoint(
        string Kind,
        (float X, float Y) Target,
        int ControlPointIndex,
        CaptureZone[] Zones)
    {
        public bool IsCtfObjective => Kind.StartsWith("ctf_", StringComparison.Ordinal);

        public static ObjectivePoint IntelAttack(float x, float y)
        {
            return new ObjectivePoint("ctf_enemy_intel", (x, y), -1, []);
        }

        public static ObjectivePoint IntelHome(float x, float y)
        {
            return new ObjectivePoint("ctf_home", (x, y), -1, []);
        }

        public static ObjectivePoint Point(float x, float y, string kind, int controlPointIndex, CaptureZone[] zones)
        {
            return new ObjectivePoint(kind, (x, y), controlPointIndex, zones);
        }

        public bool IsSatisfied(SimulationWorld world, PlayerEntity player)
        {
            if (Kind == "ctf_enemy_intel")
            {
                return player.IsCarryingIntel || player.IntersectsMarker(Target.X, Target.Y, IntelMarkerSize, IntelMarkerSize);
            }

            if (Kind == "ctf_home")
            {
                return player.IntersectsMarker(Target.X, Target.Y, IntelMarkerSize, IntelMarkerSize);
            }

            return ControlPointIndex > 0 && world.IsPlayerInControlPointCaptureZone(player, ControlPointIndex);
        }

        public bool ContainsNode(float x, float bottom)
        {
            if (Kind.StartsWith("ctf_", StringComparison.Ordinal))
            {
                return MathF.Abs(x - Target.X) <= 44f && MathF.Abs(bottom - Target.Y) <= 78f;
            }

            for (var index = 0; index < Zones.Length; index += 1)
            {
                var zone = Zones[index];
                if (x >= zone.Left && x <= zone.Right && bottom >= zone.Top - 24f && bottom <= zone.Bottom + 96f)
                {
                    return true;
                }
            }

            return Distance(x, bottom, Target.X, Target.Y) <= 72f;
        }
    }

    private readonly record struct GraphKey(string LevelName, float Width, float Height, int SolidCount, PlayerTeam Team, PlayerClass ClassId, bool CarryingIntel, bool StrictEdges);

    private readonly record struct GraphNode(int Id, float X, float Bottom);

    private readonly record struct GraphEdge(int From, int To, MotionPrimitive Primitive, int Ticks, float Cost);

    private readonly record struct RouteSearchResult(List<GraphEdge> Route, bool Complete, float Cost, float Heuristic);

    private readonly record struct MotionPrimitive(string Name, int Horizontal, JumpMode JumpMode, bool Down, int Ticks);

    private readonly record struct CaptureZone(float X, float Y, float Width, float Height)
    {
        public float Left => X - (Width * 0.5f);
        public float Right => X + (Width * 0.5f);
        public float Top => Y - (Height * 0.5f);
        public float Bottom => Y + (Height * 0.5f);
    }

    private enum JumpMode
    {
        None,
        Takeoff,
        LateTakeoff,
        Pulse,
        Climb,
    }
}
