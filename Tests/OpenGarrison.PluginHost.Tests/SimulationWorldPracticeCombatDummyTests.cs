using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldPracticeCombatDummyTests
{
    private static readonly MethodInfo ApplyPlayerDamageMethod = GetRequiredNonPublicMethod(
        "ApplyPlayerDamage",
        typeof(PlayerEntity),
        typeof(int),
        typeof(PlayerEntity),
        typeof(float),
        typeof(DamageEventFlags),
        typeof(bool),
        typeof(bool));

    private static readonly MethodInfo ApplyPlayerContinuousDamageMethod = GetRequiredNonPublicMethod(
        "ApplyPlayerContinuousDamage",
        typeof(PlayerEntity),
        typeof(float),
        typeof(PlayerEntity),
        typeof(float),
        typeof(DamageEventFlags),
        typeof(bool),
        typeof(bool));

    [Fact]
    public void SpawnPracticeCombatDummyUsesHeavyHumiliationPose()
    {
        var world = new SimulationWorld();

        world.SpawnPracticeCombatDummy();

        Assert.True(world.PracticeCombatDummyActive);
        Assert.True(world.EnemyPlayerEnabled);
        Assert.True(world.EnemyPlayer.IsAlive);
        Assert.Equal(PlayerClass.Heavy, world.EnemyPlayer.ClassId);
        Assert.True(world.IsPracticeCombatDummy(world.EnemyPlayer));
        Assert.False(world.IsPracticeDpsDummy(world.EnemyPlayer));
        Assert.True(world.IsPlayerHumiliated(world.EnemyPlayer));
        Assert.False(world.PracticeCombatDummyDpsVisible);
        Assert.Equal(0, world.PracticeCombatDummyTotalDamage);
        Assert.Equal(0d, world.PracticeCombatDummyDps);
    }

    [Fact]
    public void SpawnPracticeDpsDummyUsesHeavyHumiliationPose()
    {
        var world = new SimulationWorld();

        world.SpawnPracticeDpsDummy();

        Assert.True(world.PracticeDpsDummyActive);
        Assert.True(world.EnemyPlayerEnabled);
        Assert.True(world.EnemyPlayer.IsAlive);
        Assert.Equal(PlayerClass.Heavy, world.EnemyPlayer.ClassId);
        Assert.False(world.IsPracticeCombatDummy(world.EnemyPlayer));
        Assert.True(world.IsPracticeDpsDummy(world.EnemyPlayer));
        Assert.True(world.IsPlayerHumiliated(world.EnemyPlayer));
        Assert.False(world.PracticeCombatDummyDpsVisible);
        Assert.Equal(0, world.PracticeCombatDummyTotalDamage);
        Assert.Equal(0d, world.PracticeCombatDummyDps);
    }

    [Fact]
    public void PracticeCombatDummyTakesDamageNormallyWithoutTrackingDps()
    {
        var world = new SimulationWorld();
        world.SpawnPracticeCombatDummy();
        var target = world.EnemyPlayer;
        var attacker = world.LocalPlayer;
        var damage = 50;

        var died = InvokeApplyPlayerDamage(world, target, damage, attacker);

        Assert.False(died);
        Assert.True(target.IsAlive);
        Assert.Equal(target.MaxHealth - damage, target.Health);
        Assert.False(world.PracticeCombatDummyDpsVisible);
        Assert.Equal(0, world.PracticeCombatDummyTotalDamage);
        Assert.Equal(0d, world.PracticeCombatDummyDps);
        Assert.Equal(0f, world.PracticeCombatDummyDamageIntensity);
        var damageEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.Equal(damage, damageEvent.Amount);
        Assert.Equal(attacker.Id, damageEvent.AttackerPlayerId);
        Assert.Equal(target.Id, damageEvent.TargetEntityId);
        Assert.False(damageEvent.WasFatal);
    }

    [Fact]
    public void PracticeCombatDummyFatalDamageIsNotAbsorbed()
    {
        var world = new SimulationWorld();
        world.SpawnPracticeCombatDummy();
        var target = world.EnemyPlayer;
        var attacker = world.LocalPlayer;
        var damage = target.MaxHealth + 75;

        var died = InvokeApplyPlayerDamage(world, target, damage, attacker);

        Assert.True(died);
        Assert.Equal(0, target.Health);
        Assert.False(world.PracticeCombatDummyDpsVisible);
        Assert.Equal(0, world.PracticeCombatDummyTotalDamage);
        var damageEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.Equal(target.MaxHealth, damageEvent.Amount);
        Assert.True(damageEvent.WasFatal);
    }

    [Fact]
    public void PracticeDpsDummyAbsorbsFatalDamageAndTracksDps()
    {
        var world = new SimulationWorld();
        world.SpawnPracticeDpsDummy();
        var target = world.EnemyPlayer;
        var attacker = world.LocalPlayer;
        var damage = target.MaxHealth + 75;

        var died = InvokeApplyPlayerDamage(world, target, damage, attacker);

        Assert.False(died);
        Assert.True(target.IsAlive);
        Assert.Equal(target.MaxHealth, target.Health);
        Assert.True(world.PracticeCombatDummyDpsVisible);
        Assert.Equal(damage, world.PracticeCombatDummyTotalDamage);
        Assert.Equal(damage, world.PracticeCombatDummyDps, 5);
        Assert.True(world.PracticeCombatDummyDamageIntensity > 0f);
        var damageEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.Equal(damage, damageEvent.Amount);
        Assert.Equal(attacker.Id, damageEvent.AttackerPlayerId);
        Assert.Equal(target.Id, damageEvent.TargetEntityId);
        Assert.False(damageEvent.WasFatal);
    }

    [Fact]
    public void PracticeDpsDummyCountsContinuousDamageWithoutDamagingHealth()
    {
        var world = new SimulationWorld();
        world.SpawnPracticeDpsDummy();
        var target = world.EnemyPlayer;
        var attacker = world.LocalPlayer;

        _ = InvokeApplyPlayerContinuousDamage(world, target, 0.4f, attacker);
        _ = InvokeApplyPlayerContinuousDamage(world, target, 0.4f, attacker);
        _ = InvokeApplyPlayerContinuousDamage(world, target, 0.4f, attacker);

        Assert.True(target.IsAlive);
        Assert.Equal(target.MaxHealth, target.Health);
        Assert.Equal(1, world.PracticeCombatDummyTotalDamage);
        var damageEvent = Assert.Single(world.DrainPendingDamageEvents());
        Assert.Equal(1, damageEvent.Amount);
        Assert.False(damageEvent.WasFatal);
    }

    [Fact]
    public void PracticeDpsDummyDpsExpiresQuicklyAndNextDamageStartsFreshBurst()
    {
        var world = new SimulationWorld();
        world.SpawnPracticeDpsDummy();
        var target = world.EnemyPlayer;
        var attacker = world.LocalPlayer;

        _ = InvokeApplyPlayerDamage(world, target, 120, attacker);
        Assert.True(world.PracticeCombatDummyDpsVisible);
        Assert.Equal(120, world.PracticeCombatDummyTotalDamage);

        var timeoutTicks = (int)Math.Ceiling(4d * world.Config.TicksPerSecond);
        for (var tick = 0; tick < timeoutTicks + 1; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.False(world.PracticeCombatDummyDpsVisible);
        Assert.Equal(0, world.PracticeCombatDummyTotalDamage);
        Assert.Equal(0d, world.PracticeCombatDummyDps);
        Assert.Equal(0f, world.PracticeCombatDummyDamageIntensity);

        _ = InvokeApplyPlayerDamage(world, target, 30, attacker);

        Assert.True(world.PracticeCombatDummyDpsVisible);
        Assert.Equal(30, world.PracticeCombatDummyTotalDamage);
        Assert.Equal(30, world.PracticeCombatDummyDps, 5);
    }

    [Fact]
    public void DespawnPracticeDpsDummyClearsModeAndStats()
    {
        var world = new SimulationWorld();
        world.SpawnPracticeDpsDummy();
        _ = InvokeApplyPlayerDamage(world, world.EnemyPlayer, 50, world.LocalPlayer);

        world.DespawnPracticeDpsDummy();

        Assert.False(world.PracticeDpsDummyActive);
        Assert.False(world.EnemyPlayerEnabled);
        Assert.False(world.EnemyPlayer.IsAlive);
        Assert.Equal(0, world.PracticeCombatDummyTotalDamage);
        Assert.Equal(0d, world.PracticeCombatDummyDps);
    }

    private static bool InvokeApplyPlayerDamage(
        SimulationWorld world,
        PlayerEntity target,
        int damage,
        PlayerEntity attacker)
    {
        return (bool)ApplyPlayerDamageMethod.Invoke(
            world,
            [target, damage, attacker, PlayerEntity.SpyDamageRevealAlpha, DamageEventFlags.None, true, true])!;
    }

    private static bool InvokeApplyPlayerContinuousDamage(
        SimulationWorld world,
        PlayerEntity target,
        float damage,
        PlayerEntity attacker)
    {
        return (bool)ApplyPlayerContinuousDamageMethod.Invoke(
            world,
            [target, damage, attacker, PlayerEntity.SpyDamageRevealAlpha, DamageEventFlags.None, true, true])!;
    }

    private static MethodInfo GetRequiredNonPublicMethod(string name, params Type[] parameterTypes)
    {
        return typeof(SimulationWorld).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                parameterTypes,
                modifiers: null)
            ?? throw new MissingMethodException(typeof(SimulationWorld).FullName, name);
    }
}
