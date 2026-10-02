namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    public float GetNetworkPlayerGravityScale(byte slot)
        => ServerTuning.GetNetworkPlayerGravityScale(slot);
    public float GetNetworkPlayerMovementSpeedScale(byte slot)
        => ServerTuning.GetNetworkPlayerMovementSpeedScale(slot);
    public bool HasNetworkPlayerGravityScaleOverride(byte slot)
        => ServerTuning.HasNetworkPlayerGravityScaleOverride(slot);
    public bool HasNetworkPlayerMovementSpeedScaleOverride(byte slot)
        => ServerTuning.HasNetworkPlayerMovementSpeedScaleOverride(slot);
    public void SetDamageScale(float scale)
        => ServerTuning.SetDamageScale(scale);
    public void SetGravityScale(float scale)
        => ServerTuning.SetGravityScale(scale);
    public void SetHorizontalSpeedClampPerTick(float clampPerTick)
        => ServerTuning.SetHorizontalSpeedClampPerTick(clampPerTick);
    public void SetMovementSpeedScale(float scale)
        => ServerTuning.SetMovementSpeedScale(scale);
    public void SetPlayerScale(float scale)
        => ServerTuning.SetPlayerScale(scale);
    public void SetProjectileSpeedScale(float scale)
        => ServerTuning.SetProjectileSpeedScale(scale);
    public void SetRoundEndFriendlyFire(bool enabled)
        => ServerTuning.SetRoundEndFriendlyFire(enabled);
    public void SetVerticalSpeedClampPerTick(float clampPerTick)
        => ServerTuning.SetVerticalSpeedClampPerTick(clampPerTick);
    public bool TryClearNetworkPlayerGravityScale(byte slot)
        => ServerTuning.TryClearNetworkPlayerGravityScale(slot);
    public bool TryClearNetworkPlayerLastToDieEnemyScaling(byte slot)
        => ServerTuning.TryClearNetworkPlayerLastToDieEnemyScaling(slot);
    public bool TryClearNetworkPlayerMovementSpeedScale(byte slot)
        => ServerTuning.TryClearNetworkPlayerMovementSpeedScale(slot);
    public bool TrySetNetworkPlayerGravityScale(byte slot, float scale)
        => ServerTuning.TrySetNetworkPlayerGravityScale(slot, scale);
    public bool TrySetNetworkPlayerLastToDieEnemyScaling(byte slot, float movementSpeedMultiplier, float damageMultiplier)
        => ServerTuning.TrySetNetworkPlayerLastToDieEnemyScaling(slot, movementSpeedMultiplier, damageMultiplier);
    public bool TrySetNetworkPlayerMaxHealthOverride(byte slot, int? maxHealth, bool refillHealth = true)
        => ServerTuning.TrySetNetworkPlayerMaxHealthOverride(slot, maxHealth, refillHealth);
    public bool TrySetNetworkPlayerMovementSpeedScale(byte slot, float scale)
        => ServerTuning.TrySetNetworkPlayerMovementSpeedScale(slot, scale);
    public bool TrySetNetworkPlayerScale(byte slot, float scale)
        => ServerTuning.TrySetNetworkPlayerScale(slot, scale);
}
