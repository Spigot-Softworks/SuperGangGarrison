namespace OpenGarrison.Core;

/// <summary>
/// Owns the per-mode objective state that persists across ticks: the arena capture
/// point, control points, king-of-the-hill timers, and score-limit qualification.
/// The runtime objective controllers advance this state; <see cref="SimulationWorld"/>
/// only exposes read-only views of it.
/// </summary>
internal sealed class ObjectiveStateStore
{
    public ArenaObjectiveState Arena { get; } = new();
    public ControlPointObjectiveState ControlPoints { get; } = new();
    public KothObjectiveState Koth { get; } = new();
    public ScrObjectiveState Scr { get; } = new();
}

/// <summary>Capture-point and win-streak state for the arena mode.</summary>
internal sealed class ArenaObjectiveState
{
    /// <summary>Ticks of uncontested capturing needed to take the arena point.</summary>
    public const int PointCapTimeTicksDefault = 300;

    public PlayerTeam? PointTeam { get; set; }
    public PlayerTeam? CappingTeam { get; set; }
    public float CappingTicks { get; set; }
    public int Cappers { get; set; }
    public int UnlockTicksRemaining { get; set; }
    public int RedConsecutiveWins { get; set; }
    public int BlueConsecutiveWins { get; set; }

    /// <summary>Clears the capture in progress and re-arms the unlock timer.</summary>
    public void ResetForNewRound(int unlockTicksRemaining)
    {
        PointTeam = null;
        CappingTeam = null;
        CappingTicks = 0f;
        Cappers = 0;
        UnlockTicksRemaining = unlockTicksRemaining;
    }

    /// <summary>Clears the consecutive win counters.</summary>
    public void ResetWinStreaks()
    {
        RedConsecutiveWins = 0;
        BlueConsecutiveWins = 0;
    }

    /// <summary>Records a round win for <paramref name="winner"/> and resets the other streak.</summary>
    public void RecordRoundWin(PlayerTeam winner)
    {
        if (winner == PlayerTeam.Red)
        {
            RedConsecutiveWins += 1;
            BlueConsecutiveWins = 0;
        }
        else
        {
            BlueConsecutiveWins += 1;
            RedConsecutiveWins = 0;
        }
    }
}

/// <summary>A map marker that contributes to the capture of one control point.</summary>
internal sealed record ControlPointZone(RoomObjectMarker Marker, int ControlPointIndex);

/// <summary>Points, capture zones, and setup phase for control-point style modes.</summary>
internal sealed class ControlPointObjectiveState
{
    public List<ControlPointState> Points { get; } = new();
    public List<ControlPointZone> Zones { get; } = new();
    public bool SetupMode { get; set; }
    public int SetupTicksRemaining { get; set; }

    /// <summary>Removes all points and zones and ends the setup phase.</summary>
    public void Clear()
    {
        Points.Clear();
        Zones.Clear();
        SetupMode = false;
        SetupTicksRemaining = 0;
    }
}

/// <summary>Team timers and the unlock countdown for king-of-the-hill modes.</summary>
internal sealed class KothObjectiveState
{
    public int RedTimerTicksRemaining { get; set; }
    public int BlueTimerTicksRemaining { get; set; }
    public int UnlockTicksRemaining { get; set; }

    /// <summary>Zeroes both team timers and the unlock countdown.</summary>
    public void Clear()
    {
        RedTimerTicksRemaining = 0;
        BlueTimerTicksRemaining = 0;
        UnlockTicksRemaining = 0;
    }
}

/// <summary>Whether each team has already met the score-limit qualification this round.</summary>
internal sealed class ScrObjectiveState
{
    public bool RedWasQualified { get; set; }
    public bool BlueWasQualified { get; set; }
}
