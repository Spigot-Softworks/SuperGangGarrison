namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Stateful guard around primitive local driving. It prevents per-tick target chasing
/// from degenerating into left/right/jump churn when the target is nearby but awkward.
/// </summary>
public sealed class LocalMotionController
{
    private const int CommitTicks = 8;
    private const int BlockedEscapeTicks = 14;
    private const int JumpHoldTicks = 3;
    private const int DropHoldTicks = 8;
    private const int ProgressWindowTicks = 30;
    private const int SuppressionTicks = 90;
    private const int MaxStagnantWindows = 2;
    private const float ProgressWindowMinMovement = 10f;
    private const float TargetBucketSize = 48f;
    private const float VerticalBucketSize = 32f;
    private const float RetargetDistance = 96f;
    private const float ProbeMaxTargetDistance = 960f;
    private const float ProbeAcceptDistance = 48f;
    private const float ProbeMinimumProgress = 18f;
    private const float JumpableObstacleProbeDistance = 36f;
    private const int ProbeEntityId = -900_100;

    private readonly Dictionary<LocalMotionFailureKey, int> _suppressedTargets = [];

    private LocalMotionPlan _activePlan;
    private LocalMotionFailureKey _activeKey;
    private int _activeUntilTick;
    private int _activeStartedTick;
    private int _lastProgressTick;
    private int _stagnantWindows;
    private float _windowX;
    private float _windowY;
    private float _lastTargetX;
    private float _lastTargetY;

    public string LastTrace { get; private set; } = string.Empty;

    public void Reset()
    {
        _suppressedTargets.Clear();
        ClearActivePlan();
        LastTrace = string.Empty;
    }

    public bool TryResolveRecovery(
        SimulationWorld world,
        PlayerEntity self,
        DirectDriveTarget target,
        SteeringOutput baseSteering,
        int thinkTick,
        out SteeringOutput localSteering,
        out string trace)
    {
        DecaySuppression();
        LastTrace = string.Empty;
        localSteering = baseSteering;
        trace = string.Empty;

        var key = LocalMotionFailureKey.From(world.Level, self, target);
        if (_suppressedTargets.TryGetValue(key, out var suppressedTicks) && suppressedTicks > 0)
        {
            if (TryResolveProbePlan(world, self, target, baseSteering, thinkTick, "suppressed", out var suppressedPlan, out var suppressedTrace))
            {
                if (!TryCommitPlan(self, target, key, suppressedPlan, thinkTick, out var commitFailureTrace))
                {
                    trace = commitFailureTrace;
                    LastTrace = trace;
                    localSteering = baseSteering;
                    return false;
                }

                localSteering = ApplyPlan(baseSteering, suppressedPlan, thinkTick);
                trace = suppressedTrace;
                LastTrace = trace;
                return true;
            }

            if (TryResolveBlockedEscape(
                    world,
                    self,
                    target,
                    baseSteering,
                    key,
                    thinkTick,
                    includeJumpingPrimitive: true,
                    out localSteering,
                    out trace))
            {
                LastTrace = trace;
                return true;
            }

            trace = $"localMotion=suppressed label:{target.Label} ticks:{suppressedTicks}";
            LastTrace = trace;
            ClearActivePlan();
            return false;
        }

        if (TryContinueActivePlan(self, target, key, thinkTick, baseSteering, out localSteering, out trace))
        {
            LastTrace = trace;
            return true;
        }

        var primitiveResolved = PrimitiveDirectDrive.TryResolveRecovery(
                world,
                self,
                target,
                baseSteering,
                out var primitiveSteering,
                out var primitiveTrace);
        var primitiveNeedsObstaclePlan = IsBlockedPrimitiveMove(world, self, primitiveResolved, primitiveSteering, includeJumpingPrimitive: true);
        if (primitiveNeedsObstaclePlan
            && primitiveSteering.Jump
            && IsJumpableLowObstacleAhead(world, self, primitiveSteering.MoveDirection))
        {
            primitiveNeedsObstaclePlan = false;
        }

        if (ShouldRunProbe(self, target, primitiveResolved, primitiveSteering, primitiveNeedsObstaclePlan)
            && TryResolveProbePlan(world, self, target, baseSteering, thinkTick, "probe", out var probePlan, out var probeTrace))
        {
            if (!TryCommitPlan(self, target, key, probePlan, thinkTick, out var commitFailureTrace))
            {
                trace = commitFailureTrace;
                LastTrace = trace;
                localSteering = baseSteering;
                return false;
            }

            localSteering = ApplyPlan(baseSteering, probePlan, thinkTick);
            trace = probeTrace;
            LastTrace = trace;
            return true;
        }

        if (primitiveNeedsObstaclePlan)
        {
            var escapePlan = LocalMotionPlan.FromBlockedPrimitive(primitiveSteering, thinkTick);
            if (!TryCommitPlan(self, target, key, escapePlan, thinkTick, out var blockedCommitFailureTrace))
            {
                trace = blockedCommitFailureTrace;
                LastTrace = trace;
                localSteering = baseSteering;
                return false;
            }

            localSteering = ApplyPlan(baseSteering, escapePlan, thinkTick);
            trace = $"localMotion=blockedEscape {primitiveTrace} escape:{localSteering.MoveDirection:0} commit:{_activeUntilTick - thinkTick}";
            LastTrace = trace;
            return true;
        }

        if (!primitiveResolved)
        {
            ClearActivePlan();
            return false;
        }

        var plan = LocalMotionPlan.From(primitiveSteering, thinkTick);
        if (!TryCommitPlan(self, target, key, plan, thinkTick, out var primitiveCommitFailureTrace))
        {
            trace = primitiveCommitFailureTrace;
            LastTrace = trace;
            localSteering = baseSteering;
            return false;
        }

        localSteering = ApplyPlan(baseSteering, plan, thinkTick);
        trace = $"localMotion=start {primitiveTrace} commit:{_activeUntilTick - thinkTick}";
        LastTrace = trace;
        return true;
    }

