#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    public void PrepareHostedServerLaunchUi(bool closeHostSetup, bool disconnectNetworkClient)
    {
        CloseManualConnectMenu(clearStatus: true);
        CloseLobbyBrowser(clearStatus: true);
        _optionsMenuOpen = false;
        _pluginOptionsMenuOpen = false;
        CloseCreditsMenu();
        _controlsMenuOpen = false;
        _pendingControlsBinding = null;
        _pendingControllerControlsBinding = null;

        if (closeHostSetup)
        {
            CloseHostSetupMenu();
            _editingPlayerName = false;
        }

        if (disconnectNetworkClient)
        {
            _networkClient.SendLastToDieLeave();
            _networkClient.Disconnect();
        }
    }

    public void PrepareHostedServerConsoleLaunchState(
        string serverName,
        int port,
        int maxPlayers,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool lobbyAnnounce,
        bool autoBalance,
        bool secondaryAbilitiesEnabled,
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
            autoBalance,
            secondaryAbilitiesEnabled);

        if (!string.IsNullOrWhiteSpace(launcherLogMessage))
        {
            AppendHostedServerLog("launcher", launcherLogMessage);
        }
    }

    public bool TryStartHostedServerBackground(
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
        string? mapRotationFile,
        bool resetConsole,
        out string error)
    {
        var launchOptions = CreateHostedServerLaunchOptions(
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
            mapRotationFile);
        InitializeHostedServerConsole(reset: resetConsole);
        _hostedLastToDieSoloSimulationPauseState = null;
        _hostedLastToDieSoloSimulationPauseRetryAtMilliseconds = 0;
        return _hostedServerRuntime.TryStartBackground(launchOptions, out error);
    }

    public void BeginPendingHostedLocalConnect(int port, int delayTicks, string statusMessage)
    {
        _pendingHostedConnectState.Port = port;
        _pendingHostedConnectState.Ticks = delayTicks;
        _menuStatusMessage = statusMessage;
    }

    public void CancelPendingHostedLocalConnect(string? statusMessage = null)
    {
        _pendingHostedConnectState.Ticks = -1;
        if (statusMessage is not null)
        {
            _menuStatusMessage = statusMessage;
        }
    }
}
