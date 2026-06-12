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
            || HasGameplayResupplyAmmoDeficit();
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
            IsExperimentalOffhandEquipped = false;
        }
        if (AcquiredWeapon is not null)
        {
            AcquiredWeaponCurrentShells = AcquiredWeapon.MaxAmmo;
            AcquiredWeaponCooldownTicks = 0;
            AcquiredWeaponReloadTicksUntilNextShell = 0;
            IsAcquiredWeaponEquipped = false;
        }
        // Reset equipped slot to Primary if neither offhand nor acquired weapon is active.
        // EquipExperimentalOffhandWeapon sets SelectedGameplayEquippedSlot = Secondary;
        // StowExperimentalOffhandWeapon resets it, but HealAndResupply bypasses Stow.
        if (!IsExperimentalOffhandEquipped && !IsAcquiredWeaponEquipped)
        {
            SelectedGameplayEquippedSlot = GameplayEquipmentSlot.Primary;
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

        if (CivvieUmbrellaAirblastCooldownTicksRemaining > 0)
        {
            CivvieUmbrellaAirblastCooldownTicksRemaining -= 1;
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

        IsCivvieUmbrellaActive = false;
    }

    public bool TryActivateCivvieUmbrella(int maxChargeTicks = CivvieUmbrellaMaxChargeTicks)
    {
        maxChargeTicks = Math.Max(1, maxChargeTicks);
        CivvieUmbrellaChargeTicks = Math.Clamp(CivvieUmbrellaChargeTicks, 0, maxChargeTicks);
        if (!IsAlive
            || ClassId != PlayerClass.Quote
            || !HasSecondaryBehavior(BuiltInGameplayBehaviorIds.CivvieUmbrella)
            || IsCivvieUmbrellaBroken
            || CivvieUmbrellaChargeTicks <= 0)
        {
            IsCivvieUmbrellaActive = false;
            return false;
        }

        IsCivvieUmbrellaActive = true;
        return true;
    }

    public bool TryConsumeCivvieUmbrellaAirblast(int cooldownTicks)
    {
        if (!IsCivvieUmbrellaActive || CivvieUmbrellaAirblastCooldownTicksRemaining > 0)
        {
            return false;
        }

        CivvieUmbrellaAirblastCooldownTicksRemaining = Math.Max(1, cooldownTicks);
        return true;
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

    public void ResetCivvieUmbrellaState(int maxChargeTicks = CivvieUmbrellaMaxChargeTicks)
    {
        CivvieUmbrellaChargeTicks = Math.Max(1, maxChargeTicks);
        IsCivvieUmbrellaActive = false;
        IsCivvieUmbrellaBroken = false;
        CivvieUmbrellaAirblastCooldownTicksRemaining = 0;
    }

    private void BreakCivvieUmbrella()
    {
        CivvieUmbrellaChargeTicks = 0;
        IsCivvieUmbrellaActive = false;
        IsCivvieUmbrellaBroken = true;
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
