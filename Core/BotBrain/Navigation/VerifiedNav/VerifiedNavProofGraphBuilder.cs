namespace OpenGarrison.Core.BotBrain;

public static class VerifiedNavProofGraphBuilder
{
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
