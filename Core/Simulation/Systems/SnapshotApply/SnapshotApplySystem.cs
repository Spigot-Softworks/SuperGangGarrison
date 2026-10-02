namespace OpenGarrison.Core;

/// <summary>
/// Client-side snapshot application: applying a server snapshot (players, world and
/// objective state, structures, projectiles, remains, transient entities and event
/// queues) and protocol 64 player and projectile state. The server-side builder is
/// <see cref="SnapshotSystem"/>.
/// </summary>
internal sealed partial class SnapshotApplySystem
{
    private readonly ISnapshotApplyHost _host;

    public SnapshotApplySystem(ISnapshotApplyHost host)
    {
        _host = host;
    }
}