    private static bool IsBlockedPrimitiveMove(
        SimulationWorld world,
        PlayerEntity self,
        bool primitiveResolved,
        SteeringOutput primitiveSteering,
        bool includeJumpingPrimitive) =>
        primitiveResolved
        && MathF.Abs(primitiveSteering.MoveDirection) > 0.01f
        && !primitiveSteering.DropDown
        && (includeJumpingPrimitive || !primitiveSteering.Jump)
        && PrimitiveDirectDrive.WouldMoveIntoObstacle(world, self, MathF.Sign(primitiveSteering.MoveDirection));

    private static bool IsJumpableLowObstacleAhead(SimulationWorld world, PlayerEntity player, float direction)
    {
        if (direction == 0f)
        {
            return false;
        }

        var blockedOffset = FindJumpableObstacleOffsetAhead(player, world.Level, MathF.Sign(direction));
        if (!blockedOffset.HasValue)
        {
            return false;
        }

        return CanClearObstacleAtLift(player, world.Level, direction, blockedOffset.Value, 16f)
            || CanClearObstacleAtLift(player, world.Level, direction, blockedOffset.Value, 32f)
            || CanClearObstacleAtLift(player, world.Level, direction, blockedOffset.Value, 48f)
            || CanClearObstacleAtLift(player, world.Level, direction, blockedOffset.Value, 64f);
    }

    private static float? FindJumpableObstacleOffsetAhead(PlayerEntity player, SimpleLevel level, float direction)
    {
        for (var offset = 4f; offset <= JumpableObstacleProbeDistance; offset += 4f)
        {
            if (!player.CanOccupy(level, player.Team, player.X + (direction * offset), player.Y))
            {
                return offset;
            }
        }

        return null;
    }

    private static bool CanClearObstacleAtLift(
        PlayerEntity player,
        SimpleLevel level,
        float direction,
        float blockedOffset,
        float lift)
    {
        var liftedY = player.Y - lift;
        var clearProbeOffset = MathF.Max(JumpableObstacleProbeDistance, blockedOffset + 8f);
        return player.CanOccupy(level, player.Team, player.X, liftedY)
            && player.CanOccupy(level, player.Team, player.X + (direction * blockedOffset), liftedY)
            && player.CanOccupy(level, player.Team, player.X + (direction * clearProbeOffset), liftedY);
    }

