using System;
using System.Linq;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class CombatResolverTests
{
    [Fact]
    public void HasObstacleLineOfSight_UsesSolidGeometry()
    {
        var world = CreateWorld();
        var solid = new LevelSolid(200f, 150f, 100f, 80f);
        world.CombatTestSetLevel(CreateLevel(solids: [solid]));
        var y = Midpoint(solid.Top, solid.Bottom);

        Assert.False(world.CombatTestHasObstacleLineOfSight(solid.Left - 20f, y, solid.Right + 20f, y));
        Assert.True(world.CombatTestHasObstacleLineOfSight(solid.Left - 60f, solid.Top - 24f, solid.Left - 12f, solid.Top - 24f));
    }

    [Fact]
    public void HasDirectLineOfSight_UsesTeamGateOwnershipRules()
    {
        var world = CreateWorld();
        var gate = new RoomObjectMarker(RoomObjectType.TeamGate, 300f, 200f, 20f, 80f, "gate", PlayerTeam.Red, "test-gate");
        world.CombatTestSetLevel(CreateLevel(roomObjects: [gate]));
        var y = Midpoint(gate.Top, gate.Bottom);
        var blockingTeam = gate.Team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;

        Assert.False(world.CombatTestHasDirectLineOfSight(gate.Left - 8f, y, gate.Right + 8f, y, blockingTeam));
        Assert.True(world.CombatTestHasDirectLineOfSight(gate.Left - 8f, y, gate.Right + 8f, y, gate.Team!.Value));
    }

    [Fact]
    public void IsFlameSpawnBlocked_UsesGateRules()
    {
        var world = CreateWorld();
        var gate = new RoomObjectMarker(RoomObjectType.TeamGate, 300f, 200f, 20f, 80f, "gate", PlayerTeam.Red, "test-gate");
        world.CombatTestSetLevel(CreateLevel(roomObjects: [gate]));
        var y = Midpoint(gate.Top, gate.Bottom);
        world.EnemyPlayer.TeleportTo(gate.Left - 12f, y);

        Assert.True(world.CombatTestIsFlameSpawnBlocked(world.EnemyPlayer.X, world.EnemyPlayer.Y, gate.Right + 12f, y, world.EnemyPlayer.Team));
        Assert.False(world.CombatTestIsFlameSpawnBlocked(world.EnemyPlayer.X, world.EnemyPlayer.Y, gate.Left - 24f, y, world.EnemyPlayer.Team));
    }

    [Fact]
    public void GetNearestShotHit_ReturnsClosestEnemyPlayer()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        const float y = 220f;
        const float originX = 100f;

        world.TeleportLocalPlayer(originX, y);
        world.EnemyPlayer.TeleportTo(originX + 50f, y);

        const byte extraSlot = 3;
        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var fartherEnemy));
        fartherEnemy.TeleportTo(originX + 100f, y);

        var shot = CreateShot(world.LocalPlayer, originX, y);
        var hit = world.CombatTestGetNearestShotHit(shot, 1f, 0f, 200f);

        Assert.NotNull(hit);
        Assert.Same(world.EnemyPlayer, hit!.Value.HitPlayer);
        Assert.Null(hit.Value.HitSentry);
        Assert.InRange(hit.Value.Distance, 30f, 80f);
    }

    [Fact]
    public void GetNearestShotHit_ReturnsEnemyGeneratorWhenPathIsClear()
    {
        var world = CreateWorld();
        var generator = new RoomObjectMarker(RoomObjectType.Generator, 150f, 200f, 40f, 40f, "GeneratorBlueS", PlayerTeam.Blue, "GeneratorBlue");
        world.CombatTestSetLevel(CreateLevel(mode: GameModeKind.Generator, roomObjects: [generator]));

        var shot = CreateShot(world.LocalPlayer, 100f, 220f);
        var hit = world.CombatTestGetNearestShotHit(shot, 1f, 0f, 200f);

        Assert.NotNull(hit);
        Assert.Null(hit!.Value.HitPlayer);
        Assert.Null(hit.Value.HitSentry);
        Assert.Equal(PlayerTeam.Blue, hit.Value.HitGenerator!.Team);
        Assert.InRange(hit.Value.Distance, 49f, 51f);
    }

    [Fact]
    public void ResolveRifleHit_ReturnsEnemyPlayerWhenPathIsClear()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        const float y = 220f;
        const float originX = 100f;

        world.TeleportLocalPlayer(originX, y);
        world.EnemyPlayer.TeleportTo(originX + 70f, y);

        var hit = world.CombatTestResolveRifleHit(world.LocalPlayer, 1f, 0f, 400f);

        Assert.Same(world.EnemyPlayer, hit.HitPlayer);
        Assert.Null(hit.HitSentry);
        Assert.InRange(hit.Distance, 50f, 90f);
    }

    [Fact]
    public void ResolveRifleHit_ReturnsEnemyGeneratorWhenPathIsClear()
    {
        var world = CreateWorld();
        var generator = new RoomObjectMarker(RoomObjectType.Generator, 150f, 200f, 40f, 40f, "GeneratorBlueS", PlayerTeam.Blue, "GeneratorBlue");
        world.CombatTestSetLevel(CreateLevel(mode: GameModeKind.Generator, roomObjects: [generator]));
        world.TeleportLocalPlayer(100f, 220f);

        var hit = world.CombatTestResolveRifleHit(world.LocalPlayer, 1f, 0f, 400f);

        Assert.Null(hit.HitPlayer);
        Assert.Null(hit.HitSentry);
        Assert.Equal(PlayerTeam.Blue, hit.HitGenerator!.Team);
        Assert.InRange(hit.Distance, 49f, 51f);
    }

    [Fact]
    public void GetNearestMineHit_MarksGateCollisionAsDestroyOnHit()
    {
        var world = CreateWorld();
        var gate = new RoomObjectMarker(RoomObjectType.TeamGate, 300f, 200f, 20f, 80f, "gate", PlayerTeam.Red, "test-gate");
        world.CombatTestSetLevel(CreateLevel(roomObjects: [gate]));
        var y = Midpoint(gate.Top, gate.Bottom);
        var mine = CreateMine(world.LocalPlayer, gate.Left - 10f, y);

        var hit = world.CombatTestGetNearestMineHit(mine, 1f, 0f, gate.Width + 30f);

        Assert.NotNull(hit);
        Assert.True(hit!.Value.DestroyOnHit);
        Assert.InRange(hit.Value.Distance, 9.9f, 10.1f);
    }

    [Fact]
    public void HasSentryLineOfSight_IsBlockedByBulletWall()
    {
        var world = CreateWorld();
        var bulletWall = new RoomObjectMarker(RoomObjectType.BulletWall, 180f, 180f, 20f, 80f, "wall", null, "bullet-wall");
        world.CombatTestSetLevel(CreateLevel(roomObjects: [bulletWall]));
        var sentry = CreateSentry(PlayerTeam.Red, 120f, 220f, facingDirectionX: 1f);
        world.EnemyPlayer.TeleportTo(260f, 220f);

        Assert.False(world.CombatTestHasSentryLineOfSight(sentry, world.EnemyPlayer));
        Assert.True(world.CombatTestHasSentryLineOfSight(sentry, CreatePlayer(PlayerTeam.Blue, 160f, 220f)));
    }

    [Fact]
    public void GetNearestRocketHit_ReturnsCloserEnemySentryBeforeFartherPlayer()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        var sentry = CreateSentry(PlayerTeam.Blue, 170f, 220f, facingDirectionX: -1f);
        world.CombatTestAddSentry(sentry);
        world.EnemyPlayer.TeleportTo(240f, 220f);

        var rocket = CreateRocket(world.LocalPlayer, 100f, 220f);
        var hit = world.CombatTestGetNearestRocketHit(rocket, 1f, 0f, 200f);

        Assert.NotNull(hit);
        Assert.Null(hit!.Value.HitPlayer);
        Assert.Same(sentry, hit.Value.HitSentry);
        Assert.InRange(hit.Value.Distance, 55f, 60f);
    }

    [Fact]
    public void GetNearestRocketHit_ReturnsEnemyPlayerWhenPathIsClear()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        world.EnemyPlayer.TeleportTo(180f, 220f);

        var rocket = CreateRocket(world.LocalPlayer, 100f, 220f);
        var hit = world.CombatTestGetNearestRocketHit(rocket, 1f, 0f, 200f);

        Assert.NotNull(hit);
        Assert.Same(world.EnemyPlayer, hit!.Value.HitPlayer);
        Assert.Null(hit.Value.HitSentry);
        Assert.InRange(hit.Value.Distance, 65f, 75f);
    }

    [Fact]
    public void GetNearestFlameHit_StopsAtHealingCabinetBeforeEnemyPlayer()
    {
        var world = CreateWorld();
        var cabinet = new RoomObjectMarker(RoomObjectType.HealingCabinet, 150f, 190f, 20f, 60f, "cabinet", null, "healing-cabinet");
        world.CombatTestSetLevel(CreateLevel(roomObjects: [cabinet]));
        world.EnemyPlayer.TeleportTo(230f, 220f);

        var flame = CreateFlame(world.LocalPlayer, 100f, 220f);
        var hit = world.CombatTestGetNearestFlameHit(flame, 1f, 0f, 200f);

        Assert.NotNull(hit);
        Assert.Null(hit!.Value.HitPlayer);
        Assert.Null(hit.Value.HitSentry);
        Assert.InRange(hit.Value.Distance, 49f, 51f);
    }

    [Fact]
    public void GetNearestFlameHit_ReturnsEnemySentryWhenPathIsClear()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        var sentry = CreateSentry(PlayerTeam.Blue, 165f, 220f, facingDirectionX: -1f);
        world.CombatTestAddSentry(sentry);

        var flame = CreateFlame(world.LocalPlayer, 100f, 220f);
        var hit = world.CombatTestGetNearestFlameHit(flame, 1f, 0f, 200f);

        Assert.NotNull(hit);
        Assert.Null(hit!.Value.HitPlayer);
        Assert.Same(sentry, hit.Value.HitSentry);
        Assert.InRange(hit.Value.Distance, 50f, 55f);
    }

    [Fact]
    public void GetNearestMineHit_ReturnsEnemySentryWithoutDestroyOnHit()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        var sentry = CreateSentry(PlayerTeam.Blue, 165f, 220f, facingDirectionX: -1f);
        world.CombatTestAddSentry(sentry);

        var mine = CreateMine(world.LocalPlayer, 100f, 220f);
        var hit = world.CombatTestGetNearestMineHit(mine, 1f, 0f, 200f);

        Assert.NotNull(hit);
        Assert.False(hit!.Value.DestroyOnHit);
        Assert.InRange(hit.Value.Distance, 50f, 55f);
    }

    [Fact]
    public void GetNearestStabHit_ReturnsEnemyPlayerWhenPathIsClear()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        world.TeleportLocalPlayer(100f, 220f);
        world.EnemyPlayer.TeleportTo(125f, 220f);

        var mask = CreateStabMask(world.LocalPlayer, 0f);
        var hit = world.CombatTestGetNearestStabHit(mask, 1f, 0f);

        Assert.NotNull(hit);
        Assert.Same(world.EnemyPlayer, hit!.Value.HitPlayer);
        Assert.Null(hit.Value.HitSentry);
        Assert.InRange(hit.Value.Distance, 0.1f, StabMaskEntity.ReachLength);
    }

    [Fact]
    public void GetNearestStabHit_StopsAtGateBeforeEnemyPlayer()
    {
        var world = CreateWorld();
        var gate = new RoomObjectMarker(RoomObjectType.TeamGate, 114f, 190f, 12f, 60f, "gate", PlayerTeam.Red, "stab-gate");
        world.CombatTestSetLevel(CreateLevel(roomObjects: [gate]));
        world.TeleportLocalPlayer(100f, 220f);
        world.EnemyPlayer.TeleportTo(145f, 220f);

        var mask = CreateStabMask(world.LocalPlayer, 0f);
        var hit = world.CombatTestGetNearestStabHit(mask, 1f, 0f);

        Assert.NotNull(hit);
        Assert.Null(hit!.Value.HitPlayer);
        Assert.Null(hit.Value.HitSentry);
        Assert.InRange(hit.Value.Distance, 1f, 20f);
    }

    [Fact]
    public void GetNearestStabHit_ReturnsCloserEnemySentryBeforeFartherPlayer()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        world.TeleportLocalPlayer(100f, 220f);
        var sentry = CreateSentry(PlayerTeam.Blue, 118f, 220f, facingDirectionX: -1f);
        world.CombatTestAddSentry(sentry);
        world.EnemyPlayer.TeleportTo(160f, 220f);

        var mask = CreateStabMask(world.LocalPlayer, 0f);
        var hit = world.CombatTestGetNearestStabHit(mask, 1f, 0f);

        Assert.NotNull(hit);
        Assert.Null(hit!.Value.HitPlayer);
        Assert.Same(sentry, hit.Value.HitSentry);
        Assert.InRange(hit.Value.Distance, 0.1f, StabMaskEntity.ReachLength);
    }

    [Fact]
    public void ResolveRifleHit_StopsAtBlockingGateBeforeEnemyPlayer()
    {
        var world = CreateWorld();
        var gate = new RoomObjectMarker(RoomObjectType.TeamGate, 140f, 190f, 12f, 60f, "gate", PlayerTeam.Red, "rifle-gate");
        world.CombatTestSetLevel(CreateLevel(roomObjects: [gate]));
        world.TeleportLocalPlayer(100f, 220f);
        world.EnemyPlayer.TeleportTo(220f, 220f);

        var hit = world.CombatTestResolveRifleHit(world.LocalPlayer, 1f, 0f, 400f);

        Assert.Null(hit.HitPlayer);
        Assert.Null(hit.HitSentry);
        Assert.InRange(hit.Distance, 39f, 41f);
    }

    [Fact]
    public void ResolveRifleHit_ReturnsCloserEnemySentryBeforeFartherPlayer()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        world.TeleportLocalPlayer(100f, 220f);
        var sentry = CreateSentry(PlayerTeam.Blue, 160f, 220f, facingDirectionX: -1f);
        world.CombatTestAddSentry(sentry);
        world.EnemyPlayer.TeleportTo(240f, 220f);

        var hit = world.CombatTestResolveRifleHit(world.LocalPlayer, 1f, 0f, 400f);

        Assert.Null(hit.HitPlayer);
        Assert.Same(sentry, hit.HitSentry);
        Assert.InRange(hit.Distance, 44f, 46f);
    }

    private static SimulationWorld CreateWorld()
    {
        return new SimulationWorld(new SimulationConfig
        {
            TicksPerSecond = SimulationConfig.DefaultTicksPerSecond,
        });
    }

    private static SimpleLevel CreateLevel(
        GameModeKind mode = GameModeKind.CaptureTheFlag,
        IReadOnlyList<RoomObjectMarker>? roomObjects = null,
        IReadOnlyList<LevelSolid>? solids = null)
    {
        var spawn = new SpawnPoint(100f, 220f);
        return new SimpleLevel(
            name: "CombatTest",
            mode: mode,
            bounds: new WorldBounds(1000f, 600f),
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: spawn,
            redSpawns: [spawn],
            blueSpawns: [new SpawnPoint(900f, 220f)],
            intelBases: Array.Empty<IntelBaseMarker>(),
            roomObjects: roomObjects ?? Array.Empty<RoomObjectMarker>(),
            floorY: 500f,
            solids: solids ?? Array.Empty<LevelSolid>(),
            importedFromSource: false);
    }

    private static float Midpoint(float start, float end)
    {
        return start + ((end - start) / 2f);
    }

    private static ShotProjectileEntity CreateShot(PlayerEntity owner, float x, float y)
    {
        var shot = new ShotProjectileEntity(9001, owner.Team, owner.Id, x, y, 0f, 0f);
        shot.ApplyNetworkState(x, y, 0f, 0f, ShotProjectileEntity.LifetimeTicks);
        return shot;
    }

    private static FlameProjectileEntity CreateFlame(PlayerEntity owner, float x, float y)
    {
        var flame = new FlameProjectileEntity(9003, owner.Team, owner.Id, x, y, 0f, 0f);
        flame.ApplyNetworkState(x, y, x, y, 0f, 0f, FlameProjectileEntity.AirLifetimeTicks, attachedPlayerId: null, attachedOffsetX: 0f, attachedOffsetY: 0f);
        return flame;
    }

    private static MineProjectileEntity CreateMine(PlayerEntity owner, float x, float y)
    {
        var mine = new MineProjectileEntity(9002, owner.Team, owner.Id, x, y, 0f, 0f);
        mine.ApplyNetworkState(x, y, 0f, 0f, isStickied: false, isDestroyed: false, explosionDamage: MineProjectileEntity.BaseExplosionDamage);
        return mine;
    }

    private static RocketProjectileEntity CreateRocket(PlayerEntity owner, float x, float y)
    {
        var rocket = new RocketProjectileEntity(9004, owner.Team, owner.Id, x, y, 0f, 0f);
        rocket.ApplyNetworkState(x, y, x, y, 0f, 0f, RocketProjectileEntity.LifetimeTicks);
        return rocket;
    }

    private static StabMaskEntity CreateStabMask(PlayerEntity owner, float directionDegrees)
    {
        return new StabMaskEntity(9005, owner.Id, owner.Team, owner.X, owner.Y, directionDegrees);
    }

    private static SentryEntity CreateSentry(PlayerTeam team, float x, float y, float facingDirectionX)
    {
        return new SentryEntity(9100, ownerPlayerId: 77, team, x, y, facingDirectionX);
    }

    private static PlayerEntity CreatePlayer(PlayerTeam team, float x, float y)
    {
        var player = new PlayerEntity(9200, CharacterClassCatalog.Scout, "target");
        player.Spawn(team, x, y);
        return player;
    }
}
