using System.Collections.Generic;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CustomMapCustomSpriteTests
{
    [Fact]
    public void EnsurePlacementDefaultsUsesMapVisualScale()
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CustomMapCustomSpriteMetadata.EnsurePlacementDefaults(properties, 1.25f);
        Assert.Equal("1.25", properties[CustomMapCustomSpriteMetadata.ScalePropertyKey]);
    }

    [Fact]
    public void ImporterCreatesCenterAnchoredSpriteRoomObject()
    {
        var roomObjects = new List<RoomObjectMarker>();
        var resources = new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase)
        {
            ["icon"] = new CustomMapBuilderResource(
                "icon",
                string.Empty,
                CustomMapBuilderResourceKind.CustomSprite,
                CreatePng(32, 24)),
        };
        var context = new CustomMapEntityImportContext
        {
            RoomObjects = roomObjects,
            Resources = resources,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            CustomMapCustomSpriteMetadata.CustomSpriteEntityType,
            100f,
            80f,
            1f,
            1f,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [CustomMapCustomSpriteMetadata.ImagePropertyKey] = "icon",
                [CustomMapCustomSpriteMetadata.LayerPropertyKey] = "layer2",
                [CustomMapCustomSpriteMetadata.ZOrderPropertyKey] = "7",
                [CustomMapCustomSpriteMetadata.ScalePropertyKey] = "2",
            },
            context));

        var marker = Assert.Single(roomObjects);
        Assert.Equal(RoomObjectType.CustomMapSprite, marker.Type);
        Assert.Equal(CustomMapSpriteLayerKind.Layer2, marker.CustomMapSprite.Layer);
        Assert.Equal(7, marker.CustomMapSprite.ZOrder);
        Assert.Equal(64f, marker.Width);
        Assert.Equal(48f, marker.Height);
        Assert.Equal(100f, marker.CenterX, precision: 2);
        Assert.Equal(80f, marker.CenterY, precision: 2);
    }

    [Fact]
    public void ImporterAppliesEntityScaleToSpriteBoundsAndDrawScale()
    {
        var roomObjects = new List<RoomObjectMarker>();
        var resources = new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase)
        {
            ["icon"] = new CustomMapBuilderResource(
                "icon",
                string.Empty,
                CustomMapBuilderResourceKind.CustomSprite,
                CreatePng(32, 24)),
        };
        var context = new CustomMapEntityImportContext
        {
            RoomObjects = roomObjects,
            Resources = resources,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            CustomMapCustomSpriteMetadata.CustomSpriteEntityType,
            100f,
            80f,
            1.5f,
            0.5f,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [CustomMapCustomSpriteMetadata.ImagePropertyKey] = "icon",
                [CustomMapCustomSpriteMetadata.ScalePropertyKey] = "2",
            },
            context));

        var marker = Assert.Single(roomObjects);
        Assert.Equal(96f, marker.Width);
        Assert.Equal(24f, marker.Height);
        Assert.Equal(3f, marker.CustomMapSprite.Scale);
    }

    [Fact]
    public void ActivatorResolvesCustomSpriteByCenterPosition()
    {
        var roomObjects = new List<RoomObjectMarker>
        {
            new(
                RoomObjectType.CustomMapSprite,
                10f,
                20f,
                40f,
                20f,
                string.Empty,
                SourceName: CustomMapCustomSpriteMetadata.CustomSpriteEntityType,
                CustomMapSprite: new CustomMapSpriteConfiguration(
                    "icon",
                    CustomMapSpriteLayerKind.Bg,
                    0,
                    1f,
                    false,
                    MapSpriteTileAnchor.TopLeft,
                    0f,
                    0f)),
        };
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "trigger",
                Kind = MapLogicNodeKind.Gate,
                GateType = MapLogicGateType.Or,
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
                    [MapLogicMetadata.ActivatorEntityPropertyKey] = MapLogicEntityReference.FormatEntityRef(
                        CustomMapCustomSpriteMetadata.CustomSpriteEntityType,
                        30f,
                        30f),
                    [MapLogicMetadata.ActivatorBehaviorPropertyKey] = "disable",
                    [MapLogicMetadata.LogicInputPropertyKey] = "node:trigger",
                }),
        };
        var activators = MapLogicActivatorImporter.BuildFromEntities(entities, roomObjects, graph);
        Assert.Single(activators.Activators);
        Assert.Equal(0, activators.Activators[0].TargetRoomObjectIndex);
    }

    [Fact]
    public void FindOrAssignImageResourceDedupesBySourcePath()
    {
        var resources = new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(Path.GetTempPath(), $"sprite_{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, CreatePng(8, 8));
        try
        {
            var first = CustomMapCustomSpriteMetadata.FindOrAssignImageResource(path, resources);
            var second = CustomMapCustomSpriteMetadata.FindOrAssignImageResource(path, resources);
            Assert.Equal(first, second);
            Assert.Single(resources);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        using var stream = new MemoryStream();
        image.Save(stream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
        return stream.ToArray();
    }
}
