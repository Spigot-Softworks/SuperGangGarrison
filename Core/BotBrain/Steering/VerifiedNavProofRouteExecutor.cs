namespace OpenGarrison.Core.BotBrain;

public sealed class VerifiedNavProofRouteExecutor
{
    private const float SurfaceHorizontalTolerance = 18f;
    private const float SurfaceBottomTolerance = 14f;
    private const float EntryWindow = 34f;
    private const float MaxEdgeDivergence = 360f;
    private const float MaxRouteDivergence = 520f;
    private const float MaxRouteAttachmentDivergence = 520f;
    private const float ProgressDistance = 28f;
    private const float LaneEntryAlignmentWindow = 6f;
    private const float LaneExitCompletionWindow = 96f;
    private const float PreciseLaneExitCompletionWindow = 24f;
    private const int NoProgressTicks = 120;
    private const int MaxLaneEntryAlignmentTicks = 45;
    private const int RouteSuppressionTicks = 1800;
    private const int LaneActionExtraHoldTicks = 12;
    private const int EdgeActionExtraHoldTicks = 24;
    private const int KnownLandingActionExtraHoldTicks = 60;
    private const int PointLaneActionExtraHoldTicks = 45;
    private const int TerminalExtraHoldTicks = 120;
    private const int LaneRecoveryExtraTicks = 180;
    private const float LaneDropRecoveryThreshold = 48f;
    private const float AirborneDropRecoveryThreshold = 8f;
    private const float LaneRecoveryDeadZone = 12f;
    private const float TerminalHoldDeadZone = 10f;
    private const float TerminalActionStartWindow = 8f;
    private const float PointLaneClimbDeadZone = 2f;
    private const float PointLaneAirborneArrivalWindow = 24f;
    private const float SyntheticPointHandoffDistance = 96f;
    private const float SyntheticPointEarlyHandoffDistance = 64f;

    private readonly Dictionary<string, int> _suppressedRoutes = new(StringComparer.OrdinalIgnoreCase);
    private VerifiedNavProofRouteKind? _activeKind;
    private VerifiedNavProofGraphRoute? _activeRoute;
    private string _activeRouteKey = string.Empty;
    private int _routeEdgeIndex;
    private int _laneSegmentIndex;
    private int _edgeStartThinkTick;
    private int _routeStartThinkTick;
    private int _noProgressTicks;
    private bool _edgeActionStarted;
    private int _edgeActionTick;
    private int _laneActionTick;
    private int _laneEntryAlignmentTicks;
    private int _laneRecoveryTicks;
    private int _routeActionTick;
    private bool _terminalActionStarted;
    private bool _attachedAtRouteInterior;
    private int _terminalActionTick;
    private int _terminalStartThinkTick;
    private float _bestDistanceToExit = float.PositiveInfinity;

    public string LastTrace { get; private set; } = string.Empty;

    public bool IsActive => _activeRoute is not null;

    public void Reset()
    {
        ResetActiveRoute();
        _suppressedRoutes.Clear();
        LastTrace = string.Empty;
    }

    public bool TryResolve(
        VerifiedNavProofGraphAsset? asset,
        PlayerEntity self,
        PlayerTeam team,
        int thinkTick,
        SteeringOutput baseSteering,
        out SteeringOutput proofSteering)
    {
        proofSteering = baseSteering;
        LastTrace = string.Empty;
        if (asset is null || !self.IsAlive)
        {
            ResetActiveRoute();
            return false;
        }

        var desiredKind = self.IsCarryingIntel
            ? VerifiedNavProofRouteKind.Return
            : VerifiedNavProofRouteKind.Pickup;
        if (_activeRoute is null || _activeKind != desiredKind)
        {
            ResetActiveRoute();
            if (!TryStartRoute(asset, desiredKind, self, thinkTick))
            {
                return false;
            }
        }

        if (_activeRoute is null || _activeKind is null)
        {
            return false;
        }

        if (HasLaneSegments(asset, _activeRoute))
        {
            if (ShouldResolveCurrentEdgeBeforeLane(asset, _activeRoute))
            {
                return TryResolveEdge(asset, self, thinkTick, baseSteering, out proofSteering);
            }

            if (ShouldResolveCurrentEdgeAsActionBlock(asset, _activeRoute))
            {
                return TryResolveEdge(asset, self, thinkTick, baseSteering, out proofSteering);
            }

            return TryResolveLaneSegment(asset, self, thinkTick, baseSteering, out proofSteering);
        }

        if (_activeRoute.Actions.Count > 0 && !_attachedAtRouteInterior)
        {
            return TryResolveRouteAction(self, thinkTick, baseSteering, out proofSteering);
        }

        if (_activeRoute.EdgeIds.Count == 0 || _routeEdgeIndex >= _activeRoute.EdgeIds.Count)
        {
            return TryResolveTerminal(self, thinkTick, baseSteering, out proofSteering);
        }

        return TryResolveEdge(asset, self, thinkTick, baseSteering, out proofSteering);
    }

    private bool TryResolveEdge(
        VerifiedNavProofGraphAsset asset,
        PlayerEntity self,
        int thinkTick,
        SteeringOutput baseSteering,
        out SteeringOutput proofSteering)
    {
        proofSteering = baseSteering;
        if (_activeRoute is null || _activeKind is null)
        {
            return false;
        }

        if (_activeRoute.EdgeIds.Count == 0 || _routeEdgeIndex >= _activeRoute.EdgeIds.Count)
        {
            return TryResolveTerminal(self, thinkTick, baseSteering, out proofSteering);
        }

        var edge = ResolveEdge(asset, _activeRoute.EdgeIds[_routeEdgeIndex]);
        if (edge is null)
        {
            AbortRoute(thinkTick, "missing_edge", suppress: true);
            return false;
        }

        if (TryAdvanceArrivedEdges(asset, self, thinkTick, ref edge))
        {
            if (edge is null)
            {
                return TryResolveTerminal(self, thinkTick, baseSteering, out proofSteering);
            }
        }

        if (edge is null)
        {
            AbortRoute(thinkTick, "missing_edge_after_advance", suppress: true);
            return false;
        }

        if (!IsWithinRouteTolerance(asset, self, edge, out var currentSurfaceId, out var routeDistance))
        {
            AbortRoute(thinkTick, $"diverged edge:{edge.Id} dist:{routeDistance:0.0}", suppress: true);
            return false;
        }

        var ticksOnEdge = thinkTick - _edgeStartThinkTick + 1;
        var distanceToExit = Distance(self.X, self.Bottom, edge.ExitX, edge.ExitBottom);
        if (distanceToExit + ProgressDistance < _bestDistanceToExit)
        {
            _bestDistanceToExit = distanceToExit;
            _noProgressTicks = 0;
        }
        else
        {
            _noProgressTicks += 1;
            if (_noProgressTicks >= NoProgressTicks)
            {
                AbortRoute(thinkTick, $"no_progress edge:{edge.Id} bestExitDist:{_bestDistanceToExit:0.0}", suppress: true);
                return false;
            }
        }

        proofSteering = baseSteering;
        ApplyEdgeSteering(self, edge, currentSurfaceId, ref proofSteering);
        LastTrace =
            $"proofGraph=route:{_activeKind} edgeIndex:{_routeEdgeIndex}/{_activeRoute.EdgeIds.Count} edge:{edge.Id} " +
            $"from:{edge.FromSurfaceId} to:{edge.ToSurfaceId} ticks:{ticksOnEdge} routeTicks:{thinkTick - _routeStartThinkTick + 1} " +
            $"surface:{currentSurfaceId} dist:{routeDistance:0.0} exitDist:{distanceToExit:0.0} " +
            $"move:{proofSteering.MoveDirection:0} jump:{(proofSteering.Jump ? 1 : 0)} drop:{(proofSteering.DropDown ? 1 : 0)}";
        return true;
    }

