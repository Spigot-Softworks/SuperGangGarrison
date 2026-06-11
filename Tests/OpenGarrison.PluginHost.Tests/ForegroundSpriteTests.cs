using System.Collections.Generic;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ForegroundSpriteTests
{
    [Fact]
    public void ImporterCreatesCenterAnchoredForegroundSpriteRoomObject()
    {
        var roomObjects = new List<RoomObjectMarker>();
        var resources = new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase)
        {
            ["bush"] = new CustomMapBuilderResource(
                "bush",
                string.Empty,
                CustomMapBuilderResourceKind.CustomSprite,
                CreatePng(40, 20)),
        };
        var context = new CustomMapEntityImportContext
        {
            RoomObjects = roomObjects,
            Resources = resources,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            ForegroundSpriteMetadata.ForegroundSpriteEntityType,
            120f,
            90f,
            1f,
            1f,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ForegroundSpriteMetadata.ImagePropertyKey] = "bush",
                [ForegroundSpriteMetadata.LayerPropertyKey] = "fg",
                [ForegroundSpriteMetadata.RelativeZPropertyKey] = "3",
                [ForegroundSpriteMetadata.ScalePropertyKey] = "2",
                [ForegroundSpriteMetadata.JunglePropertyKey] = "true",
                [ForegroundSpriteMetadata.BoundaryPropertyKey] = "pixel",
            },
            context));

        var marker = Assert.Single(roomObjects);
        Assert.Equal(RoomObjectType.ForegroundSprite, marker.Type);
        Assert.Equal(ForegroundSpriteLayerKind.Fg, marker.ForegroundSprite.Layer);
        Assert.Equal(3, marker.ForegroundSprite.RelativeZ);
        Assert.True(marker.ForegroundSprite.Jungle);
        Assert.Equal(80f, marker.Width);
        Assert.Equal(40f, marker.Height);
        Assert.Equal(120f, marker.CenterX, precision: 2);
        Assert.Equal(90f, marker.CenterY, precision: 2);
    }

    [Fact]
    public void IsPlayerInside_UsesBoxBoundsByDefault()
    {
        var marker = new RoomObjectMarker(
            RoomObjectType.ForegroundSprite,
            10f,
            20f,
            30f,
            40f,
            string.Empty,
            ForegroundSprite: new ForegroundSpriteConfiguration(
                "bush",
                ForegroundSpriteLayerKind.Bg,
                0,
                1f,
                true,
                1f,
                0.35f,
                ForegroundSpriteBoundaryKind.Box,
                false,
                MapSpriteTileAnchor.TopLeft,
                0f,
                0f));

        Assert.True(ForegroundSpriteMetadata.IsPlayerInside(marker, 25f, 40f, ForegroundSpriteBoundaryKind.Box, null));
        Assert.False(ForegroundSpriteMetadata.IsPlayerInside(marker, 5f, 40f, ForegroundSpriteBoundaryKind.Box, null));
    }

    [Fact]
    public void ShouldShowJungleDependentProperty_HidesRowsWhenJungleDisabled()
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ForegroundSpriteMetadata.JunglePropertyKey] = "false",
        };

        Assert.False(ForegroundSpriteMetadata.ShouldShowJungleDependentProperty(properties, ForegroundSpriteMetadata.OutsideOpacityPropertyKey));
        Assert.True(ForegroundSpriteMetadata.ShouldShowJungleDependentProperty(properties, ForegroundSpriteMetadata.LayerPropertyKey));
    }

    [Fact]
    public void ApplyUniformScale_PreservesForegroundSpriteConfiguration()
    {
        var configuration = new ForegroundSpriteConfiguration(
            "bush",
            ForegroundSpriteLayerKind.Fg,
            4,
            1.5f,
            true,
            0.8f,
            0.25f,
            ForegroundSpriteBoundaryKind.Box,
            true,
            MapSpriteTileAnchor.TopRight,
            96f,
            64f);
        var level = new SimpleLevel(
            "scaled",
            GameModeKind.TeamDeathmatch,
            new WorldBounds(800f, 600f),
            1f,
            string.Empty,
            1,
            1,
            new SpawnPoint(100f, 100f),
            [new SpawnPoint(100f, 100f)],
            [new SpawnPoint(700f, 100f)],
            [],
            [
                new RoomObjectMarker(
                    RoomObjectType.ForegroundSprite,
                    40f,
                    50f,
                    96f,
                    64f,
                    string.Empty,
                    ForegroundSprite: configuration),
            ],
            500f,
            [],
            importedFromSource: true,
            customMapVisuals: CustomMapVisualMetadata.Empty);

        var scaled = SimpleLevelScaling.ApplyUniformScale(level, 2f);
        var marker = Assert.Single(scaled.RoomObjects);
        Assert.Equal("bush", marker.ForegroundSprite.ImageResourceName);
        Assert.Equal(ForegroundSpriteLayerKind.Fg, marker.ForegroundSprite.Layer);
        Assert.Equal(4, marker.ForegroundSprite.RelativeZ);
        Assert.True(marker.ForegroundSprite.Tile);
        Assert.Equal(192f, marker.Width);
        Assert.Equal(128f, marker.Height);
    }

    [Fact]
    public void IsPlayerInsideWithExtensions_UsesLinkedAreaZones()
    {
        var roomObjects = new List<RoomObjectMarker>
        {
            new(
                RoomObjectType.ForegroundSprite,
                10f,
                20f,
                20f,
                20f,
                string.Empty,
                ForegroundSprite: new ForegroundSpriteConfiguration(
                    "bush",
                    ForegroundSpriteLayerKind.Bg,
                    0,
                    1f,
                    true,
                    1f,
                    0.35f,
                    ForegroundSpriteBoundaryKind.Box,
                    false,
                    MapSpriteTileAnchor.TopLeft,
                    0f,
                    0f)),
            new(
                RoomObjectType.AreaExtension,
                100f,
                100f,
                30f,
                30f,
                string.Empty,
                AreaExtension: new AreaExtensionConfiguration(0, AreaExtensionKind.ForegroundSprite)),
        };

        Assert.False(ForegroundSpriteMetadata.IsPlayerInsideWithExtensions(
            roomObjects,
            0,
            50f,
            50f,
            ForegroundSpriteBoundaryKind.Box,
            null,
            _ => true));
        Assert.True(ForegroundSpriteMetadata.IsPlayerInsideWithExtensions(
            roomObjects,
            0,
            15f,
            25f,
            ForegroundSpriteBoundaryKind.Box,
            null,
            _ => true));
        Assert.True(ForegroundSpriteMetadata.IsPlayerInsideWithExtensions(
            roomObjects,
            0,
            110f,
            110f,
            ForegroundSpriteBoundaryKind.Box,
            null,
            _ => true));
    }

    [Fact]
    public void ActivatorImporterResolvesForegroundSpriteTarget()
    {
        var roomObjects = new List<RoomObjectMarker>
        {
            new(
                RoomObjectType.ForegroundSprite,
                30f,
                40f,
                64f,
                32f,
                string.Empty,
                ForegroundSprite: new ForegroundSpriteConfiguration(
                    "bush",
                    ForegroundSpriteLayerKind.Fg,
                    0,
                    1f,
                    false,
                    1f,
                    0.35f,
                    ForegroundSpriteBoundaryKind.Box,
                    false,
                    MapSpriteTileAnchor.TopLeft,
                    0f,
                    0f)),
        };
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
                    [MapLogicMetadata.ActivatorEntityPropertyKey] = MapLogicEntityReference.FormatEntityRef(
                        ForegroundSpriteMetadata.ForegroundSpriteEntityType,
                        62f,
                        56f),
                    [MapLogicMetadata.ActivatorBehaviorPropertyKey] = "disable",
                    [MapLogicMetadata.LogicInputPropertyKey] = "node:gate",
                }),
        };

        var activators = MapLogicActivatorImporter.BuildFromEntities(entities, roomObjects, graph);
        var activator = Assert.Single(activators.Activators);
        Assert.Equal(0, activator.TargetRoomObjectIndex);
    }

    [Fact]
    public void AreaImporterLinksExtensionToForegroundSprite()
    {
        var roomObjects = new List<RoomObjectMarker>
        {
            new(
                RoomObjectType.ForegroundSprite,
                10f,
                20f,
                40f,
                20f,
                string.Empty,
                ForegroundSprite: new ForegroundSpriteConfiguration(
                    "bush",
                    ForegroundSpriteLayerKind.Bg,
                    0,
                    1f,
                    true,
                    1f,
                    0.35f,
                    ForegroundSpriteBoundaryKind.Box,
                    false,
                    MapSpriteTileAnchor.TopLeft,
                    0f,
                    0f)),
        };
        var context = new CustomMapEntityImportContext
        {
            RoomObjects = roomObjects,
            UseCenterOrigin = true,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            AreaExtensionMetadata.AreaEntityType,
            120f,
            90f,
            2f,
            1f,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AreaExtensionMetadata.ExtendsPropertyKey] = MapLogicEntityReference.FormatEntityRef(
                    ForegroundSpriteMetadata.ForegroundSpriteEntityType,
                    30f,
                    30f),
            },
            context));

        Assert.Equal(2, roomObjects.Count);
        Assert.Equal(RoomObjectType.AreaExtension, roomObjects[1].Type);
        Assert.Equal(AreaExtensionKind.ForegroundSprite, roomObjects[1].AreaExtension.Kind);
        Assert.Equal(0, roomObjects[1].AreaExtension.ParentRoomObjectIndex);
    }

    [Fact]
    public void JungleReplicatedState_CanBeSetAndClearedOnPlayer()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Soldier, "Tester");
        player.TeleportTo(50f, 50f);

        Assert.True(player.SetReplicatedStateBool(
            ForegroundSpriteMetadata.JungleReplicatedStateOwnerId,
            ForegroundSpriteMetadata.JungleReplicatedStateKey(0),
            true));
        Assert.True(player.TryGetReplicatedStateBool(
            ForegroundSpriteMetadata.JungleReplicatedStateOwnerId,
            ForegroundSpriteMetadata.JungleReplicatedStateKey(0),
            out var inside));
        Assert.True(inside);

        Assert.True(player.ClearReplicatedState(
            ForegroundSpriteMetadata.JungleReplicatedStateOwnerId,
            ForegroundSpriteMetadata.JungleReplicatedStateKey(0)));
        Assert.False(player.TryGetReplicatedStateBool(
            ForegroundSpriteMetadata.JungleReplicatedStateOwnerId,
            ForegroundSpriteMetadata.JungleReplicatedStateKey(0),
            out _));
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        using var stream = new MemoryStream();
        image.Save(stream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
        return stream.ToArray();
    }
}
