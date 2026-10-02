namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    public void SpawnClientBloodFromDamage(float x, float y, int damageAmount)
        => PlayerRemains.SpawnClientBloodFromDamage(x, y, damageAmount);
    public void SpawnClientPlayerGibsFromNetworkDeath(PlayerEntity player, float? spawnX = null, float? spawnY = null)
        => PlayerRemains.SpawnClientPlayerGibsFromNetworkDeath(player, spawnX, spawnY);
}
