#nullable enable

using System;

namespace OpenGarrison.Client;

internal static class NetworkInterpolationPolicy
{
    public static bool IsSnapshotInterpolationActive(
        bool isConnected,
        bool positionSmoothingEnabled,
        bool isReplayConnection)
    {
        return isConnected || positionSmoothingEnabled || isReplayConnection;
    }

    public static float CalculateSnapshotJitterSampleSeconds(
        float arrivalIntervalSecondsTotal,
        float observedIntervalSecondsTotal,
        int burstCount)
    {
        var jitterSampleSeconds = MathF.Abs(arrivalIntervalSecondsTotal - observedIntervalSecondsTotal);
        var effectiveBurstCount = Math.Max(1, burstCount);
        if (effectiveBurstCount <= 1)
        {
            return jitterSampleSeconds;
        }

        var observedIntervalSeconds = observedIntervalSecondsTotal / effectiveBurstCount;
        return MathF.Max(
            jitterSampleSeconds,
            MathF.Max(0f, (arrivalIntervalSecondsTotal / effectiveBurstCount) - observedIntervalSeconds));
    }

    public static float CalculateSnapshotInterpolationDurationSeconds(
        bool isReplayConnection,
        float targetIntervalSeconds,
        float baseIntervalSeconds)
    {
        return isReplayConnection
            ? Math.Clamp(
                targetIntervalSeconds,
                baseIntervalSeconds * 0.75f,
                baseIntervalSeconds * 1.5f)
            : Math.Clamp(
                targetIntervalSeconds * 0.9f,
                baseIntervalSeconds * 0.5f,
                0.12f);
    }

    public static float CalculateLocalBackTimeSeconds(
        bool isReplayConnection,
        float smoothedSnapshotIntervalSeconds,
        float smoothedSnapshotJitterSeconds,
        float minimumBackTimeSeconds,
        float maximumBackTimeSeconds)
    {
        var desiredBackTimeSeconds = isReplayConnection
            ? MathF.Max(
                minimumBackTimeSeconds,
                (smoothedSnapshotIntervalSeconds * 1.1f) + (smoothedSnapshotJitterSeconds * 1.5f))
            : MathF.Max(
                minimumBackTimeSeconds,
                (smoothedSnapshotIntervalSeconds * 2.35f) + (smoothedSnapshotJitterSeconds * 2.5f));

        return Math.Clamp(desiredBackTimeSeconds, minimumBackTimeSeconds, maximumBackTimeSeconds);
    }

    public static float CalculateRemoteBackTimeSeconds(
        bool isReplayConnection,
        float smoothedSnapshotIntervalSeconds,
        float smoothedSnapshotJitterSeconds,
        float minimumBackTimeSeconds,
        float maximumBackTimeSeconds)
    {
        var desiredBackTimeSeconds = isReplayConnection
            ? MathF.Max(
                minimumBackTimeSeconds,
                (smoothedSnapshotIntervalSeconds * 1.1f) + (smoothedSnapshotJitterSeconds * 1.5f))
            : MathF.Max(
                minimumBackTimeSeconds,
                (smoothedSnapshotIntervalSeconds * 1.75f) + (smoothedSnapshotJitterSeconds * 2.75f));

        return Math.Clamp(desiredBackTimeSeconds, minimumBackTimeSeconds, maximumBackTimeSeconds);
    }

    public static float CalculateEntityBackTimeSeconds(
        float networkSnapshotInterpolationDurationSeconds,
        float smoothedSnapshotIntervalSeconds,
        float smoothedSnapshotJitterSeconds,
        int configuredTickRate,
        int expectedProjectileUpdateIntervalTicks,
        float minimumBackTimeSeconds,
        float maximumBackTimeSeconds)
    {
        var tickRate = Math.Max(1, configuredTickRate);
        var expectedProjectileIntervalSeconds = MathF.Max(
            smoothedSnapshotIntervalSeconds,
            1f / tickRate) * expectedProjectileUpdateIntervalTicks;

        return Math.Clamp(
            MathF.Max(
                networkSnapshotInterpolationDurationSeconds,
                expectedProjectileIntervalSeconds + (smoothedSnapshotJitterSeconds * 2f)),
            minimumBackTimeSeconds,
            maximumBackTimeSeconds);
    }

    public static bool ShouldUseSnapshotHistoryForProjectile(
        bool isReplayConnection,
        int projectileOwnerId,
        int? authoritativeLocalPlayerId)
    {
        return isReplayConnection
            || !authoritativeLocalPlayerId.HasValue
            || projectileOwnerId != authoritativeLocalPlayerId.Value;
    }
}
