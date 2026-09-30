#nullable enable

using System;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayVisualEventController
    {
        private readonly IGameplayContext _context;

        public GameplayVisualEventController(IGameplayContext context)
        {
            _context = context;
        }

        public void PlayPendingVisualEvents()
        {
            _context._presentedExplosionVisualsThisFrame.Clear();
            _context.AdvanceRecentPredictedExplosionVisuals();
            _context.AdvanceRecentPredictedAirBlastVisuals();
            foreach (var visualEvent in _context._world.DrainPendingVisualEvents())
            {
                PlayVisualEvent(visualEvent.EffectName, visualEvent.X, visualEvent.Y, visualEvent.DirectionDegrees, visualEvent.Count);
                _context.RememberPredictedExplosionVisual(visualEvent);
            }

            var retainedCount = 0;
            var renderTime = _context.GetProjectileRenderTimeSeconds();
            for (var index = 0; index < _context._pendingNetworkVisualEvents.Count; index++)
            {
                var visualEvent = _context._pendingNetworkVisualEvents[index];
                if (_context.ShouldSuppressPredictedExplosionVisualEcho(visualEvent))
                {
                    // Still pair the authoritative visual with its separately
                    // replicated sound so that neither channel can replay the
                    // already-predicted explosion later.
                    _ = _context.ShouldPresentAuthoritativeExplosionVisual(visualEvent);
                    continue;
                }

                if (_context.ShouldSuppressPredictedAirBlastVisualEcho(visualEvent))
                {
                    continue;
                }

                if (!NetworkInterpolationPolicy.IsSourceFrameReady(visualEvent.SourceFrame, _context._config.TicksPerSecond, renderTime))
                {
                    _context._pendingNetworkVisualEvents[retainedCount++] = visualEvent;
                    continue;
                }

                if (!_context.ShouldPresentAuthoritativeExplosionVisual(visualEvent))
                {
                    continue;
                }

                PlayVisualEvent(visualEvent.EffectName, visualEvent.X, visualEvent.Y, visualEvent.DirectionDegrees, visualEvent.Count);
            }

            _context._pendingNetworkVisualEvents.RemoveRange(retainedCount, _context._pendingNetworkVisualEvents.Count - retainedCount);
        }

        public void PlayVisualEvent(string effectName, float x, float y, float directionDegrees, int count)
        {
            if (_context.Gameplay.ImpactEffects.TryPlayVisualEvent(effectName, x, y, directionDegrees, count))
            {
                _context.RecordPresentedExplosionVisual(effectName, x, y);
                return;
            }

            if (_context.Gameplay.GoreEffects.TryPlayVisualEvent(effectName, x, y, directionDegrees, count))
            {
                return;
            }

            if (string.Equals(effectName, "WallspinDust", StringComparison.OrdinalIgnoreCase))
            {
                _context.SpawnWallspinDustVisual(x, y);
                return;
            }

            if (string.Equals(effectName, "LooseSheet", StringComparison.OrdinalIgnoreCase))
            {
                _context.SpawnLooseSheetVisual(x, y, directionDegrees);
                return;
            }

            if (string.Equals(effectName, "BottleShards", StringComparison.OrdinalIgnoreCase))
            {
                _context.Gameplay.MaterialEffects.SpawnBottleShardBurst(x, y, count, directionDegrees);
                return;
            }

            if (string.Equals(effectName, "CivvieMoney", StringComparison.OrdinalIgnoreCase))
            {
                _context.SpawnCivvieMoneyVisual(x, y, directionDegrees);
            }
        }
}
