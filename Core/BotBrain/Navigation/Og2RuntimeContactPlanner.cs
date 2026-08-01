using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Re-proves an OG2 contact from the live entry state before steering it.
/// Static graph contacts are discovered from a canonical grounded probe, but
/// route composition can hand the next edge residual velocity. This bounded
/// probe makes the edge recipe state-derived without adding map-specific rules.
/// </summary>
public static class Og2RuntimeContactPlanner
{
    private static readonly bool PerformanceTracingEnabled =
        Environment.GetEnvironmentVariable("OG_CLIENT_PERF_BOT_TRACE") is "1" or "true" or "TRUE";
    private static readonly bool TraceAllContacts =
        Environment.GetEnvironmentVariable("OG_CLIENT_PERF_BOT_TRACE_CONTACTS") is "1" or "true" or "TRUE";
    private static readonly string? PerformanceTracePath = PerformanceTracingEnabled
        ? RuntimePaths.GetLogPath($"runtime-contact-spikes-{DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)}.log")
        : null;
    private static readonly object PerformanceTraceSync = new();
    private static readonly ConditionalWeakTable<NavGraph, RuntimeContactCache> RuntimeContactCaches = new();

    // Long OG2 stair run-ups can be certified with a late jump trigger. The
    // runtime resolver must search the same bounded local horizon; clamping
    // every composed contact to tick 32 made valid class-specific recipes
    // unreachable after a high-momentum handoff.
    private const int MaximumCandidateJumpTick = 96;
    private const int MinimumProbeTicks = 48;
    private const int ProbeTailTicks = 36;
    private const int MaximumPreLaunchBrakeTicks = 20;
    // Bound runtime proof by deterministic work, not wall-clock time. A
    // timing cutoff made the accepted recipe depend on machine load and even
    // on whether tracing was enabled, which made capture results unstable.
    // Ordinary contacts use the static launch-window fast path in the
    // controller; this cap applies only to composed/recovery contacts.
    // The common OG2 runtime schedules resolve in the first few candidates;
    // allowing the late tail to run made one bot monopolize a dense frame with
    // repeated full movement simulations. A later retry from the live state
    // remains available when these bounded candidates do not prove a contact.
    private const int MaximumRuntimeContactCandidates = 1;

    public static bool TryResolve(
        SimpleLevel level,
        NavGraph graph,
        PlayerEntity player,
        PlayerTeam team,
        int fromNode,
        NavEdge edge,
        out NavEdge resolvedEdge)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var candidateCount = 0;
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

        // The same route edge is commonly entered by several roster members
        // from the same spawn lane or recovery support. Runtime proof depends
        // on the live entry state, so do not use a global edge-only cache; do
        // reuse a proof for the same edge/profile and a tightly quantized
        // state bucket. This removes duplicate movement simulations without
        // making a recipe valid for a materially different launch state.
        var cache = RuntimeContactCaches.GetValue(graph, static _ => new RuntimeContactCache());
        var cacheKey = new RuntimeContactCacheKey(
            fromNode,
            edge.ToNode,
            player.ClassId,
            team,
            player.IsCarryingIntel,
            player.IsGrounded,
            QuantizeState(player.X, 4f),
            QuantizeState(player.Y, 4f),
            QuantizeState(player.HorizontalSpeed, 8f),
            QuantizeState(player.VerticalSpeed, 8f));
        if (cache.TryGet(cacheKey, out resolvedEdge))
        {
            return true;
        }

        // A failed proof is also a useful result for this tightly quantized
        // live state. Without this negative cache, a blocked or otherwise
        // unresolvable contact is replayed from scratch every navigation
        // think, repeatedly paying the full probe budget while the bot is
        // still in the same launch bucket. The controller already owns the
        // bounded recovery/repath decision when runtime proof fails.
        if (cache.IsFailed(cacheKey))
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

