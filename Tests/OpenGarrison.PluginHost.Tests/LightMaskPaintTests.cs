using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Microsoft.Xna.Framework;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LightMaskPaintTests
{
    [Fact]
    public void ChangingMaskTypesRetainsThePaintedLayerAndBrushSize()
    {
        var properties = new Dictionary<string, string>
        {
            [MapLightMetadata.MaskKey] = "my_paint", [MapLightMetadata.MaskPixelSizeKey] = "2",
            [BuilderPaintedLightMask.BrushSizeKey] = "4",
        };
        BuilderPaintedLightMask.RememberActive(properties);
        properties[MapLightMetadata.MaskKey] = string.Empty;
        properties.Remove(MapLightMetadata.MaskPixelSizeKey);
        Assert.Equal(("my_paint", 2f), BuilderPaintedLightMask.GetLayer(properties));
        properties[MapLightMetadata.MaskKey] = "imported_png";
        BuilderPaintedLightMask.RememberActive(properties);
        Assert.Equal(("my_paint", 2f), BuilderPaintedLightMask.GetLayer(properties));
        Assert.Equal(4, BuilderPaintedLightMask.GetBrushSize(properties));
        properties[MapLightMetadata.MaskKey] = "new_paint";
        properties[MapLightMetadata.MaskPixelSizeKey] = "1";
        BuilderPaintedLightMask.RememberActive(properties);
        Assert.Equal(("new_paint", 1f), BuilderPaintedLightMask.GetLayer(properties));
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void BrushSizeChangesOnlyNewPixelsAndUndoPreservesEarlierPaint(int size)
    {
        var canvas = new LightMaskPaintCanvas(12, 12);
        canvas.PaintBrush(0, false, 2);
        canvas.CommitStroke();
        canvas.PaintBrush(6 * 12 + 6, false, size);
        canvas.CommitStroke();
        Assert.Equal(4 + size * size, canvas.Pixels.ToArray().Count(value => value != 0));
        Assert.True(canvas.Undo());
        Assert.Equal(4, canvas.Pixels.ToArray().Count(value => value != 0));
    }

    [Fact]
    public void BrushGroupsClipAtCanvasEdgesWithoutWrappingToOtherRows()
    {
        var canvas = new LightMaskPaintCanvas(5, 5);
        canvas.PaintBrush(4, false, 4);
        Assert.Equal(new[] { 4, 9, 14, 19 }, canvas.Pixels.ToArray().Select((value, index) => (value, index))
            .Where(pixel => pixel.value != 0).Select(pixel => pixel.index));
    }

    [Fact]
    public void PromotingAnOldTwoPixelMaskKeepsItsShapeAndWorldPosition()
    {
        var oldOrigin = LightmapRenderer.GetPaintedMaskOrigin(101, 99, 2, 2, 2);
        var unitOrigin = LightmapRenderer.GetPaintedMaskOrigin(101, 99, 4, 4, 1);
        var extraX = (int)(unitOrigin.X - oldOrigin.X) * 2;
        var extraY = (int)(unitOrigin.Y - oldOrigin.Y) * 2;
        var canvas = LightMaskPaintCanvas.ExpandSourcePixels(2, 2, [255, 0, 0, 128], 2, extraX, extraY);
        Assert.Equal(oldOrigin, LightmapRenderer.GetPaintedMaskOrigin(101, 99, canvas.Width, canvas.Height, 1));
        Assert.Equal(255, canvas.Pixels[0]); Assert.Equal(255, canvas.Pixels[1]);
        Assert.Equal(255, canvas.Pixels[canvas.Width]);
        Assert.Equal(128, canvas.Pixels[2 * canvas.Width + 2]);
        Assert.Equal(0, canvas.Pixels[canvas.Width - 1]);
    }

    [Theory]
    [InlineData(4, 4, 10)]
    [InlineData(5, 3, 10)]
    [InlineData(8, 4, 16)]
    public void IncreasingRadiusExpandsSavedPaintWithoutMovingItsWorldPixels(int width, int height, int side)
    {
        var pixels = new byte[width * height];
        pixels[0] = 128;
        pixels[^1] = 255;
        var saved = new LightMaskPaintCanvas(width, height, pixels);
        var expanded = saved.EnsureMinimumSize(side, side);
        var oldOrigin = LightmapRenderer.GetPaintedMaskOrigin(101, 99, width, height, 1);
        var newOrigin = LightmapRenderer.GetPaintedMaskOrigin(101, 99, expanded.Width, expanded.Height, 1);
        var left = (width < expanded.Width ? expanded.Width - width : 0) / 2;
        var top = (height < expanded.Height ? expanded.Height - height : 0) / 2;
        Assert.Equal(oldOrigin, newOrigin + new Vector2(left, top));
        Assert.Equal(128, expanded.Pixels[top * expanded.Width + left]);
        Assert.Equal(255, expanded.Pixels[(top + height - 1) * expanded.Width + left + width - 1]);
        Assert.Equal(2, expanded.Pixels.ToArray().Count(value => value != 0));
        Assert.Equal(0, expanded.Pixels[0]);
        Assert.True(expanded.Width >= side && expanded.Height >= side);
    }

    [Fact]
    public void ExpandingDraftPreservesUndoRedoAndPendingStrokes()
    {
        var canvas = new LightMaskPaintCanvas(4, 4);
        canvas.Paint(0, false); canvas.CommitStroke();
        canvas.Paint(15, false); canvas.CommitStroke();
        Assert.True(canvas.Undo());
        var expanded = canvas.EnsureMinimumSize(8, 8);
        Assert.Equal(255, expanded.Pixels[2 * 8 + 2]);
        Assert.Equal(0, expanded.Pixels[5 * 8 + 5]);
        Assert.True(expanded.Redo());
        Assert.Equal(255, expanded.Pixels[5 * 8 + 5]);
        Assert.True(expanded.Undo());
        Assert.True(expanded.Undo());
        Assert.DoesNotContain((byte)255, expanded.Pixels.ToArray());
        canvas.Paint(1, false); // An uncommitted stroke also keeps its coordinate.
        var pending = canvas.EnsureMinimumSize(8, 8);
        Assert.Equal(255, pending.Pixels[2 * 8 + 3]);
        Assert.True(pending.Undo());
        Assert.Equal(0, pending.Pixels[2 * 8 + 3]);
        Assert.Equal(255, pending.Pixels[2 * 8 + 2]);
    }

    [Fact]
    public void ReducingRadiusKeepsExistingCanvasAndPaint()
    {
        var canvas = new LightMaskPaintCanvas(8, 8);
        canvas.Paint(63, false);
        Assert.Same(canvas, canvas.EnsureMinimumSize(4, 4));
        Assert.Equal(255, canvas.Pixels[63]);
    }

    [Fact]
    public void ExistingMaskCanGrowToTheLargestSupportedLightRadius()
    {
        var canvas = new LightMaskPaintCanvas(4, 4, [255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128]);
        var side = (int)MathF.Ceiling(MapLightMetadata.MaxRadius) * 2;
        var expanded = canvas.EnsureMinimumSize(side, side);
        Assert.Equal(side, expanded.Width);
        Assert.Equal(side, expanded.Height);
        var padding = (side - 4) / 2;
        Assert.Equal(255, expanded.Pixels[padding * side + padding]);
        Assert.Equal(128, expanded.Pixels[(padding + 3) * side + padding + 3]);
    }

    [Theory]
    [InlineData(0, 4, new[] { 0, 1, 2, 3, 4 })]
    [InlineData(0, 20, new[] { 0, 5, 10, 15, 20 })]
    [InlineData(0, 24, new[] { 0, 6, 12, 18, 24 })]
    [InlineData(4, 20, new[] { 4, 8, 12, 16, 20 })]
    [InlineData(20, 4, new[] { 20, 16, 12, 8, 4 })]
    [InlineData(12, 12, new[] { 12 })]
    public void LinesIncludeBothEndpointsAndStayOnePixelWide(int start, int end, int[] expected)
        => Assert.Equal(expected, PixelPaintLine.Enumerate(start, end, 5));

    [Fact]
    public void DragSegmentsAreOneUndoableStrokeAndNewStrokesDiscardRedo()
    {
        var canvas = new LightMaskPaintCanvas(5, 5);
        canvas.PaintLine(0, 2, false);
        canvas.PaintLine(2, 4, false);
        canvas.CommitStroke();
        Assert.Equal(5, canvas.Pixels.ToArray().Count(pixel => pixel != 0));
        Assert.True(canvas.Undo());
        Assert.All(canvas.Pixels.ToArray(), pixel => Assert.Equal(0, pixel));
        Assert.True(canvas.Redo());
        Assert.Equal(5, canvas.Pixels.ToArray().Count(pixel => pixel != 0));
        canvas.PaintLine(0, 4, true);
        canvas.CommitStroke();
        Assert.True(canvas.Undo());
        canvas.Paint(24, false);
        canvas.CommitStroke();
        Assert.False(canvas.Redo());
    }

    [Fact]
    public void PaintingThenErasingWithinOneStrokeDoesNotCreateHistory()
    {
        var canvas = new LightMaskPaintCanvas(2, 2);
        canvas.Paint(1, false);
        canvas.Paint(1, true);
        canvas.CommitStroke();
        Assert.False(canvas.Undo());
    }

    [Fact]
    public void ReopenedPixelsAreCopiedAndInvalidEndpointsDoNotChangeCanvas()
    {
        byte[] pixels = [0, 255, 128, 0];
        var canvas = new LightMaskPaintCanvas(2, 2, pixels);
        pixels[1] = 0;
        Assert.Equal(255, canvas.Pixels[1]);
        Assert.False(canvas.PaintLine(-1, 3, false));
        Assert.False(canvas.PaintLine(0, 4, false));
        Assert.Equal(new byte[] { 0, 255, 128, 0 }, canvas.Pixels.ToArray());
    }

    [Theory]
    [InlineData(100, 100, 8, 8, 92, 92)]
    [InlineData(101, 99, 9, 7, 92, 92)]
    [InlineData(-1, -1, 2, 2, -4, -4)]
    public void PaintedMaskOriginIsOnTheTwoPixelGrid(float x, float y, int width, int height, float left, float top)
        => Assert.Equal(new Vector2(left, top), LightmapRenderer.GetPaintedMaskOrigin(x, y, width, height, 2));

    [Fact]
    public void PaintedPixelSizeSurvivesMapScalingAndEmission()
    {
        var light = MapLightMetadata.FromProperties(100, 100, new Dictionary<string, string>
        {
            [MapLightMetadata.MaskKey] = "painted_mask",
            [MapLightMetadata.MaskPixelSizeKey] = "2",
            [MapLightMetadata.DirectionKey] = "90",
        });
        var scaled = light.Scale(3);
        Assert.Equal(6, scaled.MaskPixelSize);
        var emitters = new List<MapLightEmitter>();
        MapLightEmission.Evaluate(scaled, 0, 0, emitters);
        Assert.Equal(6, Assert.Single(emitters).MaskPixelSize);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-2")]
    [InlineData("invalid")]
    public void InvalidPixelSizesKeepImportedMaskBehavior(string value)
    {
        var light = MapLightMetadata.FromProperties(0, 0, new Dictionary<string, string>
        {
            [MapLightMetadata.MaskKey] = "mask",
            [MapLightMetadata.MaskPixelSizeKey] = value,
        });
        Assert.Equal(0, light.MaskPixelSize);
    }

    [Theory]
    [InlineData("trigger")]
    [InlineData("cycle")]
    public void LogicReachesThirdColourAndReplicatesToClients(string action)
    {
        var graph = MapLogicGraphBuilder.Build([
            new MapLogicNodeDefinition { LogicKey = "switch", Kind = MapLogicNodeKind.CpTrigger,
                LinkedControlPointIndex = 1, OwnerRequirement = MapLogicCpTriggerOwnerRequirement.Red },
        ]);
        var point = new ControlPointState(1,
            new RoomObjectMarker(RoomObjectType.ControlPoint, 0, 0, 32, 32, "ControlPointRedS", PlayerTeam.Red, "controlPoint1"));
        var light = MapLightMetadata.FromProperties(0, 0, new Dictionary<string, string>
        {
            [MapLightMetadata.ColorKey] = "ff0000", [MapLightMetadata.AltColorKey] = "0000ff",
            [MapLightMetadata.TriggerColorKey] = "00ff00", [MapLightMetadata.ColorActionKey] = action,
            [MapLightMetadata.ColorInputKey] = "node:switch",
        });
        var lights = new[] { light };
        var server = new MapLightRuntimeState();
        server.Bind(lights, graph);
        var pulses = action == "cycle" ? 2 : 1;
        for (var i = 0; i < pulses; i++)
        {
            point.Team = PlayerTeam.Blue;
            graph.Evaluate([point]); server.Tick(lights, graph, 1f / 60);
            point.Team = PlayerTeam.Red;
            graph.Evaluate([point]); server.Tick(lights, graph, 1f / 60);
        }
        Assert.Equal(MapLightColorState.Trigger, server.GetSample(0, light.Style).ColorState);
        var client = new MapLightRuntimeState();
        client.Bind(lights, MapLogicGraph.Empty);
        client.ApplyNetworkState(0, server.GetNetworkState(0));
        var emitters = new List<MapLightEmitter>();
        MapLightEmission.Evaluate(light, 0, 0, client.GetSample(0, light.Style), MapLightMusic.Silent, emitters, beams: null);
        Assert.Equal(new MapLightingColor(0, 255, 0), Assert.Single(emitters).Color);
        point.Team = PlayerTeam.Blue;
        graph.Evaluate([point]); server.Tick(lights, graph, 1f / 60);
        Assert.Equal(action == "trigger" ? MapLightColorState.Normal : MapLightColorState.Trigger,
            server.GetSample(0, light.Style).ColorState);
    }
}
