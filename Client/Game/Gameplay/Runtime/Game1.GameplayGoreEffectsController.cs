#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayGoreEffectsController
    {
        private readonly Game1 _game;

        public GameplayGoreEffectsController(Game1 game)
        {
            _game = game;
        }

        public void ResetTransientEffects()
        {
            ResetBackstabVisuals();
            _game._bloodVisuals.Clear();
            _game._bloodSprayVisuals.Clear();
            _game._stickyGibBloodCoatings.Clear();
            _game._staleStickyGibBloodPlayerIds.Clear();
            _game._processedStickyGibBloodDropIds.Clear();
            _game._staleStickyGibBloodDropIds.Clear();
        }

        public void AdvanceBloodVisuals()
        {
            if (_game._gibLevel == 0)
            {
                _game._bloodVisuals.Clear();
                _game._bloodSprayVisuals.Clear();
                _game._stickyGibBloodCoatings.Clear();
                _game._staleStickyGibBloodPlayerIds.Clear();
                _game._processedStickyGibBloodDropIds.Clear();
                _game._staleStickyGibBloodDropIds.Clear();
                return;
            }

            for (var index = _game._bloodVisuals.Count - 1; index >= 0; index -= 1)
            {
                _game._bloodVisuals[index].TicksRemaining -= 1;
                if (_game._bloodVisuals[index].TicksRemaining <= 0)
                {
                    _game._bloodVisuals.RemoveAt(index);
                }
            }

            for (var index = _game._bloodSprayVisuals.Count - 1; index >= 0; index -= 1)
            {
                var spray = _game._bloodSprayVisuals[index];
                spray.TicksRemaining -= 1;
                if (spray.TicksRemaining <= 0)
                {
                    _game._bloodSprayVisuals.RemoveAt(index);
                    continue;
                }

                spray.VelocityY = MathF.Min(BloodDropEntity.MaxSpeed, spray.VelocityY + 0.35f);
                spray.X += spray.VelocityX;
                spray.Y += spray.VelocityY;
                spray.VelocityX *= 0.97f;
            }

            AdvanceStickyGibBloodCoatings();
        }

        public void AdvanceBackstabVisuals()
        {
            if (_game._backstabVisuals.Count == 0)
            {
                return;
            }

            var sourceTickAdvance = _game._clientUpdateElapsedSeconds * LegacyMovementModel.SourceTicksPerSecond;
            if (sourceTickAdvance <= 0f)
            {
                return;
            }

            for (var index = _game._backstabVisuals.Count - 1; index >= 0; index -= 1)
            {
                var visual = _game._backstabVisuals[index];
                visual.PendingSourceTicks += sourceTickAdvance;
                var removeVisual = false;
                while (visual.PendingSourceTicks >= 1f && !visual.Animation.IsExpired)
                {
                    visual.PendingSourceTicks -= 1f;
                    if (!TryGetBackstabOwnerPosition(visual.Animation.OwnerId, out var ownerPosition))
                    {
                        removeVisual = true;
                        break;
                    }

                    visual.Animation.AdvanceOneTick(ownerPosition.X, ownerPosition.Y);
                }

                if (removeVisual || visual.Animation.IsExpired)
                {
                    _game._backstabVisuals.RemoveAt(index);
                }
            }
        }

        public void DrawBackstabVisuals(Vector2 cameraPosition)
        {
            for (var index = 0; index < _game._backstabVisuals.Count; index += 1)
            {
                _game.DrawStabAnimation(_game._backstabVisuals[index].Animation, cameraPosition);
            }
        }

        public void DrawBloodVisuals(Vector2 cameraPosition)
        {
            if (_game._gibLevel == 0)
            {
                return;
            }

            var sprite = _game._runtimeAssets.GetSprite("BloodS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            foreach (var blood in _game._bloodVisuals)
            {
                var elapsedTicks = BloodVisual.LifetimeTicks - blood.TicksRemaining;
                var frameIndex = Math.Clamp(elapsedTicks, 0, sprite.Frames.Count - 1);
                var scale = elapsedTicks < 2 ? 0.5f : 1f;
                var alpha = elapsedTicks < 2 ? 1f : 0.5f;
                _game._spriteBatch.Draw(
                    sprite.Frames[frameIndex],
                    new Vector2(blood.X - cameraPosition.X, blood.Y - cameraPosition.Y),
                    null,
                    Color.White * alpha,
                    0f,
                    sprite.Origin.ToVector2(),
                    new Vector2(scale, scale),
                    SpriteEffects.None,
                    0f);
            }

            var bloodDropSprite = _game._runtimeAssets.GetSprite("BloodDropS");
            for (var index = 0; index < _game._bloodSprayVisuals.Count; index += 1)
            {
                var spray = _game._bloodSprayVisuals[index];
                var alpha = Math.Clamp(spray.TicksRemaining / (float)spray.InitialTicks, 0f, 1f);
                if (bloodDropSprite is not null && bloodDropSprite.Frames.Count > 0)
                {
                    _game._spriteBatch.Draw(
                        bloodDropSprite.Frames[0],
                        new Vector2(spray.X - cameraPosition.X, spray.Y - cameraPosition.Y),
                        null,
                        Color.White * alpha,
                        0f,
                        bloodDropSprite.Origin.ToVector2(),
                        Vector2.One,
                        SpriteEffects.None,
                        0f);
                    continue;
                }

                var rectangle = new Rectangle(
                    (int)(spray.X - cameraPosition.X),
                    (int)(spray.Y - cameraPosition.Y),
                    2,
                    2);
                _game._spriteBatch.Draw(_game._pixel, rectangle, Color.White * alpha);
            }
        }

        public bool TryPlayVisualEvent(string effectName, float x, float y, float directionDegrees, int count)
        {
            if (string.Equals(effectName, "BackstabBlue", StringComparison.OrdinalIgnoreCase))
            {
                SpawnBackstabVisual(ownerId: count, PlayerTeam.Blue, x, y, directionDegrees);
                return true;
            }

            if (string.Equals(effectName, "BackstabRed", StringComparison.OrdinalIgnoreCase))
            {
                SpawnBackstabVisual(ownerId: count, PlayerTeam.Red, x, y, directionDegrees);
                return true;
            }

            if (string.Equals(effectName, "GibBlood", StringComparison.OrdinalIgnoreCase))
            {
                if (_game._gibLevel == 0)
                {
                    return true;
                }

                SpawnGibBloodImpactVisuals(x, y, Math.Max(1, count));
                return true;
            }

            if (!string.Equals(effectName, "Blood", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_game._gibLevel == 0)
            {
                return true;
            }

            SpawnBloodImpactVisuals(x, y, directionDegrees, Math.Max(1, count));
            return true;
        }

        public void SpawnBackstabVisual(int ownerId, PlayerTeam team, float x, float y, float directionDegrees)
        {
            var normalizedDirection = NormalizeDirectionDegrees(directionDegrees);
            for (var index = 0; index < _game._backstabVisuals.Count; index += 1)
            {
                var animation = _game._backstabVisuals[index].Animation;
                if (ownerId != 0 && animation.OwnerId == ownerId)
                {
                    return;
                }

                if (animation.Team != team)
                {
                    continue;
                }

                if (DistanceSquared(animation.X, animation.Y, x, y) > 16f)
                {
                    continue;
                }

                if (GetAngleDifferenceDegrees(animation.DirectionDegrees, normalizedDirection) > 8f)
                {
                    continue;
                }

                return;
            }

            _game._backstabVisuals.Add(new BackstabVisual(
                new StabAnimEntity(_game._nextClientBackstabVisualId--, ownerId, team, x, y, normalizedDirection)));
        }

        public void ResetBackstabVisuals()
        {
            _game._backstabVisuals.Clear();
            _game._nextClientBackstabVisualId = -1;
        }

        public void DrawExperimentalStickyGibBloodOverlay(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha)
        {
            if (!_game.IsPracticeSessionActive
                || !_game._practiceStickyGibBloodEnabled
                || visibilityAlpha <= 0f
                || !_game._stickyGibBloodCoatings.TryGetValue(player.Id, out var coating))
            {
                return;
            }

            var sprite = _game._runtimeAssets.GetSprite("BloodS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            var fadeAlpha = coating.TicksRemaining > StickyGibBloodCoating.FadeTicks
                ? 1f
                : coating.TicksRemaining / (float)StickyGibBloodCoating.FadeTicks;
            var alpha = Math.Clamp(coating.Intensity * fadeAlpha * visibilityAlpha, 0f, 1f);
            if (alpha <= 0f)
            {
                return;
            }

            var renderPosition = _game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _game._world.LocalPlayer));
            var roundedOrigin = Game1.GetRoundedPlayerSpriteOrigin(renderPosition);
            var facingScale = Game1.GetPlayerFacingScale(player);
            var splashCount = Math.Clamp(1 + (int)MathF.Floor(coating.Intensity * 3f), 1, 3);
            for (var splashIndex = 0; splashIndex < splashCount; splashIndex += 1)
            {
                var frameIndex = Math.Abs((player.Id * 17) + (splashIndex * 7)) % sprite.Frames.Count;
                var offsetX = ((splashIndex - 1) * 6f + ((Math.Abs(player.Id + (splashIndex * 11)) % 5) - 2f)) * facingScale;
                var offsetY = splashIndex switch
                {
                    0 => -7f,
                    1 => 0f,
                    _ => 6f,
                };
                var scale = 0.48f + (coating.Intensity * 0.16f) + (splashIndex * 0.04f);
                _game._spriteBatch.Draw(
                    sprite.Frames[frameIndex],
                    new Vector2(roundedOrigin.X + offsetX - cameraPosition.X, roundedOrigin.Y + offsetY - cameraPosition.Y),
                    null,
                    Color.White * (alpha * (0.8f - (splashIndex * 0.12f))),
                    0f,
                    sprite.Origin.ToVector2(),
                    new Vector2(scale, scale),
                    SpriteEffects.None,
                    0f);
            }
        }

        private void AdvanceStickyGibBloodCoatings()
        {
            AdvanceStickyGibBloodContactCoatings();

            if (_game._stickyGibBloodCoatings.Count == 0)
            {
                return;
            }

            _game._staleStickyGibBloodPlayerIds.Clear();
            foreach (var entry in _game._stickyGibBloodCoatings)
            {
                var coatedPlayer = _game.FindPlayerById(entry.Key);
                if (coatedPlayer is null || !coatedPlayer.IsAlive)
                {
                    _game._staleStickyGibBloodPlayerIds.Add(entry.Key);
                    continue;
                }

                entry.Value.TicksRemaining -= 1;
                if (entry.Value.TicksRemaining <= 0)
                {
                    _game._staleStickyGibBloodPlayerIds.Add(entry.Key);
                }
            }

            for (var index = 0; index < _game._staleStickyGibBloodPlayerIds.Count; index += 1)
            {
                _game._stickyGibBloodCoatings.Remove(_game._staleStickyGibBloodPlayerIds[index]);
            }

            _game._staleStickyGibBloodPlayerIds.Clear();
        }

        private void AdvanceStickyGibBloodContactCoatings()
        {
            if (!_game.IsPracticeSessionActive || !_game._practiceStickyGibBloodEnabled)
            {
                _game._processedStickyGibBloodDropIds.Clear();
                _game._staleStickyGibBloodDropIds.Clear();
                return;
            }

            for (var index = 0; index < _game._world.BloodDrops.Count; index += 1)
            {
                var bloodDrop = _game._world.BloodDrops[index];
                if (_game._processedStickyGibBloodDropIds.Contains(bloodDrop.Id))
                {
                    continue;
                }

                if (!TryGetStickyGibBloodTargetPlayer(bloodDrop.X, bloodDrop.Y, bloodDrop.Scale, out var coatedPlayer))
                {
                    continue;
                }

                var intensity = Math.Clamp((int)MathF.Round(bloodDrop.Scale * 1.5f), 1, 3);
                ApplyStickyGibBloodCoating(coatedPlayer, intensity);
                _game._processedStickyGibBloodDropIds.Add(bloodDrop.Id);
            }

            if (_game._processedStickyGibBloodDropIds.Count == 0)
            {
                return;
            }

            _game._staleStickyGibBloodDropIds.Clear();
            foreach (var processedDropId in _game._processedStickyGibBloodDropIds)
            {
                var isActive = false;
                for (var bloodDropIndex = 0; bloodDropIndex < _game._world.BloodDrops.Count; bloodDropIndex += 1)
                {
                    if (_game._world.BloodDrops[bloodDropIndex].Id != processedDropId)
                    {
                        continue;
                    }

                    isActive = true;
                    break;
                }

                if (!isActive)
                {
                    _game._staleStickyGibBloodDropIds.Add(processedDropId);
                }
            }

            for (var index = 0; index < _game._staleStickyGibBloodDropIds.Count; index += 1)
            {
                _game._processedStickyGibBloodDropIds.Remove(_game._staleStickyGibBloodDropIds[index]);
            }

            _game._staleStickyGibBloodDropIds.Clear();
        }

        private void SpawnBloodImpactVisuals(float x, float y, float directionDegrees, int burstCount)
        {
            for (var index = 0; index < burstCount; index += 1)
            {
                var spreadDegrees = directionDegrees + (_game._visualRandom.NextSingle() * 57f) - 28f;
                var spreadRadians = spreadDegrees * (MathF.PI / 180f);
                var distance = burstCount > 1 ? _game._visualRandom.NextSingle() * 8f : 0f;
                _game._bloodVisuals.Add(new BloodVisual(
                    x + MathF.Cos(spreadRadians) * distance,
                    y + MathF.Sin(spreadRadians) * distance));
            }

            var sprayCount = Math.Clamp((burstCount * 2) + 4, 6, 14);
            for (var index = 0; index < sprayCount; index += 1)
            {
                var spreadDegrees = directionDegrees + (_game._visualRandom.NextSingle() * 57f) - 28f;
                var spreadRadians = spreadDegrees * (MathF.PI / 180f);
                var speed = 4f + (_game._visualRandom.NextSingle() * 14f);
                _game._bloodSprayVisuals.Add(new BloodSprayVisual(
                    x,
                    y,
                    MathF.Cos(spreadRadians) * speed,
                    MathF.Sin(spreadRadians) * speed,
                    _game._visualRandom.Next(24, 47)));
            }
        }

        private void SpawnGibBloodImpactVisuals(float x, float y, int intensity)
        {
            TryApplyStickyGibBloodCoating(x, y, intensity);

            var burstCount = Math.Max(6, intensity * 4);
            for (var index = 0; index < burstCount; index += 1)
            {
                var directionRadians = _game._visualRandom.NextSingle() * MathF.Tau;
                var distance = _game._visualRandom.NextSingle() * 11f;
                _game._bloodVisuals.Add(new BloodVisual(
                    x + MathF.Cos(directionRadians) * distance,
                    y + MathF.Sin(directionRadians) * distance));
            }

            var sprayCount = Math.Max(14, intensity * 10);
            for (var index = 0; index < sprayCount; index += 1)
            {
                var directionRadians = _game._visualRandom.NextSingle() * MathF.Tau;
                var speed = 6f + (_game._visualRandom.NextSingle() * 16f);
                var startRadius = _game._visualRandom.NextSingle() * 8f;
                _game._bloodSprayVisuals.Add(new BloodSprayVisual(
                    x + MathF.Cos(directionRadians) * startRadius,
                    y + MathF.Sin(directionRadians) * startRadius,
                    MathF.Cos(directionRadians) * speed,
                    MathF.Sin(directionRadians) * speed,
                    _game._visualRandom.Next(28, 57)));
            }
        }

        private void TryApplyStickyGibBloodCoating(float x, float y, int intensity)
        {
            if (!_game.IsPracticeSessionActive || !_game._practiceStickyGibBloodEnabled)
            {
                return;
            }

            var baseRadius = 18f + (Math.Max(1, intensity) * 4f);
            foreach (var player in _game.EnumerateRenderablePlayers())
            {
                if (!player.IsAlive || _game.GetPlayerVisibilityAlpha(player) <= 0f)
                {
                    continue;
                }

                var reach = baseRadius + (MathF.Max(player.Width, player.Height) * 0.25f);
                var deltaX = player.X - x;
                var deltaY = player.Y - y;
                if ((deltaX * deltaX) + (deltaY * deltaY) > reach * reach)
                {
                    continue;
                }

                ApplyStickyGibBloodCoating(player, intensity);
            }
        }

        private void ApplyStickyGibBloodCoating(PlayerEntity player, int intensity)
        {
            if (!_game._stickyGibBloodCoatings.TryGetValue(player.Id, out var coating))
            {
                coating = new StickyGibBloodCoating();
                _game._stickyGibBloodCoatings[player.Id] = coating;
            }

            coating.TicksRemaining = StickyGibBloodCoating.LifetimeTicks;
            coating.Intensity = Math.Clamp(
                Math.Max(coating.Intensity, 0.42f) + (Math.Min(4, Math.Max(1, intensity)) * 0.08f),
                0.42f,
                1f);
        }

        private bool TryGetStickyGibBloodTargetPlayer(float x, float y, float scale, out PlayerEntity coatedPlayer)
        {
            var padding = 2f + (scale * 3f);
            foreach (var player in _game.EnumerateRenderablePlayers())
            {
                if (!player.IsAlive || _game.GetPlayerVisibilityAlpha(player) <= 0f)
                {
                    continue;
                }

                player.GetCollisionBounds(out var left, out var top, out var right, out var bottom);
                if (x < left - padding
                    || x > right + padding
                    || y < top - padding
                    || y > bottom + padding)
                {
                    continue;
                }

                coatedPlayer = player;
                return true;
            }

            coatedPlayer = _game._world.LocalPlayer;
            return false;
        }

        private bool TryGetBackstabOwnerPosition(int ownerId, out Vector2 ownerPosition)
        {
            if (ownerId == 0)
            {
                ownerPosition = default;
                return false;
            }

            var owner = _game.FindPlayerById(ownerId);
            if (owner is null || !owner.IsAlive || owner.ClassId != PlayerClass.Spy)
            {
                ownerPosition = default;
                return false;
            }

            ownerPosition = _game.GetRenderPosition(owner, allowInterpolation: !ReferenceEquals(owner, _game._world.LocalPlayer));
            return true;
        }

        private static float NormalizeDirectionDegrees(float directionDegrees)
        {
            while (directionDegrees < 0f)
            {
                directionDegrees += 360f;
            }

            while (directionDegrees >= 360f)
            {
                directionDegrees -= 360f;
            }

            return directionDegrees;
        }

        private static float GetAngleDifferenceDegrees(float left, float right)
        {
            var difference = MathF.Abs(NormalizeDirectionDegrees(left) - NormalizeDirectionDegrees(right));
            return MathF.Min(difference, 360f - difference);
        }

        private static float DistanceSquared(float x1, float y1, float x2, float y2)
        {
            var deltaX = x2 - x1;
            var deltaY = y2 - y1;
            return (deltaX * deltaX) + (deltaY * deltaY);
        }
    }
}
