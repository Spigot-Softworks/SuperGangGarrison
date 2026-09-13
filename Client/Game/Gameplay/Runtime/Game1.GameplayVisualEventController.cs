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
            _game.AdvanceRecentPredictedAirBlastVisuals();
            foreach (var visualEvent in _game._world.DrainPendingVisualEvents())
            {
                PlayVisualEvent(visualEvent.EffectName, visualEvent.X, visualEvent.Y, visualEvent.DirectionDegrees, visualEvent.Count);
                _game.RememberPredictedExplosionVisual(visualEvent);
            }

            var retainedCount = 0;
            var renderTime = _game.GetProjectileRenderTimeSeconds();
            for (var index = 0; index < _game._pendingNetworkVisualEvents.Count; index++)
            {
                var visualEvent = _game._pendingNetworkVisualEvents[index];
                if (_game.ShouldSuppressPredictedExplosionVisualEcho(visualEvent))
                {
                    // Still pair the authoritative visual with its separately
                    // replicated sound so that neither channel can replay the
                    // already-predicted explosion later.
                    _ = _game.ShouldPresentAuthoritativeExplosionVisual(visualEvent);
                    continue;
                }

                if (_game.ShouldSuppressPredictedAirBlastVisualEcho(visualEvent))
                {
                    continue;
                }

                if (!NetworkInterpolationPolicy.IsSourceFrameReady(visualEvent.SourceFrame, _game._config.TicksPerSecond, renderTime))
                {
                    _game._pendingNetworkVisualEvents[retainedCount++] = visualEvent;
                    continue;
                }

                if (!_game.ShouldPresentAuthoritativeExplosionVisual(visualEvent))
                {
                    continue;
                }

                PlayVisualEvent(visualEvent.EffectName, visualEvent.X, visualEvent.Y, visualEvent.DirectionDegrees, visualEvent.Count);
            }

            _game._pendingNetworkVisualEvents.RemoveRange(retainedCount, _game._pendingNetworkVisualEvents.Count - retainedCount);
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
                return;
            }

            if (string.Equals(effectName, "CivvieMoney", StringComparison.OrdinalIgnoreCase))
            {
                _game.SpawnCivvieMoneyVisual(x, y, directionDegrees);
            }
        }
    }
}
