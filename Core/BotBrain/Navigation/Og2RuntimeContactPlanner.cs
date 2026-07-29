namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Re-proves an OG2 contact from the live entry state before steering it.
/// Static graph contacts are discovered from a canonical grounded probe, but
/// route composition can hand the next edge residual velocity. This bounded
/// probe makes the edge recipe state-derived without adding map-specific rules.
/// </summary>
public static class Og2RuntimeContactPlanner
{
    private const int MaximumCandidateJumpTick = 32;
    private const int MinimumProbeTicks = 48;
    private const int ProbeTailTicks = 36;

    public static bool TryResolve(
        SimpleLevel level,
        NavGraph graph,
        PlayerEntity player,
        PlayerTeam team,
        NavEdge edge,
        out NavEdge resolvedEdge)
    {
        resolvedEdge = edge;
        if (!edge.IsOg2Contact
            || edge.Kind != NavEdgeKind.Jump
            || !edge.Completion.HasWindow
            || !player.IsAlive
            || (edge.LaunchRecipe.StartGrounded && !player.IsGrounded))
        {
            return false;
        }

        var direction = MathF.Sign(edge.ProbeMoveDirectionX);
        if (direction == 0f && edge.LaunchRecipe.HasRecipe)
        {
            direction = MathF.Sign(edge.LaunchRecipe.ExpectedMoveDirectionX);
        }

        if (direction == 0f)
        {
            return false;
        }

        var definition = CharacterClassCatalog.GetDefinition(player.ClassId);
        var probeTicks = Math.Clamp(
            Math.Max(MinimumProbeTicks, edge.ProbeTicks + ProbeTailTicks),
            MinimumProbeTicks,
            128);
        var firstCandidate = edge.JumpTriggerTick > 0
            ? Math.Min(edge.JumpTriggerTick, MaximumCandidateJumpTick)
            : 0;

        foreach (var controlProfile in EnumerateControlProfiles())
        {
            foreach (var jumpTick in EnumerateCandidateJumpTicks(firstCandidate))
            {
                var probe = CreateProbe(player, team, definition, direction);
                var previousInput = default(PlayerInputSnapshot);
                var launchCaptured = false;
                var jumpStartsGrounded = false;
                var launchX = player.X;
                var launchY = player.Y;
                var launchHorizontalSpeed = player.HorizontalSpeed;
                var hasBeenAirborne = !probe.IsGrounded;

                for (var tick = 0; tick < probeTicks; tick += 1)
                {
                    var controlDirection = ResolveControlDirection(
                        direction,
                        controlProfile.Mode,
                        tick,
                        jumpTick,
                        controlProfile.HoldTicks);
                    var input = CreateInput(
                        probe,
                        controlDirection,
                        tick == jumpTick,
                        direction);
                    var jumpPressed = input.Up && !previousInput.Up;
                    if (jumpPressed
                        && edge.LaunchRecipe.JumpStartsGrounded != probe.IsGrounded)
                    {
                        // The graph records whether the jump input was consumed
                        // from ground or in the air. Do not substitute a normal
                        // jump for an air-jump contact (or vice versa): the two
                        // consume different OG2 movement state.
                        break;
                    }

                    if (jumpPressed && !launchCaptured)
                    {
                        launchCaptured = true;
                        jumpStartsGrounded = probe.IsGrounded;
                        launchX = probe.X;
                        launchY = probe.Y;
                        launchHorizontalSpeed = probe.HorizontalSpeed;
                    }

                    probe.Advance(
                        input,
                        jumpPressed,
                        level,
                        team,
                        1d / SimulationConfig.DefaultTicksPerSecond);
                    previousInput = input;
                    hasBeenAirborne |= !probe.IsGrounded;

                    if (!hasBeenAirborne
                        || !probe.IsGrounded
                        || !graph.IsEdgeCompletionSatisfied(probe.X, probe.Y, edge.Completion))
                    {
                        continue;
                    }

                    var positionTolerance = MathF.Max(
                        4f,
                        definition.MaxRunSpeed / LegacyMovementModel.SourceTicksPerSecond);
                    var speedTolerance = MathF.Max(
                        8f,
                        definition.RunPower * LegacyMovementModel.SourceTicksPerSecond * 0.25f);
                    var launchRecipe = new NavEdgeLaunchRecipe(
                        StartGrounded: player.IsGrounded,
                        LaunchTick: jumpTick,
                        LaunchMinX: launchX - positionTolerance,
                        LaunchMaxX: launchX + positionTolerance,
                        LaunchMinY: launchY - 8f,
                        LaunchMaxY: launchY + 8f,
                        LaunchMinHorizontalSpeed: launchHorizontalSpeed - speedTolerance,
                        LaunchMaxHorizontalSpeed: launchHorizontalSpeed + speedTolerance,
                        ExpectedMoveDirectionX: direction,
                        JumpStartsGrounded: jumpStartsGrounded,
                        AirControlMode: controlProfile.Mode,
                        AirControlHoldTicks: controlProfile.HoldTicks);

                    resolvedEdge = edge with
                    {
                        JumpTriggerTick = jumpTick,
                        ProbeTicks = tick + 1,
                        RequiresGroundedContinuation = true,
                        ProbeVariantAttempts = Math.Max(1, edge.ProbeVariantAttempts),
                        ProbeVariantSuccesses = Math.Max(1, edge.ProbeVariantSuccesses),
                        IsRuntimeResolved = true,
                        LaunchRecipe = launchRecipe,
                    };
                    if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_TRACE_RUNTIME_CONTACTS") is "1" or "true" or "TRUE")
                    {
                        Console.WriteLine(
                            $"alphaRuntimeProbeResult edge={edge.ToNode} mode={controlProfile.Mode} hold={controlProfile.HoldTicks} jump={jumpTick} ticks={tick + 1} " +
                            $"launch=({launchX:0.0},{launchY:0.0}) landing=({probe.X:0.0},{probe.Y:0.0}) " +
                            $"grounded={(probe.IsGrounded ? 1 : 0)}");
                    }
                    return true;
                }

                if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_TRACE_RUNTIME_CANDIDATES") is "1" or "true" or "TRUE")
                {
                    Console.WriteLine(
                        $"alphaRuntimeCandidate edge={edge.ToNode} mode={controlProfile.Mode} hold={controlProfile.HoldTicks} jump={jumpTick} " +
                        $"launch={(launchCaptured ? $"({launchX:0.0},{launchY:0.0})" : "none")} " +
                        $"final=({probe.X:0.0},{probe.Y:0.0}) grounded={(probe.IsGrounded ? 1 : 0)} " +
                        $"airborne={(hasBeenAirborne ? 1 : 0)} speed={probe.HorizontalSpeed:0.0} " +
                        $"complete={(graph.IsEdgeCompletionSatisfied(probe.X, probe.Y, edge.Completion) ? 1 : 0)}");
                }
            }
        }

        return false;
    }

