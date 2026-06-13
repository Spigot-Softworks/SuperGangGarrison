using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity : SimulationEntity
{
    private const int MaxReplicatedStateEntries = 16;
    private const int MaxDisplayNameLength = 20;
    private const string DefaultDisplayName = "Player";
    public const float MinPlayerScale = 0.25f;
    public const float MaxPlayerScale = 4f;
    public const float HeavyPrimaryMoveScale = 0.375f;
    public const int HeavyEatDurationTicks = 124;
    public const int HeavySandvichCooldownTicks = 1350;
    private const float HealingCabinetSoundCooldownSeconds = 4f;
    private static readonly float HeavyEatHealPerTick = 200f / HeavyEatDurationTicks;
    private float HeavyEatHealPerTickValue { get; set; } = HeavyEatHealPerTick;
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
    public const float MedicUberDurationSeconds = 8f;
    public const float MedicUberChargeDrainPerSourceTick = MedicUberMaxCharge / (MedicUberDurationSeconds * LegacyMovementModel.SourceTicksPerSecond);
    public const int MedicPassiveRegenIntervalSourceTicks = 30;
    public const int MedicPassiveRegenUnscathedCapSourceTicks = 10 * 30;
    public const int MedicPassiveRegenFirstThresholdSourceTicks = 7 * 30;
    public const int MedicPassiveRegenSecondThresholdSourceTicks = 14 * 30;
    public const int MedicNeedleRefillTicksDefault = 55;
    public const int MedicNeedleFireCooldownTicks = 3;
    public const int IntelRechargeMaxTicks = 900;
    public const int SpyBackstabWindupTicksDefault = 32;
    public const int SpyBackstabRecoveryTicksDefault = 18;
    public const int SpyBackstabVisualTicksDefault = 60;
    public const float SpyCloakFadePerTick = 0.05f;
    public const float SpyCloakToggleThreshold = 0.5f;
    public const float SpyMinAllyCloakAlpha = 0.5f;
    public const float SpyDamageRevealAlpha = 0.3f;
    public const float SpyMineRevealAlpha = 0.2f;
    public const float SpySniperRevealAlpha = 0.3f;
    public const int QuoteBubbleLimit = 25;
    public const int QuoteBladeEnergyCost = 15;
    public const int QuoteBladeLifetimeTicks = 15;
    public const int QuoteBladeMaxOut = 1;
    public const int CivvieUmbrellaMaxChargeTicks = 360;
    public const int CivvieUmbrellaHoldDrainPerTick = 0;
    public const int CivvieUmbrellaRechargePerTick = 1;
    public const int CivvieUmbrellaImpactDrain = 30;
    public const int CivvieUmbrellaOpeningDurationTicks = 6;
    public const int CivvieUmbrellaOpeningFrameCount = 3;
    public const int CivvieUmbrellaAirblastOpeningFrameIndex = 2;
    public const int CivvieUmbrellaAirblastOpeningTick =
        (CivvieUmbrellaAirblastOpeningFrameIndex * CivvieUmbrellaOpeningDurationTicks) / CivvieUmbrellaOpeningFrameCount;
    public const float CivvieUmbrellaFallSpeedScale = 0.25f;
    public const float CivvieUmbrellaSlowFallAimArcDegrees = 60f;
    internal const float CivvieUmbrellaSlowFallControlFactorScale = 0.48f;
    internal const float CivvieUmbrellaSlowFallFrictionFactorScale = 0.86f;
    internal const float CivvieUmbrellaSlowFallRunPowerMultiplier = 1.85f;
    public const int CivvieTauntHealAmountDefault = 15;
    public const float CivvieTauntHealRadiusDefault = 120f;
    public const int CivvieTauntHealFrameIndex = 9;
    public const float CivviePogoBaseBounceJumpScaleDefault = 0.5f;
    public const float CivviePogoSuperJumpScaleDefault = 1.125f;
    public const int CivviePogoCrunchDurationTicksDefault = 2;
    public const int CivviePogoStuckSampleIntervalTicks = 6;
    public const float CivviePogoStuckMinVerticalMovement = 3f;
    public const int CivviePogoStuckRebounceCooldownTicksDefault = 6;
    public const string CivvieTauntAbilityItemId = "ability.civilian-taunt";
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
    public const int PyroAirburstCost = 28;
    public const int PyroAirblastReloadTicks = 40;
    public const int PyroAirblastNoFlameTicks = 15;
    public const int PyroAirburstNoFlameTicks = 0;
    public const int PyroFlareCost = 35;
    public const int PyroFlareReloadTicks = 55;
    public const int PyroFlareAmmoRequirement = PyroAirblastCost + PyroFlareCost;
    public const int PyroPrimaryFuelScale = 10;
    public const int PyroPrimaryFlameCostScaled = 18;
    public const int PyroPrimaryRefillScaledPerTick = 18;
    public const int PyroPrimaryRefillBufferTicks = 7;
    public const int PyroPrimaryEmptyCooldownTicks = PyroPrimaryRefillBufferTicks * 2;
    public const int PyroFlameLoopMaintainTicks = 2;
    private const int TauntRestartCooldownTicks = 30;
    private const float TauntFrameStepPerTick = 0.3f;
    private const int ChatBubbleHoldTicks = 60;
    private const float ChatBubbleFadePerTick = 0.05f;
    private float _playerScale = 1f;

    public PlayerEntity(int id, CharacterClassDefinition classDefinition, string? displayName = null) : base(id)
    {
        ClassDefinition = classDefinition;
        DisplayName = SanitizeDisplayName(displayName);
        FacingDirectionX = 1f;
        SelectedGameplayLoadoutId = CharacterClassCatalog.RuntimeRegistry.GetDefaultLoadout(GameplayClassId).Id;
        SelectedGameplayEquippedSlot = GameplayEquipmentSlot.Primary;
        GameplayLoadoutState = CreateGameplayLoadoutState();
    }

    public float X { get; private set; }

    public float Y { get; private set; }

    public CharacterClassDefinition ClassDefinition { get; private set; }

    public PlayerClass ClassId => ClassDefinition.Id;

    public string GameplayClassId => string.IsNullOrWhiteSpace(ClassDefinition.GameplayClassId)
        ? CharacterClassCatalog.RuntimeRegistry.GetRequiredClassBinding(ClassId).ClassId
        : ClassDefinition.GameplayClassId;

    public PlayerClass BotGraphClassId => string.IsNullOrWhiteSpace(ClassDefinition.GameplayClassId)
        ? ClassId
        : ClassDefinition.BotGraphClassId;

    public string ClassName => ClassDefinition.DisplayName;

    public string DisplayName { get; private set; }

    public GameplayPlayerLoadoutState GameplayLoadoutState { get; private set; }

    public string SelectedGameplayLoadoutId { get; private set; }

    public GameplayEquipmentSlot SelectedGameplayEquippedSlot { get; private set; }

    public float PlayerScale => _playerScale;

    public float Width => ClassDefinition.Width * _playerScale;

    public float Height => ClassDefinition.Height * _playerScale;

    public float CollisionLeftOffset => ClassDefinition.CollisionLeft * _playerScale;

    public float CollisionTopOffset => ClassDefinition.CollisionTop * _playerScale;

    public float CollisionRightOffset => ClassDefinition.CollisionRight * _playerScale;

    public float CollisionBottomOffset => ClassDefinition.CollisionBottom * _playerScale;

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

    public int MaxHealth => ExperimentalMaxHealthOverrideValue ?? ClassDefinition.MaxHealth + ExperimentalMaxHealthBonusValue;

    public float Metal { get; private set; } = 100f;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Kept as an instance property to preserve the public player API.")]
    public float MaxMetal => ExperimentalMetalCapacityValue;

    public bool IsCarryingIntel { get; private set; }

    public int IntelPickupCooldownTicks { get; private set; }

    public float IntelRechargeTicks { get; private set; }

    public bool IsInSpawnRoom { get; private set; }

    public bool IsUsingHealingCabinet { get; private set; }

    public float HealingCabinetSoundCooldownSecondsRemaining { get; private set; }

    public int RemainingAirJumps { get; private set; }

    public float FacingDirectionX { get; private set; }

    public float AimDirectionDegrees { get; private set; }

    public PrimaryWeaponDefinition PrimaryWeapon => ExperimentalPrimaryWeaponOverride ?? ClassDefinition.PrimaryWeapon;
    public float AimWorldX { get; private set; }

    public float AimWorldY { get; private set; }

    public (float X, float Y) AimWorldPosition => (AimWorldX, AimWorldY);

    public int CurrentShells { get; private set; }

    public int MaxShells => PrimaryWeapon.MaxAmmo;

    public int PrimaryCooldownTicks { get; private set; }

    public bool LastPrimaryShotIgnoredAmmoCost { get; private set; }

    public int ReloadTicksUntilNextShell { get; private set; }

    public PrimaryWeaponDefinition? ExperimentalOffhandWeapon { get; private set; }

    public bool HasExperimentalOffhandWeapon => ExperimentalOffhandWeapon is not null;

    public int ExperimentalOffhandCurrentShells { get; private set; }

    public int ExperimentalOffhandMaxShells => ExperimentalOffhandWeapon?.MaxAmmo ?? 0;

    public int ExperimentalOffhandCooldownTicks { get; private set; }

    public int ExperimentalOffhandReloadTicksUntilNextShell { get; private set; }

    public bool IsExperimentalOffhandEquipped { get; private set; }

    public bool IsExperimentalOffhandPresented => IsExperimentalOffhandSelected;

    public bool IsExperimentalOffhandSelected => HasExperimentalOffhandWeapon
        && !IsAcquiredWeaponEquipped
        && (GameplayLoadoutState.EquippedSlot == GameplayEquipmentSlot.Secondary
            && string.Equals(GameplayLoadoutState.EquippedItemId, GameplayLoadoutState.SecondaryItemId, StringComparison.Ordinal)
            || IsExperimentalOffhandEquipped
            && GameplayLoadoutState.EquippedSlot == GameplayEquipmentSlot.Utility
            && string.Equals(GameplayLoadoutState.EquippedItemId, GameplayLoadoutState.UtilityItemId, StringComparison.Ordinal));

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

    public int HeavyEatCooldownDurationTicks { get; private set; } = HeavySandvichCooldownTicks;

    public bool IsTaunting { get; private set; }

    public float TauntFrameIndex { get; private set; }

    public int TauntRestartCooldownTicksRemaining { get; private set; }

    private bool TauntInputReleaseRequired { get; set; }

    private bool CivvieTauntHealPending { get; set; }

    private bool CivviePogoSuperJumpHeld { get; set; }

    private bool CivviePogoSuperJumpSoundPending { get; set; }

    private float CivviePogoStuckReferenceY { get; set; }

    private int CivviePogoStuckWatchTicks { get; set; }

    private int CivviePogoStuckRebounceCooldownTicks { get; set; }

    public float HeavyHealingAccumulator { get; private set; }

    public bool IsSniperScoped { get; private set; }

    public int SniperChargeTicks { get; private set; }

    public bool IsUsingBinoculars { get; private set; }

    public float BinocularsFocusX { get; private set; }

    public float BinocularsFocusY { get; private set; }

    public const float BinocularsMaxViewDistance = 1000f;

    public const float BinocularsMoveScale = 0.0f;

    public bool IsUbered => UberTicksRemaining > 0;

    public bool HasInfiniteAmmoFromUber => IsUbered
        || ClassId == PlayerClass.Medic
        && IsMedicUbering
        && !HasEquippedBehavior(BuiltInGameplayBehaviorIds.MedigunCrit);

    public int UberTicksRemaining { get; private set; }

    public bool IsKritzCritBoosted => KritzCritBoostTicksRemaining > 0;

    public int KritzCritBoostTicksRemaining { get; private set; }

    public float RageCharge { get; private set; }

    public bool IsRageReady { get; private set; }

    public int RageTicksRemaining { get; private set; }

    public bool IsRaging => RageTicksRemaining > 0;

    public int? MedicHealTargetId { get; private set; }

    public bool IsMedicHealing { get; private set; }

    public float MedicUberCharge { get; private set; }

    public bool IsMedicUberReady { get; private set; }

    public bool IsMedicUbering { get; private set; }

    public bool MedicUberReadyPresentationPending { get; private set; }

    public int MedicNeedleCooldownTicks { get; private set; }

    public int MedicNeedleRefillTicks { get; private set; }

    public float ContinuousHealingAccumulator { get; private set; }

    public int QuoteBubbleCount { get; private set; }

    public int QuoteBladesOut { get; private set; }

    public int CivvieUmbrellaChargeTicks { get; private set; } = CivvieUmbrellaMaxChargeTicks;

    public int CivvieUmbrellaCooldownTicks => Math.Max(0, CivvieUmbrellaMaxChargeTicks - CivvieUmbrellaChargeTicks);

    public bool IsCivvieUmbrellaActive { get; private set; }

    public bool IsCivvieUmbrellaDisabled => IsCivvieUmbrellaBroken || CivvieUmbrellaChargeTicks <= 0;

    public bool IsCivvieUmbrellaBroken { get; private set; }

    public bool IsCivviePogoActive { get; private set; }

    public int CivviePogoCrunchTicksRemaining { get; private set; }

    private int CivvieUmbrellaOpeningElapsedTicks { get; set; }

    private bool CivvieUmbrellaOpeningAirblastTriggered { get; set; }

    public bool IsExperimentalDemoknightEnabled { get; private set; }

    public bool IsExperimentalDemoknightCharging { get; private set; }

    public bool IsExperimentalDemoknightChargeFullControlEnabled => ExperimentalDemoknightChargeFullControlEnabled;

    public int ExperimentalDemoknightChargeTicksRemaining { get; private set; }

    public float ExperimentalDemoknightChargeFraction => ExperimentalDemoknightChargeTicksRemaining / (float)ExperimentalDemoknightChargeMaxTicks;

    public bool IsExperimentalDemoknightChargeDashActive { get; private set; }

    public bool IsExperimentalDemoknightChargeFlightActive { get; private set; }

    public float ExperimentalDemoknightChargeAcceleration { get; private set; }

    public bool IsExperimentalGhostDashing
        => ExperimentalGhostDashTicksRemaining > 0
            || HasReplicatedHeavyDashToggle(GameplayAbilityReplicatedState.HeavyDashActiveKey);

    public bool IsExperimentalGhostDashVisible
        => ExperimentalGhostDashVisibilityTicksRemaining > 0
            || ExperimentalGhostDashSlideVisualSpeedPerSecond > 0f
            || HasReplicatedHeavyDashToggle(GameplayAbilityReplicatedState.HeavyDashVisibleKey)
            || HasReplicatedHeavyDashToggle(GameplayAbilityReplicatedState.HeavyDashActiveKey);

    public float ExperimentalGhostDashTrailAlpha
    {
        get
        {
            if (ClassId == PlayerClass.Heavy
                && TryGetReplicatedStateFloat(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.HeavyDashTrailAlphaKey,
                    out var replicatedTrailAlpha))
            {
                return float.Clamp(replicatedTrailAlpha, 0f, 1f);
            }

            if (ExperimentalGhostDashTrailAlphaValue > 0f)
            {
                return float.Clamp(ExperimentalGhostDashTrailAlphaValue, 0f, 1f);
            }

            return HasReplicatedHeavyDashToggle(GameplayAbilityReplicatedState.HeavyDashVisibleKey)
                || HasReplicatedHeavyDashToggle(GameplayAbilityReplicatedState.HeavyDashActiveKey)
                ? 1f
                : 0f;
        }
    }

    private bool HasReplicatedHeavyDashToggle(string key)
    {
        return ClassId == PlayerClass.Heavy && HasReplicatedCoreAbilityToggle(key);
    }

    private bool ExperimentalSoldierAmmoRegeneratesWhileSwappedOutEnabled { get; set; }

    private bool ExperimentalSelfDamageHealingEnabled { get; set; }

    private bool ExperimentalSoldierInfiniteAmmoDuringRageEnabled { get; set; }

    private float ExperimentalSoldierSwappedOutAmmoRegenAccumulator { get; set; }

    private float ExperimentalReloadSpeedMultiplierValue { get; set; } = 1f;

    private bool ExperimentalDemoknightChargeFullControlEnabled { get; set; }

    private int ExperimentalDemoknightPostRageRegenTicksRemaining { get; set; }

    private float ExperimentalDemoknightPostRageRegenPerTickValue { get; set; }

    private int ExperimentalGhostDashTicksRemaining { get; set; }

    public int ExperimentalGhostDashCooldownTicksRemaining { get; private set; }

    private int ExperimentalGhostDashVisibilityTicksRemaining { get; set; }

    private int ExperimentalGhostDashMovementTicksRemaining { get; set; }

    private float ExperimentalGhostDashDistanceRemaining { get; set; }

    private float ExperimentalGhostDashSpeedPerSecondValue { get; set; }

    private bool ExperimentalGhostDashUsesMomentum { get; set; }

    internal float ExperimentalGhostDashBurstSpeedMultiplier { get; private set; }

    private bool ExperimentalGhostDashDisablesGravity { get; set; }

    public bool ExperimentalGhostDashEnablesTrail { get; private set; }

    private int ExperimentalGhostDashInitialTicks { get; set; }

    private float ExperimentalGhostDashInitialDistance { get; set; }

    private float ExperimentalGhostDashDistanceTraveled { get; set; }

    private float ExperimentalGhostDashLastMoveDistance { get; set; }

    private float ExperimentalGhostDashMomentumDirectionX { get; set; } = 1f;

    private float ExperimentalGhostDashSlideVelocityPerTick { get; set; } = ExperimentalGameplaySettings.DefaultGhostDashSlideVelocityPerTick;

    private float ExperimentalGhostDashSlideVisualSpeedPerSecond { get; set; }

    private float ExperimentalGhostDashSlideVisualInitialSpeedPerSecond { get; set; }

    private float ExperimentalGhostDashTrailAlphaValue { get; set; }

    private float ExperimentalGhostDashNextAttackDamageMultiplierValue { get; set; } = 1f;

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

    public int SpySuperjumpChargeTicks { get; private set; }

    public float SpySuperjumpChargeDirectionDegrees { get; private set; }

    public bool IsSpySuperjumping { get; private set; }

    public float SpySuperjumpHorizontalVelocity { get; private set; }

    // Tracks which movement buttons were held when superjump charging started (client-side only)
    public byte SpySuperjumpChargeStartMovementButtons { get; private set; }

    private bool SpySuperjumpChargeStartBlockedUntilAbilityRelease { get; set; }

    public const int SpySuperjumpMaxChargeTicks = 30; // 0.5s at 60 ticks/sec

    public const float SpySuperjumpMinVelocity = 200f; // units per second

    public const float SpySuperjumpMaxVelocity = 600f; // units per second

    public const int SpySuperjumpCooldownTicks = 240; // 8 seconds at 30 ticks/sec

    public int SpySuperjumpCooldownTicksRemaining { get; private set; }

    public int Kills { get; private set; }

    public int Deaths { get; private set; }

    public int GibDeaths { get; private set; }

    public int Assists { get; private set; }

    public int Caps { get; private set; }

    public float Points { get; private set; }

    public int HealPoints { get; private set; }

    public int HealingReceived { get; private set; }

    public int TimeUnscathedSourceTicks { get; private set; }

    private int MedicPassiveRegenElapsedSourceTicks { get; set; }

    public int CurrentCombo { get; private set; }

    public int HighestCombo { get; private set; }

    public int ComboTicksRemaining { get; private set; }

    public int KillStreak { get; private set; }

    public int HighestKillStreak { get; private set; }

    public int CurrentMultiKillCount { get; private set; }

    public int MultiKillTicksRemaining { get; private set; }

    public ulong BadgeMask { get; private set; }

    public bool IsChatBubbleVisible { get; private set; }

    public bool IsTypingChatMessage { get; set; }

    public int ChatBubbleFrameIndex { get; private set; }

    public float ChatBubbleAlpha { get; private set; }

    public bool IsChatBubbleFading { get; private set; }

    public int ChatBubbleTicksRemaining { get; private set; }

    internal int? LastDamageDealerPlayerId { get; private set; }

    internal int LastDamageDealerAssistTicksRemaining { get; private set; }

    internal int? SecondToLastDamageDealerPlayerId { get; private set; }

    internal int SecondToLastDamageDealerAssistTicksRemaining { get; private set; }

    private bool SpyBackstabHitboxPending { get; set; }

    private PrimaryWeaponDefinition? ExperimentalPrimaryWeaponOverride { get; set; }

    private int ExperimentalMaxHealthBonusValue { get; set; }

    private int? ExperimentalMaxHealthOverrideValue { get; set; }

    private float ExperimentalHealthPackHealingMultiplierValue { get; set; } = 1f;

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

    public float JumpSpeed => ClassDefinition.JumpSpeed * GetExperimentalJumpHeightMultiplier();

    public int MaxAirJumps => Math.Max(0, ClassDefinition.MaxAirJumps + GetExperimentalBonusAirJumps());

    public void SetPlayerScale(float scale)
    {
        _playerScale = ClampPlayerScale(scale);
    }

    public void SetAimWorldPosition(float x, float y)
    {
        AimWorldX = x;
        AimWorldY = y;
    }

    public void SetBinocularsFocusPosition(float x, float y)
    {
        BinocularsFocusX = x;
        BinocularsFocusY = y;
    }

    public static float ClampPlayerScale(float scale) => float.Clamp(scale, MinPlayerScale, MaxPlayerScale);

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
        ResetTransientState(
            pyroFlareCooldownTicks: GetInitialPyroFlareCooldownTicks(),
            resetMedicUberCharge: true,
            clearSpawnRoomState: true);
        RefreshGameplayLoadoutState();
    }

    public void SetClassDefinition(CharacterClassDefinition classDefinition)
    {
        ClassDefinition = classDefinition;
        SelectedGameplayLoadoutId = CharacterClassCatalog.RuntimeRegistry.GetDefaultLoadout(GameplayClassId).Id;
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
        ResetTransientState(
            pyroFlareCooldownTicks: GetInitialPyroFlareCooldownTicks(),
            resetMedicUberCharge: true,
            clearSpawnRoomState: false);
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
        ResetExperimentalOffhandRuntimeState(refillAmmo: false);
        SetAcquiredWeapon(null);
        ResetExperimentalPowerRuntimeState();
        ResetTransientState(
            pyroFlareCooldownTicks: 0,
            resetMedicUberCharge: false,
            clearSpawnRoomState: true);
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

    private void ResetTransientState(
        int pyroFlareCooldownTicks,
        bool resetMedicUberCharge,
        bool clearSpawnRoomState)
    {
        ContinuousDamageAccumulator = 0f;
        ResetPassiveRegenState();
        ExtinguishAfterburn();
        IsHeavyEating = false;
        HeavyEatTicksRemaining = 0;
        ClearHeavyEatCooldown();
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
        IsUsingBinoculars = false;
        BinocularsFocusX = X;
        BinocularsFocusY = Y;
        UberTicksRemaining = 0;
        KritzCritBoostTicksRemaining = 0;
        SpySuperjumpCooldownTicksRemaining = 0;
        MedicHealTargetId = null;
        IsMedicHealing = false;
        if (resetMedicUberCharge)
        {
            MedicUberCharge = ClassId == PlayerClass.Medic ? 0f : 0f;
            IsMedicUberReady = false;
            MedicUberReadyPresentationPending = false;
        }

        IsMedicUbering = false;
        MedicNeedleCooldownTicks = 0;
        MedicNeedleRefillTicks = 0;
        ContinuousHealingAccumulator = 0f;
        QuoteBubbleCount = 0;
        QuoteBladesOut = 0;
        ResetCivvieUmbrellaState();
        DeactivateCivviePogo();
        PyroAirblastCooldownTicks = 0;
        PyroFlareCooldownTicks = pyroFlareCooldownTicks;
        IsPyroPrimaryRefilling = false;
        PyroFlameLoopTicksRemaining = 0;
        PyroPrimaryRequiresReleaseAfterEmpty = false;
        LastPrimaryShotIgnoredAmmoCost = false;
        if (clearSpawnRoomState)
        {
            IsInSpawnRoom = false;
        }

        IsUsingHealingCabinet = false;
        HealingCabinetSoundCooldownSecondsRemaining = 0f;
        ClearRecentDamageDealers();
        ResetCombatPerformanceTracking();
        ResetSpyTransientState();
        ResetMovementPresentationState();
        ClearChatBubble();
    }

    private void ResetSpyTransientState()
    {
        IsSpyCloaked = false;
        SpyCloakAlpha = 1f;
        SpyBackstabWindupTicksRemaining = 0;
        SpyBackstabRecoveryTicksRemaining = 0;
        SpyBackstabVisualTicksRemaining = 0;
        SpyBackstabDirectionDegrees = 0f;
        IsSpyVisibleToEnemies = false;
        SpyBackstabHitboxPending = false;
    }

    private void ResetMovementPresentationState()
    {
        LegacyStateTickAccumulator = 0f;
        MovementState = LegacyMovementState.None;
        ResetSourceFacingDirectionState();
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

    public void SetMovementStateIfAirborne(LegacyMovementState movementState)
    {
        if (!IsGrounded)
        {
            MovementState = movementState;
        }
    }

    public void ScaleVerticalSpeed(float scale)
    {
        VerticalSpeed *= scale;
    }

    internal bool CanOccupy(SimpleLevel level, PlayerTeam team, float x, float y)
    {
        GetCollisionBounds(out var previousLeft, out var previousTop, out var previousRight, out var previousBottom);
        GetCollisionBoundsAt(x, y, out var left, out var top, out var right, out var bottom);

        if (level.IntersectsSolid(left, top, right, bottom))
        {
            return false;
        }

        foreach (var gate in level.GetBlockingTeamGates(team, IsCarryingIntel))
        {
            if (left < gate.Right && right > gate.Left && top < gate.Bottom && bottom > gate.Top)
            {
                return false;
            }
        }

        for (var wallIndex = 0; wallIndex < level.RoomObjects.Count; wallIndex += 1)
        {
            var wall = level.RoomObjects[wallIndex];
            if (wall.Type != RoomObjectType.PlayerWall || !level.IsRoomObjectActive(wallIndex))
            {
                continue;
            }

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

        if (SimpleLevelBarrierCollision.BlocksPlayerAt(
                level,
                team,
                IsCarryingIntel,
                previousLeft,
                previousRight,
                previousTop,
                previousBottom,
                left,
                top,
                right,
                bottom))
        {
            return false;
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

    internal void GetRoundedCollisionBoundsAt(float x, float y, out float left, out float top, out float right, out float bottom)
    {
        // Legacy GameMaker solid checks round collision bounds to integer coordinates.
        left = MathF.Round(x + CollisionLeftOffset);
        top = MathF.Round(y + CollisionTopOffset);
        right = MathF.Round(x + CollisionRightOffset);
        bottom = MathF.Round(y + CollisionBottomOffset);
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
        GameplayEquipmentSlot equippedSlot;
        if (IsAcquiredWeaponEquipped)
        {
            equippedSlot = GameplayEquipmentSlot.Secondary;
        }
        else if (IsExperimentalOffhandEquipped)
        {
            equippedSlot = secondaryItemId is not null
                ? GameplayEquipmentSlot.Secondary
                : GameplayEquipmentSlot.Utility;
        }
        else
        {
            equippedSlot = SelectedGameplayEquippedSlot;
        }

        return runtimeRegistry.CreatePlayerLoadoutState(
            GameplayClassId,
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

    private HashSet<string> OwnedGameplayItemIds { get; } = new(StringComparer.Ordinal);
}

