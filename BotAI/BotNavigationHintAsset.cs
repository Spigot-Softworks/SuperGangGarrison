using OpenGarrison.Core;
using System.Text.Json.Serialization;

namespace OpenGarrison.BotAI;

public enum BotNavigationHintTraversalKind
{
    Auto = 0,
    Walk = 1,
    Jump = 2,
    Drop = 3,
}

public enum BotNavigationHintBuildMode
{
    GeometryAugmented = 0,
    ExplicitGraph = 1,
}

public enum BotNavigationHintFallbackRole
{
    None = 0,
    Entry = 1,
    Exit = 2,
}

public enum BotNavigationHintFallbackTrigger
{
    NoNext = 0,
    Stuck = 1,
    Reacquire = 2,
    CarryReturn = 3,
    DirectTarget = 4,
}

public sealed class BotNavigationHintNode
{
    public string Label { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AutoLabel { get; init; }

    public IReadOnlyList<PlayerClass> Classes { get; init; } = Array.Empty<PlayerClass>();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<BotNavigationProfile>? Profiles { get; init; }

    public float X { get; init; }

    public float Y { get; init; }

    public BotNavigationNodeKind Kind { get; init; } = BotNavigationNodeKind.RouteAnchor;

    public PlayerTeam? Team { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public BotNavigationHintFallbackRole FallbackRole { get; init; }

    public IReadOnlyList<BotNavigationHintFallbackTrigger> FallbackTriggers { get; init; } = Array.Empty<BotNavigationHintFallbackTrigger>();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float FallbackRadius { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RequiresCarryingIntel { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BotNavigationHintFallbackActivationZone? FallbackActivationZone { get; init; }
}

public sealed class BotNavigationHintFallbackActivationZone
{
    public float MinX { get; init; }

    public float MinY { get; init; }

    public float MaxX { get; init; }

    public float MaxY { get; init; }
}

public sealed class BotNavigationHintLink
{
    public string FromLabel { get; init; } = string.Empty;

    public string ToLabel { get; init; } = string.Empty;

    public IReadOnlyList<PlayerClass> Classes { get; init; } = Array.Empty<PlayerClass>();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<BotNavigationProfile>? Profiles { get; init; }

    public bool Bidirectional { get; init; }

    public BotNavigationHintTraversalKind Traversal { get; init; } = BotNavigationHintTraversalKind.Auto;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool StartJumpImmediately { get; init; }

    public float CostMultiplier { get; init; } = 1f;

    public IReadOnlyList<BotNavigationHintRecordedTraversal> RecordedTraversals { get; init; } = Array.Empty<BotNavigationHintRecordedTraversal>();
}

public sealed class BotNavigationHintRecordedTraversal
{
    public PlayerClass? ClassId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BotNavigationProfile? Profile { get; init; }

    public IReadOnlyList<BotNavigationInputFrame> InputTape { get; init; } = Array.Empty<BotNavigationInputFrame>();
}

public sealed class BotNavigationHintAsset
{
    public int FormatVersion { get; init; } = BotNavigationHintStore.CurrentFormatVersion;

    public string LevelName { get; init; } = string.Empty;

    public int MapAreaIndex { get; init; } = 1;

    public BotNavigationHintBuildMode BuildMode { get; init; } = BotNavigationHintBuildMode.GeometryAugmented;

    public IReadOnlyList<BotNavigationHintNode> Nodes { get; init; } = Array.Empty<BotNavigationHintNode>();

    public IReadOnlyList<BotNavigationHintLink> Links { get; init; } = Array.Empty<BotNavigationHintLink>();
}
