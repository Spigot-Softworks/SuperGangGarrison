using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerEntityLockedPrimarySelectionTests
{
    [Fact]
    public void LockedAlternatePrimarySelectionSurvivesSameClassRespawn()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Scout");
        player.Spawn(PlayerTeam.Red, 100f, 100f);
        player.SetExperimentalOffhandWeapon(CreateWeapon("weapon.scout-nailgun"));
        player.EquipExperimentalOffhandWeapon();

        Assert.Equal(GameplayEquipmentSlot.Secondary, player.SelectedGameplayEquippedSlot);
        Assert.True(player.IsExperimentalOffhandEquipped);

        player.Kill();
        Assert.False(player.IsExperimentalOffhandEquipped);
        Assert.Equal(GameplayEquipmentSlot.Secondary, player.SelectedGameplayEquippedSlot);

        player.Spawn(PlayerTeam.Red, 100f, 100f);

        Assert.Equal(GameplayEquipmentSlot.Secondary, player.SelectedGameplayEquippedSlot);
        Assert.True(player.IsExperimentalOffhandEquipped);
        Assert.True(player.IsExperimentalOffhandSelected);
    }

    [Fact]
    public void ClassChangeResetsLockedAlternatePrimarySelection()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Scout");
        player.Spawn(PlayerTeam.Red, 100f, 100f);
        player.SetExperimentalOffhandWeapon(CreateWeapon("weapon.scout-nailgun"));
        player.EquipExperimentalOffhandWeapon();

        player.SetClassDefinition(CharacterClassCatalog.Sniper);

        Assert.Equal(GameplayEquipmentSlot.Primary, player.SelectedGameplayEquippedSlot);
        Assert.False(player.IsExperimentalOffhandEquipped);
        Assert.False(player.HasExperimentalOffhandWeapon);
    }

    [Theory]
    [InlineData(PlayerClass.Sniper, "weapon.bow")]
    [InlineData(PlayerClass.Scout, "weapon.scout-nailgun")]
    [InlineData(PlayerClass.Medic, "weapon.medigun.crit")]
    public void LockedAlternatePrimarySelectionSurvivesServerRespawn(
        PlayerClass playerClass,
        string alternateWeaponItemId)
    {
        var classDefinition = CharacterClassCatalog.GetDefinition(playerClass);
        var player = new PlayerEntity(1, classDefinition, classDefinition.DisplayName);
        player.Spawn(PlayerTeam.Red, 100f, 100f);
        player.SetExperimentalOffhandWeapon(CreateWeapon(alternateWeaponItemId));
        player.EquipExperimentalOffhandWeapon();

        player.Kill();
        // The authoritative network respawn path reapplies the class definition
        // before spawning the existing player entity.
        player.SetClassDefinition(classDefinition);
        player.Spawn(PlayerTeam.Red, 100f, 100f);

        Assert.Equal(GameplayEquipmentSlot.Secondary, player.SelectedGameplayEquippedSlot);
        Assert.True(player.IsExperimentalOffhandEquipped);
        Assert.True(player.IsExperimentalOffhandSelected);
    }

    [Theory]
    [InlineData(PlayerClass.Sniper, "weapon.bow")]
    [InlineData(PlayerClass.Scout, "weapon.scout-nailgun")]
    [InlineData(PlayerClass.Medic, "weapon.medigun.crit")]
    public void LockedAlternatePrimarySelectionSurvivesForcedDeathRespawn(
        PlayerClass playerClass,
        string alternateWeaponItemId)
    {
        var classDefinition = CharacterClassCatalog.GetDefinition(playerClass);
        var player = new PlayerEntity(1, classDefinition, classDefinition.DisplayName);
        player.Spawn(PlayerTeam.Red, 100f, 100f);
        player.SetExperimentalOffhandWeapon(CreateWeapon(alternateWeaponItemId));
        player.EquipExperimentalOffhandWeapon();

        player.ForceSetHealth(0);
        Assert.Equal(GameplayEquipmentSlot.Secondary, player.SelectedGameplayEquippedSlot);

        player.Spawn(PlayerTeam.Red, 100f, 100f);

        Assert.Equal(GameplayEquipmentSlot.Secondary, player.SelectedGameplayEquippedSlot);
        Assert.True(player.IsExperimentalOffhandEquipped);
        Assert.True(player.IsExperimentalOffhandSelected);
    }

    [Fact]
    public void AcquiredWeaponSelectionDoesNotPersistAcrossRespawn()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Soldier, "Soldier");
        player.Spawn(PlayerTeam.Red, 100f, 100f);
        Assert.True(player.TryGrantGameplayItem(CharacterClassCatalog.RuntimeRegistry.GetPrimaryItem(PlayerClass.Scout).Id));
        player.SetAcquiredWeapon(PlayerClass.Scout);
        player.EquipAcquiredWeapon();

        player.Kill();
        player.Spawn(PlayerTeam.Red, 100f, 100f);

        Assert.False(player.HasAcquiredWeapon);
        Assert.False(player.IsAcquiredWeaponEquipped);
        Assert.Equal(GameplayEquipmentSlot.Primary, player.SelectedGameplayEquippedSlot);
    }

    private static PrimaryWeaponDefinition CreateWeapon(string itemId)
    {
        return CharacterClassCatalog.RuntimeRegistry.CreatePrimaryWeaponDefinition(
            CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(itemId));
    }
}
