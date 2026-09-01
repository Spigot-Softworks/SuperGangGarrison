using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SecondaryWeaponRuntimeTests
{
    [Theory]
    [InlineData(PlayerClass.Heavy, "weapon.heavy-shotgun")]
    [InlineData(PlayerClass.Soldier, "weapon.soldier-shotgun")]
    public void HeavyAndSoldierShotgunsUseFourRoundClips(
        PlayerClass playerClass,
        string expectedItemId)
    {
        var world = CreateWorld(playerClass);

        Assert.Equal(expectedItemId, world.LocalPlayer.GameplayLoadoutState.SecondaryItemId);
        Assert.Equal(4, world.LocalPlayer.ExperimentalOffhandMaxShells);
        Assert.Equal(4, world.LocalPlayer.ExperimentalOffhandCurrentShells);
    }

    [Theory]
    [InlineData(PlayerClass.Scout, "weapon.scout-pistol", 1, 8f, "PistolKL")]
    [InlineData(PlayerClass.Engineer, "weapon.engineer-pistol", 1, 8f, "PistolKL")]
    [InlineData(PlayerClass.Heavy, "weapon.heavy-shotgun", 5, 7f, "ShotgunKL")]
    [InlineData(PlayerClass.Sniper, "weapon.sniper-smg", 1, 5f, "SmgKL")]
    public void ConventionalSecondariesUseItemAuthoredDamageAndKillfeed(
        PlayerClass playerClass,
        string expectedItemId,
        int expectedProjectileCount,
        float expectedDamage,
        string expectedKillfeedSprite)
    {
        var world = CreateWorld(playerClass);
        var player = world.LocalPlayer;
        var primaryAmmoBefore = player.CurrentShells;
        var secondaryAmmoBefore = player.ExperimentalOffhandCurrentShells;

        Assert.Equal(expectedItemId, player.GameplayLoadoutState.SecondaryItemId);
        Assert.Equal(expectedItemId, player.ExperimentalOffhandWeapon?.ItemId);
        Assert.Equal(expectedProjectileCount, player.ExperimentalOffhandWeapon?.ProjectilesPerShot);
        Assert.True(world.TrySetNetworkPlayerGameplayEquippedSlot(
            SimulationWorld.LocalPlayerSlot,
            GameplayEquipmentSlot.Secondary));

        FireSelectedWeapon(world);

        var shots = world.Shots.Where(shot => shot.OwnerId == player.Id).ToArray();
        Assert.Equal(expectedProjectileCount, shots.Length);
        Assert.All(shots, shot =>
        {
            Assert.Equal(expectedDamage, shot.DamageValue);
            Assert.Equal(expectedKillfeedSprite, shot.KillFeedWeaponSpriteNameOverride);
        });
        Assert.Equal(primaryAmmoBefore, player.CurrentShells);
        Assert.Equal(secondaryAmmoBefore - 1, player.ExperimentalOffhandCurrentShells);
    }

    [Fact]
    public void FlaregunUsesIndependentAmmoAndAuthoredProjectileDamage()
    {
        var world = CreateWorld(PlayerClass.Pyro);
        var player = world.LocalPlayer;
        var primaryAmmoBefore = player.CurrentShells;

        Assert.True(world.TrySetNetworkPlayerGameplayEquippedSlot(
            SimulationWorld.LocalPlayerSlot,
            GameplayEquipmentSlot.Secondary));
        Assert.False(player.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.SpecialChannel,
            BuiltInGameplayBehaviorIds.PyroAirblast));
        Assert.True(player.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.UtilityChannel,
            BuiltInGameplayBehaviorIds.PyroUtility));

        FireSelectedWeapon(world);

        var flare = Assert.Single(world.Flares, flare => flare.OwnerId == player.Id);
        Assert.Equal(20f, flare.DamagePerHit);
        Assert.Equal("FlareKL", flare.KillFeedWeaponSpriteName);
        Assert.Equal(primaryAmmoBefore, player.CurrentShells);
        Assert.Equal(0, player.ExperimentalOffhandCurrentShells);
        Assert.Contains(world.PendingSoundEvents, sound => sound.SoundName == "FlaregunSnd");
    }

    [Fact]
    public void StandaloneNeedlegunDoesNotConsumeMedigunAmmo()
    {
        var world = CreateWorld(PlayerClass.Medic);
        var player = world.LocalPlayer;
        var primaryAmmoBefore = player.CurrentShells;

        Assert.True(world.TrySetNetworkPlayerGameplayEquippedSlot(
            SimulationWorld.LocalPlayerSlot,
            GameplayEquipmentSlot.Secondary));

        FireSelectedWeapon(world);

        var needle = Assert.Single(world.Needles, needle => needle.OwnerId == player.Id);
        Assert.Equal(4, needle.Damage);
        Assert.Equal("NeedleKL", needle.KillFeedWeaponSpriteName);
        Assert.Equal(primaryAmmoBefore, player.CurrentShells);
        Assert.Equal(39, player.ExperimentalOffhandCurrentShells);
        Assert.Contains(world.PendingSoundEvents, sound => sound.SoundName == "MedichaingunSnd");
    }

    [Fact]
    public void HeavyDashRemainsUtilityWhileSandvichTracksTheEquippedMinigun()
    {
        var world = CreateWorld(PlayerClass.Heavy);
        var player = world.LocalPlayer;

        Assert.True(player.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.SpecialChannel,
            BuiltInGameplayBehaviorIds.HeavySandvich));
        Assert.True(player.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.UtilityChannel,
            BuiltInGameplayBehaviorIds.HeavyUtility));

        Assert.True(world.TrySetNetworkPlayerGameplayEquippedSlot(
            SimulationWorld.LocalPlayerSlot,
            GameplayEquipmentSlot.Secondary));

        Assert.False(player.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.SpecialChannel,
            BuiltInGameplayBehaviorIds.HeavySandvich));
        Assert.True(player.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.UtilityChannel,
            BuiltInGameplayBehaviorIds.HeavyUtility));
    }

    private static SimulationWorld CreateWorld(PlayerClass playerClass)
    {
        var world = new SimulationWorld();
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(playerClass);
        world.LocalPlayer.SetSpawnRoomState(false);
        world.SetLocalInput(default);
        world.SetLocalPreviousInput(default);
        _ = world.DrainPendingSoundEvents();
        return world;
    }

    private static void FireSelectedWeapon(SimulationWorld world)
    {
        world.SetLocalInput(default(PlayerInputSnapshot) with
        {
            FirePrimary = true,
            AimWorldX = world.LocalPlayer.X + 96f,
            AimWorldY = world.LocalPlayer.Y,
        });
        world.AdvanceOneTick();
    }
}
