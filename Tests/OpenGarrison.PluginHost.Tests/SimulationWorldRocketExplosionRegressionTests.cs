using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldRocketExplosionRegressionTests
{
    [Fact]
    public void RocketExplosionConsumesSameRocketInstanceOnce()
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Soldier));
        var owner = world.LocalPlayer;

        _ = world.DrainPendingSoundEvents();
        _ = world.DrainPendingVisualEvents();
        _ = world.DrainPendingDamageEvents();

        var rocket = InvokeCombatTestSpawnRocket(world, owner, owner.X + 24f, owner.Y);

        InvokeCombatTestExplodeRocket(world, rocket);

        Assert.Empty(world.Rockets);
        Assert.Equal(1, CountExplosionSounds(world.PendingSoundEvents));
        Assert.Equal(1, CountExplosionVisuals(world.PendingVisualEvents));

        InvokeCombatTestExplodeRocket(world, rocket);

        Assert.Empty(world.Rockets);
        Assert.Equal(1, CountExplosionSounds(world.PendingSoundEvents));
        Assert.Equal(1, CountExplosionVisuals(world.PendingVisualEvents));
    }

    [Fact]
    public void ClientPredictionRocketExplosionRemovesRocketWithoutPresentingAuthoritativeEvents()
    {
        var world = new SimulationWorld
        {
            ClientPredictionMode = true,
        };
        Assert.True(world.TrySetLocalClass(PlayerClass.Soldier));
        var owner = world.LocalPlayer;

        var rocket = InvokeCombatTestSpawnRocket(world, owner, owner.X + 24f, owner.Y);

        InvokeCombatTestExplodeRocket(world, rocket);

        Assert.Empty(world.Rockets);
        Assert.Empty(world.PendingSoundEvents);
        Assert.Empty(world.PendingVisualEvents);
        Assert.Empty(world.PendingDamageEvents);
    }

    private static int CountExplosionSounds(IReadOnlyList<WorldSoundEvent> events)
    {
        return events.Count(static soundEvent => soundEvent.SoundName == "ExplosionSnd");
    }

    private static int CountExplosionVisuals(IReadOnlyList<WorldVisualEvent> events)
    {
        return events.Count(static visualEvent => visualEvent.EffectName == "Explosion");
    }

    private static RocketProjectileEntity InvokeCombatTestSpawnRocket(
        SimulationWorld world,
        PlayerEntity owner,
        float x,
        float y)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "CombatTestSpawnRocket",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [owner, x, y, 0f, 0f]);
        return Assert.IsType<RocketProjectileEntity>(result);
    }

    private static void InvokeCombatTestExplodeRocket(SimulationWorld world, RocketProjectileEntity rocket)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "CombatTestExplodeRocket",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(RocketProjectileEntity)],
            modifiers: null);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [rocket]);
    }
}
