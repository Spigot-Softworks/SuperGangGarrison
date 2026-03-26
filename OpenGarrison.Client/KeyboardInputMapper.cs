using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class KeyboardInputMapper
{
    public static PlayerInputSnapshot BuildGameplaySnapshot(
        InputBindingsSettings bindings,
        KeyboardState keyboard,
        MouseState mouse,
        float cameraX,
        float cameraY)
    {
        return new PlayerInputSnapshot(
            Left: keyboard.IsKeyDown(bindings.MoveLeft) || keyboard.IsKeyDown(Keys.Left),
            Right: keyboard.IsKeyDown(bindings.MoveRight) || keyboard.IsKeyDown(Keys.Right),
            Up: keyboard.IsKeyDown(bindings.MoveUp) || keyboard.IsKeyDown(Keys.Up),
            Down: keyboard.IsKeyDown(bindings.MoveDown) || keyboard.IsKeyDown(Keys.Down),
            BuildSentry: false,
            DestroySentry: false,
            Taunt: keyboard.IsKeyDown(bindings.Taunt),
            FirePrimary: mouse.LeftButton == ButtonState.Pressed,
            FireSecondary: mouse.RightButton == ButtonState.Pressed,
            AimWorldX: cameraX + mouse.X,
            AimWorldY: cameraY + mouse.Y,
            DebugKill: false,
            DropIntel: keyboard.IsKeyDown(Keys.B));
    }
}
