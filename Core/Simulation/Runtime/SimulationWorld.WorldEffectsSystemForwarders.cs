namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    private void RegisterVisualEffect(string effectName, float x, float y, float directionDegrees = 0f, int count = 1, bool normalizeDirection = true)
        => WorldEffects.RegisterVisualEffect(effectName, x, y, directionDegrees, count, normalizeDirection);
}