    private bool TryResolveBlockedEscape(
        SimulationWorld world,
        PlayerEntity self,
        DirectDriveTarget target,
        SteeringOutput baseSteering,
        LocalMotionFailureKey key,
        int thinkTick,
        bool includeJumpingPrimitive,
        out SteeringOutput localSteering,
        out string trace)
    {
        localSteering = baseSteering;
        trace = string.Empty;
        var primitiveResolved = PrimitiveDirectDrive.TryResolveRecovery(
            world,
            self,
            target,
            baseSteering,
            out var primitiveSteering,
            out var primitiveTrace);
        if (!IsBlockedPrimitiveMove(world, self, primitiveResolved, primitiveSteering, includeJumpingPrimitive))
        {
            return false;
        }

        var escapePlan = LocalMotionPlan.FromBlockedPrimitive(primitiveSteering, thinkTick);
        if (!TryCommitPlan(self, target, key, escapePlan, thinkTick, out var blockedCommitFailureTrace))
        {
            trace = blockedCommitFailureTrace;
            return false;
        }

        localSteering = ApplyPlan(baseSteering, escapePlan, thinkTick);
        trace = $"localMotion=blockedEscape {primitiveTrace} escape:{localSteering.MoveDirection:0} commit:{_activeUntilTick - thinkTick}";
        return true;
    }

    private bool TryContinueActivePlan(
        PlayerEntity self,
        DirectDriveTarget target,
        LocalMotionFailureKey key,
        int thinkTick,
        SteeringOutput baseSteering,
        out SteeringOutput steering,
        out string trace)
    {
        steering = baseSteering;
        trace = string.Empty;
        if (!_activePlan.HasPlan || thinkTick > _activeUntilTick)
        {
            return false;
        }

        if (!IsSameActiveTarget(_activeKey, key)
            || Distance(_lastTargetX, _lastTargetY, target.X, target.Y) > RetargetDistance)
        {
            ClearActivePlan();
            return false;
        }

        if (thinkTick - _lastProgressTick >= ProgressWindowTicks)
        {
            var moved = Distance(_windowX, _windowY, self.X, self.Y);
            _windowX = self.X;
            _windowY = self.Y;
            _lastProgressTick = thinkTick;
            if (moved < ProgressWindowMinMovement)
            {
                _stagnantWindows += 1;
                if (_stagnantWindows >= MaxStagnantWindows)
                {
                    _suppressedTargets[key] = SuppressionTicks;
                    trace = $"localMotion=failed label:{target.Label} moved:{moved:0.0} suppress:{SuppressionTicks}";
                    ClearActivePlan();
                    return false;
                }
            }
            else
            {
                _stagnantWindows = 0;
            }
        }

        steering = ApplyPlan(baseSteering, _activePlan, thinkTick);
        trace =
            $"localMotion=active label:{target.Label} age:{thinkTick - _activeStartedTick} " +
            $"remaining:{_activeUntilTick - thinkTick} move:{steering.MoveDirection:0} " +
            $"jump:{(steering.Jump ? 1 : 0)} drop:{(steering.DropDown ? 1 : 0)}";
        return true;
    }

    private bool TryCommitPlan(
        PlayerEntity self,
        DirectDriveTarget target,
        LocalMotionFailureKey key,
        LocalMotionPlan plan,
        int thinkTick,
        out string failureTrace)
    {
        failureTrace = string.Empty;
        var preserveProgressWindow = _activePlan.HasPlan
            && IsSameActiveTarget(_activeKey, key)
            && Distance(_lastTargetX, _lastTargetY, target.X, target.Y) <= RetargetDistance;
        if (preserveProgressWindow
            && thinkTick - _lastProgressTick >= ProgressWindowTicks)
        {
            var moved = Distance(_windowX, _windowY, self.X, self.Y);
            _windowX = self.X;
            _windowY = self.Y;
            _lastProgressTick = thinkTick;
            if (moved < ProgressWindowMinMovement)
            {
                _stagnantWindows += 1;
                if (_stagnantWindows >= MaxStagnantWindows)
                {
                    _suppressedTargets[key] = SuppressionTicks;
                    failureTrace = $"localMotion=failed label:{target.Label} moved:{moved:0.0} suppress:{SuppressionTicks}";
                    ClearActivePlan();
                    return false;
                }
            }
            else
            {
                _stagnantWindows = 0;
            }
        }

        _activePlan = plan;
        _activeKey = key;
        _activeStartedTick = thinkTick;
        _activeUntilTick = thinkTick + plan.CommitTicks;
        if (!preserveProgressWindow)
        {
            _lastProgressTick = thinkTick;
            _stagnantWindows = 0;
            _windowX = self.X;
            _windowY = self.Y;
        }

        _lastTargetX = target.X;
        _lastTargetY = target.Y;
        return true;
    }

