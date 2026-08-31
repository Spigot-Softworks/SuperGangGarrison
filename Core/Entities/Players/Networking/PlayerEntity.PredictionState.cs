namespace OpenGarrison.Core;

using OpenGarrison.GameplayModding;

public sealed partial class PlayerEntity
{
    internal readonly record struct PredictionState(
        PlayerTeam Team,
        CharacterClassDefinition ClassDefinition,
        bool IsAlive,
        float X,
        float Y,
        float HorizontalSpeed,
        float VerticalSpeed,
        float LegacyStateTickAccumulator,
        LegacyMovementState MovementState,
        bool IsGrounded,
        int BlockedJumpRetrySuppressionTicksRemaining,
        bool IsExperimentalDemoknightChargeDashActive,
        bool IsExperimentalDemoknightChargeFlightActive,
        float ExperimentalDemoknightChargeAcceleration,
        int Health,
        int? NetworkMaxHealthOverrideValue,
        float Metal,
        bool IsCarryingIntel,
        int IntelPickupCooldownTicks,
        float IntelRechargeTicks,
        bool IsInSpawnRoom,
        int RemainingAirJumps,
        float FacingDirectionX,
        float AimDirectionDegrees,
        float SourceFacingDirectionX,
        float PreviousSourceFacingDirectionX,
        int CurrentShells,
        int PrimaryCooldownTicks,
        int ReloadTicksUntilNextShell,
        PrimaryWeaponDefinition? ExperimentalOffhandWeapon,
        int ExperimentalOffhandCurrentShells,
        int ExperimentalOffhandCooldownTicks,
        int ExperimentalOffhandReloadTicksUntilNextShell,
        bool IsExperimentalOffhandEquipped,
        PlayerClass? AcquiredWeaponClassId,
        int AcquiredWeaponCurrentShells,
        int AcquiredWeaponCooldownTicks,
        int AcquiredWeaponReloadTicksUntilNextShell,
        bool IsAcquiredWeaponEquipped,
        float ContinuousDamageAccumulator,
        int TimeUnscathedSourceTicks,
        int MedicPassiveRegenElapsedSourceTicks,
        bool IsHeavyEating,
        int HeavyEatTicksRemaining,
        int HeavyEatCooldownTicksRemaining,
        int HeavyEatCooldownDurationTicks,
        float HeavyHealingAccumulator,
        bool IsTaunting,
        float TauntFrameIndex,
        bool IsSniperScoped,
        int SniperChargeTicks,
        int SniperBowChargeTicks,
        bool IsUsingBinoculars,
        float BinocularsFocusX,
        float BinocularsFocusY,
        int UberTicksRemaining,
        int KritzCritBoostTicksRemaining,
        int KritzCritBoostProviderPlayerId,
        int KritzCritBoostProviderSlot,
        float KritzCritBoostDamageMultiplier,
        int? MedicHealTargetId,
        bool IsMedicHealing,
        float MedicUberCharge,
        bool IsMedicUberReady,
        bool IsMedicUbering,
        MedicUberDeliveryMode MedicUberDeliveryMode,
        int MedicNeedleCooldownTicks,
        int MedicNeedleRefillTicks,
        float ContinuousHealingAccumulator,
        int QuoteBubbleCount,
        int QuoteBladesOut,
        int CivvieUmbrellaChargeTicks,
        bool IsCivvieUmbrellaActive,
        bool IsCivvieUmbrellaBroken,
        bool CivvieUmbrellaAirLiftUsed,
        bool IsCivviePogoActive,
        bool IsCivviePogoSuperJumpAirPhaseActive,
        bool CivviePogoSuperJumpTrickUsed,
        int CivviePogoCrunchTicksRemaining,
        int CivviePogoTrickTicksRemaining,
        int CivviePogoTrickDurationTicks,
        int PyroAirblastCooldownTicks,
        bool IsSpyCloaked,
        float SpyCloakAlpha,
        bool IsSpySuperjumping,
        float SpySuperjumpHorizontalVelocity,
        int SpySuperjumpCooldownTicksRemaining,
        int SpyBackstabWindupTicksRemaining,
        int SpyBackstabRecoveryTicksRemaining,
        int SpyBackstabVisualTicksRemaining,
        float SpyBackstabDirectionDegrees,
        bool SpyBackstabHitboxPending,
        bool IsSpyVisibleToEnemies,
        float BurnIntensity,
        float BurnDurationSourceTicks,
        float BurnDecayDelaySourceTicksRemaining,
        float BurnIntensityDecayPerSourceTick,
        int? BurnedByPlayerId,
        float NapalmCoveredSourceTicks,
        int Kills,
        int Deaths,
        int Caps,
        float Points,
        int HealPoints,
        int ActiveDominationCount,
        bool IsDominatingLocalViewer,
        bool IsDominatedByLocalViewer,
        bool IsChatBubbleVisible,
        int ChatBubbleFrameIndex,
        float ChatBubbleAlpha,
        bool IsChatBubbleFading,
        int ChatBubbleTicksRemaining,
        bool IsTypingChatMessage = false,
        string? SelectedGameplayLoadoutId = null,
        string? SelectedGameplayPrimaryItemId = null,
        GameplayEquipmentSlot SelectedGameplayEquippedSlot = GameplayEquipmentSlot.Primary,
        int PyroFlareCooldownTicks = 0,
        int PyroPrimaryFuelScaled = 0,
        bool IsPyroPrimaryRefilling = false,
        int PyroFlameLoopTicksRemaining = 0,
        bool PyroPrimaryRequiresReleaseAfterEmpty = false,
        int Assists = 0,
        ulong BadgeMask = 0,
        int? LastDamageDealerPlayerId = null,
        int LastDamageDealerAssistTicksRemaining = 0,
        int? SecondToLastDamageDealerPlayerId = null,
        int SecondToLastDamageDealerAssistTicksRemaining = 0,
        GameplayReplicatedStateEntry[]? ReplicatedStateEntries = null,
        int SpySuperjumpChargeTicks = 0,
        float SpySuperjumpChargeDirectionDegrees = 0f,
        byte SpySuperjumpChargeStartMovementButtons = 0,
        bool SpySuperjumpChargeStartBlockedUntilAbilityRelease = false,
        int ExperimentalGhostDashTicksRemaining = 0,
        int ExperimentalGhostDashCooldownTicksRemaining = 0,
        int ExperimentalGhostDashVisibilityTicksRemaining = 0,
        int ExperimentalGhostDashMovementTicksRemaining = 0,
        float ExperimentalGhostDashDistanceRemaining = 0f,
        float ExperimentalGhostDashSpeedPerSecondValue = 0f,
        bool ExperimentalGhostDashUsesMomentum = false,
        float ExperimentalGhostDashBurstSpeedMultiplier = 0f,
        bool ExperimentalGhostDashDisablesGravity = false,
        bool ExperimentalGhostDashEnablesTrail = false,
        int ExperimentalGhostDashInitialTicks = 0,
        float ExperimentalGhostDashInitialDistance = 0f,
        float ExperimentalGhostDashDistanceTraveled = 0f,
        float ExperimentalGhostDashLastMoveDistance = 0f,
        float ExperimentalGhostDashMomentumDirectionX = 1f,
        float ExperimentalGhostDashSlideVelocityPerTick = ExperimentalGameplaySettings.DefaultGhostDashSlideVelocityPerTick,
        float ExperimentalGhostDashSlideVisualSpeedPerSecond = 0f,
        float ExperimentalGhostDashSlideVisualInitialSpeedPerSecond = 0f,
        float ExperimentalGhostDashTrailAlphaValue = 0f,
        float ExperimentalGhostDashNextAttackDamageMultiplierValue = 1f,
        float LastToDieCloakedMovementSpeedMultiplierValue = 1f,
        float LastToDieCloakedDamageTakenMultiplierValue = 1f,
        bool LastToDieRogueCommanderEnabledValue = false,
        bool LastToDieProfessionalEnabledValue = false,
        int LastToDieSpyCloakMeterUnitsValue = 0,
        int LastToDieSpyCloakMeterMaximumUnitsValue = 0,
        int LastToDieSpyRogueRampStacksValue = 0,
        int LastToDieSpyRogueRampTicksValue = 0,
        bool LastToDieMultistabEnabledValue = false,
        bool LastToDieSpringLoadedEnabledValue = false,
        bool LastToDieInstastabEnabledValue = false,
        bool LastToDieHealstabEnabledValue = false,
        bool LastToDieHealingHarnessEnabledValue = false,
        bool LastToDieDoubleJumpEnabledValue = false,
        int SpySuperjumpAvailableCharges = 1,
        bool LastToDieMedicCombatMedicEnabledValue = false,
        bool LastToDieMedicSpikedVestEnabledValue = false,
        bool LastToDieMedicIronWillEnabledValue = false,
        int LastToDieMedicIronWillHealingRemainder = 0,
        bool LastToDieMedicStimulantDripLinkActiveValue = false,
        bool LastToDieMedicAgilityDriveLinkActiveValue = false,
        bool LastToDieMedicMartyrProtectedLinkActiveValue = false,
        bool LastToDieMedicMartyrProtectorLinkActiveValue = false,
        bool LastToDieMedicKritPowerEnabledValue = false,
        bool IsDispenserBuffedValue = false,
        float DispenserAttackReloadSpeedMultiplierValue = 1f,
        int BuffBannerChargeKillsValue = 0,
        int BuffBannerDeployTicksRemainingValue = 0,
        int BuffBannerActiveTicksRemainingValue = 0);

