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

    [Fact]
    public void InterpolationAlphaTracksRemainingFixedStepFraction()
    {
        var world = new SimulationWorld();
        var simulator = new FixedStepSimulator(world);

        Assert.Equal(0f, simulator.InterpolationAlpha);

        Assert.Equal(0, simulator.Step(world.Config.FixedDeltaSeconds * 0.5d));
        Assert.InRange(simulator.InterpolationAlpha, 0.49f, 0.51f);

        Assert.Equal(1, simulator.Step(world.Config.FixedDeltaSeconds * 0.6d));
        Assert.InRange(simulator.InterpolationAlpha, 0.09f, 0.11f);
    }

    [Fact]
    public void StepWithNegativeElapsedSecondsAdvancesNoTicks()
    {
        var world = new SimulationWorld();
        var simulator = new FixedStepSimulator(world);

        Assert.Equal(0, simulator.Step(-1.0d));
        Assert.False(simulator.DroppedSimulationBacklogOnLastAdvance);
    }

    [Fact]
    public void StepWithNaNElapsedSecondsAdvancesNoTicks()
    {
        var world = new SimulationWorld();
        var simulator = new FixedStepSimulator(world);

        Assert.Equal(0, simulator.Step(double.NaN));
        Assert.False(simulator.DroppedSimulationBacklogOnLastAdvance);
    }

    [Fact]
    public void StepWithZeroMaxTicksPerAdvanceDropsBacklog()
    {
        var world = new SimulationWorld();
        var simulator = new FixedStepSimulator(world);

        var ticks = simulator.Step(
            world.Config.FixedDeltaSeconds,
            beforeTickAdvanced: null,
            onTickAdvanced: null,
            maxTicksPerAdvance: 0);

        Assert.Equal(0, ticks);
        Assert.True(simulator.DroppedSimulationBacklogOnLastAdvance);
    }
}
