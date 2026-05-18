namespace OpenGarrison.Core.BotBrain;

public static class VerifiedNavProofGraphBuilder
{
    private const int DefaultLinksPerSurfacePair = 3;

    public static VerifiedNavProofGraphAsset Build(
        VerifiedNavCandidateGraph candidateGraph,
        IEnumerable<(VerifiedNavProofRouteKind Kind, VerifiedNavProofTraceReport Trace)> traces,
        VerifiedNavProofGraphBuildOptions? options = null)
    {
        var edges = new List<VerifiedNavProofGraphEdge>();
        var laneSegments = new List<VerifiedNavProofLaneSegment>();
        var routes = new List<VerifiedNavProofGraphRoute>();
        foreach (var (kind, trace) in traces)
        {
            var routeEdgeIds = new List<int>();
            var routeLaneSegmentIds = new List<int>();
            var routeActionOnly = options?.RouteActionOnlyKinds.Contains(kind) == true;
            var atomicTraceEdgeIndexes = FindAtomicTraceEdgeIndexes(kind, trace.Edges);
            for (var traceEdgeIndex = 0; traceEdgeIndex < trace.Edges.Count; traceEdgeIndex += 1)
            {
                var traceEdge = trace.Edges[traceEdgeIndex];
                var edgeId = edges.Count;
                var laneSegmentId = laneSegments.Count;
                var actions = traceEdge.Actions ?? [];
                var emitLaneSegments = !routeActionOnly
                    && ShouldEmitLaneSegments(kind, traceEdge, atomicTraceEdgeIndexes.Contains(traceEdgeIndex));
                routeEdgeIds.Add(edgeId);
                edges.Add(new VerifiedNavProofGraphEdge(
                    edgeId,
                    kind,
                    traceEdge.FromSurfaceId,
                    traceEdge.ToSurfaceId,
                    traceEdge.EntryX,
                    traceEdge.EntryBottom,
                    traceEdge.ExitX,
                    traceEdge.ExitBottom,
                    Math.Max(1, traceEdge.ExitTick - traceEdge.EntryTick),
                    traceEdge.LeftTicks,
                    traceEdge.RightTicks,
                    traceEdge.JumpTicks,
                    traceEdge.DropTicks,
                    trace.Source,
                    actions));

                if (!emitLaneSegments)
                {
                    continue;
                }

                var traceLaneSegments = traceEdge.LaneSegments ?? [];
                if (traceLaneSegments.Count == 0)
                {
                    routeLaneSegmentIds.Add(laneSegmentId);
                    laneSegments.Add(new VerifiedNavProofLaneSegment(
                        laneSegmentId,
                        kind,
                        edgeId,
                        traceEdge.FromSurfaceId,
                        traceEdge.ToSurfaceId,
                        traceEdge.EntryTick,
                        traceEdge.ExitTick,
                        traceEdge.EntryX,
                        traceEdge.EntryBottom,
                        traceEdge.ExitX,
                        traceEdge.ExitBottom,
                        EndRequiresGrounded: true,
                        actions));
                    continue;
                }

                foreach (var traceLaneSegment in traceLaneSegments)
                {
                    var splitLaneSegmentId = laneSegments.Count;
                    routeLaneSegmentIds.Add(splitLaneSegmentId);
                    laneSegments.Add(new VerifiedNavProofLaneSegment(
                        splitLaneSegmentId,
                        kind,
                        edgeId,
                        traceLaneSegment.FromSurfaceId,
                        traceLaneSegment.ToSurfaceId,
                        traceLaneSegment.StartTick,
                        traceLaneSegment.EndTick,
                        traceLaneSegment.StartX,
                        traceLaneSegment.StartBottom,
                        traceLaneSegment.EndX,
                        traceLaneSegment.EndBottom,
                        traceLaneSegment.EndRequiresGrounded,
                        traceLaneSegment.Actions ?? []));
                }
            }

            var traceTerminalActions = trace.TerminalActions ?? [];
            var terminalActions = traceTerminalActions.Count > 0
                ? traceTerminalActions
                : BuildTerminalActionFallback(trace);
            var terminalStartX = traceTerminalActions.Count > 0 || trace.Edges.Count == 0
                ? trace.TerminalStartX
                : trace.Edges[^1].ExitX;
            var terminalStartBottom = traceTerminalActions.Count > 0 || trace.Edges.Count == 0
                ? trace.TerminalStartBottom
                : trace.Edges[^1].ExitBottom;
            routes.Add(new VerifiedNavProofGraphRoute(
                kind,
                trace.Source,
                trace.SampleCount,
                trace.SurfaceSequence,
                routeEdgeIds,
                routeLaneSegmentIds,
                trace.Actions,
                terminalStartX,
                terminalStartBottom,
                trace.TerminalEndX,
                trace.TerminalEndBottom,
                terminalActions,
                trace.StartX,
                trace.StartBottom));
        }

        return new VerifiedNavProofGraphAsset
        {
            LevelName = candidateGraph.LevelName,
            MapAreaIndex = candidateGraph.MapAreaIndex,
            Team = candidateGraph.Team,
            ClassId = candidateGraph.ClassId,
            Surfaces = candidateGraph.Surfaces,
            Edges = edges,
            LaneSegments = laneSegments,
            Routes = routes,
        };
    }

