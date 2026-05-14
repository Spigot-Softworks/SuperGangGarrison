namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Evaluates the current game state and selects a macro-level goal position
/// for the bot to navigate toward, based on the active game mode.
/// </summary>
public static class ObjectiveEvaluator
{
    private const float SniperDroppedIntelInterestDistance = 520f;
    private const float ObjectiveAllyIntelPressureDistance = 640f;

    /// <summary>
    /// Determine the bot's current goal position based on game mode and state.
    /// Returns a world (X, Y) position the bot should navigate toward.
    /// </summary>
    public static (float X, float Y) EvaluateGoal(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam ownTeam,
        PlayerEntity? combatTarget)
    {
        return world.MatchRules.Mode switch
        {
            GameModeKind.CaptureTheFlag => EvaluateCTFGoal(self, world, ownTeam, combatTarget),
            GameModeKind.Arena => EvaluateArenaGoal(self, world),
            GameModeKind.ControlPoint => EvaluateControlPointGoal(self, world, ownTeam),
            GameModeKind.KingOfTheHill => EvaluateControlPointGoal(self, world, ownTeam),
            GameModeKind.DoubleKingOfTheHill => EvaluateDoubleKingOfTheHillGoal(self, world, ownTeam),
            GameModeKind.Generator => EvaluateGeneratorGoal(self, world, ownTeam),
            GameModeKind.TeamDeathmatch => EvaluateTDMGoal(self, world, ownTeam),
            _ => EvaluateTDMGoal(self, world, ownTeam),
        };
    }

    private static (float X, float Y) EvaluateCTFGoal(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam ownTeam,
        PlayerEntity? combatTarget)
    {
        var level = world.Level;

        if (self.ClassId == PlayerClass.Sniper
            && combatTarget is not null
            && !self.IsCarryingIntel
            && HasOtherAllyAvailableForObjective(self, world, ownTeam))
        {
            return (self.X, self.Y);
        }

        // If carrying intel, go to our own base.
        if (self.IsCarryingIntel)
        {
            var ownBase = level.GetIntelBase(ownTeam);
            if (ownBase.HasValue)
            {
                return (ownBase.Value.X, ownBase.Value.Y);
            }
        }

        var enemyIntel = GetEnemyIntelState(world, ownTeam);
        if (enemyIntel.IsDropped)
        {
            if (self.ClassId == PlayerClass.Sniper
                && Distance(self.X, self.Y, enemyIntel.X, enemyIntel.Y) > SniperDroppedIntelInterestDistance
                && HasOtherAllyAvailableForObjective(self, world, ownTeam))
            {
                return EvaluateSniperFallbackGoal(self, world);
            }

            return (enemyIntel.X, enemyIntel.Y);
        }

        var ownIntel = GetOwnIntelState(world, ownTeam);
        if (ownIntel.IsDropped)
        {
            if (self.ClassId == PlayerClass.Sniper
                && Distance(self.X, self.Y, ownIntel.X, ownIntel.Y) > SniperDroppedIntelInterestDistance
                && HasOtherAllyAvailableForObjective(self, world, ownTeam))
            {
                return EvaluateSniperFallbackGoal(self, world);
            }

            return (ownIntel.X, ownIntel.Y);
        }

        foreach (var candidate in CombatDecisionResolver.EnumeratePlayers(world))
        {
            if (candidate.IsAlive
                && candidate.Id != self.Id
                && candidate.Team == ownTeam
                && candidate.IsCarryingIntel)
            {
                return (candidate.X, candidate.Y);
            }
        }

        if (self.ClassId == PlayerClass.Sniper
            && HasOtherAllyAvailableForObjective(self, world, ownTeam))
        {
            return EvaluateSniperFallbackGoal(self, world);
        }

        // Otherwise, go grab the enemy intel.
        var opposingTeam = ownTeam == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        var enemyBase = level.GetIntelBase(opposingTeam);
        if (enemyBase.HasValue)
        {
            return (enemyBase.Value.X, enemyBase.Value.Y);
        }

        // Fallback: go to center of the map.
        return (level.Bounds.Width * 0.5f, level.Bounds.Height * 0.5f);
    }

