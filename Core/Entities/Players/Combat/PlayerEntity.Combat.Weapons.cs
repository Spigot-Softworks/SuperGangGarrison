using System;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{

    public bool TryFirePrimaryWeapon()
    {
        if (ClassId == PlayerClass.Pyro)
        {
            if (!TryPreparePyroPrimaryFireAttempt())
            {
                return false;
            }

            CommitPyroPrimaryWeaponShot();
            return true;
        }

        if (!IsAlive || IsHeavyEating || IsTaunting || IsSpyCloaked || PrimaryCooldownTicks > 0 || CurrentShells < PrimaryWeapon.AmmoPerShot)
        {
            return false;
        }

        CurrentShells -= PrimaryWeapon.AmmoPerShot;
        PrimaryCooldownTicks = GetPrimaryCooldownAfterShot();
        if (PrimaryWeapon.AutoReloads
            && CurrentShells < PrimaryWeapon.MaxAmmo)
        {
            ReloadTicksUntilNextShell = PrimaryWeapon.AmmoReloadTicks;
        }

        return true;
    }

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
            return;
        }

        ExperimentalOffhandCurrentShells = weaponDefinition.MaxAmmo;
        ExperimentalOffhandCooldownTicks = 0;
        ExperimentalOffhandReloadTicksUntilNextShell = 0;
        IsExperimentalOffhandEquipped = false;
    }

    public void EquipExperimentalOffhandWeapon()
    {
        if (ExperimentalOffhandWeapon is null || !IsAlive)
        {
            return;
        }

        IsExperimentalOffhandEquipped = true;
    }

    public void StowExperimentalOffhandWeapon()
    {
        IsExperimentalOffhandEquipped = false;
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
            return;
        }

        var acquiredWeapon = AcquiredWeapon;
        AcquiredWeaponCurrentShells = acquiredWeapon?.MaxAmmo ?? 0;
        AcquiredWeaponCooldownTicks = 0;
        AcquiredWeaponReloadTicksUntilNextShell = 0;
        IsAcquiredWeaponEquipped = false;
        ResetAcquiredPyroStateFromCurrentAmmo();
        ResetAcquiredMedicNeedleStateIfUnavailable();
    }

    public void EquipAcquiredWeapon()
    {
        if (!HasAcquiredWeapon || !IsAlive)
        {
            return;
        }

        IsExperimentalOffhandEquipped = false;
        IsAcquiredWeaponEquipped = true;
    }

    public void StowAcquiredWeapon()
    {
        IsAcquiredWeaponEquipped = false;
        if (AcquiredWeaponClassId == PlayerClass.Sniper)
        {
            IsSniperScoped = false;
            SniperChargeTicks = 0;
        }
    }

    public bool TryFireAcquiredWeapon()
    {
        var weaponDefinition = AcquiredWeapon;
        if (weaponDefinition is null
            || !IsAlive
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
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
            CommitPyroPrimaryWeaponShot();
            return true;
        }

        IsExperimentalOffhandEquipped = false;
        IsAcquiredWeaponEquipped = true;
        AcquiredWeaponCurrentShells -= weaponDefinition.AmmoPerShot;
        AcquiredWeaponCooldownTicks = weaponDefinition.ReloadDelayTicks;
        if (weaponDefinition.AutoReloads
            && AcquiredWeaponCurrentShells < weaponDefinition.MaxAmmo)
        {
            AcquiredWeaponReloadTicksUntilNextShell = weaponDefinition.AmmoReloadTicks;
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
            || ExperimentalOffhandCooldownTicks > 0
            || ExperimentalOffhandCurrentShells < weaponDefinition.AmmoPerShot)
        {
            return false;
        }

        IsExperimentalOffhandEquipped = !IsAcquiredWeaponEquipped;
        ExperimentalOffhandCurrentShells -= weaponDefinition.AmmoPerShot;
        ExperimentalOffhandCooldownTicks = weaponDefinition.ReloadDelayTicks;
        if (weaponDefinition.AutoReloads
            && ExperimentalOffhandCurrentShells < weaponDefinition.MaxAmmo)
        {
            ExperimentalOffhandReloadTicksUntilNextShell = weaponDefinition.AmmoReloadTicks;
        }

        return true;
    }

    public bool TryFireQuoteBubble()
    {
        if (!IsAlive
            || ClassId != PlayerClass.Quote
            || IsHeavyEating
            || IsTaunting
            || PrimaryCooldownTicks > 0
            || QuoteBubbleCount >= QuoteBubbleLimit)
        {
            return false;
        }

        PrimaryCooldownTicks = GetPrimaryCooldownAfterShot();
        return true;
    }

    public bool TryFireQuoteBlade()
    {
        if (!IsAlive
            || ClassId != PlayerClass.Quote
            || IsHeavyEating
            || IsTaunting
            || PrimaryCooldownTicks > 0
            || QuoteBladesOut >= QuoteBladeMaxOut
            || CurrentShells < QuoteBladeEnergyCost)
        {
            return false;
        }

        CurrentShells -= QuoteBladeEnergyCost;
        PrimaryCooldownTicks = GetPrimaryCooldownAfterShot();
        return true;
    }

    public bool TryFirePyroAirblast()
    {
        if (!CanFirePyroAirblast())
        {
            return false;
        }

        SetPyroPrimaryFuelScaled(GetPyroPrimaryFuelScaledValue() - (PyroAirblastCost * PyroPrimaryFuelScale));
        PyroAirblastCooldownTicks = PyroAirblastReloadTicks;
        if (IsUsingAcquiredPyroWeapon())
        {
            AcquiredWeaponCooldownTicks = int.Max(AcquiredWeaponCooldownTicks, PyroAirblastNoFlameTicks);
            AcquiredWeaponReloadTicksUntilNextShell = PyroAirblastReloadTicks;
        }
        else
        {
            PrimaryCooldownTicks = int.Max(PrimaryCooldownTicks, PyroAirblastNoFlameTicks);
            ReloadTicksUntilNextShell = PyroAirblastReloadTicks;
        }

        IsPyroPrimaryRefilling = false;
        PyroFlameLoopTicksRemaining = 0;
        return true;
    }

    public bool CanFirePyroAirblast()
    {
        return IsAlive
            && HasPyroWeaponEquipped
            && !IsTaunting
            && PyroAirblastCooldownTicks <= 0
            && GetPyroPrimaryFuelScaledValue() >= PyroAirblastCost * PyroPrimaryFuelScale;
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
        if (pyroFuelScaled > 0
            && pyroFuelScaled < PyroPrimaryFlameCostScaled)
        {
            SetPyroPrimaryFuelScaled(pyroFuelScaled - PyroPrimaryFlameCostScaled);
            PyroPrimaryRequiresReleaseAfterEmpty = true;
        }

        var cooldownTicks = IsUsingAcquiredPyroWeapon()
            ? AcquiredWeaponCooldownTicks
            : PrimaryCooldownTicks;
        return cooldownTicks <= 0
            && GetPyroPrimaryFuelScaledValue() >= PyroPrimaryFlameCostScaled;
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
                ? AcquiredWeapon?.ReloadDelayTicks ?? 0
                : PyroPrimaryEmptyCooldownTicks;
            AcquiredWeaponReloadTicksUntilNextShell = PyroPrimaryRefillBufferTicks;
        }
        else
        {
            PrimaryCooldownTicks = !PyroPrimaryRequiresReleaseAfterEmpty
                ? PrimaryWeapon.ReloadDelayTicks
                : PyroPrimaryEmptyCooldownTicks;
            ReloadTicksUntilNextShell = PyroPrimaryRefillBufferTicks;
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
        PyroFlareCooldownTicks = PyroFlareReloadTicks;
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

    private void ResetAcquiredMedicNeedleStateIfUnavailable()
    {
        if (ClassId == PlayerClass.Medic || AcquiredWeaponClassId == PlayerClass.Medic)
        {
            return;
        }

        MedicNeedleCooldownTicks = 0;
        MedicNeedleRefillTicks = 0;
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
