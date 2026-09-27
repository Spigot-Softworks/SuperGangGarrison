using System.Security.Cryptography;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LegacyGg2MapCacheTests
{
    [Theory]
    [InlineData("koth_corinth", GameModeKind.KingOfTheHill)]
    [InlineData("ctf_conflict", GameModeKind.CaptureTheFlag)]
    [InlineData("koth_harvest", GameModeKind.KingOfTheHill)]
    public void BundledStockMapsUseGg2Pngs(string mapName, GameModeKind expectedMode)
    {
        var fixture = ProjectSourceLocator.FindFile(Path.Combine(
            "Core", "Content", "StockMaps", "Gg2", mapName + ".png"));
        Assert.False(string.IsNullOrWhiteSpace(fixture));
        Assert.True(LegacyGg2BundledStockMaps.TryRegister(mapName.ToUpperInvariant(),
            out var levelName, Path.GetDirectoryName(fixture)));

        var level = SimpleLevelFactory.CreateImportedLevel(levelName);
        Assert.NotNull(level);
        Assert.Equal(expectedMode, level.Mode);
        Assert.Equal(fixture, level.BackgroundAssetName);
        Assert.NotEmpty(level.Solids);
        Assert.NotEmpty(level.RedSpawns);
        Assert.NotEmpty(level.BlueSpawns);
    }

    [Fact]
    public void RejectsUnknownBundledStockMapInsteadOfUsingAnSggRedraw()
    {
        Assert.False(LegacyGg2BundledStockMaps.TryRegister("ctf_missing", out _));
        Assert.False(LegacyGg2BundledStockMaps.TryRegister("../ctf_conflict", out _));
    }

    [Fact]
    public void PreservesServerMapNameForNameDerivedGameMode()
    {
        var fixture = ProjectSourceLocator.FindFile(Path.Combine("Core", "Content", "StockMaps", "tdm_mantic.png"));
        Assert.False(string.IsNullOrWhiteSpace(fixture));
        var png = File.ReadAllBytes(fixture);
        var md5 = Convert.ToHexStringLower(MD5.HashData(png));
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "og-gg2-map-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var transfer = new MemoryStream();
            using (var writer = new BinaryWriter(transfer, System.Text.Encoding.Latin1, leaveOpen: true))
            {
                writer.Write(checked((uint)png.Length));
                writer.Write(png);
            }

            transfer.Position = 0;
            using var reader = new BinaryReader(transfer);
            var levelName = LegacyGg2MapCache.EnsureMapAvailable(
                reader, _ => { }, "tdm_mantic", md5, cacheDirectory);
            var level = SimpleLevelFactory.CreateImportedLevel(levelName);
            Assert.NotNull(level);
            Assert.Equal(GameModeKind.TeamDeathmatch, level.Mode);
        }
        finally
        {
            if (Directory.Exists(cacheDirectory)) Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public void DownloadsVerifiesAndReusesGg2PngMap()
    {
        var fixture = ProjectSourceLocator.FindFile(Path.Combine("Core", "Content", "StockMaps", "ctf_2dfort.png"));
        Assert.False(string.IsNullOrWhiteSpace(fixture));
        var png = File.ReadAllBytes(fixture);
        var md5 = Convert.ToHexStringLower(MD5.HashData(png));
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "og-gg2-map-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var transfer = new MemoryStream();
            using (var writer = new BinaryWriter(transfer, System.Text.Encoding.Latin1, leaveOpen: true))
            {
                writer.Write(checked((uint)png.Length));
                writer.Write(png);
            }

            transfer.Position = 0;
            using var reader = new BinaryReader(transfer);
            var requests = new List<byte[]>();
            var levelName = LegacyGg2MapCache.EnsureMapAvailable(
                reader, requests.Add, "ctf_2dfort", md5, cacheDirectory);
            Assert.Equal(new byte[] { LegacyGg2Wire.DownloadMap }, Assert.Single(requests));
            Assert.Equal($"gg2_{md5}", levelName);
            Assert.Equal(transfer.Length, transfer.Position);

            var mapPath = Path.Combine(cacheDirectory, levelName + ".png");
            Assert.Equal(png, File.ReadAllBytes(mapPath));
            var level = SimpleLevelFactory.CreateImportedLevel(levelName);
            Assert.NotNull(level);
            Assert.Equal(mapPath, level.BackgroundAssetName);
            Assert.NotEmpty(level.Solids);

            using var empty = new BinaryReader(new MemoryStream());
            Assert.Equal(levelName,
                LegacyGg2MapCache.EnsureMapAvailable(
                    empty, requests.Add, "ctf_2dfort", md5, cacheDirectory));
            Assert.Single(requests);
        }
        finally
        {
            if (Directory.Exists(cacheDirectory)) Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public void RejectsMapBytesThatDoNotMatchAdvertisedMd5()
    {
        var png = File.ReadAllBytes(ProjectSourceLocator.FindFile(
            Path.Combine("Core", "Content", "StockMaps", "ctf_2dfort.png"))!);
        using var transfer = new MemoryStream();
        using (var writer = new BinaryWriter(transfer, System.Text.Encoding.Latin1, leaveOpen: true))
        {
            writer.Write(checked((uint)png.Length));
            writer.Write(png);
        }

        transfer.Position = 0;
        using var reader = new BinaryReader(transfer);
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "og-gg2-map-test-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidDataException>(() => LegacyGg2MapCache.EnsureMapAvailable(
            reader, _ => { }, "ctf_2dfort", new string('0', 32), cacheDirectory));
        Assert.False(Directory.Exists(cacheDirectory));
    }
}
