using System;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool IsPlayerInsideCapturedPointHealingAuraForVisuals(PlayerEntity? player)
    {
        return player is not null
            && IsExperimentalPracticePowerOwner(player)
            && ExperimentalGameplaySettings.EnableCapturedPointHealingAura
            && player.IsAlive
            && IsPlayerInsideCapturedPointHealingAura(player);
    }

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

    private float GetExperimentalCapturedPointHealingPerTick()
    {
        return ExperimentalGameplaySettings.CapturedPointHealingPerSecond / Math.Max(1, Config.TicksPerSecond);
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

        if (ExperimentalGameplaySettings.EnableRage)
        {
            attacker.AddRageCharge(
                appliedDamage * ExperimentalGameplaySettings.RageDamageDealtChargeMultiplier,
                ExperimentalGameplaySettings.RageMaxCharge);
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

    private void ApplyExperimentalDamageTakenRewards(PlayerEntity target, PlayerEntity? attacker, int appliedDamage)
    {
        if (appliedDamage <= 0
            || attacker is null
            || !ExperimentalGameplaySettings.EnableRage
            || !IsExperimentalPracticePowerOwner(target)
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team)
        {
            return;
        }

        target.AddRageCharge(
            appliedDamage * ExperimentalGameplaySettings.RageDamageReceivedChargeMultiplier,
            ExperimentalGameplaySettings.RageMaxCharge);
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

        if (ExperimentalGameplaySettings.EnableFullHealOnKill)
        {
            killer.ForceSetHealth(killer.MaxHealth);
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

    private int ApplyExperimentalIncomingDamageMultiplier(PlayerEntity target, int damage)
    {
        if (damage <= 0
            || !IsExperimentalPracticePowerOwner(target)
            || !target.IsExperimentalDemoknightCharging)
        {
            return damage;
        }

        return Math.Max(
            1,
            (int)MathF.Round(damage * ExperimentalGameplaySettings.DemoknightChargeDamageTakenMultiplier));
    }

    private float ApplyExperimentalIncomingDamageMultiplier(PlayerEntity target, float damage)
    {
        if (damage <= 0f
            || !IsExperimentalPracticePowerOwner(target)
            || !target.IsExperimentalDemoknightCharging)
        {
            return damage;
        }

        return MathF.Max(
            0.01f,
            damage * ExperimentalGameplaySettings.DemoknightChargeDamageTakenMultiplier);
    }

    private void ApplyExperimentalPassivePlayerEffects(PlayerEntity player)
    {
        if (!IsExperimentalPracticePowerOwner(player))
        {
            return;
        }

        if (ExperimentalGameplaySettings.EnablePassiveHealthRegeneration)
        {
            player.ApplyContinuousHealingAndGetAmount(GetExperimentalPassiveHealthRegenerationPerTick());
        }

        if (ExperimentalGameplaySettings.EnableCapturedPointHealingAura
            && IsPlayerInsideCapturedPointHealingAura(player))
        {
            player.ApplyContinuousHealingAndGetAmount(GetExperimentalCapturedPointHealingPerTick());
        }
    }

    private bool IsPlayerInsideCapturedPointHealingAura(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return false;
        }

        for (var pointIndex = 0; pointIndex < _controlPoints.Count; pointIndex += 1)
        {
            var point = _controlPoints[pointIndex];
            if (!point.HasHealingAura || point.Team != player.Team)
            {
                continue;
            }

            for (var zoneIndex = 0; zoneIndex < _controlPointZones.Count; zoneIndex += 1)
            {
                var zone = _controlPointZones[zoneIndex];
                if (zone.ControlPointIndex != pointIndex)
                {
                    continue;
                }

                if (player.IntersectsMarker(zone.Marker.CenterX, zone.Marker.CenterY, zone.Marker.Width, zone.Marker.Height))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private float ApplyExperimentalProjectileSpeedMultiplier(PlayerEntity attacker, float launchSpeed)
    {
        if (launchSpeed <= 0f)
        {
            return launchSpeed;
        }

        var speedScale = _configuredProjectileSpeedScale;
        if (ExperimentalGameplaySettings.EnableProjectileSpeedMultiplier
            && IsExperimentalPracticePowerOwner(attacker))
        {
            speedScale *= ExperimentalGameplaySettings.ProjectileSpeedMultiplier;
        }

        return launchSpeed * speedScale;
    }

    private (float VelocityX, float VelocityY) ApplyExperimentalProjectileSpeedMultiplier(
        PlayerEntity attacker,
        float launchVelocityX,
        float launchVelocityY)
    {
        var speedScale = _configuredProjectileSpeedScale;
        if (ExperimentalGameplaySettings.EnableProjectileSpeedMultiplier
            && IsExperimentalPracticePowerOwner(attacker))
        {
            speedScale *= ExperimentalGameplaySettings.ProjectileSpeedMultiplier;
        }

        return (
            launchVelocityX * speedScale,
            launchVelocityY * speedScale);
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
