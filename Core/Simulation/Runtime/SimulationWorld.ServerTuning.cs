namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
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
        ApplyServerGameplayTuning(LocalPlayer);
        ApplyServerGameplayTuning(EnemyPlayer);
        ApplyServerGameplayTuning(FriendlyDummy);

        foreach (var player in _additionalNetworkPlayersBySlot.Values)
        {
            ApplyServerGameplayTuning(player);
        }
    }

    private void ApplyServerGameplayTuning(PlayerEntity player)
    {
        player.SetServerMovementSpeedScale(_configuredMovementSpeedScale);
        player.SetServerDamageScale(_configuredDamageScale);
        player.SetServerGravityScale(_configuredGravityScale);
        player.SetServerMovementSpeedClamps(
            _configuredHorizontalSpeedClampPerTick,
            _configuredVerticalSpeedClampPerTick);
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
