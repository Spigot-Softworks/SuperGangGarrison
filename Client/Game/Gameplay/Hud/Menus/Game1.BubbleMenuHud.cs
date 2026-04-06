#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawBubbleMenuHud()
    {
        if (_bubbleMenuKind == BubbleMenuKind.None)
        {
            return;
        }

        var renderState = new ClientBubbleMenuRenderState(
            ToClientBubbleMenuKind(_bubbleMenuKind),
            _bubbleMenuAlpha,
            _bubbleMenuXPageIndex,
            _world.LocalPlayer.AimDirectionDegrees,
            GetBubbleWheelSelectedSlot(GetScaledMouseState(GetConstrainedMouseState(Mouse.GetState()))));
        if (TryDrawClientPluginBubbleMenu(GetCurrentClientPluginCameraTopLeft(), renderState))
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

        if (spriteName is null)
        {
            return;
        }

        var frameIndex = _bubbleMenuKind == BubbleMenuKind.X && _bubbleMenuXPageIndex == 2 ? 1 : 0;
        TryDrawScreenSprite(spriteName, frameIndex, new Vector2(_bubbleMenuX, viewportHeight / 2f), Color.White * _bubbleMenuAlpha, Vector2.One);
    }

    private void UpdateBubbleMenuState(KeyboardState keyboard, MouseState mouse)
    {
        if (ShouldCloseBubbleMenuForGameplayState())
        {
            BeginClosingBubbleMenu();
            AdvanceBubbleMenuAnimation();
            return;
        }

        var leftMousePressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        var leftMouseDown = mouse.LeftButton == ButtonState.Pressed;
        var callMedicPressed = keyboard.IsKeyDown(_inputBindings.CallMedic) && !_previousKeyboard.IsKeyDown(_inputBindings.CallMedic);
        var openZPressed = keyboard.IsKeyDown(_inputBindings.OpenBubbleMenuZ) && !_previousKeyboard.IsKeyDown(_inputBindings.OpenBubbleMenuZ);
        var openXPressed = keyboard.IsKeyDown(_inputBindings.OpenBubbleMenuX) && !_previousKeyboard.IsKeyDown(_inputBindings.OpenBubbleMenuX);
        var openCPressed = keyboard.IsKeyDown(_inputBindings.OpenBubbleMenuC) && !_previousKeyboard.IsKeyDown(_inputBindings.OpenBubbleMenuC);

        if (callMedicPressed)
        {
            ApplyLocalChatBubble(45);
            BeginClosingBubbleMenu();
        }

        HandleBubbleMenuOpenKeyPresses(openZPressed, openXPressed, openCPressed);

        if (_bubbleMenuKind != BubbleMenuKind.None && !_bubbleMenuClosing)
        {
            var pressedDigit = GetPressedDigit(keyboard);
            var pluginOverrideActive = HasClientPluginBubbleMenuOverride();
            var menuKeyHeld = IsCurrentBubbleMenuKeyHeld(keyboard);
            var holdStillActive = menuKeyHeld || leftMouseDown;

            if (pluginOverrideActive)
            {
                var pluginResult = TryHandleClientPluginBubbleMenuInput(new ClientBubbleMenuInputState(
                    ToClientBubbleMenuKind(_bubbleMenuKind),
                    _bubbleMenuXPageIndex,
                    _world.LocalPlayer.AimDirectionDegrees,
                    GetBubbleMenuPointerDistanceFromCenter(mouse),
                    leftMousePressed,
                    leftMouseDown,
                    LeftMouseReleased: false,
                    pressedDigit,
                    keyboard.IsKeyDown(Keys.Q) && !_previousKeyboard.IsKeyDown(Keys.Q)));
                if (pluginResult is not null)
                {
                    if (leftMousePressed)
                    {
                        SuppressPrimaryFireUntilMouseRelease();
                    }

                    ApplyBubbleMenuPluginResult(pluginResult);
                }
            }
            else if (TryGetBubbleMenuSelection(keyboard, pressedDigit, out var bubbleFrame))
            {
                _bubbleMenuPendingFrame = bubbleFrame;
                _bubbleMenuSessionHadInteraction = true;
            }

            if (!holdStillActive)
            {
                CommitBubbleMenuSelectionAndClose();
            }
        }

        AdvanceBubbleMenuAnimation();
    }

    private void HandleBubbleMenuOpenKeyPresses(bool openZPressed, bool openXPressed, bool openCPressed)
    {
        if (openZPressed)
        {
            OpenBubbleMenu(BubbleMenuKind.Z);
        }
        else if (openXPressed)
        {
            OpenBubbleMenu(BubbleMenuKind.X);
        }
        else if (openCPressed)
        {
            OpenBubbleMenu(BubbleMenuKind.C);
        }
    }

    private bool IsCurrentBubbleMenuKeyHeld(KeyboardState keyboard)
    {
        return _bubbleMenuKind switch
        {
            BubbleMenuKind.Z => keyboard.IsKeyDown(_inputBindings.OpenBubbleMenuZ),
            BubbleMenuKind.X => keyboard.IsKeyDown(_inputBindings.OpenBubbleMenuX),
            BubbleMenuKind.C => keyboard.IsKeyDown(_inputBindings.OpenBubbleMenuC),
            _ => true,
        };
    }

    private void ApplyBubbleMenuPluginResult(ClientBubbleMenuUpdateResult result)
    {
        if (result.ClearBubbleSelection)
        {
            _bubbleMenuPendingFrame = null;
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
        _bubbleMenuAlpha = 0.01f;
        _bubbleMenuX = -30f;
        _bubbleMenuClosing = false;
        _bubbleMenuXPageIndex = 0;
        _bubbleMenuSessionHadInteraction = false;
        _bubbleMenuPendingFrame = null;
    }

    private void BeginClosingBubbleMenu()
    {
        if (_bubbleMenuKind == BubbleMenuKind.None)
        {
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

            default:
                return false;
        }
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
        }
    }

    private float GetBubbleMenuPointerDistanceFromCenter(MouseState mouse)
    {
        var center = new Vector2(ViewportWidth / 2f, ViewportHeight / 2f);
        var pointer = new Vector2(mouse.X, mouse.Y);
        return Vector2.Distance(pointer, center);
    }

    private int GetBubbleWheelSelectedSlot(MouseState mouse)
    {
        if (GetBubbleMenuPointerDistanceFromCenter(mouse) < 30f)
        {
            return 0;
        }

        var aimDirection = _world.LocalPlayer.AimDirectionDegrees + 90f;
        while (aimDirection >= 360f)
        {
            aimDirection -= 360f;
        }

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
}