        var controlProfiles = EnumerateControlProfiles(edge.LaunchRecipe).ToArray();
        // Search one control schedule across the bounded jump horizon before
        // trying the next schedule. The old nesting tried every air-control
        // variant at tick 0, then every variant at tick 1, and so on. That is
        // a particularly expensive ordering when the normal hold-direction
        // schedule succeeds a few ticks later: the same live contact paid for
        // dozens of full probe simulations before reaching the useful tick.
        // Keep the candidate set unchanged, but make the common schedule the
        // fast path. This is a runtime execution optimization only; it does
        // not alter the generated graph or the accepted movement contract.
        foreach (var controlProfile in controlProfiles)
        {
            foreach (var jumpTick in EnumerateCandidateJumpTicks(firstCandidate))
            {
                if (candidateCount >= MaximumRuntimeContactCandidates)
                {
                    TraceSlowRuntimeContact(
                        startTimestamp,
                        candidateCount,
                        level,
                        player,
                        team,
                        edge,
                        resolved: false,
                        jumpTick: -1,
                        probeTicks: -1,
                        resolutionDetails: "budget_exhausted");
                    cache.TryAddFailed(cacheKey);
                    return false;
                }

                candidateCount += 1;
                var preLaunchBrakeTicks = Math.Min(controlProfile.PreLaunchBrakeTicks, jumpTick);
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
                        controlProfile.HoldTicks,
                        preLaunchBrakeTicks,
                        player.HorizontalSpeed);
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

                    probe.AdvanceNavigationProbe(
                        input,
                        jumpPressed,
                        level,
                        team,
                        1d / SimulationConfig.DefaultTicksPerSecond);
                    previousInput = input;
                    hasBeenAirborne |= !probe.IsGrounded;

                    if (!launchCaptured
                        || !hasBeenAirborne
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
                        AirControlHoldTicks: controlProfile.HoldTicks,
                        PreLaunchBrakeTicks: preLaunchBrakeTicks);

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
                    cache.TryAdd(cacheKey, resolvedEdge);
                    if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_TRACE_RUNTIME_CONTACTS") is "1" or "true" or "TRUE")
                    {
                        Console.WriteLine(
                            $"alphaRuntimeProbeResult edge={edge.ToNode} mode={controlProfile.Mode} hold={controlProfile.HoldTicks} jump={jumpTick} ticks={tick + 1} " +
                            $"preBrake={preLaunchBrakeTicks} " +
                            $"launch=({launchX:0.0},{launchY:0.0}) landing=({probe.X:0.0},{probe.Y:0.0}) " +
                            $"grounded={(probe.IsGrounded ? 1 : 0)}");
                    }
                    TraceSlowRuntimeContact(
                        startTimestamp,
                        candidateCount,
                        level,
                        player,
                        team,
                        edge,
                        resolved: true,
                        jumpTick,
                        tick + 1,
                        $"mode={controlProfile.Mode} hold={controlProfile.HoldTicks} preBrake={preLaunchBrakeTicks} " +
                        $"launch=({launchX:0.0},{launchY:0.0}) launchSpeed={launchHorizontalSpeed:0.0} " +
                        $"landing=({probe.X:0.0},{probe.Y:0.0}) landingSpeed={probe.HorizontalSpeed:0.0} " +
                        $"landingGrounded={(probe.IsGrounded ? 1 : 0)}");
                    return true;
                }

