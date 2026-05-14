namespace OpenGarrison.Core.BotBrain;

public sealed class ObjectiveTapeExecutor
{
    private const float EntryRadius = 192f;
    private const float AdvanceRadius = 72f;
    private const float MotionProofSampleAdvanceRadius = 160f;
    private const float ObjectiveEndpointRadius = 520f;
    private const float ActionEndpointMissRadius = 260f;
    private const float MaxDivergence = 900f;
    private const float CertifiedMotionProofSampleDivergenceRadius = 420f;
    private const int MaxDivergenceTicks = 180;
    private const int MaxCompletionTicks = 45;
    private const int MissedActionTapeSuppressionTicks = 9000;
    private const int CompletedActionTapeSuppressionTicks = 1800;
    private const int MotionProofSampleNoProgressTicks = 240;
    private const int MotionProofSampleDurationSlackTicks = 360;
    private const float MotionProofSampleProgressDistance = 96f;
    private const int MotionProofActionLaneLookaheadTicks = 18;
    private const int MotionProofActionLaneJumpHintTicks = 3;
    private const int ActionTapeStallWindowTicks = 75;
    private const float ActionTapeStallMoveThreshold = 12f;
    private const float MotionProofActionDivergenceRadius = 220f;
    private const float CertifiedMotionProofActionDivergenceRadius = 720f;
    private const int MotionProofActionDivergenceTicks = 30;
    private const int CertifiedMotionProofActionDivergenceTicks = 240;
    private const float MotionProofGroundedEntryRadius = 12f;
    private const float MotionProofAirborneEntryRadius = 10f;

