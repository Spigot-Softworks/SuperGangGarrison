#nullable enable

using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdateHostSetupMenu(MouseState mouse)
    {
        var layout = HostSetupMenuLayoutCalculator.CreateMenuLayout(ViewportWidth, ViewportHeight, _hostMapEntries.Count, IsServerLauncherMode);
        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;

        if (TryHandleHostSetupTabClick(mouse, clickPressed, layout))
        {
            return;
        }

        if (IsServerLauncherMode && _hostSetupTab == HostSetupTab.ServerConsole)
        {
            HandleHostedServerConsoleMenuClick(mouse, clickPressed, layout);
            return;
        }

        UpdateHostSetupHoverIndex(mouse, layout);
        if (!clickPressed)
        {
            return;
        }

        HandleHostSetupSettingsMenuClick(mouse, layout);
    }

    private bool TryHandleHostSetupTabClick(MouseState mouse, bool clickPressed, HostSetupMenuLayout layout)
    {
        if (!IsServerLauncherMode || !clickPressed)
        {
            return false;
        }

        var tabLayout = HostSetupMenuLayoutCalculator.CreateServerLauncherTabLayout(layout.Panel);
        if (tabLayout.SettingsTabBounds.Contains(mouse.Position))
        {
            _hostSetupTab = HostSetupTab.Settings;
            _hostSetupEditField = HostSetupEditField.ServerName;
            return true;
        }

        if (tabLayout.ConsoleTabBounds.Contains(mouse.Position))
        {
            _hostSetupTab = HostSetupTab.ServerConsole;
            _hostSetupEditField = HostSetupEditField.ServerConsoleCommand;
            return true;
        }

        return false;
    }

    private void HandleHostedServerConsoleMenuClick(MouseState mouse, bool clickPressed, HostSetupMenuLayout layout)
    {
        if (!clickPressed)
        {
            return;
        }

        var consoleLayout = HostSetupMenuLayoutCalculator.CreateHostedServerConsoleLayout(layout.Panel);
        if (consoleLayout.CommandBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.ServerConsoleCommand;
            return;
        }

        if (consoleLayout.SendBounds.Contains(mouse.Position))
        {
            ExecuteHostedServerCommandFromUi(_hostedServerConsole.CreateSnapshot().CommandInput);
            return;
        }

        if (consoleLayout.ClearBounds.Contains(mouse.Position))
        {
            ClearHostedServerConsoleView();
            _menuStatusMessage = "Console view cleared.";
            return;
        }

        if (consoleLayout.StatusCommandBounds.Contains(mouse.Position))
        {
            ExecuteHostedServerCommandFromUi("status");
            return;
        }

        if (consoleLayout.PlayersCommandBounds.Contains(mouse.Position))
        {
            ExecuteHostedServerCommandFromUi("players");
            return;
        }

        if (consoleLayout.RotationCommandBounds.Contains(mouse.Position))
        {
            ExecuteHostedServerCommandFromUi("rotation");
            return;
        }

        if (consoleLayout.HelpCommandBounds.Contains(mouse.Position))
        {
            ExecuteHostedServerCommandFromUi("help");
            return;
        }

        if (!IsHostedServerRunning && consoleLayout.HostBounds.Contains(mouse.Position))
        {
            TryHostFromSetup();
            return;
        }

        if (!IsHostedServerRunning && layout.TerminalButtonBounds.Contains(mouse.Position))
        {
            TryHostFromSetup(runInTerminal: true);
            return;
        }

        if (consoleLayout.BackBounds.Contains(mouse.Position))
        {
            CloseHostSetupMenuFromBackAction();
        }
    }

    private void UpdateHostSetupHoverIndex(MouseState mouse, HostSetupMenuLayout layout)
    {
        _hostSetupHoverIndex = -1;
        if (!layout.ListRowsBounds.Contains(mouse.Position))
        {
            return;
        }

        var row = (mouse.Y - layout.ListRowsBounds.Y) / layout.RowHeight;
        if (row >= 0 && row < _hostMapEntries.Count)
        {
            _hostSetupHoverIndex = row;
        }
    }

    private void HandleHostSetupSettingsMenuClick(MouseState mouse, HostSetupMenuLayout layout)
    {
        if (layout.ServerNameBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.ServerName;
            return;
        }

        if (layout.PortBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.Port;
            return;
        }

        if (layout.SlotsBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.Slots;
            return;
        }

        if (layout.PasswordBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.Password;
            return;
        }

        if (layout.RotationFileBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.MapRotationFile;
            return;
        }

        if (layout.TimeLimitBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.TimeLimit;
            return;
        }

        if (layout.CapLimitBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.CapLimit;
            return;
        }

        if (layout.RespawnBounds.Contains(mouse.Position))
        {
            _hostSetupEditField = HostSetupEditField.RespawnSeconds;
            return;
        }

        if (_hostSetupHoverIndex >= 0 && layout.ListRowsBounds.Contains(mouse.Position))
        {
            _hostMapIndex = _hostSetupHoverIndex;
            _hostSetupEditField = HostSetupEditField.None;
            return;
        }

        if (layout.ToggleBounds.Contains(mouse.Position))
        {
            ToggleSelectedHostMap();
            return;
        }

        if (layout.MoveUpBounds.Contains(mouse.Position))
        {
            MoveSelectedHostMap(-1);
            return;
        }

        if (layout.MoveDownBounds.Contains(mouse.Position))
        {
            MoveSelectedHostMap(1);
            return;
        }

        if (layout.LobbyBounds.Contains(mouse.Position))
        {
            _hostLobbyAnnounceEnabled = !_hostLobbyAnnounceEnabled;
            return;
        }

        if (layout.AutoBalanceBounds.Contains(mouse.Position))
        {
            _hostAutoBalanceEnabled = !_hostAutoBalanceEnabled;
            return;
        }

        if (!IsHostedServerRunning && layout.HostBounds.Contains(mouse.Position))
        {
            TryHostFromSetup();
            return;
        }

        if (IsServerLauncherMode && !IsHostedServerRunning && layout.TerminalButtonBounds.Contains(mouse.Position))
        {
            TryHostFromSetup(runInTerminal: true);
            return;
        }

        if (layout.BackBounds.Contains(mouse.Position))
        {
            CloseHostSetupMenuFromBackAction();
        }
    }

    private void CloseHostSetupMenuFromBackAction()
    {
        if (!TryHandleServerLauncherBackAction())
        {
            _hostSetupOpen = false;
            _hostSetupEditField = HostSetupEditField.None;
        }
    }
}
