using System.Diagnostics;
using System.Globalization;

namespace OpenGarrison.Core.BotBrain;

public sealed class StochasticLocalMotionPlanner
{
    private const int MinimumCommitTicks = 8;
    private const int ReplanIntervalTicks = 10;
    private const int MaxCandidatesPerDecision = 10;
    private const int MinimumCandidatesBeforeStopwatchAbort = 7;
    private const int MaxSimTicksPerDecision = 320;
    private const double MaxDecisionMilliseconds = 0.25d;
    private const float MinimumAcceptedProgress = 18f;
    private const float FatalFallMargin = 128f;
    private const float DeadEndProbeDistance = 84f;
    private const float DeadEndProbeStep = 6f;
    private const float TopologyScanRadius = 336f;
    private const float TopologyScanStepX = 16f;
    private const float TopologyScanStepBottom = 8f;
    private const float FailedPocketRadius = 96f;
    private const float EscapePocketBreakthrough = 96f;
    private const int EscapePocketTicks = 720;
    private const int ProbeEntityId = -901_100;

    private LocalMacroPlan _activePlan;
    private int _activeUntilTick;
    private int _lastDecisionTick = int.MinValue / 2;
    private int _lastImprovementTick = int.MinValue / 2;
    private float _bestMetricSeen = float.PositiveInfinity;
    private float _bestSeenX;
    private float _bestSeenBottom;
    private float _failedPocketX;
    private float _failedPocketBottom;
    private float _failedPocketMetric = float.PositiveInfinity;
    private int _failedPocketUntilTick;
    private float _escapeDirection;
    private int _escapeUntilTick;
    private float _lastDecisionX;
    private float _lastDecisionBottom;
    private string _lastCandidateTrace = string.Empty;
    private string _lastNonEmptyCandidateTrace = string.Empty;

    public string LastCandidateTrace => string.IsNullOrWhiteSpace(_lastCandidateTrace)
        ? _lastNonEmptyCandidateTrace
        : _lastCandidateTrace;

    public void Reset()
    {
        _activePlan = default;
        _activeUntilTick = 0;
        _lastDecisionTick = int.MinValue / 2;
        _lastImprovementTick = int.MinValue / 2;
        _bestMetricSeen = float.PositiveInfinity;
        _bestSeenX = 0f;
        _bestSeenBottom = 0f;
        _failedPocketX = 0f;
        _failedPocketBottom = 0f;
        _failedPocketMetric = float.PositiveInfinity;
        _failedPocketUntilTick = 0;
        _escapeDirection = 0f;
        _escapeUntilTick = 0;
        _lastDecisionX = 0f;
        _lastDecisionBottom = 0f;
        _lastCandidateTrace = string.Empty;
        _lastNonEmptyCandidateTrace = string.Empty;
    }

    public bool TryResolve(
        SimulationWorld world,
        PlayerEntity self,
        StochasticLocalMotionGoal goal,
        int tick,
        out PlayerInputSnapshot input,
        out StochasticLocalMotionTrace trace)
    {
        input = default;
        trace = StochasticLocalMotionTrace.Empty;
        _lastCandidateTrace = string.Empty;
        if (!self.IsAlive)
        {
            return false;
        }

        if (_activePlan.HasPlan && tick <= _activeUntilTick && !HasReachedGoal(self, goal))
        {
            input = BuildInput(self, goal, _activePlan, tick - _activePlan.StartTick);
            trace = new StochasticLocalMotionTrace(
                Resolved: true,
                Source: "active",
                MacroLabel: _activePlan.Label,
                StartMetric: MeasureMetric(self.X, self.Bottom, goal),
                BestMetric: MeasureMetric(self.X, self.Bottom, goal),
                FinalMetric: MeasureMetric(self.X, self.Bottom, goal),
                Progress: 0f,
                Score: 0f,
                Candidates: 0,
                SimTicks: 0,
                ElapsedMilliseconds: 0d,
                Reached: false,
                RejectedReason: string.Empty);
            return true;
        }

        _activePlan = default;
        if (tick - _lastDecisionTick < ReplanIntervalTicks)
        {
            trace = StochasticLocalMotionTrace.Empty with { RejectedReason = "replan_deferred" };
            return false;
        }

        _lastDecisionTick = tick;
        var startMetric = MeasureMetric(self.X, self.Bottom, goal);
        if (startMetric < _bestMetricSeen - 16f)
        {
            _bestMetricSeen = startMetric;
            _bestSeenX = self.X;
            _bestSeenBottom = self.Bottom;
            _lastImprovementTick = tick;
        }

        var decisionMovement = MathF.Abs(self.X - _lastDecisionX) + MathF.Abs(self.Bottom - _lastDecisionBottom);
        var stagnant = tick - _lastImprovementTick >= 90 || decisionMovement <= 3f;
        if (stagnant && float.IsFinite(_bestMetricSeen))
        {
            _failedPocketX = _bestSeenX;
            _failedPocketBottom = _bestSeenBottom;
            _failedPocketMetric = _bestMetricSeen;
            _failedPocketUntilTick = Math.Max(_failedPocketUntilTick, tick + EscapePocketTicks);
            _escapeDirection = ResolveEscapeDirection(self, goal, _failedPocketX);
            _escapeUntilTick = Math.Max(_escapeUntilTick, tick + EscapePocketTicks);
        }

        if (float.IsFinite(_failedPocketMetric) && startMetric < _failedPocketMetric - EscapePocketBreakthrough)
        {
            _failedPocketUntilTick = 0;
            _escapeUntilTick = 0;
            _escapeDirection = 0f;
        }

        var avoidBestTrap = tick <= _failedPocketUntilTick && float.IsFinite(_failedPocketMetric);
        var escapingPocket = tick <= _escapeUntilTick && _escapeDirection != 0f && avoidBestTrap;
        var trapX = avoidBestTrap ? _failedPocketX : _bestSeenX;
        var trapBottom = avoidBestTrap ? _failedPocketBottom : _bestSeenBottom;
        var trapMetric = avoidBestTrap ? _failedPocketMetric : _bestMetricSeen;
        _lastDecisionX = self.X;
        _lastDecisionBottom = self.Bottom;

        var stopwatch = Stopwatch.StartNew();
        var best = StochasticProbeEvaluation.Empty(startMetric);
        var candidates = 0;
        var simTicks = 0;
        var environment = ClassifyEnvironment(world, self, goal);
        var topology = BuildLocalTopology(world.Level, self, goal, trapX, trapBottom, trapMetric, avoidBestTrap);
        var candidateTrace = new List<string>(MaxCandidatesPerDecision + 8);
        foreach (var candidate in EnumerateCandidates(self, goal, environment, topology))
        {
            if (candidates >= MaxCandidatesPerDecision
                || simTicks + candidate.DurationTicks > MaxSimTicksPerDecision
                || (candidates >= MinimumCandidatesBeforeStopwatchAbort
                    && stopwatch.Elapsed.TotalMilliseconds >= MaxDecisionMilliseconds))
            {
                break;
            }

            candidates += 1;
            simTicks += candidate.DurationTicks;
            var evaluation = EvaluateCandidate(
                world,
                self,
                goal,
                candidate,
                startMetric,
                stagnant,
                avoidBestTrap,
                trapX,
                trapBottom,
                trapMetric,
                escapingPocket,
                _escapeDirection,
                topology);
            candidateTrace.Add(FormatCandidateTrace("base", evaluation, candidate));
            if (ShouldPreferEvaluation(evaluation, best))
            {
                best = evaluation;
            }
        }

        stopwatch.Stop();
        if ((best.Accepted && IsBlockingGoalProgress(best, self, goal))
            || (!best.Accepted && IsForwardBlocked(world.Level, self, self.Team, MathF.Sign(goal.X - self.X), DeadEndProbeDistance)))
        {
            if (TryEvaluateDeadEndDetour(
                    world,
                    self,
                    goal,
                    startMetric,
                    stagnant,
                    avoidBestTrap,
                    trapX,
                    trapBottom,
                    trapMetric,
                    escapingPocket,
                    _escapeDirection,
                    topology,
                    candidateTrace,
                    out var detour)
                && (ShouldPreferEvaluation(detour, best) || detour.Score > best.Score * 0.5f))
            {
                best = detour;
            }
        }

        SetCandidateTrace(string.IsNullOrWhiteSpace(topology.Trace)
            ? string.Join(" | ", candidateTrace)
            : $"{topology.Trace} escape={(escapingPocket ? _escapeDirection.ToString("0", CultureInfo.InvariantCulture) : "0")} trap=({trapX:0},{trapBottom:0}) metric={trapMetric:0} | {string.Join(" | ", candidateTrace)}");

        if (!best.Accepted)
        {
            trace = new StochasticLocalMotionTrace(
                Resolved: false,
                Source: "probe",
                MacroLabel: string.Empty,
                StartMetric: startMetric,
                BestMetric: best.BestMetric,
                FinalMetric: best.FinalMetric,
                Progress: best.Progress,
                Score: best.Score,
                Candidates: candidates,
                SimTicks: simTicks,
                ElapsedMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
                Reached: false,
                RejectedReason: "no_candidate");
            return false;
        }

        _activePlan = LocalMacroPlan.From(best.Candidate, tick);
        _activeUntilTick = tick + _activePlan.CommitTicks;
        input = BuildInput(self, goal, _activePlan, age: 0);
        trace = new StochasticLocalMotionTrace(
            Resolved: true,
            Source: "probe",
            MacroLabel: _activePlan.Label,
            StartMetric: startMetric,
            BestMetric: best.BestMetric,
            FinalMetric: best.FinalMetric,
            Progress: best.Progress,
            Score: best.Score,
            Candidates: candidates,
            SimTicks: simTicks,
            ElapsedMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
            Reached: best.Reached,
            RejectedReason: string.Empty);
        return true;
    }

