using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const int ControlPointSetupDurationSeconds = 30;
    private const int ControlPointAttackTimeLimitMinutes = 3;

    private sealed record ControlPointZone(RoomObjectMarker Marker, int ControlPointIndex);

    public int ControlPointSetupDurationTicks => GetControlPointSetupDurationTicks();

    private int GetControlPointSetupDurationTicks()
    {
        return Math.Max(1, Config.TicksPerSecond * ControlPointSetupDurationSeconds);
    }

    private int GetControlPointAttackTimeLimitTicks()
    {
        return Math.Max(1, ControlPointAttackTimeLimitMinutes * Config.TicksPerSecond * 60);
    }

    private void ApplyControlPointSetupMatchRules()
    {
        if (!_controlPointSetupMode)
        {
            return;
        }

        MatchRules = MatchRules with
        {
            TimeLimitMinutes = ControlPointAttackTimeLimitMinutes,
            TimeLimitTicks = GetControlPointAttackTimeLimitTicks(),
        };
    }

    private static class ControlPointSetupSystem
    {
        public static void ResetForNewRound(SimulationWorld world)
        {
            if (SimulationWorld.IsKothMode(world.MatchRules.Mode))
            {
                world.ResetKothStateForNewRound();
                return;
            }

            InitializeForLevel(world);
            var hasSetupGates = world.Level.GetRoomObjects(RoomObjectType.ControlPointSetupGate).Count > 0;
            if (world._controlPoints.Count == 0)
            {
                world._controlPointSetupMode = hasSetupGates;
                world.ApplyControlPointSetupMatchRules();
                world._controlPointSetupTicksRemaining = hasSetupGates ? world.GetControlPointSetupDurationTicks() : 0;
                UpdateSetupGates(world);
                return;
            }

            world._controlPointSetupMode = hasSetupGates;
            world.ApplyControlPointSetupMatchRules();
            world._controlPointSetupTicksRemaining = world._controlPointSetupMode ? world.GetControlPointSetupDurationTicks() : 0;
            UpdateSetupGates(world);

            AssignCapTimes(world);
            AssignOwnership(world);
            ResetCappingState(world);
        }

        public static void UpdateSetupGates(SimulationWorld world)
        {
            world.Level.ControlPointSetupGatesActive = world._controlPointSetupMode && world._controlPointSetupTicksRemaining > 0;
        }

        public static void InitializeForLevel(SimulationWorld world, bool evaluateLogicGraph = true)
        {
            world._controlPoints.Clear();
            world._controlPointZones.Clear();

            var markers = world.MatchRules.Mode == GameModeKind.Arena
                ? world.Level.GetRoomObjects(RoomObjectType.ArenaControlPoint)
                : world.Level.GetRoomObjects(RoomObjectType.ControlPoint);
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
            if (evaluateLogicGraph)
            {
                world.EvaluateMapLogicGraph();
            }
        }

        private static List<RoomObjectMarker> OrderMarkers(IReadOnlyList<RoomObjectMarker> markers)
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
                    var point = world._controlPoints[closestIndex];
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

        private static void AssignCapTimes(SimulationWorld world)
        {
            var total = world._controlPoints.Count;
            if (total == 0)
            {
                return;
            }

            for (var index = 0; index < world._controlPoints.Count; index += 1)
            {
                var point = world._controlPoints[index];
                var (storedMultiplier, isCustom) = point.Marker.CapTimeMultiplierSettings;
                point.CapTimeTicks = ControlPointCapTimeMultiplierMetadata.ResolveCapTimeTicks(
                    total,
                    point.Index,
                    world._controlPointSetupMode,
                    storedMultiplier,
                    isCustom);
            }
        }

        private static void AssignOwnership(SimulationWorld world)
        {
            var totalPoints = world._controlPoints.Count;
            for (var index = 0; index < totalPoints; index += 1)
            {
                var point = world._controlPoints[index];
                var context = new ControlPointOwnershipContext(
                    point.Index,
                    totalPoints,
                    world._controlPointSetupMode,
                    world.MatchRules.Mode,
                    world.Level.ControlPointSettings.OverrideInitialOwnership);
                point.Team = ControlPointOwnershipResolver.ResolveInitialTeam(point.Marker, in context);
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
                point.RedCaptureParticipantIds.Clear();
                point.BlueCaptureParticipantIds.Clear();
                point.IsLocked = world.Level.ControlPointSettings.OverrideInitialOwnership
                    ? ControlPointLockDependencyMetadata.GetInitialLocked(point.Marker.LockRules)
                    : false;
                point.HasHealingAura = false;
            }
        }
    }
}
