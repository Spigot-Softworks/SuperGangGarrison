using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotBrainClassBehaviorTests
{
    [Fact]
    public void PyroReflectsAccurateIncomingProjectileInAirblastWindow()
    {
        var world = CreateClassWorld(PlayerClass.Pyro, out var pyro);
        var projectileId = FindProjectileIdForPyroReflect(pyro, accurate: true);
        AddIncomingRocket(world, projectileId, pyro, timeToImpactTicks: 6f);

        var decision = CombatDecisionResolver.Resolve(world, pyro, null, null, new CombatDecisionMemory());

        Assert.True(decision.FireSecondary);
        Assert.False(decision.UseAbility);
    }

    [Fact]
    public void PyroMistimesFailedReflectBeforeCorrectAirblastWindow()
    {
        var world = CreateClassWorld(PlayerClass.Pyro, out var pyro);
        var projectileId = FindProjectileIdForPyroReflect(pyro, accurate: false);
        AddIncomingRocket(world, projectileId, pyro, timeToImpactTicks: 12f);

        var decision = CombatDecisionResolver.Resolve(world, pyro, null, null, new CombatDecisionMemory());

        Assert.True(decision.FireSecondary);
        Assert.False(decision.UseAbility);
    }

    [Fact]
    public void SniperCtfGoalHoldsSightlineWhenAllyCanSeekObjective()
    {
        var world = CreateClassWorld(PlayerClass.Sniper, out var sniper);
        var ally = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Red, sniper.X + 40f, sniper.Y);
        ally.PickUpIntel();
        var enemy = AddNetworkPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Blue, sniper.X + 620f, sniper.Y);

        var goal = ObjectiveEvaluator.EvaluateGoal(sniper, world, PlayerTeam.Red, enemy);

        Assert.Equal(sniper.X, goal.X);
        Assert.Equal(sniper.Y, goal.Y);
    }

    [Fact]
    public void SniperCtfGoalTakesObjectiveWhenNoAllyCanSeekObjective()
    {
        var world = CreateClassWorld(PlayerClass.Sniper, out var sniper);

        var goal = ObjectiveEvaluator.EvaluateGoal(sniper, world, PlayerTeam.Red, combatTarget: null);

        Assert.Equal(400f, goal.X);
        Assert.Equal(100f, goal.Y);
    }

    [Fact]
    public void SniperCtfGoalRetrievesNearbyDroppedIntel()
    {
        var world = CreateClassWorld(PlayerClass.Sniper, out var sniper);
        _ = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Red, sniper.X + 40f, sniper.Y);
        world.BlueIntel.Drop(sniper.X + 160f, sniper.Y, returnTicks: 600);

        var goal = ObjectiveEvaluator.EvaluateGoal(sniper, world, PlayerTeam.Red, combatTarget: null);

        Assert.Equal(sniper.X + 160f, goal.X);
        Assert.Equal(sniper.Y, goal.Y);
    }

    [Fact]
    public void DoubleKothGoalDefendsOwnPointWhenEnemyIsCappingIt()
    {
        var world = CreateDoubleKothWorld(PlayerTeam.Red, out var player);
        var ownPoint = Assert.Single(world.ControlPoints, point => point.Marker.IsRedKothControlPoint());
        ownPoint.CappingTeam = PlayerTeam.Blue;

        var goal = ObjectiveEvaluator.EvaluateGoal(player, world, PlayerTeam.Red, combatTarget: null);

        Assert.Equal(ownPoint.HealingAuraCenterX, goal.X);
        Assert.Equal(ownPoint.HealingAuraCenterY, goal.Y);
    }

    [Fact]
    public void DoubleKothGoalAttacksEnemyPointWhenOwnPointIsSecure()
    {
        var world = CreateDoubleKothWorld(PlayerTeam.Red, out var player);
        var enemyPoint = Assert.Single(world.ControlPoints, point => point.Marker.IsBlueKothControlPoint());

        var goal = ObjectiveEvaluator.EvaluateGoal(player, world, PlayerTeam.Red, combatTarget: null);

        Assert.Equal(enemyPoint.HealingAuraCenterX, goal.X);
        Assert.Equal(enemyPoint.HealingAuraCenterY, goal.Y);
    }

    [Fact]
    public void DirectDriveDoesNotJumpInPlaceIntoCeilingForVerticalTarget()
    {
        var world = CreateDirectDriveWorld(hasBlockedHeadroom: true, out var player);

        var resolved = PrimitiveDirectDrive.TryResolveRecovery(
            world,
            player,
            new DirectDriveTarget(DirectDriveTargetKind.Carrier, player.X, player.Y - 160f, "carrier"),
            default,
            out _,
            out _);

        Assert.False(resolved);
    }

    [Fact]
    public void DirectDriveStillMovesTowardHorizontalTargetWhenHeadroomBlocked()
    {
        var world = CreateDirectDriveWorld(hasBlockedHeadroom: true, out var player);

        var resolved = PrimitiveDirectDrive.TryResolveRecovery(
            world,
            player,
            new DirectDriveTarget(DirectDriveTargetKind.Carrier, player.X + 180f, player.Y - 160f, "carrier"),
            default,
            out var steering,
            out _);

        Assert.True(resolved);
        Assert.Equal(1, steering.MoveDirection);
    }

    private static SimulationWorld CreateClassWorld(PlayerClass playerClass, out PlayerEntity player)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(playerClass));
        SetCombatLevel(world);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        player = world.LocalPlayer;
        player.TeleportTo(100f, 100f);
        return world;
    }

    private static SimulationWorld CreateDoubleKothWorld(PlayerTeam team, out PlayerEntity player)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Heavy));
        SetDoubleKothLevel(world);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, team));
        world.ForceRespawnLocalPlayer();
        player = world.LocalPlayer;
        player.TeleportTo(120f, 100f);
        return world;
    }

    private static SimulationWorld CreateDirectDriveWorld(bool hasBlockedHeadroom, out PlayerEntity player)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Pyro));
        SetDirectDriveLevel(world, hasBlockedHeadroom);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        player = world.LocalPlayer;
        player.TeleportTo(200f, 120f);
        return world;
    }

    private static int FindProjectileIdForPyroReflect(PlayerEntity pyro, bool accurate)
    {
        var method = typeof(CombatDecisionResolver).GetMethod("ShouldPyroReflectAccurately", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        for (var id = 1; id < 512; id += 1)
        {
            var result = (bool)method!.Invoke(null, [pyro, id])!;
            if (result == accurate)
            {
                return id;
            }
        }

        throw new InvalidOperationException("Could not find deterministic Pyro reflect projectile id.");
    }

    private static void AddIncomingRocket(SimulationWorld world, int projectileId, PlayerEntity target, float timeToImpactTicks)
    {
        const float speed = 12f;
        var rocket = new RocketProjectileEntity(
            projectileId,
            PlayerTeam.Blue,
            ownerId: 900 + projectileId,
            target.X + speed * timeToImpactTicks,
            target.Y,
            speed,
            directionRadians: MathF.PI);
        var field = typeof(SimulationWorld).GetField("_rockets", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var rockets = Assert.IsType<List<RocketProjectileEntity>>(field!.GetValue(world));
        rockets.Add(rocket);
    }

    private static void SetCombatLevel(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_class_behavior_test",
                    mode: GameModeKind.CaptureTheFlag,
                    bounds: new WorldBounds(2048f, 2048f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(100f, 100f),
                    redSpawns: [new SpawnPoint(100f, 100f)],
                    blueSpawns: [new SpawnPoint(400f, 100f)],
                    intelBases:
                    [
                        new IntelBaseMarker(PlayerTeam.Red, 100f, 100f),
                        new IntelBaseMarker(PlayerTeam.Blue, 400f, 100f),
                    ],
                    roomObjects: [],
                    floorY: 2048f,
                    solids: [],
                    importedFromSource: false),
            ]);
    }

    private static void SetDoubleKothLevel(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_double_koth_behavior_test",
                    mode: GameModeKind.DoubleKingOfTheHill,
                    bounds: new WorldBounds(2048f, 2048f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(120f, 100f),
                    redSpawns: [new SpawnPoint(120f, 100f)],
                    blueSpawns: [new SpawnPoint(520f, 100f)],
                    intelBases: [],
                    roomObjects:
                    [
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            80f,
                            90f,
                            40f,
                            20f,
                            "",
                            SourceName: "KothRedControlPoint"),
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            480f,
                            90f,
                            40f,
                            20f,
                            "",
                            SourceName: "KothBlueControlPoint"),
                    ],
                    floorY: 2048f,
                    solids: [],
                importedFromSource: false),
            ]);
    }

    private static void SetDirectDriveLevel(SimulationWorld world, bool hasBlockedHeadroom)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_direct_drive_test",
                    mode: GameModeKind.CaptureTheFlag,
                    bounds: new WorldBounds(2048f, 2048f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(200f, 120f),
                    redSpawns: [new SpawnPoint(200f, 120f)],
                    blueSpawns: [new SpawnPoint(600f, 120f)],
                    intelBases:
                    [
                        new IntelBaseMarker(PlayerTeam.Red, 200f, 120f),
                        new IntelBaseMarker(PlayerTeam.Blue, 600f, 120f),
                    ],
                    roomObjects: [],
                    floorY: 2048f,
                    solids: hasBlockedHeadroom ? [new LevelSolid(120f, 20f, 180f, 80f)] : [],
                    importedFromSource: false),
            ]);
    }

    private static PlayerEntity AddNetworkPlayer(
        SimulationWorld world,
        byte slot,
        PlayerClass playerClass,
        PlayerTeam team,
        float x,
        float y)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        player.TeleportTo(x, y);
        return player;
    }
}
