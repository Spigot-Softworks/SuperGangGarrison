#nullable enable

using OpenGarrison.Protocol;

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
            string? requestedMap,
            string? mapRotationFile)
        {
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
            if (_game._networkClient.Connect(host, port, _game._world.LocalPlayer.DisplayName, _game._world.LocalPlayer.BadgeMask, out var error))
            {
                _game.RecordRecentConnection(host, port);
                _game.ResetGameplayRuntimeState();
                _game.CloseLobbyBrowser(clearStatus: false);
                _game.SetNetworkStatus($"Connecting to {host}:{port}...");
                if (addConsoleFeedback)
                {
                    _game.AddNetworkConsoleLine($"connecting to {host}:{port} over udp");
                }

                return true;
            }

            _game.SetNetworkStatus($"Connect failed: {error}");
            if (addConsoleFeedback)
            {
                _game.AddNetworkConsoleLine($"connect failed: {error}");
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

            if (!CustomMapSyncService.EnsureMapAvailable(
                    welcome.LevelName,
                    welcome.IsCustomMap,
                    welcome.MapDownloadUrl,
                    welcome.MapContentHash,
                    out var welcomeMapError))
            {
                _game.ReturnToMainMenuWithNetworkStatus(welcomeMapError, $"custom map sync failed: {welcomeMapError}");
                return;
            }

            _game.ReinitializeSimulationForTickRate(welcome.TickRate);
            _game._networkClient.SetLocalPlayerSlot(welcome.PlayerSlot);
            _game._networkClient.SetServerDescription(welcome.ServerName);
            _game.ResetSpectatorTracking(enableTracking: _game._networkClient.IsSpectator);
            _game._networkClient.ClearPendingTeamSelection();
            _game._networkClient.ClearPendingClassSelection();
            _game.ResetGameplayRuntimeState();
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
                openJoinMenus: !_game._networkClient.IsSpectator,
                statusMessage: _game._networkClient.IsSpectator ? "Connected as spectator." : string.Empty);
            _game.StopMenuMusic();
            _game.AddNetworkConsoleLine(
                _game._networkClient.IsSpectator
                    ? $"connected to {welcome.ServerName} ({welcome.LevelName}) as spectator tickrate={welcome.TickRate}"
                    : $"connected to {welcome.ServerName} ({welcome.LevelName}) tickrate={welcome.TickRate}");
        }
    }
}
