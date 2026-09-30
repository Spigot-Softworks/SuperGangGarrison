using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CrosshairHudTests
{
    [Fact]
    public void RefireDrainsOnlyToFrameFourAndIgnoresPausedShellTimer()
    {
        var weapon = CharacterClassCatalog.Scattergun;
        AssertFrame("CrosshairS", 1, Game1.GetCrosshairFrame(weapon, weapon.ReloadDelayTicks, weapon.AmmoReloadTicks));
        AssertFrame("CrosshairS", 4, Game1.GetCrosshairFrame(weapon, 1, weapon.AmmoReloadTicks));
        var frames = Enumerable.Range(1, weapon.ReloadDelayTicks).Reverse()
            .Select(ticks => Game1.GetCrosshairFrame(weapon, ticks, weapon.AmmoReloadTicks).FrameIndex).ToArray();
        Assert.All(frames, frame => Assert.InRange(frame, 1, 4));
        Assert.True(frames.SequenceEqual(frames.OrderBy(frame => frame)));
    }

    [Fact]
    public void EveryShellDrainsTheRestOfTheWayThenReturnsToWhiteOutline()
    {
        var weapon = CharacterClassCatalog.Scattergun;
        var timing = new Game1.CrosshairTimingState();
        for (var shell = 0; shell < 3; shell++)
        {
            var frames = Enumerable.Range(1, weapon.AmmoReloadTicks).Reverse().Select(ticks =>
            {
                timing.Update(weapon, 0, ticks);
                return GetTimedFrame(weapon, timing, 0, ticks).FrameIndex;
            }).ToArray();
            Assert.Equal(4, frames[0]);
            Assert.Equal(9, frames[^1]);
            Assert.True(frames.SequenceEqual(frames.OrderBy(frame => frame)));
        }
        AssertFrame("CrosshairS", 0, Game1.GetCrosshairFrame(weapon, 0, 0));
    }

    [Theory]
    [InlineData("weapon.minigun")]
    [InlineData("weapon.flamethrower")]
    [InlineData("weapon.medic-needlegun")]
    [InlineData("weapon.sniper-smg")]
    [InlineData("weapon.scout-pistol")]
    [InlineData("weapon.engineer-pistol")]
    [InlineData("weapon.tommy-gun")]
    public void ContinuousWeaponsUseWhiteIdleAmmoWhileFiringAndGreyWhenBlocked(string itemId)
    {
        var registry = CharacterClassCatalog.RuntimeRegistry;
        var weapon = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem(itemId));
        Assert.True(Game1.IsContinuousCrosshairWeapon(weapon));
        AssertFrame("CrosshairS", 0, Game1.GetCrosshairFrame(weapon, 0, 30, weapon.MaxAmmo / 2, weapon.MaxAmmo));
        AssertFrame(Game1.ContinuousCrosshairSpriteName, 0,
            Game1.GetCrosshairFrame(weapon, 1, 30, weapon.MaxAmmo, weapon.MaxAmmo, isFireHeld: true));
        AssertFrame(Game1.ContinuousCrosshairSpriteName, 5,
            Game1.GetCrosshairFrame(weapon, 0, 30, weapon.MaxAmmo / 2, weapon.MaxAmmo, isFireHeld: true));
        foreach (var isFireHeld in new[] { false, true })
        {
            AssertFrame(Game1.ContinuousCrosshairSpriteName, 10,
                Game1.GetCrosshairFrame(weapon, 0, 0, 0, weapon.MaxAmmo, isFireHeld));
            AssertFrame(Game1.ContinuousCrosshairSpriteName, 10,
                Game1.GetCrosshairFrame(weapon, 0, 0, weapon.MaxAmmo, weapon.MaxAmmo, isFireHeld, isFireBlocked: true));
        }
    }

    [Theory]
    [InlineData("weapon.scattergun")]
    [InlineData("weapon.shotgun")]
    [InlineData("weapon.rocketlauncher")]
    [InlineData("weapon.minelauncher")]
    [InlineData("weapon.rifle")]
    [InlineData("weapon.revolver")]
    public void SingleShotWeaponsRetainRefireAndShellAnimation(string itemId)
    {
        var registry = CharacterClassCatalog.RuntimeRegistry;
        var weapon = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem(itemId));
        Assert.False(Game1.IsContinuousCrosshairWeapon(weapon));
        AssertFrame("CrosshairS", 1, Game1.GetCrosshairFrame(weapon, weapon.ReloadDelayTicks, 0));
    }

    [Fact]
    public void DashGreysOutHeavyCursorEvenWithFullAmmoAndNoFireHeld()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Heavy);
        player.Spawn(PlayerTeam.Red, 0, 0);
        Assert.False(Game1.IsCrosshairFireBlocked(player, player.PrimaryWeapon));
        Assert.True(player.TryStartExperimentalGhostDash(15, 60, 2f, 10f, requireExperimentalDemoknight: false));
        Assert.True(Game1.IsCrosshairFireBlocked(player, player.PrimaryWeapon));
        AssertFrame(Game1.ContinuousCrosshairSpriteName, 10,
            Game1.GetCrosshairFrame(player.PrimaryWeapon, 0, 0, player.CurrentShells, player.MaxShells,
                isFireBlocked: Game1.IsCrosshairFireBlocked(player, player.PrimaryWeapon)));
    }

    [Fact]
    public void EmptyFlamethrowerStaysGreyDuringRefillUntilReleaseAllowsFire()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Pyro);
        player.Spawn(PlayerTeam.Red, 0, 0);
        // Commit the firing path directly to empty the tank without movement.
        for (var shot = 0; shot < 112; shot++) player.CommitPyroPrimaryWeaponShot();
        Assert.True(Game1.IsCrosshairFireBlocked(player, player.PrimaryWeapon));
        AssertFrame(Game1.ContinuousCrosshairSpriteName, 10,
            Game1.GetCrosshairFrame(player.PrimaryWeapon, 14, 7, 0, player.MaxShells,
                isFireHeld: true, isFireBlocked: Game1.IsCrosshairFireBlocked(player, player.PrimaryWeapon)));
        var refilling = player.CapturePredictionState() with
        {
            CurrentShells = 100, PyroPrimaryFuelScaled = 1000, IsPyroPrimaryRefilling = true,
            PrimaryCooldownTicks = 0, ReloadTicksUntilNextShell = 0
        };
        player.RestorePredictionState(refilling);
        AssertFrame(Game1.ContinuousCrosshairSpriteName, 10,
            Game1.GetCrosshairFrame(player.PrimaryWeapon, 0, 0, player.CurrentShells, player.MaxShells,
                isFireHeld: true, isFireBlocked: Game1.IsCrosshairFireBlocked(player, player.PrimaryWeapon)));
        player.UpdatePyroPrimaryHoldState(false);
        Assert.False(Game1.IsCrosshairFireBlocked(player, player.PrimaryWeapon));
        AssertFrame("CrosshairS", 0, Game1.GetCrosshairFrame(player.PrimaryWeapon, 0, 0, player.CurrentShells, player.MaxShells));
    }

    [Fact]
    public void FlamethrowerUsesExactFuelThresholdRatherThanRoundedHudAmmo()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Pyro);
        player.Spawn(PlayerTeam.Red, 0, 0);
        var ready = player.CapturePredictionState() with
        {
            CurrentShells = 1, PyroPrimaryFuelScaled = PlayerEntity.PyroPrimaryFlameCostScaled
        };
        player.RestorePredictionState(ready);
        Assert.False(Game1.IsCrosshairFireBlocked(player, player.PrimaryWeapon));
        AssertFrame(Game1.ContinuousCrosshairSpriteName, 9,
            Game1.GetCrosshairFrame(player.PrimaryWeapon, 0, 0, 1, player.MaxShells, isFireHeld: true));
        player.RestorePredictionState(ready with { PyroPrimaryFuelScaled = PlayerEntity.PyroPrimaryFlameCostScaled - 1 });
        Assert.True(Game1.IsCrosshairFireBlocked(player, player.PrimaryWeapon));
    }

    [Fact]
    public void FlamethrowerBlockingCooldownStaysGreyThroughItsFinalTick()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Pyro);
        player.Spawn(PlayerTeam.Red, 0, 0);
        Assert.True(Game1.IsCrosshairFireBlocked(player, player.PrimaryWeapon, 14, 14));
        Assert.True(Game1.IsCrosshairFireBlocked(player, player.PrimaryWeapon, 1, 14));
        Assert.False(Game1.IsCrosshairFireBlocked(player, player.PrimaryWeapon, 0, 14));
        Assert.False(Game1.IsCrosshairFireBlocked(player, player.PrimaryWeapon, 1, 1));
    }

    [Fact]
    public void TimingsFollowModifiedCountdownsAndResetWhenWeaponChanges()
    {
        var weapon = CharacterClassCatalog.Scattergun;
        var timing = new Game1.CrosshairTimingState();
        timing.Update(weapon, 8, 12);
        AssertFrame("CrosshairS", 1, GetTimedFrame(weapon, timing, 8, 12));
        timing.Update(weapon, 1, 12);
        AssertFrame("CrosshairS", 4, GetTimedFrame(weapon, timing, 1, 12));
        timing.Update(weapon, 0, 12);
        AssertFrame("CrosshairS", 4, GetTimedFrame(weapon, timing, 0, 12));
        timing.Update(weapon, 0, 1);
        AssertFrame("CrosshairS", 9, GetTimedFrame(weapon, timing, 0, 1));
        timing.Update(CharacterClassCatalog.RocketLauncher, 16, 30);
        Assert.Equal(16, timing.CooldownDurationTicks);
        Assert.Equal(30, timing.ReloadDurationTicks);
    }

    private static Game1.CrosshairFrame GetTimedFrame(PrimaryWeaponDefinition weapon, Game1.CrosshairTimingState timing,
        int cooldown, int reload) => Game1.GetCrosshairFrame(weapon, cooldown, reload,
            cooldownDurationTicks: timing.CooldownDurationTicks, reloadDurationTicks: timing.ReloadDurationTicks);

    private static void AssertFrame(string sprite, int index, Game1.CrosshairFrame actual)
        => Assert.Equal(new Game1.CrosshairFrame(sprite, index), actual);
}