    private static SteeringOutput ApplyPlan(SteeringOutput baseSteering, LocalMotionPlan plan, int thinkTick)
    {
        var steering = baseSteering;
        var age = Math.Max(0, thinkTick - plan.StartTick);
        steering.MoveDirection = plan.MoveDirection;
        steering.Jump = (age >= plan.JumpStartAge && age < plan.JumpEndAge) || baseSteering.Jump;
        steering.DropDown = age < plan.DropEndAge;
        steering.RequestRepath = false;
        return steering;
    }

    private static bool ShouldRunProbe(
        PlayerEntity self,
        DirectDriveTarget target,
        bool primitiveResolved,
        SteeringOutput primitiveSteering,
        bool primitiveBlocked)
    {
        if (Distance(self.X, self.Y, target.X, target.Y) > ProbeMaxTargetDistance)
        {
            return false;
        }

        if (!primitiveResolved)
        {
            return true;
        }

        if (primitiveBlocked)
        {
            return true;
        }

        if (target.Label.StartsWith("proof", StringComparison.Ordinal)
            || target.Label.Contains("Dropped", StringComparison.Ordinal)
            || target.Label.Contains("dropped", StringComparison.Ordinal))
        {
            return true;
        }

        var dx = target.X - self.X;
        var dy = target.Y - self.Y;
        return MathF.Abs(dx) <= 40f
            && MathF.Abs(dy) >= 36f
            && MathF.Abs(primitiveSteering.MoveDirection) <= 0.01f;
    }

    private static bool TryResolveProbePlan(
        SimulationWorld world,
        PlayerEntity self,
        DirectDriveTarget target,
        SteeringOutput baseSteering,
        int thinkTick,
        string reason,
        out LocalMotionPlan plan,
        out string trace)
    {
        plan = default;
        trace = string.Empty;
        var startDistance = Distance(self.X, self.Y, target.X, target.Y);
        if (startDistance > ProbeMaxTargetDistance)
        {
            return false;
        }

        var direction = MathF.Sign(target.X - self.X);
        var best = LocalMotionProbeResult.Empty;
        foreach (var candidate in EnumerateProbeCandidates(direction, target.Y - self.Y))
        {
            if (TryEvaluateProbeCandidate(world, self, target, candidate, startDistance, out var result)
                && result.Score > best.Score)
            {
                best = result;
            }
        }

        if (!best.Accepted)
        {
            return false;
        }

        plan = LocalMotionPlan.FromProbe(best.Candidate, thinkTick);
        var steering = ApplyPlan(baseSteering, plan, thinkTick);
        trace =
            $"localMotion={reason}Probe label:{target.Label} macro:{best.Candidate.Label} " +
            $"start:{startDistance:0.0} final:{best.FinalDistance:0.0} best:{best.BestDistance:0.0} " +
            $"progress:{best.Progress:0.0} score:{best.Score:0.0} move:{steering.MoveDirection:0} " +
            $"jump:{(steering.Jump ? 1 : 0)} drop:{(steering.DropDown ? 1 : 0)}";
        return true;
    }

    private static IEnumerable<LocalMotionProbeCandidate> EnumerateProbeCandidates(float targetDirection, float verticalDelta)
    {
        var primaryDirection = targetDirection == 0f ? 0f : targetDirection;
        var alternateDirection = primaryDirection == 0f ? 1f : -primaryDirection;
        foreach (var direction in new[] { primaryDirection, alternateDirection, 0f })
        {
            if (direction != 0f)
            {
                yield return new LocalMotionProbeCandidate($"run:{direction:0}", direction, 12, 99, 99, 0);
                yield return new LocalMotionProbeCandidate($"runLong:{direction:0}", direction, 20, 99, 99, 0);
                yield return new LocalMotionProbeCandidate($"jump:{direction:0}", direction, 20, 0, 4, 0);
                yield return new LocalMotionProbeCandidate($"runupJump:{direction:0}", direction, 26, 5, 4, 0);
            }
            else if (verticalDelta < -28f)
            {
                yield return new LocalMotionProbeCandidate("verticalJump", 0f, 18, 0, 4, 0);
            }
        }

        if (verticalDelta > 28f)
        {
            yield return new LocalMotionProbeCandidate($"drop:{primaryDirection:0}", primaryDirection, 18, 99, 99, 12);
            if (alternateDirection != primaryDirection)
            {
                yield return new LocalMotionProbeCandidate($"dropAlt:{alternateDirection:0}", alternateDirection, 18, 99, 99, 12);
            }
        }
    }

