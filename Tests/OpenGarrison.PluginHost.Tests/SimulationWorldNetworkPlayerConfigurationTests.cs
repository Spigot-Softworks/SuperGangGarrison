using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldNetworkPlayerConfigurationTests
{
    [Fact]
    public void TrySetNetworkPlayerGameplayLoadoutUpdatesLocalPlayerAuthoritativeState()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);

        var changed = world.TrySetNetworkPlayerGameplayLoadout(SimulationWorld.LocalPlayerSlot, "soldier.black-box");

        Assert.True(changed);
        Assert.Equal("soldier.black-box", world.LocalPlayer.SelectedGameplayLoadoutId);
        Assert.Equal("soldier.black-box", world.LocalPlayer.GameplayLoadoutState.LoadoutId);
        Assert.Equal("weapon.blackbox", world.LocalPlayer.GameplayLoadoutState.PrimaryItemId);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        Assert.Equal("weapon.blackbox", world.LocalPlayer.GameplayLoadoutState.EquippedItemId);
    }

    [Fact]
    public void TrySetNetworkPlayerGameplayLoadoutCreatesAndUpdatesRemotePlayerState()
    {
        var world = new SimulationWorld();

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));

        var changed = world.TrySetNetworkPlayerGameplayLoadout(2, "soldier.direct-hit");

        Assert.True(changed);
        Assert.True(world.TryGetNetworkPlayer(2, out var remotePlayer));
        Assert.Equal(PlayerClass.Soldier, remotePlayer.ClassId);
        Assert.Equal("soldier.direct-hit", remotePlayer.SelectedGameplayLoadoutId);
        Assert.Equal("soldier.direct-hit", remotePlayer.GameplayLoadoutState.LoadoutId);
        Assert.Equal("weapon.directhit", remotePlayer.GameplayLoadoutState.PrimaryItemId);
        Assert.Equal("weapon.directhit", remotePlayer.GameplayLoadoutState.EquippedItemId);
    }

    [Fact]
    public void TrySetNetworkPlayerGameplayEquippedSlotSelectsHeavySecondaryWhenAvailable()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Heavy);

        var changed = world.TrySetNetworkPlayerGameplayEquippedSlot(SimulationWorld.LocalPlayerSlot, GameplayEquipmentSlot.Secondary);

        Assert.True(changed);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.SelectedGameplayEquippedSlot);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        Assert.Equal("ability.heavy-sandvich", world.LocalPlayer.GameplayLoadoutState.SecondaryItemId);
        Assert.Equal("ability.heavy-sandvich", world.LocalPlayer.GameplayLoadoutState.EquippedItemId);
    }

    [Fact]
    public void TrySetNetworkPlayerGameplayEquippedSlotRejectsUnavailableSoldierSecondary()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);

        var changed = world.TrySetNetworkPlayerGameplayEquippedSlot(SimulationWorld.LocalPlayerSlot, GameplayEquipmentSlot.Secondary);

        Assert.False(changed);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.SelectedGameplayEquippedSlot);
        Assert.Equal(GameplayEquipmentSlot.Primary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        Assert.Equal("weapon.rocketlauncher", world.LocalPlayer.GameplayLoadoutState.EquippedItemId);
    }

    [Fact]
    public void TrySetNetworkPlayerGameplayLoadoutRejectsUnknownLoadoutAndPreservesAuthoritativeState()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);
        var initialLoadoutId = world.LocalPlayer.SelectedGameplayLoadoutId;
        var initialState = world.LocalPlayer.GameplayLoadoutState;

        var changed = world.TrySetNetworkPlayerGameplayLoadout(SimulationWorld.LocalPlayerSlot, "soldier.not-a-real-loadout");

        Assert.False(changed);
        Assert.Equal(initialLoadoutId, world.LocalPlayer.SelectedGameplayLoadoutId);
        Assert.Equal(initialState, world.LocalPlayer.GameplayLoadoutState);
    }

    private static SimulationWorld CreateWorldWithLocalClass(PlayerClass playerClass)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(playerClass));
        Assert.Equal(playerClass, world.LocalPlayer.ClassId);
        return world;
    }
}
