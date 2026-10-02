namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const int CompetitiveReadyCountdownSeconds = 3;
    private const int DefaultCompetitiveSetupSeconds = 10;
    private const int MaximumCompetitiveSetupSeconds = 120;


    public bool CompetitiveReadyUpEnabled => ReadyUpState.Enabled;

    public int CompetitiveSetupSeconds => ReadyUpState.SetupSeconds;

    public CompetitiveReadyUpPhase CompetitiveReadyUpPhase => ReadyUpState.Phase;

    public int CompetitiveReadyUpTicksRemaining => ReadyUpState.TicksRemaining;

    public bool CompetitiveObjectivesLocked =>
        ReadyUpState.Phase is CompetitiveReadyUpPhase.Skirmish
            or CompetitiveReadyUpPhase.Countdown
            or CompetitiveReadyUpPhase.Setup;

    public bool IsNetworkPlayerReady(byte slot)
    {
        return ReadyUpState.ReadySlots.Contains(slot);
    }

    public void SetCompetitiveReadyUpEnabled(bool enabled)
    {
        if (ReadyUpState.Enabled == enabled)
        {
            return;
        }

        ReadyUpState.Enabled = enabled;
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
        ReadyUpState.SetupSeconds = Math.Clamp(seconds, 0, MaximumCompetitiveSetupSeconds);
        if (ReadyUpState.Phase == CompetitiveReadyUpPhase.Setup)
        {
            ReadyUpState.TicksRemaining = Math.Min(
                ReadyUpState.TicksRemaining,
                GetCompetitiveSetupDurationTicks());
        }
    }

    public bool TrySetNetworkPlayerReady(byte slot, bool ready)
    {
        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        if (!ReadyUpState.Enabled
            || ReadyUpState.Phase is not (CompetitiveReadyUpPhase.Skirmish or CompetitiveReadyUpPhase.Countdown))
        {
            ReadyUpState.ReadySlots.Remove(slot);
            return false;
        }

        if (ready)
        {
            ReadyUpState.ReadySlots.Add(slot);
        }
        else
        {
            ReadyUpState.ReadySlots.Remove(slot);
        }

        return true;
    }

    public bool TryToggleNetworkPlayerReady(byte slot)
    {
        if (!ReadyUpState.ReadySlots.Contains(slot))
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
            ReadyUpState.ReadySlots.Add(slot);
        }
        else
        {
            ReadyUpState.ReadySlots.Remove(slot);
        }
    }

    public void AdvanceCompetitiveReadyUp(IReadOnlyCollection<byte> playableSlots)
    {
        if (!ReadyUpState.Enabled)
        {
            return;
        }

        PruneReadyPlayers(playableSlots);

        switch (ReadyUpState.Phase)
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

                ReadyUpState.TicksRemaining -= 1;
                if (ReadyUpState.TicksRemaining <= 0)
                {
                    BeginCompetitiveSetup();
                }
                break;
            case CompetitiveReadyUpPhase.Setup:
                ReadyUpState.TicksRemaining -= 1;
                if (ReadyUpState.TicksRemaining <= 0)
                {
                    BeginCompetitiveLive();
                }
                break;
        }
    }

    private void BeginCompetitiveSkirmish(bool clearReadyPlayers)
    {
        if (!ReadyUpState.Enabled)
        {
            return;
        }

        ReadyUpState.Phase = CompetitiveReadyUpPhase.Skirmish;
        ReadyUpState.TicksRemaining = 0;
        Level.ForcedBlockingTeamGates = TeamGateLockMask.None;
        if (clearReadyPlayers)
        {
            ReadyUpState.ReadySlots.Clear();
        }

        SuspendObjectiveSetupTimersForCompetitiveHold();
    }

    private void BeginCompetitiveCountdown()
    {
        ReadyUpState.Phase = CompetitiveReadyUpPhase.Countdown;
        ReadyUpState.TicksRemaining = Math.Max(1, Config.TicksPerSecond * CompetitiveReadyCountdownSeconds);
        Level.ForcedBlockingTeamGates = TeamGateLockMask.None;
    }

    private void BeginCompetitiveSetup()
    {
        ReadyUpState.SuppressSkirmishOnNextRoundRestart = true;
        try
        {
            RestartCurrentRound(preservePlayerStats: false, enterCompetitiveSkirmish: false);
        }
        finally
        {
            ReadyUpState.SuppressSkirmishOnNextRoundRestart = false;
        }

        ReadyUpState.Phase = CompetitiveReadyUpPhase.Setup;
        ReadyUpState.TicksRemaining = GetCompetitiveSetupDurationTicks();
        Level.ForcedBlockingTeamGates = TeamGateLockMask.Red | TeamGateLockMask.Blue;
        SuspendObjectiveSetupTimersForCompetitiveHold();

        if (ReadyUpState.TicksRemaining <= 0)
        {
            BeginCompetitiveLive();
        }
    }

    private void BeginCompetitiveLive()
    {
        Level.ForcedBlockingTeamGates = TeamGateLockMask.None;
        ResetModeStateForNewRound();
        ReadyUpState.ReadySlots.Clear();
        ReadyUpState.Phase = CompetitiveReadyUpPhase.Live;
        ReadyUpState.TicksRemaining = 0;
    }

    private void ClearCompetitiveReadyUpState()
    {
        ReadyUpState.Phase = CompetitiveReadyUpPhase.Disabled;
        ReadyUpState.TicksRemaining = 0;
        ReadyUpState.ReadySlots.Clear();
        Level.ForcedBlockingTeamGates = TeamGateLockMask.None;
    }

    private void SuspendObjectiveSetupTimersForCompetitiveHold()
    {
        if (Objectives.ControlPoints.SetupMode)
        {
            Objectives.ControlPoints.SetupTicksRemaining = 0;
            UpdateControlPointSetupGates();
        }

        Objectives.Arena.UnlockTicksRemaining = 0;
        Objectives.Koth.UnlockTicksRemaining = 0;
    }

    private int GetCompetitiveSetupDurationTicks()
    {
        return Math.Max(0, ReadyUpState.SetupSeconds * Config.TicksPerSecond);
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
            if (ReadyUpState.ReadySlots.Contains(slot))
            {
                readyCount += 1;
            }
        }

        return readyCount > playerCount / 2;
    }

    private void PruneReadyPlayers(IReadOnlyCollection<byte> playableSlots)
    {
        if (ReadyUpState.ReadySlots.Count == 0)
        {
            return;
        }

        ReadyUpState.ReadySlots.RemoveWhere(slot => !playableSlots.Contains(slot));
    }

    private void ApplySnapshotCompetitiveReadyUp(byte phase, int ticksRemaining)
    {
        ReadyUpState.Phase = Enum.IsDefined(typeof(CompetitiveReadyUpPhase), phase)
            ? (CompetitiveReadyUpPhase)phase
            : CompetitiveReadyUpPhase.Disabled;
        ReadyUpState.TicksRemaining = Math.Max(0, ticksRemaining);
        Level.ForcedBlockingTeamGates = ReadyUpState.Phase == CompetitiveReadyUpPhase.Setup
            ? TeamGateLockMask.Red | TeamGateLockMask.Blue
            : TeamGateLockMask.None;
    }
}
