using System.Linq;

namespace OpenGarrison.Core;

internal sealed partial class ObjectiveRulesSystem
{
    private const int ControlPointSetupDurationSeconds = 30;
    private const int ControlPointAttackTimeLimitMinutes = 3;
    private const int ControlPointCaptureBonusMinutes = 3;
    private const int ControlPointMaximumTimeMinutes = 5;

    internal int ControlPointSetupDurationTicks => GetControlPointSetupDurationTicks();

    private int GetControlPointSetupDurationTicks()
    {
        return Math.Max(1, _host.Config.TicksPerSecond * ControlPointSetupDurationSeconds);
    }

    private int GetControlPointAttackTimeLimitTicks()
    {
        return Math.Max(1, ControlPointAttackTimeLimitMinutes * _host.Config.TicksPerSecond * 60);
    }

    private int GetControlPointCaptureBonusTicks()
    {
        return Math.Max(1, ControlPointCaptureBonusMinutes * _host.Config.TicksPerSecond * 60);
    }

    private int GetControlPointMaximumTimeTicks()
    {
        return Math.Max(1, ControlPointMaximumTimeMinutes * _host.Config.TicksPerSecond * 60);
    }

    internal void ApplyControlPointSetupMatchRules()
    {
        if (!_host.Objectives.ControlPoints.SetupMode)
        {
            return;
        }

        _host.MatchRules = _host.MatchRules with
        {
            TimeLimitMinutes = ControlPointAttackTimeLimitMinutes,
            TimeLimitTicks = GetControlPointAttackTimeLimitTicks(),
        };
    }

    internal void ResetControlPointStateForNewRound()
    {
        if (IsKothMode(_host.MatchRules.Mode))
        {
            ResetKothStateForNewRound();
            return;
        }

        InitializeControlPointsForLevel();
        var hasSetupGates = _host.Level.GetRoomObjects(RoomObjectType.ControlPointSetupGate).Count > 0;
        if (_host.Objectives.ControlPoints.Points.Count == 0)
        {
            _host.Objectives.ControlPoints.SetupMode = hasSetupGates;
            ApplyControlPointSetupMatchRules();
            _host.Objectives.ControlPoints.SetupTicksRemaining = hasSetupGates ? GetControlPointSetupDurationTicks() : 0;
            UpdateControlPointSetupGates();
            return;
        }

        _host.Objectives.ControlPoints.SetupMode = hasSetupGates;
        ApplyControlPointSetupMatchRules();
        _host.Objectives.ControlPoints.SetupTicksRemaining = _host.Objectives.ControlPoints.SetupMode ? GetControlPointSetupDurationTicks() : 0;
        UpdateControlPointSetupGates();

        AssignControlPointCapTimes();
        AssignControlPointOwnership();
        ResetControlPointCappingState();
    }

    internal void UpdateControlPointSetupGates()
    {
        _host.Level.ControlPointSetupGatesActive = _host.Objectives.ControlPoints.SetupMode && _host.Objectives.ControlPoints.SetupTicksRemaining > 0;
    }

    internal void InitializeControlPointsForLevel(bool evaluateLogicGraph = true)
    {
        _host.Objectives.ControlPoints.Points.Clear();
        _host.Objectives.ControlPoints.Zones.Clear();

        var markers = _host.MatchRules.Mode == GameModeKind.Arena
            ? _host.Level.GetRoomObjects(RoomObjectType.ArenaControlPoint)
            : _host.Level.GetRoomObjects(RoomObjectType.ControlPoint);
        if (markers.Count == 0)
        {
            return;
        }

        var orderedMarkers = OrderControlPointMarkers(markers);
        for (var index = 0; index < orderedMarkers.Count; index += 1)
        {
            var marker = orderedMarkers[index];
            _host.Objectives.ControlPoints.Points.Add(new ControlPointState(index + 1, marker));
        }

        BuildControlPointZones();
        if (evaluateLogicGraph)
        {
            _host.MapLogic.EvaluateMapLogicGraph();
        }
    }

