namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{

    public bool TryFirePrimaryWeapon()
    {
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
        if (!IsAlive
            || ClassId != PlayerClass.Pyro
            || IsTaunting
            || PyroAirblastCooldownTicks > 0
            || CurrentShells < PyroAirblastCost)
        {
            return false;
        }

        CurrentShells -= PyroAirblastCost;
        PyroAirblastCooldownTicks = PyroAirblastReloadTicks;
        PrimaryCooldownTicks = int.Max(PrimaryCooldownTicks, PyroAirblastNoFlameTicks);
        ReloadTicksUntilNextShell = 0;
        return true;
    }

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
        ContinuousDamageAccumulator = 0f;
        ExtinguishAfterburn();
        IsHeavyEating = false;
        HeavyEatTicksRemaining = 0;
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
        if (CurrentShells >= PrimaryWeapon.MaxAmmo)
        {
            ReloadTicksUntilNextShell = 0;
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
    }

    public void ScoreIntel()
    {
        IsCarryingIntel = false;
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
    }

    public void AddHealPoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        HealPoints += amount;
    }

    public void AddKill()
    {
        Kills += 1;
    }

    public void AddDeath()
    {
        Deaths += 1;
    }

    public void ResetRoundStats()
    {
        Kills = 0;
        Deaths = 0;
        Caps = 0;
        HealPoints = 0;
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

    public int GetSniperRifleDamage()
    {
        if (ClassId != PlayerClass.Sniper || !IsSniperScoped)
        {
            return SniperBaseDamage;
        }

        return SniperBaseDamage + (int)MathF.Floor(MathF.Sqrt(SniperChargeTicks * 125f / 6f));
    }

    private int GetPrimaryCooldownAfterShot()
    {
        if (ClassId == PlayerClass.Sniper && IsSniperScoped)
        {
            return PrimaryWeapon.ReloadDelayTicks + SniperScopedReloadBonusTicks;
        }

        return PrimaryWeapon.ReloadDelayTicks;
    }

    private void AdvanceWeaponState()
    {
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
            return;
        }

        if (!PrimaryWeapon.AutoReloads)
        {
            ReloadTicksUntilNextShell = 0;
            return;
        }

        if (CurrentShells >= PrimaryWeapon.MaxAmmo)
        {
            ReloadTicksUntilNextShell = 0;
            return;
        }

        if (ReloadTicksUntilNextShell > 0)
        {
            ReloadTicksUntilNextShell -= 1;
            return;
        }

        if (PrimaryWeapon.RefillsAllAtOnce)
        {
            CurrentShells = PrimaryWeapon.MaxAmmo;
            ReloadTicksUntilNextShell = 0;
            return;
        }

        CurrentShells += 1;
        if (CurrentShells < PrimaryWeapon.MaxAmmo)
        {
            ReloadTicksUntilNextShell = PrimaryWeapon.AmmoReloadTicks;
        }
    }
}
