namespace OpenGarrison.Core.BotBrain;

public sealed class ObjectiveTapeExecutor
{
    private const float EntryRadius = 192f;
    private const float AdvanceRadius = 72f;
    private const float ObjectiveEndpointRadius = 520f;
    private const float ActionEndpointMissRadius = 260f;
    private const float MaxDivergence = 900f;
    private const int MaxDivergenceTicks = 180;
    private const int MaxCompletionTicks = 45;
    private const int MissedActionTapeSuppressionTicks = 9000;
    private const int ActionTapeStallWindowTicks = 75;
    private const float ActionTapeStallMoveThreshold = 12f;
    private const float MotionProofActionDivergenceRadius = 220f;
    private const float CertifiedMotionProofActionDivergenceRadius = 720f;
    private const int MotionProofActionDivergenceTicks = 30;
    private const float MotionProofGroundedEntryRadius = 12f;
    private const float MotionProofAirborneEntryRadius = 10f;

    private BotBrainObjectiveTapeEntry? _activeTape;
    private BotBrainObjectiveTapeSegment? _activeSegment;
    private string? _suppressedTapeName;
    private int _suppressedTapeUntilTick;
    private int _sampleIndex;
    private int _segmentStartThinkTick;
    private int _actionIndex;
    private int _actionElapsedTicks;
    private int _divergenceTicks;
    private int _completionTicks;
    private int _stallWindowStartTick;
    private float _stallWindowStartX;
    private float _stallWindowStartY;

    public string LastTrace { get; private set; } = string.Empty;

    public bool IsActive => _activeTape is not null && _activeSegment is not null;

    public void Reset()
    {
        _activeTape = null;
        _activeSegment = null;
        _sampleIndex = 0;
        _segmentStartThinkTick = 0;
        _actionIndex = 0;
        _actionElapsedTicks = 0;
        _divergenceTicks = 0;
        _completionTicks = 0;
        _stallWindowStartTick = 0;
        _stallWindowStartX = 0f;
        _stallWindowStartY = 0f;
        _suppressedTapeName = null;
        _suppressedTapeUntilTick = 0;
        LastTrace = string.Empty;
    }

    public bool TryResolve(
        BotBrainObjectiveTapeAsset? asset,
        PlayerEntity self,
        PlayerTeam team,
        (float X, float Y) currentGoal,
        int thinkTick,
        SteeringOutput baseSteering,
        out SteeringOutput tapeSteering)
    {
        tapeSteering = baseSteering;
        LastTrace = string.Empty;
        if (asset is null || !self.IsAlive)
        {
            Reset();
            return false;
        }

        if (_activeSegment is null || _activeTape is null)
        {
            if (!TryStartTape(asset, self, team, currentGoal, thinkTick))
            {
                return false;
            }
        }

        if (_activeSegment is null || _activeTape is null || _activeSegment.Samples.Count == 0)
        {
            Reset();
            return false;
        }

        if (_activeSegment.RequiresCarryingIntel != self.IsCarryingIntel)
        {
            LastTrace = $"objectiveTape=complete tape:{_activeTape.Name} reason:carry_state_changed";
            Reset();
            return false;
        }

        if (_activeSegment.Actions.Count > 0)
        {
            return TryResolveActionTape(asset, self, team, currentGoal, thinkTick, baseSteering, out tapeSteering);
        }

        AdvanceSample(self, thinkTick);
        var sample = _activeSegment.Samples[Math.Clamp(_sampleIndex, 0, _activeSegment.Samples.Count - 1)];
        var distance = Distance(self.X, self.Y, sample.X, sample.Y);
        if (distance > MaxDivergence)
        {
            _divergenceTicks += 1;
            if (_divergenceTicks >= MaxDivergenceTicks)
            {
                LastTrace = $"objectiveTape=abort reason:diverged tape:{_activeTape.Name} sample:{_sampleIndex} dist:{distance:0.0}";
                Reset();
                return false;
            }
        }
        else
        {
            _divergenceTicks = 0;
        }

        if (_sampleIndex >= _activeSegment.Samples.Count - 1)
        {
            _completionTicks += 1;
            if (_completionTicks >= MaxCompletionTicks)
            {
                LastTrace = $"objectiveTape=complete tape:{_activeTape.Name}";
                Reset();
                return false;
            }
        }

        var moveDirection = ResolveMoveDirection(self, sample);
        var sameClassTape = _activeTape.PlayerClass == self.ClassId;
        tapeSteering.MoveDirection = moveDirection;
        tapeSteering.Jump = (sameClassTape && sample.Jump) || ShouldCatchUpJump(self, sample);
        tapeSteering.DropDown = sameClassTape && sample.DropDown;
        LastTrace = $"objectiveTape=tape:{_activeTape.Name} carrying:{(sample.IsCarryingIntel ? 1 : 0)} sample:{_sampleIndex}/{_activeSegment.Samples.Count} dist:{distance:0.0} move:{moveDirection} jump:{(tapeSteering.Jump ? 1 : 0)}";
        return true;
    }

