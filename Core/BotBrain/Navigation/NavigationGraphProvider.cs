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

    public NavGraph? GetGraph(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

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
}
