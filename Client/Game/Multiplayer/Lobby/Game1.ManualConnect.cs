#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private int _manualConnectControllerIndex;

    private void UpdateManualConnectMenu(KeyboardState keyboard, MouseState mouse)
    {
        GetManualConnectLayout(
            out _,
            out var hostBounds,
            out var portBounds,
            out var connectBounds,
            out var backBounds,
            out _);

        if ((keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
            || IsControllerMenuBackPressed())
        {
            CloseManualConnectMenuToOrigin(clearStatus: false);
            return;
        }

        if (keyboard.IsKeyDown(Keys.Tab) && !_previousKeyboard.IsKeyDown(Keys.Tab))
        {
            if (_lastToDieRoomCodeJoinOpen)
            {
                _connectionFlowController.SetManualConnectEditingField(editHost: true);
            }
            else
            {
                _connectionFlowController.ToggleManualConnectEditingField();
            }
        }

        if (keyboard.IsKeyDown(Keys.Enter) && !_previousKeyboard.IsKeyDown(Keys.Enter))
        {
            TryConnectFromMenu();
            return;
        }

        if (TryUpdateManualConnectControllerInput())
        {
            return;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed)
        {
            return;
        }

        var point = new Point(mouse.X, mouse.Y);
        if (hostBounds.Contains(point))
        {
            _manualConnectControllerIndex = 0;
            _connectionFlowController.SetManualConnectEditingField(editHost: true);
            if (IsTextFieldDoubleClick(TextFieldClickTarget.ManualConnectHost))
            {
                SelectAllTextInActiveField(TextFieldClickTarget.ManualConnectHost);
            }
        }
        else if (portBounds.Contains(point))
        {
            _manualConnectControllerIndex = 1;
            _connectionFlowController.SetManualConnectEditingField(editHost: false);
            if (IsTextFieldDoubleClick(TextFieldClickTarget.ManualConnectPort))
            {
                SelectAllTextInActiveField(TextFieldClickTarget.ManualConnectPort);
            }
        }
        else
        {
            ResetTextFieldClickTarget();
            if (connectBounds.Contains(point))
            {
                _manualConnectControllerIndex = _lastToDieRoomCodeJoinOpen ? 1 : 2;
                TryConnectFromMenu();
            }
            else if (backBounds.Contains(point))
            {
                _manualConnectControllerIndex = _lastToDieRoomCodeJoinOpen ? 2 : 3;
                CloseManualConnectMenuToOrigin(clearStatus: false);
            }
        }
    }

    private bool TryUpdateManualConnectControllerInput()
    {
        if (!IsControllerMenuInputActive())
        {
            return false;
        }

        if (TryConsumeControllerMenuNavigation(out var horizontalStep, out var verticalStep))
        {
            var step = verticalStep != 0 ? verticalStep : horizontalStep;
            if (step != 0)
            {
                var itemCount = _lastToDieRoomCodeJoinOpen ? 3 : 4;
                _manualConnectControllerIndex = MoveControllerMenuSelectionClamped(
                    _manualConnectControllerIndex,
                    itemCount,
                    step);
                ApplyManualConnectControllerSelection();
                return true;
            }
        }

        if (!IsControllerMenuConfirmPressed())
        {
            return false;
        }

        if (_lastToDieRoomCodeJoinOpen)
        {
            switch (_manualConnectControllerIndex)
            {
                case 0:
                    _connectionFlowController.SetManualConnectEditingField(editHost: true);
                    break;
                case 1:
                    TryConnectFromMenu();
                    break;
                default:
                    CloseManualConnectMenuToOrigin(clearStatus: false);
                    break;
            }

            return true;
        }

        switch (_manualConnectControllerIndex)
        {
            case 0:
                _connectionFlowController.SetManualConnectEditingField(editHost: true);
                break;
            case 1:
                _connectionFlowController.SetManualConnectEditingField(editHost: false);
                break;
            case 2:
                TryConnectFromMenu();
                break;
            default:
                CloseManualConnectMenuToOrigin(clearStatus: false);
                break;
        }

        return true;
    }

    private void ApplyManualConnectControllerSelection()
    {
        if (_manualConnectControllerIndex == 0)
        {
            _connectionFlowController.SetManualConnectEditingField(editHost: true);
        }
        else if (!_lastToDieRoomCodeJoinOpen && _manualConnectControllerIndex == 1)
        {
            _connectionFlowController.SetManualConnectEditingField(editHost: false);
        }
        else
        {
            _connectionFlowController.DisableManualConnectEditing();
        }
    }

    private void DrawManualConnectMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.86f);

        // Draw bottom bar and runners (in animated mode only) - behind everything else
        if (_menuBackgroundMode != MenuBackgroundMode.Static)
        {
            const int bottomBarHeight = 76;
            var barY = viewportHeight - bottomBarHeight;
            var bottomBarBounds = new Rectangle(0, barY, viewportWidth, bottomBarHeight);
            _spriteBatch.Draw(
                _pixel,
                bottomBarBounds,
                _lastToDieRoomCodeJoinOpen
                    ? new Color(0x4b, 0x4d, 0x50)
                    : new Color(0x57, 0x4f, 0x47));
            _menuBottomBarRunners.Draw(bottomBarBounds);
        }

        GetManualConnectLayout(
            out var panel,
            out var hostBounds,
            out var portBounds,
            out var connectBounds,
            out var backBounds,
            out var compactLayout);
        const float labelScale = 1f;
        const float buttonScale = 1f;
        var mouse = GetFrameMouseState();
        DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        if (_lastToDieRoomCodeJoinOpen)
        {
            DrawBitmapFontText(
                "Join Last to Die",
                new Vector2(panel.X + 24f, panel.Y + 22f),
                Color.White,
                1.15f);
        }

        DrawBitmapFontText(
            _lastToDieRoomCodeJoinOpen ? "Room Code" : "Host or OG2 Friend Code",
            new Vector2(hostBounds.X, hostBounds.Y - 16f),
            Color.White,
            labelScale);
        if (!_lastToDieRoomCodeJoinOpen)
        {
            DrawBitmapFontText("Port (direct connection only)", new Vector2(portBounds.X, portBounds.Y - 16f), Color.White, labelScale);
        }

        DrawMenuInputBoxScaled(
            hostBounds,
            _connectHostBuffer,
            _editingConnectHost || hostBounds.Contains(mouse.Position),
            buttonScale,
            _connectHostCursorIndex,
            _connectHostSelectionStart);
        if (!_lastToDieRoomCodeJoinOpen)
        {
            DrawMenuInputBoxScaled(
                portBounds,
                _connectPortBuffer,
                _editingConnectPort || portBounds.Contains(mouse.Position),
                buttonScale,
                _connectPortCursorIndex,
                _connectPortSelectionStart);
        }
        DrawMenuButtonScaled(
            connectBounds,
            _lastToDieRoomCodeJoinOpen ? "Join" : "Connect",
            (IsControllerMenuInputActive()
                && _manualConnectControllerIndex == (_lastToDieRoomCodeJoinOpen ? 1 : 2))
                || connectBounds.Contains(mouse.Position),
            buttonScale);
        DrawMenuButtonScaled(
            backBounds,
            "Back",
            (IsControllerMenuInputActive()
                && _manualConnectControllerIndex == (_lastToDieRoomCodeJoinOpen ? 2 : 3))
                || backBounds.Contains(mouse.Position),
            buttonScale);

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawBitmapFontText(_menuStatusMessage, new Vector2(panel.X + 24f, panel.Bottom - (compactLayout ? 34f : 38f)), new Color(230, 220, 180), 1f);
        }
    }

    private void GetManualConnectLayout(
        out Rectangle panel,
        out Rectangle hostBounds,
        out Rectangle portBounds,
        out Rectangle connectBounds,
        out Rectangle backBounds,
        out bool compactLayout)
    {
        var panelWidth = System.Math.Min(ViewportWidth - 32, 560);
        var desiredPanelHeight = _lastToDieRoomCodeJoinOpen
            ? 240
            : ViewportHeight < 540 ? 260 : 320;
        var panelHeight = System.Math.Min(ViewportHeight - 32, desiredPanelHeight);
        panel = new Rectangle(
            (ViewportWidth - panelWidth) / 2,
            (ViewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);

        compactLayout = panel.Height < 300 || panel.Width < 520;
        var padding = compactLayout ? 20 : 28;
        var fieldHeight = compactLayout ? 32 : 36;
        var buttonHeight = compactLayout ? 36 : 42;
        var buttonGap = compactLayout ? 12 : 20;
        var buttonWidth = (panel.Width - (padding * 2) - buttonGap) / 2;
        var contentTop = panel.Y + (_lastToDieRoomCodeJoinOpen
            ? compactLayout ? 80 : 92
            : compactLayout ? 58 : 74);
        hostBounds = new Rectangle(panel.X + padding, contentTop, panel.Width - (padding * 2), fieldHeight);
        portBounds = _lastToDieRoomCodeJoinOpen
            ? Rectangle.Empty
            : new Rectangle(
                panel.X + padding,
                hostBounds.Bottom + (compactLayout ? 42 : 52),
                System.Math.Min(220, hostBounds.Width),
                fieldHeight);
        connectBounds = new Rectangle(panel.X + padding, panel.Bottom - padding - buttonHeight - 6, buttonWidth, buttonHeight);
        backBounds = new Rectangle(connectBounds.Right + buttonGap, connectBounds.Y, buttonWidth, buttonHeight);
    }

    private void DrawPasswordPrompt()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.7f);

        var panelWidth = Math.Max(1, Math.Min(viewportWidth - 32, 520));
        var panelHeight = Math.Max(1, Math.Min(viewportHeight - 32, 220));
        var panel = new Rectangle(
            (viewportWidth - panelWidth) / 2,
            (viewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);
        _spriteBatch.Draw(_pixel, panel, new Color(34, 35, 39, 240));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText("Server Password", new Vector2(panel.X + 28f, panel.Y + 24f), Color.White, 1f);
        DrawBitmapFontText("Enter password to continue.", new Vector2(panel.X + 28f, panel.Y + 54f), new Color(200, 200, 200), 0.9f);

        var masked = new string('*', _passwordEditBuffer.Length);
        DrawMenuInputBoxScaled(
            new Rectangle(panel.X + 28, panel.Y + 92, Math.Max(1, panel.Width - 56), 36),
            masked,
            active: true,
            1f,
            _passwordEditCursorIndex,
            _passwordEditSelectionStart);
        DrawBitmapFontText("Press Enter to submit, Esc to cancel.", new Vector2(panel.X + 28f, panel.Y + 142f), new Color(200, 200, 200), 0.85f);

        if (!string.IsNullOrWhiteSpace(_passwordPromptMessage))
        {
            DrawBitmapFontText(_passwordPromptMessage, new Vector2(panel.X + 28f, panel.Bottom - 36f), new Color(230, 220, 180), 0.9f);
        }
    }
}
