using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotSpawnNodeTests
{
    [Fact]
    public void RuntimeImporterCreatesBotSpawnMarker()
    {
        var context = new CustomMapEntityImportContext();

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            BotSpawnMetadata.BotSpawnEntityType,
            160f,
            192f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                [BotSpawnMetadata.TriggerPropertyKey] = "node:alarm",
                [BotSpawnMetadata.TeamPropertyKey] = "red",
                [BotSpawnMetadata.ClassPropertyKey] = "soldier",
                [BotSpawnMetadata.KindPropertyKey] = "dummy",
                [BotSpawnMetadata.RespawnPropertyKey] = "false",
                [BotSpawnMetadata.RespawnAtPropertyKey] = "node",
                [BotSpawnMetadata.NameModePropertyKey] = "manual",
                [BotSpawnMetadata.NamePropertyKey] = "Alarm Bot",
                [BotSpawnMetadata.ForceNameplatePropertyKey] = "true",
                [BotSpawnMetadata.ForceHealthBarPropertyKey] = "true",
                [BotSpawnMetadata.DeathTriggerPropertyKey] = "node:botDead",
            },
            context));

        var marker = Assert.Single(context.BotSpawns);
        Assert.Equal(160f, marker.X);
        Assert.Equal(192f, marker.Y);
        Assert.Equal("node:alarm", marker.TriggerRef);
        Assert.Equal(PlayerTeam.Red, marker.Team);
        Assert.Equal(PlayerClass.Soldier, marker.ClassId);
        Assert.Equal(BotSpawnKind.Dummy, marker.Kind);
        Assert.False(marker.Respawn);
        Assert.Equal(BotSpawnRespawnMode.Node, marker.RespawnMode);
        Assert.Equal(BotSpawnNameMode.Manual, marker.NameMode);
        Assert.Equal("Alarm Bot", marker.Name);
        Assert.True(marker.ForceNameplate);
        Assert.True(marker.ForceHealthBar);
        Assert.Equal("node:botDead", marker.DeathTriggerRef);
    }

    [Fact]
    public void RuntimePatchResolvesBotSpawnDeathTriggerNode()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "alarm",
                Kind = MapLogicNodeKind.Gate,
                GateType = MapLogicGateType.And,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "botDead",
                Kind = MapLogicNodeKind.Gate,
                GateType = MapLogicGateType.And,
            },
        ]);
        var marker = new BotSpawnMarker(
            160f,
            192f,
            "node:alarm",
            PlayerTeam.Blue,
            PlayerClass.Soldier,
            BotSpawnKind.Bot,
            true,
            BotSpawnRespawnMode.NormalSpawn,
            BotSpawnNameMode.Random,
            string.Empty,
            false,
            false,
            "node:botDead");

        var resolved = Assert.Single(BotSpawnRuntimePatch.ResolveTriggerSignals([marker], graph));

        Assert.Equal(graph.NodeIndexByKey["alarm"], resolved.TriggerNodeIndex);
        Assert.Equal(graph.NodeIndexByKey["botDead"], resolved.DeathTriggerNodeIndex);
    }

    [Fact]
    public void MapSpawnedBotUsesRequestedPosition()
    {
        var world = CreateWorldWithOpenSpawnPosition(240f, 220f);
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new BotBrainPracticeBotController());

        Assert.True(botManager.TrySpawnMapBot(
            PlayerTeam.Blue,
            PlayerClass.Medic,
            BotSpawnKind.Bot,
            true,
            BotSpawnRespawnMode.NormalSpawn,
            BotSpawnNameMode.Random,
            string.Empty,
            false,
            false,
            240f,
            220f,
            out var slot));

        Assert.True(world.TryGetNetworkPlayer(slot, out var bot));
        Assert.Equal(PlayerTeam.Blue, bot.Team);
        Assert.Equal(PlayerClass.Medic, bot.ClassId);
        Assert.Equal(240f, bot.X);
        Assert.Equal(220f, bot.Y);
    }

    [Fact]
    public void MapSpawnedBotFindsSafePositionNearBlockedMarker()
    {
        var world = CreateWorldWithFloorAndFallbackSpawn();
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new BotBrainPracticeBotController());

        Assert.True(botManager.TrySpawnMapBot(
            PlayerTeam.Blue,
            PlayerClass.Soldier,
            BotSpawnKind.Bot,
            true,
            BotSpawnRespawnMode.NormalSpawn,
            BotSpawnNameMode.Random,
            string.Empty,
            false,
            false,
            240f,
            240f,
            out var slot));

        Assert.True(world.TryGetNetworkPlayer(slot, out var bot));
        Assert.Equal(PlayerTeam.Blue, bot.Team);
        Assert.Equal(PlayerClass.Soldier, bot.ClassId);
        Assert.InRange(bot.X, 176f, 304f);
        Assert.NotEqual(40f, bot.X);
    }

    [Fact]
    public void MapSpawnedDummyIsNotControlledByBotBrain()
    {
        var world = CreateWorldWithOpenSpawnPosition(240f, 220f);
        var botController = new ThrowingPracticeBotController();
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            botController);

        Assert.True(botManager.TrySpawnMapBot(
            PlayerTeam.Blue,
            PlayerClass.Heavy,
            BotSpawnKind.Dummy,
            true,
            BotSpawnRespawnMode.NormalSpawn,
            BotSpawnNameMode.Random,
            string.Empty,
            false,
            false,
            240f,
            220f,
            out _));

        botManager.FeedBotInputsBeforeSimulationAdvance();

        Assert.Equal(0, botController.BuildInputCalls);
    }

    [Fact]
    public void MapSpawnedBotCanUseManualNameAndDisableRespawn()
    {
        var world = CreateWorldWithOpenSpawnPosition(240f, 220f);
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new BotBrainPracticeBotController());

        Assert.True(botManager.TrySpawnMapBot(
            PlayerTeam.Blue,
            PlayerClass.Medic,
            BotSpawnKind.Bot,
            false,
            BotSpawnRespawnMode.NormalSpawn,
            BotSpawnNameMode.Manual,
            "Doc Map",
            false,
            false,
            240f,
            220f,
            out var slot));

        Assert.True(world.TryGetNetworkPlayer(slot, out var bot));
        Assert.Equal("Doc Map", bot.DisplayName);

        Assert.True(world.ForceKillNetworkPlayer(slot));
        botManager.AdvanceBotReactions();

        Assert.False(botManager.BotSlots.ContainsKey(slot));
    }

    [Fact]
    public void MapSpawnedBotUsesNonEmptyNameAsOverride()
    {
        var world = CreateWorldWithOpenSpawnPosition(240f, 220f);
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new BotBrainPracticeBotController());

        Assert.True(botManager.TrySpawnMapBot(
            PlayerTeam.Blue,
            PlayerClass.Medic,
            BotSpawnKind.Bot,
            true,
            BotSpawnRespawnMode.NormalSpawn,
            BotSpawnNameMode.Random,
            "Doc Map",
            false,
            false,
            240f,
            220f,
            out var slot));

        Assert.True(world.TryGetNetworkPlayer(slot, out var bot));
        Assert.Equal("Doc Map", bot.DisplayName);
    }

    [Fact]
    public void MapSpawnedBotAppliesForcedOverlayReplicatedStates()
    {
        var world = CreateWorldWithOpenSpawnPosition(240f, 220f);
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new BotBrainPracticeBotController());

        Assert.True(botManager.TrySpawnMapBot(
            PlayerTeam.Blue,
            PlayerClass.Medic,
            BotSpawnKind.Bot,
            true,
            BotSpawnRespawnMode.NormalSpawn,
            BotSpawnNameMode.Manual,
            "Doc Map",
            true,
            true,
            240f,
            220f,
            out var slot,
            deathTriggerNodeIndex: 3));

        Assert.True(world.TryGetNetworkPlayer(slot, out var bot));
        Assert.True(bot.TryGetReplicatedStateBool(
            BotSpawnMetadata.VisualReplicatedStateOwnerId,
            BotSpawnMetadata.ForceNameplateReplicatedStateKey,
            out var forceNameplate));
        Assert.True(forceNameplate);
        Assert.True(bot.TryGetReplicatedStateBool(
            BotSpawnMetadata.VisualReplicatedStateOwnerId,
            BotSpawnMetadata.ForceHealthBarReplicatedStateKey,
            out var forceHealthBar));
        Assert.True(forceHealthBar);
        Assert.True(bot.TryGetReplicatedStateInt(
            BotSpawnMetadata.VisualReplicatedStateOwnerId,
            BotSpawnMetadata.DeathTriggerNodeReplicatedStateKey,
            out var deathTriggerNodeIndex));
        Assert.Equal(3, deathTriggerNodeIndex);
    }

    [Fact]
    public void MapSpawnedBotDeathPulsesConfiguredLogicNode()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "botDead",
                Kind = MapLogicNodeKind.Gate,
                GateType = MapLogicGateType.And,
            },
        ]);
        var world = CreateWorldWithOpenSpawnPosition(240f, 220f, graph);
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new BotBrainPracticeBotController());
        var deathNodeIndex = graph.NodeIndexByKey["botDead"];

        Assert.True(botManager.TrySpawnMapBot(
            PlayerTeam.Blue,
            PlayerClass.Medic,
            BotSpawnKind.Bot,
            true,
            BotSpawnRespawnMode.NormalSpawn,
            BotSpawnNameMode.Random,
            string.Empty,
            false,
            false,
            240f,
            220f,
            out var slot,
            deathNodeIndex));

        botManager.AdvanceBotReactions();
        Assert.True(world.ForceKillNetworkPlayer(slot));
        botManager.AdvanceBotReactions();

        Assert.True(graph.GetOutput(deathNodeIndex));
    }

    private static SimulationWorld CreateWorldWithOpenSpawnPosition(float x, float y, MapLogicGraph? logicGraph = null)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var spawn = new SpawnPoint(x, y);
        world.CombatTestSetLevel(new SimpleLevel(
            "bot-spawn-test",
            GameModeKind.TeamDeathmatch,
            new WorldBounds(512f, 512f),
            1f,
            null,
            1,
            1,
            spawn,
            [spawn],
            [spawn],
            [],
            [],
            floorY: 512f,
            [],
            importedFromSource: false,
            logicGraph: logicGraph));
        return world;
    }

    private static SimulationWorld CreateWorldWithFloorAndFallbackSpawn()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var fallbackSpawn = new SpawnPoint(40f, 160f);
        world.CombatTestSetLevel(new SimpleLevel(
            "bot-spawn-floor-test",
            GameModeKind.TeamDeathmatch,
            new WorldBounds(512f, 320f),
            1f,
            null,
            1,
            1,
            fallbackSpawn,
            [fallbackSpawn],
            [fallbackSpawn],
            [],
            [],
            floorY: 320f,
            [new LevelSolid(0f, 240f, 512f, 80f)],
            importedFromSource: false));
        return world;
    }

    private sealed class ThrowingPracticeBotController : IPracticeBotController
    {
        public bool CollectDiagnostics { get; set; }

        public BotControllerDiagnosticsSnapshot LastDiagnostics => BotControllerDiagnosticsSnapshot.Empty;

        public int BuildInputCalls { get; private set; }

        public void Reset()
        {
        }

        public void ConfigureSpawnOverrides(
            SimulationWorld world,
            IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
        {
        }

        public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
            SimulationWorld world,
            IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
        {
            BuildInputCalls += 1;
            throw new Xunit.Sdk.XunitException("Dummy bot spawns should not request bot inputs.");
        }

        public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputsForSlots(
            SimulationWorld world,
            IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots,
            IReadOnlyCollection<byte> slotsToThink)
        {
            BuildInputCalls += 1;
            throw new Xunit.Sdk.XunitException("Dummy bot spawns should not request bot inputs.");
        }
    }
}
