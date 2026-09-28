#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public interface IHostingContext
{
    bool _controlsMenuOpen { get; set; }
    OpenGarrison.Client.HostedServerConsoleState _hostedServerConsole { get; }
    OpenGarrison.Client.HostedServerRuntimeController _hostedServerRuntime { get; }
    bool _hostLobbyAnnounceEnabled { get; set; }
    List<OpenGarrison.Core.OpenGarrisonMapRotationEntry> _hostMapEntries { get; set; }
    int _hostSetupContentScrollOffset { get; set; }
    OpenGarrison.Client.Game1.HostSetupEditField _hostSetupEditField { get; set; }
    OpenGarrison.Client.HostSetupScreen _hostSetupScreen { get; set; }
    OpenGarrison.Client.Game1.HostSetupFormState _hostSetupState { get; }
    OpenGarrison.Client.Game1.HostSetupTab _hostSetupTab { get; set; }
    bool _hostUsePlaylistFile { get; set; }
    bool _mainMenuOpen { get; set; }
    string _menuStatusMessage { get; set; }
    bool _optionsMenuOpen { get; set; }
    Nullable<OpenGarrison.Client.Game1.ControllerControlsMenuBinding> _pendingControllerControlsBinding { get; set; }
    Nullable<OpenGarrison.Client.Game1.ControlsMenuBinding> _pendingControlsBinding { get; set; }
    int _pendingHostedConnectPort { get; set; }
    bool _pluginOptionsMenuOpen { get; set; }
    Microsoft.Xna.Framework.Input.MouseState _previousMouse { get; set; }
    bool _startupSplashOpen { get; set; }
    bool IsHostedServerRunning { get; }
    bool IsServerLauncherMode { get; }
    OpenGarrison.Client.ScrollbarDragController ScrollbarDrag { get; }
    int ViewportHeight { get; }
    int ViewportWidth { get; }
    void AppendHostedServerLog(string source, string message);
    string BuildHostedServerExitMessage();
    void CancelPendingHostedLocalConnect(string statusMessage = default);
    void ClampHostSetupContentScrollOffset(OpenGarrison.Client.HostSetupMenuLayout layout);
    void ClearHostedServerConsoleView();
    void CloseAllHostSetupMapPreviews();
    void CloseCreditsMenu();
    void CloseHostSetupMenu(bool clearStatus = false);
    void CloseManualConnectMenu(bool clearStatus);
    void ExecuteHostedServerCommandFromUi(string command);
    void Exit();
    Microsoft.Xna.Framework.Rectangle GetHostSetupScrolledContentBounds(Microsoft.Xna.Framework.Rectangle bounds);
    void HandleHostSetupFieldBackspace();
    void HandleHostSetupFieldCharacterInput(char character);
    void HandleHostSetupOptionsMenu(Microsoft.Xna.Framework.Input.MouseState mouse, bool clickPressed);
    void InitializeHostedServerConsole(bool reset);
    void InitializeHostSetupFieldCursor(OpenGarrison.Client.Game1.HostSetupEditField field);
    bool IsTextFieldDoubleClick(OpenGarrison.Client.Game1.TextFieldClickTarget target);
    void OpenHostSetupMenu();
    void PrepareHostedServerConsoleLaunchState(string serverName, int port, int maxPlayers, int timeLimitMinutes, int capLimit, int respawnSeconds, bool lobbyAnnounce, bool autoBalance, bool secondaryAbilitiesEnabled, bool resetConsole, string launcherLogMessage = default);
    void PrepareHostedServerLaunchUi(bool closeHostSetup, bool disconnectNetworkClient);
    void ResetTextFieldClickTarget();
    void SelectAllTextInActiveField(OpenGarrison.Client.Game1.TextFieldClickTarget clickTarget);
    void StopHostedServer();
    bool TryHandleScrollbarRangeDrag(Microsoft.Xna.Framework.Input.MouseState mouse, Microsoft.Xna.Framework.Input.MouseState previousMouse, System.Object owner, Microsoft.Xna.Framework.Rectangle trackBounds, ref int scrollOffset, int maxScrollOffset, int viewportSize, int contentSize, int minThumbHeight = 24);
    void TryHostFromSetup(bool runInTerminal = false);
    bool TryResumeHostedServerSession(bool loadExistingLog, Nullable<int> expectedProcessId = default);
    bool TryStartHostedServerBackground(string serverName, int port, int maxPlayers, string password, string rconPassword, int timeLimitMinutes, int capLimit, int respawnSeconds, bool lobbyAnnounce, bool autoBalance, bool secondaryAbilitiesEnabled, string requestedMap, string mapRotationFile, bool resetConsole, out string error);
    bool TryStartHostedServerInTerminal(string serverName, int port, int maxPlayers, string password, string rconPassword, int timeLimitMinutes, int capLimit, int respawnSeconds, bool lobbyAnnounce, bool autoBalance, bool secondaryAbilitiesEnabled, string requestedMap, string mapRotationFile, out string error);
    void UpdateHostSetupMapsMenu(Microsoft.Xna.Framework.Input.MouseState mouse, bool clickPressed, bool rightClickPressed, OpenGarrison.Client.HostSetupMapsMenuLayout layout);
}
