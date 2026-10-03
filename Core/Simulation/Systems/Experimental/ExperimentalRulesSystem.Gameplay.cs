namespace OpenGarrison.Core;

internal sealed partial class ExperimentalRulesSystem
{
    private enum ExperimentalDamageKind
    {
        Generic,
        Bullet,
        Explosive,
        Fire,
    }

    internal bool IsPlayerInsideCapturedPointHealingAuraForVisuals(PlayerEntity? player)
    {
        return player is not null
            && player.IsAlive
            && IsPlayerInsideCapturedPointHealingAura(player);
    }

    internal bool IsExperimentalPracticePowerOwner(PlayerEntity? player)
    {
        if (player is null)
        {
            return false;
        }

        // Hosted Last to Die participants own their experimental profile by
        // network slot. The local-player check remains the practice/offline
        // fallback for worlds without an authoritative Last to Die build.
        return ReferenceEquals(player, _host.LocalPlayer)
            || (_host.NetworkPlayerRules.TryGetPlayerNetworkSlot(player, out var slot)
                && _host.LastToDieRules.TryGetLastToDieLegacyGameplaySettings(slot, out _));
    }

    private int GetExperimentalDamageBuffTicks()
    {
        return Math.Max(1, (int)MathF.Round(_host.Config.TicksPerSecond * ExperimentalGameplaySettings.OnDamageBuffDurationSeconds));
    }

    private int GetExperimentalKillBuffTicks()
    {
        return Math.Max(1, (int)MathF.Round(_host.Config.TicksPerSecond * ExperimentalGameplaySettings.OnKillBuffDurationSeconds));
    }

    private int GetExperimentalKillInvulnerabilityTicks(PlayerEntity player)
    {
        return Math.Max(1, (int)MathF.Round(_host.Config.TicksPerSecond * _host.LastToDieRules.GetLastToDieGameplaySettings(player).KillInvincibilityDurationSeconds));
    }

