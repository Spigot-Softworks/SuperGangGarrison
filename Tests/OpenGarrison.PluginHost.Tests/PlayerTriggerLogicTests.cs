using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerTriggerLogicTests
{
    [Fact]
    public void PlayerTriggerOutputsTrueWhenMatchingPlayerIsInsideZone()
    {
        var zone = CreatePlayerTriggerZone(0f, 0f, 42f, 42f, PlayerTriggerTeamFilter.Any, "trigger");
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Any,
            },
        ]);

        var player = CreatePlayer(PlayerTeam.Red, 10f, 10f);
        var context = new PlayerTriggerEvaluationContext([player], [zone], _ => true);
        graph.EvaluateCombinatorial([], context);

        Assert.True(graph.GetOutput(graph.NodeIndexByKey["trigger"]));
    }

    [Fact]
    public void PlayerTriggerUsesPlayerCollisionBounds()
    {
        var zone = CreatePlayerTriggerZone(90f, 112f, 96f, 32f, PlayerTriggerTeamFilter.Any, "trigger");
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Any,
            },
        ]);

        var player = CreatePlayer(PlayerTeam.Red, 128f, 100f);
        Assert.False(PlayerTriggerMetadata.IsPointInsideZone(player.X, player.Y, zone));

        var context = new PlayerTriggerEvaluationContext([player], [zone], _ => true);
        graph.EvaluateCombinatorial([], context);

        Assert.True(graph.GetOutput(graph.NodeIndexByKey["trigger"]));
    }

    [Fact]
    public void PlayerTriggerRespectsTeamFilter()
    {
        var zone = CreatePlayerTriggerZone(0f, 0f, 42f, 42f, PlayerTriggerTeamFilter.Red, "trigger");
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Red,
            },
        ]);

        var bluePlayer = CreatePlayer(PlayerTeam.Blue, 10f, 10f);
        var context = new PlayerTriggerEvaluationContext([bluePlayer], [zone], _ => true);
        graph.EvaluateCombinatorial([], context);

        Assert.False(graph.GetOutput(graph.NodeIndexByKey["trigger"]));
    }

    [Fact]
    public void PlayerTriggerMaxFiresLimitsImpulses()
    {
        var zone = CreatePlayerTriggerZone(0f, 0f, 42f, 42f, PlayerTriggerTeamFilter.Any, "trigger");
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Any,
                SignalMode = MapLogicSignalMode.Impulse,
                PlayerDetectMode = MapLogicPlayerDetectMode.PlayerEnter,
                PlayerTriggerMaxFires = 1,
            },
        ]);

        var player = CreatePlayer(PlayerTeam.Red, 10f, 10f);
        var emptyContext = new PlayerTriggerEvaluationContext([], [zone], _ => true);
        var occupiedContext = new PlayerTriggerEvaluationContext([player], [zone], _ => true);

        graph.ResetPlayerTriggerStates(emptyContext);
        graph.EvaluateCombinatorial([], occupiedContext);
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["trigger"]));

        graph.EvaluateCombinatorial([], emptyContext);
        graph.EvaluateCombinatorial([], occupiedContext);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["trigger"]));
    }

    [Fact]
    public void GateReadsPlayerTriggerOutput()
    {
        var zone = CreatePlayerTriggerZone(0f, 0f, 42f, 42f, PlayerTriggerTeamFilter.Any, "trigger");
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Any,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "gate",
                Kind = MapLogicNodeKind.Gate,
                GateType = MapLogicGateType.And,
                InputRef1 = "node:trigger",
                InputRef2 = "node:trigger",
            },
        ]);

        var player = CreatePlayer(PlayerTeam.Red, 10f, 10f);
        var context = new PlayerTriggerEvaluationContext([player], [zone], _ => true);
        graph.EvaluateCombinatorial([], context);

        Assert.True(graph.GetOutput(graph.NodeIndexByKey["gate"]));
    }

    [Fact]
    public void ExternalPulseSurvivesOneEvaluationAndPropagates()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "pulse",
                Kind = MapLogicNodeKind.Gate,
                GateType = MapLogicGateType.And,
            },
            new MapLogicNodeDefinition
            {
                LogicKey = "consumer",
                Kind = MapLogicNodeKind.Gate,
                GateType = MapLogicGateType.And,
                InputRef1 = "node:pulse",
                InputRef2 = "node:pulse",
            },
        ]);

        var pulseIndex = graph.NodeIndexByKey["pulse"];
        var consumerIndex = graph.NodeIndexByKey["consumer"];

        Assert.True(graph.PulseExternalOutput(pulseIndex));
        Assert.True(graph.GetOutput(pulseIndex));
        Assert.True(graph.GetOutput(consumerIndex));

        graph.EvaluateCombinatorial([]);
        Assert.True(graph.GetOutput(pulseIndex));
        Assert.True(graph.GetOutput(consumerIndex));

        graph.EvaluateCombinatorial([]);
        Assert.False(graph.GetOutput(pulseIndex));
        Assert.False(graph.GetOutput(consumerIndex));
    }

    [Fact]
    public void ImporterBuildsPlayerTriggerNodeLinkedToRoomObject()
    {
        var roomObjects = new List<RoomObjectMarker>();
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = [],
            BlueSpawns = [],
            RoomObjects = roomObjects,
            UseCenterOrigin = true,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            PlayerTriggerMetadata.PlayerTriggerEntityType,
            100f,
            120f,
            2f,
            1f,
            new Dictionary<string, string>
            {
                [PlayerTriggerMetadata.TeamPropertyKey] = "blue",
                ["logicKey"] = "playerZone",
            },
            context));

        var entities = new[]
        {
            new MapImportedEntity(
                PlayerTriggerMetadata.PlayerTriggerEntityType,
                100f,
                120f,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [PlayerTriggerMetadata.TeamPropertyKey] = "blue",
                    ["logicKey"] = "playerZone",
                }),
        };

        var graph = MapLogicGraphImporter.BuildFromEntities(entities, roomObjects);
        var node = graph.Nodes[0];
        Assert.Equal(MapLogicNodeKind.PlayerTrigger, node.Kind);
        Assert.Equal(PlayerTriggerTeamFilter.Blue, node.PlayerTriggerTeamFilter);
        Assert.Equal(0, node.PlayerTriggerRoomObjectIndex);
        Assert.Equal("playerZone", roomObjects[0].SourceName);
    }

    [Fact]
    public void PlayerTriggerIgnoresNonIntelCarriersWhenIntelCarriersOnlyIsEnabled()
    {
        var zone = CreatePlayerTriggerZone(0f, 0f, 42f, 42f, PlayerTriggerTeamFilter.Any, "trigger");
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Any,
                PlayerTriggerIntelCarriersOnly = true,
            },
        ]);

        var player = CreatePlayer(PlayerTeam.Red, 10f, 10f);
        var context = new PlayerTriggerEvaluationContext([player], [zone], _ => true);
        graph.EvaluateCombinatorial([], context);

        Assert.False(graph.GetOutput(graph.NodeIndexByKey["trigger"]));
    }

    [Fact]
    public void PlayerTriggerDetectsIntelCarriersWhenIntelCarriersOnlyIsEnabled()
    {
        var zone = CreatePlayerTriggerZone(0f, 0f, 42f, 42f, PlayerTriggerTeamFilter.Any, "trigger");
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Any,
                PlayerTriggerIntelCarriersOnly = true,
            },
        ]);

        var player = CreatePlayer(PlayerTeam.Red, 10f, 10f);
        player.PickUpIntel();
        var context = new PlayerTriggerEvaluationContext([player], [zone], _ => true);
        graph.EvaluateCombinatorial([], context);

        Assert.True(graph.GetOutput(graph.NodeIndexByKey["trigger"]));
    }

    [Fact]
    public void PlayerTriggerIntelCarriersOnlyWorksAlongsideTeamFilter()
    {
        var zone = CreatePlayerTriggerZone(0f, 0f, 42f, 42f, PlayerTriggerTeamFilter.Red, "trigger");
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Red,
                PlayerTriggerIntelCarriersOnly = true,
            },
        ]);

        var redCarrier = CreatePlayer(PlayerTeam.Red, 10f, 10f);
        redCarrier.PickUpIntel();
        var blueCarrier = CreatePlayer(PlayerTeam.Blue, 12f, 12f);
        blueCarrier.PickUpIntel();
        var context = new PlayerTriggerEvaluationContext([redCarrier, blueCarrier], [zone], _ => true);
        graph.EvaluateCombinatorial([], context);

        Assert.True(graph.GetOutput(graph.NodeIndexByKey["trigger"]));
    }

    [Fact]
    public void ImporterReadsIntelCarriersOnlyProperty()
    {
        var roomObjects = new List<RoomObjectMarker>();
        var entities = new[]
        {
            new MapImportedEntity(
                PlayerTriggerMetadata.PlayerTriggerEntityType,
                100f,
                120f,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [PlayerTriggerMetadata.TeamPropertyKey] = "any",
                    [PlayerTriggerMetadata.IntelCarriersOnlyPropertyKey] = "true",
                    ["logicKey"] = "intelZone",
                }),
        };

        var graph = MapLogicGraphImporter.BuildFromEntities(entities, roomObjects);
        var node = graph.Nodes[0];

        Assert.True(node.PlayerTriggerIntelCarriersOnly);
    }

    [Fact]
    public void SimulationEvaluatesPlayerTriggerEachTick()
    {
        var zone = CreatePlayerTriggerZone(0f, 0f, 42f, 42f, PlayerTriggerTeamFilter.Any, "trigger");
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Any,
            },
        ]);
        var world = new SimulationWorld();
        var setLevel = typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setLevel);
        setLevel.Invoke(
            world,
            [
                new SimpleLevel(
                    "player-trigger-test",
                    GameModeKind.TeamDeathmatch,
                    new WorldBounds(512f, 512f),
                    1f,
                    null,
                    0,
                    1,
                    new SpawnPoint(0f, 0f),
                    [],
                    [],
                    [],
                    [zone],
                    0f,
                    [],
                    importedFromSource: false,
                    logicGraph: graph),
            ]);

        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        world.LocalPlayer.TeleportTo(10f, 10f);

        world.TickMapLogicTimers();

        Assert.True(world.Level.LogicGraph.GetOutput(world.Level.LogicGraph.NodeIndexByKey["trigger"]));
    }

    [Fact]
    public void FrameScopedMapLogicTickDoesNotConsumePlayerEnterImpulseTwice()
    {
        var zone = CreatePlayerTriggerZone(90f, 112f, 96f, 32f, PlayerTriggerTeamFilter.Red, "trigger");
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Red,
                SignalMode = MapLogicSignalMode.Impulse,
                PlayerDetectMode = MapLogicPlayerDetectMode.PlayerEnter,
            },
        ]);
        var world = new SimulationWorld();
        var setLevel = typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setLevel);
        setLevel.Invoke(
            world,
            [
                new SimpleLevel(
                    "player-trigger-frame-test",
                    GameModeKind.TeamDeathmatch,
                    new WorldBounds(512f, 512f),
                    1f,
                    null,
                    0,
                    1,
                    new SpawnPoint(0f, 0f),
                    [],
                    [],
                    [],
                    [zone],
                    0f,
                    [],
                    importedFromSource: false,
                    logicGraph: graph),
            ]);

        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        world.LocalPlayer.TeleportTo(128f, 100f);

        world.TickMapLogicTimersOncePerFrame();
        world.TickMapLogicTimersOncePerFrame();

        Assert.True(world.Level.LogicGraph.GetOutput(world.Level.LogicGraph.NodeIndexByKey["trigger"]));
    }

    [Fact]
    public void OfflineSimulationEvaluatesPlayerTriggersBeforeAfterTickCallback()
    {
        var zone = CreatePlayerTriggerZone(90f, 112f, 96f, 32f, PlayerTriggerTeamFilter.Red, "trigger");
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Red,
                SignalMode = MapLogicSignalMode.Impulse,
                PlayerDetectMode = MapLogicPlayerDetectMode.PlayerEnter,
            },
        ]);
        var world = new SimulationWorld();
        var setLevel = typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setLevel);
        setLevel.Invoke(
            world,
            [
                new SimpleLevel(
                    "player-trigger-after-tick-test",
                    GameModeKind.TeamDeathmatch,
                    new WorldBounds(512f, 512f),
                    1f,
                    null,
                    0,
                    1,
                    new SpawnPoint(0f, 0f),
                    [],
                    [],
                    [],
                    [zone],
                    0f,
                    [],
                    importedFromSource: false,
                    logicGraph: graph),
            ]);

        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        world.LocalPlayer.TeleportTo(128f, 100f);

        var simulator = new FixedStepSimulator(world);
        var afterTickSawOutput = false;
        simulator.Step(
            world.Config.FixedDeltaSeconds,
            beforeTickAdvanced: null,
            onTickAdvanced: () =>
            {
                afterTickSawOutput = world.Level.LogicGraph.GetOutput(world.Level.LogicGraph.NodeIndexByKey["trigger"]);
            });

        Assert.True(afterTickSawOutput);
    }

    private static RoomObjectMarker CreatePlayerTriggerZone(
        float left,
        float top,
        float width,
        float height,
        PlayerTriggerTeamFilter filter,
        string logicKey)
    {
        return new RoomObjectMarker(
            RoomObjectType.PlayerTriggerZone,
            left,
            top,
            width,
            height,
            string.Empty,
            SourceName: logicKey,
            PlayerTriggerZone: new PlayerTriggerZoneConfiguration(filter));
    }

    private static PlayerEntity CreatePlayer(PlayerTeam team, float x, float y)
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");
        player.Spawn(team, x, y);
        return player;
    }
}
