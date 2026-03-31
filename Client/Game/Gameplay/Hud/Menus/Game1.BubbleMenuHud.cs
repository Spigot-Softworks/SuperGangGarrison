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
        if (_mainMenuOpen || _inGameMenuOpen || _optionsMenuOpen || _pluginOptionsMenuOpen || _controlsMenuOpen || _consoleOpen || _chatOpen || _teamSelectOpen || _classSelectOpen || _passwordPromptOpen || _world.LocalPlayerAwaitingJoin || !_world.LocalPlayer.IsAlive || _world.MatchState.IsEnded || (_killCamEnabled && _world.LocalDeathCam is not null))
        {
            BeginClosingBubbleMenu();
            AdvanceBubbleMenuAnimation();
            return;
        }

        var leftClickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        var callMedicPressed = keyboard.IsKeyDown(_inputBindings.CallMedic) && !_previousKeyboard.IsKeyDown(_inputBindings.CallMedic);
        var openZPressed = keyboard.IsKeyDown(_inputBindings.OpenBubbleMenuZ) && !_previousKeyboard.IsKeyDown(_inputBindings.OpenBubbleMenuZ);
        var openXPressed = keyboard.IsKeyDown(_inputBindings.OpenBubbleMenuX) && !_previousKeyboard.IsKeyDown(_inputBindings.OpenBubbleMenuX);
        var openCPressed = keyboard.IsKeyDown(_inputBindings.OpenBubbleMenuC) && !_previousKeyboard.IsKeyDown(_inputBindings.OpenBubbleMenuC);

        if (callMedicPressed)
        {
            ApplyLocalChatBubble(45);
            BeginClosingBubbleMenu();
        }

        if (openZPressed)
        {
            ToggleBubbleMenu(BubbleMenuKind.Z);
        }
        else if (openXPressed)
        {
            ToggleBubbleMenu(BubbleMenuKind.X);
        }
        else if (openCPressed)
        {
            ToggleBubbleMenu(BubbleMenuKind.C);
        }

        if (_bubbleMenuKind != BubbleMenuKind.None && !_bubbleMenuClosing)
        {
            var pluginResult = TryHandleClientPluginBubbleMenuInput(new ClientBubbleMenuInputState(
                ToClientBubbleMenuKind(_bubbleMenuKind),
                _bubbleMenuXPageIndex,
                _world.LocalPlayer.AimDirectionDegrees,
                GetBubbleMenuPointerDistanceFromCenter(mouse),
                leftClickPressed,
                GetPressedDigit(keyboard),
                keyboard.IsKeyDown(Keys.Q) && !_previousKeyboard.IsKeyDown(Keys.Q)));
            if (pluginResult is not null)
            {
                if (leftClickPressed)
                {
                    SuppressPrimaryFireUntilMouseRelease();
                }

                ApplyBubbleMenuPluginResult(pluginResult);
                AdvanceBubbleMenuAnimation();
                return;
            }
        }

        if (_bubbleMenuKind != BubbleMenuKind.None && !_bubbleMenuClosing && TryGetBubbleMenuSelection(keyboard, out var bubbleFrame))
        {
            if (_networkClient.IsConnected)
            {
                _networkClient.QueueChatBubble(bubbleFrame);
            }
            else
            {
                _world.SetLocalPlayerChatBubble(bubbleFrame);
            }

            BeginClosingBubbleMenu();
        }

        AdvanceBubbleMenuAnimation();
    }

    private void ApplyBubbleMenuPluginResult(ClientBubbleMenuUpdateResult result)
    {
        if (result.NewXPageIndex.HasValue)
        {
            _bubbleMenuXPageIndex = Math.Clamp(result.NewXPageIndex.Value, 0, 2);
        }

        if (result.BubbleFrame.HasValue)
        {
            ApplyLocalChatBubble(result.BubbleFrame.Value);
            BeginClosingBubbleMenu();
            return;
        }

        if (result.CloseMenu)
        {
            BeginClosingBubbleMenu();
        }
    }

    private void ToggleBubbleMenu(BubbleMenuKind kind)
    {
        if (_bubbleMenuKind == kind && !_bubbleMenuClosing)
        {
            BeginClosingBubbleMenu();
            return;
        }

        _bubbleMenuKind = kind;
        _bubbleMenuAlpha = 0.01f;
        _bubbleMenuX = -30f;
        _bubbleMenuClosing = false;
        _bubbleMenuXPageIndex = 0;
    }

    private void BeginClosingBubbleMenu()
    {
        if (_bubbleMenuKind == BubbleMenuKind.None)
        {
            return;
        }

        _bubbleMenuClosing = true;
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
    }

    private bool TryGetBubbleMenuSelection(KeyboardState keyboard, out int bubbleFrame)
    {
        bubbleFrame = -1;
        var pressedDigit = GetPressedDigit(keyboard);

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
                return false;
            }

            if (pressedDigit == 2)
            {
                _bubbleMenuXPageIndex = 2;
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
