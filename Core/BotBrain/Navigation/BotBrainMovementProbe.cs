namespace OpenGarrison.Core.BotBrain;

internal static class BotBrainMovementProbe
{
    private const int MaxProbeTicks = 120;
    private const float LandingHorizontalSlack = 36f;
    private const float SurfaceLandingHorizontalSlack = 25f;
    private const float SurfaceLandingVerticalSlack = 6f;
    private const float LandingVerticalSlack = 14f;
    private const float CompletionHorizontalPadding = 42f;
    private const float CompletionVerticalPadding = 18f;
    private const float LaunchHorizontalPadding = 16f;
    private const float LaunchVerticalPadding = 8f;
    private const float LaunchSpeedPadding = 16f;
    private const float EnsembleStartOffset = 16f;
    private const float EnsembleSmallStartOffset = 8f;
    private const float EnsembleLowIncomingSpeed = 80f;
    private const float EnsembleMediumIncomingSpeed = 160f;
    private const int PrimitiveSearchDefaultMaxExpanded = 350;
    private const int PrimitiveSearchDefaultBudgetMilliseconds = 8;
    private const float PrimitiveSearchMinimumProgressDistance = 16f;
    private static readonly int[] JumpTriggerTicks = [0, 3, 6, 10];
    private static readonly float[] EnsembleStartOffsets = [-EnsembleStartOffset, -EnsembleSmallStartOffset, 0f, EnsembleSmallStartOffset, EnsembleStartOffset];
    private static readonly float[] EnsembleIncomingSpeedMagnitudes = [0f, EnsembleLowIncomingSpeed, EnsembleMediumIncomingSpeed];

    public static bool TryCertifyTeamAgnosticEdge(
        SimpleLevel level,
        BotBrainProbeNode from,
        BotBrainProbeNode to,
        BotBrainProbeSurface? targetSurface,
        NavEdgeKind kind,
        out BotBrainMovementProbeResult result,
        out int supportedClassMask,
        bool useProbeEnsemble = false)
    {
        result = default;
        supportedClassMask = 0;
        if (kind == NavEdgeKind.Walk)
        {
            return false;
        }

        var profileResults = new List<BotBrainMovementProbeResult>(3);
        foreach (var movementProfile in EnumerateCertificationProfiles())
        {
            if (!TryCertifyForTeam(level, movementProfile, PlayerTeam.Red, from, to, targetSurface, kind, useProbeEnsemble, out var redResult)
                || !TryCertifyForTeam(level, movementProfile, PlayerTeam.Blue, from, to, targetSurface, kind, useProbeEnsemble, out var blueResult))
            {
                continue;
            }

            supportedClassMask |= BotBrainClassMask.For(movementProfile.Id);
            profileResults.Add(BotBrainMovementProbeResult.Merge(redResult, blueResult));
        }

        if (profileResults.Count == 0)
        {
            return false;
        }

        result = profileResults[0];
        return supportedClassMask != 0;
    }

