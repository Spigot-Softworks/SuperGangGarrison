#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private void EnableDebugMenu()
    {
        _debugMenuEnabled = true;
    }

    private void DisableDebugMenu()
    {
        _debugMenuEnabled = false;
        _debugMenuController.CloseDebugMenu();
    }

    private void OpenDebugMenu()
    {
        if (!_debugMenuEnabled)
        {
            return;
        }

        _debugMenuController.OpenDebugMenu();
    }

    private void CloseDebugMenu()
    {
        _debugMenuController.CloseDebugMenu();
    }

    private void UpdateDebugMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse)
    {
        if (!_debugMenuEnabled || !_debugMenuOpen)
        {
            return;
        }

        _debugMenuController.UpdateDebugMenu(keyboard, mouse);
    }

    private void DrawDebugMenu()
    {
        if (!_debugMenuEnabled || !_debugMenuOpen)
        {
            return;
        }

        _debugMenuController.DrawDebugMenu();
    }
}
