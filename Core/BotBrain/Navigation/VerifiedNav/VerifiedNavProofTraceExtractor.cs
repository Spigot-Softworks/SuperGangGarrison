namespace OpenGarrison.Core.BotBrain;

public static class VerifiedNavProofTraceExtractor
{
    private const float LandingBottomTolerance = 8f;
    private const int MaxLaneSegmentTicks = 42;
    private const int MinLaneSegmentTicks = 8;

    public static VerifiedNavProofTraceReport Extract(
        VerifiedNavCandidateGraph graph,
        IReadOnlyList<TraversalLabTickSample> samples,
        string source,
        float? startX = null,
        float? startBottom = null)
    {
        var surfaceTouches = new List<SurfaceTouch>();
        foreach (var sample in samples)
        {
            if (!sample.IsGrounded)
            {
                continue;
            }

            if (!TryFindSurface(graph, sample.X, sample.Bottom, out var surfaceId))
            {
                continue;
            }

            if (surfaceTouches.Count > 0 && surfaceTouches[^1].SurfaceId == surfaceId)
            {
                surfaceTouches[^1] = new SurfaceTouch(surfaceId, surfaceTouches[^1].EntrySample, sample);
                continue;
            }

            surfaceTouches.Add(new SurfaceTouch(surfaceId, sample, sample));
        }

        var edges = new List<VerifiedNavProofTraceEdge>();
        for (var index = 1; index < surfaceTouches.Count; index += 1)
        {
            var from = surfaceTouches[index - 1];
            var to = surfaceTouches[index];
            if (from.SurfaceId == to.SurfaceId)
            {
                continue;
            }

            var entryTick = from.EntrySample.Tick;
            var exitTick = to.EntrySample.Tick;
            var actionWindow = samples
                .Where(sample => sample.Tick >= entryTick && sample.Tick <= exitTick)
                .ToList();
            edges.Add(new VerifiedNavProofTraceEdge(
                from.SurfaceId,
                to.SurfaceId,
                entryTick,
                exitTick,
                from.EntrySample.X,
                from.EntrySample.Bottom,
                to.EntrySample.X,
                to.EntrySample.Bottom,
                actionWindow.Count(static sample => sample.InputLeft),
                actionWindow.Count(static sample => sample.InputRight),
                actionWindow.Count(static sample => sample.InputUp),
                actionWindow.Count(static sample => sample.InputDown),
                BuildActionRuns(actionWindow),
                BuildLaneSegments(graph, from.SurfaceId, to.SurfaceId, actionWindow)));
        }

        var lastSample = samples.Count > 0 ? samples[^1] : default;
        var terminalStartSample = surfaceTouches.Count > 0
            ? surfaceTouches[^1].EntrySample
            : samples.Count > 0 ? samples[0] : default;
        var terminalActions = samples.Count > 0 && surfaceTouches.Count > 0
            ? BuildActionRuns(samples
                .Where(sample => sample.Tick > terminalStartSample.Tick)
                .ToList()).ToList()
            : [];

        return new VerifiedNavProofTraceReport
        {
            LevelName = graph.LevelName,
            MapAreaIndex = graph.MapAreaIndex,
            Team = graph.Team,
            ClassId = graph.ClassId,
            Source = source,
            SampleCount = samples.Count,
            SurfaceTouchCount = surfaceTouches.Count,
            StartX = startX ?? (samples.Count > 0 ? samples[0].X : 0f),
            StartBottom = startBottom ?? (samples.Count > 0 ? samples[0].Bottom : 0f),
            SurfaceSequence = surfaceTouches.Select(static touch => touch.SurfaceId).ToList(),
            Edges = edges,
            Actions = BuildActionRuns(samples).ToList(),
            TerminalStartX = terminalStartSample.X,
            TerminalStartBottom = terminalStartSample.Bottom,
            TerminalEndX = lastSample.X,
            TerminalEndBottom = lastSample.Bottom,
            TerminalActions = terminalActions,
        };
    }

    private static bool TryFindSurface(
        VerifiedNavCandidateGraph graph,
        float x,
        float bottom,
        out int surfaceId)
    {
        foreach (var surface in graph.Surfaces)
        {
            if (x >= surface.Left - 2f
                && x <= surface.Right + 2f
                && MathF.Abs(bottom - surface.Top) <= LandingBottomTolerance)
            {
                surfaceId = surface.Id;
                return true;
            }
        }

        surfaceId = -1;
        return false;
    }

