using System;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private void AdvanceWeaponState()
    {
        if (ClassId == PlayerClass.Pyro)
        {
            AdvancePyroWeaponState();
            AdvancePyroAirblastState();
            AdvanceExperimentalOffhandWeaponState();
            AdvanceAcquiredWeaponState();
            return;
        }

        if (PrimaryWeapon.AmmoRegenPerTick > 0 && CurrentShells < PrimaryWeapon.MaxAmmo)
        {
            CurrentShells = int.Min(PrimaryWeapon.MaxAmmo, CurrentShells + PrimaryWeapon.AmmoRegenPerTick);
        }

        if (PrimaryCooldownTicks > 0)
        {
            PrimaryCooldownTicks -= 1;
        }

        AdvancePyroAirblastState();

        if (PrimaryCooldownTicks > 0)
        {
            AdvanceExperimentalOffhandWeaponState();
            AdvanceAcquiredWeaponState();
            return;
        }

        if (!PrimaryWeapon.AutoReloads)
        {
            ReloadTicksUntilNextShell = 0;
            AdvanceExperimentalOffhandWeaponState();
            AdvanceAcquiredWeaponState();
            return;
        }

        if (CurrentShells >= PrimaryWeapon.MaxAmmo)
        {
            ReloadTicksUntilNextShell = 0;
            AdvanceExperimentalOffhandWeaponState();
            AdvanceAcquiredWeaponState();
            return;
        }

        if (IsExperimentalOffhandEquipped || IsAcquiredWeaponEquipped)
        {
            AdvanceExperimentalOffhandWeaponState();
            AdvanceAcquiredWeaponState();
            return;
        }

        if (ReloadTicksUntilNextShell > 0)
        {
            ReloadTicksUntilNextShell -= 1;
            AdvanceExperimentalOffhandWeaponState();
            AdvanceAcquiredWeaponState();
            return;
        }

        if (PrimaryWeapon.RefillsAllAtOnce)
        {
            CurrentShells = PrimaryWeapon.MaxAmmo;
            ReloadTicksUntilNextShell = 0;
            AdvanceExperimentalOffhandWeaponState();
            AdvanceAcquiredWeaponState();
            return;
        }

        CurrentShells += 1;
        if (CurrentShells < PrimaryWeapon.MaxAmmo)
        {
            ReloadTicksUntilNextShell = PrimaryWeapon.AmmoReloadTicks;
        }

        AdvanceExperimentalOffhandWeaponState();
        AdvanceAcquiredWeaponState();
    }

    private void AdvanceExperimentalOffhandWeaponState()
    {
        var weaponDefinition = ExperimentalOffhandWeapon;
        if (weaponDefinition is null)
        {
            ExperimentalOffhandCurrentShells = 0;
            ExperimentalOffhandCooldownTicks = 0;
            ExperimentalOffhandReloadTicksUntilNextShell = 0;
            IsExperimentalOffhandEquipped = false;
            return;
        }

        if (ExperimentalOffhandCooldownTicks > 0)
        {
            ExperimentalOffhandCooldownTicks -= 1;
        }

        if (!weaponDefinition.AutoReloads)
        {
            ExperimentalOffhandReloadTicksUntilNextShell = 0;
            return;
        }

        if (!IsExperimentalOffhandEquipped || ExperimentalOffhandCooldownTicks > 0)
        {
            return;
        }

        if (ExperimentalOffhandCurrentShells >= weaponDefinition.MaxAmmo)
        {
            ExperimentalOffhandReloadTicksUntilNextShell = 0;
            return;
        }

        if (ExperimentalOffhandReloadTicksUntilNextShell > 0)
        {
            ExperimentalOffhandReloadTicksUntilNextShell -= 1;
            return;
        }

        if (weaponDefinition.RefillsAllAtOnce)
        {
            ExperimentalOffhandCurrentShells = weaponDefinition.MaxAmmo;
            ExperimentalOffhandReloadTicksUntilNextShell = 0;
            return;
        }

        ExperimentalOffhandCurrentShells += 1;
        if (ExperimentalOffhandCurrentShells < weaponDefinition.MaxAmmo)
        {
            ExperimentalOffhandReloadTicksUntilNextShell = weaponDefinition.AmmoReloadTicks;
        }
    }

    private void AdvanceAcquiredWeaponState()
    {
        var weaponDefinition = AcquiredWeapon;
        if (weaponDefinition is null)
        {
            AcquiredWeaponCurrentShells = 0;
            AcquiredWeaponCooldownTicks = 0;
            AcquiredWeaponReloadTicksUntilNextShell = 0;
            IsAcquiredWeaponEquipped = false;
            return;
        }

        if (weaponDefinition.Kind == PrimaryWeaponKind.FlameThrower)
        {
            AdvanceAcquiredPyroWeaponState();
            return;
        }

        if (AcquiredWeaponClassId == PlayerClass.Medic)
        {
            AdvanceAcquiredMedicWeaponState(weaponDefinition);
            return;
        }

        if (AcquiredWeaponCooldownTicks > 0)
        {
            AcquiredWeaponCooldownTicks -= 1;
        }

        if (!IsAcquiredWeaponEquipped)
        {
            return;
        }

        if (weaponDefinition.AmmoRegenPerTick > 0 && AcquiredWeaponCurrentShells < weaponDefinition.MaxAmmo)
        {
            AcquiredWeaponCurrentShells = int.Min(weaponDefinition.MaxAmmo, AcquiredWeaponCurrentShells + weaponDefinition.AmmoRegenPerTick);
        }

        if (!weaponDefinition.AutoReloads)
        {
            AcquiredWeaponReloadTicksUntilNextShell = 0;
            return;
        }

        if (AcquiredWeaponCooldownTicks > 0)
        {
            return;
        }

        if (AcquiredWeaponCurrentShells >= weaponDefinition.MaxAmmo)
        {
            AcquiredWeaponReloadTicksUntilNextShell = 0;
            return;
        }

        if (AcquiredWeaponReloadTicksUntilNextShell > 0)
        {
            AcquiredWeaponReloadTicksUntilNextShell -= 1;
            return;
        }

        if (weaponDefinition.RefillsAllAtOnce)
        {
            AcquiredWeaponCurrentShells = weaponDefinition.MaxAmmo;
            AcquiredWeaponReloadTicksUntilNextShell = 0;
            return;
        }

        AcquiredWeaponCurrentShells += 1;
        if (AcquiredWeaponCurrentShells < weaponDefinition.MaxAmmo)
        {
            AcquiredWeaponReloadTicksUntilNextShell = weaponDefinition.AmmoReloadTicks;
        }
    }

    private void AdvanceAcquiredMedicWeaponState(PrimaryWeaponDefinition weaponDefinition)
    {
        if (MedicNeedleCooldownTicks > 0)
        {
            MedicNeedleCooldownTicks -= 1;
        }

        AcquiredWeaponCooldownTicks = MedicNeedleCooldownTicks;
        AcquiredWeaponReloadTicksUntilNextShell = 0;

        if (AcquiredWeaponCurrentShells >= weaponDefinition.MaxAmmo)
        {
            MedicNeedleRefillTicks = 0;
            return;
        }

        if (MedicNeedleRefillTicks <= 0)
        {
            MedicNeedleRefillTicks = MedicNeedleRefillTicksDefault;
            return;
        }

        MedicNeedleRefillTicks -= 1;
        if (MedicNeedleRefillTicks <= 0)
        {
            AcquiredWeaponCurrentShells = weaponDefinition.MaxAmmo;
            MedicNeedleRefillTicks = 0;
        }
    }

}
