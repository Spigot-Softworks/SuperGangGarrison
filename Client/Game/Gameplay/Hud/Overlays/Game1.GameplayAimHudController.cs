#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    internal const string ContinuousCrosshairSpriteName = "CrosshairContinuousS";
    internal const int ContinuousCrosshairActiveFrameCount = 10;
    internal const int ContinuousCrosshairIdleFrameIndex = ContinuousCrosshairActiveFrameCount;
    internal const int RechargeCrosshairIdleFrameIndex = 0;
    internal const int RechargeCrosshairActiveFrameOffset = 1;
    internal const int RechargeCrosshairActiveFrameCount = 9;

    internal const int SniperChargeHudFillMaxWidth = 40;

    internal static bool IsContinuousCrosshairWeapon(PrimaryWeaponDefinition weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return weapon.Kind is PrimaryWeaponKind.Minigun or PrimaryWeaponKind.FlameThrower;
    }

    internal static int GetCrosshairFrameIndex(
        PrimaryWeaponDefinition weapon,
        int cooldownTicks,
        int reloadTicks,
        int currentAmmo = -1,
        int maxAmmo = -1,
        bool isFireHeld = false)
    {
        ArgumentNullException.ThrowIfNull(weapon);

        var remainingTicks = Math.Max(0, Math.Max(cooldownTicks, reloadTicks));
        if (IsContinuousCrosshairWeapon(weapon))
        {
            return GetContinuousCrosshairFrameIndex(
                currentAmmo,
                maxAmmo,
                isFireHeld || remainingTicks > 0);
        }

        if (remainingTicks <= 0)
        {
            return RechargeCrosshairIdleFrameIndex;
        }

        var durationTicks = Math.Max(1, Math.Max(weapon.ReloadDelayTicks, weapon.AmmoReloadTicks));
        var elapsedFraction = Math.Clamp(
            1f - (remainingTicks / (float)durationTicks),
            0f,
            1f);
        return RechargeCrosshairActiveFrameOffset + Math.Clamp(
            (int)MathF.Floor(elapsedFraction * RechargeCrosshairActiveFrameCount),
            0,
            RechargeCrosshairActiveFrameCount - 1);
    }

    internal static int GetContinuousCrosshairFrameIndex(int currentAmmo, int maxAmmo, bool isActive)
    {
        if (!isActive || maxAmmo <= 0)
        {
            return ContinuousCrosshairIdleFrameIndex;
        }

        var ammoFraction = Math.Clamp(currentAmmo / (float)maxAmmo, 0f, 1f);
        return Math.Clamp(
            (int)MathF.Floor((1f - ammoFraction) * ContinuousCrosshairActiveFrameCount),
            0,
            ContinuousCrosshairActiveFrameCount - 1);
    }

    internal static int GetSniperChargeHudFillWidthForTicks(
        int chargeTicks,
        int fullChargeTicks = PlayerEntity.SniperChargeMaxTicks)
    {
        if (chargeTicks <= 0)
        {
            return 0;
        }

        return Math.Clamp(
            (int)MathF.Ceiling(chargeTicks * (SniperChargeHudFillMaxWidth / (float)Math.Max(1, fullChargeTicks))),
            0,
            SniperChargeHudFillMaxWidth);
    }

    internal static int GetSniperBowChargeHudFillWidthForTicks(
        int chargeTicks,
        int fullChargeTicks = PlayerEntity.SniperBowMaxChargeTicks)
    {
        if (chargeTicks <= 0)
        {
            return 0;
        }

        return Math.Clamp(
            (int)MathF.Ceiling(chargeTicks * (SniperChargeHudFillMaxWidth / (float)Math.Max(1, fullChargeTicks))),
            0,
            SniperChargeHudFillMaxWidth);
    }

    private sealed class GameplayAimHudController
    {
        private readonly Game1 _game;

        public GameplayAimHudController(Game1 game)
        {
            _game = game;
        }

        public void DrawSniperHud(Vector2 screenAimPosition)
        {
            var localPlayer = _game._world.LocalPlayer;
            if (_game.GetPlayerIsSniperBowEquipped(localPlayer))
            {
                DrawSniperBowChargeHud(localPlayer, screenAimPosition);
                return;
            }

            if (!localPlayer.HasScopedSniperWeaponEquipped || !_game.GetPlayerIsSniperScoped(localPlayer))
            {
                return;
            }

            DrawSniperChargeHud(localPlayer, screenAimPosition);
        }

        public void DrawSpectatorSniperHud(PlayerEntity player, Vector2 screenAimPosition)
        {
            if (player.IsSniperBowEquipped)
            {
                DrawSniperBowChargeHud(player, screenAimPosition);
                return;
            }

            if (!player.HasScopedSniperWeaponEquipped || !_game.GetPlayerIsSniperScoped(player))
            {
                return;
            }

            DrawSniperChargeHud(player, screenAimPosition);
        }

        private void DrawSniperBowChargeHud(PlayerEntity player, Vector2 screenAimPosition)
        {
            var chargeTicks = _game.GetPlayerSniperBowChargeTicks(player);
            if (chargeTicks <= 0)
            {
                return;
            }

            var facingLeft = IsFacingLeftByAim(player);
            var chargeScaleX = facingLeft ? 1f : -1f;
            var chargePosition = screenAimPosition + new Vector2(15f * chargeScaleX, -10f);
            var isFullyCharged = chargeTicks >= player.LastToDieSniperBowFullChargeTicks;
            if (!isFullyCharged)
            {
                _game.TryDrawScreenSprite("ChargeS", 0, chargePosition, Color.White * 0.25f, new Vector2(chargeScaleX, 1f));
            }
            else
            {
                _game.TryDrawScreenSprite("FullChargeS", 0, screenAimPosition + new Vector2(65f * chargeScaleX, 0f), Color.White, Vector2.One);
            }

            var chargeWidth = GetSniperBowChargeHudFillWidthForTicks(
                chargeTicks,
                player.LastToDieSniperBowFullChargeTicks);
            if (chargeWidth <= 0)
            {
                return;
            }

            DrawSniperChargeFill(chargePosition, chargeWidth, facingLeft);
        }

        private void DrawSniperChargeHud(PlayerEntity player, Vector2 screenAimPosition)
        {
            var damage = _game.GetPlayerSniperRifleDamage(player);
            var facingLeft = IsFacingLeftByAim(player);
            var chargeScaleX = facingLeft ? 1f : -1f;
            var chargePosition = screenAimPosition + new Vector2(15f * chargeScaleX, -10f);
            if (damage < 85)
            {
                _game.TryDrawScreenSprite("ChargeS", 0, chargePosition, Color.White * 0.25f, new Vector2(chargeScaleX, 1f));
            }
            else
            {
                _game.TryDrawScreenSprite("FullChargeS", 0, screenAimPosition + new Vector2(65f * chargeScaleX, 0f), Color.White, Vector2.One);
            }

            var chargeWidth = GetSniperChargeHudFillWidthForTicks(
                _game.GetPlayerSniperChargeTicks(player),
                player.LastToDieSniperRifleFullChargeTicks);
            if (chargeWidth <= 0)
            {
                return;
            }

            DrawSniperChargeFill(chargePosition, chargeWidth, facingLeft);
        }

        private void DrawSniperChargeFill(Vector2 chargePosition, int chargeWidth, bool facingLeft)
        {
            var sprite = _game.GetResolvedSprite("ChargeS");
            if (sprite is null || sprite.Frames.Count <= 1)
            {
                return;
            }

            var frame = sprite.Frames[1];
            chargeWidth = Math.Clamp(chargeWidth, 0, frame.Width);
            if (chargeWidth <= 0)
            {
                return;
            }

            if (facingLeft)
            {
                TryDrawSniperChargeFillPart(
                    "ChargeS",
                    1,
                    new Rectangle(0, 0, chargeWidth, frame.Height),
                    chargePosition,
                    Color.White * 0.8f);
                return;
            }

            // Background uses negative X scale from chargePosition, so it extends left.
            // Place the flipped fill so it occupies the same leftward region.
            var drawPosition = new Vector2(chargePosition.X - chargeWidth, chargePosition.Y);
            TryDrawSniperChargeFillPart(
                "ChargeS",
                1,
                new Rectangle(0, 0, chargeWidth, frame.Height),
                drawPosition,
                Color.White * 0.8f,
                SpriteEffects.FlipHorizontally);
        }

        private bool TryDrawSniperChargeFillPart(string spriteName, int frameIndex, Rectangle sourceRectangle, Vector2 topLeftPosition, Color tint)
        {
            return TryDrawSniperChargeFillPart(spriteName, frameIndex, sourceRectangle, topLeftPosition, tint, SpriteEffects.None);
        }

        private bool TryDrawSniperChargeFillPart(string spriteName, int frameIndex, Rectangle sourceRectangle, Vector2 topLeftPosition, Color tint, SpriteEffects effects)
        {
            var sprite = _game.GetResolvedSprite(spriteName);
            if (sprite is null || frameIndex < 0 || frameIndex >= sprite.Frames.Count)
            {
                return false;
            }

            _game.DrawLoadedSpriteFrame(
                sprite.Frames[frameIndex],
                topLeftPosition,
                sourceRectangle,
                tint,
                0f,
                Vector2.Zero,
                Vector2.One,
                effects,
                0f);
            return true;
        }

        public void DrawCrosshair(Vector2 screenPosition)
        {
            var weapon = _game.GetLocalDisplayedMainWeaponStats();
            var cooldownTicks = _game.GetLocalDisplayedMainWeaponCooldownTicks();
            var reloadTicks = _game.GetLocalDisplayedMainWeaponReloadTicks();
            var currentAmmo = _game.GetLocalDisplayedMainWeaponCurrentShells();
            var maxAmmo = _game.GetLocalDisplayedMainWeaponMaxShells();
            var spriteName = IsContinuousCrosshairWeapon(weapon)
                ? ContinuousCrosshairSpriteName
                : "CrosshairS";
            var frameIndex = GetCrosshairFrameIndex(
                weapon,
                cooldownTicks,
                reloadTicks,
                currentAmmo,
                maxAmmo,
                _game._latestPredictedLocalInput.FirePrimary);
            var crosshair = _game.GetResolvedSprite(spriteName);
            if (crosshair is null || crosshair.Frames.Count == 0)
            {
                return;
            }

            frameIndex = Math.Clamp(frameIndex, 0, crosshair.Frames.Count - 1);
            var cursorScale = ClientSettings.GetCursorScale(_game._cursorSizePercent);
            _game.DrawLoadedSpriteFrame(
                crosshair.Frames[frameIndex],
                screenPosition,
                null,
                Color.White,
                0f,
                crosshair.Origin.ToVector2(),
                new Vector2(cursorScale, cursorScale),
                SpriteEffects.None,
                0f);
        }

        public void DrawControllerAimLine(Vector2 cameraPosition, Vector2 screenAimPosition)
        {
            if (!_game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            var playerScreenPosition = _game.GetRenderPosition(_game._world.LocalPlayer) - cameraPosition;
            var delta = screenAimPosition - playerScreenPosition;
            var length = delta.Length();
            if (length <= 1f)
            {
                return;
            }

            var direction = delta / length;
            var lineStart = playerScreenPosition + (direction * MathF.Min(12f, length * 0.25f));
            var lineEnd = screenAimPosition;
            var lineLength = (lineEnd - lineStart).Length();
            if (lineLength <= 1f)
            {
                return;
            }

            const int segments = 12;
            const float fadeStart = 0.58f;
            for (var index = 0; index < segments; index += 1)
            {
                var segmentStartT = index / (float)segments;
                var segmentEndT = (index + 1) / (float)segments;
                var segmentStart = Vector2.Lerp(lineStart, lineEnd, segmentStartT);
                var segmentEnd = Vector2.Lerp(lineStart, lineEnd, segmentEndT);
                var fadeT = Math.Clamp((segmentEndT - fadeStart) / (1f - fadeStart), 0f, 1f);
                var alpha = MathHelper.Lerp(0.9f, 0f, fadeT);
                if (alpha <= 0.01f)
                {
                    continue;
                }

                DrawScreenLine(segmentStart, segmentEnd, Color.White * alpha, 1f);
            }
        }

        private void DrawScreenLine(Vector2 start, Vector2 end, Color color, float thickness)
        {
            var delta = end - start;
            var length = delta.Length();
            if (length <= 0f)
            {
                return;
            }

            var angle = MathF.Atan2(delta.Y, delta.X);
            _game._spriteBatch.Draw(_game._pixel, start, null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }
    }
}
