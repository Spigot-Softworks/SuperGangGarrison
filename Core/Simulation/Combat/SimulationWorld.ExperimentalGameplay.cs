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

    private int GetExperimentalRageExtensionTicksPerKill()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierRageExtensionSecondsPerKill));
    }

    private int GetExperimentalSoldierLuckyBastardInvulnerabilityTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierLuckyBastardInvincibilityDurationSeconds));
    }

    private int GetExperimentalSoldierFogOfWarWindowTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierFogOfWarWindowSeconds));
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

        if (attacker.AcquiredWeaponClassId.HasValue && ExperimentalGameplaySettings.AcquiredWeaponHealingMultiplier > 1f)
        {
            ApplyExperimentalHealingReward(
                attacker,
                appliedDamage * (ExperimentalGameplaySettings.AcquiredWeaponHealingMultiplier - 1f));
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
            || !IsExperimentalPracticePowerOwner(target)
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team)
        {
            return;
        }

        if (ExperimentalGameplaySettings.EnableRage)
        {
            target.AddRageCharge(
                appliedDamage * ExperimentalGameplaySettings.RageDamageReceivedChargeMultiplier,
                ExperimentalGameplaySettings.RageMaxCharge);
        }

        if (ExperimentalGameplaySettings.EnableSoldierFogOfWar
            && target.ClassId == PlayerClass.Soldier)
        {
            target.RefreshExperimentalFogOfWar(
                GetExperimentalSoldierFogOfWarWindowTicks(),
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierFogOfWarInitialEvasionChance);
        }

        TryApplyExperimentalThornsDamage(target, attacker, appliedDamage);
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

        if (ExperimentalGameplaySettings.EnableGhostPhaseOnKill)
        {
            killer.StartExperimentalGhostPhase(GetExperimentalKillInvulnerabilityTicks());
        }

        if (ExperimentalGameplaySettings.EnableSoldierRageExtensionOnKill
            && killer.ClassId == PlayerClass.Soldier
            && killer.IsRaging)
        {
            killer.ExtendRageDuration(GetExperimentalRageExtensionTicksPerKill());
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

    private bool TryConvertExperimentalSelfDamageToHealing(PlayerEntity target, PlayerEntity? attacker, float healingAmount)
    {
        if (attacker is null
            || attacker.Id != target.Id
            || !ExperimentalGameplaySettings.EnableSelfDamageHealing
            || !target.CanConvertExperimentalSelfDamageToHealing()
            || healingAmount <= 0f)
        {
            return false;
        }

        ApplyExperimentalHealingReward(target, healingAmount);
        return true;
    }

    private float ApplyExperimentalSoldierRocketLaunchSpeed(PlayerEntity attacker, float launchSpeed)
    {
        var adjustedSpeed = ApplyExperimentalProjectileSpeedMultiplier(attacker, launchSpeed);
        if (adjustedSpeed <= 0f
            || !ExperimentalGameplaySettings.EnableSoldierStingerRockets
            || !IsExperimentalPracticePowerOwner(attacker)
            || attacker.ClassId != PlayerClass.Soldier)
        {
            return adjustedSpeed;
        }

        return adjustedSpeed * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierStingerRocketSpeedMultiplier;
    }

    private RocketCombatDefinition ApplyExperimentalSoldierRocketCombat(PlayerEntity attacker, RocketCombatDefinition? rocketCombat)
    {
        _ = Frame;
        _ = attacker;
        return rocketCombat ?? new RocketCombatDefinition();
    }

    private static float GetExperimentalSoldierStingerTurnRateRadians()
    {
        return global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierStingerRocketTurnRateDegrees * (MathF.PI / 180f);
    }

    private int ApplyExperimentalIncomingDamageMultiplier(PlayerEntity target, int damage)
    {
        if (damage <= 0
            || !IsExperimentalPracticePowerOwner(target))
        {
            return damage;
        }

        var multiplier = 1f;
        if (target.IsExperimentalDemoknightCharging)
        {
            multiplier *= ExperimentalGameplaySettings.DemoknightChargeDamageTakenMultiplier;
        }

        multiplier *= 1f - Math.Clamp(ExperimentalGameplaySettings.PassiveDamageResistance, 0f, 0.95f);
        return Math.Max(1, (int)MathF.Round(damage * multiplier));
    }

    private float ApplyExperimentalIncomingDamageMultiplier(PlayerEntity target, float damage)
    {
        if (damage <= 0f
            || !IsExperimentalPracticePowerOwner(target))
        {
            return damage;
        }

        var multiplier = 1f;
        if (target.IsExperimentalDemoknightCharging)
        {
            multiplier *= ExperimentalGameplaySettings.DemoknightChargeDamageTakenMultiplier;
        }

        multiplier *= 1f - Math.Clamp(ExperimentalGameplaySettings.PassiveDamageResistance, 0f, 0.95f);
        return MathF.Max(0.01f, damage * multiplier);
    }

    private int ApplyExperimentalOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, int damage)
    {
        if (damage <= 0)
        {
            return damage;
        }

        var multiplier = GetExperimentalOutgoingDamageMultiplier(attacker, target);
        return Math.Max(1, (int)MathF.Round(damage * multiplier));
    }

    private float ApplyExperimentalOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, float damage)
    {
        if (damage <= 0f)
        {
            return damage;
        }

        var multiplier = GetExperimentalOutgoingDamageMultiplier(attacker, target);
        return MathF.Max(0.01f, damage * multiplier);
    }

    private float GetExperimentalOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target)
    {
        if (attacker is null
            || !IsExperimentalPracticePowerOwner(attacker)
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team)
        {
            return 1f;
        }

        var multiplier = ExperimentalGameplaySettings.PassiveDamageMultiplier;
        if (ShouldApplyExperimentalSoldierBattleborn(attacker, target))
        {
            multiplier *= 1f + Math.Max(0, attacker.CurrentCombo) / 100f;
        }

        if (attacker.AcquiredWeaponClassId.HasValue)
        {
            multiplier *= ExperimentalGameplaySettings.AcquiredWeaponDamageMultiplier;
        }

        return MathF.Max(0.01f, multiplier);
    }

    private bool ShouldApplyExperimentalSoldierBattleborn(PlayerEntity? attacker, PlayerEntity target)
    {
        return attacker is not null
            && ExperimentalGameplaySettings.EnableSoldierBattleborn
            && IsExperimentalPracticePowerOwner(attacker)
            && attacker.ClassId == PlayerClass.Soldier
            && !ReferenceEquals(attacker, target)
            && attacker.Team != target.Team
            && attacker.CurrentCombo > 0;
    }

    private bool TryEvadeExperimentalDamage(
        PlayerEntity target,
        PlayerEntity? attacker,
        float damage,
        DamageEventFlags damageFlags)
    {
        if (damage <= 0f
            || attacker is null
            || !IsExperimentalPracticePowerOwner(target)
            || target.ClassId != PlayerClass.Soldier
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team
            || GetExperimentalTotalEvasionChance(target) <= 0f
            || _random.NextDouble() >= GetExperimentalTotalEvasionChance(target))
        {
            return false;
        }

        RegisterDamageEvent(
            attacker,
            DamageTargetKind.Player,
            target.Id,
            target.X,
            target.Y,
            amount: 0,
            wasFatal: false,
            target,
            damageFlags | DamageEventFlags.Evaded);
        return true;
    }

    private float GetExperimentalTotalEvasionChance(PlayerEntity target)
    {
        if (!IsExperimentalPracticePowerOwner(target))
        {
            return 0f;
        }

        return Math.Clamp(
            ExperimentalGameplaySettings.PassiveEvasionChance + target.ExperimentalFogOfWarEvasionChance,
            0f,
            0.95f);
    }

    private void TryApplyExperimentalThornsDamage(PlayerEntity target, PlayerEntity? attacker, int appliedDamage)
    {
        if (appliedDamage <= 0
            || attacker is null
            || !target.IsAlive
            || !attacker.IsAlive
            || !IsExperimentalPracticePowerOwner(target)
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team
            || ExperimentalGameplaySettings.PassiveThornsFraction <= 0f)
        {
            return;
        }

        var thornsDamage = Math.Max(1, (int)MathF.Round(appliedDamage * ExperimentalGameplaySettings.PassiveThornsFraction));
        ApplyPlayerDamage(attacker, thornsDamage, target);
    }

    private bool TryPreventExperimentalFatalDamage(PlayerEntity target, int damage)
    {
        if (damage <= 0
            || damage < target.Health
            || !ExperimentalGameplaySettings.EnableSoldierLuckyBastard
            || !IsExperimentalPracticePowerOwner(target)
            || target.ClassId != PlayerClass.Soldier
            || target.IsExperimentalLuckyBastardActive
            || target.KillStreak < global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierLuckyBastardMinimumKills)
        {
            return false;
        }

        var reviveFraction = target.KillStreak switch
        {
            3 => 0.3f,
            4 => 0.5f,
            5 => 0.8f,
            _ => 1f,
        };
        var reviveHealth = Math.Max(1, (int)MathF.Ceiling(target.MaxHealth * reviveFraction));
        target.TriggerExperimentalLuckyBastard(
            GetExperimentalSoldierLuckyBastardInvulnerabilityTicks(),
            reviveHealth);
        return true;
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
            speedScale *= ExperimentalGameplaySettings.ProjectileSpeedMultiplierValue;
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
            speedScale *= ExperimentalGameplaySettings.ProjectileSpeedMultiplierValue;
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
        return Math.Max(1, (int)MathF.Round(baseDamage * ExperimentalGameplaySettings.AirshotDamageMultiplierValue));
    }
}
