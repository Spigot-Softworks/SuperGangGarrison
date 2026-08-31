using System;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    /// <summary>
    /// Applies the deliberately small, self-contained player state carried by
    /// protocol 64.  This is separate from the legacy snapshot hydrator: the
    /// protocol-64 record does not claim to contain score, inventory, or
    /// ability-runtime fields, so those fields must not be reset here.
    /// </summary>
    public void ApplyProtocol64State(
        Protocol64PlayerState state,
        CharacterClassDefinition classDefinition,
        int ticksPerSecond = SimulationConfig.DefaultTicksPerSecond)
    {
        Team = (PlayerTeam)state.Team;
        if (!string.Equals(ClassDefinition.GameplayClassId, classDefinition.GameplayClassId, StringComparison.Ordinal)
            || ClassDefinition.Id != classDefinition.Id)
        {
            SetClassDefinition(classDefinition);
        }

        HydrateProtocol64LastToDieWeaponProfileState(state.LastToDieSpyRevolverState);
        HydrateProtocol64LastToDieSniperExtensionState(state.LastToDieSniperExtensionState);
        HydrateProtocol64LastToDieSniperRuntimeState(state.LastToDieSniperRuntimeState);
        HydrateProtocol64LastToDieMedicLinkState(state.LastToDieMedicLinkState);
        HydrateProtocol64LastToDieSpyInfiltrateState(
            state.LastToDieSpyInfiltrateState,
            ticksPerSecond);
        HydrateProtocol64LastToDieSpyAfterlifeState(
            state.LastToDieSpyAfterlifeState,
            ticksPerSecond);
        HydrateProtocol64LastToDieSniperVolleyState(
            state.LastToDieSniperVolleyState is { } volley
                ? new LastToDieSniperVolleyState(
                    volley.QueuedArrowCount,
                    volley.DueArrowCount,
                    volley.SourceTicksUntilNextArrow,
                    volley.VelocityX,
                    volley.VelocityY,
                    volley.Damage,
                    volley.FakeSpeedMultiplier,
                    DecodeLastToDieSniperArrowPayload(
                        volley.PayloadFlags,
                        volley.PoisonDamagePerSecond,
                        volley.GhostDamageMultiplier,
                        volley.CriticalDamageMultiplier))
                : default);
        HydrateProtocol64LastToDieMedicHailMaryTicks(
            state.LastToDieMedicHailMaryTicksRemaining);
        HydrateProtocol64ServerStunTicks(state.ServerStunTicksRemaining);
        HydrateKritzCritBoost(
            state.KritzCritBoostTicksRemaining > 0,
            state.KritzCritBoostTicksRemaining,
            state.KritzCritBoostProviderPlayerId,
            state.KritzCritBoostProviderSlot,
            state.KritzCritBoostDamageMultiplier);
        HydrateDispenserBuff(
            state.IsDispenserBuffed,
            state.DispenserAttackReloadSpeedMultiplier);
        ApplyNetworkMaxHealth(state.MaxHealth);

        // Protocol 64 intentionally does not carry the full legacy loadout
        // snapshot. A class change therefore clears the local secondary
        // definition before the first authoritative ammo update arrives. The
        // authoritative max value tells us that this player has a secondary;
        // hydrate the class's default secondary/weapon utility so its counter
        // has a concrete definition to render against.
        if (state.OffhandMaxAmmo > 0 && !HasExperimentalOffhandWeapon)
        {
            HydrateProtocol64DefaultSecondaryWeapon();
        }

        // Protocol 64 is the authoritative input/state stream on the online
        // transport. Its ActiveWeapon field is the only copy of the selected
        // equipment slot on that path; without applying it here, a legacy
        // snapshot (or a class-change reset) leaves the client on Primary even
        // while the server is retaining a locked alternate primary.
        ApplyProtocol64ActiveWeapon(state.ActiveWeapon);

        X = state.X;
        Y = state.Y;
        HorizontalSpeed = state.VelocityX;
        VerticalSpeed = state.VelocityY;
        IsGrounded = state.IsGrounded;
        RemainingAirJumps = Math.Max(0, state.RemainingAirJumps);
        IsAlive = state.IsAlive;
        HydrateMedicUberDeliveryState(state.MedicUberDeliveryState);
        Health = state.IsAlive
            ? int.Clamp(state.Health, 0, MaxHealth)
            : 0;
        HydrateNetworkRageState(
            state.RageCharge,
            state.IsRageReady,
            state.RageTicksRemaining);
        HydrateBuffBannerState(
            state.BuffBannerChargeKills,
            state.BuffBannerDeployTicksRemaining,
            state.BuffBannerActiveTicksRemaining);
        MedicUberCharge = ClassId == PlayerClass.Medic
            ? float.Clamp(state.MedicUberCharge, 0f, MedicUberMaxCharge)
            : 0f;
        IsMedicUberReady = ClassId == PlayerClass.Medic
            && !IsMedicUbering
            && MedicUberCharge >= GetMedicUberReadyChargeThreshold();
        MedicHealTargetId = ClassId == PlayerClass.Medic && state.MedicHealTargetId >= 0
            ? state.MedicHealTargetId
            : null;
        IsMedicHealing = MedicHealTargetId.HasValue;
        IsSpyCloaked = ClassId == PlayerClass.Spy && state.IsSpyCloaked;
        SpyCloakAlpha = ClassId == PlayerClass.Spy
            ? float.Clamp(state.SpyCloakAlpha, 0f, 1f)
            : 1f;
        HydrateLastToDieProfessionalFireChordState(
            ClassId == PlayerClass.Spy
                ? state.LastToDieProfessionalFireChordState
                : (byte)0);
        IsSpyVisibleToEnemies = ComputeSpyVisibleToEnemies(
            IsSpyCloaked,
            SpyCloakAlpha,
            SpyBackstabVisualTicksRemaining);
        HydrateLastToDieSpyCloakMeter(
            state.LastToDieSpyCloakMeterUnits,
            global::OpenGarrison.Core.LastToDie.LastToDieDerivedModifiers.SpyCloakMeterDurationSeconds
                * Math.Max(1, ticksPerSecond)
                * global::OpenGarrison.Core.LastToDie.LastToDieDerivedModifiers.SpyCloakMeterUnitsPerTick,
            state.LastToDieSpyRogueRampStacks,
            state.LastToDieSpyRogueRampTicks);
        HydrateSpyJumpBootState(
            state.IsSpySuperjumping,
            state.SpySuperjumpHorizontalVelocity,
            state.SpySuperjumpCooldownTicksRemaining,
            state.SpySuperjumpAvailableCharges,
            state.SpySuperjumpMaximumCharges,
            state.SpySuperjumpChargeTicks,
            state.SpySuperjumpChargeDirectionDegrees,
            state.SpySuperjumpChargeStartMovementButtons,
            state.SpySuperjumpChargeStartBlockedUntilAbilityRelease);
        // The stock Medic M2 needlegun is presented as a secondary ability,
        // but its authoritative ammo lives in PlayerEntity.CurrentShells.
        if (state.MaxAmmo > 0)
        {
            CurrentShells = int.Clamp(state.CurrentAmmo, 0, Math.Min(MaxShells, state.MaxAmmo));
        }

        // Protocol 64 is also the prediction reconciliation baseline. Keep
        // the primary timers authoritative just like the compact secondary
        // timers below; otherwise every rebuild can briefly see a ready gun
        // and terminate/restart its reload animation.
        PrimaryCooldownTicks = Math.Max(0, state.PrimaryCooldownTicks);
        ReloadTicksUntilNextShell = Math.Max(0, state.PrimaryReloadTicks);

        if (HasAcquiredWeapon && state.AcquiredMaxAmmo > 0)
        {
            AcquiredWeaponCurrentShells = int.Clamp(
                state.AcquiredAmmo,
                0,
                Math.Min(AcquiredWeaponMaxShells, state.AcquiredMaxAmmo));
            AcquiredWeaponCooldownTicks = Math.Max(0, state.AcquiredCooldownTicks);
            AcquiredWeaponReloadTicksUntilNextShell = Math.Max(0, state.AcquiredReloadTicks);
        }

        if (HasPyroWeaponAvailable)
        {
            SetPyroPrimaryFuelScaled(state.PyroPrimaryFuelScaled);
        }

        if (ClassId == PlayerClass.Medic || AcquiredWeaponClassId == PlayerClass.Medic)
        {
            MedicNeedleCooldownTicks = Math.Max(0, state.MedicNeedleCooldownTicks);
            MedicNeedleRefillTicks = Math.Max(0, state.MedicNeedleRefillTicks);
        }

        // Protocol 64 does not carry the legacy SnapshotMessage, so keep the
        // experimental secondary weapon's live ammo/timing on the canonical
        // player record as well. The HUD and weapon presentation both consume
        // these properties when the QUIC path is active.
        if (HasExperimentalOffhandWeapon && state.OffhandMaxAmmo > 0)
        {
            ExperimentalOffhandCurrentShells = int.Clamp(
                state.OffhandAmmo,
                0,
                Math.Min(ExperimentalOffhandMaxShells, state.OffhandMaxAmmo));
            ExperimentalOffhandCooldownTicks = Math.Max(0, state.OffhandCooldownTicks);
            ExperimentalOffhandReloadTicksUntilNextShell = Math.Max(0, state.OffhandReloadTicks);
        }

        if (!state.IsAlive)
        {
            ResetPassiveRegenState();
            ClearMedicHealingTarget();
        }
    }

    private void ApplyProtocol64ActiveWeapon(byte activeWeapon)
    {
        if (!Enum.IsDefined(typeof(GameplayEquipmentSlot), (int)activeWeapon))
        {
            return;
        }

        var equippedSlot = (GameplayEquipmentSlot)activeWeapon;
        if (!CharacterClassCatalog.RuntimeRegistry.CanEquipSlot(
                GameplayClassId,
                SelectedGameplayLoadoutId,
                equippedSlot,
                ResolveRegisteredWeaponItemId(ExperimentalOffhandWeapon),
                GameplayLoadoutState.AcquiredItemId))
        {
            return;
        }

        SelectedGameplayEquippedSlot = equippedSlot;
        if (equippedSlot != GameplayEquipmentSlot.Secondary)
        {
            IsExperimentalOffhandEquipped = false;
            IsAcquiredWeaponEquipped = false;
        }

        RefreshGameplayLoadoutState();
    }

    private void HydrateProtocol64LastToDieWeaponProfileState(ushort encoded)
    {
        if (ClassId == PlayerClass.Spy && encoded != 0)
        {
            SetReplicatedStateInt(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSpyRevolverProfileReplicatedStateKey,
                encoded);
        }
        else
        {
            ClearReplicatedState(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSpyRevolverProfileReplicatedStateKey);
        }

        if (ClassId == PlayerClass.Sniper && encoded != 0)
        {
            SetReplicatedStateInt(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSniperProfileReplicatedStateKey,
                encoded);
        }
        else
        {
            ClearReplicatedState(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSniperProfileReplicatedStateKey);
        }

        // This applies the class-specific profile before Protocol64 state clamps
        // authoritative ammo and charge values below.
        RefreshLastToDieWeaponProfileFromReplicatedStateEntries();
    }

    private void HydrateProtocol64DefaultSecondaryWeapon()
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        var candidateItemIds = new[]
        {
            GameplayLoadoutState.SecondaryItemId,
            GameplayLoadoutState.UtilityItemId,
        };

        foreach (var itemId in candidateItemIds)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                continue;
            }

            var item = runtimeRegistry.GetRequiredItem(itemId);
            if (!runtimeRegistry.TryGetPrimaryWeaponBinding(item.BehaviorId, out _))
            {
                continue;
            }

            SetExperimentalOffhandWeapon(runtimeRegistry.CreatePrimaryWeaponDefinition(item));
            if (HasExperimentalOffhandWeapon)
            {
                return;
            }
        }
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
        bool isTypingChatMessage = false,
        int networkMaxHealth = 0,
        byte medicUberDeliveryState = 0,
        int kritzCritBoostProviderPlayerId = 0,
        int kritzCritBoostProviderSlot = int.MaxValue,
        float kritzCritBoostDamageMultiplier = 1f,
        bool isDispenserBuffed = false,
        float dispenserAttackReloadSpeedMultiplier = 1f)
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

        ApplyNetworkMaxHealth(networkMaxHealth);

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
        if (!isAlive)
        {
            BlockedJumpRetrySuppressionTicksRemaining = 0;
        }
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
        HydrateMedicUberDeliveryState(
            medicUberDeliveryState != 0
                ? medicUberDeliveryState
                : ClassId == PlayerClass.Medic && isUbered
                    ? (byte)0x81
                    : MedicUberDeliveryState);
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
        IsSpyVisibleToEnemies = ComputeSpyVisibleToEnemies(
            IsSpyCloaked,
            SpyCloakAlpha,
            SpyBackstabVisualTicksRemaining);
        BurnIntensity = float.Clamp(burnIntensity, 0f, BurnMaxIntensity);
        BurnDurationSourceTicks = float.Max(0f, burnDurationSourceTicks);
        BurnDecayDelaySourceTicksRemaining = float.Max(0f, burnDecayDelaySourceTicksRemaining);
        BurnIntensityDecayPerSourceTick = float.Max(0f, burnIntensityDecayPerSourceTick);
        BurnedByPlayerId = burnedByPlayerId > 0 ? burnedByPlayerId : null;
        NapalmCoveredSourceTicks = 0f;
        UberTicksRemaining = isUbered ? DefaultUberRefreshTicks : 0;
        HydrateKritzCritBoost(
            isKritzCritBoosted,
            DefaultUberRefreshTicks,
            kritzCritBoostProviderPlayerId,
            kritzCritBoostProviderSlot,
            kritzCritBoostDamageMultiplier);
        HydrateDispenserBuff(
            isDispenserBuffed,
            dispenserAttackReloadSpeedMultiplier);
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
        ReplaceOwnedGameplayItemIds(ownedGameplayItemIds ?? []);
        ApplyReplicatedAcquiredWeaponState(gameplayAcquiredItemId);
        // ApplySnapshot receives a resolved full snapshot, including an intentionally empty
        // replicated-state list when a delta/status update removes the last runtime state.
        // Replacing the list is therefore required; skipping an empty list leaves stale
        // offhand ammo (and availability) visible on the client.
        ReplaceReplicatedStateEntries(replicatedStateEntries ?? []);
        // The LTD weapon profile is carried in replicated state and may raise the
        // authoritative clip above the stock class definition (Agent: 9 rounds).
        // Reapply ammo only after that profile has hydrated to avoid clamping it to 6.
        CurrentShells = int.Clamp(currentShells, 0, MaxShells);

        // Hydrate offhand weapon definitions before reconciling selection so Secondary-slot
        // snapshots (soldier shotgun / scout nailgun / sniper bow) can mark the offhand equipped.
        HydrateNetworkReplicatedSecondaryWeaponFromSnapshot();
        ReconcileReplicatedWeaponSelection();
        RefreshMedicUberReadyState();
        HydrateNetworkReplicatedAbilityRuntimeState();
        // Apply offhand weapon animation state so recoil/reload animations are visible to other players.
        // These values arrive via the movement delta (OffhandCooldownTicks / OffhandReloadTicks) so they
        // are delivered every tick rather than only with the budget-limited full-state update.
        ExperimentalOffhandCooldownTicks = Math.Max(0, offhandCooldownTicks);
        ExperimentalOffhandReloadTicksUntilNextShell = Math.Max(0, offhandReloadTicks);
    }

    private void ApplyReplicatedAcquiredWeaponState(string gameplayAcquiredItemId)
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        if (ClassId != PlayerClass.Soldier
            || string.IsNullOrWhiteSpace(gameplayAcquiredItemId)
            || !runtimeRegistry.CanUseAcquiredItem(GameplayClassId, gameplayAcquiredItemId)
            || !runtimeRegistry.TryResolveBoundPlayerClassForPrimaryItem(gameplayAcquiredItemId, out var acquiredWeaponClassId))
        {
            // Do not use SetAcquiredWeapon(null) here: its gameplay-input path
            // intentionally falls back to the Primary slot, while a snapshot may
            // be authoritatively selecting a normal Secondary weapon.
            AcquiredWeaponClassId = null;
            AcquiredWeaponCurrentShells = 0;
            AcquiredWeaponCooldownTicks = 0;
            AcquiredWeaponReloadTicksUntilNextShell = 0;
            IsAcquiredWeaponEquipped = false;
            ResetAcquiredPyroStateFromCurrentAmmo();
            ResetAcquiredMedicNeedleStateIfUnavailable();
            RefreshGameplayLoadoutState();
            return;
        }

        if (AcquiredWeaponClassId == acquiredWeaponClassId)
        {
            if (AcquiredWeapon is { } existingWeapon)
            {
                AcquiredWeaponCurrentShells = int.Clamp(AcquiredWeaponCurrentShells, 0, existingWeapon.MaxAmmo);
                AcquiredWeaponCooldownTicks = Math.Max(0, AcquiredWeaponCooldownTicks);
                AcquiredWeaponReloadTicksUntilNextShell = Math.Max(0, AcquiredWeaponReloadTicksUntilNextShell);
            }

            return;
        }

        // Snapshot loadout identity is authoritative for remote players. Do not
        // require the viewer to own the other player's picked-up item.
        AcquiredWeaponClassId = acquiredWeaponClassId;
        var acquiredWeapon = AcquiredWeapon;
        AcquiredWeaponCurrentShells = acquiredWeapon?.MaxAmmo ?? 0;
        AcquiredWeaponCooldownTicks = 0;
        AcquiredWeaponReloadTicksUntilNextShell = 0;
        IsAcquiredWeaponEquipped = false;
        ResetAcquiredPyroStateFromCurrentAmmo();
        ResetAcquiredMedicNeedleStateIfUnavailable();
        RefreshGameplayLoadoutState();
    }

    private void ApplyNetworkMaxHealth(int maxHealth)
    {
        NetworkMaxHealthOverrideValue = maxHealth > 0 ? maxHealth : null;
        Health = int.Clamp(Health, 0, MaxHealth);
    }

    private void HydrateNetworkReplicatedAbilityRuntimeState()
    {
        HydrateNetworkReplicatedSniperRuntimeState();
        HydrateNetworkReplicatedHeavyRuntimeState();
        HydrateNetworkReplicatedCivvieRuntimeState();
        HydrateNetworkReplicatedBuffBannerRuntimeState();
    }

    private void HydrateNetworkReplicatedBuffBannerRuntimeState()
    {
        if (ClassId != PlayerClass.Soldier)
        {
            ResetBuffBannerState();
            return;
        }

        const string ownerId = GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId;
        TryGetReplicatedStateInt(
            ownerId,
            GameplayAbilityReplicatedState.BuffBannerChargeKillsKey,
            out var chargeKills);
        TryGetReplicatedStateInt(
            ownerId,
            GameplayAbilityReplicatedState.BuffBannerDeployTicksKey,
            out var deployTicks);
        TryGetReplicatedStateInt(
            ownerId,
            GameplayAbilityReplicatedState.BuffBannerActiveTicksKey,
            out var activeTicks);
        HydrateBuffBannerState(chargeKills, deployTicks, activeTicks);
    }

    private void HydrateNetworkReplicatedSniperRuntimeState()
    {
        if (ClassId != PlayerClass.Sniper)
        {
            return;
        }

        if (IsSniperScoped
            && TryGetReplicatedStateInt(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                GameplayAbilityReplicatedState.SniperChargeTicksKey,
                out var sniperChargeTicks))
        {
            SniperChargeTicks = Math.Clamp(
                sniperChargeTicks,
                0,
                LastToDieSniperRifleFullChargeTicks);
        }

        if (TryGetReplicatedStateInt(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                GameplayAbilityReplicatedState.SniperBowChargeTicksKey,
                out var sniperBowChargeTicks))
        {
            if (IsSniperBowEquipped)
            {
                SniperBowChargeTicks = Math.Clamp(
                    sniperBowChargeTicks,
                    0,
                    LastToDieSniperBowFullChargeTicks);
            }
            else
            {
                SniperBowChargeTicks = 0;
                SniperBowChargeDirectionDegrees = 0f;
            }
        }
    }

    private void HydrateNetworkReplicatedSecondaryWeaponFromSnapshot()
    {
        if (!IsReplicatedSecondaryWeaponAvailable())
        {
            if (HasExperimentalOffhandWeapon)
            {
                var secondaryItemId = GameplayLoadoutState.SecondaryItemId;
                var offhandItemId = ResolveRegisteredWeaponItemId(ExperimentalOffhandWeapon);
                if (!string.IsNullOrWhiteSpace(secondaryItemId)
                    && !string.Equals(offhandItemId, secondaryItemId, StringComparison.Ordinal))
                {
                    SetExperimentalOffhandWeapon(null);
                }
            }

            return;
        }

        var loadoutSecondaryItemId = GameplayLoadoutState.SecondaryItemId;
        if (string.IsNullOrWhiteSpace(loadoutSecondaryItemId))
        {
            return;
        }

        var currentOffhandItemId = ResolveRegisteredWeaponItemId(ExperimentalOffhandWeapon);
        if (!string.Equals(currentOffhandItemId, loadoutSecondaryItemId, StringComparison.Ordinal))
        {
            var item = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(loadoutSecondaryItemId);
            SetExperimentalOffhandWeapon(CharacterClassCatalog.RuntimeRegistry.CreatePrimaryWeaponDefinition(item));
        }

        ApplyNetworkReplicatedSecondaryWeaponAmmo();
    }

    private void ApplyNetworkReplicatedSecondaryWeaponAmmo()
    {
        if (!HasExperimentalOffhandWeapon)
        {
            return;
        }

        const string coreReplicatedOwnerId = "core.player";
        if (TryGetReplicatedStateInt(coreReplicatedOwnerId, "secondary_weapon_ammo", out var genericAmmo))
        {
            ExperimentalOffhandCurrentShells = int.Clamp(genericAmmo, 0, ExperimentalOffhandMaxShells);
            return;
        }

        var legacyAmmoKey = ClassId switch
        {
            PlayerClass.Soldier => "soldier_shotgun_ammo",
            PlayerClass.Demoman => "demoman_gl_ammo",
            PlayerClass.Scout => "scout_nailgun_ammo",
            PlayerClass.Sniper => "sniper_bow_ammo",
            PlayerClass.Medic => "medic_kritz_ammo",
            _ => null,
        };
        if (legacyAmmoKey is null
            || !TryGetReplicatedStateInt(coreReplicatedOwnerId, legacyAmmoKey, out var legacyAmmo))
        {
            return;
        }

        ExperimentalOffhandCurrentShells = int.Clamp(legacyAmmo, 0, ExperimentalOffhandMaxShells);
    }

    private bool IsReplicatedSecondaryWeaponAvailable()
    {
        const string coreReplicatedOwnerId = "core.player";

        if (TryGetReplicatedStateBool(coreReplicatedOwnerId, "secondary_weapon_available", out var secondaryAvailable))
        {
            return secondaryAvailable;
        }

        return ClassId switch
        {
            PlayerClass.Soldier => (TryGetReplicatedStateBool(coreReplicatedOwnerId, "soldier_shotgun_available", out var shotgunAvailable)
                    && shotgunAvailable)
                || TryGetReplicatedStateInt(coreReplicatedOwnerId, "soldier_shotgun_ammo", out _)
                || TryGetReplicatedStateInt(coreReplicatedOwnerId, "soldier_shotgun_max_ammo", out _),
            PlayerClass.Demoman => TryGetReplicatedStateInt(coreReplicatedOwnerId, "demoman_gl_ammo", out _)
                || TryGetReplicatedStateInt(coreReplicatedOwnerId, "demoman_gl_max_ammo", out _),
            PlayerClass.Scout => (TryGetReplicatedStateBool(coreReplicatedOwnerId, "scout_nailgun_available", out var nailgunAvailable)
                    && nailgunAvailable)
                || TryGetReplicatedStateInt(coreReplicatedOwnerId, "scout_nailgun_ammo", out _)
                || TryGetReplicatedStateInt(coreReplicatedOwnerId, "scout_nailgun_max_ammo", out _),
            PlayerClass.Sniper => (TryGetReplicatedStateBool(coreReplicatedOwnerId, "sniper_bow_available", out var bowAvailable)
                    && bowAvailable)
                || TryGetReplicatedStateInt(coreReplicatedOwnerId, "sniper_bow_ammo", out _)
                || TryGetReplicatedStateInt(coreReplicatedOwnerId, "sniper_bow_max_ammo", out _),
            PlayerClass.Medic => (TryGetReplicatedStateBool(coreReplicatedOwnerId, "medic_kritz_available", out var kritzAvailable)
                    && kritzAvailable)
                || TryGetReplicatedStateInt(coreReplicatedOwnerId, "medic_kritz_ammo", out _)
                || TryGetReplicatedStateInt(coreReplicatedOwnerId, "medic_kritz_max_ammo", out _),
            _ => false,
        };
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

        var hasPogoTrickTicks = TryGetReplicatedStateInt(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                GameplayAbilityReplicatedState.CivviePogoTrickTicksKey,
                out var pogoTrickTicks);
        if (hasPogoTrickTicks)
        {
            CivviePogoTrickTicksRemaining = Math.Max(0, pogoTrickTicks);
        }
        else
        {
            CivviePogoTrickTicksRemaining = 0;
        }

        var hasPogoTrickDuration = TryGetReplicatedStateInt(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                GameplayAbilityReplicatedState.CivviePogoTrickDurationTicksKey,
                out var pogoTrickDurationTicks);
        if (hasPogoTrickDuration)
        {
            CivviePogoTrickDurationTicks = Math.Max(0, pogoTrickDurationTicks);
        }
        else if (!hasPogoTrickTicks || CivviePogoTrickTicksRemaining <= 0)
        {
            CivviePogoTrickDurationTicks = 0;
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
            CivviePogoTrickTicksRemaining = 0;
            CivviePogoTrickDurationTicks = 0;
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
                gameplayPrimaryItemId,
                out var validatedLoadoutState))
        {
            SelectedGameplayLoadoutId = validatedLoadoutState.LoadoutId;
            SelectedGameplayPrimaryItemId = validatedLoadoutState.PrimaryItemId;
            RefreshSelectedGameplayPrimaryWeapon();
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
