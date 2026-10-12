using OpenGarrison.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Gg2ForegroundImportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gg2-foreground-" + Guid.NewGuid().ToString("N"));

    public Gg2ForegroundImportTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void AnimatedForegroundScaleImportsTopLeftAtEntityScale()
    {
        // Like dm_fortress's grass: a 15x12 animated GIF, scale 3, animationspeed 0.3, fade false.
        var gif = Path.Combine(_root, "grs1.gif");
        using (var image = new Image<Rgba32>(15, 12, new Rgba32(0, 255, 0, 255)))
        {
            using var second = new Image<Rgba32>(15, 12, new Rgba32(0, 128, 0, 255));
            image.Frames.AddFrame(second.Frames[0]);
            image.SaveAsGif(gif);
        }

        var roomObjects = new List<RoomObjectMarker>();
        var context = new CustomMapEntityImportContext
        {
            RoomObjects = roomObjects,
            Resources = new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase)
            {
                ["grs1"] = CustomMapBuilderResourceCodec.FromFile("grs1", gif),
            },
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            "foreground_scale",
            3138f,
            1188f,
            3f,
            3f,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["resource"] = "grs1",
                ["scale"] = "3",
                ["xscale"] = "3",
                ["yscale"] = "3",
                ["animationspeed"] = "0.3",
                ["fade"] = "false",
            },
            context));

        var marker = Assert.Single(roomObjects);
        Assert.Equal(RoomObjectType.ForegroundSprite, marker.Type);
        Assert.Equal(3138f, marker.X);
        Assert.Equal(1188f, marker.Y);
        Assert.Equal(45f, marker.Width);
        Assert.Equal(36f, marker.Height);
        Assert.Equal("grs1", marker.ForegroundSprite.ImageResourceName);
        Assert.Equal(ForegroundSpriteLayerKind.Fg, marker.ForegroundSprite.Layer);
        Assert.Equal(9f, marker.ForegroundSprite.AnimationFramesPerSecond!.Value, 3);
        Assert.True(marker.ForegroundSprite.NoCoverFade);
    }

    [Fact]
    public void ForegroundWithoutResolvableResourceIsNotImported()
    {
        var context = new CustomMapEntityImportContext { RoomObjects = [] };
        Assert.False(CustomMapEntityRuntimeRegistry.TryImport(
            "foreground",
            0f,
            0f,
            1f,
            1f,
            new Dictionary<string, string> { ["resource"] = "missing" },
            context));
        Assert.Empty(context.RoomObjects);
    }
}
