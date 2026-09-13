using System.Text.Json;
using OpenGarrison.ClientShared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BrowserConsoleFontTests
{
    [Fact]
    public void BootstrapConsoleFontUsesUnscaledGameplayGlyphs()
    {
        const string imagePath = "Content/Sprites/Menu/Fonts/ConsoleFontAtlas.png";
        const string metadataPath = "Content/Sprites/Menu/Fonts/ConsoleFontAtlas.json";
        Assert.Contains(imagePath, BrowserBootstrapAssetCatalog.DefaultBinaryAssetPaths);
        Assert.Contains(metadataPath, BrowserBootstrapAssetCatalog.DefaultTextAssetPaths);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Core", "Content", "StockMaps"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var root = Path.Combine(directory!.FullName, "Core");
        using var atlas = Image.Load<Rgba32>(Path.Combine(root, imagePath));
        using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, metadataPath)));
        Assert.Equal(11, metadata.RootElement.GetProperty("LineHeight").GetInt32());
        Assert.Equal(0, metadata.RootElement.GetProperty("Spacing").GetInt32());
        foreach (var glyph in metadata.RootElement.GetProperty("Glyphs").EnumerateArray())
        {
            var character = glyph.GetProperty("Character").GetInt32();
            Assert.Equal(8, glyph.GetProperty("Advance").GetInt32());
            if (character == 32) continue;
            using var source = Image.Load<Rgba32>(Path.Combine(root, "Content", "Sprites", "GameElements", "gg2FontS.images", $"image {character - 33}.png"));
            var x = glyph.GetProperty("X").GetInt32(); var y = glyph.GetProperty("Y").GetInt32();
            Assert.Equal(source.Width, glyph.GetProperty("Width").GetInt32());
            Assert.Equal(source.Height, glyph.GetProperty("Height").GetInt32());
            for (var row = 0; row < source.Height; row++)
                for (var column = 0; column < source.Width; column++)
                {
                    Assert.Equal(source[column, row].A, atlas[x + column, y + row].A);
                    if (source[column, row].A != 0) Assert.Equal(source[column, row], atlas[x + column, y + row]);
                }
        }
    }
}
