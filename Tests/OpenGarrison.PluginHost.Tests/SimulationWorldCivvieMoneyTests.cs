using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldCivvieMoneyTests
{
    [Fact]
    public void MoneyPickupHealsWhenPlayerBodyTouchesPickupMarker()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var ally = AddRedAlly(world, slot: 2);
        ally.TeleportTo(100f, 100f);
        ally.ForceSetHealth(ally.MaxHealth - 2);

        var pickupX = ally.Right + 16f;
        world.TestAddCivvieMoneyPickup(
            world.LocalPlayer.Id,
            PlayerTeam.Red,
            pickupX,
            ally.Y);

        world.AdvanceOneTick();

        Assert.Equal(ally.MaxHealth, ally.Health);
        Assert.Equal(0, world.TestCivvieMoneyPickupCount);
        Assert.Contains(world.PendingHealingEvents, healing => healing.TargetPlayerId == ally.Id && healing.Amount == 2);
    }

    [Fact]
    public void MoneyPickupDoesNotDisappearWhenTouchingFullHealthAlly()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var ally = AddRedAlly(world, slot: 2);
        ally.TeleportTo(100f, 100f);
        ally.ForceSetHealth(ally.MaxHealth);

        world.TestAddCivvieMoneyPickup(
            world.LocalPlayer.Id,
            PlayerTeam.Red,
            ally.X,
            ally.Y);

        world.AdvanceOneTick();

        Assert.Equal(ally.MaxHealth, ally.Health);
        Assert.Equal(1, world.TestCivvieMoneyPickupCount);
        Assert.DoesNotContain(world.PendingHealingEvents, healing => healing.TargetPlayerId == ally.Id);
    }

    private static PlayerEntity AddRedAlly(SimulationWorld world, byte slot)
    {
        Assert.True(world.NetworkPlayers.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.NetworkPlayers.TrySetNetworkPlayerTeam(slot, PlayerTeam.Red));
        Assert.True(world.NetworkPlayers.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Scout));
        Assert.True(world.NetworkPlayers.TryGetNetworkPlayer(slot, out var player));
        return player;
    }
}
