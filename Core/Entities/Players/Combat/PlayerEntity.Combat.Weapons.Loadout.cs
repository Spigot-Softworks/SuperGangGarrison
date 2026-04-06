using System;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public void SetExperimentalOffhandWeapon(PrimaryWeaponDefinition? weaponDefinition)
    {
        if (weaponDefinition == ExperimentalOffhandWeapon)
        {
            if (weaponDefinition is not null)
            {
                ExperimentalOffhandCurrentShells = int.Clamp(ExperimentalOffhandCurrentShells, 0, weaponDefinition.MaxAmmo);
                ExperimentalOffhandCooldownTicks = Math.Max(0, ExperimentalOffhandCooldownTicks);
                ExperimentalOffhandReloadTicksUntilNextShell = Math.Max(0, ExperimentalOffhandReloadTicksUntilNextShell);
            }
            else
            {
                ExperimentalOffhandCurrentShells = 0;
                ExperimentalOffhandCooldownTicks = 0;
                ExperimentalOffhandReloadTicksUntilNextShell = 0;
                IsExperimentalOffhandEquipped = false;
            }

            return;
        }

        ExperimentalOffhandWeapon = weaponDefinition;
        if (weaponDefinition is null)
        {
            ExperimentalOffhandCurrentShells = 0;
            ExperimentalOffhandCooldownTicks = 0;
            ExperimentalOffhandReloadTicksUntilNextShell = 0;
            IsExperimentalOffhandEquipped = false;
            RefreshGameplayLoadoutState();
            return;
        }

        ExperimentalOffhandCurrentShells = weaponDefinition.MaxAmmo;
        ExperimentalOffhandCooldownTicks = 0;
        ExperimentalOffhandReloadTicksUntilNextShell = 0;
        IsExperimentalOffhandEquipped = false;
        RefreshGameplayLoadoutState();
    }

    public void EquipExperimentalOffhandWeapon()
    {
        if (ExperimentalOffhandWeapon is null || !IsAlive)
        {
            return;
        }

        IsExperimentalOffhandEquipped = true;
        RefreshGameplayLoadoutState();
    }

    public void StowExperimentalOffhandWeapon()
    {
        IsExperimentalOffhandEquipped = false;
        RefreshGameplayLoadoutState();
    }

    public void SetAcquiredWeapon(PlayerClass? weaponClassId)
    {
        if (!weaponClassId.HasValue
            || ClassId != PlayerClass.Soldier
            || !CharacterClassCatalog.SupportsExperimentalAcquiredWeapon(weaponClassId.Value))
        {
            weaponClassId = null;
        }

        if (weaponClassId == AcquiredWeaponClassId)
        {
            var weaponDefinition = AcquiredWeapon;
            if (weaponDefinition is not null)
            {
                AcquiredWeaponCurrentShells = int.Clamp(AcquiredWeaponCurrentShells, 0, weaponDefinition.MaxAmmo);
                AcquiredWeaponCooldownTicks = Math.Max(0, AcquiredWeaponCooldownTicks);
                AcquiredWeaponReloadTicksUntilNextShell = Math.Max(0, AcquiredWeaponReloadTicksUntilNextShell);
            }
            else
            {
                AcquiredWeaponCurrentShells = 0;
                AcquiredWeaponCooldownTicks = 0;
                AcquiredWeaponReloadTicksUntilNextShell = 0;
                IsAcquiredWeaponEquipped = false;
            }

            ResetAcquiredPyroStateFromCurrentAmmo();
            ResetAcquiredMedicNeedleStateIfUnavailable();
            RefreshGameplayLoadoutState();

            return;
        }

        AcquiredWeaponClassId = weaponClassId;
        if (weaponClassId != PlayerClass.Sniper)
        {
            IsSniperScoped = false;
            SniperChargeTicks = 0;
        }

        if (!weaponClassId.HasValue)
        {
            AcquiredWeaponCurrentShells = 0;
            AcquiredWeaponCooldownTicks = 0;
            AcquiredWeaponReloadTicksUntilNextShell = 0;
            IsAcquiredWeaponEquipped = false;
            ResetAcquiredPyroStateFromCurrentAmmo();
            ResetAcquiredMedicNeedleStateIfUnavailable();
            RefreshGameplayLoadoutState();
            return;
        }

        var acquiredWeapon = AcquiredWeapon;
        AcquiredWeaponCurrentShells = acquiredWeapon?.MaxAmmo ?? 0;
        AcquiredWeaponCooldownTicks = 0;
        AcquiredWeaponReloadTicksUntilNextShell = 0;
        IsAcquiredWeaponEquipped = false;
        ResetAcquiredPyroStateFromCurrentAmmo();
        ResetAcquiredMedicNeedleStateIfUnavailable();
        RefreshGameplayLoadoutState();
    }

    public void EquipAcquiredWeapon()
    {
        if (!HasAcquiredWeapon || !IsAlive)
        {
            return;
        }

        IsExperimentalOffhandEquipped = false;
        IsAcquiredWeaponEquipped = true;
        RefreshGameplayLoadoutState();
    }

    public void StowAcquiredWeapon()
    {
        IsAcquiredWeaponEquipped = false;
        if (AcquiredWeaponClassId == PlayerClass.Sniper)
        {
            IsSniperScoped = false;
            SniperChargeTicks = 0;
        }

        RefreshGameplayLoadoutState();
    }

    private void ResetPyroPrimaryStateFromCurrentAmmo()
    {
        if (ClassId != PlayerClass.Pyro)
        {
            ResetAcquiredPyroStateFromCurrentAmmo();
            return;
        }

        PyroPrimaryFuelScaledValue = int.Clamp(CurrentShells * PyroPrimaryFuelScale, 0, GetPyroPrimaryFuelMaxScaled());
        CurrentShells = int.Clamp(PyroPrimaryFuelScaledValue / PyroPrimaryFuelScale, 0, PrimaryWeapon.MaxAmmo);
        PyroPrimaryRequiresReleaseAfterEmpty = false;
    }

    private void ResetAcquiredPyroStateFromCurrentAmmo()
    {
        if (AcquiredWeaponClassId != PlayerClass.Pyro)
        {
            if (ClassId != PlayerClass.Pyro)
            {
                PyroPrimaryFuelScaledValue = 0;
                IsPyroPrimaryRefilling = false;
                PyroFlameLoopTicksRemaining = 0;
                PyroPrimaryRequiresReleaseAfterEmpty = false;
                PyroAirblastCooldownTicks = 0;
                PyroFlareCooldownTicks = 0;
            }

            return;
        }

        PyroPrimaryFuelScaledValue = int.Clamp(
            AcquiredWeaponCurrentShells * PyroPrimaryFuelScale,
            0,
            GetPyroPrimaryFuelMaxScaled());
        AcquiredWeaponCurrentShells = int.Clamp(
            PyroPrimaryFuelScaledValue / PyroPrimaryFuelScale,
            0,
            AcquiredWeapon?.MaxAmmo ?? 0);
        IsPyroPrimaryRefilling = false;
        PyroFlameLoopTicksRemaining = 0;
        PyroPrimaryRequiresReleaseAfterEmpty = false;
        PyroAirblastCooldownTicks = 0;
        PyroFlareCooldownTicks = 0;
    }

    private void ResetAcquiredMedicNeedleStateIfUnavailable()
    {
        if (ClassId == PlayerClass.Medic || AcquiredWeaponClassId == PlayerClass.Medic)
        {
            return;
        }

        MedicNeedleCooldownTicks = 0;
        MedicNeedleRefillTicks = 0;
    }
}
