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

    /// <summary>
    /// Launch remains away from the shot origin. Any pre-death momentum toward the
    /// attacker is stripped first so charging into a sniper (or similar) can't yank
    /// the ragdoll back at them.
    /// </summary>
    public static void ApplyDirectedLaunch(
        ref float horizontalSpeed,
        ref float verticalSpeed,
        float corpseX,
        float corpseY,
        float originX,
        float originY,
        float knockbackSpeed,
        float facingFallbackSign)
    {
        knockbackSpeed = EnforceMinimum(knockbackSpeed);
        var deltaX = corpseX - originX;
        var deltaY = corpseY - originY;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= 0.001f)
        {
            var fallbackSign = facingFallbackSign >= 0f ? 1f : -1f;
            horizontalSpeed = fallbackSign * knockbackSpeed;
            verticalSpeed = -2.0f;
            return;
        }

        var awayX = deltaX / distance;
        var awayY = deltaY / distance;

        // Drop only the component aimed at the killer; keep sideways / already-away motion.
        var alongAway = (horizontalSpeed * awayX) + (verticalSpeed * awayY);
        if (alongAway < 0f)
        {
            horizontalSpeed -= awayX * alongAway;
            verticalSpeed -= awayY * alongAway;
        }

        horizontalSpeed += awayX * knockbackSpeed;
        verticalSpeed += (awayY * knockbackSpeed * 0.45f) - 2.0f;
    }

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
