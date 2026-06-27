using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{

    public bool TryStartTaunt()
    {
        if (!IsAlive
            || IsTaunting
            || IsCivviePogoActive
            || IsHeavyEating
            || IsSpyCloaked
            || IsSpyBackstabAnimating
            || IsExperimentalDemoknightCharging
            || TauntRestartCooldownTicksRemaining > 0
            || TauntInputReleaseRequired)
        {
            return false;
        }

        IsTaunting = true;
        TauntFrameIndex = 0f;
        TauntInputReleaseRequired = true;
        return true;
    }

    public void BeginPendingCivvieTauntHeal()
    {
        if (ClassId != PlayerClass.Quote)
        {
            CivvieTauntHealPending = false;
            return;
        }

        CivvieTauntHealPending = true;
    }

    public bool ShouldTriggerCivvieTauntHeal(int healFrameIndex = CivvieTauntHealFrameIndex)
    {
        return CivvieTauntHealPending
            && IsTaunting
            && (int)MathF.Floor(TauntFrameIndex) >= Math.Max(0, healFrameIndex);
    }

    public void MarkCivvieTauntHealTriggered()
    {
        CivvieTauntHealPending = false;
    }

    public void ObserveTauntInput(bool isHeld)
    {
        if (!isHeld)
        {
            TauntInputReleaseRequired = false;
        }
    }

    private void SetHeavyEatCooldownDuration(int cooldownTicks)
    {
        HeavyEatCooldownDurationTicks = Math.Max(1, cooldownTicks);
    }

    private void SetHeavyEatCooldown(int cooldownTicks)
    {
        SetHeavyEatCooldownDuration(cooldownTicks);
        HeavyEatCooldownTicksRemaining = HeavyEatCooldownDurationTicks;
    }

    private void ClearHeavyEatCooldown()
    {
        HeavyEatCooldownTicksRemaining = 0;
        HeavyEatCooldownDurationTicks = HeavySandvichCooldownTicks;
    }

    private void ApplyObservedHeavyEatCooldown(int cooldownTicks)
    {
        if (ClassId != PlayerClass.Heavy)
        {
            ClearHeavyEatCooldown();
            return;
        }

        var observedCooldownTicks = Math.Max(0, cooldownTicks);
        if (observedCooldownTicks <= 0)
        {
            ClearHeavyEatCooldown();
            return;
        }

        var previousCooldownTicks = HeavyEatCooldownTicksRemaining;
        HeavyEatCooldownTicksRemaining = observedCooldownTicks;
        if (observedCooldownTicks > previousCooldownTicks
            || observedCooldownTicks > HeavyEatCooldownDurationTicks)
        {
            HeavyEatCooldownDurationTicks = Math.Max(HeavySandvichCooldownTicks, observedCooldownTicks);
        }
    }

    public bool TryStartHeavySelfHeal(
        int durationTicks = HeavyEatDurationTicks,
        int cooldownTicks = HeavySandvichCooldownTicks,
        float totalHeal = 200f)
    {
        if (!IsAlive || ClassId != PlayerClass.Heavy || IsHeavyEating || IsTaunting || HeavyEatCooldownTicksRemaining > 0)
        {
            return false;
        }

        durationTicks = Math.Max(1, durationTicks);
        cooldownTicks = Math.Max(1, cooldownTicks);
        SetHeavyEatCooldownDuration(cooldownTicks);
        IsHeavyEating = true;
        HeavyEatTicksRemaining = durationTicks;
        if (Health < MaxHealth)
        {
            SetHeavyEatCooldown(cooldownTicks);
        }

        HeavyEatHealPerTickValue = MathF.Max(0f, totalHeal) / durationTicks;
        HeavyHealingAccumulator = 0f;
        return true;
    }

    public bool TryCancelHeavySelfHeal(int cooldownTicks = HeavySandvichCooldownTicks * 2)
    {
        if (!IsAlive || ClassId != PlayerClass.Heavy || !IsHeavyEating)
        {
            return false;
        }

        IsHeavyEating = false;
        HeavyEatTicksRemaining = 0;
        HeavyHealingAccumulator = 0f;
        HeavyEatHealPerTickValue = HeavyEatHealPerTick;
        SetHeavyEatCooldown(cooldownTicks);
        IsTaunting = false;
        TauntFrameIndex = 0f;
        return true;
    }

    public void AdvanceTauntFrameLocally(double deltaSeconds)
    {
        var ticks = ConsumeLegacyStateTicks((float)deltaSeconds);
        for (var tick = 0; tick < ticks; tick += 1)
        {
            AdvanceTauntState();
        }
    }

    private void AdvanceTauntState()
    {
        if (TauntRestartCooldownTicksRemaining > 0)
        {
            TauntRestartCooldownTicksRemaining -= 1;
        }

        if (!IsTaunting)
        {
            return;
        }

        TauntFrameIndex += TauntFrameStepPerTick;
        var tauntEndFrame = ClassDefinition.TauntLengthFrames;
        if (TauntFrameIndex < tauntEndFrame)
        {
            return;
        }

        IsTaunting = false;
        CivvieTauntHealPending = false;
        TauntRestartCooldownTicksRemaining = TauntRestartCooldownTicks;
    }

    private void AdvanceHeavyState()
    {
        if (HeavyEatCooldownTicksRemaining > 0 && (!IsHeavyEating || Health >= MaxHealth))
        {
            HeavyEatCooldownTicksRemaining -= 1;
            if (HeavyEatCooldownTicksRemaining <= 0)
            {
                HeavyEatCooldownDurationTicks = HeavySandvichCooldownTicks;
            }
        }

        if (!IsHeavyEating)
        {
            return;
        }

        if (Health < MaxHealth)
        {
            SetHeavyEatCooldown(HeavyEatCooldownDurationTicks);
        }

        HeavyEatTicksRemaining -= 1;
        HeavyHealingAccumulator += HeavyEatHealPerTickValue;
        var wholeHealing = (int)HeavyHealingAccumulator;
        if (wholeHealing > 0)
        {
            HeavyHealingAccumulator -= wholeHealing;
            Health = int.Clamp(Health + wholeHealing, 0, MaxHealth);
        }

        if (HeavyEatTicksRemaining > 0)
        {
            return;
        }

        IsHeavyEating = false;
        HeavyEatTicksRemaining = 0;
        HeavyHealingAccumulator = 0f;
        HeavyEatHealPerTickValue = HeavyEatHealPerTick;
        IsTaunting = false;
        TauntFrameIndex = 0f;
    }

    public bool TryToggleSniperScope()
    {
        if (!IsAlive || !HasScopedSniperWeaponEquipped || IsTaunting || IsUsingBinoculars)
        {
            return false;
        }

        IsSniperScoped = !IsSniperScoped;
        if (!IsSniperScoped)
        {
            SniperChargeTicks = 0;
        }

        return true;
    }

    public bool TryToggleBinoculars()
    {
        if (!IsAlive || ClassId != PlayerClass.Sniper || IsTaunting || IsHeavyEating || IsSniperScoped)
        {
            return false;
        }

        IsUsingBinoculars = !IsUsingBinoculars;
        
        if (!IsUsingBinoculars)
        {
            // Reset focus position when exiting binoculars
            BinocularsFocusX = X;
            BinocularsFocusY = Y;
        }

        return true;
    }

    public bool TryToggleSpyCloak()
    {
        if (!IsAlive || ClassId != PlayerClass.Spy || IsCarryingIntel || IsSpyBackstabAnimating || IsTaunting)
        {
            return false;
        }

        if (IsSpyCloaked)
        {
            if (SpyCloakAlpha > SpyCloakToggleThreshold + 0.0001f)
            {
                return false;
            }
        }
        else if (SpyCloakAlpha < 0.9999f)
        {
            return false;
        }

        IsSpyCloaked = !IsSpyCloaked;
        if (!IsSpyCloaked)
        {
            IsSpyVisibleToEnemies = false;
        }

        return true;
    }

    public bool TryStartSpyBackstab(float directionDegrees)
    {
        if (!IsAlive || ClassId != PlayerClass.Spy || !IsSpyCloaked || !IsSpyBackstabReady || IsTaunting)
        {
            return false;
        }

        SpyBackstabDirectionDegrees = directionDegrees;
        SpyBackstabWindupTicksRemaining = SpyBackstabWindupTicksDefault;
        SpyBackstabRecoveryTicksRemaining = 0;
        SpyBackstabVisualTicksRemaining = SpyBackstabVisualTicksDefault;
        SpyBackstabHitboxPending = false;
        IsSpyVisibleToEnemies = true;
        return true;
    }

    public bool TryConsumeSpyBackstabHitboxTrigger(out float directionDegrees)
    {
        if (!SpyBackstabHitboxPending)
        {
            directionDegrees = 0f;
            return false;
        }

        directionDegrees = SpyBackstabDirectionDegrees;
        SpyBackstabHitboxPending = false;
        return true;
    }

    public void SetMedicHealingTarget(PlayerEntity? target)
    {
        MedicHealTargetId = target?.Id;
        IsMedicHealing = target is not null;
        if (target is null)
        {
            ClearExperimentalAdditionalMedicBeamTargets();
        }
    }

    public void ClearMedicHealingTarget()
    {
        MedicHealTargetId = null;
        IsMedicHealing = false;
        ClearExperimentalAdditionalMedicBeamTargets();
    }

    public bool IsMedicKritzUberEquipped =>
        ClassId == PlayerClass.Medic && HasEquippedBehavior(BuiltInGameplayBehaviorIds.MedigunCrit);

    public bool IsMedicMedigunSwapLocked =>
        ClassId == PlayerClass.Medic && IsMedicUbering;

    public float GetMedicUberReadyChargeThreshold() =>
        IsMedicKritzUberEquipped ? MedicKritzUberReadyChargeThreshold : MedicUberMaxCharge;

    public void GetMedicUberHudMeter(out float value, out float max)
    {
        if (ClassId != PlayerClass.Medic)
        {
            value = 0f;
            max = 1f;
            return;
        }

        if (IsMedicUbering && MedicUberUsesFixedDuration && MedicUberCommittedCharge > 0f)
        {
            value = MedicUberCharge;
            max = MedicUberCommittedCharge;
            return;
        }

        if (IsMedicKritzUberEquipped)
        {
            value = Math.Min(MedicUberCharge, MedicUberMaxCharge);
            max = MedicKritzUberReadyChargeThreshold;
            return;
        }

        value = MedicUberCharge;
        max = MedicUberMaxCharge;
    }

    public void RefreshMedicUberReadyState()
    {
        if (ClassId != PlayerClass.Medic || IsMedicUbering)
        {
            return;
        }

        var threshold = GetMedicUberReadyChargeThreshold();
        var isReady = MedicUberCharge >= threshold;
        if (isReady && !IsMedicUberReady)
        {
            MedicUberReadyPresentationPending = true;
        }

        IsMedicUberReady = isReady;
    }

    public bool TryStartMedicUber()
    {
        if (!IsAlive || ClassId != PlayerClass.Medic || !IsMedicUberReady)
        {
            return false;
        }

        // Medic carrying intel cannot activate regular uber, but can activate kritz
        var isKritz = HasEquippedBehavior(BuiltInGameplayBehaviorIds.MedigunCrit);
        if (IsCarryingIntel && !isKritz)
        {
            return false;
        }

        IsMedicUbering = true;
        IsMedicUberReady = false;
        MedicUberReadyPresentationPending = false;
        if (isKritz)
        {
            MedicUberCommittedCharge = MedicUberCharge;
            MedicUberingTotalTicks = (int)MathF.Round(MedicUberDurationSeconds * LegacyMovementModel.SourceTicksPerSecond);
            MedicUberingTicksRemaining = MedicUberingTotalTicks;
            MedicUberUsesFixedDuration = true;
        }
        else
        {
            MedicUberCommittedCharge = 0f;
            MedicUberingTicksRemaining = 0;
            MedicUberingTotalTicks = 0;
            MedicUberUsesFixedDuration = false;
        }

        return true;
    }

    public void AddMedicUberCharge(float amount)
    {
        if (ClassId != PlayerClass.Medic || IsMedicUbering || amount <= 0f)
        {
            return;
        }

        var wasReady = IsMedicUberReady;
        MedicUberCharge = float.Min(MedicUberMaxCharge, MedicUberCharge + amount);
        if (MedicUberCharge >= GetMedicUberReadyChargeThreshold())
        {
            IsMedicUberReady = true;
            MedicUberReadyPresentationPending |= !wasReady;
        }
    }

    public void FillMedicUberCharge()
    {
        if (ClassId != PlayerClass.Medic)
        {
            return;
        }

        var wasReady = IsMedicUberReady;
        MedicUberCharge = MedicUberMaxCharge;
        IsMedicUberReady = true;
        MedicUberReadyPresentationPending |= !wasReady;
    }

    public bool TryConsumeMedicUberReadyPresentation()
    {
        if (!MedicUberReadyPresentationPending)
        {
            return false;
        }

        MedicUberReadyPresentationPending = false;
        return IsAlive && ClassId == PlayerClass.Medic && IsMedicUberReady;
    }

    public bool TryFireMedicNeedle(
        int fireCooldownTicks = MedicNeedleFireCooldownTicks,
        int refillTicks = MedicNeedleRefillTicksDefault)
    {
        var ignoreAmmoCost = HasInfiniteAmmoFromUber;
        if (!IsAlive
            || ClassId != PlayerClass.Medic
            || IsTaunting
            || IsMedicHealing
            || MedicNeedleCooldownTicks > 0
            || (!ignoreAmmoCost && CurrentShells <= 0))
        {
            return false;
        }

        if (!ignoreAmmoCost)
        {
            CurrentShells -= 1;
        }

        MedicNeedleCooldownTicks = ApplyExperimentalWeaponCycleMultiplier(Math.Max(1, fireCooldownTicks));
        MedicNeedleRefillTicks = ApplyExperimentalReloadMultiplier(Math.Max(1, refillTicks));
        return true;
    }

    public bool TryFireAcquiredMedicNeedle(
        int fireCooldownTicks = MedicNeedleFireCooldownTicks,
        int refillTicks = MedicNeedleRefillTicksDefault)
    {
        var ignoreAmmoCost = HasInfiniteAmmoFromUber;
        if (!IsAlive
            || !HasAcquiredMedigunEquipped
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || MedicNeedleCooldownTicks > 0
            || (!ignoreAmmoCost && AcquiredWeaponCurrentShells <= 0))
        {
            return false;
        }

        IsExperimentalOffhandEquipped = false;
        IsAcquiredWeaponEquipped = true;
        SelectedGameplayEquippedSlot = GameplayEquipmentSlot.Secondary;
        RefreshGameplayLoadoutState();
        if (!ignoreAmmoCost)
        {
            AcquiredWeaponCurrentShells -= 1;
        }

        MedicNeedleCooldownTicks = ApplyExperimentalWeaponCycleMultiplier(Math.Max(1, fireCooldownTicks));
        MedicNeedleRefillTicks = ApplyExperimentalReloadMultiplier(Math.Max(1, refillTicks));
        return true;
    }

    public bool CanTriggerAcquiredMedigunHealsplosion()
    {
        return IsAlive
            && HasAcquiredMedigunEquipped
            && !IsHeavyEating
            && !IsTaunting
            && !IsSpyCloaked;
    }

    public void RefreshUber(int ticks = DefaultUberRefreshTicks)
    {
        if (!IsAlive)
        {
            return;
        }

        UberTicksRemaining = int.Max(UberTicksRemaining, ticks);
    }

    public void RefreshKritzCritBoost(int ticks = DefaultUberRefreshTicks)
    {
        if (!IsAlive)
        {
            return;
        }

        KritzCritBoostTicksRemaining = int.Max(KritzCritBoostTicksRemaining, ticks);
    }

    public int ApplyContinuousHealingAndGetAmount(float healing)
    {
        if (!IsAlive || healing <= 0f)
        {
            return 0;
        }

        ContinuousHealingAccumulator += healing;
        var wholeHealing = (int)ContinuousHealingAccumulator;
        if (wholeHealing <= 0)
        {
            return 0;
        }

        ContinuousHealingAccumulator -= wholeHealing;
        var previousHealth = Health;
        Health = int.Min(MaxHealth, Health + wholeHealing);
        var appliedHealing = Math.Max(0, Health - previousHealth);
        HealingReceived += appliedHealing;
        return appliedHealing;
    }

    public bool ApplyContinuousHealing(float healing)
    {
        return ApplyContinuousHealingAndGetAmount(healing) > 0;
    }

    private void AdvanceSniperState()
    {
        if (!HasScopedSniperWeaponEquipped || !IsSniperScoped || PrimaryCooldownTicks > 0)
        {
            SniperChargeTicks = 0;
            return;
        }

        if (SniperChargeTicks < SniperChargeMaxTicks)
        {
            SniperChargeTicks += 1;
        }
    }

    private void AdvanceSpyState()
    {
        if (ClassId != PlayerClass.Spy)
        {
            SpyCloakAlpha = 1f;
            IsSpyVisibleToEnemies = false;
            SpyBackstabWindupTicksRemaining = 0;
            SpyBackstabRecoveryTicksRemaining = 0;
            SpyBackstabVisualTicksRemaining = 0;
            SpyBackstabHitboxPending = false;
            return;
        }

        var baseAlpha = IsSpyCloaked
            ? float.Max(0f, SpyCloakAlpha - SpyCloakFadePerTick)
            : float.Min(1f, SpyCloakAlpha + SpyCloakFadePerTick);
        SpyCloakAlpha = baseAlpha;

        if (SpyBackstabVisualTicksRemaining > 0)
        {
            SpyBackstabVisualTicksRemaining -= 1;
        }

        if (SpyBackstabWindupTicksRemaining > 0)
        {
            SpyBackstabWindupTicksRemaining -= 1;
            if (SpyBackstabWindupTicksRemaining == 0)
            {
                SpyBackstabRecoveryTicksRemaining = SpyBackstabRecoveryTicksDefault;
                SpyBackstabHitboxPending = true;
            }
        }
        else if (SpyBackstabRecoveryTicksRemaining > 0)
        {
            SpyBackstabRecoveryTicksRemaining -= 1;
        }

        IsSpyVisibleToEnemies = ComputeSpyVisibleToEnemies(
            IsSpyCloaked,
            SpyCloakAlpha,
            SpyBackstabVisualTicksRemaining);
    }

    private void AdvancePyroAirblastState()
    {
        if (!HasPyroWeaponAvailable)
        {
            PyroAirblastCooldownTicks = 0;
            PyroFlareCooldownTicks = 0;
            return;
        }

        if (PyroAirblastCooldownTicks > 0)
        {
            PyroAirblastCooldownTicks -= 1;
        }

        if (PyroFlareCooldownTicks > 0)
        {
            PyroFlareCooldownTicks -= 1;
        }
    }

    private void AdvanceUberState()
    {
        if (UberTicksRemaining > 0)
        {
            UberTicksRemaining -= 1;
        }

        if (KritzCritBoostTicksRemaining > 0)
        {
            KritzCritBoostTicksRemaining -= 1;
        }
    }

    private void AdvanceMedicState()
    {
        if (ClassId != PlayerClass.Medic)
        {
            MedicPassiveRegenElapsedSourceTicks = 0;
            return;
        }

        AdvanceMedicPassiveRegenState();

        if (IsMedicUbering)
        {
            if (MedicUberUsesFixedDuration)
            {
                MedicUberingTicksRemaining = Math.Max(0, MedicUberingTicksRemaining - 1);
                if (MedicUberingTicksRemaining <= 0)
                {
                    MedicUberCharge = 0f;
                    IsMedicUbering = false;
                    MedicUberCommittedCharge = 0f;
                    MedicUberingTotalTicks = 0;
                    MedicUberUsesFixedDuration = false;
                }
                else
                {
                    MedicUberCharge = MedicUberCommittedCharge
                        * (MedicUberingTicksRemaining / (float)Math.Max(1, MedicUberingTotalTicks));
                }
            }
            else
            {
                MedicUberCharge = float.Max(0f, MedicUberCharge - MedicUberChargeDrainPerSourceTick);
                if (MedicUberCharge <= 0f)
                {
                    MedicUberCharge = 0f;
                    IsMedicUbering = false;
                }
            }
        }

        if (MedicNeedleCooldownTicks > 0)
        {
            MedicNeedleCooldownTicks -= 1;
        }

        if (CurrentShells >= MaxShells)
        {
            MedicNeedleRefillTicks = 0;
            return;
        }

        if (MedicNeedleRefillTicks <= 0)
        {
            MedicNeedleRefillTicks = ApplyExperimentalReloadMultiplier(MedicNeedleRefillTicksDefault);
            return;
        }

        MedicNeedleRefillTicks -= 1;
        if (MedicNeedleRefillTicks <= 0)
        {
            CurrentShells = MaxShells;
            MedicNeedleRefillTicks = 0;
        }
    }

    private void AdvanceUnscathedTime()
    {
        TimeUnscathedSourceTicks = int.Min(
            MedicPassiveRegenUnscathedCapSourceTicks,
            TimeUnscathedSourceTicks + 1);
    }

    private void ResetUnscathedTime()
    {
        TimeUnscathedSourceTicks = 0;
    }

    private void ResetPassiveRegenState()
    {
        TimeUnscathedSourceTicks = 0;
        MedicPassiveRegenElapsedSourceTicks = 0;
    }

    private void AdvanceMedicPassiveRegenState()
    {
        if (Health >= MaxHealth)
        {
            MedicPassiveRegenElapsedSourceTicks = (MedicPassiveRegenElapsedSourceTicks + 1)
                % MedicPassiveRegenIntervalSourceTicks;
            return;
        }

        MedicPassiveRegenElapsedSourceTicks += 1;
        if (MedicPassiveRegenElapsedSourceTicks < MedicPassiveRegenIntervalSourceTicks)
        {
            return;
        }

        MedicPassiveRegenElapsedSourceTicks = 0;
        var healAmount = TimeUnscathedSourceTicks < MedicPassiveRegenFirstThresholdSourceTicks
            ? 3
            : TimeUnscathedSourceTicks < MedicPassiveRegenSecondThresholdSourceTicks
                ? 4
                : 5;
        Health = int.Min(MaxHealth, Health + healAmount);
    }

    public bool TryStartSpySuperjumpCharge(float aimDirectionDegrees, bool leftHeld, bool rightHeld, bool upHeld, bool downHeld)
    {
        // Can't start charging while already superjumping or already charging or on cooldown or backstabbing or carrying intel
        if (!IsAlive || ClassId != PlayerClass.Spy || IsTaunting || IsSpyBackstabAnimating || IsCarryingIntel || SpySuperjumpChargeTicks > 0 || IsSpySuperjumping || SpySuperjumpCooldownTicksRemaining > 0 || SpySuperjumpChargeStartBlockedUntilAbilityRelease)
        {
            return false;
        }

        SpySuperjumpChargeTicks = 1;
        SpySuperjumpChargeDirectionDegrees = aimDirectionDegrees;

        // Store which movement buttons are currently held
        // These buttons will be treated as "released" for movement input purposes,
        // allowing the spy to smoothly decelerate to a stop while charging
        byte heldButtons = 0;
        if (leftHeld) heldButtons |= 0x01;
        if (rightHeld) heldButtons |= 0x02;
        if (upHeld) heldButtons |= 0x04;
        if (downHeld) heldButtons |= 0x08;
        SpySuperjumpChargeStartMovementButtons = heldButtons;

        return true;
    }

    public void CancelSpySuperjumpCharge(bool blockRestartUntilAbilityRelease = false)
    {
        SpySuperjumpChargeTicks = 0;
        SpySuperjumpChargeDirectionDegrees = 0f;
        SpySuperjumpChargeStartMovementButtons = 0;
        if (blockRestartUntilAbilityRelease)
        {
            SpySuperjumpChargeStartBlockedUntilAbilityRelease = true;
        }
    }

    public void ObserveSpySuperjumpAbilityInput(bool useAbilityHeld)
    {
        if (!useAbilityHeld)
        {
            SpySuperjumpChargeStartBlockedUntilAbilityRelease = false;
        }
    }

    public void HaltSpySuperjumpHorizontalMomentum()
    {
        if (ClassId != PlayerClass.Spy || !IsSpySuperjumping || IsGrounded)
        {
            return;
        }

        HorizontalSpeed = 0f;
        SpySuperjumpHorizontalVelocity = 0f;
    }

    public void IncrementSpySuperjumpCharge(float aimDirectionDegrees, int maxChargeTicks = SpySuperjumpMaxChargeTicks)
    {
        maxChargeTicks = Math.Max(1, maxChargeTicks);
        if (SpySuperjumpChargeTicks < maxChargeTicks)
        {
            SpySuperjumpChargeTicks += 1;
        }

        // Update aim direction every frame while charging
        SpySuperjumpChargeDirectionDegrees = aimDirectionDegrees;
    }

    public bool TryReleaseSpySuperjump(
        out float velocityX,
        out float velocityY,
        int maxChargeTicks = SpySuperjumpMaxChargeTicks,
        float minVelocity = SpySuperjumpMinVelocity,
        float maxVelocity = SpySuperjumpMaxVelocity,
        int cooldownTicks = SpySuperjumpCooldownTicks)
    {
        velocityX = 0f;
        velocityY = 0f;

        if (!IsAlive || ClassId != PlayerClass.Spy || SpySuperjumpChargeTicks <= 0)
        {
            return false;
        }

        // Calculate velocity before clearing state
        maxChargeTicks = Math.Max(1, maxChargeTicks);
        cooldownTicks = Math.Max(1, cooldownTicks);
        minVelocity = MathF.Max(0f, minVelocity);
        maxVelocity = MathF.Max(minVelocity, maxVelocity);

        var chargeFraction = float.Min(1f, SpySuperjumpChargeTicks / (float)maxChargeTicks);
        var velocity = minVelocity + (maxVelocity - minVelocity) * chargeFraction;

        var radians = SpySuperjumpChargeDirectionDegrees * (MathF.PI / 180f);
        var calculatedVelocityX = MathF.Cos(radians) * velocity;
        var calculatedVelocityY = MathF.Sin(radians) * velocity;

        // Only execute jump if grounded, but always clear charge state
        var wasGrounded = IsGrounded;

        SpySuperjumpChargeTicks = 0;
        SpySuperjumpChargeDirectionDegrees = 0f;
        SpySuperjumpChargeStartMovementButtons = 0;

        if (!wasGrounded)
        {
            return false;
        }

        velocityX = calculatedVelocityX;
        velocityY = calculatedVelocityY;
        IsSpySuperjumping = true;
        SpySuperjumpHorizontalVelocity = velocityX; // Store horizontal velocity to maintain during flight
        SpySuperjumpCooldownTicksRemaining = cooldownTicks; // Start cooldown

        return true;
    }

    private void AdvanceSpySuperjumpState()
    {
        if (ClassId != PlayerClass.Spy)
        {
            SpySuperjumpChargeTicks = 0;
            IsSpySuperjumping = false;
            SpySuperjumpHorizontalVelocity = 0f;
            SpySuperjumpCooldownTicksRemaining = 0;
            SpySuperjumpChargeStartBlockedUntilAbilityRelease = false;
            return;
        }

        // Decrement cooldown
        if (SpySuperjumpCooldownTicksRemaining > 0)
        {
            SpySuperjumpCooldownTicksRemaining -= 1;
        }

        // Clear superjumping flag and stored velocity when grounded
        if (IsSpySuperjumping && IsGrounded)
        {
            IsSpySuperjumping = false;
            SpySuperjumpHorizontalVelocity = 0f;
        }
    }
}
