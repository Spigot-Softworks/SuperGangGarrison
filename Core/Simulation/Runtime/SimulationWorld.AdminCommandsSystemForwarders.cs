namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    public bool TryBuildNetworkJumpPad(byte slot)
        => AdminCommands.TryBuildNetworkJumpPad(slot);
    public bool TryExplodeNetworkPlayer(byte slot)
        => AdminCommands.TryExplodeNetworkPlayer(slot);
    public bool TryGetNetworkPlayerInput(byte slot, out PlayerInputSnapshot input)
        => AdminCommands.TryGetNetworkPlayerInput(slot, out input);
    public bool TrySetNetworkPlayerFrozen(byte slot, bool frozen)
        => AdminCommands.TrySetNetworkPlayerFrozen(slot, frozen);
    public bool TrySetNetworkPlayerNoclip(byte slot, bool enabled)
        => AdminCommands.TrySetNetworkPlayerNoclip(slot, enabled);
    public bool TrySetNetworkPlayerRespawnOverride(byte slot, float x, float y)
        => AdminCommands.TrySetNetworkPlayerRespawnOverride(slot, x, y);
    public bool TryStunNetworkPlayer(byte slot, int durationTicks)
        => AdminCommands.TryStunNetworkPlayer(slot, durationTicks);
    public bool TryTeleportNetworkPlayerToPlayer(byte sourceSlot, byte targetSlot)
        => AdminCommands.TryTeleportNetworkPlayerToPlayer(sourceSlot, targetSlot);
}
