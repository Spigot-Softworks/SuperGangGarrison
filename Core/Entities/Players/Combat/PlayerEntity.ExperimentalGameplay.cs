using System;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public const string ServerTuningReplicatedStateOwnerId = "serverruntime";
    public const string MovementSpeedScaleReplicatedStateKey = "movescale";
    public const string GravityScaleReplicatedStateKey = "gravityscale";

    private int ExperimentalMovementBoostTicksRemaining { get; set; }

    private int ExperimentalPrimaryCooldownBuffTicksRemaining { get; set; }

    private float ExperimentalMovementSpeedMultiplierValue { get; set; } = 1f;

    private float ExperimentalPrimaryCooldownMultiplierValue { get; set; } = 1f;

    private float ServerMovementSpeedScaleValue { get; set; } = 1f;

    private float ServerDamageScaleValue { get; set; } = 1f;

    private float ServerGravityScaleValue { get; set; } = 1f;

    private float ServerHorizontalSpeedClampPerTickValue { get; set; } = LegacyMovementModel.MaxStepSpeedPerTick;

    private float ServerVerticalSpeedClampPerTickValue { get; set; } = LegacyMovementModel.MaxStepSpeedPerTick;

    private float ExperimentalPassiveMovementSpeedMultiplierValue { get; set; } = 1f;

    private float ExperimentalJumpHeightMultiplierValue { get; set; } = 1f;

    private int ExperimentalBonusAirJumpsValue { get; set; }

    private float ExperimentalDemoknightSwordRangeMultiplierValue { get; set; } = 1f;

    private int ExperimentalDemoknightSwordBaseDamageValue { get; set; } = 42;

    private float ExperimentalDemoknightSwordDamageMultiplierValue { get; set; } = 1f;

    private float ExperimentalDemoknightSwordCooldownMultiplierValue { get; set; } = 1f;

    private float ExperimentalDemoknightChargeRechargeMultiplierValue { get; set; } = 1f;

    private float ExperimentalDemoknightChargeRechargeAccumulator { get; set; }

    private bool ExperimentalDemoknightChargeWantsLift { get; set; }

    private int ExperimentalLuckyBastardTicksRemaining { get; set; }

    private int ExperimentalLuckyBastardReviveHealth { get; set; }

    private int ExperimentalFogOfWarTicksRemaining { get; set; }

    private float ExperimentalFogOfWarEvasionChanceValue { get; set; }

    public float ServerMovementSpeedScale => ServerMovementSpeedScaleValue;

    public float ServerGravityScale => ServerGravityScaleValue;

    public bool IsExperimentalLuckyBastardActive => ExperimentalLuckyBastardTicksRemaining > 0;

    public bool IsExperimentalFogOfWarActive => ExperimentalFogOfWarTicksRemaining > 0;

    public float ExperimentalFogOfWarEvasionChance => ExperimentalFogOfWarTicksRemaining > 0 ? ExperimentalFogOfWarEvasionChanceValue : 0f;

    public void SetExperimentalDemoknightEnabled(bool enabled)
    {
        var nextEnabled = enabled && ClassId == PlayerClass.Demoman;
        if (IsExperimentalDemoknightEnabled == nextEnabled)
        {
            if (!nextEnabled)
            {
                IsExperimentalDemoknightCharging = false;
                ExperimentalDemoknightChargeTicksRemaining = 0;
                ResetExperimentalDemoknightChargeMovementState();
            }

            return;
        }

        IsExperimentalDemoknightEnabled = nextEnabled;
        IsExperimentalDemoknightCharging = false;
        ExperimentalDemoknightChargeTicksRemaining = nextEnabled ? ExperimentalDemoknightChargeMaxTicks : 0;
        ExperimentalDemoknightChargeRechargeAccumulator = 0f;
        ResetExperimentalDemoknightChargeMovementState();
    }

    public bool TryFireExperimentalDemoknightSword()
    {
        if (!IsExperimentalDemoknightEnabled
            || !IsAlive
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || PrimaryCooldownTicks > 0)
        {
            return false;
        }

        var swordCooldownTicks = Math.Max(
            1,
            (int)MathF.Round(ExperimentalDemoknightSwordCooldownTicks * ExperimentalDemoknightSwordCooldownMultiplierValue));
        PrimaryCooldownTicks = ApplyExperimentalPrimaryCooldownMultiplier(swordCooldownTicks);
        return true;
    }

    public bool TryStartExperimentalDemoknightCharge()
    {
        if (!IsExperimentalDemoknightEnabled
            || !IsAlive
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || IsExperimentalDemoknightCharging
            || ExperimentalDemoknightChargeTicksRemaining < ExperimentalDemoknightChargeMaxTicks)
        {
            return false;
        }

        IsExperimentalDemoknightCharging = true;
        ExperimentalDemoknightChargeRechargeAccumulator = 0f;
        FacingDirectionX = MathF.Cos(AimDirectionDegrees * (MathF.PI / 180f)) < 0f ? -1f : 1f;
        StartExperimentalDemoknightChargeMovementState();
        return true;
    }

    public bool CancelExperimentalDemoknightCharge(bool depleteMeter)
    {
        if (!IsExperimentalDemoknightCharging)
        {
            return false;
        }

        IsExperimentalDemoknightCharging = false;
        ResetExperimentalDemoknightChargeMovementState();
        if (depleteMeter)
        {
            ExperimentalDemoknightChargeTicksRemaining = 0;
        }

        ExperimentalDemoknightChargeRechargeAccumulator = 0f;

        return true;
    }

    public void SetExperimentalPassiveMovementSpeedMultiplier(float multiplier)
    {
        ExperimentalPassiveMovementSpeedMultiplierValue = MathF.Max(1f, multiplier);
    }

    public void SetExperimentalJumpHeightMultiplier(float multiplier)
    {
        ExperimentalJumpHeightMultiplierValue = MathF.Max(1f, multiplier);
    }

    public void SetExperimentalBonusAirJumps(int bonusAirJumps)
    {
        var previousMaxAirJumps = MaxAirJumps;
        ExperimentalBonusAirJumpsValue = Math.Max(0, bonusAirJumps);
        var currentMaxAirJumps = MaxAirJumps;
        RemainingAirJumps = currentMaxAirJumps > previousMaxAirJumps
            ? Math.Min(currentMaxAirJumps, RemainingAirJumps + (currentMaxAirJumps - previousMaxAirJumps))
            : Math.Min(RemainingAirJumps, currentMaxAirJumps);
    }

    public void SetServerMovementSpeedScale(float multiplier)
    {
        ServerMovementSpeedScaleValue = MathF.Max(0.1f, multiplier);
    }

    public void SetServerDamageScale(float multiplier)
    {
        ServerDamageScaleValue = MathF.Max(0f, multiplier);
    }

    public void SetServerGravityScale(float multiplier)
    {
        ServerGravityScaleValue = MathF.Max(0f, multiplier);
    }

    public void SetServerMovementSpeedClamps(float horizontalClampPerTick, float verticalClampPerTick)
    {
        ServerHorizontalSpeedClampPerTickValue = MathF.Max(1f, horizontalClampPerTick);
        ServerVerticalSpeedClampPerTickValue = MathF.Max(1f, verticalClampPerTick);
    }

    public void SetExperimentalDemoknightSwordRangeMultiplier(float multiplier)
    {
        ExperimentalDemoknightSwordRangeMultiplierValue = MathF.Max(1f, multiplier);
    }

    public void SetExperimentalDemoknightSwordBaseDamage(int damage)
    {
        ExperimentalDemoknightSwordBaseDamageValue = Math.Max(1, damage);
    }

    public void SetExperimentalDemoknightSwordDamageMultiplier(float multiplier)
    {
        ExperimentalDemoknightSwordDamageMultiplierValue = MathF.Max(0.1f, multiplier);
    }

    public void SetExperimentalDemoknightSwordCooldownMultiplier(float multiplier)
    {
        ExperimentalDemoknightSwordCooldownMultiplierValue = MathF.Max(0.1f, multiplier);
    }

    public void SetExperimentalDemoknightChargeRechargeMultiplier(float multiplier)
    {
        ExperimentalDemoknightChargeRechargeMultiplierValue = MathF.Max(1f, multiplier);
        ExperimentalDemoknightChargeRechargeAccumulator = 0f;
    }

    public void SetExperimentalSoldierAmmoRegeneratesWhileSwappedOut(bool enabled)
    {
        ExperimentalSoldierAmmoRegeneratesWhileSwappedOutEnabled = enabled;
        if (!enabled)
        {
            ExperimentalSoldierSwappedOutAmmoRegenAccumulator = 0f;
        }
    }

    public void SetExperimentalSelfDamageHealing(bool enabled)
    {
        ExperimentalSelfDamageHealingEnabled = enabled;
    }

    public void SetExperimentalSoldierInfiniteAmmoDuringRage(bool enabled)
    {
        ExperimentalSoldierInfiniteAmmoDuringRageEnabled = enabled;
    }

    public void SetExperimentalReloadSpeedMultiplier(float multiplier)
    {
        ExperimentalReloadSpeedMultiplierValue = MathF.Max(0.1f, multiplier);
    }

    public int GetExperimentalReloadDurationTicks(int ticks)
    {
        return ApplyExperimentalReloadMultiplier(ticks);
    }

    public bool CanConvertExperimentalSelfDamageToHealing()
    {
        return ExperimentalSelfDamageHealingEnabled && IsAlive;
    }

    public void SetExperimentalDemoknightChargeFullControlEnabled(bool enabled)
    {
        ExperimentalDemoknightChargeFullControlEnabled = enabled;
    }

    public void ConfigureExperimentalDemoknightPostRageRegeneration(float healingPerSecond)
    {
        ExperimentalDemoknightPostRageRegenPerTickValue = MathF.Max(0f, healingPerSecond) / Math.Max(1, LegacyMovementModel.SourceTicksPerSecond);
    }

    public void StartExperimentalDemoknightPostRageRegeneration(int durationTicks)
    {
        ExperimentalDemoknightPostRageRegenTicksRemaining = Math.Max(0, durationTicks);
    }

    public bool TryStartExperimentalGhostDash(int dashTicks, int cooldownTicks, float nextAttackDamageMultiplier, float dashImpulse)
    {
        if (!IsExperimentalDemoknightEnabled
            || !IsAlive
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || IsExperimentalDemoknightCharging
            || ExperimentalGhostDashCooldownTicksRemaining > 0
            || dashTicks <= 0
            || cooldownTicks <= 0
            || nextAttackDamageMultiplier <= 1f)
        {
            return false;
        }

        ExperimentalGhostDashTicksRemaining = dashTicks;
        ExperimentalGhostDashVisibilityTicksRemaining = dashTicks;
        ExperimentalGhostDashCooldownTicksRemaining = cooldownTicks;
        ExperimentalGhostDashDistanceRemaining = MathF.Max(0f, dashImpulse);
        ExperimentalGhostDashSpeedPerSecondValue = dashImpulse <= 0f ? 0f : dashImpulse / 0.08f;
        ExperimentalGhostDashNextAttackDamageMultiplierValue = nextAttackDamageMultiplier;
        return true;
    }

    public void StartExperimentalGhostPhase(int ticks)
    {
        if (!IsAlive || ticks <= 0)
        {
            return;
        }

        ExperimentalGhostDashTicksRemaining = Math.Max(ExperimentalGhostDashTicksRemaining, ticks);
        ExperimentalGhostDashVisibilityTicksRemaining = Math.Max(ExperimentalGhostDashVisibilityTicksRemaining, ticks);
    }

    public void TriggerExperimentalLuckyBastard(int invulnerabilityTicks, int reviveHealth)
    {
        if (!IsAlive || invulnerabilityTicks <= 0 || reviveHealth <= 0)
        {
            return;
        }

        ExperimentalLuckyBastardTicksRemaining = Math.Max(ExperimentalLuckyBastardTicksRemaining, invulnerabilityTicks);
        ExperimentalLuckyBastardReviveHealth = Math.Clamp(reviveHealth, 1, MaxHealth);
        Health = 1;
        RefreshUber(invulnerabilityTicks);
        ConsumeKillStreak();
    }

    public void RefreshExperimentalFogOfWar(int durationTicks, float initialChance)
    {
        if (!IsAlive || durationTicks <= 0 || initialChance <= 0f)
        {
            return;
        }

        ExperimentalFogOfWarEvasionChanceValue = ExperimentalFogOfWarTicksRemaining > 0
            ? MathF.Min(1f, ExperimentalFogOfWarEvasionChanceValue * 2f)
            : MathF.Min(1f, initialChance);
        ExperimentalFogOfWarTicksRemaining = durationTicks;
    }

    public float ConsumeExperimentalNextAttackDamageMultiplier()
    {
        var multiplier = ExperimentalGhostDashNextAttackDamageMultiplierValue;
        ExperimentalGhostDashNextAttackDamageMultiplierValue = 1f;
        return multiplier;
    }

    public float GetExperimentalDemoknightSwordRange()
    {
        return ExperimentalDemoknightSwordBaseRange * ExperimentalDemoknightSwordRangeMultiplierValue;
    }

    public int GetExperimentalDemoknightSwordDamage()
    {
        if (!IsExperimentalDemoknightEnabled)
        {
            return 0;
        }

        return Math.Max(
            1,
            (int)MathF.Round(ExperimentalDemoknightSwordBaseDamageValue * ExperimentalDemoknightSwordDamageMultiplierValue));
    }

    public void ConsumeExperimentalDemoknightChargeOnHit()
    {
        if (!IsExperimentalDemoknightCharging)
        {
            return;
        }

        IsExperimentalDemoknightCharging = false;
        ExperimentalDemoknightChargeTicksRemaining = 0;
        ExperimentalDemoknightChargeRechargeAccumulator = 0f;
        ResetExperimentalDemoknightChargeMovementState();
    }

    public void GrantExperimentalMovementBoost(int ticks, float speedMultiplier)
    {
        if (!IsAlive || ticks <= 0 || speedMultiplier <= 1f)
        {
            return;
        }

        ExperimentalMovementBoostTicksRemaining = Math.Max(ExperimentalMovementBoostTicksRemaining, ticks);
        ExperimentalMovementSpeedMultiplierValue = Math.Max(ExperimentalMovementSpeedMultiplierValue, speedMultiplier);
    }

    public void GrantExperimentalPrimaryCooldownBuff(int ticks, float cooldownMultiplier)
    {
        if (!IsAlive || ticks <= 0 || cooldownMultiplier <= 0f || cooldownMultiplier >= 1f)
        {
            return;
        }

        ExperimentalPrimaryCooldownBuffTicksRemaining = Math.Max(ExperimentalPrimaryCooldownBuffTicksRemaining, ticks);
        ExperimentalPrimaryCooldownMultiplierValue = Math.Min(ExperimentalPrimaryCooldownMultiplierValue, cooldownMultiplier);
    }

    public bool TryInstantlyRefillPrimaryAmmo(int amount = 1)
    {
        if (!IsAlive || amount <= 0)
        {
            return false;
        }

        var previousShells = CurrentShells;
        CurrentShells = int.Min(PrimaryWeapon.MaxAmmo, CurrentShells + amount);
        if (CurrentShells <= previousShells)
        {
            return false;
        }

        ResetPyroPrimaryStateFromCurrentAmmo();
        if (CurrentShells >= PrimaryWeapon.MaxAmmo)
        {
            ReloadTicksUntilNextShell = 0;
        }

        if (ClassId == PlayerClass.Pyro)
        {
            IsPyroPrimaryRefilling = false;
        }

        return true;
    }

    public bool TryRequeuePrimaryFire()
    {
        if (!IsAlive || PrimaryCooldownTicks <= 0)
        {
            return false;
        }

        PrimaryCooldownTicks = 0;
        return true;
    }

    private void AdvanceExperimentalPowerState()
    {
        if (!IsExperimentalDemoknightEnabled)
        {
            IsExperimentalDemoknightCharging = false;
            ExperimentalDemoknightChargeTicksRemaining = 0;
            ExperimentalDemoknightChargeRechargeAccumulator = 0f;
            ResetExperimentalDemoknightChargeMovementState();
        }
        else if (IsExperimentalDemoknightCharging)
        {
            ExperimentalDemoknightChargeTicksRemaining = Math.Max(0, ExperimentalDemoknightChargeTicksRemaining - 1);
            if (ExperimentalDemoknightChargeTicksRemaining <= 0)
            {
                ExperimentalDemoknightChargeTicksRemaining = 0;
                IsExperimentalDemoknightCharging = false;
                ExperimentalDemoknightChargeRechargeAccumulator = 0f;
                ResetExperimentalDemoknightChargeMovementState();
            }
        }
        else if (ExperimentalDemoknightChargeTicksRemaining < ExperimentalDemoknightChargeMaxTicks)
        {
            ExperimentalDemoknightChargeRechargeAccumulator += ExperimentalDemoknightChargeRechargeMultiplierValue;
            var rechargeTicks = (int)MathF.Floor(ExperimentalDemoknightChargeRechargeAccumulator);
            if (rechargeTicks > 0)
            {
                ExperimentalDemoknightChargeRechargeAccumulator -= rechargeTicks;
                ExperimentalDemoknightChargeTicksRemaining = Math.Min(
                    ExperimentalDemoknightChargeMaxTicks,
                    ExperimentalDemoknightChargeTicksRemaining + rechargeTicks);
            }
        }
        else
        {
            ExperimentalDemoknightChargeRechargeAccumulator = 0f;
        }

        if (ExperimentalDemoknightPostRageRegenTicksRemaining > 0 && !IsRaging)
        {
            ExperimentalDemoknightPostRageRegenTicksRemaining -= 1;
            ApplyContinuousHealingAndGetAmount(ExperimentalDemoknightPostRageRegenPerTickValue);
        }

        if (ExperimentalGhostDashCooldownTicksRemaining > 0)
        {
            ExperimentalGhostDashCooldownTicksRemaining -= 1;
        }

        if (ExperimentalGhostDashTicksRemaining > 0)
        {
            ExperimentalGhostDashTicksRemaining -= 1;
            if (ExperimentalGhostDashTicksRemaining <= 0)
            {
                ExperimentalGhostDashTicksRemaining = 0;
                ExperimentalGhostDashDistanceRemaining = 0f;
                ExperimentalGhostDashSpeedPerSecondValue = 0f;
            }
        }

        if (ExperimentalGhostDashVisibilityTicksRemaining > 0)
        {
            ExperimentalGhostDashVisibilityTicksRemaining -= 1;
            if (ExperimentalGhostDashVisibilityTicksRemaining <= 0)
            {
                ExperimentalGhostDashVisibilityTicksRemaining = 0;
            }
        }

        if (ExperimentalLuckyBastardTicksRemaining > 0)
        {
            ExperimentalLuckyBastardTicksRemaining -= 1;
            if (ExperimentalLuckyBastardTicksRemaining <= 0)
            {
                ExperimentalLuckyBastardTicksRemaining = 0;
                if (IsAlive && ExperimentalLuckyBastardReviveHealth > Health)
                {
                    ForceSetHealth(ExperimentalLuckyBastardReviveHealth);
                }

                ExperimentalLuckyBastardReviveHealth = 0;
            }
        }

        if (ExperimentalFogOfWarTicksRemaining > 0)
        {
            ExperimentalFogOfWarTicksRemaining -= 1;
            if (ExperimentalFogOfWarTicksRemaining <= 0)
            {
                ExperimentalFogOfWarTicksRemaining = 0;
                ExperimentalFogOfWarEvasionChanceValue = 0f;
            }
        }

        if (ExperimentalMovementBoostTicksRemaining > 0)
        {
            ExperimentalMovementBoostTicksRemaining -= 1;
            if (ExperimentalMovementBoostTicksRemaining <= 0)
            {
                ExperimentalMovementBoostTicksRemaining = 0;
                ExperimentalMovementSpeedMultiplierValue = 1f;
            }
        }

        if (ExperimentalPrimaryCooldownBuffTicksRemaining > 0)
        {
            ExperimentalPrimaryCooldownBuffTicksRemaining -= 1;
            if (ExperimentalPrimaryCooldownBuffTicksRemaining <= 0)
            {
                ExperimentalPrimaryCooldownBuffTicksRemaining = 0;
                ExperimentalPrimaryCooldownMultiplierValue = 1f;
            }
        }
    }

    private void ResetExperimentalPowerRuntimeState()
    {
        ExperimentalMovementBoostTicksRemaining = 0;
        ExperimentalPrimaryCooldownBuffTicksRemaining = 0;
        ExperimentalMovementSpeedMultiplierValue = 1f;
        ExperimentalJumpHeightMultiplierValue = 1f;
        ExperimentalBonusAirJumpsValue = 0;
        ExperimentalPrimaryCooldownMultiplierValue = 1f;
        ExperimentalSoldierSwappedOutAmmoRegenAccumulator = 0f;
        ExperimentalDemoknightPostRageRegenTicksRemaining = 0;
        ExperimentalGhostDashTicksRemaining = 0;
        ExperimentalGhostDashCooldownTicksRemaining = 0;
        ExperimentalGhostDashVisibilityTicksRemaining = 0;
        ExperimentalGhostDashDistanceRemaining = 0f;
        ExperimentalGhostDashSpeedPerSecondValue = 0f;
        ExperimentalGhostDashNextAttackDamageMultiplierValue = 1f;
        ExperimentalLuckyBastardTicksRemaining = 0;
        ExperimentalLuckyBastardReviveHealth = 0;
        ExperimentalFogOfWarTicksRemaining = 0;
        ExperimentalFogOfWarEvasionChanceValue = 0f;
        IsExperimentalDemoknightCharging = false;
        ExperimentalDemoknightChargeRechargeAccumulator = 0f;
        ExperimentalDemoknightChargeTicksRemaining = IsExperimentalDemoknightEnabled
            ? ExperimentalDemoknightChargeMaxTicks
            : 0;
        ResetExperimentalDemoknightChargeMovementState();
        ResetRageState();
    }

    private float GetExperimentalMovementSpeedMultiplier()
    {
        var multiplier = ServerMovementSpeedScaleValue * ExperimentalPassiveMovementSpeedMultiplierValue;

        if (ExperimentalMovementBoostTicksRemaining > 0)
        {
            multiplier *= ExperimentalMovementSpeedMultiplierValue;
        }

        return multiplier;
    }

    private float GetExperimentalJumpHeightMultiplier()
    {
        return ExperimentalJumpHeightMultiplierValue;
    }

    private int GetExperimentalBonusAirJumps()
    {
        return ExperimentalBonusAirJumpsValue;
    }

    private float GetServerHorizontalSpeedClampPerTick()
    {
        return ServerHorizontalSpeedClampPerTickValue;
    }

    private float GetServerDamageScale()
    {
        return ServerDamageScaleValue;
    }

    private float GetServerScaledAirborneGravityPerTick(LegacyMovementState movementState)
    {
        var gravityScale = ServerGravityScaleValue;
        if (IsNapalmCovered)
        {
            gravityScale *= global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierNapalmGravityMultiplier;
        }

        return LegacyMovementModel.GetAirborneGravityPerTick(movementState) * gravityScale;
    }

    private float GetServerVerticalSpeedClampPerTick()
    {
        return ServerVerticalSpeedClampPerTickValue;
    }

    private void RefreshServerGameplayTuningFromReplicatedStateEntries()
    {
        var movementSpeedScale = 1f;
        if (TryGetReplicatedStateFloat(ServerTuningReplicatedStateOwnerId, MovementSpeedScaleReplicatedStateKey, out var replicatedMovementSpeedScale))
        {
            movementSpeedScale = replicatedMovementSpeedScale;
        }

        var gravityScale = 1f;
        if (TryGetReplicatedStateFloat(ServerTuningReplicatedStateOwnerId, GravityScaleReplicatedStateKey, out var replicatedGravityScale))
        {
            gravityScale = replicatedGravityScale;
        }

        SetServerMovementSpeedScale(movementSpeedScale);
        SetServerGravityScale(gravityScale);
    }

    private void StartExperimentalDemoknightChargeMovementState()
    {
        IsExperimentalDemoknightChargeDashActive = true;
        IsExperimentalDemoknightChargeFlightActive = false;
        ExperimentalDemoknightChargeAcceleration = 0f;
        ExperimentalDemoknightChargeWantsLift = false;
    }

    private void ResetExperimentalDemoknightChargeMovementState()
    {
        IsExperimentalDemoknightChargeDashActive = false;
        IsExperimentalDemoknightChargeFlightActive = false;
        ExperimentalDemoknightChargeAcceleration = 0f;
        ExperimentalDemoknightChargeWantsLift = false;
    }

    private int ApplyExperimentalPrimaryCooldownMultiplier(int cooldownTicks)
    {
        var clampedCooldown = Math.Max(1, cooldownTicks);
        if (ExperimentalPrimaryCooldownBuffTicksRemaining <= 0)
        {
            return ApplyExperimentalWeaponCycleMultiplier(clampedCooldown);
        }

        return ApplyExperimentalWeaponCycleMultiplier(
            Math.Max(1, (int)MathF.Round(clampedCooldown * ExperimentalPrimaryCooldownMultiplierValue)));
    }

    private int ApplyExperimentalReloadMultiplier(int ticks)
    {
        var clampedTicks = Math.Max(1, ticks);
        return Math.Max(1, (int)MathF.Round(clampedTicks / ExperimentalReloadSpeedMultiplierValue));
    }

    private int ApplyExperimentalWeaponCycleMultiplier(int ticks)
    {
        return ApplyExperimentalReloadMultiplier(ticks);
    }
}
