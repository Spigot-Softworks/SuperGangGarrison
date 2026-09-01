namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public const int BuffBannerDefaultMaxChargeKills = 4;
    public const int BuffBannerDefaultDeployTicks = 44;
    public const int BuffBannerDefaultActiveTicks = 150;
    public const float BuffBannerDefaultRadius = 128f;
    public const float BuffBannerDefaultDamageMultiplier = 1.35f;

    public int BuffBannerChargeKills { get; private set; }

    public int BuffBannerMaxChargeKills { get; private set; } = BuffBannerDefaultMaxChargeKills;

    public int BuffBannerDeployTicksRemaining { get; private set; }

    public int BuffBannerDeployDurationTicks { get; private set; } = BuffBannerDefaultDeployTicks;

    public int BuffBannerActiveTicksRemaining { get; private set; }

    public int BuffBannerActiveDurationTicks { get; private set; } = BuffBannerDefaultActiveTicks;

    public float BuffBannerRadius { get; private set; } = BuffBannerDefaultRadius;

    public float BuffBannerDamageMultiplier { get; private set; } = BuffBannerDefaultDamageMultiplier;

    public bool IsBuffBannerReady => BuffBannerChargeKills >= BuffBannerMaxChargeKills;

    public bool IsBuffBannerDeploying => BuffBannerDeployTicksRemaining > 0;

    public bool IsBuffBannerActive => BuffBannerActiveTicksRemaining > 0;

    public int BuffBannerMissingChargeKills => Math.Max(0, BuffBannerMaxChargeKills - BuffBannerChargeKills);

    public int BuffBannerDeployTicksElapsed => Math.Max(0, BuffBannerDeployDurationTicks - BuffBannerDeployTicksRemaining);

    public bool TryAddBuffBannerKillCharge(int amount = 1, int maxChargeKills = BuffBannerDefaultMaxChargeKills)
    {
        if (!IsAlive || ClassId != PlayerClass.Soldier || amount <= 0)
        {
            return false;
        }

        BuffBannerMaxChargeKills = Math.Max(1, maxChargeKills);
        var previousCharge = BuffBannerChargeKills;
        BuffBannerChargeKills = Math.Clamp(
            BuffBannerChargeKills + amount,
            0,
            BuffBannerMaxChargeKills);
        return BuffBannerChargeKills != previousCharge;
    }

    public bool TryStartBuffBanner(
        int maxChargeKills = BuffBannerDefaultMaxChargeKills,
        int deployTicks = BuffBannerDefaultDeployTicks,
        int activeTicks = BuffBannerDefaultActiveTicks,
        float radius = BuffBannerDefaultRadius,
        float damageMultiplier = BuffBannerDefaultDamageMultiplier)
    {
        if (!IsAlive
            || ClassId != PlayerClass.Soldier
            || IsBuffBannerDeploying
            || IsBuffBannerActive)
        {
            return false;
        }

        BuffBannerMaxChargeKills = Math.Max(1, maxChargeKills);
        if (BuffBannerChargeKills < BuffBannerMaxChargeKills)
        {
            return false;
        }

        BuffBannerChargeKills = 0;
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
        int chargeKills,
        int deployTicksRemaining,
        int activeTicksRemaining)
    {
        HydrateBuffBannerState(
            chargeKills,
            BuffBannerDefaultMaxChargeKills,
            deployTicksRemaining,
            Math.Max(BuffBannerDefaultDeployTicks, Math.Max(0, deployTicksRemaining)),
            activeTicksRemaining,
            Math.Max(BuffBannerDefaultActiveTicks, Math.Max(0, activeTicksRemaining)),
            BuffBannerDefaultRadius,
            BuffBannerDefaultDamageMultiplier);
    }

    internal void HydrateBuffBannerState(
        int chargeKills,
        int maxChargeKills,
        int deployTicksRemaining,
        int deployDurationTicks,
        int activeTicksRemaining,
        int activeDurationTicks,
        float radius,
        float damageMultiplier)
    {
        if (ClassId != PlayerClass.Soldier)
        {
            ResetBuffBannerState();
            return;
        }

        BuffBannerMaxChargeKills = Math.Max(1, maxChargeKills);
        BuffBannerChargeKills = Math.Clamp(chargeKills, 0, BuffBannerMaxChargeKills);
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
    }

    internal void ResetBuffBannerState()
    {
        BuffBannerChargeKills = 0;
        BuffBannerMaxChargeKills = BuffBannerDefaultMaxChargeKills;
        BuffBannerDeployTicksRemaining = 0;
        BuffBannerDeployDurationTicks = BuffBannerDefaultDeployTicks;
        BuffBannerActiveTicksRemaining = 0;
        BuffBannerActiveDurationTicks = BuffBannerDefaultActiveTicks;
        BuffBannerRadius = BuffBannerDefaultRadius;
        BuffBannerDamageMultiplier = BuffBannerDefaultDamageMultiplier;
    }
}
