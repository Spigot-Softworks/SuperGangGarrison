using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    public bool ApplySnapshot(SnapshotMessage snapshot, byte localPlayerSlot = 1)
        => SnapshotApply.ApplySnapshot(snapshot, localPlayerSlot);
    public bool TryPresentNetworkGibDeath(int playerId, int gibDeaths, float? spawnX = null, float? spawnY = null)
        => SnapshotApply.TryPresentNetworkGibDeath(playerId, gibDeaths, spawnX, spawnY);
    public bool ApplyProtocol64PlayerState(Protocol64PlayerState state, byte? clientLocalPlayerSlot = null)
        => SnapshotApply.ApplyProtocol64PlayerState(state, clientLocalPlayerSlot);
    public bool ApplyProtocol64ProjectileState(Protocol64ProjectileState state, byte? clientLocalPlayerSlot = null)
        => SnapshotApply.ApplyProtocol64ProjectileState(state, clientLocalPlayerSlot);
    public void ReconcileRemoteLastToDieDemoknightPresentation(IReadOnlySet<byte> demoknightServerSlots)
        => SnapshotApply.ReconcileRemoteLastToDieDemoknightPresentation(demoknightServerSlots);
    public bool RemoveProtocol64Player(Protocol64PlayerIdentity identity, byte? clientLocalPlayerSlot = null)
        => SnapshotApply.RemoveProtocol64Player(identity, clientLocalPlayerSlot);
    public bool RemoveProtocol64Projectile(ulong entityId)
        => SnapshotApply.RemoveProtocol64Projectile(entityId);
    private bool RemoveProtocol64Projectile(int entityId)
        => SnapshotApply.RemoveProtocol64Projectile(entityId);
    public void ResetProtocol64ClientLocalPlayer()
        => SnapshotApply.ResetProtocol64ClientLocalPlayer();
}
