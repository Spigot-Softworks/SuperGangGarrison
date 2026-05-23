using System;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public bool TryFirePrimaryWeapon(bool ignoreAmmoCost = false)
    {
        LastPrimaryShotIgnoredAmmoCost = false;
        if (ClassId == PlayerClass.Pyro)
        {
            if (!TryPreparePyroPrimaryFireAttempt())
            {
                return false;
            }

            CommitPyroPrimaryWeaponShot();
            return true;
        }

        if (!IsAlive || IsHeavyEating || IsTaunting || IsSpyCloaked || IsExperimentalCryoFrozen || PrimaryCooldownTicks > 0 || (!ignoreAmmoCost && CurrentShells < PrimaryWeapon.AmmoPerShot))
        {
            return false;
        }

        if (!ignoreAmmoCost)
        {
            CurrentShells -= PrimaryWeapon.AmmoPerShot;
        }
        LastPrimaryShotIgnoredAmmoCost = ignoreAmmoCost;
        PrimaryCooldownTicks = GetPrimaryCooldownAfterShot();
        if (PrimaryWeapon.AutoReloads && !ignoreAmmoCost && CurrentShells < PrimaryWeapon.MaxAmmo)
        {
            ReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(PrimaryWeapon.AmmoReloadTicks);
        }

        return true;
    }

    public bool TryFireAcquiredWeapon()
    {
        var weaponDefinition = AcquiredWeapon;
        if (weaponDefinition is null
            || !IsAlive
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || IsExperimentalCryoFrozen
            || AcquiredWeaponCooldownTicks > 0
            || AcquiredWeaponCurrentShells < weaponDefinition.AmmoPerShot)
        {
            return false;
        }

        if (weaponDefinition.Kind == PrimaryWeaponKind.FlameThrower)
        {
            if (!TryPreparePyroPrimaryFireAttempt())
            {
                return false;
            }

            IsExperimentalOffhandEquipped = false;
            IsAcquiredWeaponEquipped = true;
            SelectedGameplayEquippedSlot = GameplayEquipmentSlot.Secondary;
            RefreshGameplayLoadoutState();
            CommitPyroPrimaryWeaponShot();
            return true;
        }

        IsExperimentalOffhandEquipped = false;
        IsAcquiredWeaponEquipped = true;
        SelectedGameplayEquippedSlot = GameplayEquipmentSlot.Secondary;
        RefreshGameplayLoadoutState();
        AcquiredWeaponCurrentShells -= weaponDefinition.AmmoPerShot;
        AcquiredWeaponCooldownTicks = ApplyExperimentalWeaponCycleMultiplier(weaponDefinition.ReloadDelayTicks);
        if (weaponDefinition.AutoReloads && AcquiredWeaponCurrentShells < weaponDefinition.MaxAmmo)
        {
            AcquiredWeaponReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(weaponDefinition.AmmoReloadTicks);
        }

        return true;
    }

    public bool TryFireExperimentalOffhandWeapon()
    {
        var weaponDefinition = ExperimentalOffhandWeapon;
        if (weaponDefinition is null
            || !IsAlive
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || IsExperimentalCryoFrozen
            || ExperimentalOffhandCooldownTicks > 0
            || ExperimentalOffhandCurrentShells < weaponDefinition.AmmoPerShot)
        {
            return false;
        }

        IsExperimentalOffhandEquipped = !IsAcquiredWeaponEquipped;
        SelectedGameplayEquippedSlot = IsExperimentalOffhandEquipped
            ? GameplayEquipmentSlot.Secondary
            : SelectedGameplayEquippedSlot;
        RefreshGameplayLoadoutState();
        ExperimentalOffhandCurrentShells -= weaponDefinition.AmmoPerShot;
        ExperimentalOffhandCooldownTicks = ApplyExperimentalWeaponCycleMultiplier(weaponDefinition.ReloadDelayTicks);
        if (weaponDefinition.AutoReloads && ExperimentalOffhandCurrentShells < weaponDefinition.MaxAmmo)
        {
            ExperimentalOffhandReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(weaponDefinition.AmmoReloadTicks);
        }

        return true;
    }

    public bool TryFireQuoteBubble(int activeProjectileLimit = QuoteBubbleLimit)
    {
        activeProjectileLimit = Math.Max(0, activeProjectileLimit);
        if (!IsAlive || IsHeavyEating || IsTaunting || PrimaryCooldownTicks > 0 || QuoteBubbleCount >= activeProjectileLimit)
        {
            return false;
        }

        PrimaryCooldownTicks = GetPrimaryCooldownAfterShot();
        return true;
    }

    public bool TryFireQuoteBlade(int energyCost = QuoteBladeEnergyCost, int activeProjectileLimit = QuoteBladeMaxOut)
    {
        energyCost = Math.Max(0, energyCost);
        activeProjectileLimit = Math.Max(0, activeProjectileLimit);
        if (!IsAlive
            || IsHeavyEating
            || IsTaunting
            || PrimaryCooldownTicks > 0
            || QuoteBladesOut >= activeProjectileLimit
            || CurrentShells < energyCost)
        {
            return false;
        }

        CurrentShells -= energyCost;
        PrimaryCooldownTicks = GetPrimaryCooldownAfterShot();
        return true;
    }

    public bool TryFirePyroAirblast(
        int fuelCost = PyroAirblastCost,
        int reloadTicks = PyroAirblastReloadTicks,
        int noFlameTicks = PyroAirblastNoFlameTicks)
    {
        fuelCost = Math.Max(0, fuelCost);
        reloadTicks = Math.Max(1, reloadTicks);
        noFlameTicks = Math.Max(0, noFlameTicks);
        if (!CanFirePyroAirblast(fuelCost))
        {
            return false;
        }

        SetPyroPrimaryFuelScaled(GetPyroPrimaryFuelScaledValue() - (fuelCost * PyroPrimaryFuelScale));
        PyroAirblastCooldownTicks = ApplyExperimentalWeaponCycleMultiplier(reloadTicks);
        if (IsUsingAcquiredPyroWeapon())
        {
            AcquiredWeaponCooldownTicks = int.Max(
                AcquiredWeaponCooldownTicks,
                ApplyExperimentalWeaponCycleMultiplier(noFlameTicks));
            AcquiredWeaponReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(reloadTicks);
        }
        else
        {
            PrimaryCooldownTicks = int.Max(
                PrimaryCooldownTicks,
                ApplyExperimentalWeaponCycleMultiplier(noFlameTicks));
            ReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(reloadTicks);
        }

        IsPyroPrimaryRefilling = false;
        PyroFlameLoopTicksRemaining = 0;
        return true;
    }

    public bool TryFireExperimentalEngineerDestinyPunctuatorBlast()
    {
        if (!IsAlive
            || ClassId != PlayerClass.Engineer
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || IsExperimentalCryoFrozen
            || PrimaryCooldownTicks > 0
            || CurrentShells < global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorSecondaryShellCost)
        {
            return false;
        }

        CurrentShells -= global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorSecondaryShellCost;
        PrimaryCooldownTicks = GetPrimaryCooldownAfterShot();
        if (PrimaryWeapon.AutoReloads && CurrentShells < MaxShells)
        {
            ReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(PrimaryWeapon.AmmoReloadTicks);
        }

        return true;
    }

    public bool TryFireExperimentalSoldierThundergunner(int cooldownTicks)
    {
        if (!IsAlive
            || ClassId != PlayerClass.Soldier
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || PrimaryCooldownTicks > 0)
        {
            return false;
        }

        PrimaryCooldownTicks = ApplyExperimentalWeaponCycleMultiplier(Math.Max(1, cooldownTicks));
        return true;
    }

    public bool TryDeployExperimentalSoldierCivilDefenseTurret(int cooldownTicks)
    {
        if (!IsAlive
            || ClassId != PlayerClass.Soldier
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || PrimaryCooldownTicks > 0)
        {
            return false;
        }

        PrimaryCooldownTicks = ApplyExperimentalWeaponCycleMultiplier(Math.Max(1, cooldownTicks));
        return true;
    }

    public bool CanFirePyroAirblast(int fuelCost = PyroAirblastCost)
    {
        fuelCost = Math.Max(0, fuelCost);
        return IsAlive
            && HasPyroWeaponEquipped
            && !IsTaunting
            && PyroAirblastCooldownTicks <= 0
            && GetPyroPrimaryFuelScaledValue() >= fuelCost * PyroPrimaryFuelScale;
    }

    public bool TryPreparePyroPrimaryFireAttempt()
    {
        if (!IsAlive
            || !HasPyroWeaponEquipped
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || PyroPrimaryRequiresReleaseAfterEmpty)
        {
            return false;
        }

        var pyroFuelScaled = GetPyroPrimaryFuelScaledValue();
        if (pyroFuelScaled > 0 && pyroFuelScaled < PyroPrimaryFlameCostScaled)
        {
            SetPyroPrimaryFuelScaled(pyroFuelScaled - PyroPrimaryFlameCostScaled);
            PyroPrimaryRequiresReleaseAfterEmpty = true;
        }

        var cooldownTicks = IsUsingAcquiredPyroWeapon() ? AcquiredWeaponCooldownTicks : PrimaryCooldownTicks;
        return cooldownTicks <= 0 && GetPyroPrimaryFuelScaledValue() >= PyroPrimaryFlameCostScaled;
    }

    public void CommitPyroPrimaryWeaponShot()
    {
        if (!HasPyroWeaponEquipped)
        {
            return;
        }

        SetPyroPrimaryFuelScaled(GetPyroPrimaryFuelScaledValue() - PyroPrimaryFlameCostScaled);
        PyroPrimaryRequiresReleaseAfterEmpty = GetPyroPrimaryFuelScaledValue() <= 0;
        if (IsUsingAcquiredPyroWeapon())
        {
            AcquiredWeaponCooldownTicks = !PyroPrimaryRequiresReleaseAfterEmpty
                ? ApplyExperimentalWeaponCycleMultiplier(AcquiredWeapon?.ReloadDelayTicks ?? 0)
                : ApplyExperimentalWeaponCycleMultiplier(PyroPrimaryEmptyCooldownTicks);
            AcquiredWeaponReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(PyroPrimaryRefillBufferTicks);
        }
        else
        {
            PrimaryCooldownTicks = !PyroPrimaryRequiresReleaseAfterEmpty
                ? ApplyExperimentalWeaponCycleMultiplier(PrimaryWeapon.ReloadDelayTicks)
                : ApplyExperimentalWeaponCycleMultiplier(PyroPrimaryEmptyCooldownTicks);
            ReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(PyroPrimaryRefillBufferTicks);
        }

        IsPyroPrimaryRefilling = false;
        PyroFlameLoopTicksRemaining = PyroFlameLoopMaintainTicks;
    }

    public bool TryFirePyroFlare()
    {
        if (!IsAlive
            || !HasPyroWeaponEquipped
            || IsTaunting
            || PyroFlareCooldownTicks > 0
            || GetPyroPrimaryFuelScaledValue() < PyroFlareCost * PyroPrimaryFuelScale)
        {
            return false;
        }

        SetPyroPrimaryFuelScaled(GetPyroPrimaryFuelScaledValue() - (PyroFlareCost * PyroPrimaryFuelScale));
        PyroFlareCooldownTicks = ApplyExperimentalWeaponCycleMultiplier(PyroFlareReloadTicks);
        return true;
    }

    public void UpdatePyroPrimaryHoldState(bool isHoldingPrimary)
    {
        if (!HasPyroWeaponEquipped || isHoldingPrimary)
        {
            return;
        }

        PyroPrimaryRequiresReleaseAfterEmpty = false;
    }

    public int GetSniperRifleDamage()
    {
        if (!HasScopedSniperWeaponEquipped || !IsSniperScoped)
        {
            return SniperBaseDamage;
        }

        return SniperBaseDamage + (int)MathF.Floor(MathF.Sqrt(SniperChargeTicks * 125f / 6f));
    }

    private int GetPrimaryCooldownAfterShot()
    {
        var cooldownTicks = HasScopedSniperWeaponEquipped && IsSniperScoped
            ? PrimaryWeapon.ReloadDelayTicks + SniperScopedReloadBonusTicks
            : PrimaryWeapon.ReloadDelayTicks;
        return ApplyExperimentalPrimaryCooldownMultiplier(cooldownTicks);
    }
}
