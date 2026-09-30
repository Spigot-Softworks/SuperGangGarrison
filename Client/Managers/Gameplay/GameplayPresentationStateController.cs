#nullable enable

using Microsoft.Xna.Framework;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayPresentationStateController
    {
        private readonly IGameplayContext _context;

        public GameplayPresentationStateController(IGameplayContext context)
        {
            _context = context;
        }

        public void HandleGameplayMapTransitionIfNeeded()
        {
            var currentLevelName = _context._world.Level.Name;
            var currentMapAreaIndex = _context._world.Level.MapAreaIndex;
            if (_context._observedGameplayMapAreaIndex < 0 || string.IsNullOrWhiteSpace(_context._observedGameplayLevelName))
            {
                _context._observedGameplayLevelName = currentLevelName;
                _context._observedGameplayMapAreaIndex = currentMapAreaIndex;
                return;
            }

            if (string.Equals(_context._observedGameplayLevelName, currentLevelName, StringComparison.OrdinalIgnoreCase)
                && _context._observedGameplayMapAreaIndex == currentMapAreaIndex)
            {
                return;
            }

            _context.ResetGameplayTransitionEffects();
            _context._wasDeathCamActive = false;
            _context._wasMatchEnded = false;
            _context._observedGameplayLevelName = currentLevelName;
            _context._observedGameplayMapAreaIndex = currentMapAreaIndex;
        }

        public void UpdateGameplayWindowState()
        {
            var wantsMouseVisible = _context.ShouldShowGameplayMouseCursor();
            _context.IsMouseVisible = wantsMouseVisible && !_context.ShouldUseSoftwareMenuCursor();

            var title = WindowTitle;
            if (!string.Equals(_context._lastGameplayWindowTitle, title, StringComparison.Ordinal))
            {
                _context._lastGameplayWindowTitle = title;
                _context.Window.Title = title;
            }
        }
}
