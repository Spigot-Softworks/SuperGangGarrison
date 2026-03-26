using System;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const int ControlPointSetupTicksDefault = 1800;

    private sealed record ControlPointZone(RoomObjectMarker Marker, int ControlPointIndex);

    private void ResetControlPointStateForNewRound()
    {
        InitializeControlPointsForLevel();
        var hasSetupGates = Level.GetRoomObjects(RoomObjectType.ControlPointSetupGate).Count > 0;
        if (_controlPoints.Count == 0)
        {
            _controlPointSetupMode = hasSetupGates;
            _controlPointSetupTicksRemaining = hasSetupGates ? ControlPointSetupTicksDefault : 0;
            UpdateControlPointSetupGates();
            return;
        }

        _controlPointSetupMode = hasSetupGates;
        _controlPointSetupTicksRemaining = _controlPointSetupMode ? ControlPointSetupTicksDefault : 0;
        UpdateControlPointSetupGates();

        AssignControlPointCapTimes();
        AssignControlPointOwnership();
        ResetControlPointCappingState();
    }

    private void UpdateControlPointSetupGates()
    {
        Level.ControlPointSetupGatesActive = _controlPointSetupMode && _controlPointSetupTicksRemaining > 0;
    }

    private void InitializeControlPointsForLevel()
    {
        _controlPoints.Clear();
        _controlPointZones.Clear();

        var markers = Level.GetRoomObjects(RoomObjectType.ControlPoint);
        if (markers.Count == 0)
        {
            return;
        }

        var orderedMarkers = OrderControlPointMarkers(markers);
        for (var index = 0; index < orderedMarkers.Count; index += 1)
        {
            var marker = orderedMarkers[index];
            _controlPoints.Add(new ControlPointState(index + 1, marker));
        }

        BuildControlPointZones();
    }

    private static List<RoomObjectMarker> OrderControlPointMarkers(IReadOnlyList<RoomObjectMarker> markers)
    {
        var withIndex = new List<(int Index, RoomObjectMarker Marker)>();
        var hasExplicitIndex = false;

        foreach (var marker in markers)
        {
            if (TryParseControlPointIndex(marker, out var index))
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

    private static bool TryParseControlPointIndex(RoomObjectMarker marker, out int index)
    {
        index = 0;
        if (string.IsNullOrWhiteSpace(marker.SourceName))
        {
            return false;
        }

        const string prefix = "ControlPoint";
        if (!marker.SourceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = marker.SourceName[prefix.Length..];
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return false;
        }

        return int.TryParse(suffix, out index) && index > 0;
    }

    private void BuildControlPointZones()
    {
        var zones = Level.GetRoomObjects(RoomObjectType.CaptureZone);
        if (zones.Count == 0 || _controlPoints.Count == 0)
        {
            return;
        }

        for (var zoneIndex = 0; zoneIndex < zones.Count; zoneIndex += 1)
        {
            var zone = zones[zoneIndex];
            var closestIndex = -1;
            var closestDistance = float.MaxValue;

            for (var pointIndex = 0; pointIndex < _controlPoints.Count; pointIndex += 1)
            {
                var point = _controlPoints[pointIndex];
                var distance = DistanceBetween(zone.CenterX, zone.CenterY, point.Marker.CenterX, point.Marker.CenterY);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = pointIndex;
                }
            }

            if (closestIndex >= 0)
            {
                _controlPointZones.Add(new ControlPointZone(zone, closestIndex));
            }
        }
    }

    private void AssignControlPointCapTimes()
    {
        var total = _controlPoints.Count;
        if (total == 0)
        {
            return;
        }

        var baseTime = _controlPointSetupMode ? 6 * 30f : 7 * 30f;

        for (var index = 0; index < _controlPoints.Count; index += 1)
        {
            var point = _controlPoints[index];
            point.CapTimeTicks = Math.Max(1, (int)MathF.Round(GetControlPointCapTime(total, point.Index, baseTime, _controlPointSetupMode)));
        }
    }

    private static float GetControlPointCapTime(int totalPoints, int pointIndex, float baseTime, bool setupMode)
    {
        if (totalPoints <= 1)
        {
            return baseTime * (setupMode ? 15f : 9f);
        }

        if (setupMode)
        {
            return totalPoints switch
            {
                2 => pointIndex == 2 ? baseTime * 2.5f : baseTime * 10f,
                3 => pointIndex == 3 ? baseTime * 2.5f : pointIndex == 2 ? baseTime * 5f : baseTime * 7.5f,
                4 => pointIndex == 4 ? baseTime * 1.5f : pointIndex == 3 ? baseTime * 3f : pointIndex == 2 ? baseTime * 4.5f : baseTime * 6f,
                _ => pointIndex == 5 ? baseTime : pointIndex == 4 ? baseTime * 2f : pointIndex == 3 ? baseTime * 3f : pointIndex == 2 ? baseTime * 4f : baseTime * 5f,
            };
        }

        return totalPoints switch
        {
            2 => baseTime * 4.5f,
            3 => pointIndex == 2 ? baseTime * 4.5f : baseTime * 2.25f,
            4 => pointIndex == 4 ? baseTime * 1.5f : pointIndex == 3 ? baseTime * 3f : pointIndex == 2 ? baseTime * 3f : baseTime * 1.5f,
            _ => pointIndex == 5 ? baseTime : pointIndex == 4 ? baseTime * 2f : pointIndex == 3 ? baseTime * 3f : pointIndex == 2 ? baseTime * 2f : baseTime,
        };
    }

    private void AssignControlPointOwnership()
    {
        for (var index = 0; index < _controlPoints.Count; index += 1)
        {
            _controlPoints[index].Team = null;
        }

        if (_controlPoints.Count <= 1)
        {
            return;
        }

        if (_controlPointSetupMode)
        {
            for (var index = 0; index < _controlPoints.Count; index += 1)
            {
                _controlPoints[index].Team = PlayerTeam.Blue;
            }

            return;
        }

        var middlePoint = _controlPoints.Count / 2f;
        var middleCeiling = (int)MathF.Ceiling(middlePoint);
        for (var index = 0; index < _controlPoints.Count; index += 1)
        {
            var point = _controlPoints[index];
            if (point.Index <= middlePoint)
            {
                point.Team = PlayerTeam.Red;
            }
            else
            {
                point.Team = PlayerTeam.Blue;
            }

            if (_controlPoints.Count > 2 && point.Index == middleCeiling)
            {
                point.Team = null;
            }
        }
    }

    private void ResetControlPointCappingState()
    {
        for (var index = 0; index < _controlPoints.Count; index += 1)
        {
            var point = _controlPoints[index];
            point.CappingTicks = 0f;
            point.CappingTeam = null;
            point.Cappers = 0;
            point.RedCappers = 0;
            point.BlueCappers = 0;
            point.IsLocked = false;
        }
    }

    private static int GetControlPointCapStrength(PlayerEntity player)
    {
        return player.ClassId == PlayerClass.Scout ? 2 : 1;
    }

    private void UpdateControlPointState()
    {
        if (MatchState.IsEnded || _controlPoints.Count == 0)
        {
            return;
        }

        var redCappersByPoint = new HashSet<int>[_controlPoints.Count];
        var blueCappersByPoint = new HashSet<int>[_controlPoints.Count];
        var redCapStrengthByPoint = new int[_controlPoints.Count];
        var blueCapStrengthByPoint = new int[_controlPoints.Count];
        for (var index = 0; index < _controlPoints.Count; index += 1)
        {
            redCappersByPoint[index] = new HashSet<int>();
            blueCappersByPoint[index] = new HashSet<int>();
        }

        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive)
            {
                continue;
            }

            if (player.IsSpyCloaked || player.IsUbered)
            {
                continue;
            }

            for (var zoneIndex = 0; zoneIndex < _controlPointZones.Count; zoneIndex += 1)
            {
                var zone = _controlPointZones[zoneIndex];
                if (!player.IntersectsMarker(zone.Marker.CenterX, zone.Marker.CenterY, zone.Marker.Width, zone.Marker.Height))
                {
                    continue;
                }

                if (player.Team == PlayerTeam.Red)
                {
                    if (redCappersByPoint[zone.ControlPointIndex].Add(player.Id))
                    {
                        redCapStrengthByPoint[zone.ControlPointIndex] += GetControlPointCapStrength(player);
                    }
                }
                else
                {
                    if (blueCappersByPoint[zone.ControlPointIndex].Add(player.Id))
                    {
                        blueCapStrengthByPoint[zone.ControlPointIndex] += GetControlPointCapStrength(player);
                    }
                }
            }
        }

        for (var index = 0; index < _controlPoints.Count; index += 1)
        {
            var point = _controlPoints[index];
            var previousRedCappers = point.RedCappers;
            var previousBlueCappers = point.BlueCappers;
            var redCappers = redCapStrengthByPoint[index];
            var blueCappers = blueCapStrengthByPoint[index];
            point.RedCappers = redCappers;
            point.BlueCappers = blueCappers;

            var defended = redCappers > 0 && blueCappers > 0;
            PlayerTeam? capTeam = null;
            var cappers = 0;

            if (redCappers > 0 && blueCappers == 0 && point.Team != PlayerTeam.Red)
            {
                capTeam = PlayerTeam.Red;
                cappers = redCappers;
            }
            else if (blueCappers > 0 && redCappers == 0 && point.Team != PlayerTeam.Blue)
            {
                capTeam = PlayerTeam.Blue;
                cappers = blueCappers;
            }

            if (point.CappingTicks > 0f && point.CappingTeam != capTeam)
            {
                cappers = 0;
            }
            else if (point.Team.HasValue && capTeam == point.Team.Value)
            {
                cappers = 0;
            }

            if (_controlPointSetupMode && capTeam == PlayerTeam.Blue)
            {
                cappers = 0;
            }

            point.Cappers = cappers;

            var capStrength = 0f;
            for (var strengthIndex = 1; strengthIndex <= cappers; strengthIndex += 1)
            {
                capStrength += strengthIndex <= 2 ? 1f : 0.5f;
            }

            point.IsLocked = IsControlPointLocked(point);

            if (!point.IsLocked)
            {
                var previousTotal = previousRedCappers + previousBlueCappers;
                var currentTotal = redCappers + blueCappers;
                if (previousTotal == 0 && currentTotal > 0 && capTeam.HasValue && (!point.Team.HasValue || point.Team.Value != capTeam.Value))
                {
                    RegisterWorldSoundEvent("CPBeginCapSnd", point.Marker.CenterX, point.Marker.CenterY);
                }

                if (point.Team == PlayerTeam.Red && previousBlueCappers > 0 && previousRedCappers == 0 && redCappers > 0)
                {
                    RegisterWorldSoundEvent("CPDefendedSnd", point.Marker.CenterX, point.Marker.CenterY);
                    RecordControlPointDefendedObjectiveLog(PlayerTeam.Red, redCappersByPoint[index]);
                }
                else if (point.Team == PlayerTeam.Blue && previousRedCappers > 0 && previousBlueCappers == 0 && blueCappers > 0)
                {
                    RegisterWorldSoundEvent("CPDefendedSnd", point.Marker.CenterX, point.Marker.CenterY);
                    RecordControlPointDefendedObjectiveLog(PlayerTeam.Blue, blueCappersByPoint[index]);
                }
            }

            if (point.IsLocked)
            {
                point.CappingTicks = 0f;
                point.CappingTeam = null;
                continue;
            }

            if (capTeam.HasValue && cappers > 0 && point.CappingTicks < point.CapTimeTicks)
            {
                point.CappingTicks += capStrength;
                point.CappingTeam = capTeam;
            }
            else if (point.CappingTicks > 0f && cappers == 0 && !defended)
            {
                point.CappingTicks -= 1f;
                if (point.Team == PlayerTeam.Blue)
                {
                    point.CappingTicks -= blueCappers * 0.5f;
                }
                else if (point.Team == PlayerTeam.Red)
                {
                    point.CappingTicks -= redCappers * 0.5f;
                }
            }

            if (point.CappingTicks <= 0f)
            {
                point.CappingTicks = 0f;
                point.CappingTeam = null;
                continue;
            }

            if (point.CappingTeam.HasValue && point.CappingTicks >= point.CapTimeTicks)
            {
                CaptureControlPoint(point, index, point.CappingTeam.Value, redCappersByPoint, blueCappersByPoint);
            }
        }
    }

    private bool IsControlPointLocked(ControlPointState point)
    {
        if (!point.Team.HasValue)
        {
            return false;
        }

        if (point.Team == PlayerTeam.Blue)
        {
            if (point.Index > 1)
            {
                var previous = _controlPoints[point.Index - 2];
                if (previous.Team != PlayerTeam.Red)
                {
                    return true;
                }
            }
        }
        else if (point.Team == PlayerTeam.Red)
        {
            if (point.Index < _controlPoints.Count)
            {
                var next = _controlPoints[point.Index];
                if (next.Team != PlayerTeam.Blue)
                {
                    return true;
                }
            }

            if (_controlPointSetupMode)
            {
                return true;
            }
        }

        return false;
    }

    private void CaptureControlPoint(
        ControlPointState point,
        int pointIndex,
        PlayerTeam team,
        IReadOnlyList<HashSet<int>> redCappersByPoint,
        IReadOnlyList<HashSet<int>> blueCappersByPoint)
    {
        point.Team = team;
        point.CappingTicks = 0f;
        point.CappingTeam = null;
        point.Cappers = 0;
        point.RedCappers = 0;
        point.BlueCappers = 0;

        var capperIds = team == PlayerTeam.Red ? redCappersByPoint[pointIndex] : blueCappersByPoint[pointIndex];
        if (capperIds.Count > 0)
        {
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.Team != team || !capperIds.Contains(player.Id))
                {
                    continue;
                }

                player.AddCap();
            }
        }

        RecordControlPointCapturedObjectiveLog(team, capperIds);

        if (_controlPointSetupMode)
        {
            var bonusTicks = Config.TicksPerSecond * 60 * 5;
            MatchState = MatchState with { TimeRemainingTicks = MatchState.TimeRemainingTicks + bonusTicks };
        }

        RegisterWorldSoundEvent("CPCapturedSnd", point.Marker.CenterX, point.Marker.CenterY);
        RegisterWorldSoundEvent("IntelPutSnd", point.Marker.CenterX, point.Marker.CenterY);
    }

    private void AdvanceControlPointMatchState()
    {
        if (MatchState.IsEnded)
        {
            return;
        }

        if (_controlPointSetupMode && _controlPointSetupTicksRemaining > 0)
        {
            _controlPointSetupTicksRemaining -= 1;
            var ticksPerSecond = Config.TicksPerSecond;
            if (_controlPointSetupTicksRemaining == ticksPerSecond * 6
                || _controlPointSetupTicksRemaining == ticksPerSecond * 5
                || _controlPointSetupTicksRemaining == ticksPerSecond * 4
                || _controlPointSetupTicksRemaining == ticksPerSecond * 3)
            {
                RegisterWorldSoundEvent("CountDown1Snd", LocalPlayer.X, LocalPlayer.Y);
            }
            else if (_controlPointSetupTicksRemaining == ticksPerSecond * 2)
            {
                RegisterWorldSoundEvent("CountDown2Snd", LocalPlayer.X, LocalPlayer.Y);
            }
            else if (_controlPointSetupTicksRemaining == ticksPerSecond)
            {
                MatchState = MatchState with { TimeRemainingTicks = MatchRules.TimeLimitTicks };
                RegisterWorldSoundEvent("SirenSnd", LocalPlayer.X, LocalPlayer.Y);
            }
        }
        UpdateControlPointSetupGates();

        if (MatchState.TimeRemainingTicks > 0)
        {
            MatchState = MatchState with { TimeRemainingTicks = MatchState.TimeRemainingTicks - 1 };
        }

        var overtimeActive = MatchState.TimeRemainingTicks <= 0 && _controlPoints.Any(point => point.CappingTicks > 0f);
        if (overtimeActive && !MatchState.IsOvertime)
        {
            MatchState = MatchState with { Phase = MatchPhase.Overtime, WinnerTeam = null };
        }

        var winner = ResolveControlPointWinner(overtimeActive);
        if (winner.HasValue)
        {
            MatchState = MatchState with { Phase = MatchPhase.Ended, WinnerTeam = winner };
            QueuePendingMapChange();
            return;
        }

        if (MatchState.TimeRemainingTicks <= 0 && !overtimeActive)
        {
            MatchState = MatchState with { Phase = MatchPhase.Ended, WinnerTeam = null };
            QueuePendingMapChange();
        }
        else if (!overtimeActive && MatchState.IsOvertime)
        {
            MatchState = MatchState with { Phase = MatchPhase.Running, WinnerTeam = null };
        }
    }

    private PlayerTeam? ResolveControlPointWinner(bool overtimeActive)
    {
        if (_controlPoints.Count == 0)
        {
            return null;
        }

        if (!_controlPointSetupMode)
        {
            var firstTeam = _controlPoints[0].Team;
            var lastTeam = _controlPoints[^1].Team;
            if (firstTeam.HasValue && lastTeam.HasValue && firstTeam.Value == lastTeam.Value)
            {
                return firstTeam.Value;
            }

            return null;
        }

        var finalTeam = _controlPoints[^1].Team;
        if (finalTeam == PlayerTeam.Red)
        {
            return PlayerTeam.Red;
        }

        if (MatchState.TimeRemainingTicks <= 0 && !overtimeActive)
        {
            return PlayerTeam.Blue;
        }

        return null;
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

        _controlPointSetupMode = Level.GetRoomObjects(RoomObjectType.ControlPointSetupGate).Count > 0;
        _controlPointSetupTicksRemaining = snapshot.ControlPointSetupTicksRemaining;
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
