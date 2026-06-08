using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public void SetLocalInput(PlayerInputSnapshot input)
    {
        _localInput = input;
    }

    public void SetLocalPreviousInput(PlayerInputSnapshot input)
    {
        _previousLocalInput = input;
    }

    public void SetEnemyInput(PlayerInputSnapshot input)
    {
        _enemyInput = input;
        _enemyInputOverrideActive = true;
    }

    public void ClearEnemyInputOverride()
    {
        _enemyInput = default;
        _previousEnemyInput = default;
        _enemyInputOverrideActive = false;
    }

    public void SetLocalPlayerName(string displayName)
    {
        LocalPlayer.SetDisplayName(displayName);
    }

    public void SetLocalPlayerBadgeMask(ulong badgeMask)
    {
        LocalPlayer.SetBadgeMask(badgeMask);
    }

    public void SetLocalPlayerChatBubble(int frameIndex)
    {
        LocalPlayer.TriggerChatBubble(frameIndex);
    }

    public bool TrySetNetworkPlayerName(byte slot, string displayName)
    {
        switch (slot)
        {
            case LocalPlayerSlot:
                SetLocalPlayerName(displayName);
                return true;
            default:
                if (!TryGetOrEnsurePlayableNetworkPlayer(slot, out var player))
                {
                    return false;
                }

                player.SetDisplayName(displayName);
                return true;
        }
    }

    public bool TrySetNetworkPlayerBadgeMask(byte slot, ulong badgeMask)
    {
        switch (slot)
        {
            case LocalPlayerSlot:
                SetLocalPlayerBadgeMask(badgeMask);
                return true;
            default:
                if (!TryGetOrEnsurePlayableNetworkPlayer(slot, out var player))
                {
                    return false;
                }

                player.SetBadgeMask(badgeMask);
                return true;
        }
    }

    public bool TryTriggerNetworkPlayerChatBubble(byte slot, int frameIndex)
    {
        switch (slot)
        {
            case LocalPlayerSlot:
                SetLocalPlayerChatBubble(frameIndex);
                return true;
            default:
                if (!TryGetNetworkPlayer(slot, out var player))
                {
                    return false;
                }

                player.TriggerChatBubble(frameIndex);
                return true;
        }
    }

    public void SetNetworkPlayerIsTypingChatMessage(byte slot, bool isTyping)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return;
        }

        player.IsTypingChatMessage = isTyping;
    }

    public bool TrySetNetworkPlayerTeam(byte slot, PlayerTeam team)
    {
        if (TryGetNetworkPlayer(slot, out var configuredPlayer) && configuredPlayer.Team != team)
        {
            TryDropCarriedIntel(configuredPlayer);
            ClearDominationsForPlayer(configuredPlayer);
        }

        if (!TrySetNetworkPlayerConfiguredTeam(slot, team))
        {
            return false;
        }

        if (slot == LocalPlayerSlot)
        {
            if (FriendlyDummyEnabled && !IsNetworkPlayerAwaitingJoin(LocalPlayerSlot))
            {
                var friendlySpawn = FindFriendlyDummySpawnNearLocalPlayer();
                FriendlyDummy.SetClassDefinition(_friendlyDummyClassDefinition);
                SpawnPlayerResolved(FriendlyDummy, GetNetworkPlayerConfiguredTeam(LocalPlayerSlot), friendlySpawn.X, friendlySpawn.Y);
            }
        }

        if (!IsNetworkPlayerEnabled(slot) || !TryGetNetworkPlayer(slot, out var player))
        {
            return true;
        }

        if (IsNetworkPlayerAwaitingJoin(slot))
        {
            player.ClearMedicHealingTarget();
            player.Kill();
            return true;
        }

        player.SetClassDefinition(GetNetworkPlayerClassDefinition(slot));
        SpawnPlayerResolved(player, team, ReserveSpawn(player, team, slot), playRespawnSound: true);
        return true;
    }

    public bool TryRequestNetworkPlayerTeamSelection(byte slot, PlayerTeam team)
    {
        if (!TrySetNetworkPlayerConfiguredTeam(slot, team))
        {
            return false;
        }

        if (!IsPlayableNetworkPlayerSlot(slot) || IsNetworkPlayerAwaitingJoin(slot))
        {
            return true;
        }

        _pendingNetworkPlayerTeamSelections.Add(slot);
        return true;
    }

    public bool TryApplyNetworkPlayerClassSelection(byte slot, PlayerClass playerClass)
    {
        return CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(playerClass, out var binding)
            && TryApplyNetworkPlayerClassSelection(slot, binding.ClassId);
    }

    public bool TryApplyNetworkPlayerClassSelection(byte slot, string gameplayClassId)
    {
        if (IsNetworkPlayerAwaitingJoin(slot))
        {
            return TryCompleteNetworkPlayerJoinState(slot, gameplayClassId);
        }

        if (slot == LocalPlayerSlot)
        {
            return TrySetLocalClass(gameplayClassId);
        }

        var definition = CharacterClassCatalog.GetDefinition(gameplayClassId);
        if (!TryGetNetworkPlayer(slot, out var player)
            || (string.Equals(definition.GameplayClassId, GetNetworkPlayerClassDefinition(slot).GameplayClassId, StringComparison.Ordinal)
                && player.Team == GetNetworkPlayerConfiguredTeam(slot)
                && !HasPendingNetworkPlayerTeamSelection(slot)))
        {
            return false;
        }

        return TryApplyNetworkPlayerClassChange(slot, definition);
    }

    public void SetLocalPlayerTeam(PlayerTeam team)
    {
        TrySetNetworkPlayerTeam(LocalPlayerSlot, team);
    }

    public void SetPendingLocalPlayerClass(PlayerClass playerClass)
    {
        if (CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(playerClass, out var binding))
        {
            SetPendingLocalPlayerClass(binding.ClassId);
        }
    }

    public void SetPendingLocalPlayerClass(string gameplayClassId)
    {
        var definition = CharacterClassCatalog.GetDefinition(gameplayClassId);
        TrySetNetworkPlayerClassDefinition(LocalPlayerSlot, definition);
        LocalPlayer.SetClassDefinition(definition);
        SyncExperimentalGameplayLoadout(LocalPlayerSlot, LocalPlayer);
    }

    public bool TrySetNetworkPlayerInput(byte slot, PlayerInputSnapshot input)
    {
        if (slot == LocalPlayerSlot)
        {
            SetLocalInput(input);
            return true;
        }

        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        EnsureAdditionalNetworkPlayer(slot);
        _additionalNetworkPlayerInputs[slot] = input;
        return true;
    }

    public bool TrySetNetworkPlayerSpawnOverride(byte slot, float x, float y)
    {
        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        _networkPlayerSpawnOverrides[slot] = new SpawnPoint(x, y);
        return true;
    }

    public bool TryClearNetworkPlayerSpawnOverride(byte slot)
    {
        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        _networkPlayerSpawnOverrides.Remove(slot);
        return true;
    }

    public bool TryClearNetworkPlayerInputOverride(byte slot)
    {
        if (slot == LocalPlayerSlot)
        {
            SetLocalInput(default);
            return true;
        }

        _additionalNetworkPlayerInputs.Remove(slot);
        _additionalNetworkPlayerPreviousInputs.Remove(slot);
        return IsPlayableNetworkPlayerSlot(slot);
    }

    public PlayerTeam GetNetworkPlayerConfiguredTeam(byte slot)
    {
        return slot switch
        {
            LocalPlayerSlot => LocalPlayerTeam,
            _ when _additionalNetworkPlayerTeams.TryGetValue(slot, out var team) => team,
            _ => GetDefaultNetworkPlayerTeam(slot),
        };
    }

    public bool TrySetNetworkPlayerConfiguredTeam(byte slot, PlayerTeam team)
    {
        switch (slot)
        {
            case LocalPlayerSlot:
                LocalPlayerTeam = team;
                return true;
            default:
                if (!IsPlayableNetworkPlayerSlot(slot))
                {
                    return false;
                }

                _additionalNetworkPlayerTeams[slot] = team;
                return true;
        }
    }

    public CharacterClassDefinition GetNetworkPlayerClassDefinition(byte slot)
    {
        return slot switch
        {
            LocalPlayerSlot => _localPlayerClassDefinition,
            _ when _additionalNetworkPlayerClassDefinitions.TryGetValue(slot, out var definition) => definition,
            _ => CharacterClassCatalog.Scout,
        };
    }

    public bool TrySetNetworkPlayerClassDefinition(byte slot, CharacterClassDefinition definition)
    {
        switch (slot)
        {
            case LocalPlayerSlot:
                _localPlayerClassDefinition = definition;
                return true;
            default:
                if (!IsPlayableNetworkPlayerSlot(slot))
                {
                    return false;
                }

                _additionalNetworkPlayerClassDefinitions[slot] = definition;
                return true;
        }
    }

    public bool TrySetNetworkPlayerGameplayLoadout(byte slot, string loadoutId)
    {
        if (!TryGetOrEnsurePlayableNetworkPlayer(slot, out var player))
        {
            return false;
        }

        return player.TrySelectGameplayLoadout(loadoutId);
    }

    public bool TrySetNetworkPlayerGameplaySecondaryItem(byte slot, string? itemId)
    {
        if (!TryGetOrEnsurePlayableNetworkPlayer(slot, out var player))
        {
            return false;
        }

        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        var normalizedItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim();
        if (!runtimeRegistry.CanUseSecondaryOverrideItem(player.GameplayClassId, player.SelectedGameplayLoadoutId, normalizedItemId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(normalizedItemId) && !player.OwnsGameplayItem(normalizedItemId))
        {
            return false;
        }

        PrimaryWeaponDefinition? weaponDefinition = null;
        if (!string.IsNullOrWhiteSpace(normalizedItemId))
        {
            var selectedLoadout = runtimeRegistry.TryGetLoadout(player.GameplayClassId, player.SelectedGameplayLoadoutId, out var resolvedLoadout)
                ? resolvedLoadout
                : runtimeRegistry.GetDefaultLoadout(player.GameplayClassId);
            if (!string.Equals(selectedLoadout.SecondaryItemId, normalizedItemId, StringComparison.Ordinal))
            {
                weaponDefinition = runtimeRegistry.CreatePrimaryWeaponDefinition(runtimeRegistry.GetRequiredItem(normalizedItemId));
            }
        }

        player.SetExperimentalOffhandWeapon(weaponDefinition);
        return true;
    }

    public bool TrySetNetworkPlayerGameplayAcquiredItem(byte slot, string? itemId)
    {
        if (!TryGetOrEnsurePlayableNetworkPlayer(slot, out var player) || player.ClassId != PlayerClass.Soldier)
        {
            return false;
        }

        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        var normalizedItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim();
        if (!runtimeRegistry.CanUseAcquiredItem(player.GameplayClassId, normalizedItemId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(normalizedItemId) && !player.OwnsGameplayItem(normalizedItemId))
        {
            return false;
        }

        PlayerClass? acquiredWeaponClass = null;
        if (!string.IsNullOrWhiteSpace(normalizedItemId))
        {
            if (!runtimeRegistry.TryResolveBoundPlayerClassForPrimaryItem(normalizedItemId, out var resolvedPlayerClass))
            {
                return false;
            }

            acquiredWeaponClass = resolvedPlayerClass;
        }

        player.SetAcquiredWeapon(acquiredWeaponClass);
        return true;
    }

    public bool TryGrantNetworkPlayerGameplayItem(byte slot, string itemId)
    {
        return TryGetOrEnsurePlayableNetworkPlayer(slot, out var player)
            && player.TryGrantGameplayItem(itemId);
    }

    public bool TryRevokeNetworkPlayerGameplayItem(byte slot, string itemId)
    {
        return TryGetOrEnsurePlayableNetworkPlayer(slot, out var player)
            && player.TryRevokeGameplayItem(itemId);
    }

    public bool TrySetNetworkPlayerGameplayEquippedSlot(byte slot, GameplayEquipmentSlot equippedSlot)
    {
        if (!TryGetOrEnsurePlayableNetworkPlayer(slot, out var player))
        {
            return false;
        }

        return player.TrySelectGameplayEquippedSlot(equippedSlot);
    }

    private bool TryGetOrEnsurePlayableNetworkPlayer(byte slot, out PlayerEntity player)
    {
        if (slot != LocalPlayerSlot && IsPlayableNetworkPlayerSlot(slot))
        {
            EnsureAdditionalNetworkPlayer(slot);
        }

        return TryGetNetworkPlayer(slot, out player);
    }

    private bool TryApplyNetworkPlayerClassChange(byte slot, CharacterClassDefinition definition)
    {
        if (!TrySetNetworkPlayerClassDefinition(slot, definition) || !TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        var pendingTeamSelection = HasPendingNetworkPlayerTeamSelection(slot);
        ConsumePendingNetworkPlayerTeamSelection(slot);

        if (player.IsAlive)
        {
            var classChangeCreatesRemains = !player.IsInSpawnRoom;
            RemoveOwnedSpyArtifacts(player.Id);
            KillPlayer(
                player,
                weaponSpriteName: "DeadKL",
                killFeedMessage: player.IsInSpawnRoom ? null : player.DisplayName + ClassChangeKillFeedSuffix,
                createDeathCam: false,
                spawnRemains: classChangeCreatesRemains,
                forceCorpseRemains: classChangeCreatesRemains,
                recordKillFeed: !player.IsInSpawnRoom);
            if (pendingTeamSelection && MatchRules.Mode != GameModeKind.Arena)
            {
                TrySetNetworkPlayerRespawnTicks(slot, _configuredRespawnTicks);
            }
        }

        player.SetClassDefinition(definition);
        SyncExperimentalGameplayLoadout(slot, player);
        return true;
    }

    private bool HasPendingNetworkPlayerTeamSelection(byte slot)
    {
        return _pendingNetworkPlayerTeamSelections.Contains(slot);
    }

    private void ConsumePendingNetworkPlayerTeamSelection(byte slot)
    {
        _pendingNetworkPlayerTeamSelections.Remove(slot);
    }
}
