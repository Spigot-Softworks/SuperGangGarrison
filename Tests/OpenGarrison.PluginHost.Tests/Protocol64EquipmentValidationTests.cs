using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Protocol64EquipmentValidationTests
{
    [Theory]
    [InlineData(PlayerClass.Scout)]
    [InlineData(PlayerClass.Engineer)]
    [InlineData(PlayerClass.Pyro)]
    [InlineData(PlayerClass.Soldier)]
    [InlineData(PlayerClass.Demoman)]
    [InlineData(PlayerClass.Heavy)]
    [InlineData(PlayerClass.Sniper)]
    [InlineData(PlayerClass.Medic)]
    [InlineData(PlayerClass.Spy)]
    [InlineData(PlayerClass.Quote)]
    public void StockLoadoutsRoundTripThroughNestedEquipment(PlayerClass playerClass)
    {
        var source = JoinedWorld(playerClass);
        var state = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(1).Players);
        var receiver = JoinedWorld(playerClass);

        Assert.True(PlayerEntity.IsValidProtocol64EquipmentState(state));
        receiver.ApplyProtocol64PlayerState(state);

        Assert.Equal(source.LocalPlayer.GameplayLoadoutState, receiver.LocalPlayer.GameplayLoadoutState);
        Assert.Equal(source.LocalPlayer.IsExperimentalOffhandSelected, receiver.LocalPlayer.IsExperimentalOffhandSelected);
    }

    [Fact]
    public void InvalidNestedEquipmentDoesNotApplyItsAmmoToThePreviousWeapon()
    {
        var source = JoinedWorld(PlayerClass.Scout);
        Assert.True(source.LocalPlayer.TrySelectGameplayPrimaryItem("weapon.scout-nailgun"));
        var published = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(1).Players);
        var invalid = published with
        {
            CurrentAmmo = 0,
            MaxAmmo = 1,
            Equipment = published.Equipment! with
            {
                // A rifle is not a valid Scout primary. The runtime registry
                // must not repair this to a default and consume the wire ammo.
                PrimaryItemId = "weapon.rifle",
            },
        };

        var receiver = JoinedWorld(PlayerClass.Scout);
        var originalLoadout = receiver.LocalPlayer.GameplayLoadoutState;
        var originalAmmo = receiver.LocalPlayer.CurrentShells;

        receiver.ApplyProtocol64PlayerState(invalid);

        Assert.Equal(originalLoadout, receiver.LocalPlayer.GameplayLoadoutState);
        Assert.Equal(originalAmmo, receiver.LocalPlayer.CurrentShells);
        Assert.Equal(originalLoadout.PrimaryItemId, receiver.LocalPlayer.PrimaryWeapon.ItemId);
    }

    [Fact]
    public void NestedEquipmentRejectsAConflictingModPackIdentity()
    {
        var source = JoinedWorld(PlayerClass.Scout);
        var published = Assert.Single(new Protocol64StatePublisher(source).BuildPlayerStateBatch(1).Players);
        var invalid = published with
        {
            CurrentAmmo = 0,
            Equipment = published.Equipment! with { ModPackId = "foreign-pack" },
        };
        var receiver = JoinedWorld(PlayerClass.Scout);
        var originalLoadout = receiver.LocalPlayer.GameplayLoadoutState;
        var originalAmmo = receiver.LocalPlayer.CurrentShells;

        receiver.ApplyProtocol64PlayerState(invalid);

        Assert.Equal(originalAmmo, receiver.LocalPlayer.CurrentShells);
        Assert.Equal(originalLoadout, receiver.LocalPlayer.GameplayLoadoutState);
    }

    private static SimulationWorld JoinedWorld(PlayerClass playerClass)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(playerClass);
        world.LocalPlayer.SetSpawnRoomState(false);
        return world;
    }
}
