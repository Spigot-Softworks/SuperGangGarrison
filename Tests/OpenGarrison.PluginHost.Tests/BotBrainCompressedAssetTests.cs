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
    public void BotBrainObjectiveTapeStoreLoadsJsonGzipFromBrowserCatalog()
    {
        using var catalog = BrowserCatalogScope.Create("Content");
        var level = TraversalLabFixtures.Create(TraversalLabFixtureKind.FlatGround);
        var asset = new BotBrainObjectiveTapeAsset
        {
            LevelName = level.Name,
            MapAreaIndex = level.MapAreaIndex,
        };
        var relativePath = $"Content/BotBrainTapes/{Path.GetFileName(BotBrainObjectiveTapeStore.ResolvePath(level.Name, level.MapAreaIndex))}.gz";
        BrowserContentCatalog.SetBinaryAssets(
        [
            new KeyValuePair<string, byte[]>(relativePath, SerializeGzipJson(asset)),
        ]);

        var loaded = BotBrainObjectiveTapeStore.TryLoad(level, out var loadedAsset);

        Assert.True(loaded);
        Assert.Equal(level.Name, loadedAsset.LevelName);
    }

    [Fact]
    public void BotBrainObjectiveTapeStoreLoadsJsonGzip()
    {
        using var workspace = TempContentWorkspace.Create();
        var level = TraversalLabFixtures.Create(TraversalLabFixtureKind.FlatGround);
        var asset = new BotBrainObjectiveTapeAsset
        {
            LevelName = level.Name,
            MapAreaIndex = level.MapAreaIndex,
        };
        WriteGzipJson(BotBrainObjectiveTapeStore.ResolvePath(level.Name, level.MapAreaIndex) + ".gz", asset);

        var loaded = BotBrainObjectiveTapeStore.TryLoad(level, out var loadedAsset);

        Assert.True(loaded);
        Assert.Equal(level.Name, loadedAsset.LevelName);
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

    [Fact]
    public void VerifiedNavProofGraphAssetStoreLoadsShippedJsonGzip()
    {
        using var workspace = TempContentWorkspace.Create();
        using var environment = ProofGraphEnvironmentScope.DisableOverrides();
        var level = TraversalLabFixtures.Create(TraversalLabFixtureKind.FlatGround);
        var asset = new VerifiedNavProofGraphAsset
        {
            LevelName = level.Name,
            MapAreaIndex = level.MapAreaIndex,
            Team = PlayerTeam.Blue,
            ClassId = PlayerClass.Heavy,
            Edges =
            {
                new VerifiedNavProofGraphEdge(
                    1,
                    VerifiedNavProofRouteKind.Pickup,
                    0,
                    0,
                    0f,
                    0f,
                    16f,
                    0f,
                    1,
                    1,
                    0,
                    0,
                    0,
                    "test",
                    []),
            },
            Routes =
            {
                new VerifiedNavProofGraphRoute(
                    VerifiedNavProofRouteKind.Pickup,
                    "test",
                    2,
                    [0],
                    [1],
                    [],
                    [],
                    0f,
                    0f,
                    16f,
                    0f,
                    []),
            },
        };
        var path = Path.Combine(
            ContentRoot.Path,
            "BotBrainProofGraphs",
            $"{level.Name}.a{level.MapAreaIndex}.Blue.Heavy.verified-proof-graph.json.gz");
        WriteGzipJson(path, asset);

        var loaded = VerifiedNavProofGraphAssetStore.TryLoad(level, PlayerTeam.Blue, PlayerClass.Heavy, out var loadedAsset);

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

    private sealed class ProofGraphEnvironmentScope : IDisposable
    {
        private readonly string? _enable;
        private readonly string? _require;
        private readonly string? _path;
        private readonly string? _directory;

        private ProofGraphEnvironmentScope()
        {
            _enable = Environment.GetEnvironmentVariable(VerifiedNavProofGraphAssetStore.EnableEnvironmentVariable);
            _require = Environment.GetEnvironmentVariable(VerifiedNavProofGraphAssetStore.RequireEnvironmentVariable);
            _path = Environment.GetEnvironmentVariable(VerifiedNavProofGraphAssetStore.PathEnvironmentVariable);
            _directory = Environment.GetEnvironmentVariable(VerifiedNavProofGraphAssetStore.DirectoryEnvironmentVariable);
        }

        public static ProofGraphEnvironmentScope DisableOverrides()
        {
            var scope = new ProofGraphEnvironmentScope();
            Environment.SetEnvironmentVariable(VerifiedNavProofGraphAssetStore.EnableEnvironmentVariable, null);
            Environment.SetEnvironmentVariable(VerifiedNavProofGraphAssetStore.RequireEnvironmentVariable, null);
            Environment.SetEnvironmentVariable(VerifiedNavProofGraphAssetStore.PathEnvironmentVariable, null);
            Environment.SetEnvironmentVariable(VerifiedNavProofGraphAssetStore.DirectoryEnvironmentVariable, null);
            return scope;
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(VerifiedNavProofGraphAssetStore.EnableEnvironmentVariable, _enable);
            Environment.SetEnvironmentVariable(VerifiedNavProofGraphAssetStore.RequireEnvironmentVariable, _require);
            Environment.SetEnvironmentVariable(VerifiedNavProofGraphAssetStore.PathEnvironmentVariable, _path);
            Environment.SetEnvironmentVariable(VerifiedNavProofGraphAssetStore.DirectoryEnvironmentVariable, _directory);
        }
    }
}