    private static IEnumerable<LocalMacroCandidate> EnumerateCandidates(
        PlayerEntity self,
        StochasticLocalMotionGoal goal,
        LocalEnvironment environment,
        LocalTopology topology)
    {
        float primary = MathF.Sign(goal.X - self.X);
        if (primary == 0f)
        {
            primary = self.FacingDirectionX == 0f ? 1f : MathF.Sign(self.FacingDirectionX);
        }

        var secondary = -primary;
        if (self.IsGrounded)
        {
            var targetBelow = goal.Bottom > self.Bottom + 96f;
            if (environment.ShouldJumpGap || environment.ShouldJumpObstacle)
            {
                yield return new LocalMacroCandidate($"affordanceRunupJump:{primary:0}", primary, 34, 18, 4, 5, 0);
                yield return new LocalMacroCandidate($"affordanceJump:{primary:0}", primary, 28, 16, 0, 5, 0);
                yield return new LocalMacroCandidate($"affordanceRun:{primary:0}", primary, 18, MinimumCommitTicks, int.MaxValue, 0, 0);
            }

            yield return new LocalMacroCandidate($"run:{primary:0}", primary, 16, MinimumCommitTicks, int.MaxValue, 0, 0);
            yield return new LocalMacroCandidate($"runLong:{primary:0}", primary, 26, 10, int.MaxValue, 0, 0);
            yield return new LocalMacroCandidate($"jump:{primary:0}", primary, 24, 12, 0, 4, 0);
            yield return new LocalMacroCandidate($"runupJump:{primary:0}", primary, 30, 12, 5, 4, 0);
            yield return new LocalMacroCandidate(
                $"wallRunupVault:{primary:0}",
                primary,
                78,
                78,
                7,
                8,
                0,
                -primary,
                24);
            yield return new LocalMacroCandidate(
                $"wallRunupLateVault:{primary:0}",
                primary,
                92,
                92,
                14,
                8,
                0,
                -primary,
                32);

            var emittedFrontiers = 0;
            foreach (var frontier in topology.Frontiers)
            {
                foreach (var candidate in EnumerateFrontierCandidates(self, frontier))
                {
                    if (!candidate.Label.StartsWith("none:", StringComparison.Ordinal))
                    {
                        yield return candidate;
                    }
                }

                if (!frontier.IsPocket && !frontier.IsTrap)
                {
                    emittedFrontiers += 1;
                }

                if (emittedFrontiers >= 2)
                {
                    break;
                }
            }

            if (targetBelow)
            {
                yield return new LocalMacroCandidate($"ledgeHopFast:{primary:0}", primary, 28, 12, 0, 4, 0);
                yield return new LocalMacroCandidate($"ledgeHopLong:{primary:0}", primary, 42, 14, 0, 5, 0);
                yield return new LocalMacroCandidate($"ledgeRunupHop:{primary:0}", primary, 46, 16, 7, 5, 0);
                if (environment.GroundAhead)
                {
                    yield return new LocalMacroCandidate($"ledgeRun:{primary:0}", primary, 30, 12, int.MaxValue, 0, 0);
                }

                yield return new LocalMacroCandidate($"ledgeHop:{primary:0}", primary, 30, 12, 4, 3, 0);
            }

            yield return new LocalMacroCandidate($"vaultJump:{primary:0}", primary, 46, 18, 0, 8, 0);
            yield return new LocalMacroCandidate($"lateVaultJump:{primary:0}", primary, 58, 20, 8, 8, 0);

            yield return new LocalMacroCandidate($"brake:{primary:0}", -MathF.Sign(self.HorizontalSpeed == 0f ? primary : self.HorizontalSpeed), 8, MinimumCommitTicks, int.MaxValue, 0, 0);
            yield return new LocalMacroCandidate($"runAlt:{secondary:0}", secondary, 14, MinimumCommitTicks, int.MaxValue, 0, 0);
            if (!targetBelow && environment.HeadClear)
            {
                yield return new LocalMacroCandidate("verticalJump", 0f, 18, MinimumCommitTicks, 0, 4, 0);
            }

            yield break;
        }

        yield return new LocalMacroCandidate($"air:{primary:0}", primary, 14, MinimumCommitTicks, int.MaxValue, 0, 0);
        yield return new LocalMacroCandidate($"airLong:{primary:0}", primary, 22, 10, int.MaxValue, 0, 0);
        yield return new LocalMacroCandidate($"airBrake:{primary:0}", -MathF.Sign(self.HorizontalSpeed == 0f ? primary : self.HorizontalSpeed), 12, MinimumCommitTicks, int.MaxValue, 0, 0);
        if (self.RemainingAirJumps > 0 && goal.Bottom < self.Bottom - 20f)
        {
            yield return new LocalMacroCandidate($"airJump:{primary:0}", primary, 20, 10, 0, 4, 0);
        }

        yield return new LocalMacroCandidate("fallWait", 0f, 12, MinimumCommitTicks, int.MaxValue, 0, 0);
    }

