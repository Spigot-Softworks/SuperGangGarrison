using System;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public bool TryFirePrimaryWeapon(bool ignoreAmmoCost = false)
    {
        ignoreAmmoCost |= HasInfiniteAmmoFromUber;
        LastPrimaryShotIgnoredAmmoCost = false;
        if (ClassId == PlayerClass.Pyro)
        {
            if (!TryPreparePyroPrimaryFireAttempt(ignoreAmmoCost))
            {
                return false;
            }

            CommitPyroPrimaryWeaponShot(ignoreAmmoCost);
            LastPrimaryShotIgnoredAmmoCost = ignoreAmmoCost;
            return true;
        }

        if (!IsAlive || IsHeavyEating || IsTaunting
            || IsCivviePogoActive || IsSpyCloaked || IsExperimentalCryoFrozen || PrimaryCooldownTicks > 0 || (!ignoreAmmoCost && CurrentShells < PrimaryWeapon.AmmoPerShot))
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
        var ignoreAmmoCost = HasInfiniteAmmoFromUber;
        var weaponDefinition = AcquiredWeapon;
        if (weaponDefinition is null
            || !IsAlive
            || IsHeavyEating
            || IsTaunting
            || IsCivviePogoActive
            || IsSpyCloaked
            || IsExperimentalCryoFrozen
            || AcquiredWeaponCooldownTicks > 0
            || (!ignoreAmmoCost && AcquiredWeaponCurrentShells < weaponDefinition.AmmoPerShot))
        {
            return false;
        }

        if (weaponDefinition.Kind == PrimaryWeaponKind.FlameThrower)
        {
            if (!TryPreparePyroPrimaryFireAttempt(ignoreAmmoCost))
            {
                return false;
            }

            IsExperimentalOffhandEquipped = false;
            IsAcquiredWeaponEquipped = true;
            SelectedGameplayEquippedSlot = GameplayEquipmentSlot.Secondary;
            RefreshGameplayLoadoutState();
            CommitPyroPrimaryWeaponShot(ignoreAmmoCost);
            return true;
        }

        IsExperimentalOffhandEquipped = false;
        IsAcquiredWeaponEquipped = true;
        SelectedGameplayEquippedSlot = GameplayEquipmentSlot.Secondary;
        RefreshGameplayLoadoutState();
        if (!ignoreAmmoCost)
        {
            AcquiredWeaponCurrentShells -= weaponDefinition.AmmoPerShot;
        }

        AcquiredWeaponCooldownTicks = ApplyExperimentalWeaponCycleMultiplier(weaponDefinition.ReloadDelayTicks);
        if (weaponDefinition.AutoReloads && !ignoreAmmoCost && AcquiredWeaponCurrentShells < weaponDefinition.MaxAmmo)
        {
            AcquiredWeaponReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(weaponDefinition.AmmoReloadTicks);
        }

        return true;
    }

    public bool TryFireExperimentalOffhandWeapon()
    {
        var ignoreAmmoCost = HasInfiniteAmmoFromUber;
        var weaponDefinition = ExperimentalOffhandWeapon;
        if (weaponDefinition is null
            || !IsAlive
            || IsHeavyEating
            || IsTaunting
            || IsCivviePogoActive
            || IsSpyCloaked
            || IsExperimentalCryoFrozen
            || ExperimentalOffhandCooldownTicks > 0
            || (!ignoreAmmoCost && ExperimentalOffhandCurrentShells < weaponDefinition.AmmoPerShot))
        {
            return false;
        }

        IsExperimentalOffhandEquipped = !IsAcquiredWeaponEquipped;
        SelectedGameplayEquippedSlot = IsExperimentalOffhandEquipped
            ? GameplayEquipmentSlot.Secondary
            : SelectedGameplayEquippedSlot;
        RefreshGameplayLoadoutState();
        if (!ignoreAmmoCost)
        {
            ExperimentalOffhandCurrentShells -= weaponDefinition.AmmoPerShot;
        }

        ExperimentalOffhandCooldownTicks = ApplyExperimentalWeaponCycleMultiplier(weaponDefinition.ReloadDelayTicks);
        if (weaponDefinition.AutoReloads && !ignoreAmmoCost && ExperimentalOffhandCurrentShells < weaponDefinition.MaxAmmo)
        {
            ExperimentalOffhandReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(weaponDefinition.AmmoReloadTicks);
        }

        return true;
    }

    public bool TryFireMedicKritzHealNeedle(
        int fireCooldownTicks = -1,
        int refillTicks = MedicNeedleRefillTicksDefault)
    {
        var ignoreAmmoCost = HasInfiniteAmmoFromUber;
        var weaponDefinition = ExperimentalOffhandWeapon;
        if (weaponDefinition is null
            || !IsAlive
            || ClassId != PlayerClass.Medic
            || !HasSecondaryBehavior(BuiltInGameplayBehaviorIds.MedigunCrit)
            || !IsExperimentalOffhandEquipped
            || IsHeavyEating
            || IsTaunting
            || IsCivviePogoActive
            || IsSpyCloaked
            || IsExperimentalCryoFrozen
            || ExperimentalOffhandCooldownTicks > 0
            || (!ignoreAmmoCost && ExperimentalOffhandCurrentShells <= 0))
        {
            return false;
        }

        if (fireCooldownTicks < 0)
        {
            fireCooldownTicks = Math.Max(1, weaponDefinition.ReloadDelayTicks);
        }

        if (!ignoreAmmoCost)
        {
            ExperimentalOffhandCurrentShells -= 1;
        }

        ExperimentalOffhandCooldownTicks = ApplyExperimentalWeaponCycleMultiplier(Math.Max(1, fireCooldownTicks));
        ExperimentalOffhandReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(Math.Max(1, refillTicks));
        return true;
    }

    public bool TryFireQuoteBubble(int activeProjectileLimit = QuoteBubbleLimit)
    {
        activeProjectileLimit = Math.Max(0, activeProjectileLimit);
        if (!IsAlive || IsHeavyEating || IsTaunting
            || IsCivviePogoActive || PrimaryCooldownTicks > 0 || QuoteBubbleCount >= activeProjectileLimit)
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
        var ignoreAmmoCost = HasInfiniteAmmoFromUber;
        if (!IsAlive
            || IsHeavyEating
            || IsTaunting
            || IsCivviePogoActive
            || PrimaryCooldownTicks > 0
            || QuoteBladesOut >= activeProjectileLimit
            || (!ignoreAmmoCost && CurrentShells < energyCost))
        {
            return false;
        }

        if (!ignoreAmmoCost)
        {
            CurrentShells -= energyCost;
        }

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
        var ignoreAmmoCost = HasInfiniteAmmoFromUber;
        if (!CanFirePyroAirblast(fuelCost))
        {
            return false;
        }

        if (!ignoreAmmoCost)
        {
            SetPyroPrimaryFuelScaled(GetPyroPrimaryFuelScaledValue() - (fuelCost * PyroPrimaryFuelScale));
        }

        PyroAirblastCooldownTicks = ApplyExperimentalWeaponCycleMultiplier(reloadTicks);
        var noFlameCooldownTicks = noFlameTicks <= 0
            ? 0
            : ApplyExperimentalWeaponCycleMultiplier(noFlameTicks);
        if (IsUsingAcquiredPyroWeapon())
        {
            AcquiredWeaponCooldownTicks = int.Max(
                AcquiredWeaponCooldownTicks,
                noFlameCooldownTicks);
            AcquiredWeaponReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(reloadTicks);
        }
        else
        {
            PrimaryCooldownTicks = int.Max(
                PrimaryCooldownTicks,
                noFlameCooldownTicks);
            ReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(reloadTicks);
        }

        IsPyroPrimaryRefilling = false;
        PyroFlameLoopTicksRemaining = 0;
        return true;
    }

    public bool TryFireExperimentalEngineerDestinyPunctuatorBlast()
    {
        var ignoreAmmoCost = HasInfiniteAmmoFromUber;
        if (!IsAlive
            || ClassId != PlayerClass.Engineer
            || IsHeavyEating
            || IsTaunting
            || IsCivviePogoActive
            || IsSpyCloaked
            || IsExperimentalCryoFrozen
            || PrimaryCooldownTicks > 0
            || (!ignoreAmmoCost && CurrentShells < global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorSecondaryShellCost))
        {
            return false;
        }

        if (!ignoreAmmoCost)
        {
            CurrentShells -= global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorSecondaryShellCost;
        }

        PrimaryCooldownTicks = GetPrimaryCooldownAfterShot();
        if (PrimaryWeapon.AutoReloads && !ignoreAmmoCost && CurrentShells < MaxShells)
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
            || IsCivviePogoActive
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
            || IsCivviePogoActive
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
            && !IsCivviePogoActive
            && PyroAirblastCooldownTicks <= 0
            && (HasInfiniteAmmoFromUber || GetPyroPrimaryFuelScaledValue() >= fuelCost * PyroPrimaryFuelScale);
    }

    public bool TryPreparePyroPrimaryFireAttempt(bool ignoreAmmoCost = false)
    {
        ignoreAmmoCost |= HasInfiniteAmmoFromUber;
        if (!IsAlive
            || !HasPyroWeaponEquipped
            || IsHeavyEating
            || IsTaunting
            || IsCivviePogoActive
            || IsSpyCloaked
            || PyroPrimaryRequiresReleaseAfterEmpty)
        {
            return false;
        }

        var pyroFuelScaled = GetPyroPrimaryFuelScaledValue();
        if (!ignoreAmmoCost && pyroFuelScaled > 0 && pyroFuelScaled < PyroPrimaryFlameCostScaled)
        {
            SetPyroPrimaryFuelScaled(pyroFuelScaled - PyroPrimaryFlameCostScaled);
            PyroPrimaryRequiresReleaseAfterEmpty = true;
        }

        var cooldownTicks = IsUsingAcquiredPyroWeapon() ? AcquiredWeaponCooldownTicks : PrimaryCooldownTicks;
        return cooldownTicks <= 0 && (ignoreAmmoCost || GetPyroPrimaryFuelScaledValue() >= PyroPrimaryFlameCostScaled);
    }

    public void CommitPyroPrimaryWeaponShot(bool ignoreAmmoCost = false)
    {
        ignoreAmmoCost |= HasInfiniteAmmoFromUber;
        if (!HasPyroWeaponEquipped)
        {
            return;
        }

        if (!ignoreAmmoCost)
        {
            SetPyroPrimaryFuelScaled(GetPyroPrimaryFuelScaledValue() - PyroPrimaryFlameCostScaled);
        }

        PyroPrimaryRequiresReleaseAfterEmpty = !ignoreAmmoCost && GetPyroPrimaryFuelScaledValue() <= 0;
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
        var ignoreAmmoCost = HasInfiniteAmmoFromUber;
        if (!IsAlive
            || !HasPyroWeaponEquipped
            || IsTaunting
            || IsCivviePogoActive
            || PyroFlareCooldownTicks > 0
            || (!ignoreAmmoCost && GetPyroPrimaryFuelScaledValue() < PyroFlareCost * PyroPrimaryFuelScale))
        {
            return false;
        }

        if (!ignoreAmmoCost)
        {
            SetPyroPrimaryFuelScaled(GetPyroPrimaryFuelScaledValue() - (PyroFlareCost * PyroPrimaryFuelScale));
        }

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
