namespace OpenGarrison.Core.BotBrain;

public interface INavigationGraphProvider
{
    NavGraph? GetGraph(SimpleLevel level);

    /// <summary>
    /// Resolves a graph for an explicit warmup/build path. Unlike
    /// <see cref="GetGraph"/>, this method may synchronously load or build a
    /// graph and must not be called from a live bot Think path.
    /// </summary>
    NavGraph PreloadGraph(SimpleLevel level);

    /// <summary>
    /// Source of the most recent provider operation. GetGraph preserves the
    /// existing lowercase values: override, disabled, memory, shipped, none.
    /// PreloadGraph additionally uses built and runtime-cache.
    /// </summary>
    string LastSource { get; }

    /// <summary>
    /// Path reported by the most recent PreloadGraph operation, or an empty
    /// string when the graph came from memory or an override.
    /// </summary>
    string LastSourcePath { get; }

    /// <summary>
    /// Source name for the most recent preload using the historical diagnostic
    /// spelling: InMemory, Shipped, RuntimeCache, or Built.
    /// </summary>
    string LastPreloadSource { get; }
}