    private static IEnumerable<int> EnumerateCandidateJumpTicks(int preferred)
    {
        yield return preferred;
        for (var tick = 0; tick <= MaximumCandidateJumpTick; tick += 1)
        {
            if (tick != preferred)
            {
                yield return tick;
            }
        }
    }

    private static IEnumerable<ControlProfile> EnumerateControlProfiles()
    {
        yield return new ControlProfile(NavEdgeAirControlMode.HoldDirection, 0);
        foreach (var holdTicks in new[] { 0, 2, 4, 6, 8, 10, 12 })
        {
            yield return new ControlProfile(NavEdgeAirControlMode.ReleaseDirection, holdTicks);
        }

        foreach (var holdTicks in new[] { 2, 4, 6, 8, 10 })
        {
            yield return new ControlProfile(NavEdgeAirControlMode.CounterSteer, holdTicks);
        }
    }

    private static float ResolveControlDirection(
        float direction,
        NavEdgeAirControlMode controlMode,
        int tick,
        int jumpTick,
        int holdTicks)
    {
        if (tick <= jumpTick + holdTicks)
        {
            return direction;
        }

        return controlMode switch
        {
            NavEdgeAirControlMode.ReleaseDirection => 0f,
            NavEdgeAirControlMode.CounterSteer => -direction,
            _ => direction,
        };
    }

    private readonly record struct ControlProfile(
        NavEdgeAirControlMode Mode,
        int HoldTicks);

    private static PlayerEntity CreateProbe(
        PlayerEntity source,
        PlayerTeam team,
        CharacterClassDefinition definition,
        float movementDirection)
    {
        var probe = new PlayerEntity(-910_101, definition, "Og2RuntimeContactProbe");
        probe.Spawn(team, source.X, source.Y);
        // Runtime validation must start from the same movement state as the
        // live entity. Reconstructing only velocity/grounded/carrying state
        // can omit scoped, ability, or movement-state flags and produce a
        // probe success that the real bot cannot reproduce.
        probe.RestorePredictionState(source.CapturePredictionState());
        probe.SetPlayerScale(source.PlayerScale);
        // Keep gameplay state and the live velocity, but clear movement
        // integrator history. The canonical OG2 sweep starts from a clean
        // movement state; carrying fractional legacy ticks or source-facing
        // history across an edge makes runtime proof depend on the preceding
        // edge (and, indirectly, on combat aim).
        probe.TeleportTo(source.X, source.Y);
        probe.ApplyVelocityImpulse(source.HorizontalSpeed, source.VerticalSpeed);
        // Combat aim is not a navigation input. Normalize only the movement
        // facing bookkeeping used by the legacy movement integrator so the
        // runtime proof cannot change when the bot's weapon target changes.
        probe.RestoreMovementProbeState(
            source.IsGrounded,
            source.IsGrounded ? probe.MaxAirJumps : source.RemainingAirJumps,
            movementDirection);
        return probe;
    }

    private static PlayerInputSnapshot CreateInput(
        PlayerEntity player,
        float controlDirection,
        bool jump,
        float aimDirection) =>
        new(
            Left: controlDirection < 0f,
            Right: controlDirection > 0f,
            Up: jump,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + aimDirection * 256f,
            AimWorldY: player.Y,
            DebugKill: false,
            DropIntel: false,
            UseAbility: false);
}
