namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IMatchObjectiveHost
{
    // IPlayerCountHost
    SimpleLevel IPlayerCountHost.Level => Level;

    IEnumerable<PlayerEntity> IPlayerCountHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();

    bool IPlayerCountHost.TryGetNetworkPlayerSlot(PlayerEntity player, out byte slot) => NetworkPlayerRules.TryGetNetworkPlayerSlot(player, out slot);

    bool IPlayerCountHost.IsNetworkPlayerAwaitingJoin(byte slot) => NetworkPlayerRules.IsNetworkPlayerAwaitingJoin(slot);

    bool IPlayerCountHost.CanPlayerContributeToControlPoint(PlayerEntity player) => LastToDieRules.CanPlayerContributeToControlPoint(player);

    // Match state shared by every mode
    MatchState IMatchObjectiveHost.MatchState
    {
        get => MatchState;
        set => MatchState = value;
    }

    MatchRules IMatchObjectiveHost.MatchRules => MatchRules;

    SimulationConfig IMatchObjectiveHost.Config => Config;

    PlayerEntity IMatchObjectiveHost.LocalPlayer => LocalPlayer;

    ObjectiveStateStore IMatchObjectiveHost.Objectives => Objectives;

    bool IMatchObjectiveHost.CompetitiveObjectivesLocked => CompetitiveObjectivesLocked;

    bool IMatchObjectiveHost.TryEndRound(PlayerTeam? winnerTeam, string reason) => TryEndRound(winnerTeam, reason);

    void IMatchObjectiveHost.RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId)
        => WorldEffects.RegisterWorldSoundEvent(soundName, x, y, sourcePlayerId);

    // Capture-the-flag and SCR scoring
    int IMatchObjectiveHost.RedCaps => RedCaps;

    int IMatchObjectiveHost.BlueCaps => BlueCaps;

    TeamIntelligenceState IMatchObjectiveHost.RedIntel => RedIntel;

    TeamIntelligenceState IMatchObjectiveHost.BlueIntel => BlueIntel;

    bool IMatchObjectiveHost.IsIntelAtHome(TeamIntelligenceState intelState) => ObjectiveRules.IsIntelAtHome(intelState);

    void IMatchObjectiveHost.TryPickUpEnemyIntel(PlayerEntity player) => ObjectiveRules.TryPickUpEnemyIntel(player);

    void IMatchObjectiveHost.TryScoreCarriedIntel(PlayerEntity player) => ObjectiveRules.TryScoreCarriedIntel(player);

    void IMatchObjectiveHost.RecordIntelReturnedObjectiveLog(PlayerTeam team) => KillFeedRules.RecordIntelReturnedObjectiveLog(team);

    void IMatchObjectiveHost.EvaluateMapLogicIntelTriggersIfNeeded() => MapLogic.EvaluateMapLogicIntelTriggersIfNeeded();

    bool IMatchObjectiveHost.TryEvaluateScrThresholdCrossing(bool isRoundStart) => ObjectiveRules.TryEvaluateScrThresholdCrossing(isRoundStart);

    void IMatchObjectiveHost.UpdateScrQualificationTracking() => ObjectiveRules.UpdateScrQualificationTracking();

    // Arena
    float IMatchObjectiveHost.ConfiguredCaptureSpeedMultiplierPerPlayer => ConfiguredCaptureSpeedMultiplierPerPlayer;

    int IMatchObjectiveHost.ArenaRedAliveCount => ArenaRedAliveCount;

    int IMatchObjectiveHost.ArenaBlueAliveCount => ArenaBlueAliveCount;

    int IMatchObjectiveHost.ArenaRedPlayerCount => ArenaRedPlayerCount;

    int IMatchObjectiveHost.ArenaBluePlayerCount => ArenaBluePlayerCount;

    int IMatchObjectiveHost.CountPlayersInArenaCaptureZone(PlayerTeam team) => CountPlayersInArenaCaptureZone(team);

    // Control point, KOTH, VIP and generator
    void IMatchObjectiveHost.UpdateControlPointState() => ObjectiveRules.UpdateControlPointState();

    void IMatchObjectiveHost.UpdateControlPointSetupGates() => ObjectiveRules.UpdateControlPointSetupGates();

    void IMatchObjectiveHost.ApplyControlPointSetupMatchRules() => ObjectiveRules.ApplyControlPointSetupMatchRules();

    void IMatchObjectiveHost.UpdateKothState() => ObjectiveRules.UpdateKothState();

    void IMatchObjectiveHost.AdvanceKothMatchStateCore() => ObjectiveRules.AdvanceKothMatchStateCore();

    bool IMatchObjectiveHost.ShouldDeferVipObjectiveResolution() => VipRules.ShouldDeferVipObjectiveResolution();

    void IMatchObjectiveHost.UpdateGeneratorState() => ObjectiveRulesSystem.UpdateGeneratorState();
}
