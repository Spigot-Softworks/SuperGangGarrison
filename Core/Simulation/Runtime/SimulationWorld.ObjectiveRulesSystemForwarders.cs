using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

// Thin forwarders so remaining world code keeps its call shape; callers should migrate to the system directly over time.
public sealed partial class SimulationWorld
{
    private void AdvanceKothMatchStateCore() => ObjectiveRules.AdvanceKothMatchStateCore();
    private void ApplyScrLevelMatchSettings() => ObjectiveRules.ApplyScrLevelMatchSettings();
    private void ApplySnapshotControlPoints(SnapshotMessage snapshot) => ObjectiveRules.ApplySnapshotControlPoints(snapshot);
    private void ApplySnapshotGenerators(SnapshotMessage snapshot) => ObjectiveRules.ApplySnapshotGenerators(snapshot);
    private void ApplySnapshotKoth(SnapshotMessage snapshot) => ObjectiveRules.ApplySnapshotKoth(snapshot);
    public TeamIntelligenceState BlueIntel { get => ObjectiveRules.BlueIntel; set => ObjectiveRules.BlueIntel = value; }
    internal void CombatTestFinalizeScrRoundStart() => ObjectiveRules.CombatTestFinalizeScrRoundStart();
    private TeamIntelligenceState CreateIntelState(PlayerTeam team) => ObjectiveRules.CreateIntelState(team);
    private void FinalizeScrRoundStart() => ObjectiveRules.FinalizeScrRoundStart();
    public void ForceDropLocalIntel() => ObjectiveRules.ForceDropLocalIntel();
    public bool ForceGiveEnemyIntelToLocalPlayer() => ObjectiveRules.ForceGiveEnemyIntelToLocalPlayer();
    public IReadOnlyList<GeneratorState> Generators => ObjectiveRules.Generators;
    private ControlPointState? GetDualKothPoint(PlayerTeam homeTeam) => ObjectiveRules.GetDualKothPoint(homeTeam);
    private TeamIntelligenceState GetEnemyIntelState(PlayerTeam team) => ObjectiveRules.GetEnemyIntelState(team);
    public GeneratorState? GetGenerator(PlayerTeam team) => ObjectiveRules.GetGenerator(team);
    private static int GetPlayerIntelReturnTicks(PlayerEntity player) => ObjectiveRulesSystem.GetPlayerIntelReturnTicks(player);
    private ControlPointState? GetSingleKothPoint() => ObjectiveRules.GetSingleKothPoint();
    private const int IntelPickupCooldownTicksAfterDrop = ObjectiveRulesSystem.IntelPickupCooldownTicksAfterDrop;
    private static bool IsControlPointMode(GameModeKind mode) => ObjectiveRulesSystem.IsControlPointMode(mode);
    private bool IsIntelAtHome(TeamIntelligenceState intelState) => ObjectiveRules.IsIntelAtHome(intelState);
    private static bool IsKothMode(GameModeKind mode) => ObjectiveRulesSystem.IsKothMode(mode);
    public bool IsKothModeActive => ObjectiveRules.IsKothModeActive;
    public bool IsPlayerInControlPointCaptureZone(PlayerEntity player, int controlPointIndex) => ObjectiveRules.IsPlayerInControlPointCaptureZone(player, controlPointIndex);
    public int KothBlueTimerTicksRemaining => ObjectiveRules.KothBlueTimerTicksRemaining;
    public int KothRedTimerTicksRemaining => ObjectiveRules.KothRedTimerTicksRemaining;
    public int KothUnlockTicksRemaining => ObjectiveRules.KothUnlockTicksRemaining;
    public TeamIntelligenceState RedIntel { get => ObjectiveRules.RedIntel; set => ObjectiveRules.RedIntel = value; }
    private void ResetGeneratorStateForNewRound() => ObjectiveRules.ResetGeneratorStateForNewRound();
    private void ResetKothStateForNewRound() => ObjectiveRules.ResetKothStateForNewRound();
    private bool TryDamageGenerator(PlayerTeam targetTeam, float damage, PlayerEntity? attacker = null) => ObjectiveRules.TryDamageGenerator(targetTeam, damage, attacker);
    private void TryDropCarriedIntel() => ObjectiveRules.TryDropCarriedIntel();
    private void TryDropCarriedIntel(PlayerEntity player) => ObjectiveRules.TryDropCarriedIntel(player);
    internal bool TryEvaluateScrThresholdCrossing(bool isRoundStart) => ObjectiveRules.TryEvaluateScrThresholdCrossing(isRoundStart);
    internal bool TryModifyTeamScore(PlayerTeam team, int delta, string reason, int actorPlayerId = -1) => ObjectiveRules.TryModifyTeamScore(team, delta, reason, actorPlayerId);
    private void TryPickUpEnemyIntel(PlayerEntity player) => ObjectiveRules.TryPickUpEnemyIntel(player);
    private void TryScoreCarriedIntel(PlayerEntity player) => ObjectiveRules.TryScoreCarriedIntel(player);
    private static void UpdateGeneratorState() => ObjectiveRulesSystem.UpdateGeneratorState();
    private void UpdateKothState() => ObjectiveRules.UpdateKothState();
    internal void UpdateScrQualificationTracking() => ObjectiveRules.UpdateScrQualificationTracking();
}
