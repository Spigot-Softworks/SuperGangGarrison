using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class NetworkInterpolationTimelineTests
{
    [Fact]
    public void AdvanceTowardsSnapsToTargetWhenBeyondThreshold()
    {
        var result = NetworkInterpolationTimeline.AdvanceTowards(
            currentSeconds: 0.0,
            targetSeconds: 1.0,
            deltaSeconds: 0.016);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void AdvanceTowardsMovesTowardTargetWithoutSnapping()
    {
        var result = NetworkInterpolationTimeline.AdvanceTowards(
            currentSeconds: 1.0,
            targetSeconds: 1.1,
            deltaSeconds: 0.016);

        Assert.InRange(result, 1.030, 1.032);
        Assert.True(result > 1.016);
        Assert.True(result < 1.1);
    }

    [Fact]
    public void AdvanceTowardsReturnsTargetForNonFiniteInput()
    {
        Assert.Equal(
            1.0,
            NetworkInterpolationTimeline.AdvanceTowards(double.NaN, 1.0, 0.016));
        Assert.Equal(
            1.0,
            NetworkInterpolationTimeline.AdvanceTowards(0.5, double.PositiveInfinity, 0.016));
    }

    [Fact]
    public void AdvanceTowardsClampsDeltaSecondsToFiftyMilliseconds()
    {
        var clamped = NetworkInterpolationTimeline.AdvanceTowards(1.0, 1.1, 0.05);
        var unclamped = NetworkInterpolationTimeline.AdvanceTowards(1.0, 1.1, 1.0);

        Assert.Equal(clamped, unclamped);
    }

    [Fact]
    public void AdvanceTowardsClampsLagBehindTarget()
    {
        var result = NetworkInterpolationTimeline.AdvanceTowards(
            currentSeconds: 0.0,
            targetSeconds: 0.17,
            deltaSeconds: 0.016);

        Assert.Equal(0.07, result, precision: 6);
    }

    [Fact]
    public void AdvanceTowardsClampsLeadAheadOfTarget()
    {
        var result = NetworkInterpolationTimeline.AdvanceTowards(
            currentSeconds: 1.1,
            targetSeconds: 1.0,
            deltaSeconds: 0.016);

        Assert.Equal(1.04, result, precision: 6);
    }
}
