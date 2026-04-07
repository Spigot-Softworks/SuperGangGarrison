using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity : SimulationEntity
{
    private const int MaxReplicatedStateIdentifierLength = 64;
    private const int MaxReplicatedStateEntries = 16;
    private const int MaxDisplayNameLength = 20;
    private const string DefaultDisplayName = "Player";
    public const float HeavyPrimaryMoveScale = 0.375f;
    public const int HeavyEatDurationTicks = 124;
    public const int HeavySandvichCooldownTicks = 1350;
    private const float HealingCabinetSoundCooldownSeconds = 4f;
    private const float HeavyEatHealPerTick = 0.4f;
    private const float StepUpHeight = 6f;
    private const float StepSupportEpsilon = 2f;
    public const float SniperScopedMoveScale = 2f / 3f;
    public const float SniperScopedJumpScale = 0.75f;
    public const int SniperChargeMaxTicks = 120;
    public const int SniperBaseDamage = 35;
    public const int SniperScopedReloadBonusTicks = 20;
    private const int DefaultUberRefreshTicks = 3;
    private const float MedicHealAmountPerTick = 1f;
    private const float MedicHalfHealAmountPerTick = 0.5f;
    public const float MedicUberMaxCharge = 2000f;
    public const int MedicNeedleRefillTicksDefault = 55;
    public const int MedicNeedleFireCooldownTicks = 3;
    public const int IntelRechargeMaxTicks = 900;
    public const int SpyBackstabWindupTicksDefault = 32;
    public const int SpyBackstabRecoveryTicksDefault = 18;
    public const int SpyBackstabVisualTicksDefault = 60;
    public const float SpyCloakFadePerTick = 0.05f;
    public const float SpyCloakToggleThreshold = 0.5f;
    public const float SpyMinAllyCloakAlpha = 0.5f;
    public const float SpyDamageRevealAlpha = 0.1f;
    public const float SpyMineRevealAlpha = 0.2f;
    public const float SpySniperRevealAlpha = 0.3f;
    public const int QuoteBubbleLimit = 25;
    public const int QuoteBladeEnergyCost = 15;
    public const int QuoteBladeLifetimeTicks = 15;
    public const int QuoteBladeMaxOut = 1;
    public const float ExperimentalDemoknightSwordBaseRange = 48f;
    public const int ExperimentalDemoknightSwordCooldownTicks = 18;
    public const int ExperimentalDemoknightChargeMaxTicks = 100;
    public const float ExperimentalDemoknightGroundChargeDrivePerTick = 3f;
    public const float ExperimentalDemoknightFlightChargeDrivePerTick = 1.8f;
    public const float ExperimentalDemoknightFlightChargeAccelerationDrivePerTick = 0.15f;
    public const float ExperimentalDemoknightChargeTrimpAccelerationGainPerTick = 0.35f;
    public const float ExperimentalDemoknightChargeFlightActivationAcceleration = 1.3f;
    public const float ExperimentalDemoknightChargeBounceAccelerationThreshold = 5f;
    public const int PyroAirblastCost = 40;
    public const int PyroAirblastReloadTicks = 40;
    public const int PyroAirblastNoFlameTicks = 15;
    public const int PyroFlareCost = 35;
    public const int PyroFlareReloadTicks = 55;
    public const int PyroFlareAmmoRequirement = PyroAirblastCost + PyroFlareCost;
    public const int PyroPrimaryFuelScale = 10;
    public const int PyroPrimaryFlameCostScaled = 18;
    public const int PyroPrimaryRefillScaledPerTick = 18;
    public const int PyroPrimaryRefillBufferTicks = 7;
    public const int PyroPrimaryEmptyCooldownTicks = PyroPrimaryRefillBufferTicks * 2;
    public const int PyroFlameLoopMaintainTicks = 2;
    private const float TauntFrameStepPerTick = 0.3f;
    private const int ChatBubbleHoldTicks = 60;
    private const float ChatBubbleFadePerTick = 0.05f;

    public PlayerEntity(int id, CharacterClassDefinition classDefinition, string? displayName = null) : base(id)
    {
        ClassDefinition = classDefinition;
        DisplayName = SanitizeDisplayName(displayName);
        FacingDirectionX = 1f;
        SelectedGameplayLoadoutId = CharacterClassCatalog.RuntimeRegistry.GetDefaultLoadout(classDefinition.Id).Id;
        SelectedGameplayEquippedSlot = GameplayEquipmentSlot.Primary;
        GameplayLoadoutState = CreateGameplayLoadoutState();
    }

    public float X { get; private set; }

    public float Y { get; private set; }

    public CharacterClassDefinition ClassDefinition { get; private set; }

    public PlayerClass ClassId => ClassDefinition.Id;

    public string ClassName => ClassDefinition.DisplayName;

    public string DisplayName { get; private set; }

    public GameplayPlayerLoadoutState GameplayLoadoutState { get; private set; }

    public string SelectedGameplayLoadoutId { get; private set; }

    public GameplayEquipmentSlot SelectedGameplayEquippedSlot { get; private set; }

    public float Width => ClassDefinition.Width;

    public float Height => ClassDefinition.Height;

    public float CollisionLeftOffset => ClassDefinition.CollisionLeft;

    public float CollisionTopOffset => ClassDefinition.CollisionTop;

    public float CollisionRightOffset => ClassDefinition.CollisionRight;

    public float CollisionBottomOffset => ClassDefinition.CollisionBottom;

    public float Left => X + CollisionLeftOffset;

    public float Top => Y + CollisionTopOffset;

    public float Right => X + CollisionRightOffset;

    public float Bottom => Y + CollisionBottomOffset;

    public PlayerTeam Team { get; private set; }

    public float HorizontalSpeed { get; private set; }

    public float VerticalSpeed { get; private set; }

    public bool IsGrounded { get; private set; }

    public bool IsAlive { get; private set; }

    public int Health { get; private set; }

    public int MaxHealth => ClassDefinition.MaxHealth;

    public float Metal { get; private set; } = 100f;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Kept as an instance property to preserve the public player API.")]
    public float MaxMetal => 100f;

    public bool IsCarryingIntel { get; private set; }

    public int IntelPickupCooldownTicks { get; private set; }

    public float IntelRechargeTicks { get; private set; }

    public bool IsInSpawnRoom { get; private set; }

    public bool IsUsingHealingCabinet { get; private set; }

    public float HealingCabinetSoundCooldownSecondsRemaining { get; private set; }

    public int RemainingAirJumps { get; private set; }

    public float FacingDirectionX { get; private set; }

    public float AimDirectionDegrees { get; private set; }

    public PrimaryWeaponDefinition PrimaryWeapon => ClassDefinition.PrimaryWeapon;

    public int CurrentShells { get; private set; }

    public int MaxShells => PrimaryWeapon.MaxAmmo;

    public int PrimaryCooldownTicks { get; private set; }

    public int ReloadTicksUntilNextShell { get; private set; }

    public PrimaryWeaponDefinition? ExperimentalOffhandWeapon { get; private set; }

    public bool HasExperimentalOffhandWeapon => ExperimentalOffhandWeapon is not null;

    public int ExperimentalOffhandCurrentShells { get; private set; }

    public int ExperimentalOffhandMaxShells => ExperimentalOffhandWeapon?.MaxAmmo ?? 0;

    public int ExperimentalOffhandCooldownTicks { get; private set; }

    public int ExperimentalOffhandReloadTicksUntilNextShell { get; private set; }

    public bool IsExperimentalOffhandEquipped { get; private set; }

    public bool IsExperimentalOffhandPresented => ExperimentalOffhandWeapon is not null
        && IsExperimentalOffhandEquipped;

    public PlayerClass? AcquiredWeaponClassId { get; private set; }

    public bool HasAcquiredWeapon => AcquiredWeaponClassId.HasValue;

    public PrimaryWeaponDefinition? AcquiredWeapon => AcquiredWeaponClassId.HasValue
        ? CharacterClassCatalog.GetDefinition(AcquiredWeaponClassId.Value).PrimaryWeapon
        : null;

    public int AcquiredWeaponCurrentShells { get; private set; }

    public int AcquiredWeaponMaxShells => AcquiredWeapon?.MaxAmmo ?? 0;

    public int AcquiredWeaponCooldownTicks { get; private set; }

    public int AcquiredWeaponReloadTicksUntilNextShell { get; private set; }

    public bool IsAcquiredWeaponEquipped { get; private set; }

    public bool IsAcquiredWeaponPresented => HasAcquiredWeapon
        && IsAcquiredWeaponEquipped;

    public bool HasPyroWeaponEquipped => ClassId == PlayerClass.Pyro
        || (IsAcquiredWeaponEquipped && AcquiredWeaponClassId == PlayerClass.Pyro);

    public bool HasAcquiredMedigunEquipped => IsAcquiredWeaponEquipped
        && AcquiredWeaponClassId == PlayerClass.Medic;

    public bool HasPyroWeaponAvailable => ClassId == PlayerClass.Pyro
        || AcquiredWeaponClassId == PlayerClass.Pyro;

    public bool HasScopedSniperWeaponEquipped => ClassId == PlayerClass.Sniper
        || (IsAcquiredWeaponEquipped && AcquiredWeaponClassId == PlayerClass.Sniper);

    public string PrimaryBehaviorId => GetBehaviorIdFromItem(GameplayLoadoutState.PrimaryItemId)!;

    public string? SecondaryBehaviorId => GetBehaviorIdFromItem(GameplayLoadoutState.SecondaryItemId);

    public string? UtilityBehaviorId => GetBehaviorIdFromItem(GameplayLoadoutState.UtilityItemId);

    public string? EquippedBehaviorId => GetBehaviorIdFromItem(GameplayLoadoutState.EquippedItemId);

    public string? AcquiredBehaviorId => GetBehaviorIdFromItem(GameplayLoadoutState.AcquiredItemId);

    public float ContinuousDamageAccumulator { get; private set; }

    public bool IsHeavyEating { get; private set; }

    public int HeavyEatTicksRemaining { get; private set; }

    public int HeavyEatCooldownTicksRemaining { get; private set; }

    public bool IsTaunting { get; private set; }

    public float TauntFrameIndex { get; private set; }

    public float HeavyHealingAccumulator { get; private set; }

    public bool IsSniperScoped { get; private set; }

    public int SniperChargeTicks { get; private set; }

    public bool IsUbered => UberTicksRemaining > 0;

    public int UberTicksRemaining { get; private set; }

    public float RageCharge { get; private set; }

    public bool IsRageReady { get; private set; }

    public int RageTicksRemaining { get; private set; }

    public bool IsRaging => RageTicksRemaining > 0;

    public int? MedicHealTargetId { get; private set; }

    public bool IsMedicHealing { get; private set; }

    public float MedicUberCharge { get; private set; }

    public bool IsMedicUberReady { get; private set; }

    public bool IsMedicUbering { get; private set; }

    public int MedicNeedleCooldownTicks { get; private set; }

    public int MedicNeedleRefillTicks { get; private set; }

    public float ContinuousHealingAccumulator { get; private set; }

    public int QuoteBubbleCount { get; private set; }

    public int QuoteBladesOut { get; private set; }

    public bool IsExperimentalDemoknightEnabled { get; private set; }

    public bool IsExperimentalDemoknightCharging { get; private set; }

    public int ExperimentalDemoknightChargeTicksRemaining { get; private set; }

    public float ExperimentalDemoknightChargeFraction => ExperimentalDemoknightChargeTicksRemaining / (float)ExperimentalDemoknightChargeMaxTicks;

    public bool IsExperimentalDemoknightChargeDashActive { get; private set; }

    public bool IsExperimentalDemoknightChargeFlightActive { get; private set; }

    public float ExperimentalDemoknightChargeAcceleration { get; private set; }

    public int PyroAirblastCooldownTicks { get; private set; }

    public int PyroFlareCooldownTicks { get; private set; }

    public bool IsPyroPrimaryRefilling { get; private set; }

    public int PyroFlameLoopTicksRemaining { get; private set; }

    public bool PyroPrimaryRequiresReleaseAfterEmpty { get; private set; }

    public int PyroPrimaryFuelScaled => HasPyroWeaponAvailable
        ? PyroPrimaryFuelScaledValue
        : CurrentShells * PyroPrimaryFuelScale;

    public bool IsSpyCloaked { get; private set; }

    public float SpyCloakAlpha { get; private set; } = 1f;

    public bool IsSpyBackstabReady => SpyBackstabWindupTicksRemaining <= 0 && SpyBackstabRecoveryTicksRemaining <= 0;

    public bool IsSpyBackstabAnimating => SpyBackstabVisualTicksRemaining > 0;

    public int SpyBackstabWindupTicksRemaining { get; private set; }

    public int SpyBackstabRecoveryTicksRemaining { get; private set; }

    public int SpyBackstabVisualTicksRemaining { get; private set; }

    public float SpyBackstabDirectionDegrees { get; private set; }

    public bool IsSpyVisibleToEnemies { get; private set; }

    public bool IsSpyVisibleToAllies => !IsSpyCloaked || IsSpyBackstabReady || SpyCloakAlpha > 0f;

    public int Kills { get; private set; }

    public int Deaths { get; private set; }

    public int Assists { get; private set; }

    public int Caps { get; private set; }

    public float Points { get; private set; }

    public int HealPoints { get; private set; }

    public int CurrentCombo { get; private set; }

    public int HighestCombo { get; private set; }

    public int ComboTicksRemaining { get; private set; }

    public int KillStreak { get; private set; }

    public int HighestKillStreak { get; private set; }

    public int CurrentMultiKillCount { get; private set; }

    public int MultiKillTicksRemaining { get; private set; }

    public ulong BadgeMask { get; private set; }

    public bool IsChatBubbleVisible { get; private set; }

    public int ChatBubbleFrameIndex { get; private set; }

    public float ChatBubbleAlpha { get; private set; }

    public bool IsChatBubbleFading { get; private set; }

    public int ChatBubbleTicksRemaining { get; private set; }

    internal int? LastDamageDealerPlayerId { get; private set; }

    internal int LastDamageDealerAssistTicksRemaining { get; private set; }

    internal int? SecondToLastDamageDealerPlayerId { get; private set; }

    internal int SecondToLastDamageDealerAssistTicksRemaining { get; private set; }

    private bool SpyBackstabHitboxPending { get; set; }

    private int PyroPrimaryFuelScaledValue { get; set; }

    private float LegacyStateTickAccumulator { get; set; }

    public LegacyMovementState MovementState { get; private set; }

    private float SourceFacingDirectionX { get; set; } = 1f;

    private float PreviousSourceFacingDirectionX { get; set; } = 1f;

    public float RunPower => ClassDefinition.RunPower * GetExperimentalMovementSpeedMultiplier();

    public float JumpStrength => ClassDefinition.JumpStrength;

    public float MaxRunSpeed => ClassDefinition.MaxRunSpeed * GetExperimentalMovementSpeedMultiplier();

    public float GroundAcceleration => ClassDefinition.GroundAcceleration * GetExperimentalMovementSpeedMultiplier();

    public float GroundDeceleration => ClassDefinition.GroundDeceleration * GetExperimentalMovementSpeedMultiplier();

    public float Gravity => ClassDefinition.Gravity;

    public float JumpSpeed => ClassDefinition.JumpSpeed;

    public int MaxAirJumps => ClassDefinition.MaxAirJumps;

    public void SetDisplayName(string? displayName)
    {
        DisplayName = SanitizeDisplayName(displayName);
    }

    public void Spawn(PlayerTeam team, float x, float y)
    {
        Team = team;
        X = x;
        Y = y;
        HorizontalSpeed = 0f;
        VerticalSpeed = 0f;
        IsAlive = true;
        IsGrounded = false;
        Health = MaxHealth;
        IsCarryingIntel = false;
        IntelPickupCooldownTicks = 0;
        IntelRechargeTicks = 0f;
        RemainingAirJumps = MaxAirJumps;
        Metal = MaxMetal;
        CurrentShells = PrimaryWeapon.MaxAmmo;
        ResetPyroPrimaryStateFromCurrentAmmo();
        PrimaryCooldownTicks = 0;
        ReloadTicksUntilNextShell = 0;
        ResetExperimentalOffhandRuntimeState(refillAmmo: true);
        ResetAcquiredWeaponRuntimeState(refillAmmo: true);
        ResetExperimentalPowerRuntimeState();
        FacingDirectionX = team == PlayerTeam.Blue ? -1f : 1f;
        AimDirectionDegrees = team == PlayerTeam.Blue ? 180f : 0f;
        ContinuousDamageAccumulator = 0f;
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
        MedicUberCharge = 0f;
        IsMedicUberReady = false;
        IsMedicUbering = false;
        MedicNeedleCooldownTicks = 0;
        MedicNeedleRefillTicks = 0;
        ContinuousHealingAccumulator = 0f;
        QuoteBubbleCount = 0;
        QuoteBladesOut = 0;
        PyroAirblastCooldownTicks = 0;
        PyroFlareCooldownTicks = GetInitialPyroFlareCooldownTicks();
        IsPyroPrimaryRefilling = false;
        PyroFlameLoopTicksRemaining = 0;
        PyroPrimaryRequiresReleaseAfterEmpty = false;
        IsInSpawnRoom = false;
        IsUsingHealingCabinet = false;
        HealingCabinetSoundCooldownSecondsRemaining = 0f;
        ClearRecentDamageDealers();
        ResetCombatPerformanceTracking();
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
        ResetSourceFacingDirectionState();
        ClearChatBubble();
        RefreshGameplayLoadoutState();
    }

    public void SetClassDefinition(CharacterClassDefinition classDefinition)
    {
        ClassDefinition = classDefinition;
        SelectedGameplayLoadoutId = CharacterClassCatalog.RuntimeRegistry.GetDefaultLoadout(classDefinition.Id).Id;
        SelectedGameplayEquippedSlot = GameplayEquipmentSlot.Primary;
        Health = int.Clamp(Health, 0, MaxHealth);
        CurrentShells = int.Clamp(CurrentShells, 0, MaxShells);
        ResetPyroPrimaryStateFromCurrentAmmo();
        RemainingAirJumps = int.Min(RemainingAirJumps, MaxAirJumps);
        ResetExperimentalOffhandRuntimeState(refillAmmo: false);
        if (classDefinition.Id != PlayerClass.Soldier)
        {
            SetAcquiredWeapon(null);
        }
        else
        {
            ResetAcquiredWeaponRuntimeState(refillAmmo: false);
        }
        ResetExperimentalPowerRuntimeState();
        IntelRechargeTicks = 0f;
        ContinuousDamageAccumulator = 0f;
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
        MedicUberCharge = 0f;
        IsMedicUberReady = false;
        IsMedicUbering = false;
        MedicNeedleCooldownTicks = 0;
        MedicNeedleRefillTicks = 0;
        ContinuousHealingAccumulator = 0f;
        QuoteBubbleCount = 0;
        QuoteBladesOut = 0;
        PyroAirblastCooldownTicks = 0;
        PyroFlareCooldownTicks = GetInitialPyroFlareCooldownTicks();
        IsPyroPrimaryRefilling = false;
        PyroFlameLoopTicksRemaining = 0;
        PyroPrimaryRequiresReleaseAfterEmpty = false;
        IsUsingHealingCabinet = false;
        HealingCabinetSoundCooldownSecondsRemaining = 0f;
        ClearRecentDamageDealers();
        ResetCombatPerformanceTracking();
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
        ResetSourceFacingDirectionState();
        ClearChatBubble();
        RefreshGameplayLoadoutState();
    }

    public void Kill()
    {
        IsAlive = false;
        Health = 0;
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
        QuoteBubbleCount = 0;
        QuoteBladesOut = 0;
        PyroAirblastCooldownTicks = 0;
        PyroFlareCooldownTicks = 0;
        IsPyroPrimaryRefilling = false;
        PyroFlameLoopTicksRemaining = 0;
        PyroPrimaryRequiresReleaseAfterEmpty = false;
        IsInSpawnRoom = false;
        IsUsingHealingCabinet = false;
        HealingCabinetSoundCooldownSecondsRemaining = 0f;
        ClearRecentDamageDealers();
        ResetCombatPerformanceTracking();
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
        ResetSourceFacingDirectionState();
        ClearChatBubble();
        RefreshGameplayLoadoutState();
    }

    public void SetSpawnRoomState(bool isInSpawnRoom)
    {
        IsInSpawnRoom = isInSpawnRoom;
    }

    private int GetInitialPyroFlareCooldownTicks()
    {
        return ClassId == PlayerClass.Pyro
            ? PyroFlareReloadTicks
            : 0;
    }

    public void SetHealingCabinetState(bool isUsingHealingCabinet)
    {
        IsUsingHealingCabinet = isUsingHealingCabinet;
    }

    public bool CanPlayHealingCabinetSound()
    {
        return HealingCabinetSoundCooldownSecondsRemaining <= 0f;
    }

    public void RestartHealingCabinetSoundCooldown()
    {
        HealingCabinetSoundCooldownSecondsRemaining = HealingCabinetSoundCooldownSeconds;
    }

    private void ResetSourceFacingDirectionState()
    {
        var sourceFacingDirectionX = GetSourceFacingDirectionX(AimDirectionDegrees);
        SourceFacingDirectionX = sourceFacingDirectionX;
        PreviousSourceFacingDirectionX = sourceFacingDirectionX;
    }

    private int ConsumeLegacyStateTicks(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return 0;
        }

        LegacyStateTickAccumulator += deltaSeconds * LegacyMovementModel.SourceTicksPerSecond;
        var ticks = (int)LegacyStateTickAccumulator;
        if (ticks > 0)
        {
            LegacyStateTickAccumulator -= ticks;
        }

        return ticks;
    }

    public void SetMovementState(LegacyMovementState movementState)
    {
        MovementState = movementState;
    }

    public void ScaleVerticalSpeed(float scale)
    {
        VerticalSpeed *= scale;
    }

    internal bool CanOccupy(SimpleLevel level, PlayerTeam team, float x, float y)
    {
        GetCollisionBounds(out var previousLeft, out _, out var previousRight, out _);
        GetCollisionBoundsAt(x, y, out var left, out var top, out var right, out var bottom);

        foreach (var solid in level.Solids)
        {
            if (left < solid.Right && right > solid.Left && top < solid.Bottom && bottom > solid.Top)
            {
                return false;
            }
        }

        foreach (var gate in level.GetBlockingTeamGates(team, IsCarryingIntel))
        {
            if (left < gate.Right && right > gate.Left && top < gate.Bottom && bottom > gate.Top)
            {
                return false;
            }
        }

        foreach (var wall in level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (left < wall.Right && right > wall.Left && top < wall.Bottom && bottom > wall.Top)
            {
                if (wall.IsDirectionalDoor()
                    && !wall.BlocksDirectionalMovement(previousLeft, previousRight, left, right))
                {
                    continue;
                }

                return false;
            }
        }

        return true;
    }

    public void GetCollisionBounds(out float left, out float top, out float right, out float bottom)
    {
        GetCollisionBoundsAt(X, Y, out left, out top, out right, out bottom);
    }

    public void GetCollisionBoundsAt(float x, float y, out float left, out float top, out float right, out float bottom)
    {
        left = x + CollisionLeftOffset;
        top = y + CollisionTopOffset;
        right = x + CollisionRightOffset;
        bottom = y + CollisionBottomOffset;
    }

    private static string SanitizeDisplayName(string? displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return DefaultDisplayName;
        }

        var sanitized = displayName.Replace("#", string.Empty);
        if (sanitized.Length == 0)
        {
            return DefaultDisplayName;
        }

        return sanitized.Length > MaxDisplayNameLength
            ? sanitized[..MaxDisplayNameLength]
            : sanitized;
    }

    private void ResetExperimentalOffhandRuntimeState(bool refillAmmo)
    {
        if (ExperimentalOffhandWeapon is null)
        {
            ExperimentalOffhandCurrentShells = 0;
            ExperimentalOffhandCooldownTicks = 0;
            ExperimentalOffhandReloadTicksUntilNextShell = 0;
            IsExperimentalOffhandEquipped = false;
            return;
        }

        ExperimentalOffhandCurrentShells = refillAmmo
            ? ExperimentalOffhandWeapon.MaxAmmo
            : int.Clamp(ExperimentalOffhandCurrentShells, 0, ExperimentalOffhandWeapon.MaxAmmo);
        ExperimentalOffhandCooldownTicks = 0;
        ExperimentalOffhandReloadTicksUntilNextShell = refillAmmo
            ? 0
            : Math.Max(0, ExperimentalOffhandReloadTicksUntilNextShell);
        IsExperimentalOffhandEquipped = false;
    }

    private void ResetAcquiredWeaponRuntimeState(bool refillAmmo)
    {
        if (!AcquiredWeaponClassId.HasValue)
        {
            AcquiredWeaponCurrentShells = 0;
            AcquiredWeaponCooldownTicks = 0;
            AcquiredWeaponReloadTicksUntilNextShell = 0;
            IsAcquiredWeaponEquipped = false;
            return;
        }

        var weaponDefinition = AcquiredWeapon;
        if (weaponDefinition is null)
        {
            AcquiredWeaponClassId = null;
            AcquiredWeaponCurrentShells = 0;
            AcquiredWeaponCooldownTicks = 0;
            AcquiredWeaponReloadTicksUntilNextShell = 0;
            IsAcquiredWeaponEquipped = false;
            return;
        }

        AcquiredWeaponCurrentShells = refillAmmo
            ? weaponDefinition.MaxAmmo
            : int.Clamp(AcquiredWeaponCurrentShells, 0, weaponDefinition.MaxAmmo);
        AcquiredWeaponCooldownTicks = 0;
        AcquiredWeaponReloadTicksUntilNextShell = refillAmmo
            ? 0
            : Math.Max(0, AcquiredWeaponReloadTicksUntilNextShell);
        IsAcquiredWeaponEquipped = false;
    }

    private void RefreshGameplayLoadoutState()
    {
        GameplayLoadoutState = CreateGameplayLoadoutState();
    }

    private GameplayPlayerLoadoutState CreateGameplayLoadoutState()
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        var secondaryItemId = ResolveRegisteredWeaponItemId(ExperimentalOffhandWeapon);
        var acquiredItemId = ResolveRegisteredWeaponItemId(AcquiredWeapon);
        var equippedSlot = (IsExperimentalOffhandEquipped || IsAcquiredWeaponEquipped)
            ? GameplayEquipmentSlot.Secondary
            : SelectedGameplayEquippedSlot;

        return runtimeRegistry.CreatePlayerLoadoutState(
            ClassId,
            SelectedGameplayLoadoutId,
            equippedSlot,
            secondaryItemOverrideId: secondaryItemId,
            acquiredItemId: acquiredItemId);
    }

    private static string? ResolveRegisteredWeaponItemId(PrimaryWeaponDefinition? weaponDefinition)
    {
        if (weaponDefinition is null)
        {
            return null;
        }

        return CharacterClassCatalog.RuntimeRegistry.TryResolvePrimaryWeaponItemId(weaponDefinition, out var itemId)
            ? itemId
            : null;
    }

    private static string? GetBehaviorIdFromItem(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        return CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(itemId).BehaviorId;
    }

    public bool HasPrimaryBehavior(string behaviorId)
    {
        return string.Equals(PrimaryBehaviorId, behaviorId, StringComparison.Ordinal);
    }

    public bool HasSecondaryBehavior(string behaviorId)
    {
        return string.Equals(SecondaryBehaviorId, behaviorId, StringComparison.Ordinal);
    }

    public bool HasUtilityBehavior(string behaviorId)
    {
        return string.Equals(UtilityBehaviorId, behaviorId, StringComparison.Ordinal);
    }

    public bool HasEquippedBehavior(string behaviorId)
    {
        return string.Equals(EquippedBehaviorId, behaviorId, StringComparison.Ordinal);
    }

    public bool HasAcquiredBehavior(string behaviorId)
    {
        return string.Equals(AcquiredBehaviorId, behaviorId, StringComparison.Ordinal);
    }

    public bool HasAnyBehavior(string behaviorId)
    {
        return HasPrimaryBehavior(behaviorId)
            || HasSecondaryBehavior(behaviorId)
            || HasUtilityBehavior(behaviorId)
            || HasAcquiredBehavior(behaviorId);
    }

    public bool OwnsGameplayItem(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        return runtimeRegistry.OwnsItemByDefault(itemId)
            || OwnedGameplayItemIds.Contains(itemId);
    }

    public IReadOnlyList<string> GetOwnedGameplayItemIds()
    {
        return CharacterClassCatalog.RuntimeRegistry.ModPacks
            .SelectMany(pack => pack.Items.Keys)
            .Where(OwnsGameplayItem)
            .OrderBy(static itemId => itemId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<string> GetTrackedOwnedGameplayItemIds()
    {
        return OwnedGameplayItemIds
            .OrderBy(static itemId => itemId, StringComparer.Ordinal)
            .ToArray();
    }

    public void ClearTrackedOwnedGameplayItems()
    {
        if (OwnedGameplayItemIds.Count == 0)
        {
            return;
        }

        OwnedGameplayItemIds.Clear();
        RefreshGameplayLoadoutState();
    }

    public bool TryGrantGameplayItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        var normalizedItemId = itemId.Trim();
        if (!CharacterClassCatalog.RuntimeRegistry.RequiresTrackedOwnership(normalizedItemId))
        {
            return CharacterClassCatalog.RuntimeRegistry.OwnsItemByDefault(normalizedItemId);
        }

        return OwnedGameplayItemIds.Add(normalizedItemId);
    }

    public bool TryRevokeGameplayItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        var normalizedItemId = itemId.Trim();
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        if (!runtimeRegistry.RequiresTrackedOwnership(normalizedItemId))
        {
            return false;
        }

        var removed = OwnedGameplayItemIds.Remove(normalizedItemId);
        if (!removed)
        {
            return false;
        }

        if (string.Equals(GameplayLoadoutState.SecondaryItemId, normalizedItemId, StringComparison.Ordinal))
        {
            SetExperimentalOffhandWeapon(null);
        }

        if (string.Equals(GameplayLoadoutState.AcquiredItemId, normalizedItemId, StringComparison.Ordinal))
        {
            SetAcquiredWeapon(null);
        }

        return true;
    }

    public IReadOnlyList<GameplayReplicatedStateEntry> GetReplicatedStateEntries()
    {
        return ReplicatedStateEntries.Values
            .OrderBy(static entry => entry.OwnerId, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Key, StringComparer.Ordinal)
            .ToArray();
    }

    public bool TryGetReplicatedStateInt(string ownerId, string key, out int value)
    {
        if (TryGetReplicatedState(ownerId, key, GameplayReplicatedStateValueKind.Whole, out var entry))
        {
            value = entry.IntValue;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetReplicatedStateFloat(string ownerId, string key, out float value)
    {
        if (TryGetReplicatedState(ownerId, key, GameplayReplicatedStateValueKind.Scalar, out var entry))
        {
            value = entry.FloatValue;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetReplicatedStateBool(string ownerId, string key, out bool value)
    {
        if (TryGetReplicatedState(ownerId, key, GameplayReplicatedStateValueKind.Toggle, out var entry))
        {
            value = entry.BoolValue;
            return true;
        }

        value = default;
        return false;
    }

    public bool SetReplicatedStateInt(string ownerId, string key, int value)
    {
        return SetReplicatedState(new GameplayReplicatedStateEntry(ownerId, key, GameplayReplicatedStateValueKind.Whole, IntValue: value));
    }

    public bool SetReplicatedStateFloat(string ownerId, string key, float value)
    {
        return SetReplicatedState(new GameplayReplicatedStateEntry(ownerId, key, GameplayReplicatedStateValueKind.Scalar, FloatValue: value));
    }

    public bool SetReplicatedStateBool(string ownerId, string key, bool value)
    {
        return SetReplicatedState(new GameplayReplicatedStateEntry(ownerId, key, GameplayReplicatedStateValueKind.Toggle, BoolValue: value));
    }

    public bool ClearReplicatedState(string ownerId, string key)
    {
        return ReplicatedStateEntries.Remove(CreateReplicatedStateDictionaryKey(ownerId, key));
    }

    internal void ReplaceReplicatedStateEntries(IEnumerable<GameplayReplicatedStateEntry> entries)
    {
        ReplicatedStateEntries.Clear();
        foreach (var entry in entries)
        {
            if (ReplicatedStateEntries.Count >= MaxReplicatedStateEntries)
            {
                break;
            }

            var normalizedEntry = NormalizeReplicatedStateEntry(entry);
            if (normalizedEntry is null)
            {
                continue;
            }

            ReplicatedStateEntries[CreateReplicatedStateDictionaryKey(normalizedEntry.OwnerId, normalizedEntry.Key)] = normalizedEntry;
        }
    }

    public void ReplaceOwnedGameplayItemIds(IEnumerable<string> itemIds)
    {
        OwnedGameplayItemIds.Clear();
        foreach (var itemId in itemIds)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                continue;
            }

            var normalizedItemId = itemId.Trim();
            if (!CharacterClassCatalog.RuntimeRegistry.RequiresTrackedOwnership(normalizedItemId))
            {
                continue;
            }

            OwnedGameplayItemIds.Add(normalizedItemId);
        }

        RefreshGameplayLoadoutState();
    }

    private Dictionary<string, GameplayReplicatedStateEntry> ReplicatedStateEntries { get; } = new(StringComparer.Ordinal);

    private HashSet<string> OwnedGameplayItemIds { get; } = new(StringComparer.Ordinal);

    private bool SetReplicatedState(GameplayReplicatedStateEntry entry)
    {
        var normalizedEntry = NormalizeReplicatedStateEntry(entry);
        if (normalizedEntry is null)
        {
            return false;
        }

        var dictionaryKey = CreateReplicatedStateDictionaryKey(normalizedEntry.OwnerId, normalizedEntry.Key);
        if (!ReplicatedStateEntries.ContainsKey(dictionaryKey)
            && ReplicatedStateEntries.Count >= MaxReplicatedStateEntries)
        {
            return false;
        }

        ReplicatedStateEntries[dictionaryKey] = normalizedEntry;
        return true;
    }

    private bool TryGetReplicatedState(string ownerId, string key, GameplayReplicatedStateValueKind expectedKind, out GameplayReplicatedStateEntry entry)
    {
        if (ReplicatedStateEntries.TryGetValue(CreateReplicatedStateDictionaryKey(ownerId, key), out entry!)
            && entry.Kind == expectedKind)
        {
            return true;
        }

        entry = null!;
        return false;
    }

    private static GameplayReplicatedStateEntry? NormalizeReplicatedStateEntry(GameplayReplicatedStateEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.OwnerId) || string.IsNullOrWhiteSpace(entry.Key))
        {
            return null;
        }

        if (entry.OwnerId.Length > MaxReplicatedStateIdentifierLength
            || entry.Key.Length > MaxReplicatedStateIdentifierLength)
        {
            return null;
        }

        return entry with
        {
            OwnerId = entry.OwnerId.Trim(),
            Key = entry.Key.Trim(),
        };
    }

    private static string CreateReplicatedStateDictionaryKey(string ownerId, string key)
    {
        return string.Concat(ownerId, "::", key);
    }
}