    private bool ShouldResolveCurrentEdgeBeforeLane(
        VerifiedNavProofGraphAsset asset,
        VerifiedNavProofGraphRoute route)
    {
        if (route.EdgeIds.Count == 0 || _routeEdgeIndex >= route.EdgeIds.Count)
        {
            return false;
        }

        if (route.LaneSegmentIds is null || _laneSegmentIndex >= route.LaneSegmentIds.Count)
        {
            return true;
        }

        var currentEdge = ResolveEdge(asset, route.EdgeIds[_routeEdgeIndex]);
        var currentLaneSegment = ResolveLaneSegment(asset, route.LaneSegmentIds[_laneSegmentIndex]);
        return currentEdge is not null
            && currentLaneSegment is not null
            && currentLaneSegment.EdgeId > currentEdge.Id;
    }

    private bool ShouldResolveCurrentEdgeAsActionBlock(
        VerifiedNavProofGraphAsset asset,
        VerifiedNavProofGraphRoute route)
    {
        if (route.EdgeIds.Count == 0 || _routeEdgeIndex >= route.EdgeIds.Count)
        {
            return false;
        }

        if (route.LaneSegmentIds is null || _laneSegmentIndex >= route.LaneSegmentIds.Count)
        {
            return false;
        }

        var currentEdge = ResolveEdge(asset, route.EdgeIds[_routeEdgeIndex]);
        if (currentEdge is null || currentEdge.Actions.Count == 0)
        {
            return false;
        }

        var sameEdgeSegments = 0;
        var hasSyntheticChain = false;
        foreach (var segmentId in route.LaneSegmentIds)
        {
            var segment = ResolveLaneSegment(asset, segmentId);
            if (segment is null || segment.EdgeId != currentEdge.Id)
            {
                continue;
            }

            sameEdgeSegments += 1;
            hasSyntheticChain |= segment.FromSurfaceId < 0 && segment.ToSurfaceId < 0;
        }

        return sameEdgeSegments >= 3 && hasSyntheticChain;
    }

    private bool TryStartRoute(
        VerifiedNavProofGraphAsset asset,
        VerifiedNavProofRouteKind kind,
        PlayerEntity self,
        int thinkTick)
    {
        var selectedRoute = default(VerifiedNavProofGraphRoute);
        var selectedRouteKey = string.Empty;
        var selectedRouteIndex = -1;
        var selectedStartDistance = float.PositiveInfinity;
        var selectedRouteEdgeIndex = 0;
        var selectedLaneSegmentIndex = 0;
        var selectedAttachmentEdgeId = -1;
        var currentSurfaceId = FindCurrentSurfaceId(asset, self);
        var hasKindRoute = false;
        var hasUsableFirstEdge = false;
        var allCandidatesSuppressed = false;
        for (var routeIndex = 0; routeIndex < asset.Routes.Count; routeIndex++)
        {
            var candidate = asset.Routes[routeIndex];
            if (candidate.Kind != kind)
            {
                continue;
            }

            hasKindRoute = true;
            if (candidate.EdgeIds.Count == 0)
            {
                continue;
            }

            var candidateKey = BuildRouteKey(asset, kind, routeIndex);
            if (_suppressedRoutes.TryGetValue(candidateKey, out var suppressedUntilTick))
            {
                if (thinkTick < suppressedUntilTick)
                {
                    allCandidatesSuppressed = true;
                    continue;
                }

                _suppressedRoutes.Remove(candidateKey);
            }

            var firstEdge = ResolveEdge(asset, candidate.EdgeIds[0]);
            if (firstEdge is null)
            {
                continue;
            }

            hasUsableFirstEdge = true;
            var candidateStartX = candidate.StartX != 0f ? candidate.StartX : firstEdge.EntryX;
            var candidateStartBottom = candidate.StartBottom != 0f ? candidate.StartBottom : firstEdge.EntryBottom;
            var startDistance = Distance(self.X, self.Bottom, candidateStartX, candidateStartBottom);
            if (startDistance <= MaxRouteDivergence && startDistance < selectedStartDistance)
            {
                selectedRoute = candidate;
                selectedRouteKey = candidateKey;
                selectedRouteIndex = routeIndex;
                selectedStartDistance = startDistance;
                selectedRouteEdgeIndex = 0;
                selectedLaneSegmentIndex = 0;
                selectedAttachmentEdgeId = firstEdge.Id;
            }

            if (kind != VerifiedNavProofRouteKind.Return)
            {
                continue;
            }

            for (var edgeIndex = 1; edgeIndex < candidate.EdgeIds.Count; edgeIndex += 1)
            {
                var edge = ResolveEdge(asset, candidate.EdgeIds[edgeIndex]);
                if (edge is null)
                {
                    continue;
                }

                if (!CanAttachAtInteriorEdge(self, currentSurfaceId, edge))
                {
                    continue;
                }

                var attachmentDistance = Distance(self.X, self.Bottom, edge.EntryX, edge.EntryBottom);
                if (attachmentDistance > MaxRouteAttachmentDivergence || attachmentDistance >= selectedStartDistance)
                {
                    continue;
                }

                selectedRoute = candidate;
                selectedRouteKey = candidateKey;
                selectedRouteIndex = routeIndex;
                selectedStartDistance = attachmentDistance;
                selectedRouteEdgeIndex = edgeIndex;
                selectedLaneSegmentIndex = FindLaneSegmentIndexForEdge(asset, candidate, edge.Id);
                selectedAttachmentEdgeId = edge.Id;
            }
        }

        if (selectedRoute is null)
        {
            LastTrace = !hasKindRoute
                ? $"proofGraph=idle reason:no_route route:{kind}"
                : allCandidatesSuppressed && !hasUsableFirstEdge
                    ? $"proofGraph=idle reason:suppressed route:{kind}"
                    : !hasUsableFirstEdge
                        ? $"proofGraph=idle reason:missing_first_edge route:{kind}"
                        : $"proofGraph=idle reason:start_outside_tolerance route:{kind}";
            return false;
        }

        _activeKind = kind;
        _activeRoute = selectedRoute;
        _activeRouteKey = selectedRouteKey;
        _routeEdgeIndex = selectedRouteEdgeIndex;
        _laneSegmentIndex = selectedLaneSegmentIndex;
        _edgeStartThinkTick = thinkTick;
        _routeStartThinkTick = thinkTick;
        _noProgressTicks = 0;
        _edgeActionStarted = false;
        _edgeActionTick = 0;
        _laneActionTick = 0;
        _laneEntryAlignmentTicks = 0;
        _laneRecoveryTicks = 0;
        _routeActionTick = 0;
        _terminalActionStarted = false;
        _attachedAtRouteInterior = selectedRouteEdgeIndex > 0;
        _terminalActionTick = 0;
        _terminalStartThinkTick = 0;
        _bestDistanceToExit = float.PositiveInfinity;
        var attachmentTrace = selectedRouteEdgeIndex > 0
            ? $" attachEdge:{selectedAttachmentEdgeId} attachIndex:{selectedRouteEdgeIndex}"
            : string.Empty;
        LastTrace = $"proofGraph=selected route:{kind} index:{selectedRouteIndex} edges:{selectedRoute.EdgeIds.Count} source:{selectedRoute.Source} startDist:{selectedStartDistance:0.0}{attachmentTrace}";
        return true;
    }

