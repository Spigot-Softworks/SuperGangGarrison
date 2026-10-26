using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class BotEnvironmentNavigationTests
{
    [Theory]
    [InlineData(PlayerClass.Scout)]
    [InlineData(PlayerClass.Heavy)]
    public void ProbeExperiencesTheSamePushAsLiveMovement(PlayerClass playerClass)
    {
        var level = CreateLevel([new LevelSolid(0, 200, 700, 30)],
            [new RoomObjectMarker(RoomObjectType.MoveBoxLeft, 100, 130, 120, 70, "", Value: 300)]);
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.TestSetLevel(level);
        world.NetworkPlayers.SetPendingLocalPlayerClass(playerClass);
        world.NetworkPlayers.ForceRespawnLocalPlayer();
        var live = world.LocalPlayer;
        live.TeleportTo(160, 200 - live.CollisionBottomOffset);
        live.RestoreMovementProbeState(true, live.MaxAirJumps, 1);
        var probe = new PlayerEntity(-901_000, live.ClassDefinition, "probe");
        var state = live.CapturePredictionState();
        probe.RestorePredictionState(in state);
        var input = new PlayerInputSnapshot { Right = true };
        for (var tick = 0; tick < 8; tick++)
        {
            BotNavigationEnvironment.AdvanceProbe(level, probe, input, false, live.Team);
            world.Movement.ApplyRoomForces(live);
            live.Advance(input, false, level, live.Team, 1d / SimulationConfig.DefaultTicksPerSecond);
            Assert.Equal(live.X, probe.X);
            Assert.Equal(live.Y, probe.Y);
            Assert.Equal(live.HorizontalSpeed, probe.HorizontalSpeed);
        }
        Assert.True(probe.X < 160, "A bot cannot run right through a full-strength left push.");
    }

    [Theory]
    [InlineData(300)]
    [InlineData(150)]
    public void ConveyorGraphAllowsTheForwardRouteAndRejectsTheReverseRoute(float push)
    {
        var level = CreateLevel([new LevelSolid(0, 200, 700, 30)],
            [new RoomObjectMarker(RoomObjectType.MoveBoxLeft, 250, 130, 170, 70, "", Value: push)]);
        var graph = Og2NavigationGraphBuilder.Build(level);
        var left = graph.FindNearestNode(100, 180);
        var right = graph.FindNearestNode(600, 180);
        Assert.NotNull(graph.FindPath(right, left, PlayerClass.Scout, team: PlayerTeam.Red));
        Assert.Null(graph.FindPath(left, right, PlayerClass.Scout, team: PlayerTeam.Red));
    }

    [Theory]
    [InlineData(RoomObjectType.FragBox)]
    [InlineData(RoomObjectType.KillBox)]
    public void GraphDoesNotUseALethalPitFloorAsASafeLanding(RoomObjectType hazardType)
    {
        var level = CreateLevel([new LevelSolid(0, 200, 140, 30), new LevelSolid(260, 200, 440, 30),
            new LevelSolid(140, 380, 120, 30)],
            [new RoomObjectMarker(hazardType, 140, 330, 120, 50, "")]);
        var graph = Og2NavigationGraphBuilder.Build(level);
        for (var index = 0; index < graph.NodeCount; index++)
        {
            var node = graph.GetNode(index);
            Assert.False(node.X > 140 && node.X < 260 && node.Y > 300,
                $"Lethal pit was treated as a landing surface: {node}");
        }
    }

    [Fact]
    public void LandingForecastRejectsAPitButAllowsAJumpAcrossIt()
    {
        var level = CreateLevel([new LevelSolid(0, 200, 140, 30), new LevelSolid(230, 200, 470, 30),
            new LevelSolid(140, 380, 90, 30)],
            [new RoomObjectMarker(RoomObjectType.FragBox, 140, 330, 90, 50, "")]);
        var player = new PlayerEntity(-901_000, CharacterClassCatalog.Scout, "probe");
        player.Spawn(PlayerTeam.Red, 180, 230);
        player.RestoreMovementProbeState(false, player.MaxAirJumps, 1);
        Assert.False(BotNavigationEnvironment.HasSafeLanding(level, player, PlayerTeam.Red, default));

        player.TeleportTo(160, 110);
        player.AddImpulse(240, -60);
        Assert.True(BotNavigationEnvironment.HasSafeLanding(level, player, PlayerTeam.Red,
            new PlayerInputSnapshot { Right = true }));
    }

    [Theory]
    [InlineData(300)]
    [InlineData(150)]
    public void BotBacksAwayFromTheWrongConveyorEntrance(float push)
    {
        var level = CreateLevel([new LevelSolid(0, 200, 700, 30)],
            [new RoomObjectMarker(RoomObjectType.MoveBoxLeft, 250, 130, 170, 70, "", Value: push)]);
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.TestSetLevel(level);
        var player = world.LocalPlayer;
        player.TeleportTo(235, 200 - player.CollisionBottomOffset);
        player.RestoreMovementProbeState(true, player.MaxAirJumps, 1);
        var guard = new BotEnvironmentSteeringGuard();
        var steering = new SteeringOutput { MoveDirection = 1 };
        Assert.True(guard.Apply(world, player, ref steering));
        Assert.True(guard.StartedEscape);
        Assert.Equal("opposing_push", guard.Reason);
        Assert.True(steering.MoveDirection < 0);
        Assert.False(steering.Jump);
    }

    [Fact]
    public void ConquestBotsChooseANearbyCapturablePointInsteadOfPointIndexOrder()
    {
        var markers = new RoomObjectMarker[] {
            new(RoomObjectType.ControlPoint, 90, 170, 20, 30, "", Value: 1),
            new(RoomObjectType.ControlPoint, 340, 170, 20, 30, "", Value: 2),
            new(RoomObjectType.ControlPoint, 590, 170, 20, 30, "", Value: 3),
        };
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.TestSetLevel(CreateLevel([new LevelSolid(0, 200, 700, 30)], markers));
        world.LocalPlayer.TeleportTo(600, 180);
        var goal = ObjectiveEvaluator.EvaluateGoal(world.LocalPlayer, world, PlayerTeam.Blue, null);
        Assert.Equal(world.ControlPoints[2].HealingAuraCenterX, goal.X);
        world.ControlPoints[2].Team = PlayerTeam.Blue;
        goal = ObjectiveEvaluator.EvaluateGoal(world.LocalPlayer, world, PlayerTeam.Blue, null);
        Assert.Equal(world.ControlPoints[1].HealingAuraCenterX, goal.X);
    }

    [Fact]
    public void BotCanJumpIntoAnUpwardShaftWhenTheConveyorBlocksRetreat()
    {
        var level = CreateLevel([new LevelSolid(0, 200, 700, 30), new LevelSolid(0, 0, 100, 200)],
            [new RoomObjectMarker(RoomObjectType.MoveBoxLeft, 100, 160, 160, 40, "", Value: 150),
             new RoomObjectMarker(RoomObjectType.MoveBoxUp, 100, 0, 42, 160, "", Value: 150)]);
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.TestSetLevel(level);
        var player = world.LocalPlayer;
        player.TeleportTo(100 - player.CollisionLeftOffset, 200 - player.CollisionBottomOffset);
        player.RestoreMovementProbeState(true, player.MaxAirJumps, 1);
        var guard = new BotEnvironmentSteeringGuard();
        var steering = new SteeringOutput { MoveDirection = 1 };
        Assert.True(guard.Apply(world, player, ref steering));
        Assert.Equal(0f, steering.MoveDirection);
        Assert.True(steering.Jump);
        var startY = player.Y;
        for (var tick = 0; tick < 15; tick++)
            BotNavigationEnvironment.AdvanceProbe(level, player,
                new PlayerInputSnapshot { Up = tick == 0 }, tick == 0, player.Team);
        Assert.True(player.Y < startY - 32f);
    }

    [Fact]
    public void BotRecoversFromAirborneWallStagnationDespiteSmallVerticalBounces()
    {
        var level = CreateLevel([new LevelSolid(0, 200, 700, 30), new LevelSolid(190, 0, 20, 200)],
            [new RoomObjectMarker(RoomObjectType.MoveBoxUp, 500, 0, 42, 160, "", Value: 150)]);
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.TestSetLevel(level);
        var player = world.LocalPlayer;
        var guard = new BotEnvironmentSteeringGuard();
        var escaped = false;
        for (var tick = 0; tick <= world.Config.TicksPerSecond; tick++)
        {
            player.TeleportTo(180, 170 + tick % 4);
            player.RestoreMovementProbeState(false, player.MaxAirJumps, 1);
            var steering = new SteeringOutput { MoveDirection = 1 };
            guard.Apply(world, player, ref steering);
            if (guard.StartedEscape)
            {
                escaped = true;
                Assert.True(steering.MoveDirection < 0);
                Assert.Equal("blocked_corridor", guard.Reason);
                break;
            }
            world.AdvanceOneTick();
        }
        Assert.True(escaped, "A bouncing airborne bot must not push into a wall indefinitely.");
    }

    private static SimpleLevel CreateLevel(IReadOnlyList<LevelSolid> solids, IReadOnlyList<RoomObjectMarker> objects) =>
        new("cq_environment_test", GameModeKind.Conquest, new WorldBounds(700, 500), 1, null, 1, 1,
            new SpawnPoint(100, 180), [new SpawnPoint(100, 180)], [new SpawnPoint(600, 180)], [], objects,
            200, solids, true);
}
