#nullable enable

using System;
using System.Threading.Tasks;
using OpenGarrison.Core;
using OpenGarrison.ClientShared;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int HostedLastToDieDefaultPort = 8190;
    private Task<RelaySessionCreateResponse>? _hostedLastToDieRelayCreateTask;
    private bool _hostedLastToDieRelayLaunchRequested;
    private bool _lastToDieConnectionPresentationPending;
    private bool _lastToDieRoomCodeJoinOpen;
    private OpenGarrison.Core.LastToDie.LastToDieDifficulty _pendingHostedLastToDieDifficulty;
    private int _pendingHostedLastToDiePort;

    private void TryStartSoloLastToDieRun(
        OpenGarrison.Core.LastToDie.LastToDieDifficulty difficulty)
    {
        if (OperatingSystem.IsBrowser())
        {
            _menuStatusMessage = "Playing Last to Die solo is unavailable in browser.";
            return;
        }

        if (IsHostedServerRunning)
        {
            _menuStatusMessage = "Stop the current hosted server before starting Last to Die.";
            return;
        }

        if (!_bootstrapController.CanEnterGameplaySession(out var bootstrapReason))
        {
            _menuStatusMessage = bootstrapReason ?? "Client assets are still loading.";
            return;
        }

        var port = int.TryParse(_hostPortBuffer.Trim(), out var configuredPort)
            && configuredPort is > 0 and <= 65535
                ? configuredPort
                : HostedLastToDieDefaultPort;
        StartHostedLastToDieRun(
            difficulty,
            port,
            relay: null,
            relayFailure: string.Empty,
            maxPlayers: 1,
            publishSocialPresence: false);
    }

    private void TryStartHostedLastToDieRun(
        OpenGarrison.Core.LastToDie.LastToDieDifficulty difficulty)
    {
        if (OperatingSystem.IsBrowser())
        {
            _menuStatusMessage = "Hosting Last to Die is unavailable in browser.";
            return;
        }

        if (IsHostedServerRunning)
        {
            _menuStatusMessage = "Stop the current hosted server before starting Last to Die.";
            return;
        }

        if (!_bootstrapController.CanEnterGameplaySession(out var bootstrapReason))
        {
            _menuStatusMessage = bootstrapReason ?? "Client assets are still loading.";
            return;
        }

        var port = int.TryParse(_hostPortBuffer.Trim(), out var configuredPort)
            && configuredPort is > 0 and <= 65535
                ? configuredPort
                : HostedLastToDieDefaultPort;
        if (_hostedLastToDieRelayCreateTask is not null)
        {
            _menuStatusMessage = "A co-op relay is already being prepared.";
            return;
        }

        _pendingHostedLastToDieDifficulty = difficulty;
        _pendingHostedLastToDiePort = port;
        _hostedLastToDieRelayLaunchRequested = true;
        _menuStatusMessage = "Creating private co-op relay...";
        _hostedLastToDieRelayCreateTask = _presenceClient.CreateRelaySessionAsync(_clientIdentity);
    }

    private void CompleteHostedLastToDieRelayLaunch()
    {
        if (_hostedLastToDieRelayCreateTask is null || !_hostedLastToDieRelayCreateTask.IsCompleted)
        {
            return;
        }

        var task = _hostedLastToDieRelayCreateTask;
        _hostedLastToDieRelayCreateTask = null;
        if (!_hostedLastToDieRelayLaunchRequested)
        {
            _ = task.Exception;
            return;
        }

        _hostedLastToDieRelayLaunchRequested = false;
        var relay = task.IsCompletedSuccessfully ? task.Result : null;
        var validRelay = relay is not null
            && Uri.TryCreate(relay.HostWebSocketUrl, UriKind.Absolute, out var hostUri)
            && hostUri.Scheme is "ws" or "wss"
            && WebSocketNetworkClientMessageTransport.IsWebSocketEndpoint(relay.GuestWebSocketUrl);
        var relayFailure = validRelay
            ? string.Empty
            : task.Exception?.GetBaseException().Message ?? "relay service returned an invalid session";
        StartHostedLastToDieRun(
            _pendingHostedLastToDieDifficulty,
            _pendingHostedLastToDiePort,
            validRelay ? relay : null,
            relayFailure);
    }

    private void CancelPendingHostedLastToDieRelayLaunch()
    {
        _hostedLastToDieRelayLaunchRequested = false;
    }

    private void StartHostedLastToDieRun(
        OpenGarrison.Core.LastToDie.LastToDieDifficulty difficulty,
        int port,
        RelaySessionCreateResponse? relay,
        string relayFailure,
        int maxPlayers = 2,
        bool publishSocialPresence = true)
    {
        var launchOptions = HostedServerLaunchOptions.CreateLastToDie(
            RuntimePaths.GetConfigPath(OpenGarrisonPreferencesDocument.DefaultFileName),
            maxPlayers == 1 ? "Last To Die Solo" : "Last To Die Co-op",
            port,
            difficulty,
            maxPlayers: maxPlayers) with
        {
            RelayHostUrl = relay?.HostWebSocketUrl ?? string.Empty,
        };

        ClearReplayQueue(clearActiveReplayPath: true);
        PrepareHostedServerLaunchUi(closeHostSetup: false, disconnectNetworkClient: true);
        PrepareHostedServerConsoleLaunchState(
            launchOptions.ServerName,
            launchOptions.Port,
            launchOptions.MaxPlayers,
            launchOptions.TimeLimitMinutes,
            launchOptions.CapLimit,
            launchOptions.RespawnSeconds,
            launchOptions.LobbyAnnounce,
            launchOptions.AutoBalance,
            launchOptions.SecondaryAbilitiesEnabled,
            resetConsole: true,
            launcherLogMessage: maxPlayers == 1
                ? "Starting local authoritative Last to Die solo run."
                : relay is null
                ? $"Relay unavailable ({relayFailure}); starting direct Last to Die co-op on UDP port {port}."
                : "Starting private Last to Die co-op through the social relay.");

        if (!_hostedServerRuntime.TryStartBackground(launchOptions, out var error))
        {
            _menuStatusMessage = error;
            return;
        }

        if (publishSocialPresence)
        {
            SetHostedSocialPresenceEndpoint(port, relay?.GuestWebSocketUrl);
        }

        _lastToDieConnectionPresentationPending = true;
        CloseLastToDieMenu(clearStatus: true);
        BeginPendingHostedLocalConnect(
            port,
            delayTicks: 20,
            "Loading Last to Die...");
    }

    private void OpenLastToDieRoomCodeJoin()
    {
        if (OperatingSystem.IsBrowser())
        {
            _menuStatusMessage = "Direct Last to Die join is unavailable in browser.";
            return;
        }

        OpenManualConnectMenu();
        _lastToDieRoomCodeJoinOpen = true;
        _lastToDieConnectionPresentationPending = true;
        _connectHostBuffer = string.Empty;
        InitializeConnectHostCursor();
        _connectionFlowController.SetManualConnectEditingField(editHost: true);
        _connectPortBuffer = HostedLastToDieDefaultPort.ToString();
        _menuStatusMessage = string.Empty;
    }

    private void CloseManualConnectMenuToOrigin(bool clearStatus)
    {
        var returnToLastToDieCoOp = _lastToDieRoomCodeJoinOpen;
        CloseManualConnectMenu(clearStatus);
        if (!returnToLastToDieCoOp || !_mainMenuOpen)
        {
            return;
        }

        OpenLastToDieMenu();
        OpenLastToDieCoOpPage(true);
    }
}
