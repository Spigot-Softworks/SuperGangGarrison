using System;

namespace OpenGarrison.Core.BotBrain;

public static class VerifiedNavCandidateBuilder
{
    private const float SurfaceMergeVerticalEpsilon = 0.1f;
    private const float PortalSnapVerticalLimit = 96f;

    public static VerifiedNavCandidateGraph Build(SimpleLevel level, VerifiedNavBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(options);

        var definition = CharacterClassCatalog.GetDefinition(options.ClassId);
        var probe = new PlayerEntity(1, definition, "VerifiedNavProbe");
        var surfaces = ExtractStandableSurfaces(level, options, probe);
        var portals = BuildPortals(level, options, surfaces);
        var edges = BuildCandidateEdges(surfaces, options);

        return new VerifiedNavCandidateGraph
        {
            LevelName = level.Name,
            MapAreaIndex = level.MapAreaIndex,
            Team = options.Team,
            ClassId = options.ClassId,
            PlayerCollisionLeft = probe.CollisionLeftOffset,
            PlayerCollisionRight = probe.CollisionRightOffset,
            PlayerCollisionBottom = probe.CollisionBottomOffset,
            Surfaces = surfaces,
            Portals = portals,
            CandidateEdges = edges,
        };
    }

    private static List<VerifiedNavSurface> ExtractStandableSurfaces(
        SimpleLevel level,
        VerifiedNavBuildOptions options,
        PlayerEntity probe)
    {
        var rawSurfaces = new List<RawSurface>();
        for (var index = 0; index < level.Solids.Count; index += 1)
        {
            var solid = level.Solids[index];
            rawSurfaces.Add(new RawSurface(
                VerifiedNavSurfaceKind.SolidTop,
                solid.Left,
                solid.Right,
                solid.Top,
                index));
        }

        var platformOffset = level.Solids.Count;
        var platforms = level.GetRoomObjects(RoomObjectType.DropdownPlatform);
        for (var index = 0; index < platforms.Count; index += 1)
        {
            var platform = platforms[index];
            rawSurfaces.Add(new RawSurface(
                VerifiedNavSurfaceKind.DropdownPlatform,
                platform.Left,
                platform.Right,
                platform.Top,
                platformOffset + index));
        }

        var surfaces = new List<VerifiedNavSurface>();
        foreach (var rawSurface in rawSurfaces)
        {
            AppendStandableWindows(level, options, probe, rawSurface, surfaces);
        }

        return MergeAdjacentSurfaces(surfaces, options.MinSurfaceWidth)
            .OrderBy(static surface => surface.Top)
            .ThenBy(static surface => surface.Left)
            .Select(static (surface, index) => surface with { Id = index })
            .ToList();
    }

    private static void AppendStandableWindows(
        SimpleLevel level,
        VerifiedNavBuildOptions options,
        PlayerEntity probe,
        RawSurface rawSurface,
        List<VerifiedNavSurface> surfaces)
    {
        var minX = rawSurface.Left - probe.CollisionLeftOffset + options.SurfaceEndpointInset;
        var maxX = rawSurface.Right - probe.CollisionRightOffset - options.SurfaceEndpointInset;
        if (maxX - minX < options.MinSurfaceWidth)
        {
            return;
        }

        float? windowStart = null;
        var lastGoodX = minX;
        var sampleStep = MathF.Max(2f, options.SampleStep);
        for (var x = minX; x <= maxX + 0.001f; x += sampleStep)
        {
            var sampleX = MathF.Min(x, maxX);
            if (CanStandOnSurface(level, options.Team, probe, rawSurface, sampleX))
            {
                windowStart ??= sampleX;
                lastGoodX = sampleX;
                continue;
            }

            FlushWindow(rawSurface, windowStart, lastGoodX, surfaces, options.MinSurfaceWidth);
            windowStart = null;
        }

        FlushWindow(rawSurface, windowStart, lastGoodX, surfaces, options.MinSurfaceWidth);
    }

