namespace OpenGarrison.Core.BotBrain;

public enum VerifiedNavSurfaceKind
{
    SolidTop = 1,
    DropdownPlatform = 2,
}

public enum VerifiedNavPortalKind
{
    SurfaceLeft = 1,
    SurfaceRight = 2,
    Spawn = 3,
    EnemyIntel = 4,
    OwnIntel = 5,
    CaptureZone = 6,
    ControlPoint = 7,
}

public enum VerifiedNavEdgeIntent
{
    Walk = 1,
    Drop = 2,
    Jump = 3,
}

public enum VerifiedNavCertificationState
{
    Candidate = 1,
    Certified = 2,
    Rejected = 3,
}

public sealed record VerifiedNavSurface(
    int Id,
    VerifiedNavSurfaceKind Kind,
    float Left,
    float Right,
    float Top,
    int SourceIndex)
{
    public float Width => Right - Left;

    public float CenterX => (Left + Right) * 0.5f;
}

public sealed record VerifiedNavPortal(
    int Id,
    VerifiedNavPortalKind Kind,
    float X,
    float Bottom,
    int? SurfaceId,
    string Label);

public sealed record VerifiedNavCandidateEdge(
    int Id,
    VerifiedNavEdgeIntent Intent,
    int FromSurfaceId,
    int ToSurfaceId,
    float EntryX,
    float EntryBottom,
    float ExitX,
    float ExitBottom,
    float EstimatedCost,
    VerifiedNavCertificationState Certification,
    string RecipeHint);

public sealed class VerifiedNavCandidateGraph
{
    public int Version { get; init; } = 1;

    public string LevelName { get; init; } = string.Empty;

    public int MapAreaIndex { get; init; }

    public PlayerTeam Team { get; init; }

    public PlayerClass ClassId { get; init; }

    public float PlayerCollisionLeft { get; init; }

    public float PlayerCollisionRight { get; init; }

    public float PlayerCollisionBottom { get; init; }

    public List<VerifiedNavSurface> Surfaces { get; init; } = [];

    public List<VerifiedNavPortal> Portals { get; init; } = [];

    public List<VerifiedNavCandidateEdge> CandidateEdges { get; init; } = [];
}

public sealed class VerifiedNavBuildOptions
{
    public required PlayerTeam Team { get; init; }

    public required PlayerClass ClassId { get; init; }

    public float SampleStep { get; init; } = 8f;

    public float MinSurfaceWidth { get; init; } = 24f;

    public float SurfaceEndpointInset { get; init; } = 8f;

    // A bot can be grounded on a small, valid OG2 overhang while setting up
    // a jump. These points are kept as launch surfaces only when the probe
    // confirms the actual collision/support state; they are not inferred from
    // raw rectangle dimensions.
    public float EdgeLaunchProbeWidth { get; init; } = 16f;

    public float MinimumLaunchSurfaceWidth { get; init; } = 2f;

    public float SameSurfaceMaxEdgeLength { get; init; } = 240f;

    public float DropHorizontalReach { get; init; } = 144f;

    public float DropVerticalLimit { get; init; } = 420f;

    public float JumpHorizontalReach { get; init; } = 184f;

    public float JumpVerticalLimit { get; init; } = 180f;
}

public sealed class VerifiedNavCertificationOptions
{
    public int MaxEdges { get; init; } = 128;

    public int MaxTicks { get; init; } = 210;

    public int TraceEveryTicks { get; init; } = 6;

    public List<float> StartXOffsets { get; init; } = [0f];

    public List<float> StartBottomOffsets { get; init; } = [0f];

    public List<float> StartHorizontalSpeedOffsets { get; init; } = [0f];

    public List<float> StartVerticalSpeedOffsets { get; init; } = [0f];
}

public sealed record VerifiedNavEdgeCertificationResult(
    int EdgeId,
    VerifiedNavEdgeIntent Intent,
    int FromSurfaceId,
    int ToSurfaceId,
    bool Passed,
    int TestedPrograms,
    int PassedVariants,
    int FailedVariants,
    int ExecutedTicks,
    string Recipe,
    string FailureReason);

public sealed class VerifiedNavCertificationReport
{
    public string LevelName { get; init; } = string.Empty;

    public int MapAreaIndex { get; init; }

    public PlayerTeam Team { get; init; }

    public PlayerClass ClassId { get; init; }

    public int CandidateEdgeCount { get; init; }

    public int TestedEdgeCount { get; init; }

    public int CertifiedEdgeCount => Results.Count(static result => result.Passed);

    public int RejectedEdgeCount => Results.Count - CertifiedEdgeCount;

    public List<VerifiedNavEdgeCertificationResult> Results { get; init; } = [];
}

public sealed class VerifiedNavExplorationOptions
{
    public int MaxSurfaceExpansions { get; init; } = 2000;

    public int TargetSurfaceId { get; init; } = -1;

    public int MaxMacroTicks { get; init; } = 120;

    public float SurfaceProbeInset { get; init; } = 10f;

    public List<int> Durations { get; init; } = [8, 12, 18, 24, 32, 42, 56, 72, 96];

    public List<int> JumpHoldTicks { get; init; } = [2, 6, 10];
}

public sealed record VerifiedNavExploredEdge(
    int FromSurfaceId,
    int ToSurfaceId,
    float EntryX,
    float EntryBottom,
    float ExitX,
    float ExitBottom,
    int Ticks,
    string Macro);

public sealed class VerifiedNavExplorationReport
{
    public string LevelName { get; init; } = string.Empty;

    public int MapAreaIndex { get; init; }

    public PlayerTeam Team { get; init; }

    public PlayerClass ClassId { get; init; }

    public int StartSurfaceId { get; init; }

    public List<int> StartSurfaceIds { get; init; } = [];

    public int ExpandedSurfaceCount { get; init; }

    public int ReachableSurfaceCount { get; init; }

    public bool ReachedEnemyIntelMarker { get; init; }

    public bool ReachedOwnIntelMarker { get; init; }

    public List<int> ReachableSurfaceIds { get; init; } = [];

    public List<VerifiedNavExploredEdge> Edges { get; init; } = [];
}
