using OpenGarrison.Core;
using System.Collections;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldRocketSourceParityTests
{
    [Fact]
    public void RocketAdvanceDirectHitsEnemyUsingEndPositionMask()
    {
        var world = CreateCombatWorld();
        var owner = world.LocalPlayer;
        owner.TeleportTo(0f, 0f);

        var enemy = AddEnemy(world, id: 2, x: 20f, y: 0f);
        enemy.GetCollisionBounds(out _, out var enemyTop, out _, out _);
        var healthBefore = enemy.Health;

        _ = SpawnRocket(world, owner, x: 0f, y: enemyTop + 1f, speed: 12f, directionRadians: 0f);
        AdvanceRockets(world);

        Assert.Equal(
            RocketProjectileEntity.DirectHitDamage + (int)RocketProjectileEntity.ExplosionDamage,
            healthBefore - enemy.Health);
        Assert.Equal(0, GetRocketCount(world));
    }

    [Fact]
    public void RocketAdvanceExplodesWhenEndPositionMaskOverlapsSolid()
    {
        var world = CreateCombatWorld(
            [
                new LevelSolid(16f, -32f, 18f, 32f),
            ]);
        var owner = world.LocalPlayer;
        owner.TeleportTo(0f, 0f);

        _ = SpawnRocket(world, owner, x: 0f, y: 0f, speed: 12f, directionRadians: 0f);
        AdvanceRockets(world);

        Assert.Equal(0, GetRocketCount(world));
    }

    [Fact]
    public void RocketAdvanceExplodesWhenThinWallIsCrossedMidStep()
    {
        var world = CreateCombatWorld(
            [
                new LevelSolid(6f, -32f, 7f, 32f),
            ]);
        var owner = world.LocalPlayer;
        owner.TeleportTo(0f, 0f);

        _ = SpawnRocket(world, owner, x: 0f, y: 0f, speed: 12f, directionRadians: 0f);
        AdvanceRockets(world);

        Assert.Equal(0, GetRocketCount(world));
    }

    [Fact]
    public void RocketAdvancePrioritizesEnemyDirectHitOverWallOverlap()
    {
        var world = CreateCombatWorld(
            [
                new LevelSolid(18f, -32f, 22f, 32f),
            ]);
        var owner = world.LocalPlayer;
        owner.TeleportTo(0f, 0f);

        var enemy = AddEnemy(world, id: 2, x: 20f, y: 0f);

        _ = SpawnRocket(world, owner, x: 0f, y: 0f, speed: 12f, directionRadians: 0f);
        AdvanceRockets(world);

        Assert.True(enemy.Health < enemy.MaxHealth, $"expected enemy to take direct hit, got health {enemy.Health}");
        Assert.Equal(0, GetRocketCount(world));
    }

    [Fact]
    public void PlayerHitQueriesUseAnimatedPresentationMask()
    {
        var world = CreateCombatWorld();
        var enemy = AddEnemy(world, id: 2, x: 20f, y: 0f, playerClass: PlayerClass.Soldier);
        enemy.ApplyNetworkState(
            team: PlayerTeam.Blue,
            classDefinition: CharacterClassCatalog.Soldier,
            isAlive: true,
            x: 20f,
            y: 0f,
            horizontalSpeed: 60f,
            verticalSpeed: 0f,
            health: enemy.MaxHealth,
            currentShells: enemy.MaxShells,
            kills: 0,
            deaths: 0,
            caps: 0,
            points: 0f,
            healPoints: 0,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: 100f,
            isGrounded: true,
            remainingAirJumps: 0,
            isCarryingIntel: false,
            intelRechargeTicks: 0f,
            isSpyCloaked: false,
            spyCloakAlpha: 0f,
            isSpySuperjumping: false,
            spySuperjumpHorizontalVelocity: 0f,
            spySuperjumpCooldownTicksRemaining: 0,
            spyBackstabVisualTicksRemaining: 0,
            isUbered: false,
            isKritzCritBoosted: false,
            isHeavyEating: false,
            heavyEatTicksRemaining: 0,
            isSniperScoped: false,
            sniperChargeTicks: 0,
            isUsingBinoculars: false,
            binocularsFocusX: 20f,
            binocularsFocusY: 0f,
            facingDirectionX: 1f,
            aimDirectionDegrees: 0f,
            aimWorldX: 20f,
            aimWorldY: 0f,
            isTaunting: false,
            tauntFrameIndex: 0f,
            isChatBubbleVisible: false,
            chatBubbleFrameIndex: 0,
            chatBubbleAlpha: 0f);

        enemy.GetCollisionBounds(out var collisionLeft, out _, out _, out _);
        Assert.InRange(enemy.HorizontalSpeed, 59.9f, 60.1f);
        var spriteName = GetPlayerPresentationBodySpriteName(world, enemy);
        var manifest = GameMakerAssetManifestImporter.ImportProjectAssets();
        var maskBounds = GetPlayerPresentationHitBounds(world, enemy);

        Assert.Equal("SoldierBlueRunS", spriteName);
        Assert.True(manifest.Sprites.ContainsKey("SoldierBlueRunS"));
        Assert.Equal(20, manifest.Sprites["SoldierBlueRunS"].Mask.Left);
        Assert.InRange(maskBounds.Left, 9.9f, 10.1f);
        Assert.InRange(collisionLeft, 13.9f, 14.1f);
        Assert.True(maskBounds.Left < collisionLeft, "expected animated run mask to extend farther than the fixed collision box");
    }

    private static SimulationWorld CreateCombatWorld(IReadOnlyList<LevelSolid>? solids = null)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Soldier));
        SetCombatLevel(
            world,
            new SimpleLevel(
                name: "rocket_test",
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
                solids: solids ?? [],
                importedFromSource: false));
        world.LocalPlayer.TeleportTo(0f, 0f);
        return world;
    }

    private static PlayerEntity AddEnemy(SimulationWorld world, int id, float x, float y, PlayerClass playerClass = PlayerClass.Scout)
    {
        var networkId = checked((byte)id);
        Assert.True(world.TryPrepareNetworkPlayerJoin(networkId));
        Assert.True(world.TrySetNetworkPlayerTeam(networkId, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(networkId, playerClass));
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

    private static RocketProjectileEntity SpawnRocket(SimulationWorld world, PlayerEntity owner, float x, float y, float speed, float directionRadians)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSpawnRocket", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [owner, x, y, speed, directionRadians]);
        Assert.IsType<RocketProjectileEntity>(result);
        return (RocketProjectileEntity)result!;
    }

    private static void AdvanceRockets(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod("AdvanceRockets", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, []);
    }

    private static int GetRocketCount(SimulationWorld world)
    {
        var field = typeof(SimulationWorld).GetField("_rockets", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var rockets = field!.GetValue(world) as ICollection;
        Assert.NotNull(rockets);
        return rockets!.Count;
    }

    private static (float Left, float Top, float Right, float Bottom) GetPlayerPresentationHitBounds(SimulationWorld world, PlayerEntity player)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestGetPlayerPresentationHitBounds", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return ((float Left, float Top, float Right, float Bottom))method!.Invoke(world, [player])!;
    }

    private static string? GetPlayerPresentationBodySpriteName(SimulationWorld world, PlayerEntity player)
    {
        var method = typeof(SimulationWorld).GetMethod("GetPlayerPresentationBodySpriteName", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string?)method!.Invoke(null, [world, player]);
    }
}