                if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_TRACE_RUNTIME_CANDIDATES") is "1" or "true" or "TRUE")
                {
                    Console.WriteLine(
                        $"alphaRuntimeCandidate edge={edge.ToNode} mode={controlProfile.Mode} hold={controlProfile.HoldTicks} jump={jumpTick} " +
                        $"preBrake={preLaunchBrakeTicks} " +
                        $"launch={(launchCaptured ? $"({launchX:0.0},{launchY:0.0})" : "none")} " +
                        $"final=({probe.X:0.0},{probe.Y:0.0}) grounded={(probe.IsGrounded ? 1 : 0)} " +
                        $"airborne={(hasBeenAirborne ? 1 : 0)} speed={probe.HorizontalSpeed:0.0} " +
                        $"complete={(graph.IsEdgeCompletionSatisfied(probe.X, probe.Y, edge.Completion) ? 1 : 0)}");
                }
            }
        }

        TraceSlowRuntimeContact(
            startTimestamp,
            candidateCount,
            level,
            player,
            team,
            edge,
            resolved: false,
            jumpTick: -1,
            probeTicks: -1);
        cache.TryAddFailed(cacheKey);
        return false;
    }

    private static int QuantizeState(float value, float step) =>
        (int)MathF.Round(value / step, MidpointRounding.AwayFromZero);

    private static void TraceSlowRuntimeContact(
        long startTimestamp,
        int candidateCount,
        SimpleLevel level,
        PlayerEntity player,
        PlayerTeam team,
        NavEdge edge,
        bool resolved,
        int jumpTick,
        int probeTicks,
        string? resolutionDetails = null)
    {
        if (!PerformanceTracingEnabled || string.IsNullOrWhiteSpace(PerformanceTracePath))
        {
            return;
        }

        var elapsedMilliseconds = (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
        if (!TraceAllContacts && elapsedMilliseconds < 100d)
        {
            return;
        }

        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.Now:O} map={level.Name} team={team} class={player.ClassId} " +
            $"edgeKind={edge.Kind} toNode={edge.ToNode} candidates={candidateCount} elapsedMs={elapsedMilliseconds:F1} " +
            $"resolved={resolved} jumpTick={jumpTick} probeTicks={probeTicks} " +
            $"start=({player.X:F1},{player.Y:F1}) grounded={player.IsGrounded} speed=({player.HorizontalSpeed:F1},{player.VerticalSpeed:F1}) " +
            $"{resolutionDetails}{Environment.NewLine}");
        lock (PerformanceTraceSync)
        {
            File.AppendAllText(PerformanceTracePath, line);
        }
    }

    private static IEnumerable<int> EnumerateCandidateJumpTicks(int preferred)
    {
        // A live handoff is often already at a valid grounded launch state.
        // Try that cheap immediate candidate before spending the search budget
        // replaying a long canonical run-up from the static graph. Keep the
        // canonical tick immediately after it so delayed contacts retain their
        // original proof path when the immediate candidate cannot complete.
        if (preferred != 0)
        {
            yield return 0;
        }

        yield return preferred;
        for (var tick = 0; tick <= MaximumCandidateJumpTick; tick += 1)
        {
            if (tick != preferred && tick != 0)
            {
                yield return tick;
            }
        }
    }

    private static IEnumerable<ControlProfile> EnumerateControlProfiles(NavEdgeLaunchRecipe launchRecipe)
    {
        var seen = new HashSet<ControlProfile>();
        if (launchRecipe.HasRecipe)
        {
            var preferred = new ControlProfile(
                launchRecipe.AirControlMode,
                launchRecipe.AirControlHoldTicks,
                launchRecipe.PreLaunchBrakeTicks);
            if (seen.Add(preferred))
            {
                yield return preferred;
            }
        }

        foreach (var preLaunchBrakeTicks in new[] { 0, 4, 8, 12, 16, MaximumPreLaunchBrakeTicks })
        {
            var holdProfile = new ControlProfile(
                    NavEdgeAirControlMode.HoldDirection,
                    0,
                    preLaunchBrakeTicks);
            if (seen.Add(holdProfile))
            {
                yield return holdProfile;
            }

            foreach (var holdTicks in new[] { 0, 2, 4, 6, 8, 10, 12 })
            {
                var releaseProfile = new ControlProfile(
                    NavEdgeAirControlMode.ReleaseDirection,
                    holdTicks,
                    preLaunchBrakeTicks);
                if (seen.Add(releaseProfile))
                {
                    yield return releaseProfile;
                }
            }

            foreach (var holdTicks in new[] { 2, 4, 6, 8, 10 })
            {
                var counterSteerProfile = new ControlProfile(
                    NavEdgeAirControlMode.CounterSteer,
                    holdTicks,
                    preLaunchBrakeTicks);
                if (seen.Add(counterSteerProfile))
                {
                    yield return counterSteerProfile;
                }
            }
        }
    }

    private static float ResolveControlDirection(
        float direction,
        NavEdgeAirControlMode controlMode,
        int tick,
        int jumpTick,
        int holdTicks,
        int preLaunchBrakeTicks,
        float initialHorizontalSpeed)
    {
        var preLaunchStart = Math.Max(0, jumpTick - preLaunchBrakeTicks);
        if (tick >= preLaunchStart && tick < jumpTick)
        {
            // Counter-steer only when the live body is already moving in the
            // launch direction too quickly. If momentum is reversed, pressing
            // the launch direction is the brake: it first cancels the reverse
            // velocity and makes the certified launch state reachable.
            return initialHorizontalSpeed * direction < 0f
                ? direction
                : -direction;
        }

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
        int HoldTicks,
        int PreLaunchBrakeTicks);

    private readonly record struct RuntimeContactCacheKey(
        int FromNode,
        int ToNode,
        PlayerClass PlayerClass,
        PlayerTeam Team,
        bool CarryingIntel,
        bool Grounded,
        int X,
        int Y,
        int HorizontalSpeed,
        int VerticalSpeed);

    private sealed class RuntimeContactCache
    {
        private const int MaximumEntries = 8_192;
        private readonly ConcurrentDictionary<RuntimeContactCacheKey, NavEdge> _entries = new();
        private readonly ConcurrentDictionary<RuntimeContactCacheKey, byte> _failedEntries = new();

        public bool TryGet(RuntimeContactCacheKey key, out NavEdge edge) => _entries.TryGetValue(key, out edge);

        public bool IsFailed(RuntimeContactCacheKey key) => _failedEntries.ContainsKey(key);

        public void TryAdd(RuntimeContactCacheKey key, NavEdge edge)
        {
            if (_entries.Count < MaximumEntries)
            {
                _entries.TryAdd(key, edge);
            }
        }

        public void TryAddFailed(RuntimeContactCacheKey key)
        {
            if (_failedEntries.Count < MaximumEntries)
            {
                _failedEntries.TryAdd(key, 0);
            }
        }
    }

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
