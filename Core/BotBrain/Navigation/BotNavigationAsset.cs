using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenGarrison.Core.BotBrain;

public sealed class BotNavigationAsset
{
    public int FormatVersion { get; set; } = BotNavigationAssetStore.CurrentFormatVersion;

    public string LevelName { get; set; } = string.Empty;

    public int MapAreaIndex { get; set; }

    public string LevelFingerprint { get; set; } = string.Empty;

    public List<BotNavigationSurfaceAssetEntry> Surfaces { get; set; } = [];

    public List<BotNavigationNodeAssetEntry> Nodes { get; set; } = [];

    public List<BotNavigationEdgeAssetEntry> Edges { get; set; } = [];

    public List<BotNavigationPortalAssetEntry> Portals { get; set; } = [];

    public List<BotNavigationAnchorAssetEntry> Anchors { get; set; } = [];

    public List<BotNavigationValidationIssueAssetEntry> ValidationIssues { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BotNavigationBuildStatsAssetEntry? BuildStats { get; set; }
}

public sealed class BotNavigationSurfaceAssetEntry
{
    public int Id { get; set; }

    public float LeftX { get; set; }

    public float RightX { get; set; }

    public float TopY { get; set; }

    public bool IsDropdown { get; set; }

    public int FirstNodeIndex { get; set; }

    public int LastNodeIndex { get; set; }
}

public sealed class BotNavigationNodeAssetEntry
{
    public float X { get; set; }

    public float Y { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NavNodeKind Kind { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SurfaceId { get; set; }
}

public sealed class BotNavigationEdgeAssetEntry
{
    public int FromNode { get; set; }

    public int ToNode { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NavEdgeKind Kind { get; set; }

    public float Cost { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FromPortalId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ToPortalId { get; set; }

    public bool ProbeCertified { get; set; }

    public int ProbeJumpTriggerTick { get; set; }

    public int ProbeTicks { get; set; }

    public float ProbeMoveDirectionX { get; set; }

    public int SupportedClassMask { get; set; } = BotBrainClassMask.All;

    public int SupportedTeamMask { get; set; } = BotBrainTeamMask.All;

    public float CompletionMinX { get; set; }

    public float CompletionMaxX { get; set; }

    public float CompletionMinY { get; set; }

    public float CompletionMaxY { get; set; }

    public List<int> AcceptedLandingSurfaceIds { get; set; } = [];

    public bool RequiresGroundedContinuation { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BotNavigationLaunchRecipeAssetEntry? LaunchRecipe { get; set; }
}

public sealed class BotNavigationPortalAssetEntry
{
    public int Id { get; set; }

    public string Kind { get; set; } = string.Empty;

    public int NodeIndex { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SurfaceId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AnchorIndex { get; set; }

    public float X { get; set; }

    public float Y { get; set; }
}

public sealed class BotNavigationLaunchRecipeAssetEntry
{
    public bool StartGrounded { get; set; }

    public int LaunchTick { get; set; }

    public float LaunchMinX { get; set; }

    public float LaunchMaxX { get; set; }

    public float LaunchMinY { get; set; }

    public float LaunchMaxY { get; set; }

    public float LaunchMinHorizontalSpeed { get; set; }

    public float LaunchMaxHorizontalSpeed { get; set; }

    public float ExpectedMoveDirectionX { get; set; }
}

public sealed class BotNavigationAnchorAssetEntry
{
    public string Kind { get; set; } = string.Empty;

    public float X { get; set; }

    public float Y { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PlayerTeam? Team { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NodeIndex { get; set; }
}

public sealed class BotNavigationValidationIssueAssetEntry
{
    public string Kind { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PlayerTeam? Team { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FromNode { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ToNode { get; set; }

    public float X { get; set; }

    public float Y { get; set; }
}

public sealed class BotNavigationBuildStatsAssetEntry
{
    public int SurfacePairChecks { get; set; }

    public int NodePairChecks { get; set; }

    public int CertifiedProbeAttempts { get; set; }

    public int CertifiedProbeSuccesses { get; set; }
}
