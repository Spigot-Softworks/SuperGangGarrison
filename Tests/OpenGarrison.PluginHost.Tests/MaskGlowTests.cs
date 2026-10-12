using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MaskGlowTests
{
    [Theory]
    [InlineData(null, 0f)]
    [InlineData("bad", 0f)]
    [InlineData("NaN", 0f)]
    [InlineData("Infinity", 0f)]
    [InlineData("-20", 0f)]
    [InlineData("12.5", 12.5f)]
    [InlineData("500", 128f)]
    public void GlowDistanceIsOptionalFiniteAndBounded(string? value, float expected)
    {
        var properties = new Dictionary<string, string>();
        if (value is not null) properties[MapLightMetadata.MaskGlowRadiusKey] = value;
        var light = MapLightMetadata.FromProperties(0, 0, properties);
        Assert.Equal(expected, light.MaskGlowRadius);
        Assert.Equal(expected * 2f, light.Scale(2f).MaskGlowRadius);
    }

    [Theory]
    [InlineData("lamp")]
    [InlineData("spotlight")]
    public void MaskGlowFollowsEmittedMaskColourAndPower(string kind)
    {
        var light = MapLightMetadata.FromProperties(0, 0, new Dictionary<string, string>
        {
            [MapLightMetadata.KindKey] = kind,
            [MapLightMetadata.MaskKey] = "painted",
            [MapLightMetadata.MaskPixelSizeKey] = "1",
            [MapLightMetadata.MaskGlowRadiusKey] = "16",
            [MapLightMetadata.TriggerColorKey] = "00ff00",
            [MapLightMetadata.TransitionKey] = "fade",
        });
        var emitters = new List<MapLightEmitter>();
        var beams = new List<MapLightBeam>();
        MapLightEmission.Evaluate(light, 0, 0, new MapLightRuntimeSample(true, 0.5f, MapLightColorState.Trigger),
            default, emitters, beams);
        var emitter = Assert.Single(emitters);
        Assert.Equal(16f, emitter.MaskGlowRadius);
        Assert.Equal("painted", emitter.Mask);
        Assert.Equal(1f, emitter.MaskPixelSize);
        Assert.Equal(new MapLightingColor(0, 255, 0), emitter.Color);
        Assert.Equal(0.5f, emitter.Intensity);
        emitters.Clear();
        MapLightEmission.Evaluate(light, 0, 0, MapLightRuntimeSample.Off, default, emitters, beams);
        Assert.Empty(emitters);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(37)]
    [InlineData(128)]
    public void SinglePixelBlurHasContinuousSymmetricCoverageAndPreservesEnergy(int radius)
    {
        var pixels = new Dictionary<int, float> { [0] = 1f };
        foreach (var step in MaskGlowBuffer.GetBlurSteps(radius))
        {
            var next = new Dictionary<int, float>();
            foreach (var (position, strength) in pixels)
            {
                Add(position - (int)step, strength * 0.25f);
                Add(position, strength * 0.5f);
                Add(position + (int)step, strength * 0.25f);
            }
            pixels = next;
            void Add(int position, float strength) => next[position] = next.GetValueOrDefault(position) + strength;
        }
        Assert.Equal(1f, pixels.Values.Sum(), 5);
        Assert.Equal(-radius, pixels.Keys.Min());
        Assert.Equal(radius, pixels.Keys.Max());
        for (var x = 1; x <= radius; x++)
        {
            Assert.True(pixels.GetValueOrDefault(x) > 0f);
            Assert.Equal(pixels[-x], pixels[x]);
        }
    }

    [Fact]
    public void RotatedMasksAndOffscreenPixelsKeepTheirGlowBounds()
    {
        var bounds = MaskGlowBuffer.GetBounds(new Vector2(2, 50), new Vector2(80, 4), MathF.PI * 0.5f,
            16, Vector2.Zero, new Vector2(100), 1)!.Value;
        Assert.True(bounds.Origin.X < 0);
        Assert.True(bounds.Origin.Y < 10);
        Assert.True(bounds.Origin.Y + bounds.Height > 90);
        // A shape just outside the viewport still contributes; a distant one does not.
        Assert.NotNull(MaskGlowBuffer.GetBounds(new Vector2(-8, 50), new Vector2(2), 0, 16,
            Vector2.Zero, new Vector2(100), 1));
        Assert.Null(MaskGlowBuffer.GetBounds(new Vector2(-100, 50), new Vector2(2), 0, 16,
            Vector2.Zero, new Vector2(100), 1));
    }

    [Fact]
    public void LargePaintedCanvasUsesViewportSizedBuffersAndAnAlignedOrigin()
    {
        var bounds = MaskGlowBuffer.GetBounds(new Vector2(500), new Vector2(4000), 0, 128,
            new Vector2(17, 23), new Vector2(800, 600), 2)!.Value;
        Assert.InRange(bounds.Width, 1, 534);
        Assert.InRange(bounds.Height, 1, 434);
        Assert.Equal(0f, bounds.Origin.X % 2f);
        Assert.Equal(0f, bounds.Origin.Y % 2f);
    }
}
