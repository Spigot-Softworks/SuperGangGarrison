using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class FixedStepSimulatorTests
{
    [Fact]
    public void StepMarksBacklogDropWhenAdvanceHitsConfiguredCap()
    {
        var world = new SimulationWorld();
        var simulator = new FixedStepSimulator(world);

        var ticks = simulator.Step(
            elapsedSeconds: world.Config.FixedDeltaSeconds * 6,
            beforeTickAdvanced: null,
            onTickAdvanced: null,
            maxTicksPerAdvance: 2);

        Assert.Equal(2, ticks);
        Assert.True(simulator.DroppedSimulationBacklogOnLastAdvance);
        Assert.Equal(
            0,
            simulator.Step(
                elapsedSeconds: 0d,
                beforeTickAdvanced: null,
                onTickAdvanced: null,
                maxTicksPerAdvance: 2));
    }

    [Fact]
    public void StepLeavesBacklogDropFlagClearWhenAdvanceCompletesNormally()
    {
        var world = new SimulationWorld();
        var simulator = new FixedStepSimulator(world);

        var ticks = simulator.Step(
            elapsedSeconds: world.Config.FixedDeltaSeconds * 2,
            beforeTickAdvanced: null,
            onTickAdvanced: null,
            maxTicksPerAdvance: 4);

        Assert.Equal(2, ticks);
        Assert.False(simulator.DroppedSimulationBacklogOnLastAdvance);
    }
}
