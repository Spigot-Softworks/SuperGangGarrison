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
        _hostSetupFlowController.InitializeServerLauncherMode();
    }

    private void UpdateServerLauncherState()
    {
        _hostSetupFlowController.UpdateServerLauncherState();
    }

    private string GetHostSetupTitle()
    {
        return _hostSetupFlowController.GetHostSetupTitle();
    }

    private string GetHostSetupSubtitle()
    {
        return _hostSetupFlowController.GetHostSetupSubtitle();
    }

    private string GetHostSetupPrimaryButtonLabel()
    {
        return _hostSetupFlowController.GetHostSetupPrimaryButtonLabel();
    }

    private string GetHostSetupSecondaryButtonLabel()
    {
        return _hostSetupFlowController.GetHostSetupSecondaryButtonLabel();
    }

    private bool TryHandleServerLauncherBackAction()
    {
        return _hostSetupFlowController.TryHandleServerLauncherBackAction();
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
        bool autoBalance,
        string? requestedMap,
        string? mapRotationFile)
    {
        _hostSetupFlowController.BeginDedicatedServerLaunch(
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
        bool autoBalance,
        string? requestedMap,
        string? mapRotationFile)
    {
        _hostSetupFlowController.BeginDedicatedServerTerminalLaunch(
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
    }
}