    private static bool TryEvaluateProbeCandidate(
        SimulationWorld world,
        PlayerEntity self,
        DirectDriveTarget target,
        LocalMotionProbeCandidate candidate,
        float startDistance,
        out LocalMotionProbeResult result)
    {
        result = LocalMotionProbeResult.Empty;
        var probe = new PlayerEntity(ProbeEntityId, self.ClassDefinition, "BotBrainLocalMotionProbe");
        var state = self.CapturePredictionState();
        probe.RestorePredictionState(in state);

        var previousInput = default(PlayerInputSnapshot);
        var bestDistance = startDistance;
        for (var tick = 0; tick < candidate.DurationTicks; tick += 1)
        {
            var input = CreateProbeInput(probe, target, candidate, tick);
            var jumpPressed = input.Up && !previousInput.Up;
            probe.Advance(input, jumpPressed, world.Level, self.Team, 1d / SimulationConfig.DefaultTicksPerSecond);
            previousInput = input;

            if (!probe.IsAlive
                || probe.Bottom < -64f
                || probe.Bottom > world.Level.Bounds.Height + 128f
                || !probe.CanOccupy(world.Level, self.Team, probe.X, probe.Y)
                || probe.IsInsideBlockingTeamGate(world.Level, self.Team))
            {
                return false;
            }

            var distance = Distance(probe.X, probe.Y, target.X, target.Y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
            }
        }

        var finalDistance = Distance(probe.X, probe.Y, target.X, target.Y);
        var progress = startDistance - bestDistance;
        var accepted = finalDistance <= ProbeAcceptDistance || progress >= MathF.Max(ProbeMinimumProgress, startDistance * 0.06f);
        if (!accepted)
        {
            return false;
        }

        var score = (progress * 5f)
            + ((startDistance - finalDistance) * 1.5f)
            - (candidate.DurationTicks * 0.35f)
            - (candidate.DropTicks > 0 ? 4f : 0f)
            + (probe.IsGrounded ? 8f : 0f)
            + (finalDistance <= ProbeAcceptDistance ? 100f : 0f);
        result = new LocalMotionProbeResult(candidate, finalDistance, bestDistance, progress, score, Accepted: true);
        return true;
    }

    private static PlayerInputSnapshot CreateProbeInput(
        PlayerEntity probe,
        DirectDriveTarget target,
        LocalMotionProbeCandidate candidate,
        int tick)
    {
        var aimDirection = candidate.MoveDirection == 0f
            ? MathF.Sign(target.X - probe.X)
            : candidate.MoveDirection;
        if (aimDirection == 0f)
        {
            aimDirection = probe.FacingDirectionX == 0f ? 1f : probe.FacingDirectionX;
        }

        return new PlayerInputSnapshot(
            Left: candidate.MoveDirection < 0f,
            Right: candidate.MoveDirection > 0f,
            Up: tick >= candidate.JumpStartTick && tick < candidate.JumpEndTick,
            Down: tick < candidate.DropTicks,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: probe.X + (aimDirection * 256f),
            AimWorldY: target.Y,
            DebugKill: false);
    }

    private void DecaySuppression()
    {
        if (_suppressedTargets.Count == 0)
        {
            return;
        }

        var updates = new List<(LocalMotionFailureKey Key, int Ticks)>(_suppressedTargets.Count);
        foreach (var (key, ticks) in _suppressedTargets)
        {
            var remaining = ticks - 1;
            if (remaining <= 0)
            {
                updates.Add((key, 0));
            }
            else
            {
                updates.Add((key, remaining));
            }
        }

        for (var index = 0; index < updates.Count; index += 1)
        {
            var update = updates[index];
            if (update.Ticks <= 0)
            {
                _suppressedTargets.Remove(update.Key);
            }
            else
            {
                _suppressedTargets[update.Key] = update.Ticks;
            }
        }
    }

