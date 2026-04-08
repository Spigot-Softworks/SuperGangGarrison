namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
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
}
