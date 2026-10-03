using System.Linq;

namespace OpenGarrison.Core;

internal sealed partial class ObjectiveRulesSystem
{
    private const int VipCaptureStrength = 5;

    internal void UpdateControlPointState()
    {
        if (_host.MatchState.IsEnded || _host.Objectives.ControlPoints.Points.Count == 0)
        {
            return;
        }

        _host.MapLogic.RefreshMapLogicRuntimeIfControlPointInputsChanged();
        _host.MapLogic.TickMapLogicTimersOncePerFrame();

        var redCappersByPoint = new HashSet<int>[_host.Objectives.ControlPoints.Points.Count];
        var blueCappersByPoint = new HashSet<int>[_host.Objectives.ControlPoints.Points.Count];
        var redPlayersByPoint = new HashSet<int>[_host.Objectives.ControlPoints.Points.Count];
        var bluePlayersByPoint = new HashSet<int>[_host.Objectives.ControlPoints.Points.Count];
        var redVipDecayBlockersByPoint = new HashSet<int>[_host.Objectives.ControlPoints.Points.Count];
        var blueVipDecayBlockersByPoint = new HashSet<int>[_host.Objectives.ControlPoints.Points.Count];
        var redCapStrengthByPoint = new int[_host.Objectives.ControlPoints.Points.Count];
        var blueCapStrengthByPoint = new int[_host.Objectives.ControlPoints.Points.Count];
        var redReverseStrengthByPoint = new int[_host.Objectives.ControlPoints.Points.Count];
        var blueReverseStrengthByPoint = new int[_host.Objectives.ControlPoints.Points.Count];
        for (var index = 0; index < _host.Objectives.ControlPoints.Points.Count; index += 1)
        {
            redCappersByPoint[index] = new HashSet<int>();
            blueCappersByPoint[index] = new HashSet<int>();
            redPlayersByPoint[index] = new HashSet<int>();
            bluePlayersByPoint[index] = new HashSet<int>();
            redVipDecayBlockersByPoint[index] = new HashSet<int>();
            blueVipDecayBlockersByPoint[index] = new HashSet<int>();
        }

        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive
                || !_host.LastToDieRules.CanPlayerContributeToControlPoint(player)
                || IsIgnoringPlayerForControlPointCapture(player)
                || !_host.VipRules.CanPlayerAffectControlPointInVipMode())
            {
                continue;
            }

            if (player.Team != _host.LocalPlayer.Team
                && _host.EnumerateSimulatedPlayers().Any(owner =>
                    owner.IsAlive
                    && owner.IsRaging
                    && owner.ClassId == PlayerClass.Soldier
                    && owner.Team != player.Team
                    && _host.LastToDieRules.GetLastToDieGameplaySettings(owner).EnableSoldierRageCaptureLockout))
            {
                continue;
            }

