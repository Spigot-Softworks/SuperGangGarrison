#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class HostSetupFlowController
    {
        private readonly IHostingContext _context;

        public HostSetupFlowController(IHostingContext context)
        {
            _context = context;
        }

        public void InitializeServerLauncherMode()
        {
            _context.InitializeHostedServerConsole(reset: false);
            _context.AppendHostedServerLog("launcher", "OG2.ServerLauncher initialized.");
            _context._startupSplashOpen = false;
            _context._mainMenuOpen = true;
            _context.CloseManualConnectMenu(clearStatus: false);
            _context._optionsMenuOpen = false;
            _context._pluginOptionsMenuOpen = false;
            _context.CloseCreditsMenu();
            _context._controlsMenuOpen = false;
            _context._pendingControlsBinding = null;
            _context._pendingControllerControlsBinding = null;
            _context.CloseHostSetupMenu();
            SelectHostSetupSettingsTab();
            _context.OpenHostSetupMenu();
            if (_context.TryResumeHostedServerSession(loadExistingLog: true))
            {
                SelectHostSetupConsoleTab();
                _context._menuStatusMessage = $"Resumed dedicated server on UDP port {_context._hostedServerConsole.CreateSnapshot().StatusPort}.";
            }
            else
            {
                _context._menuStatusMessage = "Configure and start a dedicated server.";
            }
        }

        public void UpdateServerLauncherState()
        {
            if (!_context.IsServerLauncherMode)
            {
                return;
            }

            var runtimeUpdateState = _context._hostedServerRuntime.UpdateForLauncher();
            if (runtimeUpdateState is HostedServerRuntimeUpdateState.SessionEnded
                or HostedServerRuntimeUpdateState.ProcessExited)
            {
                _context._menuStatusMessage = _context.BuildHostedServerExitMessage();
            }
        }

        public string GetHostSetupTitle()
        {
            return _context.IsServerLauncherMode ? "Dedicated Server" : "Host Game";
        }

        public string GetHostSetupSubtitle()
        {
            if (!_context.IsServerLauncherMode)
            {
                return "Server rules and map rotation";
            }

            return _context.IsHostedServerRunning
                ? "Dedicated server is running in the background"
                : "Configure and run a headless server process";
        }

        public string GetHostSetupPrimaryButtonLabel()
        {
            if (!_context.IsServerLauncherMode)
            {
                return "Host";
            }

            return _context.IsHostedServerRunning ? "Server Running" : "Start Server";
        }

        public string GetHostSetupSecondaryButtonLabel()
        {
            if (!_context.IsServerLauncherMode)
            {
                return "Back";
            }

            return _context.IsHostedServerRunning ? "Stop Server" : "Quit";
        }

        public bool TryHandleServerLauncherBackAction()
        {
            if (!_context.IsServerLauncherMode)
            {
                return false;
            }

            ClearHostSetupFocus();
            if (_context.IsHostedServerRunning)
            {
                _context.AppendHostedServerLog("launcher", "Back action requested while dedicated server was running.");
                _context.StopHostedServer();
                _context._menuStatusMessage = "Dedicated server stopped.";
            }
            else
            {
                _context.AppendHostedServerLog("launcher", "Back action requested with no running server; exiting launcher.");
                _context.Exit();
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
            _context.PrepareHostedServerLaunchUi(closeHostSetup: false, disconnectNetworkClient: false);
            _context.PrepareHostedServerConsoleLaunchState(
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

            if (!_context.TryStartHostedServerBackground(
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
                _context._menuStatusMessage = error;
                return;
            }

            _context.CancelPendingHostedLocalConnect();
            _context._pendingHostedConnectPort = port;
            SelectHostSetupConsoleTab();
            ClearHostSetupFocus();
            _context._menuStatusMessage = $"Starting dedicated server on UDP port {port}...";
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
            _context.PrepareHostedServerLaunchUi(closeHostSetup: false, disconnectNetworkClient: false);
            _context.PrepareHostedServerConsoleLaunchState(
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

            if (!_context.TryStartHostedServerInTerminal(
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
                _context._menuStatusMessage = error;
                return;
            }

            _context.Exit();
        }

        public void UpdateHostSetupMenu(MouseState mouse)
        {
            var layout = HostSetupMenuLayoutCalculator.CreateMenuLayout(
                _context.ViewportWidth,
                _context.ViewportHeight,
                _context._hostMapEntries.Count,
                _context.IsServerLauncherMode,
                _context._hostSetupScreen);
            _context.ClampHostSetupContentScrollOffset(layout);
            var clickPressed = mouse.LeftButton == ButtonState.Pressed && _context._previousMouse.LeftButton != ButtonState.Pressed;

            if (TryHandleHostSetupTabClick(mouse, clickPressed, layout))
            {
                return;
            }

            if (_context.IsServerLauncherMode && _context._hostSetupTab == HostSetupTab.ServerConsole)
            {
                HandleHostedServerConsoleMenuClick(mouse, clickPressed, layout);
                return;
            }

            switch (_context._hostSetupScreen)
            {
                case HostSetupScreen.Options:
                    _context.HandleHostSetupOptionsMenu(mouse, clickPressed);
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
            // Footer actions must be handled before content scrolling. A scrollbar drag can
            // otherwise consume the click and return before Host/Back is considered, especially
            // when this overlay was opened while another menu still owned the shared scrollbar.
            if (clickPressed && TryHandleHostSetupFooterClick(mouse, layout))
            {
                return;
            }

            HandleHostSetupContentScroll(mouse, layout);
            if (_context.ScrollbarDrag.IsActive)
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

            _context.ResetTextFieldClickTarget();
            HandleHostSetupMainMenuClick(mouse, layout);
        }

        private bool TryHandleHostSetupFooterClick(MouseState mouse, HostSetupMenuLayout layout)
        {
            if (!_context.IsHostedServerRunning && layout.HostBounds.Contains(mouse.Position))
            {
                _context.TryHostFromSetup();
                return true;
            }

            if (_context.IsServerLauncherMode
                && !_context.IsHostedServerRunning
                && layout.TerminalButtonBounds.Contains(mouse.Position))
            {
                _context.TryHostFromSetup(runInTerminal: true);
                return true;
            }

            if (layout.BackBounds.Contains(mouse.Position))
            {
                CloseHostSetupMenuFromBackAction();
                return true;
            }

            return false;
        }

        private void HandleHostSetupMapsMenu(MouseState mouse, bool clickPressed, HostSetupMenuLayout layout)
        {
            var mapsLayout = HostSetupMapsMenuLayoutCalculator.Create(
                _context.ViewportWidth,
                _context.ViewportHeight,
                _context.IsServerLauncherMode);
            var rightClickPressed = mouse.RightButton == ButtonState.Pressed
                && _context._previousMouse.RightButton != ButtonState.Pressed;
            _context.UpdateHostSetupMapsMenu(mouse, clickPressed, rightClickPressed, mapsLayout);
        }

        private bool TryHandleHostSetupTextFieldClick(MouseState mouse, HostSetupMenuLayout layout)
        {
            var serverNameBounds = _context.GetHostSetupScrolledContentBounds(layout.ServerNameBounds);
            var portBounds = _context.GetHostSetupScrolledContentBounds(layout.PortBounds);
            var slotsBounds = _context.GetHostSetupScrolledContentBounds(layout.SlotsBounds);
            var passwordBounds = _context.GetHostSetupScrolledContentBounds(layout.PasswordBounds);
            var rconPasswordBounds = _context.GetHostSetupScrolledContentBounds(layout.RconPasswordBounds);
            var rotationFileBounds = _context.GetHostSetupScrolledContentBounds(layout.RotationFileBounds);
            var clickPoint = mouse.Position;

            if (HostSetupContentContains(layout, serverNameBounds, clickPoint))
            {
                FocusHostSetupField(HostSetupEditField.ServerName);
                if (_context.IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupServerName))
                {
                    _context.SelectAllTextInActiveField(TextFieldClickTarget.HostSetupServerName);
                }

                return true;
            }

            if (HostSetupContentContains(layout, portBounds, clickPoint))
            {
                FocusHostSetupField(HostSetupEditField.Port);
                if (_context.IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupPort))
                {
                    _context.SelectAllTextInActiveField(TextFieldClickTarget.HostSetupPort);
                }

                return true;
            }

            if (HostSetupContentContains(layout, slotsBounds, clickPoint))
            {
                FocusHostSetupField(HostSetupEditField.Slots);
                if (_context.IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupSlots))
                {
                    _context.SelectAllTextInActiveField(TextFieldClickTarget.HostSetupSlots);
                }

                return true;
            }

            if (HostSetupContentContains(layout, passwordBounds, clickPoint))
            {
                FocusHostSetupField(HostSetupEditField.Password);
                if (_context.IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupPassword))
                {
                    _context.SelectAllTextInActiveField(TextFieldClickTarget.HostSetupPassword);
                }

                return true;
            }

            if (HostSetupContentContains(layout, rconPasswordBounds, clickPoint))
            {
                FocusHostSetupField(HostSetupEditField.RconPassword);
                if (_context.IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupRconPassword))
                {
                    _context.SelectAllTextInActiveField(TextFieldClickTarget.HostSetupRconPassword);
                }

                return true;
            }

            var usePlaylistFileToggleBounds = HostSetupMainScreenLayout.GetUsePlaylistFileToggleBounds(
                rotationFileBounds,
                layout.CompactLayout);
            if (HostSetupContentContains(layout, usePlaylistFileToggleBounds, clickPoint))
            {
                _context._hostUsePlaylistFile = !_context._hostUsePlaylistFile;
                if (!_context._hostUsePlaylistFile && _context._hostSetupEditField == HostSetupEditField.MapRotationFile)
                {
                    FocusHostSetupField(HostSetupEditField.None);
                }

                return true;
            }

            if (HostSetupContentContains(layout, rotationFileBounds, clickPoint))
            {
                if (!_context._hostUsePlaylistFile)
                {
                    return true;
                }

                FocusHostSetupField(HostSetupEditField.MapRotationFile);
                if (_context.IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupMapRotationFile))
                {
                    _context.SelectAllTextInActiveField(TextFieldClickTarget.HostSetupMapRotationFile);
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
            if (_context.IsServerLauncherMode && _context._hostSetupTab == HostSetupTab.ServerConsole)
            {
                return HandleHostedServerConsoleTextInput(character);
            }

            switch (character)
            {
                case '\b':
                    _context.HandleHostSetupFieldBackspace();
                    break;
                case '\t':
                    _context._hostSetupState.CycleField();
                    _context.InitializeHostSetupFieldCursor(_context._hostSetupState.EditField);
                    break;
                case '\r':
                case '\n':
                    if (_context._hostSetupScreen == HostSetupScreen.Options
                        && _context._hostSetupEditField != HostSetupEditField.None)
                    {
                        _context._hostSetupState.CommitEditSnapshot();
                        ClearHostSetupFocus();
                    }
                    else if (_context._hostSetupScreen == HostSetupScreen.Options)
                    {
                        _context._hostSetupState.NavigateToMainScreen();
                        FocusHostSetupField(HostSetupEditField.ServerName);
                    }
                    else if (_context._hostSetupScreen == HostSetupScreen.Maps)
                    {
                        _context.CloseAllHostSetupMapPreviews();
                        _context._hostSetupState.ConfirmMapsScreen();
                        FocusHostSetupField(HostSetupEditField.ServerName);
                    }
                    else if (_context._hostSetupEditField != HostSetupEditField.None)
                    {
                        // Enter confirms the active field. Starting the server is
                        // deliberately kept on the Host button so an accidental
                        // Enter while editing cannot launch with partial input.
                        _context._hostSetupState.CommitEditSnapshot();
                        ClearHostSetupFocus();
                    }
                    else
                    {
                        _context.TryHostFromSetup();
                    }

                    break;
                default:
                    _context.HandleHostSetupFieldCharacterInput(character);
                    break;
            }

            if (_context._hostSetupEditField != HostSetupEditField.None)
            {
                _context._menuStatusMessage = string.Empty;
            }

            return true;
        }

        public void CloseHostSetupMenuFromBackAction()
        {
            if (!TryHandleServerLauncherBackAction())
            {
                _context.CloseHostSetupMenu();
            }
        }

        public void SelectHostSetupSettingsTab()
        {
            _context._hostSetupTab = HostSetupTab.Settings;
            _context._hostSetupState.NavigateToMainScreen();
            FocusHostSetupField(HostSetupEditField.ServerName);
        }

        public void SelectHostSetupConsoleTab()
        {
            _context._hostSetupTab = HostSetupTab.ServerConsole;
            FocusHostSetupField(HostSetupEditField.ServerConsoleCommand);
        }

        public void FocusHostSetupField(HostSetupEditField field)
        {
            if (_context._hostSetupEditField != HostSetupEditField.None
                && _context._hostSetupEditField != field)
            {
                _context._hostSetupState.CommitEditSnapshot();
            }

            _context._hostSetupEditField = field;
            if (field != HostSetupEditField.None)
            {
                _context._hostSetupState.BeginEditSnapshot(field);
            }
            _context.InitializeHostSetupFieldCursor(field);
        }

        public void ClearHostSetupFocus()
        {
            _context._hostSetupState.CommitEditSnapshot();
            _context._hostSetupEditField = HostSetupEditField.None;
        }

        private bool TryHandleHostSetupTabClick(MouseState mouse, bool clickPressed, HostSetupMenuLayout layout)
        {
            if (!_context.IsServerLauncherMode || !clickPressed)
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
                if (_context.IsTextFieldDoubleClick(TextFieldClickTarget.HostSetupConsoleCommand))
                {
                    _context.SelectAllTextInActiveField(TextFieldClickTarget.HostSetupConsoleCommand);
                }

                return;
            }

            if (consoleLayout.SendBounds.Contains(mouse.Position))
            {
                _context.ExecuteHostedServerCommandFromUi(_context._hostedServerConsole.CreateSnapshot().CommandInput);
                return;
            }

            if (consoleLayout.ClearBounds.Contains(mouse.Position))
            {
                _context.ClearHostedServerConsoleView();
                _context._menuStatusMessage = "Console view cleared.";
                return;
            }

            if (consoleLayout.StatusCommandBounds.Contains(mouse.Position))
            {
                _context.ExecuteHostedServerCommandFromUi("status");
                return;
            }

            if (consoleLayout.PlayersCommandBounds.Contains(mouse.Position))
            {
                _context.ExecuteHostedServerCommandFromUi("players");
                return;
            }

            if (consoleLayout.RotationCommandBounds.Contains(mouse.Position))
            {
                _context.ExecuteHostedServerCommandFromUi("rotation");
                return;
            }

            if (consoleLayout.HelpCommandBounds.Contains(mouse.Position))
            {
                _context.ExecuteHostedServerCommandFromUi("help");
                return;
            }

            if (!_context.IsHostedServerRunning && consoleLayout.HostBounds.Contains(mouse.Position))
            {
                _context.TryHostFromSetup();
                return;
            }

            if (!_context.IsHostedServerRunning && layout.TerminalButtonBounds.Contains(mouse.Position))
            {
                _context.TryHostFromSetup(runInTerminal: true);
                return;
            }

            if (consoleLayout.BackBounds.Contains(mouse.Position))
            {
                CloseHostSetupMenuFromBackAction();
            }
        }



        private void HandleHostSetupContentScroll(MouseState mouse, HostSetupMenuLayout layout, int wheelDelta = 0, int stepCount = 0)
        {
            var contentHeight = Game1.GetHostSetupContentHeight(layout);
            var maxContentScroll = Math.Max(0, contentHeight - layout.ContentViewportBounds.Height);
            var hostSetupContentScrollOffset = _context._hostSetupContentScrollOffset;
            if (maxContentScroll > 0
                && _context.TryHandleScrollbarRangeDrag(
                    mouse,
                    _context._previousMouse,
                    ScrollbarOwners.HostSetupContent,
                    layout.ContentScrollbarTrackBounds,
                    ref hostSetupContentScrollOffset,
                    maxContentScroll,
                    layout.ContentViewportBounds.Height,
                    contentHeight))
            {
                _context._hostSetupContentScrollOffset = hostSetupContentScrollOffset;
                return;
            }

            _context._hostSetupContentScrollOffset = hostSetupContentScrollOffset;

            if (wheelDelta == 0)
            {
                wheelDelta = mouse.ScrollWheelValue - _context._previousMouse.ScrollWheelValue;
                if (wheelDelta == 0)
                {
                    return;
                }

                stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
            }

            if (layout.ContentViewportBounds.Contains(mouse.Position))
            {
                _context._hostSetupContentScrollOffset = Math.Clamp(
                    _context._hostSetupContentScrollOffset + (wheelDelta > 0 ? -(stepCount * 28) : (stepCount * 28)),
                    0,
                    Math.Max(0, Game1.GetHostSetupContentHeight(layout) - layout.ContentViewportBounds.Height));
            }
        }

        private void HandleHostSetupMainMenuClick(MouseState mouse, HostSetupMenuLayout layout)
        {
            var serverNameBounds = _context.GetHostSetupScrolledContentBounds(layout.ServerNameBounds);
            var portBounds = _context.GetHostSetupScrolledContentBounds(layout.PortBounds);
            var slotsBounds = _context.GetHostSetupScrolledContentBounds(layout.SlotsBounds);
            var passwordBounds = _context.GetHostSetupScrolledContentBounds(layout.PasswordBounds);
            var rconPasswordBounds = _context.GetHostSetupScrolledContentBounds(layout.RconPasswordBounds);
            var rotationFileBounds = _context.GetHostSetupScrolledContentBounds(layout.RotationFileBounds);
            var lobbyBounds = _context.GetHostSetupScrolledContentBounds(layout.LobbyBounds);
            var optionsButtonBounds = _context.GetHostSetupScrolledContentBounds(layout.OptionsButtonBounds);
            var mapsButtonBounds = _context.GetHostSetupScrolledContentBounds(layout.MapsButtonBounds);

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
                _context._hostUsePlaylistFile = !_context._hostUsePlaylistFile;
                if (!_context._hostUsePlaylistFile && _context._hostSetupEditField == HostSetupEditField.MapRotationFile)
                {
                    FocusHostSetupField(HostSetupEditField.None);
                }

                return;
            }

            if (HostSetupContentContains(layout, rotationFileBounds, mouse.Position))
            {
                if (_context._hostUsePlaylistFile)
                {
                    FocusHostSetupField(HostSetupEditField.MapRotationFile);
                }

                return;
            }

            if (HostSetupContentContains(layout, lobbyBounds, mouse.Position))
            {
                _context._hostLobbyAnnounceEnabled = !_context._hostLobbyAnnounceEnabled;
                return;
            }

            if (HostSetupContentContains(layout, optionsButtonBounds, mouse.Position))
            {
                _context._hostSetupState.NavigateToOptionsScreen();
                return;
            }

            if (HostSetupContentContains(layout, mapsButtonBounds, mouse.Position))
            {
                if (_context._hostUsePlaylistFile)
                {
                    return;
                }

                _context._hostSetupState.NavigateToMapsScreen();
                var mapsLayout = HostSetupMenuLayoutCalculator.CreateMenuLayout(
                    _context.ViewportWidth,
                    _context.ViewportHeight,
                    _context._hostMapEntries.Count,
                    _context.IsServerLauncherMode,
                    HostSetupScreen.Maps);
                _context._hostSetupState.ClampMapScrollOffset(mapsLayout.VisibleRowCapacity);
                return;
            }

            if (!_context.IsHostedServerRunning && layout.HostBounds.Contains(mouse.Position))
            {
                _context.TryHostFromSetup();
                return;
            }

            if (_context.IsServerLauncherMode && !_context.IsHostedServerRunning && layout.TerminalButtonBounds.Contains(mouse.Position))
            {
                _context.TryHostFromSetup(runInTerminal: true);
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
                    _context.HandleHostSetupFieldBackspace();
                    break;
                case '\r':
                case '\n':
                    _context.ExecuteHostedServerCommandFromUi(_context._hostedServerConsole.CreateSnapshot().CommandInput);
                    break;
                case '\t':
                    FocusHostSetupField(HostSetupEditField.ServerConsoleCommand);
                    _context.InitializeHostSetupFieldCursor(_context._hostSetupState.EditField);
                    break;
                default:
                    _context.HandleHostSetupFieldCharacterInput(character);
                    break;
            }

            if (_context._hostSetupEditField != HostSetupEditField.None)
            {
                _context._menuStatusMessage = string.Empty;
            }

            return true;
        }
}
