#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float HealingCharacterEffectMinHealDelta = 1;
    private const float HealingEmitterMinDurationSeconds = 0.5f;
    private const float HealingCrossSpawnIntervalSeconds = 0.15f;
    private const float HealingCrossLifetimeSeconds = 1f;
    private const float HealingCrossInitialAlpha = 0.95f;
    private const float HealingCrossFloatUpPixelsPerSecond = 16f;
    private const int HealingCrossSquareSizePixels = 2;
    private const int HealingCrossSpawnRegionGridWidth = 16;
    private const int HealingCrossSpawnRegionGridHeight = 8;
    private const float HealingCrossOutlineColorLerp = 0.75f;
    private const int HealingCrossColorVariantCount = 4;
    private const float HealingCrossSpawnHorizontalEdgeBiasExponent = 0.55f;
    private const float HealingCrossSpawnVerticalTopBiasExponent = 1.75f;

    private static readonly (int X, int Y)[] HealingCrossSquareOffsets =
    {
        (2, 0),
        (0, 2),
        (2, 2),
        (4, 2),
        (2, 4),
    };

    private readonly Dictionary<int, int> _observedPlayerHealthForHealingCharacterEffects = new();
    private readonly Dictionary<int, bool> _wasPlayerAliveForHealingCharacterEffects = new();
    private readonly Dictionary<int, HealingCharacterEmitter> _healingCharacterEmitters = new();
    private readonly List<HealingCrossParticle> _healingCrossParticles = new();
    private readonly List<int> _staleObservedHealingHealthPlayerKeys = new();
    private readonly List<int> _staleHealingCharacterEmitterPlayerIds = new();

    private sealed class HealingCharacterEmitter
    {
        public float RemainingSpawnSeconds;
        public float NextSpawnTimer;
    }

    private sealed class HealingCrossParticle
    {
        public int PlayerId;
        public float LocalOffsetX;
        public float LocalOffsetY;
        public float ElapsedSeconds;
        public int ColorVariantIndex;
    }

    private void ResetHealingCharacterEffects()
    {
        _observedPlayerHealthForHealingCharacterEffects.Clear();
        _wasPlayerAliveForHealingCharacterEffects.Clear();
        _healingCharacterEmitters.Clear();
        _healingCrossParticles.Clear();
        _staleObservedHealingHealthPlayerKeys.Clear();
        _staleHealingCharacterEmitterPlayerIds.Clear();
        ResetHealingCharacterSweepEffects();
    }

    private void ObservePendingWorldHealingEventsForHealingCharacterEffects()
    {
        // Online presentation derives healing visuals from authoritative snapshot health only.
        // PendingHealingEvents are local-simulation feedback and are not replicated.
        if (_networkClient.IsConnected)
        {
            return;
        }

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

            if (ShouldSuppressHealingCharacterEffectForPlayer(
                    player,
                    healingEvent.Amount,
                    previousHealth: null,
                    WasPlayerAliveForHealingCharacterEffectLastFrame(player)))
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
                _wasPlayerAliveForHealingCharacterEffects.Remove(playerStateKey);
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
        var wasAlive = WasPlayerAliveForHealingCharacterEffectLastFrame(player);
        if (!_observedPlayerHealthForHealingCharacterEffects.TryGetValue(playerStateKey, out var previousHealth))
        {
            _observedPlayerHealthForHealingCharacterEffects[playerStateKey] = currentHealth;
            _wasPlayerAliveForHealingCharacterEffects[playerStateKey] = player.IsAlive;
            return;
        }

        _observedPlayerHealthForHealingCharacterEffects[playerStateKey] = currentHealth;
        if (!player.IsAlive)
        {
            _wasPlayerAliveForHealingCharacterEffects[playerStateKey] = false;
            return;
        }

        if (!wasAlive)
        {
            _wasPlayerAliveForHealingCharacterEffects[playerStateKey] = true;
            return;
        }

        var healDelta = currentHealth - previousHealth;
        if (healDelta > 0
            && ShouldTriggerHealingCharacterEffect(healDelta)
            && !ShouldSuppressHealingCharacterEffectForPlayer(player, healDelta, previousHealth, wasAlive))
        {
            QueueHealingCharacterEffect(player.Id);
        }

        _wasPlayerAliveForHealingCharacterEffects[playerStateKey] = true;
    }

    private bool WasPlayerAliveForHealingCharacterEffectLastFrame(PlayerEntity player)
    {
        return _wasPlayerAliveForHealingCharacterEffects.TryGetValue(GetPlayerStateKey(player), out var wasAlive)
            && wasAlive;
    }

    private bool IsMedicPassiveSelfHealing(PlayerEntity player, int healAmount)
    {
        if (player.ClassId != PlayerClass.Medic || healAmount <= 0 || IsPlayerBeingHealedByMedicBeam(player))
        {
            return false;
        }

        if (healAmount is not (3 or 4 or 5))
        {
            return false;
        }

        if (GetPlayerStateKey(player) == GetPlayerStateKey(_world.LocalPlayer))
        {
            return healAmount == GetMedicPassiveSelfHealTierAmount(player);
        }

        return true;
    }

    private static int GetMedicPassiveSelfHealTierAmount(PlayerEntity player)
    {
        if (player.TimeUnscathedSourceTicks < PlayerEntity.MedicPassiveRegenFirstThresholdSourceTicks)
        {
            return 3;
        }

        if (player.TimeUnscathedSourceTicks < PlayerEntity.MedicPassiveRegenSecondThresholdSourceTicks)
        {
            return 4;
        }

        return 5;
    }

    private bool IsPlayerBeingHealedByMedicBeam(PlayerEntity target)
    {
        if (_world.LocalPlayer.IsMedicHealing
            && _world.LocalPlayer.MedicHealTargetId == target.Id)
        {
            return true;
        }

        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (player.IsMedicHealing
                && player.MedicHealTargetId == target.Id)
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldSuppressHealingCharacterEffectForPlayer(
        PlayerEntity player,
        int healAmount,
        int? previousHealth,
        bool wasAliveLastFrame)
    {
        if (!player.IsAlive)
        {
            return true;
        }

        if (!wasAliveLastFrame)
        {
            return true;
        }

        if (IsMedicPassiveSelfHealing(player, healAmount))
        {
            return true;
        }

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

        if (!_healingCharacterEmitters.TryGetValue(playerId, out var emitter))
        {
            emitter = new HealingCharacterEmitter();
            _healingCharacterEmitters[playerId] = emitter;
        }

        emitter.RemainingSpawnSeconds = HealingEmitterMinDurationSeconds;
        TryQueueHealingCharacterSweep(playerId);
    }

    private void AdvanceHealingCharacterEffects(float deltaSeconds)
    {
        if (_healingCharacterEmitters.Count > 0)
        {
            _staleHealingCharacterEmitterPlayerIds.Clear();
            foreach (var playerId in _healingCharacterEmitters.Keys)
            {
                _staleHealingCharacterEmitterPlayerIds.Add(playerId);
            }

            for (var index = 0; index < _staleHealingCharacterEmitterPlayerIds.Count; index += 1)
            {
                var playerId = _staleHealingCharacterEmitterPlayerIds[index];
                if (!_healingCharacterEmitters.TryGetValue(playerId, out var emitter))
                {
                    continue;
                }

                emitter.RemainingSpawnSeconds -= deltaSeconds;
                emitter.NextSpawnTimer -= deltaSeconds;
                while (emitter.RemainingSpawnSeconds > 0f && emitter.NextSpawnTimer <= 0f)
                {
                    SpawnHealingCrossParticle(playerId);
                    emitter.NextSpawnTimer += HealingCrossSpawnIntervalSeconds;
                }

                if (emitter.RemainingSpawnSeconds <= 0f)
                {
                    _healingCharacterEmitters.Remove(playerId);
                }
            }
        }

        for (var index = _healingCrossParticles.Count - 1; index >= 0; index -= 1)
        {
            var particle = _healingCrossParticles[index];
            particle.ElapsedSeconds += deltaSeconds;
            if (particle.ElapsedSeconds >= HealingCrossLifetimeSeconds)
            {
                _healingCrossParticles.RemoveAt(index);
                continue;
            }

            _healingCrossParticles[index] = particle;
        }

        AdvanceHealingCharacterSweepEffects(deltaSeconds);
    }

    private void SpawnHealingCrossParticle(int playerId)
    {
        _healingCrossParticles.Add(new HealingCrossParticle
        {
            PlayerId = playerId,
            LocalOffsetX = SampleHealingCrossSpawnHorizontalOffset(),
            LocalOffsetY = SampleHealingCrossSpawnVerticalOffset(),
            ElapsedSeconds = 0f,
            ColorVariantIndex = _visualRandom.Next(HealingCrossColorVariantCount),
        });
    }

    private float SampleHealingCrossSpawnHorizontalOffset()
    {
        return SampleUnitIntervalBiasTowardEdges(_visualRandom.NextSingle(), HealingCrossSpawnHorizontalEdgeBiasExponent);
    }

    private float SampleHealingCrossSpawnVerticalOffset()
    {
        return SampleUnitIntervalBiasTowardStart(_visualRandom.NextSingle(), HealingCrossSpawnVerticalTopBiasExponent);
    }

    private static float SampleUnitIntervalBiasTowardEdges(float value, float exponent)
    {
        var centered = (value * 2f) - 1f;
        var remapped = MathF.Sign(centered) * MathF.Pow(MathF.Abs(centered), exponent);
        return Math.Clamp((remapped + 1f) * 0.5f, 0f, 1f);
    }

    private static float SampleUnitIntervalBiasTowardStart(float value, float exponent)
    {
        return Math.Clamp(1f - MathF.Pow(value, exponent), 0f, 1f);
    }

    private static Color GetHealingCrossColorVariant(PlayerTeam team, int variantIndex)
    {
        var teamColor = GameplayPlayerStatusEffectRenderController.GetUberOverlayColor(team);
        var clampedVariantIndex = Math.Clamp(variantIndex, 0, HealingCrossColorVariantCount - 1);
        var outlineLerp = (clampedVariantIndex / (float)(HealingCrossColorVariantCount - 1)) * HealingCrossOutlineColorLerp;
        return Color.Lerp(teamColor, Color.White, outlineLerp);
    }

    private void DrawHealingCrossParticles(
        PlayerEntity player,
        Vector2 renderPosition,
        Vector2 cameraPosition,
        float visibilityAlpha)
    {
        if (_healingCrossParticles.Count == 0 || visibilityAlpha <= 0f || !player.IsAlive)
        {
            return;
        }

        var bounds = GetPlayerScreenBounds(player, renderPosition, cameraPosition);
        var spawnRegion = GetHealingCrossSpawnRegion(bounds);

        for (var particleIndex = 0; particleIndex < _healingCrossParticles.Count; particleIndex += 1)
        {
            var particle = _healingCrossParticles[particleIndex];
            if (!DoesHealingCharacterEffectMatchPlayer(particle.PlayerId, player))
            {
                continue;
            }

            var lifetimeProgress = Math.Clamp(particle.ElapsedSeconds / HealingCrossLifetimeSeconds, 0f, 1f);
            var alpha = HealingCrossInitialAlpha * (1f - lifetimeProgress) * visibilityAlpha;
            if (alpha <= 0f)
            {
                continue;
            }

            var centerX = spawnRegion.Left + (particle.LocalOffsetX * spawnRegion.Width);
            var centerY = spawnRegion.Top + (particle.LocalOffsetY * spawnRegion.Height)
                - (particle.ElapsedSeconds * HealingCrossFloatUpPixelsPerSecond);
            var crossColor = GetHealingCrossColorVariant(player.Team, particle.ColorVariantIndex);
            DrawHealingCross(centerX, centerY, crossColor * alpha);
        }
    }

    private static Rectangle GetHealingCrossSpawnRegion(Rectangle hitboxBounds)
    {
        var width = HealingCrossSpawnRegionGridWidth * HealingCrossSquareSizePixels;
        var height = HealingCrossSpawnRegionGridHeight * HealingCrossSquareSizePixels;
        var left = hitboxBounds.Center.X - (width / 2f);
        var top = hitboxBounds.Top - height;
        return new Rectangle(
            (int)MathF.Floor(left),
            top,
            width,
            height);
    }

    private void DrawHealingCross(float centerX, float centerY, Color color)
    {
        const int crossExtentPixels = 3;
        var crossTopLeftX = (int)MathF.Floor(centerX - crossExtentPixels);
        var crossTopLeftY = (int)MathF.Floor(centerY - crossExtentPixels);

        for (var squareIndex = 0; squareIndex < HealingCrossSquareOffsets.Length; squareIndex += 1)
        {
            var offset = HealingCrossSquareOffsets[squareIndex];
            var rect = new Rectangle(
                crossTopLeftX + offset.X,
                crossTopLeftY + offset.Y,
                HealingCrossSquareSizePixels,
                HealingCrossSquareSizePixels);
            _spriteBatch.Draw(_pixel, rect, color);
        }
    }

    private bool DoesHealingCharacterEffectMatchPlayer(int effectPlayerId, PlayerEntity player)
    {
        return effectPlayerId == player.Id || effectPlayerId == GetPlayerStateKey(player);
    }
}
