#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    public bool IsServerLauncherMode => _startupMode == GameStartupMode.ServerLauncher;

    public bool IsHostedServerRunning
    {
        get
        {
            return _hostedServerRuntime.IsRunning;
        }
    }

    public void InitializeServerLauncherMode()
    {
        _hostingManager.HostSetup.InitializeServerLauncherMode();
    }

    private void UpdateServerLauncherState()
    {
        _hostingManager.HostSetup.UpdateServerLauncherState();
    }

    private string GetHostSetupTitle()
    {
        return _hostingManager.HostSetup.GetHostSetupTitle();
    }

    private string GetHostSetupSubtitle()
    {
        return _hostingManager.HostSetup.GetHostSetupSubtitle();
    }

    private string GetHostSetupPrimaryButtonLabel()
    {
        return _hostingManager.HostSetup.GetHostSetupPrimaryButtonLabel();
    }

    private string GetHostSetupSecondaryButtonLabel()
    {
        return _hostingManager.HostSetup.GetHostSetupSecondaryButtonLabel();
    }

    public bool TryHandleServerLauncherBackAction()
    {
        return _hostingManager.HostSetup.TryHandleServerLauncherBackAction();
    }

    private void BeginDedicatedServerLaunch(
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
        _hostingManager.HostSetup.BeginDedicatedServerLaunch(
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
    }

    private void BeginDedicatedServerTerminalLaunch(
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
        _hostingManager.HostSetup.BeginDedicatedServerTerminalLaunch(
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
    }
}
