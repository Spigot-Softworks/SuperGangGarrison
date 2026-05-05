#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayPresentationStateController
    {
        private readonly Game1 _game;

        public GameplayPresentationStateController(Game1 game)
        {
            _game = game;
        }

        public void HandleGameplayMapTransitionIfNeeded()
        {
            var currentLevelName = _game._world.Level.Name;
            var currentMapAreaIndex = _game._world.Level.MapAreaIndex;
            if (_game._observedGameplayMapAreaIndex < 0 || string.IsNullOrWhiteSpace(_game._observedGameplayLevelName))
            {
                _game._observedGameplayLevelName = currentLevelName;
                _game._observedGameplayMapAreaIndex = currentMapAreaIndex;
                return;
            }

            if (string.Equals(_game._observedGameplayLevelName, currentLevelName, StringComparison.OrdinalIgnoreCase)
                && _game._observedGameplayMapAreaIndex == currentMapAreaIndex)
            {
                return;
            }

            _game.ResetGameplayTransitionEffects();
            _game._wasDeathCamActive = false;
            _game._wasMatchEnded = false;
            if (_game._navEditorEnabled)
            {
                _game.DisableNavEditor("nav editor closed after map change");
            }

            _game._observedGameplayLevelName = currentLevelName;
            _game._observedGameplayMapAreaIndex = currentMapAreaIndex;
        }

        public void UpdateGameplayWindowState()
        {
            var wantsMouseVisible = _game.ShouldShowGameplayMouseCursor();
            _game.IsMouseVisible = wantsMouseVisible && !_game.ShouldUseSoftwareMenuCursor();

            var sessionTag = _game._networkClient.IsConnected
                ? _game._networkClient.IsSpectator ? "Spectating" : "Online"
                : _game.IsLastToDieSessionActive ? "Last to Die"
                : _game.IsPracticeSessionActive ? "Practice"
                : "Offline";
            var title = $"OG2 - {sessionTag} - {_game._world.Level.Name}";
            if (!string.Equals(_game._lastGameplayWindowTitle, title, StringComparison.Ordinal))
            {
                _game._lastGameplayWindowTitle = title;
                _game.Window.Title = title;
            }
        }
    }
}