            for (var zoneIndex = 0; zoneIndex < _host.Objectives.ControlPoints.Zones.Count; zoneIndex += 1)
            {
                var zone = _host.Objectives.ControlPoints.Zones[zoneIndex];
                if (!player.IntersectsMarker(zone.Marker.CenterX, zone.Marker.CenterY, zone.Marker.Width, zone.Marker.Height))
                {
                    continue;
                }

                var canProgressCapture = _host.VipRules.CanPlayerCaptureInVipMode(player);
                var reverseStrength = GetControlPointCapStrength(player);
                var pausesVipDecay = _host.VipRules.CanPlayerPauseVipCaptureDecay(player);
                if (player.Team == PlayerTeam.Red)
                {
                    if (redPlayersByPoint[zone.ControlPointIndex].Add(player.Id))
                    {
                        redReverseStrengthByPoint[zone.ControlPointIndex] += reverseStrength;
                        if (pausesVipDecay)
                        {
                            redVipDecayBlockersByPoint[zone.ControlPointIndex].Add(player.Id);
                        }
                    }

                    if (canProgressCapture && redCappersByPoint[zone.ControlPointIndex].Add(player.Id))
                    {
                        redCapStrengthByPoint[zone.ControlPointIndex] += GetCaptureProgressStrength(player);
                    }
                }
                else
                {
                    if (bluePlayersByPoint[zone.ControlPointIndex].Add(player.Id))
                    {
                        blueReverseStrengthByPoint[zone.ControlPointIndex] += reverseStrength;
                        if (pausesVipDecay)
                        {
                            blueVipDecayBlockersByPoint[zone.ControlPointIndex].Add(player.Id);
                        }
                    }

                    if (canProgressCapture && blueCappersByPoint[zone.ControlPointIndex].Add(player.Id))
                    {
                        blueCapStrengthByPoint[zone.ControlPointIndex] += GetCaptureProgressStrength(player);
                    }
                }
            }
        }

        for (var index = 0; index < _host.Objectives.ControlPoints.Points.Count; index += 1)
        {
            var point = _host.Objectives.ControlPoints.Points[index];
            var previousRedCappers = point.RedCappers;
            var previousBlueCappers = point.BlueCappers;
            var redCappers = redCapStrengthByPoint[index];
            var blueCappers = blueCapStrengthByPoint[index];
            var redPlayers = redPlayersByPoint[index].Count;
            var bluePlayers = bluePlayersByPoint[index].Count;
            point.RedCappers = redCappers;
            point.BlueCappers = blueCappers;

            var defended = IsControlPointDefended(redCappers, blueCappers, redPlayers, bluePlayers);
            PlayerTeam? capTeam = null;
            var cappers = 0;

            if (redCappers > 0 && bluePlayers == 0 && point.Team != PlayerTeam.Red)
            {
                capTeam = PlayerTeam.Red;
                cappers = redCappers;
            }
            else if (blueCappers > 0 && redPlayers == 0 && point.Team != PlayerTeam.Blue)
            {
                capTeam = PlayerTeam.Blue;
                cappers = blueCappers;
            }

            if (point.CappingTicks > 0f && capTeam.HasValue && point.CappingTeam != capTeam)
            {
                cappers = 0;
                ClearCaptureParticipants(point);
            }
            else if (point.CappingTicks > 0f && point.CappingTeam != capTeam)
            {
                cappers = 0;
            }
            else if (point.Team.HasValue && capTeam == point.Team.Value)
            {
                cappers = 0;
                ClearCaptureParticipants(point);
            }

            if (_host.Objectives.ControlPoints.SetupMode && capTeam == PlayerTeam.Blue)
            {
                cappers = 0;
                ClearCaptureParticipants(point);
            }

            point.Cappers = cappers;

            var capStrength = GetCaptureProgressPerTick(cappers);

            if (_host.Level.ControlPointSettings.OverrideInitialOwnership)
            {
                var isLocked = point.IsLocked;
                ControlPointLockDependencyMetadata.ApplyMapLockTriggers(
                    point.Marker.LockRules,
                    _host.Objectives.ControlPoints.Points,
                    _host.Level.LogicGraph,
                    ref isLocked);
                point.IsLocked = isLocked;
            }
            else
            {
                point.IsLocked = IsControlPointLocked(point);
            }

            if (!point.IsLocked)
            {
                var previousTotal = previousRedCappers + previousBlueCappers;
                var currentTotal = redCappers + blueCappers;
                if (previousTotal == 0 && currentTotal > 0 && capTeam.HasValue && (!point.Team.HasValue || point.Team.Value != capTeam.Value))
                {
                    _host.WorldEffects.RegisterWorldSoundEvent("CPBeginCapSnd", point.Marker.CenterX, point.Marker.CenterY);
                }

                if (point.Team == PlayerTeam.Red && previousBlueCappers > 0 && previousRedCappers == 0 && redCappers > 0)
                {
                    _host.WorldEffects.RegisterWorldSoundEvent("CPDefendedSnd", point.Marker.CenterX, point.Marker.CenterY);
                    _host.KillFeed.RecordControlPointDefendedObjectiveLog(PlayerTeam.Red, redCappersByPoint[index]);
                }
                else if (point.Team == PlayerTeam.Blue && previousRedCappers > 0 && previousBlueCappers == 0 && blueCappers > 0)
                {
                    _host.WorldEffects.RegisterWorldSoundEvent("CPDefendedSnd", point.Marker.CenterX, point.Marker.CenterY);
                    _host.KillFeed.RecordControlPointDefendedObjectiveLog(PlayerTeam.Blue, blueCappersByPoint[index]);
                }
            }

            if (point.IsLocked)
            {
                point.CappingTicks = 0f;
                point.CappingTeam = null;
                ClearCaptureParticipants(point);
                continue;
            }

            if (capTeam.HasValue && cappers > 0 && point.CappingTicks < point.CapTimeTicks)
            {
                TrackCaptureParticipants(point, capTeam.Value, redCappersByPoint[index], blueCappersByPoint[index]);
                point.CappingTicks += _host.IsVipModeActive
                    ? capStrength
                    : capStrength * _host.ConfiguredCaptureSpeedMultiplierPerPlayer;
                point.CappingTeam = capTeam;
            }
            else if (point.CappingTicks > 0f
                && cappers == 0
                && !defended
                && !IsCaptureDecayPausedByVipTeammates(point, index, redVipDecayBlockersByPoint, blueVipDecayBlockersByPoint))
            {
                point.CappingTicks -= 1f;
                if (point.Team == PlayerTeam.Blue)
                {
                    point.CappingTicks -= blueReverseStrengthByPoint[index] * 0.5f;
                }
                else if (point.Team == PlayerTeam.Red)
                {
                    point.CappingTicks -= redReverseStrengthByPoint[index] * 0.5f;
                }
            }

            if (point.CappingTicks <= 0f)
            {
                point.CappingTicks = 0f;
                point.CappingTeam = null;
                ClearCaptureParticipants(point);
                continue;
            }

            if (point.CappingTeam.HasValue && point.CappingTicks >= point.CapTimeTicks)
            {
                CaptureControlPoint(point, index, point.CappingTeam.Value, redCappersByPoint, blueCappersByPoint);
            }
        }
    }

    private int GetCaptureProgressStrength(PlayerEntity player)
    {
        return _host.IsVipModeActive ? VipCaptureStrength : GetControlPointCapStrength(player);
    }

    private float GetCaptureProgressPerTick(int cappers)
    {
        if (_host.IsVipModeActive)
        {
            return cappers;
        }

        var capStrength = 0f;
        for (var strengthIndex = 1; strengthIndex <= cappers; strengthIndex += 1)
        {
            capStrength += strengthIndex <= 2 ? 1f : 0.5f;
        }

        return capStrength;
    }

    private bool IsControlPointDefended(int redCappers, int blueCappers, int redPlayers, int bluePlayers)
    {
        if (_host.IsVipModeActive)
        {
            return (redCappers > 0 && bluePlayers > 0)
                || (blueCappers > 0 && redPlayers > 0);
        }

        return redPlayers > 0 && bluePlayers > 0;
    }

    private int GetControlPointCapStrength(PlayerEntity player)
    {
        if (player.ClassId == PlayerClass.Scout)
        {
            return 2;
        }

        if (_host.LastToDieRules.GetLastToDieGameplaySettings(player).EnableSoldierFastCapture
            && player.ClassId == PlayerClass.Soldier
            && _host.ExperimentalRules.IsExperimentalPracticePowerOwner(player))
        {
            return 2;
        }

        if (_host.LastToDieRules.GetLastToDieGameplaySettings(player).EnableDemoknightFastCapture
            && player.ClassId == PlayerClass.Demoman
            && player.IsExperimentalDemoknightEnabled
            && _host.ExperimentalRules.IsExperimentalPracticePowerOwner(player))
        {
            return 2;
        }

        return 1;
    }

    private static bool IsCaptureDecayPausedByVipTeammates(
        ControlPointState point,
        int pointIndex,
        HashSet<int>[] redVipDecayBlockersByPoint,
        HashSet<int>[] blueVipDecayBlockersByPoint)
    {
        return point.CappingTeam switch
        {
            PlayerTeam.Red => redVipDecayBlockersByPoint[pointIndex].Count > 0,
            PlayerTeam.Blue => blueVipDecayBlockersByPoint[pointIndex].Count > 0,
            _ => false,
        };
    }

    private bool IsIgnoringPlayerForControlPointCapture(PlayerEntity player)
    {
        if (player.IsMedicRegularUberDeliveryActive)
        {
            return !_host.LastToDieRules.CanPlayerCaptureControlPointsWhileUbered(player);
        }

        if (!player.IsUbered)
        {
            return false;
        }

        return !_host.LastToDieRules.GetLastToDieGameplaySettings(player).EnableSoldierRageCaptureDuringRage
            || !player.IsRaging
            || player.ClassId != PlayerClass.Soldier
            || !_host.ExperimentalRules.IsExperimentalPracticePowerOwner(player);
    }

    private static void TrackCaptureParticipants(
        ControlPointState point,
        PlayerTeam team,
        HashSet<int> redCappers,
        HashSet<int> blueCappers)
    {
        var participants = team == PlayerTeam.Red
            ? point.RedCaptureParticipantIds
            : point.BlueCaptureParticipantIds;
        var currentCappers = team == PlayerTeam.Red
            ? redCappers
            : blueCappers;

        foreach (var playerId in currentCappers)
        {
            participants.Add(playerId);
        }
    }

    private static void ClearCaptureParticipants(ControlPointState point)
    {
        point.RedCaptureParticipantIds.Clear();
        point.BlueCaptureParticipantIds.Clear();
    }

    private bool IsControlPointLocked(ControlPointState point)
    {
        if (IsKothMode(_host.MatchRules.Mode))
        {
            if (_host.Objectives.Koth.UnlockTicksRemaining > 0)
            {
                return true;
            }

            if (_host.MatchRules.Mode == GameModeKind.KingOfTheHill)
            {
                return false;
            }

            if (point.Marker.IsRedKothControlPoint())
            {
                return GetDualKothPoint(PlayerTeam.Blue)?.Team == PlayerTeam.Red;
            }

            if (point.Marker.IsBlueKothControlPoint())
            {
                return GetDualKothPoint(PlayerTeam.Red)?.Team == PlayerTeam.Blue;
            }

            return false;
        }

        if (!point.Team.HasValue)
        {
            return false;
        }

        if (point.Team == PlayerTeam.Blue)
        {
            if (point.Index > 1)
            {
                var previous = _host.Objectives.ControlPoints.Points[point.Index - 2];
                if (previous.Team != PlayerTeam.Red)
                {
                    return true;
                }
            }
        }
        else if (point.Team == PlayerTeam.Red)
        {
            if (point.Index < _host.Objectives.ControlPoints.Points.Count)
            {
                var next = _host.Objectives.ControlPoints.Points[point.Index];
                if (next.Team != PlayerTeam.Blue)
                {
                    return true;
                }
            }

            if (_host.Objectives.ControlPoints.SetupMode)
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
        HashSet<int>[] redCappersByPoint,
        HashSet<int>[] blueCappersByPoint)
    {
        point.Team = team;
        point.CappingTicks = 0f;
        point.CappingTeam = null;
        point.Cappers = 0;
        point.RedCappers = 0;
        point.BlueCappers = 0;
        point.HasHealingAura = _host.LastToDieRules.IsLastToDieGameplaySettingEnabled(settings => settings.EnableCapturedPointHealingAura)
            && team == PlayerTeam.Red;

        var finalCapperIds = team == PlayerTeam.Red ? redCappersByPoint[pointIndex] : blueCappersByPoint[pointIndex];
        var participantIds = team == PlayerTeam.Red ? point.RedCaptureParticipantIds : point.BlueCaptureParticipantIds;
        var capperIds = new HashSet<int>(participantIds);
        foreach (var playerId in finalCapperIds)
        {
            capperIds.Add(playerId);
        }

        if (capperIds.Count > 0)
        {
            foreach (var player in _host.EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.Team != team || !capperIds.Contains(player.Id))
                {
                    continue;
                }

                player.AddCap();
                _host.Scorekeeping.AwardObjectiveCapturePoints(player);
            }
        }

        _host.KillFeed.RecordControlPointCapturedObjectiveLog(team, capperIds);
        ClearCaptureParticipants(point);

        if (_host.Objectives.ControlPoints.SetupMode)
        {
            var updatedTimeRemainingTicks = Math.Min(
                GetControlPointMaximumTimeTicks(),
                _host.MatchState.TimeRemainingTicks + GetControlPointCaptureBonusTicks());
            _host.MatchState = _host.MatchState with { TimeRemainingTicks = updatedTimeRemainingTicks };
        }

        _host.WorldEffects.RegisterWorldSoundEvent("CPCapturedSnd", point.Marker.CenterX, point.Marker.CenterY);
        _host.WorldEffects.RegisterWorldSoundEvent("IntelPutSnd", point.Marker.CenterX, point.Marker.CenterY);
        _host.MapLogic.EvaluateMapLogicGraph(resetStatefulNodes: false);
    }
}
