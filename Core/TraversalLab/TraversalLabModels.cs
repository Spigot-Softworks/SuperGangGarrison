using System.Linq;
using System.Text.Json.Serialization;

namespace OpenGarrison.Core;

public sealed class TraversalLabScenario
{
    public string Name { get; init; } = "unnamed";

    public TraversalLabFixtureKind? Fixture { get; init; }

    public string? LevelName { get; init; }

    public int MapAreaIndex { get; init; } = 1;

    [JsonIgnore]
    public SimpleLevel? OverrideLevel { get; init; }

    public PlayerTeam Team { get; init; } = PlayerTeam.Blue;

    public PlayerClass ClassId { get; init; } = PlayerClass.Scout;

    public TraversalLabStartState Start { get; init; } = new();

    public List<TraversalLabInputStep> Steps { get; init; } = [];

    public int MaxTicks { get; init; }

    public int TraceEveryTicks { get; init; } = 1;

    public List<float> StartXOffsets { get; init; } = [];

    public List<float> StartBottomOffsets { get; init; } = [];

    public List<float> FacingDirections { get; init; } = [];

    public List<float> StartHorizontalSpeedOffsets { get; init; } = [];

    public List<float> StartVerticalSpeedOffsets { get; init; } = [];

    public List<bool> GroundedStates { get; init; } = [];

    public TraversalLabExpectation? Expectation { get; init; }
}

public enum TraversalLabFixtureKind
{
    FlatGround,
    LowBoxClimb,
    ShortGap,
    StairDescent,
}

public sealed class TraversalLabStartState
{
    public float X { get; init; }

    public float Bottom { get; init; }

    public float HorizontalSpeed { get; init; }

    public float VerticalSpeed { get; init; }

    public bool IsGrounded { get; init; }

    public int? RemainingAirJumps { get; init; }

    [JsonInclude]
    public bool IsCarryingIntel { get; init; }

    public float FacingDirectionX { get; init; } = 1f;
}

public sealed class TraversalLabInputStep
{
    public string Label { get; init; } = string.Empty;

    public int DurationTicks { get; init; }

    public bool Left { get; init; }

    public bool Right { get; init; }

    public bool Up { get; init; }

    public bool Down { get; init; }

    public bool FirePrimary { get; init; }

    public bool FireSecondary { get; init; }

    public bool FireSecondaryWeapon { get; init; }

    public bool DropIntel { get; init; }

    public float? AimFacingDirectionX { get; init; }

    public float AimDistance { get; init; } = 256f;

    public float AimOffsetY { get; init; }
}

public sealed class TraversalLabExpectation
{
    public float? FinalX { get; init; }

    public float? FinalBottom { get; init; }

    public float RadiusX { get; init; } = 8f;

    public float RadiusBottom { get; init; } = 8f;

    public float? MinX { get; init; }

    public float? MaxX { get; init; }

    public float? MinBottom { get; init; }

    public float? MaxBottom { get; init; }

    public float MinHorizontalTravel { get; init; }

    public float MinBottomTravel { get; init; }

    public bool? MustBeGrounded { get; init; }

    public bool MustLeaveGround { get; init; }

    public bool MustReground { get; init; }

    public bool? MustCarryIntel { get; init; }

    public bool? MustOverlapEnemyIntelMarker { get; init; }

    public bool? MustBeInsideBlockingTeamGate { get; init; }
}

public readonly record struct TraversalLabVariant(
    float StartXOffset,
    float StartBottomOffset,
    float FacingDirectionX,
    float StartHorizontalSpeedOffset,
    float StartVerticalSpeedOffset,
    bool StartGrounded);

public readonly record struct TraversalLabTickSample(
    int Tick,
    float X,
    float Y,
    float Bottom,
    float HorizontalSpeed,
    float VerticalSpeed,
    bool IsGrounded,
    float FacingDirectionX,
    bool SupportedBelow,
    bool BlockedLeft,
    bool BlockedRight,
    bool IsCarryingIntel,
    bool OverlapsEnemyIntelMarker,
    bool OverlapsOwnIntelMarker,
    bool IsInsideBlockingTeamGate,
    string StepLabel);

public sealed class TraversalLabCaseResult
{
    public required TraversalLabVariant Variant { get; init; }

    public bool Passed { get; init; }

    public required string FailureReason { get; init; }

    public float StartX { get; init; }

    public float StartY { get; init; }

    public float StartBottom { get; init; }

    public bool StartGrounded { get; init; }

    public float FinalX { get; init; }

    public float FinalY { get; init; }

    public float FinalBottom { get; init; }

    public float MinX { get; init; }

    public float MaxX { get; init; }

    public float MinBottom { get; init; }

    public float MaxBottom { get; init; }

    public float HorizontalTravel { get; init; }

    public float BottomTravel { get; init; }

    public bool FinalGrounded { get; init; }

    public bool FinalCarryingIntel { get; init; }

    public bool FinalOverlapsEnemyIntelMarker { get; init; }

    public bool FinalInsideBlockingTeamGate { get; init; }

    public int? FirstLeaveGroundTick { get; init; }

    public int? FirstRegroundTick { get; init; }

    public int? FirstCarryIntelTick { get; init; }

    public int ExecutedTicks { get; init; }

    public required IReadOnlyList<TraversalLabTickSample> Samples { get; init; }
}

public sealed class TraversalLabBatchResult
{
    public required string ScenarioName { get; init; }

    public required IReadOnlyList<TraversalLabCaseResult> Cases { get; init; }

    public int PassedCount => Cases.Count(static result => result.Passed);

    public int FailedCount => Cases.Count - PassedCount;

    public bool Passed => FailedCount == 0;
}

