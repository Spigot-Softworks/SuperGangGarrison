#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float HealingCharacterEffectDurationSeconds = 0.38f;
    private const float HealingCharacterEffectBandHeightPixels = 10f;
    private const int HealingCharacterEffectHorizontalPaddingPixels = 24;
    private const int HealingCharacterEffectVerticalPaddingTopPixels = 16;
    private const int HealingCharacterEffectVerticalPaddingBottomPixels = 4;
    private const int HealingCharacterEffectMinHealDelta = 14;
    private static readonly Color HealingCharacterEffectBaseColor = new(16, 238, 28);
    private static readonly float[] HealingCharacterEffectBandRowAlphas =
    {
        0.7f, 0.7f,
        0.3f, 0.3f,
        1f, 1f,
        0.3f, 0.3f,
        0.7f, 0.7f,
    };

    private readonly Dictionary<int, int> _observedPlayerHealthForHealingCharacterEffects = new();
    private readonly List<ActiveHealingCharacterEffect> _activeHealingCharacterEffects = new();
    private readonly List<int> _staleObservedHealingHealthPlayerKeys = new();

    private readonly record struct ActiveHealingCharacterEffect(int PlayerId, float ElapsedSeconds);

    private void ResetHealingCharacterEffects()
    {
        _observedPlayerHealthForHealingCharacterEffects.Clear();
        _activeHealingCharacterEffects.Clear();
        _staleObservedHealingHealthPlayerKeys.Clear();
    }

    private void ObservePendingWorldHealingEventsForHealingCharacterEffects()
    {
        foreach (var healingEvent in _world.PendingHealingEvents)
        {
            if (!ShouldTriggerHealingCharacterEffect(healingEvent.Amount))
            {
                continue;
            }

            if (!TryFindRenderablePlayerById(healingEvent.TargetPlayerId, out var player))
            {
                continue;
            }

            if (ShouldSuppressHealingCharacterEffectForPlayer(player, healingEvent.Amount, previousHealth: null))
            {
                SyncObservedHealingHealthForPlayerId(healingEvent.TargetPlayerId);
                continue;
            }

            QueueHealingCharacterEffect(player.Id);
            SyncObservedHealingHealthForPlayerId(healingEvent.TargetPlayerId);
        }
    }

    private bool TryFindRenderablePlayerById(int playerId, out PlayerEntity player)
    {
        foreach (var candidate in _gameplayPlayerRenderController.EnumerateRenderablePlayers())
        {
            if (candidate.Id == playerId || GetPlayerStateKey(candidate) == playerId)
            {
                player = candidate;
                return true;
            }
        }

        player = null!;
        return false;
    }

    private void SyncObservedHealingHealthForPlayerId(int playerId)
    {
        foreach (var player in _gameplayPlayerRenderController.EnumerateRenderablePlayers())
        {
            if (player.Id != playerId)
            {
                continue;
            }

            _observedPlayerHealthForHealingCharacterEffects[GetPlayerStateKey(player)] = Math.Max(0, player.Health);
            return;
        }
    }

    private void ObservePlayerHealthChangesForHealingCharacterEffects()
    {
        var sawPlayers = false;
        foreach (var player in _gameplayPlayerRenderController.EnumerateRenderablePlayers())
        {
            sawPlayers = true;
            ObservePlayerHealthChangeForHealingCharacterEffect(player);
        }

        if (!sawPlayers)
        {
            return;
        }

        _staleObservedHealingHealthPlayerKeys.Clear();
        foreach (var playerStateKey in _observedPlayerHealthForHealingCharacterEffects.Keys)
        {
            _staleObservedHealingHealthPlayerKeys.Add(playerStateKey);
        }

        for (var index = 0; index < _staleObservedHealingHealthPlayerKeys.Count; index += 1)
        {
            var playerStateKey = _staleObservedHealingHealthPlayerKeys[index];
            if (!IsTrackedHealingCharacterEffectPlayerStateKeyActive(playerStateKey))
            {
                _observedPlayerHealthForHealingCharacterEffects.Remove(playerStateKey);
            }
        }
    }

    private bool IsTrackedHealingCharacterEffectPlayerStateKeyActive(int playerStateKey)
    {
        if (GetPlayerStateKey(_world.LocalPlayer) == playerStateKey)
        {
            return true;
        }

        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (GetPlayerStateKey(player) == playerStateKey)
            {
                return true;
            }
        }

        return false;
    }

    private void ObservePlayerHealthChangeForHealingCharacterEffect(PlayerEntity player)
    {
        var playerStateKey = GetPlayerStateKey(player);
        var currentHealth = Math.Max(0, player.Health);
        if (!_observedPlayerHealthForHealingCharacterEffects.TryGetValue(playerStateKey, out var previousHealth))
        {
            _observedPlayerHealthForHealingCharacterEffects[playerStateKey] = currentHealth;
            return;
        }

        _observedPlayerHealthForHealingCharacterEffects[playerStateKey] = currentHealth;
        if (!player.IsAlive)
        {
            return;
        }

        var healDelta = currentHealth - previousHealth;
        if (healDelta > 0
            && ShouldTriggerHealingCharacterEffect(healDelta)
            && !ShouldSuppressHealingCharacterEffectForPlayer(player, healDelta, previousHealth))
        {
            QueueHealingCharacterEffect(player.Id);
        }
    }

    private static bool ShouldSuppressHealingCharacterEffectForPlayer(
        PlayerEntity player,
        int healAmount,
        int? previousHealth)
    {
        if (previousHealth is <= 0)
        {
            return true;
        }

        return previousHealth is null
            && healAmount >= player.MaxHealth - 1
            && Math.Max(0, player.Health) >= player.MaxHealth;
    }

    private static bool ShouldTriggerHealingCharacterEffect(int healDelta)
    {
        return healDelta >= HealingCharacterEffectMinHealDelta;
    }

    private void QueueHealingCharacterEffect(int playerId)
    {
        if (playerId < 0)
        {
            return;
        }

        _activeHealingCharacterEffects.Add(new ActiveHealingCharacterEffect(playerId, 0f));
    }

    private void AdvanceHealingCharacterEffects(float deltaSeconds)
    {
        if (_activeHealingCharacterEffects.Count == 0)
        {
            return;
        }

        for (var index = _activeHealingCharacterEffects.Count - 1; index >= 0; index -= 1)
        {
            var effect = _activeHealingCharacterEffects[index];
            effect = effect with { ElapsedSeconds = effect.ElapsedSeconds + deltaSeconds };
            if (effect.ElapsedSeconds > HealingCharacterEffectDurationSeconds)
            {
                _activeHealingCharacterEffects.RemoveAt(index);
                continue;
            }

            _activeHealingCharacterEffects[index] = effect;
        }
    }

    private void DrawHealingCharacterBodyEffects(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
    {
        DrawHealingCharacterEffectsCore(
            player,
            renderPosition,
            cameraPosition,
            visibilityAlpha,
            bodySelection,
            drawBody: true,
            drawWeapon: false);
    }

    private void DrawHealingCharacterWeaponEffects(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha, PlayerBodySpriteSelection bodySelection)
    {
        DrawHealingCharacterEffectsCore(
            player,
            renderPosition,
            cameraPosition,
            visibilityAlpha,
            bodySelection,
            drawBody: false,
            drawWeapon: true);
    }

    private void DrawHealingCharacterEffectsCore(
        PlayerEntity player,
        Vector2 renderPosition,
        Vector2 cameraPosition,
        float visibilityAlpha,
        PlayerBodySpriteSelection bodySelection,
        bool drawBody,
        bool drawWeapon)
    {
        if (_activeHealingCharacterEffects.Count == 0 || visibilityAlpha <= 0f || !player.IsAlive)
        {
            return;
        }

        var bounds = GetHealingCharacterEffectSweepBounds(player, renderPosition, cameraPosition);
        var outlineTint = Color.Lerp(HealingCharacterEffectBaseColor, Color.White, 0.55f);

        for (var effectIndex = 0; effectIndex < _activeHealingCharacterEffects.Count; effectIndex += 1)
        {
            var effect = _activeHealingCharacterEffects[effectIndex];
            if (!DoesHealingCharacterEffectMatchPlayer(effect.PlayerId, player))
            {
                continue;
            }

            var progress = Math.Clamp(effect.ElapsedSeconds / HealingCharacterEffectDurationSeconds, 0f, 1f);
            var bandBottom = MathHelper.Lerp(bounds.Bottom, bounds.Top, progress);
            var bandTop = bandBottom - HealingCharacterEffectBandHeightPixels;
            DrawHealingCharacterEffectBand(
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

    private void DrawHealingCharacterEffectBand(
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
        for (var rowIndex = 0; rowIndex < HealingCharacterEffectBandRowAlphas.Length; rowIndex += 1)
        {
            var rowAlpha = HealingCharacterEffectBandRowAlphas[rowIndex];
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

            var stripTint = ScaleHealingCharacterEffectOutlineAlpha(outlineTint, visibilityAlpha * rowAlpha);
            DrawWithGameplayScissorRectangle(clip, () =>
            {
                if (drawBody)
                {
                    _gameplayPlayerSpriteRenderController.TryDrawPlayerHealingOutlineAtPosition(
                        player,
                        renderPosition,
                        cameraPosition,
                        stripTint,
                        bodySelection);
                }

                if (drawWeapon && ShouldDrawHealingCharacterEffectWeapon(player, bodySelection))
                {
                    _gameplayWeaponRenderController.TryDrawPlayerHealingWeaponOutlineAtPosition(
                        player,
                        renderPosition,
                        cameraPosition,
                        stripTint,
                        bodySelection);
                }
            });
        }
    }

    private static Color ScaleHealingCharacterEffectOutlineAlpha(Color outlineTint, float alphaScale)
    {
        alphaScale = Math.Clamp(alphaScale, 0f, 1f);
        return new Color(
            outlineTint.R,
            outlineTint.G,
            outlineTint.B,
            (byte)Math.Clamp(outlineTint.A * alphaScale, 0f, 255f));
    }

    private bool DoesHealingCharacterEffectMatchPlayer(int effectPlayerId, PlayerEntity player)
    {
        return effectPlayerId == player.Id || effectPlayerId == GetPlayerStateKey(player);
    }

    private static Rectangle GetHealingCharacterEffectSweepBounds(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition)
    {
        var bounds = GetPlayerScreenBounds(player, renderPosition, cameraPosition);
        bounds.Inflate(HealingCharacterEffectHorizontalPaddingPixels, 0);
        bounds.Y -= HealingCharacterEffectVerticalPaddingTopPixels;
        bounds.Height += HealingCharacterEffectVerticalPaddingTopPixels + HealingCharacterEffectVerticalPaddingBottomPixels;
        return bounds;
    }

    private static bool ShouldDrawHealingCharacterEffectWeapon(PlayerEntity player, PlayerBodySpriteSelection bodySelection)
    {
        return bodySelection.SpriteName is not null
            && !player.IsTaunting;
    }

    private void DrawWithGameplayScissorRectangle(Rectangle clipBounds, Action draw)
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
