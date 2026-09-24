namespace OpenGarrison.Core;

/// <summary>
/// Corpse launch on non-gib death. Independent of projectile speed — magnitudes are
/// tuned per kill source so slow bullets still shove remains noticeably.
/// </summary>
public static class CorpseKnockbackRules
{
    /// <summary>Floor applied to any directed corpse knockback.</summary>
    public const float MinimumSpeed = 3.5f;

    /// <summary>Spy revolver / sentry / general firearm baseline.</summary>
    public const float StandardSpeed = 4.0f;

    /// <summary>Sniper rifle — a bit harder than revolver at close range.</summary>
    public const float SniperSpeed = 5.4f;

    public static float ResolveSpeed(string? weaponSpriteName, PlayerClass? killerClass = null)
    {
        if (!string.IsNullOrWhiteSpace(weaponSpriteName))
        {
            if (IsSniperKillIcon(weaponSpriteName))
            {
                return SniperSpeed;
            }

            if (IsSentryKillIcon(weaponSpriteName) || IsRevolverKillIcon(weaponSpriteName))
            {
                return StandardSpeed;
            }
        }

        if (killerClass == PlayerClass.Sniper)
        {
            return SniperSpeed;
        }

        return StandardSpeed;
    }

    public static float EnforceMinimum(float speed)
        => MathF.Max(MinimumSpeed, speed);

    public static bool IsSniperKillIcon(string weaponSpriteName)
        => ContainsKillIcon(weaponSpriteName, "RifleKL")
           || ContainsKillIcon(weaponSpriteName, "RifleChargedKL");

    public static bool IsSentryKillIcon(string weaponSpriteName)
        => ContainsKillIcon(weaponSpriteName, "TurretKL");

    public static bool IsRevolverKillIcon(string weaponSpriteName)
        => ContainsKillIcon(weaponSpriteName, "RevolverKL");

    private static bool ContainsKillIcon(string weaponSpriteName, string killIcon)
        => weaponSpriteName.Contains(killIcon, StringComparison.OrdinalIgnoreCase);
}
