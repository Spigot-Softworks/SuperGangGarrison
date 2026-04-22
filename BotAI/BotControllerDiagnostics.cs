using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public enum BotStateKind
{
    None = 0,
    Respawning = 1,
    TravelObjective = 2,
    SeekHealingCabinet = 3,
    HealAlly = 4,
    CombatAdvance = 5,
    CombatRetreat = 6,
    CombatStrafe = 7,
    Patrol = 8,
    Unstick = 9,
}

public enum BotFocusKind
{
    None = 0,
    Objective = 1,
    Enemy = 2,
    HealTarget = 3,
    HealingCabinet = 4,
}

public readonly record struct BotControllerDiagnosticsEntry(
    byte Slot,
    string DisplayName,
    PlayerTeam Team,
    PlayerClass ClassId,
    BotRole Role,
    BotStateKind State,
    BotFocusKind FocusKind,
    string FocusLabel,
    string RouteLabel,
    bool HasVisibleEnemy,
    int Health,
    int MaxHealth,
    int StuckTicks,
    int ModernStuckTicks,
    int UnstickTicks,
    int CurrentPointId,
    int NextPointId,
    int NextPoint2Id,
    float MovementTargetX,
    float MovementTargetY,
    int RequestedHorizontal,
    string MoveDebug,
    bool RequestedJump,
    string JumpDebug,
    int RouteGoalNodeId,
    float RouteGoalX,
    float RouteGoalY,
    int PreviousCurrentPointId,
    int PreviousNextPointId,
    bool IsGrounded,
    bool ProbeGrounded,
    int SecondAnchorBlockPointId,
    int SecondAnchorBlockTicksRemaining,
    int NoNextPointTicks,
    string FallbackRouteLabel,
    string FallbackTriggerLabel,
    string NavigationIssueLabel,
    int BranchFromPointId,
    int BranchToPointId,
    int BranchTicks,
    int BranchNoProgressTicks,
    int DirectTargetTicks,
    int DirectTargetNoProgressTicks);

public sealed record BotControllerDiagnosticsSnapshot(
    IReadOnlyList<BotControllerDiagnosticsEntry> Entries,
    int AliveBotCount,
    int VisibleEnemyCount,
    int HealFocusCount,
    int CabinetSeekCount,
    int UnstickCount)
{
    public static BotControllerDiagnosticsSnapshot Empty { get; } = new(
        Array.Empty<BotControllerDiagnosticsEntry>(),
        AliveBotCount: 0,
        VisibleEnemyCount: 0,
        HealFocusCount: 0,
        CabinetSeekCount: 0,
        UnstickCount: 0);

    public int ControlledBotCount => Entries.Count;
}
