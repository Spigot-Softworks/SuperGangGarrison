#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private (PlayerInputSnapshot GameplayInput, PlayerInputSnapshot NetworkInput) BuildGameplayInputs(KeyboardState keyboard, MouseState mouse, Vector2 cameraPosition)
    {
        if (_suppressPrimaryFireUntilMouseRelease
            && mouse.LeftButton != ButtonState.Pressed)
        {
            _suppressPrimaryFireUntilMouseRelease = false;
        }

        var fullInput = KeyboardInputMapper.BuildGameplaySnapshot(
            _inputBindings,
            keyboard,
            mouse,
            cameraPosition.X,
            cameraPosition.Y,
            _world.LocalPlayer.X,
            _world.LocalPlayer.Y);
        if (_bubbleMenuKind != BubbleMenuKind.None && !_bubbleMenuClosing)
        {
            fullInput = fullInput with
            {
                FirePrimary = false,
                FireSecondary = false,
                UseAbility = false,
                InteractWeapon = false,
            };
        }

        var blockedInput = ShouldPreserveAimWhileBlocked()
            ? BuildAimOnlyGameplaySnapshot(fullInput)
            : default;
        var gameplayInput = _networkClient.IsConnected
            ? default
            : IsGameplayInputBlocked()
                ? blockedInput
                : fullInput;
        var networkInput = IsGameplayInputBlocked()
            ? blockedInput
            : fullInput;

        if (_suppressPrimaryFireUntilMouseRelease)
        {
            gameplayInput = gameplayInput with
            {
                FirePrimary = false,
                UseAbility = false,
            };
            networkInput = networkInput with
            {
                FirePrimary = false,
                UseAbility = false,
            };
        }

        if (_world.IsPlayerHumiliated(_world.LocalPlayer))
        {
            gameplayInput = gameplayInput with
            {
                FirePrimary = false,
                FireSecondary = false,
                UseAbility = false,
                InteractWeapon = false,
                BuildSentry = false,
                DestroySentry = false,
            };
            networkInput = networkInput with
            {
                FirePrimary = false,
                FireSecondary = false,
                UseAbility = false,
                InteractWeapon = false,
                BuildSentry = false,
                DestroySentry = false,
            };
        }

        UpdateBuildMenuState(keyboard, mouse);
        TryShowEngineerJumpPadBuildNoticeOnUtilityPress(networkInput);

        return (gameplayInput, networkInput);
    }

    private void SuppressPrimaryFireUntilMouseRelease()
    {
        _suppressPrimaryFireUntilMouseRelease = true;
    }

    private void UpdateSpectatorTrackingHotkeys(KeyboardState keyboard, MouseState mouse)
    {
        if (!CanUseSpectatorTrackingHotkeys())
        {
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Add) || IsKeyPressed(keyboard, Keys.OemPlus) ||
            (mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton != ButtonState.Pressed))
        {
            CycleSpectatorTracking(forward: true);
        }
        else if (IsKeyPressed(keyboard, Keys.Subtract) || IsKeyPressed(keyboard, Keys.OemMinus) ||
            (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed))
        {
            CycleSpectatorTracking(forward: false);
        }
    }

    private static PlayerInputSnapshot BuildAimOnlyGameplaySnapshot(PlayerInputSnapshot input)
    {
        return input with
        {
            Left = false,
            Right = false,
            Up = false,
            Down = false,
            BuildSentry = false,
            DestroySentry = false,
            Taunt = false,
            FirePrimary = false,
            FireSecondary = false,
            UseAbility = false,
            InteractWeapon = false,
            DebugKill = false,
            DropIntel = false,
        };
    }
}
