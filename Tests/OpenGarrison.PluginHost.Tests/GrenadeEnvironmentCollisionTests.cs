using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class GrenadeEnvironmentCollisionTests
{
    [Fact]
    public void GrenadeStartingInsideBulletWallEscapesAndKeepsItsFuseAndDamage()
    {
        var bulletWall = new RoomObjectMarker(RoomObjectType.BulletWall, 100f, 0f, 60f, 60f, "BulletWall");
        var world = CreateWorld([bulletWall], []);
        var owner = world.LocalPlayer;
        owner.TeleportTo(-500f, -500f);
        var grenade = SpawnGrenade(world, owner, x: 115f, y: 30f, velocityX: -4f, velocityY: 0f);
        var fuseBefore = grenade.FuseTicksLeft;
        var damageBefore = grenade.ExplosionDamage;

        world.Projectiles.AdvanceGrenades();

        Assert.Single(world.Grenades);
        Assert.True(grenade.X < bulletWall.Left);
        Assert.True(grenade.VelocityX < 0f);
        Assert.False(grenade.HasBounced);
        Assert.Equal(fuseBefore - 1, grenade.FuseTicksLeft);
        Assert.Equal(damageBefore, grenade.ExplosionDamage);
    }

    [Theory]
    [InlineData(120f)] // The next wall touches the first.
    [InlineData(110f)] // The walls overlap.
    [InlineData(122f)] // The 2px gap is narrower than the 3px ejection clearance.
    public void GrenadeOverlapEscapeClearsConnectedAndNearConnectedWalls(float secondWallLeft)
    {
        var firstWall = new RoomObjectMarker(RoomObjectType.BulletWall, 100f, 0f, 20f, 60f, "FirstBulletWall");
        var secondWall = new RoomObjectMarker(RoomObjectType.BulletWall, secondWallLeft, 0f, 20f, 60f, "SecondBulletWall");
        var world = CreateWorld([firstWall, secondWall], []);
        var owner = world.LocalPlayer;
        owner.TeleportTo(-500f, -500f);
        var grenade = SpawnGrenade(world, owner, x: 118f, y: 30f, velocityX: 20f, velocityY: 0f);
        var fuseBefore = grenade.FuseTicksLeft;
        var damageBefore = grenade.ExplosionDamage;

        world.Projectiles.AdvanceGrenades();

        var connectedRight = MathF.Max(firstWall.Right, secondWall.Right);
        Assert.Single(world.Grenades);
        Assert.True(
            grenade.X <= firstWall.Left - GrenadeProjectileEntity.EnvironmentCollisionBackoffDistance
                || grenade.X >= connectedRight + GrenadeProjectileEntity.EnvironmentCollisionBackoffDistance,
            $"Grenade stopped inside the connected blocker span at x={grenade.X}.");
        Assert.Equal(fuseBefore - 1, grenade.FuseTicksLeft);
        Assert.Equal(damageBefore, grenade.ExplosionDamage);
    }

    [Theory]
    [InlineData(RoomObjectType.BulletWall)]
    [InlineData(RoomObjectType.TeamGate)]
    public void GrenadeStillBouncesFromOrdinaryRoomObjectBlockers(RoomObjectType blockerType)
    {
        var blocker = new RoomObjectMarker(blockerType, 100f, 0f, 6f, 60f, blockerType.ToString(), PlayerTeam.Blue);
        var world = CreateWorld([blocker], []);
        var owner = world.LocalPlayer;
        owner.TeleportTo(-500f, -500f);
        var grenade = SpawnGrenade(world, owner, x: 90f, y: 30f, velocityX: 25f, velocityY: 0f);

        world.Projectiles.AdvanceGrenades();

        Assert.Single(world.Grenades);
        Assert.True(grenade.HasBounced);
        Assert.True(grenade.X < blocker.Left);
        Assert.True(grenade.VelocityX < 0f);
        Assert.Equal(GrenadeProjectileEntity.FuseTicksRemaining - 1, grenade.FuseTicksLeft);
    }

    [Fact]
    public void GrenadeStillBouncesDownFromSolidCeiling()
    {
        var ceiling = new LevelSolid(0f, 0f, 256f, 6f);
        var world = CreateWorld([], [ceiling]);
        var owner = world.LocalPlayer;
        owner.TeleportTo(-500f, -500f);
        var grenade = SpawnGrenade(world, owner, x: 100f, y: 10f, velocityX: 0f, velocityY: -15f);

        world.Projectiles.AdvanceGrenades();

        Assert.Single(world.Grenades);
        Assert.True(grenade.HasBounced);
        Assert.True(grenade.Y > ceiling.Bottom);
        Assert.True(grenade.VelocityY > 0f);
        Assert.Equal(GrenadeProjectileEntity.FuseTicksRemaining - 1, grenade.FuseTicksLeft);
    }

    [Fact]
    public void RedShotsOnlyBarrierBlocksRedGrenadesAndLetsBlueGrenadesPass()
    {
        var filters = new BarrierTargetFilters(
            RedPlayers: BarrierTargetFilter.Allow,
            BluePlayers: BarrierTargetFilter.Allow,
            RedShots: BarrierTargetFilter.Block,
            BlueShots: BarrierTargetFilter.Allow,
            RedIntel: BarrierTargetFilter.Allow,
            BlueIntel: BarrierTargetFilter.Allow);
        var barrier = BarrierConfiguration.CreateMarker(
            100f,
            0f,
            1f,
            1f,
            new BarrierConfiguration(filters));

        var redWorld = CreateWorld([barrier], []);
        var redOwner = redWorld.LocalPlayer;
        redOwner.TeleportTo(-500f, -500f);
        var redGrenade = SpawnGrenade(redWorld, redOwner, x: 90f, y: 30f, velocityX: 25f, velocityY: 0f);
        redWorld.Projectiles.AdvanceGrenades();

        var blueWorld = CreateWorld([barrier], []);
        var blueOwner = AddBlueOwner(blueWorld);
        var blueGrenade = SpawnGrenade(blueWorld, blueOwner, x: 90f, y: 30f, velocityX: 25f, velocityY: 0f);
        blueWorld.Projectiles.AdvanceGrenades();

        Assert.True(redGrenade.HasBounced);
        Assert.True(redGrenade.X < barrier.Left);
        Assert.True(redGrenade.VelocityX < 0f);
        Assert.False(blueGrenade.HasBounced);
        Assert.True(blueGrenade.X > barrier.Right);
        Assert.True(blueGrenade.VelocityX > 0f);
        Assert.Equal(GrenadeProjectileEntity.FuseTicksRemaining - 1, redGrenade.FuseTicksLeft);
        Assert.Equal(GrenadeProjectileEntity.FuseTicksRemaining - 1, blueGrenade.FuseTicksLeft);
    }

    private static SimulationWorld CreateWorld(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        IReadOnlyList<LevelSolid> solids)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Demoman));
        world.TestSetLevel(new SimpleLevel(
            name: "grenade_environment_collision_test",
            mode: GameModeKind.TeamDeathmatch,
            bounds: new WorldBounds(2048f, 2048f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 0,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(0f, 0f),
            redSpawns: [],
            blueSpawns: [],
            intelBases: [],
            roomObjects: roomObjects,
            floorY: 2048f,
            solids: solids,
            importedFromSource: false));
        return world;
    }

    private static PlayerEntity AddBlueOwner(SimulationWorld world)
    {
        const byte playerId = 2;
        Assert.True(world.NetworkPlayers.TryPrepareNetworkPlayerJoin(playerId));
        Assert.True(world.NetworkPlayers.TrySetNetworkPlayerTeam(playerId, PlayerTeam.Blue));
        Assert.True(world.NetworkPlayers.TryApplyNetworkPlayerClassSelection(playerId, PlayerClass.Scout));
        Assert.True(world.NetworkPlayers.TryGetNetworkPlayer(playerId, out var owner));
        owner.TeleportTo(-500f, -500f);
        return owner;
    }

    private static GrenadeProjectileEntity SpawnGrenade(
        SimulationWorld world,
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY)
    {
        return world.TestSpawnGrenade(owner, x, y, velocityX, velocityY);
    }
}
