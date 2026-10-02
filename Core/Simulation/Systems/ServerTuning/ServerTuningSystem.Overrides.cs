namespace OpenGarrison.Core;

internal sealed partial class ServerTuningSystem
{
    internal bool TrySetNetworkPlayerMovementSpeedScale(byte slot, float scale)
    {
        if (!_host.TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        _host.PlayerRegistry.MovementSpeedScaleOverrides[slot] = float.Clamp(scale, 0.1f, 4f);
        ApplyServerGameplayTuning(slot, player);
        return true;
    }

    internal bool TryClearNetworkPlayerMovementSpeedScale(byte slot)
    {
        if (!_host.TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        _host.PlayerRegistry.MovementSpeedScaleOverrides.Remove(slot);
        ApplyServerGameplayTuning(slot, player);
        return true;
    }

    internal float GetNetworkPlayerMovementSpeedScale(byte slot)
    {
        return _host.TryGetNetworkPlayer(slot, out var player)
            ? player.ServerMovementSpeedScale
            : GetEffectiveNetworkPlayerMovementSpeedScale(slot);
    }

    internal bool HasNetworkPlayerMovementSpeedScaleOverride(byte slot)
    {
        return _host.PlayerRegistry.MovementSpeedScaleOverrides.ContainsKey(slot);
    }

    internal bool TrySetNetworkPlayerLastToDieEnemyScaling(
        byte slot,
        float movementSpeedMultiplier,
        float damageMultiplier)
    {
        if (!_host.TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        _host.PlayerRegistry.MovementSpeedScaleOverrides[slot] = MathF.Max(0.1f, movementSpeedMultiplier);
        _host.PlayerRegistry.LastToDieEnemyDamageScaleOverrides[slot] = MathF.Max(0f, damageMultiplier);
        ApplyServerGameplayTuning(slot, player);
        return true;
    }

    internal bool TryClearNetworkPlayerLastToDieEnemyScaling(byte slot)
    {
        if (!_host.TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        _host.PlayerRegistry.MovementSpeedScaleOverrides.Remove(slot);
        _host.PlayerRegistry.LastToDieEnemyDamageScaleOverrides.Remove(slot);
        ApplyServerGameplayTuning(slot, player);
        return true;
    }

    internal bool TrySetNetworkPlayerGravityScale(byte slot, float scale)
    {
        if (!_host.TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        _host.PlayerRegistry.GravityScaleOverrides[slot] = float.Clamp(scale, 0f, 4f);
        ApplyServerGameplayTuning(slot, player);
        return true;
    }

    internal bool TryClearNetworkPlayerGravityScale(byte slot)
    {
        if (!_host.TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        _host.PlayerRegistry.GravityScaleOverrides.Remove(slot);
        ApplyServerGameplayTuning(slot, player);
        return true;
    }

    internal float GetNetworkPlayerGravityScale(byte slot)
    {
        return _host.TryGetNetworkPlayer(slot, out var player)
            ? player.ServerGravityScale
            : GetEffectiveNetworkPlayerGravityScale(slot);
    }

    internal bool HasNetworkPlayerGravityScaleOverride(byte slot)
    {
        return _host.PlayerRegistry.GravityScaleOverrides.ContainsKey(slot);
    }

    internal bool TrySetNetworkPlayerMaxHealthOverride(byte slot, int? maxHealth, bool refillHealth = true)
    {
        if (!_host.TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        if (maxHealth.HasValue)
        {
            _host.PlayerRegistry.MaxHealthOverrides[slot] = Math.Max(1, maxHealth.Value);
        }
        else
        {
            _host.PlayerRegistry.MaxHealthOverrides.Remove(slot);
        }

        ApplyNetworkPlayerMaxHealthOverride(slot, player, refillHealth);
        return true;
    }

    internal void SetPlayerScale(float scale)
    {
        _host.MatchSettings.PlayerScale = PlayerEntity.ClampPlayerScale(scale);
        ApplyConfiguredPlayerScaleToKnownPlayers();
    }

    internal bool TrySetNetworkPlayerScale(byte slot, float scale)
    {
        if (!_host.TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        var playerTeam = player.IsAlive ? player.Team : _host.GetNetworkPlayerConfiguredTeam(slot);
        ApplyLivePlayerScaleToPlayer(player, playerTeam, PlayerEntity.ClampPlayerScale(scale));
        return true;
    }

    internal void SetMovementSpeedScale(float scale)
    {
        _host.MatchSettings.MovementSpeedScale = float.Clamp(scale, 0.1f, 4f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    internal void SetProjectileSpeedScale(float scale)
    {
        _host.MatchSettings.ProjectileSpeedScale = float.Clamp(scale, 0.1f, 4f);
    }

    internal void SetDamageScale(float scale)
    {
        _host.MatchSettings.DamageScale = float.Clamp(scale, 0f, 10f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    internal void SetGravityScale(float scale)
    {
        _host.MatchSettings.GravityScale = float.Clamp(scale, 0f, 4f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    internal void SetHorizontalSpeedClampPerTick(float clampPerTick)
    {
        _host.MatchSettings.HorizontalSpeedClampPerTick = float.Clamp(clampPerTick, 1f, 60f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    internal void SetVerticalSpeedClampPerTick(float clampPerTick)
    {
        _host.MatchSettings.VerticalSpeedClampPerTick = float.Clamp(clampPerTick, 1f, 60f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    internal void SetRoundEndFriendlyFire(bool enabled)
    {
        _host.MatchSettings.RoundEndFriendlyFireEnabled = enabled;
    }

    private void ApplyServerGameplayTuningToKnownPlayers()
    {
        ApplyServerGameplayTuning(SimulationConstants.LocalPlayerSlot, _host.LocalPlayer);
        ApplyServerGameplayTuning(slot: 0, _host.EnemyPlayer);
        ApplyServerGameplayTuning(slot: 0, _host.FriendlyDummy);

        foreach (var entry in _host.PlayerRegistry.PlayersBySlot)
        {
            ApplyServerGameplayTuning(entry.Key, entry.Value);
        }
    }

    internal void ApplyServerGameplayTuning(byte slot, PlayerEntity player)
    {
        var movementSpeedScale = GetEffectiveNetworkPlayerMovementSpeedScale(slot);
        var gravityScale = GetEffectiveNetworkPlayerGravityScale(slot);
        player.SetServerMovementSpeedScale(movementSpeedScale);
        player.SetServerDamageScale(_host.MatchSettings.DamageScale);
        player.SetLastToDieEnemyDamageMultiplier(
            slot != 0 && _host.PlayerRegistry.LastToDieEnemyDamageScaleOverrides.TryGetValue(slot, out var damageScale)
                ? damageScale
                : 1f);
        player.SetServerGravityScale(gravityScale);
        player.SetServerMovementSpeedClamps(
            _host.MatchSettings.HorizontalSpeedClampPerTick,
            _host.MatchSettings.VerticalSpeedClampPerTick);
        player.SetReplicatedStateFloat(
            PlayerEntity.ServerTuningReplicatedStateOwnerId,
            PlayerEntity.MovementSpeedScaleReplicatedStateKey,
            movementSpeedScale);
        player.SetReplicatedStateFloat(
            PlayerEntity.ServerTuningReplicatedStateOwnerId,
            PlayerEntity.GravityScaleReplicatedStateKey,
            gravityScale);
    }

    private float GetEffectiveNetworkPlayerMovementSpeedScale(byte slot)
    {
        return slot != 0 && _host.PlayerRegistry.MovementSpeedScaleOverrides.TryGetValue(slot, out var scale)
            ? scale
            : _host.MatchSettings.MovementSpeedScale;
    }

    private float GetEffectiveNetworkPlayerGravityScale(byte slot)
    {
        return slot != 0 && _host.PlayerRegistry.GravityScaleOverrides.TryGetValue(slot, out var scale)
            ? scale
            : _host.MatchSettings.GravityScale;
    }

    internal void ApplyNetworkPlayerMaxHealthOverride(byte slot, PlayerEntity player, bool refillHealth)
    {
        player.SetExperimentalMaxHealthOverride(
            slot != 0 && _host.PlayerRegistry.MaxHealthOverrides.TryGetValue(slot, out var maxHealth)
                ? maxHealth
                : null,
            refillHealth);
    }

    private void ApplyConfiguredPlayerScaleToKnownPlayers()
    {
        ApplyLivePlayerScaleToPlayer(_host.LocalPlayer, _host.LocalPlayer.Team, _host.MatchSettings.PlayerScale);
        ApplyLivePlayerScaleToPlayer(_host.EnemyPlayer, _host.DummyState.EnemyTeam, _host.MatchSettings.PlayerScale);
        ApplyLivePlayerScaleToPlayer(_host.FriendlyDummy, _host.LocalPlayer.Team, _host.MatchSettings.PlayerScale);

        foreach (var entry in _host.PlayerRegistry.PlayersBySlot)
        {
            var player = entry.Value;
            var team = player.IsAlive ? player.Team : _host.GetNetworkPlayerConfiguredTeam(entry.Key);
            ApplyLivePlayerScaleToPlayer(player, team, _host.MatchSettings.PlayerScale);
        }
    }

    private void ApplyLivePlayerScaleToPlayer(PlayerEntity player, PlayerTeam team, float scale)
    {
        if (!player.IsAlive)
        {
            player.SetPlayerScale(scale);
            return;
        }

        if (player.TryApplyLiveScale(scale, _host.Level, team))
        {
            return;
        }

        player.SetPlayerScale(scale);
        var fallbackSpawn = _host.ReserveSpawn(player, team);
        player.TeleportTo(fallbackSpawn.X, fallbackSpawn.Y);
        player.ResolveBlockingOverlap(_host.Level, team);
    }
}
