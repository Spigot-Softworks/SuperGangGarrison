namespace OpenGarrison.GameplayModding;

/// <summary>
/// Ammo behavior for a gameplay item.
/// </summary>
/// <param name="MaxAmmo">The maximum ammo.</param>
/// <param name="AmmoPerUse">The ammo consumed per use.</param>
/// <param name="ProjectilesPerUse">The projectiles spawned per use.</param>
/// <param name="UseDelaySourceTicks">The delay between uses, in source ticks.</param>
/// <param name="ReloadSourceTicks">The reload time, in source ticks.</param>
/// <param name="SpreadDegrees">The projectile spread in degrees.</param>
/// <param name="MinProjectileSpeed">The minimum projectile speed.</param>
/// <param name="AdditionalProjectileSpeed">The additional projectile speed.</param>
/// <param name="AutoReloads">Whether the item reloads automatically.</param>
/// <param name="AmmoRegenPerTick">The ammo regenerated per tick.</param>
/// <param name="RefillsAllAtOnce">Whether a reload refills all ammo at once.</param>
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
