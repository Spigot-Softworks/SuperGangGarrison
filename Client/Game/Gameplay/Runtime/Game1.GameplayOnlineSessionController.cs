#nullable enable

using System.IO;
using OpenGarrison.ClientShared;
using OpenGarrison.Protocol;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class OnlineSessionController
    {
        private readonly Game1 _game;
        private readonly GameplaySessionController _sessionController;

        public OnlineSessionController(Game1 game, GameplaySessionController sessionController)
        {
            _game = game;
            _sessionController = sessionController;
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
            if (!_game._bootstrapController.CanEnterGameplaySession(out var bootstrapReason))
            {
                _game._menuStatusMessage = bootstrapReason ?? "Browser client assets are still loading.";
                return;
            }

            _game.ClearReplayQueue(clearActiveReplayPath: true);
            _game.PrepareHostedServerLaunchUi(closeHostSetup: true, disconnectNetworkClient: true);

            if (!_game.TryStartHostedServerBackground(
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
                _game._menuStatusMessage = error;
                return;
            }

            _game.BeginPendingHostedLocalConnect(port, delayTicks: 20, "Starting local server...");
        }

        public bool TryConnectToServer(string host, int port, bool addConsoleFeedback)
        {
            return TryConnectToServer(NetworkEndpoint.ForCurrentRuntimeSinglePort(host, port), addConsoleFeedback);
        }

        public bool TryConnectToServer(NetworkEndpoint endpoint, bool addConsoleFeedback)
        {
            return TryConnectToServer(endpoint, addConsoleFeedback, OnlineConnectionIntent.Join);
        }

        public bool TryConnectToServer(NetworkEndpoint endpoint, bool addConsoleFeedback, OnlineConnectionIntent intent)
        {
            if (!_game._bootstrapController.CanEnterGameplaySession(out var bootstrapReason))
            {
                _game.SetNetworkStatus(bootstrapReason ?? "Browser client assets are still loading.");
                return false;
            }

            _game.ClearReplayQueue(clearActiveReplayPath: true);
            _game.ClearPendingNetworkMapSync();
            _game._onlineConnectionIntent = intent;
            if (!endpoint.TryResolveForCurrentRuntime(out var host, out var port, out var transport))
            {
                _game._onlineConnectionIntent = OnlineConnectionIntent.Join;
                _game.SetNetworkStatus("Connect failed: endpoint does not support this runtime.");
                if (addConsoleFeedback)
                {
                    _game.AddNetworkConsoleLine("connect failed: endpoint does not support this runtime.");
                }

                return false;
            }

            if (_game._networkClient.Connect(
                    host,
                    port,
                    _game._world.LocalPlayer.DisplayName,
                    _game._world.LocalPlayer.BadgeMask,
                    out var error,
                    _game._clientIdentity.FriendCode,
                    PlayerCardProfile.Serialize(_game._clientIdentity.PlayerCard),
                    ToProtocolConnectionIntent(intent)))
            {
                _game.SetSocialPresenceNetworkEndpoint(endpoint);
                _game.RecordRecentConnection(host, port);
                _game.ClearOnlinePlayerSocialProfiles();
                _game.ResetGameplayRuntimeState();
                // Online sessions must always start from default server-authoritative gameplay rules.
                // This prevents offline experimental settings (for example Last To Die perks)
                // from leaking into client prediction when the world instance is reused.
                _game._world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings());
                _game.CloseLobbyBrowser(clearStatus: false);
                var transportLabel = transport == NetworkEndpointTransport.WebSocket ? "WebSocket" : "UDP";
                var actionLabel = intent == OnlineConnectionIntent.Watch ? "Watching" : "Connecting to";
                _game.ShowLoadingOverlay($"{actionLabel} {host}:{port}...", progress: null);
                _game.SetNetworkStatus($"{actionLabel} {host}:{port} over {transportLabel}...");
                if (addConsoleFeedback)
                {
                    _game.AddNetworkConsoleLine($"{actionLabel.ToLowerInvariant()} {host}:{port} over {transportLabel}");
                }

                return true;
            }

            _game._onlineConnectionIntent = OnlineConnectionIntent.Join;
            _game.SetNetworkStatus($"Connect failed: {error}");
            if (addConsoleFeedback)
            {
                _game.AddNetworkConsoleLine($"connect failed: {error}");
            }

            return false;
        }

        public bool TryPlayLegacyReplay(string replayPath, bool addConsoleFeedback, bool clearQueuedReplays = true)
        {
            if (!_game._bootstrapController.CanEnterGameplaySession(out var bootstrapReason))
            {
                _game.SetNetworkStatus(bootstrapReason ?? "Browser client assets are still loading.");
                return false;
            }

            if (clearQueuedReplays)
            {
                _game.ClearReplayQueue(clearActiveReplayPath: true);
            }

            if (!ReDsmReplayTransport.TryCreate(replayPath, out var transport, out var error) || transport is null)
            {
                _game.SetNetworkStatus($"Replay failed: {error}");
                if (addConsoleFeedback)
                {
                    _game.AddNetworkConsoleLine($"replay failed: {error}");
                }

                return false;
            }

            if (_game._networkClient.Connect(transport, _game._world.LocalPlayer.DisplayName, _game._world.LocalPlayer.BadgeMask, out error))
            {
                _game._onlineConnectionIntent = OnlineConnectionIntent.Join;
                _game._activeReplayPath = replayPath.Trim();
                _game.ClearOnlinePlayerSocialProfiles();
                _game.ResetGameplayRuntimeState();
                _game._world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings());
                _game.CloseLobbyBrowser(clearStatus: false);
                _game.ShowLoadingOverlay($"Loading replay {Path.GetFileName(replayPath)}...", progress: null);
                _game.SetNetworkStatus($"Loading replay {Path.GetFileName(replayPath)}...");
                if (addConsoleFeedback)
                {
                    _game.AddNetworkConsoleLine($"loading replay {Path.GetFileName(replayPath)}");
                }

                return true;
            }

            _game.SetNetworkStatus($"Replay failed: {error}");
            if (addConsoleFeedback)
            {
                _game.AddNetworkConsoleLine($"replay failed: {error}");
            }

            return false;
        }

        public bool TryPlayOpenGarrisonDemo(string demoPath, bool addConsoleFeedback)
        {
            if (!_game._bootstrapController.CanEnterGameplaySession(out var bootstrapReason))
            {
                _game.SetNetworkStatus(bootstrapReason ?? "Browser client assets are still loading.");
                return false;
            }

            _game.ClearReplayQueue(clearActiveReplayPath: true);
            if (!OpenGarrisonDemoTransport.TryCreate(demoPath, out var transport, out var error) || transport is null)
            {
                _game.SetNetworkStatus($"Demo failed: {error}");
                if (addConsoleFeedback)
                {
                    _game.AddNetworkConsoleLine($"demo failed: {error}");
                }

                return false;
            }

            if (_game._networkClient.Connect(transport, _game._world.LocalPlayer.DisplayName, _game._world.LocalPlayer.BadgeMask, out error))
            {
                _game._onlineConnectionIntent = OnlineConnectionIntent.Join;
                _game._activeReplayPath = demoPath.Trim();
                _game.ClearOnlinePlayerSocialProfiles();
                _game.ResetGameplayRuntimeState();
                _game._world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings());
                _game.CloseLobbyBrowser(clearStatus: false);
                _game.ShowLoadingOverlay($"Loading demo {Path.GetFileName(demoPath)}...", progress: null);
                _game.SetNetworkStatus($"Loading demo {Path.GetFileName(demoPath)}...");
                if (addConsoleFeedback)
                {
                    _game.AddNetworkConsoleLine($"loading demo {Path.GetFileName(demoPath)}");
                }

                return true;
            }

            _game.SetNetworkStatus($"Demo failed: {error}");
            if (addConsoleFeedback)
            {
                _game.AddNetworkConsoleLine($"demo failed: {error}");
            }

            return false;
        }

        public void HandleWelcomeMessage(WelcomeMessage welcome)
        {
            if (welcome.Version != ProtocolVersion.Current)
            {
                _game._networkClient.Disconnect();
                _game.SetNetworkStatusAndConsole(
                    "Protocol mismatch.",
                    $"protocol mismatch: server={welcome.Version} client={ProtocolVersion.Current}");
                return;
            }

            var mapSyncStatus = _game.TryEnsureNetworkMapAvailable(
                welcome.LevelName,
                welcome.IsCustomMap,
                welcome.MapDownloadUrl,
                welcome.MapContentHash,
                out var welcomeMapError);
            if (mapSyncStatus == NetworkMapSyncStatus.Pending)
            {
                _game.QueueWelcomeAfterNetworkMapSync(welcome);
                return;
            }

            if (mapSyncStatus == NetworkMapSyncStatus.Failed)
            {
                _game.ReturnToMainMenuWithNetworkStatus(welcomeMapError, $"custom map sync failed: {welcomeMapError}");
                return;
            }

            _game.ReinitializeSimulationForTickRate(welcome.TickRate);
            _game._world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings());
            _game._networkClient.SetLocalPlayerSlot(welcome.PlayerSlot);
            if (_game._onlineConnectionIntent == OnlineConnectionIntent.Watch
                && !_game._networkClient.IsSpectator)
            {
                _game._networkClient.Disconnect();
                _game.ReturnToMainMenuWithNetworkStatus("Watch connection returned a playable slot.");
                return;
            }

            _game._networkClient.SetServerDescription(welcome.ServerName);
            _game._networkClient.SetServerMaxPlayerCount(welcome.MaxPlayerCount);
            _game.ResetSpectatorTracking(enableTracking: _game._networkClient.IsSpectator);
            _game._networkClient.ClearPendingTeamSelection();
            _game._networkClient.ClearPendingClassSelection();
            _game.ResetGameplayRuntimeState();
            _game.ShowLoadingOverlay($"Loading map {welcome.LevelName}...", progress: null);
            if (!_game._world.TryLoadLevel(welcome.LevelName, mapAreaIndex: 1, preservePlayerStats: false, mapScale: welcome.MapScale))
            {
                var loadError = $"Failed to load map: {welcome.LevelName}";
                _game.ReturnToMainMenuWithNetworkStatus(loadError);
                return;
            }

            _game._world.PrepareLocalPlayerJoin();
            _game.ResetGameplayTransitionEffects();
            _sessionController.EnterGameplaySession(
                GameplaySessionKind.Online,
                openJoinMenus: _game._onlineConnectionIntent != OnlineConnectionIntent.Watch && !_game._networkClient.IsSpectator,
                statusMessage: _game._networkClient.IsSpectator ? "Connected as spectator." : string.Empty);
            _game.StopMenuMusic();
            _game.HideLoadingOverlay();
            _game.AddNetworkConsoleLine(
                _game._networkClient.IsSpectator
                    ? $"connected to {welcome.ServerName} ({welcome.LevelName}) as spectator tickrate={welcome.TickRate}"
                    : $"connected to {welcome.ServerName} ({welcome.LevelName}) tickrate={welcome.TickRate}");
            _game.UploadSelectedCustomBubbleState();
        }

        private static ConnectionIntent ToProtocolConnectionIntent(OnlineConnectionIntent intent)
        {
            return intent == OnlineConnectionIntent.Watch
                ? ConnectionIntent.Watch
                : ConnectionIntent.Join;
        }
    }
}
