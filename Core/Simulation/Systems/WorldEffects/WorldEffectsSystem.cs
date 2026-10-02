namespace OpenGarrison.Core;

/// <summary>
/// World presentation events raised by the simulation: visual effects, blood,
/// stuck arrows, intel trails, wallspin dust, combat traces, sniper aim indicators
/// and sound events. The events are stored on the world's event stores; this
/// system decides what to emit.
/// </summary>
internal sealed partial class WorldEffectsSystem
{
    private readonly IWorldEffectsHost _host;

    public WorldEffectsSystem(IWorldEffectsHost host)
    {
        _host = host;
    }
}
