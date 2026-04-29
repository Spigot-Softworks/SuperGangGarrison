using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot;

public static class MLBotActionDecoder
{
    public static PlayerInputSnapshot Decode(in MLBotAction action)
    {
        return new PlayerInputSnapshot(
            Left: action.MoveDirection < 0,
            Right: action.MoveDirection > 0,
            Up: action.Jump,
            Down: action.Crouch,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: action.FirePrimary,
            FireSecondary: action.FireSecondary,
            AimWorldX: action.AimWorldX,
            AimWorldY: action.AimWorldY,
            DebugKill: false,
            DropIntel: false,
            FireSecondaryWeapon: false,
            InteractWeapon: false);
    }
}
