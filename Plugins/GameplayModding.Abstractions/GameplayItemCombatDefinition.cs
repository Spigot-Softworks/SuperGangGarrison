namespace OpenGarrison.GameplayModding;

/// <summary>
/// Combat behavior for a gameplay item.
/// </summary>
/// <param name="FireSoundName">The fire sound name, if any.</param>
/// <param name="KillFeedSpriteName">The kill feed sprite name, if any.</param>
/// <param name="DirectHitDamage">The direct hit damage, if any.</param>
/// <param name="DamagePerTick">The damage per tick, if any.</param>
/// <param name="DirectHitHealAmount">The direct hit heal amount, if any.</param>
/// <param name="ActiveProjectileLimit">The active projectile limit, if any.</param>
/// <param name="Rocket">The rocket combat definition, if any.</param>
/// <param name="PlayerKnockbackScale">The player knockback scale, if any.</param>
/// <param name="PlayerSlowMovementMultiplier">The player slow movement multiplier, if any.</param>
/// <param name="PlayerSlowRefreshSourceTicks">The slow refresh time in source ticks, if any.</param>
/// <param name="AirborneVelocityReach">The airborne velocity reach definition, if any.</param>
/// <param name="PlayerKnockback">The player knockback definition, if any.</param>
public sealed record GameplayItemCombatDefinition(
    string? FireSoundName = null,
    string? KillFeedSpriteName = null,
    float? DirectHitDamage = null,
    float? DamagePerTick = null,
    float? DirectHitHealAmount = null,
    int? ActiveProjectileLimit = null,
    GameplayRocketCombatDefinition? Rocket = null,
    float? PlayerKnockbackScale = null,
    float? PlayerSlowMovementMultiplier = null,
    int? PlayerSlowRefreshSourceTicks = null,
    GameplayAirborneVelocityReachDefinition? AirborneVelocityReach = null,
    GameplayPlayerKnockbackDefinition? PlayerKnockback = null);
