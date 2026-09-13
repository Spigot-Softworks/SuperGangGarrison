#nullable enable

using System;
using System.Net;
using System.Net.Http;
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
    private string _hostedLastToDieRoomCode = string.Empty;
    private OpenGarrison.Core.LastToDie.LastToDieDifficulty _pendingHostedLastToDieDifficulty;
    private int _pendingHostedLastToDiePort;

    private void TryStartSoloLastToDieRun(
        OpenGarrison.Core.LastToDie.LastToDieDifficulty difficulty)
        => TryStartEmbeddedSolo(difficulty);

    private void TryStartHostedLastToDieRun(OpenGarrison.Core.LastToDie.LastToDieDifficulty difficulty)
        => BeginPeerRoom(true, new PeerPracticeSettings(BlueBots: 0, Difficulty: difficulty == OpenGarrison.Core.LastToDie.LastToDieDifficulty.Hardcore ? "hardcore" : "standard"));

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
            && RelayRoomCode.TryNormalize(relay.RoomCode, out _)
            && Uri.TryCreate(relay.HostWebSocketUrl, UriKind.Absolute, out var hostUri)
            && hostUri.Scheme is "ws" or "wss"
            && WebSocketNetworkClientMessageTransport.IsWebSocketEndpoint(relay.GuestWebSocketUrl);
        if (!validRelay)
        {
            var relayException = task.Exception?.GetBaseException();
            _menuStatusMessage = relayException switch
            {
                HttpRequestException { StatusCode: HttpStatusCode.NotFound } =>
                    "Online co-op is temporarily unavailable because the relay service is not deployed.",
                HttpRequestException { StatusCode: HttpStatusCode.ServiceUnavailable } =>
                    "Online co-op relay is temporarily unavailable. Please try again later.",
                _ => $"Could not create the co-op room: {relayException?.Message ?? "the relay service returned an invalid session"}",
            };
            _lastToDieConnectionPresentationPending = false;
            return;
        }

        StartHostedLastToDieRun(
            _pendingHostedLastToDieDifficulty,
            _pendingHostedLastToDiePort,
            relay);
    }

    private void CancelPendingHostedLastToDieRelayLaunch()
    {
        _hostedLastToDieRelayLaunchRequested = false;
    }

    private void StartHostedLastToDieRun(
        OpenGarrison.Core.LastToDie.LastToDieDifficulty difficulty,
        int port,
        RelaySessionCreateResponse? relay,
        int maxPlayers = 2,
        bool publishSocialPresence = true)
    {
        if (maxPlayers > 1 && relay is null)
        {
            _menuStatusMessage = "Online co-op requires the relay service. Please try again later.";
            return;
        }

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
                : "Starting private Last to Die co-op through the social relay.");

        if (!_hostedServerRuntime.TryStartBackground(launchOptions, out var error))
        {
            _menuStatusMessage = error;
            return;
        }

        if (publishSocialPresence)
        {
            _hostedLastToDieRoomCode = relay?.RoomCode ?? string.Empty;
            SetHostedSocialPresenceEndpoint(0, relay?.GuestWebSocketUrl);
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
        var practice = _practiceCoOpMenu;
        OpenManagedRoomJoin();
        _practiceCoOpMenu = practice;
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
