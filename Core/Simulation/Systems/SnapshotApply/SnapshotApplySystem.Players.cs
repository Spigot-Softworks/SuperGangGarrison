using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class SnapshotApplySystem
{
    internal bool ApplySnapshot(SnapshotMessage snapshot, byte localPlayerSlot = 1)
    {
        if (!EnsureSnapshotLevelLoaded(snapshot))
        {
            return false;
        }

        // Apply string cache updates from server
        _host.ClientSnapshots.StringCache.ApplyCacheUpdates(snapshot.StringCacheUpdates);

        ApplySnapshotWorldState(snapshot);

        if (!TryResolveSnapshotLocalPlayerState(
                snapshot,
                localPlayerSlot,
                out var localPlayerState,
                out var isSpectatorSnapshot))
        {
            return false;
        }

        ApplySnapshotPlayerState(snapshot, localPlayerSlot, localPlayerState, isSpectatorSnapshot);
        var abilitySettingsPlayer = localPlayerState ?? snapshot.Players.FirstOrDefault();
        var abilitySettings = abilitySettingsPlayer?.ReplicatedStates?.FirstOrDefault(entry =>
            entry.OwnerId == GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId
            && entry.Key == GameplayAbilityReplicatedState.SpecialAbilitiesEnabledKey);
        if (abilitySettings is not null)
        {
            _host.Abilities.ApplyNetworkSpecialAbilitiesSetting(abilitySettings.BoolValue);
        }
        ApplySnapshotTransientEntities(snapshot);
        ApplySnapshotEventQueues(snapshot);

        return true;
    }

    private void ApplySnapshotPlayer(PlayerEntity player, SnapshotPlayerState snapshotPlayer)
    {
        // Resolve cached strings using cache IDs
        var modPackId = _host.ClientSnapshots.StringCache.Resolve(snapshotPlayer.GameplayModPackCacheId, snapshotPlayer.GameplayModPackId);
        var loadoutId = _host.ClientSnapshots.StringCache.Resolve(snapshotPlayer.GameplayLoadoutCacheId, snapshotPlayer.GameplayLoadoutId);
        var primaryItemId = _host.ClientSnapshots.StringCache.Resolve(snapshotPlayer.GameplayPrimaryItemCacheId, snapshotPlayer.GameplayPrimaryItemId);
        var secondaryItemId = _host.ClientSnapshots.StringCache.Resolve(snapshotPlayer.GameplaySecondaryItemCacheId, snapshotPlayer.GameplaySecondaryItemId);
        var utilityItemId = _host.ClientSnapshots.StringCache.Resolve(snapshotPlayer.GameplayUtilityItemCacheId, snapshotPlayer.GameplayUtilityItemId);
        var equippedItemId = _host.ClientSnapshots.StringCache.Resolve(snapshotPlayer.GameplayEquippedItemCacheId, snapshotPlayer.GameplayEquippedItemId);
        var acquiredItemId = _host.ClientSnapshots.StringCache.Resolve(snapshotPlayer.GameplayAcquiredItemCacheId, snapshotPlayer.GameplayAcquiredItemId);
        var gameplayClassId = _host.ClientSnapshots.StringCache.Resolve(snapshotPlayer.GameplayClassCacheId, snapshotPlayer.GameplayClassId);
        var classDefinition = ResolveSnapshotClassDefinition(snapshotPlayer, gameplayClassId);

        _host.ReadyUp.ApplySnapshotNetworkPlayerReady(snapshotPlayer.Slot, snapshotPlayer.IsReady);
        player.SetDisplayName(snapshotPlayer.Name);

        // Preserve the locally-advancing taunt frame when the player is still taunting.
        // When the server stops sending IsTaunting (taunt finished server-side), allow the
        // local animation to complete naturally rather than cutting it off mid-way — this
        // corrects for the latency offset where the client started counting from 0 while
        // the server was already partway through. Only hard-stop when the player dies.
        var localIsTaunting = snapshotPlayer.IsTaunting
            || (player.IsTaunting && snapshotPlayer.IsAlive);
        var tauntFrameIndex = localIsTaunting && player.IsTaunting
            ? player.TauntFrameIndex
            : 0f;

        var sniperChargeTicks = player.IsSniperScoped && snapshotPlayer.IsSniperScoped
            ? player.SniperChargeTicks
            : 0;

        player.ApplyNetworkState(
            (PlayerTeam)snapshotPlayer.Team,
            classDefinition,
            snapshotPlayer.IsAlive,
            snapshotPlayer.X,
            snapshotPlayer.Y,
            snapshotPlayer.HorizontalSpeed,
            snapshotPlayer.VerticalSpeed,
            snapshotPlayer.Health,
            snapshotPlayer.Ammo,
            snapshotPlayer.Kills,
            snapshotPlayer.Deaths,
            snapshotPlayer.Caps,
            snapshotPlayer.Points,
            snapshotPlayer.HealPoints,
            snapshotPlayer.ActiveDominationCount,
            snapshotPlayer.IsDominatingLocalViewer,
            snapshotPlayer.IsDominatedByLocalViewer,
            snapshotPlayer.Metal,
            snapshotPlayer.IsGrounded,
            snapshotPlayer.RemainingAirJumps,
            snapshotPlayer.IsCarryingIntel,
            snapshotPlayer.IntelRechargeTicks,
            snapshotPlayer.IsSpyCloaked,
            snapshotPlayer.SpyCloakAlpha,
            snapshotPlayer.IsSpySuperjumping,
            snapshotPlayer.SpySuperjumpHorizontalVelocity,
            snapshotPlayer.SpySuperjumpCooldownTicksRemaining,
            snapshotPlayer.SpyBackstabVisualTicksRemaining,
            snapshotPlayer.IsUbered,
            snapshotPlayer.IsKritzCritBoosted,
            snapshotPlayer.IsHeavyEating,
            snapshotPlayer.HeavyEatTicksRemaining,
            snapshotPlayer.IsSniperScoped,
            sniperChargeTicks,
            snapshotPlayer.IsUsingBinoculars,
            snapshotPlayer.BinocularsFocusX,
            snapshotPlayer.BinocularsFocusY,
            snapshotPlayer.FacingDirectionX,
            snapshotPlayer.AimDirectionDegrees,
            snapshotPlayer.AimWorldX,
            snapshotPlayer.AimWorldY,
            localIsTaunting,
            tauntFrameIndex,
            snapshotPlayer.IsChatBubbleVisible,
            snapshotPlayer.ChatBubbleFrameIndex,
            snapshotPlayer.ChatBubbleAlpha,
            snapshotPlayer.BurnIntensity,
            snapshotPlayer.BurnDurationSourceTicks,
            snapshotPlayer.BurnDecayDelaySourceTicksRemaining,
            snapshotPlayer.BurnIntensityDecayPerSourceTick,
            snapshotPlayer.BurnedByPlayerId,
            snapshotPlayer.MovementState,
            snapshotPlayer.PrimaryCooldownTicks,
            snapshotPlayer.ReloadTicksUntilNextShell,
            snapshotPlayer.MedicNeedleCooldownTicks,
            snapshotPlayer.MedicNeedleRefillTicks,
            snapshotPlayer.PyroAirblastCooldownTicks,
            snapshotPlayer.PyroFlareCooldownTicks,
            snapshotPlayer.PyroPrimaryFuelScaled,
            snapshotPlayer.IsPyroPrimaryRefilling,
            snapshotPlayer.PyroFlameLoopTicksRemaining,
            snapshotPlayer.PyroPrimaryRequiresReleaseAfterEmpty,
            snapshotPlayer.HeavyEatCooldownTicksRemaining,
            snapshotPlayer.Assists,
            snapshotPlayer.BadgeMask,
            snapshotPlayer.IsMedicHealing,
            snapshotPlayer.MedicHealTargetId,
            snapshotPlayer.MedicUberCharge,
            snapshotPlayer.IsMedicUberReady,
            modPackId,
            loadoutId,
            primaryItemId,
            secondaryItemId,
            utilityItemId,
            snapshotPlayer.GameplayEquippedSlot,
            equippedItemId,
            acquiredItemId,
            snapshotPlayer.OwnedGameplayItemIds,
            ConvertReplicatedStateEntries(snapshotPlayer.ReplicatedStates),
            snapshotPlayer.PlayerScale,
            offhandCooldownTicks: snapshotPlayer.OffhandCooldownTicks,
            offhandReloadTicks: snapshotPlayer.OffhandReloadTicks,
            gibDeaths: snapshotPlayer.GibDeaths,
            isTypingChatMessage: snapshotPlayer.IsTypingChatMessage,
            networkMaxHealth: snapshotPlayer.MaxHealth,
            medicUberDeliveryState: snapshotPlayer.MedicUberDeliveryState,
            kritzCritBoostProviderPlayerId: snapshotPlayer.KritzCritBoostProviderPlayerId,
            kritzCritBoostProviderSlot: snapshotPlayer.KritzCritBoostProviderSlot,
            kritzCritBoostDamageMultiplier: snapshotPlayer.KritzCritBoostDamageMultiplier,
            isDispenserBuffed: snapshotPlayer.IsDispenserBuffed,
            dispenserAttackReloadSpeedMultiplier: snapshotPlayer.DispenserAttackReloadSpeedMultiplier);
        if (TryGetSnapshotReplicatedStateBool(
                snapshotPlayer,
                PlayerEntity.CoreReplicatedStateOwnerId,
                PlayerEntity.SpawnRoomReplicatedStateKey,
                out var isInSpawnRoom))
        {
            player.SetSpawnRoomState(isInSpawnRoom);
        }
        player.HydrateReplicatedEngineerAlternateWeaponMode();
        var whippingCordLatched = TryGetSnapshotReplicatedStateBool(
            snapshotPlayer,
            PlayerEntity.CoreReplicatedStateOwnerId,
            WhippingCordCatalog.ReplicatedLatchKey,
            out var replicatedWhippingCordLatch)
            && replicatedWhippingCordLatch;
        _ = TryGetSnapshotReplicatedStateFloat(
            snapshotPlayer,
            PlayerEntity.CoreReplicatedStateOwnerId,
            WhippingCordCatalog.ReplicatedAnchorXKey,
            out var whippingCordAnchorX);
        _ = TryGetSnapshotReplicatedStateFloat(
            snapshotPlayer,
            PlayerEntity.CoreReplicatedStateOwnerId,
            WhippingCordCatalog.ReplicatedAnchorYKey,
            out var whippingCordAnchorY);
        _ = TryGetSnapshotReplicatedStateFloat(
            snapshotPlayer,
            PlayerEntity.CoreReplicatedStateOwnerId,
            WhippingCordCatalog.ReplicatedRopeLengthKey,
            out var whippingCordRopeLength);
        player.HydrateWhippingCordLatch(
            whippingCordLatched, whippingCordAnchorX, whippingCordAnchorY, whippingCordRopeLength);
        player.HydrateNetworkRageState(
            snapshotPlayer.RageCharge,
            snapshotPlayer.IsRageReady,
            snapshotPlayer.RageTicksRemaining);
        player.HydrateNetworkExperimentalVisualState(
            snapshotPlayer.ExperimentalCryoSlowTicksRemaining,
            snapshotPlayer.ExperimentalCryoFreezeTicksRemaining,
            snapshotPlayer.ExperimentalCryoExposureFraction,
            snapshotPlayer.ExperimentalGhostVisibilityTicksRemaining,
            snapshotPlayer.ExperimentalGhostTrailAlpha);
        player.HydrateNetworkCombatComboState(
            snapshotPlayer.CurrentCombo,
            snapshotPlayer.ComboTicksRemaining);
        player.HydrateLastToDieSpyCloakMeter(
            snapshotPlayer.LastToDieSpyCloakMeterUnits,
            global::OpenGarrison.Core.LastToDie.LastToDieDerivedModifiers.SpyCloakMeterDurationSeconds
                * Math.Max(1, _host.Config.TicksPerSecond)
                * global::OpenGarrison.Core.LastToDie.LastToDieDerivedModifiers.SpyCloakMeterUnitsPerTick,
            snapshotPlayer.LastToDieSpyRogueRampStacks,
            snapshotPlayer.LastToDieSpyRogueRampTicks);
        player.HydrateSpyJumpBootState(
            snapshotPlayer.IsSpySuperjumping,
            snapshotPlayer.SpySuperjumpHorizontalVelocity,
            snapshotPlayer.SpySuperjumpCooldownTicksRemaining,
            snapshotPlayer.SpySuperjumpAvailableCharges,
            snapshotPlayer.SpySuperjumpMaximumCharges,
            snapshotPlayer.SpySuperjumpChargeTicks,
            snapshotPlayer.SpySuperjumpChargeDirectionDegrees,
            snapshotPlayer.SpySuperjumpChargeStartMovementButtons,
            snapshotPlayer.SpySuperjumpChargeStartBlockedUntilAbilityRelease);
    }

    internal static bool TryGetSnapshotReplicatedStateBool(
        SnapshotPlayerState snapshotPlayer,
        string ownerId,
        string key,
        out bool value)
    {
        var entries = snapshotPlayer.ReplicatedStates;
        if (entries is not null)
        {
            for (var index = entries.Count - 1; index >= 0; index -= 1)
            {
                var entry = entries[index];
                if (!string.Equals(entry.OwnerId, ownerId, StringComparison.Ordinal)
                    || !string.Equals(entry.Key, key, StringComparison.Ordinal))
                {
                    continue;
                }

                if (entry.Kind == SnapshotReplicatedStateValueKind.Toggle)
                {
                    value = entry.BoolValue;
                    return true;
                }

                break;
            }
        }

        value = default;
        return false;
    }

    internal static bool TryGetSnapshotReplicatedStateFloat(
        SnapshotPlayerState snapshotPlayer,
        string ownerId,
        string key,
        out float value)
    {
        var entries = snapshotPlayer.ReplicatedStates;
        if (entries is not null)
        {
            for (var index = entries.Count - 1; index >= 0; index -= 1)
            {
                var entry = entries[index];
                if (!string.Equals(entry.OwnerId, ownerId, StringComparison.Ordinal)
                    || !string.Equals(entry.Key, key, StringComparison.Ordinal))
                {
                    continue;
                }

                if (entry.Kind == SnapshotReplicatedStateValueKind.Scalar)
                {
                    value = entry.FloatValue;
                    return true;
                }

                break;
            }
        }

        value = default;
        return false;
    }

    private static CharacterClassDefinition ResolveSnapshotClassDefinition(SnapshotPlayerState snapshotPlayer, string gameplayClassId)
    {
        if (!string.IsNullOrWhiteSpace(gameplayClassId)
            && CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(gameplayClassId, out _))
        {
            return CharacterClassCatalog.GetDefinition(gameplayClassId);
        }

        var playerClass = Enum.IsDefined(typeof(PlayerClass), (int)snapshotPlayer.ClassId)
            ? (PlayerClass)snapshotPlayer.ClassId
            : PlayerClass.Scout;
        return CharacterClassCatalog.GetDefinition(playerClass);
    }

    private static GameplayReplicatedStateEntry[] ConvertReplicatedStateEntries(IReadOnlyList<SnapshotReplicatedStateEntry>? entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return [];
        }

        var result = new GameplayReplicatedStateEntry[entries.Count];
        for (var index = 0; index < entries.Count; index += 1)
        {
            var entry = entries[index];
            result[index] = new GameplayReplicatedStateEntry(
                entry.OwnerId,
                entry.Key,
                entry.Kind switch
                {
                    SnapshotReplicatedStateValueKind.Whole => GameplayReplicatedStateValueKind.Whole,
                    SnapshotReplicatedStateValueKind.Scalar => GameplayReplicatedStateValueKind.Scalar,
                    _ => GameplayReplicatedStateValueKind.Toggle,
                },
                entry.IntValue,
                entry.FloatValue,
                entry.BoolValue);
        }

        return result;
    }

    internal void AdvanceRemoteSnapshotPlayerTauntStates()
    {
        if (_host.ClientPredictionMode && _host.LocalPlayer.IsAlive && _host.LocalPlayer.IsTaunting)
        {
            _host.LocalPlayer.AdvanceTauntFrameLocally(_host.Config.FixedDeltaSeconds);
        }

        for (var index = 0; index < _host.RemoteSnapshots.Players.Count; index += 1)
        {
            var player = _host.RemoteSnapshots.Players[index];
            if (player.IsAlive && player.IsTaunting)
            {
                player.AdvanceTauntFrameLocally(_host.Config.FixedDeltaSeconds);
            }
        }
    }

    private void SyncRemoteSnapshotPlayers(IEnumerable<SnapshotPlayerState> snapshotPlayers)
    {
        _host.RemoteSnapshots.SeenSlots.Clear();
        _host.RemoteSnapshots.Players.Clear();
        _host.RemoteSnapshots.AwaitingJoinSlots.Clear();
        _host.RemoteSnapshots.AwaitingJoinPlayerIds.Clear();
        foreach (var snapshotPlayer in snapshotPlayers)
        {
            var appliedSnapshotPlayer = NormalizeAwaitingJoinSnapshotPlayerState(snapshotPlayer);
            _host.RemoteSnapshots.SeenSlots.Add(appliedSnapshotPlayer.Slot);
            var hadRemotePlayer = _host.RemoteSnapshots.PlayersBySlot.TryGetValue(appliedSnapshotPlayer.Slot, out var existingPlayer);
            PlayerEntity player;
            if (!hadRemotePlayer || existingPlayer!.Id != appliedSnapshotPlayer.PlayerId)
            {
                // Slots are reusable across disconnects. PlayerId is the
                // immutable identity used by interpolation histories and
                // presentation de-duplication, so never carry an old slot's
                // entity across an identity change.
                if (hadRemotePlayer)
                {
                    _host.ClientSnapshots.PresentedGibDeathCountsByPlayerId.Remove(existingPlayer!.Id);
                    if (_host.RemoteSnapshots.ScoreboardPlayersBySlot.TryGetValue(appliedSnapshotPlayer.Slot, out var staleScoreboardPlayer)
                        && staleScoreboardPlayer.Id != appliedSnapshotPlayer.PlayerId)
                    {
                        _host.RemoteSnapshots.ScoreboardPlayersBySlot.Remove(appliedSnapshotPlayer.Slot);
                    }
                }

                ReserveEntityId(appliedSnapshotPlayer.PlayerId);
                var gameplayClassId = _host.ClientSnapshots.StringCache.Resolve(appliedSnapshotPlayer.GameplayClassCacheId, appliedSnapshotPlayer.GameplayClassId);
                player = new PlayerEntity(
                    appliedSnapshotPlayer.PlayerId,
                    ResolveSnapshotClassDefinition(appliedSnapshotPlayer, gameplayClassId),
                    appliedSnapshotPlayer.Name);
                _host.RemoteSnapshots.PlayersBySlot[appliedSnapshotPlayer.Slot] = player;
            }
            else
            {
                player = existingPlayer!;
            }

            var wasAlive = player.IsAlive;
            var previousGibDeaths = player.GibDeaths;
            SynchronizeNetworkGibDeathPresentationCount(player.Id, appliedSnapshotPlayer.GibDeaths);
            ApplySnapshotPlayer(player, appliedSnapshotPlayer);
            _host.NetworkPlayerRules.ApplySnapshotNetworkPlayerPingMilliseconds(appliedSnapshotPlayer.Slot, appliedSnapshotPlayer.PingMilliseconds);
            _host.NetworkPlayerRules.ApplySnapshotNetworkPlayerBot(appliedSnapshotPlayer.Slot, appliedSnapshotPlayer.IsBot);
            if (hadRemotePlayer
                && wasAlive
                && !player.IsAlive
                && appliedSnapshotPlayer.GibDeaths > previousGibDeaths
                && TryMarkNetworkGibDeathPresented(player.Id, appliedSnapshotPlayer.GibDeaths))
            {
                _host.PlayerRemains.SpawnClientPlayerGibsFromNetworkDeath(player);
            }

            if (appliedSnapshotPlayer.IsAwaitingJoin)
            {
                _host.RemoteSnapshots.AwaitingJoinSlots.Add(appliedSnapshotPlayer.Slot);
                _host.RemoteSnapshots.AwaitingJoinPlayerIds.Add(appliedSnapshotPlayer.PlayerId);
            }
            _host.RemoteSnapshots.Players.Add(player);
        }

        _host.RemoteSnapshots.StaleSlots.Clear();
        foreach (var entry in _host.RemoteSnapshots.PlayersBySlot)
        {
            if (_host.RemoteSnapshots.SeenSlots.Contains(entry.Key))
            {
                continue;
            }

            _host.RemoteSnapshots.StaleSlots.Add(entry.Key);
        }

        for (var index = 0; index < _host.RemoteSnapshots.StaleSlots.Count; index += 1)
        {
            var slot = _host.RemoteSnapshots.StaleSlots[index];
            if (!_host.RemoteSnapshots.PlayersBySlot.TryGetValue(slot, out var removedPlayer))
            {
                continue;
            }

            if (ShouldRetainMissingRemoteSnapshotPlayerForScoreboard(removedPlayer))
            {
                continue;
            }

            if (_host.RemoteSnapshots.PlayersBySlot.Remove(slot))
            {
                _host.ReadyUp.ApplySnapshotNetworkPlayerReady(slot, ready: false);
                _host.NetworkPlayerRules.ApplySnapshotNetworkPlayerPingMilliseconds(slot, -1);
                _host.NetworkPlayerRules.ApplySnapshotNetworkPlayerBot(slot, isBot: false);
                _host.ClientSnapshots.PresentedGibDeathCountsByPlayerId.Remove(removedPlayer.Id);
            }
        }

    }

    private void SyncRemoteSnapshotScoreboardPlayers(IEnumerable<SnapshotPlayerState> snapshotPlayers)
    {
        _host.RemoteSnapshots.SeenSlots.Clear();
        _host.RemoteSnapshots.ScoreboardPlayers.Clear();
        foreach (var snapshotPlayer in snapshotPlayers)
        {
            var appliedSnapshotPlayer = NormalizeAwaitingJoinSnapshotPlayerState(snapshotPlayer);
            _host.RemoteSnapshots.SeenSlots.Add(appliedSnapshotPlayer.Slot);
            PlayerEntity player;
            if (_host.RemoteSnapshots.PlayersBySlot.TryGetValue(appliedSnapshotPlayer.Slot, out var visiblePlayer)
                && visiblePlayer.Id == appliedSnapshotPlayer.PlayerId)
            {
                player = visiblePlayer;
            }
            else if (_host.RemoteSnapshots.ScoreboardPlayersBySlot.TryGetValue(appliedSnapshotPlayer.Slot, out player!)
                && player.Id == appliedSnapshotPlayer.PlayerId)
            {
                // Retained scoreboard entities can outlive a visible roster
                // entry, but only while they still represent the same player.
            }
            else
            {
                if (visiblePlayer is not null)
                {
                    _host.RemoteSnapshots.PlayersBySlot.Remove(appliedSnapshotPlayer.Slot);
                    _host.ClientSnapshots.PresentedGibDeathCountsByPlayerId.Remove(visiblePlayer.Id);
                }

                ReserveEntityId(appliedSnapshotPlayer.PlayerId);
                var gameplayClassId = _host.ClientSnapshots.StringCache.Resolve(appliedSnapshotPlayer.GameplayClassCacheId, appliedSnapshotPlayer.GameplayClassId);
                player = new PlayerEntity(
                    appliedSnapshotPlayer.PlayerId,
                    ResolveSnapshotClassDefinition(appliedSnapshotPlayer, gameplayClassId),
                    appliedSnapshotPlayer.Name);
                _host.RemoteSnapshots.ScoreboardPlayersBySlot[appliedSnapshotPlayer.Slot] = player;
            }

            ApplySnapshotPlayer(player, appliedSnapshotPlayer);
            _host.NetworkPlayerRules.ApplySnapshotNetworkPlayerBot(appliedSnapshotPlayer.Slot, appliedSnapshotPlayer.IsBot);
            _host.RemoteSnapshots.ScoreboardPlayers.Add(player);
        }

        // A normal snapshot may omit cloaked/backstabbing enemy spies from the
        // visible player roster. Keep those retained snapshot players available
        // to the scoreboard until the server explicitly reports them again.
        foreach (var entry in _host.RemoteSnapshots.PlayersBySlot)
        {
            if (_host.RemoteSnapshots.SeenSlots.Contains(entry.Key)
                || !ShouldRetainMissingRemoteSnapshotPlayerForScoreboard(entry.Value))
            {
                continue;
            }

            _host.RemoteSnapshots.SeenSlots.Add(entry.Key);
            _host.RemoteSnapshots.ScoreboardPlayersBySlot[entry.Key] = entry.Value;
            _host.RemoteSnapshots.ScoreboardPlayers.Add(entry.Value);
        }

        _host.RemoteSnapshots.StaleSlots.Clear();
        foreach (var entry in _host.RemoteSnapshots.ScoreboardPlayersBySlot)
        {
            if (!_host.RemoteSnapshots.SeenSlots.Contains(entry.Key))
            {
                _host.RemoteSnapshots.StaleSlots.Add(entry.Key);
                continue;
            }

            if (_host.RemoteSnapshots.PlayersBySlot.TryGetValue(entry.Key, out var visiblePlayer)
                && (entry.Value.Id != visiblePlayer.Id || ReferenceEquals(entry.Value, visiblePlayer)))
            {
                _host.RemoteSnapshots.StaleSlots.Add(entry.Key);
            }
        }

        for (var index = 0; index < _host.RemoteSnapshots.StaleSlots.Count; index += 1)
        {
            var slot = _host.RemoteSnapshots.StaleSlots[index];
            _host.RemoteSnapshots.ScoreboardPlayersBySlot.Remove(slot);
            if (!_host.RemoteSnapshots.PlayersBySlot.ContainsKey(slot))
            {
                _host.NetworkPlayerRules.ApplySnapshotNetworkPlayerBot(slot, isBot: false);
            }
        }
    }

    private bool ShouldRetainMissingRemoteSnapshotPlayerForScoreboard(PlayerEntity player)
    {
        return _host.LocalPlayer.IsAlive
            && player.Team != _host.LocalPlayer.Team
            && player.ClassId == PlayerClass.Spy
            && (!player.IsSpyVisibleToEnemies || player.IsSpyBackstabAnimating)
            && IsRemoteSpyHiddenFromLocalPlayer(player);
    }

    private bool IsRemoteSpyHiddenFromLocalPlayer(PlayerEntity spy)
    {
        var radians = MathF.PI * _host.LocalPlayer.AimDirectionDegrees / 180f;
        var viewerFacingSign = DeterministicMath.Cos(radians) < 0f ? -1 : 1;
        return Math.Sign(spy.X - _host.LocalPlayer.X) == -viewerFacingSign;
    }

    private void SynchronizeNetworkGibDeathPresentationCount(int playerId, int observedGibDeaths)
    {
        if (!_host.ClientSnapshots.PresentedGibDeathCountsByPlayerId.TryGetValue(playerId, out var presentedGibDeaths)
            || observedGibDeaths >= presentedGibDeaths)
        {
            return;
        }

        if (observedGibDeaths <= 0)
        {
            _host.ClientSnapshots.PresentedGibDeathCountsByPlayerId.Remove(playerId);
            return;
        }

        _host.ClientSnapshots.PresentedGibDeathCountsByPlayerId[playerId] = observedGibDeaths;
    }

    private bool TryMarkNetworkGibDeathPresented(int playerId, int gibDeaths)
    {
        if (gibDeaths <= 0)
        {
            return false;
        }

        if (_host.ClientSnapshots.PresentedGibDeathCountsByPlayerId.TryGetValue(playerId, out var presentedGibDeaths)
            && gibDeaths <= presentedGibDeaths)
        {
            return false;
        }

        _host.ClientSnapshots.PresentedGibDeathCountsByPlayerId[playerId] = gibDeaths;
        return true;
    }

    internal bool TryPresentNetworkGibDeath(int playerId, int gibDeaths, float? spawnX = null, float? spawnY = null)
    {
        var player = FindNetworkPresentationPlayerById(playerId);
        if (player is null)
        {
            return false;
        }

        if (!TryMarkNetworkGibDeathPresented(playerId, gibDeaths))
        {
            return false;
        }

        _host.PlayerRemains.SpawnClientPlayerGibsFromNetworkDeath(player, spawnX, spawnY);
        return true;
    }

    private PlayerEntity? FindNetworkPresentationPlayerById(int playerId)
    {
        if (_host.LocalPlayer.Id == playerId)
        {
            return _host.LocalPlayer;
        }

        foreach (var player in _host.RemoteSnapshots.PlayersBySlot.Values)
        {
            if (player.Id == playerId)
            {
                return player;
            }
        }

        return _host.FindPlayerById(playerId);
    }
}
