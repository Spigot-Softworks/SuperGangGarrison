#nullable enable

using Microsoft.Xna.Framework;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayPresentationStateController
    {
        private readonly IGameplayContext _context;
        private string _observedGameplayLevelName = string.Empty;
        private int _observedGameplayMapAreaIndex = -1;
        private string _lastGameplayWindowTitle = string.Empty;

        public GameplayPresentationStateController(IGameplayContext context)
        {
            _context = context;
        }

        internal void ResetObservedGameplayMapIdentity()
        {
            _observedGameplayLevelName = string.Empty;
            _observedGameplayMapAreaIndex = -1;
        }

        public void HandleGameplayMapTransitionIfNeeded()
        {
            var currentLevelName = _context._world.Level.Name;
            var currentMapAreaIndex = _context._world.Level.MapAreaIndex;
            if (_observedGameplayMapAreaIndex < 0 || string.IsNullOrWhiteSpace(_observedGameplayLevelName))
            {
                _observedGameplayLevelName = currentLevelName;
                _observedGameplayMapAreaIndex = currentMapAreaIndex;
                return;
            }

            if (string.Equals(_observedGameplayLevelName, currentLevelName, StringComparison.OrdinalIgnoreCase)
                && _observedGameplayMapAreaIndex == currentMapAreaIndex)
            {
                return;
            }

            _context.ResetGameplayTransitionEffects();
            _context._wasDeathCamActive = false;
            _context._wasMatchEnded = false;
            _observedGameplayLevelName = currentLevelName;
            _observedGameplayMapAreaIndex = currentMapAreaIndex;
        }

        public void UpdateGameplayWindowState()
        {
            var wantsMouseVisible = _context.ShouldShowGameplayMouseCursor();
            _context.IsMouseVisible = wantsMouseVisible && !_context.ShouldUseSoftwareMenuCursor();

            var title = WindowTitle;
            if (!string.Equals(_lastGameplayWindowTitle, title, StringComparison.Ordinal))
            {
                _lastGameplayWindowTitle = title;
                _context.Window.Title = title;
            }
        }
}
