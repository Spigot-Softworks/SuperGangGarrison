using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

// Thin forwarders so remaining world code keeps its call shape; callers should migrate to the system directly over time.
public sealed partial class SimulationWorld
{
    public TeamIntelligenceState BlueIntel { get => ObjectiveRules.BlueIntel; set => ObjectiveRules.BlueIntel = value; }
    public void ForceDropLocalIntel() => ObjectiveRules.ForceDropLocalIntel();
    public bool ForceGiveEnemyIntelToLocalPlayer() => ObjectiveRules.ForceGiveEnemyIntelToLocalPlayer();
    public IReadOnlyList<GeneratorState> Generators => ObjectiveRules.Generators;
    public GeneratorState? GetGenerator(PlayerTeam team) => ObjectiveRules.GetGenerator(team);
    public bool IsKothModeActive => ObjectiveRules.IsKothModeActive;
    public bool IsPlayerInControlPointCaptureZone(PlayerEntity player, int controlPointIndex) => ObjectiveRules.IsPlayerInControlPointCaptureZone(player, controlPointIndex);
    public int KothBlueTimerTicksRemaining => ObjectiveRules.KothBlueTimerTicksRemaining;
    public int KothRedTimerTicksRemaining => ObjectiveRules.KothRedTimerTicksRemaining;
    public int KothUnlockTicksRemaining => ObjectiveRules.KothUnlockTicksRemaining;
    public TeamIntelligenceState RedIntel { get => ObjectiveRules.RedIntel; set => ObjectiveRules.RedIntel = value; }

    // Control-point setup and state (moved from the ControlPointSetupSystem/ControlPointStateSystem nested classes).
    public int ControlPointSetupDurationTicks => ObjectiveRules.ControlPointSetupDurationTicks;
    public void ConfigureSpecialCaptureTheFlagRules(bool endMatchOnRedTeamIntelCapture) => ObjectiveRules.ConfigureSpecialCaptureTheFlagRules(endMatchOnRedTeamIntelCapture);
}
