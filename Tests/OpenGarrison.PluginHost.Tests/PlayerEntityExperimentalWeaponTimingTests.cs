using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerEntityExperimentalWeaponTimingTests
{
    [Fact]
    public void ReloadSpeedMultiplierScalesSoldierPrimaryOffhandAndAcquiredWeaponTimers()
    {
        const float reloadMultiplier = 1.4f;
        var player = CreateSpawnedSoldier(reloadMultiplier);
        var acquiredItemId = CharacterClassCatalog.RuntimeRegistry.GetPrimaryItem(PlayerClass.Scout).Id;

        Assert.True(player.TryGrantGameplayItem(acquiredItemId));
        player.SetExperimentalOffhandWeapon(CharacterClassCatalog.Shotgun);
        player.SetAcquiredWeapon(PlayerClass.Scout);

        Assert.True(player.TryFirePrimaryWeapon());
        Assert.Equal(GetExpectedScaledTicks(player.PrimaryWeapon.ReloadDelayTicks, reloadMultiplier), player.PrimaryCooldownTicks);
        Assert.Equal(GetExpectedScaledTicks(player.PrimaryWeapon.AmmoReloadTicks, reloadMultiplier), player.ReloadTicksUntilNextShell);

        Assert.True(player.TryFireExperimentalOffhandWeapon());
        Assert.Equal(GetExpectedScaledTicks(CharacterClassCatalog.Shotgun.ReloadDelayTicks, reloadMultiplier), player.ExperimentalOffhandCooldownTicks);
        Assert.Equal(GetExpectedScaledTicks(CharacterClassCatalog.Shotgun.AmmoReloadTicks, reloadMultiplier), player.ExperimentalOffhandReloadTicksUntilNextShell);

        Assert.True(player.TryFireAcquiredWeapon());
        Assert.NotNull(player.AcquiredWeapon);
        Assert.Equal(GetExpectedScaledTicks(player.AcquiredWeapon!.ReloadDelayTicks, reloadMultiplier), player.AcquiredWeaponCooldownTicks);
        Assert.Equal(GetExpectedScaledTicks(player.AcquiredWeapon!.AmmoReloadTicks, reloadMultiplier), player.AcquiredWeaponReloadTicksUntilNextShell);
    }

    [Fact]
    public void ReloadSpeedMultiplierScalesAcquiredPyroWeaponAndAbilities()
    {
        const float reloadMultiplier = 1.4f;
        var flamePlayer = CreateSpawnedSoldierWithAcquiredWeapon(PlayerClass.Pyro, reloadMultiplier);

        Assert.True(flamePlayer.TryFireAcquiredWeapon());
        Assert.NotNull(flamePlayer.AcquiredWeapon);
        Assert.Equal(GetExpectedScaledTicks(flamePlayer.AcquiredWeapon!.ReloadDelayTicks, reloadMultiplier), flamePlayer.AcquiredWeaponCooldownTicks);
        Assert.Equal(GetExpectedScaledTicks(PlayerEntity.PyroPrimaryRefillBufferTicks, reloadMultiplier), flamePlayer.AcquiredWeaponReloadTicksUntilNextShell);

        var airblastPlayer = CreateSpawnedSoldierWithAcquiredWeapon(PlayerClass.Pyro, reloadMultiplier);
        Assert.True(airblastPlayer.TryFirePyroAirblast());
        Assert.Equal(GetExpectedScaledTicks(PlayerEntity.PyroAirblastReloadTicks, reloadMultiplier), airblastPlayer.PyroAirblastCooldownTicks);
        Assert.Equal(GetExpectedScaledTicks(PlayerEntity.PyroAirblastNoFlameTicks, reloadMultiplier), airblastPlayer.AcquiredWeaponCooldownTicks);
        Assert.Equal(GetExpectedScaledTicks(PlayerEntity.PyroAirblastReloadTicks, reloadMultiplier), airblastPlayer.AcquiredWeaponReloadTicksUntilNextShell);

        var flarePlayer = CreateSpawnedSoldierWithAcquiredWeapon(PlayerClass.Pyro, reloadMultiplier);
        Assert.True(flarePlayer.TryFirePyroFlare());
        Assert.Equal(GetExpectedScaledTicks(PlayerEntity.PyroFlareReloadTicks, reloadMultiplier), flarePlayer.PyroFlareCooldownTicks);
    }

    [Fact]
    public void ReloadSpeedMultiplierScalesAcquiredMedicNeedleCooldown()
    {
        const float reloadMultiplier = 1.4f;
        var player = CreateSpawnedSoldierWithAcquiredWeapon(PlayerClass.Medic, reloadMultiplier);

        Assert.True(player.TryFireAcquiredMedicNeedle());
        Assert.Equal(GetExpectedScaledTicks(PlayerEntity.MedicNeedleFireCooldownTicks, reloadMultiplier), player.MedicNeedleCooldownTicks);
        Assert.Equal(GetExpectedScaledTicks(PlayerEntity.MedicNeedleRefillTicksDefault, reloadMultiplier), player.MedicNeedleRefillTicks);
    }

    private static PlayerEntity CreateSpawnedSoldier(float reloadMultiplier)
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Soldier, "Test");
        player.Spawn(PlayerTeam.Red, 0f, 0f);
        player.SetExperimentalReloadSpeedMultiplier(reloadMultiplier);
        return player;
    }

    private static PlayerEntity CreateSpawnedSoldierWithAcquiredWeapon(PlayerClass acquiredClass, float reloadMultiplier)
    {
        var player = CreateSpawnedSoldier(reloadMultiplier);
        var acquiredItemId = CharacterClassCatalog.RuntimeRegistry.GetPrimaryItem(acquiredClass).Id;
        Assert.True(player.TryGrantGameplayItem(acquiredItemId));
        player.SetAcquiredWeapon(acquiredClass);
        player.EquipAcquiredWeapon();
        Assert.Equal(acquiredClass, player.AcquiredWeaponClassId!.Value);
        return player;
    }

    private static int GetExpectedScaledTicks(int baseTicks, float reloadMultiplier)
    {
        return Math.Max(1, (int)MathF.Round(baseTicks / reloadMultiplier));
    }
}
