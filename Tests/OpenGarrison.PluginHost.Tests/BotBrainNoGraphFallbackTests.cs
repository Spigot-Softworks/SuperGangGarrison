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
    public void PracticeBotsMoveAcrossTicksOnStockArenaMapWhenNavigationGraphIsMissing()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        Assert.True(world.TryLoadLevel("Lumberyard"));
        const byte botSlot = 2;
        Assert.True(world.TryPrepareNetworkPlayerJoin(botSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(botSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(botSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(botSlot, out var bot));
        var startX = bot.X;
        var controller = new BotBrainPracticeBotController();
        var slots = new Dictionary<byte, ControlledBotSlot>
        {
            [botSlot] = new(botSlot, PlayerTeam.Red, PlayerClass.Scout),
        };
        var activeTicks = 0;

        for (var tick = 0; tick < 90; tick += 1)
        {
            var inputs = controller.BuildInputs(world, slots);
            var input = inputs.GetValueOrDefault(botSlot);
            if (IsActiveMovement(input))
            {
                activeTicks += 1;
            }

            Assert.True(world.TrySetNetworkPlayerInput(botSlot, input));
            world.AdvanceOneTick();
        }

        Assert.True(controller.TryGetBotBrainController(botSlot, out var brain));
        Assert.NotNull(brain);
        Assert.False(brain!.HasNavigationGraph);
        Assert.True(activeTicks > 60, brain.LastDirectDriveTrace);
        Assert.True(MathF.Abs(bot.X - startX) > 32f, $"startX:{startX:0.0} endX:{bot.X:0.0} trace:{brain.LastDirectDriveTrace}");
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

    private static bool IsActiveMovement(PlayerInputSnapshot input) =>
        input.Left || input.Right || input.Up || input.Down;

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
}
