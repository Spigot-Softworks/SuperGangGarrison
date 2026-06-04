using OpenGarrison.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CustomMapModernEntityRoundTripTests
{
    [Fact]
    public void ExportAndRuntimeImportModernSpawnRoundTrip()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "opengarrison-modern-map-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var backgroundPath = Path.Combine(tempDirectory, "bg.png");
            WriteSolidPng(backgroundPath, 64, 64, new Rgba32(40, 60, 80, 255));
            var walkmaskPath = Path.Combine(tempDirectory, "wm.png");
            WriteWalkmaskPng(walkmaskPath);
            var outputPath = Path.Combine(tempDirectory, "modern_map.png");

            var document = CustomMapBuilderDocument.CreateEmpty("modern_map") with
            {
                BackgroundImagePath = backgroundPath,
                WalkmaskImagePath = walkmaskPath,
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [CustomMapEntityRuntimeRegistry.EntitySchemaMetadataKey] = CustomMapEntityRuntimeRegistry.ModernEntitySchemaValue,
                },
                Entities =
                [
                    CustomMapBuilderEntity.Create("spawn", 12f, 18f, new Dictionary<string, string> { ["team"] = "red", ["forward"] = "false" }),
                    CustomMapBuilderEntity.Create("spawn", 40f, 18f, new Dictionary<string, string> { ["team"] = "blue", ["forward"] = "false" }),
                    CustomMapBuilderEntity.Create("controlPoint", 24f, 24f, new Dictionary<string, string> { ["index"] = "1" }),
                ],
            };

            CustomMapPngExporter.Export(document.NormalizeForEditing(), outputPath);
            var imported = CustomMapPngImporter.Import(outputPath);
            Assert.NotNull(imported);
            Assert.Single(imported!.Room.RedSpawns);
            Assert.Single(imported.Room.BlueSpawns);
            Assert.Contains(imported.Room.RoomObjects, marker => marker.Type == RoomObjectType.ControlPoint);
            Assert.DoesNotContain("spawn", imported.Room.UnsupportedEntities);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static void WriteWalkmaskPng(string path)
    {
        using var image = new Image<Rgba32>(4, 2);
        image[0, 0] = new Rgba32(255, 255, 255, 255);
        image[1, 0] = new Rgba32(255, 255, 255, 255);
        image[3, 1] = new Rgba32(255, 255, 255, 255);
        image.SaveAsPng(path);
    }

    private static void WriteSolidPng(string path, int width, int height, Rgba32 color)
    {
        using var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y += 1)
        {
            for (var x = 0; x < width; x += 1)
            {
                image[x, y] = color;
            }
        }

        image.SaveAsPng(path);
    }
}
