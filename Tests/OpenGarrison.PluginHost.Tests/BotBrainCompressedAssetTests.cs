using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class BotBrainCompressedAssetTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Og2GraphLoadsFromBrowserCatalogWithoutDiskFile(bool browserCompression)
    {
        using var workspace = TempContentWorkspace.Create();
        var level = TraversalLabFixtures.Create(TraversalLabFixtureKind.FlatGround);
        var key = Og2NavigationGraphCache.BuildKey(level);
        var graph = new NavGraph(
            [new NavNode(10f, 20f, NavNodeKind.Surface, 0)],
            [new List<NavEdge>()], levelName: level.Name, mode: level.Mode);
        Og2NavigationGraphCache.SaveShipped(level, key, graph, out var diskPath);
        var bytes = File.ReadAllBytes(diskPath);
        if (browserCompression)
        {
            bytes = Og2NavigationGraphPackaging.EncodeForBrowser(bytes);
            Assert.Equal(2, bytes[8]);
        }
        File.Delete(diskPath);
        // Match the browser content root, with no real file at that path.
        using var catalog = BrowserCatalogScope.Create("Content");
        BrowserContentCatalog.SetBinaryAssets(
            [new($"Content/BotBrainOg2Nav/{Path.GetFileName(diskPath)}", bytes)]);

        Assert.True(Og2NavigationGraphCache.TryLoadShipped(level, key, out var loaded, out _));
        Assert.Equal(1, loaded.NodeCount);
    }

    [Fact]
    public void BotNavigationAssetStoreLoadsShippedJsonFromBrowserCatalog()
    {
        using var catalog = BrowserCatalogScope.Create("Content");
        var level = TraversalLabFixtures.Create(TraversalLabFixtureKind.FlatGround);
        var asset = new BotNavigationAsset
        {
            FormatVersion = BotNavigationAssetStore.CurrentFormatVersion,
            LevelName = level.Name,
            MapAreaIndex = level.MapAreaIndex,
            LevelFingerprint = BotNavigationAssetStore.ComputeLevelFingerprint(level),
        };
        var relativePath = $"Content/BotBrainNav/{BotNavigationAssetStore.GetAssetFileName(level.Name, level.MapAreaIndex)}";
        BrowserContentCatalog.SetBinaryAssets(
        [
            new KeyValuePair<string, byte[]>(relativePath, JsonSerializer.SerializeToUtf8Bytes(asset)),
        ]);

        var loaded = BotNavigationAssetStore.TryLoadShipped(level, out var loadedAsset);

        Assert.True(loaded);
        Assert.Equal(asset.LevelFingerprint, loadedAsset.LevelFingerprint);
    }

    [Fact]
    public void BotNavigationLoadDiagnosticReusesCachedBrowserCatalogAsset()
    {
        using var catalog = BrowserCatalogScope.Create("Content");
        var level = TraversalLabFixtures.Create(TraversalLabFixtureKind.ShortGap);
        var asset = new BotNavigationAsset
        {
            FormatVersion = BotNavigationAssetStore.CurrentFormatVersion,
            LevelName = level.Name,
            MapAreaIndex = level.MapAreaIndex,
            LevelFingerprint = BotNavigationAssetStore.ComputeLevelFingerprint(level),
        };
        var relativePath = $"Content/BotBrainNav/{BotNavigationAssetStore.GetAssetFileName(level.Name, level.MapAreaIndex)}";
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(asset);
        BrowserContentCatalog.SetBinaryAssets(
        [
            new KeyValuePair<string, byte[]>(relativePath, jsonBytes),
        ]);

        Assert.True(BotNavigationAssetStore.TryLoadShipped(level, out _));

        BrowserContentCatalog.SetBinaryAssets(
        [
            new KeyValuePair<string, byte[]>(relativePath, Encoding.UTF8.GetBytes(new string(' ', jsonBytes.Length))),
        ]);

        var diagnostic = BotNavigationAssetStore.GetLoadDiagnostic(level);

        Assert.StartsWith("compatible:", diagnostic.ShippedStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void BotNavigationAssetStoreLoadsShippedJsonGzip()
    {
        using var workspace = TempContentWorkspace.Create();
        var level = TraversalLabFixtures.Create(TraversalLabFixtureKind.FlatGround);
        var asset = new BotNavigationAsset
        {
            FormatVersion = BotNavigationAssetStore.CurrentFormatVersion,
            LevelName = level.Name,
            MapAreaIndex = level.MapAreaIndex,
            LevelFingerprint = BotNavigationAssetStore.ComputeLevelFingerprint(level),
        };
        var path = Path.Combine(
            ContentRoot.Path,
            "BotBrainNav",
            BotNavigationAssetStore.GetAssetFileName(level.Name, level.MapAreaIndex)) + ".gz";
        WriteGzipJson(path, asset);

        var loaded = BotNavigationAssetStore.TryLoadShipped(level, out var loadedAsset);

        Assert.True(loaded);
        Assert.Equal(asset.LevelFingerprint, loadedAsset.LevelFingerprint);
    }

    [Fact]
    public void BotNavigationAuthoredCorridorStoreLoadsJsonGzip()
    {
        using var workspace = TempContentWorkspace.Create();
        var level = TraversalLabFixtures.Create(TraversalLabFixtureKind.FlatGround);
        var asset = new BotNavigationAuthoredCorridorAsset
        {
            LevelName = level.Name,
            MapAreaIndex = level.MapAreaIndex,
        };
        WriteGzipJson(BotNavigationAuthoredCorridorStore.ResolvePath(level.Name, level.MapAreaIndex) + ".gz", asset);

        var loaded = BotNavigationAuthoredCorridorStore.TryLoad(level, out var loadedAsset);

        Assert.True(loaded);
        Assert.Equal(level.Name, loadedAsset.LevelName);
    }

    private static void WriteGzipJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fileStream = File.Create(path);
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
        JsonSerializer.Serialize(gzipStream, value);
    }

    private static byte[] SerializeGzipJson<T>(T value)
    {
        using var stream = new MemoryStream();
        using (var gzipStream = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))
        {
            JsonSerializer.Serialize(gzipStream, value);
        }

        return stream.ToArray();
    }

    private sealed class BrowserCatalogScope : IDisposable
    {
        private readonly string _originalContentRoot;

        private BrowserCatalogScope(string rootPath, string originalContentRoot)
        {
            _originalContentRoot = originalContentRoot;
            ContentRoot.Initialize(rootPath);
            BrowserContentCatalog.SetBinaryAssets([]);
        }

        public static BrowserCatalogScope Create(string rootPath)
        {
            return new BrowserCatalogScope(rootPath, ContentRoot.Path);
        }

        public void Dispose()
        {
            BrowserContentCatalog.SetBinaryAssets([]);
            ContentRoot.Initialize(_originalContentRoot);
        }
    }

    private sealed class TempContentWorkspace : IDisposable
    {
        private readonly string _originalContentRoot;

        private TempContentWorkspace(string rootPath, string originalContentRoot)
        {
            RootPath = rootPath;
            _originalContentRoot = originalContentRoot;
            ContentRoot.Initialize(rootPath);
        }

        public string RootPath { get; }

        public static TempContentWorkspace Create()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "og-botbrain-compressed-assets", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);
            return new TempContentWorkspace(rootPath, ContentRoot.Path);
        }

        public void Dispose()
        {
            ContentRoot.Initialize(_originalContentRoot);
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
