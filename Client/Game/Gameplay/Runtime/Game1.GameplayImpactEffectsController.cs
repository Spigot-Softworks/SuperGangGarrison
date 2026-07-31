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
            _game._stuckArrowVisuals.Clear();
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

            var sourceTickAdvance = (float)(ClientUpdateStepSeconds * LegacyMovementModel.SourceTicksPerSecond);
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
            var sourceTickAdvance = (float)(ClientUpdateStepSeconds * LegacyMovementModel.SourceTicksPerSecond);
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

        public void AdvanceStuckArrowVisuals()
        {
            if (!_game._stuckArrowsEnabled)
            {
                if (_game._stuckArrowVisuals.Count > 0)
                {
                    _game._stuckArrowVisuals.Clear();
                }

                return;
            }

            for (var index = _game._stuckArrowVisuals.Count - 1; index >= 0; index -= 1)
            {
                var arrow = _game._stuckArrowVisuals[index];
                if (arrow.TicksUntilFade > 0)
                {
                    arrow.TicksUntilFade -= 1;
                    continue;
                }

                arrow.Alpha -= 1f / StuckArrowVisual.FadeTicks;
                if (arrow.Alpha <= 0f)
                {
                    _game._stuckArrowVisuals.RemoveAt(index);
                }
            }
        }

        public void DrawExplosionVisuals(Vector2 cameraPosition)
        {
            DrawAirBlastVisuals(cameraPosition);
            DrawBubblePopVisuals(cameraPosition);
            DrawFallbackExplosionVisuals(cameraPosition);
            var largeSprite = _game.GetResolvedSprite("ExplosionS");
            var smallSprite = _game.GetResolvedSprite("ExplosionSmallS");
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
            var sprite = _game.GetResolvedSprite("ImpactS");
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
                _game.DrawLoadedSpriteFrame(
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

        public void DrawStuckArrowVisuals(Vector2 cameraPosition)
        {
            if (!_game._stuckArrowsEnabled)
            {
                return;
            }

            var sprite = _game.GetResolvedSprite("ArrowS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            for (var index = 0; index < _game._stuckArrowVisuals.Count; index += 1)
            {
                var arrow = _game._stuckArrowVisuals[index];
                var frameIndex = Math.Clamp(arrow.FrameIndex, 0, sprite.Frames.Count - 1);
                var scale = arrow.FlipY ? new Vector2(1f, -1f) : Vector2.One;
                _game.DrawLoadedSpriteFrame(
                    sprite.Frames[frameIndex],
                    new Vector2(arrow.X - cameraPosition.X, arrow.Y - cameraPosition.Y),
                    null,
                    Color.White * arrow.Alpha,
                    arrow.RotationRadians,
                    sprite.Origin.ToVector2(),
                    scale,
                    SpriteEffects.None,
                    0f);

                // Redraw walkmask solids over the tip so it appears embedded in the wall.
                DrawWalkmaskOcclusionOverStuckArrow(arrow, cameraPosition);
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

            if (string.Equals(effectName, "StuckArrow", StringComparison.OrdinalIgnoreCase))
            {
                SpawnStuckArrowVisual(x, y, directionDegrees, count);
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

        private void SpawnStuckArrowVisual(float x, float y, float directionDegrees, int count)
        {
            if (!_game._stuckArrowsEnabled)
            {
                return;
            }

            // Dedup predicted local + networked echo of the same impact.
            for (var index = 0; index < _game._stuckArrowVisuals.Count; index += 1)
            {
                var existing = _game._stuckArrowVisuals[index];
                var deltaX = existing.X - x;
                var deltaY = existing.Y - y;
                if ((deltaX * deltaX) + (deltaY * deltaY) <= StuckArrowVisual.SpawnDedupDistanceSquared)
                {
                    return;
                }
            }

            while (_game._stuckArrowVisuals.Count >= StuckArrowVisual.MaxVisuals)
            {
                _game._stuckArrowVisuals.RemoveAt(0);
            }

            var rotationRadians = directionDegrees * (MathF.PI / 180f);
            var frameIndex = count >= (int)PlayerTeam.Blue ? 1 : 0;
            var flipY = MathF.Cos(rotationRadians) < 0f;
            _game._stuckArrowVisuals.Add(new StuckArrowVisual(x, y, rotationRadians, frameIndex, flipY));
        }

        private void DrawWalkmaskOcclusionOverStuckArrow(
            StuckArrowVisual arrow,
            Vector2 cameraPosition)
        {
            // Only mask near the tip so walkmask redraw doesn't cover the whole shaft
            // (a large AABB around the origin made shallow floor/wall sticks look like they vanished).
            const float tipOffset = 35f;
            const float tipPad = 10f;
            var tipX = arrow.X + (MathF.Cos(arrow.RotationRadians) * tipOffset);
            var tipY = arrow.Y + (MathF.Sin(arrow.RotationRadians) * tipOffset);
            var regionLeft = tipX - tipPad;
            var regionTop = tipY - tipPad;
            var regionRight = tipX + tipPad;
            var regionBottom = tipY + tipPad;

            var hasBackground = _game.TryGetLevelBackgroundTexture(out var background);
            var worldWidth = Math.Max(1f, _game._world.Bounds.Width);
            var worldHeight = Math.Max(1f, _game._world.Bounds.Height);
            var fallbackColor = new Color(46, 70, 56);

            foreach (var solid in _game._world.Level.Solids)
            {
                var left = Math.Max(solid.Left, regionLeft);
                var top = Math.Max(solid.Top, regionTop);
                var right = Math.Min(solid.Right, regionRight);
                var bottom = Math.Min(solid.Bottom, regionBottom);
                if (left >= right || top >= bottom)
                {
                    continue;
                }

                var destX = (int)MathF.Floor(left - cameraPosition.X);
                var destY = (int)MathF.Floor(top - cameraPosition.Y);
                var destWidth = Math.Max(1, (int)MathF.Ceiling(right - left));
                var destHeight = Math.Max(1, (int)MathF.Ceiling(bottom - top));
                var destination = new Rectangle(destX, destY, destWidth, destHeight);

                if (!hasBackground)
                {
                    _game._spriteBatch.Draw(_game._pixel, destination, fallbackColor);
                    continue;
                }

                var sourceX = (int)MathF.Floor(left * background.Width / worldWidth);
                var sourceY = (int)MathF.Floor(top * background.Height / worldHeight);
                var sourceWidth = Math.Max(1, (int)MathF.Ceiling((right - left) * background.Width / worldWidth));
                var sourceHeight = Math.Max(1, (int)MathF.Ceiling((bottom - top) * background.Height / worldHeight));
                sourceX = Math.Clamp(sourceX, 0, Math.Max(0, background.Width - 1));
                sourceY = Math.Clamp(sourceY, 0, Math.Max(0, background.Height - 1));
                sourceWidth = Math.Min(sourceWidth, background.Width - sourceX);
                sourceHeight = Math.Min(sourceHeight, background.Height - sourceY);
                if (sourceWidth <= 0 || sourceHeight <= 0)
                {
                    continue;
                }

                _game._spriteBatch.Draw(
                    background,
                    destination,
                    new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                    Color.White);
            }
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
            _game.DrawLoadedSpriteFrame(
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
            var sprite = _game.GetResolvedSprite("PopS");
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
                _game.DrawLoadedSpriteFrame(
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
            var sprite = _game.GetResolvedSprite("AirBlastS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            foreach (var airBlast in _game._airBlasts)
            {
                var elapsedTicks = AirBlastVisual.LifetimeTicks - airBlast.TicksRemaining;
                var frameIndex = Math.Clamp((int)MathF.Floor(elapsedTicks * sprite.Frames.Count / (float)AirBlastVisual.LifetimeTicks), 0, sprite.Frames.Count - 1);
                _game.DrawLoadedSpriteFrame(
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
