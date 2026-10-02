namespace OpenGarrison.Core;

/// <summary>Read access to who is on the field, shared by objective rules and player-count queries.</summary>
internal interface IPlayerCountHost
{
    SimpleLevel Level { get; }

    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();

    bool TryGetNetworkPlayerSlot(PlayerEntity player, out byte slot);

    bool IsNetworkPlayerAwaitingJoin(byte slot);

    bool CanPlayerContributeToControlPoint(PlayerEntity player);
}

/// <summary>
/// Everything the per-mode objective and resolution rules need from the world.
/// Grouped by concern so it is clear which mode uses what.
/// </summary>
internal interface IMatchObjectiveHost : IPlayerCountHost
{
    // Match state shared by every mode.
    MatchState MatchState { get; set; }

    MatchRules MatchRules { get; }

    SimulationConfig Config { get; }

    PlayerEntity LocalPlayer { get; }

    ObjectiveStateStore Objectives { get; }

    bool CompetitiveObjectivesLocked { get; }

    bool TryEndRound(PlayerTeam? winnerTeam, string reason);

    void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId = -1);

    // Capture-the-flag and SCR scoring.
    int RedCaps { get; }

    int BlueCaps { get; }

    TeamIntelligenceState RedIntel { get; }

    TeamIntelligenceState BlueIntel { get; }

    bool IsIntelAtHome(TeamIntelligenceState intelState);

    void TryPickUpEnemyIntel(PlayerEntity player);

    void TryScoreCarriedIntel(PlayerEntity player);

    void RecordIntelReturnedObjectiveLog(PlayerTeam team);

    void EvaluateMapLogicIntelTriggersIfNeeded();

    bool TryEvaluateScrThresholdCrossing(bool isRoundStart);

    void UpdateScrQualificationTracking();

    // Arena.
    float ConfiguredCaptureSpeedMultiplierPerPlayer { get; }

    int ArenaRedAliveCount { get; }

    int ArenaBlueAliveCount { get; }

    int ArenaRedPlayerCount { get; }

    int ArenaBluePlayerCount { get; }

    int CountPlayersInArenaCaptureZone(PlayerTeam team);

    // Control point, KOTH, VIP and generator.
    void UpdateControlPointState();

    void UpdateControlPointSetupGates();

    void ApplyControlPointSetupMatchRules();

    void UpdateKothState();

    void AdvanceKothMatchStateCore();

    bool ShouldDeferVipObjectiveResolution();

    void UpdateGeneratorState();
}

/// <summary>
/// Picks the objective and resolution rules for the active game mode.
/// Owns no match state itself; state lives in <see cref="ObjectiveStateStore"/> and the host.
/// </summary>
internal sealed class MatchObjectiveSystem
{
    private readonly IMatchObjectiveHost _host;
    private readonly ArenaUpdateController _arenaUpdate;
    private readonly ArenaResolutionController _arenaResolution;
    private readonly ControlPointResolutionController _controlPointResolution;
    private readonly CaptureTheFlagUpdateController _captureTheFlagUpdate;
    private readonly CaptureTheFlagResolutionController _captureTheFlagResolution;
    private readonly ScoreLimitResolutionController _scoreLimitResolution;
    private readonly ScrObjectiveController _scrObjectives;
    private readonly ScrResolutionController _scrResolution;

    public MatchObjectiveSystem(IMatchObjectiveHost host)
    {
        _host = host;
        _arenaUpdate = new ArenaUpdateController(host);
        _arenaResolution = new ArenaResolutionController(host);
        _controlPointResolution = new ControlPointResolutionController(host);
        _captureTheFlagUpdate = new CaptureTheFlagUpdateController(host);
        _captureTheFlagResolution = new CaptureTheFlagResolutionController(host);
        _scoreLimitResolution = new ScoreLimitResolutionController(host);
        _scrObjectives = new ScrObjectiveController(host);
        _scrResolution = new ScrResolutionController(host);
    }

    public void AdvanceObjectives()
    {
        if (_host.CompetitiveObjectivesLocked)
        {
            return;
        }

        switch (_host.MatchRules.Mode)
        {
            case GameModeKind.Arena:
                _arenaUpdate.AdvanceObjectives();
                break;
            case GameModeKind.ControlPoint:
            case GameModeKind.Vip:
                _host.UpdateControlPointState();
                break;
            case GameModeKind.KingOfTheHill:
            case GameModeKind.DoubleKingOfTheHill:
                _host.UpdateControlPointState();
                _host.UpdateKothState();
                break;
            case GameModeKind.Generator:
                _host.UpdateGeneratorState();
                break;
            case GameModeKind.TeamDeathmatch:
                break;
            case GameModeKind.Scr:
                _scrObjectives.AdvanceObjectives();
                break;
            default:
                _captureTheFlagUpdate.AdvanceObjectives();
                break;
        }
    }

    public void AdvanceResolution()
    {
        if (_host.CompetitiveObjectivesLocked)
        {
            return;
        }

        switch (_host.MatchRules.Mode)
        {
            case GameModeKind.Arena:
                _arenaResolution.AdvanceResolution();
                break;
            case GameModeKind.ControlPoint:
            case GameModeKind.Vip:
                _controlPointResolution.AdvanceResolution();
                break;
            case GameModeKind.KingOfTheHill:
            case GameModeKind.DoubleKingOfTheHill:
                _host.AdvanceKothMatchStateCore();
                break;
            case GameModeKind.Generator:
            case GameModeKind.TeamDeathmatch:
                _scoreLimitResolution.AdvanceResolution();
                break;
            case GameModeKind.Scr:
                _scrResolution.AdvanceResolution();
                break;
            default:
                _captureTheFlagResolution.AdvanceResolution();
                break;
        }
    }
}
