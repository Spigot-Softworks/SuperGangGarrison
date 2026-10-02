namespace OpenGarrison.Core;

internal sealed partial class AdminCommandsSystem
{
    internal bool TryBuildNetworkJumpPad(byte slot)
    {
        return _host.TryGetNetworkPlayer(slot, out var player)
            && _host.TryBuildJumpPad(player, ignoreMetalCost: true);
    }

    internal bool TrySetNetworkPlayerNoclip(byte slot, bool enabled)
    {
        return _host.TryGetNetworkPlayer(slot, out var player)
            && player.SetServerNoclip(enabled);
    }

    internal bool TrySetNetworkPlayerFrozen(byte slot, bool frozen)
    {
        return _host.TryGetNetworkPlayer(slot, out var player)
            && player.SetServerFrozen(frozen);
    }

    internal bool TryStunNetworkPlayer(byte slot, int durationTicks)
    {
        return _host.TryGetNetworkPlayer(slot, out var player)
            && player.SetServerStunTicks(durationTicks);
    }

    internal bool TryTeleportNetworkPlayerToPlayer(byte sourceSlot, byte targetSlot)
    {
        if (!_host.TryGetNetworkPlayer(sourceSlot, out var source)
            || !_host.TryGetNetworkPlayer(targetSlot, out var target))
        {
            return false;
        }

        source.TeleportTo(target.X, target.Y - MathF.Max(0f, source.CollisionBottomOffset - target.CollisionBottomOffset));
        if (source.IsAlive)
        {
            source.ResolveBlockingOverlap(_host.Level, source.Team);
        }

        return true;
    }

    internal bool TrySetNetworkPlayerRespawnOverride(byte slot, float x, float y)
    {
        return _host.TrySetNetworkPlayerSpawnOverride(slot, x, y);
    }

    internal bool TryExplodeNetworkPlayer(byte slot)
    {
        if (!_host.TryGetNetworkPlayer(slot, out var player) || !player.IsAlive)
        {
            return false;
        }

        _host.RegisterWorldSoundEvent("ExplosionSnd", player.X, player.Y);
        _host.RegisterVisualEffect("Explosion", player.X, player.Y);
        _host.KillPlayer(player, gibbed: true, weaponSpriteName: "ExplodeKL");
        return true;
    }

    internal bool TryGetNetworkPlayerInput(byte slot, out PlayerInputSnapshot input)
    {
        if (!NetworkPlayerSystem.IsPlayableNetworkPlayerSlot(slot))
        {
            input = default;
            return false;
        }

        input = _host.ResolveNetworkPlayerInput(slot);
        return true;
    }
}
