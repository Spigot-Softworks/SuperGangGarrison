#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    public void EnableDebugMenu()
    {
        _debugMenuEnabled = true;
    }

    public void DisableDebugMenu()
    {
        _debugMenuEnabled = false;
        _menuManager.DebugMenu.CloseDebugMenu();
    }

    public void OpenDebugMenu()
    {
        if (!_debugMenuEnabled)
        {
            return;
        }

        _menuManager.DebugMenu.OpenDebugMenu();
    }

    private void CloseDebugMenu()
    {
        _menuManager.DebugMenu.CloseDebugMenu();
    }

    public void UpdateDebugMenu(Microsoft.Xna.Framework.Input.KeyboardState keyboard, Microsoft.Xna.Framework.Input.MouseState mouse)
    {
        if (!_debugMenuEnabled || !_debugMenuOpen)
        {
            return;
        }

        _menuManager.DebugMenu.UpdateDebugMenu(keyboard, mouse);
    }

    private void DrawDebugMenu()
    {
        if (!_debugMenuEnabled || !_debugMenuOpen)
        {
            return;
        }

        _menuManager.DebugMenu.DrawDebugMenu();
    }
}
