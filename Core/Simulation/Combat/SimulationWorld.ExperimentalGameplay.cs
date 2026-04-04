using System;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private bool IsExperimentalPracticePowerOwner(PlayerEntity? player)
    {
        return player is not null
            && ReferenceEquals(player, LocalPlayer);
    }

    private int GetExperimentalDamageBuffTicks()
    {
        return Math.Max(1, (int)MathF.Round(Config.TicksPerSecond * ExperimentalGameplaySettings.OnDamageBuffDurationSeconds));
    }

    private int GetExperimentalKillBuffTicks()
    {
        return Math.Max(1, (int)MathF.Round(Config.TicksPerSecond * ExperimentalGameplaySettings.OnKillBuffDurationSeconds));
    }

    private int GetExperimentalKillInvulnerabilityTicks()
    {
        return Math.Max(1, (int)MathF.Round(Config.TicksPerSecond * ExperimentalGameplaySettings.KillInvincibilityDurationSeconds));
    }

    private float GetExperimentalPassiveHealthRegenerationPerTick()
    {
        return ExperimentalGameplaySettings.PassiveHealthRegenerationPerSecond / Math.Max(1, Config.TicksPerSecond);
    }

    private void ApplyExperimentalDamageRewards(PlayerEntity? attacker, PlayerEntity target, int appliedDamage)
    {
        if (appliedDamage <= 0
            || attacker is null
            || !IsExperimentalPracticePowerOwner(attacker)
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team)
        {
            return;
        }

        if (ExperimentalGameplaySettings.EnableHealOnDamage)
        {
            ApplyExperimentalHealingReward(attacker, appliedDamage * ExperimentalGameplaySettings.HealOnDamageFraction);
        }

        if (ExperimentalGameplaySettings.EnableRateOfFireMultiplierOnDamage)
        {
            attacker.TryRequeuePrimaryFire();
        }

        if (ExperimentalGameplaySettings.EnableSpeedOnDamage)
        {
            attacker.GrantExperimentalMovementBoost(
                GetExperimentalDamageBuffTicks(),
                ExperimentalGameplaySettings.SpeedBoostMultiplier);
        }
    }

    private void ApplyExperimentalKillRewards(PlayerEntity? killer, PlayerEntity victim)
    {
        if (killer is null
            || !IsExperimentalPracticePowerOwner(killer)
            || ReferenceEquals(killer, victim)
            || killer.Team == victim.Team)
        {
            return;
        }

        if (ExperimentalGameplaySettings.EnableHealOnKill)
        {
            ApplyExperimentalHealingReward(killer, ExperimentalGameplaySettings.HealOnKillAmount);
        }

        if (ExperimentalGameplaySettings.EnableSpeedOnKill)
        {
            killer.GrantExperimentalMovementBoost(
                GetExperimentalKillBuffTicks(),
                ExperimentalGameplaySettings.SpeedBoostMultiplier);
        }

        if (ExperimentalGameplaySettings.EnableInvincibilityOnKill)
        {
            killer.RefreshUber(GetExperimentalKillInvulnerabilityTicks());
        }
    }

    private void TryApplyExperimentalSoldierRocketHitReloadReward(PlayerEntity? attacker, RocketProjectileEntity rocket, bool hitEnemyPlayer)
    {
        if (!hitEnemyPlayer
            || attacker is null
            || !IsExperimentalPracticePowerOwner(attacker)
            || attacker.ClassId != PlayerClass.Soldier
            || !ExperimentalGameplaySettings.EnableSoldierInstantReload
            || !rocket.CanGrantExperimentalInstantReloadOnHit)
        {
            return;
        }

        attacker.TryInstantlyRefillPrimaryAmmo();
    }

    private void ApplyExperimentalHealingReward(PlayerEntity player, float healing)
    {
        ApplyHealingWithFeedback(player, healing);
    }

    private void ApplyExperimentalPassivePlayerEffects(PlayerEntity player)
    {
        if (!IsExperimentalPracticePowerOwner(player))
        {
            return;
        }

        if (!ExperimentalGameplaySettings.EnablePassiveHealthRegeneration)
        {
            return;
        }

        player.ApplyContinuousHealingAndGetAmount(GetExperimentalPassiveHealthRegenerationPerTick());
    }

    private float ApplyExperimentalProjectileSpeedMultiplier(PlayerEntity attacker, float launchSpeed)
    {
        if (!ExperimentalGameplaySettings.EnableProjectileSpeedMultiplier
            || !IsExperimentalPracticePowerOwner(attacker)
            || launchSpeed <= 0f)
        {
            return launchSpeed;
        }

        return launchSpeed * ExperimentalGameplaySettings.ProjectileSpeedMultiplier;
    }

    private (float VelocityX, float VelocityY) ApplyExperimentalProjectileSpeedMultiplier(
        PlayerEntity attacker,
        float launchVelocityX,
        float launchVelocityY)
    {
        if (!ExperimentalGameplaySettings.EnableProjectileSpeedMultiplier
            || !IsExperimentalPracticePowerOwner(attacker))
        {
            return (launchVelocityX, launchVelocityY);
        }

        return (
            launchVelocityX * ExperimentalGameplaySettings.ProjectileSpeedMultiplier,
            launchVelocityY * ExperimentalGameplaySettings.ProjectileSpeedMultiplier);
    }

    private int ApplyExperimentalAirshotDamageMultiplier(
        PlayerEntity? attacker,
        PlayerEntity target,
        int baseDamage,
        out DamageEventFlags damageFlags)
    {
        damageFlags = DamageEventFlags.None;
        if (baseDamage <= 0
            || attacker is null
            || !ExperimentalGameplaySettings.EnableAirshotDamageMultiplier
            || !IsExperimentalPracticePowerOwner(attacker)
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team
            || target.IsGrounded)
        {
            return baseDamage;
        }

        damageFlags = DamageEventFlags.Airshot;
        return Math.Max(1, (int)MathF.Round(baseDamage * ExperimentalGameplaySettings.AirshotDamageMultiplier));
    }
}
