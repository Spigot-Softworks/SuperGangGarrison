using System.Diagnostics.CodeAnalysis;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity : SimulationEntity
{
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
    }

    public float X { get; private set; }

    public float Y { get; private set; }

    public CharacterClassDefinition ClassDefinition { get; private set; }

    public PlayerClass ClassId => ClassDefinition.Id;

    public string ClassName => ClassDefinition.DisplayName;

    public string DisplayName { get; private set; }

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

    public int PyroAirblastCooldownTicks { get; private set; }

    public int PyroFlareCooldownTicks { get; private set; }

    public bool IsPyroPrimaryRefilling { get; private set; }

    public int PyroFlameLoopTicksRemaining { get; private set; }

    public bool PyroPrimaryRequiresReleaseAfterEmpty { get; private set; }

    public int PyroPrimaryFuelScaled => ClassId == PlayerClass.Pyro
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

    public int Caps { get; private set; }

    public int HealPoints { get; private set; }

    public bool IsChatBubbleVisible { get; private set; }

    public int ChatBubbleFrameIndex { get; private set; }

    public float ChatBubbleAlpha { get; private set; }

    public bool IsChatBubbleFading { get; private set; }

    public int ChatBubbleTicksRemaining { get; private set; }

    private bool SpyBackstabHitboxPending { get; set; }

    private int PyroPrimaryFuelScaledValue { get; set; }

    private float LegacyStateTickAccumulator { get; set; }

    public LegacyMovementState MovementState { get; private set; }

    private float SourceFacingDirectionX { get; set; } = 1f;

    private float PreviousSourceFacingDirectionX { get; set; } = 1f;

    public float RunPower => ClassDefinition.RunPower;

    public float JumpStrength => ClassDefinition.JumpStrength;

    public float MaxRunSpeed => ClassDefinition.MaxRunSpeed;

    public float GroundAcceleration => ClassDefinition.GroundAcceleration;

    public float GroundDeceleration => ClassDefinition.GroundDeceleration;

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
    }

    public void SetClassDefinition(CharacterClassDefinition classDefinition)
    {
        ClassDefinition = classDefinition;
        Health = int.Clamp(Health, 0, MaxHealth);
        CurrentShells = int.Clamp(CurrentShells, 0, MaxShells);
        ResetPyroPrimaryStateFromCurrentAmmo();
        RemainingAirJumps = int.Min(RemainingAirJumps, MaxAirJumps);
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
}

