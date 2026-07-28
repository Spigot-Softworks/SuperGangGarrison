namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool TryBuildNetworkJumpPad(byte slot)
    {
        return TryGetNetworkPlayer(slot, out var player)
            && TryBuildJumpPad(player, ignoreMetalCost: true);
    }

    public bool TrySetNetworkPlayerNoclip(byte slot, bool enabled)
    {
        return TryGetNetworkPlayer(slot, out var player)
            && player.SetServerNoclip(enabled);
    }

    public bool TrySetNetworkPlayerFrozen(byte slot, bool frozen)
    {
        return TryGetNetworkPlayer(slot, out var player)
            && player.SetServerFrozen(frozen);
    }

    public bool TryStunNetworkPlayer(byte slot, int durationTicks)
    {
        return TryGetNetworkPlayer(slot, out var player)
            && player.SetServerStunTicks(durationTicks);
    }

    public bool TryTeleportNetworkPlayerToPlayer(byte sourceSlot, byte targetSlot)
    {
        if (!TryGetNetworkPlayer(sourceSlot, out var source)
            || !TryGetNetworkPlayer(targetSlot, out var target))
        {
            return false;
        }

        source.TeleportTo(target.X, target.Y - MathF.Max(0f, source.CollisionBottomOffset - target.CollisionBottomOffset));
        if (source.IsAlive)
        {
            source.ResolveBlockingOverlap(Level, source.Team);
        }

        return true;
    }

    public bool TrySetNetworkPlayerRespawnOverride(byte slot, float x, float y)
    {
        return TrySetNetworkPlayerSpawnOverride(slot, x, y);
    }

    public bool TryExplodeNetworkPlayer(byte slot)
    {
        if (!TryGetNetworkPlayer(slot, out var player) || !player.IsAlive)
        {
            return false;
        }

        RegisterWorldSoundEvent("ExplosionSnd", player.X, player.Y);
        RegisterVisualEffect("Explosion", player.X, player.Y);
        KillPlayer(player, gibbed: true, weaponSpriteName: "ExplodeKL");
        return true;
    }

    public bool TryGetNetworkPlayerInput(byte slot, out PlayerInputSnapshot input)
    {
        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            input = default;
            return false;
        }

        input = ResolveNetworkPlayerInput(slot);
        return true;
    }
}
