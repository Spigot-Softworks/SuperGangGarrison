#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplaySessionStateController
    {
        private readonly Game1 _game;

        public GameplaySessionStateController(Game1 game)
        {
            _game = game;
        }

        public void EnterGameplaySession(GameplaySessionKind sessionKind, bool openJoinMenus, string? statusMessage)
        {
            if (sessionKind != GameplaySessionKind.LastToDie)
            {
                _game.ResetLastToDieState();
            }

            _game._gameplaySessionKind = sessionKind;
            _game._practiceSessionElapsedTicks = 0;
            _game._pendingHostedConnectTicks = -1;
            _game._pendingHostedConnectPort = 8190;
            _game._mainMenuOpen = false;
            _game.CloseMainMenuOverlayState();
            _game.CloseGameplayOverlayState();
            _game._teamSelectOpen = openJoinMenus;
            _game._menuStatusMessage = statusMessage ?? string.Empty;
            _game.InvalidateDiscordRichPresenceRefresh();

            // Reset animated menu background when entering gameplay
            _game._animatedMenuBackgroundController.Reset();
        }

        public void ResetToMainMenuState(string? statusMessage)
        {
            _game._pendingHostedConnectTicks = -1;
            _game._pendingHostedConnectPort = 8190;
            _game._mainMenuOpen = true;
            _game._mainMenuPage = MainMenuPage.Root;
            _game._mainMenuHoverIndex = -1;
            _game._mainMenuBottomBarHover = false;
            _game._optionsPageIndex = 0;
            _game.CloseMainMenuOverlayState();
            _game.CloseGameplayOverlayState();
            _game._editingPlayerName = false;
            _game._gameplaySessionKind = GameplaySessionKind.None;
            _game._onlineConnectionIntent = OnlineConnectionIntent.Join;
            _game._practiceSessionElapsedTicks = 0;
            _game._menuStatusMessage = statusMessage ?? string.Empty;
            _game._autoBalanceNoticeText = string.Empty;
            _game._autoBalanceNoticeTicks = 0;
            _game.InvalidateDiscordRichPresenceRefresh();

            // Initialize animated menu background if enabled
            if (_game._menuBackgroundMode != MenuBackgroundMode.Static)
            {
                _game._animatedMenuBackgroundController.Initialize(_game._menuBackgroundMode);
            }
        }

        public void ResetActiveSessionState()
        {
            _game.ResetPracticeBotManagerState(releaseWorldSlots: true);
            Game1.ResetPracticeNavigationState();
            _game._networkClient.Disconnect();
            _game.ClearSocialPresenceNetworkEndpoint();
            _game.ResetGameplayTransitionEffects();
            _game.ReinitializeSimulationForTickRate(SimulationConfig.DefaultTicksPerSecond);
            _game.ResetGameplayRuntimeState();
            _game._practiceSessionElapsedTicks = 0;
            _game.ResetSpectatorTracking(enableTracking: false);
            _game._onlineConnectionIntent = OnlineConnectionIntent.Join;
            _game.ResetLastToDieState();
            _game.ResetJumpState();
            _game.InvalidateDiscordRichPresenceRefresh();
        }
    }
}
