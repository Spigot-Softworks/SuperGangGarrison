using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Core;

public static class TraversalLabRunner
{
    private const float IntelMarkerSize = 24f;

    public static TraversalLabBatchResult Run(TraversalLabScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (scenario.OverrideLevel is null
            && scenario.Fixture is null
            && string.IsNullOrWhiteSpace(scenario.LevelName))
        {
            throw new InvalidOperationException("TraversalLab scenario requires OverrideLevel, Fixture, or LevelName.");
        }

        var variants = BuildVariants(scenario);
        var results = new List<TraversalLabCaseResult>(variants.Count);
        foreach (var variant in variants)
        {
            if (scenario.OverrideLevel is not null)
            {
                results.Add(RunSimpleLevelScenario(scenario, scenario.OverrideLevel, variant));
            }
            else if (scenario.Fixture is not null)
            {
                results.Add(RunSimpleLevelScenario(scenario, TraversalLabFixtures.Create(scenario.Fixture.Value), variant));
            }
            else
            {
                results.Add(RunWorldScenario(scenario, variant));
            }
        }

        return new TraversalLabBatchResult
        {
            ScenarioName = scenario.Name,
            Cases = results,
        };
    }

    private static List<TraversalLabVariant> BuildVariants(TraversalLabScenario scenario)
    {
        var xOffsets = scenario.StartXOffsets.Count == 0 ? [0f] : scenario.StartXOffsets;
        var bottomOffsets = scenario.StartBottomOffsets.Count == 0 ? [0f] : scenario.StartBottomOffsets;
        var facings = scenario.FacingDirections.Count == 0 ? [scenario.Start.FacingDirectionX] : scenario.FacingDirections;
        var horizontalSpeedOffsets = scenario.StartHorizontalSpeedOffsets.Count == 0 ? [0f] : scenario.StartHorizontalSpeedOffsets;
        var verticalSpeedOffsets = scenario.StartVerticalSpeedOffsets.Count == 0 ? [0f] : scenario.StartVerticalSpeedOffsets;
        var groundedStates = scenario.GroundedStates.Count == 0 ? [scenario.Start.IsGrounded] : scenario.GroundedStates;
        var variants = new List<TraversalLabVariant>(
            xOffsets.Count * bottomOffsets.Count * facings.Count * horizontalSpeedOffsets.Count * verticalSpeedOffsets.Count * groundedStates.Count);
        foreach (var xOffset in xOffsets)
        {
            foreach (var bottomOffset in bottomOffsets)
            {
                foreach (var facing in facings)
                {
                    foreach (var horizontalSpeedOffset in horizontalSpeedOffsets)
                    {
                        foreach (var verticalSpeedOffset in verticalSpeedOffsets)
                        {
                            foreach (var grounded in groundedStates)
                            {
                                variants.Add(new TraversalLabVariant(
                                    xOffset,
                                    bottomOffset,
                                    facing < 0f ? -1f : 1f,
                                    horizontalSpeedOffset,
                                    verticalSpeedOffset,
                                    grounded));
                            }
                        }
                    }
                }
            }
        }

        return variants;
    }

    private static TraversalLabCaseResult RunWorldScenario(TraversalLabScenario scenario, TraversalLabVariant variant)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        if (!world.TryLoadLevel(scenario.LevelName!, scenario.MapAreaIndex, preservePlayerStats: false))
        {
            return CreateLoadFailure(variant, $"failed_to_load_level:{scenario.LevelName}:a{scenario.MapAreaIndex}");
        }

        world.PrepareLocalPlayerJoin();
        const byte botSlot = 2;
        if (!world.TryPrepareNetworkPlayerJoin(botSlot)
            || !world.TrySetNetworkPlayerTeam(botSlot, scenario.Team)
            || !world.TryApplyNetworkPlayerClassSelection(botSlot, scenario.ClassId)
            || !world.TryGetNetworkPlayer(botSlot, out var player))
        {
            return CreateLoadFailure(variant, "failed_to_prepare_player");
        }

