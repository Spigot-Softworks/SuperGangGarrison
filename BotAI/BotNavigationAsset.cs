using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public enum BotNavigationNodeKind
{
    Surface = 0,
    Spawn = 1,
    Objective = 2,
    HealingCabinet = 3,
    RouteAnchor = 4,
}

public enum BotNavigationTraversalKind
{
    Walk = 0,
    Jump = 1,
    Drop = 2,
}

public enum BotNavigationBuildStrategy
{
    GeometrySampled = 0,
    GeometrySampledValidatedJumps = 1,
    HintAugmentedValidatedJumps = 2,
    ExplicitHintGraphValidatedTraversals = 3,
    ModernClientBotPointGraph = 4,
}

public enum BotNavigationAssetSource
{
    None = 0,
    ShippedContent = 1,
    RuntimeCache = 2,
    GeneratedAtRuntime = 3,
}

public sealed class BotNavigationInputFrame
{
    public bool Left { get; init; }

    public bool Right { get; init; }

    public bool Up { get; init; }

    public double DurationSeconds { get; init; }

    public int Ticks { get; init; } = 1;
}

public sealed class BotNavigationNode
{
    public int Id { get; init; }

    public float X { get; init; }

    public float Y { get; init; }

    public int SurfaceId { get; init; }

    public BotNavigationNodeKind Kind { get; init; }

    public PlayerTeam? Team { get; init; }

    public string Label { get; init; } = string.Empty;

    public bool RequiresGroundSupport { get; init; } = true;
}

public sealed class BotNavigationEdge
{
    public int FromNodeId { get; init; }

    public int ToNodeId { get; init; }

    public BotNavigationTraversalKind Kind { get; init; }

    public float Cost { get; init; }

    public IReadOnlyList<BotNavigationInputFrame> InputTape { get; init; } = Array.Empty<BotNavigationInputFrame>();
}

public sealed class BotNavigationBuildStats
{
    public int SurfaceCount { get; init; }

    public int CandidateNodeCount { get; init; }

    public int SurfaceSampleNodeCount { get; init; }

    public int AutoAnchorNodeCount { get; init; }

    public int HintNodeCount { get; init; }

    public int NodeCount { get; init; }

    public int EdgeCount { get; init; }

    public int WalkEdgeCount { get; init; }

    public int JumpEdgeCount { get; init; }

    public int DropEdgeCount { get; init; }

    public int HintEdgeCount { get; init; }

    public double BuildMilliseconds { get; init; }

    public double SurfaceSamplingMilliseconds { get; init; }

    public double AutoAnchorMilliseconds { get; init; }

    public double HintNodeMilliseconds { get; init; }

    public double AutomaticEdgeMilliseconds { get; init; }

    public double HintEdgeMilliseconds { get; init; }

    public double DropEdgeMilliseconds { get; init; }
}

public sealed class BotNavigationAsset
{
    public int FormatVersion { get; init; }

    public string LevelName { get; init; } = string.Empty;

    public int MapAreaIndex { get; init; } = 1;

    public PlayerClass? ClassId { get; init; }

    public BotNavigationProfile Profile { get; init; }

    public string LevelFingerprint { get; init; } = string.Empty;

    public BotNavigationBuildStrategy BuildStrategy { get; init; }

    public DateTime BuiltUtc { get; init; } = DateTime.UtcNow;

    public BotNavigationBuildStats Stats { get; init; } = new();

    public IReadOnlyList<BotNavigationNode> Nodes { get; init; } = Array.Empty<BotNavigationNode>();

    public IReadOnlyList<BotNavigationEdge> Edges { get; init; } = Array.Empty<BotNavigationEdge>();
}

public sealed record BotNavigationAssetStatus(
    PlayerClass ClassId,
    BotNavigationProfile Profile,
    bool IsLoaded,
    BotNavigationAssetSource Source,
    string Path,
    string Message,
    int NodeCount,
    int EdgeCount,
    bool IsStructurallyValid = true,
    string StructuralMessage = "");

public sealed record BotNavigationLoadResult(
    string LevelName,
    int MapAreaIndex,
    string LevelFingerprint,
    IReadOnlyDictionary<PlayerClass, BotNavigationAsset> Assets,
    IReadOnlyList<BotNavigationAssetStatus> Statuses)
{
    public static BotNavigationLoadResult Empty { get; } = new(
        string.Empty,
        1,
        string.Empty,
        new Dictionary<PlayerClass, BotNavigationAsset>(),
        Array.Empty<BotNavigationAssetStatus>());

    public bool HasAnyAssets => Assets.Count > 0;

    public string BuildSummary()
    {
        if (Statuses.Count == 0)
        {
            return "nav no classes requested";
        }

        var loadedCount = Statuses.Count(static status => status.IsLoaded);
        var missingCount = Statuses.Count - loadedCount;
        var invalidCount = Statuses.Count(static status => status.IsLoaded && !status.IsStructurallyValid);
        var uniqueLoadedStatuses = Statuses
            .Where(static status => status.IsLoaded)
            .GroupBy(static status => string.IsNullOrWhiteSpace(status.Path) ? $"nodes:{status.NodeCount}:edges:{status.EdgeCount}" : status.Path, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        var loadedNodes = uniqueLoadedStatuses.Sum(static status => status.NodeCount);
        var loadedEdges = uniqueLoadedStatuses.Sum(static status => status.EdgeCount);
        return invalidCount > 0
            ? $"nav loaded={loadedCount}/{Statuses.Count} missing={missingCount} invalid={invalidCount} nodes={loadedNodes} edges={loadedEdges}"
            : $"nav loaded={loadedCount}/{Statuses.Count} missing={missingCount} nodes={loadedNodes} edges={loadedEdges}";
    }
}
