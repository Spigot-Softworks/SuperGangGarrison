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
        // Conflict currently has no compatible shipped OG2 graph. This test
        // exercises dynamic escort route reuse, so provide the smallest
        // explicit route fixture instead of coupling the behavior assertion
        // to whichever maps happen to ship a graph.
        var controller = new BotBrainController(
            CreateEscortRouteGraph(escort.X, escort.Y, world.LocalPlayer.X, world.LocalPlayer.Y),
            forceAlphaNavigation: true);

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

    private static NavGraph CreateEscortRouteGraph(
        float escortX,
        float escortY,
        float carrierX,
        float carrierY)
    {
        var nodes = new[]
        {
            new NavNode(escortX, escortY, NavNodeKind.Surface, 1),
            new NavNode(carrierX, carrierY, NavNodeKind.Surface, 1),
        };
        var adjacency = new[]
        {
            new List<NavEdge>(),
            new List<NavEdge>(),
        };
        var cost = MathF.Sqrt(
            MathF.Pow(carrierX - escortX, 2f)
            + MathF.Pow(carrierY - escortY, 2f));
        adjacency[0].Add(new NavEdge(1, NavEdgeKind.Walk, cost));
        adjacency[1].Add(new NavEdge(0, NavEdgeKind.Walk, cost));
        return new NavGraph(
            nodes,
            adjacency,
            levelName: "SyntheticEscort",
            mode: GameModeKind.CaptureTheFlag);
    }
}
