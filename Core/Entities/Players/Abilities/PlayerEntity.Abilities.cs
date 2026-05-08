using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{

    public bool TryStartTaunt()
    {
        if (!IsAlive || IsTaunting || IsHeavyEating || IsSpyCloaked || IsSpyBackstabAnimating || IsExperimentalDemoknightCharging)
        {
            return false;
        }

        IsTaunting = true;
        TauntFrameIndex = 0f;
        return true;
    }

    public bool TryStartHeavySelfHeal()
    {
        if (!IsAlive || ClassId != PlayerClass.Heavy || IsHeavyEating || IsTaunting || HeavyEatCooldownTicksRemaining > 0)
        {
            return false;
        }

        IsHeavyEating = true;
        HeavyEatTicksRemaining = HeavyEatDurationTicks;
        if (Health < MaxHealth)
        {
            HeavyEatCooldownTicksRemaining = HeavySandvichCooldownTicks;
        }

        HeavyHealingAccumulator = 0f;
        return true;
    }

    private void AdvanceTauntState()
    {
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
    }

    private void AdvanceHeavyState()
    {
        if (HeavyEatCooldownTicksRemaining > 0 && (!IsHeavyEating || Health >= MaxHealth))
        {
            HeavyEatCooldownTicksRemaining -= 1;
        }

        if (!IsHeavyEating)
        {
            return;
        }

        if (Health < MaxHealth)
        {
            HeavyEatCooldownTicksRemaining = HeavySandvichCooldownTicks;
        }

        HeavyEatTicksRemaining -= 1;
        HeavyHealingAccumulator += HeavyEatHealPerTick;
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
        IsTaunting = false;
        TauntFrameIndex = 0f;
    }

    public bool TryToggleSniperScope()
    {
        if (!IsAlive || !HasScopedSniperWeaponEquipped || IsTaunting)
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
    }

    public void ClearMedicHealingTarget()
    {
        MedicHealTargetId = null;
        IsMedicHealing = false;
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
        return true;
    }

    public void AddMedicUberCharge(float amount)
    {
        if (ClassId != PlayerClass.Medic || IsMedicUbering || amount <= 0f)
        {
            return;
        }

        MedicUberCharge = float.Min(MedicUberMaxCharge, MedicUberCharge + amount);
        if (MedicUberCharge >= MedicUberMaxCharge)
        {
            MedicUberCharge = MedicUberMaxCharge;
            IsMedicUberReady = true;
        }
    }

    public void FillMedicUberCharge()
    {
        if (ClassId != PlayerClass.Medic)
        {
            return;
        }

        MedicUberCharge = MedicUberMaxCharge;
        IsMedicUberReady = true;
    }

    public bool TryFireMedicNeedle()
    {
        if (!IsAlive
            || ClassId != PlayerClass.Medic
            || IsTaunting
            || IsMedicHealing
            || MedicNeedleCooldownTicks > 0
            || CurrentShells <= 0)
        {
            return false;
        }

        CurrentShells -= 1;
        MedicNeedleCooldownTicks = ApplyExperimentalWeaponCycleMultiplier(MedicNeedleFireCooldownTicks);
        MedicNeedleRefillTicks = ApplyExperimentalReloadMultiplier(MedicNeedleRefillTicksDefault);
        return true;
    }

    public bool TryFireAcquiredMedicNeedle()
    {
        if (!IsAlive
            || !HasAcquiredMedigunEquipped
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || MedicNeedleCooldownTicks > 0
            || AcquiredWeaponCurrentShells <= 0)
        {
            return false;
        }

        IsExperimentalOffhandEquipped = false;
        IsAcquiredWeaponEquipped = true;
        SelectedGameplayEquippedSlot = GameplayEquipmentSlot.Secondary;
        RefreshGameplayLoadoutState();
        AcquiredWeaponCurrentShells -= 1;
        MedicNeedleCooldownTicks = ApplyExperimentalWeaponCycleMultiplier(MedicNeedleFireCooldownTicks);
        MedicNeedleRefillTicks = ApplyExperimentalReloadMultiplier(MedicNeedleRefillTicksDefault);
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
        return Math.Max(0, Health - previousHealth);
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

        SpyCloakAlpha = IsSpyCloaked
            ? float.Max(0f, SpyCloakAlpha - SpyCloakFadePerTick)
            : float.Min(1f, SpyCloakAlpha + SpyCloakFadePerTick);

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

        IsSpyVisibleToEnemies = IsSpyCloaked
            && (SpyCloakAlpha > 0f || SpyBackstabVisualTicksRemaining > 0);
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
            return;
        }

        if (IsMedicUbering)
        {
            MedicUberCharge = float.Max(0f, MedicUberCharge - 10f);
            if (MedicUberCharge <= 0f)
            {
                MedicUberCharge = 0f;
                IsMedicUbering = false;
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

    public bool TryStartSpySuperjumpCharge(float aimDirectionDegrees, bool leftHeld, bool rightHeld, bool upHeld, bool downHeld)
    {
        // Can't start charging while already superjumping or already charging or on cooldown or backstabbing or carrying intel
        if (!IsAlive || ClassId != PlayerClass.Spy || IsTaunting || IsSpyBackstabAnimating || IsCarryingIntel || SpySuperjumpChargeTicks > 0 || IsSpySuperjumping || SpySuperjumpCooldownTicksRemaining > 0)
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

    public void CancelSpySuperjumpCharge()
    {
        SpySuperjumpChargeTicks = 0;
        SpySuperjumpChargeDirectionDegrees = 0f;
        SpySuperjumpChargeStartMovementButtons = 0;
    }

    public void IncrementSpySuperjumpCharge(float aimDirectionDegrees)
    {
        if (SpySuperjumpChargeTicks < SpySuperjumpMaxChargeTicks)
        {
            SpySuperjumpChargeTicks += 1;
        }
        
        // Update aim direction every frame while charging
        SpySuperjumpChargeDirectionDegrees = aimDirectionDegrees;
    }

    public bool TryReleaseSpySuperjump(out float velocityX, out float velocityY)
    {
        velocityX = 0f;
        velocityY = 0f;

        if (!IsAlive || ClassId != PlayerClass.Spy || SpySuperjumpChargeTicks <= 0)
        {
            return false;
        }

        // Calculate velocity before clearing state
        var chargeFraction = float.Min(1f, SpySuperjumpChargeTicks / (float)SpySuperjumpMaxChargeTicks);
        var velocity = SpySuperjumpMinVelocity + (SpySuperjumpMaxVelocity - SpySuperjumpMinVelocity) * chargeFraction;
        
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
        SpySuperjumpCooldownTicksRemaining = SpySuperjumpCooldownTicks; // Start cooldown
        
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