    private static bool CanAttachAtInteriorEdge(PlayerEntity self, int currentSurfaceId, VerifiedNavProofGraphEdge edge)
    {
        if (IsKnownSurfaceMatch(currentSurfaceId, edge.FromSurfaceId)
            || IsKnownSurfaceMatch(currentSurfaceId, edge.ToSurfaceId))
        {
            return true;
        }

        var entryDistance = Distance(self.X, self.Bottom, edge.EntryX, edge.EntryBottom);
        return entryDistance <= EntryWindow
            && MathF.Abs(self.Bottom - edge.EntryBottom) <= SurfaceBottomTolerance;
    }

    private int FindLaneSegmentIndexForEdge(
        VerifiedNavProofGraphAsset asset,
        VerifiedNavProofGraphRoute route,
        int edgeId)
    {
        if (route.LaneSegmentIds is null || route.LaneSegmentIds.Count == 0 || edgeId < 0)
        {
            return 0;
        }

        for (var index = 0; index < route.LaneSegmentIds.Count; index += 1)
        {
            var segment = ResolveLaneSegment(asset, route.LaneSegmentIds[index]);
            if (segment is not null && segment.EdgeId >= edgeId)
            {
                return index;
            }
        }

        return route.LaneSegmentIds.Count;
    }

    private bool TryResolveLaneSegment(
        VerifiedNavProofGraphAsset asset,
        PlayerEntity self,
        int thinkTick,
        SteeringOutput baseSteering,
        out SteeringOutput proofSteering)
    {
        proofSteering = baseSteering;
        if (_activeRoute is null || _activeKind is null || _activeRoute.LaneSegmentIds is null)
        {
            return false;
        }

        if (_activeRoute.LaneSegmentIds.Count == 0 || _laneSegmentIndex >= _activeRoute.LaneSegmentIds.Count)
        {
            return TryResolveTerminal(self, thinkTick, baseSteering, out proofSteering);
        }

        var segment = ResolveLaneSegment(asset, _activeRoute.LaneSegmentIds[_laneSegmentIndex]);
        if (segment is null)
        {
            AbortRoute(thinkTick, "missing_lane_segment", suppress: true);
            return false;
        }

        if (TryAdvanceArrivedLaneSegments(asset, self, thinkTick, ref segment))
        {
            if (segment is null)
            {
                return TryResolveTerminal(self, thinkTick, baseSteering, out proofSteering);
            }

            if (_activeRoute is not null && ShouldResolveCurrentEdgeBeforeLane(asset, _activeRoute))
            {
                return TryResolveEdge(asset, self, thinkTick, baseSteering, out proofSteering);
            }
        }

        if (segment is null)
        {
            AbortRoute(thinkTick, "missing_lane_after_advance", suppress: true);
            return false;
        }

        if (!IsWithinLaneTolerance(asset, self, segment, out var currentSurfaceId, out var routeDistance))
        {
            AbortRoute(thinkTick, $"lane_diverged segment:{segment.Id} dist:{routeDistance:0.0}", suppress: true);
            return false;
        }

        var ticksOnSegment = thinkTick - _edgeStartThinkTick + 1;
        var distanceToExit = Distance(self.X, self.Bottom, segment.EndX, segment.EndBottom);
        if (distanceToExit + ProgressDistance < _bestDistanceToExit)
        {
            _bestDistanceToExit = distanceToExit;
            _noProgressTicks = 0;
        }
        else
        {
            _noProgressTicks += 1;
            if (_noProgressTicks >= NoProgressTicks)
            {
                if (TryResolveKnownSurfaceRecovery(segment, self, thinkTick, currentSurfaceId, routeDistance, distanceToExit, ticksOnSegment, baseSteering, out proofSteering))
                {
                    return true;
                }

                if (TryResolveLaneRecovery(segment, self, thinkTick, currentSurfaceId, routeDistance, distanceToExit, ticksOnSegment, baseSteering, out proofSteering))
                {
                    return true;
                }

                AbortRoute(thinkTick, $"lane_no_progress segment:{segment.Id} bestExitDist:{_bestDistanceToExit:0.0}", suppress: true);
                return false;
            }
        }

        if (_laneActionTick == 0
            && RequiresPreciseLaneEntry(segment)
            && IsKnownSurfaceMatch(currentSurfaceId, segment.FromSurfaceId)
            && MathF.Abs(self.X - segment.StartX) > LaneEntryAlignmentWindow)
        {
            _laneEntryAlignmentTicks += 1;
            if (_laneEntryAlignmentTicks > MaxLaneEntryAlignmentTicks)
            {
                AbortRoute(thinkTick, $"lane_entry_align_timeout segment:{segment.Id}", suppress: true);
                return false;
            }

            proofSteering.MoveDirection = MathF.Sign(segment.StartX - self.X);
            proofSteering.Jump = false;
            proofSteering.DropDown = false;
            LastTrace =
                $"proofGraph=lane route:{_activeKind} segmentIndex:{_laneSegmentIndex}/{_activeRoute?.LaneSegmentIds?.Count ?? 0} " +
                $"segment:{segment.Id} edge:{_activeKind}:{segment.EdgeId} from:{segment.FromSurfaceId} to:{segment.ToSurfaceId} " +
                $"align:1 alignTicks:{_laneEntryAlignmentTicks} actionTick:{_laneActionTick} ticks:{ticksOnSegment} " +
                $"routeTicks:{thinkTick - _routeStartThinkTick + 1} surface:{currentSurfaceId} " +
                $"entryDx:{segment.StartX - self.X:0.0} dist:{routeDistance:0.0} exitDist:{distanceToExit:0.0} " +
                $"move:{proofSteering.MoveDirection:0} jump:0 drop:0";
            return true;
        }

        var actions = segment.Actions ?? [];
        if (segment.ToSurfaceId < 0
            && actions.Count > 0
            && _laneActionTick >= GetTotalActionTicks(actions))
        {
            if (TryAdvanceSyntheticPointHandoff(asset, self, thinkTick, ref segment))
            {
                if (segment is null)
                {
                    return TryResolveTerminal(self, thinkTick, baseSteering, out proofSteering);
                }

                return TryResolveLaneSegment(asset, self, thinkTick, baseSteering, out proofSteering);
            }

            if (segment is null)
            {
                return TryResolveTerminal(self, thinkTick, baseSteering, out proofSteering);
            }

            ApplyPointLaneCorrection(segment, self, thinkTick, currentSurfaceId, routeDistance, distanceToExit, ticksOnSegment, baseSteering, out proofSteering);
            return true;
        }

        var totalActionTicks = GetTotalActionTicks(actions);
        if (segment.ToSurfaceId >= 0
            && actions.Count > 0
            && _laneActionTick >= totalActionTicks
            && !IsKnownSurfaceMatch(currentSurfaceId, segment.ToSurfaceId)
            && TryResolveKnownSurfaceRecovery(segment, self, thinkTick, currentSurfaceId, routeDistance, distanceToExit, ticksOnSegment, baseSteering, out proofSteering))
        {
            return true;
        }

        var maxHoldTicks = segment.ToSurfaceId < 0
            ? PointLaneActionExtraHoldTicks
            : segment.EndRequiresGrounded
                ? KnownLandingActionExtraHoldTicks
            : LaneActionExtraHoldTicks;
        if (!TryResolveActionRun(actions, ref _laneActionTick, holdLast: true, maxHoldTicks: maxHoldTicks, out var action))
        {
            if (TryAdvanceSyntheticPointHandoff(asset, self, thinkTick, ref segment))
            {
                if (segment is null)
                {
                    return TryResolveTerminal(self, thinkTick, baseSteering, out proofSteering);
                }

                return TryResolveLaneSegment(asset, self, thinkTick, baseSteering, out proofSteering);
            }

            if (segment is null)
            {
                return TryResolveTerminal(self, thinkTick, baseSteering, out proofSteering);
            }

            if (TryResolveKnownSurfaceRecovery(segment, self, thinkTick, currentSurfaceId, routeDistance, distanceToExit, ticksOnSegment, baseSteering, out proofSteering))
            {
                return true;
            }

            if (TryResolveLaneRecovery(segment, self, thinkTick, currentSurfaceId, routeDistance, distanceToExit, ticksOnSegment, baseSteering, out proofSteering))
            {
                return true;
            }

            AbortRoute(thinkTick, $"lane_actions_exhausted segment:{segment.Id}", suppress: true);
            return false;
        }

        proofSteering.MoveDirection = action.MoveDirection;
        proofSteering.Jump = action.Jump;
        proofSteering.DropDown = action.DropDown;
        LastTrace =
            $"proofGraph=lane route:{_activeKind} segmentIndex:{_laneSegmentIndex}/{_activeRoute?.LaneSegmentIds?.Count ?? 0} " +
            $"segment:{segment.Id} edge:{_activeKind}:{segment.EdgeId} from:{segment.FromSurfaceId} to:{segment.ToSurfaceId} " +
            $"actionTick:{_laneActionTick} ticks:{ticksOnSegment} routeTicks:{thinkTick - _routeStartThinkTick + 1} " +
            $"surface:{currentSurfaceId} dist:{routeDistance:0.0} exitDist:{distanceToExit:0.0} " +
            $"move:{proofSteering.MoveDirection:0} jump:{(proofSteering.Jump ? 1 : 0)} drop:{(proofSteering.DropDown ? 1 : 0)}";
        return true;
    }

