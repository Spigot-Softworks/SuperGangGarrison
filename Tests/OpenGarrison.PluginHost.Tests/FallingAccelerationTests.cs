using OpenGarrison.Core;
using Xunit;
namespace OpenGarrison.PluginHost.Tests;
public sealed class FallingAccelerationTests
{
    [Fact]
    public void StrongerGravityOnlyAppliesAfterApexAndIsIndependentOfStepSize()
    {
        Assert.Equal(-91f, LegacyMovementModel.AdvanceVerticalSpeedHalfStep(-100f, 0.6f, 1f / 30f), 3);
        Assert.Equal(11.25f, LegacyMovementModel.AdvanceVerticalSpeedHalfStep(0f, 0.6f, 1f / 30f), 3);
        var largeStep = LegacyMovementModel.AdvanceVerticalSpeedHalfStep(-4f, 0.6f, 1f / 30f);
        var smallSteps = -4f;
        for (var i = 0; i < 10; i++)
            smallSteps = LegacyMovementModel.AdvanceVerticalSpeedHalfStep(smallSteps, 0.6f, 1f / 300f);
        Assert.Equal(largeStep, smallSteps, 3);
        // MaxFallSpeedPerTick is 24 (GG2 interop tuning), so the 720 clamp no
        // longer bites here; the old 300 expectation assumed the former 10.
        Assert.Equal(310.25f, LegacyMovementModel.AdvanceVerticalSpeedHalfStep(299f, 0.6f, 1f / 30f));
    }
}
