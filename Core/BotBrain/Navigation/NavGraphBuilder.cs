namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Compatibility entry point for building a lightweight BotBrain navigation graph.
/// The implementation lives in BotNavigationAssetBuilder so the same graph can be
/// generated offline, serialized, shipped, and loaded without changing runtime users.
/// </summary>
public static class NavGraphBuilder
{
    public const float WaypointArrivalRadius = 16f;

    public static NavGraph Build(SimpleLevel level)
    {
        return BotNavigationAssetBuilder.BuildGraph(level);
    }
}
