#nullable enable

using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void HandleWelcomeMessage(WelcomeMessage welcome)
    {
        if (welcome.Version != ProtocolVersion.Current)
        {
            _networkClient.Disconnect();
            SetNetworkStatusAndConsole(
                "Protocol mismatch.",
                $"protocol mismatch: server={welcome.Version} client={ProtocolVersion.Current}");
            return;
        }

        if (!CustomMapSyncService.EnsureMapAvailable(
                welcome.LevelName,
                welcome.IsCustomMap,
                welcome.MapDownloadUrl,
                welcome.MapContentHash,
                out var welcomeMapError))
        {
            ReturnToMainMenuWithNetworkStatus(welcomeMapError, $"custom map sync failed: {welcomeMapError}");
            return;
        }

        ReinitializeSimulationForTickRate(welcome.TickRate);
        _networkClient.SetLocalPlayerSlot(welcome.PlayerSlot);
        _networkClient.SetServerDescription(welcome.ServerName);
        ResetSpectatorTracking(enableTracking: _networkClient.IsSpectator);
        _networkClient.ClearPendingTeamSelection();
        _networkClient.ClearPendingClassSelection();
        ResetGameplayRuntimeState();
        if (!_world.TryLoadLevel(welcome.LevelName))
        {
            var loadError = $"Failed to load map: {welcome.LevelName}";
            ReturnToMainMenuWithNetworkStatus(loadError);
            return;
        }

        _world.PrepareLocalPlayerJoin();
        ResetGameplayTransitionEffects();
        EnterGameplaySession(
            GameplaySessionKind.Online,
            openJoinMenus: !_networkClient.IsSpectator,
            statusMessage: _networkClient.IsSpectator ? "Connected as spectator." : string.Empty);
        StopMenuMusic();
        AddNetworkConsoleLine(
            _networkClient.IsSpectator
                ? $"connected to {welcome.ServerName} ({welcome.LevelName}) as spectator tickrate={welcome.TickRate}"
                : $"connected to {welcome.ServerName} ({welcome.LevelName}) tickrate={welcome.TickRate}");
    }

    private void HandleConnectionDeniedMessage(ConnectionDeniedMessage denied)
    {
        ReturnToMainMenuWithNetworkStatus(denied.Reason, $"connect denied: {denied.Reason}");
    }

    private void HandlePasswordRequestMessage()
    {
        OpenNetworkPasswordPrompt("Server requires a password.");
        AddNetworkConsoleLine("server requires a password");
    }

    private void HandlePasswordResultMessage(PasswordResultMessage passwordResult)
    {
        if (passwordResult.Accepted)
        {
            CloseNetworkPasswordPrompt();
            if (!_networkClient.IsSpectator)
            {
                OpenOnlineTeamSelection(clearPendingSelections: false, statusMessage: string.Empty);
            }

            AddNetworkConsoleLine("password accepted");
            return;
        }

        var rejectionReason = string.IsNullOrWhiteSpace(passwordResult.Reason) ? "Password rejected." : passwordResult.Reason;
        ReturnToMainMenuWithNetworkStatus(rejectionReason, $"password rejected: {passwordResult.Reason}");
    }
}
