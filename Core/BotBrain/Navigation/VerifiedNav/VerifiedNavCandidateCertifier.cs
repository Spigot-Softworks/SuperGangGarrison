using System;

namespace OpenGarrison.Core.BotBrain;

public static class VerifiedNavCandidateCertifier
{
    public static VerifiedNavCertificationReport Certify(
        SimpleLevel level,
        VerifiedNavCandidateGraph graph,
        VerifiedNavCertificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);

        var limit = options.MaxEdges <= 0
            ? graph.CandidateEdges.Count
            : Math.Min(options.MaxEdges, graph.CandidateEdges.Count);
        var results = new List<VerifiedNavEdgeCertificationResult>(limit);
        foreach (var edge in graph.CandidateEdges.Take(limit))
        {
            results.Add(CertifyEdge(level, graph, edge, options));
        }

        return new VerifiedNavCertificationReport
        {
            LevelName = graph.LevelName,
            MapAreaIndex = graph.MapAreaIndex,
            Team = graph.Team,
            ClassId = graph.ClassId,
            CandidateEdgeCount = graph.CandidateEdges.Count,
            TestedEdgeCount = results.Count,
            Results = results,
        };
    }

    private static VerifiedNavEdgeCertificationResult CertifyEdge(
        SimpleLevel level,
        VerifiedNavCandidateGraph graph,
        VerifiedNavCandidateEdge edge,
        VerifiedNavCertificationOptions options)
    {
        var programs = BuildCandidatePrograms(edge, graph.ClassId, options.MaxTicks).ToList();
        TraversalLabBatchResult? bestFailed = null;
        var testedPrograms = 0;
        foreach (var program in programs)
        {
            testedPrograms += 1;
            var scenario = BuildScenario(level, graph, edge, options, program);
            var result = TraversalLabRunner.Run(scenario);
            if (result.Passed)
            {
                var executedTicks = result.Cases.Count == 0
                    ? 0
                    : result.Cases.Max(static candidate => candidate.ExecutedTicks);
                return new VerifiedNavEdgeCertificationResult(
                    edge.Id,
                    edge.Intent,
                    edge.FromSurfaceId,
                    edge.ToSurfaceId,
                    Passed: true,
                    testedPrograms,
                    result.PassedCount,
                    result.FailedCount,
                    executedTicks,
                    program.Recipe,
                    string.Empty);
            }

            if (bestFailed is null || result.PassedCount > bestFailed.PassedCount)
            {
                bestFailed = result;
            }
        }

        var failureReason = bestFailed?.Cases
            .GroupBy(static candidate => candidate.FailureReason)
            .OrderByDescending(static group => group.Count())
            .Select(static group => group.Key)
            .FirstOrDefault();
        return new VerifiedNavEdgeCertificationResult(
            edge.Id,
            edge.Intent,
            edge.FromSurfaceId,
            edge.ToSurfaceId,
            Passed: false,
            testedPrograms,
            bestFailed?.PassedCount ?? 0,
            bestFailed?.FailedCount ?? 0,
            bestFailed?.Cases.Count > 0 ? bestFailed.Cases.Max(static candidate => candidate.ExecutedTicks) : 0,
            bestFailed?.ScenarioName ?? string.Empty,
            string.IsNullOrWhiteSpace(failureReason) ? "no_program_passed" : failureReason);
    }

    private static TraversalLabScenario BuildScenario(
        SimpleLevel level,
        VerifiedNavCandidateGraph graph,
        VerifiedNavCandidateEdge edge,
        VerifiedNavCertificationOptions options,
        CandidateProgram program)
    {
        return new TraversalLabScenario
        {
            Name = program.Recipe,
            OverrideLevel = level,
            Team = graph.Team,
            ClassId = graph.ClassId,
            Start = new TraversalLabStartState
            {
                X = edge.EntryX,
                Bottom = edge.EntryBottom,
                HorizontalSpeed = 0f,
                VerticalSpeed = 0f,
                IsGrounded = true,
                FacingDirectionX = edge.ExitX < edge.EntryX ? -1f : 1f,
            },
            Steps = program.Steps,
            MaxTicks = Math.Max(1, program.Steps.Sum(static step => Math.Max(0, step.DurationTicks))),
            TraceEveryTicks = Math.Max(1, options.TraceEveryTicks),
            StartXOffsets = options.StartXOffsets,
            StartBottomOffsets = options.StartBottomOffsets,
            StartHorizontalSpeedOffsets = options.StartHorizontalSpeedOffsets,
            StartVerticalSpeedOffsets = options.StartVerticalSpeedOffsets,
            GroundedStates = [true],
            Expectation = BuildExpectation(graph, edge),
        };
    }

    private static TraversalLabExpectation BuildExpectation(
        VerifiedNavCandidateGraph graph,
        VerifiedNavCandidateEdge edge)
    {
        var horizontalTravel = MathF.Abs(edge.ExitX - edge.EntryX);
        var targetSurface = graph.Surfaces[edge.ToSurfaceId];
        return edge.Intent switch
        {
            VerifiedNavEdgeIntent.Walk => new TraversalLabExpectation
            {
                FinalBottom = edge.ExitBottom,
                RadiusBottom = 8f,
                MinX = edge.FromSurfaceId == edge.ToSurfaceId
                    ? edge.ExitX >= edge.EntryX ? edge.ExitX - 8f : null
                    : targetSurface.Left - 12f,
                MaxX = edge.FromSurfaceId == edge.ToSurfaceId
                    ? edge.ExitX < edge.EntryX ? edge.ExitX + 8f : null
                    : targetSurface.Right + 12f,
                MinHorizontalTravel = MathF.Max(0f, horizontalTravel - 48f),
                MustBeGrounded = true,
            },
            VerifiedNavEdgeIntent.Drop => new TraversalLabExpectation
            {
                FinalBottom = edge.ExitBottom,
                RadiusBottom = 16f,
                MinX = targetSurface.Left - 12f,
                MaxX = targetSurface.Right + 12f,
                MinHorizontalTravel = MathF.Max(0f, horizontalTravel - 64f),
                MustLeaveGround = true,
                MustReground = true,
                MustBeGrounded = true,
            },
            VerifiedNavEdgeIntent.Jump => new TraversalLabExpectation
            {
                FinalBottom = edge.ExitBottom,
                RadiusBottom = 18f,
                MinX = targetSurface.Left - 12f,
                MaxX = targetSurface.Right + 12f,
                MinHorizontalTravel = MathF.Max(0f, horizontalTravel - 64f),
                MustLeaveGround = true,
                MustReground = true,
                MustBeGrounded = true,
            },
            _ => new TraversalLabExpectation(),
        };
    }

    private static IEnumerable<CandidateProgram> BuildCandidatePrograms(
        VerifiedNavCandidateEdge edge,
        PlayerClass classId,
        int maxTicks)
    {
        var dx = edge.ExitX - edge.EntryX;
        var horizontalDirection = MathF.Abs(dx) < 6f ? 0 : dx < 0f ? -1 : 1;
        var distance = MathF.Abs(edge.ExitX - edge.EntryX);
        var definition = CharacterClassCatalog.GetDefinition(classId);
        var speed = MathF.Max(1f, definition.MaxRunSpeed);
        var estimatedTicks = Math.Clamp(
            (int)MathF.Ceiling(distance / speed * SimulationConfig.DefaultTicksPerSecond) + 18,
            12,
            Math.Max(12, maxTicks));

        switch (edge.Intent)
        {
            case VerifiedNavEdgeIntent.Walk:
                foreach (var duration in BuildDurationSweep(estimatedTicks, maxTicks, [4, 6, 8, 10, 12, 16, 20, 24, 30, 38, 48, 60, 76]))
                {
                    yield return new CandidateProgram(
                        $"walk_{FormatDirection(horizontalDirection)}_{duration}",
                        [MoveStep("walk", duration, horizontalDirection, up: false, down: false)]);
                }
                break;
            case VerifiedNavEdgeIntent.Drop:
                foreach (var duration in BuildDurationSweep(Math.Max(36, estimatedTicks), maxTicks, [12, 18, 24, 32, 42, 56, 72, 96, 126, 160]))
                {
                    yield return new CandidateProgram(
                        $"drop_{FormatDirection(horizontalDirection)}_{duration}",
                        [MoveStep("drop", duration, horizontalDirection, up: false, down: edge.ExitBottom > edge.EntryBottom + 8f)]);
                }
                break;
            case VerifiedNavEdgeIntent.Jump:
                foreach (var duration in BuildDurationSweep(Math.Max(42, estimatedTicks), maxTicks, [18, 24, 32, 42, 56, 72, 96, 126, 160, 196]))
                {
                    yield return new CandidateProgram(
                        $"jump_{FormatDirection(horizontalDirection)}_{duration}",
                        [
                            MoveStep("jump", 2, horizontalDirection, up: true, down: false),
                            MoveStep("air", Math.Max(1, duration - 2), horizontalDirection, up: false, down: false),
                        ]);
                }
                break;
        }
    }

    private static IEnumerable<int> BuildDurationSweep(int estimatedTicks, int maxTicks, IReadOnlyList<int> absoluteDurations)
    {
        var seen = new HashSet<int>();
        foreach (var durationCandidate in absoluteDurations.Concat([estimatedTicks, estimatedTicks + 12, estimatedTicks + 28, estimatedTicks + 48]))
        {
            var duration = Math.Clamp(durationCandidate, 1, Math.Max(1, maxTicks));
            if (seen.Add(duration))
            {
                yield return duration;
            }
        }
    }

    private static TraversalLabInputStep MoveStep(
        string label,
        int duration,
        int horizontalDirection,
        bool up,
        bool down)
    {
        return new TraversalLabInputStep
        {
            Label = label,
            DurationTicks = Math.Max(1, duration),
            Left = horizontalDirection < 0,
            Right = horizontalDirection > 0,
            Up = up,
            Down = down,
        };
    }

    private static string FormatDirection(int horizontalDirection)
    {
        return horizontalDirection switch
        {
            < 0 => "left",
            > 0 => "right",
            _ => "neutral",
        };
    }

    private sealed record CandidateProgram(string Recipe, List<TraversalLabInputStep> Steps);
}