    private void ApplyPointLaneCorrection(
        VerifiedNavProofLaneSegment segment,
        PlayerEntity self,
        int thinkTick,
        int currentSurfaceId,
        float routeDistance,
        float distanceToExit,
        int ticksOnSegment,
        SteeringOutput baseSteering,
        out SteeringOutput proofSteering)
    {
        proofSteering = baseSteering;
        var climbTarget = segment.EndBottom < self.Bottom - SurfaceBottomTolerance;
        var horizontalDeadZone = climbTarget ? PointLaneClimbDeadZone : LaneRecoveryDeadZone;
        proofSteering.MoveDirection = MathF.Abs(segment.EndX - self.X) <= horizontalDeadZone
            ? 0f
            : MathF.Sign(segment.EndX - self.X);
        proofSteering.Jump = climbTarget;
        proofSteering.DropDown = self.IsGrounded && segment.EndBottom > self.Bottom + LaneDropRecoveryThreshold;
        LastTrace =
            $"proofGraph=lane route:{_activeKind} segmentIndex:{_laneSegmentIndex}/{_activeRoute?.LaneSegmentIds?.Count ?? 0} " +
            $"segment:{segment.Id} edge:{_activeKind}:{segment.EdgeId} from:{segment.FromSurfaceId} to:{segment.ToSurfaceId} " +
            $"recovery:point actionTick:{_laneActionTick} ticks:{ticksOnSegment} routeTicks:{thinkTick - _routeStartThinkTick + 1} " +
            $"surface:{currentSurfaceId} dist:{routeDistance:0.0} exitDist:{distanceToExit:0.0} " +
            $"move:{proofSteering.MoveDirection:0} jump:{(proofSteering.Jump ? 1 : 0)} drop:{(proofSteering.DropDown ? 1 : 0)}";
    }

    private bool TryResolveRouteAction(
        PlayerEntity self,
        int thinkTick,
        SteeringOutput baseSteering,
        out SteeringOutput proofSteering)
    {
        proofSteering = baseSteering;
        if (_activeRoute is null || _activeKind is null)
        {
            return false;
        }

        if (!TryResolveActionRun(_activeRoute.Actions, ref _routeActionTick, holdLast: false, maxHoldTicks: TerminalExtraHoldTicks, out var action))
        {
            return TryResolveTerminal(self, thinkTick, baseSteering, out proofSteering);
        }

        proofSteering.MoveDirection = action.MoveDirection;
        proofSteering.Jump = action.Jump;
        proofSteering.DropDown = action.DropDown;
        var edgeLabel = TryResolveCurrentRouteEdgeLabel(out var label) ? label : $"{_activeKind}:route";
        LastTrace =
            $"proofGraph=routeAction route:{_activeKind} actionTick:{_routeActionTick} routeTicks:{thinkTick - _routeStartThinkTick + 1} " +
            $"edge:{edgeLabel} move:{proofSteering.MoveDirection:0} jump:{(proofSteering.Jump ? 1 : 0)} drop:{(proofSteering.DropDown ? 1 : 0)}";
        return true;
    }

    private bool TryResolveCurrentRouteEdgeLabel(out string label)
    {
        if (_activeRoute is not null
            && _activeKind is not null
            && _routeEdgeIndex >= 0
            && _routeEdgeIndex < _activeRoute.EdgeIds.Count)
        {
            label = $"{_activeKind}:{_activeRoute.EdgeIds[_routeEdgeIndex]}";
            return true;
        }

        label = string.Empty;
        return false;
    }

