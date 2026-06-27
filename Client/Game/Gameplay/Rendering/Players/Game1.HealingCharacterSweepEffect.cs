#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    // Preserved for future reuse. Cross particles remain the active healing visual.
    private const bool HealingCharacterSweepEffectEnabled = false;

    private const float HealingCharacterSweepDurationSeconds = 0.38f;
    private const float HealingCharacterSweepBandHeightPixels = 8f;
    private const float HealingCharacterSweepMinSpawnIntervalSeconds = 0.3f;
    private const int HealingCharacterSweepHorizontalPaddingPixels = 24;
    private const int HealingCharacterSweepVerticalPaddingTopPixels = 16;
    private const int HealingCharacterSweepVerticalPaddingBottomPixels = 4;
    private const int HealingCharacterSweepBandEdgeRowCount = 2;
    private const float HealingCharacterSweepBandEdgeRowAlpha = 0.38f;
    private static readonly Color HealingCharacterSweepBaseColor = new(16, 238, 28);
    private static readonly Vector2[] HealingCharacterSweepTopEdgeOutlineOffsets = { new(0f, -2f) };
    private static readonly Vector2[] HealingCharacterSweepBottomEdgeOutlineOffsets = { new(0f, 2f) };

    private readonly List<ActiveHealingCharacterSweep> _activeHealingCharacterSweeps = new();
    private readonly Dictionary<int, float> _healingCharacterSweepSpawnCooldownByPlayerId = new();
    private readonly List<int> _staleHealingCharacterSweepCooldownPlayerIds = new();

    private readonly record struct ActiveHealingCharacterSweep(int PlayerId, float ElapsedSeconds);

    private void ResetHealingCharacterSweepEffects()
    {
        _activeHealingCharacterSweeps.Clear();
        _healingCharacterSweepSpawnCooldownByPlayerId.Clear();
        _staleHealingCharacterSweepCooldownPlayerIds.Clear();
    }

    private void TryQueueHealingCharacterSweep(int playerId)
    {
        if (!HealingCharacterSweepEffectEnabled || playerId < 0)
        {
            return;
        }

        if (_healingCharacterSweepSpawnCooldownByPlayerId.TryGetValue(playerId, out var cooldownRemaining)
            && cooldownRemaining > 0f)
        {
            return;
        }

        _activeHealingCharacterSweeps.Add(new ActiveHealingCharacterSweep(playerId, 0f));
        _healingCharacterSweepSpawnCooldownByPlayerId[playerId] = HealingCharacterSweepMinSpawnIntervalSeconds;
    }

    private void AdvanceHealingCharacterSweepEffects(float deltaSeconds)
    {
        if (!HealingCharacterSweepEffectEnabled)
        {
            return;
        }

        if (_healingCharacterSweepSpawnCooldownByPlayerId.Count > 0)
        {
            _staleHealingCharacterSweepCooldownPlayerIds.Clear();
            foreach (var playerId in _healingCharacterSweepSpawnCooldownByPlayerId.Keys)
            {
                _staleHealingCharacterSweepCooldownPlayerIds.Add(playerId);
            }

            for (var index = 0; index < _staleHealingCharacterSweepCooldownPlayerIds.Count; index += 1)
            {
                var playerId = _staleHealingCharacterSweepCooldownPlayerIds[index];
                if (!_healingCharacterSweepSpawnCooldownByPlayerId.TryGetValue(playerId, out var cooldownRemaining))
                {
                    continue;
                }

                cooldownRemaining -= deltaSeconds;
                if (cooldownRemaining <= 0f)
                {
                    _healingCharacterSweepSpawnCooldownByPlayerId.Remove(playerId);
                    continue;
                }

                _healingCharacterSweepSpawnCooldownByPlayerId[playerId] = cooldownRemaining;
            }
        }

        for (var index = _activeHealingCharacterSweeps.Count - 1; index >= 0; index -= 1)
        {
            var sweep = _activeHealingCharacterSweeps[index];
            sweep = sweep with { ElapsedSeconds = sweep.ElapsedSeconds + deltaSeconds };
            if (sweep.ElapsedSeconds > HealingCharacterSweepDurationSeconds)
            {
                _activeHealingCharacterSweeps.RemoveAt(index);
                continue;
            }

            _activeHealingCharacterSweeps[index] = sweep;
        }
    }

    private void DrawHealingCharacterBodySweepEffects(
        PlayerEntity player,
        Vector2 renderPosition,
        Vector2 cameraPosition,
        float visibilityAlpha,
        PlayerBodySpriteSelection bodySelection)
    {
        DrawHealingCharacterSweepEffectsCore(
            player,
            renderPosition,
            cameraPosition,
            visibilityAlpha,
            bodySelection,
            drawBody: true,
            drawWeapon: false);
    }

    private void DrawHealingCharacterWeaponSweepEffects(
        PlayerEntity player,
        Vector2 renderPosition,
        Vector2 cameraPosition,
        float visibilityAlpha,
        PlayerBodySpriteSelection bodySelection)
    {
        DrawHealingCharacterSweepEffectsCore(
            player,
            renderPosition,
            cameraPosition,
            visibilityAlpha,
            bodySelection,
            drawBody: false,
            drawWeapon: true);
    }

    private void DrawHealingCharacterSweepEffectsCore(
        PlayerEntity player,
        Vector2 renderPosition,
        Vector2 cameraPosition,
        float visibilityAlpha,
        PlayerBodySpriteSelection bodySelection,
        bool drawBody,
        bool drawWeapon)
    {
        if (!HealingCharacterSweepEffectEnabled
            || _activeHealingCharacterSweeps.Count == 0
            || visibilityAlpha <= 0f
            || !player.IsAlive)
        {
            return;
        }

        var bounds = GetHealingCharacterSweepBounds(player, renderPosition, cameraPosition);
        var outlineTint = Color.Lerp(HealingCharacterSweepBaseColor, Color.White, 0.55f);

        for (var effectIndex = 0; effectIndex < _activeHealingCharacterSweeps.Count; effectIndex += 1)
        {
            var sweep = _activeHealingCharacterSweeps[effectIndex];
            if (!DoesHealingCharacterEffectMatchPlayer(sweep.PlayerId, player))
            {
                continue;
            }

            var progress = Math.Clamp(sweep.ElapsedSeconds / HealingCharacterSweepDurationSeconds, 0f, 1f);
            var bandBottom = MathHelper.Lerp(bounds.Bottom, bounds.Top, progress);
            var bandTop = bandBottom - HealingCharacterSweepBandHeightPixels;
            DrawHealingCharacterSweepBand(
                player,
                renderPosition,
                cameraPosition,
                visibilityAlpha,
                bodySelection,
                bounds,
                bandTop,
                outlineTint,
                drawBody,
                drawWeapon);
        }
    }

    private void DrawHealingCharacterSweepBand(
        PlayerEntity player,
        Vector2 renderPosition,
        Vector2 cameraPosition,
        float visibilityAlpha,
        PlayerBodySpriteSelection bodySelection,
        Rectangle horizontalBounds,
        float bandTop,
        Color outlineTint,
        bool drawBody,
        bool drawWeapon)
    {
        var bandRowCount = (int)HealingCharacterSweepBandHeightPixels;
        for (var rowIndex = 0; rowIndex < bandRowCount; rowIndex += 1)
        {
            var isEdgeRow = rowIndex < HealingCharacterSweepBandEdgeRowCount
                || rowIndex >= bandRowCount - HealingCharacterSweepBandEdgeRowCount;
            var rowAlpha = isEdgeRow ? HealingCharacterSweepBandEdgeRowAlpha : 1f;
            IReadOnlyList<Vector2>? outlineOffsets = null;
            if (isEdgeRow)
            {
                outlineOffsets = rowIndex < HealingCharacterSweepBandEdgeRowCount
                    ? HealingCharacterSweepTopEdgeOutlineOffsets
                    : HealingCharacterSweepBottomEdgeOutlineOffsets;
            }

            var clip = Rectangle.Intersect(
                new Rectangle(
                    horizontalBounds.X,
                    (int)MathF.Floor(bandTop + rowIndex),
                    horizontalBounds.Width,
                    1),
                new Rectangle(0, 0, ViewportWidth, ViewportHeight));
            if (clip.Width <= 0 || clip.Height <= 0)
            {
                continue;
            }

            var stripTint = ScaleHealingCharacterSweepOutlineAlpha(outlineTint, visibilityAlpha * rowAlpha);
            DrawWithHealingCharacterSweepScissorRectangle(clip, () =>
            {
                if (drawBody)
                {
                    _gameplayPlayerSpriteRenderController.TryDrawPlayerHealingOutlineAtPosition(
                        player,
                        renderPosition,
                        cameraPosition,
                        stripTint,
                        bodySelection,
                        outlineOffsets);
                }

                if (drawWeapon && ShouldDrawHealingCharacterSweepWeapon(player, bodySelection))
                {
                    _gameplayWeaponRenderController.TryDrawPlayerHealingWeaponOutlineAtPosition(
                        player,
                        renderPosition,
                        cameraPosition,
                        stripTint,
                        bodySelection,
                        outlineOffsets);
                }
            });
        }
    }

    private static Color ScaleHealingCharacterSweepOutlineAlpha(Color outlineTint, float alphaScale)
    {
        alphaScale = Math.Clamp(alphaScale, 0f, 1f);
        return new Color(
            outlineTint.R,
            outlineTint.G,
            outlineTint.B,
            (byte)Math.Clamp(outlineTint.A * alphaScale, 0f, 255f));
    }

    private Rectangle GetHealingCharacterSweepBounds(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition)
    {
        var bounds = GetPlayerScreenBounds(player, renderPosition, cameraPosition);
        bounds.Inflate(HealingCharacterSweepHorizontalPaddingPixels, 0);
        bounds.Y -= HealingCharacterSweepVerticalPaddingTopPixels;
        bounds.Height += HealingCharacterSweepVerticalPaddingTopPixels + HealingCharacterSweepVerticalPaddingBottomPixels;
        return bounds;
    }

    private static bool ShouldDrawHealingCharacterSweepWeapon(PlayerEntity player, PlayerBodySpriteSelection bodySelection)
    {
        return bodySelection.SpriteName is not null
            && !player.IsTaunting;
    }

    private void DrawWithHealingCharacterSweepScissorRectangle(Rectangle clipBounds, Action draw)
    {
        if (clipBounds.Width <= 0 || clipBounds.Height <= 0)
        {
            return;
        }

        _spriteBatch.End();
        var previousScissor = GraphicsDevice.ScissorRectangle;
        using var scissorRasterizer = new RasterizerState
        {
            CullMode = CullMode.None,
            ScissorTestEnable = true,
        };

        GraphicsDevice.ScissorRectangle = clipBounds;
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: scissorRasterizer);
        draw();
        _spriteBatch.End();
        GraphicsDevice.ScissorRectangle = previousScissor;
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
    }
}