    private static bool CanStandOnSurface(
        SimpleLevel level,
        PlayerTeam team,
        PlayerEntity probe,
        RawSurface surface,
        float x)
    {
        var y = surface.Top - probe.CollisionBottomOffset;
        probe.TeleportTo(x, y);
        probe.GetCollisionBoundsAt(x, y, out var left, out var top, out var right, out var bottom);
        if (left < 0f
            || top < 0f
            || right > level.Bounds.Width
            || bottom > level.Bounds.Height)
        {
            return false;
        }

        if (!probe.CanOccupy(level, team, x, y))
        {
            return false;
        }

        return surface.Kind == VerifiedNavSurfaceKind.DropdownPlatform
            ? x + probe.CollisionRightOffset > surface.Left && x + probe.CollisionLeftOffset < surface.Right
            : !probe.CanOccupy(level, team, x, y + 1f);
    }

    private static void FlushWindow(
        RawSurface rawSurface,
        float? windowStart,
        float windowEnd,
        List<VerifiedNavSurface> surfaces,
        float minSurfaceWidth)
    {
        if (!windowStart.HasValue)
        {
            return;
        }

        if (windowEnd - windowStart.Value < minSurfaceWidth)
        {
            return;
        }

        surfaces.Add(new VerifiedNavSurface(
            surfaces.Count,
            rawSurface.Kind,
            windowStart.Value,
            windowEnd,
            rawSurface.Top,
            rawSurface.SourceIndex));
    }

    private static List<VerifiedNavSurface> MergeAdjacentSurfaces(
        List<VerifiedNavSurface> surfaces,
        float minSurfaceWidth)
    {
        var ordered = surfaces
            .OrderBy(static surface => surface.Kind)
            .ThenBy(static surface => surface.Top)
            .ThenBy(static surface => surface.Left)
            .ToList();
        var merged = new List<VerifiedNavSurface>();
        foreach (var surface in ordered)
        {
            if (merged.Count == 0)
            {
                merged.Add(surface);
                continue;
            }

            var previous = merged[^1];
            if (previous.Kind == surface.Kind
                && MathF.Abs(previous.Top - surface.Top) <= SurfaceMergeVerticalEpsilon
                && surface.Left <= previous.Right + 1f)
            {
                merged[^1] = previous with
                {
                    Right = MathF.Max(previous.Right, surface.Right),
                    SourceIndex = Math.Min(previous.SourceIndex, surface.SourceIndex),
                };
                continue;
            }

            merged.Add(surface);
        }

        return merged;
    }

    private static List<VerifiedNavPortal> BuildPortals(
        SimpleLevel level,
        VerifiedNavBuildOptions options,
        IReadOnlyList<VerifiedNavSurface> surfaces)
    {
        var portals = new List<VerifiedNavPortal>();
        foreach (var surface in surfaces)
        {
            portals.Add(new VerifiedNavPortal(
                portals.Count,
                VerifiedNavPortalKind.SurfaceLeft,
                surface.Left,
                surface.Top,
                surface.Id,
                $"surface:{surface.Id}:left"));
            portals.Add(new VerifiedNavPortal(
                portals.Count,
                VerifiedNavPortalKind.SurfaceRight,
                surface.Right,
                surface.Top,
                surface.Id,
                $"surface:{surface.Id}:right"));
        }

        AppendSpawnPortals(level.GetSpawn(options.Team, 0), "spawn:primary", VerifiedNavPortalKind.Spawn, surfaces, portals);
        if (level.GetIntelBase(GetOpposingTeam(options.Team)) is { } enemyIntel)
        {
            AppendObjectivePortal(enemyIntel.X, enemyIntel.Y, "intel:enemy", VerifiedNavPortalKind.EnemyIntel, surfaces, portals);
        }

        if (level.GetIntelBase(options.Team) is { } ownIntel)
        {
            AppendObjectivePortal(ownIntel.X, ownIntel.Y, "intel:own", VerifiedNavPortalKind.OwnIntel, surfaces, portals);
        }

        foreach (var captureZone in level.GetRoomObjects(RoomObjectType.CaptureZone))
        {
            AppendObjectivePortal(
                captureZone.CenterX,
                captureZone.Bottom,
                $"capture:{portals.Count}",
                VerifiedNavPortalKind.CaptureZone,
                surfaces,
                portals);
        }

        foreach (var controlPoint in level.GetRoomObjects(RoomObjectType.ControlPoint))
        {
            AppendObjectivePortal(
                controlPoint.CenterX,
                controlPoint.Bottom,
                $"control:{portals.Count}",
                VerifiedNavPortalKind.ControlPoint,
                surfaces,
                portals);
        }

        return portals;
    }

