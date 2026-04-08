#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class HostSetupFlowController
    {
        private readonly Game1 _game;

        public HostSetupFlowController(Game1 game)
        {
            _game = game;
        }

        public void InitializeServerLauncherMode()
        {
            _game.InitializeHostedServerConsole(reset: false);
            _game.AppendHostedServerLog("launcher", "OG2.ServerLauncher initialized.");
            _game._startupSplashOpen = false;
            _game._mainMenuOpen = true;
            _game.CloseManualConnectMenu(clearStatus: false);
            _game._optionsMenuOpen = false;
            _game._pluginOptionsMenuOpen = false;
            _game.CloseCreditsMenu();
            _game._controlsMenuOpen = false;
            _game.CloseHostSetupMenu();
            SelectHostSetupSettingsTab();
            _game.OpenHostSetupMenu();
            if (_game.TryResumeHostedServerSession(loadExistingLog: true))
            {
                SelectHostSetupConsoleTab();
                _game._menuStatusMessage = $"Resumed dedicated server on UDP port {_game._hostedServerConsole.CreateSnapshot().StatusPort}.";
            }
            else
            {
                _game._menuStatusMessage = "Configure and start a dedicated server.";
            }
        }

        public void UpdateServerLauncherState()
        {
            if (!_game.IsServerLauncherMode)
            {
                return;
            }

            var runtimeUpdateState = _game._hostedServerRuntime.UpdateForLauncher();
            if (runtimeUpdateState is HostedServerRuntimeUpdateState.SessionEnded
                or HostedServerRuntimeUpdateState.ProcessExited)
            {
                _game._menuStatusMessage = _game.BuildHostedServerExitMessage();
            }
        }

        public string GetHostSetupTitle()
        {
            return _game.IsServerLauncherMode ? "Dedicated Server" : "Host Game";
        }

        public string GetHostSetupSubtitle()
        {
            if (!_game.IsServerLauncherMode)
            {
                return "Server rules and stock map rotation";
            }

            return _game.IsHostedServerRunning
                ? "Dedicated server is running in the background"
                : "Configure and run a headless server process";
        }

        public string GetHostSetupPrimaryButtonLabel()
        {
            if (!_game.IsServerLauncherMode)
            {
                return "Host";
            }

            return _game.IsHostedServerRunning ? "Server Running" : "Start Server";
        }

        public string GetHostSetupSecondaryButtonLabel()
        {
            if (!_game.IsServerLauncherMode)
            {
                return "Back";
            }

            return _game.IsHostedServerRunning ? "Stop Server" : "Quit";
        }

        public bool TryHandleServerLauncherBackAction()
        {
            if (!_game.IsServerLauncherMode)
            {
                return false;
            }

            ClearHostSetupFocus();
            if (_game.IsHostedServerRunning)
            {
                _game.AppendHostedServerLog("launcher", "Back action requested while dedicated server was running.");
                _game.StopHostedServer();
                _game._menuStatusMessage = "Dedicated server stopped.";
            }
            else
            {
                _game.AppendHostedServerLog("launcher", "Back action requested with no running server; exiting launcher.");
                _game.Exit();
            }

            return true;
        }

        public void BeginDedicatedServerLaunch(
            string serverName,
            int port,
            int maxPlayers,
            string password,
            int timeLimitMinutes,
            int capLimit,
            int respawnSeconds,
            bool lobbyAnnounce,
            bool autoBalance,
            string? requestedMap,
            string? mapRotationFile)
        {
            _game.PrepareHostedServerLaunchUi(closeHostSetup: false, disconnectNetworkClient: false);
            _game.PrepareHostedServerConsoleLaunchState(
                serverName,
                port,
                maxPlayers,
                timeLimitMinutes,
                capLimit,
                respawnSeconds,
                lobbyAnnounce,
                autoBalance,
                resetConsole: true,
                launcherLogMessage: $"Start Server pressed for UDP port {port}.");

            if (!_game.TryStartHostedServerBackground(
                    serverName,
                    port,
                    maxPlayers,
                    password,
                    timeLimitMinutes,
                    capLimit,
                    respawnSeconds,
                    lobbyAnnounce,
                    autoBalance,
                    requestedMap,
                    mapRotationFile,
                    resetConsole: false,
                    out var error))
            {
                _game._menuStatusMessage = error;
                return;
            }

            _game.CancelPendingHostedLocalConnect();
            _game._pendingHostedConnectPort = port;
            SelectHostSetupConsoleTab();
            ClearHostSetupFocus();
            _game._menuStatusMessage = $"Starting dedicated server on UDP port {port}...";
        }

        public void BeginDedicatedServerTerminalLaunch(
            string serverName,
            int port,
            int maxPlayers,
            string password,
            int timeLimitMinutes,
            int capLimit,
            int respawnSeconds,
            bool lobbyAnnounce,
            bool autoBalance,
            string? requestedMap,
            string? mapRotationFile)
        {
            _game.PrepareHostedServerLaunchUi(closeHostSetup: false, disconnectNetworkClient: false);
            _game.PrepareHostedServerConsoleLaunchState(
                serverName,
                port,
                maxPlayers,
                timeLimitMinutes,
                capLimit,
                respawnSeconds,
                lobbyAnnounce,
                autoBalance,
                resetConsole: true);

            if (!_game.TryStartHostedServerInTerminal(
                    serverName,
                    port,
                    maxPlayers,
                    password,
                    timeLimitMinutes,
                    capLimit,
                    respawnSeconds,
                    lobbyAnnounce,
                    autoBalance,
                    requestedMap,
                    mapRotationFile,
                    out var error))
            {
                _game._menuStatusMessage = error;
                return;
            }

            _game.Exit();
        }

        public void UpdateHostSetupMenu(MouseState mouse)
        {
            var layout = HostSetupMenuLayoutCalculator.CreateMenuLayout(_game.ViewportWidth, _game.ViewportHeight, _game._hostMapEntries.Count, _game.IsServerLauncherMode);
            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _game._previousMouse.LeftButton != ButtonState.Pressed;

            if (TryHandleHostSetupTabClick(mouse, clickPressed, layout))
            {
                return;
            }

            if (_game.IsServerLauncherMode && _game._hostSetupTab == HostSetupTab.ServerConsole)
            {
                HandleHostedServerConsoleMenuClick(mouse, clickPressed, layout);
                return;
            }

            _game._hostSetupState.ClampMapScrollOffset(layout.VisibleRowCapacity);
            HandleHostSetupListScroll(mouse, layout);
            UpdateHostSetupHoverIndex(mouse, layout);
            if (!clickPressed)
            {
                return;
            }

            HandleHostSetupSettingsMenuClick(mouse, layout);
        }

        public bool HandleHostSetupTextInput(TextInputEventArgs e)
        {
            if (_game.IsServerLauncherMode && _game._hostSetupTab == HostSetupTab.ServerConsole)
            {
                return HandleHostedServerConsoleTextInput(e);
            }

            switch (e.Character)
            {
                case '\b':
                    _game._hostSetupState.BackspaceActiveField();
                    break;
                case '\t':
                    _game._hostSetupState.CycleField();
                    break;
                case '\r':
                case '\n':
                    _game.TryHostFromSetup();
                    break;
                default:
                    _game._hostSetupState.AppendCharacterToActiveField(e.Character);
                    break;
            }

            if (_game._hostSetupEditField != HostSetupEditField.None)
            {
                _game._menuStatusMessage = string.Empty;
            }

            return true;
        }

        public void CloseHostSetupMenuFromBackAction()
        {
            if (!TryHandleServerLauncherBackAction())
            {
                _game.CloseHostSetupMenu();
            }
        }

        public void SelectHostSetupSettingsTab()
        {
            _game._hostSetupTab = HostSetupTab.Settings;
            FocusHostSetupField(HostSetupEditField.ServerName);
        }

        public void SelectHostSetupConsoleTab()
        {
            _game._hostSetupTab = HostSetupTab.ServerConsole;
            FocusHostSetupField(HostSetupEditField.ServerConsoleCommand);
        }

        public void FocusHostSetupField(HostSetupEditField field)
        {
            _game._hostSetupEditField = field;
        }

        public void ClearHostSetupFocus()
        {
            _game._hostSetupEditField = HostSetupEditField.None;
        }

        private bool TryHandleHostSetupTabClick(MouseState mouse, bool clickPressed, HostSetupMenuLayout layout)
        {
            if (!_game.IsServerLauncherMode || !clickPressed)
            {
                return false;
            }

            var tabLayout = HostSetupMenuLayoutCalculator.CreateServerLauncherTabLayout(layout.Panel);
            if (tabLayout.SettingsTabBounds.Contains(mouse.Position))
            {
                SelectHostSetupSettingsTab();
                return true;
            }

            if (tabLayout.ConsoleTabBounds.Contains(mouse.Position))
            {
                SelectHostSetupConsoleTab();
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
                FocusHostSetupField(HostSetupEditField.ServerConsoleCommand);
                return;
            }

            if (consoleLayout.SendBounds.Contains(mouse.Position))
            {
                _game.ExecuteHostedServerCommandFromUi(_game._hostedServerConsole.CreateSnapshot().CommandInput);
                return;
            }

            if (consoleLayout.ClearBounds.Contains(mouse.Position))
            {
                _game.ClearHostedServerConsoleView();
                _game._menuStatusMessage = "Console view cleared.";
                return;
            }

            if (consoleLayout.StatusCommandBounds.Contains(mouse.Position))
            {
                _game.ExecuteHostedServerCommandFromUi("status");
                return;
            }

            if (consoleLayout.PlayersCommandBounds.Contains(mouse.Position))
            {
                _game.ExecuteHostedServerCommandFromUi("players");
                return;
            }

            if (consoleLayout.RotationCommandBounds.Contains(mouse.Position))
            {
                _game.ExecuteHostedServerCommandFromUi("rotation");
                return;
            }

            if (consoleLayout.HelpCommandBounds.Contains(mouse.Position))
            {
                _game.ExecuteHostedServerCommandFromUi("help");
                return;
            }

            if (!_game.IsHostedServerRunning && consoleLayout.HostBounds.Contains(mouse.Position))
            {
                _game.TryHostFromSetup();
                return;
            }

            if (!_game.IsHostedServerRunning && layout.TerminalButtonBounds.Contains(mouse.Position))
            {
                _game.TryHostFromSetup(runInTerminal: true);
                return;
            }

            if (consoleLayout.BackBounds.Contains(mouse.Position))
            {
                CloseHostSetupMenuFromBackAction();
            }
        }

        private void UpdateHostSetupHoverIndex(MouseState mouse, HostSetupMenuLayout layout)
        {
            _game._hostSetupHoverIndex = -1;
            if (!layout.ListRowsBounds.Contains(mouse.Position))
            {
                return;
            }

            var row = (mouse.Y - layout.ListRowsBounds.Y) / layout.RowHeight;
            var mapIndex = _game._hostMapScrollOffset + row;
            if (mapIndex >= 0 && mapIndex < _game._hostMapEntries.Count)
            {
                _game._hostSetupHoverIndex = mapIndex;
            }
        }

        private void HandleHostSetupListScroll(MouseState mouse, HostSetupMenuLayout layout)
        {
            var wheelDelta = mouse.ScrollWheelValue - _game._previousMouse.ScrollWheelValue;
            if (wheelDelta == 0 || !layout.ListRowsBounds.Contains(mouse.Position))
            {
                return;
            }

            var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
            _game._hostSetupState.ScrollMapList(wheelDelta > 0 ? -stepCount : stepCount, layout.VisibleRowCapacity);
        }

        private void HandleHostSetupSettingsMenuClick(MouseState mouse, HostSetupMenuLayout layout)
        {
            if (layout.ServerNameBounds.Contains(mouse.Position))
            {
                FocusHostSetupField(HostSetupEditField.ServerName);
                return;
            }

            if (layout.PortBounds.Contains(mouse.Position))
            {
                FocusHostSetupField(HostSetupEditField.Port);
                return;
            }

            if (layout.SlotsBounds.Contains(mouse.Position))
            {
                FocusHostSetupField(HostSetupEditField.Slots);
                return;
            }

            if (layout.PasswordBounds.Contains(mouse.Position))
            {
                FocusHostSetupField(HostSetupEditField.Password);
                return;
            }

            if (layout.RotationFileBounds.Contains(mouse.Position))
            {
                FocusHostSetupField(HostSetupEditField.MapRotationFile);
                return;
            }

            if (layout.TimeLimitBounds.Contains(mouse.Position))
            {
                FocusHostSetupField(HostSetupEditField.TimeLimit);
                return;
            }

            if (layout.CapLimitBounds.Contains(mouse.Position))
            {
                FocusHostSetupField(HostSetupEditField.CapLimit);
                return;
            }

            if (layout.RespawnBounds.Contains(mouse.Position))
            {
                FocusHostSetupField(HostSetupEditField.RespawnSeconds);
                return;
            }

            if (_game._hostSetupHoverIndex >= 0 && layout.ListRowsBounds.Contains(mouse.Position))
            {
                _game._hostMapIndex = _game._hostSetupHoverIndex;
                ClearHostSetupFocus();
                return;
            }

            if (layout.ToggleBounds.Contains(mouse.Position))
            {
                _game.ToggleSelectedHostMap();
                return;
            }

            if (layout.MoveUpBounds.Contains(mouse.Position))
            {
                _game.MoveSelectedHostMap(-1);
                return;
            }

            if (layout.MoveDownBounds.Contains(mouse.Position))
            {
                _game.MoveSelectedHostMap(1);
                return;
            }

            if (layout.LobbyBounds.Contains(mouse.Position))
            {
                _game._hostLobbyAnnounceEnabled = !_game._hostLobbyAnnounceEnabled;
                return;
            }

            if (layout.AutoBalanceBounds.Contains(mouse.Position))
            {
                _game._hostAutoBalanceEnabled = !_game._hostAutoBalanceEnabled;
                return;
            }

            if (!_game.IsHostedServerRunning && layout.HostBounds.Contains(mouse.Position))
            {
                _game.TryHostFromSetup();
                return;
            }

            if (_game.IsServerLauncherMode && !_game.IsHostedServerRunning && layout.TerminalButtonBounds.Contains(mouse.Position))
            {
                _game.TryHostFromSetup(runInTerminal: true);
                return;
            }

            if (layout.BackBounds.Contains(mouse.Position))
            {
                CloseHostSetupMenuFromBackAction();
            }
        }

        private bool HandleHostedServerConsoleTextInput(TextInputEventArgs e)
        {
            switch (e.Character)
            {
                case '\b':
                    _game._hostedServerConsole.BackspaceCommandInput();
                    break;
                case '\r':
                case '\n':
                    _game.ExecuteHostedServerCommandFromUi(_game._hostedServerConsole.CreateSnapshot().CommandInput);
                    break;
                case '\t':
                    FocusHostSetupField(HostSetupEditField.ServerConsoleCommand);
                    break;
                default:
                    _game._hostedServerConsole.AppendCommandInput(e.Character, 120);
                    break;
            }

            if (_game._hostSetupEditField != HostSetupEditField.None)
            {
                _game._menuStatusMessage = string.Empty;
            }

            return true;
        }
    }
}