    public static void AddExplorationLinks(
        VerifiedNavProofGraphAsset asset,
        IEnumerable<VerifiedNavExplorationReport> explorationReports,
        int linksPerSurfacePair = DefaultLinksPerSurfacePair)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(explorationReports);

        var candidates = new List<VerifiedNavProofGraphLink>();
        foreach (var report in explorationReports)
        {
            if (!string.Equals(asset.LevelName, report.LevelName, StringComparison.OrdinalIgnoreCase)
                || asset.MapAreaIndex != report.MapAreaIndex
                || asset.Team != report.Team
                || asset.ClassId != report.ClassId)
            {
                continue;
            }

            foreach (var edge in report.Edges)
            {
                if (edge.FromSurfaceId == edge.ToSurfaceId
                    || edge.FromSurfaceId < 0
                    || edge.ToSurfaceId < 0)
                {
                    continue;
                }

                candidates.Add(new VerifiedNavProofGraphLink(
                    -1,
                    edge.FromSurfaceId,
                    edge.ToSurfaceId,
                    edge.EntryX,
                    edge.EntryBottom,
                    edge.ExitX,
                    edge.ExitBottom,
                    Math.Max(1, edge.Ticks),
                    $"Exploration:{report.StartSurfaceId}:{edge.Macro}",
                    BuildActionRunsFromExplorationMacro(edge.Macro)));
            }
        }

        var existingKeys = asset.Links
            .Select(link => (link.FromSurfaceId, link.ToSurfaceId, EntryBucket: QuantizeLinkX(link.EntryX), ExitBucket: QuantizeLinkX(link.ExitX)))
            .ToHashSet();
        var nextId = asset.Links.Count;
        var compacted = candidates
            .GroupBy(link => (link.FromSurfaceId, link.ToSurfaceId))
            .SelectMany(group => group
                .OrderBy(link => link.CostTicks)
                .ThenBy(link => MathF.Abs(link.ExitX - link.EntryX))
                .GroupBy(link => (EntryBucket: QuantizeLinkX(link.EntryX), ExitBucket: QuantizeLinkX(link.ExitX)))
                .Select(static bucket => bucket.First())
                .Take(Math.Max(1, linksPerSurfacePair)))
            .OrderBy(link => link.FromSurfaceId)
            .ThenBy(link => link.ToSurfaceId)
            .ThenBy(link => link.CostTicks);

