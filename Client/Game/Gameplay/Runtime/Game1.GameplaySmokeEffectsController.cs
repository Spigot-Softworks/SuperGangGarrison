#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplaySmokeEffectsController
    {
        private readonly Game1 _game;

        public GameplaySmokeEffectsController(Game1 game)
        {
            _game = game;
        }

        public void AdvanceRocketSmokeVisuals()
        {
            if (_game._particleMode == 1)
            {
                _game._rocketSmokeVisuals.Clear();
                return;
            }

            foreach (var rocket in _game._world.Rockets)
            {
                if (_game._particleMode == 2 && ((_game._world.Frame + rocket.Id) & 1) != 0)
                {
                    continue;
                }

                var velocityX = rocket.X - rocket.PreviousX;
                var velocityY = rocket.Y - rocket.PreviousY;
                if (MathF.Abs(velocityX) <= 0.001f && MathF.Abs(velocityY) <= 0.001f)
                {
                    continue;
                }

                _game._rocketSmokeVisuals.Add(new RocketSmokeVisual(
                    rocket.X - (velocityX * 1.3f),
                    rocket.Y - (velocityY * 1.3f)));
                if (_game._particleMode == 0)
                {
                    _game._rocketSmokeVisuals.Add(new RocketSmokeVisual(
                        rocket.X - (velocityX * 0.75f),
                        rocket.Y - (velocityY * 0.75f)));
                }
            }

            for (var index = _game._rocketSmokeVisuals.Count - 1; index >= 0; index -= 1)
            {
                _game._rocketSmokeVisuals[index].TicksRemaining -= 1;
                if (_game._rocketSmokeVisuals[index].TicksRemaining <= 0)
                {
                    _game._rocketSmokeVisuals.RemoveAt(index);
                }
            }
        }

        public void AdvanceFlameSmokeVisuals()
        {
            if (_game._particleMode == 1)
            {
                _game._mineTrailVisuals.Clear();
                _game._wallspinDustVisuals.Clear();
                _game._blastJumpFlameVisuals.Clear();
                _game._flameSmokeVisuals.Clear();
                return;
            }

            foreach (var flame in _game._world.Flames)
            {
                var smokeChance = _game._particleMode == 2 ? 4 : 2;
                if (flame.IsAttached || _game._visualRandom.Next(smokeChance) != 0)
                {
                    continue;
                }

                _game._flameSmokeVisuals.Add(new FlameSmokeVisual(flame.X, flame.Y - 8f));
            }

            foreach (var player in _game.EnumerateRenderablePlayers())
            {
                if (!player.IsAlive || !IsBlastJumpVisualState(player.MovementState))
                {
                    continue;
                }

                var renderPosition = _game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _game._world.LocalPlayer));
                if (_game._visualRandom.NextSingle() < GetBlastJumpFlameProbability())
                {
                    _game._blastJumpFlameVisuals.Add(new BlastJumpFlameVisual(
                        renderPosition.X,
                        renderPosition.Y + (player.Height * 0.5f) + 1f,
                        _game._visualRandom.Next(GetBlastJumpFlameMinimumLifetimeTicks(), GetBlastJumpFlameMaximumLifetimeTicks() + 1),
                        _game._visualRandom.Next()));
                }

                if (_game._particleMode != 0)
                {
                    continue;
                }

                var smokeProbability = GetBlastJumpSmokeProbability(player);
                if (_game._visualRandom.NextSingle() >= smokeProbability)
                {
                    continue;
                }

                var smokeX = renderPosition.X - (player.HorizontalSpeed * (float)_game._config.FixedDeltaSeconds * 1.2f);
                var smokeY = renderPosition.Y - (player.VerticalSpeed * (float)_game._config.FixedDeltaSeconds * 1.2f) + (player.Height * 0.5f) + 2f;
                _game._flameSmokeVisuals.Add(new FlameSmokeVisual(smokeX, smokeY));
            }

            AdvanceWallspinDustVisuals();

            for (var index = _game._blastJumpFlameVisuals.Count - 1; index >= 0; index -= 1)
            {
                _game._blastJumpFlameVisuals[index].TicksRemaining -= 1;
                if (_game._blastJumpFlameVisuals[index].TicksRemaining <= 0)
                {
                    _game._blastJumpFlameVisuals.RemoveAt(index);
                }
            }

            for (var index = _game._flameSmokeVisuals.Count - 1; index >= 0; index -= 1)
            {
                _game._flameSmokeVisuals[index].TicksRemaining -= 1;
                if (_game._flameSmokeVisuals[index].TicksRemaining <= 0)
                {
                    _game._flameSmokeVisuals.RemoveAt(index);
                }
            }
        }

        public void AdvanceWallspinDustVisuals()
        {
            for (var index = _game._wallspinDustVisuals.Count - 1; index >= 0; index -= 1)
            {
                _game._wallspinDustVisuals[index].TicksRemaining -= 1;
                if (_game._wallspinDustVisuals[index].TicksRemaining <= 0)
                {
                    _game._wallspinDustVisuals.RemoveAt(index);
                }
            }
        }

        public void SpawnWallspinDustVisual(float x, float y, int emissionTicks = 1)
        {
            for (var emissionIndex = 0; emissionIndex < emissionTicks; emissionIndex += 1)
            {
                _game._wallspinDustVisuals.Add(new WallspinDustVisual(
                    x,
                    y,
                    _game._visualRandom.Next(GetWallspinDustMinimumLifetimeTicks(), GetWallspinDustMaximumLifetimeTicks() + 1)));
            }
        }

        public void AdvanceMineTrailVisuals()
        {
            if (_game._particleMode == 1)
            {
                _game._mineTrailVisuals.Clear();
                return;
            }

            foreach (var mine in _game._world.Mines)
            {
                if (mine.IsStickied || (_game._particleMode == 2 && ((_game._world.Frame + mine.Id) & 1) != 0))
                {
                    continue;
                }

                var velocityX = mine.X - mine.PreviousX;
                var velocityY = mine.Y - mine.PreviousY;
                if (MathF.Abs(velocityX) <= 0.001f && MathF.Abs(velocityY) <= 0.001f)
                {
                    continue;
                }

                _game._mineTrailVisuals.Add(new MineTrailVisual(mine.X, mine.Y));
            }

            for (var index = _game._mineTrailVisuals.Count - 1; index >= 0; index -= 1)
            {
                _game._mineTrailVisuals[index].TicksRemaining -= 1;
                if (_game._mineTrailVisuals[index].TicksRemaining <= 0)
                {
                    _game._mineTrailVisuals.RemoveAt(index);
                }
            }
        }

        public void DrawBlastJumpFlameVisuals(Vector2 cameraPosition)
        {
            var sprite = _game._runtimeAssets.GetSprite("FlameS");
            for (var index = 0; index < _game._blastJumpFlameVisuals.Count; index += 1)
            {
                var flame = _game._blastJumpFlameVisuals[index];
                var progress = 1f - (flame.TicksRemaining / (float)flame.InitialTicks);
                var alpha = 1f - (progress * 0.7f);
                var scale = MathF.Max(0.25f, 0.7f - (progress * 0.35f));
                if (sprite is not null && sprite.Frames.Count > 0)
                {
                    var frameIndex = Math.Abs(flame.FrameSeed + ((int)(_game._world.Frame + index))) % sprite.Frames.Count;
                    _game._spriteBatch.Draw(
                        sprite.Frames[frameIndex],
                        new Vector2(flame.X - cameraPosition.X, flame.Y - cameraPosition.Y),
                        null,
                        Color.White * alpha,
                        0f,
                        sprite.Origin.ToVector2(),
                        new Vector2(scale, scale),
                        SpriteEffects.None,
                        0f);
                    continue;
                }

                var flameSize = Math.Max(2, (int)MathF.Round(8f * scale));
                var flameRectangle = new Rectangle(
                    (int)(flame.X - (flameSize / 2f) - cameraPosition.X),
                    (int)(flame.Y - (flameSize / 2f) - cameraPosition.Y),
                    flameSize,
                    flameSize);
                _game._spriteBatch.Draw(_game._pixel, flameRectangle, new Color(255, 170, 90) * alpha);
            }
        }

        public void DrawRocketSmokeVisuals(Vector2 cameraPosition)
        {
            for (var index = 0; index < _game._rocketSmokeVisuals.Count; index += 1)
            {
                var smoke = _game._rocketSmokeVisuals[index];
                var progress = 1f - (smoke.TicksRemaining / (float)RocketSmokeVisual.LifetimeTicks);
                var alpha = 0.7f * (1f - progress);
                var radius = 4f + (progress * 8f);
                var color = new Color(176, 176, 176) * alpha;
                var smokeRectangle = new Rectangle(
                    (int)(smoke.X - radius - cameraPosition.X),
                    (int)(smoke.Y - radius - cameraPosition.Y),
                    (int)(radius * 2f),
                    (int)(radius * 2f));
                _game._spriteBatch.Draw(_game._pixel, smokeRectangle, color);
            }
        }

        public void DrawFlameSmokeVisuals(Vector2 cameraPosition)
        {
            for (var index = 0; index < _game._flameSmokeVisuals.Count; index += 1)
            {
                var smoke = _game._flameSmokeVisuals[index];
                var progress = 1f - (smoke.TicksRemaining / (float)FlameSmokeVisual.LifetimeTicks);
                var alpha = 0.55f * (1f - progress);
                var radius = 3f + (progress * 6f);
                var color = new Color(160, 160, 160) * alpha;
                var smokeRectangle = new Rectangle(
                    (int)(smoke.X - radius - cameraPosition.X),
                    (int)(smoke.Y - radius - (progress * 9f) - cameraPosition.Y),
                    (int)(radius * 2f),
                    (int)(radius * 2f));
                _game._spriteBatch.Draw(_game._pixel, smokeRectangle, color);
            }
        }

        public void DrawMineTrailVisuals(Vector2 cameraPosition)
        {
            var sprite = _game._runtimeAssets.GetSprite("MineTrailS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            for (var index = 0; index < _game._mineTrailVisuals.Count; index += 1)
            {
                var trail = _game._mineTrailVisuals[index];
                var progress = 1f - (trail.TicksRemaining / (float)MineTrailVisual.LifetimeTicks);
                var frameIndex = Math.Clamp((int)MathF.Floor(progress * sprite.Frames.Count), 0, sprite.Frames.Count - 1);
                _game._spriteBatch.Draw(
                    sprite.Frames[frameIndex],
                    new Vector2(trail.X - cameraPosition.X, trail.Y - cameraPosition.Y),
                    null,
                    Color.White,
                    0f,
                    sprite.Origin.ToVector2(),
                    Vector2.One,
                    SpriteEffects.None,
                    0f);
            }
        }

        public void DrawWallspinDustVisuals(Vector2 cameraPosition)
        {
            var sprite = _game._runtimeAssets.GetSprite("SpeedBoostS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            for (var index = 0; index < _game._wallspinDustVisuals.Count; index += 1)
            {
                var dust = _game._wallspinDustVisuals[index];
                var progress = 1f - (dust.TicksRemaining / (float)dust.TotalLifetimeTicks);
                var alpha = progress < 0.5f
                    ? MathHelper.Lerp(0.7f, 0.5f, progress * 2f)
                    : MathHelper.Lerp(0.5f, 0f, (progress - 0.5f) * 2f);
                _game._spriteBatch.Draw(
                    sprite.Frames[0],
                    new Vector2(dust.X - cameraPosition.X, dust.Y - cameraPosition.Y),
                    null,
                    Color.White * alpha,
                    -MathHelper.PiOver2,
                    sprite.Origin.ToVector2(),
                    Vector2.One,
                    SpriteEffects.None,
                    0f);
            }
        }

        private static bool IsBlastJumpVisualState(LegacyMovementState movementState)
        {
            return movementState == LegacyMovementState.ExplosionRecovery
                || movementState == LegacyMovementState.RocketJuggle
                || movementState == LegacyMovementState.FriendlyJuggle;
        }

        private static float GetBlastJumpSmokeProbability(PlayerEntity player)
        {
            var sourceTickProbability = player.MovementState switch
            {
                LegacyMovementState.ExplosionRecovery => 0.175f,
                LegacyMovementState.RocketJuggle => 0.25f,
                LegacyMovementState.FriendlyJuggle => float.Clamp(1f - ((player.RunPower + 1f) * 0.5f), 0f, 1f),
                _ => 0f,
            };
            return sourceTickProbability * (LegacyMovementModel.SourceTicksPerSecond / ClientUpdateTicksPerSecond);
        }

        private static float GetBlastJumpFlameProbability()
        {
            return (5f / 8f) * (LegacyMovementModel.SourceTicksPerSecond / ClientUpdateTicksPerSecond);
        }

        private static int GetBlastJumpFlameMinimumLifetimeTicks()
        {
            return (int)MathF.Ceiling(2f * ClientUpdateTicksPerSecond / LegacyMovementModel.SourceTicksPerSecond);
        }

        private static int GetBlastJumpFlameMaximumLifetimeTicks()
        {
            return (int)MathF.Ceiling(5f * ClientUpdateTicksPerSecond / LegacyMovementModel.SourceTicksPerSecond);
        }

        private static int GetWallspinDustMinimumLifetimeTicks()
        {
            return (int)MathF.Ceiling(Game1.GetSourceTicksAsSeconds(15f) * ClientUpdateTicksPerSecond);
        }

        private static int GetWallspinDustMaximumLifetimeTicks()
        {
            return (int)MathF.Ceiling(Game1.GetSourceTicksAsSeconds(30f) * ClientUpdateTicksPerSecond);
        }
    }
}
