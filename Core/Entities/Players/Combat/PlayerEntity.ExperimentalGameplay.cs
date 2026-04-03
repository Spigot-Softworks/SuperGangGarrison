using System;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private int ExperimentalMovementBoostTicksRemaining { get; set; }

    private int ExperimentalPrimaryCooldownBuffTicksRemaining { get; set; }

    private float ExperimentalMovementSpeedMultiplierValue { get; set; } = 1f;

    private float ExperimentalPrimaryCooldownMultiplierValue { get; set; } = 1f;

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
    }

    private float GetExperimentalMovementSpeedMultiplier()
    {
        return ExperimentalMovementBoostTicksRemaining > 0
            ? ExperimentalMovementSpeedMultiplierValue
            : 1f;
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
