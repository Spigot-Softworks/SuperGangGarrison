namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    private void GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom)
        => PresentationBounds.GetCachedPlayerPresentationHitBounds(player, out left, out top, out right, out bottom);
    private static void GetPlayerPresentationHitBounds(SimulationWorld world, PlayerEntity player, out float left, out float top, out float right, out float bottom)
        => world.PresentationBounds.GetPlayerPresentationHitBounds(player, out left, out top, out right, out bottom);
}
