namespace OpenGarrison.Core;

internal sealed partial class ReadyUpSystem
{
    private const int CompetitiveReadyCountdownSeconds = 3;
    internal const int DefaultCompetitiveSetupSeconds = 10;
    private const int MaximumCompetitiveSetupSeconds = 120;


    internal bool CompetitiveReadyUpEnabled => _host.ReadyUpState.Enabled;

    internal int CompetitiveSetupSeconds => _host.ReadyUpState.SetupSeconds;

    internal CompetitiveReadyUpPhase CompetitiveReadyUpPhase => _host.ReadyUpState.Phase;

    internal int CompetitiveReadyUpTicksRemaining => _host.ReadyUpState.TicksRemaining;

    internal bool CompetitiveObjectivesLocked =>
        _host.ReadyUpState.Phase is CompetitiveReadyUpPhase.Skirmish
            or CompetitiveReadyUpPhase.Countdown
            or CompetitiveReadyUpPhase.Setup;

    internal bool IsNetworkPlayerReady(byte slot)
    {
        return _host.ReadyUpState.ReadySlots.Contains(slot);
    }

    internal void SetCompetitiveReadyUpEnabled(bool enabled)
    {
        if (_host.ReadyUpState.Enabled == enabled)
        {
            return;
        }

        _host.ReadyUpState.Enabled = enabled;
        if (enabled)
        {
            _host.MapLifecycle.RestartCurrentRound(preservePlayerStats: false);
            return;
        }

        var wasLocked = CompetitiveObjectivesLocked;
        ClearCompetitiveReadyUpState();
        if (wasLocked)
        {
            _host.MapLifecycle.RestartCurrentRound(preservePlayerStats: false, enterCompetitiveSkirmish: false);
        }
    }

    internal void SetCompetitiveSetupSeconds(int seconds)
    {
        _host.ReadyUpState.SetupSeconds = Math.Clamp(seconds, 0, MaximumCompetitiveSetupSeconds);
        if (_host.ReadyUpState.Phase == CompetitiveReadyUpPhase.Setup)
        {
            _host.ReadyUpState.TicksRemaining = Math.Min(
                _host.ReadyUpState.TicksRemaining,
                GetCompetitiveSetupDurationTicks());
        }
    }

    internal bool TrySetNetworkPlayerReady(byte slot, bool ready)
    {
        if (!NetworkPlayerSystem.IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        if (!_host.ReadyUpState.Enabled
            || _host.ReadyUpState.Phase is not (CompetitiveReadyUpPhase.Skirmish or CompetitiveReadyUpPhase.Countdown))
        {
            _host.ReadyUpState.ReadySlots.Remove(slot);
            return false;
        }

        if (ready)
        {
            _host.ReadyUpState.ReadySlots.Add(slot);
        }
        else
        {
            _host.ReadyUpState.ReadySlots.Remove(slot);
        }

        return true;
    }

    internal bool TryToggleNetworkPlayerReady(byte slot)
    {
        if (!_host.ReadyUpState.ReadySlots.Contains(slot))
        {
            return TrySetNetworkPlayerReady(slot, ready: true);
        }

        return TrySetNetworkPlayerReady(slot, ready: false);
    }

    internal void ApplySnapshotNetworkPlayerReady(byte slot, bool ready)
    {
        if (!NetworkPlayerSystem.IsPlayableNetworkPlayerSlot(slot))
        {
            return;
        }

        if (ready)
        {
            _host.ReadyUpState.ReadySlots.Add(slot);
        }
        else
        {
            _host.ReadyUpState.ReadySlots.Remove(slot);
        }
    }

    internal void AdvanceCompetitiveReadyUp(IReadOnlyCollection<byte> playableSlots)
    {
        if (!_host.ReadyUpState.Enabled)
        {
            return;
        }

        PruneReadyPlayers(playableSlots);

        switch (_host.ReadyUpState.Phase)
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

                _host.ReadyUpState.TicksRemaining -= 1;
                if (_host.ReadyUpState.TicksRemaining <= 0)
                {
                    BeginCompetitiveSetup();
                }
                break;
            case CompetitiveReadyUpPhase.Setup:
                _host.ReadyUpState.TicksRemaining -= 1;
                if (_host.ReadyUpState.TicksRemaining <= 0)
                {
                    BeginCompetitiveLive();
                }
                break;
        }
    }

