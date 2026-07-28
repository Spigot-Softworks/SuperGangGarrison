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
            || !player.IsAlive)
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

        foreach (var jumpTick in EnumerateCandidateJumpTicks(firstCandidate))
        {
            var probe = CreateProbe(level, player, team, definition, direction);
            var previousInput = default(PlayerInputSnapshot);
            var launchCaptured = false;
            var launchX = player.X;
            var launchY = player.Y;
            var launchHorizontalSpeed = player.HorizontalSpeed;
            var hasBeenAirborne = !probe.IsGrounded;

            for (var tick = 0; tick < probeTicks; tick += 1)
            {
                var input = CreateInput(probe, direction, tick == jumpTick);
                var jumpPressed = input.Up && !previousInput.Up;
                if (jumpPressed && !launchCaptured)
                {
                    launchCaptured = true;
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
                    ExpectedMoveDirectionX: direction);

                resolvedEdge = edge with
                {
                    JumpTriggerTick = jumpTick,
                    ProbeTicks = tick + 1,
                    LaunchRecipe = launchRecipe,
                    RequiresGroundedContinuation = true,
                    ProbeVariantAttempts = Math.Max(1, edge.ProbeVariantAttempts),
                    ProbeVariantSuccesses = Math.Max(1, edge.ProbeVariantSuccesses),
                };
                return true;
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

    private static PlayerEntity CreateProbe(
        SimpleLevel level,
        PlayerEntity source,
        PlayerTeam team,
        CharacterClassDefinition definition,
        float direction)
    {
        var probe = new PlayerEntity(-910_101, definition, "Og2RuntimeContactProbe");
        probe.Spawn(team, source.X, source.Y);
        probe.TeleportTo(source.X, source.Y);
        level.GetBlockingTeamGates(team, source.IsCarryingIntel);
        if (source.IsCarryingIntel)
        {
            probe.PickUpIntel();
        }

        probe.ResolveBlockingOverlap(level, team);
        if (source.HorizontalSpeed != 0f || source.VerticalSpeed != 0f)
        {
            probe.AddImpulse(source.HorizontalSpeed, source.VerticalSpeed);
        }

        probe.RestoreMovementProbeState(
            source.IsGrounded,
            source.RemainingAirJumps,
            direction);
        return probe;
    }

    private static PlayerInputSnapshot CreateInput(
        PlayerEntity player,
        float direction,
        bool jump) =>
        new(
            Left: direction < 0f,
            Right: direction > 0f,
            Up: jump,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + direction * 256f,
            AimWorldY: player.Y,
            DebugKill: false,
            DropIntel: false,
            UseAbility: false);
}
