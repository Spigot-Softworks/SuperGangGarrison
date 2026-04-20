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
        private const int BrowserRocketSmokeVisualLimit = 48;
        private const int BrowserFlameSmokeVisualLimit = 40;
        private const int BrowserFlameSmokeSecondaryVisualLimit = 40;
        private const int BrowserBlastJumpFlameVisualLimit = 20;
        private const int BrowserMineTrailVisualLimit = 24;
        private const int BrowserWallspinDustVisualLimit = 24;
        private const float FlareSmokeSizeScale = 0.4f;
        private const float FlameSecondarySmokeSizeScale = 0.32f;
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

                if (!CanEmitBrowserVisual(_game._rocketSmokeVisuals.Count, BrowserRocketSmokeVisualLimit))
                {
                    continue;
                }

                _game._rocketSmokeVisuals.Add(new RocketSmokeVisual(
                    rocket.X - (velocityX * 1.3f),
                    rocket.Y - (velocityY * 1.3f),
                    (_game._visualRandom.NextSingle() * 3f) - 1.5f,
                    (_game._visualRandom.NextSingle() * 4f) - 2f,
                    (_game._visualRandom.NextSingle() * 6f) - 3f,
                    (_game._visualRandom.NextSingle() * 8f) - 4f,
                    3f + (_game._visualRandom.NextSingle() * 2.8f),
                    8f + (_game._visualRandom.NextSingle() * 8f),
                    0.75f + (_game._visualRandom.NextSingle() * 0.15f),
                    22 + _game._visualRandom.Next(12)));
                if (_game._particleMode == 0
                    && CanEmitBrowserVisual(_game._rocketSmokeVisuals.Count, BrowserRocketSmokeVisualLimit))
                {
                    _game._rocketSmokeVisuals.Add(new RocketSmokeVisual(
                        rocket.X - (velocityX * 0.75f),
                        rocket.Y - (velocityY * 0.75f),
                        (_game._visualRandom.NextSingle() * 3f) - 1.5f,
                        (_game._visualRandom.NextSingle() * 4f) - 2f,
                        (_game._visualRandom.NextSingle() * 6f) - 3f,
                        (_game._visualRandom.NextSingle() * 8f) - 4f,
                        3f + (_game._visualRandom.NextSingle() * 2.8f),
                        8f + (_game._visualRandom.NextSingle() * 8f),
                        0.75f + (_game._visualRandom.NextSingle() * 0.15f),
                        22 + _game._visualRandom.Next(12)));
                }

                if (rocket.CanIgniteTargets
                    && CanEmitBrowserVisual(_game._blastJumpFlameVisuals.Count, BrowserBlastJumpFlameVisualLimit))
                {
                    var flameOffset = _game._particleMode == 2 ? 0.9f : 0.55f;
                    _game._blastJumpFlameVisuals.Add(new BlastJumpFlameVisual(
                        rocket.X - (velocityX * flameOffset),
                        rocket.Y - (velocityY * flameOffset),
                        _game._visualRandom.Next(GetBlastJumpFlameMinimumLifetimeTicks(), GetBlastJumpFlameMaximumLifetimeTicks() + 1),
                        _game._visualRandom.Next()));
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
                _game._flameSmokeSecondaryVisuals.Clear();
                return;
            }

            foreach (var flare in _game._world.Flares)
            {
                if (_game._particleMode == 2 && ((_game._world.Frame + flare.Id) & 1) != 0)
                {
                    continue;
                }

                var velocityX = flare.X - flare.PreviousX;
                var velocityY = flare.Y - flare.PreviousY;
                if (MathF.Abs(velocityX) <= 0.001f && MathF.Abs(velocityY) <= 0.001f)
                {
                    continue;
                }

                if (!CanEmitBrowserVisual(_game._flameSmokeVisuals.Count, BrowserFlameSmokeVisualLimit))
                {
                    continue;
                }

                _game._flameSmokeVisuals.Add(new FlameSmokeVisual(
                    flare.X - (velocityX * 1.3f),
                    flare.Y - (velocityY * 1.3f),
                    ((_game._visualRandom.NextSingle() * 3f) - 1.5f) * FlareSmokeSizeScale,
                    ((_game._visualRandom.NextSingle() * 4f) - 2f) * FlareSmokeSizeScale,
                    ((_game._visualRandom.NextSingle() * 6f) - 3f) * FlareSmokeSizeScale,
                    ((_game._visualRandom.NextSingle() * 8f) - 4f) * FlareSmokeSizeScale,
                    (3f + (_game._visualRandom.NextSingle() * 2.8f)) * FlareSmokeSizeScale,
                    (8f + (_game._visualRandom.NextSingle() * 8f)) * FlareSmokeSizeScale,
                    0.75f + (_game._visualRandom.NextSingle() * 0.15f),
                    22 + _game._visualRandom.Next(12)));

                if (_game._particleMode == 0
                    && CanEmitBrowserVisual(_game._flameSmokeVisuals.Count, BrowserFlameSmokeVisualLimit))
                {
                    _game._flameSmokeVisuals.Add(new FlameSmokeVisual(
                        flare.X - (velocityX * 0.75f),
                        flare.Y - (velocityY * 0.75f),
                        ((_game._visualRandom.NextSingle() * 3f) - 1.5f) * FlareSmokeSizeScale,
                        ((_game._visualRandom.NextSingle() * 4f) - 2f) * FlareSmokeSizeScale,
                        ((_game._visualRandom.NextSingle() * 6f) - 3f) * FlareSmokeSizeScale,
                        ((_game._visualRandom.NextSingle() * 8f) - 4f) * FlareSmokeSizeScale,
                        (3f + (_game._visualRandom.NextSingle() * 2.8f)) * FlareSmokeSizeScale,
                        (8f + (_game._visualRandom.NextSingle() * 8f)) * FlareSmokeSizeScale,
                        0.75f + (_game._visualRandom.NextSingle() * 0.15f),
                        22 + _game._visualRandom.Next(12)));
                }
            }

            foreach (var flame in _game._world.Flames)
            {
                if (flame.IsAttached)
                {
                    continue;
                }

                var proximityFactor = GetFlameWeaponProximityFactor(flame);
                var mainSmokeProbability = GetFlameSmokeEmissionProbability(_game._particleMode, proximityFactor, secondary: false);
                if (_game._visualRandom.NextSingle() >= mainSmokeProbability)
                {
                    continue;
                }

                if (CanEmitBrowserVisual(_game._flameSmokeVisuals.Count, BrowserFlameSmokeVisualLimit))
                {
                    _game._flameSmokeVisuals.Add(new FlameSmokeVisual(
                        flame.X,
                        flame.Y - 8f,
                        (_game._visualRandom.NextSingle() * 2.4f) - 1.2f,
                        (_game._visualRandom.NextSingle() * 3f) - 1.5f,
                        (_game._visualRandom.NextSingle() * 5f) - 2.5f,
                        (_game._visualRandom.NextSingle() * 7f) - 3.5f,
                        1.2f + (_game._visualRandom.NextSingle() * 2f),
                        5f + (_game._visualRandom.NextSingle() * 6.2f),
                        0.70f + (_game._visualRandom.NextSingle() * 0.2f),
                        14 + _game._visualRandom.Next(10)));
                }

                if (CanEmitBrowserVisual(_game._flameSmokeSecondaryVisuals.Count, BrowserFlameSmokeSecondaryVisualLimit))
                {
                    var secondarySmokeProbability = GetFlameSmokeEmissionProbability(_game._particleMode, proximityFactor, secondary: true);
                    if (_game._visualRandom.NextSingle() >= secondarySmokeProbability)
                    {
                        continue;
                    }

                    _game._flameSmokeSecondaryVisuals.Add(new FlameSmokeVisual(
                        flame.X,
                        flame.Y - 8f,
                        ((_game._visualRandom.NextSingle() * 6f) - 3f) * FlameSecondarySmokeSizeScale,
                        ((_game._visualRandom.NextSingle() * 7f) - 3.5f) * FlameSecondarySmokeSizeScale,
                        ((_game._visualRandom.NextSingle() * 12f) - 6f) * FlameSecondarySmokeSizeScale,
                        ((_game._visualRandom.NextSingle() * 14f) - 7f) * FlameSecondarySmokeSizeScale,
                        (2.4f + (_game._visualRandom.NextSingle() * 2.2f)) * FlameSecondarySmokeSizeScale,
                        (7.5f + (_game._visualRandom.NextSingle() * 10.5f)) * FlameSecondarySmokeSizeScale,
                        0.68f + (_game._visualRandom.NextSingle() * 0.2f),
                        26 + _game._visualRandom.Next(16)));
                }
            }

            foreach (var player in _game.EnumerateRenderablePlayers())
            {
                if (!player.IsAlive || !IsBlastJumpVisualState(player.MovementState))
                {
                    continue;
                }

                var renderPosition = _game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _game._world.LocalPlayer));
                if (_game._visualRandom.NextSingle() < GetBlastJumpFlameProbability()
                    && CanEmitBrowserVisual(_game._blastJumpFlameVisuals.Count, BrowserBlastJumpFlameVisualLimit))
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
                if (CanEmitBrowserVisual(_game._flameSmokeVisuals.Count, BrowserFlameSmokeVisualLimit))
                {
                    _game._flameSmokeVisuals.Add(new FlameSmokeVisual(
                        smokeX,
                        smokeY,
                        (_game._visualRandom.NextSingle() * 2.4f) - 1.2f,
                        (_game._visualRandom.NextSingle() * 3f) - 1.5f,
                        (_game._visualRandom.NextSingle() * 5f) - 2.5f,
                        (_game._visualRandom.NextSingle() * 7f) - 3.5f,
                        1.2f + (_game._visualRandom.NextSingle() * 2f),
                        5f + (_game._visualRandom.NextSingle() * 6.2f),
                        0.70f + (_game._visualRandom.NextSingle() * 0.2f),
                        14 + _game._visualRandom.Next(10)));
                }
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

            for (var index = _game._flameSmokeSecondaryVisuals.Count - 1; index >= 0; index -= 1)
            {
                _game._flameSmokeSecondaryVisuals[index].TicksRemaining -= 1;
                if (_game._flameSmokeSecondaryVisuals[index].TicksRemaining <= 0)
                {
                    _game._flameSmokeSecondaryVisuals.RemoveAt(index);
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
                if (!CanEmitBrowserVisual(_game._wallspinDustVisuals.Count, BrowserWallspinDustVisualLimit))
                {
                    break;
                }

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

                if (CanEmitBrowserVisual(_game._mineTrailVisuals.Count, BrowserMineTrailVisualLimit))
                {
                    _game._mineTrailVisuals.Add(new MineTrailVisual(mine.X, mine.Y));
                }
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
            var sprite = _game.GetResolvedSprite("FlameS");
            for (var index = 0; index < _game._blastJumpFlameVisuals.Count; index += 1)
            {
                var flame = _game._blastJumpFlameVisuals[index];
                var progress = 1f - (flame.TicksRemaining / (float)flame.InitialTicks);
                var alpha = 1f - (progress * 0.7f);
                var scale = MathF.Max(0.25f, 0.7f - (progress * 0.35f));
                if (sprite is not null && sprite.Frames.Count > 0)
                {
                    var frameIndex = Math.Abs(flame.FrameSeed + ((int)(_game._world.Frame + index))) % sprite.Frames.Count;
                    _game.DrawLoadedSpriteFrame(
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
            var cells = new System.Collections.Generic.Dictionary<(int, int), (float alpha, float shade)>();
            var driftTicks = MathF.Max(1f, 0.2f / (float)_game._config.FixedDeltaSeconds);
            for (var index = 0; index < _game._rocketSmokeVisuals.Count; index += 1)
            {
                var smoke = _game._rocketSmokeVisuals[index];
                var progress = 1f - (smoke.TicksRemaining / (float)smoke.LifetimeTicks);
                var alpha = smoke.InitialAlpha * MathF.Pow(1f - progress, 0.45f);
                if (alpha <= 0.5f)
                {
                    continue;
                }

                var radius = MathHelper.Lerp(smoke.InitialRadius, smoke.FinalRadius, progress);
                var colorProgress = MathF.Min(1f, progress * 1.9f);
                var shade = MathHelper.Lerp(0.98f, 0.62f, colorProgress);
                var ageTicks = smoke.LifetimeTicks - smoke.TicksRemaining;
                var driftProgress = Math.Clamp(ageTicks / driftTicks, 0f, 1f);
                var worldX = smoke.X + smoke.OffsetX + (smoke.DriftX * driftProgress);
                var worldY = smoke.Y + smoke.OffsetY + (smoke.DriftY * driftProgress);
                var smokeSeed = (int)(smoke.X * 17f) ^ (int)(smoke.Y * 31f) ^ smoke.LifetimeTicks;
                AccumulateSmokeCircle(cells, worldX, worldY, radius, alpha, shade, progress, smokeSeed);
            }

            DrawSmokeCells(cells, cameraPosition);
        }

        public void DrawFlameSmokeVisuals(Vector2 cameraPosition)
        {
            DrawFlameSmokePass(_game._flameSmokeVisuals, cameraPosition, brightnessScale: 0.5f);
            DrawFlameSmokePass(_game._flameSmokeSecondaryVisuals, cameraPosition, brightnessScale: 0.2f);
        }

        private void DrawFlameSmokePass(
            System.Collections.Generic.IReadOnlyList<FlameSmokeVisual> smokeVisuals,
            Vector2 cameraPosition,
            float brightnessScale)
        {
            var cells = new System.Collections.Generic.Dictionary<(int, int), (float alpha, float shade)>();
            var driftTicks = MathF.Max(1f, 0.2f / (float)_game._config.FixedDeltaSeconds);
            for (var index = 0; index < smokeVisuals.Count; index += 1)
            {
                var smoke = smokeVisuals[index];
                var progress = 1f - (smoke.TicksRemaining / (float)smoke.LifetimeTicks);
                var alpha = smoke.InitialAlpha * MathF.Pow(1f - progress, 0.55f);
                if (alpha <= 0.5f)
                {
                    continue;
                }

                var radius = MathHelper.Lerp(smoke.InitialRadius, smoke.FinalRadius, progress);
                var colorProgress = MathF.Min(1f, progress * 2f);
                var shade = MathHelper.Lerp(0.96f, 0.60f, colorProgress);
                var ageTicks = smoke.LifetimeTicks - smoke.TicksRemaining;
                var driftProgress = Math.Clamp(ageTicks / driftTicks, 0f, 1f);
                var worldX = smoke.X + smoke.OffsetX + (smoke.DriftX * driftProgress);
                var worldY = smoke.Y + smoke.OffsetY + (smoke.DriftY * driftProgress);
                var smokeSeed = (int)(smoke.X * 29f) ^ (int)(smoke.Y * 13f) ^ smoke.LifetimeTicks;
                AccumulateSmokeCircle(cells, worldX, worldY, radius, alpha, shade, progress, smokeSeed);
            }

            DrawSmokeCells(cells, cameraPosition, brightnessScale);
        }

        private static void AccumulateSmokeCircle(
            System.Collections.Generic.Dictionary<(int, int), (float alpha, float shade)> cells,
            float worldCX, float worldCY, float radius, float alpha, float shade, float progress, int seed)
        {
            const float cellSize = 2f;
            var minGX = (int)MathF.Floor((worldCX - radius) / cellSize);
            var maxGX = (int)MathF.Floor((worldCX + radius) / cellSize);
            var minGY = (int)MathF.Floor((worldCY - radius) / cellSize);
            var maxGY = (int)MathF.Floor((worldCY + radius) / cellSize);
            var radiusSquared = radius * radius;
            var diameter = MathF.Max(0.0001f, radius * 2f);
            for (var gy = minGY; gy <= maxGY; gy += 1)
            {
                var cellCY = (gy * cellSize) + (cellSize * 0.5f);
                var dy = cellCY - worldCY;
                for (var gx = minGX; gx <= maxGX; gx += 1)
                {
                    var cellCX = (gx * cellSize) + (cellSize * 0.5f);
                    var dx = cellCX - worldCX;
                    var distanceSquared = (dx * dx) + (dy * dy);
                    if (distanceSquared > radiusSquared)
                    {
                        continue;
                    }

                    var radial = 1f - (MathF.Sqrt(distanceSquared) / radius);
                    var outsideFactor = 1f - radial;
                    var vertical01 = Math.Clamp((cellCY - (worldCY - radius)) / diameter, 0f, 1f);
                    var random = GetSmokeNoise01(gx, gy, seed);
                    var randomFadeBoost = (random - 0.5f) * 0.6f;
                    var pixelFadeSpeed = 0.5f + (outsideFactor * 1.35f) + (vertical01 * 0.9f) + randomFadeBoost;
                    var pixelProgress = Math.Clamp(progress * pixelFadeSpeed, 0f, 1f);
                    var pixelFade = 1f - pixelProgress;
                    if (pixelFade <= 0f)
                    {
                        continue;
                    }

                    var pixelValueJitter = 0.75f + (random * 0.5f);
                    var cellAlpha = alpha * radial * pixelFade * pixelValueJitter;
                    if (cellAlpha < 0.02f)
                    {
                        continue;
                    }

                    var key = (gx, gy);
                    if (cells.TryGetValue(key, out var existing))
                    {
                        var combined = MathF.Min(0.85f, existing.alpha + cellAlpha);
                        var blendedShade = ((existing.shade * existing.alpha) + (shade * cellAlpha)) / MathF.Max(0.0001f, existing.alpha + cellAlpha);
                        cells[key] = (combined, blendedShade);
                    }
                    else
                    {
                        cells[key] = (cellAlpha, shade);
                    }
                }
            }
        }

        private static float GetSmokeNoise01(int gx, int gy, int seed)
        {
            unchecked
            {
                var hash = (uint)seed;
                hash ^= (uint)(gx * 374761393);
                hash ^= (uint)(gy * 668265263);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return (hash & 1023u) / 1023f;
            }
        }

        private void DrawSmokeCells(
            System.Collections.Generic.Dictionary<(int, int), (float alpha, float shade)> cells,
            Vector2 cameraPosition,
            float brightnessScale = 1f)
        {
            const float cellSize = 2f;
            var clampedBrightnessScale = Math.Clamp(brightnessScale, 0f, 1f);
            foreach (var ((gx, gy), (_, shade)) in cells)
            {
                var intensity = (102f + (shade * 153f)) * clampedBrightnessScale;
                var gray = (byte)Math.Clamp((int)MathF.Round(intensity), 0, 255);
                var rect = new Rectangle(
                    (int)MathF.Round((gx * cellSize) - cameraPosition.X),
                    (int)MathF.Round((gy * cellSize) - cameraPosition.Y),
                    (int)cellSize,
                    (int)cellSize);
                _game._spriteBatch.Draw(_game._pixel, rect, new Color(gray, gray, gray));
            }
        }

        public void DrawMineTrailVisuals(Vector2 cameraPosition)
        {
            var sprite = _game.GetResolvedSprite("MineTrailS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            for (var index = 0; index < _game._mineTrailVisuals.Count; index += 1)
            {
                var trail = _game._mineTrailVisuals[index];
                var progress = 1f - (trail.TicksRemaining / (float)MineTrailVisual.LifetimeTicks);
                var frameIndex = Math.Clamp((int)MathF.Floor(progress * sprite.Frames.Count), 0, sprite.Frames.Count - 1);
                _game.DrawLoadedSpriteFrame(
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
            var sprite = _game.GetResolvedSprite("SpeedBoostS");
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
                _game.DrawLoadedSpriteFrame(
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

        private static float GetFlameWeaponProximityFactor(FlameProjectileEntity flame)
        {
            return float.Clamp(flame.TicksRemaining / (float)FlameProjectileEntity.AirLifetimeTicks, 0f, 1f);
        }

        private static float GetFlameSmokeEmissionProbability(int particleMode, float proximityFactor, bool secondary)
        {
            var baseProbability = secondary
                ? (particleMode == 2 ? 0.125f : 0.25f)
                : (particleMode == 2 ? 0.25f : 0.5f);
            var proximityCurve = proximityFactor * proximityFactor;
            var distanceScale = secondary
                ? (0.12f + (2.1f * proximityCurve))
                : (0.18f + (1.9f * proximityCurve));
            return float.Clamp(baseProbability * distanceScale, 0f, 1f);
        }

        private static bool CanEmitBrowserVisual(int currentCount, int browserLimit)
        {
            return !OperatingSystem.IsBrowser() || currentCount < browserLimit;
        }
    }
}
