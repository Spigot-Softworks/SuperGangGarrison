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
        if (IsSpyHiddenFromLocalViewer(stabAnimation.OwnerId, stabAnimation.Team, stabAnimation.X))
        {
            return;
        }

        var owner = FindPlayerById(stabAnimation.OwnerId);
        var renderPosition = owner is null
            ? new Vector2(stabAnimation.X, stabAnimation.Y)
            : GetRenderPosition(owner, allowInterpolation: !ReferenceEquals(owner, _world.LocalPlayer));
        var torsoSprite = _runtimeAssets.GetSprite(stabAnimation.Team == PlayerTeam.Blue ? "SpyBlueBackstabTorsoS" : "SpyRedBackstabTorsoS");
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
        _spriteBatch.Draw(
            torsoSprite.Frames[frameIndex],
            position,
            null,
            Color.White * stabAnimation.Alpha,
            0f,
            torsoSprite.Origin.ToVector2(),
            scale,
            SpriteEffects.None,
            0f);

        var legsSprite = _runtimeAssets.GetSprite("BackstabLegsS");
        if (legsSprite is null || legsSprite.Frames.Count == 0)
        {
            return;
        }

        var legFrameIndex = Math.Clamp(
            (stabAnimation.Team == PlayerTeam.Blue ? 1 : 0) + ((owner is not null && !GetPlayerRenderIsGrounded(owner)) ? 2 : 0),
            0,
            legsSprite.Frames.Count - 1);
        _spriteBatch.Draw(
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

        var sprite = _runtimeAssets.GetSprite(gib.SpriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            var size = (int)(6f * PlayerGibEntity.Scale);
            var rectangle = new Rectangle(
                (int)(gib.X - (size / 2f) - cameraPosition.X),
                (int)(gib.Y - (size / 2f) - cameraPosition.Y),
                size,
                size);
            _spriteBatch.Draw(_pixel, rectangle, Color.White * gib.Alpha);
            return;
        }

        var frameIndex = Math.Clamp(gib.FrameIndex, 0, sprite.Frames.Count - 1);
        _spriteBatch.Draw(
            sprite.Frames[frameIndex],
            new Vector2(gib.X - cameraPosition.X, gib.Y - cameraPosition.Y),
            null,
            Color.White * gib.Alpha,
            gib.RotationDegrees * (MathF.PI / 180f),
            sprite.Origin.ToVector2(),
            new Vector2(PlayerGibEntity.Scale, PlayerGibEntity.Scale),
            SpriteEffects.None,
            0f);
    }

    private void DrawBloodDrop(BloodDropEntity bloodDrop, Vector2 cameraPosition)
    {
        if (_gibLevel == 0)
        {
            return;
        }

        var sprite = _runtimeAssets.GetSprite("BloodDropS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            var rectangle = new Rectangle(
                (int)(bloodDrop.X - cameraPosition.X),
                (int)(bloodDrop.Y - cameraPosition.Y),
                2,
                2);
            _spriteBatch.Draw(_pixel, rectangle, Color.White * bloodDrop.Alpha);
            return;
        }

        _spriteBatch.Draw(
            sprite.Frames[0],
            new Vector2(bloodDrop.X - cameraPosition.X, bloodDrop.Y - cameraPosition.Y),
            null,
            Color.White * bloodDrop.Alpha,
            0f,
            sprite.Origin.ToVector2(),
            Vector2.One,
            SpriteEffects.None,
            0f);
    }

    private bool TryDrawSentry(SentryEntity sentry, Vector2 cameraPosition)
    {
        var renderPosition = GetRenderPosition(sentry.Id, sentry.X, sentry.Y);
        var baseSpriteName = sentry.Team == PlayerTeam.Blue ? "SentryBlue" : "SentryRed";
        var baseSprite = _runtimeAssets.GetSprite(baseSpriteName);
        if (baseSprite is null || baseSprite.Frames.Count == 0)
        {
            return false;
        }

        var baseFrameIndex = GetSentryBaseFrameIndex(sentry, baseSprite.Frames.Count);
        var baseEffects = sentry.FacingDirectionX < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        _spriteBatch.Draw(
            baseSprite.Frames[baseFrameIndex],
            new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y - cameraPosition.Y),
            null,
            Color.White,
            0f,
            baseSprite.Origin.ToVector2(),
            Vector2.One,
            baseEffects,
            0f);

        if (!sentry.IsBuilt)
        {
            return true;
        }

        if (sentry.IsRotating)
        {
            var rotateSprite = _runtimeAssets.GetSprite("TurretRotateS");
            if (rotateSprite is not null && rotateSprite.Frames.Count > 0)
            {
                var frameIndex = GetSentryRotateFrameIndex(sentry, rotateSprite.Frames.Count);
                _spriteBatch.Draw(
                    rotateSprite.Frames[frameIndex],
                    new Vector2(renderPosition.X - cameraPosition.X, renderPosition.Y - cameraPosition.Y),
                    null,
                    Color.White,
                    0f,
                    rotateSprite.Origin.ToVector2(),
                    Vector2.One,
                    SpriteEffects.None,
                    0f);
                return true;
            }
        }

        var turretSprite = _runtimeAssets.GetSprite("SentryTurretS");
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
        var drawX = renderPosition.X + (-17f + turretSprite.Origin.X) * facingScale;
        var drawY = renderPosition.Y + (-10f + turretSprite.Origin.Y) - 6f;
        _spriteBatch.Draw(
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

        var fillWidth = Math.Clamp((int)MathF.Round(barWidth * (sentry.Health / (float)SentryEntity.MaxHealth)), 0, barWidth);
        if (fillWidth > 0)
        {
            var fillRectangle = new Rectangle(backRectangle.X, backRectangle.Y, fillWidth, backRectangle.Height);
            _spriteBatch.Draw(_pixel, fillRectangle, Color.Lerp(Color.Red, Color.LimeGreen, sentry.Health / (float)SentryEntity.MaxHealth));
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

    private static int GetSentryBaseFrameIndex(SentryEntity sentry, int frameCount)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        if (sentry.IsBuilt)
        {
            return Math.Clamp(11, 0, frameCount - 1);
        }

        var buildFrame = (int)MathF.Floor((sentry.Health / (float)SentryEntity.MaxHealth) * 10f);
        return Math.Clamp(buildFrame, 0, frameCount - 1);
    }

    private static int GetSentryRotateFrameIndex(SentryEntity sentry, int frameCount)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        var teamOffset = sentry.Team == PlayerTeam.Blue ? 5 : 0;
        var progressFrame = Math.Clamp((int)MathF.Round(sentry.RotationProgress * 4f), 0, 4);
        if (sentry.RotationStartDirectionX < 0f)
        {
            progressFrame = 4 - progressFrame;
        }

        return Math.Clamp(teamOffset + progressFrame, 0, frameCount - 1);
    }

    private bool TryDrawSentryGib(SentryGibEntity gib, Vector2 cameraPosition)
    {
        if (_gibLevel < 2)
        {
            return true;
        }

        var sprite = _runtimeAssets.GetSprite("SentryGibsS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var frameIndex = Math.Clamp(gib.Team == PlayerTeam.Blue ? 1 : 0, 0, sprite.Frames.Count - 1);
        _spriteBatch.Draw(
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
}
