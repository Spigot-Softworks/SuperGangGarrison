using System;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private void AdvancePyroWeaponState()
    {
        if (PrimaryCooldownTicks > 0)
        {
            PrimaryCooldownTicks -= 1;
        }

        if (PyroFlameLoopTicksRemaining > 0)
        {
            PyroFlameLoopTicksRemaining -= 1;
        }

        var refillStartedThisTick = false;
        if (ReloadTicksUntilNextShell > 0)
        {
            ReloadTicksUntilNextShell -= 1;
            if (ReloadTicksUntilNextShell <= 0)
            {
                ReloadTicksUntilNextShell = 0;
                IsPyroPrimaryRefilling = true;
                refillStartedThisTick = true;
            }
        }

        if (!IsPyroPrimaryRefilling || refillStartedThisTick)
        {
            return;
        }

        if (PyroPrimaryFuelScaledValue >= GetPyroPrimaryFuelMaxScaled())
        {
            IsPyroPrimaryRefilling = false;
            return;
        }

        SetPyroPrimaryFuelScaled(PyroPrimaryFuelScaledValue + PyroPrimaryRefillScaledPerTick);
        if (PyroPrimaryFuelScaledValue >= GetPyroPrimaryFuelMaxScaled())
        {
            IsPyroPrimaryRefilling = false;
        }
    }

    private int GetPyroPrimaryFuelMaxScaled()
    {
        if (AcquiredWeaponClassId == PlayerClass.Pyro)
        {
            return (AcquiredWeapon?.MaxAmmo ?? 0) * PyroPrimaryFuelScale;
        }

        return PrimaryWeapon.MaxAmmo * PyroPrimaryFuelScale;
    }

    private void SetPyroPrimaryFuelScaled(int scaledFuel)
    {
        if (!HasPyroWeaponAvailable)
        {
            return;
        }

        PyroPrimaryFuelScaledValue = int.Clamp(scaledFuel, 0, GetPyroPrimaryFuelMaxScaled());
        if (IsUsingAcquiredPyroWeapon())
        {
            AcquiredWeaponCurrentShells = int.Clamp(
                PyroPrimaryFuelScaledValue / PyroPrimaryFuelScale,
                0,
                AcquiredWeapon?.MaxAmmo ?? 0);
            return;
        }

        CurrentShells = int.Clamp(PyroPrimaryFuelScaledValue / PyroPrimaryFuelScale, 0, PrimaryWeapon.MaxAmmo);
    }

    private int GetPyroPrimaryFuelScaledValue()
    {
        return PyroPrimaryFuelScaledValue;
    }

    private bool IsUsingAcquiredPyroWeapon()
    {
        return IsAcquiredWeaponEquipped && AcquiredWeaponClassId == PlayerClass.Pyro;
    }

    private void AdvanceAcquiredPyroWeaponState()
    {
        if (AcquiredWeaponCooldownTicks > 0)
        {
            AcquiredWeaponCooldownTicks -= 1;
        }

        if (PyroFlameLoopTicksRemaining > 0)
        {
            PyroFlameLoopTicksRemaining -= 1;
        }

        if (!IsAcquiredWeaponEquipped)
        {
            return;
        }

        var refillStartedThisTick = false;
        if (AcquiredWeaponReloadTicksUntilNextShell > 0)
        {
            AcquiredWeaponReloadTicksUntilNextShell -= 1;
            if (AcquiredWeaponReloadTicksUntilNextShell <= 0)
            {
                AcquiredWeaponReloadTicksUntilNextShell = 0;
                IsPyroPrimaryRefilling = true;
                refillStartedThisTick = true;
            }
        }

        if (!IsPyroPrimaryRefilling || refillStartedThisTick)
        {
            return;
        }

        if (GetPyroPrimaryFuelScaledValue() >= GetPyroPrimaryFuelMaxScaled())
        {
            IsPyroPrimaryRefilling = false;
            return;
        }

        SetPyroPrimaryFuelScaled(GetPyroPrimaryFuelScaledValue() + PyroPrimaryRefillScaledPerTick);
        if (GetPyroPrimaryFuelScaledValue() >= GetPyroPrimaryFuelMaxScaled())
        {
            IsPyroPrimaryRefilling = false;
        }
    }
}
