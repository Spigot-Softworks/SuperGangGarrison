using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldNetworkPlayerEnumerationTests
{
    [Fact]
    public void EmptyWorldEnumeratesOnlyTheLocalPlayer()
    {
        var world = new SimulationWorld();

        Assert.Equal(
            [(byte)SimulationWorld.LocalPlayerSlot],
            world.NetworkPlayers.EnumerateReplicatedNetworkPlayers().Select(entry => entry.Slot));
        Assert.Equal(
            [(byte)SimulationWorld.LocalPlayerSlot],
            world.NetworkPlayers.EnumerateActiveNetworkPlayers().Select(entry => entry.Slot));
    }

    [Fact]
    public void AwaitingRemoteJoinIsReplicatedButNotActive()
    {
        var world = new SimulationWorld();
        Assert.True(world.NetworkPlayers.TryPrepareNetworkPlayerJoin(4));

        Assert.Equal(
            [
                (byte)SimulationWorld.LocalPlayerSlot,
                (byte)4,
            ],
            world.NetworkPlayers.EnumerateReplicatedNetworkPlayers().Select(entry => entry.Slot));
        Assert.Equal(
            [(byte)SimulationWorld.LocalPlayerSlot],
            world.NetworkPlayers.EnumerateActiveNetworkPlayers().Select(entry => entry.Slot));
    }

    [Fact]
    public void ActiveRemoteSlotsRemainInSlotOrderWithoutMaterializingUnusedSlots()
    {
        var world = new SimulationWorld();
        Assert.True(world.NetworkPlayers.TryPrepareNetworkPlayerJoin(7));
        Assert.True(world.NetworkPlayers.TryApplyNetworkPlayerClassSelection(7, PlayerClass.Heavy));
        Assert.True(world.NetworkPlayers.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.NetworkPlayers.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));

        Assert.Equal(
            [
                (byte)SimulationWorld.LocalPlayerSlot,
                (byte)2,
                (byte)7,
            ],
            world.NetworkPlayers.EnumerateActiveNetworkPlayers().Select(entry => entry.Slot));
        Assert.DoesNotContain(
            world.NetworkPlayers.EnumerateReplicatedNetworkPlayers(),
            entry => entry.Slot is 3 or 6 or 8);
    }
}
