using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

// Thin forwarders so remaining world code keeps its call shape; callers should migrate to the system directly over time.
public sealed partial class SimulationWorld
{
    public int GetHealthPackSpawnRespawnTicksRemaining(int spawnIndex) => Pickups.GetHealthPackSpawnRespawnTicksRemaining(spawnIndex);
}
