using System;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{

    public bool ApplyDamage(int damage, float spyRevealAlpha = 0f)
    {
        if (!IsAlive || IsUbered || damage <= 0)
        {
            return false;
        }

        RevealSpy(spyRevealAlpha);
        Health = int.Max(0, Health - damage);
        return Health == 0;
    }

    public bool ApplyContinuousDamage(float damage, float spyRevealAlpha = 0f)
    {
        if (!IsAlive || IsUbered || damage <= 0f)
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

        SpyCloakAlpha = float.Min(1f, SpyCloakAlpha + alpha);
        IsSpyVisibleToEnemies = SpyCloakAlpha > 0f || IsSpyBackstabAnimating;
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
        HeavyEatCooldownTicksRemaining = 0;
        HeavyHealingAccumulator = 0f;
        IsTaunting = false;
        TauntFrameIndex = 0f;
        IsSniperScoped = false;
        SniperChargeTicks = 0;
        UberTicksRemaining = 0;
        MedicHealTargetId = null;
        IsMedicHealing = false;
        IsMedicUbering = false;
        MedicNeedleCooldownTicks = 0;
        MedicNeedleRefillTicks = 0;
        ContinuousHealingAccumulator = 0f;
        PyroAirblastCooldownTicks = 0;
        PyroFlareCooldownTicks = 0;
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
    }

    public void ForceSetAmmo(int shells)
    {
        CurrentShells = int.Clamp(shells, 0, PrimaryWeapon.MaxAmmo);
        ResetPyroPrimaryStateFromCurrentAmmo();
        if (CurrentShells >= PrimaryWeapon.MaxAmmo)
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

    public void HealAndResupply()
    {
        Health = MaxHealth;
        Metal = MaxMetal;
        CurrentShells = PrimaryWeapon.MaxAmmo;
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
        HeavyEatCooldownTicksRemaining = 0;
        ResetPyroPrimaryStateFromCurrentAmmo();
        ResetAcquiredPyroStateFromCurrentAmmo();
        ReloadTicksUntilNextShell = 0;
        MedicNeedleRefillTicks = 0;
        ExtinguishAfterburn();
    }

    public void AdvanceEngineerResources()
    {
        if (ClassId != PlayerClass.Engineer)
        {
            return;
        }

        if (Metal < MaxMetal)
        {
            Metal = float.Min(MaxMetal, Metal + 0.1f);
        }
    }

    public bool CanAffordSentry()
    {
        return Metal >= MaxMetal;
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
        if (ClassId == PlayerClass.Quote)
        {
            QuoteBubbleCount += 1;
        }
    }

    public void DecrementQuoteBubbleCount()
    {
        QuoteBubbleCount = int.Max(0, QuoteBubbleCount - 1);
    }

    public void IncrementQuoteBladeCount()
    {
        if (ClassId == PlayerClass.Quote)
        {
            QuoteBladesOut += 1;
        }
    }

    public void DecrementQuoteBladeCount()
    {
        QuoteBladesOut = int.Max(0, QuoteBladesOut - 1);
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
