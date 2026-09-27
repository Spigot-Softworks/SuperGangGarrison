#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{

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
        _wasDeathCamActive = _killCamEnabled
            && !_world.LocalPlayer.IsAlive
            && _world.LocalDeathCam is not null
            && GetDeathCamElapsedTicks(_world.LocalDeathCam) >= DeathCamFocusDelayTicks;
        ObservePracticeRoundCompletion();
        _wasMatchEnded = _world.MatchState.IsEnded;
    }
}
