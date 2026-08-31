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
        Assert.True(player.TrySelectGameplayPrimaryItem("weapon.scout-nailgun"));

        Assert.Equal(GameplayEquipmentSlot.Primary, player.SelectedGameplayEquippedSlot);
        Assert.Equal("weapon.scout-nailgun", player.SelectedGameplayPrimaryItemId);
        Assert.False(player.HasExperimentalOffhandWeapon);
        Assert.Equal("weapon.scout-pistol", player.GameplayLoadoutState.SecondaryItemId);

        player.Kill();
        Assert.False(player.IsExperimentalOffhandEquipped);
        Assert.Equal("weapon.scout-nailgun", player.SelectedGameplayPrimaryItemId);

        player.Spawn(PlayerTeam.Red, 100f, 100f);

        Assert.Equal(GameplayEquipmentSlot.Primary, player.SelectedGameplayEquippedSlot);
        Assert.Equal("weapon.scout-nailgun", player.GameplayLoadoutState.PrimaryItemId);
        Assert.True(player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.ScoutNailgun));
    }

    [Fact]
    public void ClassChangeResetsLockedAlternatePrimarySelection()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Scout");
        player.Spawn(PlayerTeam.Red, 100f, 100f);
        Assert.True(player.TrySelectGameplayPrimaryItem("weapon.scout-nailgun"));

        player.SetClassDefinition(CharacterClassCatalog.Sniper);

        Assert.Equal(GameplayEquipmentSlot.Primary, player.SelectedGameplayEquippedSlot);
        Assert.Equal("weapon.rifle", player.SelectedGameplayPrimaryItemId);
        Assert.False(player.IsExperimentalOffhandEquipped);
        Assert.False(player.HasExperimentalOffhandWeapon);
        Assert.Equal("weapon.sniper-smg", player.GameplayLoadoutState.SecondaryItemId);
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
        Assert.True(player.TrySelectGameplayPrimaryItem(alternateWeaponItemId));

        player.Kill();
        // The authoritative network respawn path reapplies the class definition
        // before spawning the existing player entity.
        player.SetClassDefinition(classDefinition);
        player.Spawn(PlayerTeam.Red, 100f, 100f);

        Assert.Equal(GameplayEquipmentSlot.Primary, player.SelectedGameplayEquippedSlot);
        Assert.Equal(alternateWeaponItemId, player.SelectedGameplayPrimaryItemId);
        Assert.Equal(alternateWeaponItemId, player.GameplayLoadoutState.PrimaryItemId);
        Assert.False(player.HasExperimentalOffhandWeapon);
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
        Assert.True(player.TrySelectGameplayPrimaryItem(alternateWeaponItemId));

        player.ForceSetHealth(0);
        Assert.Equal(GameplayEquipmentSlot.Primary, player.SelectedGameplayEquippedSlot);
        Assert.Equal(alternateWeaponItemId, player.SelectedGameplayPrimaryItemId);

        player.Spawn(PlayerTeam.Red, 100f, 100f);

        Assert.Equal(GameplayEquipmentSlot.Primary, player.SelectedGameplayEquippedSlot);
        Assert.Equal(alternateWeaponItemId, player.SelectedGameplayPrimaryItemId);
        Assert.Equal(alternateWeaponItemId, player.GameplayLoadoutState.PrimaryItemId);
        Assert.False(player.HasExperimentalOffhandWeapon);
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

    [Fact]
    public void WeaponGrantedAbilitiesFollowTheSelectedPrimary()
    {
        var sniper = new PlayerEntity(1, CharacterClassCatalog.Sniper, "Sniper");
        sniper.Spawn(PlayerTeam.Red, 100f, 100f);

        Assert.True(sniper.TryGetGameplayAbilityItem(
            GameplayAbilityConstants.SpecialChannel,
            BuiltInGameplayBehaviorIds.SniperScope,
            out var scope));
        Assert.Equal("ability.sniper-scope", scope.Id);

        Assert.True(sniper.TrySelectGameplayPrimaryItem("weapon.bow"));
        Assert.False(sniper.TryGetGameplayAbilityItem(
            GameplayAbilityConstants.SpecialChannel,
            BuiltInGameplayBehaviorIds.SniperScope,
            out _));

        var medic = new PlayerEntity(2, CharacterClassCatalog.Medic, "Medic");
        medic.Spawn(PlayerTeam.Red, 100f, 100f);

        Assert.True(medic.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.SpecialChannel,
            BuiltInGameplayBehaviorIds.MedicNeedlegun));

        Assert.True(medic.TrySelectGameplayPrimaryItem("weapon.medigun.crit"));
        Assert.False(medic.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.SpecialChannel,
            BuiltInGameplayBehaviorIds.MedicNeedlegun));
        Assert.True(medic.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.SpecialChannel,
            BuiltInGameplayBehaviorIds.MedicKritzHealNeedles));
    }

    [Fact]
    public void AbilityItemsRemainSeparateFromTheSecondaryWeaponSlot()
    {
        var heavy = new PlayerEntity(1, CharacterClassCatalog.Heavy, "Heavy");
        heavy.Spawn(PlayerTeam.Red, 100f, 100f);

        Assert.Equal("weapon.heavy-shotgun", heavy.GameplayLoadoutState.SecondaryItemId);
        Assert.DoesNotContain("ability.heavy-sandvich", heavy.GameplayLoadoutState.AbilityItemIds ?? []);
        Assert.Contains("ability.heavy-sandvich", heavy.GetGameplayAbilityItems().Select(static item => item.Id));
        Assert.True(heavy.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.SpecialChannel,
            BuiltInGameplayBehaviorIds.HeavySandvich));

        var soldier = new PlayerEntity(2, CharacterClassCatalog.Soldier, "Soldier");
        soldier.Spawn(PlayerTeam.Red, 100f, 100f);

        Assert.Equal("weapon.soldier-shotgun", soldier.GameplayLoadoutState.SecondaryItemId);
        Assert.Equal(BuiltInGameplayBehaviorIds.PelletGun, soldier.SecondaryBehaviorId);
        Assert.True(soldier.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.SpecialChannel,
            BuiltInGameplayBehaviorIds.ExperimentalSoldierSecondary));

        Assert.True(soldier.TrySelectGameplayEquippedSlot(GameplayEquipmentSlot.Secondary));
        Assert.True(soldier.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.SpecialChannel,
            BuiltInGameplayBehaviorIds.ExperimentalSoldierSecondary));

        Assert.True(soldier.TrySelectGameplayEquippedSlot(GameplayEquipmentSlot.Primary));
        Assert.True(soldier.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.SpecialChannel,
            BuiltInGameplayBehaviorIds.ExperimentalSoldierSecondary));
    }

    [Fact]
    public void WeaponAltFireAbilitiesOnlyResolveForTheirEquippedWeapon()
    {
        var sniper = new PlayerEntity(1, CharacterClassCatalog.Sniper, "Sniper");
        sniper.Spawn(PlayerTeam.Red, 100f, 100f);
        var registry = CharacterClassCatalog.RuntimeRegistry;
        var rifleItemId = sniper.GameplayLoadoutState.PrimaryItemId;
        var stowedRifleState = sniper.GameplayLoadoutState with
        {
            SecondaryItemId = "weapon.soldier-shotgun",
            EquippedSlot = GameplayEquipmentSlot.Secondary,
            EquippedItemId = "weapon.soldier-shotgun",
        };

        Assert.DoesNotContain(
            registry.ResolveGameplayAbilityItems(stowedRifleState),
            static item => item.Id == "ability.sniper-scope");

        var equippedRifleState = stowedRifleState with
        {
            EquippedSlot = GameplayEquipmentSlot.Primary,
            EquippedItemId = rifleItemId,
        };
        Assert.Contains(
            registry.ResolveGameplayAbilityItems(equippedRifleState),
            static item => item.Id == "ability.sniper-scope");
    }
}
