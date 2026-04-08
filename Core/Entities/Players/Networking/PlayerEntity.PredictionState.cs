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
        string? SelectedGameplayLoadoutId = null,
        GameplayEquipmentSlot SelectedGameplayEquippedSlot = GameplayEquipmentSlot.Primary,
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
            SelectedGameplayLoadoutId,
            SelectedGameplayEquippedSlot,
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
        SelectedGameplayLoadoutId = string.IsNullOrWhiteSpace(state.SelectedGameplayLoadoutId)
            ? CharacterClassCatalog.RuntimeRegistry.GetDefaultLoadout(ClassId).Id
            : state.SelectedGameplayLoadoutId;
        SelectedGameplayEquippedSlot = state.SelectedGameplayEquippedSlot;
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

}
