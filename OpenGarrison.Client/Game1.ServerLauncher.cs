#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsServerLauncherMode => _startupMode == GameStartupMode.ServerLauncher;

    private bool IsHostedServerRunning
    {
        get
        {
            return _hostedServerRuntime.IsRunning;
        }
    }

    private void InitializeServerLauncherMode()
    {
        InitializeHostedServerConsole(reset: false);
        AppendHostedServerLog("launcher", "OpenGarrison.ServerLauncher initialized.");
        _startupSplashOpen = false;
        _mainMenuOpen = true;
        _manualConnectOpen = false;
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _creditsOpen = false;
        _controlsMenuOpen = false;
        _hostSetupOpen = false;
        _hostSetupEditField = HostSetupEditField.None;
        _hostSetupTab = HostSetupTab.Settings;
        OpenHostSetupMenu();
        if (TryResumeHostedServerSession(loadExistingLog: true))
        {
            _hostSetupTab = HostSetupTab.ServerConsole;
            _hostSetupEditField = HostSetupEditField.ServerConsoleCommand;
            _menuStatusMessage = $"Resumed dedicated server on UDP port {_hostedServerConsole.CreateSnapshot().StatusPort}.";
        }
        else
        {
            _menuStatusMessage = "Configure and start a dedicated server.";
        }
    }

    private void UpdateServerLauncherState()
    {
        if (!IsServerLauncherMode)
        {
            return;
        }

        var runtimeUpdateState = _hostedServerRuntime.UpdateForLauncher();
        if (runtimeUpdateState is HostedServerRuntimeUpdateState.SessionEnded
            or HostedServerRuntimeUpdateState.ProcessExited)
        {
            _menuStatusMessage = BuildHostedServerExitMessage();
        }
    }

    private string GetHostSetupTitle()
    {
        return IsServerLauncherMode ? "Dedicated Server" : "Host Game";
    }

    private string GetHostSetupSubtitle()
    {
        if (!IsServerLauncherMode)
        {
            return "Server rules and stock map rotation";
        }

        return IsHostedServerRunning
            ? "Dedicated server is running in the background"
            : "Configure and run a headless server process";
    }

    private string GetHostSetupPrimaryButtonLabel()
    {
        if (!IsServerLauncherMode)
        {
            return "Host";
        }

        return IsHostedServerRunning ? "Server Running" : "Start Server";
    }

    private string GetHostSetupSecondaryButtonLabel()
    {
        if (!IsServerLauncherMode)
        {
            return "Back";
        }

        return IsHostedServerRunning ? "Stop Server" : "Quit";
    }

    private bool TryHandleServerLauncherBackAction()
    {
        if (!IsServerLauncherMode)
        {
            return false;
        }

        _hostSetupEditField = HostSetupEditField.None;
        if (IsHostedServerRunning)
        {
            AppendHostedServerLog("launcher", "Back action requested while dedicated server was running.");
            StopHostedServer();
            _menuStatusMessage = "Dedicated server stopped.";
        }
        else
        {
            AppendHostedServerLog("launcher", "Back action requested with no running server; exiting launcher.");
            Exit();
        }

        return true;
    }

    private void BeginDedicatedServerLaunch(
        string serverName,
        int port,
        int maxPlayers,
        string password,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool lobbyAnnounce,
        bool autoBalance)
    {
        CloseManualConnectMenu(clearStatus: true);
        CloseLobbyBrowser(clearStatus: true);
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _creditsOpen = false;
        _controlsMenuOpen = false;
        InitializeHostedServerConsole(reset: true);
        PrimeHostedServerConsoleState(
            serverName,
            port,
            maxPlayers,
            timeLimitMinutes,
            capLimit,
            respawnSeconds,
            lobbyAnnounce,
            autoBalance);
        AppendHostedServerLog("launcher", $"Start Server pressed for UDP port {port}.");

        if (!TryStartHostedServer(
                serverName,
                port,
                maxPlayers,
                password,
                timeLimitMinutes,
                capLimit,
                respawnSeconds,
                lobbyAnnounce,
                autoBalance,
                out var error))
        {
            _menuStatusMessage = error;
            return;
        }

        _pendingHostedConnectTicks = -1;
        _pendingHostedConnectPort = port;
        _hostSetupEditField = HostSetupEditField.None;
        _hostSetupTab = HostSetupTab.ServerConsole;
        _menuStatusMessage = $"Starting dedicated server on UDP port {port}...";
    }

    private void BeginDedicatedServerTerminalLaunch(
        string serverName,
        int port,
        int maxPlayers,
        string password,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool lobbyAnnounce,
        bool autoBalance)
    {
        CloseManualConnectMenu(clearStatus: true);
        CloseLobbyBrowser(clearStatus: true);
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        _creditsOpen = false;
        _controlsMenuOpen = false;
        InitializeHostedServerConsole(reset: true);
        PrimeHostedServerConsoleState(
            serverName,
            port,
            maxPlayers,
            timeLimitMinutes,
            capLimit,
            respawnSeconds,
            lobbyAnnounce,
            autoBalance);

        if (!TryStartHostedServerInTerminal(
                serverName,
                port,
                maxPlayers,
                password,
                timeLimitMinutes,
                capLimit,
                respawnSeconds,
                lobbyAnnounce,
                autoBalance,
                out var error))
        {
            _menuStatusMessage = error;
            return;
        }

        Exit();
    }
}
