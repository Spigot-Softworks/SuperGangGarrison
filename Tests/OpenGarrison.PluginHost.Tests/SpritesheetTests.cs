using System.Collections.Generic;
using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SpritesheetTests
{
    [Fact]
    public void EnsurePlacementDefaultsUsesMapVisualScaleAndAnimationDefaults()
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SpritesheetMetadata.EnsurePlacementDefaults(properties, 1.5f);
        Assert.Equal("1.5", properties[SpritesheetMetadata.ScalePropertyKey]);
        Assert.Equal("true", properties[SpritesheetMetadata.AutostartPropertyKey]);
        Assert.Equal("true", properties[SpritesheetMetadata.AutoplayPropertyKey]);
        Assert.Equal("10", properties[SpritesheetMetadata.FrameratePropertyKey]);
        Assert.Equal(SpritesheetMetadata.LoopingModeLoopValue, properties[SpritesheetMetadata.LoopingModePropertyKey]);
        Assert.Equal(string.Empty, properties[SpritesheetMetadata.StartInputPropertyKey]);
        Assert.Equal(string.Empty, properties[SpritesheetMetadata.StopInputPropertyKey]);
        Assert.Equal(string.Empty, properties[SpritesheetMetadata.NextFrameInputPropertyKey]);
    }

    [Fact]
    public void ShouldShowPropertyHidesConditionalRows()
    {
        var autostartOn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SpritesheetMetadata.AutostartPropertyKey] = "true",
            [SpritesheetMetadata.AutoplayPropertyKey] = "true",
        };
        Assert.False(SpritesheetMetadata.ShouldShowProperty(autostartOn, SpritesheetMetadata.StartInputPropertyKey));
        Assert.False(SpritesheetMetadata.ShouldShowProperty(autostartOn, SpritesheetMetadata.NextFrameInputPropertyKey));
        Assert.True(SpritesheetMetadata.ShouldShowProperty(autostartOn, SpritesheetMetadata.FrameratePropertyKey));

        var manual = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SpritesheetMetadata.AutostartPropertyKey] = "false",
            [SpritesheetMetadata.AutoplayPropertyKey] = "false",
        };
        Assert.True(SpritesheetMetadata.ShouldShowProperty(manual, SpritesheetMetadata.StartInputPropertyKey));
        Assert.True(SpritesheetMetadata.ShouldShowProperty(manual, SpritesheetMetadata.NextFrameInputPropertyKey));
        Assert.False(SpritesheetMetadata.ShouldShowProperty(manual, SpritesheetMetadata.FrameratePropertyKey));
    }

    [Fact]
    public void ResolveFrameSourceRectangleUsesGridDimensions()
    {
        var configuration = new SpritesheetConfiguration(
            "sheet",
            CustomMapSpriteLayerKind.Bg,
            0,
            1f,
            4,
            2,
            true,
            string.Empty,
            string.Empty,
            true,
            string.Empty,
            10,
            SpritesheetLoopingMode.Loop);
        var frame3 = SpritesheetMetadata.ResolveFrameSourceRectangle(128, 64, 3, configuration);
        Assert.Equal(96, frame3.X);
        Assert.Equal(0, frame3.Y);
        Assert.Equal(32, frame3.Width);
        Assert.Equal(32, frame3.Height);

        var frame5 = SpritesheetMetadata.ResolveFrameSourceRectangle(128, 64, 5, configuration);
        Assert.Equal(32, frame5.X);
        Assert.Equal(32, frame5.Y);
    }

    [Fact]
    public void ImporterCreatesCenterAnchoredSpritesheetRoomObject()
    {
        var roomObjects = new List<RoomObjectMarker>();
        var resources = new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase)
        {
            ["sheet"] = new CustomMapBuilderResource(
                "sheet",
                string.Empty,
                CustomMapBuilderResourceKind.CustomSprite,
                CreatePng(64, 32)),
        };
        var context = new CustomMapEntityImportContext
        {
            RoomObjects = roomObjects,
            Resources = resources,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            SpritesheetMetadata.SpritesheetEntityType,
            120f,
            90f,
            1f,
            1f,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [SpritesheetMetadata.ImagePropertyKey] = "sheet",
                [SpritesheetMetadata.LayerPropertyKey] = "fg",
                [SpritesheetMetadata.ZOrderPropertyKey] = "3",
                [SpritesheetMetadata.ScalePropertyKey] = "2",
                [SpritesheetMetadata.ColumnsPropertyKey] = "4",
                [SpritesheetMetadata.RowsPropertyKey] = "2",
                [SpritesheetMetadata.AutostartPropertyKey] = "false",
                [SpritesheetMetadata.StartInputPropertyKey] = "node:start",
                [SpritesheetMetadata.LoopingModePropertyKey] = SpritesheetMetadata.LoopingModeReverseValue,
            },
            context));

        var marker = Assert.Single(roomObjects);
        Assert.Equal(RoomObjectType.Spritesheet, marker.Type);
        Assert.Equal(CustomMapSpriteLayerKind.Fg, marker.Spritesheet.Layer);
        Assert.Equal(3, marker.Spritesheet.ZOrder);
        Assert.Equal(32f, marker.Width);
        Assert.Equal(32f, marker.Height);
        Assert.Equal(120f, marker.CenterX, precision: 2);
        Assert.Equal(90f, marker.CenterY, precision: 2);
        Assert.Equal(4, marker.Spritesheet.Columns);
        Assert.Equal(2, marker.Spritesheet.Rows);
        Assert.False(marker.Spritesheet.Autostart);
        Assert.Equal(SpritesheetLoopingMode.Reverse, marker.Spritesheet.LoopingMode);
    }

    [Fact]
    public void PlaybackImporterResolvesLogicInputs()
    {
        var roomObjects = new List<RoomObjectMarker>
        {
            new(
                RoomObjectType.Spritesheet,
                10f,
                20f,
                16f,
                16f,
                string.Empty,
                SourceName: SpritesheetMetadata.SpritesheetEntityType,
                Spritesheet: new SpritesheetConfiguration(
                    "sheet",
                    CustomMapSpriteLayerKind.Bg,
                    0,
                    1f,
                    2,
                    2,
                    false,
                    "node:start",
                    "node:stop",
                    false,
                    "node:next",
                    12,
                    SpritesheetLoopingMode.Loop)),
        };
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition { LogicKey = "start", Kind = MapLogicNodeKind.Latch },
            new MapLogicNodeDefinition { LogicKey = "stop", Kind = MapLogicNodeKind.Latch },
            new MapLogicNodeDefinition { LogicKey = "next", Kind = MapLogicNodeKind.Latch },
        ]);

        var playbackSet = SpritesheetPlaybackImporter.BuildFromRoomObjects(roomObjects, graph);
        var entry = Assert.Single(playbackSet.Entries);
        Assert.Equal(0, entry.RoomObjectIndex);
        Assert.Equal(0, entry.StartInputNodeIndex);
        Assert.Equal(1, entry.StopInputNodeIndex);
        Assert.Equal(2, entry.NextFrameInputNodeIndex);
        Assert.False(entry.Configuration.Autostart);
        Assert.False(entry.Configuration.Autoplay);
    }

    [Fact]
    public void AutoplayAdvancesDuringFullSimulationTickWithoutLogicTimers()
    {
        var configuration = new SpritesheetConfiguration(
            "sheet",
            CustomMapSpriteLayerKind.Bg,
            0,
            1f,
            2,
            2,
            true,
            string.Empty,
            string.Empty,
            true,
            string.Empty,
            10,
            SpritesheetLoopingMode.Loop);
        var marker = new RoomObjectMarker(
            RoomObjectType.Spritesheet,
            32f,
            32f,
            16f,
            16f,
            string.Empty,
            SourceName: SpritesheetMetadata.SpritesheetEntityType,
            Spritesheet: configuration);
        var playbackSet = new SpritesheetPlaybackSet(
        [
            new SpritesheetPlaybackEntry(0, -1, -1, -1, configuration),
        ]);
        var world = new SimulationWorld { ClientPredictionMode = false };
        var setLevel = typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setLevel);
        setLevel.Invoke(
            world,
            [
                new SimpleLevel(
                    "spritesheet-autoplay-test",
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
                    [marker],
                    0f,
                    [],
                    importedFromSource: false,
                    spritesheetPlaybackSet: playbackSet),
            ]);

        Assert.Equal(0, world.GetSpritesheetFrame(0));
        Assert.False(world.Level.LogicGraph.HasTimers);
        Assert.False(world.Level.LogicGraph.HasOscillators);

        for (var tick = 0; tick < 30; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.GetSpritesheetFrame(0) > 0);
    }

    [Fact]
    public void AutoplayAdvancesDuringClientPredictionTickWithoutLogicTimers()
    {
        var configuration = new SpritesheetConfiguration(
            "sheet",
            CustomMapSpriteLayerKind.Bg,
            0,
            1f,
            2,
            2,
            true,
            string.Empty,
            string.Empty,
            true,
            string.Empty,
            10,
            SpritesheetLoopingMode.Loop);
        var marker = new RoomObjectMarker(
            RoomObjectType.Spritesheet,
            32f,
            32f,
            16f,
            16f,
            string.Empty,
            SourceName: SpritesheetMetadata.SpritesheetEntityType,
            Spritesheet: configuration);
        var playbackSet = new SpritesheetPlaybackSet(
        [
            new SpritesheetPlaybackEntry(0, -1, -1, -1, configuration),
        ]);
        var world = new SimulationWorld { ClientPredictionMode = true };
        var setLevel = typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setLevel);
        setLevel.Invoke(
            world,
            [
                new SimpleLevel(
                    "spritesheet-client-autoplay-test",
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
                    [marker],
                    0f,
                    [],
                    importedFromSource: false,
                    spritesheetPlaybackSet: playbackSet),
            ]);

        for (var tick = 0; tick < 30; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.GetSpritesheetFrame(0) > 0);
    }

    [Fact]
    public void LoopingModeCyclesThroughAllModes()
    {
        Assert.Equal(
            SpritesheetMetadata.LoopingModeReverseValue,
            SpritesheetMetadata.CycleLoopingModePropertyValue(SpritesheetMetadata.LoopingModeLoopValue));
        Assert.Equal(
            SpritesheetMetadata.LoopingModePlayOnceValue,
            SpritesheetMetadata.CycleLoopingModePropertyValue(SpritesheetMetadata.LoopingModeReverseValue));
        Assert.Equal(
            SpritesheetMetadata.LoopingModeLoopValue,
            SpritesheetMetadata.CycleLoopingModePropertyValue(SpritesheetMetadata.LoopingModePlayOnceValue));
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        using var stream = new MemoryStream();
        image.Save(stream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
        return stream.ToArray();
    }
}
