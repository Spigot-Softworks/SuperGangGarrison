using System.Collections.Generic;
using System.IO;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MapSpriteTileTests
{
    [Fact]
    public void ParseConfiguration_ReadsTileProperties()
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [MapSpriteTileMetadata.TilePropertyKey] = "true",
            [MapSpriteTileMetadata.TileAnchorPropertyKey] = "center",
            [MapSpriteTileMetadata.TileAreaWidthPropertyKey] = "120",
            [MapSpriteTileMetadata.TileAreaHeightPropertyKey] = "80",
            [CustomMapCustomSpriteMetadata.ScalePropertyKey] = "2",
        };

        var configuration = CustomMapCustomSpriteMetadata.ParseConfiguration(properties);
        Assert.True(configuration.Tile);
        Assert.Equal(MapSpriteTileAnchor.Center, configuration.TileAnchor);
        Assert.Equal(120f, configuration.TileAreaWidth);
        Assert.Equal(80f, configuration.TileAreaHeight);
        Assert.Equal(2f, configuration.Scale);
    }

    [Fact]
    public void ResolveWorldDimensions_UsesTileAreaWhenTileEnabled()
    {
        var configuration = new CustomMapSpriteConfiguration(
            "icon",
            CustomMapSpriteLayerKind.Bg,
            0,
            2f,
            true,
            MapSpriteTileAnchor.TopLeft,
            200f,
            100f);

        var (width, height) = CustomMapCustomSpriteMetadata.ResolveWorldDimensions(32, 16, 2f, configuration);
        Assert.Equal(200f, width);
        Assert.Equal(100f, height);
    }

    [Fact]
    public void ResolveAnchorTopLeft_PlacesReferenceTileAtRequestedCorner()
    {
        var (anchorX, anchorY) = MapSpriteTileMetadata.ResolveAnchorTopLeft(
            10f,
            20f,
            100f,
            60f,
            20f,
            10f,
            MapSpriteTileAnchor.BottomRight);

        Assert.Equal(90f, anchorX);
        Assert.Equal(70f, anchorY);
    }

    [Fact]
    public void ImporterUsesTileAreaDimensionsForCustomSprite()
    {
        var roomObjects = new List<RoomObjectMarker>();
        var resources = new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase)
        {
            ["tile"] = new CustomMapBuilderResource(
                "tile",
                string.Empty,
                CustomMapBuilderResourceKind.CustomSprite,
                CreatePng(16, 16)),
        };
        var context = new CustomMapEntityImportContext
        {
            RoomObjects = roomObjects,
            Resources = resources,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            CustomMapCustomSpriteMetadata.CustomSpriteEntityType,
            50f,
            50f,
            1f,
            1f,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [CustomMapCustomSpriteMetadata.ImagePropertyKey] = "tile",
                [CustomMapCustomSpriteMetadata.ScalePropertyKey] = "2",
                [MapSpriteTileMetadata.TilePropertyKey] = "true",
                [MapSpriteTileMetadata.TileAnchorPropertyKey] = "topRight",
                [MapSpriteTileMetadata.TileAreaWidthPropertyKey] = "96",
                [MapSpriteTileMetadata.TileAreaHeightPropertyKey] = "64",
            },
            context));

        var marker = Assert.Single(roomObjects);
        Assert.True(marker.CustomMapSprite.Tile);
        Assert.Equal(MapSpriteTileAnchor.TopRight, marker.CustomMapSprite.TileAnchor);
        Assert.Equal(96f, marker.Width);
        Assert.Equal(64f, marker.Height);
        Assert.Equal(2f, marker.CustomMapSprite.Scale);
    }

    [Fact]
    public void ShouldShowTileDependentProperty_HidesTileAnchorWhenTileDisabled()
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [MapSpriteTileMetadata.TilePropertyKey] = "false",
        };

        Assert.False(MapSpriteTileMetadata.ShouldShowTileDependentProperty(
            properties,
            MapSpriteTileMetadata.TileAnchorPropertyKey));
        Assert.True(MapSpriteTileMetadata.ShouldShowTileDependentProperty(
            properties,
            MapSpriteTileMetadata.TilePropertyKey));
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        using var stream = new MemoryStream();
        image.Save(stream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
        return stream.ToArray();
    }
}
