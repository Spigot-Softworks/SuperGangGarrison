namespace OpenGarrison.Core;

/// <summary>
/// Spawning: spawn-point selection and reservation, forward spawns, control-point,
/// intel and last-to-die spawn placement, resolving a player into the world, and
/// round-start respawns.
/// </summary>
internal sealed partial class SpawnSystem
{
    private readonly ISpawnHost _host;

    public SpawnSystem(ISpawnHost host)
    {
        _host = host;
    }
}