    private bool TryResolveTerminal(
        PlayerEntity self,
        int thinkTick,
        SteeringOutput baseSteering,
        out SteeringOutput proofSteering)
    {
        proofSteering = baseSteering;
        if (_activeRoute is null || _activeKind is null)
        {
            return false;
        }

        if (_activeRoute.TerminalActions is null || _activeRoute.TerminalActions.Count == 0)
        {
            LastTrace = $"proofGraph=complete route:{_activeKind} terminal:none";
            ResetActiveRoute();
            return false;
        }

        if (!_terminalActionStarted)
        {
            _terminalActionStarted = true;
            _terminalActionTick = 0;
            _terminalStartThinkTick = thinkTick;
        }

        var dx = _activeRoute.TerminalEndX - self.X;
        var climbTarget = _activeRoute.TerminalEndBottom < self.Bottom - SurfaceBottomTolerance;
        var terminalAlignmentTicks = thinkTick - _terminalStartThinkTick + 1;
        if (climbTarget
            && MathF.Abs(_activeRoute.TerminalStartX - self.X) > TerminalActionStartWindow
            && _terminalActionTick == 0
            && terminalAlignmentTicks <= MaxLaneEntryAlignmentTicks)
        {
            proofSteering.MoveDirection = MathF.Sign(_activeRoute.TerminalStartX - self.X);
            proofSteering.Jump = false;
            proofSteering.DropDown = false;
            LastTrace =
                $"proofGraph=terminal route:{_activeKind} terminalTick:{_terminalActionTick} routeTicks:{thinkTick - _routeStartThinkTick + 1} " +
                $"terminalTicks:{terminalAlignmentTicks} mode:align " +
                $"start=({_activeRoute.TerminalStartX:0.0},{_activeRoute.TerminalStartBottom:0.0}) " +
                $"end=({_activeRoute.TerminalEndX:0.0},{_activeRoute.TerminalEndBottom:0.0}) " +
                $"startDx:{_activeRoute.TerminalStartX - self.X:0.0} " +
                $"dist:{Distance(self.X, self.Bottom, _activeRoute.TerminalEndX, _activeRoute.TerminalEndBottom):0.0} " +
                $"move:{proofSteering.MoveDirection:0} jump:0 drop:0";
            return true;
        }

        if (climbTarget
            && TryResolveActionRun(
                _activeRoute.TerminalActions,
                ref _terminalActionTick,
                holdLast: false,
                maxHoldTicks: 0,
                out var terminalAction))
        {
            proofSteering.MoveDirection = terminalAction.MoveDirection;
            proofSteering.Jump = terminalAction.Jump;
            proofSteering.DropDown = terminalAction.DropDown;
            LastTrace =
                $"proofGraph=terminal route:{_activeKind} terminalTick:{_terminalActionTick} routeTicks:{thinkTick - _routeStartThinkTick + 1} " +
                $"terminalTicks:{thinkTick - _terminalStartThinkTick + 1} mode:action " +
                $"start=({_activeRoute.TerminalStartX:0.0},{_activeRoute.TerminalStartBottom:0.0}) " +
                $"end=({_activeRoute.TerminalEndX:0.0},{_activeRoute.TerminalEndBottom:0.0}) " +
                $"dist:{Distance(self.X, self.Bottom, _activeRoute.TerminalEndX, _activeRoute.TerminalEndBottom):0.0} " +
                $"move:{proofSteering.MoveDirection:0} jump:{(proofSteering.Jump ? 1 : 0)} drop:{(proofSteering.DropDown ? 1 : 0)}";
            return true;
        }

        _terminalActionTick += 1;
        proofSteering.MoveDirection = MathF.Abs(dx) <= TerminalHoldDeadZone
            ? 0f
            : MathF.Sign(dx);
        proofSteering.Jump = climbTarget;
        proofSteering.DropDown = self.IsGrounded && _activeRoute.TerminalEndBottom > self.Bottom + LaneDropRecoveryThreshold;
        LastTrace =
            $"proofGraph=terminal route:{_activeKind} terminalTick:{_terminalActionTick} routeTicks:{thinkTick - _routeStartThinkTick + 1} " +
            $"terminalTicks:{thinkTick - _terminalStartThinkTick + 1} " +
            $"start=({_activeRoute.TerminalStartX:0.0},{_activeRoute.TerminalStartBottom:0.0}) " +
            $"end=({_activeRoute.TerminalEndX:0.0},{_activeRoute.TerminalEndBottom:0.0}) " +
            $"dist:{Distance(self.X, self.Bottom, _activeRoute.TerminalEndX, _activeRoute.TerminalEndBottom):0.0} " +
            $"move:{proofSteering.MoveDirection:0} jump:{(proofSteering.Jump ? 1 : 0)} drop:{(proofSteering.DropDown ? 1 : 0)}";
        return true;
    }

    private bool TryAdvanceArrivedEdges(
        VerifiedNavProofGraphAsset asset,
        PlayerEntity self,
        int thinkTick,
        ref VerifiedNavProofGraphEdge? edge)
    {
        var advanced = false;
        while (_activeRoute is not null
            && _routeEdgeIndex < _activeRoute.EdgeIds.Count
            && edge is not null
            && IsOnSurface(asset, self, edge.ToSurfaceId))
        {
            var completedEdgeId = edge.Id;
            _routeEdgeIndex += 1;
            AdvanceLaneSegmentIndexPastEdge(asset, completedEdgeId);
            _edgeStartThinkTick = thinkTick;
            _noProgressTicks = 0;
            _edgeActionStarted = false;
            _edgeActionTick = 0;
            _bestDistanceToExit = float.PositiveInfinity;
            advanced = true;
            edge = _routeEdgeIndex < _activeRoute.EdgeIds.Count
                ? ResolveEdge(asset, _activeRoute.EdgeIds[_routeEdgeIndex])
                : null;
        }

        return advanced;
    }

    private bool TryAdvanceArrivedLaneSegments(
        VerifiedNavProofGraphAsset asset,
        PlayerEntity self,
        int thinkTick,
        ref VerifiedNavProofLaneSegment? segment)
    {
        var advanced = false;
        while (_activeRoute is not null
            && _activeRoute.LaneSegmentIds is not null
            && _laneSegmentIndex < _activeRoute.LaneSegmentIds.Count
            && segment is not null
            && HasArrivedAtLaneSegment(asset, self, segment))
        {
            AdvanceLaneSegment(asset, thinkTick, ref segment);
            advanced = true;
        }

        return advanced;
    }

