namespace OpenGarrison.Core;

public sealed record ExperimentalGameplaySettings(
    bool EnableSoldierShotgunSecondaryWeapon = false,
    bool EnableHealOnDamage = false,
    bool EnableHealOnKill = false,
    bool EnableRage = false,
    bool EnableRateOfFireMultiplierOnDamage = false,
    bool EnableSoldierInstantReload = false,
    bool EnableSpeedOnDamage = false,
    bool EnableSpeedOnKill = false,
    bool EnablePassiveHealthRegeneration = false,
    bool EnableInvincibilityOnKill = false,
    bool EnableProjectileSpeedMultiplier = false,
    bool EnableAirshotDamageMultiplier = false,
    bool EnableComboTracking = false,
    bool EnableKillStreakTracking = false,
    bool EnableEnemyHealthPackDrops = false,
    bool EnableEnemyDroppedWeapons = false,
    float EnemyHealthPackDropChance = 0.1f)
{
    public const float DefaultEnemyHealthPackDropChance = 0.1f;
    public const float HealOnDamageFraction = 0.35f;
    public const int HealOnKillAmount = 25;
    public const float PassiveHealthRegenerationPerSecond = 3f;
    public const float RageMaxCharge = 500f;
    public const float RageDamageDealtChargeMultiplier = 1f;
    public const float RageDamageReceivedChargeMultiplier = 1f;
    public const float RageDurationSeconds = 7f;
    public const float SpeedBoostMultiplier = 1.2f;
    public const float RateOfFireCooldownMultiplier = 0.8f;
    public const float OnDamageBuffDurationSeconds = 2.5f;
    public const float OnKillBuffDurationSeconds = 3f;
    public const float KillInvincibilityDurationSeconds = 0.5f;
    public const float ProjectileSpeedMultiplier = 1.2f;
    public const float AirshotDamageMultiplier = 1.25f;
    public const float ComboTimeoutSeconds = 6f;
    public const float MultiKillWindowSeconds = 3f;
    public const float EnemyHealthPackLargeChance = 0.5f;
    public const float EnemyDroppedWeaponChance = 0.5f;
}
