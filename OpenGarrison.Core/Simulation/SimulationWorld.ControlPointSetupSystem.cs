using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const int ControlPointSetupTicksDefault = 1800;

    private sealed record ControlPointZone(RoomObjectMarker Marker, int ControlPointIndex);

    private static class ControlPointSetupSystem
    {
        public static void ResetForNewRound(SimulationWorld world)
        {
            InitializeForLevel(world);
            var hasSetupGates = world.Level.GetRoomObjects(RoomObjectType.ControlPointSetupGate).Count > 0;
            if (world._controlPoints.Count == 0)
            {
                world._controlPointSetupMode = hasSetupGates;
                world._controlPointSetupTicksRemaining = hasSetupGates ? ControlPointSetupTicksDefault : 0;
                UpdateSetupGates(world);
                return;
            }

            world._controlPointSetupMode = hasSetupGates;
            world._controlPointSetupTicksRemaining = world._controlPointSetupMode ? ControlPointSetupTicksDefault : 0;
            UpdateSetupGates(world);

            AssignCapTimes(world);
            AssignOwnership(world);
            ResetCappingState(world);
        }

        public static void UpdateSetupGates(SimulationWorld world)
        {
            world.Level.ControlPointSetupGatesActive = world._controlPointSetupMode && world._controlPointSetupTicksRemaining > 0;
        }

        public static void InitializeForLevel(SimulationWorld world)
        {
            world._controlPoints.Clear();
            world._controlPointZones.Clear();

            var markers = world.Level.GetRoomObjects(RoomObjectType.ControlPoint);
            if (markers.Count == 0)
            {
                return;
            }

            var orderedMarkers = OrderMarkers(markers);
            for (var index = 0; index < orderedMarkers.Count; index += 1)
            {
                var marker = orderedMarkers[index];
                world._controlPoints.Add(new ControlPointState(index + 1, marker));
            }

            BuildZones(world);
        }

        private static List<RoomObjectMarker> OrderMarkers(IReadOnlyList<RoomObjectMarker> markers)
        {
            var withIndex = new List<(int Index, RoomObjectMarker Marker)>();
            var hasExplicitIndex = false;

            foreach (var marker in markers)
            {
                if (TryParseIndex(marker, out var index))
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

        private static bool TryParseIndex(RoomObjectMarker marker, out int index)
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

        private static void BuildZones(SimulationWorld world)
        {
            var zones = world.Level.GetRoomObjects(RoomObjectType.CaptureZone);
            if (zones.Count == 0 || world._controlPoints.Count == 0)
            {
                return;
            }

            for (var zoneIndex = 0; zoneIndex < zones.Count; zoneIndex += 1)
            {
                var zone = zones[zoneIndex];
                var closestIndex = -1;
                var closestDistance = float.MaxValue;

                for (var pointIndex = 0; pointIndex < world._controlPoints.Count; pointIndex += 1)
                {
                    var point = world._controlPoints[pointIndex];
                    var distance = SimulationWorld.DistanceBetween(zone.CenterX, zone.CenterY, point.Marker.CenterX, point.Marker.CenterY);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestIndex = pointIndex;
                    }
                }

                if (closestIndex >= 0)
                {
                    world._controlPointZones.Add(new ControlPointZone(zone, closestIndex));
                }
            }
        }

        private static void AssignCapTimes(SimulationWorld world)
        {
            var total = world._controlPoints.Count;
            if (total == 0)
            {
                return;
            }

            var baseTime = world._controlPointSetupMode ? 6 * 30f : 7 * 30f;

            for (var index = 0; index < world._controlPoints.Count; index += 1)
            {
                var point = world._controlPoints[index];
                point.CapTimeTicks = Math.Max(1, (int)MathF.Round(GetCapTime(total, point.Index, baseTime, world._controlPointSetupMode)));
            }
        }

        private static float GetCapTime(int totalPoints, int pointIndex, float baseTime, bool setupMode)
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

        private static void AssignOwnership(SimulationWorld world)
        {
            for (var index = 0; index < world._controlPoints.Count; index += 1)
            {
                world._controlPoints[index].Team = null;
            }

            if (world._controlPoints.Count <= 1)
            {
                return;
            }

            if (world._controlPointSetupMode)
            {
                for (var index = 0; index < world._controlPoints.Count; index += 1)
                {
                    world._controlPoints[index].Team = PlayerTeam.Blue;
                }

                return;
            }

            var middlePoint = world._controlPoints.Count / 2f;
            var middleCeiling = (int)MathF.Ceiling(middlePoint);
            for (var index = 0; index < world._controlPoints.Count; index += 1)
            {
                var point = world._controlPoints[index];
                point.Team = point.Index <= middlePoint ? PlayerTeam.Red : PlayerTeam.Blue;

                if (world._controlPoints.Count > 2 && point.Index == middleCeiling)
                {
                    point.Team = null;
                }
            }
        }

        private static void ResetCappingState(SimulationWorld world)
        {
            for (var index = 0; index < world._controlPoints.Count; index += 1)
            {
                var point = world._controlPoints[index];
                point.CappingTicks = 0f;
                point.CappingTeam = null;
                point.Cappers = 0;
                point.RedCappers = 0;
                point.BlueCappers = 0;
                point.IsLocked = false;
            }
        }
    }
}
