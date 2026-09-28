#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayImpactEffectsController
    {
        private readonly IGameplayContext _context;

        public GameplayImpactEffectsController(IGameplayContext context)
        {
            _context = context;
        }

        public void ResetTransientEffects()
        {
            _context._explosions.Clear();
            _context._impactVisuals.Clear();
            _context._stuckArrowVisuals.Clear();
            _context._airBlasts.Clear();
            _context._bubblePops.Clear();
        }

        public bool TryCreateExplosionVisual(WorldSoundEvent soundEvent, out ExplosionVisual? explosion)
        {
            explosion = CreateExplosionVisual(soundEvent.X, soundEvent.Y);
            if (soundEvent.SourceFrame == 0)
            {
                return true;
            }

            // Local world events are already emitted on the local simulation
            // timeline. Only network events need to be seeded from the shared
            // presentation clock; using _world.Frame here starts fallback art
            // late by the network interpolation back-time.
            if (soundEvent.EventId == 0)
            {
                return true;
            }

            var sourceTimeSeconds = soundEvent.SourceFrame / (double)Math.Max(1, _context._config.TicksPerSecond);
            var elapsedPresentationSeconds = _context.GetProjectileRenderTimeSeconds() - sourceTimeSeconds;
            if (elapsedPresentationSeconds <= 0d)
            {
                return true;
            }

            var elapsedSourceTicks = (float)(elapsedPresentationSeconds * LegacyMovementModel.SourceTicksPerSecond);
            elapsedSourceTicks *= ExplosionVisual.PlaybackRate;
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
            for (var index = _context._airBlasts.Count - 1; index >= 0; index -= 1)
            {
                _context._airBlasts[index].TicksRemaining -= 1;
                if (_context._airBlasts[index].TicksRemaining <= 0)
                {
                    _context._airBlasts.RemoveAt(index);
                }
            }

            var sourceTickAdvance = (float)(ClientUpdateStepSeconds * LegacyMovementModel.SourceTicksPerSecond);
            if (sourceTickAdvance <= 0f)
            {
                return;
            }

            for (var index = _context._bubblePops.Count - 1; index >= 0; index -= 1)
            {
                var bubblePop = _context._bubblePops[index];
                bubblePop.PendingSourceTicks += sourceTickAdvance;
                while (bubblePop.PendingSourceTicks >= 1f && bubblePop.ElapsedSourceTicks < BubblePopVisual.LifetimeSourceTicks)
                {
                    bubblePop.PendingSourceTicks -= 1f;
                    bubblePop.ElapsedSourceTicks += 1;
                }

                if (bubblePop.ElapsedSourceTicks >= BubblePopVisual.LifetimeSourceTicks)
                {
                    _context._bubblePops.RemoveAt(index);
                }
            }

            var explosionSourceTickAdvance = sourceTickAdvance * ExplosionVisual.PlaybackRate;
            for (var index = _context._explosions.Count - 1; index >= 0; index -= 1)
            {
                var explosion = _context._explosions[index];
                explosion.PendingSourceTicks += explosionSourceTickAdvance;
                while (explosion.PendingSourceTicks >= 1f && explosion.ElapsedSourceTicks < ExplosionVisual.LifetimeSourceTicks)
                {
                    explosion.PendingSourceTicks -= 1f;
                    explosion.ElapsedSourceTicks += 1;
                }

                if (explosion.ElapsedSourceTicks >= ExplosionVisual.LifetimeSourceTicks)
                {
                    _context._explosions.RemoveAt(index);
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

            for (var index = _context._impactVisuals.Count - 1; index >= 0; index -= 1)
            {
                var impact = _context._impactVisuals[index];
                impact.PendingSourceTicks += sourceTickAdvance;
                while (impact.PendingSourceTicks >= 1f && impact.ElapsedSourceTicks < ImpactVisual.LifetimeSourceTicks)
                {
                    impact.PendingSourceTicks -= 1f;
                    impact.ElapsedSourceTicks += 1;
                }

                if (impact.ElapsedSourceTicks >= ImpactVisual.LifetimeSourceTicks)
                {
                    _context._impactVisuals.RemoveAt(index);
                }
            }
        }

        public void AdvanceStuckArrowVisuals()
        {
            if (!_context._stuckArrowsEnabled)
            {
                if (_context._stuckArrowVisuals.Count > 0)
                {
                    _context._stuckArrowVisuals.Clear();
                }

                return;
            }

            for (var index = _context._stuckArrowVisuals.Count - 1; index >= 0; index -= 1)
            {
                var arrow = _context._stuckArrowVisuals[index];
                if (arrow.TicksUntilFade > 0)
                {
                    arrow.TicksUntilFade -= 1;
                    continue;
                }

                arrow.Alpha -= 1f / StuckArrowVisual.FadeTicks;
                if (arrow.Alpha <= 0f)
                {
                    _context._stuckArrowVisuals.RemoveAt(index);
                }
            }
        }

        public void DrawExplosionVisuals(Vector2 cameraPosition)
        {
            DrawAirBlastVisuals(cameraPosition);
            DrawBubblePopVisuals(cameraPosition);
            DrawFallbackExplosionVisuals(cameraPosition);
            var largeSprite = _context.GetResolvedSprite("ExplosionS");
            var smallSprite = _context.GetResolvedSprite("ExplosionSmallS");
            if ((largeSprite is null || largeSprite.Frames.Count == 0)
                && (smallSprite is null || smallSprite.Frames.Count == 0))
            {
                return;
            }

            foreach (var explosion in _context._explosions)
            {
                if (!explosion.SmallOnly)
                {
                    DrawExplosionSprite(explosion, cameraPosition, largeSprite, 2.64f * explosion.LargeScaleMultiplier, 0.92f, explosion.LargeSpriteColor, startingFrameBias: 3);
                }
                DrawExplosionSprite(explosion, cameraPosition, smallSprite, (explosion.SmallOnly ? 1f : 1.74f) * explosion.SmallScaleMultiplier, 0.78f, explosion.SmallSpriteColor, startingFrameBias: 2);
            }
        }

        public void DrawImpactVisuals(Vector2 cameraPosition)
        {
            var sprite = _context.GetResolvedSprite("ImpactS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            for (var index = 0; index < _context._impactVisuals.Count; index += 1)
            {
                var impact = _context._impactVisuals[index];
                var secondStage = impact.ElapsedSourceTicks >= (ImpactVisual.LifetimeSourceTicks / 2);
                var alpha = secondStage ? 0.5f : 1f;
                var scale = secondStage ? 1f : 0.5f;
                _context.DrawLoadedSpriteFrame(
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
            if (!_context._stuckArrowsEnabled)
            {
                return;
            }

            var sprite = _context.GetResolvedSprite("ArrowS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            for (var index = 0; index < _context._stuckArrowVisuals.Count; index += 1)
            {
                var arrow = _context._stuckArrowVisuals[index];
                var frameIndex = Math.Clamp(arrow.FrameIndex, 0, sprite.Frames.Count - 1);
                var scale = arrow.FlipY ? new Vector2(1f, -1f) : Vector2.One;
                _context.DrawLoadedSpriteFrame(
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
                _context._explosions.Add(CreateExplosionVisual(x, y));
                return true;
            }

            if (string.Equals(effectName, "ExplosionSmall", StringComparison.OrdinalIgnoreCase))
            {
                var explosion = CreateExplosionVisual(x, y);
                explosion.SmallOnly = true;
                _context._explosions.Add(explosion);
                return true;
            }

            if (string.Equals(effectName, "HealExplosion", StringComparison.OrdinalIgnoreCase))
            {
                _context._explosions.Add(CreateHealExplosionVisual(x, y));
                return true;
            }

            if (string.Equals(effectName, "Impact", StringComparison.OrdinalIgnoreCase))
            {
                _context._impactVisuals.Add(new ImpactVisual(x, y, directionDegrees * (MathF.PI / 180f)));
                return true;
            }

            if (string.Equals(effectName, "StuckArrow", StringComparison.OrdinalIgnoreCase))
            {
                SpawnStuckArrowVisual(x, y, directionDegrees, count);
                return true;
            }

            if (string.Equals(effectName, "AirBlast", StringComparison.OrdinalIgnoreCase))
            {
                _context._airBlasts.Add(new AirBlastVisual(x, y, directionDegrees * (MathF.PI / 180f)));
                return true;
            }

            if (string.Equals(effectName, "Pop", StringComparison.OrdinalIgnoreCase))
            {
                _context._bubblePops.Add(new BubblePopVisual(x, y));
                return true;
            }

            return false;
        }

        private void SpawnStuckArrowVisual(float x, float y, float directionDegrees, int count)
        {
            if (!_context._stuckArrowsEnabled)
            {
                return;
            }

            // Dedup predicted local + networked echo of the same impact.
            for (var index = 0; index < _context._stuckArrowVisuals.Count; index += 1)
            {
                var existing = _context._stuckArrowVisuals[index];
                var deltaX = existing.X - x;
                var deltaY = existing.Y - y;
                if ((deltaX * deltaX) + (deltaY * deltaY) <= StuckArrowVisual.SpawnDedupDistanceSquared)
                {
                    return;
                }
            }

            while (_context._stuckArrowVisuals.Count >= StuckArrowVisual.MaxVisuals)
            {
                _context._stuckArrowVisuals.RemoveAt(0);
            }

            var rotationRadians = directionDegrees * (MathF.PI / 180f);
            var frameIndex = count >= (int)PlayerTeam.Blue ? 1 : 0;
            var flipY = MathF.Cos(rotationRadians) < 0f;
            _context._stuckArrowVisuals.Add(new StuckArrowVisual(x, y, rotationRadians, frameIndex, flipY));
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

            var hasBackground = _context.TryGetLevelBackgroundTexture(out var background);
            var worldWidth = Math.Max(1f, _context._world.Bounds.Width);
            var worldHeight = Math.Max(1f, _context._world.Bounds.Height);
            var fallbackColor = new Color(46, 70, 56);

            foreach (var solid in _context._world.Level.Solids)
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
                    _context._spriteBatch.Draw(_context._pixel, destination, fallbackColor);
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

                _context._spriteBatch.Draw(
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

            var progress = GetExplosionProgress(explosion);
            var rawFrameIndex = explosion.ElapsedSourceTicks == 0
                ? Math.Min(startingFrameBias, sprite.Frames.Count - 1)
                : (int)MathF.Floor(progress * sprite.Frames.Count);
            var frameIndex = Math.Clamp(rawFrameIndex, 0, sprite.Frames.Count - 1);
            _context.DrawLoadedSpriteFrame(
                sprite.Frames[frameIndex],
                new Vector2(explosion.X - cameraPosition.X, explosion.Y - cameraPosition.Y),
                null,
                tint * (alpha * MathHelper.Clamp(1f - progress, 0f, 1f)),
                0f,
                sprite.Origin.ToVector2(),
                new Vector2(scale, scale),
                SpriteEffects.None,
                0f);
        }

        private void DrawFallbackExplosionVisuals(Vector2 cameraPosition)
        {
            foreach (var explosion in _context._explosions)
            {
                var progress = GetExplosionProgress(explosion);
                var radius = (12f + (progress * 18f)) * (explosion.SmallOnly ? 0.6f : 1.2f);
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
                _context._spriteBatch.Draw(_context._pixel, outerRectangle, explosion.FallbackOuterColor * alpha);
                _context._spriteBatch.Draw(_context._pixel, innerRectangle, explosion.FallbackInnerColor * alpha);
            }
        }

        private static float GetExplosionProgress(ExplosionVisual explosion)
        {
            var elapsedSourceTicks = explosion.ElapsedSourceTicks + explosion.PendingSourceTicks;
            return MathHelper.Clamp(elapsedSourceTicks / ExplosionVisual.LifetimeSourceTicks, 0f, 1f);
        }

        private void DrawBubblePopVisuals(Vector2 cameraPosition)
        {
            var sprite = _context.GetResolvedSprite("PopS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            foreach (var bubblePop in _context._bubblePops)
            {
                var frameIndex = Math.Clamp(
                    (int)MathF.Floor(bubblePop.ElapsedSourceTicks * sprite.Frames.Count / (float)BubblePopVisual.LifetimeSourceTicks),
                    0,
                    sprite.Frames.Count - 1);
                _context.DrawLoadedSpriteFrame(
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
            var sprite = _context.GetResolvedSprite("AirBlastS");

            foreach (var airBlast in _context._airBlasts)
            {
                var elapsedTicks = AirBlastVisual.LifetimeTicks - airBlast.TicksRemaining;
                var progress = MathHelper.Clamp(elapsedTicks / (float)AirBlastVisual.LifetimeTicks, 0f, 1f);
                var alpha = MathHelper.Clamp(1f - progress, 0f, 1f);
                var renderPosition = new Vector2(airBlast.X - cameraPosition.X, airBlast.Y - cameraPosition.Y);
                if (sprite is not null && sprite.Frames.Count > 0)
                {
                    var frameIndex = Math.Clamp((int)MathF.Floor(progress * sprite.Frames.Count), 0, sprite.Frames.Count - 1);
                    _context.DrawLoadedSpriteFrame(
                        sprite.Frames[frameIndex],
                        renderPosition,
                        null,
                        Color.White * alpha,
                        airBlast.RotationRadians,
                        sprite.Origin.ToVector2(),
                        Vector2.One,
                        SpriteEffects.None,
                        0f);
                }
                else
                {
                    var radius = 10f + progress * 26f;
                    var outer = new Rectangle(
                        (int)MathF.Round(renderPosition.X - radius),
                        (int)MathF.Round(renderPosition.Y - radius * 0.4f),
                        (int)MathF.Round(radius * 2f),
                        (int)MathF.Round(radius * 0.8f));
                    _context._spriteBatch.Draw(_context._pixel, outer, new Color(210, 240, 255) * alpha);
                }
            }
        }
}
