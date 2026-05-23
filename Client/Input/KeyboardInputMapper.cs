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
        var swapWeaponsBinding = InputBindingsSettings.NormalizeSwapWeaponsBinding(bindings.SwapWeaponsBinding);
        var swapWeapon = IsSwapWeaponInputDown(bindings, swapWeaponsBinding, keyboard, mouse);
        var fireSecondary = mouse.RightButton == ButtonState.Pressed
            && swapWeaponsBinding != WeaponSwapBindingMode.MouseSecondary;
        var interactWeapon = keyboard.IsKeyDown(bindings.InteractWeapon)
            && !IsKeyboardKeyReservedForSwapWeapons(bindings, swapWeaponsBinding, bindings.InteractWeapon);
        
        return new PlayerInputSnapshot(
            Left: keyboard.IsKeyDown(bindings.MoveLeft) || keyboard.IsKeyDown(Keys.Left),
            Right: keyboard.IsKeyDown(bindings.MoveRight) || keyboard.IsKeyDown(Keys.Right),
            Up: keyboard.IsKeyDown(bindings.MoveUp) || keyboard.IsKeyDown(Keys.Up),
            Down: keyboard.IsKeyDown(bindings.MoveDown) || keyboard.IsKeyDown(Keys.Down),
            BuildSentry: false,
            DestroySentry: false,
            Taunt: keyboard.IsKeyDown(bindings.Taunt),
            FirePrimary: mouse.LeftButton == ButtonState.Pressed,
            FireSecondary: fireSecondary,
            UseAbility: keyboard.IsKeyDown(bindings.UseAbility),
            InteractWeapon: interactWeapon,
            AimWorldX: mouseWorldX,
            AimWorldY: mouseWorldY,
            DebugKill: false,
            DropIntel: keyboard.IsKeyDown(Keys.B),
            IsUsingBinoculars: isUsingBinoculars,
            BinocularsFocusX: isUsingBinoculars ? binocularsFocusX : localPlayerX,
            BinocularsFocusY: isUsingBinoculars ? binocularsFocusY : localPlayerY,
            SwapWeapon: swapWeapon,
            ReadyUp: keyboard.IsKeyDown(Keys.F4));
    }

    internal static bool IsSwapWeaponInputDown(
        InputBindingsSettings bindings,
        WeaponSwapBindingMode binding,
        KeyboardState keyboard,
        MouseState mouse)
    {
        return binding switch
        {
            WeaponSwapBindingMode.Space => keyboard.IsKeyDown(Keys.Space),
            WeaponSwapBindingMode.MouseSecondary => mouse.RightButton == ButtonState.Pressed,
            WeaponSwapBindingMode.Q => keyboard.IsKeyDown(Keys.Q),
            WeaponSwapBindingMode.Custom => keyboard.IsKeyDown(bindings.SwapWeaponsCustomKey),
            _ => keyboard.IsKeyDown(Keys.Space),
        };
    }

    private static bool IsKeyboardKeyReservedForSwapWeapons(
        InputBindingsSettings bindings,
        WeaponSwapBindingMode binding,
        Keys key)
    {
        return binding switch
        {
            WeaponSwapBindingMode.Space => key == Keys.Space,
            WeaponSwapBindingMode.Q => key == Keys.Q,
            WeaponSwapBindingMode.Custom => key == bindings.SwapWeaponsCustomKey,
            _ => false,
        };
    }
}
