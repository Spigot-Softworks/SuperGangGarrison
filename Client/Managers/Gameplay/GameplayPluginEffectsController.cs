#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

// Plays the visual effects gameplay plugins send (GameplayVisualEffects): pack sprites played
// once at a world position, and screen effects drawn over the world view.
public sealed class GameplayPluginEffectsController
    {
        private const int MaxSpriteEffects = 64;
        private const int CircleTextureSize = 1024;

        // The world radius time stops in (the DIO plugin's time_stop.radius).
        private const float TimeStopRadius = 900f;

        // result = src * (1 - dest) + dest * (1 - src): white inverts what is under it and
        // transparent leaves it alone, so masks and fades work with premultiplied sprites.
        private static readonly BlendState InvertBlend = new()
        {
            ColorSourceBlend = Blend.InverseDestinationColor,
            ColorDestinationBlend = Blend.InverseSourceColor,
            ColorBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One,
        };

        private readonly IGameplayContext _context;
        private readonly List<SpriteEffectVisual> _spriteEffects = new();
        private readonly List<ScreenEffectVisual> _screenEffects = new();
        private Texture2D? _circleTexture;

        public GameplayPluginEffectsController(IGameplayContext context)
        {
            _context = context;
        }

        // True while a time stop shows: the client's own effects hold still with it.
        public bool IsTimeStopped
        {
            get
            {
                foreach (var effect in _screenEffects)
                {
                    if (string.Equals(effect.Effect, GameplayVisualEffects.TimeStop, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void ResetTransientEffects()
        {
            _spriteEffects.Clear();
            _screenEffects.Clear();
        }

        public bool TryPlayVisualEvent(string effectName, float x, float y, float directionDegrees, int count)
        {
            if (effectName.StartsWith(GameplayVisualEffects.SpritePrefix, StringComparison.Ordinal))
            {
                SpawnSpriteEffect(effectName[GameplayVisualEffects.SpritePrefix.Length..], x, y, directionDegrees, count);
                return true;
            }

            if (effectName.StartsWith(GameplayVisualEffects.ScreenPrefix, StringComparison.Ordinal))
            {
                var effect = effectName[GameplayVisualEffects.ScreenPrefix.Length..];
                if (GameplayVisualEffects.IsKnownScreenEffect(effect) && count > 0)
                {
                    _screenEffects.Add(new ScreenEffectVisual(effect, x, y, count));
                }

                return true;
            }

            return false;
        }

        private void SpawnSpriteEffect(string spriteName, float x, float y, float rotationDegrees, int count)
        {
            // Art that hasn't downloaded yet just skips the effect.
            var sprite = _context.GetResolvedSprite(spriteName);
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            while (_spriteEffects.Count >= MaxSpriteEffects)
            {
                _spriteEffects.RemoveAt(0);
            }

            var (ticksPerFrame, flipX) = GameplayVisualEffects.DecodeSpriteCount(count);
            _spriteEffects.Add(new SpriteEffectVisual(
                spriteName,
                x,
                y,
                rotationDegrees * (MathF.PI / 180f),
                flipX,
                ticksPerFrame,
                sprite.Frames.Count * ticksPerFrame));
        }

        public void AdvanceEffects()
        {
            var sourceTickAdvance = (float)(ClientUpdateStepSeconds * LegacyMovementModel.SourceTicksPerSecond);
            if (sourceTickAdvance <= 0f)
            {
                return;
            }

            for (var index = _spriteEffects.Count - 1; index >= 0; index -= 1)
            {
                var effect = _spriteEffects[index];
                effect.ElapsedSourceTicks += sourceTickAdvance;
                if (effect.ElapsedSourceTicks >= effect.LifetimeSourceTicks)
                {
                    _spriteEffects.RemoveAt(index);
                }
            }

            for (var index = _screenEffects.Count - 1; index >= 0; index -= 1)
            {
                var effect = _screenEffects[index];
                effect.ElapsedSourceTicks += sourceTickAdvance;
                if (effect.ElapsedSourceTicks >= effect.DurationSourceTicks)
                {
                    _screenEffects.RemoveAt(index);
                }
            }
        }

        public void DrawSpriteEffects(Vector2 cameraPosition)
        {
            for (var index = 0; index < _spriteEffects.Count; index += 1)
            {
                var effect = _spriteEffects[index];
                var sprite = _context.GetResolvedSprite(effect.SpriteName);
                if (sprite is null || sprite.Frames.Count == 0)
                {
                    continue;
                }

                var frameIndex = Math.Clamp((int)(effect.ElapsedSourceTicks / effect.TicksPerFrame), 0, sprite.Frames.Count - 1);
                _context.DrawLoadedSpriteFrame(
                    sprite.Frames[frameIndex],
                    new Vector2(effect.X - cameraPosition.X, effect.Y - cameraPosition.Y),
                    null,
                    Color.White,
                    effect.RotationRadians,
                    sprite.Origin.ToVector2(),
                    new Vector2(effect.FlipX ? -1f : 1f, 1f),
                    SpriteEffects.None,
                    0f);
            }
        }

        // Drawn at the end of the world pass, in world space, so it covers the world but not the HUD.
        public void DrawScreenEffects(Vector2 cameraPosition, int viewportWidth, int viewportHeight)
        {
            if (_screenEffects.Count == 0)
            {
                return;
            }

            const int padding = 64;
            var view = new Rectangle(-padding, -padding, viewportWidth + (padding * 2), viewportHeight + (padding * 2));
            for (var index = 0; index < _screenEffects.Count; index += 1)
            {
                var effect = _screenEffects[index];
                if (string.Equals(effect.Effect, GameplayVisualEffects.TimeStop, StringComparison.Ordinal))
                {
                    DrawTimeStop(effect, cameraPosition, view);
                }
            }

            _context.RestartGameplayWorldSpriteBatch(BlendState.AlphaBlend);
        }

        // HFTF's time stop: an inverted sphere bursts out from DIO to the edge of the area time
        // stops in, holds there while time is stopped, and a negative flash marks time moving again.
        private void DrawTimeStop(ScreenEffectVisual effect, Vector2 cameraPosition, Rectangle view)
        {
            var elapsed = effect.ElapsedSourceTicks;
            var duration = effect.DurationSourceTicks;
            var burstTicks = Math.Min(14f, duration / 3f);
            const float resumeFlashTicks = 4f;

            if (elapsed >= duration - resumeFlashTicks)
            {
                _context.RestartGameplayWorldSpriteBatch(InvertBlend);
                _context._spriteBatch.Draw(_context._pixel, view, Color.White);
                return;
            }

            var progress = Math.Clamp(elapsed / burstTicks, 0f, 1f);
            var radius = TimeStopRadius * progress * progress;
            var center = new Vector2(effect.X - cameraPosition.X, effect.Y - cameraPosition.Y);
            _context.RestartGameplayWorldSpriteBatch(InvertBlend);
            _context._spriteBatch.Draw(
                GetCircleTexture(),
                new Rectangle((int)(center.X - radius), (int)(center.Y - radius), (int)(radius * 2f), (int)(radius * 2f)),
                Color.White);
        }

        private Texture2D GetCircleTexture()
        {
            if (_circleTexture is not null)
            {
                return _circleTexture;
            }

            var pixels = new Color[CircleTextureSize * CircleTextureSize];
            var center = (CircleTextureSize - 1) / 2f;
            var radius = CircleTextureSize / 2f;
            for (var y = 0; y < CircleTextureSize; y += 1)
            {
                for (var x = 0; x < CircleTextureSize; x += 1)
                {
                    var dx = x - center;
                    var dy = y - center;
                    // A one-pixel soft edge, so the held circle's rim doesn't stair-step.
                    var coverage = Math.Clamp(radius - MathF.Sqrt((dx * dx) + (dy * dy)), 0f, 1f);
                    pixels[(y * CircleTextureSize) + x] = Color.White * coverage;
                }
            }

            _circleTexture = new Texture2D(_context.GraphicsDevice, CircleTextureSize, CircleTextureSize);
            _circleTexture.SetData(pixels);
            return _circleTexture;
        }

        private sealed class SpriteEffectVisual(
            string spriteName,
            float x,
            float y,
            float rotationRadians,
            bool flipX,
            int ticksPerFrame,
            int lifetimeSourceTicks)
        {
            public string SpriteName { get; } = spriteName;

            public float X { get; } = x;

            public float Y { get; } = y;

            public float RotationRadians { get; } = rotationRadians;

            public bool FlipX { get; } = flipX;

            public int TicksPerFrame { get; } = ticksPerFrame;

            public int LifetimeSourceTicks { get; } = lifetimeSourceTicks;

            public float ElapsedSourceTicks { get; set; }
        }

        private sealed class ScreenEffectVisual(string effect, float x, float y, int durationSourceTicks)
        {
            public string Effect { get; } = effect;

            public float X { get; } = x;

            public float Y { get; } = y;

            public int DurationSourceTicks { get; } = durationSourceTicks;

            public float ElapsedSourceTicks { get; set; }
        }
}
