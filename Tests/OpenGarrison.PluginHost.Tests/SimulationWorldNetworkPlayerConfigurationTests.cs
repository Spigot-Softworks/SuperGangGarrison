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
    public void RemoteNetworkJoinAppliesStockSecondaryAndUtilityLoadout()
    {
        var world = new SimulationWorld();

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));

        Assert.True(world.TryGetNetworkPlayer(2, out var remotePlayer));
        Assert.Equal("soldier.stock", remotePlayer.SelectedGameplayLoadoutId);
        Assert.Equal("weapon.soldier-shotgun", remotePlayer.GameplayLoadoutState.SecondaryItemId);
        Assert.Equal("ability.soldier-utility", remotePlayer.GameplayLoadoutState.UtilityItemId);
        Assert.True(remotePlayer.HasExperimentalOffhandWeapon);
        Assert.Equal(remotePlayer.ExperimentalOffhandMaxShells, remotePlayer.ExperimentalOffhandCurrentShells);
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
    public void TrySetNetworkPlayerGameplayEquippedSlotSelectsSoldierShotgunWhenAvailable()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);

        var changed = world.TrySetNetworkPlayerGameplayEquippedSlot(SimulationWorld.LocalPlayerSlot, GameplayEquipmentSlot.Secondary);

        Assert.True(changed);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.SelectedGameplayEquippedSlot);
        Assert.Equal(GameplayEquipmentSlot.Secondary, world.LocalPlayer.GameplayLoadoutState.EquippedSlot);
        Assert.Equal("weapon.soldier-shotgun", world.LocalPlayer.GameplayLoadoutState.SecondaryItemId);
        Assert.Equal("weapon.soldier-shotgun", world.LocalPlayer.GameplayLoadoutState.EquippedItemId);
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

    [Fact]
    public void TrySetLocalClassWithSameClassRespawnsWhenConfiguredTeamChanged()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);
        var originalTeam = world.LocalPlayer.Team;
        var oppositeTeam = originalTeam == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;

        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, oppositeTeam));

        var changed = world.TrySetLocalClass(PlayerClass.Soldier);

        Assert.True(changed);
        Assert.Equal(oppositeTeam, world.LocalPlayer.Team);
        Assert.True(world.LocalPlayer.IsAlive);
    }

    [Fact]
    public void ChangingLocalTeamDoesNotRespawnOtherJoinedPlayers()
    {
        var world = CreateWorldWithLocalClass(PlayerClass.Soldier);

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(2, out var remotePlayer));

        var expectedHealth = Math.Max(1, remotePlayer.MaxHealth - 5);
        remotePlayer.ForceSetHealth(expectedHealth);
        var expectedPositionX = remotePlayer.X;
        var expectedPositionY = remotePlayer.Y;

        var oppositeTeam = world.LocalPlayer.Team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, oppositeTeam));

        Assert.Equal(expectedHealth, remotePlayer.Health);
        Assert.Equal(expectedPositionX, remotePlayer.X);
        Assert.Equal(expectedPositionY, remotePlayer.Y);
        Assert.True(remotePlayer.IsAlive);
    }

    private static SimulationWorld CreateWorldWithLocalClass(PlayerClass playerClass)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(playerClass));
        Assert.Equal(playerClass, world.LocalPlayer.ClassId);
        return world;
    }
}
