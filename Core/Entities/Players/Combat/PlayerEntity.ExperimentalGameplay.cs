using System;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private int ExperimentalMovementBoostTicksRemaining { get; set; }

    private int ExperimentalPrimaryCooldownBuffTicksRemaining { get; set; }

    private float ExperimentalMovementSpeedMultiplierValue { get; set; } = 1f;

    private float ExperimentalPrimaryCooldownMultiplierValue { get; set; } = 1f;

    private float ExperimentalPassiveMovementSpeedMultiplierValue { get; set; } = 1f;

    private float ExperimentalDemoknightSwordRangeMultiplierValue { get; set; } = 1f;

    private int ExperimentalDemoknightSwordBaseDamageValue { get; set; } = 42;

    private float ExperimentalDemoknightSwordDamageMultiplierValue { get; set; } = 1f;

    private float ExperimentalDemoknightSwordCooldownMultiplierValue { get; set; } = 1f;

    private float ExperimentalDemoknightChargeRechargeMultiplierValue { get; set; } = 1f;

    private float ExperimentalDemoknightChargeRechargeAccumulator { get; set; }

    private bool ExperimentalDemoknightChargeWantsLift { get; set; }

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
        ExperimentalPrimaryCooldownMultiplierValue = 1f;
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
        var multiplier = ExperimentalPassiveMovementSpeedMultiplierValue;

        if (ExperimentalMovementBoostTicksRemaining > 0)
        {
            multiplier *= ExperimentalMovementSpeedMultiplierValue;
        }

        return multiplier;
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
            return clampedCooldown;
        }

        return Math.Max(1, (int)MathF.Round(clampedCooldown * ExperimentalPrimaryCooldownMultiplierValue));
    }
}
