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