    private int GetExperimentalRageExtensionTicksPerKill()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                _host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierRageExtensionSecondsPerKill));
    }

    private int GetExperimentalSoldierLuckyBastardInvulnerabilityTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                _host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierLuckyBastardInvincibilityDurationSeconds));
    }

    private int GetExperimentalSoldierFogOfWarWindowTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Round(
                _host.Config.TicksPerSecond * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierFogOfWarWindowSeconds));
    }

    private float GetExperimentalPassiveHealthRegenerationPerTick(PlayerEntity player)
    {
        return _host.LastToDieRules.GetLastToDieGameplaySettings(player).PassiveHealthRegenerationPerSecond / Math.Max(1, _host.Config.TicksPerSecond);
    }

    private float GetExperimentalCapturedPointHealingPerTick()
    {
        return ExperimentalGameplaySettings.CapturedPointHealingPerSecond / Math.Max(1, _host.Config.TicksPerSecond);
    }

    internal void ApplyExperimentalDamageRewards(PlayerEntity? attacker, PlayerEntity target, int appliedDamage, bool allowOsmosisHealOwnedSentries = true)
    {
        if (appliedDamage <= 0
            || attacker is null
            || !IsExperimentalPracticePowerOwner(attacker)
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team)
        {
            return;
        }

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(attacker);

        if (settings.EnableHealOnDamage)
        {
            ApplyExperimentalHealingReward(attacker, appliedDamage * settings.HealOnDamageFraction);
        }

        if (attacker.IsAcquiredWeaponEquipped && settings.AcquiredWeaponHealingMultiplier > 1f)
        {
            ApplyExperimentalHealingReward(
                attacker,
                appliedDamage * (settings.AcquiredWeaponHealingMultiplier - 1f));
        }

        if (settings.EnableRage)
        {
            attacker.AddRageCharge(
                appliedDamage * global::OpenGarrison.Core.ExperimentalGameplaySettings.RageDamageDealtChargeMultiplier,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.RageMaxCharge);
        }

        if (settings.EnableRateOfFireMultiplierOnDamage)
        {
            attacker.TryRequeuePrimaryFire();
        }

        if (settings.EnableSpeedOnDamage)
        {
            attacker.GrantExperimentalMovementBoost(
                GetExperimentalDamageBuffTicks(),
                global::OpenGarrison.Core.ExperimentalGameplaySettings.SpeedBoostMultiplier);
        }

        if (allowOsmosisHealOwnedSentries)
        {
            ApplyExperimentalPlayerDamageToOwnedSentries(attacker, appliedDamage);
        }

        ApplyExperimentalEngineerPlayerDamageRewards(attacker, appliedDamage);
    }

    internal void ApplyExperimentalDamageTakenRewards(PlayerEntity target, PlayerEntity? attacker, int appliedDamage)
    {
        if (appliedDamage <= 0
            || attacker is null
            || !IsExperimentalPracticePowerOwner(target)
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team)
        {
            return;
        }

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(target);

        if (settings.EnableRage)
        {
            target.AddRageCharge(
                appliedDamage * global::OpenGarrison.Core.ExperimentalGameplaySettings.RageDamageReceivedChargeMultiplier,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.RageMaxCharge);
        }

        if (settings.EnableSoldierFogOfWar
            && target.ClassId == PlayerClass.Soldier)
        {
            target.RefreshExperimentalFogOfWar(
                GetExperimentalSoldierFogOfWarWindowTicks(),
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierFogOfWarInitialEvasionChance);
        }

        TryApplyExperimentalThornsDamage(target, attacker, appliedDamage);
    }

    internal void ApplyExperimentalKillRewards(PlayerEntity? killer, PlayerEntity victim)
    {
        if (killer is null
            || !IsExperimentalPracticePowerOwner(killer)
            || ReferenceEquals(killer, victim)
            || killer.Team == victim.Team)
        {
            return;
        }

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(killer);

        if (settings.EnableHealOnKill)
        {
            ApplyExperimentalHealingReward(killer, settings.HealOnKillAmount);
        }

        if (settings.EnableFullHealOnKill)
        {
            killer.ForceSetHealth(killer.MaxHealth);
        }

        if (settings.EnableSpeedOnKill)
        {
            killer.GrantExperimentalMovementBoost(
                GetExperimentalKillBuffTicks(),
                global::OpenGarrison.Core.ExperimentalGameplaySettings.SpeedBoostMultiplier);
        }

        if (settings.EnableInvincibilityOnKill && !killer.IsCarryingIntel)
        {
            killer.RefreshUber(GetExperimentalKillInvulnerabilityTicks(killer));
        }

        if (settings.EnableGhostPhaseOnKill)
        {
            killer.StartExperimentalGhostPhase(GetExperimentalKillInvulnerabilityTicks(killer));
        }

        if (settings.EnableSoldierRageExtensionOnKill
            && killer.ClassId == PlayerClass.Soldier
            && killer.IsRaging)
        {
            killer.ExtendRageDuration(GetExperimentalRageExtensionTicksPerKill());
        }
    }

    internal void TryApplyExperimentalSoldierRocketHitReloadReward(PlayerEntity? attacker, RocketProjectileEntity rocket, bool hitEnemyPlayer)
    {
        if (!hitEnemyPlayer
            || attacker is null
            || !IsExperimentalPracticePowerOwner(attacker)
            || attacker.ClassId != PlayerClass.Soldier
            || !_host.LastToDieRules.GetLastToDieGameplaySettings(attacker).EnableSoldierInstantReload
            || !rocket.CanGrantExperimentalInstantReloadOnHit)
        {
            return;
        }

        attacker.TryInstantlyRefillPrimaryAmmo();
    }

    private void ApplyExperimentalHealingReward(PlayerEntity player, float healing)
    {
        _host.DamageRules.ApplyHealingWithFeedback(player, healing);
    }

    internal bool TryConvertExperimentalSelfDamageToHealing(PlayerEntity target, PlayerEntity? attacker, float healingAmount)
    {
        if (attacker is null
            || attacker.Id != target.Id
            || !_host.LastToDieRules.GetLastToDieGameplaySettings(target).EnableSelfDamageHealing
            || !target.CanConvertExperimentalSelfDamageToHealing()
            || healingAmount <= 0f)
        {
            return false;
        }

        ApplyExperimentalHealingReward(target, healingAmount);
        return true;
    }

    internal float ApplyExperimentalSoldierRocketLaunchSpeed(PlayerEntity attacker, float launchSpeed)
    {
        var adjustedSpeed = ApplyExperimentalProjectileSpeedMultiplier(attacker, launchSpeed);
        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(attacker);
        if (adjustedSpeed <= 0f
            || !settings.EnableSoldierStingerRockets
            || !IsExperimentalPracticePowerOwner(attacker)
            || attacker.ClassId != PlayerClass.Soldier)
        {
            return adjustedSpeed;
        }

        return adjustedSpeed * global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierStingerRocketSpeedMultiplier;
    }

    internal RocketCombatDefinition ApplyExperimentalSoldierRocketCombat(PlayerEntity attacker, RocketCombatDefinition? rocketCombat)
    {
        _ = _host.Frame;
        _ = attacker;
        return rocketCombat ?? new RocketCombatDefinition();
    }

    internal static float GetExperimentalSoldierStingerTurnRateRadians()
    {
        return global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierStingerRocketTurnRateDegrees * (MathF.PI / 180f);
    }

    internal int ApplyExperimentalIncomingDamageMultiplier(PlayerEntity target, PlayerEntity? attacker, int damage)
    {
        if (damage <= 0
            || !IsExperimentalPracticePowerOwner(target))
        {
            return damage;
        }

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(target);
        var multiplier = 1f;
        if (target.IsExperimentalDemoknightCharging)
        {
            multiplier *= settings.DemoknightChargeDamageTakenMultiplier;
        }

        multiplier *= target.ExperimentalDamageTakenMultiplier;
        multiplier *= 1f - Math.Clamp(settings.PassiveDamageResistance, 0f, 0.95f);
        multiplier *= 1f - GetExperimentalTypedResistanceForKind(
            ResolveExperimentalDamageKind(attacker),
            settings);
        return Math.Max(1, (int)MathF.Round(damage * multiplier));
    }

    internal float ApplyExperimentalIncomingDamageMultiplier(PlayerEntity target, PlayerEntity? attacker, float damage)
    {
        if (damage <= 0f
            || !IsExperimentalPracticePowerOwner(target))
        {
            return damage;
        }

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(target);
        var multiplier = 1f;
        if (target.IsExperimentalDemoknightCharging)
        {
            multiplier *= settings.DemoknightChargeDamageTakenMultiplier;
        }

        multiplier *= target.ExperimentalDamageTakenMultiplier;
        multiplier *= 1f - Math.Clamp(settings.PassiveDamageResistance, 0f, 0.95f);
        multiplier *= 1f - GetExperimentalTypedResistanceForKind(
            ResolveExperimentalDamageKind(attacker),
            settings);
        return MathF.Max(0.01f, damage * multiplier);
    }

    internal int ApplyExperimentalOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, int damage)
    {
        if (damage <= 0)
        {
            return damage;
        }

        var multiplier = GetExperimentalOutgoingDamageMultiplier(attacker, target);
        return Math.Max(1, (int)MathF.Round(damage * multiplier));
    }

    internal float ApplyExperimentalOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, float damage)
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
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team)
        {
            return 1f;
        }

        var multiplier = attacker.ExperimentalFreezeRayOutgoingDamageMultiplier
            * attacker.LastToDieEnemyDamageMultiplier;
        if (!IsExperimentalPracticePowerOwner(attacker))
        {
            return MathF.Max(0.01f, multiplier);
        }

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(attacker);
        var damageKind = ResolveExperimentalDamageKind(attacker);
        multiplier *= settings.PassiveDamageMultiplier;
        multiplier *= GetExperimentalTypedDamageMultiplierForKind(damageKind, settings);
        if (ShouldApplyExperimentalSoldierBattleborn(attacker, target))
        {
            multiplier *= 1f + Math.Max(0, attacker.CurrentCombo) / 100f;
        }

        if (attacker.IsAcquiredWeaponEquipped)
        {
            multiplier *= settings.AcquiredWeaponDamageMultiplier;
        }

        if (settings.PassiveDamageTargetClassId == target.ClassId)
        {
            multiplier *= settings.PassiveDamageToTargetClassMultiplier;
        }

        return MathF.Max(0.01f, multiplier);
    }

    private bool ShouldApplyExperimentalSoldierBattleborn(PlayerEntity? attacker, PlayerEntity target)
    {
        return attacker is not null
            && _host.LastToDieRules.GetLastToDieGameplaySettings(attacker).EnableSoldierBattleborn
            && IsExperimentalPracticePowerOwner(attacker)
            && attacker.ClassId == PlayerClass.Soldier
            && !ReferenceEquals(attacker, target)
            && attacker.Team != target.Team
            && attacker.CurrentCombo > 0;
    }

    internal float GetExperimentalTotalEvasionChance(PlayerEntity target)
    {
        if (!IsExperimentalPracticePowerOwner(target))
        {
            return 0f;
        }

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(target);
        return Math.Clamp(
            settings.PassiveEvasionChance
                + target.ExperimentalFogOfWarEvasionChance
                + GetExperimentalEngineerMisdirectionFieldEvasionChance(target),
            0f,
            0.95f);
    }

    private float GetExperimentalEngineerMisdirectionFieldEvasionChance(PlayerEntity target)
    {
        return _host.LastToDieRules.GetLastToDieGameplaySettings(target).EnableEngineerMisdirectionField
            && IsPlayerNearExperimentalOwnedSentry(target)
            ? global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerMisdirectionFieldEvasionChance
            : 0f;
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
            || _host.LastToDieRules.GetLastToDieGameplaySettings(target).PassiveThornsFraction <= 0f)
        {
            return;
        }

        var thornsDamage = Math.Max(
            1,
            (int)MathF.Round(
                appliedDamage * _host.LastToDieRules.GetLastToDieGameplaySettings(target).PassiveThornsFraction));
        _host.ApplyPlayerDamageWithContext(
            attacker,
            thornsDamage,
            target,
            additionalTraits: PlayerDamageTraits.Reflected);
    }

    internal bool TryPreventExperimentalFatalDamage(PlayerEntity target, int damage)
    {
        if (damage <= 0
            || damage < target.Health
            || !_host.LastToDieRules.GetLastToDieGameplaySettings(target).EnableSoldierLuckyBastard
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

    internal void ApplyExperimentalPassivePlayerEffects(PlayerEntity player)
    {
        // The captured-point aura is an objective effect, not a loadout perk.
        // Apply it to every living player standing on a point owned by their
        // team, including remote co-op participants on an authoritative server.
        if (_host.LastToDieRules.IsLastToDieGameplaySettingEnabled(settings => settings.EnableCapturedPointHealingAura)
            && IsPlayerInsideCapturedPointHealingAura(player))
        {
            player.ApplyContinuousHealingAndGetAmount(GetExperimentalCapturedPointHealingPerTick());
        }

        if (!IsExperimentalPracticePowerOwner(player))
        {
            return;
        }

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(player);
        if (settings.EnablePassiveHealthRegeneration)
        {
            player.ApplyContinuousHealingAndGetAmount(GetExperimentalPassiveHealthRegenerationPerTick(player));
        }

        ApplyExperimentalEngineerPassivePlayerEffects(player);
    }

    private static ExperimentalDamageKind ResolveExperimentalDamageKind(PlayerEntity? attacker)
    {
        if (attacker is null)
        {
            return ExperimentalDamageKind.Generic;
        }

        var weaponDefinition = attacker.IsAcquiredWeaponEquipped
            ? attacker.AcquiredWeapon
            : attacker.IsExperimentalOffhandSelected
                ? attacker.ExperimentalOffhandWeapon
                : attacker.PrimaryWeapon;
        if (weaponDefinition is null)
        {
            return ExperimentalDamageKind.Generic;
        }

        return weaponDefinition.Kind switch
        {
            PrimaryWeaponKind.PelletGun or PrimaryWeaponKind.Minigun or PrimaryWeaponKind.Rifle or PrimaryWeaponKind.Revolver => ExperimentalDamageKind.Bullet,
            PrimaryWeaponKind.RocketLauncher or PrimaryWeaponKind.MineLauncher => ExperimentalDamageKind.Explosive,
            PrimaryWeaponKind.FlameThrower => ExperimentalDamageKind.Fire,
            _ => ExperimentalDamageKind.Generic,
        };
    }

    private float GetExperimentalTypedDamageMultiplierForKind(
        ExperimentalDamageKind damageKind,
        ExperimentalGameplaySettings? settings = null)
    {
        settings ??= _host.ExperimentalGameplaySettings;
        return damageKind switch
        {
            ExperimentalDamageKind.Bullet => settings.PassiveBulletDamageMultiplier,
            ExperimentalDamageKind.Explosive => settings.PassiveExplosiveDamageMultiplier,
            _ => 1f,
        };
    }

    private float GetExperimentalTypedResistanceForKind(
        ExperimentalDamageKind damageKind,
        ExperimentalGameplaySettings? settings = null)
    {
        settings ??= _host.ExperimentalGameplaySettings;
        return damageKind switch
        {
            ExperimentalDamageKind.Bullet => Math.Clamp(settings.PassiveBulletResistance, 0f, 0.95f),
            ExperimentalDamageKind.Explosive => Math.Clamp(settings.PassiveExplosiveResistance, 0f, 0.95f),
            ExperimentalDamageKind.Fire => Math.Clamp(settings.PassiveFireResistance, 0f, 0.95f),
            _ => 0f,
        };
    }

    private bool IsPlayerInsideCapturedPointHealingAura(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return false;
        }

        for (var pointIndex = 0; pointIndex < _host.Objectives.ControlPoints.Points.Count; pointIndex += 1)
        {
            var point = _host.Objectives.ControlPoints.Points[pointIndex];
            if (!point.HasHealingAura || point.Team != player.Team)
            {
                continue;
            }

            for (var zoneIndex = 0; zoneIndex < _host.Objectives.ControlPoints.Zones.Count; zoneIndex += 1)
            {
                var zone = _host.Objectives.ControlPoints.Zones[zoneIndex];
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

    internal float ApplyExperimentalProjectileSpeedMultiplier(PlayerEntity attacker, float launchSpeed)
    {
        if (launchSpeed <= 0f)
        {
            return launchSpeed;
        }

        var speedScale = _host.MatchSettings.ProjectileSpeedScale;
        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(attacker);
        if (settings.EnableProjectileSpeedMultiplier
            && IsExperimentalPracticePowerOwner(attacker))
        {
            speedScale *= settings.ProjectileSpeedMultiplierValue;
        }

        return launchSpeed * speedScale;
    }

    internal (float VelocityX, float VelocityY) ApplyExperimentalProjectileSpeedMultiplier(
        PlayerEntity attacker,
        float launchVelocityX,
        float launchVelocityY)
    {
        var speedScale = _host.MatchSettings.ProjectileSpeedScale;
        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(attacker);
        if (settings.EnableProjectileSpeedMultiplier
            && IsExperimentalPracticePowerOwner(attacker))
        {
            speedScale *= settings.ProjectileSpeedMultiplierValue;
        }

        return (
            launchVelocityX * speedScale,
            launchVelocityY * speedScale);
    }

    internal int ApplyExperimentalAirshotDamageMultiplier(
        PlayerEntity? attacker,
        PlayerEntity target,
        int baseDamage,
        out DamageEventFlags damageFlags)
    {
        damageFlags = DamageEventFlags.None;
        if (baseDamage <= 0
            || attacker is null
            || !_host.LastToDieRules.GetLastToDieGameplaySettings(attacker).EnableAirshotDamageMultiplier
            || !IsExperimentalPracticePowerOwner(attacker)
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team
            || target.IsGrounded)
        {
            return baseDamage;
        }

        damageFlags = DamageEventFlags.Airshot;
        return Math.Max(
            1,
            (int)MathF.Round(
                baseDamage * _host.LastToDieRules.GetLastToDieGameplaySettings(attacker).AirshotDamageMultiplierValue));
    }
}
