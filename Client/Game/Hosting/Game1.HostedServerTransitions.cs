#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void PrepareHostedServerLaunchUi(bool closeHostSetup, bool disconnectNetworkClient)
    {
        CloseManualConnectMenu(clearStatus: true);
        CloseLobbyBrowser(clearStatus: true);
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        CloseCreditsMenu();
        _controlsMenuOpen = false;

        if (closeHostSetup)
        {
            CloseHostSetupMenu();
            _editingPlayerName = false;
        }

        if (disconnectNetworkClient)
        {
            _networkClient.Disconnect();
        }
    }

    private void PrepareHostedServerConsoleLaunchState(
        string serverName,
        int port,
        int maxPlayers,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool lobbyAnnounce,
        bool autoBalance,
        bool resetConsole,
        string? launcherLogMessage = null)
    {
        InitializeHostedServerConsole(reset: resetConsole);
        PrimeHostedServerConsoleState(
            serverName,
            port,
            maxPlayers,
            timeLimitMinutes,
            capLimit,
            respawnSeconds,
            lobbyAnnounce,
            autoBalance);

        if (!string.IsNullOrWhiteSpace(launcherLogMessage))
        {
            AppendHostedServerLog("launcher", launcherLogMessage);
        }
    }

    private bool TryStartHostedServerBackground(
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
        string? mapRotationFile,
        bool resetConsole,
        out string error)
    {
        var launchOptions = CreateHostedServerLaunchOptions(
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
            mapRotationFile);
        InitializeHostedServerConsole(reset: resetConsole);
        return _hostedServerRuntime.TryStartBackground(launchOptions, out error);
    }

    private void BeginPendingHostedLocalConnect(int port, int delayTicks, string statusMessage)
    {
        _pendingHostedConnectPort = port;
        _pendingHostedConnectTicks = delayTicks;
        _menuStatusMessage = statusMessage;
    }

    private void CancelPendingHostedLocalConnect(string? statusMessage = null)
    {
        _pendingHostedConnectTicks = -1;
        if (statusMessage is not null)
        {
            _menuStatusMessage = statusMessage;
        }
    }
}
