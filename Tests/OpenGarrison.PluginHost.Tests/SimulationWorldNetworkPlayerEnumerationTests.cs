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
            world.NetworkPlayerRules.EnumerateReplicatedNetworkPlayers().Select(entry => entry.Slot));
        Assert.Equal(
            [(byte)SimulationWorld.LocalPlayerSlot],
            world.NetworkPlayerRules.EnumerateActiveNetworkPlayers().Select(entry => entry.Slot));
    }

    [Fact]
    public void AwaitingRemoteJoinIsReplicatedButNotActive()
    {
        var world = new SimulationWorld();
        Assert.True(world.NetworkPlayerRules.TryPrepareNetworkPlayerJoin(4));

        Assert.Equal(
            [
                (byte)SimulationWorld.LocalPlayerSlot,
                (byte)4,
            ],
            world.NetworkPlayerRules.EnumerateReplicatedNetworkPlayers().Select(entry => entry.Slot));
        Assert.Equal(
            [(byte)SimulationWorld.LocalPlayerSlot],
            world.NetworkPlayerRules.EnumerateActiveNetworkPlayers().Select(entry => entry.Slot));
    }

    [Fact]
    public void ActiveRemoteSlotsRemainInSlotOrderWithoutMaterializingUnusedSlots()
    {
        var world = new SimulationWorld();
        Assert.True(world.NetworkPlayerRules.TryPrepareNetworkPlayerJoin(7));
        Assert.True(world.NetworkPlayerRules.TryApplyNetworkPlayerClassSelection(7, PlayerClass.Heavy));
        Assert.True(world.NetworkPlayerRules.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.NetworkPlayerRules.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));

        Assert.Equal(
            [
                (byte)SimulationWorld.LocalPlayerSlot,
                (byte)2,
                (byte)7,
            ],
            world.NetworkPlayerRules.EnumerateActiveNetworkPlayers().Select(entry => entry.Slot));
        Assert.DoesNotContain(
            world.NetworkPlayerRules.EnumerateReplicatedNetworkPlayers(),
            entry => entry.Slot is 3 or 6 or 8);
    }
}
