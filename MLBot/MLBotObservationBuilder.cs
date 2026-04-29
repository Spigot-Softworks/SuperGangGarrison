using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;
using System.Runtime.CompilerServices;

namespace OpenGarrison.MLBot;

public static class MLBotObservationBuilder
{
    private const float ProbeStep = 6f;
    private const float HorizontalProbeLimit = 48f;
    private const float VerticalProbeLimit = 48f;
    private const float LandingSearchHorizontalLimit = 320f;
    private const float LandingSearchUpLimit = 384f;
    private const float LandingSearchDropLimit = 160f;
    private const float LandingSearchStep = 8f;
    private static readonly ConditionalWeakTable<SimpleLevel, TerrainAffordanceSurfaceIndex> TerrainSurfaceIndices = new();

    public static MLBotObservation Build(
        SimulationWorld world,
        byte slot,
        PlayerEntity player,
        MLBotTaskPhase taskPhase,
        MLBotObservationRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(runtimeState);

        var (objective, controlPoint) = ResolveObjective(world, player, taskPhase);
        var objectiveDistance = objective.HasObjective
            ? MathF.Sqrt((objective.RelativeX * objective.RelativeX) + (objective.RelativeY * objective.RelativeY))
            : 0f;
        var objectiveDistanceDelta = runtimeState.PreviousObjectiveDistance <= 0f
            ? 0f
            : runtimeState.PreviousObjectiveDistance - objectiveDistance;

        var waypoint = MLBotWaypointResolver.Resolve(world, player, taskPhase, objective, runtimeState, out var traversal);
        var controlPointObjective = BuildControlPointObjective(world, player, controlPoint, runtimeState);
        var probes = BuildProbeSnapshot(world, player);
        var terrainAffordance = BuildTerrainAffordanceSnapshot(world, player, objective);
        var nearestVisibleEnemy = FindNearestActor(world, player, sameTeam: false);
        var nearestVisibleTeammate = FindNearestActor(world, player, sameTeam: true);
        var owner = controlPointObjective.HasObjective ? controlPointObjective.Owner : 0;
        var cappingTeam = controlPointObjective.HasObjective ? controlPointObjective.CappingTeam : 0;
        var captureProgress = controlPointObjective.HasObjective ? controlPointObjective.CaptureProgress : 0f;
        var isLocked = controlPointObjective.HasObjective && controlPointObjective.IsLocked;
        var hasPreviousSample = runtimeState.HasPreviousSample;

        return new MLBotObservation(
            Slot: slot,
            LevelName: world.Level.Name,
            Mode: world.MatchRules.Mode,
            Team: player.Team,
            ClassId: player.ClassId,
            TaskPhase: taskPhase,
            BotX: player.X,
            BotY: player.Y,
            VelocityX: player.HorizontalSpeed,
            VelocityY: player.VerticalSpeed,
            IsGrounded: player.IsGrounded,
            FacingDirectionX: player.FacingDirectionX,
            Health: player.Health,
            MaxHealth: player.MaxHealth,
            IsCarryingIntel: player.IsCarryingIntel,
            Objective: objective,
            Waypoint: waypoint,
            Traversal: traversal,
            ControlPointObjective: controlPointObjective,
            Probes: probes,
            NearestVisibleEnemy: nearestVisibleEnemy,
            NearestVisibleTeammate: nearestVisibleTeammate,
            StuckTicks: runtimeState.StuckTicks,
            ObjectiveDistance: objectiveDistance,
            ObjectiveDistanceDelta: objectiveDistanceDelta,
            ControlPointOwner: owner,
            ControlPointCappingTeam: cappingTeam,
            ControlPointCaptureProgress: captureProgress,
            ControlPointLocked: isLocked,
            IsRespawning: !player.IsAlive,
            RemainingAirJumps: player.RemainingAirJumps,
            MaxAirJumps: player.MaxAirJumps,
            RunPower: player.RunPower,
            MaxRunSpeed: player.MaxRunSpeed,
            GroundAcceleration: player.GroundAcceleration,
            GroundDeceleration: player.GroundDeceleration,
            Gravity: player.Gravity,
            JumpSpeed: player.JumpSpeed,
            Width: player.Width,
            Height: player.Height,
            PreviousVelocityX: hasPreviousSample ? runtimeState.PreviousVelocityX : 0f,
            PreviousVelocityY: hasPreviousSample ? runtimeState.PreviousVelocityY : 0f,
            PreviousPositionDeltaX: hasPreviousSample ? player.X - runtimeState.PreviousX : 0f,
            PreviousPositionDeltaY: hasPreviousSample ? player.Y - runtimeState.PreviousY : 0f,
            PreviousObjectiveDistanceDelta: hasPreviousSample ? runtimeState.PreviousObjectiveDistanceDelta : 0f,
            PreviousFacingDirectionX: hasPreviousSample ? runtimeState.PreviousFacingDirectionX : 0f,
            PreviousIsGrounded: hasPreviousSample && runtimeState.PreviousIsGrounded,
            AirborneTicks: runtimeState.AirborneTicks,
            JumpTicks: runtimeState.JumpTicks,
            PreviousMoveInput: runtimeState.PreviousMoveInput,
            PreviousJumpPressed: runtimeState.PreviousJumpPressed,
            PreviousJumpHeld: runtimeState.PreviousJumpHeld,
            PreviousDropInput: runtimeState.PreviousDropInput,
            PreviousActionFirePrimary: runtimeState.PreviousActionFirePrimary,
            PreviousActionFireSecondary: runtimeState.PreviousActionFireSecondary,
            PreviousActionDropIntel: runtimeState.PreviousActionDropIntel,
            FramesSinceJumpPressed: runtimeState.FramesSinceJumpPressed,
            FramesSinceJumpReleased: runtimeState.FramesSinceJumpReleased,
            TerrainAffordance: terrainAffordance);
    }

