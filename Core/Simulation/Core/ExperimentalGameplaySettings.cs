namespace OpenGarrison.Core;

public sealed record ExperimentalGameplaySettings(
    bool EnableSoldierShotgunSecondaryWeapon = false,
    bool EnableHealOnDamage = false,
    bool EnableHealOnKill = false,
    bool EnableRateOfFireMultiplierOnDamage = false,
    bool EnableSoldierInstantReload = false,
    bool EnableSpeedOnDamage = false,
    bool EnableSpeedOnKill = false,
    bool EnablePassiveHealthRegeneration = false,
    bool EnableInvincibilityOnKill = false,
    bool EnableProjectileSpeedMultiplier = false,
    bool EnableAirshotDamageMultiplier = false,
    bool EnableEnemyHealthPackDrops = false,
    bool EnableEnemyDroppedWeapons = false)
{
    public const float HealOnDamageFraction = 0.35f;
    public const int HealOnKillAmount = 25;
    public const float PassiveHealthRegenerationPerSecond = 3f;
    public const float SpeedBoostMultiplier = 1.2f;
    public const float RateOfFireCooldownMultiplier = 0.8f;
    public const float OnDamageBuffDurationSeconds = 2.5f;
    public const float OnKillBuffDurationSeconds = 3f;
    public const float KillInvincibilityDurationSeconds = 0.5f;
    public const float ProjectileSpeedMultiplier = 1.2f;
    public const float AirshotDamageMultiplier = 1.25f;
    public const float EnemyHealthPackDropChance = 0.1f;
    public const float EnemyHealthPackLargeChance = 0.5f;
    public const float EnemyDroppedWeaponChance = 0.5f;
}
