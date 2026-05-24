#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

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

        if (_suppressSecondaryFireUntilMouseRelease
            && mouse.RightButton != ButtonState.Pressed)
        {
            _suppressSecondaryFireUntilMouseRelease = false;
        }

        UpdateBinocularsFocusPosition(keyboard, (float)_config.FixedDeltaSeconds);

        var fullInput = KeyboardInputMapper.BuildGameplaySnapshot(
            _inputBindings,
            keyboard,
            mouse,
            cameraPosition.X,
            cameraPosition.Y,
            _world.LocalPlayer.X,
            _world.LocalPlayer.Y,
            _world.LocalPlayer.IsUsingBinoculars,
            _binocularsFocusX,
            _binocularsFocusY);
        if (_bubbleMenuKind != BubbleMenuKind.None && !_bubbleMenuClosing)
        {
            fullInput = ApplyBubbleMenuGameplaySuppression(fullInput);
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

        if (_scoreboardOpen || _scoreboardAlpha > 0.02f)
        {
            gameplayInput = gameplayInput with
            {
                FirePrimary = false,
                FireSecondary = false,
                SwapWeapon = false,
            };
            networkInput = networkInput with
            {
                FirePrimary = false,
                FireSecondary = false,
                SwapWeapon = false,
            };
        }

        if (_suppressPrimaryFireUntilMouseRelease)
        {
            gameplayInput = gameplayInput with
            {
                FirePrimary = false,
                UseAbility = false,
                SwapWeapon = false,
            };
            networkInput = networkInput with
            {
                FirePrimary = false,
                UseAbility = false,
                SwapWeapon = false,
            };
        }

        if (_suppressSecondaryFireUntilMouseRelease)
        {
            gameplayInput = gameplayInput with
            {
                FireSecondary = false,
                SwapWeapon = false,
            };
            networkInput = networkInput with
            {
                FireSecondary = false,
                SwapWeapon = false,
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
                SwapWeapon = false,
                BuildSentry = false,
                DestroySentry = false,
            };
            networkInput = networkInput with
            {
                FirePrimary = false,
                FireSecondary = false,
                UseAbility = false,
                InteractWeapon = false,
                SwapWeapon = false,
                BuildSentry = false,
                DestroySentry = false,
            };
        }

        gameplayInput = ApplyClientOnlineSmokeInputPattern(gameplayInput);
        networkInput = ApplyClientOnlineSmokeInputPattern(networkInput);

        networkInput = networkInput with { IsTypingChatMessage = _chatOpen };

        UpdateBuildMenuState(keyboard, mouse);
        TryShowEngineerJumpPadBuildNoticeOnUtilityPress(networkInput);

        return (gameplayInput, networkInput);
    }

    private bool ShouldPreserveHealingPrimaryFireWhileBubbleMenuOpen(PlayerInputSnapshot input)
    {
        if (!input.FirePrimary)
        {
            return false;
        }

        var player = _world.LocalPlayer;
        return player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Medigun)
            || (player.IsExperimentalOffhandSelected
                && (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.Medigun)
                    || player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.MedigunCrit)));
    }

    internal static PlayerInputSnapshot ApplyBubbleMenuGameplaySuppression(PlayerInputSnapshot input)
    {
        return input with
        {
            InteractWeapon = false,
            SwapWeapon = false,
        };
    }

    private void SuppressPrimaryFireUntilMouseRelease()
    {
        _suppressPrimaryFireUntilMouseRelease = true;
    }

    private void UpdateSpectatorTrackingHotkeys(KeyboardState keyboard, MouseState mouse)
    {
        if (_scoreboardOpen || keyboard.IsKeyDown(_inputBindings.ShowScoreboard))
        {
            return;
        }

        if (!CanUseSpectatorTrackingHotkeys())
        {
            return;
        }

        if (IsKeyPressed(keyboard, Keys.LeftControl) || IsKeyPressed(keyboard, Keys.RightControl))
        {
            CycleSpectatorCameraMode();
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
            SwapWeapon = false,
            DebugKill = false,
            DropIntel = false,
        };
    }

    private void UpdateBinocularsFocusPosition(KeyboardState keyboard, float deltaSeconds)
    {
        var isUsingBinoculars = _world.LocalPlayer.IsUsingBinoculars;
        
        // Initialize focus position when binoculars are first activated
        if (isUsingBinoculars && !_wasBinocularsActive)
        {
            _binocularsFocusX = _world.LocalPlayer.X;
            _binocularsFocusY = _world.LocalPlayer.Y;
            _binocularsFocusTargetX = _world.LocalPlayer.X;
            _binocularsFocusTargetY = _world.LocalPlayer.Y;
        }
        
        _wasBinocularsActive = isUsingBinoculars;
        
        // Reset focus position when binoculars are deactivated
        if (!isUsingBinoculars)
        {
            return;
        }

        // Calculate movement direction from WSAD input
        var moveDirectionX = 0f;
        var moveDirectionY = 0f;
        
        if (keyboard.IsKeyDown(_inputBindings.MoveLeft) || keyboard.IsKeyDown(Keys.Left))
        {
            moveDirectionX -= 1f;
        }
        if (keyboard.IsKeyDown(_inputBindings.MoveRight) || keyboard.IsKeyDown(Keys.Right))
        {
            moveDirectionX += 1f;
        }
        if (keyboard.IsKeyDown(_inputBindings.MoveUp) || keyboard.IsKeyDown(Keys.Up))
        {
            moveDirectionY -= 1f;
        }
        if (keyboard.IsKeyDown(_inputBindings.MoveDown) || keyboard.IsKeyDown(Keys.Down))
        {
            moveDirectionY += 1f;
        }
        
        // Normalize direction to prevent faster diagonal movement
        var directionLength = MathF.Sqrt(moveDirectionX * moveDirectionX + moveDirectionY * moveDirectionY);
        if (directionLength > 0f)
        {
            moveDirectionX /= directionLength;
            moveDirectionY /= directionLength;
            
            // Apply movement to target position
            var moveSpeed = BinocularsMovementSpeed * deltaSeconds;
            _binocularsFocusTargetX += moveDirectionX * moveSpeed;
            _binocularsFocusTargetY += moveDirectionY * moveSpeed;
        }

        // Clamp target to max distance from player
        var playerX = _world.LocalPlayer.X;
        var playerY = _world.LocalPlayer.Y;
        var deltaX = _binocularsFocusTargetX - playerX;
        var deltaY = _binocularsFocusTargetY - playerY;
        var distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        
        if (distance > PlayerEntity.BinocularsMaxViewDistance)
        {
            var scale = PlayerEntity.BinocularsMaxViewDistance / distance;
            _binocularsFocusTargetX = playerX + deltaX * scale;
            _binocularsFocusTargetY = playerY + deltaY * scale;
        }
        
        // Smoothly interpolate current position toward target using frame-rate independent exponential smoothing
        var smoothing = 1f - MathF.Pow(1f - BinocularsSmoothingFactor, deltaSeconds * 60f);
        _binocularsFocusX = MathHelper.Lerp(_binocularsFocusX, _binocularsFocusTargetX, smoothing);
        _binocularsFocusY = MathHelper.Lerp(_binocularsFocusY, _binocularsFocusTargetY, smoothing);
    }
}
