using OpenGarrison.Core;
using System.Text.Json.Serialization;

namespace OpenGarrison.BotAI;

public enum BotNavigationScoreRoutePhase
{
    None = 0,
    CaptureObjective = 1,
    AttackIntel = 2,
    ReturnIntel = 3,
}

public sealed class BotNavigationScoreRouteEntry
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public PlayerTeam Team { get; init; }

    public BotNavigationProfile Profile { get; init; } = BotNavigationProfile.Standard;

    public BotNavigationScoreRoutePhase Phase { get; init; } = BotNavigationScoreRoutePhase.None;

    public int GoalNodeId { get; init; } = -1;

    public float GoalX { get; init; }

    public float GoalY { get; init; }

    public IReadOnlyList<int> RouteNodeIds { get; init; } = Array.Empty<int>();

    public IReadOnlyList<BotNavigationScoreRouteSegment> Segments { get; init; } = Array.Empty<BotNavigationScoreRouteSegment>();
}

public sealed class BotNavigationScoreRouteSegment
{
    public int FromNodeId { get; init; }

    public int ToNodeId { get; init; }

    public BotNavigationTraversalKind TraversalKind { get; init; } = BotNavigationTraversalKind.Walk;

    public float StartToleranceX { get; init; } = 6f;

    public float StartToleranceY { get; init; } = 6f;

    public float LandingToleranceX { get; init; } = 18f;

    public float LandingToleranceY { get; init; } = 18f;

    public bool RequireGroundedArrival { get; init; } = true;

    public IReadOnlyList<BotNavigationInputFrame> InputTape { get; init; } = Array.Empty<BotNavigationInputFrame>();
}

public sealed class BotNavigationScoreRouteAsset
{
    public int FormatVersion { get; init; } = BotNavigationScoreRouteStore.CurrentFormatVersion;

    public string LevelName { get; init; } = string.Empty;

    public int MapAreaIndex { get; init; } = 1;

    public string LevelFingerprint { get; init; } = string.Empty;

    public IReadOnlyList<BotNavigationScoreRouteEntry> Routes { get; init; } = Array.Empty<BotNavigationScoreRouteEntry>();
}
