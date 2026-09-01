namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public const int BuffBannerDefaultMaxChargeDamage = 400;
    public const int BuffBannerDefaultDeployTicks = 44;
    public const int BuffBannerDefaultActiveTicks = 150;
    public const float BuffBannerDefaultRadius = 128f;
    public const float BuffBannerDefaultDamageMultiplier = 1.35f;
    public const float BuffBannerDefaultHealthRegenPerSecond = 5f;

    public int BuffBannerChargeDamage { get; private set; }

    public int BuffBannerMaxChargeDamage { get; private set; } = BuffBannerDefaultMaxChargeDamage;

    public int BuffBannerDeployTicksRemaining { get; private set; }

    public int BuffBannerDeployDurationTicks { get; private set; } = BuffBannerDefaultDeployTicks;

    public int BuffBannerActiveTicksRemaining { get; private set; }

    public int BuffBannerActiveDurationTicks { get; private set; } = BuffBannerDefaultActiveTicks;

    public float BuffBannerRadius { get; private set; } = BuffBannerDefaultRadius;

    public float BuffBannerDamageMultiplier { get; private set; } = BuffBannerDefaultDamageMultiplier;

    public float BuffBannerHealthRegenPerSecond { get; private set; } = BuffBannerDefaultHealthRegenPerSecond;

    public bool IsBuffBannerReady => BuffBannerChargeDamage >= BuffBannerMaxChargeDamage;

    public bool IsBuffBannerDeploying => BuffBannerDeployTicksRemaining > 0;

    public bool IsBuffBannerActive => BuffBannerActiveTicksRemaining > 0;

    public int BuffBannerMissingChargeDamage => Math.Max(0, BuffBannerMaxChargeDamage - BuffBannerChargeDamage);

    public int BuffBannerDeployTicksElapsed => Math.Max(0, BuffBannerDeployDurationTicks - BuffBannerDeployTicksRemaining);

    public bool TryAddBuffBannerDamageCharge(
        int damage,
        int maxChargeDamage = BuffBannerDefaultMaxChargeDamage)
    {
        if (!IsAlive || ClassId != PlayerClass.Soldier || damage <= 0)
        {
            return false;
        }

        BuffBannerMaxChargeDamage = Math.Max(1, maxChargeDamage);
        var previousCharge = BuffBannerChargeDamage;
        BuffBannerChargeDamage = Math.Clamp(
            BuffBannerChargeDamage + damage,
            0,
            BuffBannerMaxChargeDamage);
        return BuffBannerChargeDamage != previousCharge;
    }

    public bool TryStartBuffBanner(
        int maxChargeDamage = BuffBannerDefaultMaxChargeDamage,
        int deployTicks = BuffBannerDefaultDeployTicks,
        int activeTicks = BuffBannerDefaultActiveTicks,
        float radius = BuffBannerDefaultRadius,
        float damageMultiplier = BuffBannerDefaultDamageMultiplier,
        float healthRegenPerSecond = BuffBannerDefaultHealthRegenPerSecond)
    {
        if (!IsAlive
            || ClassId != PlayerClass.Soldier
            || IsBuffBannerDeploying
            || IsBuffBannerActive)
        {
            return false;
        }

        BuffBannerMaxChargeDamage = Math.Max(1, maxChargeDamage);
        if (BuffBannerChargeDamage < BuffBannerMaxChargeDamage)
        {
            return false;
        }

        BuffBannerChargeDamage = 0;
        BuffBannerDeployDurationTicks = Math.Max(1, deployTicks);
        BuffBannerDeployTicksRemaining = BuffBannerDeployDurationTicks;
        BuffBannerActiveDurationTicks = Math.Max(1, activeTicks);
        BuffBannerActiveTicksRemaining = 0;
        BuffBannerRadius = float.IsFinite(radius)
            ? MathF.Max(1f, radius)
            : BuffBannerDefaultRadius;
        BuffBannerDamageMultiplier = float.IsFinite(damageMultiplier)
            ? MathF.Max(1f, damageMultiplier)
            : BuffBannerDefaultDamageMultiplier;
        BuffBannerHealthRegenPerSecond = float.IsFinite(healthRegenPerSecond)
            ? MathF.Max(0f, healthRegenPerSecond)
            : BuffBannerDefaultHealthRegenPerSecond;
        return true;
    }

    internal void AdvanceBuffBannerState()
    {
        if (ClassId != PlayerClass.Soldier)
        {
            ResetBuffBannerState();
            return;
        }

        if (BuffBannerDeployTicksRemaining > 0)
        {
            BuffBannerDeployTicksRemaining -= 1;
            if (BuffBannerDeployTicksRemaining == 0)
            {
                BuffBannerActiveTicksRemaining = BuffBannerActiveDurationTicks;
            }

            return;
        }

        if (BuffBannerActiveTicksRemaining > 0)
        {
            BuffBannerActiveTicksRemaining -= 1;
        }
    }

    internal void HydrateBuffBannerState(
        int chargeDamage,
        int deployTicksRemaining,
        int activeTicksRemaining)
    {
        HydrateBuffBannerState(
            chargeDamage,
            BuffBannerDefaultMaxChargeDamage,
            deployTicksRemaining,
            Math.Max(BuffBannerDefaultDeployTicks, Math.Max(0, deployTicksRemaining)),
            activeTicksRemaining,
            Math.Max(BuffBannerDefaultActiveTicks, Math.Max(0, activeTicksRemaining)),
            BuffBannerDefaultRadius,
            BuffBannerDefaultDamageMultiplier,
            BuffBannerDefaultHealthRegenPerSecond);
    }

    internal void HydrateBuffBannerState(
        int chargeDamage,
        int maxChargeDamage,
        int deployTicksRemaining,
        int deployDurationTicks,
        int activeTicksRemaining,
        int activeDurationTicks,
        float radius,
        float damageMultiplier,
        float healthRegenPerSecond)
    {
        if (ClassId != PlayerClass.Soldier)
        {
            ResetBuffBannerState();
            return;
        }

        BuffBannerMaxChargeDamage = Math.Max(1, maxChargeDamage);
        BuffBannerChargeDamage = Math.Clamp(chargeDamage, 0, BuffBannerMaxChargeDamage);
        BuffBannerDeployDurationTicks = Math.Max(1, Math.Max(deployDurationTicks, deployTicksRemaining));
        BuffBannerDeployTicksRemaining = Math.Max(0, deployTicksRemaining);
        BuffBannerActiveDurationTicks = Math.Max(1, Math.Max(activeDurationTicks, activeTicksRemaining));
        BuffBannerActiveTicksRemaining = BuffBannerDeployTicksRemaining > 0
            ? 0
            : Math.Max(0, activeTicksRemaining);
        BuffBannerRadius = float.IsFinite(radius)
            ? MathF.Max(1f, radius)
            : BuffBannerDefaultRadius;
        BuffBannerDamageMultiplier = float.IsFinite(damageMultiplier)
            ? MathF.Max(1f, damageMultiplier)
            : BuffBannerDefaultDamageMultiplier;
        BuffBannerHealthRegenPerSecond = float.IsFinite(healthRegenPerSecond)
            ? MathF.Max(0f, healthRegenPerSecond)
            : BuffBannerDefaultHealthRegenPerSecond;
    }

    internal void ResetBuffBannerState()
    {
        BuffBannerChargeDamage = 0;
        BuffBannerMaxChargeDamage = BuffBannerDefaultMaxChargeDamage;
        BuffBannerDeployTicksRemaining = 0;
        BuffBannerDeployDurationTicks = BuffBannerDefaultDeployTicks;
        BuffBannerActiveTicksRemaining = 0;
        BuffBannerActiveDurationTicks = BuffBannerDefaultActiveTicks;
        BuffBannerRadius = BuffBannerDefaultRadius;
        BuffBannerDamageMultiplier = BuffBannerDefaultDamageMultiplier;
        BuffBannerHealthRegenPerSecond = BuffBannerDefaultHealthRegenPerSecond;
    }
}
