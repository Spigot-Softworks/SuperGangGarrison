using System.Collections.Generic;
using System.Linq;
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
    public void ClientPredictionRocketExplosionRemovesRocketAndPresentsLocalEffectsWithoutDamageEvents()
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
        Assert.Equal(1, CountExplosionSounds(world.PendingSoundEvents));
        Assert.Equal(1, CountExplosionVisuals(world.PendingVisualEvents));
        Assert.Empty(world.PendingDamageEvents);
    }

    [Fact]
    public void ClientPredictionSkipsRemoteOwnedRocketAdvance()
    {
        var world = new SimulationWorld
        {
            ClientPredictionMode = true,
        };
        Assert.True(world.TrySetLocalClass(PlayerClass.Soldier));
        var remoteOwner = AddEnemy(world, id: 2, x: 128f, y: 0f);

        var rocket = InvokeCombatTestSpawnRocket(
            world,
            remoteOwner,
            remoteOwner.X + 24f,
            remoteOwner.Y,
            speed: 10f,
            directionRadians: 0f);
        var xBefore = rocket.X;
        var yBefore = rocket.Y;
        var ticksBefore = rocket.TicksRemaining;

        InvokeAdvanceRockets(world);

        Assert.Contains(world.Rockets, current => ReferenceEquals(current, rocket));
        Assert.Equal(xBefore, rocket.X);
        Assert.Equal(yBefore, rocket.Y);
        Assert.Equal(ticksBefore, rocket.TicksRemaining);
        Assert.Empty(world.PendingDamageEvents);
        Assert.Empty(world.PendingSoundEvents);
        Assert.Empty(world.PendingVisualEvents);
    }

    [Fact]
    public void ClientPredictionUsesServerResolvedLocalPlayerIdForRocketAdvance()
    {
        var world = new SimulationWorld
        {
            ClientPredictionMode = true,
        };
        Assert.True(world.TrySetLocalClass(PlayerClass.Soldier));
        var serverResolvedLocalOwner = AddEnemy(world, id: 2, x: 128f, y: 0f);
        SetAuthoritativeLocalPlayerId(world, serverResolvedLocalOwner.Id);

        var authoritativeRocket = InvokeCombatTestSpawnRocket(
            world,
            serverResolvedLocalOwner,
            serverResolvedLocalOwner.X + 24f,
            serverResolvedLocalOwner.Y,
            speed: 10f,
            directionRadians: 0f);
        var staleFallbackRocket = InvokeCombatTestSpawnRocket(
            world,
            world.LocalPlayer,
            world.LocalPlayer.X + 24f,
            world.LocalPlayer.Y,
            speed: 10f,
            directionRadians: 0f);
        var authoritativeXBefore = authoritativeRocket.X;
        var staleFallbackXBefore = staleFallbackRocket.X;

        InvokeAdvanceRockets(world);

        Assert.NotEqual(authoritativeXBefore, authoritativeRocket.X);
        Assert.Equal(staleFallbackXBefore, staleFallbackRocket.X);
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
        float y,
        float speed = 0f,
        float directionRadians = 0f)
    {
        return world.CombatTestSpawnRocket(owner, x, y, speed, directionRadians);
    }

    private static void InvokeCombatTestExplodeRocket(SimulationWorld world, RocketProjectileEntity rocket)
    {
        world.CombatTestExplodeRocket(rocket);
    }

    private static void InvokeAdvanceRockets(SimulationWorld world)
    {
        world.Projectiles.AdvanceRockets();
    }

    private static PlayerEntity AddEnemy(SimulationWorld world, int id, float x, float y)
    {
        var networkId = checked((byte)id);
        Assert.True(world.NetworkPlayerRules.TryPrepareNetworkPlayerJoin(networkId));
        Assert.True(world.NetworkPlayerRules.TrySetNetworkPlayerTeam(networkId, PlayerTeam.Blue));
        Assert.True(world.NetworkPlayerRules.TryApplyNetworkPlayerClassSelection(networkId, PlayerClass.Soldier));
        Assert.True(world.NetworkPlayerRules.TryGetNetworkPlayer(networkId, out var enemy));
        enemy.TeleportTo(x, y);
        return enemy;
    }

    private static void SetAuthoritativeLocalPlayerId(SimulationWorld world, int playerId)
    {
        world.ClientSnapshots.AuthoritativeLocalPlayerId = playerId;
    }
}
