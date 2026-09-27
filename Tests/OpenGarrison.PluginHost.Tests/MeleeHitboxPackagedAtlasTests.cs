using OpenGarrison.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class MeleeHitboxPackagedAtlasTests
{
    [Theory]
    [InlineData(WhippingCordCatalog.MeleeHitboxSpriteName, 0)]
    [InlineData(WhippingCordCatalog.WhipRecoilSpriteName, WhippingCordCatalog.ExtendedWhipFrameIndex)]
    public void WhipMaskLoadsFromAtlasWhenPackagedSpriteSourceIsAbsent(string spriteName, int frameIndex)
    {
        var sprite = StockGameplayModCatalog.Definition.Assets.Sprites[spriteName];
        var sourcePack = GameplayModPackDirectoryLoader.FindPackDirectory(
            StockGameplayModCatalog.StockPackDirectoryName);
        Assert.NotNull(sourcePack);
        var sourceFrame = Path.Combine(sourcePack, sprite.FramePaths[frameIndex].Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(sourceFrame));

        var originalContentRoot = ContentRoot.Path;
        var tempContentRoot = Path.Combine(Path.GetTempPath(), $"OpenGarrison.WhipMask.{Guid.NewGuid():N}");
        try
        {
            var tempPack = Path.Combine(tempContentRoot, "Gameplay", "stock.gg2");
            var atlasesDirectory = Path.Combine(tempContentRoot, "Browser", "Atlases");
            var manifestsDirectory = Path.Combine(tempContentRoot, "Browser", "Manifests");
            Directory.CreateDirectory(tempPack);
            Directory.CreateDirectory(atlasesDirectory);
            Directory.CreateDirectory(manifestsDirectory);
            File.Copy(Path.Combine(sourcePack, "pack.json"), Path.Combine(tempPack, "pack.json"));

            using var source = Image.Load<Rgba32>(sourceFrame);
            const int insetX = 4;
            const int insetY = 3;
            using (var atlas = new Image<Rgba32>(source.Width + insetX + 2, source.Height + insetY + 2))
            {
                for (var y = 0; y < source.Height; y += 1)
                {
                    for (var x = 0; x < source.Width; x += 1)
                    {
                        atlas[x + insetX, y + insetY] = source[x, y];
                    }
                }

                atlas.SaveAsPng(Path.Combine(atlasesDirectory, "whip-test.png"));
            }

            var frames = string.Join(",", Enumerable.Range(0, frameIndex + 1).Select(index => $$"""
                { "AtlasId": "whip-test", "X": {{insetX}}, "Y": {{insetY}}, "Width": {{source.Width}}, "Height": {{source.Height}}, "SourceImageIndex": 0, "SourceFrameIndex": {{index}} }
                """));
            var manifest = $$"""
                {
                  "PackId": "stock.gg2",
                  "Manifest": {
                    "Version": 1,
                    "Atlases": [{ "Id": "whip-test", "ImagePath": "Content/Browser/Atlases/whip-test.png", "Width": {{source.Width + insetX + 2}}, "Height": {{source.Height + insetY + 2}}, "Group": "test" }],
                    "Sprites": {
                      "{{spriteName}}": {
                        "OriginX": {{sprite.OriginX}}, "OriginY": {{sprite.OriginY}},
                        "FrameWidth": null, "FrameHeight": null,
                        "Frames": [{{frames}}],
                        "Mask": null, "SourceHash": "test"
                      }
                    }
                  }
                }
                """;
            File.WriteAllText(Path.Combine(manifestsDirectory, "stock-pack-atlas-manifest.json"), manifest);

            ContentRoot.Initialize(tempContentRoot);
            MeleeHitboxMaskCatalog.SetBrowserStockAtlasManifest(null);
            var mask = MeleeHitboxMaskCatalog.GetOrLoad(spriteName, frameIndex);

            Assert.NotNull(mask);
            Assert.Equal(source.Width, mask.Width);
            Assert.Equal(source.Height, mask.Height);
            Assert.True(mask.MaxReachFromOrigin > 50f);
            for (var y = 0; y < source.Height; y += 1)
            {
                for (var x = 0; x < source.Width; x += 1)
                {
                    Assert.Equal(source[x, y].A, mask.AlphaSamples[y * source.Width + x]);
                }
            }
        }
        finally
        {
            ContentRoot.Initialize(originalContentRoot);
            MeleeHitboxMaskCatalog.SetBrowserStockAtlasManifest(null);
            if (Directory.Exists(tempContentRoot))
            {
                Directory.Delete(tempContentRoot, recursive: true);
            }
        }
    }
}
