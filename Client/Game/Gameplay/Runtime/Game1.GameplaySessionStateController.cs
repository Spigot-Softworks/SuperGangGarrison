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
            _game._gameplaySessionKind = sessionKind;
            _game._pendingHostedConnectTicks = -1;
            _game._pendingHostedConnectPort = 8190;
            _game._mainMenuOpen = false;
            _game.CloseMainMenuOverlayState();
            _game.CloseGameplayOverlayState();
            _game._teamSelectOpen = openJoinMenus;
            _game._menuStatusMessage = statusMessage ?? string.Empty;
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
            _game._menuStatusMessage = statusMessage ?? string.Empty;
            _game._autoBalanceNoticeText = string.Empty;
            _game._autoBalanceNoticeTicks = 0;
        }

        public void ResetActiveSessionState()
        {
            _game.ResetPracticeBotManagerState(releaseWorldSlots: true);
            _game.ResetPracticeNavigationState();
            _game._networkClient.Disconnect();
            _game.ResetGameplayTransitionEffects();
            _game.ReinitializeSimulationForTickRate(SimulationConfig.DefaultTicksPerSecond);
            _game.ResetGameplayRuntimeState();
            _game.ResetSpectatorTracking(enableTracking: false);
            _game.ResetLastToDieState();
        }
    }
}
