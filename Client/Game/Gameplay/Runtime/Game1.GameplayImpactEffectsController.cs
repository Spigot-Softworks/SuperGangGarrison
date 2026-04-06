#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayImpactEffectsController
    {
        private readonly Game1 _game;

        public GameplayImpactEffectsController(Game1 game)
        {
            _game = game;
        }

        public void ResetTransientEffects()
        {
            _game._explosions.Clear();
            _game._impactVisuals.Clear();
            _game._airBlasts.Clear();
            _game._bubblePops.Clear();
        }

        public bool TryCreateExplosionVisual(WorldSoundEvent soundEvent, out ExplosionVisual? explosion)
        {
            explosion = CreateExplosionVisual(soundEvent.X, soundEvent.Y);
            if (soundEvent.SourceFrame == 0)
            {
                return true;
            }

            var currentFrame = (ulong)Math.Max(0L, _game._world.Frame);
            if (currentFrame <= soundEvent.SourceFrame)
            {
                return true;
            }

            var elapsedSourceTicks = (currentFrame - soundEvent.SourceFrame)
                * (LegacyMovementModel.SourceTicksPerSecond / (float)_game._config.TicksPerSecond);
            if (elapsedSourceTicks >= ExplosionVisual.LifetimeSourceTicks)
            {
                explosion = null;
                return false;
            }

            explosion.ElapsedSourceTicks = Math.Clamp((int)MathF.Floor(elapsedSourceTicks), 0, ExplosionVisual.LifetimeSourceTicks - 1);
            explosion.PendingSourceTicks = Math.Clamp(elapsedSourceTicks - explosion.ElapsedSourceTicks, 0f, 1f);
            return true;
        }

        public void AdvanceExplosionVisuals()
        {
            for (var index = _game._airBlasts.Count - 1; index >= 0; index -= 1)
            {
                _game._airBlasts[index].TicksRemaining -= 1;
                if (_game._airBlasts[index].TicksRemaining <= 0)
                {
                    _game._airBlasts.RemoveAt(index);
                }
            }

            var sourceTickAdvance = _game._clientUpdateElapsedSeconds * LegacyMovementModel.SourceTicksPerSecond;
            if (sourceTickAdvance <= 0f)
            {
                return;
            }

            for (var index = _game._bubblePops.Count - 1; index >= 0; index -= 1)
            {
                var bubblePop = _game._bubblePops[index];
                bubblePop.PendingSourceTicks += sourceTickAdvance;
                while (bubblePop.PendingSourceTicks >= 1f && bubblePop.ElapsedSourceTicks < BubblePopVisual.LifetimeSourceTicks)
                {
                    bubblePop.PendingSourceTicks -= 1f;
                    bubblePop.ElapsedSourceTicks += 1;
                }

                if (bubblePop.ElapsedSourceTicks >= BubblePopVisual.LifetimeSourceTicks)
                {
                    _game._bubblePops.RemoveAt(index);
                }
            }

            for (var index = _game._explosions.Count - 1; index >= 0; index -= 1)
            {
                var explosion = _game._explosions[index];
                explosion.PendingSourceTicks += sourceTickAdvance;
                while (explosion.PendingSourceTicks >= 1f && explosion.ElapsedSourceTicks < ExplosionVisual.LifetimeSourceTicks)
                {
                    explosion.PendingSourceTicks -= 1f;
                    explosion.ElapsedSourceTicks += 1;
                }

                if (explosion.ElapsedSourceTicks >= ExplosionVisual.LifetimeSourceTicks)
                {
                    _game._explosions.RemoveAt(index);
                }
            }
        }

        public void AdvanceImpactVisuals()
        {
            var sourceTickAdvance = _game._clientUpdateElapsedSeconds * LegacyMovementModel.SourceTicksPerSecond;
            if (sourceTickAdvance <= 0f)
            {
                return;
            }

            for (var index = _game._impactVisuals.Count - 1; index >= 0; index -= 1)
            {
                var impact = _game._impactVisuals[index];
                impact.PendingSourceTicks += sourceTickAdvance;
                while (impact.PendingSourceTicks >= 1f && impact.ElapsedSourceTicks < ImpactVisual.LifetimeSourceTicks)
                {
                    impact.PendingSourceTicks -= 1f;
                    impact.ElapsedSourceTicks += 1;
                }

                if (impact.ElapsedSourceTicks >= ImpactVisual.LifetimeSourceTicks)
                {
                    _game._impactVisuals.RemoveAt(index);
                }
            }
        }

        public void DrawExplosionVisuals(Vector2 cameraPosition)
        {
            DrawAirBlastVisuals(cameraPosition);
            DrawBubblePopVisuals(cameraPosition);
            DrawFallbackExplosionVisuals(cameraPosition);
            var largeSprite = _game._runtimeAssets.GetSprite("ExplosionS");
            var smallSprite = _game._runtimeAssets.GetSprite("ExplosionSmallS");
            if ((largeSprite is null || largeSprite.Frames.Count == 0)
                && (smallSprite is null || smallSprite.Frames.Count == 0))
            {
                return;
            }

            foreach (var explosion in _game._explosions)
            {
                DrawExplosionSprite(explosion, cameraPosition, largeSprite, 2.2f * explosion.LargeScaleMultiplier, 0.92f, explosion.LargeSpriteColor, startingFrameBias: 3);
                DrawExplosionSprite(explosion, cameraPosition, smallSprite, 1.45f * explosion.SmallScaleMultiplier, 0.78f, explosion.SmallSpriteColor, startingFrameBias: 2);
            }
        }

        public void DrawImpactVisuals(Vector2 cameraPosition)
        {
            var sprite = _game._runtimeAssets.GetSprite("ImpactS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            for (var index = 0; index < _game._impactVisuals.Count; index += 1)
            {
                var impact = _game._impactVisuals[index];
                var secondStage = impact.ElapsedSourceTicks >= (ImpactVisual.LifetimeSourceTicks / 2);
                var alpha = secondStage ? 0.5f : 1f;
                var scale = secondStage ? 1f : 0.5f;
                _game._spriteBatch.Draw(
                    sprite.Frames[0],
                    new Vector2(impact.X - cameraPosition.X, impact.Y - cameraPosition.Y),
                    null,
                    Color.White * alpha,
                    impact.RotationRadians,
                    sprite.Origin.ToVector2(),
                    new Vector2(scale, scale),
                    SpriteEffects.None,
                    0f);
            }
        }

        public bool TryPlayVisualEvent(string effectName, float x, float y, float directionDegrees, int count)
        {
            if (string.Equals(effectName, "Explosion", StringComparison.OrdinalIgnoreCase))
            {
                _game._explosions.Add(CreateExplosionVisual(x, y));
                return true;
            }

            if (string.Equals(effectName, "HealExplosion", StringComparison.OrdinalIgnoreCase))
            {
                _game._explosions.Add(CreateHealExplosionVisual(x, y));
                return true;
            }

            if (string.Equals(effectName, "Impact", StringComparison.OrdinalIgnoreCase))
            {
                _game._impactVisuals.Add(new ImpactVisual(x, y, directionDegrees * (MathF.PI / 180f)));
                return true;
            }

            if (string.Equals(effectName, "AirBlast", StringComparison.OrdinalIgnoreCase))
            {
                _game._airBlasts.Add(new AirBlastVisual(x, y, directionDegrees * (MathF.PI / 180f)));
                return true;
            }

            if (string.Equals(effectName, "Pop", StringComparison.OrdinalIgnoreCase))
            {
                _game._bubblePops.Add(new BubblePopVisual(x, y));
                return true;
            }

            return false;
        }

        private static ExplosionVisual CreateExplosionVisual(float x, float y, int initialElapsedSourceTicks = 1)
        {
            var explosion = new ExplosionVisual(x, y)
            {
                ElapsedSourceTicks = Math.Clamp(initialElapsedSourceTicks, 0, ExplosionVisual.LifetimeSourceTicks - 1),
            };
            return explosion;
        }

        private static ExplosionVisual CreateHealExplosionVisual(float x, float y)
        {
            var explosion = CreateExplosionVisual(x, y);
            explosion.LargeSpriteColor = new Color(255, 128, 128);
            explosion.SmallSpriteColor = new Color(255, 64, 64);
            explosion.FallbackOuterColor = new Color(230, 36, 36);
            explosion.FallbackInnerColor = new Color(255, 196, 196);
            explosion.LargeScaleMultiplier = 1.2f;
            explosion.SmallScaleMultiplier = 1.15f;
            return explosion;
        }

        private void DrawExplosionSprite(
            ExplosionVisual explosion,
            Vector2 cameraPosition,
            LoadedGameMakerSprite? sprite,
            float scale,
            float alpha,
            Color tint,
            int startingFrameBias)
        {
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            var rawFrameIndex = explosion.ElapsedSourceTicks == 0
                ? Math.Min(startingFrameBias, sprite.Frames.Count - 1)
                : (int)MathF.Floor(explosion.ElapsedSourceTicks * sprite.Frames.Count / (float)ExplosionVisual.LifetimeSourceTicks);
            var frameIndex = Math.Clamp(rawFrameIndex, 0, sprite.Frames.Count - 1);
            _game._spriteBatch.Draw(
                sprite.Frames[frameIndex],
                new Vector2(explosion.X - cameraPosition.X, explosion.Y - cameraPosition.Y),
                null,
                tint * alpha,
                0f,
                sprite.Origin.ToVector2(),
                new Vector2(scale, scale),
                SpriteEffects.None,
                0f);
        }

        private void DrawFallbackExplosionVisuals(Vector2 cameraPosition)
        {
            foreach (var explosion in _game._explosions)
            {
                var progress = explosion.ElapsedSourceTicks / (float)ExplosionVisual.LifetimeSourceTicks;
                var radius = 12f + (progress * 18f);
                var innerRadius = radius * 0.5f;
                var alpha = MathHelper.Clamp(1f - progress, 0f, 1f);
                var outerRectangle = new Rectangle(
                    (int)MathF.Round(explosion.X - cameraPosition.X - radius),
                    (int)MathF.Round(explosion.Y - cameraPosition.Y - radius),
                    (int)MathF.Round(radius * 2f),
                    (int)MathF.Round(radius * 2f));
                var innerRectangle = new Rectangle(
                    (int)MathF.Round(explosion.X - cameraPosition.X - innerRadius),
                    (int)MathF.Round(explosion.Y - cameraPosition.Y - innerRadius),
                    (int)MathF.Round(innerRadius * 2f),
                    (int)MathF.Round(innerRadius * 2f));
                _game._spriteBatch.Draw(_game._pixel, outerRectangle, explosion.FallbackOuterColor * alpha);
                _game._spriteBatch.Draw(_game._pixel, innerRectangle, explosion.FallbackInnerColor * alpha);
            }
        }

        private void DrawBubblePopVisuals(Vector2 cameraPosition)
        {
            var sprite = _game._runtimeAssets.GetSprite("PopS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            foreach (var bubblePop in _game._bubblePops)
            {
                var frameIndex = Math.Clamp(
                    (int)MathF.Floor(bubblePop.ElapsedSourceTicks * sprite.Frames.Count / (float)BubblePopVisual.LifetimeSourceTicks),
                    0,
                    sprite.Frames.Count - 1);
                _game._spriteBatch.Draw(
                    sprite.Frames[frameIndex],
                    new Vector2(bubblePop.X - cameraPosition.X, bubblePop.Y - cameraPosition.Y),
                    null,
                    Color.White,
                    0f,
                    sprite.Origin.ToVector2(),
                    Vector2.One,
                    SpriteEffects.None,
                    0f);
            }
        }

        private void DrawAirBlastVisuals(Vector2 cameraPosition)
        {
            var sprite = _game._runtimeAssets.GetSprite("AirBlastS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            foreach (var airBlast in _game._airBlasts)
            {
                var elapsedTicks = AirBlastVisual.LifetimeTicks - airBlast.TicksRemaining;
                var frameIndex = Math.Clamp((int)MathF.Floor(elapsedTicks * sprite.Frames.Count / (float)AirBlastVisual.LifetimeTicks), 0, sprite.Frames.Count - 1);
                _game._spriteBatch.Draw(
                    sprite.Frames[frameIndex],
                    new Vector2(airBlast.X - cameraPosition.X, airBlast.Y - cameraPosition.Y),
                    null,
                    Color.White,
                    airBlast.RotationRadians,
                    sprite.Origin.ToVector2(),
                    Vector2.One,
                    SpriteEffects.None,
                    0f);
            }
        }
    }
}
