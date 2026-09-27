using System.IO.Compression;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PackagedBackgroundFallbackTests
{
    [Fact]
    public void ReadsStockBackgroundFromRuntimeBundleWhenLooseImageIsMissing()
    {
        var sourcePath = ProjectSourceLocator.FindFile(
            Path.Combine("Core", "Content", "Backgrounds", "WaterwayB.png"));
        Assert.False(string.IsNullOrWhiteSpace(sourcePath));
        var expected = File.ReadAllBytes(sourcePath);
        var bundlePath = Path.Combine(Path.GetTempPath(), "og-background-test-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create))
            using (var entryStream = archive.CreateEntry("Content/Backgrounds/WaterwayB.png").Open())
            {
                entryStream.Write(expected);
            }

            Assert.True(GameMakerRuntimeAssetCache.TryReadPackagedBackgroundBytes(
                "Content/Backgrounds/WaterwayB.png", out var actual, bundlePath));
            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(bundlePath)) File.Delete(bundlePath);
        }
    }
}
