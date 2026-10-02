namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool TrySetNetworkPlayerMovementSpeedScale(byte slot, float scale)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        PlayerRegistry.MovementSpeedScaleOverrides[slot] = float.Clamp(scale, 0.1f, 4f);
        ApplyServerGameplayTuning(slot, player);
        return true;
    }

    public bool TryClearNetworkPlayerMovementSpeedScale(byte slot)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        PlayerRegistry.MovementSpeedScaleOverrides.Remove(slot);
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
        return PlayerRegistry.MovementSpeedScaleOverrides.ContainsKey(slot);
    }

    public bool TrySetNetworkPlayerLastToDieEnemyScaling(
        byte slot,
        float movementSpeedMultiplier,
        float damageMultiplier)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        PlayerRegistry.MovementSpeedScaleOverrides[slot] = MathF.Max(0.1f, movementSpeedMultiplier);
        PlayerRegistry.LastToDieEnemyDamageScaleOverrides[slot] = MathF.Max(0f, damageMultiplier);
        ApplyServerGameplayTuning(slot, player);
        return true;
    }

    public bool TryClearNetworkPlayerLastToDieEnemyScaling(byte slot)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        PlayerRegistry.MovementSpeedScaleOverrides.Remove(slot);
        PlayerRegistry.LastToDieEnemyDamageScaleOverrides.Remove(slot);
        ApplyServerGameplayTuning(slot, player);
        return true;
    }

    public bool TrySetNetworkPlayerGravityScale(byte slot, float scale)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        PlayerRegistry.GravityScaleOverrides[slot] = float.Clamp(scale, 0f, 4f);
        ApplyServerGameplayTuning(slot, player);
        return true;
    }

    public bool TryClearNetworkPlayerGravityScale(byte slot)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        PlayerRegistry.GravityScaleOverrides.Remove(slot);
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
        return PlayerRegistry.GravityScaleOverrides.ContainsKey(slot);
    }

    public bool TrySetNetworkPlayerMaxHealthOverride(byte slot, int? maxHealth, bool refillHealth = true)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        if (maxHealth.HasValue)
        {
            PlayerRegistry.MaxHealthOverrides[slot] = Math.Max(1, maxHealth.Value);
        }
        else
        {
            PlayerRegistry.MaxHealthOverrides.Remove(slot);
        }

        ApplyNetworkPlayerMaxHealthOverride(slot, player, refillHealth);
        return true;
    }

    public void SetPlayerScale(float scale)
    {
        MatchSettings.PlayerScale = PlayerEntity.ClampPlayerScale(scale);
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
        if (MathF.Abs(MatchSettings.MapScale - nextScale) <= 0.0001f)
        {
            return;
        }

        var previousScale = MatchSettings.MapScale;
        MatchSettings.MapScale = nextScale;
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
            MatchSettings.MapScale = previousScale;
        }
    }

    public void SetMovementSpeedScale(float scale)
    {
        MatchSettings.MovementSpeedScale = float.Clamp(scale, 0.1f, 4f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    public void SetProjectileSpeedScale(float scale)
    {
        MatchSettings.ProjectileSpeedScale = float.Clamp(scale, 0.1f, 4f);
    }

    public void SetDamageScale(float scale)
    {
        MatchSettings.DamageScale = float.Clamp(scale, 0f, 10f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    public void SetGravityScale(float scale)
    {
        MatchSettings.GravityScale = float.Clamp(scale, 0f, 4f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    public void SetHorizontalSpeedClampPerTick(float clampPerTick)
    {
        MatchSettings.HorizontalSpeedClampPerTick = float.Clamp(clampPerTick, 1f, 60f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    public void SetVerticalSpeedClampPerTick(float clampPerTick)
    {
        MatchSettings.VerticalSpeedClampPerTick = float.Clamp(clampPerTick, 1f, 60f);
        ApplyServerGameplayTuningToKnownPlayers();
    }

    public void SetRoundEndFriendlyFire(bool enabled)
    {
        MatchSettings.RoundEndFriendlyFireEnabled = enabled;
    }

    private void ApplyServerGameplayTuningToKnownPlayers()
    {
        ApplyServerGameplayTuning(LocalPlayerSlot, LocalPlayer);
        ApplyServerGameplayTuning(slot: 0, EnemyPlayer);
        ApplyServerGameplayTuning(slot: 0, FriendlyDummy);

        foreach (var entry in PlayerRegistry.PlayersBySlot)
        {
            ApplyServerGameplayTuning(entry.Key, entry.Value);
        }
    }

    private void ApplyServerGameplayTuning(byte slot, PlayerEntity player)
    {
        var movementSpeedScale = GetEffectiveNetworkPlayerMovementSpeedScale(slot);
        var gravityScale = GetEffectiveNetworkPlayerGravityScale(slot);
        player.SetServerMovementSpeedScale(movementSpeedScale);
        player.SetServerDamageScale(MatchSettings.DamageScale);
        player.SetLastToDieEnemyDamageMultiplier(
            slot != 0 && PlayerRegistry.LastToDieEnemyDamageScaleOverrides.TryGetValue(slot, out var damageScale)
                ? damageScale
                : 1f);
        player.SetServerGravityScale(gravityScale);
        player.SetServerMovementSpeedClamps(
            MatchSettings.HorizontalSpeedClampPerTick,
            MatchSettings.VerticalSpeedClampPerTick);
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
        return slot != 0 && PlayerRegistry.MovementSpeedScaleOverrides.TryGetValue(slot, out var scale)
            ? scale
            : MatchSettings.MovementSpeedScale;
    }

    private float GetEffectiveNetworkPlayerGravityScale(byte slot)
    {
        return slot != 0 && PlayerRegistry.GravityScaleOverrides.TryGetValue(slot, out var scale)
            ? scale
            : MatchSettings.GravityScale;
    }

    private void ApplyNetworkPlayerMaxHealthOverride(byte slot, PlayerEntity player, bool refillHealth)
    {
        player.SetExperimentalMaxHealthOverride(
            slot != 0 && PlayerRegistry.MaxHealthOverrides.TryGetValue(slot, out var maxHealth)
                ? maxHealth
                : null,
            refillHealth);
    }

    private void ApplyConfiguredPlayerScaleToKnownPlayers()
    {
        ApplyLivePlayerScaleToPlayer(LocalPlayer, LocalPlayer.Team, MatchSettings.PlayerScale);
        ApplyLivePlayerScaleToPlayer(EnemyPlayer, DummyState.EnemyTeam, MatchSettings.PlayerScale);
        ApplyLivePlayerScaleToPlayer(FriendlyDummy, LocalPlayer.Team, MatchSettings.PlayerScale);

        foreach (var entry in PlayerRegistry.PlayersBySlot)
        {
            var player = entry.Value;
            var team = player.IsAlive ? player.Team : GetNetworkPlayerConfiguredTeam(entry.Key);
            ApplyLivePlayerScaleToPlayer(player, team, MatchSettings.PlayerScale);
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
