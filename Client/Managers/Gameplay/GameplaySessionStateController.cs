#nullable enable

using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplaySessionStateController
    {
        private readonly IGameplayContext _context;

        public GameplaySessionStateController(IGameplayContext context)
        {
            _context = context;
        }

        public void EnterGameplaySession(GameplaySessionKind sessionKind, bool openJoinMenus, string? statusMessage)
        {
            if (sessionKind != GameplaySessionKind.LastToDie)
            {
                _context.ResetLastToDieState();
            }

            _context._gameplaySessionKind = sessionKind;
            _context._offlinePracticeSpectatorMode = false;
            _context._practiceSessionElapsedTicks = 0;
            _context._pendingHostedConnectTicks = -1;
            _context._pendingHostedConnectPort = OpenGarrisonPreferencesDocument.DefaultServerPort;
            _context._mainMenuOpen = false;
            _context.CloseMainMenuOverlayState();
            _context.CloseGameplayOverlayState();
            _context._teamSelectOpen = openJoinMenus;
            _context._pendingMapTeamSelection = false;
            _context._menuStatusMessage = statusMessage ?? string.Empty;
            _context.InvalidateDiscordRichPresenceRefresh();

            // Reset animated menu background when entering gameplay
            _context.Menus.AnimatedMenuBackground.Reset();
        }

        public void ResetToMainMenuState(string? statusMessage)
        {
            _context._firstPlayHints?.LeaveSession();
            _context.HideLoadingOverlay();
            _context.SetJoiningServerLoadingLabel(null);
            _context._lastToDieConnectionPresentationPending = false;
            _context._pendingHostedConnectTicks = -1;
            _context._pendingHostedConnectPort = OpenGarrisonPreferencesDocument.DefaultServerPort;
            _context._mainMenuOpen = true;
            _context._pendingMapTeamSelection = false;
            _context._mainMenuPage = MainMenuPage.Root;
            _context._mainMenuHoverIndex = -1;
            _context._mainMenuBottomBarHover = false;
            _context._optionsPageIndex = 0;
            _context.CloseMainMenuOverlayState();
            _context.CloseGameplayOverlayState();
            _context._editingPlayerName = false;
            _context._namePromptPresented = false;
            _context._gameplaySessionKind = GameplaySessionKind.None;
            _context._onlineConnectionIntent = OnlineConnectionIntent.Join;
            _context._offlinePracticeSpectatorMode = false;
            _context._practiceSessionElapsedTicks = 0;
            _context._menuStatusMessage = statusMessage ?? string.Empty;
            _context._autoBalanceNoticeText = string.Empty;
            _context._autoBalanceNoticeTicks = 0;
            _context.InvalidateDiscordRichPresenceRefresh();

            // Initialize animated menu background if enabled
            if (_context._menuBackgroundMode != MenuBackgroundMode.Static)
            {
                _context.Menus.AnimatedMenuBackground.Initialize(_context._menuBackgroundMode);
            }
        }

        public void ResetActiveSessionState()
        {
            _context.HideLoadingOverlay();
            _context.LeaveManagedRoom();
            _context.LeavePeerRoom();
            _context.StopLocalJukebox();
            _context._offlinePracticeNextMap = null;
            _context.ResetPracticeBotManagerState(releaseWorldSlots: true);
            Game1.ResetPracticeNavigationState();
            _context._networkClient.SendLastToDieLeave();
            _context._networkClient.Disconnect();
            _context.StopEmbeddedSession();
            // Returning to the menu also covers hosted Last to Die network
            // errors and the generic in-context Disconnect action. If this client
            // owns a hidden local server, release its UDP port with the rest of
            // the session. Dedicated terminal launches are not tracked here
            // and therefore remain independent.
            _context.StopHostedServer();
            _context.ClearSocialPresenceNetworkEndpoint();
            _context.ClearHostedSocialPresenceEndpoint();
            _context.ResetGameplayTransitionEffects();
            _context.ReinitializeSimulationForTickRate(SimulationConfig.DefaultTicksPerSecond);
            _context.ResetGameplayRuntimeState();
            _context._practiceSessionElapsedTicks = 0;
            _context._offlinePracticeSpectatorMode = false;
            _context.ResetSpectatorTracking(enableTracking: false);
            _context._onlineConnectionIntent = OnlineConnectionIntent.Join;
            _context.ResetLastToDieState();
            _context.ResetJumpState();
            _context.InvalidateDiscordRichPresenceRefresh();
        }
}