    public static bool TryDiscoverTeamAgnosticLandingEdge(
        SimpleLevel level,
        BotBrainProbeNode from,
        BotBrainProbeNode to,
        IReadOnlyList<BotBrainProbeSurface> landingSurfaces,
        int excludedSurfaceId,
        NavEdgeKind kind,
        out BotBrainMovementProbeResult result,
        out int supportedClassMask,
        out int landingSurfaceId,
        bool useProbeEnsemble = false)
    {
        result = default;
        supportedClassMask = 0;
        landingSurfaceId = -1;
        if (kind == NavEdgeKind.Walk || landingSurfaces.Count == 0)
        {
            return false;
        }

        var candidates = new Dictionary<int, LandingDiscoveryCandidate>();
        foreach (var movementProfile in EnumerateCertificationProfiles())
        {
            if (!TryDiscoverLandingForTeam(level, movementProfile, PlayerTeam.Red, from, to, landingSurfaces, excludedSurfaceId, kind, useProbeEnsemble, out var redResult, out var redSurfaceId)
                || !TryDiscoverLandingForTeam(level, movementProfile, PlayerTeam.Blue, from, to, landingSurfaces, excludedSurfaceId, kind, useProbeEnsemble, out var blueResult, out var blueSurfaceId)
                || redSurfaceId != blueSurfaceId)
            {
                continue;
            }

            var mergedResult = BotBrainMovementProbeResult.Merge(redResult, blueResult);
            var profileMask = BotBrainClassMask.For(movementProfile.Id);
            if (candidates.TryGetValue(redSurfaceId, out var existing))
            {
                candidates[redSurfaceId] = new LandingDiscoveryCandidate(
                    redSurfaceId,
                    existing.SupportedClassMask | profileMask,
                    BotBrainMovementProbeResult.Merge(existing.Result, mergedResult));
            }
            else
            {
                candidates.Add(redSurfaceId, new LandingDiscoveryCandidate(redSurfaceId, profileMask, mergedResult));
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        var best = candidates.Values
            .OrderByDescending(static candidate => CountSupportedProfiles(candidate.SupportedClassMask))
            .ThenBy(static candidate => candidate.Result.Ticks)
            .First();
        result = best.Result;
        supportedClassMask = best.SupportedClassMask;
        landingSurfaceId = best.SurfaceId;
        return supportedClassMask != 0;
    }

    public static bool TryDiscoverLandingEdgeForTeam(
        SimpleLevel level,
        BotBrainProbeNode from,
        BotBrainProbeNode to,
        IReadOnlyList<BotBrainProbeSurface> landingSurfaces,
        int excludedSurfaceId,
        NavEdgeKind kind,
        PlayerTeam team,
        out BotBrainMovementProbeResult result,
        out int supportedClassMask,
        out int landingSurfaceId,
        bool useProbeEnsemble = false)
    {
        result = default;
        supportedClassMask = 0;
        landingSurfaceId = -1;
        if (kind == NavEdgeKind.Walk || landingSurfaces.Count == 0)
        {
            return false;
        }

        var candidates = new Dictionary<int, LandingDiscoveryCandidate>();
        foreach (var movementProfile in EnumerateCertificationProfiles())
        {
            if (!TryDiscoverLandingForTeam(level, movementProfile, team, from, to, landingSurfaces, excludedSurfaceId, kind, useProbeEnsemble, out var profileResult, out var profileSurfaceId))
            {
                continue;
            }

            var profileMask = BotBrainClassMask.For(movementProfile.Id);
            if (candidates.TryGetValue(profileSurfaceId, out var existing))
            {
                candidates[profileSurfaceId] = new LandingDiscoveryCandidate(
                    profileSurfaceId,
                    existing.SupportedClassMask | profileMask,
                    BotBrainMovementProbeResult.Merge(existing.Result, profileResult));
            }
            else
            {
                candidates.Add(profileSurfaceId, new LandingDiscoveryCandidate(profileSurfaceId, profileMask, profileResult));
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        var best = candidates.Values
            .OrderByDescending(static candidate => CountSupportedProfiles(candidate.SupportedClassMask))
            .ThenBy(static candidate => candidate.Result.Ticks)
            .First();
        result = best.Result;
        supportedClassMask = best.SupportedClassMask;
        landingSurfaceId = best.SurfaceId;
        return supportedClassMask != 0;
    }

    private static IEnumerable<CharacterClassDefinition> EnumerateCertificationProfiles()
    {
        yield return CharacterClassCatalog.Heavy;
        yield return CharacterClassCatalog.Soldier;
        yield return CharacterClassCatalog.Scout;
    }

    private static bool TryCertifyForTeam(
        SimpleLevel level,
        CharacterClassDefinition movementProfile,
        PlayerTeam team,
        BotBrainProbeNode from,
        BotBrainProbeNode to,
        BotBrainProbeSurface? targetSurface,
        NavEdgeKind kind,
        bool useProbeEnsemble,
        out BotBrainMovementProbeResult result)
    {
        if (useProbeEnsemble)
        {
            return kind == NavEdgeKind.Jump
                ? TryRunEnsemble(level, movementProfile, team, from, to, targetSurface, kind, JumpTriggerTicks, out result)
                : TryRunEnsemble(level, movementProfile, team, from, to, targetSurface, kind, [-1], out result);
        }

        if (kind == NavEdgeKind.Jump)
        {
            foreach (var jumpTick in JumpTriggerTicks)
            {
                if (TryRun(level, movementProfile, team, from, to, targetSurface, kind, jumpTick, BotBrainProbeVariant.Default, out result))
                {
                    return true;
                }
            }

            result = default;
            return ShouldUsePrimitiveSearchProbe()
                && TryRunPrimitiveSearch(level, movementProfile, team, from, to, targetSurface, kind, out result);
        }

        return TryRun(level, movementProfile, team, from, to, targetSurface, kind, jumpTick: -1, BotBrainProbeVariant.Default, out result)
            || (ShouldUsePrimitiveSearchProbe()
                && TryRunPrimitiveSearch(level, movementProfile, team, from, to, targetSurface, kind, out result));
    }

    public static bool TryCertifyEdgeForTeam(
        SimpleLevel level,
        BotBrainProbeNode from,
        BotBrainProbeNode to,
        BotBrainProbeSurface? targetSurface,
        NavEdgeKind kind,
        PlayerTeam team,
        out BotBrainMovementProbeResult result,
        out int supportedClassMask,
        bool useProbeEnsemble = false)
    {
        result = default;
        supportedClassMask = 0;
        if (kind == NavEdgeKind.Walk)
        {
            return false;
        }

        var profileResults = new List<BotBrainMovementProbeResult>(3);
        foreach (var movementProfile in EnumerateCertificationProfiles())
        {
            if (!TryCertifyForTeam(level, movementProfile, team, from, to, targetSurface, kind, useProbeEnsemble, out var profileResult))
            {
                continue;
            }

            supportedClassMask |= BotBrainClassMask.For(movementProfile.Id);
            profileResults.Add(profileResult);
        }

        if (profileResults.Count == 0)
        {
            return false;
        }

        result = profileResults[0];
        for (var i = 1; i < profileResults.Count; i += 1)
        {
            result = BotBrainMovementProbeResult.Merge(result, profileResults[i]);
        }

        return supportedClassMask != 0;
    }

    private static bool TryDiscoverLandingForTeam(
        SimpleLevel level,
        CharacterClassDefinition movementProfile,
        PlayerTeam team,
        BotBrainProbeNode from,
        BotBrainProbeNode to,
        IReadOnlyList<BotBrainProbeSurface> landingSurfaces,
        int excludedSurfaceId,
        NavEdgeKind kind,
        bool useProbeEnsemble,
        out BotBrainMovementProbeResult result,
        out int landingSurfaceId)
    {
        if (useProbeEnsemble)
        {
            return kind == NavEdgeKind.Jump
                ? TryRunLandingDiscoveryEnsemble(level, movementProfile, team, from, to, landingSurfaces, excludedSurfaceId, kind, JumpTriggerTicks, out result, out landingSurfaceId)
                : TryRunLandingDiscoveryEnsemble(level, movementProfile, team, from, to, landingSurfaces, excludedSurfaceId, kind, [-1], out result, out landingSurfaceId);
        }

        if (kind == NavEdgeKind.Jump)
        {
            foreach (var jumpTick in JumpTriggerTicks)
            {
                if (TryRunLandingDiscovery(level, movementProfile, team, from, to, landingSurfaces, excludedSurfaceId, kind, jumpTick, BotBrainProbeVariant.Default, out result, out landingSurfaceId))
                {
                    return true;
                }
            }

            result = default;
            landingSurfaceId = -1;
            return false;
        }

        return TryRunLandingDiscovery(level, movementProfile, team, from, to, landingSurfaces, excludedSurfaceId, kind, jumpTick: -1, BotBrainProbeVariant.Default, out result, out landingSurfaceId);
    }

    private static bool TryRunEnsemble(
        SimpleLevel level,
        CharacterClassDefinition movementProfile,
        PlayerTeam team,
        BotBrainProbeNode from,
        BotBrainProbeNode to,
        BotBrainProbeSurface? targetSurface,
        NavEdgeKind kind,
        IReadOnlyList<int> jumpTicks,
        out BotBrainMovementProbeResult result)
    {
        var direction = ResolveDirection(from, to);
        var attempts = 0;
        var successes = new List<BotBrainMovementProbeResult>();
        foreach (var jumpTick in jumpTicks)
        {
            foreach (var variant in EnumerateProbeVariants(direction))
            {
                attempts += 1;
                if (TryRun(level, movementProfile, team, from, to, targetSurface, kind, jumpTick, variant, out var variantResult))
                {
                    successes.Add(variantResult);
                }
            }
        }

        if (successes.Count == 0)
        {
            result = default;
            return false;
        }

        result = successes[0].WithRobustness(attempts, successes.Count);
        for (var i = 1; i < successes.Count; i += 1)
        {
            result = BotBrainMovementProbeResult.Merge(result, successes[i].WithRobustness(attempts, successes.Count));
        }

        return true;
    }

    private static bool TryRunLandingDiscoveryEnsemble(
        SimpleLevel level,
        CharacterClassDefinition movementProfile,
        PlayerTeam team,
        BotBrainProbeNode from,
        BotBrainProbeNode to,
        IReadOnlyList<BotBrainProbeSurface> landingSurfaces,
        int excludedSurfaceId,
        NavEdgeKind kind,
        IReadOnlyList<int> jumpTicks,
        out BotBrainMovementProbeResult result,
        out int landingSurfaceId)
    {
        var direction = ResolveDirection(from, to);
        var attempts = 0;
        var successesBySurface = new Dictionary<int, List<BotBrainMovementProbeResult>>();
        foreach (var jumpTick in jumpTicks)
        {
            foreach (var variant in EnumerateProbeVariants(direction))
            {
                attempts += 1;
                if (!TryRunLandingDiscovery(level, movementProfile, team, from, to, landingSurfaces, excludedSurfaceId, kind, jumpTick, variant, out var variantResult, out var variantSurfaceId))
                {
                    continue;
                }

                if (!successesBySurface.TryGetValue(variantSurfaceId, out var successes))
                {
                    successes = [];
                    successesBySurface.Add(variantSurfaceId, successes);
                }

                successes.Add(variantResult);
            }
        }

        if (successesBySurface.Count == 0)
        {
            result = default;
            landingSurfaceId = -1;
            return false;
        }

        var best = successesBySurface
            .OrderByDescending(static pair => pair.Value.Count)
            .ThenBy(static pair => pair.Value.Min(static success => success.Ticks))
            .First();
        landingSurfaceId = best.Key;
        result = best.Value[0].WithRobustness(attempts, best.Value.Count);
        for (var i = 1; i < best.Value.Count; i += 1)
        {
            result = BotBrainMovementProbeResult.Merge(result, best.Value[i].WithRobustness(attempts, best.Value.Count));
        }

        return true;
    }

    private static bool TryRun(
        SimpleLevel level,
        CharacterClassDefinition movementProfile,
        PlayerTeam team,
        BotBrainProbeNode from,
        BotBrainProbeNode to,
        BotBrainProbeSurface? targetSurface,
        NavEdgeKind kind,
        int jumpTick,
        BotBrainProbeVariant variant,
        out BotBrainMovementProbeResult result)
    {
        var direction = ResolveDirection(from, to);
        var startX = from.X + variant.StartXOffset;

        var player = new PlayerEntity(-900_001, movementProfile, "BotBrainProbe");
        player.Spawn(team, startX, from.Y);
        player.TeleportTo(startX, from.Y);
        player.ResolveBlockingOverlap(level, team);
        player.RestoreMovementProbeState(isGrounded: true, player.MaxAirJumps, direction);
        if (variant.StartHorizontalSpeed != 0f)
        {
            player.AddImpulse(variant.StartHorizontalSpeed, 0f);
        }

        var previousInput = default(PlayerInputSnapshot);
        var bestTargetDistanceSq = float.MaxValue;
        var hasBeenAirborne = false;
        var groundedTicksAfterAirborneBeforeCompletion = 0;
        var startGrounded = player.IsGrounded;
        BotBrainMovementLaunchRecipe? launchRecipe = null;
        for (var tick = 0; tick < MaxProbeTicks; tick += 1)
        {
            var input = CreateInput(player, to, kind, direction, tick == jumpTick);
            var jumpPressed = input.Up && !previousInput.Up;
            if (kind == NavEdgeKind.Jump && jumpPressed)
            {
                launchRecipe = BotBrainMovementLaunchRecipe.FromLaunch(
                    startGrounded,
                    tick,
                    player.X,
                    player.Y,
                    player.HorizontalSpeed,
                    direction,
                    LaunchHorizontalPadding,
                    LaunchVerticalPadding,
                    LaunchSpeedPadding);
            }

            player.Advance(input, jumpPressed, level, team, 1d / SimulationConfig.DefaultTicksPerSecond);
            previousInput = input;
            if (!player.IsGrounded)
            {
                hasBeenAirborne = true;
            }

            var dx = player.X - to.X;
            var dy = player.Y - to.Y;
            bestTargetDistanceSq = MathF.Min(bestTargetDistanceSq, (dx * dx) + (dy * dy));

            if (HasCompleted(player, to, targetSurface, out var acceptedSurfaceId))
            {
                var completionMinX = targetSurface.HasValue && acceptedSurfaceId >= 0
                    ? targetSurface.Value.LeftX - SurfaceLandingHorizontalSlack
                    : player.X - CompletionHorizontalPadding;
                var completionMaxX = targetSurface.HasValue && acceptedSurfaceId >= 0
                    ? targetSurface.Value.RightX + SurfaceLandingHorizontalSlack
                    : player.X + CompletionHorizontalPadding;
                result = BotBrainMovementProbeResult.FromLanding(
                    player.X,
                    player.Y,
                    acceptedSurfaceId,
                    tick + 1,
                    Math.Max(0, jumpTick),
                    direction,
                    CompletionHorizontalPadding,
                    CompletionVerticalPadding,
                    completionMinX,
                    completionMaxX,
                    groundedTicksAfterAirborneBeforeCompletion > 0,
                    launchRecipe);
                return true;
            }

            if (hasBeenAirborne && player.IsGrounded)
            {
                groundedTicksAfterAirborneBeforeCompletion += 1;
            }

            if (player.Y > MathF.Max(to.Y, from.Y) + 800f || bestTargetDistanceSq > 1_000_000f && tick > 20)
            {
                break;
            }
        }

        result = default;
        return false;
    }

    private static bool TryRunPrimitiveSearch(
        SimpleLevel level,
        CharacterClassDefinition movementProfile,
        PlayerTeam team,
        BotBrainProbeNode from,
        BotBrainProbeNode to,
        BotBrainProbeSurface? targetSurface,
        NavEdgeKind kind,
        out BotBrainMovementProbeResult result)
    {
        result = default;
        if (kind == NavEdgeKind.Walk)
        {
            return false;
        }

        var direction = ResolveDirection(from, to);
        var startPlayer = CreatePrimitiveProbePlayer(movementProfile, team, from.X, from.Y, direction);
        var start = BotBrainPrimitiveProbeState.FromPlayer(startPlayer);
        var actions = BuildPrimitiveProbeActions(kind, direction).ToArray();
        var queue = new PriorityQueue<BotBrainPrimitiveSearchNode, float>();
        var nodes = new List<BotBrainPrimitiveSearchNode>();
        var startNode = new BotBrainPrimitiveSearchNode(0, ParentIndex: -1, start, default, CostTicks: 0);
        nodes.Add(startNode);
        queue.Enqueue(startNode, PrimitiveHeuristic(start, to, targetSurface));
        var bestCostByKey = new Dictionary<BotBrainPrimitiveProbeKey, int>
        {
            [BotBrainPrimitiveProbeKey.From(start)] = 0,
        };
        var expanded = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var maxExpanded = GetPrimitiveSearchMaxExpanded();
        var budgetMilliseconds = GetPrimitiveSearchBudgetMilliseconds();
        while (queue.Count > 0
            && expanded < maxExpanded
            && stopwatch.ElapsedMilliseconds < budgetMilliseconds)
        {
            var current = queue.Dequeue();
            if (current.Index >= nodes.Count || nodes[current.Index].CostTicks != current.CostTicks)
            {
                continue;
            }

            expanded += 1;
            foreach (var action in actions)
            {
                if (!TrySimulatePrimitiveAction(
                        level,
                        movementProfile,
                        team,
                        current.State,
                        action,
                        to,
                        targetSurface,
                        out var next,
                        out var actualAction,
                        out var completion,
                        out var reachedTarget))
                {
                    continue;
                }

                var nextCost = current.CostTicks + actualAction.Ticks;
                var node = new BotBrainPrimitiveSearchNode(nodes.Count, current.Index, next, actualAction, nextCost);
                nodes.Add(node);
                if (reachedTarget)
                {
                    var path = BuildPrimitiveProbePath(nodes, node.Index);
                    result = CreatePrimitiveProbeResult(path, completion, direction);
                    return true;
                }

                var key = BotBrainPrimitiveProbeKey.From(next);
                if (bestCostByKey.TryGetValue(key, out var previousCost) && previousCost <= nextCost)
                {
                    continue;
                }

                bestCostByKey[key] = nextCost;
                queue.Enqueue(node, nextCost + PrimitiveHeuristic(next, to, targetSurface));
            }
        }

        return false;
    }

    private static bool TrySimulatePrimitiveAction(
        SimpleLevel level,
        CharacterClassDefinition movementProfile,
        PlayerTeam team,
        BotBrainPrimitiveProbeState start,
        BotBrainPrimitiveProbeAction action,
        BotBrainProbeNode to,
        BotBrainProbeSurface? targetSurface,
        out BotBrainPrimitiveProbeState next,
        out BotBrainPrimitiveProbeAction actualAction,
        out BotBrainPrimitiveProbeCompletion completion,
        out bool reachedTarget)
    {
        var player = CreatePrimitiveProbePlayer(movementProfile, team, start);
        var previousInput = default(PlayerInputSnapshot);
        var becameAirborne = !player.IsGrounded;
        actualAction = action with
        {
            StartX = player.X,
            StartY = player.Y,
            StartHorizontalSpeed = player.HorizontalSpeed,
        };
        completion = default;
        reachedTarget = false;
        for (var tick = 0; tick < action.Ticks; tick += 1)
        {
            var input = action.GetInput(tick, player, to);
            var jumpPressed = input.Up && !previousInput.Up;
            player.Advance(input, jumpPressed, level, team, 1d / SimulationConfig.DefaultTicksPerSecond);
            previousInput = input;
            if (!player.IsAlive)
            {
                next = default;
                return false;
            }

            if (!player.IsGrounded)
            {
                becameAirborne = true;
            }

            if (HasCompleted(player, to, targetSurface, out var acceptedSurfaceId))
            {
                next = BotBrainPrimitiveProbeState.FromPlayer(player);
                actualAction = actualAction with { Ticks = tick + 1 };
                completion = new BotBrainPrimitiveProbeCompletion(
                    player.X,
                    player.Y,
                    acceptedSurfaceId,
                    next.IsGrounded);
                reachedTarget = true;
                return true;
            }

            if (ShouldStopPrimitiveActionOnLanding(action, tick, player, becameAirborne))
            {
                next = BotBrainPrimitiveProbeState.FromPlayer(player);
                actualAction = actualAction with { Ticks = tick + 1 };
                return HasPrimitiveProgressed(start, next);
            }
        }

        next = BotBrainPrimitiveProbeState.FromPlayer(player);
        if (ShouldRequirePrimitiveLanding(action) && !next.IsGrounded)
        {
            return false;
        }

        return HasPrimitiveProgressed(start, next);
    }

    private static BotBrainMovementProbeResult CreatePrimitiveProbeResult(
        IReadOnlyList<BotBrainPrimitiveProbeAction> path,
        BotBrainPrimitiveProbeCompletion completion,
        float direction)
    {
        var jumpTick = -1;
        var elapsedTicks = 0;
        BotBrainMovementLaunchRecipe? launchRecipe = null;
        foreach (var action in path)
        {
            if (action.Kind == BotBrainPrimitiveProbeActionKind.Jump)
            {
                jumpTick = elapsedTicks;
                launchRecipe = BotBrainMovementLaunchRecipe.FromLaunch(
                    startGrounded: true,
                    launchTick: elapsedTicks,
                    action.StartX,
                    action.StartY,
                    action.StartHorizontalSpeed,
                    direction,
                    LaunchHorizontalPadding,
                    LaunchVerticalPadding,
                    LaunchSpeedPadding);
                break;
            }

            elapsedTicks += action.Ticks;
        }

        var totalTicks = path.Sum(static action => action.Ticks);
        return BotBrainMovementProbeResult.FromLanding(
            completion.X,
            completion.Y,
            completion.AcceptedSurfaceId,
            totalTicks,
            Math.Max(0, jumpTick),
            direction,
            CompletionHorizontalPadding,
            CompletionVerticalPadding,
            completion.X - CompletionHorizontalPadding,
            completion.X + CompletionHorizontalPadding,
            requiresGroundedContinuation: false,
            launchRecipe);
    }

    private static IReadOnlyList<BotBrainPrimitiveProbeAction> BuildPrimitiveProbePath(
        List<BotBrainPrimitiveSearchNode> nodes,
        int nodeIndex)
    {
        var actions = new List<BotBrainPrimitiveProbeAction>();
        var current = nodeIndex;
        while (current >= 0)
        {
            var node = nodes[current];
            if (node.ParentIndex >= 0)
            {
                actions.Add(node.Action);
            }

            current = node.ParentIndex;
        }

        actions.Reverse();
        return actions;
    }

    private static IEnumerable<BotBrainPrimitiveProbeAction> BuildPrimitiveProbeActions(NavEdgeKind edgeKind, float preferredDirection)
    {
        foreach (var direction in EnumeratePrimitiveDirections(preferredDirection))
        {
            foreach (var ticks in new[] { 8, 14, 22, 34, 52 })
            {
                yield return new BotBrainPrimitiveProbeAction(BotBrainPrimitiveProbeActionKind.Run, direction, ticks);
            }

            if (edgeKind == NavEdgeKind.Jump)
            {
                foreach (var ticks in new[] { 22, 34, 52, 90 })
                {
                    yield return new BotBrainPrimitiveProbeAction(BotBrainPrimitiveProbeActionKind.Jump, direction, ticks);
                }
            }

            if (edgeKind is NavEdgeKind.Fall or NavEdgeKind.Dropdown)
            {
                foreach (var ticks in new[] { 34, 52, 90 })
                {
                    yield return new BotBrainPrimitiveProbeAction(
                        edgeKind == NavEdgeKind.Dropdown ? BotBrainPrimitiveProbeActionKind.Drop : BotBrainPrimitiveProbeActionKind.Fall,
                        direction,
                        ticks);
                }
            }
        }

        if (edgeKind == NavEdgeKind.Jump)
        {
            foreach (var ticks in new[] { 22, 34, 52 })
            {
                yield return new BotBrainPrimitiveProbeAction(BotBrainPrimitiveProbeActionKind.Jump, 0, ticks);
            }
        }
    }

    private static IEnumerable<int> EnumeratePrimitiveDirections(float preferredDirection)
    {
        var preferred = preferredDirection < 0f ? -1 : 1;
        yield return preferred;
        yield return -preferred;
    }

    private static PlayerEntity CreatePrimitiveProbePlayer(
        CharacterClassDefinition movementProfile,
        PlayerTeam team,
        float x,
        float y,
        float facingDirection)
    {
        var player = new PlayerEntity(-900_003, movementProfile, "BotBrainPrimitiveProbe");
        player.Spawn(team, x, y);
        player.TeleportTo(x, y);
        player.RestoreMovementProbeState(isGrounded: true, player.MaxAirJumps, facingDirection);
        return player;
    }

    private static PlayerEntity CreatePrimitiveProbePlayer(
        CharacterClassDefinition movementProfile,
        PlayerTeam team,
        BotBrainPrimitiveProbeState state)
    {
        var player = new PlayerEntity(-900_004, movementProfile, "BotBrainPrimitiveProbe");
        player.Spawn(team, state.X, state.Y);
        player.TeleportTo(state.X, state.Y);
        player.AddImpulse(state.HorizontalSpeed, state.VerticalSpeed);
        player.RestoreMovementProbeState(state.IsGrounded, state.RemainingAirJumps, state.FacingDirectionX);
        player.SetMovementState(state.MovementState);
        return player;
    }

    private static bool ShouldStopPrimitiveActionOnLanding(
        BotBrainPrimitiveProbeAction action,
        int tick,
        PlayerEntity player,
        bool becameAirborne) =>
        ShouldRequirePrimitiveLanding(action)
        && becameAirborne
        && tick >= 6
        && player.IsGrounded
        && player.VerticalSpeed >= 0f;

    private static bool ShouldRequirePrimitiveLanding(BotBrainPrimitiveProbeAction action) =>
        action.Kind is BotBrainPrimitiveProbeActionKind.Jump or BotBrainPrimitiveProbeActionKind.Drop or BotBrainPrimitiveProbeActionKind.Fall;

    private static bool HasPrimitiveProgressed(BotBrainPrimitiveProbeState start, BotBrainPrimitiveProbeState next) =>
        Distance(start.X, start.Bottom, next.X, next.Bottom) >= PrimitiveSearchMinimumProgressDistance;

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static float PrimitiveHeuristic(
        BotBrainPrimitiveProbeState state,
        BotBrainProbeNode to,
        BotBrainProbeSurface? targetSurface)
    {
        var goalX = targetSurface.HasValue
            ? Math.Clamp(state.X, targetSurface.Value.LeftX, targetSurface.Value.RightX)
            : to.X;
        var goalY = targetSurface.HasValue ? targetSurface.Value.TopY : to.Y;
        return Distance(state.X, state.Bottom, goalX, goalY) * 0.18f;
    }

    private static bool ShouldUsePrimitiveSearchProbe() =>
        string.Equals(
            Environment.GetEnvironmentVariable("BOTBRAIN_MOTIONPROOF_PROBE"),
            "1",
            StringComparison.OrdinalIgnoreCase);

    private static int GetPrimitiveSearchMaxExpanded() =>
        int.TryParse(Environment.GetEnvironmentVariable("BOTBRAIN_MOTIONPROOF_PROBE_MAX_EXPANDED"), out var parsed)
            ? Math.Clamp(parsed, 1, 5_000)
            : PrimitiveSearchDefaultMaxExpanded;

    private static int GetPrimitiveSearchBudgetMilliseconds() =>
        int.TryParse(Environment.GetEnvironmentVariable("BOTBRAIN_MOTIONPROOF_PROBE_BUDGET_MS"), out var parsed)
            ? Math.Clamp(parsed, 1, 250)
            : PrimitiveSearchDefaultBudgetMilliseconds;

    private static bool TryRunLandingDiscovery(
        SimpleLevel level,
        CharacterClassDefinition movementProfile,
        PlayerTeam team,
        BotBrainProbeNode from,
        BotBrainProbeNode to,
        IReadOnlyList<BotBrainProbeSurface> landingSurfaces,
        int excludedSurfaceId,
        NavEdgeKind kind,
        int jumpTick,
        BotBrainProbeVariant variant,
        out BotBrainMovementProbeResult result,
        out int landingSurfaceId)
    {
        var direction = ResolveDirection(from, to);
        var startX = from.X + variant.StartXOffset;

        var player = new PlayerEntity(-900_002, movementProfile, "BotBrainLandingProbe");
        player.Spawn(team, startX, from.Y);
        player.TeleportTo(startX, from.Y);
        player.ResolveBlockingOverlap(level, team);
        player.RestoreMovementProbeState(isGrounded: true, player.MaxAirJumps, direction);
        if (variant.StartHorizontalSpeed != 0f)
        {
            player.AddImpulse(variant.StartHorizontalSpeed, 0f);
        }

        var previousInput = default(PlayerInputSnapshot);
        var bestTargetDistanceSq = float.MaxValue;
        var hasBeenAirborne = false;
        var groundedTicksAfterAirborneBeforeCompletion = 0;
        var startGrounded = player.IsGrounded;
        BotBrainMovementLaunchRecipe? launchRecipe = null;
        for (var tick = 0; tick < MaxProbeTicks; tick += 1)
        {
            var input = CreateInput(player, to, kind, direction, tick == jumpTick);
            var jumpPressed = input.Up && !previousInput.Up;
            if (kind == NavEdgeKind.Jump && jumpPressed)
            {
                launchRecipe = BotBrainMovementLaunchRecipe.FromLaunch(
                    startGrounded,
                    tick,
                    player.X,
                    player.Y,
                    player.HorizontalSpeed,
                    direction,
                    LaunchHorizontalPadding,
                    LaunchVerticalPadding,
                    LaunchSpeedPadding);
            }

            player.Advance(input, jumpPressed, level, team, 1d / SimulationConfig.DefaultTicksPerSecond);
            previousInput = input;
            if (!player.IsGrounded)
            {
                hasBeenAirborne = true;
            }

            var dx = player.X - to.X;
            var dy = player.Y - to.Y;
            bestTargetDistanceSq = MathF.Min(bestTargetDistanceSq, (dx * dx) + (dy * dy));

            if (hasBeenAirborne && player.IsGrounded)
            {
                if (TryFindLandingSurface(player, landingSurfaces, excludedSurfaceId, out var landingSurface))
                {
                    result = BotBrainMovementProbeResult.FromLanding(
                        player.X,
                        player.Y,
                        landingSurface.Id,
                        tick + 1,
                        Math.Max(0, jumpTick),
                        direction,
                        CompletionHorizontalPadding,
                        CompletionVerticalPadding,
                        landingSurface.LeftX - SurfaceLandingHorizontalSlack,
                        landingSurface.RightX + SurfaceLandingHorizontalSlack,
                        groundedTicksAfterAirborneBeforeCompletion > 0,
                        launchRecipe);
                    landingSurfaceId = landingSurface.Id;
                    return true;
                }

                groundedTicksAfterAirborneBeforeCompletion += 1;
            }

            if (player.Y > MathF.Max(to.Y, from.Y) + 1_200f || bestTargetDistanceSq > 1_000_000f && tick > 20)
            {
                break;
            }
        }

        result = default;
        landingSurfaceId = -1;
        return false;
    }

    private static float ResolveDirection(BotBrainProbeNode from, BotBrainProbeNode to)
    {
        var direction = (float)Math.Sign(to.X - from.X);
        return direction == 0f ? 1f : direction;
    }

    private static IEnumerable<BotBrainProbeVariant> EnumerateProbeVariants(float direction)
    {
        foreach (var startOffset in EnsembleStartOffsets)
        {
            foreach (var speedMagnitude in EnsembleIncomingSpeedMagnitudes)
            {
                yield return new BotBrainProbeVariant(startOffset, direction * speedMagnitude);
            }
        }
    }

    private static PlayerInputSnapshot CreateInput(
        PlayerEntity player,
        BotBrainProbeNode to,
        NavEdgeKind kind,
        float direction,
        bool jump)
    {
        return new PlayerInputSnapshot(
            Left: direction < 0f,
            Right: direction > 0f,
            Up: jump,
            Down: kind == NavEdgeKind.Dropdown,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: to.X,
            AimWorldY: to.Y,
            DebugKill: false);
    }

    private static bool HasCompleted(
        PlayerEntity player,
        BotBrainProbeNode to,
        BotBrainProbeSurface? targetSurface,
        out int acceptedSurfaceId)
    {
        acceptedSurfaceId = -1;
        if (!player.IsGrounded)
        {
            return false;
        }

        if (targetSurface.HasValue)
        {
            var surface = targetSurface.Value;
            var nearestX = Math.Clamp(player.X, surface.LeftX, surface.RightX);
            var horizontalError = MathF.Abs(player.X - nearestX);
            var verticalError = MathF.Abs(player.Bottom - surface.TopY);
            if (horizontalError <= SurfaceLandingHorizontalSlack && verticalError <= SurfaceLandingVerticalSlack)
            {
                acceptedSurfaceId = surface.Id;
                return true;
            }

            return false;
        }

        if (MathF.Abs(player.X - to.X) <= LandingHorizontalSlack
            && MathF.Abs(player.Y - to.Y) <= LandingVerticalSlack)
        {
            return true;
        }

        return false;
    }

    private static bool TryFindLandingSurface(
        PlayerEntity player,
        IReadOnlyList<BotBrainProbeSurface> landingSurfaces,
        int excludedSurfaceId,
        out BotBrainProbeSurface landingSurface)
    {
        landingSurface = default;
        if (!player.IsGrounded)
        {
            return false;
        }

        var bestScore = float.MaxValue;
        var found = false;
        foreach (var surface in landingSurfaces)
        {
            if (surface.Id == excludedSurfaceId)
            {
                continue;
            }

            var nearestX = Math.Clamp(player.X, surface.LeftX, surface.RightX);
            var horizontalError = MathF.Abs(player.X - nearestX);
            var verticalError = MathF.Abs(player.Bottom - surface.TopY);
            if (horizontalError > SurfaceLandingHorizontalSlack || verticalError > SurfaceLandingVerticalSlack)
            {
                continue;
            }

            var score = (verticalError * 100f) + horizontalError;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            landingSurface = surface;
            found = true;
        }

        return found;
    }

    private static int CountSupportedProfiles(int supportedClassMask)
    {
        var count = 0;
        foreach (var movementProfile in EnumerateCertificationProfiles())
        {
            if ((supportedClassMask & BotBrainClassMask.For(movementProfile.Id)) != 0)
            {
                count += 1;
            }
        }

        return count;
    }

    private readonly record struct LandingDiscoveryCandidate(
        int SurfaceId,
        int SupportedClassMask,
        BotBrainMovementProbeResult Result);

    private readonly record struct BotBrainProbeVariant(float StartXOffset, float StartHorizontalSpeed)
    {
        public static BotBrainProbeVariant Default { get; } = new(0f, 0f);
    }

    private readonly record struct BotBrainPrimitiveSearchNode(
        int Index,
        int ParentIndex,
        BotBrainPrimitiveProbeState State,
        BotBrainPrimitiveProbeAction Action,
        int CostTicks);

    private readonly record struct BotBrainPrimitiveProbeCompletion(
        float X,
        float Y,
        int AcceptedSurfaceId,
        bool IsGrounded);

    private readonly record struct BotBrainPrimitiveProbeAction(
        BotBrainPrimitiveProbeActionKind Kind,
        int Direction,
        int Ticks,
        float StartX = 0f,
        float StartY = 0f,
        float StartHorizontalSpeed = 0f)
    {
        public PlayerInputSnapshot GetInput(int tick, PlayerEntity player, BotBrainProbeNode to)
        {
            var left = Direction < 0;
            var right = Direction > 0;
            var up = Kind == BotBrainPrimitiveProbeActionKind.Jump && tick == 0;
            var down = Kind == BotBrainPrimitiveProbeActionKind.Drop;
            var aimDirection = Direction == 0 ? ResolveAimDirection(player, to) : Direction;
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

        private static int ResolveAimDirection(PlayerEntity player, BotBrainProbeNode to) =>
            to.X >= player.X ? 1 : -1;
    }

    private enum BotBrainPrimitiveProbeActionKind : byte
    {
        Run,
        Jump,
        Drop,
        Fall,
    }

    private readonly record struct BotBrainPrimitiveProbeState(
        float X,
        float Y,
        float Bottom,
        float HorizontalSpeed,
        float VerticalSpeed,
        bool IsGrounded,
        int RemainingAirJumps,
        LegacyMovementState MovementState,
        float FacingDirectionX)
    {
        public static BotBrainPrimitiveProbeState FromPlayer(PlayerEntity player) =>
            new(
                player.X,
                player.Y,
                player.Bottom,
                player.HorizontalSpeed,
                player.VerticalSpeed,
                player.IsGrounded,
                player.RemainingAirJumps,
                player.MovementState,
                player.FacingDirectionX);
    }

    private readonly record struct BotBrainPrimitiveProbeKey(
        int X,
        int Bottom,
        int HorizontalSpeed,
        int VerticalSpeed,
        bool IsGrounded,
        int RemainingAirJumps,
        int MovementState,
        int FacingDirectionX)
    {
        public static BotBrainPrimitiveProbeKey From(BotBrainPrimitiveProbeState state) =>
            new(
                Quantize(state.X, 18f),
                Quantize(state.Bottom, 18f),
                Quantize(state.HorizontalSpeed, 90f),
                Quantize(state.VerticalSpeed, 90f),
                state.IsGrounded,
                Math.Clamp(state.RemainingAirJumps, 0, 2),
                (int)state.MovementState,
                Quantize(state.FacingDirectionX, 1f));

        private static int Quantize(float value, float bucket) =>
            (int)MathF.Round(value / bucket);
    }
}

internal readonly record struct BotBrainProbeNode(float X, float Y);

internal readonly record struct BotBrainProbeSurface(int Id, float LeftX, float RightX, float TopY);

internal readonly record struct BotBrainMovementLaunchRecipe(
    bool StartGrounded,
    int LaunchTick,
    float LaunchMinX,
    float LaunchMaxX,
    float LaunchMinY,
    float LaunchMaxY,
    float LaunchMinHorizontalSpeed,
    float LaunchMaxHorizontalSpeed,
    float ExpectedMoveDirectionX)
{
    public static BotBrainMovementLaunchRecipe FromLaunch(
        bool startGrounded,
        int launchTick,
        float x,
        float y,
        float horizontalSpeed,
        float expectedMoveDirectionX,
        float horizontalPadding,
        float verticalPadding,
        float speedPadding)
    {
        return new BotBrainMovementLaunchRecipe(
            startGrounded,
            launchTick,
            x - horizontalPadding,
            x + horizontalPadding,
            y - verticalPadding,
            y + verticalPadding,
            horizontalSpeed - speedPadding,
            horizontalSpeed + speedPadding,
            MathF.Sign(expectedMoveDirectionX));
    }

    public static BotBrainMovementLaunchRecipe? Merge(
        BotBrainMovementLaunchRecipe? first,
        BotBrainMovementLaunchRecipe? second)
    {
        if (!first.HasValue)
        {
            return second;
        }

        if (!second.HasValue)
        {
            return first;
        }

        var a = first.Value;
        var b = second.Value;
        if (a.LaunchTick != b.LaunchTick)
        {
            return a.LaunchTick > b.LaunchTick ? a : b;
        }

        return new BotBrainMovementLaunchRecipe(
            a.StartGrounded && b.StartGrounded,
            Math.Max(a.LaunchTick, b.LaunchTick),
            MathF.Min(a.LaunchMinX, b.LaunchMinX),
            MathF.Max(a.LaunchMaxX, b.LaunchMaxX),
            MathF.Min(a.LaunchMinY, b.LaunchMinY),
            MathF.Max(a.LaunchMaxY, b.LaunchMaxY),
            MathF.Min(a.LaunchMinHorizontalSpeed, b.LaunchMinHorizontalSpeed),
            MathF.Max(a.LaunchMaxHorizontalSpeed, b.LaunchMaxHorizontalSpeed),
            ResolveMergedMoveDirection(a.ExpectedMoveDirectionX, b.ExpectedMoveDirectionX));
    }

    private static float ResolveMergedMoveDirection(float first, float second)
    {
        if (first == 0f)
        {
            return second;
        }

        if (second == 0f || MathF.Sign(first) == MathF.Sign(second))
        {
            return first;
        }

        return 0f;
    }
}

internal readonly record struct BotBrainMovementProbeResult(
    float CompletionMinX,
    float CompletionMaxX,
    float CompletionMinY,
    float CompletionMaxY,
    int[] AcceptedLandingSurfaceIds,
    int Ticks,
    int JumpTriggerTick,
    float MoveDirectionX,
    int VariantAttempts,
    int VariantSuccesses,
    bool RequiresGroundedContinuation,
    BotBrainMovementLaunchRecipe? LaunchRecipe)
{
    public static BotBrainMovementProbeResult FromLanding(
        float x,
        float y,
        int acceptedSurfaceId,
        int ticks,
        int jumpTriggerTick,
        float moveDirectionX,
        float horizontalPadding,
        float verticalPadding)
    {
        return FromLanding(
            x,
            y,
            acceptedSurfaceId,
            ticks,
            jumpTriggerTick,
            moveDirectionX,
            horizontalPadding,
            verticalPadding,
            x - horizontalPadding,
            x + horizontalPadding,
            requiresGroundedContinuation: false,
            launchRecipe: null);
    }

    public static BotBrainMovementProbeResult FromLanding(
        float x,
        float y,
        int acceptedSurfaceId,
        int ticks,
        int jumpTriggerTick,
        float moveDirectionX,
        float horizontalPadding,
        float verticalPadding,
        float completionMinX,
        float completionMaxX,
        bool requiresGroundedContinuation,
        BotBrainMovementLaunchRecipe? launchRecipe)
    {
        var acceptedSurfaces = acceptedSurfaceId >= 0 ? [acceptedSurfaceId] : Array.Empty<int>();
        return new BotBrainMovementProbeResult(
            completionMinX,
            completionMaxX,
            y - verticalPadding,
            y + verticalPadding,
            acceptedSurfaces,
            ticks,
            jumpTriggerTick,
            moveDirectionX,
            VariantAttempts: 1,
            VariantSuccesses: 1,
            requiresGroundedContinuation,
            launchRecipe);
    }

    public BotBrainMovementProbeResult WithRobustness(int variantAttempts, int variantSuccesses)
    {
        return this with
        {
            VariantAttempts = variantAttempts,
            VariantSuccesses = variantSuccesses,
        };
    }

    public static BotBrainMovementProbeResult Merge(BotBrainMovementProbeResult first, BotBrainMovementProbeResult second)
    {
        var acceptedSurfaces = first.AcceptedLandingSurfaceIds
            .Concat(second.AcceptedLandingSurfaceIds)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();
        return new BotBrainMovementProbeResult(
            MathF.Min(first.CompletionMinX, second.CompletionMinX),
            MathF.Max(first.CompletionMaxX, second.CompletionMaxX),
            MathF.Min(first.CompletionMinY, second.CompletionMinY),
            MathF.Max(first.CompletionMaxY, second.CompletionMaxY),
            acceptedSurfaces,
            Math.Max(first.Ticks, second.Ticks),
            Math.Max(first.JumpTriggerTick, second.JumpTriggerTick),
            ResolveMergedMoveDirection(first.MoveDirectionX, second.MoveDirectionX),
            Math.Max(first.VariantAttempts, second.VariantAttempts),
            Math.Max(first.VariantSuccesses, second.VariantSuccesses),
            first.RequiresGroundedContinuation || second.RequiresGroundedContinuation,
            BotBrainMovementLaunchRecipe.Merge(first.LaunchRecipe, second.LaunchRecipe));
    }

    private static float ResolveMergedMoveDirection(float first, float second)
    {
        if (first == 0f)
        {
            return second;
        }

        if (second == 0f || MathF.Sign(first) == MathF.Sign(second))
        {
            return first;
        }

        return 0f;
    }
}

internal static class BotBrainClassMask
{
    public const int All = -1;

    public static int For(PlayerClass playerClass) => 1 << (int)playerClass;

    public static bool Contains(int mask, PlayerClass playerClass) =>
        mask == All
        || (mask & For(playerClass)) != 0
        || IsCoveredByCertifiedMovementProfile(mask, playerClass);

    private static bool IsCoveredByCertifiedMovementProfile(int mask, PlayerClass playerClass)
    {
        var candidate = CharacterClassCatalog.GetDefinition(playerClass);
        foreach (var certifiedProfile in EnumerateCertifiedProfiles(mask))
        {
            if (CanSubstituteForCertifiedProfile(candidate, certifiedProfile))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<CharacterClassDefinition> EnumerateCertifiedProfiles(int mask)
    {
        if ((mask & For(PlayerClass.Heavy)) != 0)
        {
            yield return CharacterClassCatalog.Heavy;
        }

        if ((mask & For(PlayerClass.Soldier)) != 0)
        {
            yield return CharacterClassCatalog.Soldier;
        }

        if ((mask & For(PlayerClass.Scout)) != 0)
        {
            yield return CharacterClassCatalog.Scout;
        }

        if ((mask & For(PlayerClass.Sniper)) != 0)
        {
            yield return CharacterClassCatalog.Sniper;
        }

        if ((mask & For(PlayerClass.Engineer)) != 0)
        {
            yield return CharacterClassCatalog.Engineer;
        }

        if ((mask & For(PlayerClass.Demoman)) != 0)
        {
            yield return CharacterClassCatalog.Demoman;
        }

        if ((mask & For(PlayerClass.Quote)) != 0
            && CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(PlayerClass.Quote, out _))
        {
            yield return CharacterClassCatalog.Civilian;
        }

        if ((mask & For(PlayerClass.Spy)) != 0)
        {
            yield return CharacterClassCatalog.Spy;
        }

        if ((mask & For(PlayerClass.Medic)) != 0)
        {
            yield return CharacterClassCatalog.Medic;
        }

        if ((mask & For(PlayerClass.Pyro)) != 0)
        {
            yield return CharacterClassCatalog.Pyro;
        }
    }

    private static bool CanSubstituteForCertifiedProfile(
        CharacterClassDefinition candidate,
        CharacterClassDefinition certifiedProfile)
    {
        return candidate.RunPower >= certifiedProfile.RunPower
            && candidate.JumpStrength >= certifiedProfile.JumpStrength
            && candidate.MaxAirJumps >= certifiedProfile.MaxAirJumps
            && candidate.CollisionLeft >= certifiedProfile.CollisionLeft
            && candidate.CollisionRight <= certifiedProfile.CollisionRight
            && candidate.CollisionTop >= certifiedProfile.CollisionTop
            && candidate.CollisionBottom <= certifiedProfile.CollisionBottom;
    }
}

internal static class BotBrainTeamMask
{
    public const int All = -1;

    public static int For(PlayerTeam team) => 1 << (int)team;

    public static bool Contains(int mask, PlayerTeam team) =>
        mask == All || (mask & For(team)) != 0;
}
