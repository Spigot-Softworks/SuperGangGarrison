using System.Text.Json.Serialization;

namespace OpenGarrison.BotAI;

public enum BotNavigationHintTraversalKind
{
    Auto = 0,
    Walk = 1,
    Jump = 2,
    Drop = 3,
}

public sealed class BotNavigationHintNode
{
    public string Label { get; init; } = string.Empty;

    public IReadOnlyList<BotNavigationProfile> Profiles { get; init; } = Array.Empty<BotNavigationProfile>();

    public float X { get; init; }

    public float Y { get; init; }

    public BotNavigationNodeKind Kind { get; init; } = BotNavigationNodeKind.RouteAnchor;
}

public sealed class BotNavigationHintLink
{
    public string FromLabel { get; init; } = string.Empty;

    public string ToLabel { get; init; } = string.Empty;

    public IReadOnlyList<BotNavigationProfile> Profiles { get; init; } = Array.Empty<BotNavigationProfile>();

    public bool Bidirectional { get; init; }

    public BotNavigationHintTraversalKind Traversal { get; init; } = BotNavigationHintTraversalKind.Auto;

    public float CostMultiplier { get; init; } = 1f;
}

public sealed class BotNavigationHintAsset
{
    public int FormatVersion { get; init; } = BotNavigationHintStore.CurrentFormatVersion;

    public string LevelName { get; init; } = string.Empty;

    public int MapAreaIndex { get; init; } = 1;

    public IReadOnlyList<BotNavigationHintNode> Nodes { get; init; } = Array.Empty<BotNavigationHintNode>();

    public IReadOnlyList<BotNavigationHintLink> Links { get; init; } = Array.Empty<BotNavigationHintLink>();
}
