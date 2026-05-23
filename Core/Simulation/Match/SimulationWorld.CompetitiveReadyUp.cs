namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const int CompetitiveReadyCountdownSeconds = 3;
    private const int DefaultCompetitiveSetupSeconds = 10;
    private const int MaximumCompetitiveSetupSeconds = 120;

    private readonly HashSet<byte> _readyNetworkPlayerSlots = new();
    private bool _competitiveReadyUpEnabled;
    private int _competitiveSetupSeconds = DefaultCompetitiveSetupSeconds;
    private CompetitiveReadyUpPhase _competitiveReadyUpPhase = CompetitiveReadyUpPhase.Disabled;
    private int _competitiveReadyUpTicksRemaining;
    private bool _suppressCompetitiveSkirmishOnNextRoundRestart;

    public bool CompetitiveReadyUpEnabled => _competitiveReadyUpEnabled;

    public int CompetitiveSetupSeconds => _competitiveSetupSeconds;

    public CompetitiveReadyUpPhase CompetitiveReadyUpPhase => _competitiveReadyUpPhase;

    public int CompetitiveReadyUpTicksRemaining => _competitiveReadyUpTicksRemaining;

    public bool CompetitiveObjectivesLocked =>
        _competitiveReadyUpPhase is CompetitiveReadyUpPhase.Skirmish
            or CompetitiveReadyUpPhase.Countdown
            or CompetitiveReadyUpPhase.Setup;

    public bool IsNetworkPlayerReady(byte slot)
    {
        return _readyNetworkPlayerSlots.Contains(slot);
    }

    public void SetCompetitiveReadyUpEnabled(bool enabled)
    {
        if (_competitiveReadyUpEnabled == enabled)
        {
            return;
        }

        _competitiveReadyUpEnabled = enabled;
        if (enabled)
        {
            RestartCurrentRound(preservePlayerStats: false);
            return;
        }

        var wasLocked = CompetitiveObjectivesLocked;
        ClearCompetitiveReadyUpState();
        if (wasLocked)
        {
            RestartCurrentRound(preservePlayerStats: false, enterCompetitiveSkirmish: false);
        }
    }

    public void SetCompetitiveSetupSeconds(int seconds)
    {
        _competitiveSetupSeconds = Math.Clamp(seconds, 0, MaximumCompetitiveSetupSeconds);
        if (_competitiveReadyUpPhase == CompetitiveReadyUpPhase.Setup)
        {
            _competitiveReadyUpTicksRemaining = Math.Min(
                _competitiveReadyUpTicksRemaining,
                GetCompetitiveSetupDurationTicks());
        }
    }

    public bool TrySetNetworkPlayerReady(byte slot, bool ready)
    {
        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        if (!_competitiveReadyUpEnabled
            || _competitiveReadyUpPhase is not (CompetitiveReadyUpPhase.Skirmish or CompetitiveReadyUpPhase.Countdown))
        {
            _readyNetworkPlayerSlots.Remove(slot);
            return false;
        }

        if (ready)
        {
            _readyNetworkPlayerSlots.Add(slot);
        }
        else
        {
            _readyNetworkPlayerSlots.Remove(slot);
        }

        return true;
    }

    public bool TryToggleNetworkPlayerReady(byte slot)
    {
        if (!_readyNetworkPlayerSlots.Contains(slot))
        {
            return TrySetNetworkPlayerReady(slot, ready: true);
        }

        return TrySetNetworkPlayerReady(slot, ready: false);
    }

    private void ApplySnapshotNetworkPlayerReady(byte slot, bool ready)
    {
        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return;
        }

        if (ready)
        {
            _readyNetworkPlayerSlots.Add(slot);
        }
        else
        {
            _readyNetworkPlayerSlots.Remove(slot);
        }
    }

    public void AdvanceCompetitiveReadyUp(IReadOnlyCollection<byte> playableSlots)
    {
        if (!_competitiveReadyUpEnabled)
        {
            return;
        }

        PruneReadyPlayers(playableSlots);

        switch (_competitiveReadyUpPhase)
        {
            case CompetitiveReadyUpPhase.Skirmish:
                if (HasReadyMajority(playableSlots))
                {
                    BeginCompetitiveCountdown();
                }
                break;
            case CompetitiveReadyUpPhase.Countdown:
                if (!HasReadyMajority(playableSlots))
                {
                    BeginCompetitiveSkirmish(clearReadyPlayers: false);
                    return;
                }

                _competitiveReadyUpTicksRemaining -= 1;
                if (_competitiveReadyUpTicksRemaining <= 0)
                {
                    BeginCompetitiveSetup();
                }
                break;
            case CompetitiveReadyUpPhase.Setup:
                _competitiveReadyUpTicksRemaining -= 1;
                if (_competitiveReadyUpTicksRemaining <= 0)
                {
                    BeginCompetitiveLive();
                }
                break;
        }
    }

    private void BeginCompetitiveSkirmish(bool clearReadyPlayers)
    {
        if (!_competitiveReadyUpEnabled)
        {
            return;
        }

        _competitiveReadyUpPhase = CompetitiveReadyUpPhase.Skirmish;
        _competitiveReadyUpTicksRemaining = 0;
        Level.ForcedBlockingTeamGates = TeamGateLockMask.None;
        if (clearReadyPlayers)
        {
            _readyNetworkPlayerSlots.Clear();
        }

        SuspendObjectiveSetupTimersForCompetitiveHold();
    }

    private void BeginCompetitiveCountdown()
    {
        _competitiveReadyUpPhase = CompetitiveReadyUpPhase.Countdown;
        _competitiveReadyUpTicksRemaining = Math.Max(1, Config.TicksPerSecond * CompetitiveReadyCountdownSeconds);
        Level.ForcedBlockingTeamGates = TeamGateLockMask.None;
    }

    private void BeginCompetitiveSetup()
    {
        _suppressCompetitiveSkirmishOnNextRoundRestart = true;
        try
        {
            RestartCurrentRound(preservePlayerStats: false, enterCompetitiveSkirmish: false);
        }
        finally
        {
            _suppressCompetitiveSkirmishOnNextRoundRestart = false;
        }

        _competitiveReadyUpPhase = CompetitiveReadyUpPhase.Setup;
        _competitiveReadyUpTicksRemaining = GetCompetitiveSetupDurationTicks();
        Level.ForcedBlockingTeamGates = TeamGateLockMask.Red | TeamGateLockMask.Blue;
        SuspendObjectiveSetupTimersForCompetitiveHold();

        if (_competitiveReadyUpTicksRemaining <= 0)
        {
            BeginCompetitiveLive();
        }
    }

    private void BeginCompetitiveLive()
    {
        Level.ForcedBlockingTeamGates = TeamGateLockMask.None;
        ResetModeStateForNewRound();
        _readyNetworkPlayerSlots.Clear();
        _competitiveReadyUpPhase = CompetitiveReadyUpPhase.Live;
        _competitiveReadyUpTicksRemaining = 0;
    }

    private void ClearCompetitiveReadyUpState()
    {
        _competitiveReadyUpPhase = CompetitiveReadyUpPhase.Disabled;
        _competitiveReadyUpTicksRemaining = 0;
        _readyNetworkPlayerSlots.Clear();
        Level.ForcedBlockingTeamGates = TeamGateLockMask.None;
    }

    private void SuspendObjectiveSetupTimersForCompetitiveHold()
    {
        if (_controlPointSetupMode)
        {
            _controlPointSetupTicksRemaining = 0;
            UpdateControlPointSetupGates();
        }

        _arenaUnlockTicksRemaining = 0;
        _kothUnlockTicksRemaining = 0;
    }

    private int GetCompetitiveSetupDurationTicks()
    {
        return Math.Max(0, _competitiveSetupSeconds * Config.TicksPerSecond);
    }

    private bool HasReadyMajority(IReadOnlyCollection<byte> playableSlots)
    {
        var playerCount = playableSlots.Count;
        if (playerCount <= 0)
        {
            return false;
        }

        var readyCount = 0;
        foreach (var slot in playableSlots)
        {
            if (_readyNetworkPlayerSlots.Contains(slot))
            {
                readyCount += 1;
            }
        }

        return readyCount > playerCount / 2;
    }

    private void PruneReadyPlayers(IReadOnlyCollection<byte> playableSlots)
    {
        if (_readyNetworkPlayerSlots.Count == 0)
        {
            return;
        }

        _readyNetworkPlayerSlots.RemoveWhere(slot => !playableSlots.Contains(slot));
    }

    private void ApplySnapshotCompetitiveReadyUp(byte phase, int ticksRemaining)
    {
        _competitiveReadyUpPhase = Enum.IsDefined(typeof(CompetitiveReadyUpPhase), phase)
            ? (CompetitiveReadyUpPhase)phase
            : CompetitiveReadyUpPhase.Disabled;
        _competitiveReadyUpTicksRemaining = Math.Max(0, ticksRemaining);
        Level.ForcedBlockingTeamGates = _competitiveReadyUpPhase == CompetitiveReadyUpPhase.Setup
            ? TeamGateLockMask.Red | TeamGateLockMask.Blue
            : TeamGateLockMask.None;
    }
}
