using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MapLogicEntityReferenceTests
{
    [Fact]
    public void StableIdSelectsOverlappingObjectWithoutImportedEntities()
    {
        var red = new RoomObjectMarker(RoomObjectType.CustomMapSprite, 100, 100, 48, 48, "",
            SourceName: "logicCustomSprite") { MapEntityId = "red" };
        var blue = red with { MapEntityId = "blue" };
        Assert.True(MapLogicEntityReference.TryResolveRoomObjectIndex([red, blue],
            "entity:logicCustomSprite@id:BLUE", out var index));
        Assert.Equal(1, index);
    }

    [Fact]
    public void MissingStableTargetCannotRedirectToAnotherIdentifiedObjectAtItsPosition()
    {
        var marker = new RoomObjectMarker(RoomObjectType.CustomMapSprite, 100, 100, 48, 48, "",
            SourceName: "logicCustomSprite") { MapEntityId = "red" };
        var imported = new MapImportedEntity("logicCustomSprite", 124, 124,
            new Dictionary<string, string> { ["mapEntityId"] = "missing-blue" });
        Assert.False(MapLogicEntityReference.TryResolveRoomObjectIndex([marker],
            "entity:logicCustomSprite@id:missing-blue", out var index, [imported]));
        Assert.Equal(-1, index);
    }

    [Fact]
    public void StableIdFallbackStillSupportsUntaggedLegacyMarkers()
    {
        var marker = new RoomObjectMarker(RoomObjectType.CustomMapSprite, 100, 100, 48, 48, "",
            SourceName: "logicCustomSprite");
        var imported = new MapImportedEntity("logicCustomSprite", 124, 124,
            new Dictionary<string, string> { ["mapEntityId"] = "legacy-sprite" });
        Assert.True(MapLogicEntityReference.TryResolveRoomObjectIndex([marker],
            "entity:logicCustomSprite@id:legacy-sprite", out var index, [imported]));
        Assert.Equal(0, index);
        Assert.True(MapLogicEntityReference.TryResolveRoomObjectIndex([marker],
            "entity:logicCustomSprite@124,124", out index));
        Assert.Equal(0, index);
    }

    [Fact]
    public void StableIdentityStillRequiresTheCorrectObjectType()
    {
        var marker = new RoomObjectMarker(RoomObjectType.CustomMapSprite, 100, 100, 48, 48, "",
            SourceName: "logicCustomSprite") { MapEntityId = "shared" };
        Assert.False(MapLogicEntityReference.TryResolveRoomObjectIndex([marker],
            "entity:barrier@id:shared", out _));
    }
}
