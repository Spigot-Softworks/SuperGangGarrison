namespace OpenGarrison.Core;

public sealed class ControlPointState
{
    public ControlPointState(int index, RoomObjectMarker marker)
    {
        Index = index;
        Marker = marker;
    }

    public int Index { get; }

    public RoomObjectMarker Marker { get; }

    public PlayerTeam? Team { get; set; }

    public PlayerTeam? CappingTeam { get; set; }

    public float CappingTicks { get; set; }

    public int CapTimeTicks { get; set; }

    public int RedCappers { get; set; }

    public int BlueCappers { get; set; }

    public int Cappers { get; set; }

    public bool IsLocked { get; set; }
}
