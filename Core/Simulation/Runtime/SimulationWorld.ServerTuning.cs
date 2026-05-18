namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool TrySetNetworkPlayerMovementSpeedScale(byte slot, float scale)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        _networkPlayerMovementSpeedScaleOverrides[slot] = float.Clamp(scale, 0.1f, 4f);
        ApplyServerGameplayTuning(slot, player);
        return true;
    }

    public bool TryClearNetworkPlayerMovementSpeedScale(byte slot)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        _networkPlayerMovementSpeedScaleOverrides.Remove(slot);
        ApplyServerGameplayTuning(slot, player);
        return true;
    }

    public float GetNetworkPlayerMovementSpeedScale(byte slot)
    {
        return TryGetNetworkPlayer(slot, out var player)
            ? player.ServerMovementSpeedScale
            : GetEffectiveNetworkPlayerMovementSpeedScale(slot);
    }

    public bool HasNetworkPlayerMovementSpeedScaleOverride(byte slot)
    {
        return _networkPlayerMovementSpeedScaleOverrides.ContainsKey(slot);
    }

    public bool TrySetNetworkPlayerGravityScale(byte slot, float scale)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        _networkPlayerGravityScaleOverrides[slot] = float.Clamp(scale, 0f, 4f);
        ApplyServerGameplayTuning(slot, player);
        return true;
    }

    public bool TryClearNetworkPlayerGravityScale(byte slot)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        _networkPlayerGravityScaleOverrides.Remove(slot);
        ApplyServerGameplayTuning(slot, player);
        return true;
    }

    public float GetNetworkPlayerGravityScale(byte slot)
    {
        return TryGetNetworkPlayer(slot, out var player)
            ? player.ServerGravityScale
            : GetEffectiveNetworkPlayerGravityScale(slot);
    }

    public bool HasNetworkPlayerGravityScaleOverride(byte slot)
    {
        return _networkPlayerGravityScaleOverrides.ContainsKey(slot);
    }

    public bool TrySetNetworkPlayerMaxHealthOverride(byte slot, int? maxHealth, bool refillHealth = true)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        if (maxHealth.HasValue)
        {
            _networkPlayerMaxHealthOverrides[slot] = Math.Max(1, maxHealth.Value);
        }
        else
        {
            _networkPlayerMaxHealthOverrides.Remove(slot);
        }

        ApplyNetworkPlayerMaxHealthOverride(slot, player, refillHealth);
        return true;
    }

    public void SetPlayerScale(float scale)
    {
        _configuredPlayerScale = PlayerEntity.ClampPlayerScale(scale);
        ApplyConfiguredPlayerScaleToKnownPlayers();
    }

    public bool TrySetNetworkPlayerScale(byte slot, float scale)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        var playerTeam = player.IsAlive ? player.Team : GetNetworkPlayerConfiguredTeam(slot);
        ApplyLivePlayerScaleToPlayer(player, playerTeam, PlayerEntity.ClampPlayerScale(scale));
        return true;
    }

    public void SetMapScale(float scale)
    {
        var nextScale = float.Clamp(scale, 0.25f, 4f);
        if (MathF.Abs(_configuredMapScale - nextScale) <= 0.0001f)
        {
            return;
        }

        var previousScale = _configuredMapScale;
        _configuredMapScale = nextScale;
        if (TryLoadLevel(Level.Name, Level.MapAreaIndex, preservePlayerStats: false, mapScale: nextScale))
        {
            return;
        }

        if (!Level.ImportedFromSource)
        {
            Level = SimpleLevelFactory.CreateScoutPrototypeLevel(nextScale);
            MatchRules = CreateDefaultMatchRules(Level.Mode);
            ResetModeStateForNewMap();
            RestartCurrentRound(preservePlayerStats: false);
            return;
        }

        if (!TryLoadLevel(Level.Name, Level.MapAreaIndex, preservePlayerStats: false, mapScale: previousScale))
        {
            _configuredMapScale = previousScale;
        }
    }

    public void SetMovementSpeedScale(float scale)
    {
        _configuredMovementSpeedScale = float.Clamp(scale, 0.1f, 4f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    public void SetProjectileSpeedScale(float scale)
    {
        _configuredProjectileSpeedScale = float.Clamp(scale, 0.1f, 4f);
    }

    public void SetDamageScale(float scale)
    {
        _configuredDamageScale = float.Clamp(scale, 0f, 10f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    public void SetGravityScale(float scale)
    {
        _configuredGravityScale = float.Clamp(scale, 0f, 4f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    public void SetHorizontalSpeedClampPerTick(float clampPerTick)
    {
        _configuredHorizontalSpeedClampPerTick = float.Clamp(clampPerTick, 1f, 60f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    public void SetVerticalSpeedClampPerTick(float clampPerTick)
    {
        _configuredVerticalSpeedClampPerTick = float.Clamp(clampPerTick, 1f, 60f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    public void SetRoundEndFriendlyFire(bool enabled)
    {
        _roundEndFriendlyFireEnabled = enabled;
    }

    private void ApplyServerGameplayTuningToKnownPlayers()
    {
        ApplyServerGameplayTuning(LocalPlayerSlot, LocalPlayer);
        ApplyServerGameplayTuning(slot: 0, EnemyPlayer);
        ApplyServerGameplayTuning(slot: 0, FriendlyDummy);

        foreach (var entry in _additionalNetworkPlayersBySlot)
        {
            ApplyServerGameplayTuning(entry.Key, entry.Value);
        }
    }

    private void ApplyServerGameplayTuning(byte slot, PlayerEntity player)
    {
        var movementSpeedScale = GetEffectiveNetworkPlayerMovementSpeedScale(slot);
        var gravityScale = GetEffectiveNetworkPlayerGravityScale(slot);
        player.SetServerMovementSpeedScale(movementSpeedScale);
        player.SetServerDamageScale(_configuredDamageScale);
        player.SetServerGravityScale(gravityScale);
        player.SetServerMovementSpeedClamps(
            _configuredHorizontalSpeedClampPerTick,
            _configuredVerticalSpeedClampPerTick);
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
        return slot != 0 && _networkPlayerMovementSpeedScaleOverrides.TryGetValue(slot, out var scale)
            ? scale
            : _configuredMovementSpeedScale;
    }

    private float GetEffectiveNetworkPlayerGravityScale(byte slot)
    {
        return slot != 0 && _networkPlayerGravityScaleOverrides.TryGetValue(slot, out var scale)
            ? scale
            : _configuredGravityScale;
    }

    private void ApplyNetworkPlayerMaxHealthOverride(byte slot, PlayerEntity player, bool refillHealth)
    {
        player.SetExperimentalMaxHealthOverride(
            slot != 0 && _networkPlayerMaxHealthOverrides.TryGetValue(slot, out var maxHealth)
                ? maxHealth
                : null,
            refillHealth);
    }

    private void ApplyConfiguredPlayerScaleToKnownPlayers()
    {
        ApplyLivePlayerScaleToPlayer(LocalPlayer, LocalPlayer.Team, _configuredPlayerScale);
        ApplyLivePlayerScaleToPlayer(EnemyPlayer, _enemyDummyTeam, _configuredPlayerScale);
        ApplyLivePlayerScaleToPlayer(FriendlyDummy, LocalPlayer.Team, _configuredPlayerScale);

        foreach (var entry in _additionalNetworkPlayersBySlot)
        {
            var player = entry.Value;
            var team = player.IsAlive ? player.Team : GetNetworkPlayerConfiguredTeam(entry.Key);
            ApplyLivePlayerScaleToPlayer(player, team, _configuredPlayerScale);
        }
    }

    private void ApplyLivePlayerScaleToPlayer(PlayerEntity player, PlayerTeam team, float scale)
    {
        if (!player.IsAlive)
        {
            player.SetPlayerScale(scale);
            return;
        }

        if (player.TryApplyLiveScale(scale, Level, team))
        {
            return;
        }

        player.SetPlayerScale(scale);
        var fallbackSpawn = ReserveSpawn(player, team);
        player.TeleportTo(fallbackSpawn.X, fallbackSpawn.Y);
        player.ResolveBlockingOverlap(Level, team);
    }
}