    private bool TryAdvanceSyntheticPointHandoff(
        VerifiedNavProofGraphAsset asset,
        PlayerEntity self,
        int thinkTick,
        ref VerifiedNavProofLaneSegment? segment)
    {
        if (_activeRoute is null
            || _activeRoute.LaneSegmentIds is null
            || segment is null
            || segment.ToSurfaceId >= 0
            || _laneSegmentIndex + 1 >= _activeRoute.LaneSegmentIds.Count)
        {
            return false;
        }

        var nextSegment = ResolveLaneSegment(asset, _activeRoute.LaneSegmentIds[_laneSegmentIndex + 1]);
        if (nextSegment is null)
        {
            return false;
        }

        var currentToPointDistance = Distance(self.X, self.Bottom, segment.EndX, segment.EndBottom);
        var nextStartDistance = Distance(self.X, self.Bottom, nextSegment.StartX, nextSegment.StartBottom);
        var nextEndDistance = Distance(self.X, self.Bottom, nextSegment.EndX, nextSegment.EndBottom);
        var bestHandoffDistance = MathF.Min(currentToPointDistance, MathF.Min(nextStartDistance, nextEndDistance));
        var actions = segment.Actions ?? [];
        var totalActionTicks = GetTotalActionTicks(actions);
        var actionComplete = actions.Count == 0 || _laneActionTick >= totalActionTicks;
        var preciseHandoff = RequiresPreciseLaneEntry(segment) || RequiresPreciseLaneEntry(nextSegment);
        var handoffDistance = preciseHandoff || nextSegment.ToSurfaceId >= 0
            ? PreciseLaneExitCompletionWindow
            : SyntheticPointHandoffDistance;
        var earlyHandoffDistance = preciseHandoff || nextSegment.ToSurfaceId >= 0
            ? MathF.Min(PreciseLaneExitCompletionWindow, SyntheticPointEarlyHandoffDistance)
            : SyntheticPointEarlyHandoffDistance;
        var closeEnoughForEarlyHandoff = bestHandoffDistance <= earlyHandoffDistance
            && _laneActionTick >= Math.Min(totalActionTicks, 4);
        if (!actionComplete && !closeEnoughForEarlyHandoff)
        {
            return false;
        }

        if (bestHandoffDistance > handoffDistance)
        {
            return false;
        }

        AdvanceLaneSegment(asset, thinkTick, ref segment);
        return true;
    }

    private void AdvanceLaneSegment(
        VerifiedNavProofGraphAsset asset,
        int thinkTick,
        ref VerifiedNavProofLaneSegment? segment)
    {
        if (_activeRoute?.LaneSegmentIds is null)
        {
            segment = null;
            return;
        }

        var completedEdgeId = segment?.EdgeId ?? -1;
        _laneSegmentIndex += 1;
        AdvanceRouteEdgeIndexPastEdge(completedEdgeId);
        _edgeStartThinkTick = thinkTick;
        _noProgressTicks = 0;
        _laneActionTick = 0;
        _laneEntryAlignmentTicks = 0;
        _laneRecoveryTicks = 0;
        _bestDistanceToExit = float.PositiveInfinity;
        segment = _laneSegmentIndex < _activeRoute.LaneSegmentIds.Count
            ? ResolveLaneSegment(asset, _activeRoute.LaneSegmentIds[_laneSegmentIndex])
            : null;
    }

    private void AdvanceRouteEdgeIndexPastEdge(int edgeId)
    {
        if (_activeRoute is null || edgeId < 0)
        {
            return;
        }

        for (var index = _routeEdgeIndex; index < _activeRoute.EdgeIds.Count; index++)
        {
            if (_activeRoute.EdgeIds[index] == edgeId)
            {
                _routeEdgeIndex = Math.Max(_routeEdgeIndex, index + 1);
                return;
            }
        }
    }

    private void AdvanceLaneSegmentIndexPastEdge(VerifiedNavProofGraphAsset asset, int edgeId)
    {
        if (_activeRoute?.LaneSegmentIds is null || edgeId < 0)
        {
            return;
        }

        for (var index = _laneSegmentIndex; index < _activeRoute.LaneSegmentIds.Count; index += 1)
        {
            var segment = ResolveLaneSegment(asset, _activeRoute.LaneSegmentIds[index]);
            if (segment is null || segment.EdgeId != edgeId)
            {
                continue;
            }

            _laneSegmentIndex = index + 1;
        }
    }

    private bool IsWithinRouteTolerance(
        VerifiedNavProofGraphAsset asset,
        PlayerEntity self,
        VerifiedNavProofGraphEdge edge,
        out int currentSurfaceId,
        out float distance)
    {
        currentSurfaceId = FindCurrentSurfaceId(asset, self);
        if (IsKnownSurfaceMatch(currentSurfaceId, edge.FromSurfaceId)
            || IsKnownSurfaceMatch(currentSurfaceId, edge.ToSurfaceId))
        {
            distance = 0f;
            return true;
        }

        var entryDistance = Distance(self.X, self.Bottom, edge.EntryX, edge.EntryBottom);
        var exitDistance = Distance(self.X, self.Bottom, edge.ExitX, edge.ExitBottom);
        var segmentDistance = DistanceToSegment(
            self.X,
            self.Bottom,
            edge.EntryX,
            edge.EntryBottom,
            edge.ExitX,
            edge.ExitBottom);
        distance = MathF.Min(MathF.Min(entryDistance, exitDistance), segmentDistance);
        return distance <= MaxEdgeDivergence;
    }

    private bool IsWithinLaneTolerance(
        VerifiedNavProofGraphAsset asset,
        PlayerEntity self,
        VerifiedNavProofLaneSegment segment,
        out int currentSurfaceId,
        out float distance)
    {
        currentSurfaceId = FindCurrentSurfaceId(asset, self);
        if (IsKnownSurfaceMatch(currentSurfaceId, segment.FromSurfaceId)
            || IsKnownSurfaceMatch(currentSurfaceId, segment.ToSurfaceId))
        {
            distance = 0f;
            return true;
        }

        var startDistance = Distance(self.X, self.Bottom, segment.StartX, segment.StartBottom);
        var endDistance = Distance(self.X, self.Bottom, segment.EndX, segment.EndBottom);
        var segmentDistance = DistanceToSegment(
            self.X,
            self.Bottom,
            segment.StartX,
            segment.StartBottom,
            segment.EndX,
            segment.EndBottom);
        distance = MathF.Min(MathF.Min(startDistance, endDistance), segmentDistance);
        return distance <= MaxRouteDivergence;
    }

    private bool HasArrivedAtLaneSegment(
        VerifiedNavProofGraphAsset asset,
        PlayerEntity self,
        VerifiedNavProofLaneSegment segment)
    {
        var exitDistance = Distance(self.X, self.Bottom, segment.EndX, segment.EndBottom);
        var finalLaneSegment = IsFinalLaneSegment(segment);
        var exitWindow = finalLaneSegment
            ? LaneExitCompletionWindow
            : RequiresPreciseLaneEntry(segment)
            ? PreciseLaneExitCompletionWindow
            : LaneExitCompletionWindow;
        if (!IsOnSurface(asset, self, segment.ToSurfaceId))
        {
            if (segment.ToSurfaceId >= 0)
            {
                return false;
            }

            if (segment.EndRequiresGrounded
                && !self.IsGrounded
                && (exitDistance > PointLaneAirborneArrivalWindow || _laneActionTick < GetTotalActionTicks(segment.Actions ?? [])))
            {
                return false;
            }
        }

        return exitDistance <= exitWindow;
    }

    private bool IsFinalLaneSegment(VerifiedNavProofLaneSegment segment)
    {
        if (_activeRoute?.LaneSegmentIds is null || _laneSegmentIndex < 0)
        {
            return false;
        }

        return _laneSegmentIndex >= _activeRoute.LaneSegmentIds.Count - 1
            || _activeRoute.LaneSegmentIds[_laneSegmentIndex] == segment.Id
            && _laneSegmentIndex == _activeRoute.LaneSegmentIds.Count - 1;
    }

