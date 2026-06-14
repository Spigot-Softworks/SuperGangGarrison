using System.Collections.Generic;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MapLogicEntityReferenceTests
{
    [Fact]
    public void TryFindBuilderEntityIndex_ResolvesByMapEntityIdAfterMove()
    {
        var entities = new List<CustomMapBuilderEntity>
        {
            CustomMapBuilderEntity.Create(
                "barrier",
                100f,
                200f,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [MapLogicMetadata.MapEntityIdPropertyKey] = "barrier01",
                }),
        };

        var moved = entities[0] with { X = 320f, Y = 480f };
        entities[0] = moved;

        var entityRef = MapLogicEntityReference.FormatEntityRef("barrier", "barrier01");
        Assert.True(MapLogicEntityReference.TryFindBuilderEntityIndex(entities, entityRef, out var index));
        Assert.Equal(0, index);
        Assert.Equal(320f, entities[index].X);
        Assert.Equal(480f, entities[index].Y);
    }

    [Fact]
    public void TryResolveRoomObjectIndex_IdReferenceWithoutImportedEntitiesDoesNotUseOriginFallback()
    {
        var barrier = BarrierConfiguration.CreateMarker(
            0f,
            0f,
            1f,
            1f,
            BarrierConfiguration.FromProperties(new Dictionary<string, string>()));
        var roomObjects = new List<RoomObjectMarker> { barrier };

        Assert.False(MapLogicEntityReference.TryResolveRoomObjectIndex(
            roomObjects,
            MapLogicEntityReference.FormatEntityRef("barrier", "missing-id"),
            out _));
    }

    [Fact]
    public void ImporterResolvesActivatorTargetByMapEntityId()
    {
        var barrier = BarrierConfiguration.CreateMarker(
            320f,
            480f,
            1f,
            1f,
            BarrierConfiguration.FromProperties(new Dictionary<string, string>()));
        var roomObjects = new List<RoomObjectMarker> { barrier };
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "gate",
                Kind = MapLogicNodeKind.Gate,
                GateType = MapLogicGateType.Or,
                InputRef1 = string.Empty,
            },
        ]);
        var entities = new[]
        {
            new MapImportedEntity(
                "barrier",
                320f,
                480f,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [MapLogicMetadata.MapEntityIdPropertyKey] = "barrier01",
                }),
            new MapImportedEntity(
                MapLogicMetadata.ActivatorEntityType,
                0f,
                0f,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [MapLogicMetadata.ActivatorEntityPropertyKey] = MapLogicEntityReference.FormatEntityRef(
                        "barrier",
                        "barrier01"),
                    [MapLogicMetadata.ActivatorBehaviorPropertyKey] = "disable",
                    [MapLogicMetadata.LogicInputPropertyKey] = "node:gate",
                }),
        };

        var activators = MapLogicActivatorImporter.BuildFromEntities(entities, roomObjects, graph);
        Assert.Single(activators.Activators);
        Assert.Equal(0, activators.Activators[0].TargetRoomObjectIndex);
    }
}