    internal void BeginCompetitiveSkirmish(bool clearReadyPlayers)
    {
        if (!_host.ReadyUpState.Enabled)
        {
            return;
        }

        _host.ReadyUpState.Phase = CompetitiveReadyUpPhase.Skirmish;
        _host.ReadyUpState.TicksRemaining = 0;
        _host.Level.ForcedBlockingTeamGates = TeamGateLockMask.None;
        if (clearReadyPlayers)
        {
            _host.ReadyUpState.ReadySlots.Clear();
        }

        SuspendObjectiveSetupTimersForCompetitiveHold();
    }

    private void BeginCompetitiveCountdown()
    {
        _host.ReadyUpState.Phase = CompetitiveReadyUpPhase.Countdown;
        _host.ReadyUpState.TicksRemaining = Math.Max(1, _host.Config.TicksPerSecond * CompetitiveReadyCountdownSeconds);
        _host.Level.ForcedBlockingTeamGates = TeamGateLockMask.None;
    }

    private void BeginCompetitiveSetup()
    {
        _host.ReadyUpState.SuppressSkirmishOnNextRoundRestart = true;
        try
        {
            _host.MapLifecycle.RestartCurrentRound(preservePlayerStats: false, enterCompetitiveSkirmish: false);
        }
        finally
        {
            _host.ReadyUpState.SuppressSkirmishOnNextRoundRestart = false;
        }

        _host.ReadyUpState.Phase = CompetitiveReadyUpPhase.Setup;
        _host.ReadyUpState.TicksRemaining = GetCompetitiveSetupDurationTicks();
        _host.Level.ForcedBlockingTeamGates = TeamGateLockMask.Red | TeamGateLockMask.Blue;
        SuspendObjectiveSetupTimersForCompetitiveHold();

        if (_host.ReadyUpState.TicksRemaining <= 0)
        {
            BeginCompetitiveLive();
        }
    }

    private void BeginCompetitiveLive()
    {
        _host.Level.ForcedBlockingTeamGates = TeamGateLockMask.None;
        _host.MapLifecycle.ResetModeStateForNewRound();
        _host.ReadyUpState.ReadySlots.Clear();
        _host.ReadyUpState.Phase = CompetitiveReadyUpPhase.Live;
        _host.ReadyUpState.TicksRemaining = 0;
    }

    private void ClearCompetitiveReadyUpState()
    {
        _host.ReadyUpState.Phase = CompetitiveReadyUpPhase.Disabled;
        _host.ReadyUpState.TicksRemaining = 0;
        _host.ReadyUpState.ReadySlots.Clear();
        _host.Level.ForcedBlockingTeamGates = TeamGateLockMask.None;
    }

    private void SuspendObjectiveSetupTimersForCompetitiveHold()
    {
        if (_host.Objectives.ControlPoints.SetupMode)
        {
            _host.Objectives.ControlPoints.SetupTicksRemaining = 0;
            _host.ObjectiveRules.UpdateControlPointSetupGates();
        }

        _host.Objectives.Arena.UnlockTicksRemaining = 0;
        _host.Objectives.Koth.UnlockTicksRemaining = 0;
    }

    private int GetCompetitiveSetupDurationTicks()
    {
        return Math.Max(0, _host.ReadyUpState.SetupSeconds * _host.Config.TicksPerSecond);
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
            if (_host.ReadyUpState.ReadySlots.Contains(slot))
            {
                readyCount += 1;
            }
        }

        return readyCount > playerCount / 2;
    }

    private void PruneReadyPlayers(IReadOnlyCollection<byte> playableSlots)
    {
        if (_host.ReadyUpState.ReadySlots.Count == 0)
        {
            return;
        }

        _host.ReadyUpState.ReadySlots.RemoveWhere(slot => !playableSlots.Contains(slot));
    }

    internal void ApplySnapshotCompetitiveReadyUp(byte phase, int ticksRemaining)
    {
        _host.ReadyUpState.Phase = Enum.IsDefined(typeof(CompetitiveReadyUpPhase), phase)
            ? (CompetitiveReadyUpPhase)phase
            : CompetitiveReadyUpPhase.Disabled;
        _host.ReadyUpState.TicksRemaining = Math.Max(0, ticksRemaining);
        _host.Level.ForcedBlockingTeamGates = _host.ReadyUpState.Phase == CompetitiveReadyUpPhase.Setup
            ? TeamGateLockMask.Red | TeamGateLockMask.Blue
            : TeamGateLockMask.None;
    }
}
