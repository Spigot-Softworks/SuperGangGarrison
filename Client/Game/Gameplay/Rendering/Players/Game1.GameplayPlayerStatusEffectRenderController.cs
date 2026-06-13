#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayPlayerStatusEffectRenderController
    {
        private readonly Game1 _game;

        public GameplayPlayerStatusEffectRenderController(Game1 game)
        {
            _game = game;
        }

        private static readonly Color CivvieUmbrellaShieldBarColor = new(226, 188, 92);

        public void TryDrawAdditionalHealthBar(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha)
        {
            var forceSpecialEnemyHealthBar = _game.ShouldForceLastToDieSpecialEnemyHealthBar(player);
            var forcePracticeCombatDummyHealthBar = _game._world.IsPracticeCombatDummy(player);
            if ((!_game._showHealthBarEnabled && !forceSpecialEnemyHealthBar && !forcePracticeCombatDummyHealthBar)
                || visibilityAlpha <= 0f
                || (!ReferenceEquals(player, _game._world.LocalPlayer)
                    && player.Team != _game._world.LocalPlayer.Team
                    && !forceSpecialEnemyHealthBar
                    && !forcePracticeCombatDummyHealthBar))
            {
                return;
            }

            _game.DrawHealthBar(player, cameraPosition, new Color(105, 215, 95), Color.Black, Color.Black);
        }

        public void TryDrawCivvieUmbrellaShieldBar(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha)
        {
            if (!_game._showShieldBarEnabled
                || visibilityAlpha <= 0f
                || !player.IsAlive
                || !_game.GetPlayerIsCivvieUmbrellaActive(player)
                || (!ReferenceEquals(player, _game._world.LocalPlayer)
                    && player.Team != _game._world.LocalPlayer.Team))
            {
                return;
            }

            var fillFraction = player.CivvieUmbrellaChargeTicks / (float)PlayerEntity.CivvieUmbrellaMaxChargeTicks;
            _game.DrawShieldBar(player, cameraPosition, fillFraction, CivvieUmbrellaShieldBarColor, Color.Black, Color.Black);
        }

        public void DrawExperimentalDemoknightChargeBlur(PlayerEntity player, Vector2 cameraPosition, Color spriteTint, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
        {
            var ghostTrailAlpha = player.IsExperimentalGhostDashVisible
                ? float.Clamp(player.ExperimentalGhostDashTrailAlpha, 0f, 1f)
                : 0f;
            var drawGhostTrail = ghostTrailAlpha > 0.02f;
            if ((!player.IsExperimentalDemoknightCharging && !drawGhostTrail) || visibilityAlpha <= 0.05f)
            {
                return;
            }

            var renderPosition = _game.GetRenderPosition(player);
            var blurDirection = GetExperimentalDemoknightChargeBlurDirection(player);
            if (blurDirection.LengthSquared() <= 0.0001f)
            {
                return;
            }

            var blurCount = drawGhostTrail ? Math.Max(1, (int)MathF.Ceiling(7f * ghostTrailAlpha)) : 2;
            var blurTint = drawGhostTrail
                ? Color.Lerp(spriteTint, new Color(200, 220, 235), 0.62f)
                : spriteTint * 0.45f;
            for (var blurIndex = 0; blurIndex < blurCount; blurIndex += 1)
            {
                var offsetDistance = drawGhostTrail
                    ? 4f + (ghostTrailAlpha * 3f) + (blurIndex * 9f)
                    : 5f + (blurIndex * 6f);
                var blurPosition = renderPosition - blurDirection * offsetDistance;
                var blurAlpha = visibilityAlpha * (drawGhostTrail
                    ? ghostTrailAlpha * MathF.Max(0.16f, 0.56f - (blurIndex * 0.065f))
                    : (blurIndex == 0 ? 0.18f : 0.1f));
                var tint = blurTint * blurAlpha;
                _game.TryDrawPlayerSpriteAtPosition(player, blurPosition, cameraPosition, tint, bodySelection, drawIntelOverlay: false);
                if (ShouldDrawStatusWeaponSprite(player, bodySelection))
                {
                    _game.TryDrawWeaponSpriteAtPosition(player, blurPosition, cameraPosition, tint, 1f, bodySelection);
                }
            }
        }

        public void DrawCapturedPointHealingGhosting(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
        {
            if (!_game._world.IsPlayerInsideCapturedPointHealingAuraForVisuals(player) || visibilityAlpha <= 0.05f)
            {
                return;
            }

            var blurDirection = GetExperimentalDemoknightChargeBlurDirection(player);
            if (blurDirection.LengthSquared() <= 0.0001f)
            {
                blurDirection = new Vector2(player.FacingDirectionX, 0f);
            }

            var pulse = 0.5f + (0.5f * MathF.Sin((float)(_game._world.Frame * 0.16f) + player.Id));
            var blurTint = new Color(150, 245, 165);
            for (var blurIndex = 0; blurIndex < 2; blurIndex += 1)
            {
                var offsetDistance = 2f + (blurIndex * 2.5f);
                var blurPosition = renderPosition - blurDirection * offsetDistance;
                var blurAlpha = visibilityAlpha * ((blurIndex == 0 ? 0.12f : 0.07f) + (pulse * 0.05f));
                var tint = blurTint * blurAlpha;
                _game.TryDrawPlayerSpriteAtPosition(player, blurPosition, cameraPosition, tint, bodySelection, drawIntelOverlay: false);
                if (ShouldDrawStatusWeaponSprite(player, bodySelection))
                {
                    _game.TryDrawWeaponSpriteAtPosition(player, blurPosition, cameraPosition, tint, 1f, bodySelection);
                }
            }
        }

        public void DrawExperimentalCryoOverlays(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
        {
            if (!player.IsAlive
                || visibilityAlpha <= 0.05f
                || (!player.IsExperimentalCryoSlowed && !player.IsExperimentalCryoFrozen))
            {
                return;
            }

            var cryoProgress = player.IsExperimentalCryoFrozen
                ? 1f
                : Math.Clamp(player.ExperimentalCryoExposureFraction, 0f, 1f);
            var overlayAlpha = player.IsExperimentalCryoFrozen
                ? 0.72f
                : MathHelper.Lerp(0.3f, 0.62f, cryoProgress);
            var overlayTint = Color.Lerp(new Color(142, 212, 255), new Color(186, 234, 255), cryoProgress)
                * (visibilityAlpha * overlayAlpha);
            _game.TryDrawPlayerSpriteAtPosition(player, renderPosition, cameraPosition, overlayTint, bodySelection, drawIntelOverlay: false);
            if (ShouldDrawStatusWeaponSprite(player, bodySelection))
            {
                _game.TryDrawWeaponSpriteAtPosition(player, renderPosition, cameraPosition, overlayTint, 1f, bodySelection);
            }

            if (!player.IsExperimentalCryoFrozen)
            {
                return;
            }

            var frozenBounds = Game1.GetPlayerScreenBounds(player, renderPosition, cameraPosition);
            frozenBounds.Inflate(4, 6);
            _game._spriteBatch.Draw(_game._pixel, frozenBounds, new Color(118, 198, 255, 184) * visibilityAlpha);
        }

        public void DrawExperimentalEssenceExtractorOverlay(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
        {
            if (!player.IsAlive
                || visibilityAlpha <= 0.05f
                || (!player.IsExperimentalEngineerEssenceExtractorAffected && !player.IsExperimentalGravitonAffected))
            {
                return;
            }

            if (player.IsExperimentalEngineerEssenceExtractorAffected)
            {
                DrawExperimentalStatusOverlayPass(
                    player,
                    renderPosition,
                    cameraPosition,
                    new Color(220, 220, 220) * (visibilityAlpha * 0.78f),
                    bodySelection);
                return;
            }

            DrawExperimentalStatusOverlayPass(
                player,
                renderPosition,
                cameraPosition,
                new Color(88, 88, 104) * (visibilityAlpha * 0.42f),
                bodySelection);
        }

        private void DrawExperimentalStatusOverlayPass(
            PlayerEntity player,
            Vector2 renderPosition,
            Vector2 cameraPosition,
            Color overlayTint,
            PlayerBodySpriteSelection bodySelection)
        {
            _game.TryDrawPlayerSpriteAtPosition(player, renderPosition, cameraPosition, overlayTint, bodySelection, drawIntelOverlay: false);
            if (ShouldDrawStatusWeaponSprite(player, bodySelection))
            {
                _game.TryDrawWeaponSpriteAtPosition(player, renderPosition, cameraPosition, overlayTint, 1f, bodySelection);
            }
        }

        private bool ShouldDrawStatusWeaponSprite(PlayerEntity player, PlayerBodySpriteSelection bodySelection)
        {
            return !_game.GetPlayerIsHeavyEating(player)
                && !player.IsTaunting
                && !_game._world.IsPlayerHumiliated(player);
        }

        public Color GetPlayerColor(PlayerEntity player, Color baseColor)
        {
            return baseColor * _game.GetPlayerVisibilityAlpha(player);
        }

        public void DrawAfterburnOverlay(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha)
        {
            if (!player.IsAlive || !player.IsBurning)
            {
                return;
            }

            DrawNapalmCoveredOverlay(player, renderPosition, cameraPosition, visibilityAlpha);

            var burnAlpha = player.BurnVisualAlpha;
            if (burnAlpha <= 0f || visibilityAlpha <= 0f)
            {
                return;
            }

            var count = player.BurnVisualCount;
            if (count <= 0)
            {
                return;
            }

            var sourceFrame = (int)((_game._world.Frame * LegacyMovementModel.SourceTicksPerSecond) / _game._config.TicksPerSecond);
            if (_game._flameRenderMode == 0)
            {
                var cells = new System.Collections.Generic.Dictionary<(int, int), float>();
                var baseCount = player.BurnVisualBaseCount;
                var burnRatio = baseCount > 0 ? (float)count / baseCount : 0f;
                var lowBurnScaleBoost = MathHelper.Lerp(1.45f, 1f, Math.Clamp(burnRatio, 0f, 1f));
                var basePrimaryScale = lowBurnScaleBoost;
                var minOutlineScale = 0.65f * lowBurnScaleBoost;
                var outlineScaleRange = 0.25f * lowBurnScaleBoost;
                var facingScale = MathF.Cos(MathF.PI * player.AimDirectionDegrees / 180f) < 0f ? -1f : 1f;
                var targetBackTopT = facingScale > 0f ? 0.25f : 0.75f;
                var outlineHalfW = player.Width * 0.5f;
                var outlineHalfH = player.Height * 0.5f;

                for (var flameIndex = 0; flameIndex < count; flameIndex += 1)
                {
                    var uniformT = (flameIndex + 0.5f) / count;
                    var clusterT = targetBackTopT + ((_game._visualRandom.NextSingle() * 2f - 1f) * 0.30f);
                    var t = Math.Clamp(MathHelper.Lerp(uniformT, clusterT, 0.45f), 0.01f, 0.99f);
                    var angle = MathF.PI + (t * MathF.PI);
                    var offsetX = MathF.Cos(angle) * outlineHalfW;
                    var offsetY = MathF.Sin(angle) * outlineHalfH;
                    var seed = ComputeNapalmVisualHash(player.Id, flameIndex, axis: 11);
                    _game.AccumulateProceduralFlameParticle(
                        cells,
                        seed,
                        renderPosition.X + offsetX,
                        renderPosition.Y + offsetY,
                        scale: basePrimaryScale,
                        alphaScale: burnAlpha,
                        motionX: player.HorizontalSpeed,
                        motionY: player.VerticalSpeed);
                }

                // Primary outline flames distributed around the sprite silhouette ellipse,
                // scaling in count with burn intensity.
                var effectiveAreaRatio = burnRatio <= 0f ? 0f : MathF.Max(burnRatio, 0.4f);
                var outlineCount = (int)MathF.Round(effectiveAreaRatio * baseCount * 3f);
                if (outlineCount > 0)
                {
                    outlineCount = Math.Max(outlineCount, 6);
                }
                for (var outlineIndex = 0; outlineIndex < outlineCount; outlineIndex += 1)
                {
                    // Keep upper-half coverage, but bias toward the upper back corner.
                    var uniformT = (outlineIndex + 0.5f) / outlineCount;
                    var clusterT = targetBackTopT + ((_game._visualRandom.NextSingle() * 2f - 1f) * 0.34f);
                    var t = Math.Clamp(MathHelper.Lerp(uniformT, clusterT, 0.35f), 0.01f, 0.99f);
                    var angle = MathF.PI + (t * MathF.PI);
                    var outlineX = renderPosition.X + MathF.Cos(angle) * outlineHalfW;
                    var outlineY = renderPosition.Y + MathF.Sin(angle) * outlineHalfH;
                    var outlineSeed = ComputeNapalmVisualHash(player.Id, outlineIndex, axis: 23);
                    _game.AccumulateProceduralFlameParticle(
                        cells,
                        outlineSeed,
                        outlineX,
                        outlineY,
                        scale: minOutlineScale + _game._visualRandom.NextSingle() * outlineScaleRange,
                        alphaScale: burnAlpha * 0.75f,
                        motionX: player.HorizontalSpeed,
                        motionY: player.VerticalSpeed);
                }

                // Secondary rising flame embers sourced from existing flame cells.
                // Accumulated into the same dict so they combine with primary flames during coloring.
                const float flameCellSize = 2f;
                var cellKeys = new System.Collections.Generic.List<(int, int)>(cells.Keys);
                foreach (var (gx, gy) in cellKeys)
                {
                    if (_game._visualRandom.NextSingle() >= 0.3f)
                    {
                        continue;
                    }
                    var emitX = gx * flameCellSize + flameCellSize * 0.5f;
                    var emitY = gy * flameCellSize + flameCellSize * 0.5f;
                    var emitSeed = ComputeNapalmVisualHash(player.Id, unchecked(gx * 1234 + gy * 5678), axis: 17);
                    var emitScale = 0.35f + _game._visualRandom.NextSingle() * 0.25f;
                    _game.AccumulateProceduralFlameParticle(
                        cells,
                        emitSeed,
                        emitX,
                        emitY,
                        scale: emitScale,
                        alphaScale: burnAlpha * 0.65f,
                        motionX: 0f,
                        motionY: -3f - _game._visualRandom.NextSingle() * 2f);

                    if (_game._visualRandom.NextSingle() < 0.25f)
                    {
                        _game._flameSmokeVisuals.Add(new FlameSmokeVisual(
                            emitX,
                            emitY,
                            (_game._visualRandom.NextSingle() * 4f - 2f),
                            (_game._visualRandom.NextSingle() * 4f - 2f),
                            (_game._visualRandom.NextSingle() * 4f - 2f),
                            -2f - _game._visualRandom.NextSingle() * 3f,
                            2f + _game._visualRandom.NextSingle() * 2f,
                            6f + _game._visualRandom.NextSingle() * 6f,
                            0.35f + _game._visualRandom.NextSingle() * 0.2f,
                            18 + _game._visualRandom.Next(14)));
                    }
                }

                _game.DrawProceduralFlameParticles(cells, cameraPosition, topOutlineOnly: true, drawAlpha: visibilityAlpha);
                return;
            }

            var sprite = _game.GetResolvedSprite("FlameS");
            var flameColor = Color.White * (burnAlpha * visibilityAlpha);
            for (var flameIndex = 0; flameIndex < count; flameIndex += 1)
            {
                player.GetBurnVisualOffset(flameIndex, sourceFrame, out var offsetX, out var offsetY);
                if (sprite is not null && sprite.Frames.Count > 0)
                {
                    var frameIndex = player.GetBurnVisualFrameIndex(flameIndex, sourceFrame, sprite.Frames.Count);
                    _game.DrawLoadedSpriteFrame(sprite.Frames[frameIndex], new Vector2(renderPosition.X + offsetX - cameraPosition.X, renderPosition.Y + offsetY - cameraPosition.Y), null, flameColor, 0f, sprite.Origin.ToVector2(), Vector2.One, SpriteEffects.None, 0f);
                    continue;
                }

                var flameRectangle = new Rectangle((int)(renderPosition.X + offsetX - 2f - cameraPosition.X), (int)(renderPosition.Y + offsetY - 2f - cameraPosition.Y), 4, 4);
                _game._spriteBatch.Draw(_game._pixel, flameRectangle, flameColor);
            }
        }

        private void DrawNapalmCoveredOverlay(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha)
        {
            var alpha = player.NapalmCoveredVisualAlpha * visibilityAlpha;
            if (alpha <= 0f)
            {
                return;
            }

            var sourceFrame = (int)((_game._world.Frame * LegacyMovementModel.SourceTicksPerSecond) / _game._config.TicksPerSecond);
            var count = Math.Max(8, player.BurnVisualBaseCount + 6);
            var color = new Color(14, 10, 8) * alpha;
            var streakColor = new Color(32, 24, 18) * (alpha * 0.75f);
            for (var dripIndex = 0; dripIndex < count; dripIndex += 1)
            {
                var xSeed = ComputeNapalmVisualHash(player.Id, dripIndex, axis: 0);
                var ySeed = ComputeNapalmVisualHash(player.Id, dripIndex, axis: 1);
                var speedSeed = ComputeNapalmVisualHash(player.Id, dripIndex, axis: 2);
                var sizeSeed = ComputeNapalmVisualHash(player.Id, dripIndex, axis: 3);
                var normalizedX = ((uint)xSeed / (float)uint.MaxValue) * 2f - 1f;
                var startY = ((uint)ySeed / (float)uint.MaxValue) * player.Height * 0.55f - (player.Height * 0.5f);
                var fallTicks = 18 + Math.Abs(speedSeed % 17);
                var fallProgress = PositiveModulo(sourceFrame + speedSeed, fallTicks) / (float)fallTicks;
                var dripX = renderPosition.X + normalizedX * player.Width * 0.42f;
                var dripY = renderPosition.Y + startY + (fallProgress * player.Height * 0.7f);
                var size = 3 + Math.Abs(sizeSeed % 4);
                var dripRectangle = new Rectangle(
                    (int)(dripX - cameraPosition.X),
                    (int)(dripY - cameraPosition.Y),
                    size,
                    size);
                _game._spriteBatch.Draw(_game._pixel, dripRectangle, color);

                var streakHeight = 2 + Math.Abs((sizeSeed / 3) % 5);
                var streakRectangle = new Rectangle(
                    dripRectangle.X,
                    dripRectangle.Y + size - 1,
                    Math.Max(1, size - 1),
                    streakHeight);
                _game._spriteBatch.Draw(_game._pixel, streakRectangle, streakColor);
            }
        }

        public void DrawDominationIndicator(PlayerEntity player, Vector2 cameraPosition, float visibilityAlpha)
        {
            if (!player.IsDominatingLocalViewer || visibilityAlpha <= 0f)
            {
                return;
            }

            var renderPosition = _game.GetRenderPosition(player);
            var frameIndex = player.Team == PlayerTeam.Blue ? 1 : 0;
            _game.DrawCenteredHudSprite("DominationS", frameIndex, new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y - cameraPosition.Y - 35f), Color.White * visibilityAlpha, Vector2.One);
        }

        public static Color GetUberOverlayColor(PlayerTeam team)
        {
            return team == PlayerTeam.Blue ? new Color(56, 136, 255) : new Color(255, 32, 32);
        }

        public Vector2 GetExperimentalDemoknightChargeBlurDirection(PlayerEntity player)
        {
            return GetExperimentalDemoknightChargeBlurDirectionCore(player);
        }

        private Vector2 GetExperimentalDemoknightChargeBlurDirectionCore(PlayerEntity player)
        {
            var velocity = ReferenceEquals(player, _game._world.LocalPlayer) && _game._hasPredictedLocalPlayerPosition
                ? _game._predictedLocalPlayerVelocity
                : new Vector2(player.HorizontalSpeed, player.VerticalSpeed);
            if (velocity.LengthSquared() <= 0.01f)
            {
                velocity = new Vector2(player.FacingDirectionX, 0f);
            }

            if (velocity.LengthSquared() <= 0.01f)
            {
                return Vector2.Zero;
            }

            velocity.Normalize();
            return velocity;
        }

        private static int ComputeNapalmVisualHash(int playerId, int dripIndex, int axis)
        {
            var hash = playerId;
            hash = unchecked((hash * 397) ^ dripIndex);
            hash = unchecked((hash * 397) ^ axis);
            return hash;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            if (modulus <= 0)
            {
                return 0;
            }

            var result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }
}
