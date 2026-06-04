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
            _game._pendingControlsBinding = null;
            _game._pendingControllerControlsBinding = null;
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
                return "Server rules and map rotation";
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
            string rconPassword,
            int timeLimitMinutes,
            int capLimit,
            int respawnSeconds,
            bool lobbyAnnounce,
            bool autoBalance,
            bool secondaryAbilitiesEnabled,
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
                secondaryAbilitiesEnabled,
                resetConsole: true,
                launcherLogMessage: $"Start Server pressed for UDP port {port}.");

            if (!_game.TryStartHostedServerBackground(
                    serverName,
                    port,
                    maxPlayers,
                    password,
                    rconPassword,
                    timeLimitMinutes,
                    capLimit,
                    respawnSeconds,
                    lobbyAnnounce,
                    autoBalance,
                    secondaryAbilitiesEnabled,
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
            string rconPassword,
            int timeLimitMinutes,
            int capLimit,
            int respawnSeconds,
            bool lobbyAnnounce,
            bool autoBalance,
            bool secondaryAbilitiesEnabled,
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
                secondaryAbilitiesEnabled,
                resetConsole: true);

            if (!_game.TryStartHostedServerInTerminal(
                    serverName,
                    port,
                    maxPlayers,
                    password,
                    rconPassword,
                    timeLimitMinutes,
                    capLimit,
                    respawnSeconds,
                    lobbyAnnounce,
                    autoBalance,
                    secondaryAbilitiesEnabled,
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
            var layout = HostSetupMenuLayoutCalculator.CreateMenuLayout(
                _game.ViewportWidth,
                _game.ViewportHeight,
                _game._hostMapEntries.Count,
                _game.IsServerLauncherMode,
                _game._hostSetupScreen);
            _game.ClampHostSetupContentScrollOffset(layout);
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

            switch (_game._hostSetupScreen)
            {
                case HostSetupScreen.Options:
                    _game.HandleHostSetupOptionsMenu(mouse, clickPressed);
                    return;
                case HostSetupScreen.Maps:
                    HandleHostSetupMapsMenu(mouse, clickPressed, layout);
                    return;
                default:
                    HandleHostSetupMainMenu(mouse, clickPressed, layout);
                    return;
            }
        }

        private void HandleHostSetupMainMenu(MouseState mouse, bool clickPressed, HostSetupMenuLayout layout)
        {
            HandleHostSetupContentScroll(mouse, layout);
            if (_game.ScrollbarDrag.IsActive)
            {
                return;
            }

            if (!clickPressed)
            {
                return;
            }

            if (TryHandleHostSetupTextFieldClick(mouse, layout))
            {
                return;
            }

            _game.ResetTextFieldClickTarget();
            HandleHostSetupMainMenuClick(mouse, layout);
        }

        private void HandleHostSetupMapsMenu(MouseState mouse, bool clickPressed, HostSetupMenuLayout layout)
        {
            var mapsLayout = HostSetupMapsMenuLayoutCalculator.Create(
                _game.ViewportWidth,
                _game.ViewportHeight,
                _game.IsServerLauncherMode);
            var rightClickPressed = mouse.RightButton == ButtonState.Pressed
                && _game._previousMouse.RightButton != ButtonState.Pressed;
            _game.UpdateHostSetupMapsMenu(mouse, clickPressed, rightClickPressed, mapsLayout);
        }

        private bool TryHandleHostSetupTextFieldClick(MouseState mouse, HostSetupMenuLayout layout)
        {
            var serverNameBounds = _game.GetHostSetupScrolledContentBounds(layout.ServerNameBounds);
            var portBounds = _game.GetHostSetupScrolledContentBounds(layout.PortBounds);
            var slotsBounds = _game.GetHostSetupScrolledContentBounds(layout.SlotsBounds);
            var passwordBounds = _game.GetHostSetupScrolledContentBounds(layout.PasswordBounds);
            var rconPasswordBounds = _game.GetHostSetupScrolledContentBounds(layout.RconPasswordBounds);
            var rotationFileBounds = _game.GetHostSetupScrolledContentBounds(layout.RotationFileBounds);
            var clickPoint = mouse.Position;

            if (HostSetupContentContains(layout, serverNameBounds, clickPoint))
            {
                FocusHostSetupField(HostSetupEditField.ServerName);
                if (_game.IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupServerName))
                {
                    _game.SelectAllTextInActiveField(TextFieldClickTarget.HostSetupServerName);
                }

                return true;
            }

            if (HostSetupContentContains(layout, portBounds, clickPoint))
            {
                FocusHostSetupField(HostSetupEditField.Port);
                if (_game.IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupPort))
                {
                    _game.SelectAllTextInActiveField(TextFieldClickTarget.HostSetupPort);
                }

                return true;
            }

            if (HostSetupContentContains(layout, slotsBounds, clickPoint))
            {
                FocusHostSetupField(HostSetupEditField.Slots);
                if (_game.IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupSlots))
                {
                    _game.SelectAllTextInActiveField(TextFieldClickTarget.HostSetupSlots);
                }

                return true;
            }

            if (HostSetupContentContains(layout, passwordBounds, clickPoint))
            {
                FocusHostSetupField(HostSetupEditField.Password);
                if (_game.IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupPassword))
                {
                    _game.SelectAllTextInActiveField(TextFieldClickTarget.HostSetupPassword);
                }

                return true;
            }

            if (HostSetupContentContains(layout, rconPasswordBounds, clickPoint))
            {
                FocusHostSetupField(HostSetupEditField.RconPassword);
                if (_game.IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupRconPassword))
                {
                    _game.SelectAllTextInActiveField(TextFieldClickTarget.HostSetupRconPassword);
                }

                return true;
            }

            var usePlaylistFileToggleBounds = HostSetupMainScreenLayout.GetUsePlaylistFileToggleBounds(
                rotationFileBounds,
                layout.CompactLayout);
            if (HostSetupContentContains(layout, usePlaylistFileToggleBounds, clickPoint))
            {
                _game._hostUsePlaylistFile = !_game._hostUsePlaylistFile;
                if (!_game._hostUsePlaylistFile && _game._hostSetupEditField == HostSetupEditField.MapRotationFile)
                {
                    FocusHostSetupField(HostSetupEditField.None);
                }

                return true;
            }

            if (HostSetupContentContains(layout, rotationFileBounds, clickPoint))
            {
                if (!_game._hostUsePlaylistFile)
                {
                    return true;
                }

                FocusHostSetupField(HostSetupEditField.MapRotationFile);
                if (_game.IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupMapRotationFile))
                {
                    _game.SelectAllTextInActiveField(TextFieldClickTarget.HostSetupMapRotationFile);
                }

                return true;
            }

            return false;
        }

        public bool HandleHostSetupTextInput(TextInputEventArgs e)
        {
            return HandleHostSetupTextInput(e.Character);
        }

        public bool HandleHostSetupTextInput(char character)
        {
            if (_game.IsServerLauncherMode && _game._hostSetupTab == HostSetupTab.ServerConsole)
            {
                return HandleHostedServerConsoleTextInput(character);
            }

            switch (character)
            {
                case '\b':
                    _game.HandleHostSetupFieldBackspace();
                    break;
                case '\t':
                    _game._hostSetupState.CycleField();
                    _game.InitializeHostSetupFieldCursor(_game._hostSetupState.EditField);
                    break;
                case '\r':
                case '\n':
                    if (_game._hostSetupScreen == HostSetupScreen.Options)
                    {
                        _game._hostSetupState.NavigateToMainScreen();
                        FocusHostSetupField(HostSetupEditField.ServerName);
                    }
                    else if (_game._hostSetupScreen == HostSetupScreen.Maps)
                    {
                        _game.CloseAllHostSetupMapPreviews();
                        _game._hostSetupState.ConfirmMapsScreen();
                        FocusHostSetupField(HostSetupEditField.ServerName);
                    }
                    else
                    {
                        _game.TryHostFromSetup();
                    }

                    break;
                default:
                    _game.HandleHostSetupFieldCharacterInput(character);
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
            _game._hostSetupState.NavigateToMainScreen();
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
            _game.InitializeHostSetupFieldCursor(field);
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
                if (_game.IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupConsoleCommand))
                {
                    _game.SelectAllTextInActiveField(TextFieldClickTarget.HostSetupConsoleCommand);
                }

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
            var listRowsBounds = _game.GetHostSetupScrolledContentBounds(layout.ListRowsBounds);
            if (!HostSetupContentContains(layout, listRowsBounds, mouse.Position))
            {
                return;
            }

            var row = (mouse.Y - listRowsBounds.Y) / layout.RowHeight;
            var mapIndex = _game._hostMapScrollOffset + row;
            if (mapIndex >= 0 && mapIndex < _game._hostMapEntries.Count)
            {
                _game._hostSetupHoverIndex = mapIndex;
            }
        }

        private void HandleHostSetupListScroll(MouseState mouse, HostSetupMenuLayout layout)
        {
            var wheelDelta = mouse.ScrollWheelValue - _game._previousMouse.ScrollWheelValue;
            if (wheelDelta == 0)
            {
                return;
            }

            var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
            var listRowsBounds = _game.GetHostSetupScrolledContentBounds(layout.ListRowsBounds);
            if (HostSetupContentContains(layout, listRowsBounds, mouse.Position))
            {
                _game._hostSetupState.ScrollMapList(wheelDelta > 0 ? -stepCount : stepCount, layout.VisibleRowCapacity);
                return;
            }

            HandleHostSetupContentScroll(mouse, layout, wheelDelta, stepCount);
        }

        private void HandleHostSetupContentScroll(MouseState mouse, HostSetupMenuLayout layout, int wheelDelta = 0, int stepCount = 0)
        {
            var contentHeight = Game1.GetHostSetupContentHeight(layout);
            var maxContentScroll = Math.Max(0, contentHeight - layout.ContentViewportBounds.Height);
            var hostSetupContentScrollOffset = _game._hostSetupContentScrollOffset;
            if (maxContentScroll > 0
                && _game.TryHandleScrollbarRangeDrag(
                    mouse,
                    _game._previousMouse,
                    ScrollbarOwners.HostSetupContent,
                    layout.ContentScrollbarTrackBounds,
                    ref hostSetupContentScrollOffset,
                    maxContentScroll,
                    layout.ContentViewportBounds.Height,
                    contentHeight))
            {
                _game._hostSetupContentScrollOffset = hostSetupContentScrollOffset;
                return;
            }

            _game._hostSetupContentScrollOffset = hostSetupContentScrollOffset;

            if (wheelDelta == 0)
            {
                wheelDelta = mouse.ScrollWheelValue - _game._previousMouse.ScrollWheelValue;
                if (wheelDelta == 0)
                {
                    return;
                }

                stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
            }

            if (layout.ContentViewportBounds.Contains(mouse.Position))
            {
                _game._hostSetupContentScrollOffset = Math.Clamp(
                    _game._hostSetupContentScrollOffset + (wheelDelta > 0 ? -(stepCount * 28) : (stepCount * 28)),
                    0,
                    Math.Max(0, Game1.GetHostSetupContentHeight(layout) - layout.ContentViewportBounds.Height));
            }
        }

        private void HandleHostSetupMainMenuClick(MouseState mouse, HostSetupMenuLayout layout)
        {
            var serverNameBounds = _game.GetHostSetupScrolledContentBounds(layout.ServerNameBounds);
            var portBounds = _game.GetHostSetupScrolledContentBounds(layout.PortBounds);
            var slotsBounds = _game.GetHostSetupScrolledContentBounds(layout.SlotsBounds);
            var passwordBounds = _game.GetHostSetupScrolledContentBounds(layout.PasswordBounds);
            var rconPasswordBounds = _game.GetHostSetupScrolledContentBounds(layout.RconPasswordBounds);
            var rotationFileBounds = _game.GetHostSetupScrolledContentBounds(layout.RotationFileBounds);
            var lobbyBounds = _game.GetHostSetupScrolledContentBounds(layout.LobbyBounds);
            var optionsButtonBounds = _game.GetHostSetupScrolledContentBounds(layout.OptionsButtonBounds);
            var mapsButtonBounds = _game.GetHostSetupScrolledContentBounds(layout.MapsButtonBounds);

            if (HostSetupContentContains(layout, serverNameBounds, mouse.Position))
            {
                FocusHostSetupField(HostSetupEditField.ServerName);
                return;
            }

            if (HostSetupContentContains(layout, portBounds, mouse.Position))
            {
                FocusHostSetupField(HostSetupEditField.Port);
                return;
            }

            if (HostSetupContentContains(layout, slotsBounds, mouse.Position))
            {
                FocusHostSetupField(HostSetupEditField.Slots);
                return;
            }

            if (HostSetupContentContains(layout, passwordBounds, mouse.Position))
            {
                FocusHostSetupField(HostSetupEditField.Password);
                return;
            }

            if (HostSetupContentContains(layout, rconPasswordBounds, mouse.Position))
            {
                FocusHostSetupField(HostSetupEditField.RconPassword);
                return;
            }

            var usePlaylistFileToggleBounds = HostSetupMainScreenLayout.GetUsePlaylistFileToggleBounds(
                rotationFileBounds,
                layout.CompactLayout);
            if (HostSetupContentContains(layout, usePlaylistFileToggleBounds, mouse.Position))
            {
                _game._hostUsePlaylistFile = !_game._hostUsePlaylistFile;
                if (!_game._hostUsePlaylistFile && _game._hostSetupEditField == HostSetupEditField.MapRotationFile)
                {
                    FocusHostSetupField(HostSetupEditField.None);
                }

                return;
            }

            if (HostSetupContentContains(layout, rotationFileBounds, mouse.Position))
            {
                if (_game._hostUsePlaylistFile)
                {
                    FocusHostSetupField(HostSetupEditField.MapRotationFile);
                }

                return;
            }

            if (HostSetupContentContains(layout, lobbyBounds, mouse.Position))
            {
                _game._hostLobbyAnnounceEnabled = !_game._hostLobbyAnnounceEnabled;
                return;
            }

            if (HostSetupContentContains(layout, optionsButtonBounds, mouse.Position))
            {
                _game._hostSetupState.NavigateToOptionsScreen();
                return;
            }

            if (HostSetupContentContains(layout, mapsButtonBounds, mouse.Position))
            {
                if (_game._hostUsePlaylistFile)
                {
                    return;
                }

                _game._hostSetupState.NavigateToMapsScreen();
                var mapsLayout = HostSetupMenuLayoutCalculator.CreateMenuLayout(
                    _game.ViewportWidth,
                    _game.ViewportHeight,
                    _game._hostMapEntries.Count,
                    _game.IsServerLauncherMode,
                    HostSetupScreen.Maps);
                _game._hostSetupState.ClampMapScrollOffset(mapsLayout.VisibleRowCapacity);
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

        private static bool HostSetupContentContains(HostSetupMenuLayout layout, Rectangle bounds, Point point)
        {
            return layout.ContentViewportBounds.Contains(point) && bounds.Contains(point);
        }

        private bool HandleHostedServerConsoleTextInput(TextInputEventArgs e)
        {
            return HandleHostedServerConsoleTextInput(e.Character);
        }

        private bool HandleHostedServerConsoleTextInput(char character)
        {
            switch (character)
            {
                case '\b':
                    _game.HandleHostSetupFieldBackspace();
                    break;
                case '\r':
                case '\n':
                    _game.ExecuteHostedServerCommandFromUi(_game._hostedServerConsole.CreateSnapshot().CommandInput);
                    break;
                case '\t':
                    FocusHostSetupField(HostSetupEditField.ServerConsoleCommand);
                    _game.InitializeHostSetupFieldCursor(_game._hostSetupState.EditField);
                    break;
                default:
                    _game.HandleHostSetupFieldCharacterInput(character);
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
