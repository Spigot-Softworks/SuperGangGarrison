#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum HitboxDebugMode
    {
        Off = 0,
        Filled = 1,
        Outline = 2,
    }

    private const float HitboxDebugAlpha = 0.4f;
    private static readonly Color HitboxDebugWalkmaskColor = new(64, 140, 255);
    private static readonly Color HitboxDebugPlayerColor = new(64, 220, 96);
    private static readonly Color HitboxDebugProjectileColor = new(255, 64, 64);

    private HitboxDebugMode _hitboxDebugMode;

    private void UpdateHitboxDebugHotkey(KeyboardState keyboard)
    {
        if (_consoleOpen || _mainMenuOpen || IsGameplayInputBlocked() || _navEditorEnabled)
        {
            return;
        }

        if (!IsKeyPressed(keyboard, Keys.F5))
        {
            return;
        }

        _hitboxDebugMode = _hitboxDebugMode switch
        {
            HitboxDebugMode.Off => HitboxDebugMode.Filled,
            HitboxDebugMode.Filled => HitboxDebugMode.Outline,
            _ => HitboxDebugMode.Off,
        };

        AddConsoleLine(_hitboxDebugMode switch
        {
            HitboxDebugMode.Filled => "hitbox debug: filled",
            HitboxDebugMode.Outline => "hitbox debug: outline",
            _ => "hitbox debug: off",
        });
    }

    private void DrawHitboxDebugOverlay(Vector2 cameraPosition)
    {
        if (_hitboxDebugMode == HitboxDebugMode.Off)
        {
            return;
        }

        DrawHitboxDebugWalkmasks(cameraPosition);
        DrawHitboxDebugPlayers(cameraPosition);
        DrawHitboxDebugBuildings(cameraPosition);
        DrawHitboxDebugProjectiles(cameraPosition);
        DrawHitboxDebugMeleeAndStab(cameraPosition);
        DrawHitboxDebugModeLabel();
    }

    private void DrawHitboxDebugModeLabel()
    {
        var label = _hitboxDebugMode == HitboxDebugMode.Filled
            ? "HITBOX: Filled (F5)"
            : "HITBOX: Outline (F5)";
        _spriteBatch.DrawString(_consoleFont, label, new Vector2(12f, 12f), Color.White * 0.9f);
    }

    private void DrawHitboxDebugWalkmasks(Vector2 cameraPosition)
    {
        var color = ApplyHitboxDebugAlpha(HitboxDebugWalkmaskColor);
        foreach (var solid in _world.Level.Solids)
        {
            DrawHitboxDebugWorldRectangle(solid.Left, solid.Top, solid.Right, solid.Bottom, cameraPosition, color);
        }
    }

    private void DrawHitboxDebugPlayers(Vector2 cameraPosition)
    {
        var color = ApplyHitboxDebugAlpha(HitboxDebugPlayerColor);
        foreach (var player in _gameplayPlayerRenderController.EnumerateRenderablePlayers())
        {
            if (!player.IsAlive || !IsHitboxDebugPlayerLegallyVisible(player))
            {
                continue;
            }

            var bounds = _world.CombatTestGetPlayerPresentationHitBounds(player);
            var renderPosition = GetRenderPosition(player);
            var offsetX = renderPosition.X - player.X;
            var offsetY = renderPosition.Y - player.Y;
            DrawHitboxDebugWorldRectangle(
                bounds.Left + offsetX,
                bounds.Top + offsetY,
                bounds.Right + offsetX,
                bounds.Bottom + offsetY,
                cameraPosition,
                color);
        }
    }

    private void DrawHitboxDebugBuildings(Vector2 cameraPosition)
    {
        var color = ApplyHitboxDebugAlpha(HitboxDebugProjectileColor);
        foreach (var sentry in _world.Sentries)
        {
            DrawHitboxDebugWorldRectangle(
                sentry.X - (SentryEntity.Width / 2f),
                sentry.Y - (SentryEntity.Height / 2f),
                sentry.X + (SentryEntity.Width / 2f),
                sentry.Y + (SentryEntity.Height / 2f),
                cameraPosition,
                color);
        }

        for (var index = 0; index < _world.Generators.Count; index += 1)
        {
            var generator = _world.Generators[index];
            if (generator.IsDestroyed)
            {
                continue;
            }

            DrawHitboxDebugWorldRectangle(
                generator.Marker.Left,
                generator.Marker.Top,
                generator.Marker.Right,
                generator.Marker.Bottom,
                cameraPosition,
                color);
        }
    }

    private void DrawHitboxDebugProjectiles(Vector2 cameraPosition)
    {
        var color = ApplyHitboxDebugAlpha(HitboxDebugProjectileColor);
        foreach (var rocket in _world.Rockets)
        {
            DrawHitboxDebugWorldCircle(rocket.X, rocket.Y, 5f, cameraPosition, color);
        }

        foreach (var flare in _world.Flares)
        {
            DrawHitboxDebugWorldCircle(flare.X, flare.Y, 5f, cameraPosition, color);
        }

        foreach (var mine in _world.Mines)
        {
            DrawHitboxDebugWorldCircle(mine.X, mine.Y, 5f, cameraPosition, color);
        }

        foreach (var grenade in _world.Grenades)
        {
            DrawHitboxDebugWorldCircle(grenade.X, grenade.Y, 5f, cameraPosition, color);
        }

        foreach (var bubble in _world.Bubbles)
        {
            DrawHitboxDebugWorldCircle(bubble.X, bubble.Y, BubbleProjectileEntity.Radius, cameraPosition, color);
        }

        foreach (var blade in _world.Blades)
        {
            DrawHitboxDebugWorldCircle(blade.X, blade.Y, 3f, cameraPosition, color);
        }

        foreach (var shot in _world.Shots)
        {
            DrawHitboxDebugWorldCircle(shot.X, shot.Y, 2f, cameraPosition, color);
        }

        foreach (var shot in _world.RevolverShots)
        {
            DrawHitboxDebugWorldCircle(shot.X, shot.Y, 2f, cameraPosition, color);
        }

        foreach (var needle in _world.Needles)
        {
            DrawHitboxDebugWorldCircle(needle.X, needle.Y, 2f, cameraPosition, color);
        }

        foreach (var flame in _world.Flames)
        {
            DrawHitboxDebugWorldCircle(flame.X, flame.Y, 4f, cameraPosition, color);
        }
    }

    private void DrawHitboxDebugMeleeAndStab(Vector2 cameraPosition)
    {
        var color = ApplyHitboxDebugAlpha(HitboxDebugProjectileColor);
        foreach (var stabMask in _world.StabMasks)
        {
            if (!IsHitboxDebugAttackOwnerLegallyVisible(stabMask.OwnerId))
            {
                continue;
            }

            stabMask.GetHitBounds(out var left, out var top, out var right, out var bottom);
            DrawHitboxDebugWorldRectangle(left, top, right, bottom, cameraPosition, color);
        }

        foreach (var player in _gameplayPlayerRenderController.EnumerateRenderablePlayers())
        {
            if (!player.IsAlive
                || !IsHitboxDebugPlayerLegallyVisible(player)
                || !TryGetActiveMeleeHitboxDebug(player, out var mask, out var anchorX, out var anchorY, out var facingLeft, out var maskScale))
            {
                continue;
            }

            DrawHitboxDebugMeleeMask(mask, anchorX, anchorY, facingLeft, maskScale, cameraPosition, color);
        }
    }

    private bool TryGetActiveMeleeHitboxDebug(
        PlayerEntity player,
        out MeleeHitboxMask mask,
        out float anchorX,
        out float anchorY,
        out bool facingLeft,
        out float maskScale)
    {
        mask = null!;
        anchorX = 0f;
        anchorY = 0f;
        facingLeft = false;
        maskScale = 1f;

        if (GetPlayerWeaponAnimationMode(player) != WeaponAnimationMode.Recoil)
        {
            return false;
        }

        if (!TryGetEquippedWeaponPresentation(player, out var presentation)
            || string.IsNullOrWhiteSpace(presentation.MeleeHitboxSpriteName))
        {
            return false;
        }

        if (presentation.UseTorsoReplacement
            && _playerRenderStates.TryGetValue(GetPlayerStateKey(player), out var renderState))
        {
            var duration = MathF.Max(renderState.WeaponAnimationDurationSeconds, 0.0001f);
            var progress = renderState.WeaponAnimationElapsedSeconds / duration;
            if (progress >= ExperimentalDemoknightCatalog.EyelanderFirstFrameProgress)
            {
                return false;
            }
        }

        var loaded = MeleeHitboxMaskCatalog.GetOrLoad(presentation.MeleeHitboxSpriteName);
        if (loaded is null)
        {
            return false;
        }

        mask = loaded;
        var renderPosition = GetRenderPosition(player);
        anchorX = renderPosition.X;
        anchorY = renderPosition.Y + MeleeHitboxMask.TorsoSitDownWorldOffset;
        facingLeft = MathF.Cos(player.AimDirectionDegrees * (MathF.PI / 180f)) < 0f;
        var geometryScale = MathF.Max(0.1f, player.LastToDieUniversalModifiers.MeleeScale);
        var rangeScale = player.IsExperimentalDemoknightEnabled
            ? player.GetExperimentalDemoknightSwordRange() / PlayerEntity.ExperimentalDemoknightSwordBaseRange
            : 1f;
        maskScale = geometryScale * MathF.Max(0.1f, rangeScale);
        return true;
    }

    private bool TryGetEquippedWeaponPresentation(PlayerEntity player, out GameplayItemPresentationDefinition presentation)
    {
        presentation = null!;
        var itemId = player.GameplayLoadoutState.EquippedItemId;
        if (string.IsNullOrWhiteSpace(itemId)
            || !CharacterClassCatalog.RuntimeRegistry.TryGetItem(itemId, out var item))
        {
            return false;
        }

        presentation = item.Presentation;
        return true;
    }

    private bool IsHitboxDebugPlayerLegallyVisible(PlayerEntity player)
        => GetPlayerVisibilityAlpha(player) > 0f;

    private bool IsHitboxDebugAttackOwnerLegallyVisible(int ownerId)
    {
        if (ownerId == GetResolvedLocalPlayerId() || ownerId == _world.LocalPlayer.Id)
        {
            return true;
        }

        var owner = FindPlayerById(ownerId);
        return owner is not null && IsHitboxDebugPlayerLegallyVisible(owner);
    }

    private void DrawHitboxDebugMeleeMask(
        MeleeHitboxMask mask,
        float anchorX,
        float anchorY,
        bool facingLeft,
        float maskScale,
        Vector2 cameraPosition,
        Color color)
    {
        var scale = MathF.Max(0.1f, maskScale);
        for (var pixelY = 0; pixelY < mask.Height; pixelY += 1)
        {
            for (var pixelX = 0; pixelX < mask.Width; pixelX += 1)
            {
                if (!mask.IsOpaqueAtPixel(pixelX, pixelY))
                {
                    continue;
                }

                if (_hitboxDebugMode == HitboxDebugMode.Outline
                    && !IsHitboxDebugMaskEdgePixel(mask, pixelX, pixelY))
                {
                    continue;
                }

                var localX = (pixelX + 0.5f) - mask.OriginX;
                if (facingLeft)
                {
                    localX = -localX;
                }

                var worldX = anchorX + (localX * scale);
                var worldY = anchorY + (((pixelY + 0.5f) - mask.OriginY) * scale);
                var screen = GetWorldScreenPosition(worldX, worldY, cameraPosition);
                var size = MathF.Max(1f, scale);
                DrawScreenPixelRectangle(screen - new Vector2(size * 0.5f, size * 0.5f), size, size, color);
            }
        }
    }

    private static bool IsHitboxDebugMaskEdgePixel(MeleeHitboxMask mask, int pixelX, int pixelY)
    {
        return !mask.IsOpaqueAtPixel(pixelX - 1, pixelY)
            || !mask.IsOpaqueAtPixel(pixelX + 1, pixelY)
            || !mask.IsOpaqueAtPixel(pixelX, pixelY - 1)
            || !mask.IsOpaqueAtPixel(pixelX, pixelY + 1);
    }

    private void DrawHitboxDebugWorldRectangle(
        float left,
        float top,
        float right,
        float bottom,
        Vector2 cameraPosition,
        Color color)
    {
        if (right <= left || bottom <= top)
        {
            return;
        }

        var screenLeft = left - cameraPosition.X;
        var screenTop = top - cameraPosition.Y;
        var width = right - left;
        var height = bottom - top;
        if (_hitboxDebugMode == HitboxDebugMode.Outline)
        {
            DrawHitboxDebugScreenOutline(screenLeft, screenTop, width, height, color);
            return;
        }

        DrawScreenPixelRectangle(new Vector2(screenLeft, screenTop), width, height, color);
    }

    private void DrawHitboxDebugWorldCircle(
        float centerX,
        float centerY,
        float radius,
        Vector2 cameraPosition,
        Color color)
    {
        var safeRadius = MathF.Max(0.5f, radius);
        DrawHitboxDebugWorldRectangle(
            centerX - safeRadius,
            centerY - safeRadius,
            centerX + safeRadius,
            centerY + safeRadius,
            cameraPosition,
            color);
    }

    private void DrawHitboxDebugScreenOutline(float x, float y, float width, float height, Color color)
    {
        const float thickness = 1f;
        DrawScreenPixelRectangle(new Vector2(x, y), width, thickness, color);
        DrawScreenPixelRectangle(new Vector2(x, y + height - thickness), width, thickness, color);
        DrawScreenPixelRectangle(new Vector2(x, y), thickness, height, color);
        DrawScreenPixelRectangle(new Vector2(x + width - thickness, y), thickness, height, color);
    }

    private static Color ApplyHitboxDebugAlpha(Color color)
        => color * HitboxDebugAlpha;
}
