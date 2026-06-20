#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawStabAnimation(StabAnimEntity stabAnimation, Vector2 cameraPosition)
    {
        var owner = FindPlayerById(stabAnimation.OwnerId);
        var renderPosition = owner is null
            ? new Vector2(stabAnimation.X, stabAnimation.Y)
            : GetRenderPosition(owner);
        var torsoSprite = GetResolvedSprite(stabAnimation.Team == PlayerTeam.Blue ? "SpyBlueBackstabTorsoS" : "SpyRedBackstabTorsoS");
        if (torsoSprite is null || torsoSprite.Frames.Count == 0)
        {
            var directionRadians = MathF.PI * stabAnimation.DirectionDegrees / 180f;
            var directionX = MathF.Cos(directionRadians);
            var directionY = MathF.Sin(directionRadians);
            var color = stabAnimation.Team == PlayerTeam.Blue
                ? new Color(120, 190, 255) * stabAnimation.Alpha
                : new Color(255, 150, 120) * stabAnimation.Alpha;
            DrawWorldLine(
                stabAnimation.X,
                stabAnimation.Y,
                stabAnimation.X + directionX * 36f,
                stabAnimation.Y + directionY * 36f,
                cameraPosition,
                color,
                8f);
            return;
        }

        var frameIndex = Math.Clamp(stabAnimation.FrameIndex, 0, torsoSprite.Frames.Count - 1);
        var facingLeft = stabAnimation.FacingLeft;
        var position = new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y - cameraPosition.Y);
        var scale = new Vector2(facingLeft ? -1f : 1f, 1f);
        DrawLoadedSpriteFrame(
            torsoSprite.Frames[frameIndex],
            position,
            null,
            Color.White * stabAnimation.Alpha,
            0f,
            torsoSprite.Origin.ToVector2(),
            scale,
            SpriteEffects.None,
            0f);

        var legsSprite = GetResolvedSprite("BackstabLegsS");
        if (legsSprite is null || legsSprite.Frames.Count == 0)
        {
            return;
        }

        var legFrameIndex = Math.Clamp(
            (stabAnimation.Team == PlayerTeam.Blue ? 1 : 0) + ((owner is not null && !GetPlayerRenderIsGrounded(owner)) ? 2 : 0),
            0,
            legsSprite.Frames.Count - 1);
        DrawLoadedSpriteFrame(
            legsSprite.Frames[legFrameIndex],
            position,
            null,
            Color.White * stabAnimation.Alpha,
            0f,
            legsSprite.Origin.ToVector2(),
            scale,
            SpriteEffects.None,
            0f);
    }

    private static void DrawStabMask(StabMaskEntity stabMask, Vector2 cameraPosition)
    {
        // Source StabMask is invisible; it is a hitbox, not a visible effect.
    }

    private void DrawPlayerGib(PlayerGibEntity gib, Vector2 cameraPosition)
    {
        if (_gibLevel == 0 || (_gibLevel == 1) || (_gibLevel == 2 && (gib.FrameIndex % 2 != 0)))
        {
            return;
        }

        var sprite = GetResolvedSprite(gib.SpriteName);
        var gibTint = gib.ExperimentalCryoTinted
            ? new Color(170, 228, 255)
            : Color.White;
        if (sprite is null || sprite.Frames.Count == 0)
        {
            var gibScale = GetPlayerGibRenderScale(gib);
            var size = (int)(6f * gibScale);
            var rectangle = new Rectangle(
                (int)(gib.X - (size / 2f) - cameraPosition.X),
                (int)(gib.Y - (size / 2f) - cameraPosition.Y),
                size,
                size);
            _spriteBatch.Draw(_pixel, rectangle, gibTint * gib.Alpha);
            return;
        }

        var frameIndex = Math.Clamp(gib.FrameIndex, 0, sprite.Frames.Count - 1);
        var renderScale = GetPlayerGibRenderScale(gib);
        DrawLoadedSpriteFrame(
            sprite.Frames[frameIndex],
            new Vector2(gib.X - cameraPosition.X, gib.Y - cameraPosition.Y),
            null,
            gibTint * gib.Alpha,
            gib.RotationDegrees * (MathF.PI / 180f),
            sprite.Origin.ToVector2(),
            new Vector2(renderScale, renderScale),
            SpriteEffects.None,
            0f);
    }

    private static float GetPlayerGibRenderScale(PlayerGibEntity gib)
    {
        return IsExperimentalDemoknightDecapHeadSprite(gib.SpriteName)
            ? 1f
            : PlayerGibEntity.Scale;
    }

    private static bool IsExperimentalDemoknightDecapHeadSprite(string spriteName)
    {
        return !string.Equals(spriteName, "HeadS", StringComparison.Ordinal)
            && spriteName.EndsWith("HeadS", StringComparison.Ordinal);
    }

    private void DrawBloodDrop(BloodDropEntity bloodDrop, Vector2 cameraPosition)
    {
        if (_gibLevel == 0)
        {
            return;
        }

        var sprite = GetResolvedSprite("BloodDropS");
        var bloodTint = bloodDrop.ExperimentalCryoTinted
            ? new Color(176, 232, 255)
            : Color.White;
        if (sprite is null || sprite.Frames.Count == 0)
        {
            var size = Math.Max(2, (int)MathF.Round(2f * bloodDrop.Scale));
            var rectangle = new Rectangle(
                (int)(bloodDrop.X - (size / 2f) - cameraPosition.X),
                (int)(bloodDrop.Y - (size / 2f) - cameraPosition.Y),
                size,
                size);
            _spriteBatch.Draw(_pixel, rectangle, bloodTint * bloodDrop.Alpha);
            return;
        }

        DrawLoadedSpriteFrame(
            sprite.Frames[0],
            new Vector2(bloodDrop.X - cameraPosition.X, bloodDrop.Y - cameraPosition.Y),
            null,
            bloodTint * bloodDrop.Alpha,
            0f,
            sprite.Origin.ToVector2(),
            new Vector2(bloodDrop.Scale, bloodDrop.Scale),
            SpriteEffects.None,
            0f);
    }

    private bool TryDrawSentry(SentryEntity sentry, Vector2 cameraPosition)
    {
        var renderPosition = RoundToSourcePixels(GetRenderPosition(sentry.Id, sentry.X, sentry.Y));
        var baseSpriteName = sentry.Team == PlayerTeam.Blue ? "SentryBlue" : "SentryRed";
        var baseSprite = GetResolvedSprite(baseSpriteName);
        if (baseSprite is null || baseSprite.Frames.Count == 0)
        {
            return false;
        }

        var drawBaseChassis = !(sentry.IsBuilt && _world.IsExperimentalEngineerFloatingSentry(sentry));
        if (drawBaseChassis)
        {
            var baseFrameIndex = GetSentryBaseFrameIndex(sentry, baseSprite.Frames.Count);
            var baseEffects = sentry.FacingDirectionX < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            DrawLoadedSpriteFrame(
                baseSprite.Frames[baseFrameIndex],
                new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y - cameraPosition.Y),
                null,
                Color.White,
                0f,
                baseSprite.Origin.ToVector2(),
                Vector2.One,
                baseEffects,
                0f);
        }

        if (!sentry.IsBuilt)
        {
            return true;
        }

        var turretSprite = GetResolvedSprite("SentryTurretS");
        if (turretSprite is null || turretSprite.Frames.Count == 0)
        {
            return true;
        }

        var facingScale = sentry.FacingDirectionX < 0f ? -1f : 1f;
        var turretFrameIndex = Math.Clamp(sentry.Team == PlayerTeam.Blue ? 1 : 0, 0, turretSprite.Frames.Count - 1);
        var turretDirectionDegrees = sentry.HasActiveTarget
            ? sentry.AimDirectionDegrees
            : (facingScale < 0f ? 180f : 0f);
        var turretAngleDegrees = facingScale < 0f ? turretDirectionDegrees + 180f : turretDirectionDegrees;
        var turretRotation = MathF.PI * turretAngleDegrees / 180f;
        var drawX = renderPosition.X + (turretSprite.Origin.X - 17f) * facingScale;
        var drawY = renderPosition.Y + turretSprite.Origin.Y - 10f;
        DrawLoadedSpriteFrame(
            turretSprite.Frames[turretFrameIndex],
            new Vector2(drawX - cameraPosition.X, drawY - cameraPosition.Y),
            null,
            Color.White,
            turretRotation,
            turretSprite.Origin.ToVector2(),
            new Vector2(facingScale, 1f),
            SpriteEffects.None,
            0f);
        return true;
    }

    private void DrawDamageableZoneHealthBars(Vector2 cameraPosition)
    {
        const int barWidth = 20;
        const int barHeight = 5;
        for (var index = 0; index < _world.Level.RoomObjects.Count; index += 1)
        {
            if (!_world.Level.IsRoomObjectActive(index))
            {
                continue;
            }

            var marker = _world.Level.RoomObjects[index];
            if (marker.Type != RoomObjectType.DamageableZone || !marker.DamageableZone.ShowHealthBar)
            {
                continue;
            }

            var maxHealth = marker.DamageableZone.MaxHealth;
            if (maxHealth <= 0f)
            {
                continue;
            }

            var currentHealth = _world.GetDamageableZoneHealth(index);
            if (currentHealth <= 0f)
            {
                continue;
            }

            var centerX = marker.CenterX - cameraPosition.X;
            var centerY = marker.CenterY - cameraPosition.Y;
            var backRectangle = new Rectangle(
                (int)(centerX - (barWidth * 0.5f)),
                (int)(centerY - (marker.Height * 0.5f) - barHeight - 2f),
                barWidth,
                barHeight);
            _spriteBatch.Draw(_pixel, backRectangle, Color.Black);

            var fillWidth = Math.Clamp((int)MathF.Round(barWidth * (currentHealth / maxHealth)), 0, barWidth);
            if (fillWidth > 0)
            {
                var fillRectangle = new Rectangle(backRectangle.X, backRectangle.Y, fillWidth, backRectangle.Height);
                _spriteBatch.Draw(_pixel, fillRectangle, Color.Lerp(Color.Red, Color.LimeGreen, currentHealth / maxHealth));
            }
        }
    }

    private void DrawSentryHealthBar(SentryEntity sentry, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(sentry.Id, sentry.X, sentry.Y);
        const int barWidth = 20;
        var backRectangle = new Rectangle(
            (int)(renderPosition.X - 10f - cameraPosition.X),
            (int)(renderPosition.Y - 30f - cameraPosition.Y),
            barWidth,
            5);
        _spriteBatch.Draw(_pixel, backRectangle, Color.Black);

        var fillWidth = Math.Clamp((int)MathF.Round(barWidth * (sentry.Health / (float)sentry.MaxHealth)), 0, barWidth);
        if (fillWidth > 0)
        {
            var fillRectangle = new Rectangle(backRectangle.X, backRectangle.Y, fillWidth, backRectangle.Height);
            _spriteBatch.Draw(_pixel, fillRectangle, Color.Lerp(Color.Red, Color.LimeGreen, sentry.Health / (float)sentry.MaxHealth));
        }
    }

    private void DrawSentryShotTrace(SentryEntity sentry, Vector2 cameraPosition)
    {
        if (!sentry.IsBuilt || !sentry.IsShotTraceVisible)
        {
            return;
        }

        var renderPosition = GetRenderPosition(sentry.Id, sentry.X, sentry.Y);
        var directionRadians = MathF.PI * sentry.AimDirectionDegrees / 180f;
        var facingScale = sentry.FacingDirectionX < 0f ? -1f : 1f;
        var muzzleX = renderPosition.X + MathF.Cos(directionRadians) * 10f - (4f * facingScale);
        var muzzleY = renderPosition.Y + MathF.Sin(directionRadians) * 10f - 2f;
        DrawWorldLine(muzzleX, muzzleY, sentry.LastShotTargetX, sentry.LastShotTargetY, cameraPosition, new Color(255, 232, 90, 153), 2f);
    }

    private int GetSentryBaseFrameIndex(SentryEntity sentry, int frameCount)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        if (sentry.IsBuilt)
        {
            if (_world.IsExperimentalEngineerFloatingSentry(sentry))
            {
                return 0;
            }

            return Math.Clamp(11, 0, frameCount - 1);
        }

        var buildFrame = (int)MathF.Floor((sentry.Health / (float)sentry.MaxHealth) * 10f);
        return Math.Clamp(buildFrame, 0, frameCount - 1);
    }

    private bool TryDrawSentryGib(SentryGibEntity gib, Vector2 cameraPosition)
    {
        var sprite = GetResolvedSprite("SentryGibsS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var frameIndex = Math.Clamp(gib.Team == PlayerTeam.Blue ? 1 : 0, 0, sprite.Frames.Count - 1);
        DrawLoadedSpriteFrame(
            sprite.Frames[frameIndex],
            new Vector2(gib.X - cameraPosition.X, gib.Y - cameraPosition.Y),
            null,
            Color.White * gib.Alpha,
            0f,
            sprite.Origin.ToVector2(),
            Vector2.One,
            SpriteEffects.None,
            0f);
        return true;
    }

    private bool TryDrawJumpPad(JumpPadEntity jumpPad, Vector2 cameraPosition)
    {
        var spriteName = jumpPad.Team == PlayerTeam.Blue ? "JumpPadBlue" : "JumpPadRed";
        var buildSpriteName = jumpPad.Team == PlayerTeam.Blue ? "JumpPadBlueBuild" : "JumpPadRedBuild";
        var renderPosition = RoundToSourcePixels(GetRenderPosition(jumpPad.Id, jumpPad.X, jumpPad.Y));
        var jumpPadRenderYOffset = jumpPad.HasLanded ? 10f : 0f;

        if (!jumpPad.IsBuilt)
        {
            var buildSprite = _runtimeAssets.GetSprite(buildSpriteName);
            if (buildSprite is null || buildSprite.Frames.Count == 0)
            {
                return false;
            }

            var buildProgress = (jumpPad.Health - JumpPadEntity.InitialHealth) / (float)(JumpPadEntity.MaxHealth - JumpPadEntity.InitialHealth);
            var buildFrame = (int)MathF.Floor(buildProgress * (buildSprite.Frames.Count - 1));
            buildFrame = Math.Clamp(buildFrame, 0, buildSprite.Frames.Count - 1);
            DrawLoadedSpriteFrame(
                buildSprite.Frames[buildFrame],
                new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y + jumpPadRenderYOffset - cameraPosition.Y),
                null,
                Color.White,
                0f,
                buildSprite.Origin.ToVector2(),
                Vector2.One,
                SpriteEffects.None,
                0f);
            return true;
        }

        var sprite = _runtimeAssets.GetSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var animatedFrameCount = Math.Min(6, sprite.Frames.Count);
        const long millisecondsPerFrame = 100;
        var frameIndex = animatedFrameCount > 0
            ? (int)((Environment.TickCount64 / millisecondsPerFrame) % animatedFrameCount)
            : 0;

        DrawLoadedSpriteFrame(
            sprite.Frames[frameIndex],
            new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y + jumpPadRenderYOffset - cameraPosition.Y),
            null,
            Color.White,
            0f,
            sprite.Origin.ToVector2(),
            Vector2.One,
            SpriteEffects.None,
            0f);
        return true;
    }

    private bool TryDrawJumpPadGib(JumpPadGibEntity gib, Vector2 cameraPosition)
    {
        var sprite = GetResolvedSprite("JumpPadGibsS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var frameIndex = Math.Clamp(gib.Team == PlayerTeam.Blue ? 1 : 0, 0, sprite.Frames.Count - 1);
        DrawLoadedSpriteFrame(
            sprite.Frames[frameIndex],
            new Vector2(gib.X - cameraPosition.X, gib.Y - cameraPosition.Y),
            null,
            Color.White,
            0f,
            sprite.Origin.ToVector2(),
            Vector2.One,
            SpriteEffects.None,
            0f);
        return true;
    }

    private void DrawCivilDefenseTurret(CivilDefenseTurretEntity turret, Vector2 cameraPosition)
    {
        var renderPosition = RoundToSourcePixels(GetRenderPosition(turret.Id, turret.X, turret.Y));
        var teamColor = turret.Team == PlayerTeam.Blue
            ? new Color(90, 180, 255)
            : new Color(235, 125, 95);
        var bodyColor = turret.IsBuilt ? teamColor : teamColor * 0.72f;
        var bodyRectangle = new Rectangle(
            (int)(renderPosition.X - CivilDefenseTurretEntity.Width / 2f - cameraPosition.X),
            (int)(renderPosition.Y - CivilDefenseTurretEntity.Height / 2f - cameraPosition.Y),
            (int)CivilDefenseTurretEntity.Width,
            (int)CivilDefenseTurretEntity.Height);
        _spriteBatch.Draw(_pixel, bodyRectangle, bodyColor);

        var directionRadians = MathF.PI * turret.AimDirectionDegrees / 180f;
        var muzzleX = renderPosition.X + MathF.Cos(directionRadians) * 10f;
        var muzzleY = renderPosition.Y + MathF.Sin(directionRadians) * 10f - 2f;
        DrawWorldLine(
            renderPosition.X,
            renderPosition.Y - 2f,
            muzzleX,
            muzzleY,
            cameraPosition,
            new Color(255, 245, 150, 210),
            3f);

        DrawCivilDefenseTurretHealthBar(turret, renderPosition, cameraPosition);
        if (turret.IsBuilt && turret.IsShotTraceVisible)
        {
            DrawWorldLine(
                muzzleX,
                muzzleY,
                turret.LastShotTargetX,
                turret.LastShotTargetY,
                cameraPosition,
                new Color(255, 232, 90, 170),
                2f);
        }
    }

    private void DrawCivilDefenseTurretHealthBar(CivilDefenseTurretEntity turret, Vector2 renderPosition, Vector2 cameraPosition)
    {
        const int barWidth = 18;
        var backRectangle = new Rectangle(
            (int)(renderPosition.X - barWidth / 2f - cameraPosition.X),
            (int)(renderPosition.Y - 28f - cameraPosition.Y),
            barWidth,
            3);
        _spriteBatch.Draw(_pixel, backRectangle, Color.Black);

        var fillWidth = Math.Clamp((int)MathF.Round(barWidth * (turret.Health / (float)CivilDefenseTurretEntity.MaxHealth)), 0, barWidth);
        if (fillWidth <= 0)
        {
            return;
        }

        var fillRectangle = new Rectangle(backRectangle.X, backRectangle.Y, fillWidth, backRectangle.Height);
        _spriteBatch.Draw(_pixel, fillRectangle, Color.Lerp(Color.Red, Color.LimeGreen, turret.Health / (float)CivilDefenseTurretEntity.MaxHealth));
    }

    private void DrawHealthPack(HealthPackEntity healthPack, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(healthPack.Id, healthPack.X, healthPack.Y);
        var alpha = healthPack.Alpha;
        var pulseSprite = GetResolvedSprite("HealthPackPulseS");
        if (pulseSprite is not null && pulseSprite.Frames.Count > 0)
        {
            var pulseFrameIndex = (int)((_world.Frame / 5) % pulseSprite.Frames.Count);
            DrawLoadedSpriteFrame(
                pulseSprite.Frames[pulseFrameIndex],
                new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y - cameraPosition.Y),
                null,
                Color.White * alpha,
                0f,
                pulseSprite.Origin.ToVector2(),
                Vector2.One,
                SpriteEffects.None,
                0f);
        }
        else
        {
            var pulseRectangle = new Rectangle(
                (int)(renderPosition.X - 14f - cameraPosition.X),
                (int)(renderPosition.Y - 6f - cameraPosition.Y),
                28,
                12);
            _spriteBatch.Draw(_pixel, pulseRectangle, new Color(130, 255, 150) * (0.4f * alpha));
        }

        var packSpriteName = healthPack.Size == HealthPackSize.Large
            ? "HealthPackLargeS"
            : "HealthPackSmallS";
        var packSprite = GetResolvedSprite(packSpriteName);
        if (packSprite is not null && packSprite.Frames.Count > 0)
        {
            var packFrameIndex = (int)((_world.Frame / 5) % packSprite.Frames.Count);
            DrawLoadedSpriteFrame(
                packSprite.Frames[packFrameIndex],
                new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y - cameraPosition.Y),
                null,
                Color.White * alpha,
                0f,
                packSprite.Origin.ToVector2(),
                Vector2.One,
                SpriteEffects.None,
                0f);
            return;
        }

        var fillColor = healthPack.Size == HealthPackSize.Large
            ? new Color(140, 255, 140)
            : new Color(200, 255, 200);
        var packRectangle = new Rectangle(
            (int)(renderPosition.X - 8f - cameraPosition.X),
            (int)(renderPosition.Y - 8f - cameraPosition.Y),
            16,
            16);
        _spriteBatch.Draw(_pixel, packRectangle, fillColor * alpha);
        _spriteBatch.Draw(_pixel, new Rectangle(packRectangle.X + 6, packRectangle.Y + 2, 4, 12), new Color(220, 32, 32) * alpha);
        _spriteBatch.Draw(_pixel, new Rectangle(packRectangle.X + 2, packRectangle.Y + 6, 12, 4), new Color(220, 32, 32) * alpha);
    }

    private void DrawDroppedWeapon(DroppedWeaponEntity droppedWeapon, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(droppedWeapon.Id, droppedWeapon.X, droppedWeapon.Y);
        var alpha = droppedWeapon.Alpha;
        if (!CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(droppedWeapon.WeaponClassId, out _))
        {
            return;
        }

        var presentation = CharacterClassCatalog.RuntimeRegistry.GetPrimaryItem(droppedWeapon.WeaponClassId).Presentation;
        var spriteName = presentation.WorldSpriteName;
        var frameIndex = droppedWeapon.Team == PlayerTeam.Blue ? 1 : 0;
        var rotation = droppedWeapon.HasLanded
            ? 0f
            : MathF.Atan2(droppedWeapon.VerticalSpeed, droppedWeapon.HorizontalSpeed == 0f ? 1f : droppedWeapon.HorizontalSpeed);

        var outlineColor = new Color(255, 226, 74) * (alpha * 0.35f);
        if (spriteName is not null && GetResolvedSprite(spriteName) is not null)
        {
            TryDrawSprite(spriteName, frameIndex, renderPosition.X - 1f, renderPosition.Y, cameraPosition, outlineColor, rotation);
            TryDrawSprite(spriteName, frameIndex, renderPosition.X + 1f, renderPosition.Y, cameraPosition, outlineColor, rotation);
            TryDrawSprite(spriteName, frameIndex, renderPosition.X, renderPosition.Y - 1f, cameraPosition, outlineColor, rotation);
            TryDrawSprite(spriteName, frameIndex, renderPosition.X, renderPosition.Y + 1f, cameraPosition, outlineColor, rotation);
            TryDrawSprite(spriteName, frameIndex, renderPosition.X, renderPosition.Y, cameraPosition, Color.White * alpha, rotation);
            return;
        }

        var fallbackRectangle = new Rectangle(
            (int)(renderPosition.X - 10f - cameraPosition.X),
            (int)(renderPosition.Y - 3f - cameraPosition.Y),
            20,
            6);
        var outlineRectangle = new Rectangle(
            fallbackRectangle.X - 1,
            fallbackRectangle.Y - 1,
            fallbackRectangle.Width + 2,
            fallbackRectangle.Height + 2);
        _spriteBatch.Draw(_pixel, outlineRectangle, new Color(255, 226, 74) * (0.28f * alpha));
        _spriteBatch.Draw(_pixel, fallbackRectangle, new Color(230, 230, 230) * alpha);
        _spriteBatch.Draw(_pixel, new Rectangle(fallbackRectangle.X, fallbackRectangle.Bottom, fallbackRectangle.Width, 2), Color.Black * (0.55f * alpha));
    }

    private void DrawDroppedWeaponInteractionHud(Vector2 cameraPosition)
    {
        if (!_world.ExperimentalGameplaySettings.EnableEnemyDroppedWeapons
            || !_world.LocalPlayer.IsAlive
            || _world.LocalPlayer.ClassId != PlayerClass.Soldier
            || TryGetNearbyDroppedWeaponForLocalPlayer() is not { } nearbyWeapon)
        {
            return;
        }

        var weaponName = CharacterClassCatalog.RuntimeRegistry.GetPrimaryItem(nearbyWeapon.WeaponClassId).DisplayName.ToUpperInvariant();
        var prompt = $"PRESS Q FOR {weaponName}";
        var renderPosition = GetRenderPosition(_world.LocalPlayer);
        var labelPosition = new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y - cameraPosition.Y - 42f);
        DrawBitmapFontTextCentered(prompt, labelPosition + new Vector2(2f, 2f), Color.Black * 0.9f, 0.92f);
        DrawBitmapFontTextCentered(prompt, labelPosition, new Color(255, 226, 74), 0.92f);
    }

    private DroppedWeaponEntity? TryGetNearbyDroppedWeaponForLocalPlayer()
    {
        var localPlayer = _world.LocalPlayer;
        var bestDistanceSquared = float.MaxValue;
        DroppedWeaponEntity? bestWeapon = null;
        for (var index = 0; index < _world.DroppedWeapons.Count; index += 1)
        {
            var candidate = _world.DroppedWeapons[index];
            if (!localPlayer.IntersectsMarker(
                    candidate.X,
                    candidate.Y,
                    DroppedWeaponEntity.PickupWidth,
                    DroppedWeaponEntity.PickupHeight))
            {
                continue;
            }

            var deltaX = candidate.X - localPlayer.X;
            var deltaY = candidate.Y - localPlayer.Y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestWeapon = candidate;
        }

        return bestWeapon;
    }
}
