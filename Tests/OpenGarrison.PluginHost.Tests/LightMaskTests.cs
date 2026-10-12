using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LightMaskTests
{
    [Fact]
    public void MaskPropertyParsesAndStaysOptional()
    {
        var plain = MapLightMetadata.FromProperties(0f, 0f, new Dictionary<string, string>());
        Assert.Null(plain.Mask);

        var empty = MapLightMetadata.FromProperties(0f, 0f, new Dictionary<string, string> { ["mask"] = "  " });
        Assert.Null(empty.Mask);

        var masked = MapLightMetadata.FromProperties(10f, 20f, new Dictionary<string, string> { ["mask"] = " window " });
        Assert.Equal("window", masked.Mask);
        Assert.Equal("window", masked.Scale(2f).Mask);
    }

    [Fact]
    public void SolidPixelsPassLightAndTransparentOnesBlockIt()
    {
        // Black on transparent: alpha decides.
        var pixels = new[] { new XnaColor(0, 0, 0, 255), new XnaColor(0, 0, 0, 0), new XnaColor(0, 0, 0, 128) };
        var mask = LightMaskTextureCache.BuildMaskPixels(pixels);
        Assert.Equal(new XnaColor(255, 255, 255, 255), mask[0]);
        Assert.Equal(new XnaColor(0, 0, 0, 0), mask[1]);
        Assert.Equal(128, mask[2].A);
        Assert.Equal(128, mask[2].R);

        // No transparency at all: brightness decides (white lit, black dark).
        var opaque = LightMaskTextureCache.BuildMaskPixels(new[] { XnaColor.White, XnaColor.Black });
        Assert.Equal(255, opaque[0].A);
        Assert.Equal(0, opaque[1].A);
    }
}
