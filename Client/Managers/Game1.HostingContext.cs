#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1 : IHostingContext
{
    bool IHostingContext._controlsMenuOpen { get => _controlsMenuOpen; set => _controlsMenuOpen = value; }

    OpenGarrison.Client.HostedServerConsoleState IHostingContext._hostedServerConsole { get => _hostedServerConsole; }

    OpenGarrison.Client.HostedServerRuntimeController IHostingContext._hostedServerRuntime { get => _hostedServerRuntime; }

    bool IHostingContext._hostLobbyAnnounceEnabled { get => _hostLobbyAnnounceEnabled; set => _hostLobbyAnnounceEnabled = value; }

    List<OpenGarrison.Core.OpenGarrisonMapRotationEntry> IHostingContext._hostMapEntries { get => _hostMapEntries; set => _hostMapEntries = value; }

    int IHostingContext._hostSetupContentScrollOffset { get => _hostSetupContentScrollOffset; set => _hostSetupContentScrollOffset = value; }

    OpenGarrison.Client.Game1.HostSetupEditField IHostingContext._hostSetupEditField { get => _hostSetupEditField; set => _hostSetupEditField = value; }

    OpenGarrison.Client.HostSetupScreen IHostingContext._hostSetupScreen { get => _hostSetupScreen; set => _hostSetupScreen = value; }

    OpenGarrison.Client.Game1.HostSetupFormState IHostingContext._hostSetupState { get => _hostSetupState; }

    OpenGarrison.Client.Game1.HostSetupTab IHostingContext._hostSetupTab { get => _hostSetupTab; set => _hostSetupTab = value; }

    bool IHostingContext._hostUsePlaylistFile { get => _hostUsePlaylistFile; set => _hostUsePlaylistFile = value; }

    bool IHostingContext._mainMenuOpen { get => _mainMenuOpen; set => _mainMenuOpen = value; }

    string IHostingContext._menuStatusMessage { get => _menuStatusMessage; set => _menuStatusMessage = value; }

    bool IHostingContext._optionsMenuOpen { get => _optionsMenuOpen; set => _optionsMenuOpen = value; }

    Nullable<OpenGarrison.Client.Game1.ControllerControlsMenuBinding> IHostingContext._pendingControllerControlsBinding { get => _pendingControllerControlsBinding; set => _pendingControllerControlsBinding = value; }

    Nullable<OpenGarrison.Client.Game1.ControlsMenuBinding> IHostingContext._pendingControlsBinding { get => _pendingControlsBinding; set => _pendingControlsBinding = value; }

    int IHostingContext._pendingHostedConnectPort { get => _pendingHostedConnectPort; set => _pendingHostedConnectPort = value; }

    bool IHostingContext._pluginOptionsMenuOpen { get => _pluginOptionsMenuOpen; set => _pluginOptionsMenuOpen = value; }

    Microsoft.Xna.Framework.Input.MouseState IHostingContext._previousMouse { get => _previousMouse; set => _previousMouse = value; }

    bool IHostingContext._startupSplashOpen { get => _startupSplashOpen; set => _startupSplashOpen = value; }

    bool IHostingContext.IsHostedServerRunning { get => IsHostedServerRunning; }

    bool IHostingContext.IsServerLauncherMode { get => IsServerLauncherMode; }

    OpenGarrison.Client.ScrollbarDragController IHostingContext.ScrollbarDrag { get => ScrollbarDrag; }

    int IHostingContext.ViewportHeight { get => ViewportHeight; }

    int IHostingContext.ViewportWidth { get => ViewportWidth; }

    void IHostingContext.AppendHostedServerLog(string source, string message) { AppendHostedServerLog(source, message); }

    string IHostingContext.BuildHostedServerExitMessage() => BuildHostedServerExitMessage();

    void IHostingContext.CancelPendingHostedLocalConnect(string statusMessage = default) { CancelPendingHostedLocalConnect(statusMessage); }

    void IHostingContext.ClampHostSetupContentScrollOffset(OpenGarrison.Client.HostSetupMenuLayout layout) { ClampHostSetupContentScrollOffset(layout); }

    void IHostingContext.ClearHostedServerConsoleView() { ClearHostedServerConsoleView(); }

    void IHostingContext.CloseAllHostSetupMapPreviews() { CloseAllHostSetupMapPreviews(); }

    void IHostingContext.CloseCreditsMenu() { CloseCreditsMenu(); }

    void IHostingContext.CloseHostSetupMenu(bool clearStatus = false) { CloseHostSetupMenu(clearStatus); }

    void IHostingContext.CloseManualConnectMenu(bool clearStatus) { CloseManualConnectMenu(clearStatus); }

    void IHostingContext.ExecuteHostedServerCommandFromUi(string command) { ExecuteHostedServerCommandFromUi(command); }

    void IHostingContext.Exit() { Exit(); }

    Microsoft.Xna.Framework.Rectangle IHostingContext.GetHostSetupScrolledContentBounds(Microsoft.Xna.Framework.Rectangle bounds) => GetHostSetupScrolledContentBounds(bounds);

    void IHostingContext.HandleHostSetupFieldBackspace() { HandleHostSetupFieldBackspace(); }

    void IHostingContext.HandleHostSetupFieldCharacterInput(char character) { HandleHostSetupFieldCharacterInput(character); }

    void IHostingContext.HandleHostSetupOptionsMenu(Microsoft.Xna.Framework.Input.MouseState mouse, bool clickPressed) { HandleHostSetupOptionsMenu(mouse, clickPressed); }

    void IHostingContext.InitializeHostedServerConsole(bool reset) { InitializeHostedServerConsole(reset); }

    void IHostingContext.InitializeHostSetupFieldCursor(OpenGarrison.Client.Game1.HostSetupEditField field) { InitializeHostSetupFieldCursor(field); }

    bool IHostingContext.IsTextFieldDoubleClick(OpenGarrison.Client.Game1.TextFieldClickTarget target) => IsTextFieldDoubleClick(target);

    void IHostingContext.OpenHostSetupMenu() { OpenHostSetupMenu(); }

    void IHostingContext.PrepareHostedServerConsoleLaunchState(string serverName, int port, int maxPlayers, int timeLimitMinutes, int capLimit, int respawnSeconds, bool lobbyAnnounce, bool autoBalance, bool secondaryAbilitiesEnabled, bool resetConsole, string launcherLogMessage = default) { PrepareHostedServerConsoleLaunchState(serverName, port, maxPlayers, timeLimitMinutes, capLimit, respawnSeconds, lobbyAnnounce, autoBalance, secondaryAbilitiesEnabled, resetConsole, launcherLogMessage); }

    void IHostingContext.PrepareHostedServerLaunchUi(bool closeHostSetup, bool disconnectNetworkClient) { PrepareHostedServerLaunchUi(closeHostSetup, disconnectNetworkClient); }

    void IHostingContext.ResetTextFieldClickTarget() { ResetTextFieldClickTarget(); }

    void IHostingContext.SelectAllTextInActiveField(OpenGarrison.Client.Game1.TextFieldClickTarget clickTarget) { SelectAllTextInActiveField(clickTarget); }

    void IHostingContext.StopHostedServer() { StopHostedServer(); }

    bool IHostingContext.TryHandleScrollbarRangeDrag(Microsoft.Xna.Framework.Input.MouseState mouse, Microsoft.Xna.Framework.Input.MouseState previousMouse, System.Object owner, Microsoft.Xna.Framework.Rectangle trackBounds, ref int scrollOffset, int maxScrollOffset, int viewportSize, int contentSize, int minThumbHeight = 24) => TryHandleScrollbarRangeDrag(mouse, previousMouse, owner, trackBounds, ref scrollOffset, maxScrollOffset, viewportSize, contentSize, minThumbHeight);

    void IHostingContext.TryHostFromSetup(bool runInTerminal = false) { TryHostFromSetup(runInTerminal); }

    bool IHostingContext.TryResumeHostedServerSession(bool loadExistingLog, Nullable<int> expectedProcessId = default) => TryResumeHostedServerSession(loadExistingLog, expectedProcessId);

    bool IHostingContext.TryStartHostedServerBackground(string serverName, int port, int maxPlayers, string password, string rconPassword, int timeLimitMinutes, int capLimit, int respawnSeconds, bool lobbyAnnounce, bool autoBalance, bool secondaryAbilitiesEnabled, string requestedMap, string mapRotationFile, bool resetConsole, out string error) => TryStartHostedServerBackground(serverName, port, maxPlayers, password, rconPassword, timeLimitMinutes, capLimit, respawnSeconds, lobbyAnnounce, autoBalance, secondaryAbilitiesEnabled, requestedMap, mapRotationFile, resetConsole, out error);

    bool IHostingContext.TryStartHostedServerInTerminal(string serverName, int port, int maxPlayers, string password, string rconPassword, int timeLimitMinutes, int capLimit, int respawnSeconds, bool lobbyAnnounce, bool autoBalance, bool secondaryAbilitiesEnabled, string requestedMap, string mapRotationFile, out string error) => TryStartHostedServerInTerminal(serverName, port, maxPlayers, password, rconPassword, timeLimitMinutes, capLimit, respawnSeconds, lobbyAnnounce, autoBalance, secondaryAbilitiesEnabled, requestedMap, mapRotationFile, out error);

    void IHostingContext.UpdateHostSetupMapsMenu(Microsoft.Xna.Framework.Input.MouseState mouse, bool clickPressed, bool rightClickPressed, OpenGarrison.Client.HostSetupMapsMenuLayout layout) { UpdateHostSetupMapsMenu(mouse, clickPressed, rightClickPressed, layout); }

}