    private void ApplyEdgeSteering(
        PlayerEntity self,
        VerifiedNavProofGraphEdge edge,
        int currentSurfaceId,
        ref SteeringOutput steering)
    {
        var entryActionWindow = GetEntryActionWindow(edge);
        var atEntry = IsKnownSurfaceMatch(currentSurfaceId, edge.FromSurfaceId)
            && MathF.Abs(self.X - edge.EntryX) <= entryActionWindow;
        var seekingEntry = !_edgeActionStarted && !atEntry;
        if ((_edgeActionStarted || atEntry) && TryResolveProofAction(edge, out var action))
        {
            steering.MoveDirection = action.MoveDirection;
            steering.Jump = action.Jump;
            steering.DropDown = action.DropDown;
            return;
        }

        var targetX = seekingEntry ? edge.EntryX : edge.ExitX;
        var moveDirection = MathF.Abs(targetX - self.X) <= 8f
            ? ResolveProofDirection(edge)
            : MathF.Sign(targetX - self.X);

        steering.MoveDirection = moveDirection;
        steering.Jump = ShouldJump(self, edge, currentSurfaceId, seekingEntry, _edgeActionTick);
        steering.DropDown = !seekingEntry && edge.DropTicks > 0 && _edgeActionTick <= Math.Clamp(edge.DropTicks + 3, 4, 18);
        if (_edgeActionStarted && !seekingEntry)
        {
            if (!steering.Jump && self.IsGrounded && edge.ExitBottom < self.Bottom - SurfaceBottomTolerance)
            {
                steering.Jump = true;
            }

            if (!steering.DropDown && self.IsGrounded && edge.ExitBottom > self.Bottom + LaneDropRecoveryThreshold)
            {
                steering.DropDown = true;
            }
        }
    }

    private bool TryResolveProofAction(VerifiedNavProofGraphEdge edge, out VerifiedNavProofGraphActionRun action)
    {
        action = default!;
        var actions = edge.Actions ?? [];
        if (actions.Count == 0)
        {
            return false;
        }

        if (!_edgeActionStarted)
        {
            _edgeActionStarted = true;
            _edgeActionTick = 0;
        }

        return TryResolveActionRun(actions, ref _edgeActionTick, holdLast: true, maxHoldTicks: EdgeActionExtraHoldTicks, out action);
    }

    private static float GetEntryActionWindow(VerifiedNavProofGraphEdge edge)
        => edge.JumpTicks > 0 || edge.DropTicks > 0
            ? 10f
            : EntryWindow;

    private static bool TryResolveActionRun(
        IReadOnlyList<VerifiedNavProofGraphActionRun> actions,
        ref int actionTick,
        bool holdLast,
        int maxHoldTicks,
        out VerifiedNavProofGraphActionRun action)
    {
        action = default!;
        if (actions.Count == 0)
        {
            return false;
        }

        var totalTicks = 0;
        foreach (var run in actions)
        {
            totalTicks += Math.Max(0, run.Ticks);
        }

        if (actionTick >= totalTicks)
        {
            if (holdLast)
            {
                if (maxHoldTicks >= 0 && actionTick >= totalTicks + maxHoldTicks)
                {
                    return false;
                }

                action = actions[^1];
                actionTick += 1;
                return true;
            }

            return false;
        }

        var remainingTick = actionTick;
        actionTick += 1;
        foreach (var run in actions)
        {
            if (remainingTick < run.Ticks)
            {
                action = run;
                return true;
            }

            remainingTick -= run.Ticks;
        }

        return false;
    }

    private static int GetTotalActionTicks(IReadOnlyList<VerifiedNavProofGraphActionRun> actions)
    {
        var totalTicks = 0;
        foreach (var action in actions)
        {
            totalTicks += Math.Max(0, action.Ticks);
        }

        return totalTicks;
    }

    private static bool ShouldJump(
        PlayerEntity self,
        VerifiedNavProofGraphEdge edge,
        int currentSurfaceId,
        bool seekingEntry,
        int ticksOnEdge)
    {
        if (seekingEntry)
        {
            return false;
        }

        var hasJumpIntent = edge.JumpTicks > 0
            || (edge.FromSurfaceId != edge.ToSurfaceId
                && edge.DropTicks == 0
                && MathF.Abs(edge.ExitX - edge.EntryX) > 48f
                && edge.ExitBottom <= edge.EntryBottom + 24f);
        if (!hasJumpIntent)
        {
            return false;
        }

        if (IsKnownSurfaceMatch(currentSurfaceId, edge.FromSurfaceId)
            && self.IsGrounded
            && MathF.Abs(self.X - edge.EntryX) <= EntryWindow)
        {
            return true;
        }

        var jumpWindow = Math.Clamp(edge.JumpTicks + 4, 5, 24);
        if (ticksOnEdge <= jumpWindow)
        {
            return true;
        }

        return IsKnownSurfaceMatch(currentSurfaceId, edge.FromSurfaceId)
            && self.IsGrounded
            && edge.ExitBottom < edge.EntryBottom - 16f
            && MathF.Abs(self.X - edge.EntryX) <= EntryWindow;
    }

    private static float ResolveProofDirection(VerifiedNavProofGraphEdge edge)
    {
        if (edge.RightTicks != edge.LeftTicks)
        {
            return edge.RightTicks > edge.LeftTicks ? 1f : -1f;
        }

        if (MathF.Abs(edge.ExitX - edge.EntryX) > 1f)
        {
            return MathF.Sign(edge.ExitX - edge.EntryX);
        }

        return 0f;
    }

    private bool IsOnSurface(VerifiedNavProofGraphAsset asset, PlayerEntity self, int surfaceId)
    {
        if (surfaceId < 0)
        {
            return false;
        }

        var surface = asset.Surfaces.FirstOrDefault(surface => surface.Id == surfaceId);
        return surface is not null
            && self.IsGrounded
            && self.X >= surface.Left - SurfaceHorizontalTolerance
            && self.X <= surface.Right + SurfaceHorizontalTolerance
            && MathF.Abs(self.Bottom - surface.Top) <= SurfaceBottomTolerance;
    }

    private static int FindCurrentSurfaceId(VerifiedNavProofGraphAsset asset, PlayerEntity self)
    {
        if (!self.IsGrounded)
        {
            return -1;
        }

        var bestSurfaceId = -1;
        var bestBottomDelta = float.PositiveInfinity;
        foreach (var surface in asset.Surfaces)
        {
            if (self.X < surface.Left - SurfaceHorizontalTolerance
                || self.X > surface.Right + SurfaceHorizontalTolerance)
            {
                continue;
            }

            var bottomDelta = MathF.Abs(self.Bottom - surface.Top);
            if (bottomDelta <= SurfaceBottomTolerance && bottomDelta < bestBottomDelta)
            {
                bestBottomDelta = bottomDelta;
                bestSurfaceId = surface.Id;
            }
        }

        return bestSurfaceId;
    }

    private VerifiedNavProofGraphEdge? ResolveEdge(VerifiedNavProofGraphAsset asset, int edgeId)
        => asset.Edges.FirstOrDefault(edge => edge.Id == edgeId);

    private static bool HasLaneSegments(VerifiedNavProofGraphAsset asset, VerifiedNavProofGraphRoute route)
        => route.LaneSegmentIds is not null
            && route.LaneSegmentIds.Count > 0
            && asset.LaneSegments.Count > 0;

