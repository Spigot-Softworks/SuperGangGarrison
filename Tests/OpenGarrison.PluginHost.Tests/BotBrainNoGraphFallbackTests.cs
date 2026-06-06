using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotBrainNoGraphFallbackTests
{
    [Fact]
    public void BotDirectSeeksObjectiveOnStockArenaMapWhenNavigationGraphIsMissing()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        Assert.True(world.TryLoadLevel("Lumberyard"));
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        var controller = new BotBrainController();

        var input = controller.Think(world.LocalPlayer, world, PlayerTeam.Red);

        Assert.False(controller.HasNavigationGraph);
        Assert.True(IsActiveMovement(input), controller.LastDirectDriveTrace);
        Assert.Contains("noGraph", controller.LastDirectDriveTrace, StringComparison.Ordinal);
        Assert.Contains("noGraphObjective", controller.LastDirectDriveTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void PracticeBotsEnterCombatOrCaptureOnLumberyardWhenNavigationGraphIsMissing()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        Assert.True(world.TryLoadLevel("Lumberyard"));
        world.PrepareLocalPlayerJoin();
        const byte redBotSlot = 2;
        const byte blueBotSlot = 3;
        Assert.True(world.TryPrepareNetworkPlayerJoin(redBotSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(redBotSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(redBotSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(redBotSlot, out var redBot));
        Assert.True(world.TryPrepareNetworkPlayerJoin(blueBotSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(blueBotSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(blueBotSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(blueBotSlot, out var blueBot));
        var redStartX = redBot.X;
        var blueStartX = blueBot.X;
        var controller = new BotBrainPracticeBotController();
        var slots = new Dictionary<byte, ControlledBotSlot>
        {
            [redBotSlot] = new(redBotSlot, PlayerTeam.Red, PlayerClass.Scout),
            [blueBotSlot] = new(blueBotSlot, PlayerTeam.Blue, PlayerClass.Scout),
        };
        var activeTicks = 0;
        var combatTick = -1;
        var captureTick = -1;

        for (var tick = 0; tick < 720; tick += 1)
        {
            var inputs = controller.BuildInputs(world, slots);
            var redInput = inputs.GetValueOrDefault(redBotSlot);
            var blueInput = inputs.GetValueOrDefault(blueBotSlot);
            if (IsActiveMovement(redInput) || IsActiveMovement(blueInput))
            {
                activeTicks += 1;
            }

            if (combatTick < 0
                && (redInput.FirePrimary
                    || redInput.FireSecondary
                    || blueInput.FirePrimary
                    || blueInput.FireSecondary
                    || HasCombatTarget(controller, redBotSlot)
                    || HasCombatTarget(controller, blueBotSlot)))
            {
                combatTick = tick;
            }

            if (captureTick < 0
                && (IsInArenaCaptureZone(world, redBot) || IsInArenaCaptureZone(world, blueBot)))
            {
                captureTick = tick;
            }

            Assert.True(world.TrySetNetworkPlayerInput(redBotSlot, redInput));
            Assert.True(world.TrySetNetworkPlayerInput(blueBotSlot, blueInput));
            world.AdvanceOneTick();

            if (combatTick >= 0 || captureTick >= 0)
            {
                break;
            }
        }

        Assert.True(controller.TryGetBotBrainController(redBotSlot, out var redBrain));
        Assert.True(controller.TryGetBotBrainController(blueBotSlot, out var blueBrain));
        Assert.NotNull(redBrain);
        Assert.NotNull(blueBrain);
        Assert.False(redBrain!.HasNavigationGraph);
        Assert.False(blueBrain!.HasNavigationGraph);
        Assert.True(activeTicks > 180, $"redTrace:{redBrain.LastDirectDriveTrace} blueTrace:{blueBrain.LastDirectDriveTrace}");
        Assert.True(
            MathF.Abs(redBot.X - redStartX) > 256f || MathF.Abs(blueBot.X - blueStartX) > 256f,
            $"redStart:{redStartX:0.0} redEnd:{redBot.X:0.0} blueStart:{blueStartX:0.0} blueEnd:{blueBot.X:0.0} redTrace:{redBrain.LastDirectDriveTrace} blueTrace:{blueBrain.LastDirectDriveTrace}");
        Assert.True(
            combatTick >= 0 || captureTick >= 0,
            $"combatTick:{combatTick} captureTick:{captureTick} red:({redBot.X:0.0},{redBot.Y:0.0}) blue:({blueBot.X:0.0},{blueBot.Y:0.0}) redTrace:{redBrain.LastDirectDriveTrace} blueTrace:{blueBrain.LastDirectDriveTrace}");
    }

    [Fact]
    public void BotDirectSeeksObjectiveWhenNavigationGraphIsMissing()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        SetNoGraphCaptureTheFlagLevel(world);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.TeleportTo(100f, 100f);
        var controller = new BotBrainController();

        var input = controller.Think(world.LocalPlayer, world, PlayerTeam.Red);

        Assert.False(controller.HasNavigationGraph);
        Assert.True(input.Right);
        Assert.False(input.Left);
        Assert.Contains("noGraph", controller.LastDirectDriveTrace, StringComparison.Ordinal);
        Assert.Contains("noGraphObjective", controller.LastDirectDriveTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void BotTreatsEmptyNavigationGraphAsGraphlessForObjectiveSeeking()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        SetNoGraphCaptureTheFlagLevel(world);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.TeleportTo(100f, 100f);
        var controller = new BotBrainController(
            new NavGraph([], [], levelName: "empty", mode: GameModeKind.CaptureTheFlag));

        var input = controller.Think(world.LocalPlayer, world, PlayerTeam.Red);

        Assert.False(controller.HasNavigationGraph);
        Assert.True(input.Right);
        Assert.False(input.Left);
        Assert.Contains("noGraph", controller.LastDirectDriveTrace, StringComparison.Ordinal);
        Assert.Contains("noGraphObjective", controller.LastDirectDriveTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void BotJumpsOverObstacleNearControlPointWhenNavigationGraphIsMissing()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.TeleportTo(100f, 120f);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        SetNoGraphControlPointObstacleLevel(world, world.LocalPlayer);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var controller = new BotBrainController();

        var input = controller.Think(world.LocalPlayer, world, PlayerTeam.Red);

        Assert.False(controller.HasNavigationGraph);
        Assert.True(input.Right, controller.LastDirectDriveTrace);
        Assert.True(input.Up, controller.LastDirectDriveTrace);
        Assert.Contains("noGraph", controller.LastDirectDriveTrace, StringComparison.Ordinal);
        Assert.Contains("capturePoint", controller.LastDirectDriveTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void BotJumpsOverObstacleWhenControlPointTargetIsInsideDirectSeekDeadZone()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.TeleportTo(100f, 120f);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        SetNoGraphControlPointDeadZoneObstacleLevel(world, world.LocalPlayer);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var controller = new BotBrainController();

        var input = controller.Think(world.LocalPlayer, world, PlayerTeam.Red);

        Assert.False(controller.HasNavigationGraph);
        Assert.True(input.Right, controller.LastDirectDriveTrace);
        Assert.True(input.Up, controller.LastDirectDriveTrace);
        Assert.Contains("noGraph", controller.LastDirectDriveTrace, StringComparison.Ordinal);
        Assert.Contains("capturePoint", controller.LastDirectDriveTrace, StringComparison.Ordinal);
        Assert.Contains("capturePointObstacleJump", controller.LastDirectDriveTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void BotUsesLocalMotionRecoveryWhenGraphlessControlPointEnemyClearMoveIsBlocked()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.TeleportTo(100f, 120f);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        const byte enemySlot = 2;
        Assert.True(world.TryPrepareNetworkPlayerJoin(enemySlot));
        Assert.True(world.TrySetNetworkPlayerTeam(enemySlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(enemySlot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(enemySlot, out var enemy));
        enemy.TeleportTo(world.LocalPlayer.X + 250f, world.LocalPlayer.Y);
        enemy.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: -1f);
        SetNoGraphControlPointEnemyClearBlockedLevel(world, world.LocalPlayer, enemy);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        enemy.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: -1f);
        var controller = new BotBrainController();

        var input = controller.Think(world.LocalPlayer, world, PlayerTeam.Red);

        Assert.False(controller.HasNavigationGraph);
        Assert.True(input.Right, controller.LastDirectDriveTrace);
        Assert.True(input.Up, controller.LastDirectDriveTrace);
        Assert.Contains("controlPointClearEnemy", controller.LastDirectDriveTrace, StringComparison.Ordinal);
        Assert.Contains("blockedRecovery", controller.LastDirectDriveTrace, StringComparison.Ordinal);
        Assert.Contains("localMotion=", controller.LastDirectDriveTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void BotBacksAwayWhenGraphlessObjectiveBlockedByUnjumpableObstacle()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.TeleportTo(100f, 120f);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        SetNoGraphUnjumpableObstacleLevel(world, world.LocalPlayer);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var controller = new BotBrainController();

        var input = controller.Think(world.LocalPlayer, world, PlayerTeam.Red);

        Assert.False(controller.HasNavigationGraph);
        Assert.True(input.Left, controller.LastDirectDriveTrace);
        Assert.False(input.Right, controller.LastDirectDriveTrace);
        Assert.False(input.Up, controller.LastDirectDriveTrace);
        Assert.Contains("noGraph", controller.LastDirectDriveTrace, StringComparison.Ordinal);
        Assert.Contains("localMotion=blockedEscape", controller.LastDirectDriveTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalMotionBacksAwayWhenBlockedPrimitiveCannotProbeThroughObstacle()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.TeleportTo(100f, 120f);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        SetNoGraphUnjumpableObstacleLevel(world, world.LocalPlayer);
        world.LocalPlayer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var controller = new LocalMotionController();
        var target = new DirectDriveTarget(DirectDriveTargetKind.Objective, world.LocalPlayer.X + 240f, world.LocalPlayer.Y, "blockedObjective");
        var escaped = false;
        var steering = new SteeringOutput();
        var trace = string.Empty;

        for (var tick = 1; tick <= 90; tick += 1)
        {
            _ = controller.TryResolveRecovery(world, world.LocalPlayer, target, new SteeringOutput(), tick, out steering, out trace);
            if (trace.Contains("blockedEscape", StringComparison.Ordinal))
            {
                escaped = true;
                break;
            }
        }

        Assert.True(escaped, trace);
        Assert.True(steering.MoveDirection < 0f, trace);
        Assert.False(steering.Jump, trace);
    }

    private static bool IsActiveMovement(PlayerInputSnapshot input) =>
        input.Left || input.Right || input.Up || input.Down;

    private static bool HasCombatTarget(BotBrainPracticeBotController controller, byte slot) =>
        controller.TryGetBotBrainController(slot, out var brain)
        && brain?.LastCombatTarget is not null;

    private static bool IsInArenaCaptureZone(SimulationWorld world, PlayerEntity player)
    {
        var captureZones = world.Level.GetRoomObjects(RoomObjectType.CaptureZone);
        for (var index = 0; index < captureZones.Count; index += 1)
        {
            var zone = captureZones[index];
            if (player.IntersectsMarker(zone.CenterX, zone.CenterY, zone.Width, zone.Height))
            {
                return true;
            }
        }

        return false;
    }

    private static void SetNoGraphCaptureTheFlagLevel(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_no_graph_direct_seek_test",
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

    private static void SetNoGraphControlPointObstacleLevel(SimulationWorld world, PlayerEntity player)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var obstacleLeft = player.Right + 2f;
        var obstacleTop = player.Bottom - 24f;
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_no_graph_control_point_obstacle_test",
                    mode: GameModeKind.ControlPoint,
                    bounds: new WorldBounds(512f, 512f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(player.X, player.Y),
                    redSpawns: [new SpawnPoint(player.X, player.Y)],
                    blueSpawns: [new SpawnPoint(player.X + 240f, player.Y)],
                    intelBases: [],
                    roomObjects:
                    [
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            player.X + 40f,
                            player.Y,
                            20f,
                            20f,
                            "",
                            SourceName: "ControlPoint1"),
                    ],
                    floorY: 512f,
                    solids:
                    [
                        new LevelSolid(obstacleLeft, obstacleTop, 12f, 24f),
                    ],
                    importedFromSource: false),
            ]);
    }

    private static void SetNoGraphControlPointDeadZoneObstacleLevel(SimulationWorld world, PlayerEntity player)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var captureZoneLeft = player.X + 16f;
        var obstacleLeft = player.Right + 8f;
        var obstacleTop = player.Bottom - 24f;
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_no_graph_control_point_dead_zone_obstacle_test",
                    mode: GameModeKind.ControlPoint,
                    bounds: new WorldBounds(512f, 512f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(player.X, player.Y),
                    redSpawns: [new SpawnPoint(player.X, player.Y)],
                    blueSpawns: [new SpawnPoint(player.X + 240f, player.Y)],
                    intelBases: [],
                    roomObjects:
                    [
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            player.X + 8f,
                            player.Y,
                            4f,
                            20f,
                            "",
                            SourceName: "ControlPoint1"),
                        new RoomObjectMarker(
                            RoomObjectType.CaptureZone,
                            captureZoneLeft,
                            player.Y,
                            2f,
                            20f,
                            "",
                            SourceName: "CaptureZone"),
                    ],
                    floorY: 512f,
                    solids:
                    [
                        new LevelSolid(obstacleLeft, obstacleTop, 12f, 24f),
                    ],
                    importedFromSource: false),
            ]);
    }

    private static void SetNoGraphControlPointEnemyClearBlockedLevel(SimulationWorld world, PlayerEntity player, PlayerEntity enemy)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var obstacleLeft = player.Right + 2f;
        var obstacleTop = player.Bottom - 8f;
        var pointX = (player.X + enemy.X) * 0.5f;
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_no_graph_control_point_enemy_clear_blocked_test",
                    mode: GameModeKind.ControlPoint,
                    bounds: new WorldBounds(768f, 512f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(player.X, player.Y),
                    redSpawns: [new SpawnPoint(player.X, player.Y)],
                    blueSpawns: [new SpawnPoint(enemy.X, enemy.Y)],
                    intelBases: [],
                    roomObjects:
                    [
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            pointX,
                            player.Y,
                            20f,
                            20f,
                            "",
                            SourceName: "ControlPoint1"),
                        new RoomObjectMarker(
                            RoomObjectType.CaptureZone,
                            pointX - 40f,
                            player.Y - 20f,
                            80f,
                            40f,
                            "",
                            SourceName: "CaptureZone"),
                    ],
                    floorY: 512f,
                    solids:
                    [
                        new LevelSolid(obstacleLeft, obstacleTop, 48f, 8f),
                    ],
                    importedFromSource: false),
            ]);
    }

    private static void SetNoGraphUnjumpableObstacleLevel(SimulationWorld world, PlayerEntity player)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var obstacleLeft = player.Right + 2f;
        var pointX = player.X + 284f;
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_no_graph_unjumpable_obstacle_test",
                    mode: GameModeKind.ControlPoint,
                    bounds: new WorldBounds(768f, 512f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(player.X, player.Y),
                    redSpawns: [new SpawnPoint(player.X, player.Y)],
                    blueSpawns: [new SpawnPoint(player.X + 320f, player.Y)],
                    intelBases: [],
                    roomObjects:
                    [
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            pointX,
                            player.Y,
                            20f,
                            20f,
                            "",
                            SourceName: "ControlPoint1"),
                        new RoomObjectMarker(
                            RoomObjectType.CaptureZone,
                            pointX - 40f,
                            player.Y - 20f,
                            80f,
                            40f,
                            "",
                            SourceName: "CaptureZone"),
                    ],
                    floorY: 512f,
                    solids:
                    [
                        new LevelSolid(obstacleLeft, 0f, 160f, 512f),
                    ],
                    importedFromSource: false),
            ]);
    }
}
