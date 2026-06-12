using OpenGarrison.Core;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldStingerRocketTests
{
    [Fact]
    public void StingerRocketsUsePhaseOneDamageBonusAndReducedLaunchSpeed()
    {
        var stingerWorld = CreateSoldierWorld(new ExperimentalGameplaySettings(EnableSoldierStingerRockets: true));
        var baselineRocketCombat = new RocketCombatDefinition();
        var modifiedRocketCombat = InvokeStingerRocketCombat(stingerWorld, stingerWorld.LocalPlayer, baselineRocketCombat);
        var modifiedLaunchSpeed = InvokeStingerLaunchSpeed(stingerWorld, stingerWorld.LocalPlayer, CharacterClassCatalog.RocketLauncher.MinShotSpeed);

        Assert.Equal(baselineRocketCombat.DirectHitDamage, modifiedRocketCombat.DirectHitDamage);
        AssertApproximately(baselineRocketCombat.ExplosionDamage, modifiedRocketCombat.ExplosionDamage);
        AssertApproximately(
            CharacterClassCatalog.RocketLauncher.MinShotSpeed * ExperimentalGameplaySettings.DefaultSoldierStingerRocketSpeedMultiplier,
            modifiedLaunchSpeed);

        var phaseOneRocket = new RocketProjectileEntity(
            id: 1,
            team: PlayerTeam.Red,
            ownerId: 1,
            x: 0f,
            y: 0f,
            speed: 12f,
            directionRadians: 0f,
            enableExperimentalStingerTracking: true);
        AssertApproximately(ExperimentalGameplaySettings.DefaultSoldierStingerDamageMultiplier, phaseOneRocket.ExperimentalStingerDamageMultiplier);
        AssertApproximately(ExperimentalGameplaySettings.DefaultSoldierStingerBlastRadiusMultiplier, phaseOneRocket.ExperimentalStingerBlastRadiusMultiplier);

        Assert.True(phaseOneRocket.TryApplyExperimentalStingerSpeedBurst(ExperimentalGameplaySettings.DefaultSoldierStingerBurstSpeedMultiplier));
        AssertApproximately(1f, phaseOneRocket.ExperimentalStingerDamageMultiplier);
        AssertApproximately(1f, phaseOneRocket.ExperimentalStingerBlastRadiusMultiplier);
    }

    [Fact]
    public void StingerPrimaryFireBurstsTrackedRocketInsteadOfSpawningAnother()
    {
        var rocket = new RocketProjectileEntity(
            id: 1,
            team: PlayerTeam.Red,
            ownerId: 1,
            x: 0f,
            y: 0f,
            speed: 12f,
            directionRadians: 0f,
            enableExperimentalStingerTracking: true);
        var speedBeforeBurst = rocket.Speed;

        Assert.True(rocket.TryApplyExperimentalStingerSpeedBurst(ExperimentalGameplaySettings.DefaultSoldierStingerBurstSpeedMultiplier));
        AssertApproximately(
            speedBeforeBurst * ExperimentalGameplaySettings.DefaultSoldierStingerBurstSpeedMultiplier,
            rocket.Speed);

        var speedAfterBurst = rocket.Speed;
        Assert.False(rocket.TryApplyExperimentalStingerSpeedBurst(ExperimentalGameplaySettings.DefaultSoldierStingerBurstSpeedMultiplier));
        AssertApproximately(speedAfterBurst, rocket.Speed);
    }

    [Fact]
    public void ManualStingerDetonationTriplesOwnerSelfKnockback()
    {
        var baselineWorld = CreateSoldierWorld(new ExperimentalGameplaySettings());
        var baselineOwner = baselineWorld.LocalPlayer;
        baselineOwner.TeleportTo(100f, 100f);
        var baselineRocket = new RocketProjectileEntity(
            id: 1,
            team: baselineOwner.Team,
            ownerId: baselineOwner.Id,
            x: baselineOwner.X,
            y: baselineOwner.Y,
            speed: 0f,
            directionRadians: 0f,
            rangeAnchorOwnerId: baselineOwner.Id,
            lastKnownRangeOriginX: baselineOwner.X,
            lastKnownRangeOriginY: baselineOwner.Y);

        InvokeRocketExplosion(baselineWorld, baselineRocket);
        var baselineImpulseSpeed = GetPlayerSpeedMagnitude(baselineOwner);

        var stingerWorld = CreateSoldierWorld(new ExperimentalGameplaySettings(EnableSoldierStingerRockets: true));
        var stingerOwner = stingerWorld.LocalPlayer;
        stingerOwner.TeleportTo(100f, 100f);
        var stingerRocket = new RocketProjectileEntity(
            id: 1,
            team: stingerOwner.Team,
            ownerId: stingerOwner.Id,
            x: stingerOwner.X,
            y: stingerOwner.Y,
            speed: 0f,
            directionRadians: 0f,
            rangeAnchorOwnerId: stingerOwner.Id,
            lastKnownRangeOriginX: stingerOwner.X,
            lastKnownRangeOriginY: stingerOwner.Y,
            enableExperimentalStingerTracking: true);
        stingerRocket.ArmExperimentalManualDetonation();

        InvokeRocketExplosion(stingerWorld, stingerRocket);
        var stingerImpulseSpeed = GetPlayerSpeedMagnitude(stingerOwner);

        AssertApproximately(
            baselineImpulseSpeed * ExperimentalGameplaySettings.DefaultSoldierStingerManualDetonationSelfKnockbackMultiplier,
            stingerImpulseSpeed,
            tolerance: 0.01f);
    }

    [Fact]
    public void DisableSelfDamagePreservesRocketJumpImpulse()
    {
        var world = CreateSoldierWorld(new ExperimentalGameplaySettings(DisableSelfDamage: true));
        var owner = world.LocalPlayer;
        owner.TeleportTo(100f, 100f);
        owner.ForceSetHealth(owner.MaxHealth);
        var healthBefore = owner.Health;

        var rocket = new RocketProjectileEntity(
            id: 1,
            team: owner.Team,
            ownerId: owner.Id,
            x: owner.X,
            y: owner.Y,
            speed: 0f,
            directionRadians: 0f,
            rangeAnchorOwnerId: owner.Id,
            lastKnownRangeOriginX: owner.X,
            lastKnownRangeOriginY: owner.Y);

        InvokeRocketExplosion(world, rocket);

        Assert.Equal(healthBefore, owner.Health);
        Assert.True(GetPlayerSpeedMagnitude(owner) > 0f);
    }

    [Fact]
    public void DangerCloseChainExplosionDrainsQueueWithoutLeavingReentrantState()
    {
        var world = CreateSoldierWorld(new ExperimentalGameplaySettings(EnableSoldierDangerClose: true));

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(2, out var firstVictim));

        Assert.True(world.TryPrepareNetworkPlayerJoin(3));
        Assert.True(world.TrySetNetworkPlayerTeam(3, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(3, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(3, out var secondVictim));

        firstVictim.TeleportTo(world.LocalPlayer.X + 20f, world.LocalPlayer.Y);
        secondVictim.TeleportTo(firstVictim.X + 4f, firstVictim.Y);
        firstVictim.ForceSetHealth(1);
        secondVictim.ForceSetHealth(1);

        var killMethod = typeof(SimulationWorld).GetMethod("KillPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(killMethod);

        _ = killMethod!.Invoke(
            world,
            [
                firstVictim,
                true,
                world.LocalPlayer,
                "RocketKL",
                DeadBodyAnimationKind.Default,
                null,
                null,
                null,
                true,
                true,
                false,
                true,
            ]);

        Assert.False(firstVictim.IsAlive);
        Assert.False(secondVictim.IsAlive);

        var processingField = typeof(SimulationWorld).GetField("_processingDangerCloseExplosions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(processingField);
        Assert.False((bool)processingField!.GetValue(world)!);

        var queueField = typeof(SimulationWorld).GetField("_pendingDangerCloseExplosions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(queueField);
        var queue = queueField!.GetValue(world);
        var countProperty = queue?.GetType().GetProperty("Count");
        Assert.NotNull(countProperty);
        Assert.Equal(0, (int)countProperty!.GetValue(queue)!);
    }

    private static SimulationWorld CreateSoldierWorld(ExperimentalGameplaySettings settings)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Soldier));
        world.ConfigureExperimentalGameplaySettings(settings);
        return world;
    }

    private static RocketCombatDefinition InvokeStingerRocketCombat(SimulationWorld world, PlayerEntity attacker, RocketCombatDefinition combat)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "ApplyExperimentalSoldierRocketCombat",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [attacker, combat]);
        Assert.IsType<RocketCombatDefinition>(result);
        return (RocketCombatDefinition)result!;
    }

    private static float InvokeStingerLaunchSpeed(SimulationWorld world, PlayerEntity attacker, float launchSpeed)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "ApplyExperimentalSoldierRocketLaunchSpeed",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [attacker, launchSpeed]);
        Assert.IsType<float>(result);
        return (float)result!;
    }

    private static float GetPlayerSpeedMagnitude(PlayerEntity player)
    {
        return MathF.Sqrt((player.HorizontalSpeed * player.HorizontalSpeed) + (player.VerticalSpeed * player.VerticalSpeed));
    }

    private static void InvokeRocketExplosion(SimulationWorld world, RocketProjectileEntity rocket)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "ExplodeRocket",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [rocket, null, null, null, -1]);
    }

    private static void AssertApproximately(float expected, float actual, float tolerance = 0.001f)
    {
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
    }
}