    private bool TryStartTape(
        BotBrainObjectiveTapeAsset asset,
        PlayerEntity self,
        PlayerTeam team,
        (float X, float Y) currentGoal,
        int thinkTick,
        string? skipTapeName = null)
    {
        BotBrainObjectiveTapeEntry? bestTape = null;
        BotBrainObjectiveTapeSegment? bestSegment = null;
        var bestStartIndex = 0;
        var bestScore = float.MaxValue;
        var seenTapes = 0;
        var seenTeamTapes = 0;
        var rejectedCarry = 0;
        var rejectedClass = 0;
        var rejectedEntry = 0;
        var rejectedEndpoint = 0;
        foreach (var tape in asset.Tapes)
        {
            seenTapes += 1;
            if (skipTapeName is not null
                && tape.Name.Equals(skipTapeName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_suppressedTapeName is not null
                && thinkTick < _suppressedTapeUntilTick
                && tape.Name.Equals(_suppressedTapeName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (tape.Team != team)
            {
                continue;
            }

            seenTeamTapes += 1;
            foreach (var segment in tape.Segments)
            {
                if (!SupportsClass(tape.PlayerClass, self.ClassId, segment))
                {
                    rejectedClass += 1;
                    continue;
                }

                if (segment.RequiresCarryingIntel != self.IsCarryingIntel || segment.Samples.Count < 2)
                {
                    rejectedCarry += 1;
                    continue;
                }

                var last = segment.Samples[^1];
                var isMotionProofActionTape = segment.Actions.Count > 0 && IsMotionProofTape(tape);
                var hasCertifiedPortal = HasCertifiedPortal(segment);
                var startIndex = segment.Actions.Count > 0
                    ? isMotionProofActionTape
                        ? FindNearestMotionProofActionEntrySampleIndex(segment, self, hasCertifiedPortal)
                        : 0
                    : FindNearestSampleIndex(segment, self.X, self.Y);
                if (startIndex < 0)
                {
                    rejectedEntry += 1;
                    continue;
                }

                var startSample = segment.Samples[startIndex];
                var entryX = hasCertifiedPortal ? segment.EntryX!.Value : startSample.X;
                var entryY = hasCertifiedPortal ? segment.EntryY!.Value : startSample.Y;
                if (segment.Actions.Count > 0 && startSample.IsGrounded != self.IsGrounded)
                {
                    continue;
                }

                var entryDistance = Distance(self.X, self.Y, entryX, entryY);
                var entryRadius = hasCertifiedPortal ? segment.EntryRadius!.Value : EntryRadius;
                if (entryDistance > entryRadius)
                {
                    rejectedEntry += 1;
                    continue;
                }

                var endpointDistance = Distance(currentGoal.X, currentGoal.Y, last.X, last.Y);
                if (segment.Actions.Count == 0 && endpointDistance > ObjectiveEndpointRadius)
                {
                    rejectedEndpoint += 1;
                    continue;
                }

                var actionTicks = segment.Actions.Count > 0
                    ? segment.Actions.Sum(static action => Math.Max(1, action.Ticks))
                    : 0;
                var score = entryDistance + (endpointDistance * 0.65f) + (startIndex * 0.25f) + (actionTicks * 0.1f);
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestTape = tape;
                bestSegment = segment;
                bestStartIndex = startIndex;
            }
        }

        if (bestTape is null || bestSegment is null)
        {
            if (seenTapes > 0)
            {
                LastTrace = $"objectiveTape=idle tapes:{seenTapes} team:{seenTeamTapes} rejectCarry:{rejectedCarry} rejectClass:{rejectedClass} rejectEntry:{rejectedEntry} rejectEndpoint:{rejectedEndpoint}";
            }

            return false;
        }

        _activeTape = bestTape;
        _activeSegment = bestSegment;
        _sampleIndex = bestStartIndex;
        _segmentStartThinkTick = bestSegment.Actions.Count > 0
            ? thinkTick - Math.Max(0, bestSegment.Samples[Math.Clamp(bestStartIndex, 0, bestSegment.Samples.Count - 1)].Tick)
            : thinkTick;
        _actionIndex = 0;
        _actionElapsedTicks = 0;
        _divergenceTicks = 0;
        _completionTicks = 0;
        _stallWindowStartTick = thinkTick;
        _stallWindowStartX = self.X;
        _stallWindowStartY = self.Y;
        return true;
    }

    private bool TryResolveActionTape(
        BotBrainObjectiveTapeAsset asset,
        PlayerEntity self,
        PlayerTeam team,
        (float X, float Y) currentGoal,
        int thinkTick,
        SteeringOutput baseSteering,
        out SteeringOutput tapeSteering)
    {
        tapeSteering = baseSteering;
        if (_activeSegment is null || _activeTape is null)
        {
            Reset();
            return false;
        }

        if (IsActionTapeStalled(self, thinkTick))
        {
            var stalledName = _activeTape.Name;
            _suppressedTapeName = stalledName;
            _suppressedTapeUntilTick = thinkTick + MissedActionTapeSuppressionTicks;
            ResetActiveTape();
            LastTrace = $"objectiveTape=abort reason:action_stalled tape:{stalledName}";
            return false;
        }

        var elapsedTicks = Math.Max(0, thinkTick - _segmentStartThinkTick);
        ResolveActionIndex(elapsedTicks);
        if (IsMotionProofTape(_activeTape) && IsMotionProofActionTapeDiverged(self, elapsedTicks))
        {
            _divergenceTicks += 1;
            if (_divergenceTicks >= MotionProofActionDivergenceTicks)
            {
                var divergedName = _activeTape.Name;
                _suppressedTapeName = divergedName;
                _suppressedTapeUntilTick = thinkTick + MissedActionTapeSuppressionTicks;
                ResetActiveTape();
                LastTrace = $"objectiveTape=abort reason:motionproof_diverged tape:{divergedName}";
                return false;
            }
        }
        else
        {
            _divergenceTicks = 0;
        }

        if (_actionIndex >= _activeSegment.Actions.Count)
        {
            var completedName = _activeTape.Name;
            var lastSample = _activeSegment.Samples[^1];
            var hasCertifiedPortal = HasCertifiedPortal(_activeSegment);
            var endpointX = hasCertifiedPortal ? _activeSegment.ExitX!.Value : lastSample.X;
            var endpointY = hasCertifiedPortal ? _activeSegment.ExitY!.Value : lastSample.Y;
            var endpointDistance = Distance(self.X, self.Y, endpointX, endpointY);
            var endpointRadius = hasCertifiedPortal ? _activeSegment.ExitRadius!.Value : ActionEndpointMissRadius;
            if ((hasCertifiedPortal || _activeTape.PlayerClass == PlayerClass.Heavy) && endpointDistance > endpointRadius)
            {
                _suppressedTapeName = completedName;
                _suppressedTapeUntilTick = thinkTick + MissedActionTapeSuppressionTicks;
                ResetActiveTape();
                LastTrace = $"objectiveTape=abort reason:action_endpoint_missed tape:{completedName} dist:{endpointDistance:0.0}";
                return false;
            }

            Reset();
            if (TryStartTape(asset, self, team, currentGoal, thinkTick, completedName)
                && _activeSegment is not null
                && _activeSegment.Actions.Count > 0)
            {
                return TryResolveActionTape(asset, self, team, currentGoal, thinkTick, baseSteering, out tapeSteering);
            }

            LastTrace = $"objectiveTape=complete tape:{completedName}";
            return false;
        }

        var action = _activeSegment.Actions[_actionIndex];
        var moveDirection = action.Direction < 0 ? -1 : action.Direction > 0 ? 1 : 0;
        var aimDirection = moveDirection == 0 ? MathF.Sign(self.FacingDirectionX) : moveDirection;
        if (aimDirection == 0)
        {
            aimDirection = 1;
        }

        tapeSteering.MoveDirection = moveDirection;
        tapeSteering.Jump = action.Kind.Equals("Jump", StringComparison.OrdinalIgnoreCase)
            && _actionElapsedTicks == 0;
        tapeSteering.DropDown = action.Kind.Equals("Drop", StringComparison.OrdinalIgnoreCase);
        tapeSteering.HasAimOverride = true;
        tapeSteering.AimOverrideX = self.X + (aimDirection * 160f);
        tapeSteering.AimOverrideY = self.Y;
        LastTrace = $"objectiveTape=tape:{_activeTape.Name} action:{_actionIndex}/{_activeSegment.Actions.Count} kind:{action.Kind} tick:{_actionElapsedTicks}/{action.Ticks} move:{moveDirection} jump:{(tapeSteering.Jump ? 1 : 0)}";
        return true;
    }

    private bool IsMotionProofActionTapeDiverged(PlayerEntity self, int elapsedTicks)
    {
        if (_activeSegment is null || _activeSegment.Samples.Count == 0)
        {
            return false;
        }

        var sample = FindSampleAtElapsedTick(_activeSegment, elapsedTicks);
        var divergenceRadius = HasCertifiedPortal(_activeSegment)
            ? CertifiedMotionProofActionDivergenceRadius
            : MotionProofActionDivergenceRadius;
        return Distance(self.X, self.Y, sample.X, sample.Y) > divergenceRadius;
    }

    private bool IsActionTapeStalled(PlayerEntity self, int thinkTick)
    {
        if (_stallWindowStartTick <= 0)
        {
            _stallWindowStartTick = thinkTick;
            _stallWindowStartX = self.X;
            _stallWindowStartY = self.Y;
            return false;
        }

        if (thinkTick - _stallWindowStartTick < ActionTapeStallWindowTicks)
        {
            return false;
        }

        var moved = Distance(self.X, self.Y, _stallWindowStartX, _stallWindowStartY);
        _stallWindowStartTick = thinkTick;
        _stallWindowStartX = self.X;
        _stallWindowStartY = self.Y;
        return moved < ActionTapeStallMoveThreshold;
    }

    private void ResetActiveTape()
    {
        _activeTape = null;
        _activeSegment = null;
        _sampleIndex = 0;
        _segmentStartThinkTick = 0;
        _actionIndex = 0;
        _actionElapsedTicks = 0;
        _divergenceTicks = 0;
        _completionTicks = 0;
        _stallWindowStartTick = 0;
        _stallWindowStartX = 0f;
        _stallWindowStartY = 0f;
    }

    private void ResolveActionIndex(int elapsedTicks)
    {
        if (_activeSegment is null)
        {
            return;
        }

        var remaining = elapsedTicks;
        for (var index = 0; index < _activeSegment.Actions.Count; index += 1)
        {
            var actionTicks = Math.Max(1, _activeSegment.Actions[index].Ticks);
            if (remaining < actionTicks)
            {
                _actionIndex = index;
                _actionElapsedTicks = remaining;
                return;
            }

            remaining -= actionTicks;
        }

        _actionIndex = _activeSegment.Actions.Count;
        _actionElapsedTicks = 0;
    }

    private static int FindNearestSampleIndex(BotBrainObjectiveTapeSegment segment, float x, float y)
    {
        var bestIndex = 0;
        var bestDistance = float.PositiveInfinity;
        for (var i = 0; i < segment.Samples.Count; i += 1)
        {
            var sample = segment.Samples[i];
            var distance = DistanceSquared(x, y, sample.X, sample.Y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestIndex = i;
        }

        return bestIndex;
    }

    private static int FindNearestMotionProofActionEntrySampleIndex(
        BotBrainObjectiveTapeSegment segment,
        PlayerEntity self,
        bool hasCertifiedPortal)
    {
        var bestIndex = -1;
        var bestDistance = float.PositiveInfinity;
        var entryRadius = hasCertifiedPortal
            ? segment.EntryRadius!.Value
            : self.IsGrounded ? MotionProofGroundedEntryRadius : MotionProofAirborneEntryRadius;
        for (var i = 0; i < segment.Samples.Count; i += 1)
        {
            var sample = segment.Samples[i];
            if (sample.IsGrounded != self.IsGrounded)
            {
                continue;
            }

            var distance = Distance(self.X, self.Y, sample.X, sample.Y);
            if (distance > entryRadius || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestIndex = i;
        }

        return bestIndex;
    }

    private void AdvanceSample(PlayerEntity self, int thinkTick)
    {
        if (_activeSegment is null)
        {
            return;
        }

        var isMotionProofSampleLane = _activeSegment.Actions.Count == 0 && IsMotionProofTape(_activeTape);
        if (!isMotionProofSampleLane)
        {
            var elapsedTicks = Math.Max(0, thinkTick - _segmentStartThinkTick);
            while (_sampleIndex + 1 < _activeSegment.Samples.Count
                && _activeSegment.Samples[_sampleIndex + 1].Tick <= elapsedTicks)
            {
                _sampleIndex += 1;
            }
        }

        while (_sampleIndex + 1 < _activeSegment.Samples.Count)
        {
            var current = _activeSegment.Samples[_sampleIndex];
            if (Distance(self.X, self.Y, current.X, current.Y) > AdvanceRadius)
            {
                break;
            }

            _sampleIndex += 1;
        }

        while (_sampleIndex + 1 < _activeSegment.Samples.Count)
        {
            var next = _activeSegment.Samples[_sampleIndex + 1];
            if (Distance(self.X, self.Y, next.X, next.Y) <= AdvanceRadius)
            {
                _sampleIndex += 1;
                continue;
            }

            break;
        }
    }

    private static int ResolveMoveDirection(PlayerEntity self, BotBrainObjectiveTapeSample sample)
    {
        var dx = sample.X - self.X;
        if (MathF.Abs(dx) > 24f)
        {
            return dx > 0f ? 1 : -1;
        }

        if (MathF.Abs(sample.MoveDirection) > 0.1f)
        {
            return sample.MoveDirection > 0f ? 1 : -1;
        }

        return MathF.Abs(dx) <= 18f ? 0 : dx > 0f ? 1 : -1;
    }

    private static bool ShouldCatchUpJump(PlayerEntity self, BotBrainObjectiveTapeSample sample) =>
        self.IsGrounded && sample.Y < self.Y - 28f;

    private static bool SupportsClass(
        PlayerClass tapeClass,
        PlayerClass selfClass,
        BotBrainObjectiveTapeSegment segment) =>
        tapeClass == selfClass
        || (segment.Actions.Count == 0
            && !(tapeClass == PlayerClass.Heavy && selfClass == PlayerClass.Scout));

    private static BotBrainObjectiveTapeSample FindSampleAtElapsedTick(BotBrainObjectiveTapeSegment segment, int elapsedTicks)
    {
        var best = segment.Samples[0];
        for (var i = 1; i < segment.Samples.Count; i += 1)
        {
            if (segment.Samples[i].Tick > elapsedTicks)
            {
                break;
            }

            best = segment.Samples[i];
        }

        return best;
    }

    private static bool IsMotionProofTape(BotBrainObjectiveTapeEntry? tape) =>
        tape is not null && tape.Name.StartsWith("MotionProof.", StringComparison.OrdinalIgnoreCase);

    private static bool HasCertifiedPortal(BotBrainObjectiveTapeSegment segment) =>
        segment.EntryX.HasValue
        && segment.EntryY.HasValue
        && segment.EntryRadius.HasValue
        && segment.ExitX.HasValue
        && segment.ExitY.HasValue
        && segment.ExitRadius.HasValue;

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static float DistanceSquared(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return (dx * dx) + (dy * dy);
    }
}
