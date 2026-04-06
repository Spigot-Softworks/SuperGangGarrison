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

        var fullInput = KeyboardInputMapper.BuildGameplaySnapshot(_inputBindings, keyboard, mouse, cameraPosition.X, cameraPosition.Y);
        if (_bubbleMenuKind != BubbleMenuKind.None && !_bubbleMenuClosing)
        {
            fullInput = fullInput with
            {
                FirePrimary = false,
                FireSecondary = false,
                FireSecondaryWeapon = false,
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
                FireSecondaryWeapon = false,
            };
            networkInput = networkInput with
            {
                FirePrimary = false,
                FireSecondaryWeapon = false,
            };
        }

        if (_world.IsPlayerHumiliated(_world.LocalPlayer))
        {
            gameplayInput = gameplayInput with
            {
                FirePrimary = false,
                FireSecondary = false,
                FireSecondaryWeapon = false,
                InteractWeapon = false,
                BuildSentry = false,
                DestroySentry = false,
            };
            networkInput = networkInput with
            {
                FirePrimary = false,
                FireSecondary = false,
                FireSecondaryWeapon = false,
                InteractWeapon = false,
                BuildSentry = false,
                DestroySentry = false,
            };
        }

        UpdateBuildMenuState(keyboard, mouse);

        return (gameplayInput, networkInput);
    }

    private void SuppressPrimaryFireUntilMouseRelease()
    {
        _suppressPrimaryFireUntilMouseRelease = true;
    }

    private void UpdateSpectatorTrackingHotkeys(KeyboardState keyboard)
    {
        if (!CanUseSpectatorTrackingHotkeys())
        {
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Add) || IsKeyPressed(keyboard, Keys.OemPlus))
        {
            CycleSpectatorTracking(forward: true);
        }
        else if (IsKeyPressed(keyboard, Keys.Subtract) || IsKeyPressed(keyboard, Keys.OemMinus))
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
            FireSecondaryWeapon = false,
            InteractWeapon = false,
            DebugKill = false,
            DropIntel = false,
        };
    }
}