    private static (MLBotObjectiveSnapshot Objective, ControlPointState? ControlPoint) ResolveObjective(
        SimulationWorld world,
        PlayerEntity player,
        MLBotTaskPhase taskPhase)
    {
        var homeIntel = player.Team == PlayerTeam.Red ? world.RedIntel : world.BlueIntel;

        return taskPhase switch
        {
            MLBotTaskPhase.AttackIntel => (CreateObjective(player, GetEnemyIntelState(world, player.Team).X, GetEnemyIntelState(world, player.Team).Y, homeIntel.HomeX, homeIntel.HomeY), null),
            MLBotTaskPhase.ReturnIntel => (CreateObjective(player, homeIntel.HomeX, homeIntel.HomeY, homeIntel.HomeX, homeIntel.HomeY), null),
            MLBotTaskPhase.CaptureObjective => ResolveControlPointObjective(world, player, homeIntel.HomeX, homeIntel.HomeY),
            _ => (CreateObjective(player, player.X, player.Y, homeIntel.HomeX, homeIntel.HomeY, hasObjective: false), null),
        };
    }

    private static TeamIntelligenceState GetEnemyIntelState(SimulationWorld world, PlayerTeam team)
    {
        return team == PlayerTeam.Red ? world.BlueIntel : world.RedIntel;
    }

    private static (MLBotObjectiveSnapshot Objective, ControlPointState? ControlPoint) ResolveControlPointObjective(
        SimulationWorld world,
        PlayerEntity player,
        float homeX,
        float homeY)
    {
        ControlPointState? bestPoint = null;
        var bestDistanceSquared = float.MaxValue;

        for (var index = 0; index < world.ControlPoints.Count; index += 1)
        {
            var point = world.ControlPoints[index];
            if (point.IsLocked && !world.IsKothModeActive)
            {
                continue;
            }

            if (!world.IsKothModeActive && point.Team == player.Team && point.CappingTeam != player.Team)
            {
                continue;
            }

            var dx = point.Marker.CenterX - player.X;
            var dy = point.Marker.CenterY - player.Y;
            var distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestPoint = point;
            }
        }

        if (bestPoint is null)
        {
            return (CreateObjective(player, player.X, player.Y, homeX, homeY, hasObjective: false), null);
        }

