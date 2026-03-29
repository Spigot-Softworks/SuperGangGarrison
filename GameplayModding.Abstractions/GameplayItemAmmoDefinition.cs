namespace OpenGarrison.GameplayModding;

public sealed record GameplayItemAmmoDefinition(
    int MaxAmmo = 0,
    int AmmoPerUse = 0,
    int ProjectilesPerUse = 0,
    int UseDelaySourceTicks = 0,
    int ReloadSourceTicks = 0,
    float SpreadDegrees = 0f,
    float MinProjectileSpeed = 0f,
    float AdditionalProjectileSpeed = 0f,
    bool AutoReloads = true,
    int AmmoRegenPerTick = 0,
    bool RefillsAllAtOnce = false);
