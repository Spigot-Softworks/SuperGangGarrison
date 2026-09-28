#nullable enable

using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplaySessionController
    {
        private readonly IGameplayContext _context;

        public GameplaySessionController(IGameplayContext context)
        {
            _context = context;
        }

        public void EnterGameplaySession(GameplaySessionKind sessionKind, bool openJoinMenus, string? statusMessage = null)
        {
            _context.Gameplay.SessionState.EnterGameplaySession(sessionKind, openJoinMenus, statusMessage);
        }

        public void ResetToMainMenuState(string? statusMessage)
        {
            _context.Gameplay.SessionState.ResetToMainMenuState(statusMessage);
        }

        public void ReturnToMainMenu(string? statusMessage = null)
        {
            ResetActiveSessionState();
            ResetToMainMenuState(statusMessage);
        }

        public void ResetActiveSessionState()
        {
            _context.Gameplay.SessionState.ResetActiveSessionState();
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
            _context.Gameplay.OnlineSession.BeginHostedGame(
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

        public bool TryConnectToServer(string host, int port, bool addConsoleFeedback)
        {
            return _context.Gameplay.OnlineSession.TryConnectToServer(host, port, addConsoleFeedback);
        }

        public bool TryConnectLegacyGg2Server(string host, int port, bool addConsoleFeedback)
        {
            return _context.Gameplay.OnlineSession.TryConnectLegacyGg2Server(host, port, addConsoleFeedback);
        }

        public bool TryConnectToServer(NetworkEndpoint endpoint, bool addConsoleFeedback)
        {
            return _context.Gameplay.OnlineSession.TryConnectToServer(endpoint, addConsoleFeedback);
        }

        public bool TryConnectToServer(NetworkEndpoint endpoint, bool addConsoleFeedback, OnlineConnectionIntent intent)
        {
            return _context.Gameplay.OnlineSession.TryConnectToServer(endpoint, addConsoleFeedback, intent);
        }

        public bool TryAdvancePendingConnectionCandidate(string disconnectReason)
        {
            return _context.Gameplay.OnlineSession.TryAdvancePendingConnectionCandidate(disconnectReason);
        }

        public void ClearPendingConnectionCandidates()
        {
            _context.Gameplay.OnlineSession.ClearPendingConnectionCandidates();
        }

        public bool TryPlayLegacyReplay(string replayPath, bool addConsoleFeedback, bool clearQueuedReplays = true)
        {
            return _context.Gameplay.OnlineSession.TryPlayLegacyReplay(replayPath, addConsoleFeedback, clearQueuedReplays);
        }

        public bool TryPlayOpenGarrisonDemo(string demoPath, bool addConsoleFeedback)
        {
            return _context.Gameplay.OnlineSession.TryPlayOpenGarrisonDemo(demoPath, addConsoleFeedback);
        }

        public bool TrySeekOpenGarrisonDemo(int deltaMilliseconds, out int targetMilliseconds, out string error)
        {
            return _context.Gameplay.OnlineSession.TrySeekOpenGarrisonDemo(deltaMilliseconds, out targetMilliseconds, out error);
        }

        public void HandleWelcomeMessage(OpenGarrison.Protocol.WelcomeMessage welcome)
        {
            _context.Gameplay.OnlineSession.HandleWelcomeMessage(welcome);
        }

        public void TryStartPracticeFromSetup()
        {
            _context.Gameplay.OfflineSession.TryStartPracticeFromSetup();
        }

        public void RestartPracticeSession()
        {
            _context.Gameplay.OfflineSession.RestartPracticeSession();
        }

        public void BeginPracticeSession(string levelName)
        {
            _context.Gameplay.OfflineSession.BeginPracticeSession(levelName);
        }

        public bool BeginLastToDieStage(string levelName)
        {
            return _context.Gameplay.OfflineSession.BeginLastToDieStage(levelName);
        }

        public bool TryBeginOfflineBotSession(
            string levelName,
            GameplaySessionKind sessionKind,
            int tickRate,
            ExperimentalGameplaySettings experimentalSettings,
            int timeLimitMinutes,
            int capLimit,
            int respawnSeconds,
            bool enableInstantRedTeamIntelCaptureWin,
            bool openJoinMenus,
            string consoleSessionName)
        {
            return _context.Gameplay.OfflineSession.TryBeginOfflineBotSession(
                levelName,
                sessionKind,
                tickRate,
                experimentalSettings,
                timeLimitMinutes,
                capLimit,
                respawnSeconds,
                enableInstantRedTeamIntelCaptureWin,
                openJoinMenus,
                consoleSessionName);
        }
}