    private BotBrainObjectiveTapeEntry? _activeTape;
    private BotBrainObjectiveTapeSegment? _activeSegment;
    private readonly Dictionary<string, int> _suppressedTapes = new(StringComparer.OrdinalIgnoreCase);
    private int _sampleIndex;
    private int _segmentStartThinkTick;
    private int _actionIndex;
    private int _actionElapsedTicks;
    private int _divergenceTicks;
    private int _completionTicks;
    private int _stallWindowStartTick;
    private float _stallWindowStartX;
    private float _stallWindowStartY;
    private int _lastSampleProgressIndex;
    private int _lastSampleProgressThinkTick;
    private float _bestSampleLaneExitDistance;

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
        _lastSampleProgressIndex = 0;
        _lastSampleProgressThinkTick = 0;
        _bestSampleLaneExitDistance = float.PositiveInfinity;
        _suppressedTapes.Clear();
        LastTrace = string.Empty;
    }

    public bool TryResolve(
        BotBrainObjectiveTapeAsset? asset,
        NavGraph? graph,
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
            if (!TryStartTape(asset, graph, self, team, currentGoal, thinkTick))
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

        if (_activeSegment.Actions.Count > 0
            && !ShouldExecuteActionTapeAsSampleLane(_activeTape, _activeSegment))
        {
            return TryResolveActionTape(asset, graph, self, team, currentGoal, thinkTick, baseSteering, out tapeSteering);
        }

        var actionDerivedSampleLane = ShouldExecuteActionTapeAsSampleLane(_activeTape, _activeSegment);
        var isMotionProofSampleLane = IsMotionProofSampleLane(_activeTape, _activeSegment);
        var hasCertifiedPortal = HasCertifiedPortal(_activeSegment);
        AdvanceSample(self, thinkTick);
        var sample = _activeSegment.Samples[Math.Clamp(_sampleIndex, 0, _activeSegment.Samples.Count - 1)];
        var distance = Distance(self.X, self.Y, sample.X, sample.Y);
        var divergenceRadius = actionDerivedSampleLane && hasCertifiedPortal
            ? CertifiedMotionProofActionDivergenceRadius
            : isMotionProofSampleLane && hasCertifiedPortal
            ? CertifiedMotionProofSampleDivergenceRadius
            : MaxDivergence;
        if (distance > divergenceRadius)
        {
            _divergenceTicks += 1;
            if (_divergenceTicks >= MaxDivergenceTicks)
            {
                var divergedName = _activeTape.Name;
                SuppressTape(divergedName, thinkTick, MissedActionTapeSuppressionTicks);
                ResetActiveTape();
                LastTrace = $"objectiveTape=abort reason:diverged tape:{divergedName} sample:{_sampleIndex} dist:{distance:0.0}";
                return false;
            }
        }
        else
        {
            _divergenceTicks = 0;
        }

        if (isMotionProofSampleLane
            && !actionDerivedSampleLane
            && hasCertifiedPortal
            && IsMotionProofSampleLaneNotProgressing(self, thinkTick, out var noProgressReason))
        {
            var stalledName = _activeTape.Name;
            SuppressTape(stalledName, thinkTick, MissedActionTapeSuppressionTicks);
            ResetActiveTape();
            LastTrace = $"objectiveTape=abort reason:{noProgressReason} tape:{stalledName}";
            return false;
        }

        if (_sampleIndex >= _activeSegment.Samples.Count - 1)
        {
            var lastSample = _activeSegment.Samples[^1];
            var endpointX = hasCertifiedPortal ? _activeSegment.ExitX!.Value : lastSample.X;
            var endpointY = hasCertifiedPortal ? _activeSegment.ExitY!.Value : lastSample.Y;
            var endpointRadius = hasCertifiedPortal ? _activeSegment.ExitRadius!.Value : ActionEndpointMissRadius;
            var endpointDistance = Distance(self.X, self.Y, endpointX, endpointY);
            if (!hasCertifiedPortal || endpointDistance <= endpointRadius)
            {
                _completionTicks += 1;
                if (_completionTicks >= MaxCompletionTicks)
                {
                    LastTrace = $"objectiveTape=complete tape:{_activeTape.Name}";
                    Reset();
                    return false;
                }
            }
            else
            {
                _completionTicks = 0;
            }
        }

        var moveDirection = ResolveMoveDirection(self, sample);
        var sameClassTape = _activeTape.PlayerClass == self.ClassId;
        var replaySampleButtons = sameClassTape && !isMotionProofSampleLane;
        var actionHintTrace = string.Empty;
        tapeSteering.MoveDirection = moveDirection;
        tapeSteering.Jump = (replaySampleButtons && sample.Jump)
            || (!actionDerivedSampleLane && ShouldCatchUpJump(self, sample));
        tapeSteering.DropDown = replaySampleButtons && sample.DropDown;
        if (actionDerivedSampleLane)
        {
            ApplyMotionProofActionLaneHint(self, sample, thinkTick, ref tapeSteering, ref actionHintTrace);
            moveDirection = (int)tapeSteering.MoveDirection;
        }

        LastTrace = $"objectiveTape=tape:{_activeTape.Name} carrying:{(sample.IsCarryingIntel ? 1 : 0)} sample:{_sampleIndex}/{_activeSegment.Samples.Count} dist:{distance:0.0} move:{moveDirection} jump:{(tapeSteering.Jump ? 1 : 0)}{actionHintTrace}";
        return true;
    }

    private bool TryStartTape(
        BotBrainObjectiveTapeAsset asset,
        NavGraph? graph,
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

            if (_suppressedTapes.TryGetValue(tape.Name, out var suppressedUntilTick))
            {
                if (thinkTick < suppressedUntilTick)
                {
                    continue;
                }

                _suppressedTapes.Remove(tape.Name);
            }

            if (tape.Team != team)
            {
                continue;
            }

            seenTeamTapes += 1;
            foreach (var segment in tape.Segments)
            {
                if (!SupportsClass(tape, self.ClassId, segment))
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
                var executeAsSampleLane = ShouldExecuteActionTapeAsSampleLane(tape, segment);
                if (isMotionProofActionTape && ShouldDisableMotionProofActionReplay() && !executeAsSampleLane)
                {
                    rejectedClass += 1;
                    continue;
                }

                var hasCertifiedPortal = HasCertifiedPortal(segment);
                var startIndex = segment.Actions.Count > 0 && !executeAsSampleLane
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
                if (segment.Actions.Count > 0
                    && !executeAsSampleLane
                    && !hasCertifiedPortal
                    && startSample.IsGrounded != self.IsGrounded)
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
                if ((segment.Actions.Count == 0 || executeAsSampleLane) && endpointDistance > ObjectiveEndpointRadius)
                {
                    rejectedEndpoint += 1;
                    continue;
                }

                if (!HasPostTapeRouteAuthority(graph, tape, segment, self, team, currentGoal, last, hasCertifiedPortal))
                {
                    rejectedEndpoint += 1;
                    continue;
                }

                var actionTicks = segment.Actions.Count > 0 && !executeAsSampleLane
                    ? segment.Actions.Sum(static action => Math.Max(1, action.Ticks))
                    : 0;
                var score = entryDistance
                    + (endpointDistance * 0.65f)
                    + (startIndex * 0.25f)
                    + (actionTicks * 0.1f)
                    + GetTapeSelectionPenalty(tape, self.ClassId, segment);
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
        _segmentStartThinkTick = bestSegment.Actions.Count > 0 && !ShouldExecuteActionTapeAsSampleLane(bestTape, bestSegment)
            ? thinkTick - Math.Max(0, bestSegment.Samples[Math.Clamp(bestStartIndex, 0, bestSegment.Samples.Count - 1)].Tick)
            : thinkTick;
        _actionIndex = 0;
        _actionElapsedTicks = 0;
        _divergenceTicks = 0;
        _completionTicks = 0;
        _stallWindowStartTick = thinkTick;
        _stallWindowStartX = self.X;
        _stallWindowStartY = self.Y;
        _lastSampleProgressIndex = bestStartIndex;
        _lastSampleProgressThinkTick = thinkTick;
        _bestSampleLaneExitDistance = HasCertifiedPortal(bestSegment)
            ? Distance(self.X, self.Y, bestSegment.ExitX!.Value, bestSegment.ExitY!.Value)
            : float.PositiveInfinity;
        return true;
    }

    private bool TryResolveActionTape(
        BotBrainObjectiveTapeAsset asset,
        NavGraph? graph,
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

        var isCertifiedMotionProofActionTape = IsMotionProofTape(_activeTape) && HasCertifiedPortal(_activeSegment);
        if (!isCertifiedMotionProofActionTape && IsActionTapeStalled(self, thinkTick))
        {
            var stalledName = _activeTape.Name;
            SuppressTape(stalledName, thinkTick, MissedActionTapeSuppressionTicks);
            ResetActiveTape();
            LastTrace = $"objectiveTape=abort reason:action_stalled tape:{stalledName}";
            return false;
        }

        var elapsedTicks = Math.Max(0, thinkTick - _segmentStartThinkTick);
        ResolveActionIndex(elapsedTicks);
        if (IsMotionProofTape(_activeTape) && IsMotionProofActionTapeDiverged(self, elapsedTicks))
        {
            _divergenceTicks += 1;
            var maxDivergenceTicks = isCertifiedMotionProofActionTape
                ? CertifiedMotionProofActionDivergenceTicks
                : MotionProofActionDivergenceTicks;
            if (_divergenceTicks >= maxDivergenceTicks)
            {
                var divergedName = _activeTape.Name;
                SuppressTape(divergedName, thinkTick, MissedActionTapeSuppressionTicks);
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
                SuppressTape(completedName, thinkTick, MissedActionTapeSuppressionTicks);
                ResetActiveTape();
                LastTrace = $"objectiveTape=abort reason:action_endpoint_missed tape:{completedName} dist:{endpointDistance:0.0}";
                return false;
            }

            SuppressTape(completedName, thinkTick, CompletedActionTapeSuppressionTicks);
            ResetActiveTape();
            if (TryStartTape(asset, graph, self, team, currentGoal, thinkTick, completedName)
                && _activeSegment is not null
                && _activeSegment.Actions.Count > 0)
            {
                return TryResolveActionTape(asset, graph, self, team, currentGoal, thinkTick, baseSteering, out tapeSteering);
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

    private bool IsMotionProofSampleLaneNotProgressing(PlayerEntity self, int thinkTick, out string reason)
    {
        reason = string.Empty;
        if (_activeSegment is null || !HasCertifiedPortal(_activeSegment))
        {
            return false;
        }

        var exitDistance = Distance(self.X, self.Y, _activeSegment.ExitX!.Value, _activeSegment.ExitY!.Value);
        if (_sampleIndex > _lastSampleProgressIndex
            || exitDistance <= _bestSampleLaneExitDistance - MotionProofSampleProgressDistance)
        {
            _lastSampleProgressIndex = _sampleIndex;
            _lastSampleProgressThinkTick = thinkTick;
            _bestSampleLaneExitDistance = MathF.Min(_bestSampleLaneExitDistance, exitDistance);
            return false;
        }

        _bestSampleLaneExitDistance = MathF.Min(_bestSampleLaneExitDistance, exitDistance);
        if (thinkTick - _lastSampleProgressThinkTick >= MotionProofSampleNoProgressTicks
            && exitDistance > _activeSegment.ExitRadius!.Value)
        {
            reason = "motionproof_sample_no_progress";
            return true;
        }

        var firstTick = _activeSegment.Samples[0].Tick;
        var lastTick = _activeSegment.Samples[^1].Tick;
        var expectedDuration = Math.Max(1, lastTick - firstTick);
        var elapsed = Math.Max(0, thinkTick - _segmentStartThinkTick);
        if (elapsed > (expectedDuration * 3) + MotionProofSampleDurationSlackTicks
            && exitDistance > _activeSegment.ExitRadius!.Value)
        {
            reason = "motionproof_sample_over_budget";
            return true;
        }

        return false;
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
        _lastSampleProgressIndex = 0;
        _lastSampleProgressThinkTick = 0;
        _bestSampleLaneExitDistance = float.PositiveInfinity;
    }

    private void SuppressTape(string tapeName, int thinkTick, int ticks)
    {
        var untilTick = thinkTick + ticks;
        if (_suppressedTapes.TryGetValue(tapeName, out var existingUntilTick)
            && existingUntilTick >= untilTick)
        {
            return;
        }

        _suppressedTapes[tapeName] = untilTick;
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
        if (hasCertifiedPortal)
        {
            var first = segment.Samples[0];
            if (Distance(self.X, self.Y, segment.EntryX!.Value, segment.EntryY!.Value) <= segment.EntryRadius!.Value
                || Distance(self.X, self.Y, first.X, first.Y) <= segment.EntryRadius!.Value)
            {
                return 0;
            }
        }

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

        var isMotionProofSampleLane = IsMotionProofSampleLane(_activeTape, _activeSegment);
        var actionDerivedSampleLane = _activeTape is not null
            && ShouldExecuteActionTapeAsSampleLane(_activeTape, _activeSegment);
        var advanceRadius = isMotionProofSampleLane ? MotionProofSampleAdvanceRadius : AdvanceRadius;
        if (!isMotionProofSampleLane || actionDerivedSampleLane)
        {
            var elapsedTicks = Math.Max(0, thinkTick - _segmentStartThinkTick);
            var sampleTicks = actionDerivedSampleLane
                ? elapsedTicks + MotionProofActionLaneLookaheadTicks
                : elapsedTicks;
            while (_sampleIndex + 1 < _activeSegment.Samples.Count
                && _activeSegment.Samples[_sampleIndex + 1].Tick <= sampleTicks)
            {
                _sampleIndex += 1;
            }
        }

        while (_sampleIndex + 1 < _activeSegment.Samples.Count)
        {
            var current = _activeSegment.Samples[_sampleIndex];
            if (Distance(self.X, self.Y, current.X, current.Y) > advanceRadius)
            {
                break;
            }

            _sampleIndex += 1;
        }

        while (_sampleIndex + 1 < _activeSegment.Samples.Count)
        {
            var next = _activeSegment.Samples[_sampleIndex + 1];
            if (Distance(self.X, self.Y, next.X, next.Y) <= advanceRadius)
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

    private void ApplyMotionProofActionLaneHint(
        PlayerEntity self,
        BotBrainObjectiveTapeSample sample,
        int thinkTick,
        ref SteeringOutput tapeSteering,
        ref string actionHintTrace)
    {
        if (_activeSegment is null || _activeSegment.Actions.Count == 0)
        {
            return;
        }

        var elapsedTicks = Math.Max(0, thinkTick - _segmentStartThinkTick);
        ResolveActionIndex(elapsedTicks);
        if (_actionIndex >= _activeSegment.Actions.Count)
        {
            actionHintTrace = " action:complete";
            return;
        }

        var action = _activeSegment.Actions[_actionIndex];
        var actionMoveDirection = action.Direction < 0 ? -1 : action.Direction > 0 ? 1 : 0;
        if (actionMoveDirection != 0 && ShouldUseMotionProofActionMoveHint(self, sample, action))
        {
            tapeSteering.MoveDirection = actionMoveDirection;
        }

        if (ShouldUseMotionProofActionJumpHint(self, sample, action))
        {
            tapeSteering.Jump = true;
        }

        if (action.Kind.Equals("Drop", StringComparison.OrdinalIgnoreCase))
        {
            tapeSteering.DropDown = true;
        }

        var aimDirection = tapeSteering.MoveDirection == 0f
            ? MathF.Sign(self.FacingDirectionX)
            : MathF.Sign(tapeSteering.MoveDirection);
        if (aimDirection == 0)
        {
            aimDirection = 1;
        }

        tapeSteering.HasAimOverride = true;
        tapeSteering.AimOverrideX = self.X + (aimDirection * 160f);
        tapeSteering.AimOverrideY = self.Y;
        actionHintTrace = $" action:{_actionIndex}/{_activeSegment.Actions.Count} kind:{action.Kind} tick:{_actionElapsedTicks}/{action.Ticks}";
    }

    private static bool ShouldUseMotionProofActionMoveHint(
        PlayerEntity self,
        BotBrainObjectiveTapeSample sample,
        BotBrainObjectiveTapeAction action)
    {
        var dx = MathF.Abs(sample.X - self.X);
        return action.Kind.Equals("Jump", StringComparison.OrdinalIgnoreCase)
            || action.Kind.Equals("Drop", StringComparison.OrdinalIgnoreCase)
            || dx <= 32f;
    }

    private bool ShouldUseMotionProofActionJumpHint(
        PlayerEntity self,
        BotBrainObjectiveTapeSample sample,
        BotBrainObjectiveTapeAction action)
    {
        if (!action.Kind.Equals("Jump", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!self.IsGrounded && self.RemainingAirJumps <= 0)
        {
            return false;
        }

        if (_actionElapsedTicks <= MotionProofActionLaneJumpHintTicks)
        {
            return true;
        }

        return self.IsGrounded
            && sample.Y < self.Y - 20f
            && _actionElapsedTicks <= Math.Max(MotionProofActionLaneJumpHintTicks, action.Ticks / 3);
    }

    private static bool SupportsClass(
        BotBrainObjectiveTapeEntry tape,
        PlayerClass selfClass,
        BotBrainObjectiveTapeSegment segment) =>
        tape.PlayerClass == selfClass
        || (IsHeavyActionTapeCrossClassEnabled()
            && tape.PlayerClass == PlayerClass.Heavy
            && selfClass != PlayerClass.Scout
            && segment.RequiresCarryingIntel
            && segment.Actions.Count > 0
            && IsReturnStageTape(tape))
        || (IsValidatedActionTapeCrossClassEnabled()
            && tape.PlayerClass != PlayerClass.Heavy
            && selfClass != PlayerClass.Scout
            && segment.RequiresCarryingIntel
            && segment.Actions.Count > 0
            && IsMotionProofTape(tape)
            && IsReturnActionTape(tape))
        || (segment.Actions.Count == 0
            && !(tape.PlayerClass == PlayerClass.Heavy && selfClass == PlayerClass.Scout));

    private static float GetTapeSelectionPenalty(
        BotBrainObjectiveTapeEntry tape,
        PlayerClass selfClass,
        BotBrainObjectiveTapeSegment segment)
    {
        if (tape.PlayerClass == selfClass)
        {
            return 0f;
        }

        if (segment.Actions.Count == 0)
        {
            return 160f;
        }

        if (tape.PlayerClass == PlayerClass.Heavy && IsReturnStageTape(tape))
        {
            return tape.Name.Contains(".Mirrored", StringComparison.OrdinalIgnoreCase)
                ? 520f
                : 260f;
        }

        if (IsValidatedActionTapeCrossClassEnabled()
            && IsMotionProofTape(tape)
            && IsReturnActionTape(tape)
            && segment.RequiresCarryingIntel)
        {
            return tape.Name.Contains(".Mirrored", StringComparison.OrdinalIgnoreCase)
                ? 900f
                : 720f;
        }

        return 1200f;
    }

    private static bool HasPostTapeRouteAuthority(
        NavGraph? graph,
        BotBrainObjectiveTapeEntry tape,
        BotBrainObjectiveTapeSegment segment,
        PlayerEntity self,
        PlayerTeam team,
        (float X, float Y) currentGoal,
        BotBrainObjectiveTapeSample lastSample,
        bool hasCertifiedPortal)
    {
        if (segment.Actions.Count == 0 || tape.PlayerClass == self.ClassId)
        {
            return true;
        }

        var endpointX = hasCertifiedPortal ? segment.ExitX!.Value : lastSample.X;
        var endpointY = hasCertifiedPortal ? segment.ExitY!.Value : lastSample.Y;
        if (Distance(endpointX, endpointY, currentGoal.X, currentGoal.Y) <= ActionEndpointMissRadius)
        {
            return true;
        }

        if (graph is null)
        {
            return false;
        }

        var startNode = graph.FindNearestTraversalStartNode(endpointX, endpointY, maxAboveDistance: 48f);
        if (startNode < 0)
        {
            return false;
        }

        var goalNode = graph.FindNearestReachableNode(
            currentGoal.X,
            currentGoal.Y,
            startNode,
            self.ClassId,
            team: team,
            carryingIntel: segment.RequiresCarryingIntel,
            verticalWeight: 8f,
            penalizeLowerCandidate: true);
        if (goalNode < 0)
        {
            return false;
        }

        var goal = graph.GetNode(goalNode);
        var goalDistance = Distance(goal.X, goal.Y, currentGoal.X, currentGoal.Y);
        if (goalDistance > ObjectiveEndpointRadius)
        {
            return false;
        }

        var path = graph.FindPath(startNode, goalNode, self.ClassId, team: team, carryingIntel: segment.RequiresCarryingIntel);
        return path is not null && path.Count >= 2;
    }

    private static bool IsReturnStageTape(BotBrainObjectiveTapeEntry tape) =>
        tape.Name.Contains(".ReturnStage", StringComparison.OrdinalIgnoreCase)
        || tape.Name.Contains(".ReturnAction", StringComparison.OrdinalIgnoreCase);

    private static bool IsReturnActionTape(BotBrainObjectiveTapeEntry tape) =>
        tape.Name.Contains(".ReturnAction", StringComparison.OrdinalIgnoreCase);

    private static bool IsHeavyActionTapeCrossClassEnabled() =>
        Environment.GetEnvironmentVariable("BOTBRAIN_ALLOW_HEAVY_ACTION_TAPES_CROSS_CLASS") is "1" or "true" or "TRUE";

    private static bool IsValidatedActionTapeCrossClassEnabled() =>
        Environment.GetEnvironmentVariable("BOTBRAIN_ALLOW_VALIDATED_ACTION_TAPES_CROSS_CLASS") is "1" or "true" or "TRUE";

    private static bool ShouldDisableMotionProofActionReplay() =>
        Environment.GetEnvironmentVariable("BOTBRAIN_DISABLE_MOTIONPROOF_ACTION_REPLAY") is "1" or "true" or "TRUE";

    private static bool ShouldExecuteActionTapeAsSampleLane(
        BotBrainObjectiveTapeEntry tape,
        BotBrainObjectiveTapeSegment segment) =>
        segment.Actions.Count > 0
        && IsMotionProofTape(tape)
        && Environment.GetEnvironmentVariable("BOTBRAIN_MOTIONPROOF_ACTIONS_AS_SAMPLE_LANES") is "1" or "true" or "TRUE";

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

    private static bool IsMotionProofSampleLane(BotBrainObjectiveTapeEntry? tape, BotBrainObjectiveTapeSegment segment) =>
        IsMotionProofTape(tape)
        && (segment.Actions.Count == 0
            || (tape is not null && ShouldExecuteActionTapeAsSampleLane(tape, segment)));

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
