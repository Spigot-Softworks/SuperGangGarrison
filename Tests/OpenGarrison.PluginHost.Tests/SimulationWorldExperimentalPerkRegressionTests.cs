using OpenGarrison.Core;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldExperimentalPerkRegressionTests
{
    [Fact]
    public void SpeedLoaderReloadMultiplierReducesRocketRefillTicks()
    {
        const float reloadMultiplier = 1f / 0.6f;
        var player = new PlayerEntity(1, CharacterClassCatalog.Soldier, "Test");
        player.Spawn(PlayerTeam.Red, 0f, 0f);
        player.SetExperimentalReloadSpeedMultiplier(reloadMultiplier);

        player.ForceSetAmmo(player.PrimaryWeapon.MaxAmmo);
        Assert.True(player.TryFirePrimaryWeapon());

        var expectedReloadTicks = Math.Max(1, (int)MathF.Round(player.PrimaryWeapon.AmmoReloadTicks / reloadMultiplier));
        Assert.Equal(expectedReloadTicks, player.ReloadTicksUntilNextShell);
    }

    [Fact]
    public void ShrapnelJunkieConvertsSelfDamageIntoHealing()
    {
        var world = CreateSoldierWorld(new ExperimentalGameplaySettings(EnableSelfDamageHealing: true));
        var player = world.LocalPlayer;
        player.ForceSetHealth(Math.Max(1, player.MaxHealth / 2));
        var healthBefore = player.Health;

        var applyContinuousDamageMethod = typeof(SimulationWorld).GetMethod(
            "ApplyPlayerContinuousDamage",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(applyContinuousDamageMethod);

        var died = (bool)applyContinuousDamageMethod!.Invoke(
            world,
            [player, 12f, player, PlayerEntity.SpyDamageRevealAlpha, DamageEventFlags.None])!;

        Assert.False(died);
        Assert.True(player.Health > healthBefore);
    }

    [Fact]
    public void UntouchableKillRewardAppliesGhostPhaseInsteadOfUber()
    {
        var world = CreateSoldierWorld(new ExperimentalGameplaySettings(
            EnableGhostPhaseOnKill: true,
            KillInvincibilityDurationSeconds: 1f));

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(2, out var victim));
        victim.ForceSetHealth(1);

        var killMethod = typeof(SimulationWorld).GetMethod("KillPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(killMethod);
        _ = killMethod!.Invoke(
            world,
            [
                victim,
                true,
                world.LocalPlayer,
                "RocketKL",
                DeadBodyAnimationKind.Default,
                null,
                null,
                null,
                true,
                true,
            ]);

        Assert.True(world.LocalPlayer.IsExperimentalGhostDashing);
        Assert.False(world.LocalPlayer.IsUbered);
    }

    private static SimulationWorld CreateSoldierWorld(ExperimentalGameplaySettings settings)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Soldier));
        world.ConfigureExperimentalGameplaySettings(settings);
        world.ForceRespawnLocalPlayer();
        return world;
    }
}
