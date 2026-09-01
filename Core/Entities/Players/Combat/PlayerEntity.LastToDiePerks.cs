using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public const string LastToDieMedicLinkReplicatedStateOwnerId = "ltd.link";

    public const string LastToDieMedicLinkReplicatedStateKey = "effects";

    public const string LastToDieProfessionalFireChordReplicatedStateKey = "professional_chord";

    private const byte LastToDieProfessionalFireChordInactive = 0;

    private const byte LastToDieProfessionalFireChordArmed = 1;

    private const byte LastToDieProfessionalFireChordConsumed = 2;

    private const byte LastToDieMedicStimulantDripLinkFlag = 1 << 0;

    private const byte LastToDieMedicAgilityDriveLinkFlag = 1 << 1;

    private const byte LastToDieMedicMartyrProtectedLinkFlag = 1 << 2;

    private const byte LastToDieMedicMartyrProtectorLinkFlag = 1 << 3;

    private const byte LastToDieMedicLinkKnownFlags =
        LastToDieMedicStimulantDripLinkFlag
        | LastToDieMedicAgilityDriveLinkFlag
        | LastToDieMedicMartyrProtectedLinkFlag
        | LastToDieMedicMartyrProtectorLinkFlag;

    private float LastToDieCloakedMovementSpeedMultiplierValue { get; set; } = 1f;

    private float LastToDieCloakedDamageTakenMultiplierValue { get; set; } = 1f;

    private bool LastToDieRogueCommanderEnabledValue { get; set; }

    private bool LastToDieProfessionalEnabledValue { get; set; }

    private bool LastToDieMultistabEnabledValue { get; set; }

    private bool LastToDieSpringLoadedEnabledValue { get; set; }

    private bool LastToDieInstastabEnabledValue { get; set; }

    private bool LastToDieHealstabEnabledValue { get; set; }

    private bool LastToDieHealingHarnessEnabledValue { get; set; }

    private bool LastToDieDoubleJumpEnabledValue { get; set; }

    private bool LastToDieMedicCombatMedicEnabledValue { get; set; }

    private bool LastToDieMedicSpikedVestEnabledValue { get; set; }

    private bool LastToDieMedicIronWillEnabledValue { get; set; }

    private bool LastToDieMedicModifiedSpringEnabledValue { get; set; }

    private bool LastToDieMedicRejuvenationRayEnabledValue { get; set; }

    private bool LastToDieMedicKritPowerEnabledValue { get; set; }

    private int LastToDieMedicIronWillHealingRemainder { get; set; }

    private bool LastToDieMedicStimulantDripLinkActiveValue { get; set; }

    private bool LastToDieMedicAgilityDriveLinkActiveValue { get; set; }

    private bool LastToDieMedicMartyrProtectedLinkActiveValue { get; set; }

    private bool LastToDieMedicMartyrProtectorLinkActiveValue { get; set; }

    private int LastToDieSpyCloakMeterUnitsValue { get; set; }

    private int LastToDieSpyCloakMeterMaximumUnitsValue { get; set; }

    private int LastToDieSpyRogueRampStacksValue { get; set; }

    private int LastToDieSpyRogueRampTicksValue { get; set; }

    private byte LastToDieProfessionalFireChordStateValue { get; set; }

    public bool LastToDieRogueCommanderEnabled => LastToDieRogueCommanderEnabledValue;

    public bool LastToDieProfessionalEnabled => LastToDieProfessionalEnabledValue;

    internal bool LastToDieMultistabEnabled => LastToDieMultistabEnabledValue;

    internal bool LastToDieSpringLoadedEnabled => LastToDieSpringLoadedEnabledValue;

    internal bool LastToDieInstastabEnabled => LastToDieInstastabEnabledValue;

    internal bool LastToDieHealstabEnabled => LastToDieHealstabEnabledValue;

    internal bool LastToDieHealingHarnessEnabled => LastToDieHealingHarnessEnabledValue;

    internal bool LastToDieDoubleJumpEnabled => LastToDieDoubleJumpEnabledValue;

    internal bool LastToDieMedicIronWillEnabled => LastToDieMedicIronWillEnabledValue;

    internal bool LastToDieMedicModifiedSpringEnabled =>
        IsAlive && ClassId == PlayerClass.Medic && LastToDieMedicModifiedSpringEnabledValue;

    public bool LastToDieMedicRejuvenationRayEnabled =>
        ClassId == PlayerClass.Medic && LastToDieMedicRejuvenationRayEnabledValue;

    public bool LastToDieMedicKritPowerEnabled =>
        ClassId == PlayerClass.Medic && LastToDieMedicKritPowerEnabledValue;

    public bool LastToDieMedicStimulantDripLinkActive =>
        IsAlive && LastToDieMedicStimulantDripLinkActiveValue;

    public bool LastToDieMedicAgilityDriveLinkActive =>
        IsAlive && LastToDieMedicAgilityDriveLinkActiveValue;

    public bool LastToDieMedicMartyrProtectedLinkActive =>
        IsAlive && LastToDieMedicMartyrProtectedLinkActiveValue;

    public bool LastToDieMedicMartyrProtectorLinkActive =>
        IsAlive && LastToDieMedicMartyrProtectorLinkActiveValue;

    public byte LastToDieMedicLinkState => EncodeLastToDieMedicLinkState(
        LastToDieMedicStimulantDripLinkActiveValue,
        LastToDieMedicAgilityDriveLinkActiveValue,
        LastToDieMedicMartyrProtectedLinkActiveValue,
        LastToDieMedicMartyrProtectorLinkActiveValue);

    internal float LastToDieMedicLinkAttackSpeedMultiplier =>
        LastToDieMedicStimulantDripLinkActive
            ? LastToDieDerivedModifiers.MedicStimulantDripAttackSpeedMultiplier
            : 1f;

    internal float LastToDieMedicLinkOutgoingDamageMultiplier =>
        LastToDieMedicStimulantDripLinkActive
            ? LastToDieDerivedModifiers.MedicStimulantDripDamageMultiplier
            : 1f;

    internal float LastToDieMedicLinkMovementSpeedMultiplier =>
        LastToDieMedicAgilityDriveLinkActive
            ? LastToDieDerivedModifiers.MedicAgilityDriveMovementSpeedMultiplier
            : 1f;

    internal float LastToDieMedicLinkEvasionChance =>
        LastToDieMedicAgilityDriveLinkActive
            ? LastToDieDerivedModifiers.MedicAgilityDriveEvasionChance
            : 0f;

    public bool LastToDieSniperExtremeConditioningEnabled =>
        IsAlive
        && ClassId == PlayerClass.Sniper
        && LastToDieSniperProfile.ExtremeConditioningEnabled;

    public int LastToDieSpyCloakMeterUnits => LastToDieSpyCloakMeterUnitsValue;

    public int LastToDieSpyCloakMeterMaximumUnits => LastToDieSpyCloakMeterMaximumUnitsValue;

    public int LastToDieSpyRogueRampStacks => LastToDieSpyRogueRampStacksValue;

    public int LastToDieSpyRogueRampTicks => LastToDieSpyRogueRampTicksValue;

    public byte LastToDieProfessionalFireChordState => LastToDieProfessionalFireChordStateValue;

    public float LastToDieSpyCloakMeterFraction => LastToDieSpyCloakMeterMaximumUnitsValue <= 0
        ? 1f
        : LastToDieSpyCloakMeterUnitsValue / (float)LastToDieSpyCloakMeterMaximumUnitsValue;

    internal bool HasLastToDieSpyCloakMeter =>
        LastToDieRogueCommanderEnabledValue || LastToDieProfessionalEnabledValue;

    internal bool CanFireLastToDieProfessionalRevolverWhileCloaked =>
        IsAlive
        && ClassId == PlayerClass.Spy
        && PrimaryWeapon.Kind == PrimaryWeaponKind.Revolver
        && IsSpyCloaked
        && LastToDieProfessionalEnabledValue
        && LastToDieSpyCloakMeterUnitsValue >= GetLastToDieProfessionalShotCost();

    internal float LastToDieSpyRogueOutgoingDamageBonus =>
        IsAlive && ClassId == PlayerClass.Spy && !IsSpyCloaked && LastToDieRogueCommanderEnabledValue
            ? LastToDieSpyRogueRampStacksValue * LastToDieDerivedModifiers.SpyRogueRampBonusPerStack
            : 0f;

    internal float LastToDieCloakedMovementSpeedMultiplier =>
        IsAlive && ClassId == PlayerClass.Spy && IsSpyCloaked
            ? LastToDieCloakedMovementSpeedMultiplierValue
            : 1f;

    internal float LastToDieCloakedDamageTakenMultiplier =>
        IsAlive && ClassId == PlayerClass.Spy && IsSpyCloaked
            ? LastToDieCloakedDamageTakenMultiplierValue
            : 1f;

    internal float LastToDieIncomingDamageMultiplier
    {
        get
        {
            if (!IsAlive)
            {
                return 1f;
            }

            var multiplier = 1f;
            if (LastToDieMedicStimulantDripLinkActiveValue)
            {
                multiplier *= LastToDieDerivedModifiers.MedicStimulantDripDamageTakenMultiplier;
            }

            if (ClassId == PlayerClass.Spy)
            {
                multiplier *= IsSpyCloaked
                    ? LastToDieCloakedDamageTakenMultiplierValue
                    : 1f;
                multiplier *= LastToDieRogueCommanderEnabledValue && !IsSpyCloaked
                    ? 1f - LastToDieSpyRogueOutgoingDamageBonus
                    : 1f;
            }
            else if (ClassId == PlayerClass.Medic)
            {
                if (LastToDieMedicMartyrProtectorLinkActiveValue)
                {
                    multiplier *= LastToDieDerivedModifiers.MedicMartyrDamageTakenMultiplier;
                }

                if (LastToDieMedicCombatMedicEnabledValue
                    && Health * 2L < MaxHealth)
                {
                    multiplier *= LastToDieDerivedModifiers.MedicCombatMedicDamageTakenMultiplier;
                }

                if (LastToDieMedicSpikedVestEnabledValue)
                {
                    multiplier *= LastToDieDerivedModifiers.MedicSpikedVestDamageTakenMultiplier;
                }
            }

            return Math.Clamp(multiplier, 0.05f, 1f);
        }
    }

    internal void SetLastToDieCloakedPerkMultipliers(
        float movementSpeedMultiplier,
        float damageTakenMultiplier)
    {
        LastToDieCloakedMovementSpeedMultiplierValue = MathF.Max(1f, movementSpeedMultiplier);
        LastToDieCloakedDamageTakenMultiplierValue = Math.Clamp(damageTakenMultiplier, 0.05f, 1f);
    }

    internal void ConfigureLastToDieMedicSelfPerks(
        bool combatMedicEnabled,
        bool spikedVestEnabled,
        bool ironWillEnabled,
        bool modifiedSpringEnabled,
        bool resetDynamicState)
    {
        var ironWillChanged = LastToDieMedicIronWillEnabledValue != ironWillEnabled;
        var modifiedSpringChanged =
            LastToDieMedicModifiedSpringEnabledValue != modifiedSpringEnabled;
        if (modifiedSpringChanged && ClassId == PlayerClass.Medic)
        {
            RescaleLastToDieMedicModifiedSpringTimers(
                LastToDieMedicModifiedSpringEnabledValue
                    ? LastToDieDerivedModifiers.MedicModifiedSpringAttackSpeedMultiplier
                    : 1f,
                modifiedSpringEnabled
                    ? LastToDieDerivedModifiers.MedicModifiedSpringAttackSpeedMultiplier
                    : 1f);
        }

        LastToDieMedicCombatMedicEnabledValue = combatMedicEnabled;
        LastToDieMedicSpikedVestEnabledValue = spikedVestEnabled;
        LastToDieMedicIronWillEnabledValue = ironWillEnabled;
        LastToDieMedicModifiedSpringEnabledValue = modifiedSpringEnabled;
        if (resetDynamicState || ironWillChanged)
        {
            LastToDieMedicIronWillHealingRemainder = 0;
        }
    }

    internal void ConfigureLastToDieMedicRejuvenationRay(bool enabled)
    {
        LastToDieMedicRejuvenationRayEnabledValue = enabled;
    }

    internal void ConfigureLastToDieMedicKritPower(bool enabled)
    {
        LastToDieMedicKritPowerEnabledValue = enabled;
    }

    private void RescaleLastToDieMedicModifiedSpringTimers(
        float oldSpeedMultiplier,
        float newSpeedMultiplier)
    {
        MedicNeedleCooldownTicks = RescaleLastToDieMedicLinkWeaponTimer(
            MedicNeedleCooldownTicks,
            oldSpeedMultiplier,
            newSpeedMultiplier);
        MedicNeedleRefillTicks = RescaleLastToDieMedicLinkWeaponTimer(
            MedicNeedleRefillTicks,
            oldSpeedMultiplier,
            newSpeedMultiplier);
        MedicHealDartCooldownTicks = RescaleLastToDieMedicLinkWeaponTimer(
            MedicHealDartCooldownTicks,
            oldSpeedMultiplier,
            newSpeedMultiplier);
    }

    internal bool TryRestoreLastToDieSupportRelayAmmo()
    {
        if (!IsAlive)
        {
            return false;
        }

        if (IsAcquiredWeaponEquipped)
        {
            var weapon = AcquiredWeapon;
            if (weapon is null || weapon.MaxAmmo <= 0)
            {
                return false;
            }

            if (weapon.Kind == PrimaryWeaponKind.FlameThrower)
            {
                return TryRestoreLastToDieSupportRelayPyroFuel();
            }

            var restored = ResolveLastToDieSupportRelayAmmoRestore(
                AcquiredWeaponCurrentShells,
                weapon.MaxAmmo);
            if (restored <= 0)
            {
                return false;
            }

            AcquiredWeaponCurrentShells += restored;
            return true;
        }

        if (IsExperimentalOffhandEquipped)
        {
            var weapon = ExperimentalOffhandWeapon;
            if (weapon is null || weapon.MaxAmmo <= 0)
            {
                return false;
            }

            var restored = ResolveLastToDieSupportRelayAmmoRestore(
                ExperimentalOffhandCurrentShells,
                weapon.MaxAmmo);
            if (restored <= 0)
            {
                return false;
            }

            ExperimentalOffhandCurrentShells += restored;
            return true;
        }

        if (ClassId == PlayerClass.Pyro)
        {
            return TryRestoreLastToDieSupportRelayPyroFuel();
        }

        var primaryRestored = ResolveLastToDieSupportRelayAmmoRestore(
            CurrentShells,
            MaxShells);
        if (primaryRestored <= 0)
        {
            return false;
        }

        CurrentShells += primaryRestored;
        return true;
    }

    private bool TryRestoreLastToDieSupportRelayPyroFuel()
    {
        var maximumScaledFuel = GetPyroPrimaryFuelMaxScaled();
        var scaledDeficit = maximumScaledFuel - GetPyroPrimaryFuelScaledValue();
        if (maximumScaledFuel <= 0 || scaledDeficit <= 0)
        {
            return false;
        }

        var scaledDivisor = checked(
            PyroPrimaryFuelScale * LastToDieDerivedModifiers.MedicSupportRelayAmmoRestoreDivisor);
        var restoredWholeFuel = checked((scaledDeficit + scaledDivisor - 1) / scaledDivisor);
        SetPyroPrimaryFuelScaled(checked(
            GetPyroPrimaryFuelScaledValue() + (restoredWholeFuel * PyroPrimaryFuelScale)));
        return true;
    }

    private static int ResolveLastToDieSupportRelayAmmoRestore(
        int currentAmmo,
        int maximumAmmo)
    {
        var deficit = Math.Max(0, maximumAmmo - Math.Max(0, currentAmmo));
        return deficit <= 0
            ? 0
            : checked(
                (deficit + LastToDieDerivedModifiers.MedicSupportRelayAmmoRestoreDivisor - 1)
                    / LastToDieDerivedModifiers.MedicSupportRelayAmmoRestoreDivisor);
    }

    private int ApplyLastToDieMedicPassiveRegenerationMultiplier(int healing)
    {
        if (healing <= 0
            || !LastToDieMedicIronWillEnabledValue
            || ClassId != PlayerClass.Medic
            || MaxHealth <= 0
            || Health * 10L >= MaxHealth * 3L)
        {
            LastToDieMedicIronWillHealingRemainder = 0;
            return healing;
        }

        var scaledHealing = (healing * LastToDieDerivedModifiers.MedicIronWillRegenerationNumerator)
            + LastToDieMedicIronWillHealingRemainder;
        LastToDieMedicIronWillHealingRemainder = scaledHealing
            % LastToDieDerivedModifiers.MedicIronWillRegenerationDenominator;
        return scaledHealing / LastToDieDerivedModifiers.MedicIronWillRegenerationDenominator;
    }

    internal void ResetLastToDieMedicDynamicState()
    {
        LastToDieMedicIronWillHealingRemainder = 0;
    }

    internal void SetLastToDieMedicLinkProjection(
        bool stimulantDripActive,
        bool agilityDriveActive,
        bool martyrProtectedActive = false,
        bool martyrProtectorActive = false)
    {
        ApplyLastToDieMedicLinkState(
            EncodeLastToDieMedicLinkState(
                stimulantDripActive,
                agilityDriveActive,
                martyrProtectedActive,
                martyrProtectorActive),
            rescaleActiveWeaponTimers: true,
            writeReplicatedState: true);
    }

    internal void HydrateProtocol64LastToDieMedicLinkState(byte encoded)
    {
        ApplyLastToDieMedicLinkState(
            encoded,
            rescaleActiveWeaponTimers: false,
            writeReplicatedState: false);
    }

    private void RefreshLastToDieMedicLinkFromReplicatedStateEntries()
    {
        var encoded = TryGetReplicatedStateInt(
            LastToDieMedicLinkReplicatedStateOwnerId,
            LastToDieMedicLinkReplicatedStateKey,
            out var replicatedState)
                ? replicatedState
                : 0;
        ApplyLastToDieMedicLinkState(
            encoded,
            rescaleActiveWeaponTimers: false,
            writeReplicatedState: false);
    }

    private void ApplyLastToDieMedicLinkState(
        int encoded,
        bool rescaleActiveWeaponTimers,
        bool writeReplicatedState)
    {
        var sanitized = checked((byte)(encoded & LastToDieMedicLinkKnownFlags));
        var stimulantDripActive = (sanitized & LastToDieMedicStimulantDripLinkFlag) != 0;
        var agilityDriveActive = (sanitized & LastToDieMedicAgilityDriveLinkFlag) != 0;
        var martyrProtectedActive = (sanitized & LastToDieMedicMartyrProtectedLinkFlag) != 0;
        var martyrProtectorActive = (sanitized & LastToDieMedicMartyrProtectorLinkFlag) != 0;
        if (LastToDieMedicStimulantDripLinkActiveValue == stimulantDripActive
            && LastToDieMedicAgilityDriveLinkActiveValue == agilityDriveActive
            && LastToDieMedicMartyrProtectedLinkActiveValue == martyrProtectedActive
            && LastToDieMedicMartyrProtectorLinkActiveValue == martyrProtectorActive)
        {
            return;
        }

        if (rescaleActiveWeaponTimers
            && LastToDieMedicStimulantDripLinkActiveValue != stimulantDripActive)
        {
            var oldSpeedMultiplier = LastToDieMedicStimulantDripLinkActiveValue
                ? LastToDieDerivedModifiers.MedicStimulantDripAttackSpeedMultiplier
                : 1f;
            var newSpeedMultiplier = stimulantDripActive
                ? LastToDieDerivedModifiers.MedicStimulantDripAttackSpeedMultiplier
                : 1f;
            RescaleLastToDieMedicLinkWeaponTimers(oldSpeedMultiplier, newSpeedMultiplier);
        }

        LastToDieMedicStimulantDripLinkActiveValue = stimulantDripActive;
        LastToDieMedicAgilityDriveLinkActiveValue = agilityDriveActive;
        LastToDieMedicMartyrProtectedLinkActiveValue = martyrProtectedActive;
        LastToDieMedicMartyrProtectorLinkActiveValue = martyrProtectorActive;
        if (!writeReplicatedState)
        {
            return;
        }

        if (sanitized == 0)
        {
            ClearReplicatedState(
                LastToDieMedicLinkReplicatedStateOwnerId,
                LastToDieMedicLinkReplicatedStateKey);
        }
        else
        {
            SetReplicatedStateInt(
                LastToDieMedicLinkReplicatedStateOwnerId,
                LastToDieMedicLinkReplicatedStateKey,
                sanitized);
        }
    }

    private void RescaleLastToDieMedicLinkWeaponTimers(
        float oldSpeedMultiplier,
        float newSpeedMultiplier)
    {
        PrimaryCooldownTicks = RescaleLastToDieMedicLinkWeaponTimer(
            PrimaryCooldownTicks,
            oldSpeedMultiplier,
            newSpeedMultiplier);
        ReloadTicksUntilNextShell = RescaleLastToDieMedicLinkWeaponTimer(
            ReloadTicksUntilNextShell,
            oldSpeedMultiplier,
            newSpeedMultiplier);
        ExperimentalOffhandCooldownTicks = RescaleLastToDieMedicLinkWeaponTimer(
            ExperimentalOffhandCooldownTicks,
            oldSpeedMultiplier,
            newSpeedMultiplier);
        ExperimentalOffhandReloadTicksUntilNextShell = RescaleLastToDieMedicLinkWeaponTimer(
            ExperimentalOffhandReloadTicksUntilNextShell,
            oldSpeedMultiplier,
            newSpeedMultiplier);
        AcquiredWeaponCooldownTicks = RescaleLastToDieMedicLinkWeaponTimer(
            AcquiredWeaponCooldownTicks,
            oldSpeedMultiplier,
            newSpeedMultiplier);
        AcquiredWeaponReloadTicksUntilNextShell = RescaleLastToDieMedicLinkWeaponTimer(
            AcquiredWeaponReloadTicksUntilNextShell,
            oldSpeedMultiplier,
            newSpeedMultiplier);
        MedicNeedleCooldownTicks = RescaleLastToDieMedicLinkWeaponTimer(
            MedicNeedleCooldownTicks,
            oldSpeedMultiplier,
            newSpeedMultiplier);
        MedicNeedleRefillTicks = RescaleLastToDieMedicLinkWeaponTimer(
            MedicNeedleRefillTicks,
            oldSpeedMultiplier,
            newSpeedMultiplier);
        PyroAirblastCooldownTicks = RescaleLastToDieMedicLinkWeaponTimer(
            PyroAirblastCooldownTicks,
            oldSpeedMultiplier,
            newSpeedMultiplier);
        PyroFlareCooldownTicks = RescaleLastToDieMedicLinkWeaponTimer(
            PyroFlareCooldownTicks,
            oldSpeedMultiplier,
            newSpeedMultiplier);
    }

    private static int RescaleLastToDieMedicLinkWeaponTimer(
        int remainingTicks,
        float oldSpeedMultiplier,
        float newSpeedMultiplier)
    {
        if (remainingTicks <= 0)
        {
            return 0;
        }

        return Math.Max(
            1,
            (int)MathF.Ceiling(remainingTicks * oldSpeedMultiplier / newSpeedMultiplier));
    }

    private static byte EncodeLastToDieMedicLinkState(
        bool stimulantDripActive,
        bool agilityDriveActive,
        bool martyrProtectedActive,
        bool martyrProtectorActive)
    {
        var encoded = (byte)0;
        if (stimulantDripActive)
        {
            encoded |= LastToDieMedicStimulantDripLinkFlag;
        }

        if (agilityDriveActive)
        {
            encoded |= LastToDieMedicAgilityDriveLinkFlag;
        }

        if (martyrProtectedActive)
        {
            encoded |= LastToDieMedicMartyrProtectedLinkFlag;
        }

        if (martyrProtectorActive)
        {
            encoded |= LastToDieMedicMartyrProtectorLinkFlag;
        }

        return encoded;
    }

    private int ApplyLastToDieSniperRifleCycleSpeed(int cooldownTicks)
    {
        cooldownTicks = Math.Max(1, cooldownTicks);
        if (!IsAlive
            || ClassId != PlayerClass.Sniper
            || PrimaryWeapon.Kind != PrimaryWeaponKind.Rifle
            || (!LastToDieSniperProfile.GreasedBoltEnabled
                && !LastToDieSniperProfile.LightMarksmanEnabled
                && !LastToDieSniperProfile.FiftyCalEnabled))
        {
            return cooldownTicks;
        }

        return LastToDieSniperProfile.ScaleRifleCycleTicks(cooldownTicks);
    }

    private float LastToDieSniperMovementSpeedMultiplier =>
        IsAlive
        && ClassId == PlayerClass.Sniper
        && LastToDieSniperProfile.ExtremeConditioningEnabled
            ? LastToDieSniperProfile.MovementSpeedMultiplier
            : 1f;

    internal void ConfigureLastToDieSpyStabAndJumpBootPerks(
        bool multistabEnabled,
        bool springLoadedEnabled,
        bool instastabEnabled,
        bool healstabEnabled,
        bool healingHarnessEnabled,
        bool doubleJumpEnabled,
        bool resetDynamicState)
    {
        var previousMaximumCharges = SpySuperjumpMaximumCharges;
        var previousAvailableCharges = SpySuperjumpAvailableCharges;
        LastToDieMultistabEnabledValue = multistabEnabled;
        LastToDieSpringLoadedEnabledValue = springLoadedEnabled;
        LastToDieInstastabEnabledValue = instastabEnabled;
        LastToDieHealstabEnabledValue = healstabEnabled;
        LastToDieHealingHarnessEnabledValue = healingHarnessEnabled;
        LastToDieDoubleJumpEnabledValue = doubleJumpEnabled;

        var maximumCharges = doubleJumpEnabled ? 2 : 1;
        SpySuperjumpMaximumChargesValue = maximumCharges;
        if (resetDynamicState)
        {
            SpySuperjumpAvailableCharges = maximumCharges;
            SpySuperjumpCooldownTicksRemaining = 0;
            CancelSpySuperjumpCharge();
        }
        else if (maximumCharges != previousMaximumCharges)
        {
            var spentCharges = Math.Max(0, previousMaximumCharges - previousAvailableCharges);
            SpySuperjumpAvailableCharges = Math.Clamp(maximumCharges - spentCharges, 0, maximumCharges);
        }
        else
        {
            SpySuperjumpAvailableCharges = Math.Clamp(
                SpySuperjumpAvailableCharges,
                0,
                maximumCharges);
        }
    }

    internal void ConfigureLastToDieSpyCloakMeter(
        bool rogueCommanderEnabled,
        bool professionalEnabled,
        int ticksPerSecond,
        bool resetDynamicState)
    {
        var wasEnabled = HasLastToDieSpyCloakMeter;
        var hadHydratedDynamicState = LastToDieSpyCloakMeterMaximumUnitsValue > 0;
        LastToDieRogueCommanderEnabledValue = rogueCommanderEnabled;
        LastToDieProfessionalEnabledValue = professionalEnabled;
        var enabled = HasLastToDieSpyCloakMeter;
        LastToDieSpyCloakMeterMaximumUnitsValue = enabled
            ? Math.Clamp(
                LastToDieDerivedModifiers.SpyCloakMeterDurationSeconds
                    * Math.Max(1, ticksPerSecond)
                    * LastToDieDerivedModifiers.SpyCloakMeterUnitsPerTick,
                LastToDieDerivedModifiers.SpyProfessionalShotCostDivisor,
                ushort.MaxValue)
            : 0;

        if (!professionalEnabled)
        {
            ResetLastToDieProfessionalFireChord();
        }

        if (!enabled)
        {
            LastToDieSpyCloakMeterUnitsValue = 0;
            ResetLastToDieSpyRogueRamp();
            ResetLastToDieProfessionalFireChord();
            return;
        }

        if (resetDynamicState || !wasEnabled && !hadHydratedDynamicState)
        {
            ResetLastToDieSpyCloakDynamicState();
        }
        else
        {
            LastToDieSpyCloakMeterUnitsValue = Math.Clamp(
                LastToDieSpyCloakMeterUnitsValue,
                0,
                LastToDieSpyCloakMeterMaximumUnitsValue);
            if (!LastToDieRogueCommanderEnabledValue)
            {
                ResetLastToDieSpyRogueRamp();
            }
        }
    }

    internal void HydrateLastToDieSpyCloakMeter(
        int meterUnits,
        int maximumUnits,
        int rogueRampStacks,
        int rogueRampTicks = 0)
    {
        LastToDieSpyCloakMeterMaximumUnitsValue = Math.Clamp(maximumUnits, 0, ushort.MaxValue);
        LastToDieSpyCloakMeterUnitsValue = Math.Clamp(
            meterUnits,
            0,
            LastToDieSpyCloakMeterMaximumUnitsValue);
        LastToDieSpyRogueRampStacksValue = Math.Clamp(
            rogueRampStacks,
            0,
            LastToDieDerivedModifiers.SpyRogueMaximumRampStacks);
        var ticksPerSecond = LastToDieSpyCloakMeterMaximumUnitsValue <= 0
            ? 1
            : Math.Max(
                1,
                LastToDieSpyCloakMeterMaximumUnitsValue
                    / (LastToDieDerivedModifiers.SpyCloakMeterDurationSeconds
                        * LastToDieDerivedModifiers.SpyCloakMeterUnitsPerTick));
        LastToDieSpyRogueRampTicksValue = LastToDieSpyRogueRampStacksValue
                >= LastToDieDerivedModifiers.SpyRogueMaximumRampStacks
            ? 0
            : Math.Clamp(rogueRampTicks, 0, ticksPerSecond - 1);
    }

    internal void AdvanceLastToDieSpyCloakMeter(int ticksPerSecond)
    {
        if (!HasLastToDieSpyCloakMeter || !IsAlive || ClassId != PlayerClass.Spy)
        {
            return;
        }

        if (IsSpyCloaked)
        {
            ResetLastToDieSpyRogueRamp();
            if (!LastToDieRogueCommanderEnabledValue)
            {
                return;
            }

            LastToDieSpyCloakMeterUnitsValue = Math.Max(
                0,
                LastToDieSpyCloakMeterUnitsValue - LastToDieDerivedModifiers.SpyCloakMeterUnitsPerTick);
            if (LastToDieSpyCloakMeterUnitsValue == 0)
            {
                ForceDecloak();
            }

            return;
        }

        LastToDieSpyCloakMeterUnitsValue = Math.Min(
            LastToDieSpyCloakMeterMaximumUnitsValue,
            LastToDieSpyCloakMeterUnitsValue + LastToDieDerivedModifiers.SpyCloakMeterUnitsPerTick);
        if (!LastToDieRogueCommanderEnabledValue
            || LastToDieSpyRogueRampStacksValue >= LastToDieDerivedModifiers.SpyRogueMaximumRampStacks)
        {
            return;
        }

        LastToDieSpyRogueRampTicksValue += 1;
        if (LastToDieSpyRogueRampTicksValue >= Math.Max(1, ticksPerSecond))
        {
            LastToDieSpyRogueRampTicksValue = 0;
            LastToDieSpyRogueRampStacksValue += 1;
        }
    }

    internal void ResetLastToDieSpyCloakDynamicState()
    {
        LastToDieSpyCloakMeterUnitsValue = LastToDieSpyCloakMeterMaximumUnitsValue;
        ResetLastToDieSpyRogueRamp();
        ResetLastToDieProfessionalFireChord();
    }

    internal void OnLastToDieSpyCloakStarted()
    {
        ResetLastToDieSpyRogueRamp();
        ResetLastToDieProfessionalFireChord();
    }

    internal bool TryBeginLastToDieProfessionalFireChord()
    {
        if (LastToDieProfessionalFireChordStateValue != LastToDieProfessionalFireChordInactive)
        {
            return true;
        }

        if (!CanFireLastToDieProfessionalRevolverWhileCloaked)
        {
            return false;
        }

        SetLastToDieProfessionalFireChordState(LastToDieProfessionalFireChordArmed);
        return true;
    }

    internal bool MarkLastToDieProfessionalFireChordConsumed()
    {
        if (!IsAlive
            || ClassId != PlayerClass.Spy
            || !IsSpyCloaked
            || !LastToDieProfessionalEnabledValue)
        {
            return false;
        }

        SetLastToDieProfessionalFireChordState(LastToDieProfessionalFireChordConsumed);
        return true;
    }

    internal bool TryReleaseLastToDieProfessionalFireChord(out bool shouldDecloak)
    {
        shouldDecloak = false;
        if (LastToDieProfessionalFireChordStateValue == LastToDieProfessionalFireChordInactive)
        {
            return false;
        }

        shouldDecloak = LastToDieProfessionalFireChordStateValue == LastToDieProfessionalFireChordArmed
            && IsAlive
            && ClassId == PlayerClass.Spy
            && IsSpyCloaked
            && LastToDieProfessionalEnabledValue;
        ResetLastToDieProfessionalFireChord();
        return true;
    }

    internal void HydrateLastToDieProfessionalFireChordState(byte state)
    {
        var sanitized = state <= LastToDieProfessionalFireChordConsumed
            ? state
            : LastToDieProfessionalFireChordInactive;
        SetLastToDieProfessionalFireChordState(sanitized);
    }

    private void RefreshLastToDieProfessionalFireChordFromReplicatedStateEntries()
    {
        var state = TryGetReplicatedStateInt(
            LastToDieWeaponReplicatedStateOwnerId,
            LastToDieProfessionalFireChordReplicatedStateKey,
            out var replicatedState)
                && replicatedState is >= LastToDieProfessionalFireChordInactive
                    and <= LastToDieProfessionalFireChordConsumed
                ? (byte)replicatedState
                : LastToDieProfessionalFireChordInactive;
        LastToDieProfessionalFireChordStateValue = state;
    }

    private void ResetLastToDieProfessionalFireChord() =>
        SetLastToDieProfessionalFireChordState(LastToDieProfessionalFireChordInactive);

    private void SetLastToDieProfessionalFireChordState(byte state)
    {
        LastToDieProfessionalFireChordStateValue = state;
        if (state == LastToDieProfessionalFireChordInactive)
        {
            ClearReplicatedState(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieProfessionalFireChordReplicatedStateKey);
        }
        else
        {
            SetReplicatedStateInt(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieProfessionalFireChordReplicatedStateKey,
                state);
        }
    }

    internal bool TrySpendLastToDieProfessionalShotCost()
    {
        if (!CanFireLastToDieProfessionalRevolverWhileCloaked)
        {
            return false;
        }

        LastToDieSpyCloakMeterUnitsValue -= GetLastToDieProfessionalShotCost();
        return true;
    }

    private int GetLastToDieProfessionalShotCost() =>
        Math.Max(1, LastToDieSpyCloakMeterMaximumUnitsValue / LastToDieDerivedModifiers.SpyProfessionalShotCostDivisor);

    private void ResetLastToDieSpyRogueRamp()
    {
        LastToDieSpyRogueRampStacksValue = 0;
        LastToDieSpyRogueRampTicksValue = 0;
    }

    internal void ClearLastToDiePerkModifiers()
    {
        SetLastToDieCloakedPerkMultipliers(1f, 1f);
        ConfigureLastToDieSpyCloakMeter(
            rogueCommanderEnabled: false,
            professionalEnabled: false,
            ticksPerSecond: 1,
            resetDynamicState: true);
        ConfigureLastToDieSpyStabAndJumpBootPerks(
            multistabEnabled: false,
            springLoadedEnabled: false,
            instastabEnabled: false,
            healstabEnabled: false,
            healingHarnessEnabled: false,
            doubleJumpEnabled: false,
            resetDynamicState: true);
        ConfigureLastToDieSpyInfiltrate(
            enabled: false,
            ticksPerSecond: 1,
            resetDynamicState: true);
        ConfigureLastToDieSpyAfterlife(
            enabled: false,
            ticksPerSecond: 1,
            resetDynamicState: true);
        ConfigureLastToDieMedicSelfPerks(
            combatMedicEnabled: false,
            spikedVestEnabled: false,
            ironWillEnabled: false,
            modifiedSpringEnabled: false,
            resetDynamicState: true);
        ConfigureLastToDieMedicRejuvenationRay(enabled: false);
        SetLastToDieMedicLinkProjection(
            stimulantDripActive: false,
            agilityDriveActive: false,
            martyrProtectedActive: false,
            martyrProtectorActive: false);
        SetLastToDieSniperProfile(null);
    }
}
