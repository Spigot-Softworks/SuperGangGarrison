namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Resolves the graph that is already available for a level without building
/// one on the live bot-think path.
/// </summary>
public sealed class NavigationGraphProvider : INavigationGraphProvider
{
    private readonly bool _disableShippedNavigationGraph;
    private readonly NavGraph? _graphOverride;

    public NavigationGraphProvider()
    {
    }

    public NavigationGraphProvider(bool disableShippedNavigationGraph)
    {
        _disableShippedNavigationGraph = disableShippedNavigationGraph;
    }

    public NavigationGraphProvider(NavGraph graphOverride)
    {
        _graphOverride = graphOverride ?? throw new ArgumentNullException(nameof(graphOverride));
    }

    public string LastSource { get; private set; } = "none";

    public string LastSourcePath { get; private set; } = string.Empty;

    public string LastPreloadSource { get; private set; } = "None";

    public NavGraph? GetGraph(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        LastSourcePath = string.Empty;

        if (_graphOverride is not null)
        {
            LastSource = "override";
            return _graphOverride;
        }

        if (_disableShippedNavigationGraph)
        {
            LastSource = "disabled";
            return null;
        }

        // Both lookups are non-building. An unwarmed controller must remain
        // graphless rather than synchronously generating an OG2 graph.
        if (Og2NavigationGraphStore.TryGetCached(level, out var warmedGraph))
        {
            LastSource = "memory";
            return warmedGraph;
        }

        if (Og2NavigationGraphStore.TryLoadShipped(level, out var shippedGraph))
        {
            LastSource = "shipped";
            return shippedGraph;
        }

        LastSource = "none";
        return null;
    }

    /// <summary>
    /// Resolves the shared graph for startup, practice warmup, tooling, or
    /// tests. The live GetGraph path remains strictly non-building.
    ///
    /// LastSource keeps the provider's lowercase source vocabulary and adds
    /// "built" and "runtime-cache" for this explicit warmup path.
    /// LastPreloadSource retains the historical diagnostic names used by the
    /// startup logs: InMemory, Shipped, RuntimeCache, and Built.
    /// </summary>
    public NavGraph PreloadGraph(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        if (_graphOverride is not null)
        {
            LastSource = "override";
            LastPreloadSource = "Override";
            LastSourcePath = string.Empty;
            return _graphOverride;
        }

        // The disable-shipped option belongs to the non-building live path.
        // An explicit preload is the caller's request to acquire a graph and
        // therefore retains the historical store behavior of loading or
        // building it regardless of that live-path option.
        var graph = Og2NavigationGraphStore.GetOrBuild(level, out var resolution);
        LastSource = resolution.Source switch
        {
            Og2NavigationGraphResolutionSource.InMemory => "memory",
            Og2NavigationGraphResolutionSource.Shipped => "shipped",
            Og2NavigationGraphResolutionSource.RuntimeCache => "runtime-cache",
            Og2NavigationGraphResolutionSource.Built => "built",
            _ => "none",
        };
        LastPreloadSource = resolution.Source.ToString();
        LastSourcePath = resolution.Path;
        return graph;
    }

    /// <summary>
    /// Builds the persistent-cache key used by the provider's graph pipeline.
    /// These maintenance helpers are intentionally the only cache operations
    /// exposed to tooling and diagnostics outside this navigation directory.
    /// </summary>
    public static string BuildCacheKey(
        SimpleLevel level,
        string? sweepTicksOverride = null,
        string? contactGraphOverride = null)
    {
        return Og2NavigationGraphCache.BuildKey(level, sweepTicksOverride, contactGraphOverride);
    }

    public static bool TryGetWarmedGraph(SimpleLevel level, out NavGraph graph)
    {
        return Og2NavigationGraphStore.TryGetCached(level, out graph);
    }

    public static bool TryLoadShippedGraph(SimpleLevel level, out NavGraph graph)
    {
        return Og2NavigationGraphStore.TryLoadShipped(level, out graph);
    }

    public static bool TryLoadShippedGraph(
        SimpleLevel level,
        string key,
        out NavGraph graph,
        out string path)
    {
        return Og2NavigationGraphCache.TryLoadShipped(level, key, out graph, out path);
    }

    public static bool TryLoadPersistentGraph(
        SimpleLevel level,
        string key,
        out NavGraph graph,
        out string path)
    {
        return Og2NavigationGraphCache.TryLoad(level, key, out graph, out path);
    }

    public static void SaveShippedGraph(
        SimpleLevel level,
        string key,
        NavGraph graph,
        out string path)
    {
        Og2NavigationGraphCache.SaveShipped(level, key, graph, out path);
    }

    public static NavigationGraphCacheCompactionResult CompactPersistentCache()
    {
        var result = Og2NavigationGraphCache.CompactPersistentCache();
        return new NavigationGraphCacheCompactionResult(
            result.Scanned,
            result.Compressed,
            result.Skipped,
            result.Failed,
            result.Pruned,
            result.BytesBefore,
            result.BytesAfter,
            result.BytesPruned);
    }
}

/// <summary>
/// Result of compacting the provider's persistent navigation cache.
/// </summary>
public readonly record struct NavigationGraphCacheCompactionResult(
    int Scanned,
    int Compressed,
    int Skipped,
    int Failed,
    int Pruned,
    long BytesBefore,
    long BytesAfter,
    long BytesPruned);