    private static IEnumerable<LocalMacroCandidate> EnumerateFrontierCandidates(PlayerEntity self, LocalFrontier frontier)
    {
        var direction = MathF.Sign(frontier.X - self.X);
        if (direction == 0f)
        {
            yield return new LocalMacroCandidate("none:frontier", 0f, 0, 0, int.MaxValue, 0, 0);
            yield break;
        }

        var dx = MathF.Abs(frontier.X - self.X);
        var dy = frontier.Bottom - self.Bottom;
        var duration = (int)Math.Clamp((dx / 4f) + 16f, 24f, 62f);
        var labelDirection = direction.ToString("0", CultureInfo.InvariantCulture);
        if (dy < -34f)
        {
            var climbDuration = (int)Math.Clamp(duration + 14, 42, 76);
            yield return new LocalMacroCandidate(
                $"frontierBackoffClimb:{frontier.Kind}:{labelDirection}",
                direction,
                climbDuration + 12,
                climbDuration + 12,
                4,
                5,
                0,
                -direction,
                12);
            yield return new LocalMacroCandidate($"frontierClimbImmediate:{frontier.Kind}:{labelDirection}", direction, climbDuration, climbDuration, 0, 5, 0);
            yield break;
        }

        if (dy > 28f || frontier.Kind.Contains("landing", StringComparison.Ordinal))
        {
            var jumpStart = dx > 96f ? 6 : 0;
            yield return new LocalMacroCandidate($"frontierHop:{frontier.Kind}:{labelDirection}", direction, duration, duration, jumpStart, 5, 0);
            yield break;
        }

        yield return new LocalMacroCandidate($"frontierRun:{frontier.Kind}:{labelDirection}", direction, duration, duration, int.MaxValue, 0, 0);
        if (frontier.IsEdge)
        {
            var jumpStart = dx > 96f ? 6 : 0;
            yield return new LocalMacroCandidate($"frontierEdgeHop:{frontier.Kind}:{labelDirection}", direction, duration, duration, jumpStart, 5, 0);
        }
    }

    private static LocalTopology BuildLocalTopology(
        SimpleLevel level,
        PlayerEntity self,
        StochasticLocalMotionGoal goal,
        float trapX,
        float trapBottom,
        float trapMetric,
        bool avoidBestTrap)
    {
        float goalDirection = MathF.Sign(goal.X - self.X);
        if (goalDirection == 0f)
        {
            goalDirection = self.FacingDirectionX == 0f ? 1f : MathF.Sign(self.FacingDirectionX);
        }

        var startMetric = MeasureMetric(self.X, self.Bottom, goal);
        var all = new List<LocalFrontier>(32);
        var minX = self.X - TopologyScanRadius;
        var maxX = self.X + TopologyScanRadius;
        for (var x = minX; x <= maxX; x += TopologyScanStepX)
        {
            foreach (var bottom in FindStandableBottomsAt(
                         level,
                         self,
                         self.Team,
                         x,
                         self.Bottom - 224f,
                         self.Bottom + 320f))
            {
                if (HasNearbyFrontier(all, x, bottom))
                {
                    continue;
                }

                var towardGoal = MathF.Sign(x - self.X) == goalDirection;
                var metric = MeasureMetric(x, bottom, goal);
                var metricProgress = startMetric - metric;
                var top = bottom - self.CollisionBottomOffset;
                var wallAhead = !self.CanOccupy(level, self.Team, x + (goalDirection * 18f), top);
                var sameSurfaceAhead = TryFindStandableBottomAt(
                    level,
                    self,
                    self.Team,
                    x + (goalDirection * 64f),
                    bottom - 38f,
                    bottom + 64f,
                    out _);
                var landingAhead = TryFindStandableBottomAt(
                    level,
                    self,
                    self.Team,
                    x + (goalDirection * 96f),
                    bottom - 96f,
                    bottom + 144f,
                    out _);
                var verticalDelta = self.Bottom - bottom;
                var isClimbBand = verticalDelta > 24f;
                var isEdge = !sameSurfaceAhead && landingAhead;
                var isPocket = wallAhead && !sameSurfaceAhead && !landingAhead;
                var isTrap = avoidBestTrap
                    && MathF.Abs(bottom - trapBottom) <= 32f
                    && MathF.Abs(x - trapX) <= FailedPocketRadius
                    && metric >= trapMetric - 16f;
                var kind = isTrap
                    ? "trap"
                    : isPocket
                        ? "pocket"
                        : isClimbBand
                            ? "climbBand"
                            : isEdge
                                ? "edgeLanding"
                                : sameSurfaceAhead
                                    ? "surface"
                                    : "landing";
                var score = (metricProgress * 2.2f)
                    + (towardGoal ? 850f : -450f)
                    + (sameSurfaceAhead ? 360f : 0f)
                    + (isEdge ? 520f : 0f)
                    + (landingAhead ? 420f : 0f)
                    + (isClimbBand ? 720f : 0f)
                    - (isPocket ? 1800f : 0f)
                    - (isTrap ? 7000f : 0f)
                    - (MathF.Abs(x - self.X) * 0.18f)
                    - (MathF.Abs(bottom - self.Bottom) * 0.18f);
                if (towardGoal || score > 250f || isClimbBand)
                {
                    all.Add(new LocalFrontier(x, bottom, kind, score, isEdge, isPocket, isTrap));
                }
            }
        }

        all.Sort(static (a, b) => b.Score.CompareTo(a.Score));
        var selected = new List<LocalFrontier>(2);
        foreach (var frontier in all)
        {
            if (selected.Count >= 2)
            {
                break;
            }

            if (frontier.IsTrap || frontier.IsPocket)
            {
                continue;
            }

            selected.Add(frontier);
        }

        var trace = selected.Count == 0
            ? "topology:none"
            : "topology:" + string.Join(
                ";",
                selected.Select(static frontier =>
                    $"{frontier.Kind}@({frontier.X:0},{frontier.Bottom:0}) score={frontier.Score:0}"));
        return new LocalTopology(selected, trace);
    }

    private static bool HasNearbyFrontier(List<LocalFrontier> frontiers, float x, float bottom)
    {
        foreach (var frontier in frontiers)
        {
            if (MathF.Abs(frontier.X - x) <= 28f
                && MathF.Abs(frontier.Bottom - bottom) <= 24f)
            {
                return true;
            }
        }

        return false;
    }

    private static float ResolveEscapeDirection(PlayerEntity self, StochasticLocalMotionGoal goal, float pocketX)
    {
        var direction = MathF.Sign(self.X - pocketX);
        if (direction != 0f)
        {
            return direction;
        }

        direction = -MathF.Sign(goal.X - self.X);
        if (direction != 0f)
        {
            return direction;
        }

        return self.FacingDirectionX == 0f ? 1f : -MathF.Sign(self.FacingDirectionX);
    }

    private void SetCandidateTrace(string trace)
    {
        _lastCandidateTrace = trace;
        if (!string.IsNullOrWhiteSpace(trace))
        {
            _lastNonEmptyCandidateTrace = trace;
        }
    }

    private static bool TryFindStandableBottomAt(
        SimpleLevel level,
        PlayerEntity self,
        PlayerTeam team,
        float x,
        float minBottom,
        float maxBottom,
        out float bottom)
    {
        bottom = 0f;
        var bestScore = float.PositiveInfinity;
        var found = false;
        for (var candidateBottom = minBottom; candidateBottom <= maxBottom; candidateBottom += TopologyScanStepBottom)
        {
            if (!CanStandAt(level, self, team, x, candidateBottom))
            {
                continue;
            }

            var score = MathF.Abs(candidateBottom - self.Bottom);
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bottom = candidateBottom;
            found = true;
        }

        return found;
    }