    private static bool HasOtherAllyAvailableForObjective(PlayerEntity self, SimulationWorld world, PlayerTeam ownTeam)
    {
        foreach (var candidate in CombatDecisionResolver.EnumeratePlayers(world))
        {
            if (candidate.IsAlive
                && candidate.Id != self.Id
                && candidate.Team == ownTeam
                && candidate.ClassId != PlayerClass.Sniper
                && IsAllyApplyingObjectivePressure(candidate, world, ownTeam))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAllyApplyingObjectivePressure(PlayerEntity candidate, SimulationWorld world, PlayerTeam ownTeam)
    {
        if (candidate.IsCarryingIntel)
        {
            return true;
        }

        var enemyIntel = GetEnemyIntelState(world, ownTeam);
        if (!enemyIntel.IsCarried
            && Distance(candidate.X, candidate.Y, enemyIntel.X, enemyIntel.Y) <= ObjectiveAllyIntelPressureDistance)
        {
            return true;
        }

        var ownIntel = GetOwnIntelState(world, ownTeam);
        return ownIntel.IsDropped
            && Distance(candidate.X, candidate.Y, ownIntel.X, ownIntel.Y) <= ObjectiveAllyIntelPressureDistance;
    }

    private static (float X, float Y) EvaluateSniperFallbackGoal(PlayerEntity self, SimulationWorld world)
    {
        return (self.X, self.Y);
    }

    private static TeamIntelligenceState GetEnemyIntelState(SimulationWorld world, PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? world.RedIntel : world.BlueIntel;
    }

    private static TeamIntelligenceState GetOwnIntelState(SimulationWorld world, PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? world.BlueIntel : world.RedIntel;
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static (float X, float Y) EvaluateArenaGoal(
        PlayerEntity self,
        SimulationWorld world)
    {
        var captureZones = world.Level.GetRoomObjects(RoomObjectType.CaptureZone);
        if (captureZones.Count > 0)
        {
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            foreach (var zone in captureZones)
            {
                minX = MathF.Min(minX, zone.CenterX - zone.Width * 0.5f);
                minY = MathF.Min(minY, zone.CenterY - zone.Height * 0.5f);
                maxX = MathF.Max(maxX, zone.CenterX + zone.Width * 0.5f);
                maxY = MathF.Max(maxY, zone.CenterY + zone.Height * 0.5f);
            }

            if (float.IsFinite(minX) && float.IsFinite(minY) && float.IsFinite(maxX) && float.IsFinite(maxY))
            {
                return ((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            }
        }

        if (world.ControlPoints.Count > 0)
        {
            var point = world.ControlPoints[0];
            return (point.HealingAuraCenterX, point.HealingAuraCenterY);
        }

        // Move toward the arena control point.
        var controlPoint = world.Level.GetFirstRoomObject(RoomObjectType.ArenaControlPoint);
        if (controlPoint.HasValue)
        {
            return (controlPoint.Value.CenterX, controlPoint.Value.CenterY);
        }

        return (world.Level.Bounds.Width * 0.5f, world.Level.Bounds.Height * 0.5f);
    }

    private static (float X, float Y) EvaluateControlPointGoal(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam ownTeam)
    {
        // Find an unowned or contested control point to go capture.
        foreach (var cp in world.ControlPoints)
        {
            if (!cp.IsLocked && (cp.Team != ownTeam || cp.CappingTeam.HasValue))
            {
                return (cp.HealingAuraCenterX, cp.HealingAuraCenterY);
            }
        }

        // All points owned — defend the first one.
        ControlPointState? ownedFrontier = null;
        var ownedFrontierDistance = float.PositiveInfinity;
        foreach (var cp in world.ControlPoints)
        {
            if (cp.IsLocked || cp.Team != ownTeam)
            {
                continue;
            }

            var distance = Distance(self.X, self.Y, cp.HealingAuraCenterX, cp.HealingAuraCenterY);
            if (distance >= ownedFrontierDistance)
            {
                continue;
            }

            ownedFrontier = cp;
            ownedFrontierDistance = distance;
        }

        if (ownedFrontier is not null)
        {
            return (ownedFrontier.HealingAuraCenterX, ownedFrontier.HealingAuraCenterY);
        }

        if (world.ControlPoints.Count > 0)
        {
            var cp = world.ControlPoints[0];
            return (cp.HealingAuraCenterX, cp.HealingAuraCenterY);
        }

        // Fallback: KOTH single point.
        var kothPoint = world.Level.GetFirstRoomObject(RoomObjectType.ControlPoint);
        if (kothPoint.HasValue)
        {
            return (kothPoint.Value.CenterX, kothPoint.Value.CenterY);
        }

        return (world.Level.Bounds.Width * 0.5f, world.Level.Bounds.Height * 0.5f);
    }

    private static (float X, float Y) EvaluateDoubleKingOfTheHillGoal(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam ownTeam)
    {
        var ownPoint = ResolveDualKothPoint(world, ownTeam);
        var enemyTeam = GetOpposingTeam(ownTeam);
        var enemyPoint = ResolveDualKothPoint(world, enemyTeam);

        if (ownPoint is not null
            && (ownPoint.Team != ownTeam || ownPoint.CappingTeam.HasValue))
        {
            return GetControlPointGoal(ownPoint);
        }

        if (enemyPoint is not null
            && (enemyPoint.Team != ownTeam || enemyPoint.CappingTeam.HasValue))
        {
            return GetControlPointGoal(enemyPoint);
        }

        if (ownPoint is not null)
        {
            return GetControlPointGoal(ownPoint);
        }

        return EvaluateControlPointGoal(self, world, ownTeam);
    }

    private static ControlPointState? ResolveDualKothPoint(SimulationWorld world, PlayerTeam homeTeam)
    {
        for (var index = 0; index < world.ControlPoints.Count; index += 1)
        {
            var point = world.ControlPoints[index];
            var marker = point.Marker;
            if ((homeTeam == PlayerTeam.Red && marker.IsRedKothControlPoint())
                || (homeTeam == PlayerTeam.Blue && marker.IsBlueKothControlPoint()))
            {
                return point;
            }
        }

        if (world.ControlPoints.Count != 2)
        {
            return null;
        }

        ControlPointState? left = null;
        ControlPointState? right = null;
        for (var index = 0; index < world.ControlPoints.Count; index += 1)
        {
            var point = world.ControlPoints[index];
            if (left is null || point.Marker.CenterX < left.Marker.CenterX)
            {
                left = point;
            }

            if (right is null || point.Marker.CenterX > right.Marker.CenterX)
            {
                right = point;
            }
        }

        return homeTeam == PlayerTeam.Red ? left : right;
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
    }

    private static (float X, float Y) GetControlPointGoal(ControlPointState point)
    {
        return (point.HealingAuraCenterX, point.HealingAuraCenterY);
    }

    private static (float X, float Y) EvaluateGeneratorGoal(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam ownTeam)
    {
        var opposingTeam = GetOpposingTeam(ownTeam);
        foreach (var generator in world.Generators)
        {
            if (generator.Team == opposingTeam && !generator.IsDestroyed)
            {
                return (generator.Marker.CenterX, generator.Marker.CenterY);
            }
        }

        foreach (var generator in world.Generators)
        {
            if (generator.Team == opposingTeam)
            {
                return (generator.Marker.CenterX, generator.Marker.CenterY);
            }
        }

        return (world.Level.Bounds.Width * 0.5f, world.Level.Bounds.Height * 0.5f);
    }

    private static (float X, float Y) EvaluateTDMGoal(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam ownTeam)
    {
        // In TDM, seek enemies. Move toward center of map if no target is visible.
        // The target selector will handle combat once enemies are in range.
        return (world.Level.Bounds.Width * 0.5f, world.Level.Bounds.Height * 0.5f);
    }
}
