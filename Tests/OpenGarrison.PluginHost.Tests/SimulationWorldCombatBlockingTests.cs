using System.Collections;
using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldCombatBlockingTests
{
    [Fact]
    public void RifleHitStopsOnFriendlyBeforeEnemy()
    {
        var world = CreateCombatWorld(PlayerClass.Sniper);
        var attacker = world.LocalPlayer;
        attacker.TeleportTo(0f, 0f);
        _ = AddNetworkPlayer(world, slot: 2, PlayerTeam.Red, PlayerClass.Soldier, x: 48f, y: 0f);
        var enemy = AddNetworkPlayer(world, slot: 3, PlayerTeam.Blue, PlayerClass.Soldier, x: 96f, y: 0f);

        var hit = ResolveRifleHit(world, attacker, directionX: 1f, directionY: 0f, maxDistance: 200f);

        Assert.Null(hit.HitPlayer);
        Assert.True(hit.Distance < enemy.X);
    }

    [Fact]
    public void DirectFireShotStopsOnFriendlyBeforeEnemy()
    {
        var world = CreateCombatWorld(PlayerClass.Scout);
        var owner = world.LocalPlayer;
        owner.TeleportTo(-64f, 0f);
        _ = AddNetworkPlayer(world, slot: 2, PlayerTeam.Red, PlayerClass.Soldier, x: 48f, y: 0f);
        var enemy = AddNetworkPlayer(world, slot: 3, PlayerTeam.Blue, PlayerClass.Soldier, x: 96f, y: 0f);
        var shot = new ShotProjectileEntity(10_000, owner.Team, owner.Id, 0f, 0f, 0f, 0f);

        var hit = GetNearestShotHit(world, shot, directionX: 1f, directionY: 0f, maxDistance: 200f);

        Assert.NotNull(hit);
        Assert.Null(hit.Value.HitPlayer);
        Assert.True(hit.Value.Distance < enemy.X);
    }

    [Fact]
    public void MineLauncherOverflowDetonatesOnlyOldestMine()
    {
        var world = CreateCombatWorld(PlayerClass.Demoman);
        var owner = world.LocalPlayer;
        for (var index = 0; index < owner.PrimaryWeapon.MaxAmmo; index += 1)
        {
            _ = SpawnMine(world, owner, x: 64f + index, y: 0f, stickied: true);
        }

        ExplodeOldestMine(world, owner.Id, triggerNearbyMines: false);

        Assert.Equal(owner.PrimaryWeapon.MaxAmmo - 1, GetMineCount(world));
    }

    [Fact]
    public void HoldingFireAtStickyCapDoesNotDetonateWhileWeaponCannotFire()
    {
        var world = CreateCombatWorld(PlayerClass.Demoman);
        var player = world.LocalPlayer;
        Assert.True(player.TryFirePrimaryWeapon());
        for (var index = 0; index < player.PrimaryWeapon.MaxAmmo; index += 1)
        {
            _ = SpawnMine(world, player, x: 64f + index, y: 0f, stickied: true);
        }

        InvokeTryHandleNetworkPrimaryFire(
            world,
            player,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: true,
                FireSecondary: false,
                AimWorldX: 128f,
                AimWorldY: 0f,
                DebugKill: false),
            primaryPressed: true,
            suppressPyroPrimaryThisTick: false);

        Assert.Equal(player.PrimaryWeapon.MaxAmmo, GetMineCount(world));
    }

    [Fact]
    public void MineLauncherDoesNotStartStickyInsideBlockedMuzzleOffset()
    {
        const float solidLeft = 85f;
        var world = new SimulationWorld();
        SetCombatLevel(world, CreateLevel(
            roomObjects: [],
            solids:
            [
                new LevelSolid(solidLeft, 80f, 32f, 32f),
            ]));
        Assert.True(world.TrySetLocalClass(PlayerClass.Demoman));
        world.ForceRespawnLocalPlayer();
        var player = world.LocalPlayer;
        player.TeleportTo(80f, 96f);

        Assert.True(player.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(world, player, 160f, 96f);

        var mine = Assert.Single(world.Mines);
        Assert.True(mine.X < solidLeft);

        world.AdvanceOneTick();

        mine = Assert.Single(world.Mines);
        Assert.True(mine.IsStickied);
        Assert.True(mine.X < solidLeft);
    }

    [Fact]
    public void IntelPickupRejectsPickupThatWouldMakeCurrentTeamGateBlocking()
    {
        var world = new SimulationWorld();
        SetCombatLevel(world, CreateLevel(roomObjects:
        [
            new RoomObjectMarker(RoomObjectType.TeamGate, 104f, 64f, 48f, 96f, "RedGate", PlayerTeam.Red),
        ]));
        Assert.True(world.TrySetLocalClass(PlayerClass.Soldier));
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.TeleportTo(128f, 96f);
        world.BlueIntel.Drop(world.LocalPlayer.X, world.LocalPlayer.Y, returnTicks: 300);

        world.AdvanceOneTick();

        Assert.False(world.LocalPlayer.IsCarryingIntel);
        Assert.True(world.BlueIntel.IsDropped);
    }

    private static SimulationWorld CreateCombatWorld(PlayerClass playerClass)
    {
        var world = new SimulationWorld();
        SetCombatLevel(world, CreateLevel(roomObjects: []));
        if (world.LocalPlayer.ClassId != playerClass)
        {
            Assert.True(world.TrySetLocalClass(playerClass));
        }

        Assert.Equal(playerClass, world.LocalPlayer.ClassId);
        world.ForceRespawnLocalPlayer();
        return world;
    }

    private static SimpleLevel CreateLevel(IReadOnlyList<RoomObjectMarker> roomObjects, IReadOnlyList<LevelSolid>? solids = null)
    {
        return new SimpleLevel(
            name: "combat_blocking_test",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(2048f, 2048f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(128f, 96f),
            redSpawns: [new SpawnPoint(128f, 96f)],
            blueSpawns: [new SpawnPoint(512f, 96f)],
            intelBases:
            [
                new IntelBaseMarker(PlayerTeam.Red, 128f, 96f),
                new IntelBaseMarker(PlayerTeam.Blue, 512f, 96f),
            ],
            roomObjects: roomObjects,
            floorY: 2048f,
            solids: solids ?? [],
            importedFromSource: false);
    }

    private static PlayerEntity AddNetworkPlayer(SimulationWorld world, byte slot, PlayerTeam team, PlayerClass playerClass, float x, float y)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        player.TeleportTo(x, y);
        return player;
    }

    private static void SetCombatLevel(SimulationWorld world, SimpleLevel level)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [level]);
    }

    private static (float Distance, PlayerEntity? HitPlayer) ResolveRifleHit(
        SimulationWorld world,
        PlayerEntity attacker,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestResolveRifleHit", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [attacker, directionX, directionY, maxDistance]);
        Assert.NotNull(result);
        var distance = (float)result!.GetType().GetField("Item1")!.GetValue(result)!;
        var hitPlayer = (PlayerEntity?)result.GetType().GetField("Item2")!.GetValue(result);
        return (distance, hitPlayer);
    }

    private static (float Distance, PlayerEntity? HitPlayer)? GetNearestShotHit(
        SimulationWorld world,
        ShotProjectileEntity shot,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestGetNearestShotHit", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [shot, directionX, directionY, maxDistance]);
        if (result is null)
        {
            return null;
        }

        var distance = (float)result.GetType().GetField("Item1")!.GetValue(result)!;
        var hitPlayer = (PlayerEntity?)result.GetType().GetField("Item4")!.GetValue(result);
        return (distance, hitPlayer);
    }

    private static MineProjectileEntity SpawnMine(SimulationWorld world, PlayerEntity owner, float x, float y, bool stickied)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSpawnMine", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [owner, x, y, 0f, 0f, stickied]);
        return Assert.IsType<MineProjectileEntity>(result);
    }

    private static void ExplodeOldestMine(SimulationWorld world, int ownerId, bool triggerNearbyMines)
    {
        var method = typeof(SimulationWorld).GetMethod("ExplodeOldestMine", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [ownerId, triggerNearbyMines]);
    }

    private static void InvokeTryHandleNetworkPrimaryFire(
        SimulationWorld world,
        PlayerEntity player,
        PlayerInputSnapshot input,
        bool primaryPressed,
        bool suppressPyroPrimaryThisTick)
    {
        var method = typeof(SimulationWorld).GetMethod("TryHandleNetworkPrimaryFire", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [player, input, primaryPressed, suppressPyroPrimaryThisTick]);
    }

    private static void InvokeFirePrimaryWeapon(SimulationWorld world, PlayerEntity player, float aimWorldX, float aimWorldY)
    {
        var method = typeof(SimulationWorld).GetMethod("FirePrimaryWeapon", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [player, aimWorldX, aimWorldY]);
    }

    private static int GetMineCount(SimulationWorld world)
    {
        var field = typeof(SimulationWorld).GetField("_mines", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var mines = Assert.IsAssignableFrom<ICollection>(field!.GetValue(world));
        return mines.Count;
    }
}