    private static List<float> FindStandableBottomsAt(
        SimpleLevel level,
        PlayerEntity self,
        PlayerTeam team,
        float x,
        float minBottom,
        float maxBottom)
    {
        var bottoms = new List<float>(4);
        for (var candidateBottom = minBottom; candidateBottom <= maxBottom; candidateBottom += TopologyScanStepBottom)
        {
            if (!CanStandAt(level, self, team, x, candidateBottom))
            {
                continue;
            }

            var duplicate = false;
            foreach (var bottom in bottoms)
            {
                if (MathF.Abs(bottom - candidateBottom) <= 24f)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
            {
                bottoms.Add(candidateBottom);
            }

            if (bottoms.Count >= 5)
            {
                break;
            }
        }

        bottoms.Sort((a, b) => MathF.Abs(a - self.Bottom).CompareTo(MathF.Abs(b - self.Bottom)));
        return bottoms;
    }

    private static bool CanStandAt(SimpleLevel level, PlayerEntity self, PlayerTeam team, float x, float bottom)
    {
        var y = bottom - self.CollisionBottomOffset;
        return self.CanOccupy(level, team, x, y)
            && !self.CanOccupy(level, team, x, y + 2f);
    }

    private static FrontierFit FindFrontierFit(LocalTopology topology, float x, float bottom, float bestMetric)
    {
        var bestScore = 0f;
        var isTrap = false;
        foreach (var frontier in topology.Frontiers)
        {
            var distance = Distance(x, bottom, frontier.X, frontier.Bottom);
            if (distance > 112f)
            {
                continue;
            }

            var proximityScore = MathF.Max(0f, 112f - distance) * 9f;
            var score = frontier.Score + proximityScore - (bestMetric * 0.02f);
            if (score > bestScore)
            {
                bestScore = score;
            }

            isTrap |= frontier.IsTrap;
        }

        return new FrontierFit(bestScore, isTrap);
    }

    private static StochasticProbeEvaluation EvaluateCandidate(
        SimulationWorld world,
        PlayerEntity self,
        StochasticLocalMotionGoal goal,
        LocalMacroCandidate candidate,
        float startMetric,
        bool stagnant,
        bool avoidBestTrap,
        float trapX,
        float trapBottom,
        float trapMetric,
        bool escapingPocket,
        float escapeDirection,
        LocalTopology topology)
    {
        var probe = new PlayerEntity(ProbeEntityId, self.ClassDefinition, "StochasticLocalMotionProbe");
        var state = self.CapturePredictionState();
        probe.RestorePredictionState(in state);

        var previousInput = default(PlayerInputSnapshot);
        var bestMetric = startMetric;
        var bestGroundedMetric = self.IsGrounded ? startMetric : float.PositiveInfinity;
        var reached = false;
        for (var age = 0; age < candidate.DurationTicks; age += 1)
        {
            var input = BuildInput(probe, goal, LocalMacroPlan.From(candidate, startTick: 0), age);
            var jumpPressed = input.Up && !previousInput.Up;
            probe.Advance(input, jumpPressed, world.Level, self.Team, 1d / SimulationConfig.DefaultTicksPerSecond);
            previousInput = input;

            if (!IsValidProbeState(world, probe, self.Team))
            {
                return StochasticProbeEvaluation.Rejected(candidate, startMetric, bestMetric);
            }

            var metric = MeasureMetric(probe.X, probe.Bottom, goal);
            bestMetric = MathF.Min(bestMetric, metric);
            if (probe.IsGrounded)
            {
                bestGroundedMetric = MathF.Min(bestGroundedMetric, metric);
            }

            if (HasReachedGoal(probe, goal))
            {
                reached = true;
                break;
            }
        }

        var finalMetric = MeasureMetric(probe.X, probe.Bottom, goal);
        var progress = startMetric - bestMetric;
        var endpointProgress = startMetric - finalMetric;
        var groundedProgress = float.IsFinite(bestGroundedMetric) ? startMetric - bestGroundedMetric : 0f;
        var requiredProgress = Math.Clamp(startMetric * 0.15f, 4f, MinimumAcceptedProgress);
        var goalDirection = MathF.Sign(goal.X - self.X);
        var endpointForwardBlocked = IsForwardBlocked(world.Level, probe, self.Team, goalDirection, 36f);
        var endsBlocked = EndsBlockedTowardGoal(world.Level, probe, self.Team, candidate.MoveDirection, goal)
            || endpointForwardBlocked;
        var needsContinuation = endsBlocked && !reached;
        var continuation = needsContinuation
            ? EvaluateBestContinuation(world, probe, self.Team, goal, finalMetric)
            : ContinuationEvaluation.Empty;
        var continuationProgress = continuation.Accepted && float.IsFinite(continuation.FinalMetric)
            ? startMetric - continuation.FinalMetric
            : 0f;
        var continuationImprovesEndpoint = continuation.Accepted
            && continuation.FinalMetric <= finalMetric - 12f;
        var displacement = MathF.Abs(probe.X - self.X) + (MathF.Abs(probe.Bottom - self.Bottom) * 0.5f);
        var frontierFit = FindFrontierFit(topology, probe.X, probe.Bottom, bestMetric);
        var routeNovelty = MathF.Abs(probe.Bottom - self.Bottom) >= 42f
            || (avoidBestTrap && MathF.Abs(probe.Bottom - trapBottom) >= 42f)
            || (frontierFit.Score >= 900f && progress >= 96f);
        var accepted = reached
            || (progress >= requiredProgress && !needsContinuation)
            || (probe.IsGrounded && groundedProgress >= requiredProgress && !needsContinuation)
            || (probe.IsGrounded && frontierFit.Score >= 800f && progress >= requiredProgress)
            || (needsContinuation
                && continuationImprovesEndpoint
                && continuation.FinalMetric <= startMetric + 96f
                && continuationProgress >= requiredProgress);
        if (accepted
            && !reached
            && candidate.MoveDirection == 0f
            && displacement < 36f)
        {
            return StochasticProbeEvaluation.Rejected(candidate, startMetric, bestMetric, finalMetric, progress, probe.X, probe.Bottom, endsBlocked);
        }

        if (accepted
            && !reached
            && endsBlocked
            && finalMetric >= startMetric - 4f)
        {
            return StochasticProbeEvaluation.Rejected(candidate, startMetric, bestMetric, finalMetric, progress, probe.X, probe.Bottom, endsBlocked);
        }

        if (accepted
            && !reached
            && !continuation.Accepted
            && finalMetric > startMetric + 180f)
        {
            return StochasticProbeEvaluation.Rejected(candidate, startMetric, bestMetric, finalMetric, progress, probe.X, probe.Bottom, endsBlocked);
        }

        if (accepted
            && !reached
            && avoidBestTrap
            && Distance(probe.X, probe.Bottom, trapX, trapBottom) <= FailedPocketRadius
            && finalMetric >= trapMetric - 12f
            && (endsBlocked || displacement < 48f)
            && !routeNovelty)
        {
            return StochasticProbeEvaluation.Rejected(candidate, startMetric, bestMetric, finalMetric, progress, probe.X, probe.Bottom, endsBlocked);
        }

        var escapeDisplacement = escapingPocket
            ? (probe.X - self.X) * escapeDirection
            : 0f;
        var endTrapDistance = avoidBestTrap
            ? Distance(probe.X, probe.Bottom, trapX, trapBottom)
            : float.PositiveInfinity;
        var escapeDistanceGain = escapingPocket
            ? endTrapDistance - Distance(self.X, self.Bottom, trapX, trapBottom)
            : 0f;
        if (accepted
            && !reached
            && escapingPocket
            && bestMetric >= trapMetric - EscapePocketBreakthrough
            && endTrapDistance <= FailedPocketRadius
            && escapeDisplacement < 28f
            && escapeDistanceGain < 28f
            && !routeNovelty)
        {
            return StochasticProbeEvaluation.Rejected(candidate, startMetric, bestMetric, finalMetric, progress, probe.X, probe.Bottom, endsBlocked);
        }

        if (!accepted)
        {
            return StochasticProbeEvaluation.Rejected(candidate, startMetric, bestMetric, finalMetric, progress, probe.X, probe.Bottom, endsBlocked);
        }

        var movingAgainstGoal = MathF.Sign(candidate.MoveDirection) != 0f
            && goalDirection != 0f
            && MathF.Sign(candidate.MoveDirection) != goalDirection;
        var targetBelow = goal.Bottom > self.Bottom + 96f;
        var suspiciousTargetBelowLedgeRun = targetBelow
            && candidate.Label.StartsWith("ledgeRun:", StringComparison.Ordinal);
        var scoredProgress = needsContinuation ? MathF.Min(progress, MathF.Max(0f, continuationProgress)) : progress;
        var scoredEndpointProgress = needsContinuation ? MathF.Min(endpointProgress, MathF.Max(0f, continuationProgress)) : endpointProgress;
        var endpointRegression = MathF.Max(0f, finalMetric - startMetric);
        var score = (scoredProgress * 3.5f)
            + (scoredEndpointProgress * 2.5f)
            + (groundedProgress * 1.25f)
            + (continuationProgress * 2.25f)
            + (continuation.Accepted ? continuation.Score * 0.35f : 0f)
            + frontierFit.Score
            + (reached ? 2000f : 0f)
            + (probe.IsGrounded ? 80f : -120f)
            - (candidate.DurationTicks * 1.5f)
            - (candidate.JumpStartTick == int.MaxValue ? 0f : 18f)
            - (candidate.DropTicks > 0 ? 22f : 0f)
            - (movingAgainstGoal ? 35f : 0f)
            - (stagnant && movingAgainstGoal ? 260f : 0f)
            - (stagnant && candidate.Label.Contains("Reverse", StringComparison.Ordinal) ? 360f : 0f)
            - (suspiciousTargetBelowLedgeRun ? 1250f : 0f)
            - (endpointRegression * 3.0f)
            + (escapingPocket ? Math.Clamp(escapeDisplacement, -96f, 160f) * 8f : 0f)
            + (escapingPocket ? Math.Clamp(escapeDistanceGain, -64f, 160f) * 5f : 0f)
            - (escapingPocket && MathF.Sign(candidate.MoveDirection) == -escapeDirection && endTrapDistance <= FailedPocketRadius + 48f ? 900f : 0f);
        if (frontierFit.IsTrap)
        {
            score -= 6000f;
        }

        if (avoidBestTrap
            && Distance(probe.X, probe.Bottom, trapX, trapBottom) <= FailedPocketRadius
            && finalMetric >= trapMetric - 12f
            && (endsBlocked || displacement < 48f)
            && !routeNovelty)
        {
            score -= 8000f;
        }
        if (needsContinuation)
        {
            score += continuationImprovesEndpoint ? -140f : -900f;
        }

        return new StochasticProbeEvaluation(
            Accepted: true,
            Candidate: candidate,
            BestMetric: bestMetric,
            FinalMetric: finalMetric,
            Progress: progress,
            Score: score,
            Reached: reached,
            EndsBlocked: endsBlocked,
            EndX: probe.X,
            EndBottom: probe.Bottom);
    }

    private static bool TryEvaluateDeadEndDetour(
        SimulationWorld world,
        PlayerEntity self,
        StochasticLocalMotionGoal goal,
        float startMetric,
        bool stagnant,
        bool avoidBestTrap,
        float trapX,
        float trapBottom,
        float trapMetric,
        bool escapingPocket,
        float escapeDirection,
        LocalTopology topology,
        List<string> candidateTrace,
        out StochasticProbeEvaluation best)
    {
        best = StochasticProbeEvaluation.Empty(startMetric);
        var direction = (float)MathF.Sign(goal.X - self.X);
        if (direction == 0f)
        {
            return false;
        }

        var startClearance = MeasureForwardClearance(world.Level, self, self.Team, direction, DeadEndProbeDistance);
        var allowReverseDetours = startClearance <= 30f || MathF.Abs(self.HorizontalSpeed) < 40f;
        var candidates = 0;
        var simTicks = 0;
        foreach (var candidate in EnumerateDeadEndDetourCandidates(direction, allowReverseDetours))
        {
            candidates += 1;
            simTicks += candidate.DurationTicks;
            var evaluation = EvaluateDeadEndDetourCandidate(
                world,
                self,
                goal,
                candidate,
                startMetric,
                direction,
                startClearance,
                stagnant,
                avoidBestTrap,
                trapX,
                trapBottom,
                trapMetric,
                escapingPocket,
                escapeDirection,
                topology);
            candidateTrace.Add(FormatCandidateTrace("detour", evaluation, candidate));
            if (ShouldPreferEvaluation(evaluation, best))
            {
                best = evaluation;
            }
        }

        if (!best.Accepted)
        {
            return false;
        }

        best = best with
        {
            Candidates = candidates,
            SimTicks = simTicks,
        };
        return true;
    }

    private static string FormatCandidateTrace(string group, StochasticProbeEvaluation evaluation, LocalMacroCandidate candidate)
    {
        var status = evaluation.Accepted ? "ok" : "reject";
        return
            $"{group}:{candidate.Label}:{status}:score={evaluation.Score:0.0}:progress={evaluation.Progress:0.0}:" +
            $"best={evaluation.BestMetric:0.0}:final={evaluation.FinalMetric:0.0}:end=({evaluation.EndX:0.0},{evaluation.EndBottom:0.0}):blocked={(evaluation.EndsBlocked ? 1 : 0)}";
    }

    private static bool ShouldPreferEvaluation(StochasticProbeEvaluation candidate, StochasticProbeEvaluation current)
    {
        if (!candidate.Accepted)
        {
            return false;
        }

        if (!current.Accepted)
        {
            return true;
        }

        if (candidate.EndsBlocked != current.EndsBlocked)
        {
            if (candidate.EndsBlocked)
            {
                return candidate.Score > current.Score + 750f
                    && candidate.FinalMetric < current.FinalMetric - 72f;
            }

            return candidate.Score + 750f >= current.Score
                || candidate.FinalMetric < current.FinalMetric - 48f;
        }

        return candidate.Score > current.Score;
    }

    private static IEnumerable<LocalMacroCandidate> EnumerateDeadEndDetourCandidates(float direction, bool allowReverseDetours)
    {
        var alternate = -direction;
        yield return new LocalMacroCandidate($"detourJump:{direction:0}", direction, 44, 44, 0, 5, 0);
        yield return new LocalMacroCandidate($"detourRunupJump:{direction:0}", direction, 56, 56, 8, 5, 0);
        yield return new LocalMacroCandidate("detourVerticalJump", 0f, 38, 38, 0, 5, 0);
        if (!allowReverseDetours)
        {
            yield break;
        }

        yield return new LocalMacroCandidate($"detourReverseDrop:{alternate:0}", alternate, 56, 56, int.MaxValue, 0, 10);
        yield return new LocalMacroCandidate($"detourReverseRun:{alternate:0}", alternate, 48, 48, int.MaxValue, 0, 0);
        yield return new LocalMacroCandidate($"detourReverseJump:{alternate:0}", alternate, 52, 52, 0, 5, 0);
    }

    private static StochasticProbeEvaluation EvaluateDeadEndDetourCandidate(
        SimulationWorld world,
        PlayerEntity self,
        StochasticLocalMotionGoal goal,
        LocalMacroCandidate candidate,
        float startMetric,
        float goalDirection,
        float startClearance,
        bool stagnant,
        bool avoidBestTrap,
        float trapX,
        float trapBottom,
        float trapMetric,
        bool escapingPocket,
        float escapeDirection,
        LocalTopology topology)
    {
        var probe = new PlayerEntity(ProbeEntityId, self.ClassDefinition, "StochasticLocalMotionDetourProbe");
        var state = self.CapturePredictionState();
        probe.RestorePredictionState(in state);

        var previousInput = default(PlayerInputSnapshot);
        var bestMetric = startMetric;
        var reached = false;
        for (var age = 0; age < candidate.DurationTicks; age += 1)
        {
            var input = BuildInput(probe, goal, LocalMacroPlan.From(candidate, startTick: 0), age);
            var jumpPressed = input.Up && !previousInput.Up;
            probe.Advance(input, jumpPressed, world.Level, self.Team, 1d / SimulationConfig.DefaultTicksPerSecond);
            previousInput = input;

            if (!IsValidProbeState(world, probe, self.Team))
            {
                return StochasticProbeEvaluation.Rejected(candidate, startMetric, bestMetric);
            }

            var metric = MeasureMetric(probe.X, probe.Bottom, goal);
            bestMetric = MathF.Min(bestMetric, metric);
            if (HasReachedGoal(probe, goal))
            {
                reached = true;
                break;
            }
        }

        var finalMetric = MeasureMetric(probe.X, probe.Bottom, goal);
        var progress = startMetric - bestMetric;
        var finalClearance = MeasureForwardClearance(world.Level, probe, self.Team, goalDirection, DeadEndProbeDistance);
        var clearanceGain = finalClearance - startClearance;
        var verticalGainTowardGoal = MathF.Abs(goal.Bottom - self.Bottom) - MathF.Abs(goal.Bottom - probe.Bottom);
        var endsBlocked = EndsBlockedTowardGoal(world.Level, probe, self.Team, candidate.MoveDirection, goal);
        var escapedWall = finalClearance >= MathF.Min(DeadEndProbeDistance, startClearance + 36f)
            && !endsBlocked;
        var continuation = EvaluateBestContinuation(world, probe, self.Team, goal, finalMetric);
        var displacement = MathF.Abs(probe.X - self.X) + (MathF.Abs(probe.Bottom - self.Bottom) * 0.5f);
        if (candidate.MoveDirection == 0f
            && displacement < 36f
            && !reached)
        {
            return StochasticProbeEvaluation.Rejected(candidate, startMetric, bestMetric, finalMetric, progress, probe.X, probe.Bottom, endsBlocked);
        }

        var accepted = reached
            || progress >= 12f
            || continuation.Accepted
            || (escapedWall && verticalGainTowardGoal >= -48f)
            || (escapedWall && MathF.Abs(probe.X - self.X) >= 24f);
        if (accepted
            && !reached
            && !continuation.Accepted
            && finalMetric > startMetric + 160f)
        {
            return StochasticProbeEvaluation.Rejected(candidate, startMetric, bestMetric, finalMetric, progress, probe.X, probe.Bottom, endsBlocked);
        }

        if (accepted
            && !reached
            && avoidBestTrap
            && Distance(probe.X, probe.Bottom, trapX, trapBottom) <= FailedPocketRadius
            && finalMetric >= trapMetric - 12f
            && (endsBlocked || displacement < 48f))
        {
            return StochasticProbeEvaluation.Rejected(candidate, startMetric, bestMetric, finalMetric, progress, probe.X, probe.Bottom, endsBlocked);
        }

        var escapeDisplacement = escapingPocket
            ? (probe.X - self.X) * escapeDirection
            : 0f;
        var endTrapDistance = avoidBestTrap
            ? Distance(probe.X, probe.Bottom, trapX, trapBottom)
            : float.PositiveInfinity;
        var escapeDistanceGain = escapingPocket
            ? endTrapDistance - Distance(self.X, self.Bottom, trapX, trapBottom)
            : 0f;
        if (accepted
            && !reached
            && escapingPocket
            && bestMetric >= trapMetric - EscapePocketBreakthrough
            && endTrapDistance <= FailedPocketRadius
            && escapeDisplacement < 28f
            && escapeDistanceGain < 28f)
        {
            return StochasticProbeEvaluation.Rejected(candidate, startMetric, bestMetric, finalMetric, progress, probe.X, probe.Bottom, endsBlocked);
        }

        if (!accepted)
        {
            return StochasticProbeEvaluation.Rejected(candidate, startMetric, bestMetric, finalMetric, progress, probe.X, probe.Bottom, endsBlocked);
        }

        var movingAgainstGoal = MathF.Sign(candidate.MoveDirection) != 0f
            && MathF.Sign(candidate.MoveDirection) != goalDirection;
        if (movingAgainstGoal
            && !continuation.Accepted
            && finalMetric > startMetric + 48f)
        {
            return StochasticProbeEvaluation.Rejected(candidate, startMetric, bestMetric, finalMetric, progress, probe.X, probe.Bottom, endsBlocked);
        }

        var frontierFit = FindFrontierFit(topology, probe.X, probe.Bottom, bestMetric);
        var endpointRegression = MathF.Max(0f, finalMetric - startMetric);
        var score = (progress * 3f)
            + (clearanceGain * 4f)
            + (verticalGainTowardGoal * 1.5f)
            + (continuation.Accepted ? continuation.Score * 0.45f : -120f)
            + frontierFit.Score
            + (escapedWall ? 220f : 0f)
            + (reached ? 2000f : 0f)
            + (probe.IsGrounded ? 35f : -25f)
            - (candidate.DurationTicks * 0.75f)
            - (movingAgainstGoal ? 35f : 0f)
            - (stagnant && movingAgainstGoal ? 320f : 0f)
            - (stagnant && candidate.Label.Contains("Reverse", StringComparison.Ordinal) ? 420f : 0f)
            - (endpointRegression * 3.5f)
            + (escapingPocket ? Math.Clamp(escapeDisplacement, -96f, 160f) * 8f : 0f)
            + (escapingPocket ? Math.Clamp(escapeDistanceGain, -64f, 160f) * 5f : 0f)
            - (escapingPocket && MathF.Sign(candidate.MoveDirection) == -escapeDirection && endTrapDistance <= FailedPocketRadius + 48f ? 900f : 0f);
        if (frontierFit.IsTrap)
        {
            score -= 6000f;
        }

        if (avoidBestTrap
            && Distance(probe.X, probe.Bottom, trapX, trapBottom) <= FailedPocketRadius
            && finalMetric >= trapMetric - 12f
            && (endsBlocked || displacement < 48f))
        {
            score -= 8000f;
        }
        if (endsBlocked && !reached)
        {
            score -= 360f;
        }

        return new StochasticProbeEvaluation(
            Accepted: true,
            Candidate: candidate,
            BestMetric: bestMetric,
            FinalMetric: finalMetric,
            Progress: progress,
            Score: score,
            Reached: reached,
            EndsBlocked: endsBlocked,
            EndX: probe.X,
            EndBottom: probe.Bottom);
    }

    private static ContinuationEvaluation EvaluateBestContinuation(
        SimulationWorld world,
        PlayerEntity start,
        PlayerTeam team,
        StochasticLocalMotionGoal goal,
        float startMetric)
    {
        var best = ContinuationEvaluation.Empty;
        var candidates = 0;
        var simTicks = 0;
        var environment = ClassifyEnvironment(world, start, goal);
        var topology = LocalTopology.Empty;
        foreach (var candidate in EnumerateLipContinuationCandidates(start, goal, environment))
        {
            candidates += 1;
            simTicks += candidate.DurationTicks;
            var evaluation = EvaluateContinuationCandidate(world, start, team, goal, candidate, startMetric, allowBlockedProgress: true);
            if (evaluation.Accepted && evaluation.Score > best.Score)
            {
                best = evaluation;
            }
        }

        foreach (var candidate in EnumerateCandidates(start, goal, environment, topology))
        {
            if (candidates >= 8 || simTicks + candidate.DurationTicks > 180)
            {
                break;
            }

            candidates += 1;
            simTicks += candidate.DurationTicks;
            var evaluation = EvaluateContinuationCandidate(world, start, team, goal, candidate, startMetric, allowBlockedProgress: false);
            if (evaluation.Accepted && evaluation.Score > best.Score)
            {
                best = evaluation;
            }
        }

        return best;
    }

    private static IEnumerable<LocalMacroCandidate> EnumerateLipContinuationCandidates(
        PlayerEntity start,
        StochasticLocalMotionGoal goal,
        LocalEnvironment environment)
    {
        float direction = MathF.Sign(goal.X - start.X);
        if (direction == 0f)
        {
            direction = environment.Direction == 0f ? 1f : MathF.Sign(environment.Direction);
        }

        yield return new LocalMacroCandidate($"continuationLipJump:{direction:0}", direction, 42, 42, 0, 6, 0);
        yield return new LocalMacroCandidate($"continuationRunupLipJump:{direction:0}", direction, 54, 54, 5, 6, 0);
        yield return new LocalMacroCandidate(
            $"continuationBackoffLipJump:{direction:0}",
            direction,
            64,
            64,
            7,
            6,
            0,
            PreMoveDirection: -direction,
            PreMoveTicks: 12);
    }

    private static ContinuationEvaluation EvaluateContinuationCandidate(
        SimulationWorld world,
        PlayerEntity start,
        PlayerTeam team,
        StochasticLocalMotionGoal goal,
        LocalMacroCandidate candidate,
        float startMetric,
        bool allowBlockedProgress)
    {
        var probe = new PlayerEntity(ProbeEntityId, start.ClassDefinition, "StochasticLocalMotionContinuationProbe");
        var state = start.CapturePredictionState();
        probe.RestorePredictionState(in state);

        var previousInput = default(PlayerInputSnapshot);
        var bestMetric = startMetric;
        var reached = false;
        for (var age = 0; age < candidate.DurationTicks; age += 1)
        {
            var input = BuildInput(probe, goal, LocalMacroPlan.From(candidate, startTick: 0), age);
            var jumpPressed = input.Up && !previousInput.Up;
            probe.Advance(input, jumpPressed, world.Level, team, 1d / SimulationConfig.DefaultTicksPerSecond);
            previousInput = input;

            if (!IsValidProbeState(world, probe, team))
            {
                return ContinuationEvaluation.Empty;
            }

            bestMetric = MathF.Min(bestMetric, MeasureMetric(probe.X, probe.Bottom, goal));
            if (HasReachedGoal(probe, goal))
            {
                reached = true;
                break;
            }
        }

        var finalMetric = MeasureMetric(probe.X, probe.Bottom, goal);
        var progress = startMetric - bestMetric;
        var endpointProgress = startMetric - finalMetric;
        var endsBlocked = EndsBlockedTowardGoal(world.Level, probe, team, candidate.MoveDirection, goal);
        var displacement = MathF.Abs(probe.X - start.X) + (MathF.Abs(probe.Bottom - start.Bottom) * 0.5f);
        var blockedButUsefulLip = allowBlockedProgress
            && probe.IsGrounded
            && displacement >= 22f
            && (progress >= 36f || endpointProgress >= 36f)
            && HasHeadClear(world.Level, probe, team)
            && !IsInsideForwardWall(world.Level, probe, team, candidate.MoveDirection);
        var accepted = reached || (progress >= 18f && !endsBlocked) || (endpointProgress >= 18f && !endsBlocked) || blockedButUsefulLip;
        if (!accepted)
        {
            return ContinuationEvaluation.Empty;
        }

        var score = (progress * 4f)
            + (endpointProgress * 2f)
            + (reached ? 1200f : 0f)
            + (probe.IsGrounded ? 60f : -40f)
            + (blockedButUsefulLip ? 260f : 0f)
            - (candidate.DurationTicks * 1.0f)
            - (endsBlocked && !blockedButUsefulLip ? 420f : 0f)
            - (endsBlocked && blockedButUsefulLip ? 120f : 0f);
        return new ContinuationEvaluation(true, score, finalMetric, endsBlocked, candidate.MoveDirection);
    }

    private static bool IsBlockingGoalProgress(
        StochasticProbeEvaluation evaluation,
        PlayerEntity self,
        StochasticLocalMotionGoal goal)
    {
        if (!evaluation.Accepted || !evaluation.EndsBlocked)
        {
            return false;
        }

        var goalDirection = MathF.Sign(goal.X - self.X);
        var startMetric = MeasureMetric(self.X, self.Bottom, goal);
        return goalDirection != 0f
            && MathF.Sign(evaluation.Candidate.MoveDirection) == goalDirection
            && evaluation.Progress < MathF.Max(MinimumAcceptedProgress * 1.5f, startMetric * 0.08f);
    }

    private static bool EndsBlockedTowardGoal(
        SimpleLevel level,
        PlayerEntity probe,
        PlayerTeam team,
        float moveDirection,
        StochasticLocalMotionGoal goal)
    {
        var direction = MathF.Sign(moveDirection);
        if (direction == 0f)
        {
            direction = MathF.Sign(goal.X - probe.X);
        }

        return IsForwardBlocked(level, probe, team, direction, 18f);
    }

    private static bool IsForwardBlocked(
        SimpleLevel level,
        PlayerEntity self,
        PlayerTeam team,
        float direction,
        float maxDistance)
    {
        if (direction == 0f)
        {
            return false;
        }

        return MeasureForwardClearance(level, self, team, direction, maxDistance) < maxDistance;
    }

    private static float MeasureForwardClearance(
        SimpleLevel level,
        PlayerEntity self,
        PlayerTeam team,
        float direction,
        float maxDistance)
    {
        if (direction == 0f)
        {
            return maxDistance;
        }

        for (var distance = DeadEndProbeStep; distance <= maxDistance; distance += DeadEndProbeStep)
        {
            if (!self.CanOccupy(level, team, self.X + (direction * distance), self.Y))
            {
                return distance;
            }
        }

        return maxDistance;
    }

    private static bool HasHeadClear(SimpleLevel level, PlayerEntity self, PlayerTeam team)
    {
        return self.CanOccupy(level, team, self.X, self.Y - 12f)
            && self.CanOccupy(level, team, self.X, self.Y - 36f)
            && self.CanOccupy(level, team, self.X, self.Y - 60f);
    }

    private static bool IsInsideForwardWall(SimpleLevel level, PlayerEntity self, PlayerTeam team, float direction)
    {
        direction = MathF.Sign(direction);
        if (direction == 0f)
        {
            return false;
        }

        return !self.CanOccupy(level, team, self.X + (direction * 4f), self.Y - 44f)
            && !self.CanOccupy(level, team, self.X + (direction * 4f), self.Y - 16f)
            && !self.CanOccupy(level, team, self.X + (direction * 4f), self.Y);
    }

    private static bool IsValidProbeState(SimulationWorld world, PlayerEntity probe, PlayerTeam team)
    {
        return probe.IsAlive
            && probe.Bottom >= -FatalFallMargin
            && probe.Bottom <= world.Level.Bounds.Height + FatalFallMargin
            && probe.CanOccupy(world.Level, team, probe.X, probe.Y)
            && !probe.IsInsideBlockingTeamGate(world.Level, team);
    }

    private static LocalEnvironment ClassifyEnvironment(
        SimulationWorld world,
        PlayerEntity self,
        StochasticLocalMotionGoal goal)
    {
        float direction = MathF.Sign(goal.X - self.X);
        if (direction == 0f)
        {
            direction = self.FacingDirectionX == 0f ? 1f : MathF.Sign(self.FacingDirectionX);
        }

        var groundContact = !self.CanOccupy(world.Level, self.Team, self.X, self.Y + 1f);
        var headClear = HasHeadClear(world.Level, self, self.Team);
        var groundAhead = HasGroundAhead(world.Level, self, self.Team, direction, 15f);
        var footBlocked = !self.CanOccupy(world.Level, self.Team, self.X + (direction * 8f), self.Y);
        var waistBlocked = !self.CanOccupy(world.Level, self.Team, self.X + (direction * 15f), self.Y - 10f);
        var movingToward = MathF.Abs(self.HorizontalSpeed) <= 20f
            || MathF.Sign(self.HorizontalSpeed) == direction;
        var riseToGoal = self.Bottom - goal.Bottom;
        var gapCandidate = groundContact
            && headClear
            && !groundAhead
            && movingToward
            && riseToGoal >= -144f;
        var obstacleCandidate = groundContact
            && headClear
            && movingToward
            && (footBlocked || waistBlocked)
            && HasStepOrLipAhead(world.Level, self, self.Team, direction);

        return new LocalEnvironment(
            Direction: direction,
            GroundContact: groundContact,
            HeadClear: headClear,
            GroundAhead: groundAhead,
            FootBlocked: footBlocked,
            WaistBlocked: waistBlocked,
            ShouldJumpGap: gapCandidate,
            ShouldJumpObstacle: obstacleCandidate);
    }

    private static bool HasGroundAhead(
        SimpleLevel level,
        PlayerEntity self,
        PlayerTeam team,
        float direction,
        float distance)
    {
        return !self.CanOccupy(level, team, self.X + (direction * distance), self.Y + 8f)
            || !self.CanOccupy(level, team, self.X + (direction * distance), self.Y + 16f);
    }

    private static bool HasStepOrLipAhead(
        SimpleLevel level,
        PlayerEntity self,
        PlayerTeam team,
        float direction)
    {
        for (var distance = 6f; distance <= 60f; distance += 6f)
        {
            var x = self.X + (direction * distance);
            if (self.CanOccupy(level, team, x, self.Y))
            {
                continue;
            }

            return self.CanOccupy(level, team, x, self.Y - 8f)
                || self.CanOccupy(level, team, x, self.Y - 16f)
                || self.CanOccupy(level, team, x, self.Y - 24f);
        }

        return false;
    }

    private static PlayerInputSnapshot BuildInput(
        PlayerEntity self,
        StochasticLocalMotionGoal goal,
        LocalMacroPlan plan,
        int age)
    {
        var inPreMove = age < plan.PreMoveTicks && plan.PreMoveDirection != 0f;
        var macroAge = inPreMove ? 0 : age - plan.PreMoveTicks;
        var moveDirection = inPreMove ? plan.PreMoveDirection : plan.MoveDirection;
        var aimDirection = moveDirection == 0f
            ? goal.X >= self.X ? 1f : -1f
            : moveDirection;
        return new PlayerInputSnapshot(
            Left: moveDirection < 0f,
            Right: moveDirection > 0f,
            Up: !inPreMove && macroAge >= plan.JumpStartAge && macroAge < plan.JumpEndAge,
            Down: !inPreMove && macroAge < plan.DropEndAge,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: self.X + (aimDirection * 256f),
            AimWorldY: goal.Bottom,
            DebugKill: false);
    }

    private static float MeasureMetric(float x, float bottom, StochasticLocalMotionGoal goal)
    {
        var horizontal = MathF.Max(0f, MathF.Abs(goal.X - x) - goal.AcceptanceX);
        var vertical = MathF.Max(0f, MathF.Abs(goal.Bottom - bottom) - goal.AcceptanceBottom);
        return horizontal + (vertical * 2.4f);
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static bool HasReachedGoal(PlayerEntity self, StochasticLocalMotionGoal goal)
    {
        return MathF.Abs(self.X - goal.X) <= goal.AcceptanceX
            && MathF.Abs(self.Bottom - goal.Bottom) <= goal.AcceptanceBottom;
    }

    private readonly record struct LocalMacroCandidate(
        string Label,
        float MoveDirection,
        int DurationTicks,
        int CommitTicks,
        int JumpStartTick,
        int JumpHoldTicks,
        int DropTicks,
        float PreMoveDirection = 0f,
        int PreMoveTicks = 0);

    private readonly record struct LocalMacroPlan(
        bool HasPlan,
        string Label,
        float MoveDirection,
        int StartTick,
        int CommitTicks,
        int JumpStartAge,
        int JumpEndAge,
        int DropEndAge,
        float PreMoveDirection,
        int PreMoveTicks)
    {
        public static LocalMacroPlan From(LocalMacroCandidate candidate, int startTick)
        {
            var jumpStart = candidate.JumpStartTick == int.MaxValue ? int.MaxValue : candidate.JumpStartTick;
            return new LocalMacroPlan(
                HasPlan: true,
                candidate.Label,
                candidate.MoveDirection,
                startTick,
                Math.Max(MinimumCommitTicks, candidate.DurationTicks),
                jumpStart,
                candidate.JumpStartTick == int.MaxValue ? int.MaxValue : candidate.JumpStartTick + candidate.JumpHoldTicks,
                candidate.DropTicks,
                candidate.PreMoveDirection,
                candidate.PreMoveTicks);
        }
    }

    private readonly record struct LocalEnvironment(
        float Direction,
        bool GroundContact,
        bool HeadClear,
        bool GroundAhead,
        bool FootBlocked,
        bool WaistBlocked,
        bool ShouldJumpGap,
        bool ShouldJumpObstacle);

    private sealed record LocalTopology(
        IReadOnlyList<LocalFrontier> Frontiers,
        string Trace)
    {
        public static LocalTopology Empty { get; } = new([], string.Empty);
    }

    private readonly record struct LocalFrontier(
        float X,
        float Bottom,
        string Kind,
        float Score,
        bool IsEdge,
        bool IsPocket,
        bool IsTrap);

    private readonly record struct FrontierFit(
        float Score,
        bool IsTrap);

    private readonly record struct StochasticProbeEvaluation(
        bool Accepted,
        LocalMacroCandidate Candidate,
        float BestMetric,
        float FinalMetric,
        float Progress,
        float Score,
        bool Reached,
        bool EndsBlocked = false,
        float EndX = 0f,
        float EndBottom = 0f,
        int Candidates = 0,
        int SimTicks = 0,
        double ElapsedMilliseconds = 0d)
    {
        public static StochasticProbeEvaluation Empty(float startMetric) =>
            new(false, default, startMetric, startMetric, 0f, float.NegativeInfinity, false);

        public static StochasticProbeEvaluation Rejected(
            LocalMacroCandidate candidate,
            float startMetric,
            float bestMetric,
            float? finalMetric = null,
            float progress = 0f,
            float endX = 0f,
            float endBottom = 0f,
            bool endsBlocked = false) =>
            new(false, candidate, bestMetric, finalMetric ?? startMetric, progress, float.NegativeInfinity, false, endsBlocked, endX, endBottom);
    }

    private readonly record struct ContinuationEvaluation(
        bool Accepted,
        float Score,
        float FinalMetric,
        bool EndsBlocked,
        float MoveDirection)
    {
        public static ContinuationEvaluation Empty => new(false, float.NegativeInfinity, float.PositiveInfinity, false, 0f);
    }
}

public readonly record struct StochasticLocalMotionGoal(
    float X,
    float Bottom,
    float AcceptanceX,
    float AcceptanceBottom,
    string Label)
{
    public static StochasticLocalMotionGoal FromPoint(
        float x,
        float bottom,
        string label,
        float acceptanceX = 36f,
        float acceptanceBottom = 24f) =>
        new(x, bottom, acceptanceX, acceptanceBottom, label);
}

public readonly record struct StochasticLocalMotionTrace(
    bool Resolved,
    string Source,
    string MacroLabel,
    float StartMetric,
    float BestMetric,
    float FinalMetric,
    float Progress,
    float Score,
    int Candidates,
    int SimTicks,
    double ElapsedMilliseconds,
    bool Reached,
    string RejectedReason)
{
    public static StochasticLocalMotionTrace Empty =>
        new(false, string.Empty, string.Empty, 0f, 0f, 0f, 0f, 0f, 0, 0, 0d, false, string.Empty);
}
