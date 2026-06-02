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
        var fireSecondary = mouse.RightButton == ButtonState.Pressed;
        var interactWeapon = InputBindingInput.IsDown(bindings.InteractWeapon, keyboard, mouse)
            && !IsBindingReservedForSwapWeapons(bindings, swapWeaponsBinding, bindings.InteractWeapon);
        
        return new PlayerInputSnapshot(
            Left: InputBindingInput.IsDown(bindings.MoveLeft, keyboard, mouse) || keyboard.IsKeyDown(Keys.Left),
            Right: InputBindingInput.IsDown(bindings.MoveRight, keyboard, mouse) || keyboard.IsKeyDown(Keys.Right),
            Up: InputBindingInput.IsDown(bindings.MoveUp, keyboard, mouse) || keyboard.IsKeyDown(Keys.Up),
            Down: InputBindingInput.IsDown(bindings.MoveDown, keyboard, mouse) || keyboard.IsKeyDown(Keys.Down),
            BuildSentry: false,
            DestroySentry: false,
            Taunt: InputBindingInput.IsDown(bindings.Taunt, keyboard, mouse),
            FirePrimary: mouse.LeftButton == ButtonState.Pressed,
            FireSecondary: fireSecondary,
            UseAbility: InputBindingInput.IsDown(bindings.UseAbility, keyboard, mouse),
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
            WeaponSwapBindingMode.Custom => InputBindingInput.IsDown(bindings.SwapWeaponsCustomKey, keyboard, mouse),
            _ => keyboard.IsKeyDown(Keys.Space),
        };
    }

    private static bool IsBindingReservedForSwapWeapons(
        InputBindingsSettings bindings,
        WeaponSwapBindingMode binding,
        InputBinding inputBinding)
    {
        return binding switch
        {
            WeaponSwapBindingMode.Space => inputBinding.IsKeyboardKey(Keys.Space),
            WeaponSwapBindingMode.MouseSecondary => inputBinding.Kind == InputBindingKind.Mouse && inputBinding.MouseButton == InputMouseButton.Right,
            WeaponSwapBindingMode.Q => inputBinding.IsKeyboardKey(Keys.Q),
            WeaponSwapBindingMode.Custom => inputBinding == bindings.SwapWeaponsCustomKey,
            _ => false,
        };
    }
}
