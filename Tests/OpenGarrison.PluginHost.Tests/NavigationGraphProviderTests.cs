using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(MapDirectoryTestGroup.Name)]
public sealed class NavigationGraphProviderTests
{
    [Fact]
    public void WarmedCacheHitReturnsWarmedGraph()
    {
        var level = CreateTopDownLevel($"provider_warmed_{Guid.NewGuid():N}");
        var warmingProvider = new NavigationGraphProvider();
        var warmedGraph = warmingProvider.PreloadGraph(level);
        var provider = new NavigationGraphProvider();

        var graph = provider.GetGraph(level);

        Assert.Same(warmedGraph, graph);
        Assert.Equal("memory", provider.LastSource);
    }

    [Fact]
    public void CacheMissFallsBackToShippedGraph()
    {
        var originalContentRoot = ContentRoot.Path;
        var coreContent = ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content"));
        Assert.False(string.IsNullOrWhiteSpace(coreContent));
        ContentRoot.Initialize(coreContent!);
        try
        {
            var level = SimpleLevelFactory.CreateImportedLevel("Conflict");
            Assert.NotNull(level);
            var provider = new NavigationGraphProvider();

            Assert.False(NavigationGraphProvider.TryGetWarmedGraph(level!, out _));
            var graph = provider.GetGraph(level!);

            Assert.NotNull(graph);
            Assert.Equal("shipped", provider.LastSource);
        }
        finally
        {
            ContentRoot.Initialize(originalContentRoot);
        }
    }

    [Fact]
    public void PreloadBuildsWhenGraphIsMissing()
    {
        // The persistent runtime cache is keyed by level geometry, not name, so
        // an identical-geometry build from another test could satisfy this
        // lookup. Disable it to force the build path.
        var originalCacheSetting = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_PERSISTENT_CACHE");
        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_PERSISTENT_CACHE", "0");
        try
        {
            var level = CreateTopDownLevel($"provider_preload_build_{Guid.NewGuid():N}");
            Assert.False(NavigationGraphProvider.TryGetWarmedGraph(level, out _));
            var provider = new NavigationGraphProvider();

            var graph = provider.PreloadGraph(level);

            Assert.NotNull(graph);
            Assert.Equal("built", provider.LastSource);
            Assert.Equal("Built", provider.LastPreloadSource);
            Assert.NotEmpty(provider.LastSourcePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_PERSISTENT_CACHE", originalCacheSetting);
        }
    }

    [Fact]
    public void PreloadHitsWarmedCache()
    {
        var level = CreateTopDownLevel($"provider_preload_warmed_{Guid.NewGuid():N}");
        var warmingProvider = new NavigationGraphProvider();
        var warmedGraph = warmingProvider.PreloadGraph(level);
        var provider = new NavigationGraphProvider();

        var graph = provider.PreloadGraph(level);

        Assert.Same(warmedGraph, graph);
        Assert.Equal("memory", provider.LastSource);
        Assert.Equal("InMemory", provider.LastPreloadSource);
        Assert.Empty(provider.LastSourcePath);
    }

    [Fact]
    public void CacheMissOnBothReturnsNull()
    {
        var provider = new NavigationGraphProvider();

        var graph = provider.GetGraph(CreateTopDownLevel($"provider_missing_{Guid.NewGuid():N}"));

        Assert.Null(graph);
        Assert.Equal("none", provider.LastSource);
    }

    [Fact]
    public void DisabledProviderReturnsNull()
    {
        var provider = new NavigationGraphProvider(disableShippedNavigationGraph: true);

        var graph = provider.GetGraph(CreateTopDownLevel($"provider_disabled_{Guid.NewGuid():N}"));

        Assert.Null(graph);
        Assert.Equal("disabled", provider.LastSource);
    }

    [Fact]
    public void OverrideProviderReturnsOverrideGraph()
    {
        var overrideGraph = new NavGraph([], []);
        var provider = new NavigationGraphProvider(overrideGraph);

        var graph = provider.GetGraph(CreateTopDownLevel($"provider_override_{Guid.NewGuid():N}"));

        Assert.Same(overrideGraph, graph);
        Assert.Equal("override", provider.LastSource);
    }

    private static SimpleLevel CreateTopDownLevel(string name)
    {
        return new SimpleLevel(
            name: name,
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(800f, 600f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(160f, 300f),
            redSpawns: [new SpawnPoint(160f, 300f)],
            blueSpawns: [new SpawnPoint(520f, 300f)],
            intelBases: [],
            roomObjects: [],
            floorY: 600f,
            solids: [],
            importedFromSource: false,
            isTopDown: true);
    }
}
