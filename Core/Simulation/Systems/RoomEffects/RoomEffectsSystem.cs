namespace OpenGarrison.Core;

/// <summary>
/// Room effects applied during the per-player tick: healing cabinets, spawn-room
/// state and room hazards.
/// </summary>
internal sealed partial class RoomEffectsSystem
{
    private readonly IRoomEffectsHost _host;

    public RoomEffectsSystem(IRoomEffectsHost host)
    {
        _host = host;
    }
}