    private static IReadOnlyList<VerifiedNavProofTraceLaneSegment> BuildLaneSegments(
        VerifiedNavCandidateGraph graph,
        int fromSurfaceId,
        int toSurfaceId,
        IReadOnlyList<TraversalLabTickSample> samples)
    {
        if (samples.Count < 2)
        {
            return [];
        }

        var segments = new List<VerifiedNavProofTraceLaneSegment>();
        var startIndex = 0;
        var activeFromSurfaceId = fromSurfaceId;
        for (var index = 1; index < samples.Count; index += 1)
        {
            var start = samples[startIndex];
            var previous = samples[index - 1];
            var current = samples[index];
            var ticks = current.Tick - start.Tick;
            var landed = current.IsGrounded && !previous.IsGrounded;
            var maxed = ticks >= MaxLaneSegmentTicks;
            if (!maxed && (!landed || ticks < MinLaneSegmentTicks))
            {
                continue;
            }

            var isFinal = index == samples.Count - 1;
            var endSurfaceId = ResolveSegmentEndSurfaceId(graph, current, isFinal, toSurfaceId);
            segments.Add(BuildLaneSegment(samples, startIndex, index, activeFromSurfaceId, endSurfaceId));
            startIndex = index;
            activeFromSurfaceId = endSurfaceId;
        }

        if (startIndex < samples.Count - 1)
        {
            segments.Add(BuildLaneSegment(samples, startIndex, samples.Count - 1, activeFromSurfaceId, toSurfaceId));
        }
        else if (segments.Count > 0 && segments[^1].ToSurfaceId != toSurfaceId)
        {
            var previous = segments[^1];
            segments[^1] = previous with { ToSurfaceId = toSurfaceId };
        }

        return segments.Count <= 1
            ? []
            : segments;
    }

    private static VerifiedNavProofTraceLaneSegment BuildLaneSegment(
        IReadOnlyList<TraversalLabTickSample> samples,
        int startIndex,
        int endIndex,
        int fromSurfaceId,
        int toSurfaceId)
    {
        var start = samples[startIndex];
        var end = samples[endIndex];
        var actionWindow = new List<TraversalLabTickSample>(endIndex - startIndex + 1);
        for (var index = startIndex; index <= endIndex; index += 1)
        {
            actionWindow.Add(samples[index]);
        }

        return new VerifiedNavProofTraceLaneSegment(
            fromSurfaceId,
            toSurfaceId,
            start.Tick,
            end.Tick,
            start.X,
            start.Bottom,
            end.X,
            end.Bottom,
            end.IsGrounded,
            BuildActionRuns(actionWindow));
    }

    private static int ResolveSegmentEndSurfaceId(
        VerifiedNavCandidateGraph graph,
        TraversalLabTickSample sample,
        bool isFinal,
        int finalSurfaceId)
    {
        if (isFinal)
        {
            return finalSurfaceId;
        }

        return sample.IsGrounded && TryFindSurface(graph, sample.X, sample.Bottom, out var surfaceId)
            ? surfaceId
            : -1;
    }

    private static IReadOnlyList<VerifiedNavProofGraphActionRun> BuildActionRuns(IReadOnlyList<TraversalLabTickSample> samples)
    {
        var runs = new List<VerifiedNavProofGraphActionRun>();
        foreach (var sample in samples)
        {
            var direction = sample.InputRight == sample.InputLeft
                ? 0f
                : sample.InputRight
                    ? 1f
                    : -1f;
            if (runs.Count > 0
                && runs[^1].MoveDirection == direction
                && runs[^1].Jump == sample.InputUp
                && runs[^1].DropDown == sample.InputDown)
            {
                var previous = runs[^1];
                runs[^1] = previous with { Ticks = previous.Ticks + 1 };
                continue;
            }

            runs.Add(new VerifiedNavProofGraphActionRun(
                Ticks: 1,
                MoveDirection: direction,
                Jump: sample.InputUp,
                DropDown: sample.InputDown));
        }

        return runs;
    }

    private readonly record struct SurfaceTouch(
        int SurfaceId,
        TraversalLabTickSample EntrySample,
        TraversalLabTickSample ExitSample);
}
