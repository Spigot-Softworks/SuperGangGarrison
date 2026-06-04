using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerEntityTauntRegressionTests
{
    private const float TauntFrameStepPerTick = 0.3f;

    [Fact]
    public void FullSimulationAdvancesTauntFrameOncePerTick()
    {
        var world = CreateJoinedScoutWorld();
        world.ClientPredictionMode = false;

        Assert.True(world.LocalPlayer.TryStartTaunt());
        Assert.Equal(0f, world.LocalPlayer.TauntFrameIndex);

        world.AdvanceOneTick();

        Assert.Equal(TauntFrameStepPerTick, world.LocalPlayer.TauntFrameIndex, precision: 3);
    }

    [Fact]
    public void ClientPredictionAdvancesTauntFrameOncePerTick()
    {
        var world = CreateJoinedScoutWorld();
        world.ClientPredictionMode = true;

        Assert.True(world.LocalPlayer.TryStartTaunt());
        Assert.Equal(0f, world.LocalPlayer.TauntFrameIndex);

        world.AdvanceOneTick();

        Assert.Equal(TauntFrameStepPerTick, world.LocalPlayer.TauntFrameIndex, precision: 3);
    }

    private static SimulationWorld CreateJoinedScoutWorld()
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Harvest"));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        return world;
    }
}
