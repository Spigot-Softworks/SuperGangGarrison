namespace OpenGarrison.Core;

using OpenGarrison.GameplayModding;

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
        bool IsExperimentalDemoknightChargeDashActive,
        bool IsExperimentalDemoknightChargeFlightActive,
        float ExperimentalDemoknightChargeAcceleration,
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
        PrimaryWeaponDefinition? ExperimentalOffhandWeapon,
        int ExperimentalOffhandCurrentShells,
        int ExperimentalOffhandCooldownTicks,
        int ExperimentalOffhandReloadTicksUntilNextShell,
        bool IsExperimentalOffhandEquipped,
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
        float Points,
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
        bool PyroPrimaryRequiresReleaseAfterEmpty = false,
        int Assists = 0,
        ulong BadgeMask = 0,
        int? LastDamageDealerPlayerId = null,
        int LastDamageDealerAssistTicksRemaining = 0,
        int? SecondToLastDamageDealerPlayerId = null,
        int SecondToLastDamageDealerAssistTicksRemaining = 0,
        GameplayReplicatedStateEntry[]? ReplicatedStateEntries = null);

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
            IsExperimentalDemoknightChargeDashActive,
            IsExperimentalDemoknightChargeFlightActive,
            ExperimentalDemoknightChargeAcceleration,
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
            ExperimentalOffhandWeapon,
            ExperimentalOffhandCurrentShells,
            ExperimentalOffhandCooldownTicks,
            ExperimentalOffhandReloadTicksUntilNextShell,
            IsExperimentalOffhandEquipped,
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
            Points,
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
            PyroPrimaryRequiresReleaseAfterEmpty,
            Assists,
            BadgeMask,
            LastDamageDealerPlayerId,
            LastDamageDealerAssistTicksRemaining,
            SecondToLastDamageDealerPlayerId,
            SecondToLastDamageDealerAssistTicksRemaining,
            GetReplicatedStateEntries().ToArray());
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
        IsExperimentalDemoknightChargeDashActive = state.IsExperimentalDemoknightChargeDashActive;
        IsExperimentalDemoknightChargeFlightActive = state.IsExperimentalDemoknightChargeFlightActive;
        ExperimentalDemoknightChargeAcceleration = state.ExperimentalDemoknightChargeAcceleration;
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
        ExperimentalOffhandWeapon = state.ExperimentalOffhandWeapon;
        ExperimentalOffhandCurrentShells = int.Clamp(
            state.ExperimentalOffhandCurrentShells,
            0,
            state.ExperimentalOffhandWeapon?.MaxAmmo ?? 0);
        ExperimentalOffhandCooldownTicks = Math.Max(0, state.ExperimentalOffhandCooldownTicks);
        ExperimentalOffhandReloadTicksUntilNextShell = Math.Max(0, state.ExperimentalOffhandReloadTicksUntilNextShell);
        IsExperimentalOffhandEquipped = state.ExperimentalOffhandWeapon is not null && state.IsExperimentalOffhandEquipped;
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
        Points = state.Points;
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
        Assists = state.Assists;
        BadgeMask = BadgeCatalog.SanitizeBadgeMask(state.BadgeMask);
        LastDamageDealerPlayerId = state.LastDamageDealerPlayerId;
        LastDamageDealerAssistTicksRemaining = state.LastDamageDealerAssistTicksRemaining;
        SecondToLastDamageDealerPlayerId = state.SecondToLastDamageDealerPlayerId;
        SecondToLastDamageDealerAssistTicksRemaining = state.SecondToLastDamageDealerAssistTicksRemaining;
        ReplaceReplicatedStateEntries(state.ReplicatedStateEntries ?? []);
        RefreshGameplayLoadoutState();
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
        float points,
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
        int heavyEatCooldownTicksRemaining = 0,
        int assists = 0,
        ulong badgeMask = 0,
        string gameplayModPackId = "",
        string gameplayLoadoutId = "",
        string gameplayPrimaryItemId = "",
        string gameplaySecondaryItemId = "",
        string gameplayUtilityItemId = "",
        byte gameplayEquippedSlot = 0,
        string gameplayEquippedItemId = "",
        string gameplayAcquiredItemId = "",
        IReadOnlyList<GameplayReplicatedStateEntry>? replicatedStateEntries = null)
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
        IsExperimentalDemoknightChargeDashActive = false;
        IsExperimentalDemoknightChargeFlightActive = false;
        ExperimentalDemoknightChargeAcceleration = 0f;
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
        Assists = Math.Max(0, assists);
        Caps = Math.Max(0, caps);
        Points = Math.Max(0f, points);
        HealPoints = Math.Max(0, healPoints);
        BadgeMask = BadgeCatalog.SanitizeBadgeMask(badgeMask);
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

        ClearRecentDamageDealers();
        if (IsUbered)
        {
            ExtinguishAfterburn();
        }

        ApplyReplicatedGameplayLoadoutState(
            gameplayModPackId,
            gameplayLoadoutId,
            gameplayPrimaryItemId,
            gameplaySecondaryItemId,
            gameplayUtilityItemId,
            gameplayEquippedSlot,
            gameplayEquippedItemId,
            gameplayAcquiredItemId);
        ReplaceReplicatedStateEntries(replicatedStateEntries ?? []);
    }

    private void ApplyReplicatedGameplayLoadoutState(
        string gameplayModPackId,
        string gameplayLoadoutId,
        string gameplayPrimaryItemId,
        string gameplaySecondaryItemId,
        string gameplayUtilityItemId,
        byte gameplayEquippedSlot,
        string gameplayEquippedItemId,
        string gameplayAcquiredItemId)
    {
        if (string.IsNullOrWhiteSpace(gameplayModPackId)
            || string.IsNullOrWhiteSpace(gameplayLoadoutId)
            || string.IsNullOrWhiteSpace(gameplayPrimaryItemId)
            || string.IsNullOrWhiteSpace(gameplayEquippedItemId))
        {
            RefreshGameplayLoadoutState();
            return;
        }

        var equippedSlot = Enum.IsDefined(typeof(GameplayEquipmentSlot), (int)gameplayEquippedSlot)
            ? (GameplayEquipmentSlot)gameplayEquippedSlot
            : GameplayEquipmentSlot.Primary;

        GameplayLoadoutState = new GameplayPlayerLoadoutState(
            ModPackId: gameplayModPackId,
            ClassId: CharacterClassCatalog.RuntimeRegistry.GetRequiredClassBinding(ClassId).ClassId,
            LoadoutId: gameplayLoadoutId,
            PrimaryItemId: gameplayPrimaryItemId,
            SecondaryItemId: string.IsNullOrWhiteSpace(gameplaySecondaryItemId) ? null : gameplaySecondaryItemId,
            UtilityItemId: string.IsNullOrWhiteSpace(gameplayUtilityItemId) ? null : gameplayUtilityItemId,
            EquippedSlot: equippedSlot,
            EquippedItemId: gameplayEquippedItemId,
            AcquiredItemId: string.IsNullOrWhiteSpace(gameplayAcquiredItemId) ? null : gameplayAcquiredItemId);
    }
}
