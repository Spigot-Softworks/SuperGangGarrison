#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdateManualConnectMenu(KeyboardState keyboard, MouseState mouse)
    {
        GetManualConnectLayout(
            out _,
            out var hostBounds,
            out var portBounds,
            out var connectBounds,
            out var backBounds,
            out _);

        if (keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
        {
            CloseManualConnectMenu(clearStatus: false);
            return;
        }

        if (keyboard.IsKeyDown(Keys.Tab) && !_previousKeyboard.IsKeyDown(Keys.Tab))
        {
            var editHost = !_editingConnectHost;
            _editingConnectHost = editHost;
            _editingConnectPort = !editHost;
        }

        if (keyboard.IsKeyDown(Keys.Enter) && !_previousKeyboard.IsKeyDown(Keys.Enter))
        {
            TryConnectFromMenu();
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
            _editingConnectHost = true;
            _editingConnectPort = false;
        }
        else if (portBounds.Contains(point))
        {
            _editingConnectHost = false;
            _editingConnectPort = true;
        }
        else if (connectBounds.Contains(point))
        {
            TryConnectFromMenu();
        }
        else if (backBounds.Contains(point))
        {
            CloseManualConnectMenu(clearStatus: false);
        }
    }

    private void DrawManualConnectMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.78f);

        GetManualConnectLayout(
            out var panel,
            out var hostBounds,
            out var portBounds,
            out var connectBounds,
            out var backBounds,
            out var compactLayout);
        var titleScale = compactLayout ? 0.94f : 1f;
        var labelScale = compactLayout ? 0.84f : 1f;
        var buttonScale = compactLayout ? 0.9f : 1f;
        var infoScale = compactLayout ? 0.78f : 0.9f;
        _spriteBatch.Draw(_pixel, panel, new Color(34, 35, 39, 235));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText("Join (manual)", new Vector2(panel.X + 24f, panel.Y + 22f), Color.White, titleScale);
        DrawBitmapFontText("Connect straight to a known server address.", new Vector2(panel.X + 24f, panel.Y + 48f), new Color(210, 210, 210), infoScale);
        DrawBitmapFontText("Host", new Vector2(hostBounds.X, hostBounds.Y - 16f), Color.White, labelScale);
        DrawBitmapFontText("Port", new Vector2(portBounds.X, portBounds.Y - 16f), Color.White, labelScale);

        DrawMenuInputBoxScaled(hostBounds, _connectHostBuffer, _editingConnectHost, buttonScale);
        DrawMenuInputBoxScaled(portBounds, _connectPortBuffer, _editingConnectPort, buttonScale);
        DrawMenuButtonScaled(connectBounds, "Connect", false, buttonScale);
        DrawMenuButtonScaled(backBounds, "Back", false, buttonScale);

        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            DrawBitmapFontText(_menuStatusMessage, new Vector2(panel.X + 24f, panel.Bottom - (compactLayout ? 34f : 38f)), new Color(230, 220, 180), infoScale);
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
        var panelHeight = System.Math.Min(ViewportHeight - 32, ViewportHeight < 540 ? 260 : 320);
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
        var contentTop = panel.Y + (compactLayout ? 92 : 116);
        hostBounds = new Rectangle(panel.X + padding, contentTop, panel.Width - (padding * 2), fieldHeight);
        portBounds = new Rectangle(panel.X + padding, hostBounds.Bottom + (compactLayout ? 42 : 52), System.Math.Min(220, hostBounds.Width), fieldHeight);
        connectBounds = new Rectangle(panel.X + padding, panel.Bottom - padding - buttonHeight - 6, buttonWidth, buttonHeight);
        backBounds = new Rectangle(connectBounds.Right + buttonGap, connectBounds.Y, buttonWidth, buttonHeight);
    }

    private void DrawPasswordPrompt()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.7f);

        var panelWidth = 520;
        var panelHeight = 220;
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
        DrawMenuInputBox(new Rectangle(panel.X + 28, panel.Y + 92, panel.Width - 56, 36), masked, active: true);
        DrawBitmapFontText("Press Enter to submit, Esc to cancel.", new Vector2(panel.X + 28f, panel.Y + 142f), new Color(200, 200, 200), 0.85f);

        if (!string.IsNullOrWhiteSpace(_passwordPromptMessage))
        {
            DrawBitmapFontText(_passwordPromptMessage, new Vector2(panel.X + 28f, panel.Bottom - 36f), new Color(230, 220, 180), 0.9f);
        }
    }
}
