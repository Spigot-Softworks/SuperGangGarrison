using System;

namespace OpenGarrison.Core;

public sealed class ControlPointState
{
    public ControlPointState(int index, RoomObjectMarker marker)
    {
        Index = index;
        Marker = marker;
        HealingAuraCenterX = marker.CenterX;
        HealingAuraCenterY = marker.CenterY;
        HealingAuraWidth = Math.Max(48f, marker.Width * 1.4f);
        HealingAuraHeight = Math.Max(28f, marker.Height * 1.25f);
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

    public bool HasHealingAura { get; set; }

    public float HealingAuraCenterX { get; set; }

    public float HealingAuraCenterY { get; set; }

    public float HealingAuraWidth { get; set; }

    public float HealingAuraHeight { get; set; }
}
