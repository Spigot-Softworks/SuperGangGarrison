using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotBrainCarrierPerformanceTests
{
    [Fact]
    public void DynamicEscortCarrierReusesMovingCarrierRouteWithinSameGoalBand()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        Assert.True(world.TryLoadLevel("Conflict", 1, preservePlayerStats: false));
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.SetPendingLocalPlayerClass(PlayerClass.Scout);
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.TeleportTo(2500f, 444f);
        Assert.True(world.ForceGiveEnemyIntelToLocalPlayer());

        var escort = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Red, 1300f, 612f);
        var controller = new BotBrainController();

        _ = controller.Think(escort, world, PlayerTeam.Red);

        Assert.Contains("directRoute=dynamicEscortCarrier", controller.LastDirectDriveTrace, StringComparison.Ordinal);

        world.LocalPlayer.TeleportTo(world.LocalPlayer.X - 64f, world.LocalPlayer.Y);
        _ = controller.Think(escort, world, PlayerTeam.Red);

        Assert.Contains("directRoute=dynamicEscortCarrier", controller.LastDirectDriveTrace, StringComparison.Ordinal);
        Assert.Contains("reuseMoving", controller.LastDirectDriveTrace, StringComparison.Ordinal);
    }

    private static PlayerEntity AddNetworkPlayer(
        SimulationWorld world,
        byte slot,
        PlayerClass playerClass,
        PlayerTeam team,
        float x,
        float y)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        player.TeleportTo(x, y);
        return player;
    }
}
