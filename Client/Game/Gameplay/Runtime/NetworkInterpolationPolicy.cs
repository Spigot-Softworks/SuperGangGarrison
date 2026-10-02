#nullable enable

using System;

namespace OpenGarrison.Client;

internal static class NetworkInterpolationPolicy
{
    /// <summary>Weight of each new snapshot when filtering the server clock offset.</summary>
    public const double ServerClockOffsetFilterAlpha = 0.05d;

    /// <summary>Offset change treated as a timeline reset (reconnect, map change) rather than jitter.</summary>
    public const double ServerClockOffsetResetThresholdSeconds = 0.1d;

    /// <summary>How far the filtered estimate may trail the latest-snapshot anchor before it is pulled in.</summary>
    public const double ServerClockMaxBehindAnchorSeconds = 0.05d;

    /// <summary>
    /// Filters the offset between the server timeline and the local clock.
    /// </summary>
    /// <remarks>
    /// Snapshots are timestamped when the frame that processes them runs, so
    /// each raw offset sample carries up to a frame of quantization plus network
    /// jitter. Re-anchoring the estimated server time on every snapshot makes
    /// the render clock chase a sawtooth and speed up and slow down by roughly
    /// 20% frame to frame, which reads as uneven motion of remote players and
    /// projectiles. A slow average keeps the estimate advancing with the local
    /// clock while still following real drift.
    /// </remarks>
    public static double FilterServerClockOffset(bool hasFilteredOffset, double filteredOffsetSeconds, double sampleOffsetSeconds)
    {
        if (!double.IsFinite(sampleOffsetSeconds))
        {
            return filteredOffsetSeconds;
        }

        if (!hasFilteredOffset
            || !double.IsFinite(filteredOffsetSeconds)
            || Math.Abs(sampleOffsetSeconds - filteredOffsetSeconds) > ServerClockOffsetResetThresholdSeconds)
        {
            return sampleOffsetSeconds;
        }

        return filteredOffsetSeconds + ((sampleOffsetSeconds - filteredOffsetSeconds) * ServerClockOffsetFilterAlpha);
    }

    /// <summary>
    /// Estimated server time from the filtered offset, bounded by the newest
    /// snapshot so it never extrapolates past the anchor's headroom and never
    /// trails it far enough to delay presentation.
    /// </summary>
    public static double EstimateServerTimeSeconds(
        double clockSeconds,
        double filteredOffsetSeconds,
        double latestSnapshotServerTimeSeconds,
        double anchoredEstimateSeconds,
        double extrapolationHeadroomSeconds)
    {
        var filteredEstimate = clockSeconds + filteredOffsetSeconds;
        var upperBound = latestSnapshotServerTimeSeconds + extrapolationHeadroomSeconds;
        var lowerBound = Math.Min(upperBound, anchoredEstimateSeconds - ServerClockMaxBehindAnchorSeconds);
        return Math.Clamp(filteredEstimate, lowerBound, upperBound);
    }

    public static bool IsSourceFrameReady(ulong sourceFrame, int tickRate, double renderTimeSeconds)
        => sourceFrame == 0 || sourceFrame / (double)Math.Max(1, tickRate) <= renderTimeSeconds;

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

    public static float CalculateProjectileBackTimeSeconds(
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
        var desiredBackTimeSeconds = MathF.Max(
            networkSnapshotInterpolationDurationSeconds,
            MathF.Max(
                expectedProjectileIntervalSeconds * 2f,
                expectedProjectileIntervalSeconds + (smoothedSnapshotJitterSeconds * 3f)));

        return Math.Clamp(desiredBackTimeSeconds, minimumBackTimeSeconds, maximumBackTimeSeconds);
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
