using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationPerformanceBotScratchTests
{
    [Fact]
    public void LaterBuildDoesNotMutatePreviouslyReturnedInputs()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        world.CombatTestSetLevel(new SimpleLevel(
            name: "simulation_performance_bot_scratch",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(1_024f, 768f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(64f, 64f),
            redSpawns: [new SpawnPoint(64f, 64f)],
            blueSpawns: [new SpawnPoint(960f, 704f)],
            intelBases: [],
            roomObjects: [],
            floorY: 768f,
            solids: [],
            importedFromSource: false));

        const byte botSlot = 2;
        Assert.True(world.TryPrepareNetworkPlayerJoin(botSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(botSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(botSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(botSlot, out var bot));
        bot.TeleportTo(64f, 64f);
        bot.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);

        var controlledSlots = new Dictionary<byte, ControlledBotSlot>
        {
            [botSlot] = new(botSlot, PlayerTeam.Red, PlayerClass.Scout),
        };
        var controller = new BotBrainPracticeBotController(disableShippedNavigationGraphs: true);
        var retainedInputs = controller.BuildInputsForSlots(world, controlledSlots, [botSlot]);
        Assert.True(retainedInputs.TryGetValue(botSlot, out var retainedInput));

        var nextInputs = controller.BuildInputsForSlots(world, controlledSlots, Array.Empty<byte>());

        Assert.Empty(nextInputs);
        Assert.True(retainedInputs.TryGetValue(botSlot, out var inputAfterNextBuild));
        Assert.Equal(retainedInput, inputAfterNextBuild);
    }
}
