namespace OpenGarrison.Core;

public enum PrimaryWeaponKind
{
    PelletGun = 1,
    FlameThrower = 2,
    RocketLauncher = 3,
    MineLauncher = 4,
    Minigun = 5,
    Rifle = 6,
    Medigun = 7,
    Revolver = 8,
    Blade = 9,
}

public sealed record PrimaryWeaponDefinition(
    string DisplayName,
    PrimaryWeaponKind Kind,
    int MaxAmmo,
    int AmmoPerShot,
    int ProjectilesPerShot,
    int ReloadDelayTicks,
    int AmmoReloadTicks,
    float SpreadDegrees,
    float MinShotSpeed,
    float AdditionalRandomShotSpeed,
    string? FireSoundName = null,
    float? DirectHitDamage = null,
    float? DamagePerTick = null,
    float? DirectHitHealAmount = null,
    RocketCombatDefinition? RocketCombat = null,
    bool AutoReloads = true,
    int AmmoRegenPerTick = 0,
    bool RefillsAllAtOnce = false);
