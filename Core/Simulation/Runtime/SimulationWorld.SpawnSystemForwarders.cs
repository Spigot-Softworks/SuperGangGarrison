namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    public bool TryConfigureNetworkPlayerLastToDieEnemySpawn(byte slot, PlayerTeam spawnSide, bool repositionAlivePlayer)
        => Spawns.TryConfigureNetworkPlayerLastToDieEnemySpawn(slot, spawnSide, repositionAlivePlayer);
    public bool TryMoveLocalPlayerToControlPointSpawn()
        => Spawns.TryMoveLocalPlayerToControlPointSpawn();
    public bool TryMoveLocalPlayerToIntelSpawn()
        => Spawns.TryMoveLocalPlayerToIntelSpawn();
    public bool TryMoveNetworkPlayerToLastToDieEnemySpawn(byte slot, PlayerTeam spawnSide)
        => Spawns.TryMoveNetworkPlayerToLastToDieEnemySpawn(slot, spawnSide);
    public bool TryMoveNetworkPlayerToLastToDieObjectiveSpawn(byte slot)
        => Spawns.TryMoveNetworkPlayerToLastToDieObjectiveSpawn(slot);
}
