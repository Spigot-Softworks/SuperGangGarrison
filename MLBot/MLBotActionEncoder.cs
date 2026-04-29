using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot;

public static class MLBotActionEncoder
{
    public static MLBotAction Encode(PlayerInputSnapshot input)
    {
        var moveDirection = 0;
        if (input.Left && !input.Right)
        {
            moveDirection = -1;
        }
        else if (input.Right && !input.Left)
        {
            moveDirection = 1;
        }

        return new MLBotAction(
            MoveDirection: moveDirection,
            Jump: input.Up,
            Crouch: input.Down,
            FirePrimary: input.FirePrimary,
            FireSecondary: input.FireSecondary,
            DropIntel: input.DropIntel,
            AimWorldX: input.AimWorldX,
            AimWorldY: input.AimWorldY);
    }
}
