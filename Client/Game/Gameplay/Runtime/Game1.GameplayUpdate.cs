#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsGameplayBindingKey(Keys key)
    {
        return _inputBindings.MoveUp.IsKeyboardKey(key)
            || _inputBindings.MoveDown.IsKeyboardKey(key)
            || _inputBindings.MoveLeft.IsKeyboardKey(key)
            || _inputBindings.MoveRight.IsKeyboardKey(key)
            || _inputBindings.Taunt.IsKeyboardKey(key)
            || _inputBindings.CallMedic.IsKeyboardKey(key)
            || _inputBindings.UseAbility.IsKeyboardKey(key)
            || IsSwapWeaponsKeyboardBindingKey(key)
            || _inputBindings.InteractWeapon.IsKeyboardKey(key)
            || _inputBindings.ChangeTeam.IsKeyboardKey(key)
            || _inputBindings.ChangeClass.IsKeyboardKey(key)
            || _inputBindings.ShowScoreboard.IsKeyboardKey(key)
            || _inputBindings.ToggleConsole.IsKeyboardKey(key)
            || _inputBindings.OpenBubbleMenuZ.IsKeyboardKey(key)
            || _inputBindings.OpenBubbleMenuX.IsKeyboardKey(key)
            || _inputBindings.OpenBubbleMenuC.IsKeyboardKey(key)
            || _inputBindings.CustomBubble.IsKeyboardKey(key);
    }

    private bool IsSwapWeaponsKeyboardBindingKey(Keys key)
    {
        return InputBindingsSettings.NormalizeSwapWeaponsBinding(_inputBindings.SwapWeaponsBinding) switch
        {
            WeaponSwapBindingMode.Space => key == Keys.Space,
            WeaponSwapBindingMode.Q => key == Keys.Q,
            WeaponSwapBindingMode.Custom => _inputBindings.SwapWeaponsCustomKey.IsKeyboardKey(key),
            _ => false,
        };
    }

    private static bool IsChatShortcutHeld(KeyboardState keyboard)
    {
        return keyboard.IsKeyDown(Keys.Y)
            || keyboard.IsKeyDown(Keys.U);
    }

    private bool IsChatShortcutPressed(KeyboardState keyboard, Keys key)
    {
        return IsKeyPressed(keyboard, key);
    }

    private void UpdateGameplayScreenState(KeyboardState keyboard, MouseState mouse)
    {
        _gameplayScreenStateController.UpdateGameplayScreenState(keyboard, mouse);
    }

    private void FinalizeGameplayFrame(KeyboardState keyboard, MouseState mouse)
    {
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        _wasLocalPlayerAlive = _world.LocalPlayer.IsAlive;
        _wasDeathCamActive = _killCamEnabled
            && !_world.LocalPlayer.IsAlive
            && _world.LocalDeathCam is not null
            && GetDeathCamElapsedTicks(_world.LocalDeathCam) >= DeathCamFocusDelayTicks;
        _wasMatchEnded = _world.MatchState.IsEnded;
    }
}