    internal PredictionState CapturePredictionState()
    {
        return new PredictionState(
            Team,
            ClassDefinition,
            IsAlive,
            X,
            Y,
            HorizontalSpeed,
            VerticalSpeed,
            LegacyStateTickAccumulator,
            MovementState,
            IsGrounded,
            BlockedJumpRetrySuppressionTicksRemaining,
            IsExperimentalDemoknightChargeDashActive,
            IsExperimentalDemoknightChargeFlightActive,
            ExperimentalDemoknightChargeAcceleration,
            Health,
            NetworkMaxHealthOverrideValue,
            Metal,
            IsCarryingIntel,
            IntelPickupCooldownTicks,
            IntelRechargeTicks,
            IsInSpawnRoom,
            RemainingAirJumps,
            FacingDirectionX,
            AimDirectionDegrees,
            SourceFacingDirectionX,
            PreviousSourceFacingDirectionX,
            CurrentShells,
            PrimaryCooldownTicks,
            ReloadTicksUntilNextShell,
            ExperimentalOffhandWeapon,
            ExperimentalOffhandCurrentShells,
            ExperimentalOffhandCooldownTicks,
            ExperimentalOffhandReloadTicksUntilNextShell,
            IsExperimentalOffhandEquipped,
            AcquiredWeaponClassId,
            AcquiredWeaponCurrentShells,
            AcquiredWeaponCooldownTicks,
            AcquiredWeaponReloadTicksUntilNextShell,
            IsAcquiredWeaponEquipped,
            ContinuousDamageAccumulator,
            TimeUnscathedSourceTicks,
            MedicPassiveRegenElapsedSourceTicks,
            IsHeavyEating,
            HeavyEatTicksRemaining,
            HeavyEatCooldownTicksRemaining,
            HeavyEatCooldownDurationTicks,
            HeavyHealingAccumulator,
            IsTaunting,
            TauntFrameIndex,
            IsSniperScoped,
            SniperChargeTicks,
            SniperBowChargeTicks,
            IsUsingBinoculars,
            BinocularsFocusX,
            BinocularsFocusY,
            UberTicksRemaining,
            KritzCritBoostTicksRemaining,
            KritzCritBoostProviderPlayerId,
            KritzCritBoostProviderSlot,
            KritzCritBoostDamageMultiplier,
            MedicHealTargetId,
            IsMedicHealing,
            MedicUberCharge,
            IsMedicUberReady,
            IsMedicUbering,
            MedicUberDeliveryMode,
            MedicNeedleCooldownTicks,
            MedicNeedleRefillTicks,
            ContinuousHealingAccumulator,
            QuoteBubbleCount,
            QuoteBladesOut,
            CivvieUmbrellaChargeTicks,
            IsCivvieUmbrellaActive,
            IsCivvieUmbrellaBroken,
            CivvieUmbrellaAirLiftUsed,
            IsCivviePogoActive,
            IsCivviePogoSuperJumpAirPhaseActive,
            CivviePogoSuperJumpTrickUsed,
            CivviePogoCrunchTicksRemaining,
            CivviePogoTrickTicksRemaining,
            CivviePogoTrickDurationTicks,
            PyroAirblastCooldownTicks,
            IsSpyCloaked,
            SpyCloakAlpha,
            IsSpySuperjumping,
            SpySuperjumpHorizontalVelocity,
            SpySuperjumpCooldownTicksRemaining,
            SpyBackstabWindupTicksRemaining,
            SpyBackstabRecoveryTicksRemaining,
            SpyBackstabVisualTicksRemaining,
            SpyBackstabDirectionDegrees,
            SpyBackstabHitboxPending,
            IsSpyVisibleToEnemies,
            BurnIntensity,
            BurnDurationSourceTicks,
            BurnDecayDelaySourceTicksRemaining,
            BurnIntensityDecayPerSourceTick,
            BurnedByPlayerId,
            NapalmCoveredSourceTicks,
            Kills,
            Deaths,
            Caps,
            Points,
            HealPoints,
            ActiveDominationCount,
            IsDominatingLocalViewer,
            IsDominatedByLocalViewer,
            IsChatBubbleVisible,
            ChatBubbleFrameIndex,
            ChatBubbleAlpha,
            IsChatBubbleFading,
            ChatBubbleTicksRemaining,
            IsTypingChatMessage,
            SelectedGameplayLoadoutId,
            SelectedGameplayPrimaryItemId,
            SelectedGameplayEquippedSlot,
            PyroFlareCooldownTicks,
            PyroPrimaryFuelScaled,
            IsPyroPrimaryRefilling,
            PyroFlameLoopTicksRemaining,
            PyroPrimaryRequiresReleaseAfterEmpty,
            Assists,
            BadgeMask,
            LastDamageDealerPlayerId,
            LastDamageDealerAssistTicksRemaining,
            SecondToLastDamageDealerPlayerId,
            SecondToLastDamageDealerAssistTicksRemaining,
            GetReplicatedStateEntries().ToArray(),
            SpySuperjumpChargeTicks,
            SpySuperjumpChargeDirectionDegrees,
            SpySuperjumpChargeStartMovementButtons,
            SpySuperjumpChargeStartBlockedUntilAbilityRelease,
            ExperimentalGhostDashTicksRemaining,
            ExperimentalGhostDashCooldownTicksRemaining,
            ExperimentalGhostDashVisibilityTicksRemaining,
            ExperimentalGhostDashMovementTicksRemaining,
            ExperimentalGhostDashDistanceRemaining,
            ExperimentalGhostDashSpeedPerSecondValue,
            ExperimentalGhostDashUsesMomentum,
            ExperimentalGhostDashBurstSpeedMultiplier,
            ExperimentalGhostDashDisablesGravity,
            ExperimentalGhostDashEnablesTrail,
            ExperimentalGhostDashInitialTicks,
            ExperimentalGhostDashInitialDistance,
            ExperimentalGhostDashDistanceTraveled,
            ExperimentalGhostDashLastMoveDistance,
            ExperimentalGhostDashMomentumDirectionX,
            ExperimentalGhostDashSlideVelocityPerTick,
            ExperimentalGhostDashSlideVisualSpeedPerSecond,
            ExperimentalGhostDashSlideVisualInitialSpeedPerSecond,
            ExperimentalGhostDashTrailAlphaValue,
            ExperimentalGhostDashNextAttackDamageMultiplierValue,
            LastToDieCloakedMovementSpeedMultiplierValue,
            LastToDieCloakedDamageTakenMultiplierValue,
            LastToDieRogueCommanderEnabledValue,
            LastToDieProfessionalEnabledValue,
            LastToDieSpyCloakMeterUnitsValue,
            LastToDieSpyCloakMeterMaximumUnitsValue,
            LastToDieSpyRogueRampStacksValue,
            LastToDieSpyRogueRampTicksValue,
            LastToDieMultistabEnabledValue,
            LastToDieSpringLoadedEnabledValue,
            LastToDieInstastabEnabledValue,
            LastToDieHealstabEnabledValue,
            LastToDieHealingHarnessEnabledValue,
            LastToDieDoubleJumpEnabledValue,
            SpySuperjumpAvailableCharges,
            LastToDieMedicCombatMedicEnabledValue,
            LastToDieMedicSpikedVestEnabledValue,
            LastToDieMedicIronWillEnabledValue,
            LastToDieMedicIronWillHealingRemainder,
            LastToDieMedicStimulantDripLinkActiveValue,
            LastToDieMedicAgilityDriveLinkActiveValue,
            LastToDieMedicMartyrProtectedLinkActiveValue,
            LastToDieMedicMartyrProtectorLinkActiveValue,
            LastToDieMedicKritPowerEnabledValue,
            IsDispenserBuffed,
            DispenserAttackReloadSpeedMultiplier,
            BuffBannerChargeKills,
            BuffBannerDeployTicksRemaining,
            BuffBannerActiveTicksRemaining);
    }

