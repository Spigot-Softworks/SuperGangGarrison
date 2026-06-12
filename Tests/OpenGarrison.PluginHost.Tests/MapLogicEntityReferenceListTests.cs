using System.Collections.Generic;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MapLogicEntityReferenceListTests
{
    [Fact]
    public void Parse_TreatsSingleRefWithoutSeparatorAsOneItem()
    {
        var value = MapLogicEntityReference.FormatEntityRef("barrier", "barrier01");
        var refs = MapLogicEntityReferenceList.Parse(value);
        Assert.Single(refs);
        Assert.Equal(value, refs[0]);
    }

    [Fact]
    public void Format_UsesSeparatorOnlyForMultipleRefs()
    {
        var first = MapLogicEntityReference.FormatEntityRef("barrier", "barrier01");
        var second = MapLogicEntityReference.FormatEntityRef("barrier", "barrier02");
        Assert.Equal(first, MapLogicEntityReferenceList.Format([first]));
        Assert.Equal($"{first}|{second}", MapLogicEntityReferenceList.Format([first, second]));
    }

    [Fact]
    public void AppendDistinct_AvoidsDuplicateRefs()
    {
        var first = MapLogicEntityReference.FormatEntityRef("barrier", "barrier01");
        var combined = MapLogicEntityReferenceList.AppendDistinct(first, first);
        Assert.Equal(first, combined);
    }

    [Fact]
    public void ImporterBuildsOneActivatorPerTargetRef()
    {
        var barrierA = BarrierConfiguration.CreateMarker(
            100f,
            200f,
            1f,
            1f,
            BarrierConfiguration.FromProperties(new Dictionary<string, string>()));
        var barrierB = BarrierConfiguration.CreateMarker(
            140f,
            200f,
            1f,
            1f,
            BarrierConfiguration.FromProperties(new Dictionary<string, string>()));
        var roomObjects = new List<RoomObjectMarker> { barrierA, barrierB };
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
                MapLogicMetadata.ActivatorEntityType,
                0f,
                0f,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [MapLogicMetadata.ActivatorEntityPropertyKey] = MapLogicEntityReferenceList.Format(
                    [
                        MapLogicEntityReference.FormatEntityRef("barrier", 100f, 200f),
                        MapLogicEntityReference.FormatEntityRef("barrier", 140f, 200f),
                    ]),
                    [MapLogicMetadata.ActivatorBehaviorPropertyKey] = "disable",
                    [MapLogicMetadata.LogicInputPropertyKey] = "node:gate",
                }),
        };

        var activators = MapLogicActivatorImporter.BuildFromEntities(entities, roomObjects, graph);
        Assert.Equal(2, activators.Activators.Count);
        Assert.Equal(0, activators.Activators[0].TargetRoomObjectIndex);
        Assert.Equal(1, activators.Activators[1].TargetRoomObjectIndex);
    }
}
