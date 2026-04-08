#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsGameplayBindingKey(Keys key)
    {
        return _inputBindings.MoveUp == key
            || _inputBindings.MoveDown == key
            || _inputBindings.MoveLeft == key
            || _inputBindings.MoveRight == key
            || _inputBindings.Taunt == key
            || _inputBindings.CallMedic == key
            || _inputBindings.FireSecondaryWeapon == key
            || _inputBindings.InteractWeapon == key
            || _inputBindings.ChangeTeam == key
            || _inputBindings.ChangeClass == key
            || _inputBindings.ShowScoreboard == key
            || _inputBindings.ToggleConsole == key
            || _inputBindings.OpenBubbleMenuZ == key
            || _inputBindings.OpenBubbleMenuX == key
            || _inputBindings.OpenBubbleMenuC == key;
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
