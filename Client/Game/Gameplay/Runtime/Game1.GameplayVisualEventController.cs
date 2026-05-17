#nullable enable

using System;
using System.Collections.Generic;
using OpenGarrison.Core;

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
            foreach (var visualEvent in _game._world.DrainPendingVisualEvents())
            {
                if (ShouldDeferExplosionVisualToPendingSound(visualEvent.EffectName, visualEvent.X, visualEvent.Y))
                {
                    continue;
                }

                PlayVisualEvent(visualEvent.EffectName, visualEvent.X, visualEvent.Y, visualEvent.DirectionDegrees, visualEvent.Count);
            }

            foreach (var visualEvent in _game._pendingNetworkVisualEvents)
            {
                if (ShouldDeferExplosionVisualToPendingSound(visualEvent.EffectName, visualEvent.X, visualEvent.Y))
                {
                    continue;
                }

                PlayVisualEvent(visualEvent.EffectName, visualEvent.X, visualEvent.Y, visualEvent.DirectionDegrees, visualEvent.Count);
            }

            _game._pendingNetworkVisualEvents.Clear();
        }

        private bool ShouldDeferExplosionVisualToPendingSound(string effectName, float x, float y)
        {
            return string.Equals(effectName, "Explosion", StringComparison.OrdinalIgnoreCase)
                && (HasMatchingPendingExplosionSound(_game._world.PendingSoundEvents, x, y)
                    || HasMatchingPendingExplosionSound(_game._pendingNetworkSoundEvents, x, y));
        }

        private static bool HasMatchingPendingExplosionSound(IEnumerable<WorldSoundEvent> soundEvents, float x, float y)
        {
            const float epsilon = 0.01f;
            foreach (var soundEvent in soundEvents)
            {
                if (string.Equals(soundEvent.SoundName, "ExplosionSnd", StringComparison.OrdinalIgnoreCase)
                    && MathF.Abs(soundEvent.X - x) <= epsilon
                    && MathF.Abs(soundEvent.Y - y) <= epsilon)
                {
                    return true;
                }
            }

            return false;
        }

        public void PlayVisualEvent(string effectName, float x, float y, float directionDegrees, int count)
        {
            if (_game._gameplayImpactEffectsController.TryPlayVisualEvent(effectName, x, y, directionDegrees, count))
            {
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
