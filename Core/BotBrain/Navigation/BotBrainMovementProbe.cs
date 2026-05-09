namespace OpenGarrison.Core.BotBrain;

internal static class BotBrainMovementProbe
{
    private const int MaxProbeTicks = 120;
    private const float LandingHorizontalSlack = 36f;
    private const float SurfaceLandingHorizontalSlack = 25f;
    private const float LandingVerticalSlack = 14f;
    private const float CompletionHorizontalPadding = 42f;
    private const float CompletionVerticalPadding = 18f;
    private const float LaunchHorizontalPadding = 16f;
    private const float LaunchVerticalPadding = 8f;
    private const float LaunchSpeedPadding = 16f;
    private static readonly int[] JumpTriggerTicks = [0, 3, 6, 10];

    public static bool TryCertifyTeamAgnosticEdge(
        SimpleLevel level,
        BotBrainProbeNode from,
        BotBrainProbeNode to,
        BotBrainProbeSurface? targetSurface,
        NavEdgeKind kind,
        out BotBrainMovementProbeResult result,
        out int supportedClassMask)
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
            if (!TryCertifyForTeam(level, movementProfile, PlayerTeam.Red, from, to, targetSurface, kind, out var redResult)
                || !TryCertifyForTeam(level, movementProfile, PlayerTeam.Blue, from, to, targetSurface, kind, out var blueResult))
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
        out int landingSurfaceId)
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
            if (!TryDiscoverLandingForTeam(level, movementProfile, PlayerTeam.Red, from, to, landingSurfaces, excludedSurfaceId, kind, out var redResult, out var redSurfaceId)
                || !TryDiscoverLandingForTeam(level, movementProfile, PlayerTeam.Blue, from, to, landingSurfaces, excludedSurfaceId, kind, out var blueResult, out var blueSurfaceId)
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
        out int landingSurfaceId)
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
            if (!TryDiscoverLandingForTeam(level, movementProfile, team, from, to, landingSurfaces, excludedSurfaceId, kind, out var profileResult, out var profileSurfaceId))
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
        out BotBrainMovementProbeResult result)
    {
        if (kind == NavEdgeKind.Jump)
        {
            foreach (var jumpTick in JumpTriggerTicks)
            {
                if (TryRun(level, movementProfile, team, from, to, targetSurface, kind, jumpTick, out result))
                {
                    return true;
                }
            }

            result = default;
            return false;
        }

        return TryRun(level, movementProfile, team, from, to, targetSurface, kind, jumpTick: -1, out result);
    }

    public static bool TryCertifyEdgeForTeam(
        SimpleLevel level,
        BotBrainProbeNode from,
        BotBrainProbeNode to,
        BotBrainProbeSurface? targetSurface,
        NavEdgeKind kind,
        PlayerTeam team,
        out BotBrainMovementProbeResult result,
        out int supportedClassMask)
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
            if (!TryCertifyForTeam(level, movementProfile, team, from, to, targetSurface, kind, out var profileResult))
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
        out BotBrainMovementProbeResult result,
        out int landingSurfaceId)
    {
        if (kind == NavEdgeKind.Jump)
        {
            foreach (var jumpTick in JumpTriggerTicks)
            {
                if (TryRunLandingDiscovery(level, movementProfile, team, from, to, landingSurfaces, excludedSurfaceId, kind, jumpTick, out result, out landingSurfaceId))
                {
                    return true;
                }
            }

            result = default;
            landingSurfaceId = -1;
            return false;
        }

        return TryRunLandingDiscovery(level, movementProfile, team, from, to, landingSurfaces, excludedSurfaceId, kind, jumpTick: -1, out result, out landingSurfaceId);
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
        out BotBrainMovementProbeResult result)
    {
        var direction = (float)Math.Sign(to.X - from.X);
        if (direction == 0f)
        {
            direction = 1f;
        }

        var player = new PlayerEntity(-900_001, movementProfile, "BotBrainProbe");
        player.Spawn(team, from.X, from.Y);
        player.TeleportTo(from.X, from.Y);
        player.ResolveBlockingOverlap(level, team);
        player.RestoreMovementProbeState(isGrounded: true, player.MaxAirJumps, direction);

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
        out BotBrainMovementProbeResult result,
        out int landingSurfaceId)
    {
        var direction = (float)Math.Sign(to.X - from.X);
        if (direction == 0f)
        {
            direction = 1f;
        }

        var player = new PlayerEntity(-900_002, movementProfile, "BotBrainLandingProbe");
        player.Spawn(team, from.X, from.Y);
        player.TeleportTo(from.X, from.Y);
        player.ResolveBlockingOverlap(level, team);
        player.RestoreMovementProbeState(isGrounded: true, player.MaxAirJumps, direction);

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
            if (horizontalError <= SurfaceLandingHorizontalSlack && verticalError <= LandingVerticalSlack)
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
            if (horizontalError > SurfaceLandingHorizontalSlack || verticalError > LandingVerticalSlack)
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
            requiresGroundedContinuation,
            launchRecipe);
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

        if ((mask & For(PlayerClass.Quote)) != 0)
        {
            yield return CharacterClassCatalog.Quote;
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
