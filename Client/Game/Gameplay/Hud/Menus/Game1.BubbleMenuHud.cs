#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Globalization;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawBubbleMenuHud()
    {
        if (_bubbleMenuKind == BubbleMenuKind.None)
        {
            return;
        }

        var bubbleMenuMouse = GetScaledMouseState(GetConstrainedMouseState(GetCurrentMouseState()));
        var renderState = new ClientBubbleMenuRenderState(
            ToClientBubbleMenuKind(_bubbleMenuKind),
            _bubbleMenuAlpha,
            _bubbleMenuXPageIndex,
            GetBubbleWheelPointerDirectionDegrees(bubbleMenuMouse),
            GetBubbleWheelSelectedSlot(bubbleMenuMouse));
        if (_bubbleMenuKind != BubbleMenuKind.Custom
            && TryDrawClientPluginBubbleMenu(GetCurrentClientPluginCameraTopLeft(), renderState))
        {
            return;
        }

        var viewportHeight = ViewportHeight;
        var spriteName = _bubbleMenuKind switch
        {
            BubbleMenuKind.Z => "BubbleMenuZS",
            BubbleMenuKind.X when _bubbleMenuXPageIndex == 0 => "BubbleMenuXS",
            BubbleMenuKind.X => "BubbleMenuX2S",
            BubbleMenuKind.C => "BubbleMenuCS",
            _ => null,
        };

        if (_bubbleMenuKind == BubbleMenuKind.Custom)
        {
            DrawCustomBubbleMenuHud(viewportHeight);
            return;
        }

        if (spriteName is null)
        {
            return;
        }

        var frameIndex = _bubbleMenuKind == BubbleMenuKind.X && _bubbleMenuXPageIndex == 2 ? 1 : 0;
        TryDrawScreenSprite(spriteName, frameIndex, new Vector2(_bubbleMenuX, viewportHeight / 2f), Color.White * _bubbleMenuAlpha, Vector2.One);
    }

    private void DrawCustomBubbleMenuHud(int viewportHeight)
    {
        var center = new Vector2(_bubbleMenuX + 76f, viewportHeight / 2f);
        for (var slotIndex = 0; slotIndex < CustomBubbleDocument.SlotCount; slotIndex += 1)
        {
            // Keyboard selection commits and closes immediately, so there is no persistent
            // hovered slot to highlight.
            DrawCustomBubbleMenuSlot(
                slotIndex,
                center + GetCustomBubbleMenuSlotOffset(slotIndex),
                selected: false);
        }
    }

    private static Vector2 GetCustomBubbleMenuSlotOffset(int slotIndex)
    {
        return slotIndex switch
        {
            0 => new Vector2(0f, -58f),
            1 => new Vector2(54f, 0f),
            2 => new Vector2(0f, 58f),
            _ => Vector2.Zero,
        };
    }

    private void DrawCustomBubbleMenuSlot(int slotIndex, Vector2 anchor, bool selected)
    {
        const float scale = 0.62f;
        var alpha = Math.Clamp(_bubbleMenuAlpha, 0f, 1f);
        var shellOrigin = new Vector2(CustomBubbleShellOriginX, CustomBubbleShellOriginY);
        var shellTopLeft = anchor - (shellOrigin * scale);
        var shellBounds = new Rectangle(
            (int)MathF.Round(shellTopLeft.X),
            (int)MathF.Round(shellTopLeft.Y),
            Math.Max(1, (int)MathF.Round(CustomBubbleShellPixelWidth * scale)),
            Math.Max(1, (int)MathF.Round(CustomBubbleShellPixelHeight * scale)));
        var outlineBounds = new Rectangle(
            shellBounds.X - 4,
            shellBounds.Y - 4,
            shellBounds.Width + 8,
            shellBounds.Height + 8);
        var hasSlot = _customBubbleDocument.HasSlot(slotIndex);
        var outlineColor = selected && hasSlot
            ? new Color(255, 235, 145) * alpha
            : new Color(30, 30, 30) * (0.65f * alpha);

        DrawRoundedRectangleOutline(
            outlineBounds,
            new Color(0, 0, 0) * (0.35f * alpha),
            outlineColor,
            outlineThickness: selected && hasSlot ? 3 : 2,
            radius: 6);

        var shellFrame = GetCustomBubbleShellFrame();
        if (hasSlot
            && shellFrame is not null
            && TryGetLocalCustomBubbleTexture(slotIndex, out var artTexture))
        {
            DrawLoadedSpriteFrame(
                shellFrame,
                anchor,
                null,
                Color.White * alpha,
                0f,
                shellOrigin,
                new Vector2(scale),
                SpriteEffects.None,
                0f);
            _spriteBatch.Draw(artTexture, shellBounds, Color.White * alpha);
        }
        else
        {
            DrawInsetHudPanel(shellBounds, new Color(24, 24, 24) * (0.8f * alpha), new Color(58, 58, 58) * (0.6f * alpha));
        }

        DrawHudTextCentered((slotIndex + 1).ToString(CultureInfo.InvariantCulture), new Vector2(outlineBounds.X + 9f, outlineBounds.Y + 9f), Color.White * alpha, 1f);
    }

    private void UpdateBubbleMenuState(KeyboardState keyboard, MouseState mouse)
    {
        TryHandleBinocularsPlayerPing(mouse);

        if (ShouldCloseBubbleMenuForGameplayState())
        {
            BeginClosingBubbleMenu();
            AdvanceBubbleMenuAnimation();
            return;
        }

        var leftMousePressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        var leftMouseDown = mouse.LeftButton == ButtonState.Pressed;
        var leftMouseReleased = mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
        var callMedicPressed = IsBindingPressed(keyboard, mouse, _inputBindings.CallMedic)
            || (IsControllerGameplayInputActive() && IsControllerButtonPressed(ControllerCallMedicButton));
        var openZPressed = IsBindingPressed(keyboard, mouse, _inputBindings.OpenBubbleMenuZ);
        var openXPressed = IsBindingPressed(keyboard, mouse, _inputBindings.OpenBubbleMenuX);
        var openCPressed = IsBindingPressed(keyboard, mouse, _inputBindings.OpenBubbleMenuC);
        var pressAndClickBehavior = IsBubbleWheelPressAndClick();

        if (callMedicPressed)
        {
            ApplyLocalChatBubble(45);
            BeginClosingBubbleMenu();
        }

        HandleBubbleMenuOpenKeyPresses(openZPressed, openXPressed, openCPressed);

        if (_bubbleMenuKind != BubbleMenuKind.None && !_bubbleMenuClosing)
        {
            var pressedDigit = GetPressedDigit(keyboard);
            var pluginOverrideActive = _bubbleMenuKind != BubbleMenuKind.Custom && HasClientPluginBubbleMenuOverride();

            if (pluginOverrideActive)
            {
                var pluginResult = TryHandleClientPluginBubbleMenuInput(new ClientBubbleMenuInputState(
                    ToClientBubbleMenuKind(_bubbleMenuKind),
                    _bubbleMenuXPageIndex,
                    GetBubbleWheelPointerDirectionDegrees(mouse),
                    GetBubbleMenuPointerDistanceFromCenter(mouse),
                    leftMousePressed,
                    leftMouseDown,
                    leftMouseReleased,
                    pressedDigit,
                    keyboard.IsKeyDown(Keys.Q) && !_previousKeyboard.IsKeyDown(Keys.Q)));
                if (pluginResult is not null)
                {
                    ApplyBubbleMenuPluginResult(pluginResult);
                }

                if (!_bubbleMenuClosing
                    && pressedDigit.HasValue
                    && _bubbleMenuPendingFrame.HasValue)
                {
                    CommitBubbleMenuSelectionAndClose();
                }
                else if (!_bubbleMenuClosing
                    && pressAndClickBehavior
                    && leftMousePressed
                    && _bubbleMenuPendingFrame.HasValue)
                {
                    SuppressPrimaryFireUntilMouseRelease();
                    CommitBubbleMenuSelectionAndClose();
                }
                else if (!_bubbleMenuClosing
                    && !pressAndClickBehavior
                    && !IsCurrentBubbleMenuKeyHeld(keyboard, mouse))
                {
                    CommitBubbleMenuSelectionAndClose();
                }
            }
            else if (TryGetBubbleMenuSelection(keyboard, pressedDigit, out var bubbleFrame))
            {
                _bubbleMenuPendingFrame = bubbleFrame;
                _bubbleMenuSessionHadInteraction = true;
                CommitBubbleMenuSelectionAndClose();
            }
        }

        AdvanceBubbleMenuAnimation();
    }

    private bool IsBubbleWheelPressAndClick()
    {
        return GetBubbleWheelBehaviorSetting() == BubbleWheelBehavior.PressAndClick;
    }

    private void HandleBubbleMenuOpenKeyPresses(bool openZPressed, bool openXPressed, bool openCPressed)
    {
        if (openZPressed)
        {
            if (_bubbleMenuKind == BubbleMenuKind.Z && !_bubbleMenuClosing)
            {
                BeginClosingBubbleMenu();
            }
            else
            {
                OpenBubbleMenu(BubbleMenuKind.Z);
            }
        }
        else if (openXPressed)
        {
            if (_bubbleMenuKind == BubbleMenuKind.X && !_bubbleMenuClosing)
            {
                BeginClosingBubbleMenu();
            }
            else
            {
                OpenBubbleMenu(BubbleMenuKind.X);
            }
        }
        else if (openCPressed)
        {
            if (_bubbleMenuKind == BubbleMenuKind.C && !_bubbleMenuClosing)
            {
                BeginClosingBubbleMenu();
            }
            else
            {
                OpenBubbleMenu(BubbleMenuKind.C);
            }
        }
    }

    private bool IsCurrentBubbleMenuKeyHeld(KeyboardState keyboard, MouseState mouse)
    {
        return _bubbleMenuKind switch
        {
            BubbleMenuKind.Z => IsBindingDown(keyboard, mouse, _inputBindings.OpenBubbleMenuZ),
            BubbleMenuKind.X => IsBindingDown(keyboard, mouse, _inputBindings.OpenBubbleMenuX),
            BubbleMenuKind.C => IsBindingDown(keyboard, mouse, _inputBindings.OpenBubbleMenuC),
            BubbleMenuKind.Custom => IsBindingDown(keyboard, mouse, _inputBindings.CustomBubble),
            _ => true,
        };
    }

    private void ApplyBubbleMenuPluginResult(ClientBubbleMenuUpdateResult result)
    {
        if (result.ClearBubbleSelection)
        {
            _bubbleMenuPendingFrame = null;
            _bubbleMenuSessionHadInteraction = true;
        }

        if (result.NewXPageIndex.HasValue)
        {
            _bubbleMenuXPageIndex = Math.Clamp(result.NewXPageIndex.Value, 0, 2);
            _bubbleMenuSessionHadInteraction = true;
            _bubbleMenuPendingFrame = null;
        }

        if (result.BubbleFrame.HasValue)
        {
            _bubbleMenuPendingFrame = result.BubbleFrame.Value;
            _bubbleMenuSessionHadInteraction = true;
            return;
        }

        if (result.CloseMenu)
        {
            BeginClosingBubbleMenu();
        }
    }

    private void OpenBubbleMenu(BubbleMenuKind kind)
    {
        if (_bubbleMenuKind == kind && !_bubbleMenuClosing)
        {
            return;
        }

        _bubbleMenuKind = kind;
        _bubbleMenuAlpha = HasClientPluginBubbleMenuOverride() ? 0.99f : 0.01f;
        _bubbleMenuX = -30f;
        _bubbleMenuClosing = false;
        _bubbleMenuXPageIndex = 0;
        _bubbleMenuSessionHadInteraction = false;
        _bubbleMenuPendingFrame = null;
    }

    private void ResetBubbleMenuInteractionState()
    {
        _bubbleMenuKind = BubbleMenuKind.None;
        _bubbleMenuAlpha = 0.01f;
        _bubbleMenuX = -30f;
        _bubbleMenuClosing = false;
        _bubbleMenuXPageIndex = 0;
        _bubbleMenuSessionHadInteraction = false;
        _bubbleMenuPendingFrame = null;
        _suppressPrimaryFireUntilMouseRelease = false;
        ResetClientPluginBubbleMenuInputState();
    }

    private void FinishBubbleWheelMenuImmediately()
    {
        _bubbleMenuKind = BubbleMenuKind.None;
        _bubbleMenuAlpha = 0.01f;
        _bubbleMenuX = -30f;
        _bubbleMenuClosing = false;
        _bubbleMenuXPageIndex = 0;
        _bubbleMenuSessionHadInteraction = false;
        _bubbleMenuPendingFrame = null;
        ResetClientPluginBubbleMenuInputState();
    }

    private bool IsBubbleWheelPluginMenuActive()
    {
        return _bubbleMenuKind != BubbleMenuKind.Custom && HasClientPluginBubbleMenuOverride();
    }

    private void BeginClosingBubbleMenu()
    {
        if (_bubbleMenuKind == BubbleMenuKind.None)
        {
            return;
        }

        if (IsBubbleWheelPluginMenuActive())
        {
            FinishBubbleWheelMenuImmediately();
            return;
        }

        _bubbleMenuClosing = true;
        _bubbleMenuPendingFrame = null;
        ResetClientPluginBubbleMenuInputState();
    }

    private void CommitBubbleMenuSelectionAndClose()
    {
        if (_bubbleMenuPendingFrame.HasValue)
        {
            ApplyBubbleMenuFrame(_bubbleMenuKind, _bubbleMenuPendingFrame.Value, keepMenuOpen: false);
            return;
        }

        if (!_bubbleMenuSessionHadInteraction)
        {
            ApplyRecentBubbleFrame(_bubbleMenuKind);
            return;
        }

        BeginClosingBubbleMenu();
    }

    private void AdvanceBubbleMenuAnimation()
    {
        if (_bubbleMenuKind == BubbleMenuKind.None)
        {
            return;
        }

        if (!_bubbleMenuClosing)
        {
            if (IsBubbleWheelPluginMenuActive())
            {
                _bubbleMenuAlpha = 0.99f;
                return;
            }

            if (_bubbleMenuAlpha < 0.99f)
            {
                _bubbleMenuAlpha = AdvanceOpeningAlpha(_bubbleMenuAlpha, 0.01f, 0.99f);
            }

            if (_bubbleMenuX < 31f)
            {
                _bubbleMenuX = MathF.Min(31f, _bubbleMenuX + ScaleLegacyUiDistance(15f));
            }

            return;
        }

        if (_bubbleMenuAlpha > 0.01f)
        {
            _bubbleMenuAlpha = AdvanceClosingAlpha(_bubbleMenuAlpha, 0.01f);
        }

        _bubbleMenuX -= ScaleLegacyUiDistance(15f);
        if (_bubbleMenuX > -62f)
        {
            return;
        }

        _bubbleMenuKind = BubbleMenuKind.None;
        _bubbleMenuClosing = false;
        _bubbleMenuAlpha = 0.01f;
        _bubbleMenuX = -30f;
        _bubbleMenuXPageIndex = 0;
        _bubbleMenuSessionHadInteraction = false;
        _bubbleMenuPendingFrame = null;
    }

    private bool TryGetBubbleMenuSelection(KeyboardState keyboard, int? pressedDigit, out int bubbleFrame)
    {
        bubbleFrame = -1;

        switch (_bubbleMenuKind)
        {
            case BubbleMenuKind.Z:
                if (pressedDigit == 0)
                {
                    BeginClosingBubbleMenu();
                    return false;
                }

                if (pressedDigit is >= 1 and <= 9)
                {
                    bubbleFrame = 19 + pressedDigit.Value;
                    return true;
                }

                return false;

            case BubbleMenuKind.C:
                if (pressedDigit == 0)
                {
                    BeginClosingBubbleMenu();
                    return false;
                }

                if (pressedDigit is >= 1 and <= 9)
                {
                    bubbleFrame = 35 + pressedDigit.Value;
                    return true;
                }

                return false;

            case BubbleMenuKind.X:
                return TryGetBubbleMenuXSelection(keyboard, pressedDigit, out bubbleFrame);

            case BubbleMenuKind.Custom:
                return TryGetCustomBubbleMenuSelection(pressedDigit, out bubbleFrame);

            default:
                return false;
        }
    }

    private bool TryGetCustomBubbleMenuSelection(int? pressedDigit, out int bubbleFrame)
    {
        bubbleFrame = -1;
        if (pressedDigit == 0)
        {
            BeginClosingBubbleMenu();
            return false;
        }

        if (!pressedDigit.HasValue
            || pressedDigit.Value < 1
            || pressedDigit.Value > CustomBubbleDocument.SlotCount)
        {
            return false;
        }

        var slotIndex = pressedDigit.Value - 1;
        if (!_customBubbleDocument.HasSlot(slotIndex))
        {
            return false;
        }

        bubbleFrame = ChatBubbleFrameCatalog.GetCustomBubbleFrame(slotIndex);
        return true;
    }

    private bool TryGetBubbleMenuXSelection(KeyboardState keyboard, int? pressedDigit, out int bubbleFrame)
    {
        bubbleFrame = -1;
        if (_bubbleMenuXPageIndex == 0)
        {
            if (pressedDigit == 0)
            {
                BeginClosingBubbleMenu();
                return false;
            }

            if (pressedDigit == 1)
            {
                _bubbleMenuXPageIndex = 1;
                _bubbleMenuSessionHadInteraction = true;
                _bubbleMenuPendingFrame = null;
                return false;
            }

            if (pressedDigit == 2)
            {
                _bubbleMenuXPageIndex = 2;
                _bubbleMenuSessionHadInteraction = true;
                _bubbleMenuPendingFrame = null;
                return false;
            }

            if (pressedDigit is >= 3 and <= 9)
            {
                bubbleFrame = 26 + pressedDigit.Value;
                return true;
            }

            return false;
        }

        if (keyboard.IsKeyDown(Keys.Q) && !_previousKeyboard.IsKeyDown(Keys.Q))
        {
            bubbleFrame = _bubbleMenuXPageIndex == 2 ? 48 : 47;
            return true;
        }

        if (!pressedDigit.HasValue)
        {
            return false;
        }

        var offset = _bubbleMenuXPageIndex == 2 ? 10 : 0;
        bubbleFrame = pressedDigit.Value == 0
            ? 9 + offset
            : (pressedDigit.Value - 1) + offset;
        return true;
    }

    private int? GetPressedDigit(KeyboardState keyboard)
    {
        for (var digit = 0; digit <= 9; digit += 1)
        {
            var key = Keys.D0 + digit;
            if (keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key))
            {
                return digit;
            }

            var numPadKey = Keys.NumPad0 + digit;
            if (keyboard.IsKeyDown(numPadKey) && !_previousKeyboard.IsKeyDown(numPadKey))
            {
                return digit;
            }
        }

        return null;
    }

    private void ApplyBubbleMenuFrame(BubbleMenuKind kind, int bubbleFrame, bool keepMenuOpen)
    {
        if (bubbleFrame < 0)
        {
            return;
        }

        SetRecentBubbleFrame(kind, bubbleFrame);
        _bubbleMenuSessionHadInteraction = true;
        ApplyLocalChatBubble(bubbleFrame);
        if (!keepMenuOpen)
        {
            BeginClosingBubbleMenu();
        }
    }

    private void ApplyRecentBubbleFrame(BubbleMenuKind kind)
    {
        var recentBubbleFrame = GetRecentBubbleFrame(kind);
        if (recentBubbleFrame >= 0)
        {
            ApplyBubbleMenuFrame(kind, recentBubbleFrame, keepMenuOpen: false);
        }
    }

    private int GetRecentBubbleFrame(BubbleMenuKind kind)
    {
        return kind switch
        {
            BubbleMenuKind.Z => _recentBubbleFrameZ,
            BubbleMenuKind.X => _recentBubbleFrameX,
            BubbleMenuKind.C => _recentBubbleFrameC,
            BubbleMenuKind.Custom => _recentBubbleFrameCustom,
            _ => -1,
        };
    }

    private void SetRecentBubbleFrame(BubbleMenuKind kind, int bubbleFrame)
    {
        switch (kind)
        {
            case BubbleMenuKind.Z:
                _recentBubbleFrameZ = bubbleFrame;
                break;
            case BubbleMenuKind.X:
                _recentBubbleFrameX = bubbleFrame;
                break;
            case BubbleMenuKind.C:
                _recentBubbleFrameC = bubbleFrame;
                break;
            case BubbleMenuKind.Custom:
                _recentBubbleFrameCustom = bubbleFrame;
                break;
        }
    }

    private float GetBubbleMenuPointerDistanceFromCenter(MouseState mouse)
    {
        var center = GetBubbleMenuScreenCenter();
        var pointer = new Vector2(mouse.X, mouse.Y);
        return Vector2.Distance(pointer, center);
    }

    private Vector2 GetBubbleMenuScreenCenter()
    {
        return new Vector2(ViewportWidth / 2f, ViewportHeight / 2f);
    }

    private float GetBubbleWheelPointerDirectionDegrees(MouseState mouse)
    {
        var delta = new Vector2(mouse.X, mouse.Y) - GetBubbleMenuScreenCenter();
        var aimDirection = MathF.Atan2(delta.Y, delta.X) * (180f / MathF.PI) + 90f;
        while (aimDirection >= 360f)
        {
            aimDirection -= 360f;
        }

        while (aimDirection < 0f)
        {
            aimDirection += 360f;
        }

        return aimDirection;
    }

    private int GetBubbleWheelSelectedSlot(MouseState mouse)
    {
        if (GetBubbleMenuPointerDistanceFromCenter(mouse) < 30f)
        {
            return 0;
        }

        var aimDirection = GetBubbleWheelPointerDirectionDegrees(mouse);
        return Math.Clamp((int)(aimDirection / 40f) + 1, 1, 9);
    }

    private static ClientBubbleMenuKind ToClientBubbleMenuKind(BubbleMenuKind kind)
    {
        return kind switch
        {
            BubbleMenuKind.Z => ClientBubbleMenuKind.Z,
            BubbleMenuKind.X => ClientBubbleMenuKind.X,
            BubbleMenuKind.C => ClientBubbleMenuKind.C,
            _ => ClientBubbleMenuKind.None,
        };
    }

    private void ResetClientPluginBubbleMenuInputState()
    {
        _ = TryHandleClientPluginBubbleMenuInput(new ClientBubbleMenuInputState(
            ClientBubbleMenuKind.None,
            0,
            _world.LocalPlayer.AimDirectionDegrees,
            0f,
            LeftMousePressed: false,
            LeftMouseDown: false,
            LeftMouseReleased: false,
            PressedDigit: null,
            QPressed: false));
    }

    private void ApplyLocalChatBubble(int bubbleFrame)
    {
        if (_networkClient.IsConnected)
        {
            _networkClient.QueueChatBubble(bubbleFrame);
            return;
        }

        _world.SetLocalPlayerChatBubble(bubbleFrame);
    }

    // When the sniper has binoculars active, a left-click on a player emits the
    // speech bubble corresponding to that player's class, identical to selecting
    // it from the bubble menu manually.
    private void TryHandleBinocularsPlayerPing(MouseState mouse)
    {
        if (_world.LocalPlayer.ClassId != PlayerClass.Sniper
            || !GetPlayerIsUsingBinoculars(_world.LocalPlayer)
            || _bubbleMenuKind != BubbleMenuKind.None
            || !_world.LocalPlayer.IsAlive
            || _world.MatchState.IsEnded
            || _chatOpen)
        {
            return;
        }

        var leftMousePressed = mouse.LeftButton == ButtonState.Pressed
            && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!leftMousePressed)
        {
            return;
        }

        // Pick the player nearest to the mouse cursor's world position in the binoculars view.
        // _latestLocalAimWorldX/Y is already the cursor's world position (set by PrepareFrame).
        var pickWorldX = _hasLatestLocalAimWorldPosition ? _latestLocalAimWorldX : _binocularsFocusX;
        var pickWorldY = _hasLatestLocalAimWorldPosition ? _latestLocalAimWorldY : _binocularsFocusY;
        const float pickRadius = 30f;
        var bestDistanceSquared = pickRadius * pickRadius;
        PlayerEntity? target = null;

        foreach (var player in EnumerateRenderablePlayers())
        {
            if (ReferenceEquals(player, _world.LocalPlayer)
                || !player.IsAlive
                || GetPlayerVisibilityAlpha(player) <= 0f
                || IsSpyHiddenFromLocalViewer(player))
            {
                continue;
            }

            var renderPosition = GetRenderPosition(player);
            var dx = renderPosition.X - pickWorldX;
            var dy = renderPosition.Y - pickWorldY;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared > bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            target = player;
        }

        if (target is null)
        {
            return;
        }

        SuppressPrimaryFireUntilMouseRelease();

        // Bubble frames: Red 0-8, Blue 10-18 (offset +10). Class→frame index mapping:
        // Scout=0, Pyro=1, Soldier=2, Heavy=3, Demoman=4, Medic=5, Engineer=6, Spy=7, Sniper=8
        var classIndex = target.ClassId switch
        {
            PlayerClass.Scout    => 0,
            PlayerClass.Pyro     => 1,
            PlayerClass.Soldier  => 2,
            PlayerClass.Heavy    => 3,
            PlayerClass.Demoman  => 4,
            PlayerClass.Medic    => 5,
            PlayerClass.Engineer => 6,
            PlayerClass.Spy      => 7,
            PlayerClass.Sniper   => 8,
            _                    => 0,
        };
        var bubbleFrame = target.Team == PlayerTeam.Blue ? classIndex + 10 : classIndex;
        ApplyLocalChatBubble(bubbleFrame);
    }
}