    private static List<RoomObjectMarker> OrderControlPointMarkers(IReadOnlyList<RoomObjectMarker> markers)
    {
        var withIndex = new List<(int Index, RoomObjectMarker Marker)>();
        var hasExplicitIndex = false;

        foreach (var marker in markers)
        {
            if (ControlPointMarkerIndex.TryGetIndex(marker, out var index))
            {
                hasExplicitIndex = true;
                withIndex.Add((index, marker));
            }
            else
            {
                withIndex.Add((0, marker));
            }
        }

        if (hasExplicitIndex && withIndex.All(entry => entry.Index > 0))
        {
            return withIndex
                .OrderBy(entry => entry.Index)
                .Select(entry => entry.Marker)
                .ToList();
        }

        return markers
            .OrderBy(marker => marker.CenterX)
            .ThenBy(marker => marker.CenterY)
            .ToList();
    }

    private void BuildControlPointZones()
    {
        var zones = _host.Level.GetRoomObjects(RoomObjectType.CaptureZone);
        if (zones.Count == 0 || _host.Objectives.ControlPoints.Points.Count == 0)
        {
            return;
        }

        for (var zoneIndex = 0; zoneIndex < zones.Count; zoneIndex += 1)
        {
            var zone = zones[zoneIndex];
            var closestIndex = -1;
            var closestDistance = float.MaxValue;
            for (var pointIndex = 0; pointIndex < _host.Objectives.ControlPoints.Points.Count; pointIndex += 1)
            {
                var point = _host.Objectives.ControlPoints.Points[pointIndex];
                var distance = SimulationMath.DistanceBetween(zone.CenterX, zone.CenterY, point.Marker.CenterX, point.Marker.CenterY);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = pointIndex;
                }
            }

            if (closestIndex >= 0)
            {
                _host.Objectives.ControlPoints.Zones.Add(new ControlPointZone(zone, closestIndex));
                var point = _host.Objectives.ControlPoints.Points[closestIndex];
                var currentArea = point.HealingAuraWidth * point.HealingAuraHeight;
                var zoneArea = zone.Width * zone.Height;
                if (zoneArea >= currentArea)
                {
                    point.HealingAuraCenterX = zone.CenterX;
                    point.HealingAuraCenterY = zone.CenterY;
                    point.HealingAuraWidth = Math.Max(48f, zone.Width);
                    point.HealingAuraHeight = Math.Max(28f, zone.Height);
                }
            }
        }
    }

    private void AssignControlPointCapTimes()
    {
        var total = _host.Objectives.ControlPoints.Points.Count;
        if (total == 0)
        {
            return;
        }

        for (var index = 0; index < _host.Objectives.ControlPoints.Points.Count; index += 1)
        {
            var point = _host.Objectives.ControlPoints.Points[index];
            var (storedMultiplier, isCustom) = point.Marker.CapTimeMultiplierSettings;
            point.CapTimeTicks = ControlPointCapTimeMultiplierMetadata.ResolveCapTimeTicks(
                total,
                point.Index,
                _host.Objectives.ControlPoints.SetupMode,
                storedMultiplier,
                isCustom);
        }
    }

    private void AssignControlPointOwnership()
    {
        var totalPoints = _host.Objectives.ControlPoints.Points.Count;
        for (var index = 0; index < totalPoints; index += 1)
        {
            var point = _host.Objectives.ControlPoints.Points[index];
            var context = new ControlPointOwnershipContext(
                point.Index,
                totalPoints,
                _host.Objectives.ControlPoints.SetupMode,
                _host.MatchRules.Mode,
                _host.Level.ControlPointSettings.OverrideInitialOwnership);
            point.Team = ControlPointOwnershipResolver.ResolveInitialTeam(point.Marker, in context);
        }
    }

    private void ResetControlPointCappingState()
    {
        for (var index = 0; index < _host.Objectives.ControlPoints.Points.Count; index += 1)
        {
            var point = _host.Objectives.ControlPoints.Points[index];
            point.CappingTicks = 0f;
            point.CappingTeam = null;
            point.Cappers = 0;
            point.RedCappers = 0;
            point.BlueCappers = 0;
            point.RedCaptureParticipantIds.Clear();
            point.BlueCaptureParticipantIds.Clear();
            point.IsLocked = _host.Level.ControlPointSettings.OverrideInitialOwnership
                ? ControlPointLockDependencyMetadata.GetInitialLocked(point.Marker.LockRules)
                : false;
            point.HasHealingAura = false;
        }
    }
}
