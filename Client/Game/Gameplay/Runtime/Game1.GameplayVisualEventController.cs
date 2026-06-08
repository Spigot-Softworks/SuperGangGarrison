#nullable enable

using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayVisualEventController
    {
        private readonly Game1 _game;

        public GameplayVisualEventController(Game1 game)
        {
            _game = game;
        }

        public void PlayPendingVisualEvents()
        {
            _game._presentedExplosionVisualsThisFrame.Clear();
            _game.AdvanceRecentPredictedExplosionVisuals();
            foreach (var visualEvent in _game._world.DrainPendingVisualEvents())
            {
                PlayVisualEvent(visualEvent.EffectName, visualEvent.X, visualEvent.Y, visualEvent.DirectionDegrees, visualEvent.Count);
                _game.RememberPredictedExplosionVisual(visualEvent);
            }

            foreach (var visualEvent in _game._pendingNetworkVisualEvents)
            {
                if (_game.ShouldSuppressPredictedExplosionVisualEcho(visualEvent))
                {
                    continue;
                }

                PlayVisualEvent(visualEvent.EffectName, visualEvent.X, visualEvent.Y, visualEvent.DirectionDegrees, visualEvent.Count);
            }

            _game._pendingNetworkVisualEvents.Clear();
        }

        public void PlayVisualEvent(string effectName, float x, float y, float directionDegrees, int count)
        {
            if (_game._gameplayImpactEffectsController.TryPlayVisualEvent(effectName, x, y, directionDegrees, count))
            {
                _game.RecordPresentedExplosionVisual(effectName, x, y);
                return;
            }

            if (_game._gameplayGoreEffectsController.TryPlayVisualEvent(effectName, x, y, directionDegrees, count))
            {
                return;
            }

            if (string.Equals(effectName, "WallspinDust", StringComparison.OrdinalIgnoreCase))
            {
                _game.SpawnWallspinDustVisual(x, y);
                return;
            }

            if (string.Equals(effectName, "LooseSheet", StringComparison.OrdinalIgnoreCase))
            {
                _game.SpawnLooseSheetVisual(x, y, directionDegrees);
            }
        }
    }
}