    private VerifiedNavProofLaneSegment? ResolveLaneSegment(VerifiedNavProofGraphAsset asset, int segmentId)
        => asset.LaneSegments.FirstOrDefault(segment => segment.Id == segmentId);

    private static bool RequiresPreciseLaneEntry(VerifiedNavProofLaneSegment segment)
        => (segment.Actions?.Any(static action => action.Jump || action.DropDown) ?? false)
            || MathF.Abs(segment.EndBottom - segment.StartBottom) > SurfaceBottomTolerance;

    private static bool IsKnownSurfaceMatch(int currentSurfaceId, int targetSurfaceId)
        => targetSurfaceId >= 0 && currentSurfaceId == targetSurfaceId;

    private bool TryResolveLaneRecovery(
        VerifiedNavProofLaneSegment segment,
        PlayerEntity self,
        int thinkTick,
        int currentSurfaceId,
        float routeDistance,
        float distanceToExit,
        int ticksOnSegment,
        SteeringOutput baseSteering,
        out SteeringOutput proofSteering)
    {
        proofSteering = baseSteering;
        if (!ShouldAttemptDropRecovery(segment, self, currentSurfaceId))
        {
            return false;
        }

        _laneRecoveryTicks += 1;
        if (_laneRecoveryTicks > LaneRecoveryExtraTicks)
        {
            AbortRoute(thinkTick, $"lane_recovery_exhausted segment:{segment.Id}", suppress: true);
            return false;
        }

        proofSteering.MoveDirection = MathF.Abs(segment.EndX - self.X) <= LaneRecoveryDeadZone
            ? 0f
            : MathF.Sign(segment.EndX - self.X);
        proofSteering.Jump = false;
        proofSteering.DropDown = self.IsGrounded;
        LastTrace =
            $"proofGraph=lane route:{_activeKind} segmentIndex:{_laneSegmentIndex}/{_activeRoute?.LaneSegmentIds?.Count ?? 0} " +
            $"segment:{segment.Id} edge:{_activeKind}:{segment.EdgeId} from:{segment.FromSurfaceId} to:{segment.ToSurfaceId} " +
            $"recovery:drop recoveryTicks:{_laneRecoveryTicks} actionTick:{_laneActionTick} ticks:{ticksOnSegment} " +
            $"routeTicks:{thinkTick - _routeStartThinkTick + 1} surface:{currentSurfaceId} dist:{routeDistance:0.0} " +
            $"exitDist:{distanceToExit:0.0} move:{proofSteering.MoveDirection:0} jump:0 drop:{(proofSteering.DropDown ? 1 : 0)}";
        return true;
    }

    private bool TryResolveKnownSurfaceRecovery(
        VerifiedNavProofLaneSegment segment,
        PlayerEntity self,
        int thinkTick,
        int currentSurfaceId,
        float routeDistance,
        float distanceToExit,
        int ticksOnSegment,
        SteeringOutput baseSteering,
        out SteeringOutput proofSteering)
    {
        proofSteering = baseSteering;
        if (segment.ToSurfaceId < 0 || IsKnownSurfaceMatch(currentSurfaceId, segment.ToSurfaceId))
        {
            return false;
        }

        _laneRecoveryTicks += 1;
        if (_laneRecoveryTicks > LaneRecoveryExtraTicks)
        {
            AbortRoute(thinkTick, $"known_surface_recovery_exhausted segment:{segment.Id}", suppress: true);
            return false;
        }

        var targetAbove = segment.EndBottom < self.Bottom - SurfaceBottomTolerance;
        var targetBelow = segment.EndBottom > self.Bottom + LaneDropRecoveryThreshold;
        proofSteering.MoveDirection = MathF.Abs(segment.EndX - self.X) <= LaneRecoveryDeadZone
            ? 0f
            : MathF.Sign(segment.EndX - self.X);
        proofSteering.Jump = targetAbove;
        proofSteering.DropDown = self.IsGrounded && targetBelow;
        LastTrace =
            $"proofGraph=lane route:{_activeKind} segmentIndex:{_laneSegmentIndex}/{_activeRoute?.LaneSegmentIds?.Count ?? 0} " +
            $"segment:{segment.Id} edge:{_activeKind}:{segment.EdgeId} from:{segment.FromSurfaceId} to:{segment.ToSurfaceId} " +
            $"recovery:surface recoveryTicks:{_laneRecoveryTicks} actionTick:{_laneActionTick} ticks:{ticksOnSegment} " +
            $"routeTicks:{thinkTick - _routeStartThinkTick + 1} surface:{currentSurfaceId} dist:{routeDistance:0.0} " +
            $"exitDist:{distanceToExit:0.0} move:{proofSteering.MoveDirection:0} jump:{(proofSteering.Jump ? 1 : 0)} " +
            $"drop:{(proofSteering.DropDown ? 1 : 0)}";
        return true;
    }

    private static bool ShouldAttemptDropRecovery(
        VerifiedNavProofLaneSegment segment,
        PlayerEntity self,
        int currentSurfaceId)
    {
        if (IsKnownSurfaceMatch(currentSurfaceId, segment.ToSurfaceId))
        {
            return false;
        }

        if (self.IsGrounded)
        {
            return segment.EndBottom > self.Bottom + LaneDropRecoveryThreshold;
        }

        return segment.EndBottom > self.Bottom + AirborneDropRecoveryThreshold;
    }

    private void AbortRoute(int thinkTick, string reason, bool suppress)
    {
        if (suppress && _activeRoute is not null && _activeKind is not null)
        {
            _suppressedRoutes[_activeRouteKey] = thinkTick + RouteSuppressionTicks;
        }

        LastTrace = $"proofGraph=abort route:{_activeKind?.ToString() ?? "none"} reason:{reason}";
        ResetActiveRoute();
    }

    private void ResetActiveRoute()
    {
        _activeKind = null;
        _activeRoute = null;
        _activeRouteKey = string.Empty;
        _routeEdgeIndex = 0;
        _laneSegmentIndex = 0;
        _edgeStartThinkTick = 0;
        _routeStartThinkTick = 0;
        _noProgressTicks = 0;
        _edgeActionStarted = false;
        _edgeActionTick = 0;
        _laneActionTick = 0;
        _laneEntryAlignmentTicks = 0;
        _laneRecoveryTicks = 0;
        _routeActionTick = 0;
        _terminalActionStarted = false;
        _attachedAtRouteInterior = false;
        _terminalActionTick = 0;
        _terminalStartThinkTick = 0;
        _bestDistanceToExit = float.PositiveInfinity;
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static string BuildRouteKey(
        VerifiedNavProofGraphAsset asset,
        VerifiedNavProofRouteKind kind,
        int routeIndex)
    {
        return $"{asset.LevelName}:{asset.MapAreaIndex}:{asset.Team}:{asset.ClassId}:{kind}:{routeIndex}";
    }

    private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared <= 0.0001f)
        {
            return Distance(px, py, ax, ay);
        }

        var t = (((px - ax) * dx) + ((py - ay) * dy)) / lengthSquared;
        t = Math.Clamp(t, 0f, 1f);
        return Distance(px, py, ax + (t * dx), ay + (t * dy));
    }
}
