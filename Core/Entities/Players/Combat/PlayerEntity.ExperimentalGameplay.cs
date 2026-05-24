using System;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public const string ServerTuningReplicatedStateOwnerId = "serverruntime";
    public const string MovementSpeedScaleReplicatedStateKey = "movescale";
    public const string GravityScaleReplicatedStateKey = "gravityscale";

    private int ExperimentalMovementBoostTicksRemaining { get; set; }

    private int ExperimentalPrimaryCooldownBuffTicksRemaining { get; set; }

    private float ExperimentalMovementSpeedMultiplierValue { get; set; } = 1f;

    private float ExperimentalPrimaryCooldownMultiplierValue { get; set; } = 1f;

    private float ServerMovementSpeedScaleValue { get; set; } = 1f;

    private float ServerDamageScaleValue { get; set; } = 1f;

    private float ServerGravityScaleValue { get; set; } = 1f;

    private float ServerHorizontalSpeedClampPerTickValue { get; set; } = LegacyMovementModel.MaxStepSpeedPerTick;

    private float ServerVerticalSpeedClampPerTickValue { get; set; } = LegacyMovementModel.MaxStepSpeedPerTick;

    private float ExperimentalPassiveMovementSpeedMultiplierValue { get; set; } = 1f;

    private float ExperimentalJumpHeightMultiplierValue { get; set; } = 1f;

    private int ExperimentalBonusAirJumpsValue { get; set; }

    private float ExperimentalDemoknightSwordRangeMultiplierValue { get; set; } = 1f;

    private int ExperimentalDemoknightSwordBaseDamageValue { get; set; } = 42;

    private float ExperimentalDemoknightSwordDamageMultiplierValue { get; set; } = 1f;

    private float ExperimentalDemoknightSwordCooldownMultiplierValue { get; set; } = 1f;

    private float ExperimentalDemoknightChargeRechargeMultiplierValue { get; set; } = 1f;

    private float ExperimentalDemoknightChargeRechargeAccumulator { get; set; }

    private bool ExperimentalDemoknightChargeWantsLift { get; set; }

    private int ExperimentalLuckyBastardTicksRemaining { get; set; }

    private int ExperimentalLuckyBastardReviveHealth { get; set; }

    private int ExperimentalFogOfWarTicksRemaining { get; set; }

    private float ExperimentalFogOfWarEvasionChanceValue { get; set; }

    private float ExperimentalShieldHealthValue { get; set; }

    private float ExperimentalMetalCapacityValue { get; set; } = 100f;

    private float ExperimentalPassiveMetalRegenerationPerTickValue { get; set; } = 0.1f;

    private float ExperimentalEngineerMetalMovementSpeedMultiplierValue { get; set; } = 1f;

    private int ExperimentalCryoSlowTicksRemaining { get; set; }

    private float ExperimentalCryoSlowMovementMultiplierValue { get; set; } = 1f;

    private int ExperimentalCryoFreezeTicksRemaining { get; set; }

    private int ExperimentalCryoHitCountValue { get; set; }

    private int ExperimentalCryoExposureTicksValue { get; set; }

    private float ExperimentalCryoExposureValue { get; set; }

    private int ExperimentalCryoExposureThresholdValue { get; set; }

    private float ExperimentalCryoExposureDecayPerTickValue { get; set; }

    private bool ExperimentalCryoExposureRefreshedThisTick { get; set; }

    private int? ExperimentalCryoSourcePlayerId { get; set; }

    private int ExperimentalDamageTakenDebuffTicksRemaining { get; set; }

    private float ExperimentalDamageTakenMultiplierValue { get; set; } = 1f;

    private int ExperimentalEssenceExtractorSlowTicksRemaining { get; set; }

    private float ExperimentalEssenceExtractorSlowMovementMultiplierValue { get; set; } = 1f;

    private int ExperimentalFreezeRayCombatDebuffTicksRemaining { get; set; }

    private float ExperimentalFreezeRayWeaponCycleMultiplierValue { get; set; } = 1f;

    private float ExperimentalFreezeRayOutgoingDamageMultiplierValue { get; set; } = 1f;

    private float ExperimentalEssenceExtractorPendingHealingValue { get; set; }

    private int? ExperimentalAdditionalMedicBeamTargetPlayerId1Value { get; set; }

    private int? ExperimentalAdditionalMedicBeamTargetPlayerId2Value { get; set; }

    private int ExperimentalConfusionTicksRemaining { get; set; }

    private int? ExperimentalConfusedAttackTargetPlayerIdValue { get; set; }

    private int ExperimentalConfusedAttackTargetTicksRemaining { get; set; }

    private int ExperimentalConfusionRetaliationTicksRemaining { get; set; }

    private float ExperimentalJumpPadAuraMovementSpeedMultiplierValue { get; set; } = 1f;

    private int ExperimentalJumpPadBurstTicksRemaining { get; set; }

    private float ExperimentalJumpPadBurstMovementSpeedMultiplierValue { get; set; } = 1f;

    private int ExperimentalJumpPadSlowTicksRemaining { get; set; }

    private float ExperimentalJumpPadSlowMovementSpeedMultiplierValue { get; set; } = 1f;

    private int ExperimentalGravitonVisualTicksRemaining { get; set; }

    private int DirectFireSlowTicksRemaining { get; set; }

    private float DirectFireSlowMovementMultiplierValue { get; set; } = 1f;

    public float ServerMovementSpeedScale => ServerMovementSpeedScaleValue;

    public float ServerGravityScale => ServerGravityScaleValue;

    public bool IsExperimentalLuckyBastardActive => ExperimentalLuckyBastardTicksRemaining > 0;

    public bool IsExperimentalFogOfWarActive => ExperimentalFogOfWarTicksRemaining > 0;

    public float ExperimentalFogOfWarEvasionChance => ExperimentalFogOfWarTicksRemaining > 0 ? ExperimentalFogOfWarEvasionChanceValue : 0f;

    public float ExperimentalShieldHealth => ExperimentalShieldHealthValue;

    public float PassiveMetalRegenerationPerTick => ClassId == PlayerClass.Engineer
        ? MathF.Max(0f, ExperimentalPassiveMetalRegenerationPerTickValue)
        : 0f;

    public bool IsExperimentalCryoSlowed => ExperimentalCryoSlowTicksRemaining > 0;

    public bool IsDirectFireSlowed => DirectFireSlowTicksRemaining > 0;

    public bool IsExperimentalCryoFrozen => ExperimentalCryoFreezeTicksRemaining > 0;

    public bool IsExperimentalEngineerEssenceExtractorPresented { get; private set; }

    public bool IsExperimentalEngineerFreezeRayPresented { get; private set; }

    public ExperimentalEngineerAlternateWeaponMode ExperimentalEngineerAlternateWeaponMode { get; private set; }

    public int ExperimentalCryoExposureTicks => ExperimentalCryoExposureTicksValue;

    public float ExperimentalCryoExposureFraction => ExperimentalCryoExposureThresholdValue > 0
        ? Math.Clamp(ExperimentalCryoExposureValue / ExperimentalCryoExposureThresholdValue, 0f, 1f)
        : 0f;

    public float ExperimentalDamageTakenMultiplier => ExperimentalDamageTakenDebuffTicksRemaining > 0
        ? ExperimentalDamageTakenMultiplierValue
        : 1f;

    public bool IsExperimentalEngineerEssenceExtractorAffected => ExperimentalDamageTakenDebuffTicksRemaining > 0;

    public bool IsExperimentalGravitonAffected => ExperimentalGravitonVisualTicksRemaining > 0;

    public float ExperimentalHealthPackHealingMultiplier => MathF.Max(0f, ExperimentalHealthPackHealingMultiplierValue);

    public float ExperimentalFreezeRayOutgoingDamageMultiplier => ExperimentalFreezeRayCombatDebuffTicksRemaining > 0
        ? ExperimentalFreezeRayOutgoingDamageMultiplierValue
        : 1f;

    public bool IsExperimentalConfused => ExperimentalConfusionTicksRemaining > 0;

    public int? ExperimentalConfusedAttackTargetPlayerId => ExperimentalConfusedAttackTargetTicksRemaining > 0
        ? ExperimentalConfusedAttackTargetPlayerIdValue
        : null;

    public bool IsExperimentalConfusionRetaliationMarked => ExperimentalConfusionRetaliationTicksRemaining > 0;

    public int? ExperimentalAdditionalMedicBeamTargetPlayerId1 => ExperimentalAdditionalMedicBeamTargetPlayerId1Value;

    public int? ExperimentalAdditionalMedicBeamTargetPlayerId2 => ExperimentalAdditionalMedicBeamTargetPlayerId2Value;

    public void SetExperimentalDemoknightEnabled(bool enabled)
    {
        var nextEnabled = enabled && ClassId == PlayerClass.Demoman;
        if (IsExperimentalDemoknightEnabled == nextEnabled)
        {
            if (!nextEnabled)
            {
                IsExperimentalDemoknightCharging = false;
                ExperimentalDemoknightChargeTicksRemaining = 0;
                ResetExperimentalDemoknightChargeMovementState();
            }

            return;
        }

        IsExperimentalDemoknightEnabled = nextEnabled;
        IsExperimentalDemoknightCharging = false;
        ExperimentalDemoknightChargeTicksRemaining = nextEnabled ? ExperimentalDemoknightChargeMaxTicks : 0;
        ExperimentalDemoknightChargeRechargeAccumulator = 0f;
        ResetExperimentalDemoknightChargeMovementState();
    }

    public bool TryFireExperimentalDemoknightSword()
    {
        if (!IsExperimentalDemoknightEnabled
            || !IsAlive
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || PrimaryCooldownTicks > 0)
        {
            return false;
        }

        var swordCooldownTicks = Math.Max(
            1,
            (int)MathF.Round(ExperimentalDemoknightSwordCooldownTicks * ExperimentalDemoknightSwordCooldownMultiplierValue));
        PrimaryCooldownTicks = ApplyExperimentalPrimaryCooldownMultiplier(swordCooldownTicks);
        return true;
    }

    public bool TryStartExperimentalDemoknightCharge()
    {
        if (!IsExperimentalDemoknightEnabled
            || !IsAlive
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || IsExperimentalDemoknightCharging
            || ExperimentalDemoknightChargeTicksRemaining < ExperimentalDemoknightChargeMaxTicks)
        {
            return false;
        }

        IsExperimentalDemoknightCharging = true;
        ExperimentalDemoknightChargeRechargeAccumulator = 0f;
        FacingDirectionX = MathF.Cos(AimDirectionDegrees * (MathF.PI / 180f)) < 0f ? -1f : 1f;
        StartExperimentalDemoknightChargeMovementState();
        return true;
    }

    public bool CancelExperimentalDemoknightCharge(bool depleteMeter)
    {
        if (!IsExperimentalDemoknightCharging)
        {
            return false;
        }

        IsExperimentalDemoknightCharging = false;
        ResetExperimentalDemoknightChargeMovementState();
        if (depleteMeter)
        {
            ExperimentalDemoknightChargeTicksRemaining = 0;
        }

        ExperimentalDemoknightChargeRechargeAccumulator = 0f;

        return true;
    }

    public void SetExperimentalPassiveMovementSpeedMultiplier(float multiplier)
    {
        ExperimentalPassiveMovementSpeedMultiplierValue = MathF.Max(1f, multiplier);
    }

    public void SetExperimentalJumpPadAuraMovementSpeedMultiplier(float multiplier)
    {
        ExperimentalJumpPadAuraMovementSpeedMultiplierValue = MathF.Max(1f, multiplier);
    }

    public void SetExperimentalJumpHeightMultiplier(float multiplier)
    {
        ExperimentalJumpHeightMultiplierValue = MathF.Max(1f, multiplier);
    }

    public void SetExperimentalBonusAirJumps(int bonusAirJumps)
    {
        var previousMaxAirJumps = MaxAirJumps;
        ExperimentalBonusAirJumpsValue = Math.Max(0, bonusAirJumps);
        var currentMaxAirJumps = MaxAirJumps;
        RemainingAirJumps = currentMaxAirJumps > previousMaxAirJumps
            ? Math.Min(currentMaxAirJumps, RemainingAirJumps + (currentMaxAirJumps - previousMaxAirJumps))
            : Math.Min(RemainingAirJumps, currentMaxAirJumps);
    }

    public void SetServerMovementSpeedScale(float multiplier)
    {
        ServerMovementSpeedScaleValue = MathF.Max(0.1f, multiplier);
    }

    public void SetServerDamageScale(float multiplier)
    {
        ServerDamageScaleValue = MathF.Max(0f, multiplier);
    }

    public void SetServerGravityScale(float multiplier)
    {
        ServerGravityScaleValue = MathF.Max(0f, multiplier);
    }

    public void SetServerMovementSpeedClamps(float horizontalClampPerTick, float verticalClampPerTick)
    {
        ServerHorizontalSpeedClampPerTickValue = MathF.Max(1f, horizontalClampPerTick);
        ServerVerticalSpeedClampPerTickValue = MathF.Max(1f, verticalClampPerTick);
    }

    public void SetExperimentalDemoknightSwordRangeMultiplier(float multiplier)
    {
        ExperimentalDemoknightSwordRangeMultiplierValue = MathF.Max(1f, multiplier);
    }

    public void SetExperimentalDemoknightSwordBaseDamage(int damage)
    {
        ExperimentalDemoknightSwordBaseDamageValue = Math.Max(1, damage);
    }

    public void SetExperimentalDemoknightSwordDamageMultiplier(float multiplier)
    {
        ExperimentalDemoknightSwordDamageMultiplierValue = MathF.Max(0.1f, multiplier);
    }

    public void SetExperimentalDemoknightSwordCooldownMultiplier(float multiplier)
    {
        ExperimentalDemoknightSwordCooldownMultiplierValue = MathF.Max(0.1f, multiplier);
    }

    public void SetExperimentalDemoknightChargeRechargeMultiplier(float multiplier)
    {
        ExperimentalDemoknightChargeRechargeMultiplierValue = MathF.Max(1f, multiplier);
        ExperimentalDemoknightChargeRechargeAccumulator = 0f;
    }

    public void SetExperimentalSoldierAmmoRegeneratesWhileSwappedOut(bool enabled)
    {
        ExperimentalSoldierAmmoRegeneratesWhileSwappedOutEnabled = enabled;
        if (!enabled)
        {
            ExperimentalSoldierSwappedOutAmmoRegenAccumulator = 0f;
        }
    }

    public void SetExperimentalSelfDamageHealing(bool enabled)
    {
        ExperimentalSelfDamageHealingEnabled = enabled;
    }

    public void SetExperimentalSoldierInfiniteAmmoDuringRage(bool enabled)
    {
        ExperimentalSoldierInfiniteAmmoDuringRageEnabled = enabled;
    }

    public void SetExperimentalReloadSpeedMultiplier(float multiplier)
    {
        ExperimentalReloadSpeedMultiplierValue = MathF.Max(0.1f, multiplier);
    }

    public void SetExperimentalShieldHealth(float shieldHealth)
    {
        ExperimentalShieldHealthValue = MathF.Max(0f, shieldHealth);
    }

    public void SetExperimentalMaxHealthBonus(int bonusHealth)
    {
        ExperimentalMaxHealthBonusValue = Math.Max(0, bonusHealth);
        Health = int.Clamp(Health, 0, MaxHealth);
    }

    public void SetExperimentalMaxHealthOverride(int? maxHealth, bool refillHealth = false)
    {
        ExperimentalMaxHealthOverrideValue = maxHealth.HasValue
            ? Math.Max(1, maxHealth.Value)
            : null;
        Health = refillHealth && IsAlive
            ? MaxHealth
            : int.Clamp(Health, 0, MaxHealth);
    }

    public void SetExperimentalHealthPackHealingMultiplier(float multiplier)
    {
        ExperimentalHealthPackHealingMultiplierValue = MathF.Max(0f, multiplier);
    }

    public void ConfigureExperimentalMetal(float maxMetal, float passiveRegenPerTick)
    {
        var previousMaxMetal = ExperimentalMetalCapacityValue;
        var hadFullMetal = previousMaxMetal > 0f && Metal >= previousMaxMetal - 0.001f;
        ExperimentalMetalCapacityValue = MathF.Max(1f, maxMetal);
        ExperimentalPassiveMetalRegenerationPerTickValue = MathF.Max(0f, passiveRegenPerTick);
        Metal = hadFullMetal && MaxMetal > previousMaxMetal
            ? MaxMetal
            : float.Clamp(Metal, 0f, MaxMetal);
    }

    public void ClearExperimentalShield()
    {
        ExperimentalShieldHealthValue = 0f;
    }

    public void SetExperimentalEngineerEssenceExtractorPresented(bool presented)
    {
        IsExperimentalEngineerEssenceExtractorPresented = presented;
    }

    public void SetExperimentalEngineerFreezeRayPresented(bool presented)
    {
        IsExperimentalEngineerFreezeRayPresented = presented;
    }

    public void SetExperimentalEngineerAlternateWeaponMode(ExperimentalEngineerAlternateWeaponMode mode)
    {
        ExperimentalEngineerAlternateWeaponMode = mode;
    }

    public void SetExperimentalEngineerMetalMovementSpeedMultiplier(float multiplier)
    {
        ExperimentalEngineerMetalMovementSpeedMultiplierValue = MathF.Max(1f, multiplier);
    }

    public void RefreshExperimentalCryoSlow(int ticks, float movementMultiplier)
    {
        if (!IsAlive || ticks <= 0)
        {
            return;
        }

        ExperimentalCryoSlowTicksRemaining = Math.Max(ExperimentalCryoSlowTicksRemaining, ticks);
        ExperimentalCryoSlowMovementMultiplierValue = Math.Clamp(
            Math.Min(ExperimentalCryoSlowMovementMultiplierValue, movementMultiplier),
            0.05f,
            1f);
    }

    public bool AccumulateExperimentalCryoHit(int ownerPlayerId, int freezeThresholdHits, int freezeDurationTicks, float slowMovementMultiplier, int slowTicks)
    {
        if (!IsAlive || ownerPlayerId <= 0 || freezeThresholdHits <= 0 || freezeDurationTicks <= 0)
        {
            return false;
        }

        if (ExperimentalCryoSourcePlayerId != ownerPlayerId)
        {
            ExperimentalCryoSourcePlayerId = ownerPlayerId;
            ExperimentalCryoHitCountValue = 0;
            ExperimentalCryoExposureTicksValue = 0;
            ExperimentalCryoExposureThresholdValue = freezeThresholdHits;
        }

        ExperimentalCryoHitCountValue = Math.Min(freezeThresholdHits, ExperimentalCryoHitCountValue + 1);
        SetExperimentalCryoExposure(ExperimentalCryoHitCountValue, freezeThresholdHits);
        ExperimentalCryoExposureDecayPerTickValue = 0f;
        ExperimentalCryoExposureRefreshedThisTick = true;
        RefreshExperimentalCryoSlow(slowTicks, slowMovementMultiplier);
        if (ExperimentalCryoHitCountValue < freezeThresholdHits)
        {
            return false;
        }

        ExperimentalCryoFreezeTicksRemaining = Math.Max(ExperimentalCryoFreezeTicksRemaining, freezeDurationTicks);
        ClearExperimentalCryoAccumulationState();
        ExperimentalCryoSlowTicksRemaining = Math.Max(ExperimentalCryoSlowTicksRemaining, freezeDurationTicks);
        ExperimentalCryoSlowMovementMultiplierValue = 0.05f;
        HorizontalSpeed = 0f;
        VerticalSpeed = 0f;
        return true;
    }

    public bool AccumulateExperimentalCryoExposure(int ownerPlayerId, int freezeThresholdTicks, int exposureWindowTicks, int freezeDurationTicks, float slowMovementMultiplier, int slowTicks)
    {
        if (!IsAlive || ownerPlayerId <= 0 || freezeThresholdTicks <= 0 || freezeDurationTicks <= 0)
        {
            return false;
        }

        if (ExperimentalCryoSourcePlayerId != ownerPlayerId)
        {
            ExperimentalCryoSourcePlayerId = ownerPlayerId;
            ExperimentalCryoHitCountValue = 0;
            ClearExperimentalCryoAccumulationState();
            ExperimentalCryoSourcePlayerId = ownerPlayerId;
        }

        SetExperimentalCryoExposure(Math.Min(freezeThresholdTicks, ExperimentalCryoExposureValue + 1f), freezeThresholdTicks);
        ExperimentalCryoExposureDecayPerTickValue = freezeThresholdTicks / (float)Math.Max(freezeThresholdTicks, exposureWindowTicks);
        ExperimentalCryoExposureRefreshedThisTick = true;
        RefreshExperimentalCryoSlow(slowTicks, slowMovementMultiplier);
        if (ExperimentalCryoExposureTicksValue < freezeThresholdTicks)
        {
            return false;
        }

        ExperimentalCryoFreezeTicksRemaining = Math.Max(ExperimentalCryoFreezeTicksRemaining, freezeDurationTicks);
        ClearExperimentalCryoAccumulationState();
        ExperimentalCryoSlowTicksRemaining = Math.Max(ExperimentalCryoSlowTicksRemaining, freezeDurationTicks);
        ExperimentalCryoSlowMovementMultiplierValue = 0.05f;
        HorizontalSpeed = 0f;
        VerticalSpeed = 0f;
        return true;
    }

    public void RefreshExperimentalDamageTakenDebuff(int ticks, float multiplier)
    {
        if (!IsAlive || ticks <= 0 || multiplier <= 1f)
        {
            return;
        }

        ExperimentalDamageTakenDebuffTicksRemaining = Math.Max(ExperimentalDamageTakenDebuffTicksRemaining, ticks);
        ExperimentalDamageTakenMultiplierValue = Math.Max(ExperimentalDamageTakenMultiplierValue, multiplier);
    }

    public void RefreshExperimentalEngineerEssenceExtractorSlow(int ticks, float movementMultiplier)
    {
        if (!IsAlive || ticks <= 0)
        {
            return;
        }

        ExperimentalEssenceExtractorSlowTicksRemaining = Math.Max(ExperimentalEssenceExtractorSlowTicksRemaining, ticks);
        ExperimentalEssenceExtractorSlowMovementMultiplierValue = Math.Clamp(
            Math.Min(ExperimentalEssenceExtractorSlowMovementMultiplierValue, movementMultiplier),
            0.05f,
            1f);
    }

    public void RefreshExperimentalFreezeRayCombatDebuff(int ticks, float weaponCycleMultiplier, float outgoingDamageMultiplier)
    {
        if (!IsAlive || ticks <= 0)
        {
            return;
        }

        ExperimentalFreezeRayCombatDebuffTicksRemaining = Math.Max(ExperimentalFreezeRayCombatDebuffTicksRemaining, ticks);
        ExperimentalFreezeRayWeaponCycleMultiplierValue = Math.Max(ExperimentalFreezeRayWeaponCycleMultiplierValue, weaponCycleMultiplier);
        ExperimentalFreezeRayOutgoingDamageMultiplierValue = Math.Clamp(
            Math.Min(ExperimentalFreezeRayOutgoingDamageMultiplierValue, outgoingDamageMultiplier),
            0.05f,
            1f);
    }

    public void GrantExperimentalJumpPadBurst(int ticks, float speedMultiplier)
    {
        if (!IsAlive || ticks <= 0 || speedMultiplier <= 1f)
        {
            return;
        }

        ExperimentalJumpPadBurstTicksRemaining = Math.Max(ExperimentalJumpPadBurstTicksRemaining, ticks);
        ExperimentalJumpPadBurstMovementSpeedMultiplierValue = Math.Max(ExperimentalJumpPadBurstMovementSpeedMultiplierValue, speedMultiplier);
    }

    public void RefreshExperimentalJumpPadSlow(int ticks, float movementMultiplier)
    {
        if (!IsAlive || ticks <= 0)
        {
            return;
        }

        ExperimentalJumpPadSlowTicksRemaining = Math.Max(ExperimentalJumpPadSlowTicksRemaining, ticks);
        ExperimentalJumpPadSlowMovementSpeedMultiplierValue = Math.Clamp(
            Math.Min(ExperimentalJumpPadSlowMovementSpeedMultiplierValue, movementMultiplier),
            0.05f,
            1f);
    }

    public void RefreshDirectFireSlow(int ticks, float movementMultiplier)
    {
        if (!IsAlive || ticks <= 0)
        {
            return;
        }

        DirectFireSlowTicksRemaining = Math.Max(DirectFireSlowTicksRemaining, ticks);
        DirectFireSlowMovementMultiplierValue = Math.Clamp(
            Math.Min(DirectFireSlowMovementMultiplierValue, movementMultiplier),
            0.05f,
            1f);
    }

    public void RefreshExperimentalGravitonEffect(int ticks)
    {
        if (!IsAlive || ticks <= 0)
        {
            return;
        }

        ExperimentalGravitonVisualTicksRemaining = Math.Max(ExperimentalGravitonVisualTicksRemaining, ticks);
    }

    public int AccumulateExperimentalEngineerEssenceExtractorHealing(float amount, float chunkSize)
    {
        if (!IsAlive || amount <= 0f || chunkSize <= 0f)
        {
            return 0;
        }

        ExperimentalEssenceExtractorPendingHealingValue = Math.Max(0f, ExperimentalEssenceExtractorPendingHealingValue + amount);
        var chunkCount = (int)MathF.Floor(ExperimentalEssenceExtractorPendingHealingValue / chunkSize);
        if (chunkCount <= 0)
        {
            return 0;
        }

        var appliedHealing = (int)MathF.Round(chunkCount * chunkSize);
        ExperimentalEssenceExtractorPendingHealingValue = Math.Max(0f, ExperimentalEssenceExtractorPendingHealingValue - appliedHealing);
        return appliedHealing;
    }

    public int FlushExperimentalEngineerEssenceExtractorHealing()
    {
        var pendingHealing = (int)MathF.Floor(ExperimentalEssenceExtractorPendingHealingValue);
        ExperimentalEssenceExtractorPendingHealingValue = 0f;
        return Math.Max(0, pendingHealing);
    }

    public void SetExperimentalAdditionalMedicBeamTargets(PlayerEntity? target1, PlayerEntity? target2)
    {
        ExperimentalAdditionalMedicBeamTargetPlayerId1Value = target1?.Id;
        ExperimentalAdditionalMedicBeamTargetPlayerId2Value = target2?.Id;
    }

    public void ClearExperimentalAdditionalMedicBeamTargets()
    {
        ExperimentalAdditionalMedicBeamTargetPlayerId1Value = null;
        ExperimentalAdditionalMedicBeamTargetPlayerId2Value = null;
    }

    public void RefreshExperimentalConfusion(int ticks)
    {
        if (!IsAlive || ticks <= 0)
        {
            return;
        }

        ExperimentalConfusionTicksRemaining = Math.Max(ExperimentalConfusionTicksRemaining, ticks);
    }

    public void SetExperimentalConfusedAttackTarget(int? targetPlayerId, int ticks)
    {
        if (!targetPlayerId.HasValue || ticks <= 0)
        {
            ExperimentalConfusedAttackTargetPlayerIdValue = null;
            ExperimentalConfusedAttackTargetTicksRemaining = 0;
            return;
        }

        ExperimentalConfusedAttackTargetPlayerIdValue = targetPlayerId.Value;
        ExperimentalConfusedAttackTargetTicksRemaining = Math.Max(ExperimentalConfusedAttackTargetTicksRemaining, ticks);
    }

    public void MarkExperimentalConfusionRetaliation(int ticks)
    {
        if (!IsAlive || ticks <= 0)
        {
            return;
        }

        ExperimentalConfusionRetaliationTicksRemaining = Math.Max(ExperimentalConfusionRetaliationTicksRemaining, ticks);
    }

    public void ClearExperimentalConfusionRetaliation()
    {
        ExperimentalConfusionRetaliationTicksRemaining = 0;
    }

    public int AbsorbExperimentalShieldDamage(int damage)
    {
        if (damage <= 0 || ExperimentalShieldHealthValue <= 0f)
        {
            return damage;
        }

        var blockedDamage = Math.Min(damage, (int)MathF.Ceiling(ExperimentalShieldHealthValue));
        ExperimentalShieldHealthValue = Math.Max(0f, ExperimentalShieldHealthValue - blockedDamage);
        return Math.Max(0, damage - blockedDamage);
    }

    public float AbsorbExperimentalShieldDamage(float damage)
    {
        if (damage <= 0f || ExperimentalShieldHealthValue <= 0f)
        {
            return damage;
        }

        var blockedDamage = MathF.Min(damage, ExperimentalShieldHealthValue);
        ExperimentalShieldHealthValue = MathF.Max(0f, ExperimentalShieldHealthValue - blockedDamage);
        return MathF.Max(0f, damage - blockedDamage);
    }

    public int GetExperimentalReloadDurationTicks(int ticks)
    {
        return ApplyExperimentalReloadMultiplier(ticks);
    }

    public bool CanConvertExperimentalSelfDamageToHealing()
    {
        return ExperimentalSelfDamageHealingEnabled && IsAlive;
    }

    public void SetExperimentalDemoknightChargeFullControlEnabled(bool enabled)
    {
        ExperimentalDemoknightChargeFullControlEnabled = enabled;
    }

    public void ConfigureExperimentalDemoknightPostRageRegeneration(float healingPerSecond)
    {
        ExperimentalDemoknightPostRageRegenPerTickValue = MathF.Max(0f, healingPerSecond) / Math.Max(1, LegacyMovementModel.SourceTicksPerSecond);
    }

    public void StartExperimentalDemoknightPostRageRegeneration(int durationTicks)
    {
        ExperimentalDemoknightPostRageRegenTicksRemaining = Math.Max(0, durationTicks);
    }

    public bool TryStartExperimentalGhostDash(
        int dashTicks,
        int cooldownTicks,
        float nextAttackDamageMultiplier,
        float dashImpulse,
        bool requireExperimentalDemoknight = true,
        bool useMomentum = false,
        int? movementTicks = null,
        float slideVelocityPerTick = ExperimentalGameplaySettings.DefaultGhostDashSlideVelocityPerTick,
        float burstSpeedMultiplier = 4f,
        bool disableGravity = true,
        bool enableGhostTrail = true)
    {
        if ((requireExperimentalDemoknight && !IsExperimentalDemoknightEnabled)
            || !IsAlive
            || IsHeavyEating
            || IsTaunting
            || IsSpyCloaked
            || IsExperimentalDemoknightCharging
            || ExperimentalGhostDashCooldownTicksRemaining > 0
            || dashTicks <= 0
            || cooldownTicks <= 0
            || nextAttackDamageMultiplier <= 1f)
        {
            return false;
        }

        var dashMovementTicks = Math.Max(1, movementTicks ?? dashTicks);
        ExperimentalGhostDashTicksRemaining = dashTicks;
        ExperimentalGhostDashVisibilityTicksRemaining = Math.Max(dashTicks, dashMovementTicks);
        ExperimentalGhostDashCooldownTicksRemaining = cooldownTicks;
        ExperimentalGhostDashMovementTicksRemaining = dashMovementTicks;
        ExperimentalGhostDashUsesMomentum = useMomentum;
        ExperimentalGhostDashBurstSpeedMultiplier = MathF.Max(0f, burstSpeedMultiplier);
        ExperimentalGhostDashDisablesGravity = disableGravity;
        ExperimentalGhostDashEnablesTrail = enableGhostTrail;
        ExperimentalGhostDashInitialTicks = dashMovementTicks;
        ExperimentalGhostDashInitialDistance = MathF.Max(0f, dashImpulse);
        ExperimentalGhostDashDistanceTraveled = 0f;
        ExperimentalGhostDashLastMoveDistance = 0f;
        ExperimentalGhostDashDistanceRemaining = MathF.Max(0f, dashImpulse);
        ExperimentalGhostDashSpeedPerSecondValue = useMomentum || dashImpulse <= 0f ? 0f : dashImpulse / 0.08f;
        ExperimentalGhostDashMomentumDirectionX = FacingDirectionX < 0f ? -1f : 1f;
        ExperimentalGhostDashSlideVelocityPerTick = MathF.Max(0f, slideVelocityPerTick);
        ExperimentalGhostDashSlideVisualSpeedPerSecond = 0f;
        ExperimentalGhostDashSlideVisualInitialSpeedPerSecond = 0f;
        ExperimentalGhostDashTrailAlphaValue = 0.12f;
        ExperimentalGhostDashNextAttackDamageMultiplierValue = nextAttackDamageMultiplier;
        return true;
    }

    public void StartExperimentalGhostPhase(int ticks)
    {
        if (!IsAlive || ticks <= 0)
        {
            return;
        }

        ExperimentalGhostDashTicksRemaining = Math.Max(ExperimentalGhostDashTicksRemaining, ticks);
        ExperimentalGhostDashVisibilityTicksRemaining = Math.Max(ExperimentalGhostDashVisibilityTicksRemaining, ticks);
    }

    public void TriggerExperimentalLuckyBastard(int invulnerabilityTicks, int reviveHealth)
    {
        if (!IsAlive || invulnerabilityTicks <= 0 || reviveHealth <= 0)
        {
            return;
        }

        ExperimentalLuckyBastardTicksRemaining = Math.Max(ExperimentalLuckyBastardTicksRemaining, invulnerabilityTicks);
        ExperimentalLuckyBastardReviveHealth = Math.Clamp(reviveHealth, 1, MaxHealth);
        Health = 1;
        RefreshUber(invulnerabilityTicks);
        ConsumeKillStreak();
    }

    public void RefreshExperimentalFogOfWar(int durationTicks, float initialChance)
    {
        if (!IsAlive || durationTicks <= 0 || initialChance <= 0f)
        {
            return;
        }

        ExperimentalFogOfWarEvasionChanceValue = ExperimentalFogOfWarTicksRemaining > 0
            ? MathF.Min(1f, ExperimentalFogOfWarEvasionChanceValue * 2f)
            : MathF.Min(1f, initialChance);
        ExperimentalFogOfWarTicksRemaining = durationTicks;
    }

    public float ConsumeExperimentalNextAttackDamageMultiplier()
    {
        var multiplier = ExperimentalGhostDashNextAttackDamageMultiplierValue;
        ExperimentalGhostDashNextAttackDamageMultiplierValue = 1f;
        return multiplier;
    }

    public float GetExperimentalDemoknightSwordRange()
    {
        return ExperimentalDemoknightSwordBaseRange * ExperimentalDemoknightSwordRangeMultiplierValue;
    }

    public int GetExperimentalDemoknightSwordDamage()
    {
        if (!IsExperimentalDemoknightEnabled)
        {
            return 0;
        }

        return Math.Max(
            1,
            (int)MathF.Round(ExperimentalDemoknightSwordBaseDamageValue * ExperimentalDemoknightSwordDamageMultiplierValue));
    }

    public void ConsumeExperimentalDemoknightChargeOnHit()
    {
        if (!IsExperimentalDemoknightCharging)
        {
            return;
        }

        IsExperimentalDemoknightCharging = false;
        ExperimentalDemoknightChargeTicksRemaining = 0;
        ExperimentalDemoknightChargeRechargeAccumulator = 0f;
        ResetExperimentalDemoknightChargeMovementState();
    }

    public void GrantExperimentalMovementBoost(int ticks, float speedMultiplier)
    {
        if (!IsAlive || ticks <= 0 || speedMultiplier <= 1f)
        {
            return;
        }

        ExperimentalMovementBoostTicksRemaining = Math.Max(ExperimentalMovementBoostTicksRemaining, ticks);
        ExperimentalMovementSpeedMultiplierValue = Math.Max(ExperimentalMovementSpeedMultiplierValue, speedMultiplier);
    }

    public void GrantExperimentalPrimaryCooldownBuff(int ticks, float cooldownMultiplier)
    {
        if (!IsAlive || ticks <= 0 || cooldownMultiplier <= 0f || cooldownMultiplier >= 1f)
        {
            return;
        }

        ExperimentalPrimaryCooldownBuffTicksRemaining = Math.Max(ExperimentalPrimaryCooldownBuffTicksRemaining, ticks);
        ExperimentalPrimaryCooldownMultiplierValue = Math.Min(ExperimentalPrimaryCooldownMultiplierValue, cooldownMultiplier);
    }

    public bool TryInstantlyRefillPrimaryAmmo(int amount = 1)
    {
        if (!IsAlive || amount <= 0)
        {
            return false;
        }

        var previousShells = CurrentShells;
        CurrentShells = int.Min(PrimaryWeapon.MaxAmmo, CurrentShells + amount);
        if (CurrentShells <= previousShells)
        {
            return false;
        }

        ResetPyroPrimaryStateFromCurrentAmmo();
        if (CurrentShells >= PrimaryWeapon.MaxAmmo)
        {
            ReloadTicksUntilNextShell = 0;
        }

        if (ClassId == PlayerClass.Pyro)
        {
            IsPyroPrimaryRefilling = false;
        }

        return true;
    }

    public bool TryRequeuePrimaryFire()
    {
        if (!IsAlive || PrimaryCooldownTicks <= 0)
        {
            return false;
        }

        PrimaryCooldownTicks = 0;
        return true;
    }

    private void AdvanceExperimentalPowerState()
    {
        if (ExperimentalCryoSlowTicksRemaining > 0)
        {
            ExperimentalCryoSlowTicksRemaining -= 1;
            if (ExperimentalCryoSlowTicksRemaining <= 0)
            {
                ExperimentalCryoSlowTicksRemaining = 0;
                ExperimentalCryoSlowMovementMultiplierValue = 1f;
                ExperimentalCryoHitCountValue = 0;
                if (ExperimentalCryoExposureDecayPerTickValue <= 0f || ExperimentalCryoExposureValue <= 0f)
                {
                    ClearExperimentalCryoAccumulationState();
                }
            }
        }

        if (ExperimentalCryoFreezeTicksRemaining > 0)
        {
            ExperimentalCryoFreezeTicksRemaining -= 1;
            HorizontalSpeed = 0f;
            VerticalSpeed = 0f;
            if (ExperimentalCryoFreezeTicksRemaining <= 0)
            {
                ExperimentalCryoFreezeTicksRemaining = 0;
            }
        }

        if (ExperimentalCryoExposureDecayPerTickValue > 0f)
        {
            if (!ExperimentalCryoExposureRefreshedThisTick
                && ExperimentalCryoFreezeTicksRemaining <= 0
                && ExperimentalCryoExposureValue > 0f)
            {
                SetExperimentalCryoExposure(
                    Math.Max(0f, ExperimentalCryoExposureValue - ExperimentalCryoExposureDecayPerTickValue),
                    ExperimentalCryoExposureThresholdValue);
                if (ExperimentalCryoExposureValue <= 0.001f && ExperimentalCryoSlowTicksRemaining <= 0)
                {
                    ClearExperimentalCryoAccumulationState();
                }
            }

            ExperimentalCryoExposureRefreshedThisTick = false;
        }
        else
        {
            ExperimentalCryoExposureRefreshedThisTick = false;
        }

        if (ExperimentalDamageTakenDebuffTicksRemaining > 0)
        {
            ExperimentalDamageTakenDebuffTicksRemaining -= 1;
            if (ExperimentalDamageTakenDebuffTicksRemaining <= 0)
            {
                ExperimentalDamageTakenDebuffTicksRemaining = 0;
                ExperimentalDamageTakenMultiplierValue = 1f;
            }
        }

        if (ExperimentalEssenceExtractorSlowTicksRemaining > 0)
        {
            ExperimentalEssenceExtractorSlowTicksRemaining -= 1;
            if (ExperimentalEssenceExtractorSlowTicksRemaining <= 0)
            {
                ExperimentalEssenceExtractorSlowTicksRemaining = 0;
                ExperimentalEssenceExtractorSlowMovementMultiplierValue = 1f;
            }
        }

        if (ExperimentalFreezeRayCombatDebuffTicksRemaining > 0)
        {
            ExperimentalFreezeRayCombatDebuffTicksRemaining -= 1;
            if (ExperimentalFreezeRayCombatDebuffTicksRemaining <= 0)
            {
                ExperimentalFreezeRayCombatDebuffTicksRemaining = 0;
                ExperimentalFreezeRayWeaponCycleMultiplierValue = 1f;
                ExperimentalFreezeRayOutgoingDamageMultiplierValue = 1f;
            }
        }

        if (ExperimentalJumpPadBurstTicksRemaining > 0)
        {
            ExperimentalJumpPadBurstTicksRemaining -= 1;
            if (ExperimentalJumpPadBurstTicksRemaining <= 0)
            {
                ExperimentalJumpPadBurstTicksRemaining = 0;
                ExperimentalJumpPadBurstMovementSpeedMultiplierValue = 1f;
            }
        }

        if (ExperimentalJumpPadSlowTicksRemaining > 0)
        {
            ExperimentalJumpPadSlowTicksRemaining -= 1;
            if (ExperimentalJumpPadSlowTicksRemaining <= 0)
            {
                ExperimentalJumpPadSlowTicksRemaining = 0;
                ExperimentalJumpPadSlowMovementSpeedMultiplierValue = 1f;
            }
        }

        if (ExperimentalGravitonVisualTicksRemaining > 0)
        {
            ExperimentalGravitonVisualTicksRemaining -= 1;
            if (ExperimentalGravitonVisualTicksRemaining <= 0)
            {
                ExperimentalGravitonVisualTicksRemaining = 0;
            }
        }

        if (DirectFireSlowTicksRemaining > 0)
        {
            DirectFireSlowTicksRemaining -= 1;
            if (DirectFireSlowTicksRemaining <= 0)
            {
                DirectFireSlowTicksRemaining = 0;
                DirectFireSlowMovementMultiplierValue = 1f;
            }
        }

        if (ExperimentalConfusionTicksRemaining > 0)
        {
            ExperimentalConfusionTicksRemaining -= 1;
            if (ExperimentalConfusionTicksRemaining <= 0)
            {
                ExperimentalConfusionTicksRemaining = 0;
                ExperimentalConfusedAttackTargetPlayerIdValue = null;
                ExperimentalConfusedAttackTargetTicksRemaining = 0;
            }
        }

        if (ExperimentalConfusedAttackTargetTicksRemaining > 0)
        {
            ExperimentalConfusedAttackTargetTicksRemaining -= 1;
            if (ExperimentalConfusedAttackTargetTicksRemaining <= 0)
            {
                ExperimentalConfusedAttackTargetTicksRemaining = 0;
                ExperimentalConfusedAttackTargetPlayerIdValue = null;
            }
        }

        if (ExperimentalConfusionRetaliationTicksRemaining > 0)
        {
            ExperimentalConfusionRetaliationTicksRemaining -= 1;
            if (ExperimentalConfusionRetaliationTicksRemaining <= 0)
            {
                ExperimentalConfusionRetaliationTicksRemaining = 0;
            }
        }

        if (!IsExperimentalDemoknightEnabled)
        {
            IsExperimentalDemoknightCharging = false;
            ExperimentalDemoknightChargeTicksRemaining = 0;
            ExperimentalDemoknightChargeRechargeAccumulator = 0f;
            ResetExperimentalDemoknightChargeMovementState();
        }
        else if (IsExperimentalDemoknightCharging)
        {
            ExperimentalDemoknightChargeTicksRemaining = Math.Max(0, ExperimentalDemoknightChargeTicksRemaining - 1);
            if (ExperimentalDemoknightChargeTicksRemaining <= 0)
            {
                ExperimentalDemoknightChargeTicksRemaining = 0;
                IsExperimentalDemoknightCharging = false;
                ExperimentalDemoknightChargeRechargeAccumulator = 0f;
                ResetExperimentalDemoknightChargeMovementState();
            }
        }
        else if (ExperimentalDemoknightChargeTicksRemaining < ExperimentalDemoknightChargeMaxTicks)
        {
            ExperimentalDemoknightChargeRechargeAccumulator += ExperimentalDemoknightChargeRechargeMultiplierValue;
            var rechargeTicks = (int)MathF.Floor(ExperimentalDemoknightChargeRechargeAccumulator);
            if (rechargeTicks > 0)
            {
                ExperimentalDemoknightChargeRechargeAccumulator -= rechargeTicks;
                ExperimentalDemoknightChargeTicksRemaining = Math.Min(
                    ExperimentalDemoknightChargeMaxTicks,
                    ExperimentalDemoknightChargeTicksRemaining + rechargeTicks);
            }
        }
        else
        {
            ExperimentalDemoknightChargeRechargeAccumulator = 0f;
        }

        if (ExperimentalDemoknightPostRageRegenTicksRemaining > 0 && !IsRaging)
        {
            ExperimentalDemoknightPostRageRegenTicksRemaining -= 1;
            ApplyContinuousHealingAndGetAmount(ExperimentalDemoknightPostRageRegenPerTickValue);
        }

        if (ExperimentalGhostDashCooldownTicksRemaining > 0)
        {
            ExperimentalGhostDashCooldownTicksRemaining -= 1;
        }

        if (ExperimentalGhostDashTicksRemaining > 0)
        {
            ExperimentalGhostDashTicksRemaining -= 1;
            if (ExperimentalGhostDashTicksRemaining <= 0)
            {
                ExperimentalGhostDashTicksRemaining = 0;
            }
        }

        if (ExperimentalGhostDashMovementTicksRemaining > 0)
        {
            ExperimentalGhostDashMovementTicksRemaining -= 1;
            if (ExperimentalGhostDashMovementTicksRemaining <= 0)
            {
                ExperimentalGhostDashMovementTicksRemaining = 0;
            }
        }

        if (ExperimentalGhostDashVisibilityTicksRemaining > 0)
        {
            ExperimentalGhostDashVisibilityTicksRemaining -= 1;
            if (ExperimentalGhostDashVisibilityTicksRemaining <= 0)
            {
                ExperimentalGhostDashVisibilityTicksRemaining = 0;
            }
        }

        AdvanceExperimentalGhostDashTrailVisualState();

        if (ExperimentalLuckyBastardTicksRemaining > 0)
        {
            ExperimentalLuckyBastardTicksRemaining -= 1;
            if (ExperimentalLuckyBastardTicksRemaining <= 0)
            {
                ExperimentalLuckyBastardTicksRemaining = 0;
                if (IsAlive && ExperimentalLuckyBastardReviveHealth > Health)
                {
                    ForceSetHealth(ExperimentalLuckyBastardReviveHealth);
                }

                ExperimentalLuckyBastardReviveHealth = 0;
            }
        }

        if (ExperimentalFogOfWarTicksRemaining > 0)
        {
            ExperimentalFogOfWarTicksRemaining -= 1;
            if (ExperimentalFogOfWarTicksRemaining <= 0)
            {
                ExperimentalFogOfWarTicksRemaining = 0;
                ExperimentalFogOfWarEvasionChanceValue = 0f;
            }
        }

        if (ExperimentalMovementBoostTicksRemaining > 0)
        {
            ExperimentalMovementBoostTicksRemaining -= 1;
            if (ExperimentalMovementBoostTicksRemaining <= 0)
            {
                ExperimentalMovementBoostTicksRemaining = 0;
                ExperimentalMovementSpeedMultiplierValue = 1f;
            }
        }

        if (ExperimentalPrimaryCooldownBuffTicksRemaining > 0)
        {
            ExperimentalPrimaryCooldownBuffTicksRemaining -= 1;
            if (ExperimentalPrimaryCooldownBuffTicksRemaining <= 0)
            {
                ExperimentalPrimaryCooldownBuffTicksRemaining = 0;
                ExperimentalPrimaryCooldownMultiplierValue = 1f;
            }
        }
    }

    private void ResetExperimentalPowerRuntimeState()
    {
        ExperimentalMovementBoostTicksRemaining = 0;
        ExperimentalPrimaryCooldownBuffTicksRemaining = 0;
        ExperimentalMovementSpeedMultiplierValue = 1f;
        ExperimentalJumpHeightMultiplierValue = 1f;
        ExperimentalBonusAirJumpsValue = 0;
        ExperimentalPrimaryCooldownMultiplierValue = 1f;
        ExperimentalSoldierSwappedOutAmmoRegenAccumulator = 0f;
        ExperimentalDemoknightPostRageRegenTicksRemaining = 0;
        ExperimentalGhostDashTicksRemaining = 0;
        ExperimentalGhostDashCooldownTicksRemaining = 0;
        ExperimentalGhostDashVisibilityTicksRemaining = 0;
        ExperimentalGhostDashMovementTicksRemaining = 0;
        ResetExperimentalGhostDashMovementState();
        ResetExperimentalGhostDashVisualState();
        ExperimentalGhostDashNextAttackDamageMultiplierValue = 1f;
        ExperimentalLuckyBastardTicksRemaining = 0;
        ExperimentalLuckyBastardReviveHealth = 0;
        ExperimentalFogOfWarTicksRemaining = 0;
        ExperimentalFogOfWarEvasionChanceValue = 0f;
        ExperimentalShieldHealthValue = 0f;
        ExperimentalMetalCapacityValue = 100f;
        ExperimentalPassiveMetalRegenerationPerTickValue = 0.1f;
        ExperimentalEngineerMetalMovementSpeedMultiplierValue = 1f;
        Metal = float.Clamp(Metal, 0f, MaxMetal);
        ExperimentalCryoSlowTicksRemaining = 0;
        ExperimentalCryoSlowMovementMultiplierValue = 1f;
        ExperimentalCryoFreezeTicksRemaining = 0;
        ExperimentalCryoHitCountValue = 0;
        ExperimentalCryoExposureTicksValue = 0;
        ExperimentalCryoExposureValue = 0f;
        ExperimentalCryoExposureThresholdValue = 0;
        ExperimentalCryoExposureDecayPerTickValue = 0f;
        ExperimentalCryoExposureRefreshedThisTick = false;
        ExperimentalCryoSourcePlayerId = null;
        ExperimentalPrimaryWeaponOverride = null;
        ExperimentalMaxHealthBonusValue = 0;
        ExperimentalHealthPackHealingMultiplierValue = 1f;
        IsExperimentalEngineerEssenceExtractorPresented = false;
        IsExperimentalEngineerFreezeRayPresented = false;
        ExperimentalEngineerAlternateWeaponMode = ExperimentalEngineerAlternateWeaponMode.None;
        ExperimentalDamageTakenDebuffTicksRemaining = 0;
        ExperimentalDamageTakenMultiplierValue = 1f;
        ExperimentalEssenceExtractorSlowTicksRemaining = 0;
        ExperimentalEssenceExtractorSlowMovementMultiplierValue = 1f;
        ExperimentalFreezeRayCombatDebuffTicksRemaining = 0;
        ExperimentalFreezeRayWeaponCycleMultiplierValue = 1f;
        ExperimentalFreezeRayOutgoingDamageMultiplierValue = 1f;
        ExperimentalEssenceExtractorPendingHealingValue = 0f;
        ExperimentalAdditionalMedicBeamTargetPlayerId1Value = null;
        ExperimentalAdditionalMedicBeamTargetPlayerId2Value = null;
        ExperimentalConfusionTicksRemaining = 0;
        ExperimentalConfusedAttackTargetPlayerIdValue = null;
        ExperimentalConfusedAttackTargetTicksRemaining = 0;
        ExperimentalConfusionRetaliationTicksRemaining = 0;
        ExperimentalJumpPadAuraMovementSpeedMultiplierValue = 1f;
        ExperimentalJumpPadBurstTicksRemaining = 0;
        ExperimentalJumpPadBurstMovementSpeedMultiplierValue = 1f;
        ExperimentalJumpPadSlowTicksRemaining = 0;
        ExperimentalJumpPadSlowMovementSpeedMultiplierValue = 1f;
        ExperimentalGravitonVisualTicksRemaining = 0;
        DirectFireSlowTicksRemaining = 0;
        DirectFireSlowMovementMultiplierValue = 1f;
        IsExperimentalDemoknightCharging = false;
        ExperimentalDemoknightChargeRechargeAccumulator = 0f;
        ExperimentalDemoknightChargeTicksRemaining = IsExperimentalDemoknightEnabled
            ? ExperimentalDemoknightChargeMaxTicks
            : 0;
        ResetExperimentalDemoknightChargeMovementState();
        ResetRageState();
    }

    private void ResetExperimentalGhostDashMovementState()
    {
        ExperimentalGhostDashDistanceRemaining = 0f;
        ExperimentalGhostDashSpeedPerSecondValue = 0f;
        ExperimentalGhostDashUsesMomentum = false;
        ExperimentalGhostDashBurstSpeedMultiplier = 0f;
        ExperimentalGhostDashDisablesGravity = false;
        ExperimentalGhostDashEnablesTrail = false;
        ExperimentalGhostDashInitialTicks = 0;
        ExperimentalGhostDashInitialDistance = 0f;
        ExperimentalGhostDashDistanceTraveled = 0f;
        ExperimentalGhostDashLastMoveDistance = 0f;
        ExperimentalGhostDashMomentumDirectionX = 1f;
        ExperimentalGhostDashSlideVelocityPerTick = ExperimentalGameplaySettings.DefaultGhostDashSlideVelocityPerTick;
    }

    private void AdvanceExperimentalGhostDashTrailVisualState()
    {
        const float trailRampPerTick = 1f / 6f;
        var dashMovementVisible = ExperimentalGhostDashTicksRemaining > 0
            || ExperimentalGhostDashMovementTicksRemaining > 0
            || ExperimentalGhostDashDistanceRemaining > 0f
            || ExperimentalGhostDashVisibilityTicksRemaining > 0;
        if (!dashMovementVisible && ExperimentalGhostDashSlideVisualSpeedPerSecond <= 0f)
        {
            ExperimentalGhostDashTrailAlphaValue = 0f;
            return;
        }

        ExperimentalGhostDashTrailAlphaValue = MathF.Min(1f, ExperimentalGhostDashTrailAlphaValue + trailRampPerTick);
        if (ExperimentalGhostDashSlideVisualSpeedPerSecond <= 0f)
        {
            return;
        }

        var nextSlideSpeed = LegacyMovementModel.AdvanceHorizontalSpeed(
            ExperimentalGhostDashSlideVisualSpeedPerSecond,
            RunPower,
            movementScale: 1f,
            hasHorizontalInput: false,
            horizontalDirection: 0f,
            state: LegacyMovementState.None,
            isCarryingIntel: false,
            deltaSeconds: 1f / LegacyMovementModel.SourceTicksPerSecond);
        ExperimentalGhostDashSlideVisualSpeedPerSecond = MathF.Max(0f, MathF.Abs(nextSlideSpeed));
        if (ExperimentalGhostDashSlideVisualSpeedPerSecond <= 0f)
        {
            ResetExperimentalGhostDashVisualState();
            return;
        }

        if (!dashMovementVisible && ExperimentalGhostDashSlideVisualInitialSpeedPerSecond > 0f)
        {
            var slideFraction = float.Clamp(
                ExperimentalGhostDashSlideVisualSpeedPerSecond / ExperimentalGhostDashSlideVisualInitialSpeedPerSecond,
                0f,
                1f);
            ExperimentalGhostDashTrailAlphaValue = MathF.Min(
                ExperimentalGhostDashTrailAlphaValue,
                MathF.Max(0.18f, slideFraction));
        }
    }

    private void ResetExperimentalGhostDashVisualState()
    {
        ExperimentalGhostDashSlideVisualSpeedPerSecond = 0f;
        ExperimentalGhostDashSlideVisualInitialSpeedPerSecond = 0f;
        ExperimentalGhostDashTrailAlphaValue = 0f;
    }

    private float GetExperimentalMovementSpeedMultiplier()
    {
        var multiplier = ServerMovementSpeedScaleValue * ExperimentalPassiveMovementSpeedMultiplierValue;
        multiplier *= ExperimentalEngineerMetalMovementSpeedMultiplierValue;

        if (ExperimentalMovementBoostTicksRemaining > 0)
        {
            multiplier *= ExperimentalMovementSpeedMultiplierValue;
        }

        multiplier *= ExperimentalJumpPadAuraMovementSpeedMultiplierValue;

        if (ExperimentalJumpPadBurstTicksRemaining > 0)
        {
            multiplier *= ExperimentalJumpPadBurstMovementSpeedMultiplierValue;
        }

        if (ExperimentalCryoSlowTicksRemaining > 0)
        {
            multiplier *= ExperimentalCryoSlowMovementMultiplierValue;
        }

        if (ExperimentalEssenceExtractorSlowTicksRemaining > 0)
        {
            multiplier *= ExperimentalEssenceExtractorSlowMovementMultiplierValue;
        }

        if (ExperimentalJumpPadSlowTicksRemaining > 0)
        {
            multiplier *= ExperimentalJumpPadSlowMovementSpeedMultiplierValue;
        }

        if (DirectFireSlowTicksRemaining > 0)
        {
            multiplier *= DirectFireSlowMovementMultiplierValue;
        }

        return multiplier;
    }

    private float GetExperimentalJumpHeightMultiplier()
    {
        return ExperimentalJumpHeightMultiplierValue;
    }

    private int GetExperimentalBonusAirJumps()
    {
        return ExperimentalBonusAirJumpsValue;
    }

    private float GetServerHorizontalSpeedClampPerTick()
    {
        return ServerHorizontalSpeedClampPerTickValue;
    }

    private float GetServerDamageScale()
    {
        return ServerDamageScaleValue;
    }

    private float GetServerScaledAirborneGravityPerTick(LegacyMovementState movementState)
    {
        var gravityScale = ServerGravityScaleValue;
        if (IsNapalmCovered)
        {
            gravityScale *= global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierNapalmGravityMultiplier;
        }

        return LegacyMovementModel.GetAirborneGravityPerTick(movementState) * gravityScale;
    }

    private float GetServerVerticalSpeedClampPerTick()
    {
        return ServerVerticalSpeedClampPerTickValue;
    }

    private void RefreshServerGameplayTuningFromReplicatedStateEntries()
    {
        var movementSpeedScale = 1f;
        if (TryGetReplicatedStateFloat(ServerTuningReplicatedStateOwnerId, MovementSpeedScaleReplicatedStateKey, out var replicatedMovementSpeedScale))
        {
            movementSpeedScale = replicatedMovementSpeedScale;
        }

        var gravityScale = 1f;
        if (TryGetReplicatedStateFloat(ServerTuningReplicatedStateOwnerId, GravityScaleReplicatedStateKey, out var replicatedGravityScale))
        {
            gravityScale = replicatedGravityScale;
        }

        SetServerMovementSpeedScale(movementSpeedScale);
        SetServerGravityScale(gravityScale);
    }

    private void StartExperimentalDemoknightChargeMovementState()
    {
        IsExperimentalDemoknightChargeDashActive = true;
        IsExperimentalDemoknightChargeFlightActive = false;
        ExperimentalDemoknightChargeAcceleration = 0f;
        ExperimentalDemoknightChargeWantsLift = false;
    }

    private void ResetExperimentalDemoknightChargeMovementState()
    {
        IsExperimentalDemoknightChargeDashActive = false;
        IsExperimentalDemoknightChargeFlightActive = false;
        ExperimentalDemoknightChargeAcceleration = 0f;
        ExperimentalDemoknightChargeWantsLift = false;
    }

    private int ApplyExperimentalPrimaryCooldownMultiplier(int cooldownTicks)
    {
        var clampedCooldown = Math.Max(1, cooldownTicks);
        if (ExperimentalPrimaryCooldownBuffTicksRemaining <= 0)
        {
            return ApplyExperimentalWeaponCycleMultiplier(clampedCooldown);
        }

        return ApplyExperimentalWeaponCycleMultiplier(
            Math.Max(1, (int)MathF.Round(clampedCooldown * ExperimentalPrimaryCooldownMultiplierValue)));
    }

    private int ApplyExperimentalReloadMultiplier(int ticks)
    {
        var clampedTicks = Math.Max(1, ticks);
        return Math.Max(1, (int)MathF.Round(clampedTicks / ExperimentalReloadSpeedMultiplierValue));
    }

    private int ApplyExperimentalWeaponCycleMultiplier(int ticks)
    {
        var adjustedTicks = ApplyExperimentalReloadMultiplier(ticks);
        if (ExperimentalFreezeRayCombatDebuffTicksRemaining > 0)
        {
            adjustedTicks = Math.Max(1, (int)MathF.Round(adjustedTicks * ExperimentalFreezeRayWeaponCycleMultiplierValue));
        }

        return adjustedTicks;
    }

    private void SetExperimentalCryoExposure(float exposure, int thresholdTicks)
    {
        ExperimentalCryoExposureThresholdValue = Math.Max(0, thresholdTicks);
        ExperimentalCryoExposureValue = Math.Clamp(exposure, 0f, Math.Max(0f, ExperimentalCryoExposureThresholdValue));
        ExperimentalCryoExposureTicksValue = (int)MathF.Round(ExperimentalCryoExposureValue);
    }

    private void ClearExperimentalCryoAccumulationState()
    {
        ExperimentalCryoHitCountValue = 0;
        ExperimentalCryoExposureTicksValue = 0;
        ExperimentalCryoExposureValue = 0f;
        ExperimentalCryoExposureThresholdValue = 0;
        ExperimentalCryoExposureDecayPerTickValue = 0f;
        ExperimentalCryoExposureRefreshedThisTick = false;
        ExperimentalCryoSourcePlayerId = null;
    }
}
