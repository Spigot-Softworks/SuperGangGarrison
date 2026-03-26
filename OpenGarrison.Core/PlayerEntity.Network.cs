namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    internal readonly record struct PredictionState(
        PlayerTeam Team,
        CharacterClassDefinition ClassDefinition,
        bool IsAlive,
        float X,
        float Y,
        float HorizontalSpeed,
        float VerticalSpeed,
        float LegacyStateTickAccumulator,
        LegacyMovementState MovementState,
        bool IsGrounded,
        int Health,
        float Metal,
        bool IsCarryingIntel,
        int IntelPickupCooldownTicks,
        float IntelRechargeTicks,
        bool IsInSpawnRoom,
        int RemainingAirJumps,
        float FacingDirectionX,
        float AimDirectionDegrees,
        float SourceFacingDirectionX,
        float PreviousSourceFacingDirectionX,
        int CurrentShells,
        int PrimaryCooldownTicks,
        int ReloadTicksUntilNextShell,
        float ContinuousDamageAccumulator,
        bool IsHeavyEating,
        int HeavyEatTicksRemaining,
        int HeavyEatCooldownTicksRemaining,
        float HeavyHealingAccumulator,
        bool IsTaunting,
        float TauntFrameIndex,
        bool IsSniperScoped,
        int SniperChargeTicks,
        int UberTicksRemaining,
        int? MedicHealTargetId,
        bool IsMedicHealing,
        float MedicUberCharge,
        bool IsMedicUberReady,
        bool IsMedicUbering,
        int MedicNeedleCooldownTicks,
        int MedicNeedleRefillTicks,
        float ContinuousHealingAccumulator,
        int QuoteBubbleCount,
        int QuoteBladesOut,
        int PyroAirblastCooldownTicks,
        bool IsSpyCloaked,
        float SpyCloakAlpha,
        int SpyBackstabWindupTicksRemaining,
        int SpyBackstabRecoveryTicksRemaining,
        int SpyBackstabVisualTicksRemaining,
        float SpyBackstabDirectionDegrees,
        bool SpyBackstabHitboxPending,
        bool IsSpyVisibleToEnemies,
        float BurnIntensity,
        float BurnDurationSourceTicks,
        float BurnDecayDelaySourceTicksRemaining,
        float BurnIntensityDecayPerSourceTick,
        int? BurnedByPlayerId,
        int Kills,
        int Deaths,
        int Caps,
        int HealPoints,
        int ActiveDominationCount,
        bool IsDominatingLocalViewer,
        bool IsDominatedByLocalViewer,
        bool IsChatBubbleVisible,
        int ChatBubbleFrameIndex,
        float ChatBubbleAlpha,
        bool IsChatBubbleFading,
        int ChatBubbleTicksRemaining,
        int PyroFlareCooldownTicks = 0,
        int PyroPrimaryFuelScaled = 0,
        bool IsPyroPrimaryRefilling = false,
        int PyroFlameLoopTicksRemaining = 0,
        bool PyroPrimaryRequiresReleaseAfterEmpty = false);

    internal PredictionState CapturePredictionState()
    {
        return new PredictionState(
            Team,
            ClassDefinition,
            IsAlive,
            X,
            Y,
            HorizontalSpeed,
            VerticalSpeed,
            LegacyStateTickAccumulator,
            MovementState,
            IsGrounded,
            Health,
            Metal,
            IsCarryingIntel,
            IntelPickupCooldownTicks,
            IntelRechargeTicks,
            IsInSpawnRoom,
            RemainingAirJumps,
            FacingDirectionX,
            AimDirectionDegrees,
            SourceFacingDirectionX,
            PreviousSourceFacingDirectionX,
            CurrentShells,
            PrimaryCooldownTicks,
            ReloadTicksUntilNextShell,
            ContinuousDamageAccumulator,
            IsHeavyEating,
            HeavyEatTicksRemaining,
            HeavyEatCooldownTicksRemaining,
            HeavyHealingAccumulator,
            IsTaunting,
            TauntFrameIndex,
            IsSniperScoped,
            SniperChargeTicks,
            UberTicksRemaining,
            MedicHealTargetId,
            IsMedicHealing,
            MedicUberCharge,
            IsMedicUberReady,
            IsMedicUbering,
            MedicNeedleCooldownTicks,
            MedicNeedleRefillTicks,
            ContinuousHealingAccumulator,
            QuoteBubbleCount,
            QuoteBladesOut,
            PyroAirblastCooldownTicks,
            IsSpyCloaked,
            SpyCloakAlpha,
            SpyBackstabWindupTicksRemaining,
            SpyBackstabRecoveryTicksRemaining,
            SpyBackstabVisualTicksRemaining,
            SpyBackstabDirectionDegrees,
            SpyBackstabHitboxPending,
            IsSpyVisibleToEnemies,
            BurnIntensity,
            BurnDurationSourceTicks,
            BurnDecayDelaySourceTicksRemaining,
            BurnIntensityDecayPerSourceTick,
            BurnedByPlayerId,
            Kills,
            Deaths,
            Caps,
            HealPoints,
            ActiveDominationCount,
            IsDominatingLocalViewer,
            IsDominatedByLocalViewer,
            IsChatBubbleVisible,
            ChatBubbleFrameIndex,
            ChatBubbleAlpha,
            IsChatBubbleFading,
            ChatBubbleTicksRemaining,
            PyroFlareCooldownTicks,
            PyroPrimaryFuelScaled,
            IsPyroPrimaryRefilling,
            PyroFlameLoopTicksRemaining,
            PyroPrimaryRequiresReleaseAfterEmpty);
    }

    internal void RestorePredictionState(in PredictionState state)
    {
        Team = state.Team;
        ClassDefinition = state.ClassDefinition;
        IsAlive = state.IsAlive;
        X = state.X;
        Y = state.Y;
        HorizontalSpeed = state.HorizontalSpeed;
        VerticalSpeed = state.VerticalSpeed;
        LegacyStateTickAccumulator = state.LegacyStateTickAccumulator;
        MovementState = state.MovementState;
        IsGrounded = state.IsGrounded;
        Health = state.Health;
        Metal = state.Metal;
        IsCarryingIntel = state.IsCarryingIntel;
        IntelPickupCooldownTicks = state.IntelPickupCooldownTicks;
        IntelRechargeTicks = float.Clamp(state.IntelRechargeTicks, 0f, IntelRechargeMaxTicks);
        IsInSpawnRoom = state.IsInSpawnRoom;
        RemainingAirJumps = state.RemainingAirJumps;
        FacingDirectionX = state.FacingDirectionX;
        AimDirectionDegrees = state.AimDirectionDegrees;
        SourceFacingDirectionX = state.SourceFacingDirectionX;
        PreviousSourceFacingDirectionX = state.PreviousSourceFacingDirectionX;
        CurrentShells = state.CurrentShells;
        PrimaryCooldownTicks = state.PrimaryCooldownTicks;
        ReloadTicksUntilNextShell = state.ReloadTicksUntilNextShell;
        ContinuousDamageAccumulator = state.ContinuousDamageAccumulator;
        IsHeavyEating = state.IsHeavyEating;
        HeavyEatTicksRemaining = state.HeavyEatTicksRemaining;
        HeavyEatCooldownTicksRemaining = state.HeavyEatCooldownTicksRemaining;
        HeavyHealingAccumulator = state.HeavyHealingAccumulator;
        IsTaunting = state.IsTaunting;
        TauntFrameIndex = state.TauntFrameIndex;
        IsSniperScoped = state.IsSniperScoped;
        SniperChargeTicks = state.SniperChargeTicks;
        UberTicksRemaining = state.UberTicksRemaining;
        MedicHealTargetId = state.MedicHealTargetId;
        IsMedicHealing = state.IsMedicHealing;
        MedicUberCharge = state.MedicUberCharge;
        IsMedicUberReady = state.IsMedicUberReady;
        IsMedicUbering = state.IsMedicUbering;
        MedicNeedleCooldownTicks = state.MedicNeedleCooldownTicks;
        MedicNeedleRefillTicks = state.MedicNeedleRefillTicks;
        ContinuousHealingAccumulator = state.ContinuousHealingAccumulator;
        QuoteBubbleCount = state.QuoteBubbleCount;
        QuoteBladesOut = state.QuoteBladesOut;
        PyroAirblastCooldownTicks = state.PyroAirblastCooldownTicks;
        PyroFlareCooldownTicks = state.PyroFlareCooldownTicks;
        IsSpyCloaked = state.IsSpyCloaked;
        SpyCloakAlpha = float.Clamp(state.SpyCloakAlpha, 0f, 1f);
        SpyBackstabWindupTicksRemaining = state.SpyBackstabWindupTicksRemaining;
        SpyBackstabRecoveryTicksRemaining = state.SpyBackstabRecoveryTicksRemaining;
        SpyBackstabVisualTicksRemaining = state.SpyBackstabVisualTicksRemaining;
        SpyBackstabDirectionDegrees = state.SpyBackstabDirectionDegrees;
        SpyBackstabHitboxPending = state.SpyBackstabHitboxPending;
        IsSpyVisibleToEnemies = state.IsSpyVisibleToEnemies;
        BurnIntensity = float.Clamp(state.BurnIntensity, 0f, BurnMaxIntensity);
        BurnDurationSourceTicks = float.Max(0f, state.BurnDurationSourceTicks);
        BurnDecayDelaySourceTicksRemaining = float.Max(0f, state.BurnDecayDelaySourceTicksRemaining);
        BurnIntensityDecayPerSourceTick = float.Max(0f, state.BurnIntensityDecayPerSourceTick);
        BurnedByPlayerId = state.BurnedByPlayerId;
        Kills = state.Kills;
        Deaths = state.Deaths;
        Caps = state.Caps;
        HealPoints = state.HealPoints;
        ActiveDominationCount = state.ActiveDominationCount;
        IsDominatingLocalViewer = state.IsDominatingLocalViewer;
        IsDominatedByLocalViewer = state.IsDominatedByLocalViewer;
        IsChatBubbleVisible = state.IsChatBubbleVisible;
        ChatBubbleFrameIndex = state.ChatBubbleFrameIndex;
        ChatBubbleAlpha = state.ChatBubbleAlpha;
        IsChatBubbleFading = state.IsChatBubbleFading;
        ChatBubbleTicksRemaining = state.ChatBubbleTicksRemaining;
        PyroPrimaryFuelScaledValue = state.PyroPrimaryFuelScaled;
        IsPyroPrimaryRefilling = state.IsPyroPrimaryRefilling;
        PyroFlameLoopTicksRemaining = state.PyroFlameLoopTicksRemaining;
        PyroPrimaryRequiresReleaseAfterEmpty = state.PyroPrimaryRequiresReleaseAfterEmpty;
    }

    public void ApplyNetworkState(
        PlayerTeam team,
        CharacterClassDefinition classDefinition,
        bool isAlive,
        float x,
        float y,
        float horizontalSpeed,
        float verticalSpeed,
        int health,
        int currentShells,
        int kills,
        int deaths,
        int caps,
        int healPoints,
        int activeDominationCount,
        bool isDominatingLocalViewer,
        bool isDominatedByLocalViewer,
        float metal,
        bool isGrounded,
        int remainingAirJumps,
        bool isCarryingIntel,
        float intelRechargeTicks,
        bool isSpyCloaked,
        float spyCloakAlpha,
        bool isUbered,
        bool isHeavyEating,
        int heavyEatTicksRemaining,
        bool isSniperScoped,
        int sniperChargeTicks,
        float facingDirectionX,
        float aimDirectionDegrees,
        bool isTaunting,
        float tauntFrameIndex,
        bool isChatBubbleVisible,
        int chatBubbleFrameIndex,
        float chatBubbleAlpha,
        float burnIntensity = 0f,
        float burnDurationSourceTicks = 0f,
        float burnDecayDelaySourceTicksRemaining = 0f,
        float burnIntensityDecayPerSourceTick = 0f,
        int burnedByPlayerId = -1,
        byte movementState = (byte)LegacyMovementState.None,
        int primaryCooldownTicks = 0,
        int reloadTicksUntilNextShell = 0,
        int medicNeedleCooldownTicks = 0,
        int medicNeedleRefillTicks = 0,
        int pyroAirblastCooldownTicks = 0,
        int pyroFlareCooldownTicks = 0,
        int pyroPrimaryFuelScaled = 0,
        bool isPyroPrimaryRefilling = false,
        int pyroFlameLoopTicksRemaining = 0,
        bool pyroPrimaryRequiresReleaseAfterEmpty = false,
        int heavyEatCooldownTicksRemaining = 0)
    {
        Team = team;
        ClassDefinition = classDefinition;
        X = x;
        Y = y;
        HorizontalSpeed = horizontalSpeed;
        VerticalSpeed = verticalSpeed;
        LegacyStateTickAccumulator = 0f;
        MovementState = movementState <= (byte)LegacyMovementState.FriendlyJuggle
            ? (LegacyMovementState)movementState
            : LegacyMovementState.None;
        IsGrounded = isGrounded;
        IsAlive = isAlive;
        Health = int.Clamp(health, 0, MaxHealth);
        CurrentShells = int.Clamp(currentShells, 0, MaxShells);
        if (ClassId == PlayerClass.Pyro)
        {
            PyroPrimaryFuelScaledValue = int.Clamp(
                pyroPrimaryFuelScaled > 0 ? pyroPrimaryFuelScaled : CurrentShells * PyroPrimaryFuelScale,
                0,
                GetPyroPrimaryFuelMaxScaled());
            CurrentShells = int.Clamp(PyroPrimaryFuelScaledValue / PyroPrimaryFuelScale, 0, MaxShells);
            IsPyroPrimaryRefilling = isPyroPrimaryRefilling;
            PyroFlameLoopTicksRemaining = Math.Max(0, pyroFlameLoopTicksRemaining);
            PyroPrimaryRequiresReleaseAfterEmpty = pyroPrimaryRequiresReleaseAfterEmpty;
        }
        else
        {
            PyroPrimaryFuelScaledValue = 0;
            IsPyroPrimaryRefilling = false;
            PyroFlameLoopTicksRemaining = 0;
            PyroPrimaryRequiresReleaseAfterEmpty = false;
        }
        PrimaryCooldownTicks = Math.Max(0, primaryCooldownTicks);
        ReloadTicksUntilNextShell = Math.Max(0, reloadTicksUntilNextShell);
        MedicNeedleCooldownTicks = ClassId == PlayerClass.Medic
            ? Math.Max(0, medicNeedleCooldownTicks)
            : 0;
        MedicNeedleRefillTicks = ClassId == PlayerClass.Medic
            ? Math.Max(0, medicNeedleRefillTicks)
            : 0;
        Kills = Math.Max(0, kills);
        Deaths = Math.Max(0, deaths);
        Caps = Math.Max(0, caps);
        HealPoints = Math.Max(0, healPoints);
        ActiveDominationCount = Math.Max(0, activeDominationCount);
        IsDominatingLocalViewer = isDominatingLocalViewer;
        IsDominatedByLocalViewer = isDominatedByLocalViewer;
        Metal = float.Clamp(metal, 0f, MaxMetal);
        RemainingAirJumps = IsAlive
            ? (isGrounded ? MaxAirJumps : int.Clamp(remainingAirJumps, 0, MaxAirJumps))
            : MaxAirJumps;
        IsCarryingIntel = isCarryingIntel;
        IntelRechargeTicks = isCarryingIntel ? float.Clamp(intelRechargeTicks, 0f, IntelRechargeMaxTicks) : 0f;
        IsSpyCloaked = isSpyCloaked;
        SpyCloakAlpha = float.Clamp(spyCloakAlpha, 0f, 1f);
        SpyBackstabWindupTicksRemaining = 0;
        SpyBackstabRecoveryTicksRemaining = 0;
        SpyBackstabVisualTicksRemaining = 0;
        SpyBackstabDirectionDegrees = 0f;
        SpyBackstabHitboxPending = false;
        IsSpyVisibleToEnemies = IsSpyCloaked && SpyCloakAlpha > 0f;
        BurnIntensity = float.Clamp(burnIntensity, 0f, BurnMaxIntensity);
        BurnDurationSourceTicks = float.Max(0f, burnDurationSourceTicks);
        BurnDecayDelaySourceTicksRemaining = float.Max(0f, burnDecayDelaySourceTicksRemaining);
        BurnIntensityDecayPerSourceTick = float.Max(0f, burnIntensityDecayPerSourceTick);
        BurnedByPlayerId = burnedByPlayerId > 0 ? burnedByPlayerId : null;
        UberTicksRemaining = isUbered ? DefaultUberRefreshTicks : 0;
        IsHeavyEating = isHeavyEating;
        HeavyEatTicksRemaining = Math.Max(0, heavyEatTicksRemaining);
        HeavyEatCooldownTicksRemaining = ClassId == PlayerClass.Heavy
            ? Math.Max(0, heavyEatCooldownTicksRemaining)
            : 0;
        IsSniperScoped = isSniperScoped;
        SniperChargeTicks = Math.Max(0, sniperChargeTicks);
        if (!IsHeavyEating)
        {
            HeavyHealingAccumulator = 0f;
        }
        if (ClassId != PlayerClass.Quote)
        {
            QuoteBubbleCount = 0;
            QuoteBladesOut = 0;
        }
        PyroAirblastCooldownTicks = ClassId == PlayerClass.Pyro
            ? Math.Max(0, pyroAirblastCooldownTicks)
            : 0;
        PyroFlareCooldownTicks = ClassId == PlayerClass.Pyro
            ? Math.Max(0, pyroFlareCooldownTicks)
            : 0;
        FacingDirectionX = facingDirectionX;
        AimDirectionDegrees = aimDirectionDegrees;
        ResetSourceFacingDirectionState();
        IsTaunting = isTaunting;
        TauntFrameIndex = tauntFrameIndex;
        IsChatBubbleVisible = isChatBubbleVisible;
        ChatBubbleFrameIndex = chatBubbleFrameIndex;
        ChatBubbleAlpha = chatBubbleAlpha;
        IsChatBubbleFading = false;
        ChatBubbleTicksRemaining = 0;

        if (!IsChatBubbleVisible)
        {
            ChatBubbleFrameIndex = 0;
            ChatBubbleAlpha = 0f;
        }

        if (!IsAlive)
        {
            Health = 0;
            PrimaryCooldownTicks = 0;
            ReloadTicksUntilNextShell = 0;
            MedicNeedleCooldownTicks = 0;
            MedicNeedleRefillTicks = 0;
            IsPyroPrimaryRefilling = false;
            PyroFlameLoopTicksRemaining = 0;
            PyroPrimaryRequiresReleaseAfterEmpty = false;
            IsCarryingIntel = false;
            IntelRechargeTicks = 0f;
            IsSniperScoped = false;
            SniperChargeTicks = 0;
            MovementState = LegacyMovementState.None;
            ExtinguishAfterburn();
        }

        if (IsUbered)
        {
            ExtinguishAfterburn();
        }
    }
}
