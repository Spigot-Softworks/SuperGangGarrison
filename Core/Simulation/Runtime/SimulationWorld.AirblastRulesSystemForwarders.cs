namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    private const float PyroAirblastDistance = SimulationConstants.PyroAirblastDistance;
    private const float PyroAirblastMineSpeedFloor = SimulationMath.PyroAirblastMineSpeedFloor;
}
