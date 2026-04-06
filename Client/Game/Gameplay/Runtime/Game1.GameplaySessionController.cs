#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplaySessionController
    {
        private readonly GameplaySessionStateController _stateController;
        private readonly OnlineSessionController _onlineSessionController;
        private readonly OfflineSessionController _offlineSessionController;

        public GameplaySessionController(Game1 game)
        {
            _stateController = new GameplaySessionStateController(game);
            _onlineSessionController = new OnlineSessionController(game, this);
            _offlineSessionController = new OfflineSessionController(game, this);
        }

        public void EnterGameplaySession(GameplaySessionKind sessionKind, bool openJoinMenus, string? statusMessage = null)
        {
            _stateController.EnterGameplaySession(sessionKind, openJoinMenus, statusMessage);
        }

        public void ResetToMainMenuState(string? statusMessage)
        {
            _stateController.ResetToMainMenuState(statusMessage);
        }

        public void ReturnToMainMenu(string? statusMessage = null)
        {
            ResetActiveSessionState();
            ResetToMainMenuState(statusMessage);
        }

        public void ResetActiveSessionState()
        {
            _stateController.ResetActiveSessionState();
        }

        public void BeginHostedGame(
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
            _onlineSessionController.BeginHostedGame(
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

        public bool TryConnectToServer(string host, int port, bool addConsoleFeedback)
        {
            return _onlineSessionController.TryConnectToServer(host, port, addConsoleFeedback);
        }

        public void HandleWelcomeMessage(OpenGarrison.Protocol.WelcomeMessage welcome)
        {
            _onlineSessionController.HandleWelcomeMessage(welcome);
        }

        public void TryStartPracticeFromSetup()
        {
            _offlineSessionController.TryStartPracticeFromSetup();
        }

        public void RestartPracticeSession()
        {
            _offlineSessionController.RestartPracticeSession();
        }

        public void BeginPracticeSession(string levelName)
        {
            _offlineSessionController.BeginPracticeSession(levelName);
        }

        public bool BeginLastToDieStage(string levelName)
        {
            return _offlineSessionController.BeginLastToDieStage(levelName);
        }

        public bool TryBeginOfflineBotSession(
            string levelName,
            GameplaySessionKind sessionKind,
            int tickRate,
            ExperimentalGameplaySettings experimentalSettings,
            int timeLimitMinutes,
            int capLimit,
            int respawnSeconds,
            bool openJoinMenus,
            string consoleSessionName)
        {
            return _offlineSessionController.TryBeginOfflineBotSession(
                levelName,
                sessionKind,
                tickRate,
                experimentalSettings,
                timeLimitMinutes,
                capLimit,
                respawnSeconds,
                openJoinMenus,
                consoleSessionName);
        }
    }
}
