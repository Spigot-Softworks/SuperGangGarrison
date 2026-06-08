using OpenGarrison.Core;
using System.Collections;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldGrenadeDamageTests
{
    [Fact]
    public void GrenadeDirectHitDealsDirectImpactDamage()
    {
        var world = CreateCombatWorld();
        var owner = world.LocalPlayer;
        owner.TeleportTo(-500f, 0f);
        var enemy = AddEnemy(world, id: 2, x: 32f, y: 0f);
        var healthBefore = enemy.Health;

        _ = SpawnGrenade(world, owner, x: 0f, y: 0f, velocityX: 40f, velocityY: 0f);
        AdvanceGrenades(world);

        Assert.Equal(healthBefore - (int)GrenadeProjectileEntity.DirectHitDamage, enemy.Health);
        Assert.Equal(0, GetGrenadeCount(world));
    }

    [Fact]
    public void GrenadeSplashWithoutDirectHitUsesBaseExplosionDamage()
    {
        var world = CreateCombatWorld();
        var owner = world.LocalPlayer;
        owner.TeleportTo(-500f, 0f);
        var enemy = AddEnemy(world, id: 2, x: 32f, y: 0f);
        var healthBefore = enemy.Health;

        var grenade = SpawnGrenade(world, owner, x: enemy.X, y: enemy.Y);
        ExplodeGrenade(world, grenade);

        Assert.Equal(healthBefore - (int)GrenadeProjectileEntity.BaseExplosionDamage, enemy.Health);
    }

    [Fact]
    public void ClientPredictionGrenadeExplosionEmitsLocalPresentationWithoutDamage()
    {
        var world = CreateCombatWorld();
        world.ClientPredictionMode = true;
        var owner = world.LocalPlayer;
        owner.TeleportTo(-500f, 0f);
        var enemy = AddEnemy(world, id: 2, x: 32f, y: 0f);
        var healthBefore = enemy.Health;

        var grenade = SpawnGrenade(world, owner, x: enemy.X, y: enemy.Y);
        ExplodeGrenade(world, grenade);

        Assert.Equal(healthBefore, enemy.Health);
        Assert.Single(world.PendingSoundEvents, soundEvent => soundEvent.SoundName == "ExplosionSnd");
        Assert.Single(world.PendingVisualEvents, visualEvent => visualEvent.EffectName == "Explosion");
        Assert.Empty(world.PendingDamageEvents);
    }

    [Fact]
    public void ClientPredictionSkipsRemoteOwnedGrenadeAdvance()
    {
        var world = CreateCombatWorld();
        world.ClientPredictionMode = true;
        var remoteOwner = AddEnemy(world, id: 2, x: 128f, y: 0f);

        var grenade = SpawnGrenade(world, remoteOwner, x: 0f, y: 0f, velocityX: 40f, velocityY: 0f);
        var xBefore = grenade.X;
        var yBefore = grenade.Y;
        var velocityXBefore = grenade.VelocityX;
        var velocityYBefore = grenade.VelocityY;
        var fuseBefore = grenade.FuseTicksLeft;

        AdvanceGrenades(world);

        Assert.Equal(1, GetGrenadeCount(world));
        Assert.Equal(xBefore, grenade.X);
        Assert.Equal(yBefore, grenade.Y);
        Assert.Equal(velocityXBefore, grenade.VelocityX);
        Assert.Equal(velocityYBefore, grenade.VelocityY);
        Assert.Equal(fuseBefore, grenade.FuseTicksLeft);
        Assert.Empty(world.PendingDamageEvents);
        Assert.Empty(world.PendingSoundEvents);
        Assert.Empty(world.PendingVisualEvents);
    }

    private static SimulationWorld CreateCombatWorld()
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Demoman));
        SetCombatLevel(
            world,
            new SimpleLevel(
                name: "grenade_damage_test",
                mode: GameModeKind.CaptureTheFlag,
                bounds: new WorldBounds(2048f, 2048f),
                mapScale: 1f,
                backgroundAssetName: null,
                mapAreaIndex: 1,
                mapAreaCount: 1,
                localSpawn: new SpawnPoint(0f, 0f),
                redSpawns: [new SpawnPoint(0f, 0f)],
                blueSpawns: [new SpawnPoint(256f, 0f)],
                intelBases:
                [
                    new IntelBaseMarker(PlayerTeam.Red, 0f, 0f),
                    new IntelBaseMarker(PlayerTeam.Blue, 256f, 0f),
                ],
                roomObjects: [],
                floorY: 2048f,
                solids: [],
                importedFromSource: false));
        return world;
    }

    private static PlayerEntity AddEnemy(SimulationWorld world, int id, float x, float y)
    {
        var networkId = checked((byte)id);
        Assert.True(world.TryPrepareNetworkPlayerJoin(networkId));
        Assert.True(world.TrySetNetworkPlayerTeam(networkId, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(networkId, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(networkId, out var enemy));
        enemy.TeleportTo(x, y);
        return enemy;
    }

    private static void SetCombatLevel(SimulationWorld world, SimpleLevel level)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [level]);
    }

    private static GrenadeProjectileEntity SpawnGrenade(
        SimulationWorld world,
        PlayerEntity owner,
        float x,
        float y,
        float velocityX = 0f,
        float velocityY = 0f)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSpawnGrenade", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [owner, x, y, velocityX, velocityY]);
        Assert.IsType<GrenadeProjectileEntity>(result);
        return (GrenadeProjectileEntity)result!;
    }

    private static void ExplodeGrenade(SimulationWorld world, GrenadeProjectileEntity grenade)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestExplodeGrenade", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [grenade]);
    }

    private static void AdvanceGrenades(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod("AdvanceGrenades", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, []);
    }

    private static int GetGrenadeCount(SimulationWorld world)
    {
        var field = typeof(SimulationWorld).GetField("_grenades", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var grenades = field!.GetValue(world) as ICollection;
        Assert.NotNull(grenades);
        return grenades!.Count;
    }
}