public sealed class TraversalLabPrimitiveSuite
{
    public string Name { get; init; } = "unnamed_suite";

    public PlayerTeam Team { get; init; } = PlayerTeam.Blue;

    public PlayerClass ClassId { get; init; } = PlayerClass.Scout;

    public List<TraversalLabPerturbationProfile> PerturbationProfiles { get; init; } = [];

    public List<TraversalLabPrimitiveDefinition> Primitives { get; init; } = [];
}

public sealed class TraversalLabPerturbationProfile
{
    public string Name { get; init; } = "default";

    public List<float> StartXOffsets { get; init; } = [];

    public List<float> StartBottomOffsets { get; init; } = [];

    public List<float> FacingDirections { get; init; } = [];

    public List<float> StartHorizontalSpeedOffsets { get; init; } = [];

    public List<float> StartVerticalSpeedOffsets { get; init; } = [];

    public List<bool> GroundedStates { get; init; } = [];
}

public sealed class TraversalLabPrimitiveDefinition
{
    public string Name { get; init; } = "unnamed_primitive";

    public string? ArtifactLabel { get; init; }

    public TraversalLabFixtureKind? Fixture { get; init; }

    public string? LevelName { get; init; }

    public int MapAreaIndex { get; init; } = 1;

    public PlayerTeam? Team { get; init; }

    public PlayerClass? ClassId { get; init; }

    public TraversalLabStartState Start { get; init; } = new();

    public List<TraversalLabInputStep> Steps { get; init; } = [];

    public int MaxTicks { get; init; }

    public int TraceEveryTicks { get; init; } = 1;

    public string PerturbationProfile { get; init; } = "default";

    public TraversalLabExpectation? Expectation { get; init; }
}

public sealed class TraversalLabPrimitiveSuiteResult
{
    public required string SuiteName { get; init; }

    public required IReadOnlyList<TraversalLabPrimitiveCertificationResult> PrimitiveResults { get; init; }

    public int PassedCount => PrimitiveResults.Count(static result => result.Passed);

    public int FailedCount => PrimitiveResults.Count - PassedCount;

    public bool Passed => FailedCount == 0;
}

public sealed class TraversalLabPrimitiveCertificationResult
{
    public required string PrimitiveName { get; init; }

    public string? ArtifactLabel { get; init; }

    public required string PerturbationProfileName { get; init; }

    public required TraversalLabBatchResult BatchResult { get; init; }

    public required TraversalLabCertificationEnvelope Envelope { get; init; }

    public bool Passed => BatchResult.Passed;
}

public sealed class TraversalLabCertificationEnvelope
{
    public int TotalVariants { get; init; }

    public int CertifiedVariants { get; init; }

    public float StartXOffsetMin { get; init; }

    public float StartXOffsetMax { get; init; }

    public float StartBottomOffsetMin { get; init; }

    public float StartBottomOffsetMax { get; init; }

    public float StartHorizontalSpeedOffsetMin { get; init; }

    public float StartHorizontalSpeedOffsetMax { get; init; }

    public float StartVerticalSpeedOffsetMin { get; init; }

    public float StartVerticalSpeedOffsetMax { get; init; }

    public float FinalXMin { get; init; }

    public float FinalXMax { get; init; }

    public float FinalBottomMin { get; init; }

    public float FinalBottomMax { get; init; }

    public bool SupportsFacingLeft { get; init; }

    public bool SupportsFacingRight { get; init; }

    public bool SupportsGroundedStart { get; init; }

    public bool SupportsAirborneStart { get; init; }

    public required IReadOnlyList<TraversalLabCertifiedWindow> Windows { get; init; }
}

public sealed class TraversalLabCertifiedWindow
{
    public float StartXOffsetMin { get; init; }

    public float StartXOffsetMax { get; init; }

    public float StartBottomOffset { get; init; }

    public float FacingDirectionX { get; init; }

    public float StartHorizontalSpeedOffset { get; init; }

    public float StartVerticalSpeedOffset { get; init; }

    public bool StartGrounded { get; init; }

    public int CertifiedVariantCount { get; init; }

    public float FinalXMin { get; init; }

    public float FinalXMax { get; init; }

    public float FinalBottomMin { get; init; }

    public float FinalBottomMax { get; init; }

    public bool FinalGrounded { get; init; }
}

public sealed class TraversalLabObjectiveSeamArtifact
{
    public int Version { get; init; } = 1;

    public List<TraversalLabObjectiveSeamCertification> Programs { get; init; } = [];
}

public sealed class TraversalLabObjectiveSeamCertification
{
    public string Label { get; init; } = string.Empty;

    public List<TraversalLabObjectiveSeamStartWindowArtifact> StartWindows { get; init; } = [];

    public List<TraversalLabObjectiveSeamCompletionWindowArtifact> CompletionWindows { get; init; } = [];
}

public sealed class TraversalLabObjectiveSeamStartWindowArtifact
{
    public float StartXMin { get; init; }

    public float StartXMax { get; init; }

    public float StartBottom { get; init; }

    public float StartBottomTolerance { get; init; }

    public float FacingDirectionX { get; init; }

    public float HorizontalSpeedCenter { get; init; }

    public float HorizontalSpeedTolerance { get; init; }

    public float VerticalSpeedCenter { get; init; }

    public float VerticalSpeedTolerance { get; init; }

    public bool RequireGrounded { get; init; }
}

public sealed class TraversalLabObjectiveSeamCompletionWindowArtifact
{
    public float XMin { get; init; }

    public float XMax { get; init; }

    public float Bottom { get; init; }

    public float BottomTolerance { get; init; }

    public bool RequireGrounded { get; init; }
}