    private void ClearActivePlan()
    {
        _activePlan = default;
        _activeKey = default;
        _activeUntilTick = 0;
        _activeStartedTick = 0;
        _lastProgressTick = 0;
        _stagnantWindows = 0;
        _windowX = 0f;
        _windowY = 0f;
        _lastTargetX = 0f;
        _lastTargetY = 0f;
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static bool IsSameActiveTarget(LocalMotionFailureKey left, LocalMotionFailureKey right) =>
        left.LevelName == right.LevelName
        && left.MapAreaIndex == right.MapAreaIndex
        && left.Team == right.Team
        && left.ClassId == right.ClassId
        && left.TargetKind == right.TargetKind
        && left.TargetXBucket == right.TargetXBucket
        && left.TargetYBucket == right.TargetYBucket
        && left.CarryingIntel == right.CarryingIntel;

    private readonly record struct LocalMotionPlan(
        bool HasPlan,
        float MoveDirection,
        int StartTick,
        int CommitTicks,
        int JumpStartAge,
        int JumpEndAge,
        int DropEndAge)
    {
        public static LocalMotionPlan From(SteeringOutput steering, int thinkTick) =>
            new(
                HasPlan: true,
                steering.MoveDirection,
                thinkTick,
                LocalMotionController.CommitTicks,
                steering.Jump ? 0 : int.MaxValue,
                steering.Jump ? JumpHoldTicks : int.MaxValue,
                steering.DropDown ? DropHoldTicks : 0);

        public static LocalMotionPlan FromProbe(LocalMotionProbeCandidate candidate, int thinkTick) =>
            new(
                HasPlan: true,
                candidate.MoveDirection,
                thinkTick,
                Math.Max(LocalMotionController.CommitTicks, candidate.DurationTicks),
                candidate.JumpStartTick,
                candidate.JumpEndTick,
                candidate.DropTicks);

        public static LocalMotionPlan FromBlockedPrimitive(SteeringOutput steering, int thinkTick)
        {
            float escapeDirection = -MathF.Sign(steering.MoveDirection);
            if (escapeDirection == 0f)
            {
                escapeDirection = 1;
            }

            return new(
                HasPlan: true,
                escapeDirection,
                thinkTick,
                LocalMotionController.BlockedEscapeTicks,
                int.MaxValue,
                int.MaxValue,
                0);
        }
    }

    private readonly record struct LocalMotionProbeCandidate(
        string Label,
        float MoveDirection,
        int DurationTicks,
        int JumpStartTick,
        int JumpEndTick,
        int DropTicks);

    private readonly record struct LocalMotionProbeResult(
        LocalMotionProbeCandidate Candidate,
        float FinalDistance,
        float BestDistance,
        float Progress,
        float Score,
        bool Accepted)
    {
        public static LocalMotionProbeResult Empty => new(default, float.PositiveInfinity, float.PositiveInfinity, 0f, float.NegativeInfinity, false);
    }

    private readonly record struct LocalMotionFailureKey(
        string LevelName,
        int MapAreaIndex,
        PlayerTeam Team,
        PlayerClass ClassId,
        DirectDriveTargetKind TargetKind,
        int StartXBucket,
        int StartYBucket,
        int TargetXBucket,
        int TargetYBucket,
        int HorizontalSpeedBucket,
        int VerticalSpeedBucket,
        bool Grounded,
        bool CarryingIntel)
    {
        public static LocalMotionFailureKey From(SimpleLevel level, PlayerEntity self, DirectDriveTarget target) =>
            new(
                level.Name,
                level.MapAreaIndex,
                self.Team,
                self.BotGraphClassId,
                target.Kind,
                Quantize(self.X, TargetBucketSize),
                Quantize(self.Bottom, VerticalBucketSize),
                Quantize(target.X, TargetBucketSize),
                Quantize(target.Y, VerticalBucketSize),
                Quantize(self.HorizontalSpeed, 80f),
                Quantize(self.VerticalSpeed, 80f),
                self.IsGrounded,
                self.IsCarryingIntel);

        private static int Quantize(float value, float bucketSize) =>
            (int)MathF.Round(value / bucketSize);
    }
}
