namespace OpenGarrison.Core;

/// <summary>
/// Player input and the per-player tick: routing primary, secondary and utility
/// actions (weapons, abilities, building, taunts), and advancing a living player
/// through movement, room effects, damage-over-time and pending actions. Slow-phase
/// tracing lives here too.
/// </summary>
internal sealed partial class PlayerInputSystem
{
    private readonly IPlayerInputHost _host;

    public PlayerInputSystem(IPlayerInputHost host)
    {
        _host = host;
    }
}