    private static void AppendSpawnPortals(
        SpawnPoint spawn,
        string label,
        VerifiedNavPortalKind kind,
        IReadOnlyList<VerifiedNavSurface> surfaces,
        List<VerifiedNavPortal> portals)
    {
        AppendObjectivePortal(spawn.X, spawn.Y, label, kind, surfaces, portals);
    }

    private static void AppendObjectivePortal(
        float x,
        float y,
        string label,
        VerifiedNavPortalKind kind,
        IReadOnlyList<VerifiedNavSurface> surfaces,
        List<VerifiedNavPortal> portals)
    {
        var snapped = FindNearestSurfaceBelow(surfaces, x, y);
        portals.Add(new VerifiedNavPortal(
            portals.Count,
            kind,
            snapped?.X ?? x,
            snapped?.Bottom ?? y,
            snapped?.SurfaceId,
            label));
    }

    private static (float X, float Bottom, int SurfaceId)? FindNearestSurfaceBelow(
        IReadOnlyList<VerifiedNavSurface> surfaces,
        float x,
        float y)
    {
        (float X, float Bottom, int SurfaceId)? best = null;
        var bestDy = float.PositiveInfinity;
        foreach (var surface in surfaces)
        {
            if (x < surface.Left || x > surface.Right)
            {
                continue;
            }

            var dy = surface.Top - y;
            if (dy < -PortalSnapVerticalLimit || dy > bestDy)
            {
                continue;
            }

            bestDy = dy;
            best = (float.Clamp(x, surface.Left, surface.Right), surface.Top, surface.Id);
        }

        return best;
    }

    private static List<VerifiedNavCandidateEdge> BuildCandidateEdges(
        IReadOnlyList<VerifiedNavSurface> surfaces,
        VerifiedNavBuildOptions options)
    {
        var edges = new List<VerifiedNavCandidateEdge>();
        AppendSameSurfaceWalkEdges(surfaces, options, edges);
        AppendTransferEdges(surfaces, options, edges);
        return edges;
    }

    private static void AppendSameSurfaceWalkEdges(
        IReadOnlyList<VerifiedNavSurface> surfaces,
        VerifiedNavBuildOptions options,
        List<VerifiedNavCandidateEdge> edges)
    {
        foreach (var surface in surfaces)
        {
            var segmentLength = MathF.Max(options.MinSurfaceWidth, options.SameSurfaceMaxEdgeLength);
            for (var x = surface.Left; x < surface.Right - 0.001f; x += segmentLength)
            {
                var nextX = MathF.Min(surface.Right, x + segmentLength);
                if (nextX - x < options.MinSurfaceWidth)
                {
                    continue;
                }

                AddEdge(
                    edges,
                    VerifiedNavEdgeIntent.Walk,
                    surface.Id,
                    surface.Id,
                    x,
                    surface.Top,
                    nextX,
                    surface.Top,
                    RecipeHint: "hold_right");
                AddEdge(
                    edges,
                    VerifiedNavEdgeIntent.Walk,
                    surface.Id,
                    surface.Id,
                    nextX,
                    surface.Top,
                    x,
                    surface.Top,
                    RecipeHint: "hold_left");
            }
        }
    }

    private static void AppendTransferEdges(
        IReadOnlyList<VerifiedNavSurface> surfaces,
        VerifiedNavBuildOptions options,
        List<VerifiedNavCandidateEdge> edges)
    {
        var emitted = new HashSet<TransferEdgeKey>();
        foreach (var from in surfaces)
        {
            foreach (var to in surfaces)
            {
                if (from.Id == to.Id)
                {
                    continue;
                }

                var horizontalGap = GetHorizontalGap(from, to);
                var verticalDelta = to.Top - from.Top;
                if (MathF.Abs(verticalDelta) <= 18f
                    && horizontalGap <= options.JumpHorizontalReach)
                {
                    AddTransferEdges(edges, emitted, VerifiedNavEdgeIntent.Walk, from, to);
                    AddTransferEdges(edges, emitted, VerifiedNavEdgeIntent.Jump, from, to);
                }

                if (verticalDelta > 0f
                    && verticalDelta <= options.DropVerticalLimit
                    && horizontalGap <= options.DropHorizontalReach)
                {
                    AddTransferEdges(edges, emitted, VerifiedNavEdgeIntent.Drop, from, to);
                    continue;
                }

                if (verticalDelta < 0f
                    && -verticalDelta <= options.JumpVerticalLimit
                    && horizontalGap <= options.JumpHorizontalReach)
                {
                    AddTransferEdges(edges, emitted, VerifiedNavEdgeIntent.Jump, from, to);
                }
            }
        }
    }

