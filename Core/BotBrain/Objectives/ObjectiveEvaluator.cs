namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Evaluates the current game state and selects a macro-level goal position
/// for the bot to navigate toward, based on the active game mode.
/// </summary>
public static class ObjectiveEvaluator
{
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
            GameModeKind.CaptureTheFlag => EvaluateCTFGoal(self, world, ownTeam),
            GameModeKind.Arena => EvaluateArenaGoal(self, world),
            GameModeKind.ControlPoint => EvaluateControlPointGoal(self, world, ownTeam),
            GameModeKind.KingOfTheHill => EvaluateControlPointGoal(self, world, ownTeam),
            GameModeKind.DoubleKingOfTheHill => EvaluateControlPointGoal(self, world, ownTeam),
            GameModeKind.Generator => EvaluateGeneratorGoal(self, world, ownTeam),
            GameModeKind.TeamDeathmatch => EvaluateTDMGoal(self, world, ownTeam),
            _ => EvaluateTDMGoal(self, world, ownTeam),
        };
    }

    private static (float X, float Y) EvaluateCTFGoal(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam ownTeam)
    {
        var level = world.Level;

        // If carrying intel, go to our own base.
        if (self.IsCarryingIntel)
        {
            var ownBase = level.GetIntelBase(ownTeam);
            if (ownBase.HasValue)
            {
                return (ownBase.Value.X, ownBase.Value.Y);
            }
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

    private static (float X, float Y) EvaluateArenaGoal(
        PlayerEntity self,
        SimulationWorld world)
    {
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
            if (cp.Team != ownTeam || cp.CappingTeam.HasValue)
            {
                return (cp.HealingAuraCenterX, cp.HealingAuraCenterY);
            }
        }

        // All points owned — defend the first one.
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

    private static (float X, float Y) EvaluateGeneratorGoal(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam ownTeam)
    {
        var generator = world.Level.GetFirstRoomObject(RoomObjectType.Generator);
        if (generator.HasValue)
        {
            return (generator.Value.CenterX, generator.Value.CenterY);
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
