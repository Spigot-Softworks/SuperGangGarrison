#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed partial class GameplayGoreEffectsController
    {
        private const int GibBloodExplosionClientTicksPerFrame = 3;
        private const int GibBloodExplosionLifetimeClientTicks = 3 * GibBloodExplosionClientTicksPerFrame;
        private const float GibBloodExplosionDrawScale = 1.4f;
        private readonly List<GibBloodExplosionVisual> _gibBloodExplosionVisuals = new();
        private readonly IGameplayContext _context;

        private sealed class GibBloodExplosionVisual(float x, float y)
        {
            public float X { get; } = x;
            public float Y { get; } = y;
            public int AgeClientTicks { get; set; }
        }

        public GameplayGoreEffectsController(IGameplayContext context)
        {
            _context = context;
        }

        public void ResetTransientEffects()
        {
            ResetBackstabVisuals();
            _context._bloodVisuals.Clear();
            _context._bloodSprayVisuals.Clear();
            _gibBloodExplosionVisuals.Clear();
            _context._stickyGibBloodCoatings.Clear();
            _context._staleStickyGibBloodPlayerIds.Clear();
            _context._processedStickyGibBloodDropIds.Clear();
            _context._staleStickyGibBloodDropIds.Clear();
            ResetBloodSquibEffects();
            _context.ResetDynamicRagdollEffects();
            _context.ResetCorpseAcidDissolves();
        }

        public void AdvanceBloodVisuals()
        {
            _context.AdvanceDynamicRagdolls();
            _context.SyncDynamicRagdollsWithDeadBodies();
            _context.AdvanceCorpseAcidDissolves();

            if (!_context.AreBloodVisualsEnabled)
            {
                _context._bloodVisuals.Clear();
                _context._bloodSprayVisuals.Clear();
                _gibBloodExplosionVisuals.Clear();
                ResetBloodSquibEffects();
                _context._stickyGibBloodCoatings.Clear();
                _context._staleStickyGibBloodPlayerIds.Clear();
                _context._processedStickyGibBloodDropIds.Clear();
                _context._staleStickyGibBloodDropIds.Clear();
                return;
            }

            if (_context._bloodRenderMode == 0)
            {
                _context._bloodVisuals.Clear();
                _context._bloodSprayVisuals.Clear();
                AdvanceBloodSquibEffects();
                AdvanceStickyGibBloodCoatings();
                return;
            }

            ResetBloodSquibEffects();

            for (var index = _context._bloodVisuals.Count - 1; index >= 0; index -= 1)
            {
                _context._bloodVisuals[index].TicksRemaining -= 1;
                if (_context._bloodVisuals[index].TicksRemaining <= 0)
                {
                    _context._bloodVisuals.RemoveAt(index);
                }
            }

            for (var index = _context._bloodSprayVisuals.Count - 1; index >= 0; index -= 1)
            {
                var spray = _context._bloodSprayVisuals[index];
                spray.TicksRemaining -= 1;
                if (spray.TicksRemaining <= 0)
                {
                    _context._bloodSprayVisuals.RemoveAt(index);
                    continue;
                }

                spray.VelocityY = MathF.Min(BloodDropEntity.MaxSpeed, spray.VelocityY + 0.45f);
                spray.X += spray.VelocityX;
                spray.Y += spray.VelocityY;
                spray.VelocityX *= 0.96f;
            }

            AdvanceStickyGibBloodCoatings();
        }

        public void AdvanceGibBloodExplosions()
        {
            if (!_context.AreBloodVisualsEnabled)
            {
                _gibBloodExplosionVisuals.Clear();
                return;
            }

            for (var index = _gibBloodExplosionVisuals.Count - 1; index >= 0; index -= 1)
            {
                var visual = _gibBloodExplosionVisuals[index];
                visual.AgeClientTicks += 1;
                if (visual.AgeClientTicks >= GibBloodExplosionLifetimeClientTicks)
                {
                    _gibBloodExplosionVisuals.RemoveAt(index);
                }
            }
        }

        public void AdvanceBackstabVisuals()
        {
            if (_context._backstabVisuals.Count == 0)
            {
                return;
            }

            var sourceTickAdvance = (float)(ClientUpdateStepSeconds * LegacyMovementModel.SourceTicksPerSecond);
            if (sourceTickAdvance <= 0f)
            {
                return;
            }

            for (var index = _context._backstabVisuals.Count - 1; index >= 0; index -= 1)
            {
                var visual = _context._backstabVisuals[index];
                if (IsBackstabVisualOwnerInactive(visual.Animation.OwnerId))
                {
                    _context._backstabVisuals.RemoveAt(index);
                    continue;
                }

                visual.PendingSourceTicks += sourceTickAdvance;
                while (visual.PendingSourceTicks >= 1f && !visual.Animation.IsExpired)
                {
                    visual.PendingSourceTicks -= 1f;
                    if (TryGetBackstabOwnerPosition(visual.Animation.OwnerId, out var ownerPosition))
                    {
                        visual.Animation.AdvanceOneTick(ownerPosition.X, ownerPosition.Y);
                    }
                    else
                    {
                        visual.Animation.AdvanceOneTick(visual.Animation.X, visual.Animation.Y);
                    }
                }

                if (visual.Animation.IsExpired)
                {
                    _context._backstabVisuals.RemoveAt(index);
                }
            }
        }

        public void DrawBackstabVisuals(Vector2 cameraPosition)
        {
            for (var index = 0; index < _context._backstabVisuals.Count; index += 1)
            {
                var backstabVisual = _context._backstabVisuals[index].Animation;
                if (backstabVisual.OwnerId != 0)
                {
                    var owner = _context.FindPlayerById(backstabVisual.OwnerId);
                    if (owner is null || !owner.IsAlive || owner.ClassId != PlayerClass.Spy)
                    {
                        continue;
                    }

                    if (owner.IsAlive
                        && owner.ClassId == PlayerClass.Spy
                        && _context.IsSpyHiddenFromLocalViewer(owner))
                    {
                        continue;
                    }
                }

                _context.DrawStabAnimation(backstabVisual, cameraPosition);
            }
        }

        private bool IsBackstabVisualOwnerInactive(int ownerId)
        {
            if (ownerId == 0)
            {
                return false;
            }

            var owner = _context.FindPlayerById(ownerId);
            return owner is null || !owner.IsAlive || owner.ClassId != PlayerClass.Spy;
        }

        public void DrawBloodVisuals(Vector2 cameraPosition)
        {
            if (!_context.AreBloodVisualsEnabled || _context._bloodRenderMode == 0)
            {
                // Squib flight particles are drawn with gameplay effects; settled pools with the map.
                return;
            }

            var sprite = _context.GetResolvedSprite("BloodS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            foreach (var blood in _context._bloodVisuals)
            {
                var elapsedTicks = BloodVisual.LifetimeTicks - blood.TicksRemaining;
                var frameIndex = Math.Clamp(elapsedTicks, 0, sprite.Frames.Count - 1);
                var scale = elapsedTicks < 2 ? 0.5f : 1f;
                var alpha = elapsedTicks < 2 ? 1f : 0.5f;
                _context.DrawLoadedSpriteFrame(
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

            var bloodDropSprite = _context.GetResolvedSprite("BloodDropS");
            for (var index = 0; index < _context._bloodSprayVisuals.Count; index += 1)
            {
                var spray = _context._bloodSprayVisuals[index];
                var alpha = Math.Clamp(spray.TicksRemaining / (float)spray.InitialTicks, 0f, 1f);
                if (bloodDropSprite is not null && bloodDropSprite.Frames.Count > 0)
                {
                    _context.DrawLoadedSpriteFrame(
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
                _context._spriteBatch.Draw(_context._pixel, rectangle, Color.White * alpha);
            }
        }

        public void DrawGibBloodExplosions(Vector2 cameraPosition)
        {
            if (!_context.AreBloodVisualsEnabled || _gibBloodExplosionVisuals.Count == 0)
            {
                return;
            }

            var sprite = _context.GetResolvedSprite("GibBloodExplosionS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            foreach (var visual in _gibBloodExplosionVisuals)
            {
                var frameIndex = Math.Min(visual.AgeClientTicks / GibBloodExplosionClientTicksPerFrame, sprite.Frames.Count - 1);
                _context.DrawLoadedSpriteFrame(
                    sprite.Frames[frameIndex],
                    new Vector2(visual.X - cameraPosition.X, visual.Y - cameraPosition.Y),
                    null,
                    Color.White,
                    0f,
                    sprite.Origin.ToVector2(),
                    new Vector2(GibBloodExplosionDrawScale, GibBloodExplosionDrawScale),
                    SpriteEffects.None,
                    0f);
            }
        }

        public void DrawBloodSquibPools(Vector2 cameraPosition)
        {
            if (!_context.AreBloodVisualsEnabled || _context._bloodRenderMode != 0)
            {
                return;
            }

            DrawSettledBloodSquibPools(cameraPosition);
        }

        public void DrawBloodSquibFlight(Vector2 cameraPosition)
        {
            if (!_context.AreBloodVisualsEnabled || _context._bloodRenderMode != 0)
            {
                return;
            }

            DrawFlyingBloodSquibParticles(cameraPosition);
        }

        public bool TryPlayVisualEvent(string effectName, float x, float y, float directionDegrees, int count)
        {
            if (string.Equals(effectName, "BackstabBlue", StringComparison.OrdinalIgnoreCase))
            {
                _context.SpawnBackstabVisual(ownerId: count, PlayerTeam.Blue, x, y, directionDegrees);
                return true;
            }

            if (string.Equals(effectName, "BackstabRed", StringComparison.OrdinalIgnoreCase))
            {
                _context.SpawnBackstabVisual(ownerId: count, PlayerTeam.Red, x, y, directionDegrees);
                return true;
            }

            if (string.Equals(effectName, "GibBlood", StringComparison.OrdinalIgnoreCase))
            {
                if (!_context.AreBloodVisualsEnabled)
                {
                    return true;
                }

                _gibBloodExplosionVisuals.Add(new GibBloodExplosionVisual(x, y));
                SpawnGibBloodImpactVisuals(x, y, Math.Max(1, count));
                return true;
            }

            if (!string.Equals(effectName, "Blood", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!_context.AreBloodVisualsEnabled)
            {
                return true;
            }

            SpawnBloodImpactVisuals(x, y, directionDegrees, Math.Max(1, count), explosive: false);
            return true;
        }

        public void SpawnImmediateFatalDamageVisuals(float x, float y, int damageAmount)
        {
            if (!_context.AreBloodVisualsEnabled)
            {
                return;
            }

            var burstCount = Math.Clamp(Math.Max(3, damageAmount / 18), 3, 8);
            SpawnBloodImpactVisuals(x, y, 270f, burstCount, explosive: true);
        }

        public void SpawnBackstabVisual(
            int ownerId,
            PlayerTeam team,
            float x,
            float y,
            float directionDegrees,
            int speedMultiplier = 1)
        {
            var normalizedDirection = NormalizeDirectionDegrees(directionDegrees);
            for (var index = 0; index < _context._backstabVisuals.Count; index += 1)
            {
                var animation = _context._backstabVisuals[index].Animation;
                if (ownerId != 0 && animation.OwnerId == ownerId)
                {
                    _context._backstabVisuals[index] = new BackstabVisual(
                        new StabAnimEntity(
                            _context._nextClientBackstabVisualId--,
                            ownerId,
                            team,
                            x,
                            y,
                            normalizedDirection,
                            speedMultiplier));
                    return;
                }

                if (ownerId != 0)
                {
                    continue;
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

            _context._backstabVisuals.Add(new BackstabVisual(
                new StabAnimEntity(
                    _context._nextClientBackstabVisualId--,
                    ownerId,
                    team,
                    x,
                    y,
                    normalizedDirection,
                    speedMultiplier)));
        }

        public void ResetBackstabVisuals()
        {
            _context._backstabVisuals.Clear();
            _context._nextClientBackstabVisualId = -1;
        }

        public void DrawExperimentalStickyGibBloodOverlay(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha)
        {
            if (!_context.IsPracticeSessionActive
                || !_context._practiceStickyGibBloodEnabled
                || visibilityAlpha <= 0f
                || !_context._stickyGibBloodCoatings.TryGetValue(player.Id, out var coating))
            {
                return;
            }

            var sprite = _context.GetResolvedSprite("BloodS");
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            var fadeTicks = Math.Max(
                1,
                (int)MathF.Round(StickyGibBloodCoating.FadeTicks * _context.GetBloodPersistenceScale()));
            var fadeAlpha = coating.TicksRemaining > fadeTicks
                ? 1f
                : coating.TicksRemaining / (float)fadeTicks;
            var alpha = Math.Clamp(coating.Intensity * fadeAlpha * visibilityAlpha, 0f, 1f);
            if (alpha <= 0f)
            {
                return;
            }

            var renderPosition = _context.GetRenderPosition(player);
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
                _context.DrawLoadedSpriteFrame(
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

            if (_context._stickyGibBloodCoatings.Count == 0)
            {
                return;
            }

            _context._staleStickyGibBloodPlayerIds.Clear();
            foreach (var entry in _context._stickyGibBloodCoatings)
            {
                var coatedPlayer = _context.FindPlayerById(entry.Key);
                if (coatedPlayer is null || !coatedPlayer.IsAlive)
                {
                    _context._staleStickyGibBloodPlayerIds.Add(entry.Key);
                    continue;
                }

                entry.Value.TicksRemaining -= 1;
                if (entry.Value.TicksRemaining <= 0)
                {
                    _context._staleStickyGibBloodPlayerIds.Add(entry.Key);
                }
            }

            for (var index = 0; index < _context._staleStickyGibBloodPlayerIds.Count; index += 1)
            {
                _context._stickyGibBloodCoatings.Remove(_context._staleStickyGibBloodPlayerIds[index]);
            }

            _context._staleStickyGibBloodPlayerIds.Clear();
        }

        private void AdvanceStickyGibBloodContactCoatings()
        {
            if (!_context.IsPracticeSessionActive || !_context._practiceStickyGibBloodEnabled)
            {
                _context._processedStickyGibBloodDropIds.Clear();
                _context._staleStickyGibBloodDropIds.Clear();
                return;
            }

            for (var index = 0; index < _context._world.BloodDrops.Count; index += 1)
            {
                var bloodDrop = _context._world.BloodDrops[index];
                if (_context._processedStickyGibBloodDropIds.Contains(bloodDrop.Id))
                {
                    continue;
                }

                if (!TryGetStickyGibBloodTargetPlayer(bloodDrop.X, bloodDrop.Y, bloodDrop.Scale, out var coatedPlayer))
                {
                    continue;
                }

                var intensity = Math.Clamp((int)MathF.Round(bloodDrop.Scale * 1.5f), 1, 3);
                ApplyStickyGibBloodCoating(coatedPlayer, intensity);
                _context._processedStickyGibBloodDropIds.Add(bloodDrop.Id);
            }

            if (_context._processedStickyGibBloodDropIds.Count == 0)
            {
                return;
            }

            _context._staleStickyGibBloodDropIds.Clear();
            foreach (var processedDropId in _context._processedStickyGibBloodDropIds)
            {
                var isActive = false;
                for (var bloodDropIndex = 0; bloodDropIndex < _context._world.BloodDrops.Count; bloodDropIndex += 1)
                {
                    if (_context._world.BloodDrops[bloodDropIndex].Id != processedDropId)
                    {
                        continue;
                    }

                    isActive = true;
                    break;
                }

                if (!isActive)
                {
                    _context._staleStickyGibBloodDropIds.Add(processedDropId);
                }
            }

            for (var index = 0; index < _context._staleStickyGibBloodDropIds.Count; index += 1)
            {
                _context._processedStickyGibBloodDropIds.Remove(_context._staleStickyGibBloodDropIds[index]);
            }

            _context._staleStickyGibBloodDropIds.Clear();
        }

        private void SpawnBloodImpactVisuals(float x, float y, float directionDegrees, int burstCount, bool explosive = false)
        {
            if (!_context.AreBloodVisualsEnabled)
            {
                return;
            }

            if (_context._bloodRenderMode == 0)
            {
                SpawnBloodSquibBurst(x, y, directionDegrees, burstCount, explosive);
                return;
            }

            var splashCount = explosive
                ? _context.ScaleBloodVisualCount(Math.Clamp(Math.Max(4, burstCount), 4, 10))
                : _context.ScaleBloodVisualCount(1);
            for (var index = 0; index < splashCount; index += 1)
            {
                var spreadDegrees = explosive
                    ? _context._visualRandom.NextSingle() * 360f
                    : directionDegrees + (_context._visualRandom.NextSingle() * 6f) - 3f;
                var spreadRadians = spreadDegrees * (MathF.PI / 180f);
                var distance = splashCount > 1 ? _context._visualRandom.NextSingle() * (explosive ? 8f : 1f) : 0f;
                _context._bloodVisuals.Add(new BloodVisual(
                    x + MathF.Cos(spreadRadians) * distance,
                    y + MathF.Sin(spreadRadians) * distance));
            }

            var sprayCount = explosive
                ? _context.ScaleBloodVisualCount(Math.Clamp(Math.Max(8, burstCount * 2), 8, 16))
                : _context.ScaleBloodVisualCount(1);
            for (var index = 0; index < sprayCount; index += 1)
            {
                var spreadDegrees = explosive
                    ? _context._visualRandom.NextSingle() * 360f
                    : directionDegrees + (_context._visualRandom.NextSingle() * 6f) - 3f;
                var spreadRadians = spreadDegrees * (MathF.PI / 180f);
                var speed = explosive
                    ? 4.5f + (_context._visualRandom.NextSingle() * 10f)
                    : 2.5f + (_context._visualRandom.NextSingle() * 3f);
                _context._bloodSprayVisuals.Add(new BloodSprayVisual(
                    x,
                    y,
                    MathF.Cos(spreadRadians) * speed,
                    MathF.Sin(spreadRadians) * speed,
                    explosive ? _context._visualRandom.Next(26, 48) : _context._visualRandom.Next(16, 30)));
            }
        }

        private void SpawnGibBloodImpactVisuals(float x, float y, int intensity)
        {
            TryApplyStickyGibBloodCoating(x, y, intensity);

            if (!_context.AreBloodVisualsEnabled)
            {
                return;
            }

            if (_context._bloodRenderMode == 0)
            {
                SpawnBloodSquibGibBurst(x, y, intensity);
                return;
            }

            var burstCount = _context.ScaleBloodVisualCount(Math.Clamp(8 + (intensity * 2), 8, 16));
            for (var index = 0; index < burstCount; index += 1)
            {
                var directionRadians = _context._visualRandom.NextSingle() * MathF.Tau;
                var distance = _context._visualRandom.NextSingle() * 8f;
                _context._bloodVisuals.Add(new BloodVisual(
                    x + MathF.Cos(directionRadians) * distance,
                    y + MathF.Sin(directionRadians) * distance));
            }

            var sprayCount = _context.ScaleBloodVisualCount(Math.Clamp(10 + (intensity * 3), 10, 20));
            for (var index = 0; index < sprayCount; index += 1)
            {
                var directionRadians = _context._visualRandom.NextSingle() * MathF.Tau;
                var speed = 5f + (_context._visualRandom.NextSingle() * 12f);
                var startRadius = _context._visualRandom.NextSingle() * 5f;
                _context._bloodSprayVisuals.Add(new BloodSprayVisual(
                    x + MathF.Cos(directionRadians) * startRadius,
                    y + MathF.Sin(directionRadians) * startRadius,
                    MathF.Cos(directionRadians) * speed,
                    MathF.Sin(directionRadians) * speed,
                    _context._visualRandom.Next(26, 48)));
            }
        }

        private void TryApplyStickyGibBloodCoating(float x, float y, int intensity)
        {
            if (!_context.IsPracticeSessionActive || !_context._practiceStickyGibBloodEnabled)
            {
                return;
            }

            var baseRadius = 18f + (Math.Max(1, intensity) * 4f);
            foreach (var player in _context.EnumerateRenderablePlayers())
            {
                if (!player.IsAlive || _context.GetPlayerVisibilityAlpha(player) <= 0f)
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
            if (!_context._stickyGibBloodCoatings.TryGetValue(player.Id, out var coating))
            {
                coating = new StickyGibBloodCoating();
                _context._stickyGibBloodCoatings[player.Id] = coating;
            }

            coating.TicksRemaining = Math.Max(
                1,
                (int)MathF.Round(StickyGibBloodCoating.LifetimeTicks * _context.GetBloodPersistenceScale()));
            coating.Intensity = Math.Clamp(
                Math.Max(coating.Intensity, 0.42f) + (Math.Min(4, Math.Max(1, intensity)) * 0.08f),
                0.42f,
                1f);
        }

        private bool TryGetStickyGibBloodTargetPlayer(float x, float y, float scale, out PlayerEntity coatedPlayer)
        {
            var padding = 2f + (scale * 3f);
            foreach (var player in _context.EnumerateRenderablePlayers())
            {
                if (!player.IsAlive || _context.GetPlayerVisibilityAlpha(player) <= 0f)
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

            coatedPlayer = _context._world.LocalPlayer;
            return false;
        }

        private bool TryGetBackstabOwnerPosition(int ownerId, out Vector2 ownerPosition)
        {
            if (ownerId == 0)
            {
                ownerPosition = default;
                return false;
            }

            var owner = _context.FindPlayerById(ownerId);
            if (owner is null || !owner.IsAlive || owner.ClassId != PlayerClass.Spy)
            {
                ownerPosition = default;
                return false;
            }

            ownerPosition = _context.GetRenderPosition(owner);
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
