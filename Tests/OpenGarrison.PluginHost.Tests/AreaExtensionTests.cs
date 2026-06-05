using System.Collections.Generic;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class AreaExtensionTests
{
    [Fact]
    public void PlayerTriggerOutputsTrueWhenPlayerInsideExtensionArea()
    {
        var roomObjects = new List<RoomObjectMarker>
        {
            new(
                RoomObjectType.PlayerTriggerZone,
                40f,
                40f,
                20f,
                20f,
                string.Empty,
                SourceName: "trigger",
                PlayerTriggerZone: new PlayerTriggerZoneConfiguration(PlayerTriggerTeamFilter.Any)),
            new(
                RoomObjectType.AreaExtension,
                200f,
                200f,
                30f,
                30f,
                string.Empty,
                SourceName: AreaExtensionMetadata.AreaEntityType,
                AreaExtension: new AreaExtensionConfiguration(0, AreaExtensionKind.PlayerTrigger)),
        };

        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerZoneRoomObjectIndices = [0, 1],
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Any,
            },
        ]);

        var player = CreatePlayer(PlayerTeam.Red, 215f, 215f);
        var context = new PlayerTriggerEvaluationContext([player], roomObjects, _ => true);
        graph.EvaluateCombinatorial([], context);

        Assert.True(graph.GetOutput(graph.NodeIndexByKey["trigger"]));
    }

    [Fact]
    public void ExtensionInactiveWhenParentMaskDisabled()
    {
        var roomObjects = new List<RoomObjectMarker>
        {
            new(
                RoomObjectType.PlayerTriggerZone,
                0f,
                0f,
                40f,
                40f,
                string.Empty,
                SourceName: "trigger",
                PlayerTriggerZone: new PlayerTriggerZoneConfiguration(PlayerTriggerTeamFilter.Any)),
            new(
                RoomObjectType.AreaExtension,
                100f,
                100f,
                40f,
                40f,
                string.Empty,
                SourceName: AreaExtensionMetadata.AreaEntityType,
                AreaExtension: new AreaExtensionConfiguration(0, AreaExtensionKind.PlayerTrigger)),
        };

        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.PlayerTrigger,
                PlayerTriggerRoomObjectIndex = 0,
                PlayerTriggerZoneRoomObjectIndices = [0, 1],
                PlayerTriggerTeamFilter = PlayerTriggerTeamFilter.Any,
            },
        ]);

        var player = CreatePlayer(PlayerTeam.Red, 120f, 120f);
        var activeMask = new[] { false, true };
        var context = new PlayerTriggerEvaluationContext(
            [player],
            roomObjects,
            index => IsRoomObjectActive(activeMask, roomObjects, index));
        graph.EvaluateCombinatorial([], context);

        Assert.False(graph.GetOutput(graph.NodeIndexByKey["trigger"]));
    }

    [Fact]
    public void ImporterCreatesAreaExtensionAfterParentTrigger()
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
            50f,
            50f,
            1f,
            1f,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["logicKey"] = "zone",
            },
            context));

        var extendsRef = MapLogicEntityReference.FormatEntityRef(
            PlayerTriggerMetadata.PlayerTriggerEntityType,
            50f,
            50f);
        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            AreaExtensionMetadata.AreaEntityType,
            120f,
            120f,
            1f,
            1f,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AreaExtensionMetadata.ExtendsPropertyKey] = extendsRef,
            },
            context));

        Assert.Equal(2, roomObjects.Count);
        Assert.Equal(RoomObjectType.AreaExtension, roomObjects[1].Type);
        Assert.Equal(0, roomObjects[1].AreaExtension.ParentRoomObjectIndex);
        Assert.Equal(AreaExtensionKind.PlayerTrigger, roomObjects[1].AreaExtension.Kind);
    }

    private static bool IsRoomObjectActive(
        bool[] activeMask,
        IReadOnlyList<RoomObjectMarker> roomObjects,
        int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= activeMask.Length)
        {
            return true;
        }

        if (!activeMask[roomObjectIndex])
        {
            return false;
        }

        if (roomObjectIndex >= roomObjects.Count)
        {
            return true;
        }

        var marker = roomObjects[roomObjectIndex];
        if (marker.Type != RoomObjectType.AreaExtension)
        {
            return true;
        }

        var parentIndex = marker.AreaExtension.ParentRoomObjectIndex;
        return parentIndex < 0 || IsRoomObjectActive(activeMask, roomObjects, parentIndex);
    }

    private static PlayerEntity CreatePlayer(PlayerTeam team, float x, float y)
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");
        player.Spawn(team, x, y);
        return player;
    }
}
