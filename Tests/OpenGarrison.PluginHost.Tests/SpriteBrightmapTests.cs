using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

/// <summary>Character brightmaps: baking the glow image and borrowing brightmaps across frames.</summary>
public sealed class SpriteBrightmapTests
{
    private static Color[] Solid(int width, int height, Color color, params (int X, int Y)[] holes)
    {
        var pixels = new Color[width * height];
        Array.Fill(pixels, color);
        foreach (var (x, y) in holes)
        {
            pixels[(y * width) + x] = Color.Transparent;
        }

        return pixels;
    }

    [Fact]
    public void GlowIsTheSpriteMultipliedByTheBrightmap()
    {
        var frame = Solid(2, 1, new Color(200, 100, 50, 255));
        var mask = new[] { new Color(255, 255, 255, 255), new Color(128, 128, 128, 255) };
        var glow = SpriteBrightmapLibrary.BakeGlow(frame, 2, 1, mask, 2, 1, 1);

        Assert.NotNull(glow);
        Assert.Equal(new Color(200, 100, 50, 255), glow![0]);
        Assert.Equal(new Color(100, 50, 25, 255), glow[1]);
    }

    [Fact]
    public void HalfSizeBrightmapsCoverTheirFrameInBlocks()
    {
        Assert.True(SpriteBrightmapLibrary.TryGetUpscale(64, 64, 32, 32, out var upscale));
        Assert.Equal(2, upscale);
        Assert.True(SpriteBrightmapLibrary.TryGetUpscale(96, 64, 48, 32, out upscale));
        Assert.Equal(2, upscale);
        Assert.False(SpriteBrightmapLibrary.TryGetUpscale(64, 64, 48, 32, out _));
        Assert.False(SpriteBrightmapLibrary.TryGetUpscale(64, 64, 30, 30, out _));

        var frame = Solid(4, 2, Color.White);
        var mask = new[] { Color.Black, Color.White };
        var glow = SpriteBrightmapLibrary.BakeGlow(frame, 4, 2, mask, 2, 1, 2)!;
        Assert.Equal(Color.Black, new Color(glow[1].R, glow[1].G, glow[1].B));
        Assert.Equal(Color.White, glow[2]);
        Assert.Equal(Color.White, glow[7]);
    }

    [Fact]
    public void AllBlackBrightmapsAndTransparentPixelsAddNothing()
    {
        var frame = Solid(2, 1, Color.White, (1, 0));
        Assert.Null(SpriteBrightmapLibrary.BakeGlow(frame, 2, 1, new[] { Color.Black, Color.White }, 2, 1, 1));
        Assert.Null(SpriteBrightmapLibrary.BakeGlow(frame, 2, 1, new[] { Color.Transparent, Color.Transparent }, 2, 1, 1));
    }

    [Fact]
    public void FramesBorrowABrightmapFromTheSameSilhouetteMovedAFewPixels()
    {
        // A 1x2 block at (1,1) in the donor, at (1,0) in the frame: moved up one pixel.
        var donor = new Color[16];
        donor[(1 * 4) + 1] = Color.White;
        donor[(2 * 4) + 1] = Color.White;
        var moved = new Color[16];
        moved[(0 * 4) + 1] = Color.White;
        moved[(1 * 4) + 1] = Color.White;

        Assert.True(SpriteBrightmapLibrary.TryFindSilhouetteShift(donor, donor, 4, 4, out var shiftX, out var shiftY));
        Assert.Equal((0, 0), (shiftX, shiftY));
        Assert.True(SpriteBrightmapLibrary.TryFindSilhouetteShift(donor, moved, 4, 4, out shiftX, out shiftY));
        Assert.Equal((0, -1), (shiftX, shiftY));

        // The borrowed brightmap moves with it.
        var mask = new Color[16];
        mask[(1 * 4) + 1] = Color.White;
        var glow = SpriteBrightmapLibrary.BakeGlow(moved, 4, 4, mask, 4, 4, 1, shiftX, shiftY)!;
        Assert.Equal(Color.White, glow[(0 * 4) + 1]);
        Assert.Equal(0, glow[(1 * 4) + 1].R);

        var different = new Color[16];
        different[0] = Color.White;
        Assert.False(SpriteBrightmapLibrary.TryFindSilhouetteShift(donor, different, 4, 4, out _, out _));
        Assert.False(SpriteBrightmapLibrary.TryFindSilhouetteShift(donor, new Color[16], 4, 4, out _, out _));
    }

    [Fact]
    public void BrightmapsSettingDefaultsOnAndRoundTrips()
    {
        Assert.True(new OpenGarrisonPreferencesDocument().CharacterBrightmapsEnabled);
        var path = Path.Combine(Path.GetTempPath(), $"og-brightmaps-{Guid.NewGuid():N}.ini");
        try
        {
            new OpenGarrisonPreferencesDocument { CharacterBrightmapsEnabled = false }.Save(path);
            Assert.False(OpenGarrisonPreferencesDocument.Load(path).CharacterBrightmapsEnabled);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
