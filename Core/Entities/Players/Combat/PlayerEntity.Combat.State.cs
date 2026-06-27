using System;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{

    public bool ApplyDamage(int damage, float spyRevealAlpha = 0f)
    {
        if (!IsAlive || IsUbered || IsExperimentalGhostDashing || damage <= 0)
        {
            return false;
        }

        RevealSpy(spyRevealAlpha);
        Health = int.Max(0, Health - damage);
        ResetUnscathedTime();
        return Health == 0;
    }

    public bool ApplyContinuousDamage(float damage, float spyRevealAlpha = 0f)
    {
        if (!IsAlive || IsUbered || IsExperimentalGhostDashing || damage <= 0f)
        {
            return false;
        }

        ContinuousDamageAccumulator += damage;
        var wholeDamage = (int)ContinuousDamageAccumulator;
        if (wholeDamage <= 0)
        {
            return false;
        }

        ContinuousDamageAccumulator -= wholeDamage;
        return ApplyDamage(wholeDamage, spyRevealAlpha);
    }

    public void RevealSpy(float alpha)
    {
        if (!IsAlive || ClassId != PlayerClass.Spy || !IsSpyCloaked || alpha <= 0f)
        {
            return;
        }

        SpyCloakAlpha = float.Max(SpyCloakAlpha, alpha);
        // When a cloaked spy is damaged, keep them cloaked and let cloak alpha
        // fade back toward 0 at the normal spy cloak fade rate.
        IsSpyVisibleToEnemies = true;
    }

    public void ForceDecloak()
    {
        if (ClassId != PlayerClass.Spy)
        {
            return;
        }

        IsSpyCloaked = false;
        SpyCloakAlpha = 1f;
        IsSpyVisibleToEnemies = false;
    }

    public void ForceEndSpyStealthForHumiliation()
    {
        if (ClassId != PlayerClass.Spy)
        {
            return;
        }

        ForceDecloak();
        SpyBackstabWindupTicksRemaining = 0;
        SpyBackstabRecoveryTicksRemaining = 0;
        SpyBackstabVisualTicksRemaining = 0;
        SpyBackstabDirectionDegrees = 0f;
        SpyBackstabHitboxPending = false;
        SpySuperjumpChargeTicks = 0;
        IsSpySuperjumping = false;
        SpySuperjumpHorizontalVelocity = 0f;
    }

    public void ForceEndSniperScopeForHumiliation()
    {
        if (ClassId != PlayerClass.Sniper)
        {
            return;
        }

        IsSniperScoped = false;
        SniperChargeTicks = 0;
    }

    public void ForceSetHealth(int health)
    {
        Health = int.Clamp(health, 0, MaxHealth);
        if (Health > 0)
        {
            IsAlive = true;
            return;
        }

        IsAlive = false;
        HorizontalSpeed = 0f;
        VerticalSpeed = 0f;
        IsGrounded = false;
        IsCarryingIntel = false;
        IntelRechargeTicks = 0f;
        ContinuousDamageAccumulator = 0f;
        ResetExperimentalOffhandRuntimeState(refillAmmo: false);
        SetAcquiredWeapon(null);
        ResetExperimentalPowerRuntimeState();
        ExtinguishAfterburn();
        IsHeavyEating = false;
        HeavyEatTicksRemaining = 0;
        ClearHeavyEatCooldown();
        ResetPassiveRegenState();
        HeavyEatHealPerTickValue = HeavyEatHealPerTick;
        HeavyHealingAccumulator = 0f;
        IsTaunting = false;
        TauntFrameIndex = 0f;
        TauntRestartCooldownTicksRemaining = 0;
        TauntInputReleaseRequired = false;
        CivvieTauntHealPending = false;
        CivviePogoSuperJumpSoundPending = false;
        IsSniperScoped = false;
        SniperChargeTicks = 0;
        UberTicksRemaining = 0;
        KritzCritBoostTicksRemaining = 0;
        MedicHealTargetId = null;
        IsMedicHealing = false;
        IsMedicUbering = false;
        MedicNeedleCooldownTicks = 0;
        MedicNeedleRefillTicks = 0;
        ContinuousHealingAccumulator = 0f;
        PyroAirblastCooldownTicks = 0;
        PyroFlareCooldownTicks = 0;
        ResetCivvieUmbrellaState();
        DeactivateCivviePogo();
        IsPyroPrimaryRefilling = false;
        PyroFlameLoopTicksRemaining = 0;
        ClearRecentDamageDealers();
        IsSpyCloaked = false;
        SpyCloakAlpha = 1f;
        SpyBackstabWindupTicksRemaining = 0;
        SpyBackstabRecoveryTicksRemaining = 0;
        SpyBackstabVisualTicksRemaining = 0;
        SpyBackstabDirectionDegrees = 0f;
        IsSpyVisibleToEnemies = false;
        SpyBackstabHitboxPending = false;
        LegacyStateTickAccumulator = 0f;
        MovementState = LegacyMovementState.None;
        ClearChatBubble();
        RefreshGameplayLoadoutState();
    }

    public void ForceSetAmmo(int shells)
    {
        CurrentShells = int.Clamp(shells, 0, MaxShells);
        ResetPyroPrimaryStateFromCurrentAmmo();
        if (CurrentShells >= MaxShells)
        {
            ReloadTicksUntilNextShell = 0;
        }
        else if (ClassId == PlayerClass.Pyro)
        {
            ReloadTicksUntilNextShell = 0;
            IsPyroPrimaryRefilling = false;
        }
        else if (ReloadTicksUntilNextShell <= 0)
        {
            ReloadTicksUntilNextShell = PrimaryWeapon.AmmoReloadTicks;
        }
    }

    public bool NeedsHealingCabinetResupply()
    {
        return Health < MaxHealth
            || Metal < MaxMetal
            || CurrentShells < PrimaryWeapon.MaxAmmo
            || ReloadTicksUntilNextShell > 0
            || MedicNeedleRefillTicks > 0
            || IsBurning
            || HasGameplayResupplyCooldown()
            || HasGameplayResupplyAmmoDeficit()
            || NeedsCivvieUmbrellaResupply();
    }

    private bool HasGameplayResupplyCooldown()
    {
        return HeavyEatCooldownTicksRemaining > 0
            || ExperimentalGhostDashCooldownTicksRemaining > 0
            || SpySuperjumpCooldownTicksRemaining > 0
            || MedicNeedleCooldownTicks > 0
            || PyroAirblastCooldownTicks > 0
            || PyroFlareCooldownTicks > 0
            || ExperimentalOffhandCooldownTicks > 0
            || ExperimentalOffhandReloadTicksUntilNextShell > 0
            || AcquiredWeaponCooldownTicks > 0
            || AcquiredWeaponReloadTicksUntilNextShell > 0;
    }

    private bool HasGameplayResupplyAmmoDeficit()
    {
        return ExperimentalOffhandWeapon is not null
            && ExperimentalOffhandCurrentShells < ExperimentalOffhandWeapon.MaxAmmo
            || AcquiredWeapon is not null
            && AcquiredWeaponCurrentShells < AcquiredWeapon.MaxAmmo;
    }

    private bool NeedsCivvieUmbrellaResupply()
    {
        return HasSecondaryBehavior(BuiltInGameplayBehaviorIds.CivvieUmbrella)
            && (CivvieUmbrellaChargeTicks < CivvieUmbrellaMaxChargeTicks || IsCivvieUmbrellaBroken);
    }

    public void HealAndResupply()
    {
        Health = MaxHealth;
        Metal = MaxMetal;
        CurrentShells = MaxShells;
        if (ExperimentalOffhandWeapon is not null)
        {
            ExperimentalOffhandCurrentShells = ExperimentalOffhandWeapon.MaxAmmo;
            ExperimentalOffhandCooldownTicks = 0;
            ExperimentalOffhandReloadTicksUntilNextShell = 0;
        }
        if (AcquiredWeapon is not null)
        {
            AcquiredWeaponCurrentShells = AcquiredWeapon.MaxAmmo;
            AcquiredWeaponCooldownTicks = 0;
            AcquiredWeaponReloadTicksUntilNextShell = 0;
        }
        ClearGameplayAbilityCooldownsForResupply();
        ResetPyroPrimaryStateFromCurrentAmmo();
        ResetAcquiredPyroStateFromCurrentAmmo();
        ReloadTicksUntilNextShell = 0;
        MedicNeedleRefillTicks = 0;
        ExtinguishAfterburn();
        RefreshGameplayLoadoutState();
    }

    private void ClearGameplayAbilityCooldownsForResupply()
    {
        ClearHeavyEatCooldown();
        ExperimentalGhostDashCooldownTicksRemaining = 0;
        SpySuperjumpCooldownTicksRemaining = 0;
        MedicNeedleCooldownTicks = 0;
        PyroAirblastCooldownTicks = 0;
        PyroFlareCooldownTicks = 0;
        ResetCivvieUmbrellaState();
    }

    public void AdvanceEngineerResources()
    {
        if (ClassId != PlayerClass.Engineer)
        {
            return;
        }

        var passiveRegenPerTick = PassiveMetalRegenerationPerTick;
        if (passiveRegenPerTick > 0f && Metal < MaxMetal)
        {
            Metal = float.Min(MaxMetal, Metal + passiveRegenPerTick);
        }
    }

    public bool CanAffordSentry()
    {
        return Metal >= 100f;
    }

    public bool SpendMetal(float amount)
    {
        if (Metal < amount)
        {
            return false;
        }

        Metal -= amount;
        return true;
    }

    public void AddMetal(float amount)
    {
        Metal = float.Clamp(Metal + amount, 0f, MaxMetal);
    }

    public void PickUpIntel()
    {
        IsCarryingIntel = true;
        IntelRechargeTicks = 0f;
    }

    public void PickUpIntel(float rechargeTicks)
    {
        IsCarryingIntel = true;
        IntelRechargeTicks = float.Clamp(rechargeTicks, 0f, IntelRechargeMaxTicks);
    }

    public void ScoreIntel()
    {
        IsCarryingIntel = false;
        IntelRechargeTicks = 0f;
        Caps += 1;
    }

    public void AddCap()
    {
        Caps += 1;
    }

    public void DropIntel(int pickupCooldownTicks)
    {
        IsCarryingIntel = false;
        IntelPickupCooldownTicks = pickupCooldownTicks;
        IntelRechargeTicks = 0f;
    }

    public void IncrementQuoteBubbleCount()
    {
        QuoteBubbleCount += 1;
    }

    public void DecrementQuoteBubbleCount()
    {
        QuoteBubbleCount = int.Max(0, QuoteBubbleCount - 1);
    }

    public void IncrementQuoteBladeCount()
    {
        QuoteBladesOut += 1;
    }

    public void DecrementQuoteBladeCount()
    {
        QuoteBladesOut = int.Max(0, QuoteBladesOut - 1);
    }

    public void AdvanceCivvieUmbrellaState(
        int maxChargeTicks = CivvieUmbrellaMaxChargeTicks,
        int holdDrainPerTick = CivvieUmbrellaHoldDrainPerTick,
        int rechargePerTick = CivvieUmbrellaRechargePerTick)
    {
        maxChargeTicks = Math.Max(1, maxChargeTicks);
        holdDrainPerTick = Math.Max(0, holdDrainPerTick);
        rechargePerTick = Math.Max(0, rechargePerTick);
        CivvieUmbrellaChargeTicks = Math.Clamp(CivvieUmbrellaChargeTicks, 0, maxChargeTicks);

        if (ClassId != PlayerClass.Quote || !HasSecondaryBehavior(BuiltInGameplayBehaviorIds.CivvieUmbrella))
        {
            ResetCivvieUmbrellaState(maxChargeTicks);
            return;
        }

        if (IsCivvieUmbrellaActive)
        {
            CivvieUmbrellaChargeTicks = Math.Max(0, CivvieUmbrellaChargeTicks - holdDrainPerTick);
            if (CivvieUmbrellaChargeTicks <= 0)
            {
                BreakCivvieUmbrella();
            }
        }
        else if (CivvieUmbrellaChargeTicks < maxChargeTicks)
        {
            CivvieUmbrellaChargeTicks = Math.Min(maxChargeTicks, CivvieUmbrellaChargeTicks + rechargePerTick);
            if (IsCivvieUmbrellaBroken && CivvieUmbrellaChargeTicks >= maxChargeTicks)
            {
                IsCivvieUmbrellaBroken = false;
            }
        }
    }

    public void SyncCivvieUmbrellaSecondaryInput(bool secondaryHeld)
    {
        if (ClassId != PlayerClass.Quote || !HasSecondaryBehavior(BuiltInGameplayBehaviorIds.CivvieUmbrella))
        {
            return;
        }

        if (!secondaryHeld)
        {
            IsCivvieUmbrellaActive = false;
        }
    }

    public bool ShouldPausePrimaryReloadForCivvieUmbrella()
    {
        return ClassId == PlayerClass.Quote
            && IsCivvieUmbrellaActive
            && HasSecondaryBehavior(BuiltInGameplayBehaviorIds.CivvieUmbrella);
    }

    public bool TryActivateCivvieUmbrella(int maxChargeTicks = CivvieUmbrellaMaxChargeTicks)
    {
        maxChargeTicks = Math.Max(1, maxChargeTicks);
        CivvieUmbrellaChargeTicks = Math.Clamp(CivvieUmbrellaChargeTicks, 0, maxChargeTicks);
        if (!IsAlive
            || ClassId != PlayerClass.Quote
            || !HasSecondaryBehavior(BuiltInGameplayBehaviorIds.CivvieUmbrella)
            || IsCivviePogoActive
            || IsCivvieUmbrellaBroken
            || CivvieUmbrellaChargeTicks <= 0)
        {
            IsCivvieUmbrellaActive = false;
            return false;
        }

        var wasActive = IsCivvieUmbrellaActive;
        IsCivvieUmbrellaActive = true;
        if (!wasActive)
        {
            TryApplyCivvieUmbrellaAirLift();
        }

        return true;
    }

    private void TryApplyCivvieUmbrellaAirLift()
    {
        if (IsGrounded || CivvieUmbrellaAirLiftUsed)
        {
            return;
        }

        var liftSpeed = CivvieUmbrellaAirLiftSpeedPerTick
            * LegacyMovementModel.SourceTicksPerSecond;
        VerticalSpeed = -liftSpeed;
        CivvieUmbrellaAirLiftUsed = true;
    }

    private void ResetCivvieUmbrellaAirLift()
    {
        CivvieUmbrellaAirLiftUsed = false;
    }

    public void BeginCivvieUmbrellaOpening()
    {
        CivvieUmbrellaOpeningElapsedTicks = 0;
        CivvieUmbrellaOpeningAirblastTriggered = false;
    }

    public bool ShouldTriggerCivvieUmbrellaOpeningAirblast()
    {
        return !CivvieUmbrellaOpeningAirblastTriggered
            && CivvieUmbrellaOpeningElapsedTicks >= CivvieUmbrellaAirblastOpeningTick;
    }

    public void MarkCivvieUmbrellaOpeningAirblastTriggered()
    {
        CivvieUmbrellaOpeningAirblastTriggered = true;
    }

    public void AdvanceCivvieUmbrellaOpeningTick()
    {
        if (CivvieUmbrellaOpeningElapsedTicks < CivvieUmbrellaOpeningDurationTicks)
        {
            CivvieUmbrellaOpeningElapsedTicks += 1;
        }
    }

    private void ResetCivvieUmbrellaOpening()
    {
        CivvieUmbrellaOpeningElapsedTicks = 0;
        CivvieUmbrellaOpeningAirblastTriggered = false;
    }

    public bool TryAbsorbCivvieUmbrellaHit(int drainTicks = CivvieUmbrellaImpactDrain)
    {
        if (!IsCivvieUmbrellaActive || IsCivvieUmbrellaBroken || CivvieUmbrellaChargeTicks <= 0)
        {
            return false;
        }

        CivvieUmbrellaChargeTicks = Math.Max(0, CivvieUmbrellaChargeTicks - Math.Max(0, drainTicks));
        if (CivvieUmbrellaChargeTicks <= 0)
        {
            BreakCivvieUmbrella();
        }

        return true;
    }

    public static int GetCivvieUmbrellaSplashExplosionImpactMultiplier(float splashIntensity)
    {
        var normalized = float.Clamp(splashIntensity, 0f, 1f);
        if (normalized < (1f / 3f))
        {
            return CivvieUmbrellaMinSplashExplosionImpactMultiplier;
        }

        if (normalized < (2f / 3f))
        {
            return CivvieUmbrellaMidSplashExplosionImpactMultiplier;
        }

        return CivvieUmbrellaSplashExplosionImpactMultiplier;
    }

    public static int GetCivvieUmbrellaSplashExplosionDrainTicks(float splashIntensity)
        => CivvieUmbrellaImpactDrain * GetCivvieUmbrellaSplashExplosionImpactMultiplier(splashIntensity);

    public static float NormalizeExplosionSplashIntensity(float distanceFactor, float splashThresholdFactor)
    {
        if (distanceFactor <= splashThresholdFactor)
        {
            return 0f;
        }

        if (splashThresholdFactor >= 1f)
        {
            return 1f;
        }

        return float.Clamp(
            (distanceFactor - splashThresholdFactor) / (1f - splashThresholdFactor),
            0f,
            1f);
    }

    public static int GetCivvieUmbrellaSplashExplosionDrainTicks(float distanceFactor, float splashThresholdFactor)
        => GetCivvieUmbrellaSplashExplosionDrainTicks(
            NormalizeExplosionSplashIntensity(distanceFactor, splashThresholdFactor));

    public static int GetCivvieUmbrellaSplashExplosionDrainTicksFromDamage(float appliedDamage, float maxSplashDamage)
    {
        if (maxSplashDamage <= 0f)
        {
            return CivvieUmbrellaImpactDrain * CivvieUmbrellaMinSplashExplosionImpactMultiplier;
        }

        return GetCivvieUmbrellaSplashExplosionDrainTicks(appliedDamage / maxSplashDamage);
    }

    public static bool IsCriticalDamageMultiplierBoosted(float criticalDamageMultiplier)
        => criticalDamageMultiplier > 1.0001f;

    public static int ScaleCivvieUmbrellaDrainForCriticalBoost(int drainTicks, bool isCriticalBoosted)
        => isCriticalBoosted
            ? drainTicks * CivvieUmbrellaCriticalBoostDrainMultiplier
            : drainTicks;

    public void ResetCivvieUmbrellaState(int maxChargeTicks = CivvieUmbrellaMaxChargeTicks)
    {
        CivvieUmbrellaChargeTicks = Math.Max(1, maxChargeTicks);
        IsCivvieUmbrellaActive = false;
        IsCivvieUmbrellaBroken = false;
        ResetCivvieUmbrellaAirLift();
        ResetCivvieUmbrellaOpening();
    }

    private void BreakCivvieUmbrella()
    {
        CivvieUmbrellaChargeTicks = 0;
        IsCivvieUmbrellaActive = false;
        IsCivvieUmbrellaBroken = true;
        ResetCivvieUmbrellaOpening();
    }

    private void AdvanceIntelCarryState()
    {
        if (!IsCarryingIntel)
        {
            IntelRechargeTicks = 0f;
            return;
        }

        if (IsHeavyEating)
        {
            return;
        }

        var sourceHorizontalSpeed = MathF.Min(MathF.Abs(HorizontalSpeed) / LegacyMovementModel.SourceTicksPerSecond, 7f);
        var rechargePerTick = IntelRechargeMaxTicks / ((3f + (sourceHorizontalSpeed / 3.5f)) * LegacyMovementModel.SourceTicksPerSecond);
        IntelRechargeTicks = MathF.Min(IntelRechargeMaxTicks, IntelRechargeTicks + rechargePerTick);
    }
}