        InitializePlayer(world.Level, scenario, variant, player);
        return SimulateVariant(
            scenario,
            variant,
            player,
            world.Level,
            scenario.Team,
            runTick: input =>
            {
                if (!world.TrySetNetworkPlayerInput(botSlot, input))
                {
                    return false;
                }

                world.AdvanceOneTick();
                return true;
            });
    }

    private static TraversalLabCaseResult RunSimpleLevelScenario(
        TraversalLabScenario scenario,
        SimpleLevel level,
        TraversalLabVariant variant)
    {
        var definition = CharacterClassCatalog.GetDefinition(scenario.ClassId);
        var player = new PlayerEntity(1, definition, "TraversalLab");
        InitializePlayer(level, scenario, variant, player);
        var previousInput = default(PlayerInputSnapshot);
        return SimulateVariant(
            scenario,
            variant,
            player,
            level,
            scenario.Team,
            runTick: input =>
            {
                var jumpPressed = input.Up && !previousInput.Up;
                player.Advance(
                    input,
                    jumpPressed,
                    level,
                    scenario.Team,
                    deltaSeconds: 1d / SimulationConfig.DefaultTicksPerSecond);
                previousInput = input;
                return true;
            });
    }

    private static void InitializePlayer(
        SimpleLevel level,
        TraversalLabScenario scenario,
        TraversalLabVariant variant,
        PlayerEntity player)
    {
        var startX = scenario.Start.X + variant.StartXOffset;
        var startBottom = scenario.Start.Bottom + variant.StartBottomOffset;
        var startY = startBottom - player.CollisionBottomOffset;
        player.Spawn(scenario.Team, startX, startY);
        player.TeleportTo(startX, startY);
        player.ResolveBlockingOverlap(level, scenario.Team);
        var startHorizontalSpeed = scenario.Start.HorizontalSpeed + variant.StartHorizontalSpeedOffset;
        var startVerticalSpeed = scenario.Start.VerticalSpeed + variant.StartVerticalSpeedOffset;
        if (startHorizontalSpeed != 0f || startVerticalSpeed != 0f)
        {
            player.AddImpulse(startHorizontalSpeed, startVerticalSpeed);
        }

        if (scenario.Start.IsCarryingIntel)
        {
            player.PickUpIntel();
        }

        player.RestoreMovementProbeState(
            variant.StartGrounded,
            scenario.Start.RemainingAirJumps,
            variant.FacingDirectionX);
        player.SetAimWorldPosition(startX + (variant.FacingDirectionX * 256f), player.Y);
    }

    private static TraversalLabCaseResult SimulateVariant(
        TraversalLabScenario scenario,
        TraversalLabVariant variant,
        PlayerEntity player,
        SimpleLevel level,
        PlayerTeam team,
        Func<PlayerInputSnapshot, bool> runTick)
    {
        var startX = player.X;
        var startBottom = player.Bottom;
        var executedTicks = 0;
        var minX = player.X;
        var maxX = player.X;
        var minBottom = player.Bottom;
        var maxBottom = player.Bottom;
        int? firstLeaveGroundTick = null;
        int? firstRegroundTick = null;
        int? firstCarryIntelTick = player.IsCarryingIntel ? 0 : null;
        var samples = new List<TraversalLabTickSample>();
        samples.Add(CreateSample(level, team, player, tick: 0, stepLabel: "start"));
        var totalTicks = scenario.MaxTicks > 0
            ? scenario.MaxTicks
            : Math.Max(1, scenario.Steps.Sum(static step => Math.Max(0, step.DurationTicks)));
        var traceEveryTicks = Math.Max(1, scenario.TraceEveryTicks);

        for (var tick = 0; tick < totalTicks; tick += 1)
        {
            var step = ResolveStepForTick(scenario.Steps, tick, out var stepLabel);
            var input = CreateInput(player, step);
            if (!runTick(input))
            {
                return FinalizeResult(
                    scenario,
                    variant,
                    player,
                    level,
                    team,
                    samples,
                    executedTicks,
                    startX,
                    startBottom,
                    minX,
                    maxX,
                    minBottom,
                    maxBottom,
                    firstLeaveGroundTick,
                    firstRegroundTick,
                    firstCarryIntelTick,
                    passed: false,
                    failureReason: "failed_to_apply_tick");
            }

            executedTicks = tick + 1;
            minX = MathF.Min(minX, player.X);
            maxX = MathF.Max(maxX, player.X);
            minBottom = MathF.Min(minBottom, player.Bottom);
            maxBottom = MathF.Max(maxBottom, player.Bottom);
            if (!player.IsGrounded && !firstLeaveGroundTick.HasValue)
            {
                firstLeaveGroundTick = tick + 1;
            }

            if (firstLeaveGroundTick.HasValue
                && player.IsGrounded
                && !firstRegroundTick.HasValue)
            {
                firstRegroundTick = tick + 1;
            }

            if (player.IsCarryingIntel && !firstCarryIntelTick.HasValue)
            {
                firstCarryIntelTick = tick + 1;
            }

            if (tick % traceEveryTicks == 0 || tick == totalTicks - 1)
            {
                samples.Add(CreateSample(level, team, player, tick + 1, stepLabel));
            }
        }

        var (passed, failureReason) = EvaluateExpectation(
            player,
            level,
            team,
            scenario.Expectation,
            startX,
            startBottom,
            minX,
            maxX,
            minBottom,
            maxBottom,
            firstLeaveGroundTick,
            firstRegroundTick,
            firstCarryIntelTick);
        return FinalizeResult(
            scenario,
            variant,
            player,
            level,
            team,
            samples,
            executedTicks,
            startX,
            startBottom,
            minX,
            maxX,
            minBottom,
            maxBottom,
            firstLeaveGroundTick,
            firstRegroundTick,
            firstCarryIntelTick,
            passed,
            failureReason);
    }

    private static TraversalLabCaseResult FinalizeResult(
        TraversalLabScenario scenario,
        TraversalLabVariant variant,
        PlayerEntity player,
        SimpleLevel level,
        PlayerTeam team,
        List<TraversalLabTickSample> samples,
        int executedTicks,
        float startX,
        float startBottom,
        float minX,
        float maxX,
        float minBottom,
        float maxBottom,
        int? firstLeaveGroundTick,
        int? firstRegroundTick,
        int? firstCarryIntelTick,
        bool passed,
        string failureReason)
    {
        var horizontalTravel = MathF.Max(MathF.Abs(maxX - startX), MathF.Abs(minX - startX));
        var bottomTravel = MathF.Max(MathF.Abs(maxBottom - startBottom), MathF.Abs(minBottom - startBottom));
        var startSample = samples.Count > 0 ? samples[0] : default;
        var finalSample = samples.Count > 0 ? samples[^1] : default;
        return new TraversalLabCaseResult
        {
            Variant = variant,
            Passed = passed,
            FailureReason = passed ? string.Empty : failureReason,
            StartX = startSample.X,
            StartY = startSample.Y,
            StartBottom = startSample.Bottom,
            StartGrounded = startSample.IsGrounded,
            FinalX = player.X,
            FinalY = player.Y,
            FinalBottom = player.Bottom,
            MinX = minX,
            MaxX = maxX,
            MinBottom = minBottom,
            MaxBottom = maxBottom,
            HorizontalTravel = horizontalTravel,
            BottomTravel = bottomTravel,
            FinalGrounded = player.IsGrounded,
            FinalCarryingIntel = player.IsCarryingIntel,
            FinalOverlapsEnemyIntelMarker = finalSample.OverlapsEnemyIntelMarker,
            FinalInsideBlockingTeamGate = finalSample.IsInsideBlockingTeamGate,
            FirstLeaveGroundTick = firstLeaveGroundTick,
            FirstRegroundTick = firstRegroundTick,
            FirstCarryIntelTick = firstCarryIntelTick,
            ExecutedTicks = executedTicks,
            Samples = samples,
        };
    }

    private static TraversalLabCaseResult CreateLoadFailure(TraversalLabVariant variant, string failureReason)
    {
        return new TraversalLabCaseResult
        {
            Variant = variant,
            Passed = false,
            FailureReason = failureReason,
            StartX = 0f,
            StartY = 0f,
            StartBottom = 0f,
            StartGrounded = false,
            FinalX = 0f,
            FinalY = 0f,
            FinalBottom = 0f,
            MinX = 0f,
            MaxX = 0f,
            MinBottom = 0f,
            MaxBottom = 0f,
            HorizontalTravel = 0f,
            BottomTravel = 0f,
            FinalGrounded = false,
            FinalCarryingIntel = false,
            FinalOverlapsEnemyIntelMarker = false,
            FinalInsideBlockingTeamGate = false,
            FirstLeaveGroundTick = null,
            FirstRegroundTick = null,
            FirstCarryIntelTick = null,
            ExecutedTicks = 0,
            Samples = [],
        };
    }

    private static TraversalLabTickSample CreateSample(
        SimpleLevel level,
        PlayerTeam team,
        PlayerEntity player,
        int tick,
        string stepLabel)
    {
        var supportedBelow = !player.CanOccupy(level, team, player.X, player.Y + 1f);
        var blockedLeft = !player.CanOccupy(level, team, player.X - 1f, player.Y);
        var blockedRight = !player.CanOccupy(level, team, player.X + 1f, player.Y);
        var overlapsEnemyIntelMarker = TryGetIntelMarker(level, team, opposing: true, out var enemyIntel)
            && player.IntersectsMarker(enemyIntel.X, enemyIntel.Y, IntelMarkerSize, IntelMarkerSize);
        var overlapsOwnIntelMarker = TryGetIntelMarker(level, team, opposing: false, out var ownIntel)
            && player.IntersectsMarker(ownIntel.X, ownIntel.Y, IntelMarkerSize, IntelMarkerSize);
        var insideBlockingTeamGate = player.IsInsideBlockingTeamGate(level, team);
        return new TraversalLabTickSample(
            Tick: tick,
            X: player.X,
            Y: player.Y,
            Bottom: player.Bottom,
            HorizontalSpeed: player.HorizontalSpeed,
            VerticalSpeed: player.VerticalSpeed,
            IsGrounded: player.IsGrounded,
            FacingDirectionX: player.FacingDirectionX,
            SupportedBelow: supportedBelow,
            BlockedLeft: blockedLeft,
            BlockedRight: blockedRight,
            IsCarryingIntel: player.IsCarryingIntel,
            OverlapsEnemyIntelMarker: overlapsEnemyIntelMarker,
            OverlapsOwnIntelMarker: overlapsOwnIntelMarker,
            IsInsideBlockingTeamGate: insideBlockingTeamGate,
            StepLabel: stepLabel);
    }

    private static (bool Passed, string FailureReason) EvaluateExpectation(
        PlayerEntity player,
        SimpleLevel level,
        PlayerTeam team,
        TraversalLabExpectation? expectation,
        float startX,
        float startBottom,
        float minX,
        float maxX,
        float minBottom,
        float maxBottom,
        int? firstLeaveGroundTick,
        int? firstRegroundTick,
        int? firstCarryIntelTick)
    {
        if (expectation is null)
        {
            return (true, string.Empty);
        }

        var horizontalTravel = MathF.Max(MathF.Abs(maxX - startX), MathF.Abs(minX - startX));
        var bottomTravel = MathF.Max(MathF.Abs(maxBottom - startBottom), MathF.Abs(minBottom - startBottom));

        if (expectation.FinalX.HasValue
            && MathF.Abs(player.X - expectation.FinalX.Value) > expectation.RadiusX)
        {
            return (false, $"final_x_out_of_window:{player.X:0.0}");
        }

        if (expectation.FinalBottom.HasValue
            && MathF.Abs(player.Bottom - expectation.FinalBottom.Value) > expectation.RadiusBottom)
        {
            return (false, $"final_bottom_out_of_window:{player.Bottom:0.0}");
        }

        if (expectation.MinX.HasValue && player.X < expectation.MinX.Value)
        {
            return (false, $"final_x_below_min:{player.X:0.0}");
        }

        if (expectation.MaxX.HasValue && player.X > expectation.MaxX.Value)
        {
            return (false, $"final_x_above_max:{player.X:0.0}");
        }

        if (expectation.MinBottom.HasValue && player.Bottom < expectation.MinBottom.Value)
        {
            return (false, $"final_bottom_below_min:{player.Bottom:0.0}");
        }

        if (expectation.MaxBottom.HasValue && player.Bottom > expectation.MaxBottom.Value)
        {
            return (false, $"final_bottom_above_max:{player.Bottom:0.0}");
        }

        if (horizontalTravel < expectation.MinHorizontalTravel)
        {
            return (false, $"horizontal_travel_below_min:{horizontalTravel:0.0}");
        }

        if (bottomTravel < expectation.MinBottomTravel)
        {
            return (false, $"bottom_travel_below_min:{bottomTravel:0.0}");
        }

        if (expectation.MustBeGrounded.HasValue
            && player.IsGrounded != expectation.MustBeGrounded.Value)
        {
            return (false, player.IsGrounded ? "expected_airborne" : "expected_grounded");
        }

        if (expectation.MustLeaveGround && !firstLeaveGroundTick.HasValue)
        {
            return (false, "never_left_ground");
        }

        if (expectation.MustReground && !firstRegroundTick.HasValue)
        {
            return (false, "never_regrounded");
        }

        if (expectation.MustCarryIntel.HasValue
            && player.IsCarryingIntel != expectation.MustCarryIntel.Value)
        {
            return (false, expectation.MustCarryIntel.Value ? "did_not_pick_up_intel" : "unexpected_intel_pickup");
        }

        if (expectation.MustOverlapEnemyIntelMarker.HasValue
            && (TryGetIntelMarker(level, team, opposing: true, out var enemyIntel)
                && player.IntersectsMarker(enemyIntel.X, enemyIntel.Y, IntelMarkerSize, IntelMarkerSize)) != expectation.MustOverlapEnemyIntelMarker.Value)
        {
            return (false, expectation.MustOverlapEnemyIntelMarker.Value ? "final_not_overlapping_enemy_intel_marker" : "unexpected_enemy_intel_marker_overlap");
        }

        if (expectation.MustBeInsideBlockingTeamGate.HasValue
            && player.IsInsideBlockingTeamGate(level, team) != expectation.MustBeInsideBlockingTeamGate.Value)
        {
            return (false, expectation.MustBeInsideBlockingTeamGate.Value ? "not_inside_blocking_team_gate" : "unexpected_blocking_team_gate_overlap");
        }

        return (true, string.Empty);
    }

    private static bool TryGetIntelMarker(SimpleLevel level, PlayerTeam team, bool opposing, out IntelBaseMarker marker)
    {
        var markerValue = level.GetIntelBase(opposing ? GetOpposingTeam(team) : team);
        if (markerValue.HasValue)
        {
            marker = markerValue.Value;
            return true;
        }

        marker = default;
        return false;
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }

    private static TraversalLabInputStep ResolveStepForTick(
        List<TraversalLabInputStep> steps,
        int tick,
        out string stepLabel)
    {
        var remainingTick = tick;
        for (var index = 0; index < steps.Count; index += 1)
        {
            var step = steps[index];
            var durationTicks = Math.Max(0, step.DurationTicks);
            if (remainingTick < durationTicks)
            {
                stepLabel = string.IsNullOrWhiteSpace(step.Label) ? $"step_{index}" : step.Label;
                return step;
            }

            remainingTick -= durationTicks;
        }

        stepLabel = "idle";
        return new TraversalLabInputStep { DurationTicks = 1 };
    }

    private static PlayerInputSnapshot CreateInput(PlayerEntity player, TraversalLabInputStep step)
    {
        var aimFacingDirection = step.AimFacingDirectionX.HasValue
            ? (step.AimFacingDirectionX.Value < 0f ? -1f : 1f)
            : step.Left == step.Right
                ? player.FacingDirectionX
                : step.Right ? 1f : -1f;
        var aimWorldX = player.X + (aimFacingDirection * MathF.Max(32f, MathF.Abs(step.AimDistance)));
        var aimWorldY = player.Y + step.AimOffsetY;
        return new PlayerInputSnapshot(
            Left: step.Left,
            Right: step.Right,
            Up: step.Up,
            Down: step.Down,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: step.FirePrimary,
            FireSecondary: step.FireSecondary,
            AimWorldX: aimWorldX,
            AimWorldY: aimWorldY,
            DebugKill: false,
            DropIntel: step.DropIntel,
            UseAbility: step.FireSecondaryWeapon);
    }
}
