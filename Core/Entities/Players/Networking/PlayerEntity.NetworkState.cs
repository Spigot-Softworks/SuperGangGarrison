using System;
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
        bool isSpySuperjumping,
        float spySuperjumpHorizontalVelocity,
        int spySuperjumpCooldownTicksRemaining,
        int spyBackstabVisualTicksRemaining,
        bool isUbered,
        bool isKritzCritBoosted,
        bool isHeavyEating,
        int heavyEatTicksRemaining,
        bool isSniperScoped,
        int sniperChargeTicks,
        bool isUsingBinoculars,
        float binocularsFocusX,
        float binocularsFocusY,
        float facingDirectionX,
        float aimDirectionDegrees,
        float aimWorldX,
        float aimWorldY,
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
        bool isMedicHealing = false,
        int medicHealTargetId = -1,
        float medicUberCharge = 0f,
        bool isMedicUberReady = false,
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
        float playerScale = 1f,
        int offhandCooldownTicks = 0,
        int offhandReloadTicks = 0,
        int gibDeaths = 0,
        bool isTypingChatMessage = false)
    {
        var previousHealth = Health;
        Team = team;
        if (!string.Equals(ClassDefinition.GameplayClassId, classDefinition.GameplayClassId, StringComparison.Ordinal)
            || ClassDefinition.Id != classDefinition.Id)
        {
            SetClassDefinition(classDefinition);
        }
        else
        {
            ClassDefinition = classDefinition;
        }

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
        if (!isAlive)
        {
            ResetPassiveRegenState();
        }
        else if (Health < previousHealth)
        {
            ResetUnscathedTime();
        }
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
        GibDeaths = Math.Max(0, gibDeaths);
        Assists = Math.Max(0, assists);
        Caps = Math.Max(0, caps);
        Points = Math.Max(0f, points);
        HealPoints = Math.Max(0, healPoints);
        BadgeMask = BadgeCatalog.SanitizeBadgeMask(badgeMask);
        IsMedicHealing = isMedicHealing;
        MedicHealTargetId = medicHealTargetId >= 0 ? medicHealTargetId : null;
        MedicUberCharge = ClassId == PlayerClass.Medic
            ? float.Clamp(medicUberCharge, 0f, MedicUberMaxCharge)
            : 0f;
        IsMedicUberReady = ClassId == PlayerClass.Medic
            && (isMedicUberReady || MedicUberCharge >= MedicKritzUberReadyChargeThreshold);
        IsMedicUbering = isUbered;
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
        IsSpySuperjumping = isSpySuperjumping;
        SpySuperjumpHorizontalVelocity = spySuperjumpHorizontalVelocity;
        SpySuperjumpCooldownTicksRemaining = ClassId == PlayerClass.Spy
            ? Math.Max(0, spySuperjumpCooldownTicksRemaining)
            : 0;
        SpyBackstabWindupTicksRemaining = 0;
        SpyBackstabRecoveryTicksRemaining = 0;
        SpyBackstabVisualTicksRemaining = ClassId == PlayerClass.Spy
            ? Math.Max(0, spyBackstabVisualTicksRemaining)
            : 0;
        SpyBackstabDirectionDegrees = 0f;
        SpyBackstabHitboxPending = false;
        IsSpyVisibleToEnemies = IsSpyCloaked && SpyCloakAlpha > 0f;
        BurnIntensity = float.Clamp(burnIntensity, 0f, BurnMaxIntensity);
        BurnDurationSourceTicks = float.Max(0f, burnDurationSourceTicks);
        BurnDecayDelaySourceTicksRemaining = float.Max(0f, burnDecayDelaySourceTicksRemaining);
        BurnIntensityDecayPerSourceTick = float.Max(0f, burnIntensityDecayPerSourceTick);
        BurnedByPlayerId = burnedByPlayerId > 0 ? burnedByPlayerId : null;
        NapalmCoveredSourceTicks = 0f;
        UberTicksRemaining = isUbered ? DefaultUberRefreshTicks : 0;
        KritzCritBoostTicksRemaining = isKritzCritBoosted ? DefaultUberRefreshTicks : 0;
        IsHeavyEating = isHeavyEating;
        HeavyEatTicksRemaining = Math.Max(0, heavyEatTicksRemaining);
        ApplyObservedHeavyEatCooldown(heavyEatCooldownTicksRemaining);
        IsSniperScoped = isSniperScoped;
        SniperChargeTicks = Math.Max(0, sniperChargeTicks);
        IsUsingBinoculars = isUsingBinoculars;
        BinocularsFocusX = binocularsFocusX;
        BinocularsFocusY = binocularsFocusY;
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
        AimWorldX = aimWorldX;
        AimWorldY = aimWorldY;
        ResetSourceFacingDirectionState();
        IsTaunting = isTaunting;
        TauntFrameIndex = tauntFrameIndex;
        IsChatBubbleVisible = isChatBubbleVisible;
        ChatBubbleFrameIndex = chatBubbleFrameIndex;
        ChatBubbleAlpha = chatBubbleAlpha;
        IsTypingChatMessage = isTypingChatMessage;
        IsChatBubbleFading = false;
        ChatBubbleTicksRemaining = 0;
        MedicHealTargetId = isMedicHealing && medicHealTargetId >= 0 ? medicHealTargetId : null;
        IsMedicHealing = IsAlive && MedicHealTargetId.HasValue;

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
            MedicHealTargetId = null;
            IsMedicHealing = false;
            IsUsingBinoculars = false;
            MovementState = LegacyMovementState.None;
            ExtinguishAfterburn();
        }

        ClearRecentDamageDealers();
        if (IsUbered)
        {
            ExtinguishAfterburn();
        }

        // Pre-set the selected equipped slot so that if ApplyReplicatedGameplayLoadoutState falls back
        // to RefreshGameplayLoadoutState (e.g., strings cleared under budget pressure), it uses the
        // correct slot delivered via the movement delta rather than staying on its previous value.
        if (Enum.IsDefined(typeof(GameplayEquipmentSlot), (int)gameplayEquippedSlot))
        {
            SelectedGameplayEquippedSlot = (GameplayEquipmentSlot)gameplayEquippedSlot;
            if (SelectedGameplayEquippedSlot != GameplayEquipmentSlot.Secondary)
            {
                IsExperimentalOffhandEquipped = false;
                IsAcquiredWeaponEquipped = false;
            }
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
        ReconcileReplicatedWeaponSelection();
        RefreshMedicUberReadyState();
        ReplaceOwnedGameplayItemIds(ownedGameplayItemIds ?? []);
        ReplaceReplicatedStateEntries(replicatedStateEntries ?? []);
        HydrateNetworkReplicatedAbilityRuntimeState();
        // Apply offhand weapon animation state so recoil/reload animations are visible to other players.
        // These values arrive via the movement delta (OffhandCooldownTicks / OffhandReloadTicks) so they
        // are delivered every tick rather than only with the budget-limited full-state update.
        ExperimentalOffhandCooldownTicks = Math.Max(0, offhandCooldownTicks);
        ExperimentalOffhandReloadTicksUntilNextShell = Math.Max(0, offhandReloadTicks);
    }

    private void HydrateNetworkReplicatedAbilityRuntimeState()
    {
        HydrateNetworkReplicatedHeavyRuntimeState();
        HydrateNetworkReplicatedCivvieRuntimeState();
    }

    private void HydrateNetworkReplicatedHeavyRuntimeState()
    {
        if (ClassId == PlayerClass.Heavy
            && TryGetReplicatedStateInt(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                GameplayAbilityReplicatedState.HeavyDashCooldownTicksKey,
                out var heavyDashCooldownTicks))
        {
            ExperimentalGhostDashCooldownTicksRemaining = Math.Max(0, heavyDashCooldownTicks);
        }
        else if (ClassId != PlayerClass.Heavy)
        {
            ExperimentalGhostDashCooldownTicksRemaining = 0;
        }

        if (ClassId != PlayerClass.Quote)
        {
            return;
        }

        if (TryGetReplicatedStateInt(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                GameplayAbilityReplicatedState.CivviePogoTrickTicksKey,
                out var pogoTrickTicks))
        {
            CivviePogoTrickTicksRemaining = Math.Max(0, pogoTrickTicks);
        }
    }

    private void HydrateNetworkReplicatedCivvieRuntimeState()
    {
        if (ClassId != PlayerClass.Quote)
        {
            CivvieUmbrellaChargeTicks = CivvieUmbrellaMaxChargeTicks;
            IsCivvieUmbrellaActive = false;
            IsCivvieUmbrellaBroken = false;
            IsCivviePogoActive = false;
            CivviePogoCrunchTicksRemaining = 0;
            return;
        }

        const string CoreAbilityOwnerId = GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId;
        if (TryGetReplicatedStateInt(
                CoreAbilityOwnerId,
                GameplayAbilityReplicatedState.CivvieUmbrellaCooldownTicksKey,
                out var umbrellaCooldownTicks))
        {
            CivvieUmbrellaChargeTicks = Math.Clamp(
                CivvieUmbrellaMaxChargeTicks - Math.Max(0, umbrellaCooldownTicks),
                0,
                CivvieUmbrellaMaxChargeTicks);
        }

        if (TryGetReplicatedStateBool(
                CoreAbilityOwnerId,
                GameplayAbilityReplicatedState.CivvieUmbrellaActiveKey,
                out var umbrellaActive))
        {
            IsCivvieUmbrellaActive = umbrellaActive;
        }

        if (TryGetReplicatedStateBool(
                CoreAbilityOwnerId,
                GameplayAbilityReplicatedState.CivvieUmbrellaDisabledKey,
                out var umbrellaDisabled))
        {
            IsCivvieUmbrellaBroken = umbrellaDisabled;
            if (umbrellaDisabled)
            {
                IsCivvieUmbrellaActive = false;
            }
        }

        if (TryGetReplicatedStateBool(
                CoreAbilityOwnerId,
                GameplayAbilityReplicatedState.CivviePogoActiveKey,
                out var pogoActive))
        {
            IsCivviePogoActive = pogoActive;
        }

        if (TryGetReplicatedStateInt(
                CoreAbilityOwnerId,
                GameplayAbilityReplicatedState.CivviePogoCrunchTicksKey,
                out var pogoCrunchTicks))
        {
            CivviePogoCrunchTicksRemaining = Math.Max(0, pogoCrunchTicks);
        }
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
                GameplayClassId,
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

    private void ReconcileReplicatedWeaponSelection()
    {
        if (GameplayLoadoutState.EquippedSlot != GameplayEquipmentSlot.Secondary)
        {
            IsExperimentalOffhandEquipped = false;
            IsAcquiredWeaponEquipped = false;
            return;
        }

        var equippedItemId = GameplayLoadoutState.EquippedItemId;
        var acquiredItemId = GameplayLoadoutState.AcquiredItemId;
        var acquiredSelected = HasAcquiredWeapon
            && !string.IsNullOrWhiteSpace(acquiredItemId)
            && string.Equals(equippedItemId, acquiredItemId, StringComparison.Ordinal);
        var offhandSelected = !acquiredSelected
            && HasExperimentalOffhandWeapon
            && !string.IsNullOrWhiteSpace(GameplayLoadoutState.SecondaryItemId)
            && string.Equals(equippedItemId, GameplayLoadoutState.SecondaryItemId, StringComparison.Ordinal);

        IsAcquiredWeaponEquipped = acquiredSelected;
        IsExperimentalOffhandEquipped = offhandSelected;
    }
}
