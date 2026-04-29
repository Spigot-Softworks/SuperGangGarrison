using System.Linq;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void ResetControlPointStateForNewRound()
    {
        ControlPointSetupSystem.ResetForNewRound(this);
    }

    private void UpdateControlPointSetupGates()
    {
        ControlPointSetupSystem.UpdateSetupGates(this);
    }

    private void InitializeControlPointsForLevel()
    {
        ControlPointSetupSystem.InitializeForLevel(this);
    }

    private void UpdateControlPointState()
    {
        ControlPointStateSystem.Update(this);
    }

    private void AdvanceControlPointMatchState()
    {
        _runtimeController.AdvanceLegacyControlPointMatchState();
    }

    public bool IsPlayerInControlPointCaptureZone(PlayerEntity player, int controlPointIndex)
    {
        if (controlPointIndex <= 0 || controlPointIndex > _controlPoints.Count)
        {
            return false;
        }

        var zeroBasedControlPointIndex = controlPointIndex - 1;
        var hasExplicitZone = false;
        for (var zoneIndex = 0; zoneIndex < _controlPointZones.Count; zoneIndex += 1)
        {
            var zone = _controlPointZones[zoneIndex];
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

        var point = _controlPoints[zeroBasedControlPointIndex];
        return player.IntersectsMarker(point.Marker.CenterX, point.Marker.CenterY, point.Marker.Width, point.Marker.Height);
    }

    private void ApplySnapshotControlPoints(SnapshotMessage snapshot)
    {
        if (snapshot.ControlPoints.Count == 0)
        {
            return;
        }

        InitializeControlPointsForLevel();
        if (_controlPoints.Count == 0)
        {
            return;
        }

        if (IsKothMode((GameModeKind)snapshot.GameMode))
        {
            _controlPointSetupMode = false;
            _controlPointSetupTicksRemaining = 0;
        }
        else
        {
            _controlPointSetupMode = Level.GetRoomObjects(RoomObjectType.ControlPointSetupGate).Count > 0;
            _controlPointSetupTicksRemaining = snapshot.ControlPointSetupTicksRemaining;
        }

        UpdateControlPointSetupGates();

        for (var index = 0; index < snapshot.ControlPoints.Count; index += 1)
        {
            var pointState = snapshot.ControlPoints[index];
            var target = _controlPoints.FirstOrDefault(point => point.Index == pointState.Index);
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
        }
    }
}
