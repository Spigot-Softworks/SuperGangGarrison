using System.Linq;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class ObjectiveRulesSystem
{

    public bool IsPlayerInControlPointCaptureZone(PlayerEntity player, int controlPointIndex)
    {
        if (controlPointIndex <= 0 || controlPointIndex > _host.Objectives.ControlPoints.Points.Count)
        {
            return false;
        }

        var zeroBasedControlPointIndex = controlPointIndex - 1;
        var hasExplicitZone = false;
        for (var zoneIndex = 0; zoneIndex < _host.Objectives.ControlPoints.Zones.Count; zoneIndex += 1)
        {
            var zone = _host.Objectives.ControlPoints.Zones[zoneIndex];
            if (zone.ControlPointIndex != zeroBasedControlPointIndex)
            {
                continue;
            }

            hasExplicitZone = true;
            if (player.IntersectsMarker(zone.Marker.CenterX, zone.Marker.CenterY, zone.Marker.Width, zone.Marker.Height))
            {
                return true;
            }
        }

        if (hasExplicitZone)
        {
            return false;
        }

        var point = _host.Objectives.ControlPoints.Points[zeroBasedControlPointIndex];
        return player.IntersectsMarker(point.Marker.CenterX, point.Marker.CenterY, point.Marker.Width, point.Marker.Height);
    }

    internal void ApplySnapshotControlPoints(SnapshotMessage snapshot)
    {
        if (snapshot.ControlPoints.Count == 0)
        {
            return;
        }

        var previousSetupTicksRemaining = _host.Objectives.ControlPoints.SetupTicksRemaining;
        _host.InitializeControlPointsForLevel(evaluateLogicGraph: false);
        if (_host.Objectives.ControlPoints.Points.Count == 0)
        {
            return;
        }

        if (IsKothMode((GameModeKind)snapshot.GameMode))
        {
            _host.Objectives.ControlPoints.SetupMode = false;
            _host.Objectives.ControlPoints.SetupTicksRemaining = 0;
        }
        else
        {
            _host.Objectives.ControlPoints.SetupMode = _host.Level.GetRoomObjects(RoomObjectType.ControlPointSetupGate).Count > 0;
            _host.Objectives.ControlPoints.SetupTicksRemaining = snapshot.ControlPointSetupTicksRemaining;
        }

        _host.UpdateControlPointSetupGates();

        for (var index = 0; index < snapshot.ControlPoints.Count; index += 1)
        {
            var pointState = snapshot.ControlPoints[index];
            var target = _host.Objectives.ControlPoints.Points.FirstOrDefault(point => point.Index == pointState.Index);
            if (target is null)
            {
                continue;
            }

            target.Team = pointState.Team == 0 ? null : (PlayerTeam)pointState.Team;
            target.CappingTeam = pointState.CappingTeam == 0 ? null : (PlayerTeam)pointState.CappingTeam;
            target.CappingTicks = pointState.CappingTicks;
            target.CapTimeTicks = pointState.CapTimeTicks;
            target.Cappers = pointState.Cappers;
            target.IsLocked = pointState.IsLocked;
            target.HasHealingAura = pointState.HasHealingAura;
        }

        var enteredSetupPhase = _host.ControlPointSetupDurationTicks > 0
            && _host.Objectives.ControlPoints.SetupTicksRemaining >= _host.ControlPointSetupDurationTicks
            && previousSetupTicksRemaining < _host.Objectives.ControlPoints.SetupTicksRemaining;
        _host.SyncMapLogicRuntimeFromAuthoritativeControlPoints(enteredSetupPhase);
    }
}
