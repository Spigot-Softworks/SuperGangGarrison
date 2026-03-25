#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdateInterpolatedWorldState()
    {
        if (!_networkClient.IsConnected)
        {
            _interpolatedEntityPositions.Clear();
            _interpolatedIntelPositions.Clear();
            _entityInterpolationTracks.Clear();
            _intelInterpolationTracks.Clear();
            _entitySnapshotHistories.Clear();
            _intelSnapshotHistories.Clear();
            _remotePlayerSnapshotHistories.Clear();
            ResetSnapshotStateHistory();
            _lastAppliedSnapshotFrame = 0;
            _hasReceivedSnapshot = false;
            _lastSnapshotReceivedTimeSeconds = -1d;
            _latestSnapshotServerTimeSeconds = -1d;
            _latestSnapshotReceivedClockSeconds = -1d;
            _networkSnapshotInterpolationDurationSeconds = 1f / _config.TicksPerSecond;
            _smoothedSnapshotIntervalSeconds = 1f / _config.TicksPerSecond;
            _smoothedSnapshotJitterSeconds = 0f;
            _remotePlayerInterpolationBackTimeSeconds = RemotePlayerMinimumInterpolationBackTimeSeconds;
            _remotePlayerRenderTimeSeconds = 0d;
            _lastRemotePlayerRenderTimeClockSeconds = -1d;
            _hasRemotePlayerRenderTime = false;
            ResetTransientPresentationEffects();
            ResetProcessedNetworkEventHistory();
            _hasPredictedLocalPlayerPosition = false;
            _hasPredictedLocalActionState = false;
            _predictedLocalPlayerShadow = null;
            _pendingPredictedInputs.Clear();
            return;
        }

        _activeInterpolatedEntityIds.Clear();
        var remotePlayerRenderTimeSeconds = GetRemotePlayerRenderTimeSeconds();
        var entityRenderTimeSeconds = GetEntityRenderTimeSeconds();
        var localPlayerStateKey = GetPlayerStateKey(_world.LocalPlayer);
        _interpolatedEntityPositions[localPlayerStateKey] = new Vector2(_world.LocalPlayer.X, _world.LocalPlayer.Y);
        _activeInterpolatedEntityIds.Add(localPlayerStateKey);
        foreach (var player in EnumerateRemotePlayersForView())
        {
            UpdateInterpolatedRemotePlayerPosition(player, remotePlayerRenderTimeSeconds);
            _activeInterpolatedEntityIds.Add(player.Id);
        }

        foreach (var deadBody in _world.DeadBodies)
        {
            UpdateInterpolatedEntityPosition(deadBody.Id, deadBody.X, deadBody.Y, entityRenderTimeSeconds);
            _activeInterpolatedEntityIds.Add(deadBody.Id);
        }

        foreach (var sentry in _world.Sentries)
        {
            UpdateInterpolatedEntityPosition(sentry.Id, sentry.X, sentry.Y, entityRenderTimeSeconds);
            _activeInterpolatedEntityIds.Add(sentry.Id);
        }

        foreach (var shot in _world.Shots)
        {
            UpdateInterpolatedEntityPosition(shot.Id, shot.X, shot.Y, entityRenderTimeSeconds);
            _activeInterpolatedEntityIds.Add(shot.Id);
        }

        foreach (var bubble in _world.Bubbles)
        {
            UpdateInterpolatedEntityPosition(bubble.Id, bubble.X, bubble.Y, entityRenderTimeSeconds);
            _activeInterpolatedEntityIds.Add(bubble.Id);
        }

        foreach (var blade in _world.Blades)
        {
            UpdateInterpolatedEntityPosition(blade.Id, blade.X, blade.Y, entityRenderTimeSeconds);
            _activeInterpolatedEntityIds.Add(blade.Id);
        }

        foreach (var shot in _world.RevolverShots)
        {
            UpdateInterpolatedEntityPosition(shot.Id, shot.X, shot.Y, entityRenderTimeSeconds);
            _activeInterpolatedEntityIds.Add(shot.Id);
        }

        foreach (var needle in _world.Needles)
        {
            UpdateInterpolatedEntityPosition(needle.Id, needle.X, needle.Y, entityRenderTimeSeconds);
            _activeInterpolatedEntityIds.Add(needle.Id);
        }

        foreach (var flame in _world.Flames)
        {
            UpdateInterpolatedEntityPosition(flame.Id, flame.X, flame.Y, entityRenderTimeSeconds);
            _activeInterpolatedEntityIds.Add(flame.Id);
        }

        foreach (var rocket in _world.Rockets)
        {
            UpdateInterpolatedEntityPosition(rocket.Id, rocket.X, rocket.Y, entityRenderTimeSeconds);
            _activeInterpolatedEntityIds.Add(rocket.Id);
        }

        foreach (var mine in _world.Mines)
        {
            UpdateInterpolatedEntityPosition(mine.Id, mine.X, mine.Y, entityRenderTimeSeconds);
            _activeInterpolatedEntityIds.Add(mine.Id);
        }

        foreach (var gib in _world.PlayerGibs)
        {
            UpdateInterpolatedEntityPosition(gib.Id, gib.X, gib.Y, entityRenderTimeSeconds);
            _activeInterpolatedEntityIds.Add(gib.Id);
        }

        foreach (var bloodDrop in _world.BloodDrops)
        {
            UpdateInterpolatedEntityPosition(bloodDrop.Id, bloodDrop.X, bloodDrop.Y, entityRenderTimeSeconds);
            _activeInterpolatedEntityIds.Add(bloodDrop.Id);
        }

        _staleInterpolatedEntityIds.Clear();
        foreach (var entityId in _interpolatedEntityPositions.Keys)
        {
            if (!_activeInterpolatedEntityIds.Contains(entityId))
            {
                _staleInterpolatedEntityIds.Add(entityId);
            }
        }

        foreach (var entityId in _staleInterpolatedEntityIds)
        {
            _interpolatedEntityPositions.Remove(entityId);
            _entityInterpolationTracks.Remove(entityId);
            _entitySnapshotHistories.Remove(entityId);
            _remotePlayerSnapshotHistories.Remove(entityId);
        }

        UpdateInterpolatedIntelPosition(_world.RedIntel, entityRenderTimeSeconds);
        UpdateInterpolatedIntelPosition(_world.BlueIntel, entityRenderTimeSeconds);
    }

    private void UpdateInterpolatedEntityPosition(int entityId, float x, float y, double renderTimeSeconds)
    {
        if (_entitySnapshotHistories.TryGetValue(entityId, out var history) && history.Count > 0)
        {
            _interpolatedEntityPositions[entityId] = EvaluateEntitySnapshotHistory(history, renderTimeSeconds);
            return;
        }

        if (!_entityInterpolationTracks.TryGetValue(entityId, out var track))
        {
            _interpolatedEntityPositions[entityId] = new Vector2(x, y);
            return;
        }

        _interpolatedEntityPositions[entityId] = EvaluateInterpolationTrack(track);
    }

    private void UpdateInterpolatedIntelPosition(TeamIntelligenceState intelState, double renderTimeSeconds)
    {
        if (_intelSnapshotHistories.TryGetValue(intelState.Team, out var history) && history.Count > 0)
        {
            _interpolatedIntelPositions[intelState.Team] = EvaluateEntitySnapshotHistory(history, renderTimeSeconds);
            return;
        }

        if (!_intelInterpolationTracks.TryGetValue(intelState.Team, out var track))
        {
            _interpolatedIntelPositions[intelState.Team] = new Vector2(intelState.X, intelState.Y);
            return;
        }

        _interpolatedIntelPositions[intelState.Team] = EvaluateInterpolationTrack(track);
    }

    private void CaptureRemoteInterpolationTargets(SnapshotMessage snapshot)
    {
        if (!_networkClient.IsConnected)
        {
            return;
        }

        var snapshotServerTimeSeconds = GetSnapshotTimelineTimeSeconds(snapshot.Frame, snapshot.TickRate);
        var localPlayerSlot = _networkClient.LocalPlayerSlot;
        for (var playerIndex = 0; playerIndex < snapshot.Players.Count; playerIndex += 1)
        {
            var player = snapshot.Players[playerIndex];
            if (player.Slot >= SimulationWorld.FirstSpectatorSlot || player.IsSpectator)
            {
                continue;
            }

            if (!_networkClient.IsSpectator && player.Slot == localPlayerSlot)
            {
                continue;
            }

            AppendRemotePlayerSnapshot(player, snapshotServerTimeSeconds);
        }

        for (var deadBodyIndex = 0; deadBodyIndex < snapshot.DeadBodies.Count; deadBodyIndex += 1)
        {
            var deadBody = snapshot.DeadBodies[deadBodyIndex];
            CaptureEntityInterpolationTarget(true, deadBody.Id, deadBody.X, deadBody.Y, Vector2.Zero, 0f, 0f, snapshotServerTimeSeconds);
        }

        for (var sentryIndex = 0; sentryIndex < snapshot.Sentries.Count; sentryIndex += 1)
        {
            var sentry = snapshot.Sentries[sentryIndex];
            CaptureEntityInterpolationTarget(true, sentry.Id, sentry.X, sentry.Y, Vector2.Zero, 0f, 0f, snapshotServerTimeSeconds);
        }

        for (var shotIndex = 0; shotIndex < snapshot.Shots.Count; shotIndex += 1)
        {
            var shot = snapshot.Shots[shotIndex];
            CaptureProjectileInterpolationTarget(shot.Id, shot.X, shot.Y, new Vector2(shot.VelocityX, shot.VelocityY), 18f, snapshotServerTimeSeconds);
        }

        for (var bubbleIndex = 0; bubbleIndex < snapshot.Bubbles.Count; bubbleIndex += 1)
        {
            var bubble = snapshot.Bubbles[bubbleIndex];
            CaptureProjectileInterpolationTarget(bubble.Id, bubble.X, bubble.Y, new Vector2(bubble.VelocityX, bubble.VelocityY), 18f, snapshotServerTimeSeconds);
        }

        for (var bladeIndex = 0; bladeIndex < snapshot.Blades.Count; bladeIndex += 1)
        {
            var blade = snapshot.Blades[bladeIndex];
            CaptureProjectileInterpolationTarget(blade.Id, blade.X, blade.Y, new Vector2(blade.VelocityX, blade.VelocityY), 18f, snapshotServerTimeSeconds);
        }

        for (var shotIndex = 0; shotIndex < snapshot.RevolverShots.Count; shotIndex += 1)
        {
            var shot = snapshot.RevolverShots[shotIndex];
            CaptureProjectileInterpolationTarget(shot.Id, shot.X, shot.Y, new Vector2(shot.VelocityX, shot.VelocityY), 20f, snapshotServerTimeSeconds);
        }

        for (var needleIndex = 0; needleIndex < snapshot.Needles.Count; needleIndex += 1)
        {
            var needle = snapshot.Needles[needleIndex];
            CaptureProjectileInterpolationTarget(needle.Id, needle.X, needle.Y, new Vector2(needle.VelocityX, needle.VelocityY), 18f, snapshotServerTimeSeconds);
        }

        for (var flameIndex = 0; flameIndex < snapshot.Flames.Count; flameIndex += 1)
        {
            var flame = snapshot.Flames[flameIndex];
            CaptureProjectileInterpolationTarget(flame.Id, flame.X, flame.Y, new Vector2(flame.VelocityX, flame.VelocityY), 36f, snapshotServerTimeSeconds);
        }

        for (var rocketIndex = 0; rocketIndex < snapshot.Rockets.Count; rocketIndex += 1)
        {
            var rocket = snapshot.Rockets[rocketIndex];
            var rocketVelocity = new Vector2(MathF.Cos(rocket.DirectionRadians) * rocket.Speed, MathF.Sin(rocket.DirectionRadians) * rocket.Speed);
            CaptureProjectileInterpolationTarget(rocket.Id, rocket.X, rocket.Y, rocketVelocity, 24f, snapshotServerTimeSeconds);
        }

        for (var mineIndex = 0; mineIndex < snapshot.Mines.Count; mineIndex += 1)
        {
            var mine = snapshot.Mines[mineIndex];
            CaptureProjectileInterpolationTarget(mine.Id, mine.X, mine.Y, new Vector2(mine.VelocityX, mine.VelocityY), 18f, snapshotServerTimeSeconds);
        }

        for (var gibIndex = 0; gibIndex < snapshot.PlayerGibs.Count; gibIndex += 1)
        {
            var gib = snapshot.PlayerGibs[gibIndex];
            CaptureEntityInterpolationTarget(true, gib.Id, gib.X, gib.Y, new Vector2(gib.VelocityX, gib.VelocityY), 0.03f, 12f, snapshotServerTimeSeconds);
        }

        for (var bloodDropIndex = 0; bloodDropIndex < snapshot.BloodDrops.Count; bloodDropIndex += 1)
        {
            var bloodDrop = snapshot.BloodDrops[bloodDropIndex];
            CaptureEntityInterpolationTarget(true, bloodDrop.Id, bloodDrop.X, bloodDrop.Y, new Vector2(bloodDrop.VelocityX, bloodDrop.VelocityY), 0.03f, 8f, snapshotServerTimeSeconds);
        }

        CaptureIntelInterpolationTarget((PlayerTeam)snapshot.RedIntel.Team, snapshot.RedIntel.X, snapshot.RedIntel.Y, snapshotServerTimeSeconds);
        CaptureIntelInterpolationTarget((PlayerTeam)snapshot.BlueIntel.Team, snapshot.BlueIntel.X, snapshot.BlueIntel.Y, snapshotServerTimeSeconds);
    }

    private void UpdateSnapshotTiming(ulong snapshotFrame, int tickRate, int burstCount)
    {
        var effectiveBurstCount = Math.Max(1, burstCount);
        var baseIntervalSeconds = tickRate > 0
            ? MathF.Max(1f / 120f, 1f / tickRate)
            : 1f / SimulationConfig.DefaultTicksPerSecond;
        var snapshotReceivedTimeSeconds = _networkInterpolationClockSeconds;
        var snapshotServerTimeSeconds = GetSnapshotTimelineTimeSeconds(snapshotFrame, tickRate);
        if (_hasReceivedSnapshot)
        {
            var observedIntervalSecondsTotal = (float)Math.Max(
                0d,
                snapshotServerTimeSeconds - _latestSnapshotServerTimeSeconds);
            if (observedIntervalSecondsTotal > 0f)
            {
                var observedIntervalSeconds = observedIntervalSecondsTotal / effectiveBurstCount;
                var clampedObservedIntervalSeconds = Math.Clamp(observedIntervalSeconds, baseIntervalSeconds * 0.5f, 0.25f);
                _smoothedSnapshotIntervalSeconds += (clampedObservedIntervalSeconds - _smoothedSnapshotIntervalSeconds) * 0.2f;

                var arrivalIntervalSecondsTotal = (float)Math.Max(
                    0d,
                    snapshotReceivedTimeSeconds - _lastSnapshotReceivedTimeSeconds);
                var arrivalIntervalSeconds = arrivalIntervalSecondsTotal / effectiveBurstCount;
                var jitterSampleSeconds = MathF.Abs(arrivalIntervalSeconds - observedIntervalSeconds);
                _smoothedSnapshotJitterSeconds += (jitterSampleSeconds - _smoothedSnapshotJitterSeconds) * 0.1f;
            }
        }
        else
        {
            _smoothedSnapshotIntervalSeconds = baseIntervalSeconds;
            _smoothedSnapshotJitterSeconds = 0f;
            _hasReceivedSnapshot = true;
        }

        _lastSnapshotReceivedTimeSeconds = snapshotReceivedTimeSeconds;
        _latestSnapshotServerTimeSeconds = snapshotServerTimeSeconds;
        _latestSnapshotReceivedClockSeconds = snapshotReceivedTimeSeconds;
        var targetIntervalSeconds = MathF.Max(baseIntervalSeconds, _smoothedSnapshotIntervalSeconds);
        _networkSnapshotInterpolationDurationSeconds = Math.Clamp(
            targetIntervalSeconds * 0.9f,
            baseIntervalSeconds * 0.5f,
            0.12f);
        var desiredBackTimeSeconds = Math.Clamp(
            MathF.Max(
                RemotePlayerMinimumInterpolationBackTimeSeconds,
                (_smoothedSnapshotIntervalSeconds * 3f) + (_smoothedSnapshotJitterSeconds * 6f)),
            RemotePlayerMinimumInterpolationBackTimeSeconds,
            RemotePlayerMaximumInterpolationBackTimeSeconds);
        var backTimeAdjustmentAlpha = desiredBackTimeSeconds >= _remotePlayerInterpolationBackTimeSeconds
            ? 0.25f
            : 0.12f;
        _remotePlayerInterpolationBackTimeSeconds +=
            (desiredBackTimeSeconds - _remotePlayerInterpolationBackTimeSeconds) * backTimeAdjustmentAlpha;
        _remotePlayerInterpolationBackTimeSeconds = Math.Clamp(
            _remotePlayerInterpolationBackTimeSeconds,
            RemotePlayerMinimumInterpolationBackTimeSeconds,
            RemotePlayerMaximumInterpolationBackTimeSeconds);
    }

    private void CaptureEntityInterpolationTarget(bool isActive, int entityId, float x, float y)
    {
        CaptureEntityInterpolationTarget(isActive, entityId, x, y, Vector2.Zero, 0f, 0f, _latestSnapshotServerTimeSeconds);
    }

    private void UpdateInterpolatedRemotePlayerPosition(PlayerEntity player, double renderTimeSeconds)
    {
        if (!_remotePlayerSnapshotHistories.TryGetValue(player.Id, out var history) || history.Count == 0)
        {
            _interpolatedEntityPositions[player.Id] = new Vector2(player.X, player.Y);
            return;
        }

        if (history.Count == 1)
        {
            _interpolatedEntityPositions[player.Id] = EvaluateRemotePlayerExtrapolation(history[0], renderTimeSeconds);
            return;
        }

        if (renderTimeSeconds <= history[0].TimeSeconds)
        {
            _interpolatedEntityPositions[player.Id] = history[0].Position;
            return;
        }

        for (var index = 1; index < history.Count; index += 1)
        {
            var newer = history[index];
            if (renderTimeSeconds > newer.TimeSeconds)
            {
                continue;
            }

            var older = history[index - 1];
            _interpolatedEntityPositions[player.Id] = InterpolateRemotePlayerSample(older, newer, renderTimeSeconds);
            return;
        }

        _interpolatedEntityPositions[player.Id] = EvaluateRemotePlayerExtrapolation(history[^1], renderTimeSeconds);
    }

    private void AppendRemotePlayerSnapshot(PlayerEntity player, double snapshotTimeSeconds)
    {
        AppendRemotePlayerSnapshot(
            player.Id,
            new PlayerSnapshotSample(
                new Vector2(player.X, player.Y),
                new Vector2(player.HorizontalSpeed, player.VerticalSpeed),
                snapshotTimeSeconds,
                player.Team,
                player.ClassId,
                player.IsAlive));
    }

    private void AppendRemotePlayerSnapshot(SnapshotPlayerState player, double snapshotTimeSeconds)
    {
        AppendRemotePlayerSnapshot(
            player.PlayerId,
            new PlayerSnapshotSample(
                new Vector2(player.X, player.Y),
                new Vector2(player.HorizontalSpeed, player.VerticalSpeed),
                snapshotTimeSeconds,
                (PlayerTeam)player.Team,
                (PlayerClass)player.ClassId,
                player.IsAlive));
    }

    private void AppendRemotePlayerSnapshot(int playerId, PlayerSnapshotSample sample)
    {
        if (!_remotePlayerSnapshotHistories.TryGetValue(playerId, out var history))
        {
            history = new List<PlayerSnapshotSample>(4);
            _remotePlayerSnapshotHistories[playerId] = history;
        }

        if (ShouldResetRemotePlayerSnapshotHistory(sample, history))
        {
            history.Clear();
            history.Add(sample);
            _interpolatedEntityPositions[playerId] = sample.Position;
        }
        else
        {
            if (history.Count > 0)
            {
                var latest = history[^1];
                if (sample.TimeSeconds <= latest.TimeSeconds)
                {
                    history[^1] = sample;
                }
                else
                {
                    history.Add(sample);
                }
            }
            else
            {
                history.Add(sample);
            }
        }

        var minHistoryTimeSeconds = sample.TimeSeconds - SnapshotHistoryRetentionSeconds;
        while (history.Count > 2 && history[1].TimeSeconds < minHistoryTimeSeconds)
        {
            history.RemoveAt(0);
        }

        if (!_interpolatedEntityPositions.ContainsKey(playerId))
        {
            _interpolatedEntityPositions[playerId] = sample.Position;
        }

        _entityInterpolationTracks.Remove(playerId);
    }

    private static bool ShouldResetRemotePlayerSnapshotHistory(
        PlayerSnapshotSample sample,
        List<PlayerSnapshotSample> history)
    {
        if (history.Count == 0)
        {
            return true;
        }

        var latest = history[^1];
        if (latest.Team != sample.Team
            || latest.ClassId != sample.ClassId
            || latest.IsAlive != sample.IsAlive)
        {
            return true;
        }

        var sampleJumpThreshold = GetRemotePlayerTeleportSnapThreshold(latest, sample);
        if (Vector2.DistanceSquared(latest.Position, sample.Position) > sampleJumpThreshold * sampleJumpThreshold)
        {
            return true;
        }

        return false;
    }

    private static float GetRemotePlayerTeleportSnapThreshold(
        PlayerSnapshotSample older,
        PlayerSnapshotSample newer)
    {
        var intervalSeconds = (float)Math.Clamp(
            newer.TimeSeconds - older.TimeSeconds,
            1d / SimulationConfig.DefaultTicksPerSecond,
            0.2d);
        var maxExpectedSpeed = MathF.Max(older.Velocity.Length(), newer.Velocity.Length());
        return MathF.Max(
            RemotePlayerTeleportSnapDistance,
            (maxExpectedSpeed * intervalSeconds * 4f) + 64f);
    }

    private static Vector2 InterpolateRemotePlayerSample(PlayerSnapshotSample older, PlayerSnapshotSample newer, double renderTimeSeconds)
    {
        var durationSeconds = newer.TimeSeconds - older.TimeSeconds;
        if (durationSeconds <= 0.0001d)
        {
            return newer.Position;
        }

        // Remote player movement is smoother and more stable when we interpolate
        // directly between authoritative positions instead of fitting a cubic curve
        // through raw network velocities that can change abruptly.
        var alpha = float.Clamp((float)((renderTimeSeconds - older.TimeSeconds) / durationSeconds), 0f, 1f);
        return Vector2.Lerp(older.Position, newer.Position, alpha);
    }

    private static Vector2 EvaluateRemotePlayerExtrapolation(PlayerSnapshotSample sample, double renderTimeSeconds)
    {
        var extrapolationSeconds = float.Clamp(
            (float)(renderTimeSeconds - sample.TimeSeconds),
            0f,
            RemotePlayerExtrapolationDurationSeconds);
        if (extrapolationSeconds <= 0f || sample.Velocity == Vector2.Zero)
        {
            return sample.Position;
        }

        var offset = sample.Velocity * extrapolationSeconds;
        var distance = offset.Length();
        var maxDistance = MathF.Max(16f, sample.Velocity.Length() * RemotePlayerExtrapolationDurationSeconds);
        if (distance > maxDistance && distance > 0f)
        {
            offset *= maxDistance / distance;
        }

        return sample.Position + offset;
    }

    private double GetRemotePlayerRenderTimeSeconds()
    {
        var targetRenderTimeSeconds = GetSnapshotRenderTimeSeconds(_remotePlayerInterpolationBackTimeSeconds);
        if (!_hasRemotePlayerRenderTime)
        {
            _remotePlayerRenderTimeSeconds = targetRenderTimeSeconds;
            _lastRemotePlayerRenderTimeClockSeconds = _networkInterpolationClockSeconds;
            _hasRemotePlayerRenderTime = true;
            return _remotePlayerRenderTimeSeconds;
        }

        var deltaSeconds = Math.Clamp(
            _networkInterpolationClockSeconds - _lastRemotePlayerRenderTimeClockSeconds,
            0d,
            0.05d);
        _lastRemotePlayerRenderTimeClockSeconds = _networkInterpolationClockSeconds;
        _remotePlayerRenderTimeSeconds = NetworkInterpolationTimeline.AdvanceTowards(
            _remotePlayerRenderTimeSeconds,
            targetRenderTimeSeconds,
            deltaSeconds);
        return _remotePlayerRenderTimeSeconds;
    }

    private static double GetSnapshotTimelineTimeSeconds(ulong snapshotFrame, int tickRate)
    {
        var effectiveTickRate = tickRate > 0 ? tickRate : SimulationConfig.DefaultTicksPerSecond;
        return snapshotFrame / (double)effectiveTickRate;
    }

    private double GetSnapshotRenderTimeSeconds(float backTimeSeconds)
    {
        return GetEstimatedServerTimeSeconds() - backTimeSeconds;
    }

    private double GetEstimatedServerTimeSeconds()
    {
        if (_latestSnapshotServerTimeSeconds < 0d)
        {
            return _networkInterpolationClockSeconds;
        }

        if (_latestSnapshotReceivedClockSeconds < 0d)
        {
            return _latestSnapshotServerTimeSeconds;
        }

        var extrapolationHeadroomSeconds = Math.Clamp(
            Math.Max(
                _smoothedSnapshotIntervalSeconds + (_smoothedSnapshotJitterSeconds * 2f),
                0.05f),
            0.05f,
            0.15f);
        var localElapsedSinceSnapshotSeconds = Math.Clamp(
            _networkInterpolationClockSeconds - _latestSnapshotReceivedClockSeconds,
            0d,
            extrapolationHeadroomSeconds);
        return _latestSnapshotServerTimeSeconds + localElapsedSinceSnapshotSeconds;
    }

    private void CaptureProjectileInterpolationTarget(int entityId, float x, float y, Vector2 velocity, float maxExtrapolationDistance, double snapshotTimeSeconds)
    {
        var extrapolationDurationSeconds = Math.Clamp(
            MathF.Max(
                _networkSnapshotInterpolationDurationSeconds * 2f,
                1f / LegacyMovementModel.SourceTicksPerSecond),
            1f / LegacyMovementModel.SourceTicksPerSecond,
            ProjectileInterpolationExtrapolationCeilingSeconds);
        var projectileVelocityPerSecond = velocity * _config.TicksPerSecond;
        var projectileMaxExtrapolationDistance = MathF.Max(
            maxExtrapolationDistance,
            projectileVelocityPerSecond.Length() * extrapolationDurationSeconds);
        CaptureEntityInterpolationTarget(
            true,
            entityId,
            x,
            y,
            projectileVelocityPerSecond,
            extrapolationDurationSeconds,
            projectileMaxExtrapolationDistance,
            snapshotTimeSeconds,
            ignoreUnchangedSample: true);
    }

    private void CaptureEntityInterpolationTarget(
        bool isActive,
        int entityId,
        float x,
        float y,
        Vector2 velocity,
        float extrapolationDurationSeconds,
        float maxExtrapolationDistance,
        double snapshotTimeSeconds,
        bool ignoreUnchangedSample = false)
    {
        if (!isActive)
        {
            _entityInterpolationTracks.Remove(entityId);
            _entitySnapshotHistories.Remove(entityId);
            _interpolatedEntityPositions[entityId] = new Vector2(x, y);
            return;
        }

        AppendEntitySnapshot(
            _entitySnapshotHistories,
            entityId,
            new Vector2(x, y),
            velocity,
            snapshotTimeSeconds,
            extrapolationDurationSeconds,
            maxExtrapolationDistance,
            ignoreUnchangedSample);
        _entityInterpolationTracks.Remove(entityId);
    }

    private void CaptureIntelInterpolationTarget(PlayerTeam team, float x, float y, double snapshotTimeSeconds)
    {
        AppendEntitySnapshot(
            _intelSnapshotHistories,
            team,
            new Vector2(x, y),
            Vector2.Zero,
            snapshotTimeSeconds,
            0f,
            0f);
        _intelInterpolationTracks.Remove(team);
    }

    private void AppendEntitySnapshot<TKey>(
        Dictionary<TKey, List<EntitySnapshotSample>> histories,
        TKey key,
        Vector2 position,
        Vector2 velocity,
        double snapshotTimeSeconds,
        float extrapolationDurationSeconds,
        float maxExtrapolationDistance,
        bool ignoreUnchangedSample = false)
        where TKey : notnull
    {
        var sample = new EntitySnapshotSample(
            position,
            velocity,
            snapshotTimeSeconds,
            extrapolationDurationSeconds,
            maxExtrapolationDistance);
        if (!histories.TryGetValue(key, out var history))
        {
            history = new List<EntitySnapshotSample>(4);
            histories[key] = history;
        }

        if (history.Count > 0)
        {
            var latest = history[^1];
            if (ignoreUnchangedSample
                && latest.Position == sample.Position
                && latest.Velocity == sample.Velocity)
            {
                return;
            }

            if (sample.TimeSeconds <= latest.TimeSeconds)
            {
                history[^1] = sample;
            }
            else if (ShouldResetEntitySnapshotHistory(latest, sample))
            {
                history.Clear();
                history.Add(sample);
            }
            else
            {
                history.Add(sample);
            }
        }
        else
        {
            history.Add(sample);
        }

        var minHistoryTimeSeconds = snapshotTimeSeconds - SnapshotHistoryRetentionSeconds;
        while (history.Count > 2 && history[1].TimeSeconds < minHistoryTimeSeconds)
        {
            history.RemoveAt(0);
        }
    }

    private static bool ShouldResetEntitySnapshotHistory(EntitySnapshotSample older, EntitySnapshotSample newer)
    {
        var intervalSeconds = (float)Math.Max(
            1d / SimulationConfig.DefaultTicksPerSecond,
            newer.TimeSeconds - older.TimeSeconds);
        var maxExpectedSpeed = MathF.Max(older.Velocity.Length(), newer.Velocity.Length());
        var extrapolationAllowance = MathF.Max(older.MaxExtrapolationDistance, newer.MaxExtrapolationDistance);
        var snapThreshold = MathF.Max(24f, (maxExpectedSpeed * intervalSeconds * 3f) + extrapolationAllowance + 8f);
        return Vector2.DistanceSquared(older.Position, newer.Position) > snapThreshold * snapThreshold;
    }

    private Vector2 EvaluateEntitySnapshotHistory(List<EntitySnapshotSample> history, double renderTimeSeconds)
    {
        if (history.Count == 0)
        {
            return Vector2.Zero;
        }

        if (history.Count == 1)
        {
            return EvaluateEntitySampleExtrapolation(history[0], renderTimeSeconds);
        }

        if (renderTimeSeconds <= history[0].TimeSeconds)
        {
            return history[0].Position;
        }

        for (var index = 1; index < history.Count; index += 1)
        {
            var newer = history[index];
            if (renderTimeSeconds > newer.TimeSeconds)
            {
                continue;
            }

            var older = history[index - 1];
            return InterpolateEntitySnapshotSample(older, newer, renderTimeSeconds);
        }

        return EvaluateEntitySampleExtrapolation(history[^1], renderTimeSeconds);
    }

    private static Vector2 InterpolateEntitySnapshotSample(EntitySnapshotSample older, EntitySnapshotSample newer, double renderTimeSeconds)
    {
        var durationSeconds = newer.TimeSeconds - older.TimeSeconds;
        if (durationSeconds <= 0.0001d)
        {
            return newer.Position;
        }

        var alpha = float.Clamp((float)((renderTimeSeconds - older.TimeSeconds) / durationSeconds), 0f, 1f);
        return Vector2.Lerp(older.Position, newer.Position, alpha);
    }

    private static Vector2 EvaluateEntitySampleExtrapolation(EntitySnapshotSample sample, double renderTimeSeconds)
    {
        var extrapolationSeconds = float.Clamp(
            (float)(renderTimeSeconds - sample.TimeSeconds),
            0f,
            sample.ExtrapolationDurationSeconds);
        if (extrapolationSeconds <= 0f || sample.Velocity == Vector2.Zero)
        {
            return sample.Position;
        }

        var offset = sample.Velocity * extrapolationSeconds;
        var distance = offset.Length();
        if (sample.MaxExtrapolationDistance > 0f && distance > sample.MaxExtrapolationDistance)
        {
            offset *= sample.MaxExtrapolationDistance / distance;
        }

        return sample.Position + offset;
    }

    private double GetEntityRenderTimeSeconds()
    {
        return GetSnapshotRenderTimeSeconds(MathF.Min(_networkSnapshotInterpolationDurationSeconds, 0.045f));
    }

    private Vector2 EvaluateInterpolationTrack(InterpolationTrack track)
    {
        if (track.DurationSeconds <= 0f)
        {
            if (track.ExtrapolationDurationSeconds <= 0f || track.MaxExtrapolationDistance <= 0f || track.Velocity == Vector2.Zero)
            {
                return track.Target;
            }

            var immediateExtraSeconds = float.Clamp((float)(_networkInterpolationClockSeconds - track.StartTimeSeconds), 0f, track.ExtrapolationDurationSeconds);
            var immediateExtraOffset = track.Velocity * immediateExtraSeconds;
            var immediateExtraDistance = immediateExtraOffset.Length();
            if (immediateExtraDistance > track.MaxExtrapolationDistance)
            {
                immediateExtraOffset *= track.MaxExtrapolationDistance / immediateExtraDistance;
            }

            return track.Target + immediateExtraOffset;
        }

        var elapsedSeconds = _networkInterpolationClockSeconds - track.StartTimeSeconds;
        if (elapsedSeconds <= track.DurationSeconds)
        {
            var alpha = float.Clamp((float)(elapsedSeconds / track.DurationSeconds), 0f, 1f);
            return Vector2.Lerp(track.Start, track.Target, alpha);
        }

        if (track.ExtrapolationDurationSeconds <= 0f || track.MaxExtrapolationDistance <= 0f || track.Velocity == Vector2.Zero)
        {
            return track.Target;
        }

        var extraSeconds = float.Clamp((float)(elapsedSeconds - track.DurationSeconds), 0f, track.ExtrapolationDurationSeconds);
        var extraOffset = track.Velocity * extraSeconds;
        var extraDistance = extraOffset.Length();
        if (extraDistance > track.MaxExtrapolationDistance)
        {
            extraOffset *= track.MaxExtrapolationDistance / extraDistance;
        }

        return track.Target + extraOffset;
    }
}
