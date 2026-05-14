using System;

namespace OpenGarrison.Core.BotBrain;

public static class VerifiedNavSurfaceExplorer
{
    private const float LandingBottomTolerance = 8f;
    private const float IntelMarkerSize = 24f;

    public static VerifiedNavExplorationReport Explore(
        SimpleLevel level,
        VerifiedNavCandidateGraph graph,
        int startSurfaceId,
        VerifiedNavExplorationOptions options)
    {
        return ExploreMany(level, graph, [startSurfaceId], options);
    }

    public static VerifiedNavExplorationReport ExploreMany(
        SimpleLevel level,
        VerifiedNavCandidateGraph graph,
        IReadOnlyCollection<int> startSurfaceIds,
        VerifiedNavExplorationOptions options)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(startSurfaceIds);

        var normalizedStartSurfaceIds = startSurfaceIds
            .Where(surfaceId => surfaceId >= 0 && surfaceId < graph.Surfaces.Count)
            .Distinct()
            .ToArray();
        if (normalizedStartSurfaceIds.Length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startSurfaceIds), "At least one start surface must be inside the graph.");
        }

        var definition = CharacterClassCatalog.GetDefinition(graph.ClassId);
        var queue = new PriorityQueue<ExplorationState, float>();
        var reachable = new HashSet<int>(normalizedStartSurfaceIds);
        var reachedEnemyIntelMarker = false;
        var reachedOwnIntelMarker = false;
        var expanded = new HashSet<ExplorationStateCell>();
        var edgeKeys = new HashSet<ExploredEdgeKey>();
        var edges = new List<VerifiedNavExploredEdge>();
        foreach (var startSurfaceId in normalizedStartSurfaceIds)
        {
            foreach (var state in BuildStartStates(graph.Surfaces[startSurfaceId], definition, options.SurfaceProbeInset))
            {
                queue.Enqueue(state, ScoreState(graph, options.TargetSurfaceId, state));
                expanded.Add(ExplorationStateCell.From(state));
            }
        }

        while (queue.Count > 0 && expanded.Count < options.MaxSurfaceExpansions)
        {
            var state = queue.Dequeue();
            var surface = graph.Surfaces[state.SurfaceId];
            foreach (var macro in BuildMacros(options))
            {
                var result = SimulateMacro(level, graph, definition, state, macro);
                if (!result.Valid)
                {
                    continue;
                }

                reachedEnemyIntelMarker |= result.OverlappedEnemyIntelMarker;
                reachedOwnIntelMarker |= result.OverlappedOwnIntelMarker;
                var combinedMacro = string.IsNullOrWhiteSpace(state.PendingMacro)
                    ? macro.Label
                    : $"{state.PendingMacro}+{macro.Label}";
                var key = new ExploredEdgeKey(state.SurfaceId, result.SurfaceId, combinedMacro, (int)MathF.Round(state.X / 8f));
                if (result.IsGrounded && result.SurfaceId != state.SurfaceId && edgeKeys.Add(key))
                {
                    edges.Add(new VerifiedNavExploredEdge(
                        state.SurfaceId,
                        result.SurfaceId,
                        state.X,
                        surface.Top,
                        result.X,
                        result.Bottom,
                        macro.DurationTicks,
                        combinedMacro));
                }

                if (result.IsGrounded)
                {
                    reachable.Add(result.SurfaceId);
                }

                var nextState = result.ToState(combinedMacro);
                var cell = ExplorationStateCell.From(nextState);
                if (expanded.Add(cell))
                {
                    queue.Enqueue(nextState, ScoreState(graph, options.TargetSurfaceId, nextState));
                }
            }
        }

        return new VerifiedNavExplorationReport
        {
            LevelName = graph.LevelName,
            MapAreaIndex = graph.MapAreaIndex,
            Team = graph.Team,
            ClassId = graph.ClassId,
            StartSurfaceId = normalizedStartSurfaceIds[0],
            StartSurfaceIds = normalizedStartSurfaceIds.Order().ToList(),
            ExpandedSurfaceCount = expanded.Count,
            ReachableSurfaceCount = reachable.Count,
            ReachedEnemyIntelMarker = reachedEnemyIntelMarker,
            ReachedOwnIntelMarker = reachedOwnIntelMarker,
            ReachableSurfaceIds = reachable.Order().ToList(),
            Edges = edges,
        };
    }

    private static float ScoreState(
        VerifiedNavCandidateGraph graph,
        int targetSurfaceId,
        ExplorationState state)
    {
        if (targetSurfaceId < 0 || targetSurfaceId >= graph.Surfaces.Count)
        {
            return state.IsGrounded ? 0f : 100f;
        }

        var target = graph.Surfaces[targetSurfaceId];
        var dx = target.CenterX - state.X;
        var dy = target.Top - state.Bottom;
        var airbornePenalty = state.IsGrounded ? 0f : 32f;
        var velocityTowardTarget = MathF.Sign(dx) == MathF.Sign(state.HorizontalSpeed)
            ? MathF.Min(120f, MathF.Abs(state.HorizontalSpeed))
            : 0f;
        return MathF.Sqrt((dx * dx) + (dy * dy)) + airbornePenalty - velocityTowardTarget;
    }

    private static IEnumerable<ExplorationState> BuildStartStates(
        VerifiedNavSurface surface,
        CharacterClassDefinition definition,
        float inset)
    {
        foreach (var x in BuildSurfaceProbeXs(surface, inset))
        {
            yield return new ExplorationState(
                surface.Id,
                x,
                surface.Top,
                HorizontalSpeed: 0f,
                VerticalSpeed: 0f,
                IsGrounded: true,
                RemainingAirJumps: definition.MaxAirJumps,
                FacingDirectionX: 1f,
                PendingMacro: string.Empty);
            yield return new ExplorationState(
                surface.Id,
                x,
                surface.Top,
                HorizontalSpeed: 0f,
                VerticalSpeed: 0f,
                IsGrounded: true,
                RemainingAirJumps: definition.MaxAirJumps,
                FacingDirectionX: -1f,
                PendingMacro: string.Empty);
        }
    }

    private static IEnumerable<float> BuildSurfaceProbeXs(VerifiedNavSurface surface, float inset)
    {
        var left = MathF.Min(surface.Right, surface.Left + MathF.Max(0f, inset));
        var right = MathF.Max(surface.Left, surface.Right - MathF.Max(0f, inset));
        var center = (surface.Left + surface.Right) * 0.5f;
        yield return left;
        if (MathF.Abs(center - left) > 4f && MathF.Abs(center - right) > 4f)
        {
            yield return center;
        }

        if (MathF.Abs(right - left) > 4f)
        {
            yield return right;
        }
    }

    private static IEnumerable<ExplorationMacro> BuildMacros(VerifiedNavExplorationOptions options)
    {
        foreach (var duration in options.Durations.Where(duration => duration <= Math.Max(1, options.MaxMacroTicks)))
        {
            foreach (var direction in new[] { -1, 1 })
            {
                yield return new ExplorationMacro($"run.{direction}.{duration}", direction, duration, 0, Drop: false);
                yield return new ExplorationMacro($"drop.{direction}.{duration}", direction, duration, 0, Drop: true);
                foreach (var jumpHold in options.JumpHoldTicks)
                {
                    yield return new ExplorationMacro($"jump.{direction}.{duration}.{jumpHold}", direction, duration, Math.Min(duration, jumpHold), Drop: false);
                    yield return new ExplorationMacro($"dropjump.{direction}.{duration}.{jumpHold}", direction, duration, Math.Min(duration, jumpHold), Drop: true);
                }
            }

            yield return new ExplorationMacro($"idle.{duration}", 0, duration, 0, Drop: false);
            yield return new ExplorationMacro($"drop.neutral.{duration}", 0, duration, 0, Drop: true);
            foreach (var jumpHold in options.JumpHoldTicks)
            {
                yield return new ExplorationMacro($"jump.neutral.{duration}.{jumpHold}", 0, duration, Math.Min(duration, jumpHold), Drop: false);
            }
        }
    }

    private static ExplorationResult SimulateMacro(
        SimpleLevel level,
        VerifiedNavCandidateGraph graph,
        CharacterClassDefinition definition,
        ExplorationState state,
        ExplorationMacro macro)
    {
        var player = new PlayerEntity(1, definition, "VerifiedNavExplorer");
        var startY = state.Bottom - player.CollisionBottomOffset;
        player.Spawn(graph.Team, state.X, startY);
        player.TeleportTo(state.X, startY);
        if (state.HorizontalSpeed != 0f || state.VerticalSpeed != 0f)
        {
            player.AddImpulse(state.HorizontalSpeed, state.VerticalSpeed);
        }

        player.RestoreMovementProbeState(
            state.IsGrounded,
            state.RemainingAirJumps,
            macro.Direction == 0 ? state.FacingDirectionX : macro.Direction);

        var previousInput = default(PlayerInputSnapshot);
        var overlappedEnemyIntelMarker = IsOverlappingIntelMarker(level, graph.Team, opposing: true, player);
        var overlappedOwnIntelMarker = IsOverlappingIntelMarker(level, graph.Team, opposing: false, player);
        for (var tick = 0; tick < macro.DurationTicks; tick += 1)
        {
            var input = CreateInput(player, macro, tick);
            var jumpPressed = input.Up && !previousInput.Up;
            player.Advance(input, jumpPressed, level, graph.Team, 1d / SimulationConfig.DefaultTicksPerSecond);
            previousInput = input;
            overlappedEnemyIntelMarker |= IsOverlappingIntelMarker(level, graph.Team, opposing: true, player);
            overlappedOwnIntelMarker |= IsOverlappingIntelMarker(level, graph.Team, opposing: false, player);
            if (player.Bottom < -64f || player.Bottom > level.Bounds.Height + 128f)
            {
                return ExplorationResult.Invalid;
            }
        }

        if (!player.IsGrounded)
        {
            return new ExplorationResult(
                true,
                state.SurfaceId,
                player.X,
                player.Bottom,
                player.HorizontalSpeed,
                player.VerticalSpeed,
                player.IsGrounded,
                player.RemainingAirJumps,
                player.FacingDirectionX,
                overlappedEnemyIntelMarker,
                overlappedOwnIntelMarker);
        }

        if (!TryFindLandingSurface(player.X, player.Bottom, level, graph, definition, out var surfaceId))
        {
            surfaceId = state.SurfaceId;
        }

        return new ExplorationResult(
            true,
            surfaceId,
            player.X,
            player.Bottom,
            player.HorizontalSpeed,
            player.VerticalSpeed,
            player.IsGrounded,
            player.RemainingAirJumps,
            player.FacingDirectionX,
            overlappedEnemyIntelMarker,
            overlappedOwnIntelMarker);
    }

    private static bool IsOverlappingIntelMarker(
        SimpleLevel level,
        PlayerTeam team,
        bool opposing,
        PlayerEntity player)
    {
        var marker = level.GetIntelBase(opposing ? GetOpposingTeam(team) : team);
        return marker.HasValue
            && player.IntersectsMarker(marker.Value.X, marker.Value.Y, IntelMarkerSize, IntelMarkerSize);
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }

    private static PlayerInputSnapshot CreateInput(
        PlayerEntity player,
        ExplorationMacro macro,
        int tick)
    {
        var aimDirection = macro.Direction == 0 ? player.FacingDirectionX : macro.Direction;
        return new PlayerInputSnapshot(
            Left: macro.Direction < 0,
            Right: macro.Direction > 0,
            Up: tick < macro.JumpHoldTicks,
            Down: macro.Drop,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + (aimDirection * 256f),
            AimWorldY: player.Y,
            DebugKill: false,
            DropIntel: false,
            UseAbility: false);
    }

    private static bool TryFindLandingSurface(
        float x,
        float bottom,
        SimpleLevel level,
        VerifiedNavCandidateGraph graph,
        CharacterClassDefinition definition,
        out int surfaceId)
    {
        var probe = new PlayerEntity(1, definition, "VerifiedNavLandingProbe");
        foreach (var surface in graph.Surfaces)
        {
            if (x < surface.Left - 2f
                || x > surface.Right + 2f
                || MathF.Abs(bottom - surface.Top) > LandingBottomTolerance)
            {
                continue;
            }

            var y = surface.Top - probe.CollisionBottomOffset;
            probe.TeleportTo(x, y);
            if (probe.CanOccupy(level, graph.Team, x, y))
            {
                surfaceId = surface.Id;
                return true;
            }
        }

        surfaceId = -1;
        return false;
    }

    private readonly record struct ExplorationMacro(
        string Label,
        int Direction,
        int DurationTicks,
        int JumpHoldTicks,
        bool Drop);

    private readonly record struct ExplorationResult(
        bool Valid,
        int SurfaceId,
        float X,
        float Bottom,
        float HorizontalSpeed,
        float VerticalSpeed,
        bool IsGrounded,
        int RemainingAirJumps,
        float FacingDirectionX,
        bool OverlappedEnemyIntelMarker,
        bool OverlappedOwnIntelMarker)
    {
        public static ExplorationResult Invalid => new(false, -1, 0f, 0f, 0f, 0f, false, 0, 1f, false, false);

        public ExplorationState ToState(string combinedMacro)
        {
            var pendingMacro = IsGrounded ? string.Empty : combinedMacro;
            if (pendingMacro.Length > 240)
            {
                pendingMacro = pendingMacro[^240..];
            }

            return new ExplorationState(
                SurfaceId,
                X,
                Bottom,
                HorizontalSpeed,
                VerticalSpeed,
                IsGrounded,
                RemainingAirJumps,
                FacingDirectionX,
                pendingMacro);
        }
    }

    private readonly record struct ExploredEdgeKey(
        int FromSurfaceId,
        int ToSurfaceId,
        string Macro,
        int EntryXBucket);

    private readonly record struct ExplorationState(
        int SurfaceId,
        float X,
        float Bottom,
        float HorizontalSpeed,
        float VerticalSpeed,
        bool IsGrounded,
        int RemainingAirJumps,
        float FacingDirectionX,
        string PendingMacro);

    private readonly record struct ExplorationStateCell(
        int SurfaceId,
        int XBucket,
        int BottomBucket,
        int HorizontalSpeedBucket,
        int VerticalSpeedBucket,
        bool Grounded,
        int Facing)
    {
        public static ExplorationStateCell From(ExplorationState state)
        {
            return new ExplorationStateCell(
                state.SurfaceId,
                (int)MathF.Round(state.X / 24f),
                (int)MathF.Round(state.Bottom / 24f),
                (int)MathF.Round(state.HorizontalSpeed / 60f),
                (int)MathF.Round(state.VerticalSpeed / 60f),
                state.IsGrounded,
                state.FacingDirectionX < 0f ? -1 : 1);
        }
    }
}
