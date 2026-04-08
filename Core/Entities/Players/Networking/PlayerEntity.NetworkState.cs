using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
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
        IReadOnlyList<string>? ownedGameplayItemIds = null,
        IReadOnlyList<GameplayReplicatedStateEntry>? replicatedStateEntries = null,
        float playerScale = 1f)
    {
        Team = team;
        ClassDefinition = classDefinition;
        SetPlayerScale(playerScale);
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
        ReplaceOwnedGameplayItemIds(ownedGameplayItemIds ?? []);
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

        if (CharacterClassCatalog.RuntimeRegistry.TryCreateValidatedPlayerLoadoutState(
                ClassId,
                gameplayLoadoutId,
                equippedSlot,
                string.IsNullOrWhiteSpace(gameplaySecondaryItemId) ? null : gameplaySecondaryItemId,
                string.IsNullOrWhiteSpace(gameplayAcquiredItemId) ? null : gameplayAcquiredItemId,
                out var validatedLoadoutState))
        {
            SelectedGameplayLoadoutId = validatedLoadoutState.LoadoutId;
            SelectedGameplayEquippedSlot = validatedLoadoutState.EquippedSlot;
            GameplayLoadoutState = validatedLoadoutState;
            return;
        }

        RefreshGameplayLoadoutState();
    }
}
