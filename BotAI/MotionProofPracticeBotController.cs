using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace OpenGarrison.BotAI;

public sealed class MotionProofPracticeBotController : IPracticeBotController
{
    private const float GraphGoalRadius = 160f;
    private const float GraphCtfGoalRadius = 64f;
    private const float GraphAttachRadius = 192f;
    private const float GraphGroundedAttachVerticalTolerance = 96f;
    private const float GraphOpeningActionAttachDistance = 72f;
    private const float GraphOpeningActionHorizontalTolerance = 28f;
    private const float StaticGoalQuantization = 64f;
    private const float DynamicGoalQuantization = 256f;
    private const float NoProgressDistance = 0.1f;
    private const int NoProgressTickLimit = 30;
    private const int EnemyGraphNoProgressTickLimit = 12;
    private const int EnemyGraphRescueJumpTicks = 8;
    private const int DynamicGraphGoalCandidateCount = 160;
    private const int DynamicGraphLineOfSightCandidateCount = 48;
    private const float DynamicGraphGoalDistanceCostScale = 6f;
    private const int DynamicGraphNoLineOfSightPenaltyTicks = 20_000;
    private const float ObjectiveTapeFastForwardSeconds = 0.1f;
    private const int ObjectiveNavigationRescueJumpTicks = NoProgressTickLimit - 2;
    private const float ObstacleProbeDistance = 10f;
    private const int MaxRouteMapCacheEntries = 128;
    private const int MaxGraphRouteActions = 2048;
    private const float DirectSeekHorizontalDeadZone = 14f;
    private const float DirectSeekVerticalJumpThreshold = 26f;
    private const float DirectSeekVerticalDropThreshold = 72f;
    private const float DirectSeekVerticalSearchThreshold = 64f;
    private const int DirectSeekRescueJumpTicks = 8;
    private const float DirectSeekDropProbeStep = 24f;
    private const float DirectSeekDropProbeMinDistance = 96f;
    private const float DirectSeekDropProbeMaxDistance = 420f;
    private const float DirectSeekDropProbeDepth = 56f;
    private const float DirectSeekObstacleProbeDistance = 38f;
    private const float DirectSeekEscapeProbeStep = 36f;
    private const float DirectSeekEscapeProbeMaxDistance = 720f;
    private const float DirectSeekLineOfSightSearchDistance = 720f;
    private const float DirectSeekClimbProbeMaxDistance = 128f;
    private const float DirectSeekClimbProbeStep = 16f;
    private const float DirectSeekClimbClearance = 84f;
    private const int DirectSeekDynamicRescueJumpPulseTicks = 10;
    private const int DirectSeekDynamicRescueJumpPulsePeriodTicks = 24;
    private const int DirectSeekBackupTicks = 6;
    private const int DirectSeekBackupJumpPulseTicks = 8;
    private const int DirectSeekStuckBackupTicks = 12;
    private const int DirectSeekVerticalFlipNoGoalProgressTicks = 90;
    private const int DirectSeekGraphEscalationTicks = 75;
    private const int DirectSeekGraphEscalationNoGoalProgressTicks = 45;
    private const float DirectSeekStateStaleDistance = 384f;
    private const float DirectSeekVerticalSearchHorizontalRadius = 560f;
    private const float SlotStateTeleportResetDistance = 640f;
    private const float LocalObjectiveDirectHorizontalRadius = 560f;
    private const float LocalObjectiveDirectVerticalRadius = 520f;
    private const float LocalObjectiveDirectReleaseHorizontalRadius = 1200f;
    private const float LocalObjectiveDirectReleaseVerticalRadius = 1000f;
    private const int ObjectiveGraphRecoveryTicks = 45;
    private const int ObjectiveTapeNoGoalProgressTicks = 90;
    private const float ObjectiveTerminalDirectRadius = 480f;
    private const float ObjectiveTerminalTightRadius = 96f;
    private const float CtfIntelPickupMarkerSize = 24f;
    private const float TapeReattachStartRadiusX = 12f;
    private const float TapeReattachStartRadiusBottom = 12f;
    private const float TapeStartAlignmentReadyRadiusX = 2f;
    private const float TapeReattachWindowRadiusX = 28f;
    private const float TapeReattachWindowRadiusBottom = 28f;
    private const float TapeReattachDuplicateDistance = 24f;
    private const float TapeReattachSearchDistance = 960f;
    private const float TapeReattachLocalDriftDistance = 256f;
    private const float TapeReattachSpawnSearchDistance = 2048f;
    private const float TapeReattachSpawnStartSeekDistance = 256f;
    private const float TapeExhaustedObjectiveFinishDistance = 160f;
    private const float TapeReattachStableHorizontalSpeed = 1f;
    private const float TapeReattachStableVerticalSpeed = 1f;
    private const float TapeStartAlignmentStableHorizontalSpeed = 0.75f;
    private const float TapeStartAlignmentStableVerticalSpeed = 0.75f;
    private const float SpawnReplayWindowRadiusX = 96f;
    private const float SpawnReplayWindowRadiusBottom = 48f;
    private const float ObjectiveHoldStrafeHalfWidth = 44f;
    private const float ObjectiveCaptureStrafeHalfWidth = 18f;
    private const int ObjectiveHoldStrafePeriodTicks = 32;
    private const int ObjectiveHoldHopPeriodTicks = 54;
    private const int ObjectiveHoldHopPulseTicks = 4;
    private const float ConflictReturnSeamStartX = 845f;
    private const float ConflictReturnSeamStartBottom = 642f;
    private const float ConflictReturnSeamToleranceX = 28f;
    private const float ConflictReturnSeamToleranceBottom = 36f;
    private const float ConflictReturnSeamGoalX = 3252f;
    private const float ConflictReturnSeamGoalBottom = 444f;
    private const float ConflictReturnSeamGoalRadius = 96f;
    private const float ConflictHeavyReturnSeamStartX = 2940f;
    private const float ConflictHeavyReturnSeamStartBottom = 897f;
    private const float ConflictHeavyReturnSeamToleranceX = 2f;
    private const float ConflictHeavyReturnSeamToleranceBottom = 2f;
    private const float ConflictHeavyReturnSeamGoalX = 3252f;
    private const float ConflictHeavyReturnSeamGoalBottom = 444f;
    private const float ConflictHeavyReturnSeamGoalRadius = 96f;
    private const float ConflictQuoteReturnSeamStartX = 709f;
    private const float ConflictQuoteReturnSeamStartBottom = 558f;
    private const float ConflictQuoteReturnSeamToleranceX = 40f;
    private const float ConflictQuoteReturnSeamToleranceBottom = 48f;
    private const float ConflictQuoteReturnSeamGoalX = 3252f;
    private const float ConflictQuoteReturnSeamGoalBottom = 444f;
    private const float ConflictQuoteReturnSeamGoalRadius = 96f;
    private const float ConflictHeavyAttackSeamStartX = 1119f;
    private const float ConflictHeavyAttackSeamStartBottom = 654f;
    private const float ConflictHeavyAttackSeamToleranceX = 32f;
    private const float ConflictHeavyAttackSeamToleranceBottom = 40f;
    private const float ConflictHeavyAttackSeamGoalX = 538f;
    private const float ConflictHeavyAttackSeamGoalBottom = 468f;
    private const float ConflictHeavyAttackSeamGoalRadius = 96f;
    private const float ConflictQuoteFinalReturnSeamStartX = 3234f;
    private const float ConflictQuoteFinalReturnSeamStartBottom = 809f;
    private const float ConflictQuoteFinalReturnSeamToleranceX = 96f;
    private const float ConflictQuoteFinalReturnSeamToleranceBottom = 144f;
    private const float ConflictQuoteFinalReturnSeamGoalX = 3252f;
    private const float ConflictQuoteFinalReturnSeamGoalBottom = 444f;
    private const float ConflictQuoteFinalReturnSeamGoalRadius = 96f;
    private const float ConflictQuoteLateReturnSeamStartX = 2572f;
    private const float ConflictQuoteLateReturnSeamStartBottom = 654f;
    private const float ConflictQuoteLateReturnSeamToleranceX = 24f;
    private const float ConflictQuoteLateReturnSeamToleranceBottom = 24f;
    private const float ConflictQuoteLateReturnSeamGoalX = 3252f;
    private const float ConflictQuoteLateReturnSeamGoalBottom = 444f;
    private const float ConflictQuoteLateReturnSeamGoalRadius = 96f;
    private const float ConflictHeavyFinalReturnSeamStartX = 2570f;
    private const float ConflictHeavyFinalReturnSeamStartBottom = 654f;
    private const float ConflictHeavyFinalReturnSeamToleranceX = 2f;
    private const float ConflictHeavyFinalReturnSeamToleranceBottom = 2f;
    private const float ConflictHeavyFinalReturnSeamGoalX = 3252f;
    private const float ConflictHeavyFinalReturnSeamGoalBottom = 444f;
    private const float ConflictHeavyFinalReturnSeamGoalRadius = 96f;
    private const float ConflictHeavyLateReturnSeamStartX = 3356f;
    private const float ConflictHeavyLateReturnSeamStartBottom = 840f;
    private const float ConflictHeavyLateReturnSeamToleranceX = 48f;
    private const float ConflictHeavyLateReturnSeamToleranceBottom = 56f;
    private const float ConflictHeavyLateReturnSeamGoalX = 3252f;
    private const float ConflictHeavyLateReturnSeamGoalBottom = 444f;
    private const float ConflictHeavyLateReturnSeamGoalRadius = 96f;
    private const float TwodFortTwoScoutAttackSeamStartX = 239f;
    private const float TwodFortTwoScoutAttackSeamStartBottom = 849f;
    private const float TwodFortTwoScoutAttackSeamToleranceX = 24f;
    private const float TwodFortTwoScoutAttackSeamToleranceBottom = 40f;
    private const float TwodFortTwoScoutAttackSeamGoalX = 330f;
    private const float TwodFortTwoScoutAttackSeamGoalBottom = 690f;
    private const float TwodFortTwoScoutAttackSeamGoalRadius = 96f;
    private const float TwodFortTwoScoutReturnSeamStartX = 2753f;
    private const float TwodFortTwoScoutReturnSeamStartBottom = 1164f;
    private const float TwodFortTwoScoutReturnSeamToleranceX = 24f;
    private const float TwodFortTwoScoutReturnSeamToleranceBottom = 24f;
    private const float TwodFortTwoScoutReturnSeamGoalX = 3096f;
    private const float TwodFortTwoScoutReturnSeamGoalBottom = 726f;
    private const float TwodFortTwoScoutReturnSeamGoalRadius = 128f;
    private const float TwodFortTwoScoutReturnRecoverySeamStartX = 1259f;
    private const float TwodFortTwoScoutReturnRecoverySeamStartBottom = 798f;
    private const float TwodFortTwoScoutReturnRecoverySeamToleranceX = 8f;
    private const float TwodFortTwoScoutReturnRecoverySeamToleranceBottom = 12f;
    private const float TwodFortTwoScoutReturnRecoverySeamGoalX = 1749f;
    private const float TwodFortTwoScoutReturnRecoverySeamGoalBottom = 924f;
    private const float TwodFortTwoScoutReturnRecoverySeamGoalRadius = 80f;
    private const float TwodFortTwoScoutDeepRecoverySeamStartX = 1320f;
    private const float TwodFortTwoScoutDeepRecoverySeamStartBottom = 1041f;
    private const float TwodFortTwoScoutDeepRecoverySeamToleranceX = 2f;
    private const float TwodFortTwoScoutDeepRecoverySeamToleranceBottom = 2f;
    private const float TwodFortTwoScoutDeepRecoverySeamGoalX = 1749f;
    private const float TwodFortTwoScoutDeepRecoverySeamGoalBottom = 924f;
    private const float TwodFortTwoScoutDeepRecoverySeamGoalRadius = 80f;
    private const float TwodFortTwoScoutUpperRecoverySeamStartX = 1543f;
    private const float TwodFortTwoScoutUpperRecoverySeamStartBottom = 1109f;
    private const float TwodFortTwoScoutUpperRecoverySeamToleranceX = 8f;
    private const float TwodFortTwoScoutUpperRecoverySeamToleranceBottom = 12f;
    private const float TwodFortTwoScoutUpperRecoverySeamGoalX = 3096f;
    private const float TwodFortTwoScoutUpperRecoverySeamGoalBottom = 726f;
    private const float TwodFortTwoScoutUpperRecoverySeamGoalRadius = 128f;
    private const float TwodFortTwoScoutLateReturnSeamStartX = 3365f;
    private const float TwodFortTwoScoutLateReturnSeamStartBottom = 939f;
    private const float TwodFortTwoScoutLateReturnSeamToleranceX = 80f;
    private const float TwodFortTwoScoutLateReturnSeamToleranceBottom = 80f;
    private const float TwodFortTwoScoutLateReturnSeamGoalX = 3096f;
    private const float TwodFortTwoScoutLateReturnSeamGoalBottom = 726f;
    private const float TwodFortTwoScoutLateReturnSeamGoalRadius = 128f;
    private const float TruefortBlueDemomanAttackSeamStartX = 1230f;
    private const float TruefortBlueDemomanAttackSeamStartBottom = 636f;
    private const float TruefortBlueDemomanAttackSeamToleranceX = 72f;
    private const float TruefortBlueDemomanAttackSeamToleranceBottom = 72f;
    private const float TruefortBlueDemomanAttackSeamGoalX = 384f;
    private const float TruefortBlueDemomanAttackSeamGoalBottom = 864f;
    private const float TruefortBlueDemomanAttackSeamGoalRadius = 96f;
    private const float TruefortBlueDemomanLateAttackSeamStartX = 827f;
    private const float TruefortBlueDemomanLateAttackSeamStartBottom = 546f;
    private const float TruefortBlueDemomanLateAttackSeamToleranceX = 40f;
    private const float TruefortBlueDemomanLateAttackSeamToleranceBottom = 24f;
    private const float TruefortBlueDemomanDeepAttackSeamStartX = 721f;
    private const float TruefortBlueDemomanDeepAttackSeamStartBottom = 756f;
    private const float TruefortBlueDemomanDeepAttackSeamToleranceX = 40f;
    private const float TruefortBlueDemomanDeepAttackSeamToleranceBottom = 24f;
    private const float TruefortBlueDemomanPickupSeamStartX = 417f;
    private const float TruefortBlueDemomanPickupSeamStartBottom = 882f;
    private const float TruefortBlueDemomanPickupSeamToleranceX = 40f;
    private const float TruefortBlueDemomanPickupSeamToleranceBottom = 24f;
    private const float TruefortBlueDemomanPickupSeamGoalX = 361f;
    private const float TruefortBlueDemomanPickupSeamGoalBottom = 882f;
    private const float TruefortBlueDemomanPickupSeamGoalRadius = 12f;
    private const float TruefortBlueDemomanReturnSeamStartX = 4624f;
    private const float TruefortBlueDemomanReturnSeamStartBottom = 576f;
    private const float TruefortBlueDemomanReturnSeamToleranceX = 72f;
    private const float TruefortBlueDemomanReturnSeamToleranceBottom = 72f;
    private const float TruefortBlueDemomanReturnSeamGoalX = 4962f;
    private const float TruefortBlueDemomanReturnSeamGoalBottom = 864f;
    private const float TruefortBlueDemomanReturnSeamGoalRadius = 96f;
    private const float TruefortBlueDemomanFullReturnSeamStartX = 380f;
    private const float TruefortBlueDemomanFullReturnSeamStartBottom = 856f;
    private const float TruefortBlueDemomanFullReturnSeamToleranceX = 128f;
    private const float TruefortBlueDemomanFullReturnSeamToleranceBottom = 64f;
    private const float TruefortBlueDemomanLateReturnPocketSeamStartX = 1126f;
    private const float TruefortBlueDemomanLateReturnPocketSeamStartBottom = 846f;
    private const float TruefortBlueDemomanLateReturnPocketSeamToleranceX = 6f;
    private const float TruefortBlueDemomanLateReturnPocketSeamToleranceBottom = 6f;
    private const float TruefortBlueDemomanLateReturnPocketSeamGoalX = 667f;
    private const float TruefortBlueDemomanLateReturnPocketSeamGoalBottom = 912f;
    private const float TruefortBlueDemomanLateReturnPocketSeamGoalRadius = 48f;
    private const float TruefortBlueDemomanLateReturnStage997SeamStartX = 997f;
    private const float TruefortBlueDemomanLateReturnStage997SeamStartBottom = 846f;
    private const float TruefortBlueDemomanLateReturnStage997SeamToleranceX = 6f;
    private const float TruefortBlueDemomanLateReturnStage997SeamToleranceBottom = 6f;
    private const float TruefortBlueDemomanLateReturnStage997SeamGoalX = 654f;
    private const float TruefortBlueDemomanLateReturnStage997SeamGoalBottom = 912f;
    private const float TruefortBlueDemomanLateReturnStage997SeamGoalRadius = 12f;
    private const float TruefortBlueDemomanLateReturnStage654SeamStartX = 654f;
    private const float TruefortBlueDemomanLateReturnStage654SeamStartBottom = 912f;
    private const float TruefortBlueDemomanLateReturnStage654SeamToleranceX = 20f;
    private const float TruefortBlueDemomanLateReturnStage654SeamToleranceBottom = 12f;
    private const float TruefortBlueDemomanLateReturnStage654SeamGoalX = 445f;
    private const float TruefortBlueDemomanLateReturnStage654SeamGoalBottom = 912f;
    private const float TruefortBlueDemomanLateReturnStage654SeamGoalRadius = 12f;
    private const float TruefortBlueDemomanLateReturnStage445SeamStartX = 445f;
    private const float TruefortBlueDemomanLateReturnStage445SeamStartBottom = 912f;
    private const float TruefortBlueDemomanLateReturnStage445SeamToleranceX = 8f;
    private const float TruefortBlueDemomanLateReturnStage445SeamToleranceBottom = 8f;
    private const float TruefortBlueDemomanLateReturnStage445SeamGoalX = 361f;
    private const float TruefortBlueDemomanLateReturnStage445SeamGoalBottom = 882f;
    private const float TruefortBlueDemomanLateReturnStage445SeamGoalRadius = 12f;
    private const float TruefortBlueDemomanLateReturnStage361SeamStartX = 361f;
    private const float TruefortBlueDemomanLateReturnStage361SeamStartBottom = 882f;
    private const float TruefortBlueDemomanLateReturnStage361SeamToleranceX = 8f;
    private const float TruefortBlueDemomanLateReturnStage361SeamToleranceBottom = 8f;
    private const float TruefortBlueDemomanLateReturnStage361SeamGoalX = 103f;
    private const float TruefortBlueDemomanLateReturnStage361SeamGoalBottom = 696f;
    private const float TruefortBlueDemomanLateReturnStage361SeamGoalRadius = 16f;
    private const float TruefortBlueDemomanLateReturnStage103SeamStartX = 103f;
    private const float TruefortBlueDemomanLateReturnStage103SeamStartBottom = 696f;
    private const float TruefortBlueDemomanLateReturnStage103SeamToleranceX = 8f;
    private const float TruefortBlueDemomanLateReturnStage103SeamToleranceBottom = 8f;
    private const float TruefortBlueDemomanLateReturnStage103SeamGoalX = 706f;
    private const float TruefortBlueDemomanLateReturnStage103SeamGoalBottom = 912f;
    private const float TruefortBlueDemomanLateReturnStage103SeamGoalRadius = 24f;
    private const float GalleryBlueSpawnExitGoalX = 3135f;
    private const float GalleryBlueSpawnExitGoalBottom = 900f;
    private const float GalleryBlueSpawnExitGoalRadius = 64f;
    private const float HealTargetSeekDistance = 360f;
    private const float MedicHealFireDistance = 300f;
    private const float MedicHealFollowDistance = 120f;
    private const float MedicNeedleRange = 420f;
    private const float MedicUberEnemyThreatDistance = 220f;
    private const float LowHealthFraction = 0.3f;
    private const int HeavyIdleEatHealth = 100;
    private const int HeavyCombatEatHealth = 30;
    private const float MineThreatDistance = 400f;
    private const float MineDetonationRadius = 50f;
    private const float CombatSightDistance = 520f;
    private const float CombatSeekVerticalTolerance = 120f;
    private const float CombatSeekDropTolerance = 140f;
    private const float CombatSeekJumpHorizontalRadius = 240f;
    private const float CombatSeekObstacleProbeDistance = 18f;
    private const float SniperScopeDistance = 150f;
    private const int SniperMinimumChargeTicks = 50;
    private const float SoldierOffhandDistance = 260f;
    private const float PyroAirblastDistance = 170f;
    private const float QuoteBladeMinimumDistance = 80f;
    private const float QuoteBladeMaximumDistance = 220f;
    private const string MotionProofContentRelativeDirectory = "Core/Content/MotionProof";
    private const string MotionProofTapesDirectoryName = "tapes";
    private const string MotionProofGraphsDirectoryName = "graphs";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly ObjectiveSeamProgram ConflictBlueEngineerReturnSeamProgram = new(
        "conflict_engineer_return_seam",
        new MotionProofAction[]
        {
            new("Run", -1, 8), new("Run", -1, 8), new("Run", -1, 8), new("Jump", -1, 17),
            new("Drop", -1, 9), new("Jump", 1, 20), new("Run", 1, 8), new("Run", 1, 22),
            new("Run", 1, 34), new("Run", 1, 34), new("Run", 1, 52), new("Jump", 1, 23),
            new("Run", 1, 22), new("Jump", 1, 21), new("Run", 1, 8), new("Jump", 1, 32),
            new("Run", 1, 14), new("Jump", 1, 33), new("Run", 1, 14), new("Jump", 1, 27),
            new("Run", 1, 34), new("Run", 1, 52), new("Jump", 1, 28), new("Run", 1, 40),
        },
        GoalX: ConflictReturnSeamGoalX,
        GoalBottom: ConflictReturnSeamGoalBottom,
        GoalRadius: ConflictReturnSeamGoalRadius,
        DurationTicks: 628,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram ConflictBlueMedicReturnSeamProgram = new(
        "conflict_medic_return_seam",
        new MotionProofAction[]
        {
            new("Run", -1, 22), new("Jump", -1, 17), new("Drop", -1, 7), new("Jump", 0, 7),
            new("Jump", 1, 21), new("Run", 1, 8), new("Jump", 1, 17), new("Run", 1, 22),
            new("Run", 1, 14), new("Run", 1, 34), new("Run", 1, 34), new("Jump", 1, 23),
            new("Run", 1, 14), new("Jump", 1, 21), new("Run", 1, 8), new("Run", 1, 34),
            new("Run", 1, 52), new("Jump", 1, 28), new("Run", 1, 22), new("Run", 1, 34),
            new("Run", 1, 22), new("Jump", 1, 28), new("Run", 1, 35),
        },
        GoalX: ConflictReturnSeamGoalX,
        GoalBottom: ConflictReturnSeamGoalBottom,
        GoalRadius: ConflictReturnSeamGoalRadius,
        DurationTicks: 584,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram ConflictBlueSpyReturnSeamProgram = new(
        "conflict_spy_return_seam",
        new MotionProofAction[]
        {
            new("Run", -1, 22), new("Jump", -1, 17), new("Run", -1, 8), new("Jump", 1, 17),
            new("Run", 1, 22), new("Run", 1, 34), new("Run", 1, 52), new("Run", 1, 34),
            new("Jump", 1, 23), new("Run", 1, 14), new("Run", 1, 14), new("Jump", 1, 23),
            new("Run", 1, 52), new("Run", 1, 34), new("Jump", 1, 26), new("Run", 1, 22),
            new("Run", 1, 52), new("Jump", 1, 28), new("Run", 1, 35),
        },
        GoalX: ConflictReturnSeamGoalX,
        GoalBottom: ConflictReturnSeamGoalBottom,
        GoalRadius: ConflictReturnSeamGoalRadius,
        DurationTicks: 589,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram ConflictBlueQuoteReturnSeamProgram = new(
        "conflict_quote_return_seam",
        new MotionProofAction[]
        {
            new("Run", -1, 8), new("Jump", 1, 20), new("Run", 1, 8), new("Run", 1, 22),
            new("Run", 1, 8), new("Drop", 1, 17), new("Run", 1, 52), new("Jump", 1, 28),
            new("Jump", 1, 24), new("Run", 1, 22), new("Run", 1, 8), new("Jump", 1, 21),
            new("Jump", 1, 34), new("Run", 1, 8), new("Jump", 1, 7), new("Run", 1, 34),
            new("Jump", 1, 31), new("Jump", 1, 23), new("Run", 1, 52), new("Jump", 1, 29),
            new("Run", 1, 37),
        },
        GoalX: ConflictQuoteReturnSeamGoalX,
        GoalBottom: ConflictQuoteReturnSeamGoalBottom,
        GoalRadius: ConflictQuoteReturnSeamGoalRadius,
        DurationTicks: 493,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram ConflictBlueHeavyReturnSeamProgram = new(
        "conflict_heavy_return_seam",
        new MotionProofAction[]
        {
            new("Run", 1, 22), new("Drop", 1, 18), new("Drop", 1, 7), new("Run", 1, 52),
        },
        GoalX: ConflictHeavyLateReturnSeamStartX,
        GoalBottom: ConflictHeavyLateReturnSeamStartBottom,
        GoalRadius: 64f,
        DurationTicks: 99,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram ConflictBlueQuoteFinalReturnSeamProgram = new(
        "conflict_quote_final_return_seam",
        new MotionProofAction[]
        {
            new("Run", 1, 8), new("Run", 1, 8), new("Run", 1, 52), new("Jump", -1, 18), new("Run", -1, 43),
        },
        GoalX: ConflictQuoteFinalReturnSeamGoalX,
        GoalBottom: ConflictQuoteFinalReturnSeamGoalBottom,
        GoalRadius: ConflictQuoteFinalReturnSeamGoalRadius,
        DurationTicks: 169,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram ConflictBlueQuoteLateReturnSeamProgram = new(
        "conflict_quote_late_return_seam",
        new MotionProofAction[]
        {
            new("Run", -1, 14), new("Drop", -1, 7), new("Run", -1, 14), new("Jump", 1, 19),
            new("Drop", 1, 12), new("Run", 1, 8), new("Run", 1, 52), new("Jump", 1, 28),
            new("Run", 1, 37),
        },
        GoalX: ConflictQuoteLateReturnSeamGoalX,
        GoalBottom: ConflictQuoteLateReturnSeamGoalBottom,
        GoalRadius: ConflictQuoteLateReturnSeamGoalRadius,
        DurationTicks: 191,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram ConflictBlueHeavyLateReturnSeamProgram = new(
        "conflict_heavy_late_return_seam",
        new MotionProofAction[]
        {
            new("Drop", 1, 10), new("Run", 1, 14), new("Run", 1, 34),
            new("Jump", -1, 18), new("Drop", -1, 7), new("Run", -1, 51),
        },
        GoalX: ConflictHeavyLateReturnSeamGoalX,
        GoalBottom: ConflictHeavyLateReturnSeamGoalBottom,
        GoalRadius: ConflictHeavyLateReturnSeamGoalRadius,
        DurationTicks: 134,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram ConflictBlueHeavyFinalReturnSeamProgram = new(
        "conflict_heavy_final_return_seam",
        new MotionProofAction[]
        {
            new("Run", -1, 8), new("Run", -1, 34), new("Jump", 1, 19), new("Run", 1, 14),
            new("Run", 1, 22), new("Run", 1, 34), new("Run", 1, 14), new("Jump", 1, 31),
            new("Jump", 1, 28), new("Run", 1, 48),
        },
        GoalX: ConflictHeavyFinalReturnSeamGoalX,
        GoalBottom: ConflictHeavyFinalReturnSeamGoalBottom,
        GoalRadius: ConflictHeavyFinalReturnSeamGoalRadius,
        DurationTicks: 252,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram ConflictBlueHeavyAttackSeamProgram = new(
        "conflict_heavy_attack_seam",
        new MotionProofAction[]
        {
            new("Run", 1, 14), new("Drop", 1, 7), new("Jump", 1, 17), new("Jump", -1, 17),
            new("Drop", -1, 37), new("Run", -1, 52), new("Run", -1, 52),
        },
        GoalX: ConflictHeavyAttackSeamGoalX,
        GoalBottom: ConflictHeavyAttackSeamGoalBottom,
        GoalRadius: ConflictHeavyAttackSeamGoalRadius,
        DurationTicks: 256,
        NoMovementTicks: 20);

    private static readonly ObjectiveSeamProgram TwodFortTwoBlueScoutAttackSeamProgram = new(
        "twodforttwo_scout_attack_seam",
        new MotionProofAction[]
        {
            new("Run", -1, 8), new("Jump", -1, 15), new("Jump", 1, 18), new("Run", 1, 16),
        },
        GoalX: TwodFortTwoScoutAttackSeamGoalX,
        GoalBottom: TwodFortTwoScoutAttackSeamGoalBottom,
        GoalRadius: TwodFortTwoScoutAttackSeamGoalRadius,
        DurationTicks: 97,
        NoMovementTicks: 12);

    private static readonly ObjectiveSeamProgram TwodFortTwoBlueScoutReturnSeamProgram = new(
        "twodforttwo_scout_return_seam",
        new MotionProofAction[]
        {
            new("Jump", 1, 29), new("Jump", 1, 28), new("Jump", 1, 18), new("Jump", 1, 17),
            new("Jump", -1, 17), new("Run", -1, 8), new("Run", 1, 8), new("Jump", 1, 17),
            new("Jump", -1, 18), new("Run", -1, 16),
        },
        GoalX: TwodFortTwoScoutReturnSeamGoalX,
        GoalBottom: TwodFortTwoScoutReturnSeamGoalBottom,
        GoalRadius: TwodFortTwoScoutReturnSeamGoalRadius,
        DurationTicks: 176,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram TwodFortTwoBlueScoutReturnRecoverySeamProgram = new(
        "twodforttwo_scout_return_recovery_seam",
        new MotionProofAction[]
        {
            new("Jump", 1, 41), new("Run", 1, 29),
        },
        GoalX: TwodFortTwoScoutReturnRecoverySeamGoalX,
        GoalBottom: TwodFortTwoScoutReturnRecoverySeamGoalBottom,
        GoalRadius: TwodFortTwoScoutReturnRecoverySeamGoalRadius,
        DurationTicks: 70,
        NoMovementTicks: 14);

    private static readonly ObjectiveSeamProgram TwodFortTwoBlueScoutDeepRecoverySeamProgram = new(
        "twodforttwo_scout_deep_recovery_seam",
        new MotionProofAction[]
        {
            new("Run", 1, 8), new("Run", -1, 8), new("Run", -1, 8), new("Run", -1, 34),
            new("Run", -1, 52), new("Jump", 0, 22), new("Jump", 1, 17), new("Run", 1, 22),
            new("Jump", 1, 21), new("Jump", 1, 18), new("Jump", 1, 23), new("Jump", 1, 43),
            new("Run", 1, 11),
        },
        GoalX: TwodFortTwoScoutDeepRecoverySeamGoalX,
        GoalBottom: TwodFortTwoScoutDeepRecoverySeamGoalBottom,
        GoalRadius: TwodFortTwoScoutDeepRecoverySeamGoalRadius,
        DurationTicks: 287,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram TwodFortTwoBlueScoutUpperRecoverySeamProgram = new(
        "twodforttwo_scout_upper_recovery_seam",
        new MotionProofAction[]
        {
            new("Run", -1, 8), new("Jump", -1, 32), new("Run", -1, 34), new("Run", -1, 52),
            new("Jump", 0, 21), new("Jump", 1, 17), new("Run", 1, 22), new("Jump", 1, 21),
            new("Jump", 1, 17), new("Jump", 1, 21), new("Jump", 1, 43), new("Run", 1, 15),
        },
        GoalX: TwodFortTwoScoutUpperRecoverySeamGoalX,
        GoalBottom: TwodFortTwoScoutUpperRecoverySeamGoalBottom,
        GoalRadius: TwodFortTwoScoutUpperRecoverySeamGoalRadius,
        DurationTicks: 303,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram TwodFortTwoBlueScoutLateReturnSeamProgram = new(
        "twodforttwo_scout_late_return_seam",
        new MotionProofAction[]
        {
            new("Run", -1, 8), new("Jump", -1, 15), new("Jump", 1, 17),
            new("Run", 1, 8), new("Jump", -1, 23), new("Run", -1, 14),
        },
        GoalX: TwodFortTwoScoutLateReturnSeamGoalX,
        GoalBottom: TwodFortTwoScoutLateReturnSeamGoalBottom,
        GoalRadius: TwodFortTwoScoutLateReturnSeamGoalRadius,
        DurationTicks: 85,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram TruefortBlueDemomanAttackSeamProgram = new(
        "truefort_blue_demoman_attack_seam",
        new MotionProofAction[]
        {
            new("Run", -1, 34),
            new("Jump", 1, 18),
            new("Run", 1, 8),
            new("Run", -1, 14),
            new("Jump", -1, 20),
            new("Drop", -1, 30),
            new("Run", -1, 8),
            new("Run", 1, 14),
            new("Run", -1, 8),
            new("Run", 1, 14),
            new("Run", -1, 8),
            new("Run", -1, 52),
            new("Jump", -1, 22),
        },
        GoalX: TruefortBlueDemomanAttackSeamGoalX,
        GoalBottom: TruefortBlueDemomanAttackSeamGoalBottom,
        GoalRadius: TruefortBlueDemomanAttackSeamGoalRadius,
        DurationTicks: 250,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram TruefortBlueDemomanReturnSeamProgram = new(
        "truefort_blue_demoman_return_seam",
        new MotionProofAction[]
        {
            new("Run", -1, 8),
            new("Run", -1, 22),
            new("Run", 1, 14),
            new("Run", -1, 8),
            new("Jump", 1, 12),
            new("Run", 1, 34),
            new("Jump", 1, 23),
            new("Jump", 1, 22),
        },
        GoalX: TruefortBlueDemomanReturnSeamGoalX,
        GoalBottom: TruefortBlueDemomanReturnSeamGoalBottom,
        GoalRadius: TruefortBlueDemomanReturnSeamGoalRadius,
        DurationTicks: 143,
        NoMovementTicks: 90);

    // Mirrored from the proven Truefort Red Demoman return path.
    private static readonly ObjectiveSeamProgram TruefortBlueDemomanFullReturnSeamProgram = new(
        "truefort_blue_demoman_full_return_seam",
        new MotionProofAction[]
        {
            new("Jump", 1, 31),
            new("Drop", 1, 73),
            new("Jump", -1, 19),
            new("Drop", -1, 7),
            new("Jump", 1, 21),
            new("Drop", 1, 7),
            new("Jump", -1, 20),
            new("Run", -1, 8),
            new("Drop", 1, 7),
            new("Jump", 1, 17),
            new("Run", 1, 8),
            new("Run", 1, 34),
            new("Run", 1, 34),
            new("Jump", 1, 43),
            new("Run", 1, 34),
            new("Run", 1, 34),
            new("Run", 1, 52),
            new("Jump", 1, 23),
            new("Run", 1, 52),
            new("Jump", 1, 28),
            new("Run", 1, 14),
            new("Run", 1, 22),
            new("Jump", 1, 28),
            new("Run", 1, 52),
            new("Jump", 1, 28),
            new("Drop", 1, 13),
            new("Run", 1, 52),
            new("Jump", 1, 24),
            new("Run", 1, 14),
            new("Run", 1, 34),
            new("Jump", 1, 28),
            new("Run", 1, 14),
            new("Run", 1, 22),
            new("Drop", -1, 7),
            new("Jump", -1, 17),
            new("Run", 1, 14),
            new("Jump", 1, 20),
            new("Run", 1, 14),
            new("Jump", 1, 11),
            new("Run", 1, 14),
            new("Run", -1, 8),
            new("Run", -1, 8),
            new("Run", 1, 8),
            new("Run", -1, 8),
            new("Jump", 1, 13),
            new("Run", 1, 8),
            new("Run", 1, 34),
            new("Run", 1, 22),
            new("Jump", 1, 22),
        },
        GoalX: TruefortBlueDemomanReturnSeamGoalX,
        GoalBottom: TruefortBlueDemomanReturnSeamGoalBottom,
        GoalRadius: TruefortBlueDemomanReturnSeamGoalRadius,
        DurationTicks: 1125,
        NoMovementTicks: 120);

    private static readonly ObjectiveSeamProgram TruefortBlueDemomanLateReturnPocketRetreatSeamProgram = new(
        "truefort_blue_demoman_late_return_pocket_retreat",
        new MotionProofAction[]
        {
            new("Run", -1, 120),
        },
        GoalX: TruefortBlueDemomanLateReturnStage997SeamStartX,
        GoalBottom: TruefortBlueDemomanLateReturnStage997SeamStartBottom,
        GoalRadius: 8f,
        DurationTicks: 120,
        NoMovementTicks: 18,
        StartWindows:
        [
            new ObjectiveSeamStartWindow(
                StartXMin: 1094f,
                StartXMax: 1130f,
                StartBottom: TruefortBlueDemomanLateReturnPocketSeamStartBottom,
                StartBottomTolerance: TruefortBlueDemomanLateReturnPocketSeamToleranceBottom,
                FacingDirectionX: 1f,
                HorizontalSpeedTolerance: TapeStartAlignmentStableHorizontalSpeed,
                VerticalSpeedTolerance: TapeStartAlignmentStableVerticalSpeed,
                RequireGrounded: true),
        ],
        CompletionWindow: new ObjectiveSeamCompletionWindow(
            XMin: 997f,
            XMax: 1013f,
            Bottom: TruefortBlueDemomanLateReturnStage997SeamStartBottom,
            BottomTolerance: TruefortBlueDemomanLateReturnStage997SeamToleranceBottom,
            RequireGrounded: true));

    private static readonly ObjectiveSeamProgram TruefortBlueDemomanLateReturnStage997SeamProgram = new(
        "truefort_blue_demoman_late_return_stage_997_seam",
        new MotionProofAction[]
        {
            new("Jump", -1, 28),
            new("Run", -1, 44),
        },
        GoalX: TruefortBlueDemomanLateReturnStage997SeamGoalX,
        GoalBottom: TruefortBlueDemomanLateReturnStage997SeamGoalBottom,
        GoalRadius: TruefortBlueDemomanLateReturnStage997SeamGoalRadius,
        DurationTicks: 72,
        NoMovementTicks: 18,
        StartWindows:
        [
            new ObjectiveSeamStartWindow(
                StartXMin: 997f,
                StartXMax: 1013f,
                StartBottom: TruefortBlueDemomanLateReturnStage997SeamStartBottom,
                StartBottomTolerance: TruefortBlueDemomanLateReturnStage997SeamToleranceBottom,
                FacingDirectionX: 1f,
                HorizontalSpeedTolerance: TapeStartAlignmentStableHorizontalSpeed,
                VerticalSpeedTolerance: TapeStartAlignmentStableVerticalSpeed,
                RequireGrounded: true),
            new ObjectiveSeamStartWindow(
                StartXMin: 997f,
                StartXMax: 1013f,
                StartBottom: TruefortBlueDemomanLateReturnStage997SeamStartBottom,
                StartBottomTolerance: TruefortBlueDemomanLateReturnStage997SeamToleranceBottom,
                FacingDirectionX: -1f,
                HorizontalSpeedTolerance: TapeStartAlignmentStableHorizontalSpeed,
                VerticalSpeedTolerance: TapeStartAlignmentStableVerticalSpeed,
                RequireGrounded: true),
        ],
        CompletionWindow: new ObjectiveSeamCompletionWindow(
            XMin: 677f,
            XMax: 695f,
            Bottom: TruefortBlueDemomanLateReturnStage654SeamStartBottom,
            BottomTolerance: TruefortBlueDemomanLateReturnStage654SeamToleranceBottom,
            RequireGrounded: true));

    private static readonly ObjectiveSeamProgram TruefortBlueDemomanLateReturnStage654SeamProgram = new(
        "truefort_blue_demoman_late_return_stage_654_seam",
        new MotionProofAction[]
        {
            new("Run", -1, 60),
        },
        GoalX: TruefortBlueDemomanLateReturnStage654SeamGoalX,
        GoalBottom: TruefortBlueDemomanLateReturnStage654SeamGoalBottom,
        GoalRadius: TruefortBlueDemomanLateReturnStage654SeamGoalRadius,
        DurationTicks: 60,
        NoMovementTicks: 18,
        StartWindows:
        [
            new ObjectiveSeamStartWindow(
                StartXMin: 677f,
                StartXMax: 695f,
                StartBottom: TruefortBlueDemomanLateReturnStage654SeamStartBottom,
                StartBottomTolerance: TruefortBlueDemomanLateReturnStage654SeamToleranceBottom,
                FacingDirectionX: 1f,
                HorizontalSpeedTolerance: TapeStartAlignmentStableHorizontalSpeed,
                VerticalSpeedTolerance: TapeStartAlignmentStableVerticalSpeed,
                RequireGrounded: true),
            new ObjectiveSeamStartWindow(
                StartXMin: 677f,
                StartXMax: 695f,
                StartBottom: TruefortBlueDemomanLateReturnStage654SeamStartBottom,
                StartBottomTolerance: TruefortBlueDemomanLateReturnStage654SeamToleranceBottom,
                FacingDirectionX: -1f,
                HorizontalSpeedTolerance: TapeStartAlignmentStableHorizontalSpeed,
                VerticalSpeedTolerance: TapeStartAlignmentStableVerticalSpeed,
                RequireGrounded: true),
        ],
        CompletionWindow: new ObjectiveSeamCompletionWindow(
            XMin: 445f,
            XMax: 446f,
            Bottom: TruefortBlueDemomanLateReturnStage654SeamGoalBottom,
            BottomTolerance: TruefortBlueDemomanLateReturnStage445SeamToleranceBottom,
            RequireGrounded: true));

    private static readonly ObjectiveSeamProgram TruefortBlueDemomanLateReturnStage445SeamProgram = new(
        "truefort_blue_demoman_late_return_stage_445_seam",
        new MotionProofAction[]
        {
            new("Jump", -1, 28),
            new("Run", -1, 32),
        },
        GoalX: TruefortBlueDemomanLateReturnStage445SeamGoalX,
        GoalBottom: TruefortBlueDemomanLateReturnStage445SeamGoalBottom,
        GoalRadius: TruefortBlueDemomanLateReturnStage445SeamGoalRadius,
        DurationTicks: 60,
        NoMovementTicks: 18,
        StartWindows:
        [
            new ObjectiveSeamStartWindow(
                StartXMin: 445f,
                StartXMax: 461f,
                StartBottom: TruefortBlueDemomanLateReturnStage445SeamStartBottom,
                StartBottomTolerance: TruefortBlueDemomanLateReturnStage445SeamToleranceBottom,
                FacingDirectionX: 1f,
                HorizontalSpeedTolerance: TapeStartAlignmentStableHorizontalSpeed,
                VerticalSpeedTolerance: TapeStartAlignmentStableVerticalSpeed,
                RequireGrounded: true),
            new ObjectiveSeamStartWindow(
                StartXMin: 445f,
                StartXMax: 461f,
                StartBottom: TruefortBlueDemomanLateReturnStage445SeamStartBottom,
                StartBottomTolerance: TruefortBlueDemomanLateReturnStage445SeamToleranceBottom,
                FacingDirectionX: -1f,
                HorizontalSpeedTolerance: TapeStartAlignmentStableHorizontalSpeed,
                VerticalSpeedTolerance: TapeStartAlignmentStableVerticalSpeed,
                RequireGrounded: true),
        ],
        CompletionWindow: new ObjectiveSeamCompletionWindow(
            XMin: 361f,
            XMax: 362f,
            Bottom: TruefortBlueDemomanLateReturnStage445SeamGoalBottom,
            BottomTolerance: TruefortBlueDemomanLateReturnStage361SeamToleranceBottom,
            RequireGrounded: true));

    private static readonly ObjectiveSeamProgram TruefortBlueDemomanLateReturnStage361SeamProgram = new(
        "truefort_blue_demoman_late_return_stage_361_seam",
        new MotionProofAction[]
        {
            new("Jump", -1, 32),
            new("Run", -1, 40),
        },
        GoalX: TruefortBlueDemomanLateReturnStage361SeamGoalX,
        GoalBottom: TruefortBlueDemomanLateReturnStage361SeamGoalBottom,
        GoalRadius: TruefortBlueDemomanLateReturnStage361SeamGoalRadius,
        DurationTicks: 72,
        NoMovementTicks: 18,
        StartWindows:
        [
            new ObjectiveSeamStartWindow(
                StartXMin: 361f,
                StartXMax: 377f,
                StartBottom: TruefortBlueDemomanLateReturnStage361SeamStartBottom,
                StartBottomTolerance: TruefortBlueDemomanLateReturnStage361SeamToleranceBottom,
                FacingDirectionX: 1f,
                HorizontalSpeedTolerance: TapeStartAlignmentStableHorizontalSpeed,
                VerticalSpeedTolerance: TapeStartAlignmentStableVerticalSpeed,
                RequireGrounded: true),
            new ObjectiveSeamStartWindow(
                StartXMin: 361f,
                StartXMax: 377f,
                StartBottom: TruefortBlueDemomanLateReturnStage361SeamStartBottom,
                StartBottomTolerance: TruefortBlueDemomanLateReturnStage361SeamToleranceBottom,
                FacingDirectionX: -1f,
                HorizontalSpeedTolerance: TapeStartAlignmentStableHorizontalSpeed,
                VerticalSpeedTolerance: TapeStartAlignmentStableVerticalSpeed,
                RequireGrounded: true),
        ],
        CompletionWindow: new ObjectiveSeamCompletionWindow(
            XMin: 103f,
            XMax: 139f,
            Bottom: TruefortBlueDemomanLateReturnStage361SeamGoalBottom,
            BottomTolerance: TruefortBlueDemomanLateReturnStage103SeamToleranceBottom,
            RequireGrounded: true));

    private static readonly ObjectiveSeamProgram TruefortBlueDemomanLateReturnStage103SeamProgram = new(
        "truefort_blue_demoman_late_return_stage_103_seam",
        new MotionProofAction[]
        {
            new("Run", 1, 140),
        },
        GoalX: TruefortBlueDemomanLateReturnStage103SeamGoalX,
        GoalBottom: TruefortBlueDemomanLateReturnStage103SeamGoalBottom,
        GoalRadius: TruefortBlueDemomanLateReturnStage103SeamGoalRadius,
        DurationTicks: 140,
        NoMovementTicks: 18,
        StartWindows:
        [
            new ObjectiveSeamStartWindow(
                StartXMin: 103f,
                StartXMax: 119f,
                StartBottom: TruefortBlueDemomanLateReturnStage103SeamStartBottom,
                StartBottomTolerance: TruefortBlueDemomanLateReturnStage103SeamToleranceBottom,
                FacingDirectionX: 1f,
                HorizontalSpeedTolerance: TapeStartAlignmentStableHorizontalSpeed,
                VerticalSpeedTolerance: TapeStartAlignmentStableVerticalSpeed,
                RequireGrounded: true),
            new ObjectiveSeamStartWindow(
                StartXMin: 103f,
                StartXMax: 119f,
                StartBottom: TruefortBlueDemomanLateReturnStage103SeamStartBottom,
                StartBottomTolerance: TruefortBlueDemomanLateReturnStage103SeamToleranceBottom,
                FacingDirectionX: -1f,
                HorizontalSpeedTolerance: TapeStartAlignmentStableHorizontalSpeed,
                VerticalSpeedTolerance: TapeStartAlignmentStableVerticalSpeed,
                RequireGrounded: true),
        ],
        CompletionWindow: new ObjectiveSeamCompletionWindow(
            XMin: 766f,
            XMax: 787f,
            Bottom: TruefortBlueDemomanLateReturnStage103SeamGoalBottom,
            BottomTolerance: TruefortBlueDemomanLateReturnStage654SeamToleranceBottom,
            RequireGrounded: true));

    private static readonly ObjectiveSeamProgram TruefortBlueDemomanLateAttackSeamProgram = new(
        "truefort_blue_demoman_late_attack_seam",
        new MotionProofAction[]
        {
            new("Run", -1, 8),
            new("Run", 1, 8),
            new("Run", 1, 8),
            new("Run", -1, 14),
            new("Run", 1, 8),
            new("Jump", -1, 11),
            new("Run", -1, 22),
            new("Run", -1, 22),
            new("Jump", -1, 25),
            new("Run", -1, 1),
        },
        GoalX: TruefortBlueDemomanAttackSeamGoalX,
        GoalBottom: TruefortBlueDemomanAttackSeamGoalBottom,
        GoalRadius: TruefortBlueDemomanAttackSeamGoalRadius,
        DurationTicks: 127,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram TruefortBlueDemomanDeepAttackSeamProgram = new(
        "truefort_blue_demoman_deep_attack_seam",
        new MotionProofAction[]
        {
            new("Run", 1, 14),
            new("Run", 1, 14),
            new("Run", -1, 14),
            new("Run", -1, 22),
            new("Run", -1, 22),
            new("Jump", -1, 25),
            new("Run", -1, 1),
        },
        GoalX: TruefortBlueDemomanAttackSeamGoalX,
        GoalBottom: TruefortBlueDemomanAttackSeamGoalBottom,
        GoalRadius: TruefortBlueDemomanAttackSeamGoalRadius,
        DurationTicks: 112,
        NoMovementTicks: 18);

    private static readonly ObjectiveSeamProgram TruefortBlueDemomanPickupSeamProgram = new(
        "truefort_blue_demoman_pickup_seam",
        new MotionProofAction[]
        {
            new("Run", -1, 24),
        },
        GoalX: TruefortBlueDemomanPickupSeamGoalX,
        GoalBottom: TruefortBlueDemomanPickupSeamGoalBottom,
        GoalRadius: TruefortBlueDemomanPickupSeamGoalRadius,
        DurationTicks: 24,
        NoMovementTicks: 12);

    private static readonly ObjectiveSeamProgram GalleryBlueSpawnExitSeamProgram = new(
        "gallery_blue_spawn_exit",
        new MotionProofAction[]
        {
            new("Run", -1, 24), new("Run", -1, 24),
        },
        GoalX: GalleryBlueSpawnExitGoalX,
        GoalBottom: GalleryBlueSpawnExitGoalBottom,
        GoalRadius: GalleryBlueSpawnExitGoalRadius,
        DurationTicks: 48,
        NoMovementTicks: 18);

    private readonly Dictionary<byte, ReplayState> _statesBySlot = new();
    private readonly Dictionary<string, PreparedMotionProofArtifact?> _artifactsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PreparedMotionGraph?> _graphsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GraphRouteMap> _routeMapsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<byte, GraphReplayState> _graphStatesBySlot = new();
    private readonly Dictionary<byte, DirectSeekState> _directSeekStatesBySlot = new();
    private readonly Dictionary<byte, ObjectiveSeamState> _objectiveSeamStatesBySlot = new();
    private readonly Dictionary<byte, ObjectiveSeamProgram> _deferredObjectiveSeamProgramsBySlot = new();
    private readonly Dictionary<byte, string> _objectiveSeamIssueBySlot = new();
    private readonly Dictionary<byte, int> _objectiveHoldPointBySlot = new();
    private readonly Dictionary<byte, int> _localObjectiveDirectPointBySlot = new();
    private readonly Dictionary<byte, bool> _spawnReattachEligibleBySlot = new();
    private readonly Dictionary<byte, PlayerInputSnapshot> _inputs = new();
    private readonly Dictionary<byte, bool> _aliveBySlot = new();
    private readonly Random _random = new(1337);

    public bool CollectDiagnostics { get; set; }

    public BotControllerDiagnosticsSnapshot LastDiagnostics { get; private set; } = BotControllerDiagnosticsSnapshot.Empty;

    public void Reset()
    {
        _statesBySlot.Clear();
        _graphStatesBySlot.Clear();
        _directSeekStatesBySlot.Clear();
        _objectiveSeamStatesBySlot.Clear();
        _deferredObjectiveSeamProgramsBySlot.Clear();
        _objectiveSeamIssueBySlot.Clear();
        _objectiveHoldPointBySlot.Clear();
        _localObjectiveDirectPointBySlot.Clear();
        _spawnReattachEligibleBySlot.Clear();
        _inputs.Clear();
        _aliveBySlot.Clear();
        _artifactsByKey.Clear();
        _graphsByKey.Clear();
        _routeMapsByKey.Clear();
        LastDiagnostics = BotControllerDiagnosticsSnapshot.Empty;
    }

    public void ConfigureSpawnOverrides(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        foreach (var entry in controlledSlots)
        {
            var controlledSlot = entry.Value;
            var artifact = GetArtifact(world, controlledSlot);
            if (artifact is null
                || float.IsNaN(artifact.AttackPhase.StartX)
                || float.IsNaN(artifact.AttackPhase.StartBottom))
            {
                world.TryClearNetworkPlayerSpawnOverride(entry.Key);
                continue;
            }

            var definition = CharacterClassCatalog.GetDefinition(controlledSlot.ClassId);
            var spawnY = artifact.AttackPhase.StartBottom - (definition.CollisionBottom * world.ConfiguredPlayerScale);
            world.TrySetNetworkPlayerSpawnOverride(entry.Key, artifact.AttackPhase.StartX, spawnY);
        }
    }

    public IReadOnlyDictionary<byte, PlayerInputSnapshot> BuildInputs(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        _inputs.Clear();
        var diagnostics = CollectDiagnostics ? new List<BotControllerDiagnosticsEntry>(controlledSlots.Count) : null;
        var allPlayers = world.EnumerateActiveNetworkPlayers()
            .Select(entry => entry.Item2)
            .ToArray();
        foreach (var entry in controlledSlots)
        {
            var slot = entry.Key;
            var controlledSlot = entry.Value;
            if (!world.TryGetNetworkPlayer(slot, out var player))
            {
                ResetSlotState(slot);
                _inputs[slot] = default;
                continue;
            }

            if (!player.IsAlive)
            {
                ResetSlotState(slot);
                _aliveBySlot[slot] = false;
                var idleInput = BuildIdleInput(world, player);
                _inputs[slot] = idleInput;
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, new GraphGoal("respawning", player.X, player.Bottom, GraphGoalRadius), idleInput));
                continue;
            }

            var hadAliveState = _aliveBySlot.TryGetValue(slot, out var wasAlive);
            if (!hadAliveState || !wasAlive)
            {
                ResetSlotState(slot);
                _spawnReattachEligibleBySlot[slot] = true;
            }
            else if (ShouldResetSlotStateForTeleport(slot, player))
            {
                ResetSlotState(slot);
            }

            _aliveBySlot[slot] = true;
            var hadActiveSeamStateAtTickStart = _objectiveSeamStatesBySlot.TryGetValue(slot, out var activeSeamStateAtTickStart);
            if (TryGetDirectEnemyGoal(world, player, out var enemyGoal))
            {
                _statesBySlot.Remove(slot);
                if (ShouldEscalateEnemyChaseToGraph(slot, world, player, enemyGoal))
                {
                    var enemyGraph = GetGraph(world, controlledSlot);
                    if (enemyGraph is not null
                        && TryBuildGraphInput(slot, world, controlledSlot, enemyGraph, enemyGoal, player, out var enemyGraphInput))
                    {
                        _inputs[slot] = ApplyCombatInput(world, player, enemyGraphInput, allPlayers);
                        diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, enemyGoal, _inputs[slot]));
                        continue;
                    }
                }

                _graphStatesBySlot.Remove(slot);
                var directInput = BuildDirectSeekInput(slot, world, controlledSlot.Team, player, enemyGoal);
                _inputs[slot] = ApplyCombatInput(world, player, directInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, enemyGoal, _inputs[slot]));
                continue;
            }

            var artifact = GetArtifact(world, controlledSlot);
            var graph = GetGraph(world, controlledSlot);
            GraphGoal graphGoal = default;
            var hasObjectiveGoal = TryGetGraphGoal(world, controlledSlot.Team, player, out graphGoal);
            var hasGraphGoal = graph is not null && hasObjectiveGoal;

            if (hadActiveSeamStateAtTickStart && !hasObjectiveGoal)
            {
                LogObjectiveSeamEvent(slot, player, activeSeamStateAtTickStart.Program, "seam_skipped:no_objective_goal");
            }

            if (hasObjectiveGoal
                && TryBuildObjectiveSeamInput(slot, world, controlledSlot, player, graphGoal, out var seamInput, out var seamGoal))
            {
                _inputs[slot] = ApplyCombatInput(
                    world,
                    player,
                    seamInput,
                    allPlayers,
                    preservePassiveAim: true,
                    preserveMovementAuthority: true);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, seamGoal, _inputs[slot]));
                continue;
            }

            if (hadActiveSeamStateAtTickStart)
            {
                var seamIssue = _objectiveSeamIssueBySlot.TryGetValue(slot, out var issue) ? issue : "none";
                var seamStillActive = _objectiveSeamStatesBySlot.TryGetValue(slot, out var survivingSeamState);
                var message = seamStillActive
                    ? $"seam_skipped:fallthrough:{seamIssue}"
                    : $"seam_lost_before_fallthrough:{seamIssue}";
                LogObjectiveSeamEvent(
                    slot,
                    player,
                    seamStillActive ? survivingSeamState.Program : activeSeamStateAtTickStart.Program,
                    message);
            }

            if (hasObjectiveGoal
                && TryBuildObjectiveTerminalInput(slot, world, controlledSlot.Team, player, graphGoal, out var terminalInput))
            {
                _localObjectiveDirectPointBySlot.Remove(slot);
                _graphStatesBySlot.Remove(slot);
                _inputs[slot] = ApplyCombatInput(world, player, terminalInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                continue;
            }

            if (hasObjectiveGoal
                && (artifact is null || !ShouldUseObjectiveTape(world))
                && ShouldUseLocalObjectiveDirectSeek(slot, world, graphGoal, player))
            {
                _statesBySlot.Remove(slot);
                if (graph is not null
                    && ShouldUseGraphForLocalObjectiveCompletion(world, graphGoal)
                    && TryBuildGraphInput(slot, world, controlledSlot, graph!, graphGoal, player, out var localObjectiveGraphInput))
                {
                    _inputs[slot] = ApplyCombatInput(world, player, localObjectiveGraphInput, allPlayers);
                    diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                    continue;
                }

                _graphStatesBySlot.Remove(slot);
                var localObjectiveInput = BuildDirectSeekInput(slot, world, controlledSlot.Team, player, graphGoal);
                _inputs[slot] = ApplyCombatInput(world, player, localObjectiveInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                continue;
            }

            if (hasGraphGoal && IsDynamicGraphGoal(graphGoal.Label))
            {
                _statesBySlot.Remove(slot);
                if (ShouldUseDynamicGraphObjectiveNavigation(world, graphGoal)
                    && TryBuildGraphInput(slot, world, controlledSlot, graph!, graphGoal, player, out var dynamicGraphInput))
                {
                    _inputs[slot] = ApplyCombatInput(world, player, dynamicGraphInput, allPlayers);
                    diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                    continue;
                }

                _graphStatesBySlot.Remove(slot);
                var directGoalInput = BuildDirectSeekInput(slot, world, controlledSlot.Team, player, graphGoal);
                _inputs[slot] = ApplyCombatInput(world, player, directGoalInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                continue;
            }

            if (hasGraphGoal
                && ShouldPreferGraphNavigation(world, graphGoal, artifact is not null)
                && TryBuildGraphInput(slot, world, controlledSlot, graph!, graphGoal, player, out var preferredGraphInput))
            {
                _inputs[slot] = ApplyCombatInput(world, player, preferredGraphInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                continue;
            }

            if (artifact is { } objectiveArtifact && ShouldUseObjectiveTape(world))
            {
                if (hasGraphGoal
                    && TryBuildObjectiveTapeGraphRecoveryInput(slot, world, controlledSlot, graph, player, graphGoal, out var activeRecoveryGraphInput))
                {
                    _inputs[slot] = ApplyCombatInput(world, player, activeRecoveryGraphInput, allPlayers);
                    diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                    continue;
                }

                var tapeInput = BuildTapeInput(slot, world, controlledSlot, objectiveArtifact, graph, player, out var tapeGoal);
                if (hasGraphGoal
                    && ShouldRecoverObjectiveTapeWithGraph(slot, world, player, objectiveArtifact)
                    && TryBuildGraphInput(slot, world, controlledSlot, graph!, graphGoal, player, out var recoveryGraphInput))
                {
                    if (_statesBySlot.TryGetValue(slot, out var recoveryState))
                    {
                        _statesBySlot[slot] = recoveryState with { GraphRecoveryActive = true };
                    }

                    _inputs[slot] = ApplyCombatInput(world, player, recoveryGraphInput, allPlayers);
                    diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                    continue;
                }

                _inputs[slot] = ApplyCombatInput(world, player, tapeInput, allPlayers, preservePassiveAim: true);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, tapeGoal, _inputs[slot]));
                continue;
            }

            if (hasGraphGoal
                && TryBuildGraphInput(slot, world, controlledSlot, graph!, graphGoal, player, out var graphInput))
            {
                _inputs[slot] = ApplyCombatInput(world, player, graphInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                continue;
            }

            if (hasObjectiveGoal && !ShouldUseObjectiveTape(world))
            {
                var directGoalInput = BuildDirectSeekInput(slot, world, controlledSlot.Team, player, graphGoal);
                _inputs[slot] = ApplyCombatInput(world, player, directGoalInput, allPlayers);
                diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, graphGoal, _inputs[slot]));
                continue;
            }

            if (artifact is null)
            {
                _inputs[slot] = BuildIdleInput(world, player);
                continue;
            }

            var fallbackInput = BuildTapeInput(slot, world, controlledSlot, artifact, graph, player, out var fallbackGoal);
            _inputs[slot] = ApplyCombatInput(world, player, fallbackInput, allPlayers, preservePassiveAim: true);
            diagnostics?.Add(CreateDiagnostics(slot, controlledSlot, player, fallbackGoal, _inputs[slot]));
        }

        PruneStates(controlledSlots);
        LastDiagnostics = diagnostics is null
            ? BotControllerDiagnosticsSnapshot.Empty
            : new BotControllerDiagnosticsSnapshot(
                diagnostics,
                AliveBotCount: diagnostics.Count,
                VisibleEnemyCount: 0,
                HealFocusCount: 0,
                CabinetSeekCount: 0,
                UnstickCount: 0);
        return _inputs;
    }

    private PlayerInputSnapshot BuildTapeInput(
        byte slot,
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PreparedMotionProofArtifact artifact,
        PreparedMotionGraph? graph,
        PlayerEntity player,
        out GraphGoal diagnosticGoal)
    {
        var phase = SelectPhase(world, player);
        var phaseData = phase == ReplayPhase.Return ? artifact.ReturnPhase : artifact.AttackPhase;
        var tape = phaseData.Actions;
        diagnosticGoal = new GraphGoal($"tape_{phase}", player.X, player.Bottom, GraphGoalRadius);
        if (tape.Count == 0)
        {
            return BuildIdleInput(world, player);
        }

        var state = GetReplayState(slot, world, controlledSlot, artifact, phase);
        if (state.Phase != phase)
        {
            var captureImplicitReturnStart = phase == ReplayPhase.Return && float.IsNaN(phaseData.StartX);
            state = state with
            {
                Phase = phase,
                ActionIndex = 0,
                ActionTick = 0,
                LastX = player.X,
                LastBottom = player.Bottom,
                NoProgressTicks = 0,
                NoHorizontalProgressTicks = 0,
                BestGoalDistance = float.NaN,
                NoGoalProgressTicks = 0,
                ReattachResumeActionIndex = -1,
                ActiveReattachPoint = null,
                SpawnExitPrefixResumeActionIndex = -1,
                ImplicitPhaseStartX = captureImplicitReturnStart ? player.X : float.NaN,
                ImplicitPhaseStartBottom = captureImplicitReturnStart ? player.Bottom : float.NaN,
                GraphRecoveryActive = false,
                PendingForwardReattach = false,
            };
        }

        if (state.ActionIndex >= tape.Count)
        {
            if (HasCompletedTapePhase(world, controlledSlot.Team, player, phase))
            {
                _statesBySlot[slot] = state;
                if (TryBuildObjectiveHoldInput(world, slot, controlledSlot.Team, player, out var holdInput))
                {
                    return holdInput;
                }

                return BuildIdleInput(world, player);
            }

            if (TryGetGraphGoal(world, controlledSlot.Team, player, out var fallbackRecoveryGoal))
            {
                if (TryBuildTapeExhaustedReattachInput(
                        slot,
                        world,
                        controlledSlot,
                        graph,
                        player,
                        phaseData,
                        fallbackRecoveryGoal,
                        ref state,
                        out var exhaustedReattachInput,
                        out diagnosticGoal))
                {
                    _statesBySlot[slot] = state;
                    return exhaustedReattachInput;
                }

                _statesBySlot[slot] = state;
                if (ShouldStrictlyReplayObjectiveTape(world))
                {
                    diagnosticGoal = fallbackRecoveryGoal;
                    if (ShouldPreferGraphForStrictTapeRecovery(world, controlledSlot.Team, player, fallbackRecoveryGoal, graph)
                        && TryBuildGraphInput(slot, world, controlledSlot, graph!, fallbackRecoveryGoal, player, out var strictRecoveryGraphInput))
                    {
                        return strictRecoveryGraphInput;
                    }

                    return BuildDirectSeekInput(slot, world, controlledSlot.Team, player, fallbackRecoveryGoal);
                }

                if (graph is not null
                    && TryBuildGraphInput(slot, world, controlledSlot, graph, fallbackRecoveryGoal, player, out var fallbackRecoveryGraphInput))
                {
                    return fallbackRecoveryGraphInput;
                }

                return BuildDirectSeekInput(slot, world, controlledSlot.Team, player, fallbackRecoveryGoal);
            }

            state = state with { ActionIndex = 0, ActionTick = 0 };
        }

        if (HasCompletedTapePhase(world, controlledSlot.Team, player, phase))
        {
            _statesBySlot[slot] = state;
            if (TryBuildObjectiveHoldInput(world, slot, controlledSlot.Team, player, out var holdInput))
            {
                return holdInput;
            }

            return BuildIdleInput(world, player);
        }

        var hasObjectiveGoal = TryGetGraphGoal(world, controlledSlot.Team, player, out var objectiveGoal);
        var objectiveDistance = hasObjectiveGoal
            ? Distance(player.X, player.Bottom, objectiveGoal.X, objectiveGoal.Bottom)
            : float.NaN;
        if (TryBuildImplicitReturnStartWaitInput(world, phase, phaseData, state, player, out var implicitReturnStartInput, out diagnosticGoal))
        {
            _statesBySlot[slot] = state;
            return implicitReturnStartInput;
        }

        if (TryBuildSpawnExitTapePrefixInput(
                slot,
                world,
                controlledSlot,
                graph,
                player,
                phaseData,
                hasObjectiveGoal ? objectiveGoal : null,
                objectiveDistance,
                ref state,
                out var spawnExitInput,
                out diagnosticGoal))
        {
            _statesBySlot[slot] = state;
            return spawnExitInput;
        }

        if (TryBuildTapeReattachInput(
                slot,
                world,
                controlledSlot,
                graph,
                player,
                phase,
                phaseData,
                ref state,
                hasObjectiveGoal ? objectiveGoal : null,
                objectiveDistance,
                out var reattachInput,
                out diagnosticGoal))
        {
            _statesBySlot[slot] = state;
            return reattachInput;
        }

        state = TrackReplayProgress(state, player, objectiveDistance);
        var strictReplay = ShouldStrictlyReplayObjectiveTape(world);
        var action = tape[state.ActionIndex];
        if (!strictReplay
            && state.NoProgressTicks >= GetObjectiveTapeFastForwardTicks(world)
            && CanFastForwardInertTapeAction(tape, state.ActionIndex))
        {
            state = state with
            {
                ActionIndex = FindNextActiveTapeActionIndex(tape, state.ActionIndex),
                ActionTick = 0,
                LastX = player.X,
                LastBottom = player.Bottom,
                NoProgressTicks = 0,
                BestGoalDistance = objectiveDistance,
                NoGoalProgressTicks = 0,
            };
            action = tape[state.ActionIndex];
        }

        if (!strictReplay
            && hasObjectiveGoal
            && state.NoGoalProgressTicks >= ObjectiveTapeNoGoalProgressTicks
            && TryFindBetterObjectiveTapeAction(tape, state.ActionIndex, objectiveGoal.X - player.X, out var betterActionIndex))
        {
            state = state with
            {
                ActionIndex = betterActionIndex,
                ActionTick = 0,
                LastX = player.X,
                LastBottom = player.Bottom,
                BestGoalDistance = objectiveDistance,
                NoGoalProgressTicks = 0,
            };
            action = tape[state.ActionIndex];
        }

        if (!strictReplay && IsGroundedIdleAction(action, player))
        {
            state = state with
            {
                ActionIndex = FindNextActiveTapeActionIndex(tape, state.ActionIndex),
                ActionTick = 0,
                LastX = player.X,
                LastBottom = player.Bottom,
                NoProgressTicks = 0,
                BestGoalDistance = objectiveDistance,
                NoGoalProgressTicks = 0,
            };
            if (state.ActionIndex < tape.Count)
            {
                action = tape[state.ActionIndex];
            }
        }

        var input = BuildActionInput(action, state.ActionTick, player);
        if (!strictReplay)
        {
            input = ApplyObjectiveTapeRescueInput(
                world,
                controlledSlot.Team,
                player,
                action,
                input,
                state.NoProgressTicks,
                allowObstacleJump: false,
                rescueJumpTicks: ObjectiveNavigationRescueJumpTicks);
        }

        _statesBySlot[slot] = AdvanceReplayState(state, tape) with { LastX = player.X, LastBottom = player.Bottom };
        return input;
    }

    private bool TryBuildSpawnExitTapePrefixInput(
        byte slot,
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PreparedMotionGraph? graph,
        PlayerEntity player,
        TapePhaseData phaseData,
        GraphGoal? objectiveGoal,
        float objectiveDistance,
        ref ReplayState state,
        out PlayerInputSnapshot input,
        out GraphGoal diagnosticGoal)
    {
        input = default;
        diagnosticGoal = default;
        if (!_spawnReattachEligibleBySlot.TryGetValue(slot, out var spawnReattachEligible)
            || !spawnReattachEligible)
        {
            return false;
        }

        if (state.SpawnExitPrefixResumeActionIndex < 0
            && (state.ActionIndex != 0 || state.ActionTick != 0))
        {
            return false;
        }

        var isNearSpawnReplayWindow = IsWithinTeamSpawnReplayWindow(world, controlledSlot.Team, player);
        if (!player.IsInSpawnRoom && !isNearSpawnReplayWindow)
        {
            _spawnReattachEligibleBySlot[slot] = false;
            state = state with
            {
                ReattachResumeActionIndex = -1,
                ActiveReattachPoint = null,
                SpawnExitPrefixResumeActionIndex = -1,
            };
            return false;
        }

        var shouldPreferSpawnExitPrefix = HasOutsideSpawnRoomReattachPoint(phaseData.ReattachPoints)
            && !IsNearTapePhaseStartForSpawnReplay(player, phaseData);

        if (!shouldPreferSpawnExitPrefix
            && TryFindNearbyTapePhaseStartAlignmentPoint(phaseData, player, player.IsInSpawnRoom || isNearSpawnReplayWindow, out var startAlignmentPoint))
        {
            if (!IsNearTapePhaseStartForSpawnReplay(player, phaseData))
            {
                if (MathF.Abs(player.X - startAlignmentPoint.X) <= TapeStartAlignmentReadyRadiusX
                    && IsReadyToStartTapeFromNearbySpawnOffset(player))
                {
                    _directSeekStatesBySlot.Remove(slot);
                    state = state with
                    {
                        ActionIndex = 0,
                        ActionTick = 0,
                        LastX = player.X,
                        LastBottom = player.Bottom,
                        NoProgressTicks = 0,
                        NoHorizontalProgressTicks = 0,
                        BestGoalDistance = objectiveDistance,
                        NoGoalProgressTicks = 0,
                        ReattachResumeActionIndex = -1,
                        ActiveReattachPoint = null,
                        SpawnExitPrefixResumeActionIndex = -1,
                    };
                    return false;
                }

                diagnosticGoal = new GraphGoal(
                    "tape_start_align",
                    startAlignmentPoint.X,
                    startAlignmentPoint.Bottom,
                    MathF.Max(startAlignmentPoint.RadiusX, startAlignmentPoint.RadiusBottom));
                input = BuildTapeStartAlignmentInput(player, startAlignmentPoint);
                state = state with
                {
                    ReattachResumeActionIndex = -1,
                    ActiveReattachPoint = null,
                    SpawnExitPrefixResumeActionIndex = -1,
                };
                return true;
            }

            if (IsReadyToStartTapeFromNearbySpawnOffset(player))
            {
                _directSeekStatesBySlot.Remove(slot);
                state = state with
                {
                    ActionIndex = 0,
                    ActionTick = 0,
                    LastX = player.X,
                    LastBottom = player.Bottom,
                    NoProgressTicks = 0,
                    NoHorizontalProgressTicks = 0,
                    BestGoalDistance = objectiveDistance,
                    NoGoalProgressTicks = 0,
                    ReattachResumeActionIndex = -1,
                    ActiveReattachPoint = null,
                    SpawnExitPrefixResumeActionIndex = -1,
                };
                return false;
            }
            else
            {
                diagnosticGoal = new GraphGoal(
                    "tape_start_wait",
                    startAlignmentPoint.X,
                    startAlignmentPoint.Bottom,
                    MathF.Max(startAlignmentPoint.RadiusX, startAlignmentPoint.RadiusBottom));
                input = BuildIdleInput(world, player);
                state = state with
                {
                    ReattachResumeActionIndex = -1,
                    ActiveReattachPoint = null,
                    SpawnExitPrefixResumeActionIndex = -1,
                };
                return true;
            }
        }

        TapeReattachPoint spawnExitPoint;
        var spawnExitResumeActionIndex = state.SpawnExitPrefixResumeActionIndex;
        if (spawnExitResumeActionIndex >= 0)
        {
            if (!TryResolveTapeReattachPoint(phaseData, spawnExitResumeActionIndex, out spawnExitPoint))
            {
                _spawnReattachEligibleBySlot[slot] = false;
                state = state with
                {
                    ReattachResumeActionIndex = -1,
                    ActiveReattachPoint = null,
                    SpawnExitPrefixResumeActionIndex = -1,
                };
                return false;
            }
        }
        else if (!TryFindSpawnExitTapePrefixTarget(
                phaseData.ReattachPoints,
                player,
                objectiveGoal,
                objectiveDistance,
                out spawnExitPoint))
        {
            return false;
        }

        spawnExitResumeActionIndex = spawnExitPoint.ResumeActionIndex;
        if (!IsDirectlyContestableGoal(
                world,
                controlledSlot.Team,
                player,
                spawnExitPoint.X,
                spawnExitPoint.Bottom,
                CombatSeekVerticalTolerance,
                CombatSeekDropTolerance,
                CombatSeekJumpHorizontalRadius))
        {
            _spawnReattachEligibleBySlot[slot] = false;
            state = state with
            {
                ReattachResumeActionIndex = -1,
                ActiveReattachPoint = null,
                SpawnExitPrefixResumeActionIndex = -1,
            };
            return false;
        }

        if (IsReadyToResumeTapeAtReattachPoint(player, spawnExitPoint))
        {
            _spawnReattachEligibleBySlot[slot] = false;
            _directSeekStatesBySlot.Remove(slot);
            state = state with
            {
                ActionIndex = spawnExitResumeActionIndex,
                ActionTick = 0,
                LastX = player.X,
                LastBottom = player.Bottom,
                NoProgressTicks = 0,
                NoHorizontalProgressTicks = 0,
                BestGoalDistance = objectiveDistance,
                NoGoalProgressTicks = 0,
                ReattachResumeActionIndex = -1,
                ActiveReattachPoint = null,
                SpawnExitPrefixResumeActionIndex = -1,
            };
            return false;
        }

        diagnosticGoal = new GraphGoal(
            $"tape_spawn_exit_{spawnExitPoint.ResumeActionIndex}",
            spawnExitPoint.X,
            spawnExitPoint.Bottom,
            MathF.Max(spawnExitPoint.RadiusX, spawnExitPoint.RadiusBottom));
        input = BuildTapeRecoverySeekInput(slot, world, controlledSlot, graph, player, diagnosticGoal);
        state = state with
        {
            ReattachResumeActionIndex = -1,
            ActiveReattachPoint = null,
            SpawnExitPrefixResumeActionIndex = spawnExitResumeActionIndex,
        };
        return true;
    }

    private bool TryBuildTapeReattachInput(
        byte slot,
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PreparedMotionGraph? graph,
        PlayerEntity player,
        ReplayPhase phase,
        TapePhaseData phaseData,
        ref ReplayState state,
        GraphGoal? objectiveGoal,
        float objectiveDistance,
        out PlayerInputSnapshot input,
        out GraphGoal diagnosticGoal)
    {
        input = default;
        diagnosticGoal = default;
        if (phaseData.ReattachPoints.Count == 0)
        {
            state = state with { ReattachResumeActionIndex = -1, ActiveReattachPoint = null };
            return false;
        }

        var reattachResumeActionIndex = state.ReattachResumeActionIndex;
        if (reattachResumeActionIndex < 0)
        {
            var spawnReattachEligible = _spawnReattachEligibleBySlot.TryGetValue(slot, out var isEligible) && isEligible;
            var pendingForwardReattach = state.PendingForwardReattach;
            TapeReattachPoint reattachPoint;
            var startedFromSpawnOrPhase = false;
            if (pendingForwardReattach)
            {
                if (!TryFindForwardTapeReattachPoint(phase, phaseData.ReattachPoints, player, state.ActionIndex, objectiveGoal, objectiveDistance, out reattachPoint))
                {
                    return false;
                }
            }
            else if (ShouldStartTapeReattach(player, phaseData, state, spawnReattachEligible))
            {
                if (!TryFindTapePhaseStartReattachPoint(phase, phaseData, player, objectiveGoal, objectiveDistance, out reattachPoint))
                {
                    return false;
                }

                startedFromSpawnOrPhase = true;
            }
            else
            {
                if (!ShouldStartTapeRecoveryReattach(state)
                    || !TryFindForwardTapeReattachPoint(phase, phaseData.ReattachPoints, player, state.ActionIndex, objectiveGoal, objectiveDistance, out reattachPoint))
                {
                    return false;
                }
            }

            reattachResumeActionIndex = reattachPoint.ResumeActionIndex;
            state = state with
            {
                ReattachResumeActionIndex = reattachResumeActionIndex,
                ActiveReattachPoint = reattachPoint,
                PendingForwardReattach = false,
            };
            if (startedFromSpawnOrPhase)
            {
                _spawnReattachEligibleBySlot[slot] = false;
            }
        }

        var activePoint = state.ActiveReattachPoint;
        if (activePoint is null
            || activePoint.Value.ResumeActionIndex != reattachResumeActionIndex)
        {
            if (!TryResolveTapeReattachPoint(phaseData, reattachResumeActionIndex, out var resolvedPoint))
            {
                state = state with { ReattachResumeActionIndex = -1, ActiveReattachPoint = null };
                return false;
            }

            activePoint = resolvedPoint;
            state = state with { ActiveReattachPoint = resolvedPoint };
        }

        var currentReattachPoint = activePoint.Value;
        if (currentReattachPoint.UseLiveBottom)
        {
            currentReattachPoint = currentReattachPoint with { Bottom = player.Bottom };
        }

        if (IsReadyToResumeTapeAtReattachPoint(player, currentReattachPoint))
        {
            state = state with
            {
                ActionIndex = currentReattachPoint.ResumeActionIndex,
                ActionTick = 0,
                LastX = player.X,
                LastBottom = player.Bottom,
                NoProgressTicks = 0,
                NoHorizontalProgressTicks = 0,
                BestGoalDistance = float.NaN,
                NoGoalProgressTicks = 0,
                ReattachResumeActionIndex = -1,
                ActiveReattachPoint = null,
                SpawnExitPrefixResumeActionIndex = -1,
                PendingForwardReattach = false,
            };
            return false;
        }

        if (objectiveGoal is { } activeObjectiveGoal
            && TrySelectObjectiveSeamProgram(world, controlledSlot, player, activeObjectiveGoal, out var seamProgram)
            && TryStartObjectiveSeamInput(slot, world, controlledSlot, player, seamProgram, out input, out diagnosticGoal))
        {
            state = state with
            {
                ReattachResumeActionIndex = -1,
                ActiveReattachPoint = null,
                SpawnExitPrefixResumeActionIndex = -1,
            };
            return true;
        }

        if (!IsDirectlyContestableGoal(
                world,
                controlledSlot.Team,
                player,
                currentReattachPoint.X,
                currentReattachPoint.Bottom,
                CombatSeekVerticalTolerance,
                CombatSeekDropTolerance,
                CombatSeekJumpHorizontalRadius))
        {
            state = state with
            {
                ReattachResumeActionIndex = -1,
                ActiveReattachPoint = null,
                SpawnExitPrefixResumeActionIndex = -1,
            };
            return false;
        }

        diagnosticGoal = new GraphGoal(
            $"tape_reattach_{currentReattachPoint.ResumeActionIndex}",
            currentReattachPoint.X,
            currentReattachPoint.Bottom,
            MathF.Max(currentReattachPoint.RadiusX, currentReattachPoint.RadiusBottom));
        input = BuildTapeRecoverySeekInput(slot, world, controlledSlot, graph, player, diagnosticGoal);
        return true;
    }

    private PlayerInputSnapshot BuildTapeRecoverySeekInput(
        byte slot,
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PreparedMotionGraph? graph,
        PlayerEntity player,
        GraphGoal goal)
    {
        if (ShouldStrictlyReplayObjectiveTape(world))
        {
            if (ShouldPreferGraphForStrictTapeRecovery(world, controlledSlot.Team, player, goal, graph)
                && TryBuildGraphInput(slot, world, controlledSlot, graph!, goal, player, out var strictGraphInput))
            {
                return strictGraphInput;
            }

            return BuildDirectSeekInput(slot, world, controlledSlot.Team, player, goal);
        }

        if (graph is not null
            && TryBuildGraphInput(slot, world, controlledSlot, graph, goal, player, out var graphInput))
        {
            return graphInput;
        }

        return BuildDirectSeekInput(slot, world, controlledSlot.Team, player, goal);
    }

    private static PlayerInputSnapshot BuildTapeStartAlignmentInput(PlayerEntity player, TapeReattachPoint point)
    {
        var deltaX = point.X - player.X;
        var direction = MathF.Abs(deltaX) > point.RadiusX ? Math.Sign(deltaX) : 0;
        var aimDirection = direction != 0
            ? direction
            : player.FacingDirectionX < 0f ? -1 : 1;
        return new PlayerInputSnapshot(
            Left: direction < 0,
            Right: direction > 0,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + (aimDirection * 160f),
            AimWorldY: player.Y,
            DebugKill: false);
    }

    private bool TryBuildTapeExhaustedReattachInput(
        byte slot,
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PreparedMotionGraph? graph,
        PlayerEntity player,
        TapePhaseData phaseData,
        GraphGoal objectiveGoal,
        ref ReplayState state,
        out PlayerInputSnapshot input,
        out GraphGoal diagnosticGoal)
    {
        input = default;
        diagnosticGoal = default;
        var currentObjectiveDistance = Distance(player.X, player.Bottom, objectiveGoal.X, objectiveGoal.Bottom);
        TapeReattachPoint point;
        if (!TryFindLocalTapeDriftReattachPoint(phaseData, player, state.ActionIndex, out point)
            && !TryFindBestObjectiveTapeReattachPoint(
                phaseData.ReattachPoints,
                player,
                requireOutsideSpawnRoom: player.IsInSpawnRoom,
                currentActionIndex: -1,
                objectiveGoal,
                currentObjectiveDistance,
                out point)
            && !TryFindNearestTapeReattachPoint(phaseData, player, out point))
        {
            return false;
        }

        if (IsReadyToResumeTapeAtReattachPoint(player, point))
        {
            diagnosticGoal = new GraphGoal(
                $"tape_exhausted_resume_{point.ResumeActionIndex}",
                point.X,
                point.Bottom,
                MathF.Max(point.RadiusX, point.RadiusBottom));
            state = state with
            {
                ActionIndex = point.ResumeActionIndex,
                ActionTick = 0,
                LastX = player.X,
                LastBottom = player.Bottom,
                NoProgressTicks = 0,
                NoHorizontalProgressTicks = 0,
                BestGoalDistance = currentObjectiveDistance,
                NoGoalProgressTicks = 0,
                ReattachResumeActionIndex = -1,
                ActiveReattachPoint = null,
                SpawnExitPrefixResumeActionIndex = -1,
            };

            _directSeekStatesBySlot.Remove(slot);
            var action = phaseData.Actions[Math.Clamp(point.ResumeActionIndex, 0, phaseData.Actions.Count - 1)];
            input = BuildActionInput(action, 0, player);
            return true;
        }

        var isTerminalPoint = point.ResumeActionIndex >= phaseData.Actions.Count - 1;
        if (isTerminalPoint
            && currentObjectiveDistance <= MathF.Max(TapeExhaustedObjectiveFinishDistance, objectiveGoal.Radius))
        {
            if (TryBuildObjectiveTerminalInput(slot, world, controlledSlot.Team, player, objectiveGoal, out input))
            {
                diagnosticGoal = objectiveGoal;
                state = state with
                {
                    ReattachResumeActionIndex = -1,
                    ActiveReattachPoint = null,
                    SpawnExitPrefixResumeActionIndex = -1,
                };
                return true;
            }

            diagnosticGoal = objectiveGoal;
            state = state with
            {
                ReattachResumeActionIndex = -1,
                ActiveReattachPoint = null,
                SpawnExitPrefixResumeActionIndex = -1,
            };
            input = BuildTapeRecoverySeekInput(slot, world, controlledSlot, graph, player, objectiveGoal);
            return true;
        }

        diagnosticGoal = new GraphGoal(
            $"tape_exhausted_reattach_{point.ResumeActionIndex}",
            point.X,
            point.Bottom,
            MathF.Max(point.RadiusX, point.RadiusBottom));
        state = state with
        {
            ReattachResumeActionIndex = point.ResumeActionIndex,
            ActiveReattachPoint = point,
            SpawnExitPrefixResumeActionIndex = -1,
        };
        input = BuildTapeRecoverySeekInput(slot, world, controlledSlot, graph, player, diagnosticGoal);
        return true;
    }

    private static bool HasCompletedTapePhase(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        ReplayPhase phase)
    {
        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            return phase == ReplayPhase.Attack
                ? player.IsCarryingIntel
                : !player.IsCarryingIntel;
        }

        if (world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
            && TryGetGraphGoal(world, team, player, out _)
            && SelectKothTargetPoint(world, team) is { } point)
        {
            return world.IsPlayerInControlPointCaptureZone(player, point.Index)
                && !player.IsSpyCloaked;
        }

        return true;
    }

    private bool TryBuildObjectiveHoldInput(
        SimulationWorld world,
        byte slot,
        PlayerTeam team,
        PlayerEntity player,
        out PlayerInputSnapshot input)
    {
        input = default;
        if (world.MatchRules.Mode is not (GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
            || SelectKothTargetPoint(world, team) is not { } point)
        {
            _objectiveHoldPointBySlot.Remove(slot);
            return false;
        }

        var inCaptureZone = world.IsPlayerInControlPointCaptureZone(player, point.Index)
            && !player.IsSpyCloaked;
        var enemyCappers = team == PlayerTeam.Blue ? point.RedCappers : point.BlueCappers;
        var contested = enemyCappers > 0
            || point.CappingTeam.HasValue && point.CappingTeam.Value != team && point.CappingTicks > 0f;
        var ownedAndStable = point.Team == team && !contested;
        var activelyCapturing = point.CappingTeam == team && point.CappingTicks > 0f;
        var latched = _objectiveHoldPointBySlot.TryGetValue(slot, out var heldPointIndex)
            && heldPointIndex == point.Index;

        if (ownedAndStable)
        {
            _objectiveHoldPointBySlot.Remove(slot);
            if (!inCaptureZone)
            {
                return false;
            }
        }
        else if (inCaptureZone || activelyCapturing || latched)
        {
            _objectiveHoldPointBySlot[slot] = point.Index;
        }
        else
        {
            return false;
        }

        if (!inCaptureZone && !ownedAndStable)
        {
            var goal = new GraphGoal($"control_point_{point.Index}", point.Marker.CenterX, point.Marker.CenterY, GraphGoalRadius);
            input = BuildDirectSeekInput(slot, world, team, player, goal);
            return true;
        }

        var centerX = point.Marker.CenterX;
        var halfWidth = ownedAndStable ? ObjectiveHoldStrafeHalfWidth : ObjectiveCaptureStrafeHalfWidth;
        var leftBound = centerX - halfWidth;
        var rightBound = centerX + halfWidth;
        var phase = (int)((world.Frame + slot * 11) / ObjectiveHoldStrafePeriodTicks);
        var direction = phase % 2 == 0 ? -1 : 1;
        if (player.X <= leftBound)
        {
            direction = 1;
        }
        else if (player.X >= rightBound)
        {
            direction = -1;
        }

        if (WouldMoveIntoObstacle(world, team, player, direction, probeDistance: 16f))
        {
            var reversed = -direction;
            direction = WouldMoveIntoObstacle(world, team, player, reversed, probeDistance: 16f) ? 0 : reversed;
        }

        var hopFrame = (int)((world.Frame + slot * 7) % ObjectiveHoldHopPeriodTicks);
        var shouldHop = player.IsGrounded && hopFrame < ObjectiveHoldHopPulseTicks;
        input = new PlayerInputSnapshot(
            Left: direction < 0,
            Right: direction > 0,
            Up: shouldHop,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + (direction == 0 ? player.FacingDirectionX : direction) * 160f,
            AimWorldY: player.Y,
            DebugKill: false);
        return true;
    }

    private BotControllerDiagnosticsEntry CreateDiagnostics(
        byte slot,
        ControlledBotSlot controlledSlot,
        PlayerEntity player,
        GraphGoal goal,
        PlayerInputSnapshot input)
    {
        var hasGraphState = _graphStatesBySlot.TryGetValue(slot, out var graphState);
        var hasDirectState = _directSeekStatesBySlot.TryGetValue(slot, out var directState)
            && directState.GoalKey.Equals(BuildDirectSeekGoalKey(goal), StringComparison.OrdinalIgnoreCase);
        var hasReplayState = _statesBySlot.TryGetValue(slot, out var replayState);
        var hasSeamState = _objectiveSeamStatesBySlot.TryGetValue(slot, out var seamState);
        var actionCount = graphState.Actions?.Count ?? 0;
        var actionTicks = graphState.Actions is not null && graphState.ActionIndex < actionCount
            ? graphState.Actions[graphState.ActionIndex].Ticks
            : -1;
        var routeLabel = hasDirectState
            ? $"motion_proof:{goal.Label}:direct:{directState.NoProgressTicks}:{directState.BackupTicksRemaining}"
            : actionCount > 0
            ? $"motion_proof:{goal.Label}:s{graphState.StartNodeIndex}:g{graphState.GoalNodeIndex}:{graphState.ActionIndex}/{actionCount}:{graphState.ActionTick}"
            : hasSeamState
            ? $"motion_proof:{goal.Label}:seam:{seamState.Program.Label}:{seamState.ActionIndex}:{seamState.ActionTick}:{seamState.NoMovementTicks}"
            : hasReplayState
            ? $"motion_proof:{goal.Label}:tape:{replayState.Phase}:{replayState.ActionIndex}:{replayState.ActionTick}:{replayState.NoProgressTicks}:{replayState.NoGoalProgressTicks}:{replayState.ReattachResumeActionIndex}"
            : $"motion_proof:{goal.Label}:idle";
        var currentPointId = hasGraphState
            ? graphState.ActionIndex
            : hasSeamState
                ? seamState.ActionIndex
            : hasReplayState
                ? replayState.ActionIndex
                : -1;
        var nextPointId = hasGraphState
            ? actionTicks
            : hasSeamState
                ? seamState.ActionTick
            : hasReplayState
                ? replayState.ActionTick
                : -1;
        var nextPoint2Id = hasGraphState
            ? actionCount
            : hasSeamState
                ? seamState.NoMovementTicks
            : hasReplayState
                ? replayState.NoProgressTicks
                : -1;
        return new BotControllerDiagnosticsEntry(
            slot,
            player.DisplayName,
            controlledSlot.Team,
            controlledSlot.ClassId,
            BotRole.AttackObjective,
            BotStateKind.TravelObjective,
            BotFocusKind.Objective,
            goal.Label,
            routeLabel,
            HasVisibleEnemy: false,
            player.Health,
            player.MaxHealth,
            StuckTicks: 0,
            ModernStuckTicks: 0,
            UnstickTicks: 0,
            CurrentPointId: currentPointId,
            NextPointId: nextPointId,
            NextPoint2Id: nextPoint2Id,
            MovementTargetX: hasGraphState && graphState.GoalNodeIndex >= 0 ? graphState.GoalNodeX : goal.X,
            MovementTargetY: hasGraphState && graphState.GoalNodeIndex >= 0 ? graphState.GoalNodeBottom : goal.Bottom,
            RequestedHorizontal: input.Right ? 1 : input.Left ? -1 : 0,
            MoveDebug: input.Left ? "left" : input.Right ? "right" : "none",
            RequestedJump: input.Up,
            JumpDebug: input.Up ? "jump" : "none",
            RouteGoalNodeId: -1,
            RouteGoalX: goal.X,
            RouteGoalY: goal.Bottom,
            PreviousCurrentPointId: -1,
            PreviousNextPointId: -1,
            player.IsGrounded,
            ProbeGrounded: player.IsGrounded,
            SecondAnchorBlockPointId: -1,
            SecondAnchorBlockTicksRemaining: 0,
            NoNextPointTicks: 0,
            FallbackRouteLabel: string.Empty,
            FallbackTriggerLabel: string.Empty,
            NavigationIssueLabel: _objectiveSeamIssueBySlot.TryGetValue(slot, out var seamIssue) ? seamIssue : string.Empty,
            BranchFromPointId: -1,
            BranchToPointId: -1,
            BranchTicks: 0,
            BranchNoProgressTicks: 0,
            DirectTargetTicks: hasDirectState ? directState.TotalTicks : 0,
            DirectTargetNoProgressTicks: hasDirectState ? directState.NoProgressTicks : 0);
    }

    private PreparedMotionProofArtifact? GetArtifact(SimulationWorld world, ControlledBotSlot controlledSlot)
    {
        var key = $"{world.Level.Name}.a{world.Level.MapAreaIndex}.{controlledSlot.Team}.{controlledSlot.ClassId}";
        if (_artifactsByKey.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var root = ResolveMotionProofContentRoot();

        var path = ResolveFirstExistingMotionProofArtifactPath(
            root,
            MotionProofTapesDirectoryName,
            $"{world.Level.Name}.{controlledSlot.Team}.{controlledSlot.ClassId}.json");
        if (!File.Exists(path))
        {
            _artifactsByKey[key] = null;
            return null;
        }

        var rawArtifact = ReadJsonArtifact<MotionProofArtifact>(path);
        var artifact = rawArtifact is null
            ? null
            : PrepareMotionProofArtifact(world, controlledSlot, rawArtifact);
        _artifactsByKey[key] = artifact;
        return artifact;
    }

    private static PreparedMotionProofArtifact PrepareMotionProofArtifact(
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        MotionProofArtifact artifact)
    {
        var attackPhase = PrepareTapePhaseData(artifact.Map, artifact.Area, controlledSlot.Team, controlledSlot.ClassId, artifact.Attack);
        var returnPhase = artifact.Return.Count > 0
            ? PrepareTapePhaseData(
                artifact.Map,
                artifact.Area,
                controlledSlot.Team,
                controlledSlot.ClassId,
                artifact.Return,
                leadInActions: artifact.Attack,
                requireCarryingIntelStart: true)
            : TapePhaseData.Empty(artifact.Return);
        return new PreparedMotionProofArtifact(artifact, attackPhase, returnPhase);
    }

    private static TapePhaseData PrepareTapePhaseData(
        string map,
        int area,
        PlayerTeam team,
        PlayerClass classId,
        IReadOnlyList<MotionProofAction> actions,
        IReadOnlyList<MotionProofAction>? leadInActions = null,
        bool requireCarryingIntelStart = false)
    {
        if (actions.Count == 0
            || OperatingSystem.IsBrowser()
            || !TryCreateArtifactProbeWorld(map, area, team, classId, out var world, out var bot))
        {
            return TapePhaseData.Empty(actions);
        }

        const byte botSlot = 2;
        if (leadInActions is { Count: > 0 }
            && !TryReplayArtifactActions(world, botSlot, bot, leadInActions, stopWhenCarryingIntel: requireCarryingIntelStart))
        {
            return TapePhaseData.Empty(actions);
        }

        if (requireCarryingIntelStart && !bot.IsCarryingIntel)
        {
            return TapePhaseData.Empty(actions);
        }

        var startX = bot.X;
        var startBottom = bot.Bottom;
        var points = new List<TapeReattachPoint>();
        var lastAddedX = startX;
        var lastAddedBottom = startBottom;
        for (var actionIndex = 0; actionIndex < actions.Count; actionIndex += 1)
        {
            var action = actions[actionIndex];
            for (var tick = 0; tick < Math.Max(1, action.Ticks); tick += 1)
            {
                if (!world.TrySetNetworkPlayerInput(botSlot, BuildActionInput(action, tick, bot)))
                {
                    return new TapePhaseData(startX, startBottom, actions, points, points.ToDictionary(point => point.ResumeActionIndex));
                }

                world.AdvanceOneTick();
            }

            var resumeActionIndex = actionIndex + 1;
            if (resumeActionIndex >= actions.Count)
            {
                break;
            }

            var endX = bot.X;
            var endBottom = bot.Bottom;
            if (points.Count > 0
                && Distance(endX, endBottom, lastAddedX, lastAddedBottom) < TapeReattachDuplicateDistance)
            {
                continue;
            }

            points.Add(new TapeReattachPoint(
                resumeActionIndex,
                endX,
                endBottom,
                TapeReattachWindowRadiusX,
                TapeReattachWindowRadiusBottom,
                bot.IsInSpawnRoom,
                bot.IsGrounded,
                bot.HorizontalSpeed,
                bot.VerticalSpeed,
                UseLiveBottom: false));
            lastAddedX = endX;
            lastAddedBottom = endBottom;
        }

        return new TapePhaseData(startX, startBottom, actions, points, points.ToDictionary(point => point.ResumeActionIndex));
    }

    private static bool TryReplayArtifactActions(
        SimulationWorld world,
        byte botSlot,
        PlayerEntity bot,
        IReadOnlyList<MotionProofAction> actions,
        bool stopWhenCarryingIntel)
    {
        foreach (var action in actions)
        {
            for (var tick = 0; tick < Math.Max(1, action.Ticks); tick += 1)
            {
                if (!world.TrySetNetworkPlayerInput(botSlot, BuildActionInput(action, tick, bot)))
                {
                    return false;
                }

                world.AdvanceOneTick();
                if (stopWhenCarryingIntel && bot.IsCarryingIntel)
                {
                    return true;
                }
            }
        }

        return !stopWhenCarryingIntel || bot.IsCarryingIntel;
    }

    private static bool TryCreateArtifactProbeWorld(
        string map,
        int area,
        PlayerTeam team,
        PlayerClass classId,
        out SimulationWorld world,
        out PlayerEntity bot)
    {
        world = new SimulationWorld();
        bot = null!;
        if (!world.TryLoadLevel(map, area, preservePlayerStats: false))
        {
            return false;
        }

        world.PrepareLocalPlayerJoin();
        const byte botSlot = 2;
        return world.TryPrepareNetworkPlayerJoin(botSlot)
            && world.TrySetNetworkPlayerTeam(botSlot, team)
            && world.TryApplyNetworkPlayerClassSelection(botSlot, classId)
            && world.TryGetNetworkPlayer(botSlot, out bot);
    }

    private static string ResolveMotionProofContentRoot()
    {
        var root = Environment.GetEnvironmentVariable("OG_MOTION_PROOF_DIR");
        if (!string.IsNullOrWhiteSpace(root))
        {
            return root;
        }

        root = Path.Combine(ContentRoot.Path, "MotionProof");
        if (OperatingSystem.IsBrowser() || Directory.Exists(root))
        {
            return root;
        }

        return ProjectSourceLocator.FindDirectory(MotionProofContentRelativeDirectory) ?? root;
    }

    private static string ResolveFirstExistingMotionProofArtifactPath(
        string root,
        string preferredSubdirectory,
        string fileName)
    {
        var candidates = EnumerateMotionProofArtifactPaths(root, preferredSubdirectory, fileName).ToArray();
        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static IEnumerable<string> EnumerateMotionProofArtifactPaths(
        string root,
        string preferredSubdirectory,
        string fileName)
    {
        yield return Path.Combine(root, preferredSubdirectory, fileName);
        yield return Path.Combine(root, fileName);
    }

    private static bool ShouldUseObjectiveTape(SimulationWorld world)
    {
        return world.MatchRules.Mode is GameModeKind.CaptureTheFlag or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
            && !HasExplicitGraphGoalOverride();
    }

    private bool ShouldRecoverObjectiveTapeWithGraph(
        byte slot,
        SimulationWorld world,
        PlayerEntity player,
        PreparedMotionProofArtifact artifact)
    {
        if (ShouldStrictlyReplayObjectiveTape(world))
        {
            return false;
        }

        if (!_statesBySlot.TryGetValue(slot, out var state))
        {
            return false;
        }

        var phase = SelectPhase(world, player);
        var phaseData = phase == ReplayPhase.Return ? artifact.ReturnPhase : artifact.AttackPhase;
        if (state.ReattachResumeActionIndex >= 0
            || state.SpawnExitPrefixResumeActionIndex >= 0
            || state.ActiveReattachPoint is not null)
        {
            return false;
        }

        if (state.ActionIndex >= phaseData.Actions.Count)
        {
            return true;
        }

        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            && phase != ReplayPhase.Return)
        {
            return false;
        }

        return state.NoProgressTicks >= ObjectiveGraphRecoveryTicks
            || state.NoHorizontalProgressTicks >= ObjectiveGraphRecoveryTicks
            || state.NoGoalProgressTicks >= ObjectiveGraphRecoveryTicks;
    }

    private static bool ShouldPreferGraphNavigation(SimulationWorld world, GraphGoal goal, bool hasObjectiveArtifact)
    {
        if (IsDynamicGraphGoal(goal.Label))
        {
            return true;
        }

        if (hasObjectiveArtifact && ShouldUseObjectiveTape(world))
        {
            return false;
        }

        return !IsEnabled(Environment.GetEnvironmentVariable("OG_MOTION_PROOF_FORCE_OBJECTIVE_TAPES"))
            && (world.MatchRules.Mode is GameModeKind.CaptureTheFlag or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill);
    }

    private bool TryBuildObjectiveTapeGraphRecoveryInput(
        byte slot,
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PreparedMotionGraph? graph,
        PlayerEntity player,
        GraphGoal goal,
        out PlayerInputSnapshot input)
    {
        input = default;
        if (graph is null
            || !_statesBySlot.TryGetValue(slot, out var state)
            || !state.GraphRecoveryActive)
        {
            return false;
        }

        if (TryBuildGraphInput(slot, world, controlledSlot, graph, goal, player, out input))
        {
            return true;
        }

        _statesBySlot[slot] = state with { GraphRecoveryActive = false };
        return false;
    }

    private static bool ShouldUseDynamicGraphObjectiveNavigation(SimulationWorld world, GraphGoal goal)
    {
        return world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            && goal.Label.Equals("enemy_carrier", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldUseGraphForLocalObjectiveCompletion(SimulationWorld world, GraphGoal goal)
    {
        return world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
            && goal.Label.StartsWith("control_point_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldPreferGraphForStrictTapeRecovery(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        GraphGoal goal,
        PreparedMotionGraph? graph)
    {
        if (graph is null
            || goal.Label.StartsWith("tape_", StringComparison.OrdinalIgnoreCase)
            || goal.Label.StartsWith("terminal_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsDirectlyContestableGoal(
            world,
            team,
            player,
            goal.X,
            goal.Bottom,
            verticalTolerance: 96f,
            dropTolerance: DirectSeekVerticalDropThreshold,
            jumpHorizontalRadius: DirectSeekVerticalSearchHorizontalRadius);
    }

    private static bool ShouldStrictlyReplayObjectiveTape(SimulationWorld world)
    {
        return ShouldUseObjectiveTape(world)
            && !IsEnabled(Environment.GetEnvironmentVariable("OG_MOTION_PROOF_MUTATE_OBJECTIVE_TAPES"));
    }

    private bool ShouldUseLocalObjectiveDirectSeek(byte slot, SimulationWorld world, GraphGoal goal, PlayerEntity player)
    {
        if (world.MatchRules.Mode is not (GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
            || !goal.Label.StartsWith("control_point_", StringComparison.OrdinalIgnoreCase))
        {
            _localObjectiveDirectPointBySlot.Remove(slot);
            return false;
        }

        var targetPoint = SelectKothTargetPoint(world, player.Team);
        if (targetPoint is null
            || player.IsSpyCloaked
            || world.IsPlayerInControlPointCaptureZone(player, targetPoint.Index))
        {
            _localObjectiveDirectPointBySlot.Remove(slot);
            return false;
        }

        var deltaX = MathF.Abs(goal.X - player.X);
        var deltaBottom = MathF.Abs(goal.Bottom - player.Bottom);
        var isLatched = _localObjectiveDirectPointBySlot.TryGetValue(slot, out var latchedPointIndex)
            && latchedPointIndex == targetPoint.Index;
        if (isLatched)
        {
            if (deltaX <= LocalObjectiveDirectReleaseHorizontalRadius
                && deltaBottom <= LocalObjectiveDirectReleaseVerticalRadius)
            {
                return true;
            }

            _localObjectiveDirectPointBySlot.Remove(slot);
            return false;
        }

        if (deltaX <= LocalObjectiveDirectHorizontalRadius
            && deltaBottom <= LocalObjectiveDirectVerticalRadius)
        {
            _localObjectiveDirectPointBySlot[slot] = targetPoint.Index;
            return true;
        }

        return false;
    }

    private static bool HasExplicitGraphGoalOverride()
    {
        var envX = Environment.GetEnvironmentVariable("OG_MOTION_PROOF_GOAL_X");
        var envY = Environment.GetEnvironmentVariable("OG_MOTION_PROOF_GOAL_Y");
        return float.TryParse(envX, out _) && float.TryParse(envY, out _);
    }

    private PreparedMotionGraph? GetGraph(SimulationWorld world, ControlledBotSlot controlledSlot)
    {
        var movementProfile = GetMovementProfileId(controlledSlot.ClassId);
        var key = $"{world.Level.Name}.a{world.Level.MapAreaIndex}.{controlledSlot.Team}.{movementProfile}";
        if (_graphsByKey.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var root = ResolveMotionProofContentRoot();

        var candidatePaths = EnumerateMotionProofArtifactPaths(root, MotionProofGraphsDirectoryName, $"{world.Level.Name}.{controlledSlot.Team}.{movementProfile}.graph.json.gz")
            .Concat(EnumerateMotionProofArtifactPaths(root, MotionProofGraphsDirectoryName, $"{world.Level.Name}.{controlledSlot.Team}.{movementProfile}.graph.json"))
            .Concat(EnumerateMotionProofArtifactPaths(root, MotionProofGraphsDirectoryName, $"{world.Level.Name}.{movementProfile}.graph.json.gz"))
            .Concat(EnumerateMotionProofArtifactPaths(root, MotionProofGraphsDirectoryName, $"{world.Level.Name}.{movementProfile}.graph.json"))
            .Concat(EnumerateMotionProofArtifactPaths(root, MotionProofGraphsDirectoryName, $"{world.Level.Name}.{controlledSlot.Team}.{controlledSlot.ClassId}.graph.json.gz"))
            .Concat(EnumerateMotionProofArtifactPaths(root, MotionProofGraphsDirectoryName, $"{world.Level.Name}.{controlledSlot.Team}.{controlledSlot.ClassId}.graph.json"))
            .ToArray();
        var path = candidatePaths.FirstOrDefault(File.Exists);
        if (!File.Exists(path))
        {
            _graphsByKey[key] = null;
            return null;
        }

        var artifact = ReadJsonArtifact<MotionGraphArtifact>(path);
        var graph = artifact is null ? null : PreparedMotionGraph.From(artifact, Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path)));
        _graphsByKey[key] = graph;
        return graph;
    }

    private bool TryBuildGraphInput(
        byte slot,
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PreparedMotionGraph graph,
        GraphGoal goal,
        PlayerEntity player,
        out PlayerInputSnapshot input)
    {
        input = default;
        var mapKey = $"{world.Level.Name}.a{world.Level.MapAreaIndex}.{graph.Identity}.{controlledSlot.Team}.{graph.Version}";
        var goalKey = BuildGraphGoalKey(goal);
        var hasExistingState = _graphStatesBySlot.TryGetValue(slot, out var state);
        var shouldFinishPickupTape = hasExistingState
            && state.MapKey == mapKey
            && state.ActionIndex < state.Actions.Count
            && player.IsCarryingIntel
            && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
            && state.GoalKey.StartsWith("enemy_intel:", StringComparison.OrdinalIgnoreCase);
        if (!hasExistingState
            || state.MapKey != mapKey
            || (!shouldFinishPickupTape && state.GoalKey != goalKey)
            || state.ActionIndex >= state.Actions.Count)
        {
            if (!TryRebuildGraphReplayState(
                    graph,
                    controlledSlot,
                    world,
                    player,
                    mapKey,
                    goalKey,
                    goal,
                    out state))
            {
                _graphStatesBySlot.Remove(slot);
                return false;
            }
        }
        else
        {
            state = TrackGraphProgress(state, player);
            if (Math.Max(state.NoProgressTicks, state.NoHorizontalProgressTicks) >= GetGraphNoProgressTickLimit(goal.Label))
            {
                _graphStatesBySlot.Remove(slot);
                return false;
            }

            if (state.ActionIndex > 0
                && state.ActionTick == 0)
            {
                if (!TryRebuildGraphReplayState(
                        graph,
                        controlledSlot,
                        world,
                        player,
                        mapKey,
                        goalKey,
                        goal,
                        out state))
                {
                    _graphStatesBySlot.Remove(slot);
                    return false;
                }
            }
        }

        if (state.ActionIndex >= state.Actions.Count)
        {
            _graphStatesBySlot[slot] = state;
            return false;
        }

        if (IsDynamicGraphGoal(goal.Label)
            && TryBuildDynamicGraphWaypointInput(slot, world, controlledSlot.Team, graph, state, player, goal, out input, out state))
        {
            _graphStatesBySlot[slot] = state with { LastX = player.X, LastBottom = player.Bottom };
            return true;
        }

        var action = state.Actions[state.ActionIndex];
        if (IsGroundedIdleAction(action, player))
        {
            state = AdvanceGraphReplayState(state) with { LastX = player.X, LastBottom = player.Bottom, NoProgressTicks = 0, NoHorizontalProgressTicks = 0 };
            if (state.ActionIndex < state.Actions.Count)
            {
                action = state.Actions[state.ActionIndex];
            }
        }

        input = BuildActionInput(action, state.ActionTick, player);
        input = ApplyNavigationRescueInput(
            world,
            controlledSlot.Team,
            player,
            action,
            input,
            state.NoProgressTicks,
            state.NoHorizontalProgressTicks,
            noGoalProgressTicks: 0,
            allowObstacleJump: true,
            rescueJumpTicks: GetGraphRescueJumpTicks(goal.Label),
            allowDirectionReverse: true);
        _directSeekStatesBySlot.Remove(slot);
        _graphStatesBySlot[slot] = AdvanceGraphReplayState(state) with { LastX = player.X, LastBottom = player.Bottom };
        return true;
    }

    private bool TryRebuildGraphReplayState(
        PreparedMotionGraph graph,
        ControlledBotSlot controlledSlot,
        SimulationWorld world,
        PlayerEntity player,
        string mapKey,
        string goalKey,
        GraphGoal goal,
        out GraphReplayState state)
    {
        state = default;
        if (!TryBuildGraphPlan(
                graph,
                controlledSlot.Team,
                player.IsCarryingIntel,
                player,
                player.X,
                player.Bottom,
                player.IsGrounded,
                player.HorizontalSpeed,
                player.VerticalSpeed,
                player.Height,
                world,
                goal.X,
                goal.Bottom,
                goal.Radius,
                goal.Label,
                out var plan))
        {
            return false;
        }

        state = new GraphReplayState(
            mapKey,
            goalKey,
            plan.Actions,
            ActionIndex: 0,
            ActionTick: 0,
            plan.StartNodeIndex,
            plan.GoalNodeIndex,
            plan.GoalNodeX,
            plan.GoalNodeBottom,
            plan.PathNodeIndices,
            LastX: player.X,
            LastBottom: player.Bottom,
            NoProgressTicks: 0,
            NoHorizontalProgressTicks: 0);
        return true;
    }

    private static bool TryGetGraphGoal(SimulationWorld world, PlayerTeam team, PlayerEntity player, out GraphGoal goal)
    {
        var envX = Environment.GetEnvironmentVariable("OG_MOTION_PROOF_GOAL_X");
        var envY = Environment.GetEnvironmentVariable("OG_MOTION_PROOF_GOAL_Y");
        if (float.TryParse(envX, out var overrideX) && float.TryParse(envY, out var overrideY))
        {
            goal = new GraphGoal("override", overrideX, overrideY, GraphGoalRadius);
            return true;
        }

        if (ShouldPrioritizeEnemyGoals(world)
            && TrySelectEnemyGoal(world, player, out goal))
        {
            return true;
        }

        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            return TryGetCtfGraphGoal(world, team, player, out goal);
        }

        if (world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
        {
            return TryGetKothGraphGoal(world, team, player, out goal);
        }

        goal = default;
        return false;
    }

    private bool TryBuildObjectiveSeamInput(
        byte slot,
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PlayerEntity player,
        GraphGoal goal,
        out PlayerInputSnapshot input,
        out GraphGoal diagnosticGoal)
    {
        input = default;
        diagnosticGoal = default;

        if (_deferredObjectiveSeamProgramsBySlot.TryGetValue(slot, out var deferredProgram))
        {
            _deferredObjectiveSeamProgramsBySlot.Remove(slot);
            if (IsInsideObjectiveSeamStartWindow(player, deferredProgram, ignoreVelocityTolerances: false))
            {
                LogObjectiveSeamEvent(slot, player, deferredProgram, "seam_chain_resume");
                return TryStartObjectiveSeamInput(slot, world, controlledSlot, player, deferredProgram, out input, out diagnosticGoal);
            }

            LogObjectiveSeamEvent(slot, player, deferredProgram, "seam_chain_resume_rejected");
        }

        if (_objectiveSeamStatesBySlot.TryGetValue(slot, out var seamState))
        {
            if (IsObjectiveSeamGoalReached(player, seamState.Program))
            {
                _objectiveSeamIssueBySlot[slot] = "seam_goal_reached";
                LogObjectiveSeamEvent(slot, player, seamState.Program, _objectiveSeamIssueBySlot[slot]);
                _objectiveSeamStatesBySlot.Remove(slot);
                if (TrySelectObjectiveSeamProgram(
                        world,
                        controlledSlot,
                        player,
                        goal,
                        out var chainedProgram,
                        previousProgram: seamState.Program,
                        ignoreVelocityTolerances: true)
                    && !ReferenceEquals(chainedProgram, seamState.Program))
                {
                    if (HasCertifiedObjectiveSeamStartWindows(chainedProgram))
                    {
                        _deferredObjectiveSeamProgramsBySlot[slot] = chainedProgram;
                        diagnosticGoal = new GraphGoal(chainedProgram.Label, chainedProgram.GoalX, chainedProgram.GoalBottom, chainedProgram.GoalRadius);
                        input = BuildIdleInput(world, player);
                        LogObjectiveSeamEvent(slot, player, chainedProgram, $"seam_chain_deferred_from:{seamState.Program.Label}");
                        return true;
                    }

                    LogObjectiveSeamEvent(slot, player, chainedProgram, $"seam_chain_from:{seamState.Program.Label}");
                    return TryStartObjectiveSeamInput(slot, world, controlledSlot, player, chainedProgram, out input, out diagnosticGoal);
                }

                ArmObjectiveSeamReplayRecovery(slot, player);
                LogObjectiveSeamEvent(slot, player, seamState.Program, "seam_chain_none");
            }
            else
            {
                if (TryContinueObjectiveSeamInput(slot, world, controlledSlot.Team, player, ref seamState, out input, out diagnosticGoal))
                {
                    return true;
                }

                _objectiveSeamStatesBySlot.Remove(slot);
            }
        }

        if (!TrySelectObjectiveSeamProgram(world, controlledSlot, player, goal, out var program))
        {
            return false;
        }

        return TryStartObjectiveSeamInput(slot, world, controlledSlot, player, program, out input, out diagnosticGoal);
    }

    private bool TryContinueObjectiveSeamInput(
        byte slot,
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        ref ObjectiveSeamState state,
        out PlayerInputSnapshot input,
        out GraphGoal diagnosticGoal)
    {
        input = default;
        diagnosticGoal = default;
        if (IsObjectiveSeamGoalReached(player, state.Program))
        {
            ArmObjectiveSeamReplayRecovery(slot, player);
            _objectiveSeamIssueBySlot[slot] = "seam_goal_reached";
            LogObjectiveSeamEvent(slot, player, state.Program, _objectiveSeamIssueBySlot[slot]);
            _objectiveSeamStatesBySlot.Remove(slot);
            return false;
        }

        if (state.ActionIndex < 0 || state.ActionIndex >= state.Program.Actions.Count)
        {
            _objectiveSeamIssueBySlot[slot] = "seam_action_exhausted";
            LogObjectiveSeamEvent(slot, player, state.Program, _objectiveSeamIssueBySlot[slot]);
            _objectiveSeamStatesBySlot.Remove(slot);
            return false;
        }

        if (state.TicksRemaining <= 0)
        {
            _objectiveSeamIssueBySlot[slot] = "seam_time_budget_exhausted";
            LogObjectiveSeamEvent(slot, player, state.Program, _objectiveSeamIssueBySlot[slot]);
            _objectiveSeamStatesBySlot.Remove(slot);
            return false;
        }

        var moved = Distance(player.X, player.Bottom, state.LastX, state.LastBottom);
        var noMovementTicks = moved >= NoProgressDistance ? 0 : state.NoMovementTicks + 1;
        if (noMovementTicks >= state.Program.NoMovementTicks)
        {
            _objectiveSeamIssueBySlot[slot] = $"seam_no_movement:{noMovementTicks}";
            LogObjectiveSeamEvent(slot, player, state.Program, _objectiveSeamIssueBySlot[slot]);
            _objectiveSeamStatesBySlot.Remove(slot);
            return false;
        }

        var action = state.Program.Actions[state.ActionIndex];
        input = BuildActionInput(action, state.ActionTick, player);
        diagnosticGoal = new GraphGoal(state.Program.Label, state.Program.GoalX, state.Program.GoalBottom, state.Program.GoalRadius);
        state = AdvanceObjectiveSeamState(state, player, noMovementTicks);
        _objectiveSeamStatesBySlot[slot] = state;
        return true;
    }

    private bool TryStartObjectiveSeamInput(
        byte slot,
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PlayerEntity player,
        ObjectiveSeamProgram program,
        out PlayerInputSnapshot input,
        out GraphGoal diagnosticGoal)
    {
        input = default;
        diagnosticGoal = default;
        var seamState = new ObjectiveSeamState(
            program,
            ActionIndex: 0,
            ActionTick: 0,
            TicksRemaining: Math.Max(program.DurationTicks, program.Actions.Sum(action => Math.Max(1, action.Ticks))),
            NoMovementTicks: 0,
            LastX: player.X,
            LastBottom: player.Bottom);
        _statesBySlot.Remove(slot);
        _graphStatesBySlot.Remove(slot);
        _directSeekStatesBySlot.Remove(slot);
        _objectiveSeamStatesBySlot[slot] = seamState;
        LogObjectiveSeamEvent(slot, player, program, "seam_start");
        return TryContinueObjectiveSeamInput(slot, world, controlledSlot.Team, player, ref seamState, out input, out diagnosticGoal);
    }

    private static ObjectiveSeamState AdvanceObjectiveSeamState(ObjectiveSeamState state, PlayerEntity player, int noMovementTicks)
    {
        var nextActionTick = state.ActionTick + 1;
        var nextActionIndex = state.ActionIndex;
        var currentAction = state.Program.Actions[state.ActionIndex];
        if (nextActionTick >= Math.Max(1, currentAction.Ticks))
        {
            nextActionIndex += 1;
            nextActionTick = 0;
        }

        return state with
        {
            ActionIndex = nextActionIndex,
            ActionTick = nextActionTick,
            TicksRemaining = state.TicksRemaining - 1,
            NoMovementTicks = noMovementTicks,
            LastX = player.X,
            LastBottom = player.Bottom,
        };
    }

    private static bool IsObjectiveSeamGoalReached(PlayerEntity player, ObjectiveSeamProgram program)
    {
        var completionWindows = GetObjectiveSeamCompletionWindows(program);
        foreach (var completionWindow in completionWindows)
        {
            if (player.X >= completionWindow.XMin
                && player.X <= completionWindow.XMax
                && MathF.Abs(player.Bottom - completionWindow.Bottom) <= completionWindow.BottomTolerance
                && (!completionWindow.RequireGrounded || player.IsGrounded))
            {
                return true;
            }
        }

        if (HasCertifiedObjectiveSeamCompletionWindows(program))
        {
            return false;
        }

        return Distance(player.X, player.Bottom, program.GoalX, program.GoalBottom) <= program.GoalRadius;
    }

    private static bool IsInsideObjectiveSeamStartWindow(PlayerEntity player, ObjectiveSeamProgram program)
        => IsInsideObjectiveSeamStartWindow(player, program, ignoreVelocityTolerances: false);

    private static bool IsInsideObjectiveSeamStartWindow(
        PlayerEntity player,
        ObjectiveSeamProgram program,
        bool ignoreVelocityTolerances)
    {
        if (ignoreVelocityTolerances && HasCertifiedObjectiveSeamStartWindows(program))
        {
            ignoreVelocityTolerances = false;
        }

        var startWindows = GetObjectiveSeamStartWindows(program);
        if (startWindows.Count == 0)
        {
            return false;
        }

        foreach (var window in startWindows)
        {
            if (player.X < window.StartXMin
                || player.X > window.StartXMax
                || MathF.Abs(player.Bottom - window.StartBottom) > window.StartBottomTolerance
                || (!ignoreVelocityTolerances && MathF.Abs(player.HorizontalSpeed - window.HorizontalSpeedCenter) > window.HorizontalSpeedTolerance)
                || (!ignoreVelocityTolerances && MathF.Abs(player.VerticalSpeed - window.VerticalSpeedCenter) > window.VerticalSpeedTolerance)
                || player.IsGrounded != window.RequireGrounded)
            {
                continue;
            }

            if (window.FacingDirectionX > 0f && player.FacingDirectionX <= 0f)
            {
                continue;
            }

            if (window.FacingDirectionX < 0f && player.FacingDirectionX >= 0f)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static IReadOnlyList<ObjectiveSeamStartWindow> GetObjectiveSeamStartWindows(ObjectiveSeamProgram program)
    {
        if (!TraversalLabObjectiveSeamArtifactStore.TryGetCertification(program.Label, out var certification)
            || certification.StartWindows.Count == 0)
        {
            return program.StartWindows;
        }

        return certification.StartWindows
            .Select(static window => new ObjectiveSeamStartWindow(
                window.StartXMin,
                window.StartXMax,
                window.StartBottom,
                window.StartBottomTolerance,
                window.FacingDirectionX,
                window.HorizontalSpeedTolerance,
                window.VerticalSpeedTolerance,
                window.RequireGrounded,
                window.HorizontalSpeedCenter,
                window.VerticalSpeedCenter))
            .ToArray();
    }

    private static IReadOnlyList<ObjectiveSeamCompletionWindow> GetObjectiveSeamCompletionWindows(ObjectiveSeamProgram program)
    {
        if (TraversalLabObjectiveSeamArtifactStore.TryGetCertification(program.Label, out var certification)
            && certification.CompletionWindows.Count > 0)
        {
            return certification.CompletionWindows
                .Select(static window => new ObjectiveSeamCompletionWindow(
                    window.XMin,
                    window.XMax,
                    window.Bottom,
                    window.BottomTolerance,
                    window.RequireGrounded))
                .ToArray();
        }

        return program.CompletionWindow is { } completionWindow
            ? [completionWindow]
            : [];
    }

    private static bool HasCertifiedObjectiveSeamCompletionWindows(ObjectiveSeamProgram program)
        => TraversalLabObjectiveSeamArtifactStore.TryGetCertification(program.Label, out var certification)
            && certification.CompletionWindows.Count > 0;

    private static bool HasCertifiedObjectiveSeamStartWindows(ObjectiveSeamProgram program)
        => TraversalLabObjectiveSeamArtifactStore.TryGetCertification(program.Label, out var certification)
            && certification.StartWindows.Count > 0;

    private static bool IsAllowedObjectiveSeamChainCandidate(
        ObjectiveSeamProgram? previousProgram,
        ObjectiveSeamProgram candidateProgram)
    {
        if (previousProgram is null)
        {
            return true;
        }

        if (string.Equals(previousProgram.Label, candidateProgram.Label, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TraversalLabObjectiveSeamArtifactStore.TryGetCertifiedSuccessorLabels(previousProgram.Label, out var successorLabels))
        {
            return true;
        }

        return successorLabels.Contains(candidateProgram.Label, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TrySelectObjectiveSeamProgram(
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PlayerEntity player,
        GraphGoal goal,
        out ObjectiveSeamProgram program,
        ObjectiveSeamProgram? previousProgram = null,
        bool ignoreVelocityTolerances = false)
    {
        program = default!;
        if (world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
            && string.Equals(world.Level.Name, "Gallery", StringComparison.OrdinalIgnoreCase)
            && controlledSlot.Team == PlayerTeam.Blue
            && player.IsInSpawnRoom
            && goal.Label.StartsWith("control_point_", StringComparison.OrdinalIgnoreCase)
            && controlledSlot.ClassId is PlayerClass.Medic or PlayerClass.Demoman)
        {
            program = GalleryBlueSpawnExitSeamProgram;
            return true;
        }

        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag)
        {
            return false;
        }

        if (string.Equals(world.Level.Name, "TwodFortTwo", StringComparison.OrdinalIgnoreCase)
            && controlledSlot.Team == PlayerTeam.Blue
            && controlledSlot.ClassId == PlayerClass.Scout)
        {
            return false;
        }

        if (string.Equals(world.Level.Name, "Truefort", StringComparison.OrdinalIgnoreCase)
            && controlledSlot.Team == PlayerTeam.Blue
            && controlledSlot.ClassId == PlayerClass.Demoman)
        {
            if (player.IsCarryingIntel
                && player.IsGrounded
                && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
                && IsAllowedObjectiveSeamChainCandidate(previousProgram, TruefortBlueDemomanLateReturnPocketRetreatSeamProgram)
                && IsInsideObjectiveSeamStartWindow(player, TruefortBlueDemomanLateReturnPocketRetreatSeamProgram, ignoreVelocityTolerances))
            {
                program = TruefortBlueDemomanLateReturnPocketRetreatSeamProgram;
                return true;
            }

            if (player.IsCarryingIntel
                && player.IsGrounded
                && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
                && IsAllowedObjectiveSeamChainCandidate(previousProgram, TruefortBlueDemomanLateReturnStage997SeamProgram)
                && IsInsideObjectiveSeamStartWindow(player, TruefortBlueDemomanLateReturnStage997SeamProgram, ignoreVelocityTolerances))
            {
                program = TruefortBlueDemomanLateReturnStage997SeamProgram;
                return true;
            }

            if (player.IsCarryingIntel
                && player.IsGrounded
                && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
                && IsAllowedObjectiveSeamChainCandidate(previousProgram, TruefortBlueDemomanLateReturnStage654SeamProgram)
                && IsInsideObjectiveSeamStartWindow(player, TruefortBlueDemomanLateReturnStage654SeamProgram, ignoreVelocityTolerances))
            {
                program = TruefortBlueDemomanLateReturnStage654SeamProgram;
                return true;
            }

            if (player.IsCarryingIntel
                && player.IsGrounded
                && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
                && IsAllowedObjectiveSeamChainCandidate(previousProgram, TruefortBlueDemomanLateReturnStage445SeamProgram)
                && IsInsideObjectiveSeamStartWindow(player, TruefortBlueDemomanLateReturnStage445SeamProgram, ignoreVelocityTolerances))
            {
                program = TruefortBlueDemomanLateReturnStage445SeamProgram;
                return true;
            }

            if (player.IsCarryingIntel
                && player.IsGrounded
                && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
                && IsAllowedObjectiveSeamChainCandidate(previousProgram, TruefortBlueDemomanLateReturnStage361SeamProgram)
                && IsInsideObjectiveSeamStartWindow(player, TruefortBlueDemomanLateReturnStage361SeamProgram, ignoreVelocityTolerances))
            {
                program = TruefortBlueDemomanLateReturnStage361SeamProgram;
                return true;
            }

            if (player.IsCarryingIntel
                && player.IsGrounded
                && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
                && IsAllowedObjectiveSeamChainCandidate(previousProgram, TruefortBlueDemomanLateReturnStage103SeamProgram)
                && IsInsideObjectiveSeamStartWindow(player, TruefortBlueDemomanLateReturnStage103SeamProgram, ignoreVelocityTolerances))
            {
                program = TruefortBlueDemomanLateReturnStage103SeamProgram;
                return true;
            }

            if (player.IsCarryingIntel
                && player.IsGrounded
                && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
                && MathF.Abs(player.X - TruefortBlueDemomanFullReturnSeamStartX) <= TruefortBlueDemomanFullReturnSeamToleranceX
                && MathF.Abs(player.Bottom - TruefortBlueDemomanFullReturnSeamStartBottom) <= TruefortBlueDemomanFullReturnSeamToleranceBottom)
            {
                program = TruefortBlueDemomanFullReturnSeamProgram;
                return true;
            }

            if (player.IsCarryingIntel
                && player.IsGrounded
                && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
                && MathF.Abs(player.X - TruefortBlueDemomanReturnSeamStartX) <= TruefortBlueDemomanReturnSeamToleranceX
                && MathF.Abs(player.Bottom - TruefortBlueDemomanReturnSeamStartBottom) <= TruefortBlueDemomanReturnSeamToleranceBottom)
            {
                program = TruefortBlueDemomanReturnSeamProgram;
                return true;
            }

            if (!player.IsCarryingIntel
                && player.IsGrounded
                && MathF.Abs(player.HorizontalSpeed) <= TapeStartAlignmentStableHorizontalSpeed
                && MathF.Abs(player.VerticalSpeed) <= TapeStartAlignmentStableVerticalSpeed
                && goal.Label.Equals("enemy_intel", StringComparison.OrdinalIgnoreCase)
                && MathF.Abs(player.X - TruefortBlueDemomanAttackSeamStartX) <= TruefortBlueDemomanAttackSeamToleranceX
                && MathF.Abs(player.Bottom - TruefortBlueDemomanAttackSeamStartBottom) <= TruefortBlueDemomanAttackSeamToleranceBottom)
            {
                program = TruefortBlueDemomanAttackSeamProgram;
                return true;
            }

            if (!player.IsCarryingIntel
                && player.IsGrounded
                && goal.Label.Equals("enemy_intel", StringComparison.OrdinalIgnoreCase)
                && MathF.Abs(player.X - TruefortBlueDemomanLateAttackSeamStartX) <= TruefortBlueDemomanLateAttackSeamToleranceX
                && MathF.Abs(player.Bottom - TruefortBlueDemomanLateAttackSeamStartBottom) <= TruefortBlueDemomanLateAttackSeamToleranceBottom)
            {
                program = TruefortBlueDemomanLateAttackSeamProgram;
                return true;
            }

            if (!player.IsCarryingIntel
                && player.IsGrounded
                && goal.Label.Equals("enemy_intel", StringComparison.OrdinalIgnoreCase)
                && MathF.Abs(player.X - TruefortBlueDemomanPickupSeamStartX) <= TruefortBlueDemomanPickupSeamToleranceX
                && MathF.Abs(player.Bottom - TruefortBlueDemomanPickupSeamStartBottom) <= TruefortBlueDemomanPickupSeamToleranceBottom)
            {
                program = TruefortBlueDemomanPickupSeamProgram;
                return true;
            }

            if (!player.IsCarryingIntel
                && player.IsGrounded
                && goal.Label.Equals("enemy_intel", StringComparison.OrdinalIgnoreCase)
                && MathF.Abs(player.X - TruefortBlueDemomanDeepAttackSeamStartX) <= TruefortBlueDemomanDeepAttackSeamToleranceX
                && MathF.Abs(player.Bottom - TruefortBlueDemomanDeepAttackSeamStartBottom) <= TruefortBlueDemomanDeepAttackSeamToleranceBottom)
            {
                program = TruefortBlueDemomanDeepAttackSeamProgram;
                return true;
            }

        }

        if (string.Equals(world.Level.Name, "Conflict", StringComparison.OrdinalIgnoreCase))
        {
            if (false
                && !player.IsCarryingIntel
                && controlledSlot.ClassId == PlayerClass.Heavy
                && goal.Label.Equals("enemy_intel", StringComparison.OrdinalIgnoreCase)
                && MathF.Abs(player.X - ConflictHeavyAttackSeamStartX) <= ConflictHeavyAttackSeamToleranceX
                && MathF.Abs(player.Bottom - ConflictHeavyAttackSeamStartBottom) <= ConflictHeavyAttackSeamToleranceBottom)
            {
                program = ConflictBlueHeavyAttackSeamProgram;
                return true;
            }

            if (false
                && player.IsCarryingIntel
                && controlledSlot.ClassId == PlayerClass.Heavy
                && MathF.Abs(player.X - ConflictHeavyFinalReturnSeamStartX) <= ConflictHeavyFinalReturnSeamToleranceX
                && MathF.Abs(player.Bottom - ConflictHeavyFinalReturnSeamStartBottom) <= ConflictHeavyFinalReturnSeamToleranceBottom)
            {
                program = ConflictBlueHeavyFinalReturnSeamProgram;
                return true;
            }

            if (false
                && player.IsCarryingIntel
                && controlledSlot.ClassId == PlayerClass.Heavy
                && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
                && MathF.Abs(player.X - ConflictHeavyLateReturnSeamStartX) <= ConflictHeavyLateReturnSeamToleranceX
                && MathF.Abs(player.Bottom - ConflictHeavyLateReturnSeamStartBottom) <= ConflictHeavyLateReturnSeamToleranceBottom)
            {
                program = ConflictBlueHeavyLateReturnSeamProgram;
                return true;
            }

            if (false
                && player.IsCarryingIntel
                && controlledSlot.ClassId == PlayerClass.Heavy
                && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
                && MathF.Abs(player.X - ConflictHeavyReturnSeamStartX) <= ConflictHeavyReturnSeamToleranceX
                && MathF.Abs(player.Bottom - ConflictHeavyReturnSeamStartBottom) <= ConflictHeavyReturnSeamToleranceBottom)
            {
                program = ConflictBlueHeavyReturnSeamProgram;
                return true;
            }

            if (player.IsCarryingIntel
                && controlledSlot.ClassId == PlayerClass.Quote
                && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
                && MathF.Abs(player.X - ConflictQuoteLateReturnSeamStartX) <= ConflictQuoteLateReturnSeamToleranceX
                && MathF.Abs(player.Bottom - ConflictQuoteLateReturnSeamStartBottom) <= ConflictQuoteLateReturnSeamToleranceBottom)
            {
                program = ConflictBlueQuoteLateReturnSeamProgram;
                return true;
            }

            if (player.IsCarryingIntel
                && controlledSlot.ClassId == PlayerClass.Quote
                && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
                && MathF.Abs(player.X - ConflictQuoteReturnSeamStartX) <= ConflictQuoteReturnSeamToleranceX
                && MathF.Abs(player.Bottom - ConflictQuoteReturnSeamStartBottom) <= ConflictQuoteReturnSeamToleranceBottom)
            {
                program = ConflictBlueQuoteReturnSeamProgram;
                return true;
            }

            if (player.IsCarryingIntel
                && controlledSlot.ClassId == PlayerClass.Quote
                && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
                && MathF.Abs(player.X - ConflictQuoteFinalReturnSeamStartX) <= ConflictQuoteFinalReturnSeamToleranceX
                && MathF.Abs(player.Bottom - ConflictQuoteFinalReturnSeamStartBottom) <= ConflictQuoteFinalReturnSeamToleranceBottom)
            {
                program = ConflictBlueQuoteFinalReturnSeamProgram;
                return true;
            }

            if (player.IsCarryingIntel
                && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
                && MathF.Abs(player.X - ConflictReturnSeamStartX) <= ConflictReturnSeamToleranceX
                && MathF.Abs(player.Bottom - ConflictReturnSeamStartBottom) <= ConflictReturnSeamToleranceBottom)
            {
                program = controlledSlot.ClassId switch
                {
                    PlayerClass.Engineer => ConflictBlueEngineerReturnSeamProgram,
                    PlayerClass.Medic => ConflictBlueMedicReturnSeamProgram,
                    PlayerClass.Spy => ConflictBlueSpyReturnSeamProgram,
                    _ => null!,
                };
                return program is not null;
            }

        }

        if (string.Equals(world.Level.Name, "TwodFortTwo", StringComparison.OrdinalIgnoreCase)
            && controlledSlot.Team == PlayerTeam.Blue
            && controlledSlot.ClassId == PlayerClass.Scout
            && player.IsCarryingIntel
            && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
            && MathF.Abs(player.X - TwodFortTwoScoutUpperRecoverySeamStartX) <= TwodFortTwoScoutUpperRecoverySeamToleranceX
            && MathF.Abs(player.Bottom - TwodFortTwoScoutUpperRecoverySeamStartBottom) <= TwodFortTwoScoutUpperRecoverySeamToleranceBottom)
        {
            program = TwodFortTwoBlueScoutUpperRecoverySeamProgram;
            return true;
        }

        if (string.Equals(world.Level.Name, "TwodFortTwo", StringComparison.OrdinalIgnoreCase)
            && controlledSlot.Team == PlayerTeam.Blue
            && controlledSlot.ClassId == PlayerClass.Scout
            && player.IsCarryingIntel
            && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
            && MathF.Abs(player.X - TwodFortTwoScoutDeepRecoverySeamStartX) <= TwodFortTwoScoutDeepRecoverySeamToleranceX
            && MathF.Abs(player.Bottom - TwodFortTwoScoutDeepRecoverySeamStartBottom) <= TwodFortTwoScoutDeepRecoverySeamToleranceBottom)
        {
            program = TwodFortTwoBlueScoutDeepRecoverySeamProgram;
            return true;
        }

        if (string.Equals(world.Level.Name, "TwodFortTwo", StringComparison.OrdinalIgnoreCase)
            && controlledSlot.Team == PlayerTeam.Blue
            && controlledSlot.ClassId == PlayerClass.Scout
            && player.IsCarryingIntel
            && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
            && MathF.Abs(player.X - TwodFortTwoScoutReturnRecoverySeamStartX) <= TwodFortTwoScoutReturnRecoverySeamToleranceX
            && MathF.Abs(player.Bottom - TwodFortTwoScoutReturnRecoverySeamStartBottom) <= TwodFortTwoScoutReturnRecoverySeamToleranceBottom)
        {
            program = TwodFortTwoBlueScoutReturnRecoverySeamProgram;
            return true;
        }

        if (string.Equals(world.Level.Name, "TwodFortTwo", StringComparison.OrdinalIgnoreCase)
            && controlledSlot.Team == PlayerTeam.Blue
            && controlledSlot.ClassId == PlayerClass.Scout
            && player.IsCarryingIntel
            && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
            && MathF.Abs(player.X - TwodFortTwoScoutLateReturnSeamStartX) <= TwodFortTwoScoutLateReturnSeamToleranceX
            && MathF.Abs(player.Bottom - TwodFortTwoScoutLateReturnSeamStartBottom) <= TwodFortTwoScoutLateReturnSeamToleranceBottom)
        {
            program = TwodFortTwoBlueScoutLateReturnSeamProgram;
            return true;
        }

        if (string.Equals(world.Level.Name, "TwodFortTwo", StringComparison.OrdinalIgnoreCase)
            && controlledSlot.Team == PlayerTeam.Blue
            && controlledSlot.ClassId == PlayerClass.Scout
            && player.IsCarryingIntel
            && goal.Label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
            && MathF.Abs(player.X - TwodFortTwoScoutReturnSeamStartX) <= TwodFortTwoScoutReturnSeamToleranceX
            && MathF.Abs(player.Bottom - TwodFortTwoScoutReturnSeamStartBottom) <= TwodFortTwoScoutReturnSeamToleranceBottom)
        {
            program = TwodFortTwoBlueScoutReturnSeamProgram;
            return true;
        }

        if (string.Equals(world.Level.Name, "TwodFortTwo", StringComparison.OrdinalIgnoreCase)
            && controlledSlot.Team == PlayerTeam.Blue
            && controlledSlot.ClassId == PlayerClass.Scout
            && !player.IsCarryingIntel
            && goal.Label.Equals("enemy_intel", StringComparison.OrdinalIgnoreCase)
            && MathF.Abs(player.X - TwodFortTwoScoutAttackSeamStartX) <= TwodFortTwoScoutAttackSeamToleranceX
            && MathF.Abs(player.Bottom - TwodFortTwoScoutAttackSeamStartBottom) <= TwodFortTwoScoutAttackSeamToleranceBottom)
        {
            program = TwodFortTwoBlueScoutAttackSeamProgram;
            return true;
        }

        return false;
    }

    private static bool TryGetKothGraphGoal(SimulationWorld world, PlayerTeam team, PlayerEntity player, out GraphGoal goal)
    {
        var point = SelectKothTargetPoint(world, team);
        if (point is null)
        {
            goal = default;
            return false;
        }

        var enemyCappers = team == PlayerTeam.Blue ? point.RedCappers : point.BlueCappers;
        var contested = enemyCappers > 0
            || point.CappingTeam.HasValue && point.CappingTeam.Value != team && point.CappingTicks > 0f;
        if (point.Team == team && !contested && TrySelectEnemyGoal(world, player, out goal))
        {
            return true;
        }

        goal = new GraphGoal($"control_point_{point.Index}", point.Marker.CenterX, point.Marker.CenterY, GraphGoalRadius);
        return true;
    }

    private static bool TryGetCtfGraphGoal(SimulationWorld world, PlayerTeam team, PlayerEntity player, out GraphGoal goal)
    {
        var ownIntel = GetIntelState(world, team);
        var enemyTeam = GetOpposingTeam(team);
        var enemyIntel = GetIntelState(world, enemyTeam);
        if (player.IsCarryingIntel)
        {
            goal = new GraphGoal("own_intel", ownIntel.HomeX, ownIntel.HomeY, GraphCtfGoalRadius);
            return true;
        }

        if (TryFindIntelCarrier(world, enemyTeam, out var enemyCarrier))
        {
            goal = new GraphGoal("enemy_carrier", enemyCarrier.X, enemyCarrier.Bottom, GraphGoalRadius);
            return true;
        }

        if (TryFindIntelCarrier(world, team, out var allyCarrier))
        {
            goal = new GraphGoal("ally_carrier", allyCarrier.X, allyCarrier.Bottom, GraphGoalRadius);
            return true;
        }

        if (enemyIntel.IsDropped)
        {
            goal = new GraphGoal("dropped_enemy_intel", enemyIntel.X, enemyIntel.Y, GraphCtfGoalRadius);
            return true;
        }

        if (ownIntel.IsDropped)
        {
            goal = new GraphGoal("dropped_own_intel", ownIntel.X, ownIntel.Y, GraphCtfGoalRadius);
            return true;
        }

        goal = new GraphGoal("enemy_intel", enemyIntel.HomeX, enemyIntel.HomeY, GraphCtfGoalRadius);
        return true;
    }

    private bool TryBuildObjectiveTerminalInput(
        byte slot,
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        GraphGoal goal,
        out PlayerInputSnapshot input)
    {
        input = default;
        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            if (IsCtfTerminalGoal(goal.Label)
                && player.IntersectsMarker(goal.X, goal.Bottom, CtfIntelPickupMarkerSize, CtfIntelPickupMarkerSize))
            {
                input = BuildCtfTerminalInput(slot, world, team, player, goal);
                return true;
            }

            return false;
        }

        if (world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
            && goal.Label.StartsWith("control_point_", StringComparison.OrdinalIgnoreCase)
            && SelectKothTargetPoint(world, team) is { } point)
        {
            if (world.IsPlayerInControlPointCaptureZone(player, point.Index))
            {
                return TryBuildObjectiveHoldInput(world, slot, team, player, out input);
            }
        }

        return false;
    }

    private PlayerInputSnapshot BuildCtfTerminalInput(
        byte slot,
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        GraphGoal goal)
    {
        if (player.IntersectsMarker(goal.X, goal.Bottom, CtfIntelPickupMarkerSize, CtfIntelPickupMarkerSize))
        {
            return BuildIntelMarkerHoldInput(world, slot, team, player, goal);
        }

        var terminalGoal = new GraphGoal($"terminal_{goal.Label}", goal.X, goal.Bottom, ObjectiveTerminalTightRadius);
        var input = BuildDirectSeekInput(slot, world, team, player, terminalGoal);
        if (MathF.Abs(goal.X - player.X) <= DirectSeekHorizontalDeadZone
            && !player.IntersectsMarker(goal.X, goal.Bottom, CtfIntelPickupMarkerSize, CtfIntelPickupMarkerSize))
        {
            return BuildIntelMarkerHoldInput(world, slot, team, player, goal) with
            {
                Down = goal.Bottom > player.Bottom + DirectSeekVerticalDropThreshold,
            };
        }

        return input;
    }

    private static PlayerInputSnapshot BuildIntelMarkerHoldInput(
        SimulationWorld world,
        byte slot,
        PlayerTeam team,
        PlayerEntity player,
        GraphGoal goal)
    {
        var direction = ((world.Frame + slot * 13) / ObjectiveHoldStrafePeriodTicks) % 2 == 0 ? -1 : 1;
        if (WouldMoveIntoObstacle(world, team, player, direction, probeDistance: 16f))
        {
            var reversed = -direction;
            direction = WouldMoveIntoObstacle(world, team, player, reversed, probeDistance: 16f) ? 0 : reversed;
        }

        var hopFrame = (int)((world.Frame + slot * 5) % ObjectiveHoldHopPeriodTicks);
        return new PlayerInputSnapshot(
            Left: direction < 0,
            Right: direction > 0,
            Up: player.IsGrounded && hopFrame < ObjectiveHoldHopPulseTicks,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: goal.X,
            AimWorldY: goal.Bottom,
            DebugKill: false);
    }

    private static bool IsCtfTerminalGoal(string label)
    {
        return label.Equals("enemy_intel", StringComparison.OrdinalIgnoreCase)
            || label.Equals("own_intel", StringComparison.OrdinalIgnoreCase)
            || label.Equals("dropped_enemy_intel", StringComparison.OrdinalIgnoreCase)
            || label.Equals("dropped_own_intel", StringComparison.OrdinalIgnoreCase);
    }

    private static TeamIntelligenceState GetIntelState(SimulationWorld world, PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? world.BlueIntel : world.RedIntel;
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }

    private static bool TryFindIntelCarrier(SimulationWorld world, PlayerTeam team, out PlayerEntity carrier)
    {
        foreach (var (_, candidate) in world.EnumerateActiveNetworkPlayers())
        {
            if (candidate.Team == team
                && candidate.IsAlive
                && candidate.IsCarryingIntel)
            {
                carrier = candidate;
                return true;
            }
        }

        carrier = null!;
        return false;
    }

    private static bool TryGetDirectEnemyGoal(SimulationWorld world, PlayerEntity player, out GraphGoal goal)
    {
        if (!ShouldPrioritizeEnemyGoals(world))
        {
            goal = default;
            return false;
        }

        return TrySelectEnemyGoal(world, player, out goal);
    }

    private PlayerInputSnapshot BuildDirectSeekInput(
        byte slot,
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        GraphGoal goal)
    {
        var goalKey = BuildDirectSeekGoalKey(goal);
        var currentGoalDistance = Distance(player.X, player.Bottom, goal.X, goal.Bottom);
        var hasState = _directSeekStatesBySlot.TryGetValue(slot, out var state)
            && state.GoalKey.Equals(goalKey, StringComparison.OrdinalIgnoreCase);
        if (!hasState)
        {
            state = new DirectSeekState(goalKey, player.X, player.Bottom, currentGoalDistance, NoProgressTicks: 0, NoHorizontalProgressTicks: 0, NoGoalProgressTicks: 0, TotalTicks: 0, BackupTicksRemaining: 0, BackupDirection: 0, VerticalSearchDirection: 0);
        }
        else if (ShouldResetDirectSeekState(state, currentGoalDistance))
        {
            state = new DirectSeekState(goalKey, player.X, player.Bottom, currentGoalDistance, NoProgressTicks: 0, NoHorizontalProgressTicks: 0, NoGoalProgressTicks: 0, TotalTicks: 0, BackupTicksRemaining: 0, BackupDirection: 0, VerticalSearchDirection: 0);
        }
        else
        {
            state = TrackDirectSeekProgress(state, player, currentGoalDistance);
        }

        var deltaX = goal.X - player.X;
        var deltaBottom = goal.Bottom - player.Bottom;
        var goalCenterY = goal.Bottom - (player.Height * 0.5f);
        var isLocalObjectiveGoal = goal.Label.StartsWith("control_point_", StringComparison.OrdinalIgnoreCase);
        var targetAbove = deltaBottom < -DirectSeekVerticalJumpThreshold;
        var targetBelow = deltaBottom > DirectSeekVerticalDropThreshold
            || (isLocalObjectiveGoal
                && MathF.Abs(deltaX) <= 96f
                && deltaBottom > DirectSeekVerticalJumpThreshold * 2f);
        var isDynamicEnemyGoal = IsDynamicGraphGoal(goal.Label);
        var allowReverseRecovery = isDynamicEnemyGoal || isLocalObjectiveGoal;
        var isTapeReattachGoal = goal.Label.StartsWith("tape_reattach_", StringComparison.OrdinalIgnoreCase);
        var hasGoalLineOfSight = !isDynamicEnemyGoal
            || HasLineOfSight(world, player.X, player.Y, goal.X, goalCenterY, team, player.IsCarryingIntel);
        var targetDirection = MathF.Abs(deltaX) > DirectSeekHorizontalDeadZone
            ? Math.Sign(deltaX)
            : 0;
        var verticalSearchDirection = state.VerticalSearchDirection;
        var nearLocalObjectiveClimbWindow = player.IsGrounded
            && isLocalObjectiveGoal
            && !targetBelow
            && MathF.Abs(deltaX) <= GraphAttachRadius;
        var leftJumpable = nearLocalObjectiveClimbWindow && HasJumpableVerticalAccessAhead(world, team, player, -1);
        var rightJumpable = nearLocalObjectiveClimbWindow && HasJumpableVerticalAccessAhead(world, team, player, 1);
        var hasNearbyLocalObjectiveClimbAccess = leftJumpable || rightJumpable;
        var shouldUseWideVerticalSearch = targetAbove
            && (isLocalObjectiveGoal || isDynamicEnemyGoal)
            && MathF.Abs(deltaX) <= DirectSeekVerticalSearchHorizontalRadius;
        var shouldSearchForLineOfSight = isDynamicEnemyGoal
            && !hasGoalLineOfSight
            && (currentGoalDistance <= DirectSeekLineOfSightSearchDistance
                || state.NoGoalProgressTicks >= DirectSeekRescueJumpTicks);
        var shouldSearchLocalObjectiveDrop = isLocalObjectiveGoal
            && targetBelow
            && MathF.Abs(deltaX) <= 96f;
        var shouldSearchLocalObjectiveClimb = hasNearbyLocalObjectiveClimbAccess
            && (!hasGoalLineOfSight
                || targetAbove
                || state.NoHorizontalProgressTicks >= 2);
        var shouldSearchVertically = (MathF.Abs(deltaBottom) >= DirectSeekVerticalSearchThreshold
                && (MathF.Abs(deltaX) <= GraphAttachRadius || shouldUseWideVerticalSearch || verticalSearchDirection != 0))
            || shouldSearchForLineOfSight
            || shouldSearchLocalObjectiveDrop
            || shouldSearchLocalObjectiveClimb;
        var shouldRefreshVerticalSearchDirection = shouldSearchVertically
            && verticalSearchDirection != 0
            && ((shouldSearchForLineOfSight && state.NoGoalProgressTicks >= DirectSeekRescueJumpTicks)
                || state.NoHorizontalProgressTicks >= DirectSeekRescueJumpTicks
                || (player.IsGrounded
                    && WouldMoveIntoObstacle(
                        world,
                        team,
                        player,
                        verticalSearchDirection,
                        probeDistance: DirectSeekObstacleProbeDistance)));
        if (isTapeReattachGoal
            && targetAbove
            && MathF.Abs(deltaX) > GraphAttachRadius)
        {
            shouldSearchVertically = false;
            verticalSearchDirection = 0;
        }
        if (shouldSearchVertically)
        {
            if (shouldSearchLocalObjectiveClimb && hasNearbyLocalObjectiveClimbAccess)
            {
                verticalSearchDirection = leftJumpable && rightJumpable
                    ? SelectVerticalSearchDirection(world, team, player, goal, state.VerticalSearchDirection)
                    : leftJumpable ? -1 : 1;
            }
            else if (verticalSearchDirection == 0
                || shouldRefreshVerticalSearchDirection
                || (shouldSearchForLineOfSight
                    && state.NoHorizontalProgressTicks >= DirectSeekStuckBackupTicks * 2
                    && state.NoGoalProgressTicks >= DirectSeekVerticalFlipNoGoalProgressTicks))
            {
                verticalSearchDirection = SelectVerticalSearchDirection(world, team, player, goal, state.VerticalSearchDirection);
            }

            if (!allowReverseRecovery
                && !targetBelow
                && targetDirection != 0
                && verticalSearchDirection != 0
                && verticalSearchDirection != targetDirection)
            {
                verticalSearchDirection = targetDirection;
            }

            targetDirection = verticalSearchDirection;
        }
        else
        {
            verticalSearchDirection = 0;
        }

        if (targetDirection == 0
            && player.IsGrounded
            && !targetBelow
            && isLocalObjectiveGoal)
        {
            if (leftJumpable || rightJumpable)
            {
                targetDirection = leftJumpable && rightJumpable
                    ? SelectVerticalSearchDirection(world, team, player, goal, player.FacingDirectionX >= 0f ? 1 : -1)
                    : leftJumpable ? -1 : 1;
            }
        }

        var direction = targetDirection;
        var backupEscape = false;
        var horizontalStall = direction != 0
            && player.IsGrounded
            && state.NoHorizontalProgressTicks >= DirectSeekRescueJumpTicks;
        if (state.BackupTicksRemaining > 0 && state.BackupDirection != 0)
        {
            direction = state.BackupDirection;
            backupEscape = true;
            state = state with { BackupTicksRemaining = state.BackupTicksRemaining - 1 };
        }
        else if (allowReverseRecovery
            && targetDirection != 0
            && ShouldReverseDirectSeek(state, shouldSearchVertically, targetAbove)
            && (WouldMoveIntoObstacle(world, team, player, targetDirection)
                || MathF.Abs(deltaBottom) >= DirectSeekVerticalSearchThreshold
                || state.NoHorizontalProgressTicks >= DirectSeekStuckBackupTicks * 2))
        {
            direction = -targetDirection;
            backupEscape = true;
            if (shouldSearchVertically)
            {
                verticalSearchDirection = direction;
            }

            state = state with
            {
                BackupTicksRemaining = DirectSeekBackupTicks,
                BackupDirection = direction,
                NoProgressTicks = 0,
            };
        }

        var frontFootBlocked = direction != 0
            && player.IsGrounded
            && HasDirectSeekForwardFootBlock(world, team, player, direction, probeDistance: 8f)
            && HasDirectSeekJumpHeadClear(world, team, player);
        var upcomingFootBlock = direction != 0
            && player.IsGrounded
            && HasUpcomingDirectSeekFootBlock(world, team, player, direction, startProbeDistance: 18f, endProbeDistance: 42f, step: 6f)
            && HasDirectSeekJumpHeadClear(world, team, player);
        var obstacleAhead = direction != 0
            && (WouldMoveIntoObstacle(world, team, player, direction, DirectSeekObstacleProbeDistance) || frontFootBlocked);
        if (targetBelow
            && shouldSearchVertically
            && player.IsGrounded
            && TryFindNearestDropDirection(world, team, player, verticalSearchDirection, out var dropDirection))
        {
            if (verticalSearchDirection != 0
                && dropDirection != verticalSearchDirection
                && state.NoGoalProgressTicks < DirectSeekVerticalFlipNoGoalProgressTicks)
            {
                dropDirection = verticalSearchDirection;
            }

            direction = dropDirection;
            obstacleAhead = WouldMoveIntoObstacle(world, team, player, direction);
            verticalSearchDirection = direction;
        }

        var verticalClimbJump = targetAbove
            && direction != 0
            && ShouldJumpForVerticalChase(
                world,
                team,
                player,
                direction,
                state.NoProgressTicks,
                state.NoHorizontalProgressTicks,
                state.NoGoalProgressTicks,
                state.TotalTicks);
        var immediateClimbJump = direction != 0
            && player.IsGrounded
            && HasJumpableVerticalAccessAhead(world, team, player, direction)
            && (!targetBelow || targetAbove || MathF.Abs(deltaBottom) <= DirectSeekClimbClearance);
        var belowObstacleClearJump = targetBelow
            && direction != 0
            && obstacleAhead
            && HasJumpableVerticalAccessAhead(world, team, player, direction);
        var belowLipRecoveryJump = targetBelow
            && direction != 0
            && player.IsGrounded
            && state.NoHorizontalProgressTicks >= 4
            && MathF.Abs(deltaBottom) <= 160f
            && frontFootBlocked;
        var immediateLipJump = !targetBelow
            && direction != 0
            && player.IsGrounded
            && frontFootBlocked;
        var ledgeApproachJump = targetAbove
            && direction != 0
            && player.IsGrounded
            && MathF.Abs(deltaX) >= 24f
            && MathF.Abs(deltaX) <= 168f
            && upcomingFootBlock;
        var dynamicRescueJumpPulse = isDynamicEnemyGoal
            && !targetBelow
            && direction != 0
            && state.NoGoalProgressTicks >= DirectSeekRescueJumpTicks
            && ((state.NoGoalProgressTicks - DirectSeekRescueJumpTicks) % DirectSeekDynamicRescueJumpPulsePeriodTicks) < DirectSeekDynamicRescueJumpPulseTicks
            && (!targetAbove || hasGoalLineOfSight || obstacleAhead);
        var backupEscapeJump = backupEscape
            && !targetBelow
            && direction != 0
            && state.BackupTicksRemaining >= DirectSeekBackupTicks - DirectSeekBackupJumpPulseTicks;
        var groundJump = (!targetBelow && obstacleAhead)
            || immediateClimbJump
            || verticalClimbJump
            || belowObstacleClearJump
            || belowLipRecoveryJump
            || immediateLipJump
            || ledgeApproachJump
            || (targetAbove && hasGoalLineOfSight && MathF.Abs(deltaX) <= 260f)
            || (targetBelow && shouldSearchVertically && obstacleAhead)
            || dynamicRescueJumpPulse
            || backupEscapeJump
            || (horizontalStall && !targetBelow)
            || (!targetBelow && state.NoProgressTicks >= DirectSeekRescueJumpTicks && targetDirection != 0);
        var airRescueJump = !targetBelow
            && !player.IsGrounded
            && direction != 0
            && state.NoProgressTicks >= DirectSeekRescueJumpTicks;
        var shouldJump = (player.IsGrounded && groundJump) || dynamicRescueJumpPulse || airRescueJump;
        var shouldDrop = !shouldJump
            && targetBelow
            && (MathF.Abs(deltaX) <= 96f || shouldSearchVertically);

        var aimDirection = targetDirection != 0 ? targetDirection : player.FacingDirectionX;
        var input = new PlayerInputSnapshot(
            Left: direction < 0,
            Right: direction > 0,
            Up: shouldJump,
            Down: shouldDrop,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: goal.X + (aimDirection * 24f),
            AimWorldY: goalCenterY,
            DebugKill: false);

        _directSeekStatesBySlot[slot] = state with
        {
            LastX = player.X,
            LastBottom = player.Bottom,
            BestGoalDistance = state.BestGoalDistance,
            TotalTicks = state.TotalTicks + 1,
            VerticalSearchDirection = verticalSearchDirection,
        };
        return input;
    }

    private static bool ShouldEscalateEnemyChaseToGraph(byte slot, SimulationWorld world, PlayerEntity player, GraphGoal goal)
    {
        // Enemy chasing needs continuous retargeting. The baked graph is still useful for static goals, but
        // its momentum-specific edges are too brittle for live moving targets, so dynamic combat stays on
        // the adaptive direct seeker.
        return false;
    }

    private static bool ShouldResetDirectSeekState(DirectSeekState state, float currentGoalDistance)
    {
        return state.NoGoalProgressTicks >= DirectSeekRescueJumpTicks
            && currentGoalDistance > state.BestGoalDistance + DirectSeekStateStaleDistance;
    }

    private static bool ShouldReverseDirectSeek(DirectSeekState state, bool shouldSearchVertically, bool targetAbove)
    {
        if (targetAbove && shouldSearchVertically)
        {
            return state.NoHorizontalProgressTicks >= DirectSeekStuckBackupTicks * 2
                && state.NoGoalProgressTicks >= DirectSeekVerticalFlipNoGoalProgressTicks;
        }

        if (state.NoProgressTicks >= DirectSeekStuckBackupTicks)
        {
            return true;
        }

        if (!shouldSearchVertically || state.NoHorizontalProgressTicks < DirectSeekStuckBackupTicks)
        {
            return false;
        }

        return !targetAbove || state.NoGoalProgressTicks >= DirectSeekVerticalFlipNoGoalProgressTicks;
    }

    private static int SelectVerticalSearchDirection(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        GraphGoal goal,
        int fallbackDirection)
    {
        var leftScore = ScoreVerticalSearchDirection(world, team, player, goal, -1, fallbackDirection);
        var rightScore = ScoreVerticalSearchDirection(world, team, player, goal, 1, fallbackDirection);
        if (MathF.Abs(leftScore - rightScore) <= 0.01f)
        {
            if (fallbackDirection != 0)
            {
                return Math.Sign(fallbackDirection);
            }

            return player.FacingDirectionX >= 0f ? 1 : -1;
        }

        return rightScore > leftScore ? 1 : -1;
    }

    private static float ScoreVerticalSearchDirection(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        GraphGoal goal,
        int direction,
        int fallbackDirection)
    {
        var clearDistance = 0f;
        var bestScore = fallbackDirection == direction ? 24f : 0f;
        var foundJumpableAccess = false;
        var targetBelow = goal.Bottom > player.Bottom + DirectSeekVerticalDropThreshold;
        var targetAbove = goal.Bottom < player.Bottom - DirectSeekVerticalJumpThreshold;
        var dynamicEnemyGoal = IsDynamicGraphGoal(goal.Label);
        var goalCenterY = goal.Bottom - (player.Height * 0.5f);
        for (var distance = DirectSeekEscapeProbeStep; distance <= DirectSeekEscapeProbeMaxDistance; distance += DirectSeekEscapeProbeStep)
        {
            var offsetX = direction * distance;
            if (!IsHorizontallyClearProbe(world, team, player, offsetX))
            {
                if (targetAbove
                    && IsJumpableVerticalAccessProbe(world, team, player, direction, distance))
                {
                    foundJumpableAccess = true;
                    bestScore = MathF.Max(bestScore, 12_000f - distance);
                }

                break;
            }

            clearDistance = distance;
            var probeX = player.X + offsetX;
            var distanceFromProbe = Distance(probeX, player.Bottom, goal.X, goal.Bottom);
            var horizontalDistanceFromProbe = MathF.Abs(goal.X - probeX);
            var score = dynamicEnemyGoal
                ? -distanceFromProbe + (distance * 0.1f)
                : -horizontalDistanceFromProbe - (distanceFromProbe * 0.25f) + (distance * 0.05f);
            if (HasLineOfSight(world, probeX, player.Y, goal.X, goalCenterY, team, player.IsCarryingIntel))
            {
                score += 20_000f - distanceFromProbe;
            }

            if (targetBelow && IsViableDropProbe(world, team, player, offsetX))
            {
                score += 8_000f - distance;
            }

            if (targetBelow)
            {
                score += MathF.Max(0f, EstimateDropClearance(world, team, player, offsetX));
            }
            else if (targetAbove && WouldMoveIntoObstacle(world, team, player, direction, distance))
            {
                score += 120f;
            }

            if (score > bestScore)
            {
                bestScore = score;
            }
        }

        if (clearDistance <= 0f && !foundJumpableAccess)
        {
            return -100_000f;
        }

        return bestScore + clearDistance;
    }

    private static bool ShouldJumpForVerticalChase(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        int direction,
        int noProgressTicks,
        int noHorizontalProgressTicks,
        int noGoalProgressTicks,
        int totalTicks)
    {
        if (!player.IsGrounded || direction == 0)
        {
            return false;
        }

        if (HasJumpableVerticalAccessAhead(world, team, player, direction))
        {
            return true;
        }

        return noProgressTicks >= 4
            || noHorizontalProgressTicks >= 4
            || noGoalProgressTicks >= 8
            || totalTicks % 10 == 0;
    }

    private static bool HasJumpableVerticalAccessAhead(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        int direction)
    {
        for (var distance = DirectSeekClimbProbeStep; distance <= DirectSeekClimbProbeMaxDistance; distance += DirectSeekClimbProbeStep)
        {
            if (IsJumpableVerticalAccessProbe(world, team, player, direction, distance))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDirectSeekForwardFootBlock(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        int direction,
        float probeDistance)
    {
        var feetY = player.Bottom;
        var probeX = player.X + (Math.Sign(direction) * probeDistance);
        return IntersectsMovementBlocker(world, team, player, probeX - 1f, feetY - 1f, probeX + 1f, feetY + 4f);
    }

    private static bool HasUpcomingDirectSeekFootBlock(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        int direction,
        float startProbeDistance,
        float endProbeDistance,
        float step)
    {
        if (direction == 0)
        {
            return false;
        }

        for (var probeDistance = startProbeDistance; probeDistance <= endProbeDistance; probeDistance += step)
        {
            if (HasDirectSeekForwardFootBlock(world, team, player, direction, probeDistance))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDirectSeekJumpHeadClear(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player)
    {
        var feetY = player.Bottom;
        return !IntersectsMovementBlocker(world, team, player, player.X - 1f, feetY - (DirectSeekClimbClearance + 2f), player.X + 1f, feetY - 2f);
    }

    private static bool IsJumpableVerticalAccessProbe(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        int direction,
        float distance)
    {
        var probeX = player.X + (Math.Sign(direction) * distance);
        if (IsCollisionClearAt(world, team, player, probeX, player.Y))
        {
            return false;
        }

        for (var clearance = 24f; clearance <= DirectSeekClimbClearance; clearance += 20f)
        {
            if (IsCollisionClearAt(world, team, player, probeX, player.Y - clearance))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCollisionClearAt(SimulationWorld world, PlayerTeam team, PlayerEntity player, float x, float y)
    {
        player.GetCollisionBoundsAt(x, y, out var left, out var top, out var right, out var bottom);
        if (IsOutsideLevelBounds(world, left, top, right, bottom))
        {
            return false;
        }

        if (world.Level.IntersectsSolid(left, top, right, bottom))
        {
            return false;
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, player.IsCarryingIntel))
        {
            if (RectanglesOverlap(left, top, right, bottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                return false;
            }
        }

        foreach (var wall in world.Level.RoomObjects)
        {
            if (wall.Type != RoomObjectType.PlayerWall)
            {
                continue;
            }

            if (RectanglesOverlap(left, top, right, bottom, wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IntersectsMovementBlocker(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        float left,
        float top,
        float right,
        float bottom)
    {
        if (IsOutsideLevelBounds(world, left, top, right, bottom))
        {
            return true;
        }

        if (world.Level.IntersectsSolid(left, top, right, bottom))
        {
            return true;
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, player.IsCarryingIntel))
        {
            if (RectanglesOverlap(left, top, right, bottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                return true;
            }
        }

        foreach (var wall in world.Level.RoomObjects)
        {
            if (wall.Type != RoomObjectType.PlayerWall)
            {
                continue;
            }

            if (RectanglesOverlap(left, top, right, bottom, wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                return true;
            }
        }

        return false;
    }

    private static float EstimateDropClearance(SimulationWorld world, PlayerTeam team, PlayerEntity player, float offsetX)
    {
        var probeX = player.X + offsetX;
        player.GetCollisionBoundsAt(probeX, player.Y, out var left, out _, out var right, out var bottom);
        foreach (var gate in world.Level.GetBlockingTeamGates(team, player.IsCarryingIntel))
        {
            if (RectanglesOverlap(left, bottom + 1f, right, bottom + DirectSeekDropProbeDepth, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                return 0f;
            }
        }

        var clearDepth = 0f;
        for (var depth = DirectSeekDropProbeStep; depth <= DirectSeekDropProbeMaxDistance; depth += DirectSeekDropProbeStep)
        {
            if (world.Level.IntersectsSolid(left, bottom + 1f, right, bottom + depth))
            {
                break;
            }

            clearDepth = depth;
        }

        return clearDepth;
    }

    private static bool TryFindNearestDropDirection(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        int preferredDirection,
        out int direction)
    {
        var left = FindDropSearchCandidate(world, team, player, -1);
        var right = FindDropSearchCandidate(world, team, player, 1);
        if (!left.HasValue && !right.HasValue)
        {
            direction = 0;
            return false;
        }

        if (!left.HasValue)
        {
            direction = 1;
            return true;
        }

        if (!right.HasValue)
        {
            direction = -1;
            return true;
        }

        if (preferredDirection < 0
            && ShouldKeepPreferredDropDirection(left.Value, right.Value))
        {
            direction = -1;
            return true;
        }

        if (preferredDirection > 0
            && ShouldKeepPreferredDropDirection(right.Value, left.Value))
        {
            direction = 1;
            return true;
        }

        direction = right.Value.ClearRunDistance > left.Value.ClearRunDistance + DirectSeekDropProbeStep
            ? 1
            : left.Value.ClearRunDistance > right.Value.ClearRunDistance + DirectSeekDropProbeStep
            ? -1
            : right.Value.DropDistance <= left.Value.DropDistance ? 1 : -1;
        return true;
    }

    private static bool ShouldKeepPreferredDropDirection(DropSearchCandidate preferred, DropSearchCandidate alternate)
    {
        var alternateHasMeaningfullyMoreRunway = alternate.ClearRunDistance > preferred.ClearRunDistance + DirectSeekDropProbeStep;
        var alternateHasMeaningfullyShorterDrop = alternate.DropDistance + DirectSeekDropProbeStep < preferred.DropDistance;
        return !alternateHasMeaningfullyMoreRunway && !alternateHasMeaningfullyShorterDrop;
    }

    private static DropSearchCandidate? FindDropSearchCandidate(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        int direction)
    {
        var clearRunDistance = 0f;
        float? dropDistance = null;
        for (var distance = DirectSeekDropProbeStep; distance <= DirectSeekDropProbeMaxDistance; distance += DirectSeekDropProbeStep)
        {
            var offsetX = direction * distance;
            if (!IsHorizontallyClearProbe(world, team, player, offsetX))
            {
                break;
            }

            clearRunDistance = distance;
            if (distance >= DirectSeekDropProbeMinDistance
                && dropDistance is null
                && IsViableDropProbe(world, team, player, offsetX))
            {
                dropDistance = distance;
            }
        }

        return dropDistance.HasValue ? new DropSearchCandidate(dropDistance.Value, clearRunDistance) : null;
    }

    private static bool IsViableDropProbe(SimulationWorld world, PlayerTeam team, PlayerEntity player, float offsetX)
    {
        var probeX = player.X + offsetX;
        player.GetCollisionBoundsAt(probeX, player.Y, out var left, out var top, out var right, out var bottom);
        if (IsOutsideLevelBounds(world, left, top, right, bottom))
        {
            return false;
        }

        if (world.Level.IntersectsSolid(left, top, right, bottom))
        {
            return false;
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, player.IsCarryingIntel))
        {
            if (RectanglesOverlap(left, top, right, bottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                return false;
            }
        }

        return !world.Level.IntersectsSolid(
            left,
            bottom + 1f,
            right,
            bottom + DirectSeekDropProbeDepth);
    }

    private static bool IsHorizontallyClearProbe(SimulationWorld world, PlayerTeam team, PlayerEntity player, float offsetX)
    {
        var probeX = player.X + offsetX;
        player.GetCollisionBoundsAt(probeX, player.Y, out var left, out var top, out var right, out var bottom);
        if (IsOutsideLevelBounds(world, left, top, right, bottom))
        {
            return false;
        }

        if (world.Level.IntersectsSolid(left, top, right, bottom))
        {
            return false;
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, player.IsCarryingIntel))
        {
            if (RectanglesOverlap(left, top, right, bottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                return false;
            }
        }

        foreach (var wall in world.Level.RoomObjects)
        {
            if (wall.Type != RoomObjectType.PlayerWall)
            {
                continue;
            }

            if (RectanglesOverlap(left, top, right, bottom, wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldPrioritizeEnemyGoals(SimulationWorld world)
    {
        return world.ExperimentalGameplaySettings.EnablePracticeBotsPrioritizeKills
            || IsEnabled(Environment.GetEnvironmentVariable("OG_MOTION_PROOF_CHASE_ENEMIES"));
    }

    private static bool TrySelectEnemyGoal(SimulationWorld world, PlayerEntity player, out GraphGoal goal)
    {
        var bestDistance = MathF.Max(CombatSightDistance, GetCombatSeekCommitDistance(player.ClassId, CombatTargetKind.Player));
        byte bestSlot = 0;
        PlayerEntity? bestEnemy = null;
        foreach (var (slot, candidate) in world.EnumerateActiveNetworkPlayers())
        {
            if (candidate.Id == player.Id
                || !candidate.IsAlive
                || candidate.Team == player.Team)
            {
                continue;
            }

            var distance = Distance(player.X, player.Bottom, candidate.X, candidate.Bottom);
            if (distance >= bestDistance
                || !HasCombatLineOfSight(world, player, candidate)
                || !IsDirectlyContestableEnemyGoal(world, player, candidate))
            {
                continue;
            }

            bestDistance = distance;
            bestSlot = slot;
            bestEnemy = candidate;
        }

        if (bestEnemy is null)
        {
            goal = default;
            return false;
        }

        goal = new GraphGoal($"enemy_{bestSlot}", bestEnemy.X, bestEnemy.Bottom, GraphGoalRadius);
        return true;
    }

    private static bool IsDirectlyContestableEnemyGoal(
        SimulationWorld world,
        PlayerEntity player,
        PlayerEntity enemy)
    {
        return IsDirectlyContestableGoal(
            world,
            player.Team,
            player,
            enemy.X,
            enemy.Bottom,
            CombatSeekVerticalTolerance,
            CombatSeekDropTolerance,
            CombatSeekJumpHorizontalRadius);
    }

    private static bool IsDirectlyContestableGoal(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        float goalX,
        float goalBottom,
        float verticalTolerance,
        float dropTolerance,
        float jumpHorizontalRadius)
    {
        var deltaX = goalX - player.X;
        var deltaBottom = goalBottom - player.Bottom;
        if (MathF.Abs(deltaBottom) > verticalTolerance)
        {
            return false;
        }

        var direction = Math.Sign(deltaX);
        if (direction == 0)
        {
            return true;
        }

        var obstacleAhead = WouldMoveIntoObstacle(world, team, player, direction, CombatSeekObstacleProbeDistance);
        if (!obstacleAhead)
        {
            return true;
        }

        if (!player.IsGrounded)
        {
            return false;
        }

        var targetAbove = deltaBottom < -DirectSeekVerticalJumpThreshold;
        if (targetAbove
            && MathF.Abs(deltaX) <= jumpHorizontalRadius
            && HasJumpableVerticalAccessAhead(world, team, player, direction))
        {
            return true;
        }

        var targetBelow = deltaBottom > dropTolerance;
        return targetBelow
            && MathF.Abs(deltaX) <= 96f
            && TryFindNearestDropDirection(world, team, player, direction, out var dropDirection)
            && dropDirection == direction;
    }

    private static bool IsEnabled(string? value)
    {
        return value is not null
            && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetMovementProfileId(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Scout => "Scout",
            PlayerClass.Heavy => "Heavy",
            PlayerClass.Soldier or PlayerClass.Sniper => "Soldier",
            PlayerClass.Engineer or PlayerClass.Demoman => "Demoman",
            PlayerClass.Pyro or PlayerClass.Medic or PlayerClass.Spy => "Fast",
            PlayerClass.Quote => "Quote",
            _ => classId.ToString(),
        };
    }

    private static string BuildGraphGoalKey(GraphGoal goal)
    {
        var quantum = IsDynamicGraphGoal(goal.Label) ? DynamicGoalQuantization : StaticGoalQuantization;
        return $"{goal.Label}:{QuantizeGoalCoordinate(goal.X, quantum)}:{QuantizeGoalCoordinate(goal.Bottom, quantum)}";
    }

    private static string BuildDirectSeekGoalKey(GraphGoal goal)
    {
        return BuildGraphGoalKey(goal);
    }

    private static string BuildRouteMapKey(
        PreparedMotionGraph graph,
        PlayerTeam team,
        bool carryingIntel,
        float goalX,
        float goalBottom,
        float goalRadius,
        string goalLabel)
    {
        var quantum = IsDynamicGraphGoal(goalLabel) ? DynamicGoalQuantization : StaticGoalQuantization;
        return $"{graph.Identity}:{graph.Version}:{team}:{carryingIntel}:{QuantizeGoalCoordinate(goalX, quantum)}:{QuantizeGoalCoordinate(goalBottom, quantum)}:{QuantizeGoalCoordinate(goalRadius, StaticGoalQuantization)}";
    }

    private static bool IsDynamicGraphGoal(string? label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return false;
        }

        return (label.StartsWith("enemy_", StringComparison.OrdinalIgnoreCase)
                && !label.Equals("enemy_intel", StringComparison.OrdinalIgnoreCase))
            || label.StartsWith("ally_carrier", StringComparison.OrdinalIgnoreCase)
            || label.StartsWith("dropped_", StringComparison.OrdinalIgnoreCase)
            || label.Equals("override", StringComparison.OrdinalIgnoreCase);
    }

    private static int QuantizeGoalCoordinate(float value, float quantum)
    {
        return (int)MathF.Round(value / MathF.Max(1f, quantum));
    }

    private static ControlPointState? SelectKothTargetPoint(SimulationWorld world, PlayerTeam team)
    {
        if (world.MatchRules.Mode == GameModeKind.DoubleKingOfTheHill)
        {
            return world.ControlPoints.FirstOrDefault(point =>
                team == PlayerTeam.Blue
                    ? point.Marker.IsRedKothControlPoint()
                    : point.Marker.IsBlueKothControlPoint());
        }

        return world.ControlPoints.FirstOrDefault(point => point.Marker.IsSingleKothControlPoint())
            ?? (world.ControlPoints.Count > 0 ? world.ControlPoints[0] : null);
    }

    private static GraphReplayState AdvanceGraphReplayState(GraphReplayState state)
    {
        var nextTick = state.ActionTick + 1;
        if (nextTick < state.Actions[state.ActionIndex].Ticks)
        {
            return state with { ActionTick = nextTick };
        }

        return state with { ActionIndex = state.ActionIndex + 1, ActionTick = 0 };
    }

    private bool TryBuildGraphPlan(
        PreparedMotionGraph graph,
        PlayerTeam team,
        bool carryingIntel,
        PlayerEntity player,
        float startX,
        float startBottom,
        bool startGrounded,
        float startHorizontalSpeed,
        float startVerticalSpeed,
        float playerHeight,
        SimulationWorld world,
        float goalX,
        float goalBottom,
        float goalRadius,
        string goalLabel,
        out GraphPlan plan)
    {
        plan = default;
        var traversableNodes = BuildTraversableGraphNodeMask(graph, world, team, player);
        var startCandidates = FindStartGraphNodesWithinRadius(graph.Nodes, startX, startBottom, startGrounded, startHorizontalSpeed, startVerticalSpeed, GraphAttachRadius, 64)
            .Where(index => index >= 0 && index < traversableNodes.Length && traversableNodes[index])
            .ToArray();
        if (startCandidates.Length == 0)
        {
            return false;
        }

        var isDynamicGoal = IsDynamicGraphGoal(goalLabel);
        var goalCandidates = FindGraphNodesWithinRadius(graph.Nodes, goalX, goalBottom, goalRadius, 32)
            .Where(index => index >= 0 && index < traversableNodes.Length && traversableNodes[index])
            .ToArray();
        if (goalCandidates.Length == 0)
        {
            if (!isDynamicGoal)
            {
                return false;
            }

            goalCandidates = FindNearestGraphNodes(graph.Nodes, goalX, goalBottom, 4, preferStable: false)
                .Where(index => index >= 0 && index < traversableNodes.Length && traversableNodes[index])
                .ToArray();
        }

        if (isDynamicGoal)
        {
            var lineOfSightGoalCandidates = SelectDynamicGraphGoalCandidates(
                graph,
                world,
                team,
                carryingIntel,
                goalCandidates,
                goalX,
                goalBottom,
                playerHeight,
                requireLineOfSight: true,
                DynamicGraphLineOfSightCandidateCount);
            if (TrySelectGraphPlan(lineOfSightGoalCandidates, useCache: false, out plan))
            {
                return true;
            }
        }

        if (TrySelectGraphPlan(goalCandidates, useCache: !isDynamicGoal, out plan))
        {
            return true;
        }

        if (!isDynamicGoal)
        {
            return false;
        }

        var fallbackGoalCandidates = FindNearestGraphNodes(graph.Nodes, goalX, goalBottom, DynamicGraphGoalCandidateCount, preferStable: false)
            .Where(index => index >= 0 && index < traversableNodes.Length && traversableNodes[index])
            .ToArray();
        var lineOfSightFallbackCandidates = SelectDynamicGraphGoalCandidates(
            graph,
            world,
            team,
            carryingIntel,
            fallbackGoalCandidates,
            goalX,
            goalBottom,
            playerHeight,
            requireLineOfSight: true,
            DynamicGraphLineOfSightCandidateCount);
        if (TrySelectGraphPlan(lineOfSightFallbackCandidates, useCache: false, out plan))
        {
            return true;
        }

        fallbackGoalCandidates = SelectDynamicGraphGoalCandidates(
            graph,
            world,
            team,
            carryingIntel,
            fallbackGoalCandidates,
            goalX,
            goalBottom,
            playerHeight,
            requireLineOfSight: false,
            DynamicGraphLineOfSightCandidateCount);
        return TrySelectGraphPlan(fallbackGoalCandidates, useCache: false, out plan);

        bool TrySelectGraphPlan(int[] candidateGoalNodes, bool useCache, out GraphPlan selectedPlan)
        {
            selectedPlan = default;
            if (candidateGoalNodes.Length == 0)
            {
                return false;
            }

            var routeMap = useCache
                ? GetOrBuildRouteMap(graph, team, carryingIntel, candidateGoalNodes, goalX, goalBottom, goalRadius, goalLabel, traversableNodes)
                : BuildReverseRouteMap(
                    graph,
                    team,
                    carryingIntel,
                    candidateGoalNodes,
                    goalX,
                    goalBottom,
                    traversableNodes,
                    isDynamicGoal
                        ? BuildDynamicGraphGoalSeedCosts(graph, world, team, carryingIntel, candidateGoalNodes, goalX, goalBottom, playerHeight)
                        : null);
            var bestScore = float.MaxValue;
            var bestTicks = int.MaxValue;
            foreach (var startIndex in startCandidates)
            {
                if (!TryBuildGraphPlanFromRouteMap(graph, routeMap, startIndex, out var candidateActions, out var reachedGoalIndex, out var pathNodeIndices))
                {
                    continue;
                }

                if (!IsCtfTerminalGoal(goalLabel)
                    && !IsAdmissibleGraphStartCandidate(world, team, player, graph.Nodes[startIndex], candidateActions, goalX))
                {
                    continue;
                }

                var candidateNode = graph.Nodes[reachedGoalIndex];
                var candidateDistance = Distance(candidateNode.X, candidateNode.Bottom, goalX, goalBottom);
                var candidateTicks = routeMap.CostByNode[startIndex];
                var candidateLineOfSight = !isDynamicGoal
                    || HasLineOfSight(
                        world,
                        candidateNode.X,
                        candidateNode.Y,
                        goalX,
                        goalBottom - (playerHeight * 0.5f),
                        team,
                        carryingIntel);
                var candidateScore = candidateTicks
                    + (candidateDistance * (isDynamicGoal ? DynamicGraphGoalDistanceCostScale : 1f))
                    + (isDynamicGoal && !candidateLineOfSight ? DynamicGraphNoLineOfSightPenaltyTicks : 0);
                if (candidateScore > bestScore
                    || (MathF.Abs(candidateScore - bestScore) <= 0.1f && candidateTicks >= bestTicks))
                {
                    continue;
                }

                bestScore = candidateScore;
                bestTicks = candidateTicks;
                selectedPlan = new GraphPlan(candidateActions, startIndex, reachedGoalIndex, candidateNode.X, candidateNode.Bottom, pathNodeIndices);
            }

            return selectedPlan.Actions is { Count: > 0 };
        }
    }

    private static int[] SelectDynamicGraphGoalCandidates(
        PreparedMotionGraph graph,
        SimulationWorld world,
        PlayerTeam team,
        bool carryingIntel,
        int[] candidates,
        float goalX,
        float goalBottom,
        float playerHeight,
        bool requireLineOfSight,
        int maxCount)
    {
        if (candidates.Length == 0)
        {
            return Array.Empty<int>();
        }

        var goalCenterY = goalBottom - (playerHeight * 0.5f);
        return candidates
            .Distinct()
            .Select(index =>
            {
                var node = graph.Nodes[index];
                var hasLineOfSight = HasLineOfSight(world, node.X, node.Y, goalX, goalCenterY, team, carryingIntel);
                var distance = Distance(node.X, node.Bottom, goalX, goalBottom);
                return new
                {
                    Index = index,
                    Distance = distance,
                    HasLineOfSight = hasLineOfSight,
                    Score = distance
                        + (hasLineOfSight ? 0f : 10_000f)
                        + (node.IsGrounded ? 0f : 96f),
                };
            })
            .Where(entry => !requireLineOfSight || entry.HasLineOfSight)
            .OrderBy(entry => entry.Score)
            .ThenBy(entry => entry.Distance)
            .Take(maxCount)
            .Select(entry => entry.Index)
            .ToArray();
    }

    private static bool IsAdmissibleGraphStartCandidate(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        MotionGraphNode startNode,
        IReadOnlyList<MotionProofAction> actions,
        float goalX)
    {
        var attachDeltaX = startNode.X - player.X;
        var attachDeltaBottom = startNode.Bottom - player.Bottom;
        var attachDistance = Distance(player.X, player.Bottom, startNode.X, startNode.Bottom);
        if (player.IsGrounded
            && startNode.IsGrounded
            && MathF.Abs(attachDeltaBottom) > GraphGroundedAttachVerticalTolerance)
        {
            return false;
        }

        var openingActionIndex = FindFirstActiveGraphActionIndex(actions);
        if (openingActionIndex < 0)
        {
            return true;
        }

        var openingAction = actions[openingActionIndex];
        if (openingAction.Direction == 0)
        {
            return attachDistance <= GraphOpeningActionAttachDistance
                || MathF.Abs(attachDeltaX) <= GraphOpeningActionHorizontalTolerance;
        }

        var attachDirection = Math.Sign(attachDeltaX);
        if (attachDistance > GraphOpeningActionAttachDistance
            && attachDirection != 0
            && attachDirection != openingAction.Direction
            && MathF.Abs(attachDeltaX) > GraphOpeningActionHorizontalTolerance)
        {
            return false;
        }

        if (!WouldMoveIntoObstacle(world, team, player, openingAction.Direction, DirectSeekObstacleProbeDistance))
        {
            return true;
        }

        var reverseClear = !WouldMoveIntoObstacle(world, team, player, -openingAction.Direction, DirectSeekObstacleProbeDistance);
        if (reverseClear)
        {
            return false;
        }

        var goalDirection = Math.Sign(goalX - player.X);
        return goalDirection == 0 || goalDirection == openingAction.Direction;
    }

    private static int FindFirstActiveGraphActionIndex(IReadOnlyList<MotionProofAction> actions)
    {
        for (var index = 0; index < actions.Count; index += 1)
        {
            if (!IsInertTapeAction(actions[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static Dictionary<int, int> BuildDynamicGraphGoalSeedCosts(
        PreparedMotionGraph graph,
        SimulationWorld world,
        PlayerTeam team,
        bool carryingIntel,
        int[] goalCandidates,
        float goalX,
        float goalBottom,
        float playerHeight)
    {
        var goalCenterY = goalBottom - (playerHeight * 0.5f);
        var seedCosts = new Dictionary<int, int>();
        foreach (var goalIndex in goalCandidates.Distinct())
        {
            if (goalIndex < 0 || goalIndex >= graph.Nodes.Count)
            {
                continue;
            }

            var node = graph.Nodes[goalIndex];
            var distance = Distance(node.X, node.Bottom, goalX, goalBottom);
            var hasLineOfSight = HasLineOfSight(world, node.X, node.Y, goalX, goalCenterY, team, carryingIntel);
            var seedCost = (int)MathF.Round(distance * DynamicGraphGoalDistanceCostScale)
                + (hasLineOfSight ? 0 : DynamicGraphNoLineOfSightPenaltyTicks)
                + (node.IsGrounded ? 0 : 256);
            seedCosts[goalIndex] = Math.Max(0, seedCost);
        }

        return seedCosts;
    }

    private GraphRouteMap GetOrBuildRouteMap(
        PreparedMotionGraph graph,
        PlayerTeam team,
        bool carryingIntel,
        int[] goalCandidates,
        float goalX,
        float goalBottom,
        float goalRadius,
        string goalLabel,
        bool[] traversableNodes)
    {
        var key = BuildRouteMapKey(graph, team, carryingIntel, goalX, goalBottom, goalRadius, goalLabel);
        if (_routeMapsByKey.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_routeMapsByKey.Count >= MaxRouteMapCacheEntries)
        {
            _routeMapsByKey.Clear();
        }

        var routeMap = BuildReverseRouteMap(graph, team, carryingIntel, goalCandidates, goalX, goalBottom, traversableNodes, goalSeedCosts: null);
        _routeMapsByKey[key] = routeMap;
        return routeMap;
    }

    private static GraphRouteMap BuildReverseRouteMap(
        PreparedMotionGraph graph,
        PlayerTeam team,
        bool carryingIntel,
        int[] goalCandidates,
        float goalX,
        float goalBottom,
        bool[] traversableNodes,
        Dictionary<int, int>? goalSeedCosts)
    {
        var goalSet = goalCandidates.ToHashSet();
        var queue = new PriorityQueue<int, float>();
        var costByNode = new int[graph.Nodes.Count];
        var nextEdgeByNode = new MotionGraphEdge?[graph.Nodes.Count];
        var settled = new bool[graph.Nodes.Count];
        Array.Fill(costByNode, int.MaxValue);
        foreach (var goalIndex in goalCandidates)
        {
            if (goalIndex < 0 || goalIndex >= costByNode.Length)
            {
                continue;
            }

            if (goalIndex >= traversableNodes.Length || !traversableNodes[goalIndex])
            {
                continue;
            }

            var seedCost = goalSeedCosts is not null && goalSeedCosts.TryGetValue(goalIndex, out var seeded)
                ? Math.Max(0, seeded)
                : 0;
            costByNode[goalIndex] = seedCost;
            queue.Enqueue(goalIndex, seedCost);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (settled[current])
            {
                continue;
            }

            settled[current] = true;

            if (!graph.ReverseEdgesByNode.TryGetValue(current, out var incomingEdges))
            {
                continue;
            }

            var currentCost = costByNode[current];
            foreach (var edge in incomingEdges)
            {
                if (!IsEdgeAllowedForTeam(edge, team) || !IsEdgeAllowedForCarry(edge, carryingIntel))
                {
                    continue;
                }

                if (edge.From < 0
                    || edge.From >= traversableNodes.Length
                    || !traversableNodes[edge.From]
                    || current >= traversableNodes.Length
                    || !traversableNodes[current])
                {
                    continue;
                }

                var nextCost = currentCost + edge.CostTicks;
                if (nextCost >= costByNode[edge.From])
                {
                    continue;
                }

                costByNode[edge.From] = nextCost;
                nextEdgeByNode[edge.From] = edge;
                queue.Enqueue(edge.From, nextCost);
            }
        }

        return new GraphRouteMap(goalSet, costByNode, nextEdgeByNode);
    }

    private static bool[] BuildTraversableGraphNodeMask(
        PreparedMotionGraph graph,
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player)
    {
        var traversable = new bool[graph.Nodes.Count];
        for (var index = 0; index < graph.Nodes.Count; index += 1)
        {
            var node = graph.Nodes[index];
            traversable[index] = IsCollisionClearAt(world, team, player, node.X, node.Y);
        }

        return traversable;
    }

    private bool TryBuildDynamicGraphWaypointInput(
        byte slot,
        SimulationWorld world,
        PlayerTeam team,
        PreparedMotionGraph graph,
        GraphReplayState state,
        PlayerEntity player,
        GraphGoal finalGoal,
        out PlayerInputSnapshot input,
        out GraphReplayState updatedState)
    {
        input = default;
        updatedState = state;
        if (state.PathNodeIndices.Count < 2)
        {
            return false;
        }

        var actionIndex = Math.Clamp(state.ActionIndex, 0, state.PathNodeIndices.Count - 1);
        while (actionIndex < state.PathNodeIndices.Count - 1)
        {
            var node = graph.Nodes[state.PathNodeIndices[actionIndex + 1]];
            if (Distance(player.X, player.Bottom, node.X, node.Bottom) > 72f)
            {
                break;
            }

            actionIndex += 1;
        }

        var lookaheadIndex = Math.Min(state.PathNodeIndices.Count - 1, actionIndex + 3);
        for (var candidateIndex = lookaheadIndex; candidateIndex > actionIndex + 1; candidateIndex -= 1)
        {
            var candidate = graph.Nodes[state.PathNodeIndices[candidateIndex]];
            if (Distance(player.X, player.Bottom, candidate.X, candidate.Bottom) <= 220f
                && HasLineOfSight(world, player.X, player.Y, candidate.X, candidate.Y, team, player.IsCarryingIntel))
            {
                lookaheadIndex = candidateIndex;
                break;
            }
        }

        var waypointNode = graph.Nodes[state.PathNodeIndices[lookaheadIndex]];
        if (lookaheadIndex >= state.PathNodeIndices.Count - 1
            && HasLineOfSight(
                world,
                player.X,
                player.Y,
                finalGoal.X,
                finalGoal.Bottom - (player.Height * 0.5f),
                team,
                player.IsCarryingIntel))
        {
            input = BuildDirectSeekInput(slot, world, team, player, finalGoal);
            updatedState = state with { ActionIndex = actionIndex };
            return true;
        }

        var waypointGoal = new GraphGoal(
            $"{finalGoal.Label}_waypoint",
            waypointNode.X,
            waypointNode.Bottom,
            MathF.Min(finalGoal.Radius, 96f));
        input = BuildDirectSeekInput(slot, world, team, player, waypointGoal);
        updatedState = state with { ActionIndex = actionIndex };
        return true;
    }

    private static bool TryBuildGraphPlanFromRouteMap(
        PreparedMotionGraph graph,
        GraphRouteMap routeMap,
        int startIndex,
        out IReadOnlyList<MotionProofAction> actions,
        out int reachedGoalIndex,
        out IReadOnlyList<int> pathNodeIndices)
    {
        actions = Array.Empty<MotionProofAction>();
        reachedGoalIndex = -1;
        pathNodeIndices = Array.Empty<int>();
        if (startIndex < 0
            || startIndex >= routeMap.CostByNode.Length
            || routeMap.CostByNode[startIndex] == int.MaxValue)
        {
            return false;
        }

        var planned = new List<MotionProofAction>();
        var pathNodes = new List<int> { startIndex };
        var pathNode = startIndex;
        for (var steps = 0; steps < MaxGraphRouteActions; steps += 1)
        {
            if (routeMap.GoalNodes.Contains(pathNode))
            {
                actions = planned;
                reachedGoalIndex = pathNode;
                pathNodeIndices = pathNodes;
                return actions.Count > 0;
            }

            var edge = routeMap.NextEdgeByNode[pathNode];
            if (edge is null)
            {
                return false;
            }

            planned.Add(edge.Action);
            pathNode = edge.To;
            pathNodes.Add(pathNode);
        }

        return false;
    }

    private static bool IsEdgeAllowedForTeam(MotionGraphEdge edge, PlayerTeam team)
    {
        return edge.TeamMask.Equals("Any", StringComparison.OrdinalIgnoreCase)
            || edge.TeamMask.Equals(team.ToString(), StringComparison.OrdinalIgnoreCase)
            || edge.TeamMask.Equals("Both", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEdgeAllowedForCarry(MotionGraphEdge edge, bool carryingIntel)
    {
        return edge.CarryMask.Equals("Any", StringComparison.OrdinalIgnoreCase)
            || edge.CarryMask.Equals(carryingIntel ? "Carrying" : "Free", StringComparison.OrdinalIgnoreCase)
            || edge.CarryMask.Equals("Both", StringComparison.OrdinalIgnoreCase);
    }

    private static int FindNearestGraphNode(IReadOnlyList<MotionGraphNode> nodes, float x, float bottom)
    {
        var bestIndex = 0;
        var bestDistance = float.MaxValue;
        for (var index = 0; index < nodes.Count; index += 1)
        {
            var node = nodes[index];
            var distance = Distance(node.X, node.Bottom, x, bottom);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static int[] FindNearestGraphNodes(
        IReadOnlyList<MotionGraphNode> nodes,
        float x,
        float bottom,
        int count,
        bool preferStable = false)
    {
        return nodes
            .Select((node, index) => new
            {
                Index = index,
                Distance = Distance(node.X, node.Bottom, x, bottom),
                Score = Distance(node.X, node.Bottom, x, bottom) + (preferStable && !IsStableGraphNode(node) ? 10000f : 0f),
            })
            .OrderBy(entry => entry.Score)
            .ThenBy(entry => entry.Distance)
            .Take(Math.Min(count, nodes.Count))
            .Select(entry => entry.Index)
            .ToArray();
    }

    private static int[] FindGraphNodesWithinRadius(
        IReadOnlyList<MotionGraphNode> nodes,
        float x,
        float bottom,
        float radius,
        int maxCount,
        bool preferStable = false)
    {
        return nodes
            .Select((node, index) => new
            {
                Index = index,
                Distance = Distance(node.X, node.Bottom, x, bottom),
                Score = Distance(node.X, node.Bottom, x, bottom) + (preferStable && !IsStableGraphNode(node) ? radius : 0f),
            })
            .Where(entry => entry.Distance <= radius)
            .OrderBy(entry => entry.Score)
            .ThenBy(entry => entry.Distance)
            .Take(Math.Min(maxCount, nodes.Count))
            .Select(entry => entry.Index)
            .ToArray();
    }

    private static int[] FindStartGraphNodesWithinRadius(
        IReadOnlyList<MotionGraphNode> nodes,
        float x,
        float bottom,
        bool grounded,
        float horizontalSpeed,
        float verticalSpeed,
        float radius,
        int maxCount)
    {
        var entries = nodes
            .Select((node, index) =>
            {
                var distance = Distance(node.X, node.Bottom, x, bottom);
                var horizontalMismatch = MathF.Abs(node.HorizontalSpeed - horizontalSpeed);
                var verticalMismatch = MathF.Abs(node.VerticalSpeed - verticalSpeed);
                var speedMismatch = horizontalMismatch + verticalMismatch;
                var stationaryPenalty = MathF.Abs(horizontalSpeed) <= 5f && MathF.Abs(verticalSpeed) <= 5f && speedMismatch > 20f
                    ? 128f
                    : 0f;
                var groundedPenalty = node.IsGrounded == grounded ? 0f : 32f;
                var groundedVerticalPenalty = grounded && node.IsGrounded
                    ? MathF.Max(0f, MathF.Abs(node.Bottom - bottom) - GraphOpeningActionHorizontalTolerance)
                    : 0f;
                return new
                {
                    Index = index,
                    Distance = distance,
                    Score = distance + speedMismatch + stationaryPenalty + groundedPenalty + groundedVerticalPenalty,
                };
            })
            .Where(entry => entry.Distance <= radius)
            .ToArray();
        if (MathF.Abs(horizontalSpeed) <= 5f && MathF.Abs(verticalSpeed) <= 5f)
        {
            var restAnchors = entries
                .Where(entry => MathF.Abs(nodes[entry.Index].HorizontalSpeed) <= 5f && MathF.Abs(nodes[entry.Index].VerticalSpeed) <= 5f)
                .ToArray();
            if (restAnchors.Length > 0)
            {
                entries = restAnchors;
            }
        }

        return entries
            .OrderBy(entry => entry.Score)
            .ThenBy(entry => entry.Distance)
            .Take(Math.Min(maxCount, nodes.Count))
            .Select(entry => entry.Index)
            .ToArray();
    }

    private static bool IsStableGraphNode(MotionGraphNode node)
    {
        return node.IsGrounded
            && MathF.Abs(node.HorizontalSpeed) <= 5f
            && MathF.Abs(node.VerticalSpeed) <= 5f;
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static T? ReadJsonArtifact<T>(string path)
    {
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            using var file = File.OpenRead(path);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            return JsonSerializer.Deserialize<T>(gzip, JsonOptions);
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions);
    }

    private ReplayState GetReplayState(
        byte slot,
        SimulationWorld world,
        ControlledBotSlot controlledSlot,
        PreparedMotionProofArtifact artifact,
        ReplayPhase phase)
    {
        var mapKey = $"{world.Level.Name}.a{world.Level.MapAreaIndex}.{controlledSlot.Team}.{controlledSlot.ClassId}.{artifact.Artifact.Version}";
        if (_statesBySlot.TryGetValue(slot, out var state) && state.MapKey == mapKey)
        {
            return state;
        }

        return new ReplayState(
            mapKey,
            phase,
            ActionIndex: 0,
            ActionTick: 0,
            LastX: float.NaN,
            LastBottom: float.NaN,
            NoProgressTicks: 0,
            NoHorizontalProgressTicks: 0,
            BestGoalDistance: float.NaN,
            NoGoalProgressTicks: 0,
            ReattachResumeActionIndex: -1,
            ActiveReattachPoint: null,
            SpawnExitPrefixResumeActionIndex: -1,
            ImplicitPhaseStartX: float.NaN,
            ImplicitPhaseStartBottom: float.NaN,
            GraphRecoveryActive: false,
            PendingForwardReattach: false);
    }

    private static ReplayPhase SelectPhase(SimulationWorld world, PlayerEntity player)
    {
        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag && player.IsCarryingIntel)
        {
            return ReplayPhase.Return;
        }

        return ReplayPhase.Attack;
    }

    private static ReplayState AdvanceReplayState(ReplayState state, IReadOnlyList<MotionProofAction> tape)
    {
        if (state.ActionIndex >= tape.Count)
        {
            return state;
        }

        var nextTick = state.ActionTick + 1;
        if (nextTick < tape[state.ActionIndex].Ticks)
        {
            return state with { ActionTick = nextTick };
        }

        return state with { ActionIndex = state.ActionIndex + 1, ActionTick = 0 };
    }

    private static bool ShouldStartTapeReattach(PlayerEntity player, TapePhaseData phaseData, ReplayState state, bool spawnReattachEligible)
    {
        if (state.ActionIndex != 0 || state.ActionTick != 0)
        {
            return false;
        }

        if (IsNearTapePhaseStartForSpawnReplay(player, phaseData))
        {
            return false;
        }

        return !player.IsInSpawnRoom || !spawnReattachEligible;
    }

    private static bool IsFarFromTapePhaseStart(PlayerEntity player, TapePhaseData phaseData)
    {
        return MathF.Abs(player.X - phaseData.StartX) > TapeReattachStartRadiusX
            || MathF.Abs(player.Bottom - phaseData.StartBottom) > TapeReattachStartRadiusBottom;
    }

    private static bool IsNearTapePhaseStartForSpawnReplay(PlayerEntity player, TapePhaseData phaseData)
    {
        if (!player.IsInSpawnRoom)
        {
            return !IsFarFromTapePhaseStart(player, phaseData);
        }

        return MathF.Abs(player.X - phaseData.StartX) <= SpawnReplayWindowRadiusX
            && MathF.Abs(player.Bottom - phaseData.StartBottom) <= TapeReattachWindowRadiusBottom;
    }

    private static bool ShouldStartTapeRecoveryReattach(ReplayState state)
    {
        return false;
    }

    private void ArmObjectiveSeamReplayRecovery(byte slot, PlayerEntity player)
    {
        if (!_statesBySlot.TryGetValue(slot, out var replayState))
        {
            return;
        }

        _statesBySlot[slot] = replayState with
        {
            LastX = player.X,
            LastBottom = player.Bottom,
            NoProgressTicks = 0,
            NoHorizontalProgressTicks = 0,
            BestGoalDistance = float.NaN,
            NoGoalProgressTicks = 0,
            ReattachResumeActionIndex = -1,
            ActiveReattachPoint = null,
            SpawnExitPrefixResumeActionIndex = -1,
            GraphRecoveryActive = false,
            PendingForwardReattach = true,
        };
    }

    private static bool TryFindNearestTapeReattachPoint(TapePhaseData phaseData, PlayerEntity player, out TapeReattachPoint point)
    {
        point = default;
        if (player.IsInSpawnRoom && TryFindNearestTapeReattachPoint(phaseData.ReattachPoints, player, requireOutsideSpawnRoom: true, out point))
        {
            return true;
        }

        return TryFindNearestTapeReattachPoint(phaseData.ReattachPoints, player, requireOutsideSpawnRoom: false, out point);
    }

    private static bool TryFindLocalTapeDriftReattachPoint(
        TapePhaseData phaseData,
        PlayerEntity player,
        int currentActionIndex,
        out TapeReattachPoint point)
    {
        point = default;
        if (currentActionIndex <= 0)
        {
            return false;
        }

        var bestDistance = float.MaxValue;
        var hasPoint = false;
        foreach (var candidate in phaseData.ReattachPoints)
        {
            if (candidate.ResumeActionIndex >= currentActionIndex)
            {
                continue;
            }

            var distance = Distance(player.X, player.Bottom, candidate.X, candidate.Bottom);
            if (distance > TapeReattachLocalDriftDistance
                || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            point = candidate;
            hasPoint = true;
        }

        return hasPoint;
    }

    private static bool TryFindTapePhaseStartReattachPoint(
        ReplayPhase phase,
        TapePhaseData phaseData,
        PlayerEntity player,
        GraphGoal? objectiveGoal,
        float objectiveDistance,
        out TapeReattachPoint point)
    {
        if (phase == ReplayPhase.Return
            && !float.IsNaN(phaseData.StartX)
            && !float.IsNaN(phaseData.StartBottom)
            && IsFarFromTapePhaseStart(player, phaseData))
        {
            point = new TapeReattachPoint(
                ResumeActionIndex: 0,
                X: phaseData.StartX,
                Bottom: phaseData.StartBottom,
                RadiusX: TapeReattachStartRadiusX,
                RadiusBottom: TapeReattachStartRadiusBottom,
                IsInSpawnRoom: false,
                IsGrounded: true,
                HorizontalSpeed: 0f,
                VerticalSpeed: 0f,
                UseLiveBottom: false);
            return true;
        }

        if (TryFindEarliestSpawnExitTapeReattachPoint(phaseData.ReattachPoints, player, out point))
        {
            return true;
        }

        if (objectiveGoal is { } phaseGoal
            && TryFindNearestObjectiveTapeReattachPoint(
                phaseData.ReattachPoints,
                player,
                requireOutsideSpawnRoom: player.IsInSpawnRoom,
                currentActionIndex: -1,
                objectiveGoal: phaseGoal,
                currentObjectiveDistance: objectiveDistance,
                out point))
        {
            return true;
        }

        if (TryFindNearestTapeReattachPoint(phaseData, player, out point))
        {
            return true;
        }

        if (player.IsInSpawnRoom
            && !HasOutsideSpawnRoomReattachPoint(phaseData.ReattachPoints)
            && !float.IsNaN(phaseData.StartX)
            && !float.IsNaN(phaseData.StartBottom))
        {
            point = new TapeReattachPoint(
                ResumeActionIndex: 0,
                X: phaseData.StartX,
                Bottom: phaseData.StartBottom,
                RadiusX: TapeReattachStartRadiusX,
                RadiusBottom: TapeReattachStartRadiusBottom,
                IsInSpawnRoom: true,
                IsGrounded: true,
                HorizontalSpeed: 0f,
                VerticalSpeed: 0f,
                UseLiveBottom: false);
            return true;
        }

        return false;
    }

    private static bool TryFindEarliestSpawnExitTapeReattachPoint(
        IReadOnlyList<TapeReattachPoint> points,
        PlayerEntity player,
        out TapeReattachPoint point)
    {
        point = default;
        if (!player.IsInSpawnRoom)
        {
            return false;
        }

        foreach (var candidate in points)
        {
            if (candidate.IsInSpawnRoom
                || !IsStableTapeReattachPoint(candidate)
                || Distance(player.X, player.Bottom, candidate.X, candidate.Bottom) > TapeReattachSpawnSearchDistance)
            {
                continue;
            }

            point = candidate;
            return true;
        }

        return false;
    }

    private static bool TryFindFirstOutsideSpawnTapeReattachPoint(
        IReadOnlyList<TapeReattachPoint> points,
        out TapeReattachPoint point)
    {
        foreach (var candidate in points)
        {
            if (!candidate.IsInSpawnRoom)
            {
                point = candidate;
                return true;
            }
        }

        point = default;
        return false;
    }

    private static bool TryFindSpawnExitTapePrefixTarget(
        IReadOnlyList<TapeReattachPoint> points,
        PlayerEntity player,
        GraphGoal? objectiveGoal,
        float objectiveDistance,
        out TapeReattachPoint point)
    {
        if (objectiveGoal is { } phaseGoal
            && TryFindNearestObjectiveTapeReattachPoint(
                points,
                player,
                requireOutsideSpawnRoom: true,
                currentActionIndex: -1,
                objectiveGoal: phaseGoal,
                currentObjectiveDistance: objectiveDistance,
                out point))
        {
            return true;
        }

        return TryFindFirstOutsideSpawnTapeReattachPoint(points, out point);
    }

    private static bool TryFindNearbyTapePhaseStartAlignmentPoint(
        TapePhaseData phaseData,
        PlayerEntity player,
        bool isWithinSpawnReplayWindow,
        out TapeReattachPoint point)
    {
        point = default;
        if (!isWithinSpawnReplayWindow
            || float.IsNaN(phaseData.StartX)
            || float.IsNaN(phaseData.StartBottom))
        {
            return false;
        }

        var distanceToStart = Distance(player.X, player.Bottom, phaseData.StartX, phaseData.StartBottom);
        if (distanceToStart > TapeReattachSpawnStartSeekDistance
            || MathF.Abs(player.Bottom - phaseData.StartBottom) > TapeReattachWindowRadiusBottom)
        {
            return false;
        }

        point = new TapeReattachPoint(
            ResumeActionIndex: 0,
            X: phaseData.StartX,
            Bottom: player.Bottom,
            RadiusX: TapeStartAlignmentReadyRadiusX,
            RadiusBottom: TapeReattachStartRadiusBottom,
            IsInSpawnRoom: true,
            IsGrounded: true,
            HorizontalSpeed: 0f,
            VerticalSpeed: 0f,
            UseLiveBottom: true);
        return true;
    }

    private static bool IsWithinTeamSpawnReplayWindow(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player)
    {
        var spawns = team == PlayerTeam.Blue ? world.Level.BlueSpawns : world.Level.RedSpawns;
        var playerFeetY = player.Bottom;
        foreach (var spawn in spawns)
        {
            var spawnFeetY = spawn.Y + player.CollisionBottomOffset;
            if (MathF.Abs(player.X - spawn.X) <= SpawnReplayWindowRadiusX
                && MathF.Abs(playerFeetY - spawnFeetY) <= SpawnReplayWindowRadiusBottom)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveTapeReattachPoint(
        TapePhaseData phaseData,
        int resumeActionIndex,
        out TapeReattachPoint point)
    {
        if (resumeActionIndex == 0
            && !float.IsNaN(phaseData.StartX)
            && !float.IsNaN(phaseData.StartBottom))
        {
            point = new TapeReattachPoint(
                ResumeActionIndex: 0,
                X: phaseData.StartX,
                Bottom: phaseData.StartBottom,
                RadiusX: TapeReattachStartRadiusX,
                RadiusBottom: TapeReattachStartRadiusBottom,
                IsInSpawnRoom: true,
                IsGrounded: true,
                HorizontalSpeed: 0f,
                VerticalSpeed: 0f,
                UseLiveBottom: false);
            return true;
        }

        return phaseData.ReattachPointsByResumeIndex.TryGetValue(resumeActionIndex, out point);
    }

    private static bool HasOutsideSpawnRoomReattachPoint(IReadOnlyList<TapeReattachPoint> points)
    {
        foreach (var point in points)
        {
            if (!point.IsInSpawnRoom)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindForwardTapeReattachPoint(
        ReplayPhase phase,
        IReadOnlyList<TapeReattachPoint> points,
        PlayerEntity player,
        int currentActionIndex,
        GraphGoal? objectiveGoal,
        float objectiveDistance,
        out TapeReattachPoint point)
    {
        if (phase == ReplayPhase.Return
            && objectiveGoal is { } returnGoal
            && TryFindBestObjectiveTapeReattachPoint(
                points,
                player,
                requireOutsideSpawnRoom: false,
                currentActionIndex,
                returnGoal,
                objectiveDistance,
                out point))
        {
            return true;
        }

        point = default;
        var bestDistance = float.MaxValue;
        var hasPoint = false;
        foreach (var candidate in points)
        {
            if (candidate.ResumeActionIndex <= currentActionIndex)
            {
                continue;
            }

            var distance = Distance(player.X, player.Bottom, candidate.X, candidate.Bottom);
            if (distance > TapeReattachSearchDistance
                || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            point = candidate;
            hasPoint = true;
        }

        return hasPoint;
    }

    private static bool TryFindBestObjectiveTapeReattachPoint(
        IReadOnlyList<TapeReattachPoint> points,
        PlayerEntity player,
        bool requireOutsideSpawnRoom,
        int currentActionIndex,
        GraphGoal objectiveGoal,
        float currentObjectiveDistance,
        out TapeReattachPoint point)
    {
        point = default;
        var bestScore = float.MaxValue;
        var hasPoint = false;
        var distanceLimit = requireOutsideSpawnRoom && currentActionIndex < 0
            ? TapeReattachSpawnSearchDistance
            : TapeReattachSearchDistance;
        var requireStablePoint = requireOutsideSpawnRoom && currentActionIndex < 0;
        foreach (var candidate in points)
        {
            if (requireOutsideSpawnRoom && candidate.IsInSpawnRoom)
            {
                continue;
            }

            if (requireStablePoint && !IsStableTapeReattachPoint(candidate))
            {
                continue;
            }

            if (currentActionIndex >= 0 && candidate.ResumeActionIndex <= currentActionIndex)
            {
                continue;
            }

            var distance = Distance(player.X, player.Bottom, candidate.X, candidate.Bottom);
            if (distance > distanceLimit)
            {
                continue;
            }

            var candidateGoalDistance = Distance(candidate.X, candidate.Bottom, objectiveGoal.X, objectiveGoal.Bottom);
            if (!float.IsNaN(currentObjectiveDistance)
                && candidateGoalDistance > currentObjectiveDistance + 64f)
            {
                continue;
            }

            var score = candidateGoalDistance * 6f + distance;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            point = candidate;
            hasPoint = true;
        }

        if (!hasPoint && requireStablePoint)
        {
            return TryFindBestObjectiveTapeReattachPoint(
                points,
                player,
                requireOutsideSpawnRoom,
                currentActionIndex: 0,
                objectiveGoal,
                currentObjectiveDistance,
                out point);
        }

        return hasPoint;
    }

    private static bool TryFindNearestObjectiveTapeReattachPoint(
        IReadOnlyList<TapeReattachPoint> points,
        PlayerEntity player,
        bool requireOutsideSpawnRoom,
        int currentActionIndex,
        GraphGoal objectiveGoal,
        float currentObjectiveDistance,
        out TapeReattachPoint point)
    {
        point = default;
        var bestDistance = float.MaxValue;
        var hasPoint = false;
        var distanceLimit = requireOutsideSpawnRoom && currentActionIndex < 0
            ? TapeReattachSpawnSearchDistance
            : TapeReattachSearchDistance;
        var requireStablePoint = requireOutsideSpawnRoom && currentActionIndex < 0;
        foreach (var candidate in points)
        {
            if (requireOutsideSpawnRoom && candidate.IsInSpawnRoom)
            {
                continue;
            }

            if (requireStablePoint && !IsStableTapeReattachPoint(candidate))
            {
                continue;
            }

            if (currentActionIndex >= 0 && candidate.ResumeActionIndex <= currentActionIndex)
            {
                continue;
            }

            var distance = Distance(player.X, player.Bottom, candidate.X, candidate.Bottom);
            if (distance > distanceLimit || distance >= bestDistance)
            {
                continue;
            }

            var candidateGoalDistance = Distance(candidate.X, candidate.Bottom, objectiveGoal.X, objectiveGoal.Bottom);
            if (!float.IsNaN(currentObjectiveDistance)
                && candidateGoalDistance > currentObjectiveDistance + 64f)
            {
                continue;
            }

            bestDistance = distance;
            point = candidate;
            hasPoint = true;
        }

        if (!hasPoint && requireStablePoint)
        {
            return TryFindNearestObjectiveTapeReattachPoint(
                points,
                player,
                requireOutsideSpawnRoom,
                currentActionIndex: 0,
                objectiveGoal,
                currentObjectiveDistance,
                out point);
        }

        return hasPoint;
    }

    private static bool IsStableTapeReattachPoint(TapeReattachPoint point)
    {
        return point.IsGrounded
            && MathF.Abs(point.HorizontalSpeed) <= TapeReattachStableHorizontalSpeed
            && MathF.Abs(point.VerticalSpeed) <= TapeReattachStableVerticalSpeed;
    }

    private static bool TryFindNearestTapeReattachPoint(
        IReadOnlyList<TapeReattachPoint> points,
        PlayerEntity player,
        bool requireOutsideSpawnRoom,
        out TapeReattachPoint point)
    {
        point = default;
        var bestDistance = float.MaxValue;
        var hasPoint = false;
        foreach (var candidate in points)
        {
            if (requireOutsideSpawnRoom && candidate.IsInSpawnRoom)
            {
                continue;
            }

            var distance = Distance(player.X, player.Bottom, candidate.X, candidate.Bottom);
            if (distance > TapeReattachSearchDistance
                || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            point = candidate;
            hasPoint = true;
        }

        return hasPoint;
    }

    private static bool IsWithinTapeReattachWindow(PlayerEntity player, TapeReattachPoint point)
    {
        return MathF.Abs(player.X - point.X) <= point.RadiusX
            && MathF.Abs(player.Bottom - point.Bottom) <= point.RadiusBottom;
    }

    private static bool IsReadyToResumeTapeAtReattachPoint(PlayerEntity player, TapeReattachPoint point)
    {
        if (!IsWithinTapeReattachWindow(player, point))
        {
            return false;
        }

        if (point.UseLiveBottom)
        {
            return player.IsGrounded == point.IsGrounded
                && MathF.Abs(player.X - point.X) <= TapeStartAlignmentReadyRadiusX
                && MathF.Abs(player.HorizontalSpeed - point.HorizontalSpeed) <= TapeStartAlignmentStableHorizontalSpeed
                && MathF.Abs(player.VerticalSpeed - point.VerticalSpeed) <= TapeStartAlignmentStableVerticalSpeed;
        }

        if (!IsStableTapeReattachPoint(point))
        {
            return true;
        }

        return player.IsGrounded == point.IsGrounded
            && MathF.Abs(player.HorizontalSpeed - point.HorizontalSpeed) <= TapeReattachStableHorizontalSpeed
            && MathF.Abs(player.VerticalSpeed - point.VerticalSpeed) <= TapeReattachStableVerticalSpeed;
    }

    private static bool IsReadyToStartTapeFromNearbySpawnOffset(PlayerEntity player)
    {
        return MathF.Abs(player.HorizontalSpeed) <= TapeStartAlignmentStableHorizontalSpeed
            && MathF.Abs(player.VerticalSpeed) <= TapeStartAlignmentStableVerticalSpeed;
    }

    private static bool TryBuildImplicitReturnStartWaitInput(
        SimulationWorld world,
        ReplayPhase phase,
        TapePhaseData phaseData,
        ReplayState state,
        PlayerEntity player,
        out PlayerInputSnapshot input,
        out GraphGoal diagnosticGoal)
    {
        input = default;
        diagnosticGoal = default;
        if (phase != ReplayPhase.Return
            || !float.IsNaN(phaseData.StartX)
            || state.ActionIndex != 0
            || state.ActionTick != 0
            || float.IsNaN(state.ImplicitPhaseStartX)
            || float.IsNaN(state.ImplicitPhaseStartBottom))
        {
            return false;
        }

        if (player.IsGrounded
            && MathF.Abs(player.HorizontalSpeed) <= TapeStartAlignmentStableHorizontalSpeed
            && MathF.Abs(player.VerticalSpeed) <= TapeStartAlignmentStableVerticalSpeed)
        {
            return false;
        }

        diagnosticGoal = new GraphGoal(
            "tape_return_start_wait",
            state.ImplicitPhaseStartX,
            state.ImplicitPhaseStartBottom,
            TapeReattachStartRadiusX);
        input = BuildIdleInput(world, player);
        return true;
    }

    private static ReplayState TrackReplayProgress(ReplayState state, PlayerEntity player, float objectiveDistance)
    {
        var goalState = state;
        if (!float.IsNaN(objectiveDistance))
        {
            if (float.IsNaN(state.BestGoalDistance) || objectiveDistance + 8f < state.BestGoalDistance)
            {
                goalState = state with { BestGoalDistance = objectiveDistance, NoGoalProgressTicks = 0 };
            }
            else
            {
                goalState = state with { NoGoalProgressTicks = state.NoGoalProgressTicks + 1 };
            }
        }

        if (float.IsNaN(state.LastX) || float.IsNaN(state.LastBottom))
        {
            return goalState with { LastX = player.X, LastBottom = player.Bottom, NoProgressTicks = 0, NoHorizontalProgressTicks = 0 };
        }

        var moved = Distance(player.X, player.Bottom, state.LastX, state.LastBottom);
        var movedHorizontally = MathF.Abs(player.X - state.LastX);
        return goalState with
        {
            NoProgressTicks = moved >= NoProgressDistance ? 0 : state.NoProgressTicks + 1,
            NoHorizontalProgressTicks = movedHorizontally >= NoProgressDistance ? 0 : state.NoHorizontalProgressTicks + 1,
        };
    }

    private static void LogObjectiveSeamEvent(byte slot, PlayerEntity player, ObjectiveSeamProgram program, string message)
    {
        if (!IsEnabled(Environment.GetEnvironmentVariable("OG_BOT_DIAGNOSTICS")))
        {
            return;
        }

        Console.WriteLine(
            $"objective-seam slot={slot} label={program.Label} event={message} pos=({player.X:0.0},{player.Bottom:0.0}) vel=({player.HorizontalSpeed:0.0},{player.VerticalSpeed:0.0}) grounded={player.IsGrounded} facing={player.FacingDirectionX:0.0}");
    }

    private static GraphReplayState TrackGraphProgress(GraphReplayState state, PlayerEntity player)
    {
        if (float.IsNaN(state.LastX) || float.IsNaN(state.LastBottom))
        {
            return state with { LastX = player.X, LastBottom = player.Bottom, NoProgressTicks = 0, NoHorizontalProgressTicks = 0 };
        }

        var moved = Distance(player.X, player.Bottom, state.LastX, state.LastBottom);
        var movedHorizontally = MathF.Abs(player.X - state.LastX);
        return state with
        {
            NoProgressTicks = moved >= NoProgressDistance ? 0 : state.NoProgressTicks + 1,
            NoHorizontalProgressTicks = movedHorizontally >= NoProgressDistance ? 0 : state.NoHorizontalProgressTicks + 1,
        };
    }

    private static DirectSeekState TrackDirectSeekProgress(DirectSeekState state, PlayerEntity player, float currentGoalDistance)
    {
        var movedHorizontally = MathF.Abs(player.X - state.LastX);
        var improvedGoalDistance = currentGoalDistance + 8f < state.BestGoalDistance;
        var madeMeaningfulProgress = movedHorizontally >= NoProgressDistance || improvedGoalDistance;
        return state with
        {
            NoProgressTicks = madeMeaningfulProgress ? 0 : state.NoProgressTicks + 1,
            NoHorizontalProgressTicks = movedHorizontally >= NoProgressDistance ? 0 : state.NoHorizontalProgressTicks + 1,
            BestGoalDistance = improvedGoalDistance ? currentGoalDistance : state.BestGoalDistance,
            NoGoalProgressTicks = improvedGoalDistance ? 0 : state.NoGoalProgressTicks + 1,
        };
    }

    private bool ShouldResetSlotStateForTeleport(byte slot, PlayerEntity player)
    {
        if (_directSeekStatesBySlot.TryGetValue(slot, out var directState)
            && ShouldResetSlotStateForTeleport(player, directState.LastX, directState.LastBottom))
        {
            return true;
        }

        if (_graphStatesBySlot.TryGetValue(slot, out var graphState)
            && ShouldResetSlotStateForTeleport(player, graphState.LastX, graphState.LastBottom))
        {
            return true;
        }

        return _statesBySlot.TryGetValue(slot, out var replayState)
            && ShouldResetSlotStateForTeleport(player, replayState.LastX, replayState.LastBottom);
    }

    private static bool ShouldResetSlotStateForTeleport(PlayerEntity player, float lastX, float lastBottom)
    {
        if (float.IsNaN(lastX) || float.IsNaN(lastBottom))
        {
            return false;
        }

        return Distance(player.X, player.Bottom, lastX, lastBottom) >= SlotStateTeleportResetDistance;
    }

    private static int GetObjectiveTapeFastForwardTicks(SimulationWorld world)
    {
        return Math.Max(1, (int)MathF.Ceiling(world.Config.TicksPerSecond * ObjectiveTapeFastForwardSeconds));
    }

    private static int GetGraphNoProgressTickLimit(string goalLabel)
    {
        return IsDynamicGraphGoal(goalLabel) ? EnemyGraphNoProgressTickLimit : NoProgressTickLimit;
    }

    private static int GetGraphRescueJumpTicks(string goalLabel)
    {
        return IsDynamicGraphGoal(goalLabel) ? EnemyGraphRescueJumpTicks : ObjectiveNavigationRescueJumpTicks;
    }

    private static bool CanFastForwardInertTapeAction(IReadOnlyList<MotionProofAction> actions, int currentIndex)
    {
        return currentIndex >= 0
            && currentIndex < actions.Count
            && IsInertTapeAction(actions[currentIndex]);
    }

    private static bool ShouldForceFastForwardInertTapeAction(
        IReadOnlyList<MotionProofAction> actions,
        int currentIndex,
        int actionTick,
        int fastForwardTicks)
    {
        if (currentIndex < 0
            || currentIndex >= actions.Count
            || !IsInertTapeAction(actions[currentIndex]))
        {
            return false;
        }

        return actionTick >= Math.Max(1, fastForwardTicks);
    }

    private static int FindNextActiveTapeActionIndex(IReadOnlyList<MotionProofAction> actions, int currentIndex)
    {
        for (var index = Math.Min(currentIndex + 1, actions.Count - 1); index < actions.Count; index += 1)
        {
            if (!IsInertTapeAction(actions[index]))
            {
                return index;
            }
        }

        return Math.Min(currentIndex + 1, actions.Count - 1);
    }

    private static bool TryFindBetterObjectiveTapeAction(
        IReadOnlyList<MotionProofAction> actions,
        int currentIndex,
        float goalDeltaX,
        out int actionIndex)
    {
        var desiredDirection = Math.Sign(goalDeltaX);
        if (desiredDirection == 0)
        {
            actionIndex = currentIndex;
            return false;
        }

        for (var index = Math.Min(currentIndex + 1, actions.Count - 1); index < actions.Count; index += 1)
        {
            var action = actions[index];
            if (IsInertTapeAction(action))
            {
                continue;
            }

            if (action.Direction == desiredDirection || IsVerticalRecoveryTapeAction(action))
            {
                actionIndex = index;
                return index != currentIndex;
            }
        }

        actionIndex = currentIndex;
        return false;
    }

    private static bool IsVerticalRecoveryTapeAction(MotionProofAction action)
    {
        return action.Kind.Equals("Jump", StringComparison.OrdinalIgnoreCase)
            || action.Kind.Equals("Drop", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInertTapeAction(MotionProofAction action)
    {
        return action.Kind.Equals("Idle", StringComparison.OrdinalIgnoreCase)
            || (action.Direction == 0
                && !action.Kind.Equals("Jump", StringComparison.OrdinalIgnoreCase)
                && !action.Kind.Equals("Drop", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTapeDirectionalRecoveryCandidate(MotionProofAction action)
    {
        return IsInertTapeAction(action)
            || action.Kind.Equals("Run", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGroundedIdleAction(MotionProofAction action, PlayerEntity player)
    {
        return player.IsGrounded
            && action.Kind.Equals("Idle", StringComparison.OrdinalIgnoreCase);
    }

    private static PlayerInputSnapshot BuildActionInput(MotionProofAction action, int tick, PlayerEntity player)
    {
        var left = action.Direction < 0;
        var right = action.Direction > 0;
        var up = string.Equals(action.Kind, "Jump", StringComparison.OrdinalIgnoreCase) && tick == 0;
        var down = string.Equals(action.Kind, "Drop", StringComparison.OrdinalIgnoreCase);
        var aimDirection = action.Direction == 0 ? 1 : action.Direction;
        return new PlayerInputSnapshot(
            Left: left,
            Right: right,
            Up: up,
            Down: down,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + (aimDirection * 160f),
            AimWorldY: player.Y,
            DebugKill: false);
    }

    private static PlayerInputSnapshot ApplyNavigationRescueInput(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        MotionProofAction action,
        PlayerInputSnapshot input,
        int noProgressTicks,
        int noHorizontalProgressTicks,
        int noGoalProgressTicks,
        bool allowObstacleJump,
        int rescueJumpTicks,
        bool allowDirectionReverse)
    {
        var direction = input.Right ? 1 : input.Left ? -1 : action.Direction;
        if (direction == 0)
        {
            return input;
        }

        var blockedAhead = player.IsGrounded
            && WouldMoveIntoObstacle(world, team, player, direction);
        var frontFootBlocked = allowObstacleJump
            && player.IsGrounded
            && HasDirectSeekForwardFootBlock(world, team, player, direction, probeDistance: 8f)
            && HasDirectSeekJumpHeadClear(world, team, player);
        var obstacleAhead = allowObstacleJump && blockedAhead;
        var goalStallTicks = Math.Max(0, noGoalProgressTicks - rescueJumpTicks);
        var blockedHorizontalStall = (blockedAhead || frontFootBlocked)
            && noHorizontalProgressTicks >= rescueJumpTicks;
        var blockedGoalStall = blockedAhead
            && noGoalProgressTicks >= rescueJumpTicks;
        var jumpableLipStall = frontFootBlocked
            && noHorizontalProgressTicks >= 2
            && action.Kind.Equals("Run", StringComparison.OrdinalIgnoreCase);
        var shouldJump = noProgressTicks >= rescueJumpTicks
            || blockedHorizontalStall
            || blockedGoalStall
            || jumpableLipStall
            || (obstacleAhead && action.Kind.Equals("Run", StringComparison.OrdinalIgnoreCase));
        if (!shouldJump)
        {
            return input;
        }

        if (allowDirectionReverse
            && (noProgressTicks >= rescueJumpTicks || blockedHorizontalStall || blockedGoalStall)
            && blockedAhead
            && !WouldMoveIntoObstacle(world, team, player, -direction, probeDistance: DirectSeekObstacleProbeDistance))
        {
            direction = -direction;
        }

        return input with
        {
            Up = player.IsGrounded || noProgressTicks >= rescueJumpTicks || noHorizontalProgressTicks >= rescueJumpTicks || goalStallTicks > 0,
            Left = direction < 0,
            Right = direction > 0,
        };
    }

    private static PlayerInputSnapshot ApplyObjectiveTapeRescueInput(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        MotionProofAction action,
        PlayerInputSnapshot input,
        int noProgressTicks,
        bool allowObstacleJump,
        int rescueJumpTicks)
    {
        var direction = input.Right ? 1 : input.Left ? -1 : action.Direction;
        if (direction == 0)
        {
            return input;
        }

        var blockedAhead = player.IsGrounded
            && WouldMoveIntoObstacle(world, team, player, direction);
        var obstacleAhead = allowObstacleJump && blockedAhead;
        var shouldJump = noProgressTicks >= rescueJumpTicks
            || (obstacleAhead && action.Kind.Equals("Run", StringComparison.OrdinalIgnoreCase));
        if (!shouldJump)
        {
            return input;
        }

        if (noProgressTicks >= rescueJumpTicks
            && blockedAhead
            && !WouldMoveIntoObstacle(world, team, player, -direction, probeDistance: DirectSeekObstacleProbeDistance))
        {
            direction = -direction;
        }

        return input with
        {
            Up = player.IsGrounded || noProgressTicks >= rescueJumpTicks,
            Left = direction < 0,
            Right = direction > 0,
        };
    }

    private static bool WouldMoveIntoObstacle(
        SimulationWorld world,
        PlayerTeam team,
        PlayerEntity player,
        int direction,
        float probeDistance = ObstacleProbeDistance)
    {
        var probeX = player.X + (Math.Sign(direction) * probeDistance);
        player.GetCollisionBoundsAt(probeX, player.Y, out var left, out var top, out var right, out var bottom);
        if (IsOutsideLevelBounds(world, left, top, right, bottom))
        {
            return true;
        }

        if (world.Level.IntersectsSolid(left, top, right, bottom))
        {
            return true;
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, player.IsCarryingIntel))
        {
            if (RectanglesOverlap(left, top, right, bottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                return true;
            }
        }

        foreach (var wall in world.Level.RoomObjects)
        {
            if (wall.Type != RoomObjectType.PlayerWall)
            {
                continue;
            }

            if (RectanglesOverlap(left, top, right, bottom, wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOutsideLevelBounds(SimulationWorld world, float left, float top, float right, float bottom)
    {
        return left < 0f
            || top < 0f
            || right > world.Level.Bounds.Width
            || bottom > world.Level.Bounds.Height;
    }

    private static (float X, float Y) ResolveIdleAimTarget(SimulationWorld world, PlayerEntity player)
    {
        var gunspinDegrees = (float)((world.SimulationTimeSeconds * 360d) % 360d);
        var radians = gunspinDegrees * (MathF.PI / 180f);
        return (player.X + (MathF.Cos(radians) * 96f), player.Y + (MathF.Sin(radians) * 96f));
    }

    private static PlayerInputSnapshot BuildIdleInput(SimulationWorld world, PlayerEntity player)
    {
        var aimTarget = ResolveIdleAimTarget(world, player);
        return new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: aimTarget.X,
            AimWorldY: aimTarget.Y,
            DebugKill: false);
    }

    private PlayerInputSnapshot ApplyCombatInput(
        SimulationWorld world,
        PlayerEntity player,
        PlayerInputSnapshot movementInput,
        IReadOnlyList<PlayerEntity> allPlayers,
        bool preservePassiveAim = false,
        bool preserveMovementAuthority = false)
    {
        if (!player.IsAlive)
        {
            return movementInput;
        }

        var hasCombatTarget = TrySelectCombatTarget(world, player, allPlayers, out var target);
        var targetPlayer = target.Player;
        var healTarget = player.ClassId == PlayerClass.Medic
            ? FindBestMedicHealTarget(world, player, allPlayers)
            : null;
        var isBeingHealed = IsPlayerBeingHealed(allPlayers, player);
        var firePrimary = false;
        var fireSecondary = false;
        var fireSecondaryWeapon = false;
        var idleAimTarget = ResolveIdleAimTarget(world, player);
        var aimX = preservePassiveAim ? movementInput.AimWorldX : idleAimTarget.X;
        var aimY = preservePassiveAim ? movementInput.AimWorldY : idleAimTarget.Y;
        if (hasCombatTarget && !preserveMovementAuthority)
        {
            movementInput = ApplyCombatSeekMovement(world, player, movementInput, target);
        }

        if (targetPlayer is not null && !preserveMovementAuthority)
        {
            movementInput = ApplyCombatSpacingInput(movementInput, player, targetPlayer);
        }

        if (player.ClassId == PlayerClass.Medic)
        {
            if (healTarget is not null)
            {
                if (!preserveMovementAuthority)
                {
                    movementInput = ApplyMedicSupportMovement(world, player, movementInput, healTarget);
                }
                aimX = healTarget.X;
                aimY = GetCombatAimTargetY(healTarget);
                firePrimary = Distance(player.X, player.Y, healTarget.X, healTarget.Y) <= MedicHealFireDistance
                    && HasCombatLineOfSight(world, player, healTarget);
                fireSecondary = player.IsMedicUberReady
                    && (healTarget.Health < healTarget.MaxHealth / 2
                        || GetHealthFraction(player) < LowHealthFraction
                        || (hasCombatTarget
                            && Distance(player.X, player.Y, target.X, target.Y) <= MedicUberEnemyThreatDistance));
            }
            else if (hasCombatTarget && Distance(player.X, player.Y, target.X, target.Y) <= MedicNeedleRange)
            {
                aimX = target.X;
                aimY = target.Y;
                fireSecondary = true;
            }

            fireSecondaryWeapon = player.IsMedicUberReady
                && (player.Health < 40 || healTarget is { Health: < 50 });

            return movementInput with
            {
                FirePrimary = firePrimary,
                FireSecondary = fireSecondary,
                AimWorldX = aimX,
                AimWorldY = aimY,
                UseAbility = fireSecondaryWeapon,
            };
        }

        if (player.ClassId == PlayerClass.Heavy)
        {
            var shouldEat = ShouldHeavyEat(player, isBeingHealed, hasCombatTarget ? HeavyCombatEatHealth : HeavyIdleEatHealth);
            if (hasCombatTarget)
            {
                aimX = target.X;
                aimY = target.Y;
            }

            return movementInput with
            {
                FirePrimary = hasCombatTarget && !shouldEat && ShouldFirePrimaryAtTarget(player, target),
                FireSecondary = shouldEat,
                AimWorldX = aimX,
                AimWorldY = aimY,
            };
        }

        if (player.ClassId == PlayerClass.Demoman)
        {
            if (hasCombatTarget)
            {
                aimX = target.X;
                aimY = target.Y;
                firePrimary = ShouldFirePrimaryAtTarget(player, target);
            }

            fireSecondary = ShouldDetonateMines(world, player, allPlayers);
            return movementInput with
            {
                FirePrimary = firePrimary,
                FireSecondary = fireSecondary,
                AimWorldX = aimX,
                AimWorldY = aimY,
            };
        }

        if (player.ClassId == PlayerClass.Sniper)
        {
            if (!hasCombatTarget)
            {
                return movementInput with
                {
                    FireSecondary = player.IsSniperScoped,
                    AimWorldX = aimX,
                    AimWorldY = aimY,
                };
            }

            var sniperDistance = Distance(player.X, player.Y, target.X, target.Y);
            aimX = target.X;
            aimY = target.Y;
            fireSecondary = player.IsSniperScoped
                ? sniperDistance < SniperScopeDistance
                : sniperDistance >= SniperScopeDistance;
            firePrimary = player.IsSniperScoped && player.SniperChargeTicks >= SniperMinimumChargeTicks;
            return movementInput with
            {
                FirePrimary = firePrimary,
                FireSecondary = fireSecondary,
                AimWorldX = aimX,
                AimWorldY = aimY,
            };
        }

        if (player.ClassId == PlayerClass.Spy)
        {
            if (!hasCombatTarget)
            {
                return movementInput with
                {
                    FireSecondary = player.IsSpyCloaked,
                    AimWorldX = aimX,
                    AimWorldY = aimY,
                };
            }

            aimX = target.X;
            aimY = target.Y;
            var spyDistance = Distance(player.X, player.Y, target.X, target.Y);
            fireSecondary = player.IsCarryingIntel
                ? false
                : player.IsSpyCloaked
                    ? spyDistance > 96f && !player.IsSpyBackstabAnimating
                    : false;
            firePrimary = player.IsSpyCloaked
                ? target.Kind == CombatTargetKind.Player && spyDistance <= 64f
                : ShouldFirePrimaryAtTarget(player, target);
            return movementInput with
            {
                FirePrimary = firePrimary,
                FireSecondary = fireSecondary,
                AimWorldX = aimX,
                AimWorldY = aimY,
            };
        }

        if (!hasCombatTarget)
        {
            return movementInput with
            {
                AimWorldX = aimX,
                AimWorldY = aimY,
            };
        }

        var distance = Distance(player.X, player.Y, target.X, target.Y);
        firePrimary = ShouldFirePrimaryAtTarget(player, target);
        fireSecondary = player.ClassId == PlayerClass.Demoman
            ? ShouldDetonateOwnedMinesAtTarget(world, player, target)
            : player.ClassId == PlayerClass.Quote
            && distance is > QuoteBladeMinimumDistance and <= QuoteBladeMaximumDistance;
        fireSecondaryWeapon = ShouldFireSecondaryWeaponAtTarget(player, target, firePrimary, fireSecondary);
        return movementInput with
        {
            FirePrimary = firePrimary,
            FireSecondary = fireSecondary,
            AimWorldX = target.X,
            AimWorldY = target.Y,
            UseAbility = fireSecondaryWeapon,
        };
    }

    private static PlayerInputSnapshot ApplyCombatSeekMovement(
        SimulationWorld world,
        PlayerEntity player,
        PlayerInputSnapshot movementInput,
        CombatTarget target)
    {
        if (!ShouldCommitMovementToCombatTarget(player, target, out var desiredSpacing, out var targetBottom))
        {
            return movementInput;
        }

        var deltaX = target.X - player.X;
        var deltaBottom = targetBottom - player.Bottom;
        if (MathF.Abs(deltaBottom) > CombatSeekVerticalTolerance)
        {
            return movementInput;
        }

        var direction = MathF.Abs(deltaX) > desiredSpacing
            ? Math.Sign(deltaX)
            : 0;
        var targetAbove = deltaBottom < -DirectSeekVerticalJumpThreshold;
        var targetBelow = deltaBottom > CombatSeekDropTolerance;
        var obstacleAhead = direction != 0
            && WouldMoveIntoObstacle(world, player.Team, player, direction, CombatSeekObstacleProbeDistance);
        if (obstacleAhead)
        {
            return movementInput;
        }

        var shouldJump = player.IsGrounded
            && direction != 0
            && targetAbove
            && MathF.Abs(deltaX) <= CombatSeekJumpHorizontalRadius;
        var shouldDrop = !shouldJump
            && targetBelow
            && MathF.Abs(deltaX) <= 96f;

        return movementInput with
        {
            Left = direction < 0,
            Right = direction > 0,
            Up = shouldJump,
            Down = shouldDrop,
        };
    }

    private static PlayerInputSnapshot ApplyCombatSpacingInput(PlayerInputSnapshot movementInput, PlayerEntity player, PlayerEntity target)
    {
        var minimumSpacing = GetMinimumCombatSpacing(player.ClassId);
        if (MathF.Abs(target.Bottom - player.Bottom) > 48f
            || MathF.Abs(target.X - player.X) >= minimumSpacing)
        {
            return movementInput;
        }

        var awayDirection = Math.Sign(player.X - target.X);
        if (awayDirection == 0)
        {
            awayDirection = player.FacingDirectionX >= 0f ? -1 : 1;
        }

        return movementInput with
        {
            Left = awayDirection < 0,
            Right = awayDirection > 0,
        };
    }

    private static bool ShouldCommitMovementToCombatTarget(
        PlayerEntity player,
        CombatTarget target,
        out float desiredSpacing,
        out float targetBottom)
    {
        desiredSpacing = GetMinimumCombatSpacing(player.ClassId);
        targetBottom = target.Player?.Bottom ?? target.Y;

        if (player.IsCarryingIntel)
        {
            return false;
        }

        if (target.Kind != CombatTargetKind.Player)
        {
            return false;
        }

        if (target.Player is { IsSpyCloaked: true }
            && player.ClassId != PlayerClass.Pyro)
        {
            return false;
        }

        var commitDistance = GetCombatSeekCommitDistance(player.ClassId, target.Kind);
        return Distance(player.X, player.Y, target.X, target.Y) <= commitDistance;
    }

    private static PlayerInputSnapshot ApplyMedicSupportMovement(
        SimulationWorld world,
        PlayerEntity player,
        PlayerInputSnapshot movementInput,
        PlayerEntity healTarget)
    {
        var healDistance = Distance(player.X, player.Y, healTarget.X, healTarget.Y);
        if (healDistance <= MedicHealFollowDistance)
        {
            return movementInput;
        }

        var direction = Math.Sign(healTarget.X - player.X);
        if (direction == 0)
        {
            return movementInput;
        }

        if (WouldMoveIntoObstacle(world, player.Team, player, direction, probeDistance: 16f))
        {
            var reverseDirection = -direction;
            direction = WouldMoveIntoObstacle(world, player.Team, player, reverseDirection, probeDistance: 16f)
                ? 0
                : reverseDirection;
        }

        var jump = movementInput.Up;
        if (direction != 0
            && player.IsGrounded
            && (healTarget.Y < player.Y - 24f
                || WouldMoveIntoObstacle(world, player.Team, player, direction, probeDistance: 16f)))
        {
            jump = true;
        }

        return movementInput with
        {
            Left = direction < 0,
            Right = direction > 0,
            Up = jump,
        };
    }

    private static float GetCombatSeekCommitDistance(PlayerClass classId, CombatTargetKind targetKind)
    {
        if (targetKind == CombatTargetKind.Generator)
        {
            return classId switch
            {
                PlayerClass.Pyro => 200f,
                PlayerClass.Spy => 120f,
                _ => 260f,
            };
        }

        return classId switch
        {
            PlayerClass.Pyro => 210f,
            PlayerClass.Spy => 120f,
            PlayerClass.Scout => 300f,
            PlayerClass.Heavy => 340f,
            PlayerClass.Soldier => 360f,
            PlayerClass.Demoman => 280f,
            PlayerClass.Sniper => 260f,
            PlayerClass.Medic => 240f,
            PlayerClass.Quote => 240f,
            _ => 280f,
        };
    }

    private static float GetMinimumCombatSpacing(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Pyro => 72f,
            PlayerClass.Scout => 96f,
            PlayerClass.Spy => 72f,
            PlayerClass.Heavy => 160f,
            PlayerClass.Soldier => 180f,
            PlayerClass.Demoman => 180f,
            PlayerClass.Sniper => 180f,
            _ => 110f,
        };
    }

    private static bool TrySelectCombatTarget(
        SimulationWorld world,
        PlayerEntity player,
        IReadOnlyList<PlayerEntity> allPlayers,
        out CombatTarget target)
    {
        target = default;
        var found = false;
        var bestDistance = MathF.Max(CombatSightDistance, GetMaximumFireRange(player.ClassId));

        for (var index = 0; index < world.Generators.Count; index += 1)
        {
            var candidate = world.Generators[index];
            if (candidate.Team == player.Team || candidate.IsDestroyed)
            {
                continue;
            }

            var candidateX = candidate.Marker.CenterX;
            var candidateY = candidate.Marker.CenterY;
            var distance = Distance(player.X, player.Y, candidateX, candidateY);
            if (distance >= bestDistance
                || !HasLineOfSight(world, player.X, player.Y, candidateX, candidateY, player.Team, player.IsCarryingIntel))
            {
                continue;
            }

            found = true;
            bestDistance = distance;
            target = new CombatTarget(CombatTargetKind.Generator, candidate.Team, candidateX, candidateY, Generator: candidate);
        }

        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            var treatAsFriendlyFireTarget = SimulationWorld.ShouldTreatPlayerAsExperimentalFriendlyFireTarget(player, candidate);
            if (candidate.Id == player.Id
                || !candidate.IsAlive
                || (candidate.Team == player.Team && !treatAsFriendlyFireTarget))
            {
                continue;
            }

            var distance = Distance(player.X, player.Y, candidate.X, candidate.Y);
            if (distance >= bestDistance || !HasCombatLineOfSight(world, player, candidate))
            {
                continue;
            }

            found = true;
            bestDistance = distance;
            target = new CombatTarget(CombatTargetKind.Player, candidate.Team, candidate.X, GetCombatAimTargetY(candidate), Player: candidate);
        }

        for (var index = 0; index < world.Sentries.Count; index += 1)
        {
            var candidate = world.Sentries[index];
            if (candidate.Team == player.Team || candidate.Health <= 0)
            {
                continue;
            }

            var candidateY = candidate.Y - (SentryEntity.Height * 0.5f);
            var distance = Distance(player.X, player.Y, candidate.X, candidateY);
            if (distance >= bestDistance
                || !HasLineOfSight(world, player.X, player.Y, candidate.X, candidateY, player.Team, player.IsCarryingIntel))
            {
                continue;
            }

            found = true;
            bestDistance = distance;
            target = new CombatTarget(CombatTargetKind.Sentry, candidate.Team, candidate.X, candidateY, Sentry: candidate);
        }

        return found;
    }

    private static bool ShouldFirePrimaryAtTarget(PlayerEntity player, CombatTarget target)
    {
        if (target.Kind == CombatTargetKind.Player
            && target.Player is { IsSpyCloaked: true }
            && player.ClassId != PlayerClass.Pyro)
        {
            return false;
        }

        var distance = Distance(player.X, player.Y, target.X, target.Y);
        return player.PrimaryWeapon.Kind switch
        {
            PrimaryWeaponKind.PelletGun => distance <= 280f,
            PrimaryWeaponKind.Minigun => distance <= 340f,
            PrimaryWeaponKind.Rifle => distance <= 900f,
            PrimaryWeaponKind.Revolver => distance <= 480f,
            PrimaryWeaponKind.FlameThrower => distance <= 150f,
            PrimaryWeaponKind.RocketLauncher => distance is >= 120f and <= 520f,
            PrimaryWeaponKind.MineLauncher => distance is >= 90f and <= 260f,
            PrimaryWeaponKind.Blade => distance <= 64f,
            PrimaryWeaponKind.Medigun => false,
            _ => distance <= GetMaximumFireRange(player.ClassId),
        };
    }

    private static bool ShouldDetonateOwnedMinesAtTarget(
        SimulationWorld world,
        PlayerEntity player,
        CombatTarget target)
    {
        if (player.ClassId != PlayerClass.Demoman
            || player.IsExperimentalDemoknightEnabled)
        {
            return false;
        }

        var detonationRadius = MineProjectileEntity.BlastRadius + 18f;
        var detonationRadiusSquared = detonationRadius * detonationRadius;
        for (var mineIndex = 0; mineIndex < world.Mines.Count; mineIndex += 1)
        {
            var mine = world.Mines[mineIndex];
            if (mine.OwnerId != player.Id
                || mine.Team != player.Team
                || !mine.IsStickied
                || mine.IsDestroyed)
            {
                continue;
            }

            if (DistanceSquared(mine.X, mine.Y, target.X, target.Y) <= detonationRadiusSquared)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldFireSecondaryWeaponAtTarget(
        PlayerEntity player,
        CombatTarget target,
        bool firePrimary,
        bool fireSecondary)
    {
        var distance = Distance(player.X, player.Y, target.X, target.Y);
        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.PyroUtility))
        {
            return target.Kind == CombatTargetKind.Player
                && player.CanFirePyroAirblast()
                && distance <= PyroAirblastDistance;
        }

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.SoldierSecondaryWeapon))
        {
            return !fireSecondary
                && (!firePrimary || player.CurrentShells <= 0 || player.PrimaryCooldownTicks > 0)
                && player.ExperimentalOffhandCurrentShells > 0
                && player.ExperimentalOffhandCooldownTicks <= 0
                && distance <= SoldierOffhandDistance;
        }

        return false;
    }

    private static PlayerEntity? FindBestMedicHealTarget(
        SimulationWorld world,
        PlayerEntity player,
        IReadOnlyList<PlayerEntity> allPlayers)
    {
        PlayerEntity? bestTarget = null;
        var bestScore = float.PositiveInfinity;
        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (!candidate.IsAlive
                || candidate.Team != player.Team
                || candidate.Id == player.Id)
            {
                continue;
            }

            if (!HasCombatLineOfSight(world, player, candidate))
            {
                continue;
            }

            var distance = Distance(player.X, player.Y, candidate.X, candidate.Y);
            var score = (distance / 1000f) + (GetHealthFraction(candidate) * 2f);

            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    private bool ShouldDetonateMines(
        SimulationWorld world,
        PlayerEntity player,
        IReadOnlyList<PlayerEntity> allPlayers)
    {
        var ownedMineCount = 0;
        for (var mineIndex = 0; mineIndex < world.Mines.Count; mineIndex += 1)
        {
            var mine = world.Mines[mineIndex];
            if (mine.OwnerId != player.Id || mine.IsDestroyed)
            {
                continue;
            }

            ownedMineCount += 1;
            if (Distance(player.X, player.Y, mine.X, mine.Y) >= MineThreatDistance)
            {
                continue;
            }

            for (var playerIndex = 0; playerIndex < allPlayers.Count; playerIndex += 1)
            {
                var candidate = allPlayers[playerIndex];
                if (!candidate.IsAlive
                    || candidate.Id == player.Id
                    || candidate.Team == player.Team)
                {
                    continue;
                }

                if (Distance(candidate.X, candidate.Y, mine.X, mine.Y) <= MineDetonationRadius)
                {
                    return true;
                }
            }
        }

        if (ownedMineCount >= player.PrimaryWeapon.MaxAmmo)
        {
            var roll = _random.Next(0, 101);
            return roll is >= 70 and <= 71;
        }

        return false;
    }

    private static bool IsPlayerBeingHealed(IReadOnlyList<PlayerEntity> allPlayers, PlayerEntity player)
    {
        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (!candidate.IsAlive
                || candidate.Team != player.Team
                || candidate.ClassId != PlayerClass.Medic
                || !candidate.IsMedicHealing
                || candidate.MedicHealTargetId != player.Id)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool ShouldHeavyEat(PlayerEntity player, bool isBeingHealed, int healthThreshold)
    {
        return player.ClassId == PlayerClass.Heavy
            && !isBeingHealed
            && !player.IsHeavyEating
            && player.HeavyEatCooldownTicksRemaining <= 0
            && player.Health <= healthThreshold;
    }

    private static bool NeedsHealing(PlayerEntity player)
    {
        return player.Health < player.MaxHealth
            || player.IsBurning
            || player.IsCarryingIntel
            || GetHealthFraction(player) < LowHealthFraction;
    }

    private static float GetHealthFraction(PlayerEntity player)
    {
        return player.MaxHealth <= 0 ? 0f : player.Health / (float)player.MaxHealth;
    }

    private static float DistanceSquared(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return (dx * dx) + (dy * dy);
    }

    private static float GetMaximumFireRange(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Pyro => 230f,
            PlayerClass.Heavy => 520f,
            PlayerClass.Soldier => 700f,
            PlayerClass.Sniper => 900f,
            PlayerClass.Spy => 480f,
            PlayerClass.Medic => MedicNeedleRange,
            _ => 520f,
        };
    }

    private static bool HasCombatLineOfSight(SimulationWorld world, PlayerEntity origin, PlayerEntity target)
    {
        return HasLineOfSight(
            world,
            origin.X,
            origin.Y,
            target.X,
            GetCombatAimTargetY(target),
            origin.Team,
            origin.IsCarryingIntel);
    }

    private static bool HasLineOfSight(
        SimulationWorld world,
        float originX,
        float originY,
        float targetX,
        float targetY,
        PlayerTeam team,
        bool carryingIntel)
    {
        var distance = Distance(originX, originY, targetX, targetY);
        if (distance <= 0.0001f)
        {
            return true;
        }

        var directionX = (targetX - originX) / distance;
        var directionY = (targetY - originY) / distance;
        var lineLeft = MathF.Min(originX, targetX);
        var lineTop = MathF.Min(originY, targetY);
        var lineRight = MathF.Max(originX, targetX);
        var lineBottom = MathF.Max(originY, targetY);
        foreach (var solid in world.Level.Solids)
        {
            if (!RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, solid.Left, solid.Top, solid.Right, solid.Bottom))
            {
                continue;
            }

            if (GetRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, solid.Left, solid.Top, solid.Right, solid.Bottom, distance).HasValue)
            {
                return false;
            }
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, carryingIntel))
        {
            if (!RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                continue;
            }

            if (GetRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, gate.Left, gate.Top, gate.Right, gate.Bottom, distance).HasValue)
            {
                return false;
            }
        }

        foreach (var wall in world.Level.RoomObjects)
        {
            if (wall.Type != RoomObjectType.PlayerWall && wall.Type != RoomObjectType.BulletWall)
            {
                continue;
            }

            if (!RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                continue;
            }

            if (GetRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, wall.Left, wall.Top, wall.Right, wall.Bottom, distance).HasValue)
            {
                return false;
            }
        }

        return true;
    }

    private static float GetCombatAimTargetY(PlayerEntity target)
    {
        return (target.Top + target.Bottom) * 0.5f;
    }

    private static bool RectanglesOverlap(
        float leftA,
        float topA,
        float rightA,
        float bottomA,
        float leftB,
        float topB,
        float rightB,
        float bottomB)
    {
        return leftA < rightB && rightA > leftB && topA < bottomB && bottomA > topB;
    }

    private static float? GetRayIntersectionDistanceWithRectangle(
        float originX,
        float originY,
        float directionX,
        float directionY,
        float left,
        float top,
        float right,
        float bottom,
        float maxDistance)
    {
        var tMin = 0f;
        var tMax = maxDistance;
        if (!ClipRaySlab(originX, directionX, left, right, ref tMin, ref tMax)
            || !ClipRaySlab(originY, directionY, top, bottom, ref tMin, ref tMax))
        {
            return null;
        }

        return tMin >= 0f && tMin <= maxDistance ? tMin : null;
    }

    private static bool ClipRaySlab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(direction) < 0.0001f)
        {
            return origin >= min && origin <= max;
        }

        var inverse = 1f / direction;
        var t1 = (min - origin) * inverse;
        var t2 = (max - origin) * inverse;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = MathF.Max(tMin, t1);
        tMax = MathF.Min(tMax, t2);
        return tMin <= tMax;
    }

    private void PruneStates(IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        var staleSlots = new List<byte>();
        foreach (var slot in _statesBySlot.Keys)
        {
            if (!controlledSlots.ContainsKey(slot))
            {
                staleSlots.Add(slot);
            }
        }

        foreach (var slot in staleSlots)
        {
            ResetSlotState(slot);
        }

        staleSlots.Clear();
        foreach (var slot in _aliveBySlot.Keys)
        {
            if (!controlledSlots.ContainsKey(slot))
            {
                staleSlots.Add(slot);
            }
        }

        foreach (var slot in staleSlots)
        {
            ResetSlotState(slot);
        }
    }

    private void ResetSlotState(byte slot)
    {
        _statesBySlot.Remove(slot);
        _graphStatesBySlot.Remove(slot);
        _directSeekStatesBySlot.Remove(slot);
        _objectiveSeamStatesBySlot.Remove(slot);
        _deferredObjectiveSeamProgramsBySlot.Remove(slot);
        _objectiveSeamIssueBySlot.Remove(slot);
        _objectiveHoldPointBySlot.Remove(slot);
        _localObjectiveDirectPointBySlot.Remove(slot);
        _aliveBySlot.Remove(slot);
    }

    private enum ReplayPhase
    {
        Attack,
        Return,
    }

    private readonly record struct ReplayState(
        string MapKey,
        ReplayPhase Phase,
        int ActionIndex,
        int ActionTick,
        float LastX,
        float LastBottom,
        int NoProgressTicks,
        int NoHorizontalProgressTicks,
        float BestGoalDistance,
        int NoGoalProgressTicks,
        int ReattachResumeActionIndex,
        TapeReattachPoint? ActiveReattachPoint,
        int SpawnExitPrefixResumeActionIndex,
        float ImplicitPhaseStartX,
        float ImplicitPhaseStartBottom,
        bool GraphRecoveryActive,
        bool PendingForwardReattach);

    private readonly record struct GraphGoal(string Label, float X, float Bottom, float Radius);

    private sealed record ObjectiveSeamProgram(
        string Label,
        IReadOnlyList<MotionProofAction> Actions,
        float GoalX,
        float GoalBottom,
        float GoalRadius,
        int DurationTicks,
        int NoMovementTicks,
        IReadOnlyList<ObjectiveSeamStartWindow>? StartWindows = null,
        ObjectiveSeamCompletionWindow? CompletionWindow = null)
    {
        public IReadOnlyList<ObjectiveSeamStartWindow> StartWindows { get; init; } = StartWindows ?? [];
    }

    private readonly record struct ObjectiveSeamStartWindow(
        float StartXMin,
        float StartXMax,
        float StartBottom,
        float StartBottomTolerance,
        float FacingDirectionX,
        float HorizontalSpeedTolerance,
        float VerticalSpeedTolerance,
        bool RequireGrounded,
        float HorizontalSpeedCenter = 0f,
        float VerticalSpeedCenter = 0f);

    private readonly record struct ObjectiveSeamCompletionWindow(
        float XMin,
        float XMax,
        float Bottom,
        float BottomTolerance,
        bool RequireGrounded);

    private readonly record struct ObjectiveSeamState(
        ObjectiveSeamProgram Program,
        int ActionIndex,
        int ActionTick,
        int TicksRemaining,
        int NoMovementTicks,
        float LastX,
        float LastBottom);

    private enum CombatTargetKind
    {
        Player,
        Sentry,
        Generator,
    }

    private readonly record struct CombatTarget(
        CombatTargetKind Kind,
        PlayerTeam Team,
        float X,
        float Y,
        PlayerEntity? Player = null,
        SentryEntity? Sentry = null,
        GeneratorState? Generator = null);

    private readonly record struct DirectSeekState(
        string GoalKey,
        float LastX,
        float LastBottom,
        float BestGoalDistance,
        int NoProgressTicks,
        int NoHorizontalProgressTicks,
        int NoGoalProgressTicks,
        int TotalTicks,
        int BackupTicksRemaining,
        int BackupDirection,
        int VerticalSearchDirection);

    private readonly record struct DropSearchCandidate(float DropDistance, float ClearRunDistance);

    private readonly record struct GraphReplayState(
        string MapKey,
        string GoalKey,
        IReadOnlyList<MotionProofAction> Actions,
        int ActionIndex,
        int ActionTick,
        int StartNodeIndex,
        int GoalNodeIndex,
        float GoalNodeX,
        float GoalNodeBottom,
        IReadOnlyList<int> PathNodeIndices,
        float LastX,
        float LastBottom,
        int NoProgressTicks,
        int NoHorizontalProgressTicks);

    private readonly record struct GraphPlan(
        IReadOnlyList<MotionProofAction> Actions,
        int StartNodeIndex,
        int GoalNodeIndex,
        float GoalNodeX,
        float GoalNodeBottom,
        IReadOnlyList<int> PathNodeIndices);

    private sealed record GraphRouteMap(
        HashSet<int> GoalNodes,
        int[] CostByNode,
        MotionGraphEdge?[] NextEdgeByNode);

    private sealed class PreparedMotionGraph
    {
        private PreparedMotionGraph(
            int version,
            string identity,
            IReadOnlyList<MotionGraphNode> nodes,
            IReadOnlyDictionary<int, MotionGraphEdge[]> edgesByNode,
            IReadOnlyDictionary<int, MotionGraphEdge[]> reverseEdgesByNode)
        {
            Version = version;
            Identity = identity;
            Nodes = nodes;
            EdgesByNode = edgesByNode;
            ReverseEdgesByNode = reverseEdgesByNode;
        }

        public int Version { get; }

        public string Identity { get; }

        public IReadOnlyList<MotionGraphNode> Nodes { get; }

        public IReadOnlyDictionary<int, MotionGraphEdge[]> EdgesByNode { get; }

        public IReadOnlyDictionary<int, MotionGraphEdge[]> ReverseEdgesByNode { get; }

        public static PreparedMotionGraph From(MotionGraphArtifact artifact, string identity)
        {
            var edgesByNode = artifact.Edges
                .GroupBy(edge => edge.From)
                .ToDictionary(group => group.Key, group => group.ToArray());
            var reverseEdgesByNode = artifact.Edges
                .GroupBy(edge => edge.To)
                .ToDictionary(group => group.Key, group => group.ToArray());
            return new PreparedMotionGraph(artifact.Version, identity, artifact.Nodes, edgesByNode, reverseEdgesByNode);
        }
    }

    private sealed record MotionGraphArtifact(
        int Version,
        string Map,
        int Area,
        string Mode,
        string Team,
        string Class,
        IReadOnlyList<MotionGraphNode> Nodes,
        IReadOnlyList<MotionGraphEdge> Edges);

    private sealed record MotionGraphNode(
        int Id,
        float X,
        float Y,
        float Bottom,
        bool IsGrounded,
        float HorizontalSpeed = 0f,
        float VerticalSpeed = 0f,
        int RemainingAirJumps = 0);

    private sealed record MotionGraphEdge(
        int From,
        int To,
        int CostTicks,
        MotionProofAction Action,
        string TeamMask = "Any",
        string CarryMask = "Any");

    private sealed record MotionProofArtifact(
        int Version,
        string Map,
        int Area,
        string Mode,
        string Team,
        string Class,
        int AttackTicks,
        int ReturnTicks,
        IReadOnlyList<MotionProofAction> Attack,
        IReadOnlyList<MotionProofAction> Return);

    private sealed record MotionProofAction(string Kind, int Direction, int Ticks);

    private sealed record PreparedMotionProofArtifact(
        MotionProofArtifact Artifact,
        TapePhaseData AttackPhase,
        TapePhaseData ReturnPhase);

    private sealed record TapePhaseData(
        float StartX,
        float StartBottom,
        IReadOnlyList<MotionProofAction> Actions,
        IReadOnlyList<TapeReattachPoint> ReattachPoints,
        IReadOnlyDictionary<int, TapeReattachPoint> ReattachPointsByResumeIndex)
    {
        public static TapePhaseData Empty(IReadOnlyList<MotionProofAction> actions) =>
            new(float.NaN, float.NaN, actions, Array.Empty<TapeReattachPoint>(), new Dictionary<int, TapeReattachPoint>());
    }

    private readonly record struct TapeReattachPoint(
        int ResumeActionIndex,
        float X,
        float Bottom,
        float RadiusX,
        float RadiusBottom,
        bool IsInSpawnRoom,
        bool IsGrounded,
        float HorizontalSpeed,
        float VerticalSpeed,
        bool UseLiveBottom);
}
