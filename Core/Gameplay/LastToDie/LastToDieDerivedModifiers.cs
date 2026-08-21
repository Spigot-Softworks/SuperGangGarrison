namespace OpenGarrison.Core.LastToDie;

/// <summary>
/// Pure, per-player aggregation of LTD perks. Runtime code consumes this value
/// instead of consulting the client-owned offline experimental settings.
/// </summary>
public sealed record LastToDieDerivedModifiers(
    int MaximumHealthBonus = 0,
    float DamageHealingFraction = 0f,
    float ScopedHealingPerSecond = 0f,
    float GroundedVsAirborneDamageMultiplier = 1f,
    float AirborneVsGroundedDamageMultiplier = 1f,
    float CloakedMovementSpeedMultiplier = 1f,
    float CloakedHealingPerSecond = 0f,
    float CloakedDamageTakenMultiplier = 1f,
    float CloakedEvasionChance = 0f,
    bool RogueCommanderEnabled = false,
    bool ProfessionalEnabled = false,
    bool MultistabEnabled = false,
    bool SpringLoadedEnabled = false,
    bool InstastabEnabled = false,
    bool HealstabEnabled = false,
    bool HealingHarnessEnabled = false,
    bool DoubleJumpEnabled = false,
    bool InfiltrateEnabled = false,
    bool AfterlifeEnabled = false,
    bool MedicTraumaSurgeonEnabled = false,
    float MedicUberChargeGainMultiplier = 1f,
    float MedicHomeostasisHealingFraction = 0f,
    bool MedicCombatMedicEnabled = false,
    bool MedicFieldCommanderEnabled = false,
    bool MedicStoicEnabled = false,
    bool MedicSpikedVestEnabled = false,
    bool MedicIronWillEnabled = false,
    bool MedicExsanguinationEnabled = false,
    bool MedicStimulantDripEnabled = false,
    bool MedicAgilityDriveEnabled = false,
    bool MedicRejuvenationRayEnabled = false,
    bool MedicModifiedSpringEnabled = false,
    bool MedicSupportRelayEnabled = false,
    bool MedicMartyrEnabled = false,
    bool MedicHailMaryEnabled = false,
    bool MedicNeurotoxinEnabled = false,
    bool MedicJavelinEnabled = false,
    bool MedicKritPowerEnabled = false,
    LastToDieSniperProfile? SniperProfile = null,
    LastToDieSpyRevolverProfile? SpyRevolverProfile = null)
{
    public const float SpyStanceDamageMultiplier = 1.6f;

    public const float SpyRejuvenationMovementSpeedMultiplier = 1.3f;

    public const float SpyRejuvenationHealingPerSecond = 9f;

    public const float SpyChameleonShellDamageTakenMultiplier = 0.4f;

    public const float SpyShroudEvasionChance = 0.6f;

    public const float SpyVampireDamageHealingFraction = 0.111f;

    public const int SpyCloakMeterDurationSeconds = 8;

    public const int SpyCloakMeterUnitsPerTick = 5;

    public const int SpyProfessionalShotCostDivisor = 5;

    public const int SpyRogueMaximumRampStacks = 10;

    public const float SpyRogueRampBonusPerStack = 0.05f;

    public const int SpyVampireHealingNumerator = 111;

    public const int SpyVampireHealingDenominator = 1000;

    public const int SpyHealstabHealing = 60;

    public const int SpyHealingHarnessHealing = 60;

    public const float SpyMultistabRadius = 96f;

    public const int SpyInstastabSpeedMultiplier = 6;

    public const int SpyDoubleJumpChargeSpeedMultiplier = 2;

    public const float SpyInfiltrateDurationSeconds = 0.30f;

    public const float SpyInfiltrateCooldownSeconds = 6f;

    public const float SpyInfiltrateDistance = 220f;

    public const float SpyInfiltrateHorizontalSpeed =
        SpyInfiltrateDistance / SpyInfiltrateDurationSeconds;

    public const float SpyAfterlifeWindowSeconds = 5f;

    public const float SpyAfterlifeCooldownSeconds = 60f;

    public const float MedicTraumaSurgeonMaximumHealingMultiplier = 1.5f;

    public const float MedicTraumaSurgeonMaximumHealingHealthFraction = 0.1f;

    public const float MedicOverchargedUberChargeGainMultiplier = 2f;

    public const float MedicHomeostasisHealingShare = 0.35f;

    public const int MedicHomeostasisHealingNumerator = 7;

    public const int MedicHomeostasisHealingDenominator = 20;

    public const float MedicCombatMedicDamageMultiplier = 1.3f;

    public const float MedicCombatMedicDamageTakenMultiplier = 0.7f;

    public const float MedicCombatMedicHealthThreshold = 0.5f;

    public const float MedicStoicMaximumEvasionChance = 0.5f;

    public const float MedicSpikedVestDamageTakenMultiplier = 0.85f;

    public const float MedicSpikedVestReflectionFraction = 0.3f;

    public const int MedicSpikedVestReflectionNumerator = 3;

    public const int MedicSpikedVestReflectionDenominator = 10;

    public const float MedicIronWillHealthThreshold = 0.3f;

    public const float MedicIronWillRegenerationMultiplier = 2.5f;

    public const int MedicIronWillRegenerationNumerator = 5;

    public const int MedicIronWillRegenerationDenominator = 2;

    public const int MedicExsanguinationDurationSeconds = 3;

    public const float MedicExsanguinationBleedDamagePerSecond = 2f;

    public const float MedicExsanguinationMovementSpeedMultiplier = 0.8f;

    public const float MedicStimulantDripAttackSpeedMultiplier = 1.2f;

    public const float MedicStimulantDripDamageMultiplier = 1.2f;

    public const float MedicStimulantDripDamageTakenMultiplier = 0.8f;

    public const float MedicAgilityDriveMovementSpeedMultiplier = 1.25f;

    public const float MedicAgilityDriveEvasionChance = 0.25f;

    public const float MedicRejuvenationRayHealingMultiplier = 4f;

    public const float MedicModifiedSpringAttackSpeedMultiplier = 2f;

    public const int MedicSupportRelayAmmoRestoreDivisor = 5;

    public const int MedicSupportRelayCooldownSeconds = 5;

    public const float MedicMartyrDamageTakenMultiplier = 0.7f;

    public const float MedicHailMaryInvulnerabilitySeconds = 0.5f;

    public const int MedicNeurotoxinStunSeconds = 2;

    public const int MedicNeurotoxinPreStunnedDamageMultiplier = 3;

    public const float MedicKritPowerCriticalDamageMultiplier = 3.5f;

    public const float MedicJavelinFuseSeconds = 0.75f;

    public const float MedicJavelinBlastRadius = 96f;

    public const int MedicJavelinEnemyCenterDamage = 22;

    public const int MedicJavelinEnemyEdgeDamage = 11;

    public const int MedicJavelinAllyCenterHealing = 30;

    public const int MedicJavelinAllyEdgeHealing = 15;

    public static LastToDieDerivedModifiers FromPerks(IEnumerable<LastToDiePerkId> perks)
    {
        ArgumentNullException.ThrowIfNull(perks);
        var owned = perks.ToHashSet();
        var spyRevolverProfile = LastToDieSpyRevolverProfile.FromPerks(owned);
        var sniperProfile = LastToDieSniperProfile.FromPerks(owned);
        return new LastToDieDerivedModifiers(
            MaximumHealthBonus: owned.Contains(LastToDiePerkIds.Medic.VitalityTrinket) ? 75 : 0,
            DamageHealingFraction: owned.Contains(LastToDiePerkIds.Spy.Vampire)
                ? SpyVampireDamageHealingFraction
                : 0f,
            ScopedHealingPerSecond: owned.Contains(LastToDiePerkIds.Sniper.Zen) ? 7f : 0f,
            GroundedVsAirborneDamageMultiplier: owned.Contains(LastToDiePerkIds.Spy.Grounded)
                ? SpyStanceDamageMultiplier
                : 1f,
            AirborneVsGroundedDamageMultiplier: owned.Contains(LastToDiePerkIds.Spy.Acrobat)
                ? SpyStanceDamageMultiplier
                : 1f,
            CloakedMovementSpeedMultiplier: owned.Contains(LastToDiePerkIds.Spy.Rejuvenation)
                ? SpyRejuvenationMovementSpeedMultiplier
                : 1f,
            CloakedHealingPerSecond: owned.Contains(LastToDiePerkIds.Spy.Rejuvenation)
                ? SpyRejuvenationHealingPerSecond
                : 0f,
            CloakedDamageTakenMultiplier: owned.Contains(LastToDiePerkIds.Spy.ChameleonShell)
                ? SpyChameleonShellDamageTakenMultiplier
                : 1f,
            CloakedEvasionChance: owned.Contains(LastToDiePerkIds.Spy.Shroud)
                ? SpyShroudEvasionChance
                : 0f,
            RogueCommanderEnabled: owned.Contains(LastToDiePerkIds.Spy.RogueCommander),
            ProfessionalEnabled: owned.Contains(LastToDiePerkIds.Spy.Professional),
            MultistabEnabled: owned.Contains(LastToDiePerkIds.Spy.Multistab),
            SpringLoadedEnabled: owned.Contains(LastToDiePerkIds.Spy.SpringLoaded),
            InstastabEnabled: owned.Contains(LastToDiePerkIds.Spy.Instastab),
            HealstabEnabled: owned.Contains(LastToDiePerkIds.Spy.Healstab),
            HealingHarnessEnabled: owned.Contains(LastToDiePerkIds.Spy.HealingHarness),
            DoubleJumpEnabled: owned.Contains(LastToDiePerkIds.Spy.DoubleJump),
            InfiltrateEnabled: owned.Contains(LastToDiePerkIds.Spy.Infiltrate),
            AfterlifeEnabled: owned.Contains(LastToDiePerkIds.Spy.Afterlife),
            MedicTraumaSurgeonEnabled: owned.Contains(LastToDiePerkIds.Medic.TraumaSurgeon),
            MedicUberChargeGainMultiplier: owned.Contains(LastToDiePerkIds.Medic.Overcharged)
                ? MedicOverchargedUberChargeGainMultiplier
                : 1f,
            MedicHomeostasisHealingFraction: owned.Contains(LastToDiePerkIds.Medic.Homeostasis)
                ? MedicHomeostasisHealingShare
                : 0f,
            MedicCombatMedicEnabled: owned.Contains(LastToDiePerkIds.Medic.CombatMedic),
            MedicFieldCommanderEnabled: owned.Contains(LastToDiePerkIds.Medic.FieldCommander),
            MedicStoicEnabled: owned.Contains(LastToDiePerkIds.Medic.Stoic),
            MedicSpikedVestEnabled: owned.Contains(LastToDiePerkIds.Medic.SpikedVest),
            MedicIronWillEnabled: owned.Contains(LastToDiePerkIds.Medic.IronWill),
            MedicExsanguinationEnabled: owned.Contains(LastToDiePerkIds.Medic.Exsanguination),
            MedicStimulantDripEnabled: owned.Contains(LastToDiePerkIds.Medic.StimulantDrip),
            MedicAgilityDriveEnabled: owned.Contains(LastToDiePerkIds.Medic.AgilityDrive),
            MedicRejuvenationRayEnabled: owned.Contains(LastToDiePerkIds.Medic.RejuvenationRay),
            MedicModifiedSpringEnabled: owned.Contains(LastToDiePerkIds.Medic.ModifiedSpring),
            MedicSupportRelayEnabled: owned.Contains(LastToDiePerkIds.Medic.SupportRelay),
            MedicMartyrEnabled: owned.Contains(LastToDiePerkIds.Medic.Martyr),
            MedicHailMaryEnabled: owned.Contains(LastToDiePerkIds.Medic.HailMary),
            MedicNeurotoxinEnabled: owned.Contains(LastToDiePerkIds.Medic.Neurotoxin),
            MedicJavelinEnabled: owned.Contains(LastToDiePerkIds.Medic.Javelin),
            MedicKritPowerEnabled: owned.Contains(LastToDiePerkIds.Medic.KritPower),
            SniperProfile: sniperProfile.IsActive ? sniperProfile : null,
            SpyRevolverProfile: spyRevolverProfile.IsActive ? spyRevolverProfile : null);
    }
}
