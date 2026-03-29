using System;

namespace OpenGarrison.Core;

public static class NetworkInterpolationTimeline
{
    public static double AdvanceTowards(
        double currentSeconds,
        double targetSeconds,
        double deltaSeconds,
        double snapThresholdSeconds = 0.18d,
        double catchUpRate = 12d,
        double slowDownRate = 8d,
        double maxLagBehindTargetSeconds = 0.10d,
        double maxLeadAheadOfTargetSeconds = 0.04d)
    {
        if (!double.IsFinite(currentSeconds) || !double.IsFinite(targetSeconds))
        {
            return targetSeconds;
        }

        var clampedDeltaSeconds = double.Clamp(deltaSeconds, 0d, 0.05d);
        var advancedSeconds = currentSeconds + clampedDeltaSeconds;
        var errorSeconds = targetSeconds - advancedSeconds;
        if (Math.Abs(errorSeconds) >= snapThresholdSeconds)
        {
            return targetSeconds;
        }

        var correctionRate = errorSeconds >= 0d ? catchUpRate : slowDownRate;
        var correctionAlpha = 1d - Math.Exp(-correctionRate * clampedDeltaSeconds);
        var correctedSeconds = advancedSeconds + (errorSeconds * correctionAlpha);
        var minSeconds = targetSeconds - maxLagBehindTargetSeconds;
        var maxSeconds = targetSeconds + maxLeadAheadOfTargetSeconds;
        return double.Clamp(correctedSeconds, minSeconds, maxSeconds);
    }
}
