using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private static class ControlPointStateSystem
    {
        private const int VipCaptureStrength = 5;

        public static void Update(SimulationWorld world)
        {
            if (world.MatchState.IsEnded || world._controlPoints.Count == 0)
            {
                return;
            }

            world.RefreshMapLogicRuntimeIfControlPointInputsChanged();
            world.TickMapLogicTimers();

            var redCappersByPoint = new HashSet<int>[world._controlPoints.Count];
            var blueCappersByPoint = new HashSet<int>[world._controlPoints.Count];
            var redPlayersByPoint = new HashSet<int>[world._controlPoints.Count];
            var bluePlayersByPoint = new HashSet<int>[world._controlPoints.Count];
            var redVipDecayBlockersByPoint = new HashSet<int>[world._controlPoints.Count];
            var blueVipDecayBlockersByPoint = new HashSet<int>[world._controlPoints.Count];
            var redCapStrengthByPoint = new int[world._controlPoints.Count];
            var blueCapStrengthByPoint = new int[world._controlPoints.Count];
            var redReverseStrengthByPoint = new int[world._controlPoints.Count];
            var blueReverseStrengthByPoint = new int[world._controlPoints.Count];
            for (var index = 0; index < world._controlPoints.Count; index += 1)
            {
                redCappersByPoint[index] = new HashSet<int>();
                blueCappersByPoint[index] = new HashSet<int>();
                redPlayersByPoint[index] = new HashSet<int>();
                bluePlayersByPoint[index] = new HashSet<int>();
                redVipDecayBlockersByPoint[index] = new HashSet<int>();
                blueVipDecayBlockersByPoint[index] = new HashSet<int>();
            }

            foreach (var player in world.EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive
                    || player.IsSpyCloaked
                    || IsIgnoringPlayerForCapture(world, player)
                    || !world.CanPlayerAffectControlPointInVipMode(player))
                {
                    continue;
                }

                if (world.ExperimentalGameplaySettings.EnableSoldierRageCaptureLockout
                    && world.LocalPlayer.IsRaging
                    && player.Team != world.LocalPlayer.Team)
                {
                    continue;
                }

                for (var zoneIndex = 0; zoneIndex < world._controlPointZones.Count; zoneIndex += 1)
                {
                    var zone = world._controlPointZones[zoneIndex];
                    if (!player.IntersectsMarker(zone.Marker.CenterX, zone.Marker.CenterY, zone.Marker.Width, zone.Marker.Height))
                    {
                        continue;
                    }

                    var canProgressCapture = world.CanPlayerCaptureInVipMode(player);
                    var reverseStrength = GetCapStrength(world, player);
                    var pausesVipDecay = world.CanPlayerPauseVipCaptureDecay(player);
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
                            redCapStrengthByPoint[zone.ControlPointIndex] += GetCaptureProgressStrength(world, player);
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
                            blueCapStrengthByPoint[zone.ControlPointIndex] += GetCaptureProgressStrength(world, player);
                        }
                    }
                }
            }

            for (var index = 0; index < world._controlPoints.Count; index += 1)
            {
                var point = world._controlPoints[index];
                var previousRedCappers = point.RedCappers;
                var previousBlueCappers = point.BlueCappers;
                var redCappers = redCapStrengthByPoint[index];
                var blueCappers = blueCapStrengthByPoint[index];
                var redPlayers = redPlayersByPoint[index].Count;
                var bluePlayers = bluePlayersByPoint[index].Count;
                point.RedCappers = redCappers;
                point.BlueCappers = blueCappers;

                var defended = IsPointDefended(world, redCappers, blueCappers, redPlayers, bluePlayers);
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

                if (world._controlPointSetupMode && capTeam == PlayerTeam.Blue)
                {
                    cappers = 0;
                    ClearCaptureParticipants(point);
                }

                point.Cappers = cappers;

                var capStrength = GetCaptureProgressPerTick(world, cappers);

                if (world.Level.ControlPointSettings.OverrideInitialOwnership)
                {
                    var isLocked = point.IsLocked;
                    ControlPointLockDependencyMetadata.ApplyMapLockTriggers(
                        point.Marker.LockRules,
                        world._controlPoints,
                        world.Level.LogicGraph,
                        ref isLocked);
                    point.IsLocked = isLocked;
                }
                else
                {
                    point.IsLocked = IsLocked(world, point);
                }

                if (!point.IsLocked)
                {
                    var previousTotal = previousRedCappers + previousBlueCappers;
                    var currentTotal = redCappers + blueCappers;
                    if (previousTotal == 0 && currentTotal > 0 && capTeam.HasValue && (!point.Team.HasValue || point.Team.Value != capTeam.Value))
                    {
                        world.RegisterWorldSoundEvent("CPBeginCapSnd", point.Marker.CenterX, point.Marker.CenterY);
                    }

                    if (point.Team == PlayerTeam.Red && previousBlueCappers > 0 && previousRedCappers == 0 && redCappers > 0)
                    {
                        world.RegisterWorldSoundEvent("CPDefendedSnd", point.Marker.CenterX, point.Marker.CenterY);
                        world.RecordControlPointDefendedObjectiveLog(PlayerTeam.Red, redCappersByPoint[index]);
                    }
                    else if (point.Team == PlayerTeam.Blue && previousRedCappers > 0 && previousBlueCappers == 0 && blueCappers > 0)
                    {
                        world.RegisterWorldSoundEvent("CPDefendedSnd", point.Marker.CenterX, point.Marker.CenterY);
                        world.RecordControlPointDefendedObjectiveLog(PlayerTeam.Blue, blueCappersByPoint[index]);
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
                    point.CappingTicks += capStrength;
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
                    CapturePoint(world, point, index, point.CappingTeam.Value, redCappersByPoint, blueCappersByPoint);
                }
            }
        }

        private static int GetCaptureProgressStrength(SimulationWorld world, PlayerEntity player)
        {
            return world.IsVipModeActive ? VipCaptureStrength : GetCapStrength(world, player);
        }

        private static float GetCaptureProgressPerTick(SimulationWorld world, int cappers)
        {
            if (world.IsVipModeActive)
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

        private static bool IsPointDefended(SimulationWorld world, int redCappers, int blueCappers, int redPlayers, int bluePlayers)
        {
            if (world.IsVipModeActive)
            {
                return (redCappers > 0 && bluePlayers > 0)
                    || (blueCappers > 0 && redPlayers > 0);
            }

            return redPlayers > 0 && bluePlayers > 0;
        }

        private static int GetCapStrength(SimulationWorld world, PlayerEntity player)
        {
            if (player.ClassId == PlayerClass.Scout)
            {
                return 2;
            }

            if (world.ExperimentalGameplaySettings.EnableSoldierFastCapture
                && player.ClassId == PlayerClass.Soldier
                && world.IsExperimentalPracticePowerOwner(player))
            {
                return 2;
            }

            if (world.ExperimentalGameplaySettings.EnableDemoknightFastCapture
                && player.ClassId == PlayerClass.Demoman
                && player.IsExperimentalDemoknightEnabled
                && world.IsExperimentalPracticePowerOwner(player))
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

        private static bool IsIgnoringPlayerForCapture(SimulationWorld world, PlayerEntity player)
        {
            if (!player.IsUbered)
            {
                return false;
            }

            return !world.ExperimentalGameplaySettings.EnableSoldierRageCaptureDuringRage
                || !player.IsRaging
                || player.ClassId != PlayerClass.Soldier
                || !world.IsExperimentalPracticePowerOwner(player);
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

        private static bool IsLocked(SimulationWorld world, ControlPointState point)
        {
            if (SimulationWorld.IsKothMode(world.MatchRules.Mode))
            {
                if (world._kothUnlockTicksRemaining > 0)
                {
                    return true;
                }

                if (world.MatchRules.Mode == GameModeKind.KingOfTheHill)
                {
                    return false;
                }

                if (point.Marker.IsRedKothControlPoint())
                {
                    return world.GetDualKothPoint(PlayerTeam.Blue)?.Team == PlayerTeam.Red;
                }

                if (point.Marker.IsBlueKothControlPoint())
                {
                    return world.GetDualKothPoint(PlayerTeam.Red)?.Team == PlayerTeam.Blue;
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
                    var previous = world._controlPoints[point.Index - 2];
                    if (previous.Team != PlayerTeam.Red)
                    {
                        return true;
                    }
                }
            }
            else if (point.Team == PlayerTeam.Red)
            {
                if (point.Index < world._controlPoints.Count)
                {
                    var next = world._controlPoints[point.Index];
                    if (next.Team != PlayerTeam.Blue)
                    {
                        return true;
                    }
                }

                if (world._controlPointSetupMode)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CapturePoint(
            SimulationWorld world,
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
            point.HasHealingAura = world.ExperimentalGameplaySettings.EnableCapturedPointHealingAura
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
                foreach (var player in world.EnumerateSimulatedPlayers())
                {
                    if (!player.IsAlive || player.Team != team || !capperIds.Contains(player.Id))
                    {
                        continue;
                    }

                    player.AddCap();
                    world.AwardObjectiveCapturePoints(player);
                }
            }

            world.RecordControlPointCapturedObjectiveLog(team, capperIds);
            ClearCaptureParticipants(point);

            if (world._controlPointSetupMode)
            {
                var bonusTicks = world.Config.TicksPerSecond * 60 * 5;
                world.MatchState = world.MatchState with { TimeRemainingTicks = world.MatchState.TimeRemainingTicks + bonusTicks };
            }

            world.RegisterWorldSoundEvent("CPCapturedSnd", point.Marker.CenterX, point.Marker.CenterY);
            world.RegisterWorldSoundEvent("IntelPutSnd", point.Marker.CenterX, point.Marker.CenterY);
            world.EvaluateMapLogicGraph(resetStatefulNodes: false);
        }
    }
}