        return (CreateObjective(player, bestPoint.Marker.CenterX, bestPoint.Marker.CenterY, homeX, homeY), bestPoint);
    }

    private static MLBotObjectiveSnapshot CreateObjective(PlayerEntity player, float worldX, float worldY, float homeX, float homeY, bool hasObjective = true)
    {
        return new MLBotObjectiveSnapshot(
            HasObjective: hasObjective,
            WorldX: worldX,
            WorldY: worldY,
            RelativeX: worldX - player.X,
            RelativeY: worldY - player.Y,
            HomeX: homeX,
            HomeY: homeY,
            HomeRelativeX: homeX - player.X,
            HomeRelativeY: homeY - player.Y);
    }

    private static MLBotProbeSnapshot BuildProbeSnapshot(SimulationWorld world, PlayerEntity player)
    {
        var facing = player.FacingDirectionX >= 0f ? 1f : -1f;
        var forwardOffset = facing * ProbeStep;
        var footTop = player.Bottom - MathF.Max(6f, player.Height * 0.2f);
        var headBottom = player.Top + MathF.Max(10f, player.Height * 0.35f);

        var footObstacleDistance = MeasureHorizontalClearance(world, player, footTop, player.Bottom, forwardOffset, HorizontalProbeLimit);
        var headObstacleDistance = MeasureHorizontalClearance(world, player, player.Top, headBottom, forwardOffset, HorizontalProbeLimit);
        var leftFootObstacleDistance = MeasureHorizontalClearance(world, player, footTop, player.Bottom, -ProbeStep, HorizontalProbeLimit);
        var leftHeadObstacleDistance = MeasureHorizontalClearance(world, player, player.Top, headBottom, -ProbeStep, HorizontalProbeLimit);
        var rightFootObstacleDistance = MeasureHorizontalClearance(world, player, footTop, player.Bottom, ProbeStep, HorizontalProbeLimit);
        var rightHeadObstacleDistance = MeasureHorizontalClearance(world, player, player.Top, headBottom, ProbeStep, HorizontalProbeLimit);
        var ceilingDistance = MeasureVerticalClearance(world, player, upwards: true, VerticalProbeLimit);
        var groundAheadDistance = MeasureGroundDistance(world, player, facing, HorizontalProbeLimit);
        var dropAheadDepth = MeasureDropDepth(world, player, facing, VerticalProbeLimit);
        var leftGroundDistance = MeasureGroundDistance(world, player, -1f, HorizontalProbeLimit);
        var rightGroundDistance = MeasureGroundDistance(world, player, 1f, HorizontalProbeLimit);
        var leftDropDepth = MeasureDropDepth(world, player, -1f, VerticalProbeLimit);
        var rightDropDepth = MeasureDropDepth(world, player, 1f, VerticalProbeLimit);
        var blockingGateAhead = IsBlockingGateAhead(world, player, facing);
        var touchingLeftWall = world.Level.IntersectsSolid(player.Left - 1f, footTop, player.Left, player.Bottom);
        var touchingRightWall = world.Level.IntersectsSolid(player.Right, footTop, player.Right + 1f, player.Bottom);
        var touchingCeiling = world.Level.IntersectsSolid(player.Left, player.Top - 1f, player.Right, player.Top);

        return new MLBotProbeSnapshot(
            ForwardFootObstacleDistance: footObstacleDistance,
            ForwardHeadObstacleDistance: headObstacleDistance,
            GroundAheadDistance: groundAheadDistance,
            DropAheadDepth: dropAheadDepth,
            CeilingDistance: ceilingDistance,
            LedgeHeightAhead: dropAheadDepth,
            BlockingGateAhead: blockingGateAhead,
            LeftFootObstacleDistance: leftFootObstacleDistance,
            LeftHeadObstacleDistance: leftHeadObstacleDistance,
            RightFootObstacleDistance: rightFootObstacleDistance,
            RightHeadObstacleDistance: rightHeadObstacleDistance,
            LeftGroundDistance: leftGroundDistance,
            RightGroundDistance: rightGroundDistance,
            LeftDropDepth: leftDropDepth,
            RightDropDepth: rightDropDepth,
            TouchingLeftWall: touchingLeftWall,
            TouchingRightWall: touchingRightWall,
            TouchingCeiling: touchingCeiling);
    }

    private static float MeasureHorizontalClearance(SimulationWorld world, PlayerEntity player, float top, float bottom, float directionStep, float limit)
    {
        var direction = directionStep >= 0f ? 1f : -1f;
        if (direction == 0f)
        {
            direction = 1f;
        }

        for (var distance = ProbeStep; distance <= limit; distance += ProbeStep)
        {
            var offset = distance * direction;
            var left = player.Left + offset;
            var right = player.Right + offset;
            if (world.Level.IntersectsSolid(left, top, right, bottom))
            {
                return distance;
            }
        }

        return limit;
    }

    private static MLBotTerrainAffordanceSnapshot BuildTerrainAffordanceSnapshot(
        SimulationWorld world,
        PlayerEntity player,
        MLBotObjectiveSnapshot objective)
    {
        var surfaceIndex = TerrainSurfaceIndices.GetValue(world.Level, static level => TerrainAffordanceSurfaceIndex.Build(level));
        var left = FindBestLandingCandidate(world, player, objective, surfaceIndex, -1f);
        var right = FindBestLandingCandidate(world, player, objective, surfaceIndex, 1f);
        var bestUpward = ChooseBestUpwardLanding(left, right);
        var objectiveDirection = objective.HasObjective ? MathF.Sign(objective.RelativeX) : 0f;
        var bestDirection = bestUpward.HasValue ? MathF.Sign(bestUpward.Value.RelativeX) : 0f;

        return new MLBotTerrainAffordanceSnapshot(
            HasLeftLanding: left.HasValue,
            LeftLandingRelativeX: left?.RelativeX ?? 0f,
            LeftLandingRelativeY: left?.RelativeY ?? 0f,
            LeftLandingSurfaceDeltaY: left?.SurfaceDeltaY ?? 0f,
            LeftLandingObjectiveDistanceDelta: left?.ObjectiveDistanceDelta ?? 0f,
            LeftLandingIsHigher: left is { IsHigher: true },
            LeftLandingRequiresJump: left is { RequiresJump: true },
            HasRightLanding: right.HasValue,
            RightLandingRelativeX: right?.RelativeX ?? 0f,
            RightLandingRelativeY: right?.RelativeY ?? 0f,
            RightLandingSurfaceDeltaY: right?.SurfaceDeltaY ?? 0f,
            RightLandingObjectiveDistanceDelta: right?.ObjectiveDistanceDelta ?? 0f,
            RightLandingIsHigher: right is { IsHigher: true },
            RightLandingRequiresJump: right is { RequiresJump: true },
            HasBestUpwardLanding: bestUpward.HasValue,
            BestUpwardLandingRelativeX: bestUpward?.RelativeX ?? 0f,
            BestUpwardLandingRelativeY: bestUpward?.RelativeY ?? 0f,
            BestUpwardLandingSurfaceDeltaY: bestUpward?.SurfaceDeltaY ?? 0f,
            BestUpwardLandingObjectiveDistanceDelta: bestUpward?.ObjectiveDistanceDelta ?? 0f,
            BestUpwardLandingDirection: bestDirection,
            BestUpwardLandingMovesAwayFromObjective: bestUpward.HasValue
                && objectiveDirection != 0f
                && bestDirection != 0f
                && objectiveDirection != bestDirection,
            BestUpwardLandingHorizontalGap: MathF.Abs(bestUpward?.RelativeX ?? 0f),
            BestUpwardLandingHeadroom: bestUpward?.Headroom ?? 0f,
            CurrentSurfaceClearanceLeft: MeasureSurfaceClearance(world, player, -1f),
            CurrentSurfaceClearanceRight: MeasureSurfaceClearance(world, player, 1f));
    }

    private static LandingCandidate? ChooseBestUpwardLanding(LandingCandidate? left, LandingCandidate? right)
    {
        var best = default(LandingCandidate?);
        Consider(left);
        Consider(right);
        return best;

        void Consider(LandingCandidate? candidate)
        {
            if (!candidate.HasValue || !candidate.Value.IsHigher)
            {
                return;
            }

            if (!best.HasValue || candidate.Value.Score > best.Value.Score)
            {
                best = candidate;
            }
        }
    }

    private static LandingCandidate? FindBestLandingCandidate(
        SimulationWorld world,
        PlayerEntity player,
        MLBotObjectiveSnapshot objective,
        TerrainAffordanceSurfaceIndex surfaceIndex,
        float direction)
    {
        player.GetCollisionBounds(out _, out _, out _, out var currentBottom);
        player.GetCollisionBoundsAt(0f, 0f, out var collisionLeftOffset, out _, out var collisionRightOffset, out _);
        var bottomOffset = currentBottom - player.Y;
        var minSurfaceY = currentBottom - LandingSearchUpLimit;
        var maxSurfaceY = currentBottom + LandingSearchDropLimit;
        var minTargetX = direction < 0f ? player.X - LandingSearchHorizontalLimit : player.X + LandingSearchStep;
        var maxTargetX = direction < 0f ? player.X - LandingSearchStep : player.X + LandingSearchHorizontalLimit;
        LandingCandidate? best = null;

        foreach (var surface in surfaceIndex.Query(MathF.Min(minTargetX, maxTargetX), MathF.Max(minTargetX, maxTargetX), minSurfaceY, maxSurfaceY))
        {
            var platformMinTargetX = surface.Left - collisionRightOffset + 1f;
            var platformMaxTargetX = surface.Right - collisionLeftOffset - 1f;
            var candidateMinX = MathF.Max(MathF.Min(minTargetX, maxTargetX), platformMinTargetX);
            var candidateMaxX = MathF.Min(MathF.Max(minTargetX, maxTargetX), platformMaxTargetX);
            if (candidateMinX > candidateMaxX)
            {
                continue;
            }

            var objectiveX = objective.HasObjective ? objective.WorldX : player.X;
            ConsiderLandingX(direction < 0f ? candidateMaxX : candidateMinX);
            ConsiderLandingX((candidateMinX + candidateMaxX) * 0.5f);
            ConsiderLandingX(float.Clamp(objectiveX, candidateMinX, candidateMaxX));

            void ConsiderLandingX(float targetX)
            {
                if (MathF.Abs(targetX - player.X) < LandingSearchStep)
                {
                    return;
                }

                var targetY = surface.Top - bottomOffset;
                player.GetCollisionBoundsAt(targetX, targetY, out var left, out _, out var right, out var bottom);
                if (right <= surface.Left || left >= surface.Right || MathF.Abs(bottom - surface.Top) > 0.25f)
                {
                    return;
                }

                if (!CanOccupyAt(world, player, targetX, targetY)
                    || !HasApproximateJumpArcClearance(world, player, targetX, targetY))
                {
                    return;
                }

                var distance = MathF.Abs(targetX - player.X);
                var surfaceDeltaY = surface.Top - currentBottom;
                var relativeX = targetX - player.X;
                var relativeY = targetY - player.Y;
                var currentObjectiveDistance = objective.HasObjective
                    ? MathF.Sqrt((objective.RelativeX * objective.RelativeX) + (objective.RelativeY * objective.RelativeY))
                    : 0f;
                var targetObjectiveDistance = objective.HasObjective
                    ? MathF.Sqrt(((objective.WorldX - targetX) * (objective.WorldX - targetX))
                        + ((objective.WorldY - targetY) * (objective.WorldY - targetY)))
                    : 0f;
                var objectiveDelta = currentObjectiveDistance - targetObjectiveDistance;
                var verticalGain = MathF.Max(0f, -surfaceDeltaY);
                var headroom = MeasureHeadroomAt(world, player, targetX, targetY);
                var candidate = new LandingCandidate(
                    RelativeX: relativeX,
                    RelativeY: relativeY,
                    SurfaceDeltaY: surfaceDeltaY,
                    ObjectiveDistanceDelta: objectiveDelta,
                    Headroom: headroom,
                    IsHigher: surfaceDeltaY <= -8f,
                    RequiresJump: surfaceDeltaY <= -8f || distance >= 32f,
                    Score: objectiveDelta + (verticalGain * 2.25f) - (distance * 0.12f) + MathF.Min(headroom, 96f) * 0.02f);

                if (!best.HasValue || candidate.Score > best.Value.Score)
                {
                    best = candidate;
                }
            }
        }

        return best;
    }

    private static bool HasApproximateJumpArcClearance(
        SimulationWorld world,
        PlayerEntity player,
        float targetX,
        float targetY)
    {
        var relativeY = targetY - player.Y;
        var verticalGain = MathF.Max(0f, -relativeY);
        var arcLift = MathF.Max(48f, verticalGain + 32f);
        const int samples = 10;
        for (var sample = 1; sample < samples; sample += 1)
        {
            var t = sample / (float)samples;
            var x = Lerp(player.X, targetX, t);
            var y = Lerp(player.Y, targetY, t) - (MathF.Sin(t * MathF.PI) * arcLift);
            if (!CanOccupyAt(world, player, x, y))
            {
                return false;
            }
        }

        return true;
    }

    private static float MeasureSurfaceClearance(SimulationWorld world, PlayerEntity player, float direction)
    {
        player.GetCollisionBounds(out _, out _, out _, out var currentBottom);
        for (var distance = LandingSearchStep; distance <= LandingSearchHorizontalLimit; distance += LandingSearchStep)
        {
            var targetX = player.X + (distance * direction);
            if (!CanOccupyAt(world, player, targetX, player.Y))
            {
                return MathF.Max(0f, distance - LandingSearchStep);
            }

            player.GetCollisionBoundsAt(targetX, player.Y, out var left, out _, out var right, out _);
            var supportTop = world.Level.FindBlockingSolidTop(left, currentBottom, right, currentBottom + 2f);
            if (!supportTop.HasValue)
            {
                return MathF.Max(0f, distance - LandingSearchStep);
            }
        }

        return LandingSearchHorizontalLimit;
    }

    private static float MeasureHeadroomAt(SimulationWorld world, PlayerEntity player, float x, float y)
    {
        player.GetCollisionBoundsAt(x, y, out var left, out var top, out var right, out _);
        for (var distance = LandingSearchStep; distance <= 160f; distance += LandingSearchStep)
        {
            if (world.Level.IntersectsSolid(left, top - distance, right, top))
            {
                return MathF.Max(0f, distance - LandingSearchStep);
            }
        }

        return 160f;
    }

    private static bool CanOccupyAt(SimulationWorld world, PlayerEntity player, float x, float y)
    {
        player.GetCollisionBoundsAt(x, y, out var left, out var top, out var right, out var bottom);
        if (left < 0f || right > world.Level.Bounds.Width || top < 0f || bottom > world.Level.Bounds.Height)
        {
            return false;
        }

        if (world.Level.IntersectsSolid(left, top, right, bottom))
        {
            return false;
        }

        foreach (var wall in world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (left < wall.Right && right > wall.Left && top < wall.Bottom && bottom > wall.Top)
            {
                return false;
            }
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(player.Team, player.IsCarryingIntel))
        {
            if (left < gate.Right && right > gate.Left && top < gate.Bottom && bottom > gate.Top)
            {
                return false;
            }
        }

        return true;
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + ((to - from) * amount);
    }

    private readonly record struct LandingCandidate(
        float RelativeX,
        float RelativeY,
        float SurfaceDeltaY,
        float ObjectiveDistanceDelta,
        float Headroom,
        bool IsHigher,
        bool RequiresJump,
        float Score);

    private sealed class TerrainAffordanceSurfaceIndex
    {
        private const float CellSize = 256f;
        private readonly LandingSurface[] _surfaces;
        private readonly Dictionary<int, List<int>> _surfaceIndicesByCell;

        private TerrainAffordanceSurfaceIndex(LandingSurface[] surfaces, Dictionary<int, List<int>> surfaceIndicesByCell)
        {
            _surfaces = surfaces;
            _surfaceIndicesByCell = surfaceIndicesByCell;
        }

        public static TerrainAffordanceSurfaceIndex Build(SimpleLevel level)
        {
            var surfaces = MergeSolidsIntoSurfaces(level.Solids);
            var indicesByCell = new Dictionary<int, List<int>>();
            for (var index = 0; index < surfaces.Length; index += 1)
            {
                var surface = surfaces[index];
                for (var cell = Cell(surface.Left); cell <= Cell(surface.Right); cell += 1)
                {
                    if (!indicesByCell.TryGetValue(cell, out var indices))
                    {
                        indices = [];
                        indicesByCell[cell] = indices;
                    }

                    indices.Add(index);
                }
            }

            return new TerrainAffordanceSurfaceIndex(surfaces, indicesByCell);
        }

        public IEnumerable<LandingSurface> Query(float minX, float maxX, float minY, float maxY)
        {
            if (_surfaces.Length == 0)
            {
                yield break;
            }

            var seen = new HashSet<int>();
            for (var cell = Cell(minX); cell <= Cell(maxX); cell += 1)
            {
                if (!_surfaceIndicesByCell.TryGetValue(cell, out var indices))
                {
                    continue;
                }

                for (var index = 0; index < indices.Count; index += 1)
                {
                    var surfaceIndex = indices[index];
                    if (!seen.Add(surfaceIndex))
                    {
                        continue;
                    }

                    var surface = _surfaces[surfaceIndex];
                    if (surface.Right < minX || surface.Left > maxX || surface.Top < minY || surface.Top > maxY)
                    {
                        continue;
                    }

                    yield return surface;
                }
            }
        }

        private static LandingSurface[] MergeSolidsIntoSurfaces(IReadOnlyList<LevelSolid> solids)
        {
            var ordered = solids
                .Where(solid => solid.Width > 0f && solid.Height > 0f)
                .OrderBy(solid => solid.Top)
                .ThenBy(solid => solid.Left)
                .ToArray();
            var surfaces = new List<LandingSurface>(ordered.Length);
            foreach (var solid in ordered)
            {
                if (surfaces.Count > 0)
                {
                    var previous = surfaces[^1];
                    if (MathF.Abs(previous.Top - solid.Top) <= 0.25f && solid.Left <= previous.Right + 1f)
                    {
                        surfaces[^1] = previous with { Right = MathF.Max(previous.Right, solid.Right) };
                        continue;
                    }
                }

                surfaces.Add(new LandingSurface(solid.Left, solid.Right, solid.Top));
            }

            return surfaces.ToArray();
        }

        private static int Cell(float x)
        {
            return (int)MathF.Floor(x / CellSize);
        }
    }

    private readonly record struct LandingSurface(float Left, float Right, float Top);

    private static float MeasureVerticalClearance(SimulationWorld world, PlayerEntity player, bool upwards, float limit)
    {
        var direction = upwards ? -1f : 1f;
        for (var distance = ProbeStep; distance <= limit; distance += ProbeStep)
        {
            var offset = distance * direction;
            var top = player.Top + offset;
            var bottom = player.Bottom + offset;
            if (world.Level.IntersectsSolid(player.Left, top, player.Right, bottom))
            {
                return distance;
            }
        }

        return limit;
    }

    private static float MeasureGroundDistance(SimulationWorld world, PlayerEntity player, float facing, float limit)
    {
        var probeCenterX = player.X + (facing * MathF.Max(8f, player.Width * 0.35f));
        for (var distance = 0f; distance <= limit; distance += ProbeStep)
        {
            var top = player.Bottom + distance;
            var bottom = top + ProbeStep;
            if (world.Level.IntersectsSolid(probeCenterX - 2f, top, probeCenterX + 2f, bottom))
            {
                return distance;
            }
        }

        return limit;
    }

    private static float MeasureDropDepth(SimulationWorld world, PlayerEntity player, float facing, float limit)
    {
        var probeCenterX = player.X + (facing * MathF.Max(12f, player.Width * 0.45f));
        for (var distance = 0f; distance <= limit; distance += ProbeStep)
        {
            var top = player.Bottom + distance;
            var bottom = top + ProbeStep;
            if (world.Level.IntersectsSolid(probeCenterX - 2f, top, probeCenterX + 2f, bottom))
            {
                return distance;
            }
        }

        return limit;
    }

    private static bool IsBlockingGateAhead(SimulationWorld world, PlayerEntity player, float facing)
    {
        var gates = world.Level.GetBlockingTeamGates(player.Team, player.IsCarryingIntel);
        if (gates.Count == 0)
        {
            return false;
        }

        var probeLeft = facing >= 0f ? player.Right : player.Left - 18f;
        var probeRight = facing >= 0f ? player.Right + 18f : player.Left;
        for (var index = 0; index < gates.Count; index += 1)
        {
            var gate = gates[index];
            if (gate.Right < probeLeft
                || gate.Left > probeRight
                || gate.Bottom < player.Top
                || gate.Top > player.Bottom)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static MLBotActorSummary FindNearestActor(SimulationWorld world, PlayerEntity player, bool sameTeam)
    {
        PlayerEntity? nearest = null;
        var bestDistanceSquared = float.MaxValue;

        foreach (var (_, other) in world.EnumerateActiveNetworkPlayers())
        {
            if (other.Id == player.Id || !other.IsAlive || (other.Team == player.Team) != sameTeam)
            {
                continue;
            }

            var dx = other.X - player.X;
            var dy = other.Y - player.Y;
            var distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                nearest = other;
            }
        }

        if (nearest is null)
        {
            return default;
        }

        var relativeX = nearest.X - player.X;
        var relativeY = nearest.Y - player.Y;
        return new MLBotActorSummary(
            Exists: true,
            RelativeX: relativeX,
            RelativeY: relativeY,
            Distance: MathF.Sqrt(bestDistanceSquared),
            HasLineOfSight: HasLineOfSight(world, player, nearest));
    }

    private static MLBotControlPointObjectiveSnapshot BuildControlPointObjective(
        SimulationWorld world,
        PlayerEntity player,
        ControlPointState? point,
        MLBotObservationRuntimeState runtimeState)
    {
        if (point is null)
        {
            return default;
        }

        var owner = point.Team switch
        {
            PlayerTeam.Red => 1,
            PlayerTeam.Blue => 2,
            _ => 0,
        };
        var cappingTeam = point.CappingTeam switch
        {
            PlayerTeam.Red => 1,
            PlayerTeam.Blue => 2,
            _ => 0,
        };
        var captureProgress = point.CapTimeTicks <= 0
            ? 0f
            : point.CappingTicks / point.CapTimeTicks;
        var samePointAsPrevious = runtimeState.PreviousControlPointObjectiveIndex == point.Index;
        var captureProgressDelta = samePointAsPrevious
            ? captureProgress - runtimeState.PreviousControlPointCaptureProgress
            : 0f;
        var isPlayerInCaptureZone = world.IsPlayerInControlPointCaptureZone(player, point.Index);
        var timeOnPointTicks = isPlayerInCaptureZone
            ? samePointAsPrevious && runtimeState.PreviousOnControlPointObjective
                ? runtimeState.TimeOnControlPointTicks + 1f
                : 1f
            : 0f;
        var timeSinceLeftPointTicks = isPlayerInCaptureZone
            ? 0f
            : samePointAsPrevious
                ? runtimeState.TimeSinceLeftControlPointTicks + 1f
                : 0f;
        var friendlyCappers = player.Team == PlayerTeam.Red ? point.RedCappers : point.BlueCappers;
        var enemyCappers = player.Team == PlayerTeam.Red ? point.BlueCappers : point.RedCappers;
        var totalCappers = point.RedCappers + point.BlueCappers;
        var isKothMode = world.IsKothModeActive;
        var friendlyKothTimerTicksRemaining = 0;
        var enemyKothTimerTicksRemaining = 0;
        if (isKothMode)
        {
            friendlyKothTimerTicksRemaining = player.Team == PlayerTeam.Red
                ? world.KothRedTimerTicksRemaining
                : world.KothBlueTimerTicksRemaining;
            enemyKothTimerTicksRemaining = player.Team == PlayerTeam.Red
                ? world.KothBlueTimerTicksRemaining
                : world.KothRedTimerTicksRemaining;
        }

        return new MLBotControlPointObjectiveSnapshot(
            HasObjective: true,
            Index: point.Index,
            Owner: owner,
            CappingTeam: cappingTeam,
            CaptureProgress: captureProgress,
            CaptureProgressDelta: captureProgressDelta,
            IsLocked: point.IsLocked,
            FriendlyCappers: friendlyCappers,
            EnemyCappers: enemyCappers,
            TotalCappers: totalCappers,
            IsContested: point.RedCappers > 0 && point.BlueCappers > 0,
            IsPlayerInCaptureZone: isPlayerInCaptureZone,
            TimeOnPointTicks: timeOnPointTicks,
            TimeSinceLeftPointTicks: timeSinceLeftPointTicks,
            FriendlyKothTimerTicksRemaining: friendlyKothTimerTicksRemaining,
            EnemyKothTimerTicksRemaining: enemyKothTimerTicksRemaining,
            KothUnlockTicksRemaining: isKothMode ? world.KothUnlockTicksRemaining : 0,
            IsKothMode: isKothMode,
            IsDoubleKothMode: world.MatchRules.Mode == GameModeKind.DoubleKingOfTheHill);
    }

    private static bool HasLineOfSight(SimulationWorld world, PlayerEntity source, PlayerEntity target)
    {
        return HasLineOfSight(
            world,
            source.X,
            source.Y,
            target.X,
            target.Y - (target.Height / 4f),
            source.Team,
            source.IsCarryingIntel);
    }

    private static bool HasLineOfSight(
        SimulationWorld world,
        float originX,
        float originY,
        float targetX,
        float targetY,
        PlayerTeam team,
        bool carryingIntel)
    {
        var deltaX = targetX - originX;
        var deltaY = targetY - originY;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= 0.0001f)
        {
            return true;
        }

        var directionX = deltaX / distance;
        var directionY = deltaY / distance;
        foreach (var solid in world.Level.Solids)
        {
            if (RayIntersectsRectangle(originX, originY, directionX, directionY, solid.Left, solid.Top, solid.Right, solid.Bottom, distance))
            {
                return false;
            }
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, carryingIntel))
        {
            if (RayIntersectsRectangle(originX, originY, directionX, directionY, gate.Left, gate.Top, gate.Right, gate.Bottom, distance))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RayIntersectsRectangle(
        float originX,
        float originY,
        float directionX,
        float directionY,
        float left,
        float top,
        float right,
        float bottom,
        float maxDistance)
    {
        const float epsilon = 0.0001f;
        var minDistance = 0f;
        var maxDistanceAlongRay = maxDistance;

        if (MathF.Abs(directionX) < epsilon)
        {
            if (originX < left || originX > right)
            {
                return false;
            }
        }
        else
        {
            var invDirectionX = 1f / directionX;
            var tx1 = (left - originX) * invDirectionX;
            var tx2 = (right - originX) * invDirectionX;
            minDistance = MathF.Max(minDistance, MathF.Min(tx1, tx2));
            maxDistanceAlongRay = MathF.Min(maxDistanceAlongRay, MathF.Max(tx1, tx2));
        }

        if (MathF.Abs(directionY) < epsilon)
        {
            if (originY < top || originY > bottom)
            {
                return false;
            }
        }
        else
        {
            var invDirectionY = 1f / directionY;
            var ty1 = (top - originY) * invDirectionY;
            var ty2 = (bottom - originY) * invDirectionY;
            minDistance = MathF.Max(minDistance, MathF.Min(ty1, ty2));
            maxDistanceAlongRay = MathF.Min(maxDistanceAlongRay, MathF.Max(ty1, ty2));
        }

        return maxDistanceAlongRay >= minDistance
            && maxDistanceAlongRay >= 0f
            && minDistance <= maxDistance;
    }
}
