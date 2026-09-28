#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using OpenGarrison.ClientShared;
using OpenGarrison.Protocol;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class OnlineSessionController
    {
        private readonly IGameplayContext _context;
        private IReadOnlyList<NetworkEndpointCandidate>? _pendingConnectionCandidates;
        private NetworkEndpoint _pendingConnectionEndpoint;
        private OnlineConnectionIntent _pendingConnectionIntent = OnlineConnectionIntent.Join;
        private int _nextPendingConnectionCandidateIndex;

        public OnlineSessionController(IGameplayContext context)
        {
            _context = context;
        }

        public void BeginHostedGame(
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
            if (!_context.Gameplay.Bootstrap.CanEnterGameplaySession(out var bootstrapReason))
            {
                _context.SetPersistedMenuStatusMessage(bootstrapReason ?? "Browser client assets are still loading.");
                return;
            }

            _context.ClearReplayQueue(clearActiveReplayPath: true);
            _context.PrepareHostedServerLaunchUi(closeHostSetup: true, disconnectNetworkClient: true);

            if (!_context.TryStartHostedServerBackground(
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
                    mapRotationFile,
                    resetConsole: false,
                    out var error))
            {
                _context._menuStatusMessage = error;
                return;
            }

            _context.BeginPendingHostedLocalConnect(port, delayTicks: 20, "Starting local server...");
        }

        public bool TryConnectToServer(string host, int port, bool addConsoleFeedback)
        {
            return TryConnectToServer(NetworkEndpoint.ForCurrentRuntimeSinglePort(host, port), addConsoleFeedback);
        }

        public bool TryConnectLegacyGg2Server(string host, int port, bool addConsoleFeedback)
        {
            if (!_context.Gameplay.Bootstrap.CanEnterGameplaySession(out var bootstrapReason))
            {
                _context.SetNetworkStatus(bootstrapReason ?? "Client assets are still loading.");
                return false;
            }

            _context.ClearReplayQueue(clearActiveReplayPath: true);
            _context.ClearPendingNetworkMapSync();
            _context._onlineConnectionIntent = OnlineConnectionIntent.Join;
            _pendingConnectionCandidates = null;
            var connected = OperatingSystem.IsBrowser()
                ? TryConnectBrowserGg2Gateway(host, port, out var transport, out var error)
                : LegacyGg2NetworkClientTransport.TryConnect(host, port, out transport, out error);
            if (!connected
                || transport is null)
            {
                _context.SetNetworkStatus($"GG2 connection failed: {error}");
                if (addConsoleFeedback) _context.AddNetworkConsoleLine($"GG2 connection failed: {error}");
                return false;
            }

            if (!_context._networkClient.Connect(
                    transport,
                    _context._world.LocalPlayer.DisplayName,
                    _context._world.LocalPlayer.BadgeMask,
                    out error))
            {
                _context.SetNetworkStatus($"GG2 connection failed: {error}");
                if (addConsoleFeedback) _context.AddNetworkConsoleLine($"GG2 connection failed: {error}");
                return false;
            }

            _context.ClearOnlinePlayerSocialProfiles();
            _context.ResetGameplayRuntimeState();
            _context._world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings());
            _context.CloseLobbyBrowser(clearStatus: false);
            _context.SetJoiningServerLoadingLabel($"GG2 {host}:{port}");
            _context.ShowJoiningServerLoadingOverlay();
            _context.SetNetworkStatus($"Connecting to GG2 {host}:{port}...");
            if (addConsoleFeedback) _context.AddNetworkConsoleLine($"connecting to GG2 {host}:{port}");
            return true;
        }

        private static bool TryConnectBrowserGg2Gateway(
            string host, int port, out INetworkClientMessageTransport? transport, out string error)
        {
            transport = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
            {
                error = "A GG2 server address and port are required.";
                return false;
            }

            var gateway = OpenGarrison.ClientShared.ClientDistribution.CreateGg2GatewayEndpoint(host, port);
            if (!NetworkClientMessageTransportRegistry.TryConnect(gateway.AbsoluteUri, 0, out var websocket, out error)
                || websocket is null)
                return false;
            transport = new LegacyGg2BrowserMessageTransport(websocket, host, port);
            return true;
        }

        public bool TryConnectToServer(NetworkEndpoint endpoint, bool addConsoleFeedback)
        {
            return TryConnectToServer(endpoint, addConsoleFeedback, OnlineConnectionIntent.Join);
        }

        public bool TryConnectToServer(NetworkEndpoint endpoint, bool addConsoleFeedback, OnlineConnectionIntent intent)
        {
            if (OpenGarrison.ClientShared.ClientDistribution.IsGg2Only)
            {
                _context.SetNetworkStatus("This edition connects through the GG2 server browser.");
                return false;
            }
            if (IsRestrictedBrowserEdition
                && (intent != OnlineConnectionIntent.Join
                    || !OpenGarrison.ClientShared.ClientDistribution.AllowsEndpoint(endpoint.WebSocketUrl)))
            {
                _context.SetNetworkStatus("Join a Last to Die room to connect.");
                return false;
            }
            if (!_context.Gameplay.Bootstrap.CanEnterGameplaySession(out var bootstrapReason))
            {
                _context.SetNetworkStatus(bootstrapReason ?? "Browser client assets are still loading.");
                return false;
            }

            _context.ClearReplayQueue(clearActiveReplayPath: true);
            _context.ClearPendingNetworkMapSync();
            _context._onlineConnectionIntent = intent;
            var candidates = endpoint.GetConnectionCandidates();
            _pendingConnectionCandidates = null;
            _nextPendingConnectionCandidateIndex = 0;
            if (candidates.Count == 0)
            {
                _context._onlineConnectionIntent = OnlineConnectionIntent.Join;
                _context.SetNetworkStatus("Connect failed: endpoint does not support this runtime.");
                if (addConsoleFeedback)
                {
                    _context.AddNetworkConsoleLine("connect failed: endpoint does not support this runtime.");
                }

                return false;
            }

            _pendingConnectionCandidates = candidates;
            _pendingConnectionEndpoint = endpoint;
            _pendingConnectionIntent = intent;
            var errors = new List<string>();
            for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex += 1)
            {
                var candidate = candidates[candidateIndex];
                if (TryConnectCandidate(endpoint, candidate, intent, out var error))
                {
                    _nextPendingConnectionCandidateIndex = candidateIndex + 1;
                    PresentSuccessfulConnectionStart(endpoint, candidate, intent, addConsoleFeedback);
                    return true;
                }

                errors.Add($"{FormatNetworkEndpointTransport(candidate.Transport)}: {error}");
            }

            _pendingConnectionCandidates = null;
            _context._onlineConnectionIntent = OnlineConnectionIntent.Join;
            var errorText = errors.Count == 0 ? "no compatible endpoint" : string.Join("; ", errors);
            _context.SetNetworkStatus($"Connect failed: {errorText}");
            if (addConsoleFeedback)
            {
                _context.AddNetworkConsoleLine($"connect failed: {errorText}");
            }

            return false;
        }

        public bool TryAdvancePendingConnectionCandidate(string disconnectReason)
        {
            var candidates = _pendingConnectionCandidates;
            if (candidates is null
                || _nextPendingConnectionCandidateIndex >= candidates.Count
                || string.IsNullOrWhiteSpace(disconnectReason)
                || !disconnectReason.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var endpoint = _pendingConnectionEndpoint;
            var intent = _pendingConnectionIntent;
            var errors = new List<string> { $"{FormatNetworkEndpointTransport(candidates[_nextPendingConnectionCandidateIndex - 1].Transport)}: {disconnectReason}" };
            while (_nextPendingConnectionCandidateIndex < candidates.Count)
            {
                var candidateIndex = _nextPendingConnectionCandidateIndex;
                var candidate = candidates[candidateIndex];
                _nextPendingConnectionCandidateIndex = candidateIndex + 1;
                if (TryConnectCandidate(endpoint, candidate, intent, out var error))
                {
                    PresentSuccessfulConnectionStart(endpoint, candidate, intent, addConsoleFeedback: false);
                    return true;
                }

                errors.Add($"{FormatNetworkEndpointTransport(candidate.Transport)}: {error}");
            }

            _pendingConnectionCandidates = null;
            _context._onlineConnectionIntent = OnlineConnectionIntent.Join;
            _context.SetNetworkStatus($"Connect failed: {string.Join("; ", errors)}");
            return false;
        }

        public void ClearPendingConnectionCandidates()
        {
            _pendingConnectionCandidates = null;
            _nextPendingConnectionCandidateIndex = 0;
        }

        private bool TryConnectCandidate(
            NetworkEndpoint endpoint,
            NetworkEndpointCandidate candidate,
            OnlineConnectionIntent intent,
            out string error)
        {
            var clientInstanceId = Guid.TryParse(_context._clientIdentity.ClientId, out var parsedClientInstanceId)
                ? parsedClientInstanceId
                : Guid.Empty;
            var connected = _context._networkClient.Connect(
                candidate.Host,
                candidate.Port,
                _context._world.LocalPlayer.DisplayName,
                _context._world.LocalPlayer.BadgeMask,
                out error,
                _context._clientIdentity.FriendCode,
                PlayerCardProfile.Serialize(_context._clientIdentity.PlayerCard),
                ToProtocolConnectionIntent(intent),
                clientInstanceId);
            if (connected)
            {
                if (CustomMapSyncService.TryCreateServerDownloadBaseUri(endpoint, candidate, out var mapDownloadBaseUri))
                {
                    _context._networkClient.SetMapDownloadBaseUri(mapDownloadBaseUri);
                }

                _context.EnsureAutomaticDemoRecordingForConnection(candidate.Host);
            }

            return connected;
        }

        private void PresentSuccessfulConnectionStart(
            NetworkEndpoint endpoint,
            NetworkEndpointCandidate candidate,
            OnlineConnectionIntent intent,
            bool addConsoleFeedback)
        {
            _context.SetSocialPresenceNetworkEndpoint(endpoint);
            if (!IsRestrictedBrowserEdition) _context.RecordRecentConnection(candidate.Host, candidate.Port);
            _context.ClearOnlinePlayerSocialProfiles();
            _context.ResetGameplayRuntimeState();
            // Online sessions must always start from default server-authoritative gameplay rules.
            // This prevents offline experimental settings (for example Last To Die perks)
            // from leaking into client prediction when the world instance is reused.
            _context._world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings());
            _context.CloseLobbyBrowser(clearStatus: false);
            var transportLabel = FormatNetworkEndpointTransport(candidate.Transport);
            var actionLabel = intent == OnlineConnectionIntent.Watch ? "Watching" : "Connecting to";
            _context.SetJoiningServerLoadingLabel(_context.HasManagedRoom ? "Last to Die" : endpoint.AddressLabel);
            _context.ShowJoiningServerLoadingOverlay();
            _context.SetNetworkStatus(
                _context._lastToDieConnectionPresentationPending
                    ? "Loading Last to Die..."
                    : $"{actionLabel} {candidate.Host}:{candidate.Port} over {transportLabel}...");
            if (addConsoleFeedback)
            {
                _context.AddNetworkConsoleLine($"{actionLabel.ToLowerInvariant()} {candidate.Host}:{candidate.Port} over {transportLabel}");
            }
        }

        private static string FormatNetworkEndpointTransport(NetworkEndpointTransport transport)
            => transport switch
            {
                NetworkEndpointTransport.Quic => "QUIC64",
                NetworkEndpointTransport.WebSocket => "WebSocket",
                _ => "UDP",
            };

        public bool TryPlayLegacyReplay(string replayPath, bool addConsoleFeedback, bool clearQueuedReplays = true)
        {
            if (!_context.Gameplay.Bootstrap.CanEnterGameplaySession(out var bootstrapReason))
            {
                _context.SetNetworkStatus(bootstrapReason ?? "Browser client assets are still loading.");
                return false;
            }

            if (clearQueuedReplays)
            {
                _context.ClearReplayQueue(clearActiveReplayPath: true);
            }

            if (!ReDsmReplayTransport.TryCreate(replayPath, out var transport, out var error) || transport is null)
            {
                _context.SetNetworkStatus($"Replay failed: {error}");
                if (addConsoleFeedback)
                {
                    _context.AddNetworkConsoleLine($"replay failed: {error}");
                }

                return false;
            }

            if (_context._networkClient.Connect(transport, _context._world.LocalPlayer.DisplayName, _context._world.LocalPlayer.BadgeMask, out error))
            {
                _context._onlineConnectionIntent = OnlineConnectionIntent.Join;
                _context._activeReplayPath = replayPath.Trim();
                _context.ClearOnlinePlayerSocialProfiles();
                _context.ResetGameplayRuntimeState();
                _context._world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings());
                _context.CloseLobbyBrowser(clearStatus: false);
                _context.ShowLoadingOverlay($"Loading replay {Path.GetFileName(replayPath)}...", progress: null);
                _context.SetNetworkStatus($"Loading replay {Path.GetFileName(replayPath)}...");
                if (addConsoleFeedback)
                {
                    _context.AddNetworkConsoleLine($"loading replay {Path.GetFileName(replayPath)}");
                }

                return true;
            }

            _context.SetNetworkStatus($"Replay failed: {error}");
            if (addConsoleFeedback)
            {
                _context.AddNetworkConsoleLine($"replay failed: {error}");
            }

            return false;
        }

        public bool TryPlayOpenGarrisonDemo(string demoPath, bool addConsoleFeedback)
        {
            if (!_context.Gameplay.Bootstrap.CanEnterGameplaySession(out var bootstrapReason))
            {
                _context.SetNetworkStatus(bootstrapReason ?? "Browser client assets are still loading.");
                return false;
            }

            _context.ClearReplayQueue(clearActiveReplayPath: true);
            if (!OpenGarrisonDemoTransport.TryCreate(demoPath, out var transport, out var error) || transport is null)
            {
                _context.SetNetworkStatus($"Demo failed: {error}");
                if (addConsoleFeedback)
                {
                    _context.AddNetworkConsoleLine($"demo failed: {error}");
                }

                return false;
            }

            if (_context._networkClient.Connect(transport, _context._world.LocalPlayer.DisplayName, _context._world.LocalPlayer.BadgeMask, out error))
            {
                _context._onlineConnectionIntent = OnlineConnectionIntent.Join;
                _context._activeReplayPath = demoPath.Trim();
                _context.ClearOnlinePlayerSocialProfiles();
                _context.ResetGameplayRuntimeState();
                _context._world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings());
                _context.CloseLobbyBrowser(clearStatus: false);
                _context.ShowLoadingOverlay($"Loading demo {Path.GetFileName(demoPath)}...", progress: null);
                _context.SetNetworkStatus($"Loading demo {Path.GetFileName(demoPath)}...");
                if (addConsoleFeedback)
                {
                    _context.AddNetworkConsoleLine($"loading demo {Path.GetFileName(demoPath)}");
                }

                return true;
            }

            _context.SetNetworkStatus($"Demo failed: {error}");
            if (addConsoleFeedback)
            {
                _context.AddNetworkConsoleLine($"demo failed: {error}");
            }

            return false;
        }

        public bool TrySeekOpenGarrisonDemo(int deltaMilliseconds, out int targetMilliseconds, out string error)
        {
            targetMilliseconds = 0;
            error = string.Empty;
            if (!_context._networkClient.TryCreateSeekedReplayTransport(
                    deltaMilliseconds,
                    out var transport,
                    out targetMilliseconds,
                    out error)
                || transport is null)
            {
                return false;
            }

            if (!_context._networkClient.Connect(
                    transport,
                    _context._world.LocalPlayer.DisplayName,
                    _context._world.LocalPlayer.BadgeMask,
                    out error))
            {
                transport.Dispose();
                return false;
            }

            _context._onlineConnectionIntent = OnlineConnectionIntent.Join;
            _context._replaySeekCatchUpActive = true;
            _context._replaySeekTargetMilliseconds = targetMilliseconds;
            _context.ClearOnlinePlayerSocialProfiles();
            _context.ResetGameplayRuntimeState();
            _context._world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings());
            _context.ShowLoadingOverlay($"Seeking replay to {Game1.FormatReplayPlaybackTime(targetMilliseconds)}...", progress: null);
            _context.SetNetworkStatus($"Seeking replay to {Game1.FormatReplayPlaybackTime(targetMilliseconds)}...");
            return true;
        }

        public void HandleWelcomeMessage(WelcomeMessage welcome)
        {
            // Hello retries can leave more than one welcome in flight while a map loads.
            // Once admitted, a repeated reply must not reset the world or join menus.
            // Reconnect resets the slot, and replay seeks intentionally replay welcomes.
            if (!_context._networkClient.IsReplayConnection && _context._networkClient.LocalPlayerSlot != 0)
            {
                return;
            }

            ClearPendingConnectionCandidates();
            if (welcome.Version != ProtocolVersion.Current)
            {
                _context._networkClient.Disconnect();
                _context.SetNetworkStatusAndConsole(
                    "Protocol mismatch.",
                    $"protocol mismatch: server={welcome.Version} client={ProtocolVersion.Current}");
                return;
            }

            // Map acquisition can span many frames. Keep normal pings running during
            // that work instead of retrying an already completed handshake.
            _context._networkClient.AcknowledgeWelcomeReceipt();
            var mapSyncStatus = _context.TryEnsureNetworkMapAvailable(
                welcome.LevelName,
                welcome.IsCustomMap,
                welcome.MapDownloadUrl,
                welcome.MapContentHash,
                out var welcomeMapError);
            if (mapSyncStatus == NetworkMapSyncStatus.Pending)
            {
                _context.QueueWelcomeAfterNetworkMapSync(welcome);
                return;
            }

            if (mapSyncStatus == NetworkMapSyncStatus.Failed)
            {
                _context.ReturnToMainMenuWithNetworkStatus(welcomeMapError, $"custom map sync failed: {welcomeMapError}");
                return;
            }

            _context.ReinitializeSimulationForTickRate(welcome.TickRate);
            _context._world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings());
            _context._networkClient.SetLocalPlayerSlot(welcome.PlayerSlot);
            if (_context._onlineConnectionIntent == OnlineConnectionIntent.Watch
                && !_context._networkClient.IsSpectator)
            {
                _context._networkClient.Disconnect();
                _context.ReturnToMainMenuWithNetworkStatus("Watch connection returned a playable slot.");
                return;
            }

            _context._networkClient.SetServerDescription(welcome.ServerName);
            _context._networkClient.SetServerMaxPlayerCount(welcome.MaxPlayerCount);
            _context.ResetSpectatorTracking(enableTracking: _context._networkClient.IsSpectator);
            _context._networkClient.ClearPendingTeamSelection();
            _context._networkClient.ClearPendingClassSelection();
            _context.ResetGameplayRuntimeState();
            _context._serverLocalPredictionEnabled = welcome.LocalPredictionEnabled && !_context._networkClient.IsSpectator;
            _context.ShowJoiningServerLoadingOverlay();
            if (!_context._world.TryLoadLevel(welcome.LevelName, mapAreaIndex: 1, preservePlayerStats: false, mapScale: welcome.MapScale))
            {
                var loadError = $"Failed to load map: {welcome.LevelName}";
                _context.ReturnToMainMenuWithNetworkStatus(loadError);
                return;
            }

            _context._world.ConfigureSessionPresentationSeed(welcome.LevelName, welcome.MapContentHash);

            _context._world.PrepareLocalPlayerJoin();
            _context.ResetGameplayTransitionEffects();
            _context.BeginNetworkWorldWarmup(welcome.LevelName);
            _context.Gameplay.Session.EnterGameplaySession(
                GameplaySessionKind.Online,
                openJoinMenus: !_context._networkClient.IsReplayConnection
                    && _context._onlineConnectionIntent != OnlineConnectionIntent.Watch
                    && (!_context._networkClient.IsSpectator || _context._networkClient.IsLegacyGg2Connection),
                statusMessage: _context._networkClient.IsSpectator && !_context._networkClient.IsLegacyGg2Connection
                    ? "Connected as spectator."
                    : string.Empty);
            _context.StopMenuMusic();
            if (_context._peerRoomSession?.State is { Kind: "Practice", Players: { } roomPlayers })
            {
                var seat = roomPlayers.FirstOrDefault(player => player.Slot == welcome.PlayerSlot);
                _context._networkClient.QueueTeamSelection(seat?.Team == "Blue" ? PlayerTeam.Blue : PlayerTeam.Red);
                _context._teamSelectOpen = false;
                _context.OpenGameplayClassSelection();
            }
            _context.AddNetworkConsoleLine(
                _context._networkClient.IsSpectator
                    ? $"connected to {welcome.ServerName} ({welcome.LevelName}) as spectator tickrate={welcome.TickRate}"
                    : $"connected to {welcome.ServerName} ({welcome.LevelName}) tickrate={welcome.TickRate}");
            if (!_context._networkClient.IsLegacyGg2Connection)
            {
                _context.BeginGameplayAccountAttach();
                _context.UploadSelectedCustomBubbleState();
            }
        }

        private static ConnectionIntent ToProtocolConnectionIntent(OnlineConnectionIntent intent)
        {
            return intent == OnlineConnectionIntent.Watch
                ? ConnectionIntent.Watch
                : ConnectionIntent.Join;
        }
}
