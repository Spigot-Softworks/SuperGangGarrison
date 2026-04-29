using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot;

public static class MLBotTaskStateResolver
{
    public static MLBotTaskPhase Resolve(SimulationWorld world, PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return MLBotTaskPhase.None;
        }

        if (player.IsCarryingIntel)
        {
            return MLBotTaskPhase.ReturnIntel;
        }

        return world.MatchRules.Mode switch
        {
            GameModeKind.CaptureTheFlag => MLBotTaskPhase.AttackIntel,
            GameModeKind.ControlPoint => MLBotTaskPhase.CaptureObjective,
            GameModeKind.KingOfTheHill => MLBotTaskPhase.CaptureObjective,
            GameModeKind.DoubleKingOfTheHill => MLBotTaskPhase.CaptureObjective,
            GameModeKind.Arena => MLBotTaskPhase.CaptureObjective,
            _ => MLBotTaskPhase.None,
        };
    }
}
