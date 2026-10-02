namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    private bool TryStartSpyBackstab(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        => PlayerInput.TryStartSpyBackstab(attacker, aimWorldX, aimWorldY);
    private static double ElapsedMilliseconds(long startTimestamp)
        => PlayerInputSystem.ElapsedMilliseconds(startTimestamp);
    private static PlayerInputSnapshot ResetMovementInput(PlayerInputSnapshot input)
        => PlayerInputSystem.ResetMovementInput(input);
}
