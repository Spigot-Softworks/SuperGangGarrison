using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private static class ControlPointStateSystem
    {
        public static void Update(SimulationWorld world)
        {
            if (world.MatchState.IsEnded || world._controlPoints.Count == 0)
            {
                return;
            }

            var redCappersByPoint = new HashSet<int>[world._controlPoints.Count];
            var blueCappersByPoint = new HashSet<int>[world._controlPoints.Count];
            var redCapStrengthByPoint = new int[world._controlPoints.Count];
            var blueCapStrengthByPoint = new int[world._controlPoints.Count];
            for (var index = 0; index < world._controlPoints.Count; index += 1)
            {
                redCappersByPoint[index] = new HashSet<int>();
                blueCappersByPoint[index] = new HashSet<int>();
            }

            foreach (var player in world.EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.IsSpyCloaked || player.IsUbered)
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

                    if (player.Team == PlayerTeam.Red)
                    {
                        if (redCappersByPoint[zone.ControlPointIndex].Add(player.Id))
                        {
                            redCapStrengthByPoint[zone.ControlPointIndex] += GetCapStrength(player);
                        }
                    }
                    else if (blueCappersByPoint[zone.ControlPointIndex].Add(player.Id))
                    {
                        blueCapStrengthByPoint[zone.ControlPointIndex] += GetCapStrength(player);
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

                if (world._controlPointSetupMode && capTeam == PlayerTeam.Blue)
                {
                    cappers = 0;
                }

                point.Cappers = cappers;

                var capStrength = 0f;
                for (var strengthIndex = 1; strengthIndex <= cappers; strengthIndex += 1)
                {
                    capStrength += strengthIndex <= 2 ? 1f : 0.5f;
                }

                point.IsLocked = IsLocked(world, point);

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
                    CapturePoint(world, point, index, point.CappingTeam.Value, redCappersByPoint, blueCappersByPoint);
                }
            }
        }

        private static int GetCapStrength(PlayerEntity player)
        {
            return player.ClassId == PlayerClass.Scout ? 2 : 1;
        }

        private static bool IsLocked(SimulationWorld world, ControlPointState point)
        {
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

            var capperIds = team == PlayerTeam.Red ? redCappersByPoint[pointIndex] : blueCappersByPoint[pointIndex];
            if (capperIds.Count > 0)
            {
                foreach (var player in world.EnumerateSimulatedPlayers())
                {
                    if (!player.IsAlive || player.Team != team || !capperIds.Contains(player.Id))
                    {
                        continue;
                    }

                    player.AddCap();
                }
            }

            world.RecordControlPointCapturedObjectiveLog(team, capperIds);

            if (world._controlPointSetupMode)
            {
                var bonusTicks = world.Config.TicksPerSecond * 60 * 5;
                world.MatchState = world.MatchState with { TimeRemainingTicks = world.MatchState.TimeRemainingTicks + bonusTicks };
            }

            world.RegisterWorldSoundEvent("CPCapturedSnd", point.Marker.CenterX, point.Marker.CenterY);
            world.RegisterWorldSoundEvent("IntelPutSnd", point.Marker.CenterX, point.Marker.CenterY);
        }
    }
}
