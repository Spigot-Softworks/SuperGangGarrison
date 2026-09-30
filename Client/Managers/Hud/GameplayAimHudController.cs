#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public partial class Game1
{
    internal const string ContinuousCrosshairSpriteName = "CrosshairContinuousS";
    internal const int ContinuousCrosshairActiveFrameCount = 10;
    internal const int ContinuousCrosshairDisabledFrameIndex = ContinuousCrosshairActiveFrameCount;
    internal const int RechargeCrosshairIdleFrameIndex = 0;
    internal const int RechargeCrosshairActiveFrameOffset = 1;
    internal const int RechargeCrosshairActiveFrameCount = 9;
    internal const int RechargeCrosshairRefireLastFrameIndex = 4;

    internal readonly record struct CrosshairFrame(string SpriteName, int FrameIndex);

    internal const int SniperChargeHudFillMaxWidth = 40;

    internal static bool IsContinuousCrosshairWeapon(PrimaryWeaponDefinition weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return weapon.Kind is PrimaryWeaponKind.Minigun or PrimaryWeaponKind.FlameThrower or PrimaryWeaponKind.Medigun
            || (weapon.Kind == PrimaryWeaponKind.PelletGun && weapon.RefillsAllAtOnce)
            || (CharacterClassCatalog.RuntimeRegistry.TryGetItem(weapon.ItemId, out var item)
                && item.BehaviorId == BuiltInGameplayBehaviorIds.Needlegun);
    }

    internal static CrosshairFrame GetCrosshairFrame(
        PrimaryWeaponDefinition weapon,
        int cooldownTicks,
        int reloadTicks,
        int currentAmmo = -1,
        int maxAmmo = -1,
        bool isFireHeld = false,
        bool isFireBlocked = false,
        int? cooldownDurationTicks = null,
        int? reloadDurationTicks = null)
    {
        ArgumentNullException.ThrowIfNull(weapon);

        if (IsContinuousCrosshairWeapon(weapon))
        {
            // Flamethrower fuel has sub-unit precision. Its exact firing gate
            // is checked on the player rather than the rounded HUD ammo count.
            var requiredAmmo = weapon.Kind == PrimaryWeaponKind.FlameThrower ? 1 : weapon.AmmoPerShot;
            if (isFireBlocked || (currentAmmo >= 0 && currentAmmo < requiredAmmo))
            {
                return new(ContinuousCrosshairSpriteName, ContinuousCrosshairDisabledFrameIndex);
            }

            // Reload/refill timers do not mean the weapon is firing. Ready idle
            // weapons use the white outline from the single-shot sprite.
            return isFireHeld && maxAmmo > 0
                ? new(ContinuousCrosshairSpriteName, GetContinuousCrosshairFrameIndex(currentAmmo, maxAmmo))
                : new("CrosshairS", RechargeCrosshairIdleFrameIndex);
        }

        if (cooldownTicks > 0)
        {
            var duration = Math.Max(1, cooldownDurationTicks ?? weapon.ReloadDelayTicks);
            var elapsedFraction = Math.Clamp(1f - cooldownTicks / (float)duration, 0f, 1f);
            return new("CrosshairS", RechargeCrosshairActiveFrameOffset + Math.Clamp(
                (int)MathF.Floor(elapsedFraction * RechargeCrosshairRefireLastFrameIndex),
                0,
                RechargeCrosshairRefireLastFrameIndex - 1));
        }

        if (weapon.AutoReloads && reloadTicks > 0)
        {
            var duration = Math.Max(1, reloadDurationTicks ?? weapon.AmmoReloadTicks);
            var elapsedFraction = Math.Clamp(1f - reloadTicks / (float)duration, 0f, 1f);
            return new("CrosshairS", RechargeCrosshairRefireLastFrameIndex + Math.Clamp(
                (int)MathF.Floor(elapsedFraction * (RechargeCrosshairActiveFrameCount - RechargeCrosshairRefireLastFrameIndex + 1)),
                0,
                RechargeCrosshairActiveFrameCount - RechargeCrosshairRefireLastFrameIndex));
        }

        return new("CrosshairS", RechargeCrosshairIdleFrameIndex);
    }

    internal static int GetContinuousCrosshairFrameIndex(int currentAmmo, int maxAmmo)
    {
        var ammoFraction = Math.Clamp(currentAmmo / (float)Math.Max(1, maxAmmo), 0f, 1f);
        return Math.Clamp(
            (int)MathF.Floor((1f - ammoFraction) * ContinuousCrosshairActiveFrameCount),
            0,
            ContinuousCrosshairActiveFrameCount - 1);
    }

    internal static bool IsCrosshairFireBlocked(PlayerEntity player, PrimaryWeaponDefinition weapon,
        int cooldownTicks = 0, int cooldownDurationTicks = 0)
    {
        return !player.IsAlive || player.IsHeavyEating || player.IsTaunting
            || player.IsBuffBannerDeploying || player.IsCivviePogoActive
            || player.IsExperimentalCryoFrozen
            || (player.ClassId == PlayerClass.Heavy && player.IsExperimentalGhostDashing)
            || (player.IsSpyCloaked && !player.CanFireLastToDieProfessionalRevolverWhileCloaked)
            || (weapon.Kind == PrimaryWeaponKind.FlameThrower
                && (player.PyroPrimaryRequiresReleaseAfterEmpty
                    || (cooldownTicks > 0 && cooldownDurationTicks > weapon.ReloadDelayTicks)
                    || (!player.HasInfiniteAmmoFromUber
                        && player.PyroPrimaryFuelScaled < PlayerEntity.PyroPrimaryFlameCostScaled)));
    }

    // Observe the actual countdowns so perks and alternate weapon timings use
    // their real durations rather than the stock weapon's reload duration.
    internal sealed class CrosshairTimingState
    {
        private PrimaryWeaponDefinition? _weapon;
        private int _cooldownTicks;
        private int _reloadTicks;
        public int CooldownDurationTicks { get; private set; }
        public int ReloadDurationTicks { get; private set; }

        public void Update(PrimaryWeaponDefinition weapon, int cooldownTicks, int reloadTicks)
        {
            if (_weapon != weapon)
            {
                _weapon = weapon;
                _cooldownTicks = 0;
                _reloadTicks = 0;
                CooldownDurationTicks = Math.Max(1, cooldownTicks);
                ReloadDurationTicks = Math.Max(1, reloadTicks);
            }

            if (cooldownTicks > _cooldownTicks)
            {
                CooldownDurationTicks = cooldownTicks;
            }
            if (reloadTicks > _reloadTicks)
            {
                ReloadDurationTicks = reloadTicks;
            }

            _cooldownTicks = cooldownTicks;
            _reloadTicks = reloadTicks;
        }
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

}

public sealed class GameplayAimHudController
    {
        private readonly IHudContext _context;
        private readonly CrosshairTimingState _crosshairTiming = new();

        public GameplayAimHudController(IHudContext context)
        {
            _context = context;
        }

        public void DrawSniperHud(Vector2 screenAimPosition)
        {
            var localPlayer = _context._world.LocalPlayer;
            if (_context.GetPlayerIsSniperBowEquipped(localPlayer)
                || _context.GetPlayerIsMortarLauncherEquipped(localPlayer))
            {
                DrawSniperBowChargeHud(localPlayer, screenAimPosition);
                return;
            }

            if (!localPlayer.HasScopedSniperWeaponEquipped || !_context.GetPlayerIsSniperScoped(localPlayer))
            {
                return;
            }

            DrawSniperChargeHud(localPlayer, screenAimPosition);
        }

        public void DrawSpectatorSniperHud(PlayerEntity player, Vector2 screenAimPosition)
        {
            if (player.IsSniperBowEquipped || player.IsMortarLauncherEquipped)
            {
                DrawSniperBowChargeHud(player, screenAimPosition);
                return;
            }

            if (!player.HasScopedSniperWeaponEquipped || !_context.GetPlayerIsSniperScoped(player))
            {
                return;
            }

            DrawSniperChargeHud(player, screenAimPosition);
        }

        private void DrawSniperBowChargeHud(PlayerEntity player, Vector2 screenAimPosition)
        {
            var chargeTicks = _context.GetPlayerSniperBowChargeTicks(player);
            if (chargeTicks <= 0)
            {
                return;
            }

            var facingLeft = IsFacingLeftByAim(player);
            var chargeScaleX = facingLeft ? 1f : -1f;
            var chargePosition = screenAimPosition + new Vector2(15f * chargeScaleX, -10f);
            var fullChargeTicks = player.IsMortarLauncherEquipped
                ? PlayerEntity.MortarLauncherMaxChargeTicks
                : player.LastToDieSniperBowFullChargeTicks;
            var isFullyCharged = chargeTicks >= fullChargeTicks;
            if (!isFullyCharged)
            {
                _context.TryDrawScreenSprite("ChargeS", 0, chargePosition, Color.White * 0.25f, new Vector2(chargeScaleX, 1f));
            }
            else
            {
                _context.TryDrawScreenSprite("FullChargeS", 0, screenAimPosition + new Vector2(65f * chargeScaleX, 0f), Color.White, Vector2.One);
            }

            var chargeWidth = GetSniperBowChargeHudFillWidthForTicks(
                chargeTicks,
                fullChargeTicks);
            if (chargeWidth <= 0)
            {
                return;
            }

            DrawSniperChargeFill(chargePosition, chargeWidth, facingLeft);
        }

        private void DrawSniperChargeHud(PlayerEntity player, Vector2 screenAimPosition)
        {
            var damage = _context.GetPlayerSniperRifleDamage(player);
            var facingLeft = IsFacingLeftByAim(player);
            var chargeScaleX = facingLeft ? 1f : -1f;
            var chargePosition = screenAimPosition + new Vector2(15f * chargeScaleX, -10f);
            if (damage < 85)
            {
                _context.TryDrawScreenSprite("ChargeS", 0, chargePosition, Color.White * 0.25f, new Vector2(chargeScaleX, 1f));
            }
            else
            {
                _context.TryDrawScreenSprite("FullChargeS", 0, screenAimPosition + new Vector2(65f * chargeScaleX, 0f), Color.White, Vector2.One);
            }

            var chargeWidth = GetSniperChargeHudFillWidthForTicks(
                _context.GetPlayerSniperChargeTicks(player),
                player.SniperRifleFullChargeTicks);
            if (chargeWidth <= 0)
            {
                return;
            }

            DrawSniperChargeFill(chargePosition, chargeWidth, facingLeft);
        }

        private void DrawSniperChargeFill(Vector2 chargePosition, int chargeWidth, bool facingLeft)
        {
            var sprite = _context.GetResolvedSprite("ChargeS");
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
            var sprite = _context.GetResolvedSprite(spriteName);
            if (sprite is null || frameIndex < 0 || frameIndex >= sprite.Frames.Count)
            {
                return false;
            }

            _context.DrawLoadedSpriteFrame(
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
            var weapon = _context.GetLocalDisplayedMainWeaponStats();
            var cooldownTicks = _context.GetLocalDisplayedMainWeaponCooldownTicks();
            var reloadTicks = _context.GetLocalDisplayedMainWeaponReloadTicks();
            var currentAmmo = _context.GetLocalDisplayedMainWeaponCurrentShells();
            var maxAmmo = _context.GetLocalDisplayedMainWeaponMaxShells();
            var player = _context.GetPlayerPredictedPresentationState(_context._world.LocalPlayer);
            _crosshairTiming.Update(weapon, cooldownTicks, reloadTicks);
            var frame = GetCrosshairFrame(
                weapon,
                cooldownTicks,
                reloadTicks,
                player.HasInfiniteAmmoFromUber ? maxAmmo : currentAmmo,
                maxAmmo,
                _context._latestPredictedLocalInput.FirePrimary,
                IsCrosshairFireBlocked(player, weapon, cooldownTicks, _crosshairTiming.CooldownDurationTicks)
                    || _context.GetPlayerIsExperimentalGhostDashing(_context._world.LocalPlayer),
                _crosshairTiming.CooldownDurationTicks,
                _crosshairTiming.ReloadDurationTicks);
            var crosshair = _context.GetResolvedSprite(frame.SpriteName);
            if (crosshair is null || crosshair.Frames.Count == 0)
            {
                return;
            }

            var frameIndex = Math.Clamp(frame.FrameIndex, 0, crosshair.Frames.Count - 1);
            var cursorScale = ClientSettings.GetCursorScale(_context._cursorSizePercent);
            _context.DrawLoadedSpriteFrame(
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
            if (!_context._world.LocalPlayer.IsAlive)
            {
                return;
            }

            var playerScreenPosition = _context.GetWorldHudScreenPosition(
                _context.GetRenderPosition(_context._world.LocalPlayer),
                cameraPosition);
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
            _context._spriteBatch.Draw(_context._pixel, start, null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }
}