    internal void RestorePredictionState(in PredictionState state)
    {
        Team = state.Team;
        ClassDefinition = state.ClassDefinition;
        IsAlive = state.IsAlive;
        X = state.X;
        Y = state.Y;
        HorizontalSpeed = state.HorizontalSpeed;
        VerticalSpeed = state.VerticalSpeed;
        LegacyStateTickAccumulator = state.LegacyStateTickAccumulator;
        MovementState = state.MovementState;
        IsGrounded = state.IsGrounded;
        BlockedJumpRetrySuppressionTicksRemaining = Math.Max(0, state.BlockedJumpRetrySuppressionTicksRemaining);
        IsExperimentalDemoknightChargeDashActive = state.IsExperimentalDemoknightChargeDashActive;
        IsExperimentalDemoknightChargeFlightActive = state.IsExperimentalDemoknightChargeFlightActive;
        ExperimentalDemoknightChargeAcceleration = state.ExperimentalDemoknightChargeAcceleration;
        NetworkMaxHealthOverrideValue = state.NetworkMaxHealthOverrideValue;
        Health = int.Clamp(state.Health, 0, MaxHealth);
        Metal = state.Metal;
        IsCarryingIntel = state.IsCarryingIntel;
        IntelPickupCooldownTicks = state.IntelPickupCooldownTicks;
        IntelRechargeTicks = float.Clamp(state.IntelRechargeTicks, 0f, IntelRechargeMaxTicks);
        IsInSpawnRoom = state.IsInSpawnRoom;
        RemainingAirJumps = state.RemainingAirJumps;
        FacingDirectionX = state.FacingDirectionX;
        AimDirectionDegrees = state.AimDirectionDegrees;
        SourceFacingDirectionX = state.SourceFacingDirectionX;
        PreviousSourceFacingDirectionX = state.PreviousSourceFacingDirectionX;
        CurrentShells = state.CurrentShells;
        PrimaryCooldownTicks = state.PrimaryCooldownTicks;
        ReloadTicksUntilNextShell = state.ReloadTicksUntilNextShell;
        ExperimentalOffhandWeapon = state.ExperimentalOffhandWeapon;
        ExperimentalOffhandCurrentShells = int.Clamp(
            state.ExperimentalOffhandCurrentShells,
            0,
            state.ExperimentalOffhandWeapon?.MaxAmmo ?? 0);
        ExperimentalOffhandCooldownTicks = Math.Max(0, state.ExperimentalOffhandCooldownTicks);
        ExperimentalOffhandReloadTicksUntilNextShell = Math.Max(0, state.ExperimentalOffhandReloadTicksUntilNextShell);
        IsExperimentalOffhandEquipped = state.ExperimentalOffhandWeapon is not null && state.IsExperimentalOffhandEquipped;
        AcquiredWeaponClassId = state.AcquiredWeaponClassId;
        AcquiredWeaponCurrentShells = int.Clamp(
            state.AcquiredWeaponCurrentShells,
            0,
            AcquiredWeapon?.MaxAmmo ?? 0);
        AcquiredWeaponCooldownTicks = Math.Max(0, state.AcquiredWeaponCooldownTicks);
        AcquiredWeaponReloadTicksUntilNextShell = Math.Max(0, state.AcquiredWeaponReloadTicksUntilNextShell);
        IsAcquiredWeaponEquipped = state.AcquiredWeaponClassId.HasValue && state.IsAcquiredWeaponEquipped;
        ContinuousDamageAccumulator = state.ContinuousDamageAccumulator;
        TimeUnscathedSourceTicks = state.TimeUnscathedSourceTicks;
        MedicPassiveRegenElapsedSourceTicks = state.MedicPassiveRegenElapsedSourceTicks;
        IsHeavyEating = state.IsHeavyEating;
        HeavyEatTicksRemaining = state.HeavyEatTicksRemaining;
        HeavyEatCooldownTicksRemaining = state.HeavyEatCooldownTicksRemaining;
        HeavyEatCooldownDurationTicks = HeavyEatCooldownTicksRemaining > 0
            ? Math.Max(1, state.HeavyEatCooldownDurationTicks)
            : HeavySandvichCooldownTicks;
        HeavyHealingAccumulator = state.HeavyHealingAccumulator;
        IsTaunting = state.IsTaunting;
        TauntFrameIndex = state.TauntFrameIndex;
        IsSniperScoped = state.IsSniperScoped;
        SniperChargeTicks = Math.Clamp(
            state.SniperChargeTicks,
            0,
            LastToDieSniperRifleFullChargeTicks);
        SniperBowChargeTicks = Math.Clamp(
            state.SniperBowChargeTicks,
            0,
            LastToDieSniperBowFullChargeTicks);
        IsUsingBinoculars = state.IsUsingBinoculars;
        BinocularsFocusX = state.BinocularsFocusX;
        BinocularsFocusY = state.BinocularsFocusY;
        UberTicksRemaining = state.UberTicksRemaining;
        KritzCritBoostTicksRemaining = state.KritzCritBoostTicksRemaining;
        KritzCritBoostProviderPlayerId = state.KritzCritBoostProviderPlayerId;
        KritzCritBoostProviderSlot = state.KritzCritBoostProviderSlot;
        KritzCritBoostDamageMultiplier = state.KritzCritBoostDamageMultiplier;
        HydrateDispenserBuff(
            state.IsDispenserBuffedValue,
            state.DispenserAttackReloadSpeedMultiplierValue);
        HydrateBuffBannerState(
            state.BuffBannerChargeKillsValue,
            state.BuffBannerDeployTicksRemainingValue,
            state.BuffBannerActiveTicksRemainingValue);
        MedicHealTargetId = state.MedicHealTargetId;
        IsMedicHealing = state.IsMedicHealing;
        MedicUberCharge = state.MedicUberCharge;
        IsMedicUberReady = state.IsMedicUberReady;
        IsMedicUbering = state.IsMedicUbering;
        MedicUberDeliveryMode = state.IsMedicUbering
            ? state.MedicUberDeliveryMode
            : MedicUberDeliveryMode.None;
        MedicNeedleCooldownTicks = state.MedicNeedleCooldownTicks;
        MedicNeedleRefillTicks = state.MedicNeedleRefillTicks;
        ContinuousHealingAccumulator = state.ContinuousHealingAccumulator;
        QuoteBubbleCount = state.QuoteBubbleCount;
        QuoteBladesOut = state.QuoteBladesOut;
        CivvieUmbrellaChargeTicks = Math.Clamp(state.CivvieUmbrellaChargeTicks, 0, CivvieUmbrellaMaxChargeTicks);
        IsCivvieUmbrellaActive = state.IsCivvieUmbrellaActive;
        IsCivvieUmbrellaBroken = state.IsCivvieUmbrellaBroken;
        CivvieUmbrellaAirLiftUsed = state.CivvieUmbrellaAirLiftUsed;
        IsCivviePogoActive = state.IsCivviePogoActive;
        IsCivviePogoSuperJumpAirPhaseActive = state.IsCivviePogoSuperJumpAirPhaseActive;
        CivviePogoSuperJumpTrickUsed = state.CivviePogoSuperJumpTrickUsed;
        CivviePogoCrunchTicksRemaining = Math.Max(0, state.CivviePogoCrunchTicksRemaining);
        CivviePogoTrickTicksRemaining = Math.Max(0, state.CivviePogoTrickTicksRemaining);
        CivviePogoTrickDurationTicks = Math.Max(0, state.CivviePogoTrickDurationTicks);
        PyroAirblastCooldownTicks = state.PyroAirblastCooldownTicks;
        PyroFlareCooldownTicks = state.PyroFlareCooldownTicks;
        IsSpyCloaked = state.IsSpyCloaked;
        SpyCloakAlpha = float.Clamp(state.SpyCloakAlpha, 0f, 1f);
        IsSpySuperjumping = state.IsSpySuperjumping;
        SpySuperjumpHorizontalVelocity = state.SpySuperjumpHorizontalVelocity;
        SpySuperjumpCooldownTicksRemaining = state.SpySuperjumpCooldownTicksRemaining;
        SpySuperjumpChargeTicks = Math.Max(0, state.SpySuperjumpChargeTicks);
        SpySuperjumpChargeDirectionDegrees = state.SpySuperjumpChargeDirectionDegrees;
        SpySuperjumpChargeStartMovementButtons = state.SpySuperjumpChargeStartMovementButtons;
        SpySuperjumpChargeStartBlockedUntilAbilityRelease = state.SpySuperjumpChargeStartBlockedUntilAbilityRelease;
        ExperimentalGhostDashTicksRemaining = Math.Max(0, state.ExperimentalGhostDashTicksRemaining);
        ExperimentalGhostDashCooldownTicksRemaining = Math.Max(0, state.ExperimentalGhostDashCooldownTicksRemaining);
        ExperimentalGhostDashVisibilityTicksRemaining = Math.Max(0, state.ExperimentalGhostDashVisibilityTicksRemaining);
        ExperimentalGhostDashMovementTicksRemaining = Math.Max(0, state.ExperimentalGhostDashMovementTicksRemaining);
        ExperimentalGhostDashDistanceRemaining = MathF.Max(0f, state.ExperimentalGhostDashDistanceRemaining);
        ExperimentalGhostDashSpeedPerSecondValue = MathF.Max(0f, state.ExperimentalGhostDashSpeedPerSecondValue);
        ExperimentalGhostDashUsesMomentum = state.ExperimentalGhostDashUsesMomentum;
        ExperimentalGhostDashBurstSpeedMultiplier = MathF.Max(0f, state.ExperimentalGhostDashBurstSpeedMultiplier);
        ExperimentalGhostDashDisablesGravity = state.ExperimentalGhostDashDisablesGravity;
        ExperimentalGhostDashEnablesTrail = state.ExperimentalGhostDashEnablesTrail;
        ExperimentalGhostDashInitialTicks = Math.Max(0, state.ExperimentalGhostDashInitialTicks);
        ExperimentalGhostDashInitialDistance = MathF.Max(0f, state.ExperimentalGhostDashInitialDistance);
        ExperimentalGhostDashDistanceTraveled = MathF.Max(0f, state.ExperimentalGhostDashDistanceTraveled);
        ExperimentalGhostDashLastMoveDistance = MathF.Max(0f, state.ExperimentalGhostDashLastMoveDistance);
        ExperimentalGhostDashMomentumDirectionX = state.ExperimentalGhostDashMomentumDirectionX < 0f ? -1f : 1f;
        ExperimentalGhostDashSlideVelocityPerTick = MathF.Max(0f, state.ExperimentalGhostDashSlideVelocityPerTick);
        ExperimentalGhostDashSlideVisualSpeedPerSecond = MathF.Max(0f, state.ExperimentalGhostDashSlideVisualSpeedPerSecond);
        ExperimentalGhostDashSlideVisualInitialSpeedPerSecond = MathF.Max(0f, state.ExperimentalGhostDashSlideVisualInitialSpeedPerSecond);
        ExperimentalGhostDashTrailAlphaValue = float.Clamp(state.ExperimentalGhostDashTrailAlphaValue, 0f, 1f);
        ExperimentalGhostDashNextAttackDamageMultiplierValue = MathF.Max(1f, state.ExperimentalGhostDashNextAttackDamageMultiplierValue);
        LastToDieCloakedMovementSpeedMultiplierValue = MathF.Max(
            1f,
            state.LastToDieCloakedMovementSpeedMultiplierValue);
        LastToDieCloakedDamageTakenMultiplierValue = Math.Clamp(
            state.LastToDieCloakedDamageTakenMultiplierValue,
            0.05f,
            1f);
        LastToDieRogueCommanderEnabledValue = state.LastToDieRogueCommanderEnabledValue;
        LastToDieProfessionalEnabledValue = state.LastToDieProfessionalEnabledValue;
        LastToDieMultistabEnabledValue = state.LastToDieMultistabEnabledValue;
        LastToDieSpringLoadedEnabledValue = state.LastToDieSpringLoadedEnabledValue;
        LastToDieInstastabEnabledValue = state.LastToDieInstastabEnabledValue;
        LastToDieHealstabEnabledValue = state.LastToDieHealstabEnabledValue;
        LastToDieHealingHarnessEnabledValue = state.LastToDieHealingHarnessEnabledValue;
        LastToDieDoubleJumpEnabledValue = state.LastToDieDoubleJumpEnabledValue;
        LastToDieMedicCombatMedicEnabledValue = state.LastToDieMedicCombatMedicEnabledValue;
        LastToDieMedicSpikedVestEnabledValue = state.LastToDieMedicSpikedVestEnabledValue;
        LastToDieMedicIronWillEnabledValue = state.LastToDieMedicIronWillEnabledValue;
        LastToDieMedicIronWillHealingRemainder = Math.Clamp(
            state.LastToDieMedicIronWillHealingRemainder,
            0,
            global::OpenGarrison.Core.LastToDie.LastToDieDerivedModifiers.MedicIronWillRegenerationDenominator - 1);
        SpySuperjumpMaximumChargesValue = state.LastToDieDoubleJumpEnabledValue ? 2 : 1;
        SpySuperjumpAvailableCharges = Math.Clamp(
            state.SpySuperjumpAvailableCharges,
            0,
            SpySuperjumpMaximumCharges);
        LastToDieSpyCloakMeterMaximumUnitsValue = Math.Clamp(
            state.LastToDieSpyCloakMeterMaximumUnitsValue,
            0,
            ushort.MaxValue);
        LastToDieSpyCloakMeterUnitsValue = Math.Clamp(
            state.LastToDieSpyCloakMeterUnitsValue,
            0,
            LastToDieSpyCloakMeterMaximumUnitsValue);
        LastToDieSpyRogueRampStacksValue = Math.Clamp(
            state.LastToDieSpyRogueRampStacksValue,
            0,
            global::OpenGarrison.Core.LastToDie.LastToDieDerivedModifiers.SpyRogueMaximumRampStacks);
        LastToDieSpyRogueRampTicksValue = Math.Max(0, state.LastToDieSpyRogueRampTicksValue);
        SpyBackstabWindupTicksRemaining = state.SpyBackstabWindupTicksRemaining;
        SpyBackstabRecoveryTicksRemaining = state.SpyBackstabRecoveryTicksRemaining;
        SpyBackstabVisualTicksRemaining = state.SpyBackstabVisualTicksRemaining;
        SpyBackstabDirectionDegrees = state.SpyBackstabDirectionDegrees;
        SpyBackstabHitboxPending = state.SpyBackstabHitboxPending;
        IsSpyVisibleToEnemies = state.IsSpyVisibleToEnemies;
        BurnIntensity = float.Clamp(state.BurnIntensity, 0f, BurnMaxIntensity);
        BurnDurationSourceTicks = float.Max(0f, state.BurnDurationSourceTicks);
        BurnDecayDelaySourceTicksRemaining = float.Max(0f, state.BurnDecayDelaySourceTicksRemaining);
        BurnIntensityDecayPerSourceTick = float.Max(0f, state.BurnIntensityDecayPerSourceTick);
        BurnedByPlayerId = state.BurnedByPlayerId;
        NapalmCoveredSourceTicks = float.Max(0f, state.NapalmCoveredSourceTicks);
        Kills = state.Kills;
        Deaths = state.Deaths;
        Caps = state.Caps;
        Points = state.Points;
        HealPoints = state.HealPoints;
        ActiveDominationCount = state.ActiveDominationCount;
        IsDominatingLocalViewer = state.IsDominatingLocalViewer;
        IsDominatedByLocalViewer = state.IsDominatedByLocalViewer;
        IsChatBubbleVisible = state.IsChatBubbleVisible;
        ChatBubbleFrameIndex = state.ChatBubbleFrameIndex;
        ChatBubbleAlpha = state.ChatBubbleAlpha;
        IsChatBubbleFading = state.IsChatBubbleFading;
        ChatBubbleTicksRemaining = state.ChatBubbleTicksRemaining;
        IsTypingChatMessage = state.IsTypingChatMessage;
        SelectedGameplayLoadoutId = string.IsNullOrWhiteSpace(state.SelectedGameplayLoadoutId)
            ? CharacterClassCatalog.RuntimeRegistry.GetDefaultLoadout(GameplayClassId).Id
            : state.SelectedGameplayLoadoutId;
        SelectedGameplayPrimaryItemId = string.IsNullOrWhiteSpace(state.SelectedGameplayPrimaryItemId)
            ? CharacterClassCatalog.RuntimeRegistry.GetDefaultLoadout(GameplayClassId).Primary?.DefaultItemId
                ?? CharacterClassCatalog.RuntimeRegistry.GetDefaultLoadout(GameplayClassId).PrimaryItemId
            : state.SelectedGameplayPrimaryItemId;
        RefreshSelectedGameplayPrimaryWeapon();
        SelectedGameplayEquippedSlot = state.SelectedGameplayEquippedSlot;
        PyroPrimaryFuelScaledValue = state.PyroPrimaryFuelScaled;
        IsPyroPrimaryRefilling = state.IsPyroPrimaryRefilling;
        PyroFlameLoopTicksRemaining = state.PyroFlameLoopTicksRemaining;
        PyroPrimaryRequiresReleaseAfterEmpty = state.PyroPrimaryRequiresReleaseAfterEmpty;
        Assists = state.Assists;
        BadgeMask = BadgeCatalog.SanitizeBadgeMask(state.BadgeMask);
        LastDamageDealerPlayerId = state.LastDamageDealerPlayerId;
        LastDamageDealerAssistTicksRemaining = state.LastDamageDealerAssistTicksRemaining;
        SecondToLastDamageDealerPlayerId = state.SecondToLastDamageDealerPlayerId;
        SecondToLastDamageDealerAssistTicksRemaining = state.SecondToLastDamageDealerAssistTicksRemaining;
        ReplaceReplicatedStateEntries(state.ReplicatedStateEntries ?? []);
        LastToDieMedicStimulantDripLinkActiveValue =
            state.LastToDieMedicStimulantDripLinkActiveValue;
        LastToDieMedicAgilityDriveLinkActiveValue =
            state.LastToDieMedicAgilityDriveLinkActiveValue;
        LastToDieMedicMartyrProtectedLinkActiveValue =
            state.LastToDieMedicMartyrProtectedLinkActiveValue;
        LastToDieMedicMartyrProtectorLinkActiveValue =
            state.LastToDieMedicMartyrProtectorLinkActiveValue;
        LastToDieMedicKritPowerEnabledValue =
            state.LastToDieMedicKritPowerEnabledValue;
        CurrentShells = int.Clamp(state.CurrentShells, 0, MaxShells);
        RefreshGameplayLoadoutState();
    }

}