        foreach (var link in compacted)
        {
            var key = (link.FromSurfaceId, link.ToSurfaceId, EntryBucket: QuantizeLinkX(link.EntryX), ExitBucket: QuantizeLinkX(link.ExitX));
            if (!existingKeys.Add(key))
            {
                continue;
            }

            asset.Links.Add(link with { Id = nextId++ });
        }
    }

    private static int QuantizeLinkX(float x) => (int)MathF.Round(x / 32f);

    private static IReadOnlyList<VerifiedNavProofGraphActionRun> BuildActionRunsFromExplorationMacro(string macro)
    {
        var actions = new List<VerifiedNavProofGraphActionRun>();
        foreach (var part in macro.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseExplorationMacro(part, out var ticks, out var moveDirection, out var jumpTicks, out var drop))
            {
                continue;
            }

            AppendActionRun(actions, ticks, moveDirection, jumpTicks, drop);
        }

        return actions;
    }

    private static bool TryParseExplorationMacro(
        string macro,
        out int ticks,
        out float moveDirection,
        out int jumpTicks,
        out bool drop)
    {
        ticks = 0;
        moveDirection = 0f;
        jumpTicks = 0;
        drop = false;
        var parts = macro.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        var verb = parts[0];
        drop = verb.StartsWith("drop", StringComparison.OrdinalIgnoreCase);
        var directionIndex = verb is "idle" ? -1 : 1;
        var durationIndex = verb is "idle" ? 1 : 2;
        if (directionIndex >= 0 && directionIndex < parts.Length)
        {
            moveDirection = string.Equals(parts[directionIndex], "neutral", StringComparison.OrdinalIgnoreCase)
                ? 0f
                : float.TryParse(parts[directionIndex], out var parsedDirection)
                    ? MathF.Sign(parsedDirection)
                    : 0f;
        }

        if (durationIndex >= parts.Length || !int.TryParse(parts[durationIndex], out ticks))
        {
            return false;
        }

        if (verb.Contains("jump", StringComparison.OrdinalIgnoreCase)
            && durationIndex + 1 < parts.Length
            && int.TryParse(parts[durationIndex + 1], out var parsedJumpTicks))
        {
            jumpTicks = Math.Clamp(parsedJumpTicks, 0, ticks);
        }

        return ticks > 0;
    }

    private static void AppendActionRun(
        List<VerifiedNavProofGraphActionRun> actions,
        int ticks,
        float moveDirection,
        int jumpTicks,
        bool drop)
    {
        if (jumpTicks > 0)
        {
            AppendMergedActionRun(actions, jumpTicks, moveDirection, Jump: true, drop: drop);
        }

        var remainingTicks = ticks - jumpTicks;
        if (remainingTicks > 0)
        {
            AppendMergedActionRun(actions, remainingTicks, moveDirection, Jump: false, drop: drop);
        }
    }

    private static void AppendMergedActionRun(
        List<VerifiedNavProofGraphActionRun> actions,
        int ticks,
        float moveDirection,
        bool Jump,
        bool drop)
    {
        if (ticks <= 0)
        {
            return;
        }

        if (actions.Count > 0
            && actions[^1].MoveDirection == moveDirection
            && actions[^1].Jump == Jump
            && actions[^1].DropDown == drop)
        {
            var previous = actions[^1];
            actions[^1] = previous with { Ticks = previous.Ticks + ticks };
            return;
        }

        actions.Add(new VerifiedNavProofGraphActionRun(ticks, moveDirection, Jump, drop));
    }

    private static HashSet<int> FindAtomicTraceEdgeIndexes(
        VerifiedNavProofRouteKind kind,
        IReadOnlyList<VerifiedNavProofTraceEdge> edges)
    {
        var atomic = new HashSet<int>();
        if (kind != VerifiedNavProofRouteKind.Return)
        {
            return atomic;
        }

        for (var index = 0; index < edges.Count; index += 1)
        {
            var edge = edges[index];
            var laneSegments = edge.LaneSegments ?? [];
            if (!ShouldKeepAsAtomicProofEdge(edge, laneSegments.Count))
            {
                continue;
            }

            atomic.Add(index);
            if (index > 0)
            {
                atomic.Add(index - 1);
            }
        }

        return atomic;
    }

    private static bool ShouldEmitLaneSegments(VerifiedNavProofRouteKind kind, VerifiedNavProofTraceEdge edge, bool forceAtomic)
    {
        var laneSegments = edge.LaneSegments ?? [];
        if (forceAtomic)
        {
            return false;
        }

        if (laneSegments.Count < 3)
        {
            return true;
        }

        if (kind == VerifiedNavProofRouteKind.Return)
        {
            return true;
        }

        if (IsComplexProofEdge(edge))
        {
            return true;
        }

        var syntheticSegments = 0;
        var groundedSyntheticTargets = 0;
        foreach (var laneSegment in laneSegments)
        {
            if (laneSegment.FromSurfaceId < 0 || laneSegment.ToSurfaceId < 0)
            {
                syntheticSegments += 1;
            }

            if (laneSegment.ToSurfaceId < 0 && laneSegment.EndRequiresGrounded)
            {
                groundedSyntheticTargets += 1;
            }
        }

        return syntheticSegments < 3 || groundedSyntheticTargets < 2;
    }

    private static bool IsComplexProofEdge(VerifiedNavProofTraceEdge edge)
    {
        if (edge.JumpTicks > 1 && edge.DropTicks > 0)
        {
            return true;
        }

        if (edge.JumpTicks > 2 && edge.LeftTicks > 0 && edge.RightTicks > 0)
        {
            return true;
        }

        return CountDirectionChanges(edge.Actions ?? []) >= 2 && edge.JumpTicks > 0;
    }

    private static bool ShouldKeepAsAtomicProofEdge(VerifiedNavProofTraceEdge edge, int laneSegmentCount)
    {
        var actions = edge.Actions ?? [];
        var verticalDrop = edge.EntryBottom - edge.ExitBottom;
        if (verticalDrop >= 180f && laneSegmentCount >= 4)
        {
            return false;
        }

        if (edge.JumpTicks > 1 && edge.LeftTicks > 0 && edge.RightTicks > 0)
        {
            return true;
        }

        if (laneSegmentCount >= 6 && actions.Count >= 6)
        {
            return true;
        }

        return CountDirectionChanges(actions) >= 2 && edge.JumpTicks > 0;
    }

    private static int CountDirectionChanges(IReadOnlyList<VerifiedNavProofGraphActionRun> actions)
    {
        var previousDirection = 0f;
        var directionChanges = 0;
        foreach (var action in actions)
        {
            if (action.MoveDirection == 0f)
            {
                continue;
            }

            if (previousDirection != 0f && MathF.Sign(action.MoveDirection) != MathF.Sign(previousDirection))
            {
                directionChanges += 1;
            }

            previousDirection = action.MoveDirection;
        }

        return directionChanges;
    }

    private static IReadOnlyList<VerifiedNavProofGraphActionRun> BuildTerminalActionFallback(
        VerifiedNavProofTraceReport trace)
    {
        var traceActions = trace.Actions ?? [];
        if (trace.Edges.Count == 0 || traceActions.Count == 0)
        {
            return [];
        }

        var startTick = Math.Clamp(trace.Edges[^1].ExitTick, 0, trace.SampleCount);
        var endTick = Math.Max(startTick, trace.SampleCount);
        var runs = new List<VerifiedNavProofGraphActionRun>();
        var cursor = 0;
        foreach (var run in traceActions)
        {
            var runStart = cursor;
            var runEnd = cursor + Math.Max(0, run.Ticks);
            cursor = runEnd;
            var overlapStart = Math.Max(startTick, runStart);
            var overlapEnd = Math.Min(endTick, runEnd);
            if (overlapEnd <= overlapStart)
            {
                continue;
            }

            var ticks = overlapEnd - overlapStart;
            if (runs.Count > 0
                && runs[^1].MoveDirection == run.MoveDirection
                && runs[^1].Jump == run.Jump
                && runs[^1].DropDown == run.DropDown)
            {
                var previous = runs[^1];
                runs[^1] = previous with { Ticks = previous.Ticks + ticks };
                continue;
            }

            runs.Add(run with { Ticks = ticks });
        }

        return runs;
    }
}

public sealed class VerifiedNavProofGraphBuildOptions
{
    public HashSet<VerifiedNavProofRouteKind> RouteActionOnlyKinds { get; init; } = [];
}
