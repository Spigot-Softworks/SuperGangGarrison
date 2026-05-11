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
        float cameraY,
        float localPlayerX,
        float localPlayerY,
        bool isUsingBinoculars = false,
        float binocularsFocusX = 0f,
        float binocularsFocusY = 0f)
    {
        var mouseWorldX = cameraX + mouse.X;
        var mouseWorldY = cameraY + mouse.Y;
        
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
            UseAbility: keyboard.IsKeyDown(bindings.UseAbility),
            InteractWeapon: keyboard.IsKeyDown(bindings.InteractWeapon),
            AimWorldX: mouseWorldX,
            AimWorldY: mouseWorldY,
            DebugKill: false,
            DropIntel: keyboard.IsKeyDown(Keys.B),
            IsUsingBinoculars: isUsingBinoculars,
            BinocularsFocusX: isUsingBinoculars ? binocularsFocusX : localPlayerX,
            BinocularsFocusY: isUsingBinoculars ? binocularsFocusY : localPlayerY);
    }
}
