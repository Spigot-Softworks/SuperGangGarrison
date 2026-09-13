using System.Collections;
using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CivilDefenseTurretRegressionTests
{
    [Theory]
    [InlineData("shot")]
    [InlineData("needle")]
    [InlineData("revolver")]
    [InlineData("rocket")]
    [InlineData("mine")]
    public void FastProjectileCrossingFromOutsideRangeIsInterceptedBeforeDamage(string kind)
    {
        var world = CreateWorld();
        var turret = DeployBuilt(world);
        var health = world.LocalPlayer.Health;
        var projectile = AddProjectile(world, kind, PlayerTeam.Blue, 700f, world.LocalPlayer.Y, -400f);

        world.AdvanceOneTick();

        Assert.Equal(health, world.LocalPlayer.Health);
        Assert.False(Entities(world).ContainsKey(projectile.Id));
        Assert.Equal(CivilDefenseTurretEntity.ReloadTicks, turret.ReloadTicksRemaining);
        Assert.InRange(turret.LastShotTargetX, 600f, 610f);
    }

    [Fact]
    public void CooldownStopsOnlyOneProjectilePerVolleyAndThenRecovers()
    {
        var world = CreateWorld();
        var turret = DeployBuilt(world);
        var first = AddProjectile(world, "shot", PlayerTeam.Blue, 550f, 500f, 0f, 90001);
        var second = AddProjectile(world, "shot", PlayerTeam.Blue, 560f, 500f, 0f, 90002);
        world.AdvanceOneTick();
        Assert.Equal(1, world.Shots.Count);
        for (var tick = 1; tick < CivilDefenseTurretEntity.ReloadTicks; tick++) world.AdvanceOneTick();
        Assert.Single(world.Shots);
        world.AdvanceOneTick();
        Assert.Empty(world.Shots);
        Assert.Equal(CivilDefenseTurretEntity.ReloadTicks, turret.ReloadTicksRemaining);
    }

    [Fact]
    public void FriendlyProjectilesAndOccludedEnemyProjectilesAreNotIntercepted()
    {
        var world = CreateWorld(wall: true);
        var turret = DeployBuilt(world);
        var friendly = AddProjectile(world, "shot", PlayerTeam.Red, 440f, 510f, 0f, 90001);
        var enemy = AddProjectile(world, "shot", PlayerTeam.Blue, 560f, 510f, 0f, 90002);
        world.AdvanceOneTick();
        Assert.True(Entities(world).ContainsKey(friendly.Id));
        Assert.True(Entities(world).ContainsKey(enemy.Id));
        Assert.True(turret.CanFire());
    }

    [Fact]
    public void AHitBeforeEnteringDefenseRangeIsNotUndoneByLaterInterception()
    {
        var world = CreateWorld();
        var turret = DeployBuilt(world);
        world.LocalPlayer.TeleportTo(680f, world.LocalPlayer.Y);
        var health = world.LocalPlayer.Health;
        AddProjectile(world, "shot", PlayerTeam.Blue, 750f, world.LocalPlayer.Y, -400f);
        world.AdvanceOneTick();
        Assert.True(world.LocalPlayer.Health < health);
        Assert.True(turret.CanFire());
    }

    [Fact]
    public void RepeatedDeploymentAndRespawnKeepOneTurretAndRejectionDoesNotSpendCooldown()
    {
        var world = CreateWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Soldier.CivilDefenseTurret]));
        var deploy = typeof(SimulationWorld).GetMethod("TryHandleExperimentalSoldierCivilDefenseTurret", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.True((bool)deploy.Invoke(world, [world.LocalPlayer])!);
        for (var tick = 0; tick < 120; tick++) world.AdvanceOneTick();
        Assert.Equal(0, world.LocalPlayer.PrimaryCooldownTicks);
        for (var attempt = 0; attempt < 12; attempt++)
            Assert.False((bool)deploy.Invoke(world, [world.LocalPlayer])!);
        Assert.Single(world.CivilDefenseTurrets);
        Assert.Equal(0, world.LocalPlayer.PrimaryCooldownTicks);
        world.ForceRespawnLocalPlayer();
        Assert.False((bool)deploy.Invoke(world, [world.LocalPlayer])!);
        Assert.Single(world.CivilDefenseTurrets);
    }

    [Fact]
    public void PredictionDoesNotCreateOrFireAuthoritativeTurrets()
    {
        var world = CreateWorld();
        var turret = DeployBuilt(world);
        world.ClientPredictionMode = true;
        Assert.False(Deploy(world));
        var shot = AddProjectile(world, "shot", PlayerTeam.Blue, 550f, 500f, 0f);
        typeof(SimulationWorld).GetMethod("AdvanceCivilDefenseTurrets", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(world, null);
        Assert.True(turret.CanFire());
        Assert.True(Entities(world).ContainsKey(shot.Id));
    }

    internal static SimulationWorld CreateWorld(bool wall = false)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableEnemyTrainingDummy = false, EnableFriendlySupportDummy = false });
        var solids = new List<LevelSolid> { new(0, 550, 1600, 50) };
        if (wall) solids.Add(new(500, 400, 20, 150));
        world.CombatTestSetLevel(new SimpleLevel("turret_regression", GameModeKind.CaptureTheFlag,
            new WorldBounds(1600, 600), 1, null, 1, 1, new(400, 100), [new(400, 100)], [new(1400, 100)],
            [], [], 550, solids, false));
        world.SetPendingLocalPlayerClass(PlayerClass.Soldier);
        world.ForceRespawnLocalPlayer();
        for (var tick = 0; tick < 90; tick++) world.AdvanceOneTick();
        return world;
    }

    private static bool Deploy(SimulationWorld world) => (bool)typeof(SimulationWorld)
        .GetMethod("TryDeployCivilDefenseTurret", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [world.LocalPlayer])!;

    internal static CivilDefenseTurretEntity DeployBuilt(SimulationWorld world)
    {
        Assert.True(Deploy(world));
        for (var tick = 0; tick < 90; tick++) world.AdvanceOneTick();
        var turret = Assert.Single(world.CivilDefenseTurrets);
        Assert.True(turret.CanFire());
        return turret;
    }

    private static Dictionary<int, SimulationEntity> Entities(SimulationWorld world) =>
        (Dictionary<int, SimulationEntity>)typeof(SimulationWorld).GetField("_entities", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(world)!;

    private static SimulationEntity AddProjectile(SimulationWorld world, string kind, PlayerTeam team, float x, float y, float velocityX, int id = 90000)
    {
        (SimulationEntity Entity, string Collection) projectile = kind switch
        {
            "needle" => (new NeedleProjectileEntity(id, team, 9999, x, y, velocityX, 0), "_needles"),
            "revolver" => (new RevolverProjectileEntity(id, team, 9999, x, y, velocityX, 0), "_revolverShots"),
            "rocket" => (new RocketProjectileEntity(id, team, 9999, x, y, MathF.Abs(velocityX) * world.Config.TicksPerSecond, MathF.PI), "_rockets"),
            "mine" => (new MineProjectileEntity(id, team, 9999, x, y, velocityX, 0), "_mines"),
            _ => (new ShotProjectileEntity(id, team, 9999, x, y, velocityX, 0), "_shots"),
        };
        ((IList)typeof(SimulationWorld).GetField(projectile.Collection, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(world)!).Add(projectile.Entity);
        Entities(world).Add(id, projectile.Entity);
        return projectile.Entity;
    }
}