    private static float GetHorizontalGap(VerifiedNavSurface from, VerifiedNavSurface to)
    {
        if (from.Right < to.Left)
        {
            return to.Left - from.Right;
        }

        if (to.Right < from.Left)
        {
            return from.Left - to.Right;
        }

        return 0f;
    }

    private static void AddTransferEdges(
        List<VerifiedNavCandidateEdge> edges,
        HashSet<TransferEdgeKey> emitted,
        VerifiedNavEdgeIntent intent,
        VerifiedNavSurface from,
        VerifiedNavSurface to)
    {
        var overlapLeft = MathF.Max(from.Left, to.Left);
        var overlapRight = MathF.Min(from.Right, to.Right);
        if (overlapRight >= overlapLeft)
        {
            var overlapCenter = (overlapLeft + overlapRight) * 0.5f;
            AddTransferEdge(edges, emitted, intent, from, to, overlapCenter, overlapCenter);
            AddTransferEdge(edges, emitted, intent, from, to, overlapLeft, overlapLeft);
            AddTransferEdge(edges, emitted, intent, from, to, overlapRight, overlapRight);
        }

        AddTransferEdge(edges, emitted, intent, from, to, from.Left, float.Clamp(from.Left, to.Left, to.Right));
        AddTransferEdge(edges, emitted, intent, from, to, from.Right, float.Clamp(from.Right, to.Left, to.Right));
        AddTransferEdge(edges, emitted, intent, from, to, float.Clamp(to.Left, from.Left, from.Right), to.Left);
        AddTransferEdge(edges, emitted, intent, from, to, float.Clamp(to.Right, from.Left, from.Right), to.Right);
    }

    private static void AddTransferEdge(
        List<VerifiedNavCandidateEdge> edges,
        HashSet<TransferEdgeKey> emitted,
        VerifiedNavEdgeIntent intent,
        VerifiedNavSurface from,
        VerifiedNavSurface to,
        float fromX,
        float toX)
    {
        fromX = float.Clamp(fromX, from.Left, from.Right);
        toX = float.Clamp(toX, to.Left, to.Right);
        var key = new TransferEdgeKey(
            intent,
            from.Id,
            to.Id,
            (int)MathF.Round(fromX / 8f),
            (int)MathF.Round(toX / 8f));
        if (!emitted.Add(key))
        {
            return;
        }

        var dx = toX - fromX;
        var direction = MathF.Abs(dx) < 6f ? "neutral" : dx < 0f ? "left" : "right";
        var recipe = intent == VerifiedNavEdgeIntent.Drop
            ? $"hold_{direction}_drop_probe"
            : $"hold_{direction}_jump_probe";
        AddEdge(edges, intent, from.Id, to.Id, fromX, from.Top, toX, to.Top, recipe);
    }

    private static void AddEdge(
        List<VerifiedNavCandidateEdge> edges,
        VerifiedNavEdgeIntent intent,
        int fromSurfaceId,
        int toSurfaceId,
        float entryX,
        float entryBottom,
        float exitX,
        float exitBottom,
        string RecipeHint)
    {
        var dx = exitX - entryX;
        var dy = exitBottom - entryBottom;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        edges.Add(new VerifiedNavCandidateEdge(
            edges.Count,
            intent,
            fromSurfaceId,
            toSurfaceId,
            entryX,
            entryBottom,
            exitX,
            exitBottom,
            distance,
            VerifiedNavCertificationState.Candidate,
            RecipeHint));
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }

    private readonly record struct RawSurface(
        VerifiedNavSurfaceKind Kind,
        float Left,
        float Right,
        float Top,
        int SourceIndex);

    private readonly record struct TransferEdgeKey(
        VerifiedNavEdgeIntent Intent,
        int FromSurfaceId,
        int ToSurfaceId,
        int EntryXBucket,
        int ExitXBucket);
}
