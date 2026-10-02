using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class NetworkPlayerSystem
{
    public void SetLocalInput(PlayerInputSnapshot input)
    {
        _host.LocalState.Input = input;
    }

    public void SetLocalPreviousInput(PlayerInputSnapshot input)
    {
        _host.LocalState.PreviousInput = input;
    }

    public void SetEnemyInput(PlayerInputSnapshot input)
    {
        _host.DummyState.EnemyInput = input;
        _host.DummyState.EnemyInputOverrideActive = true;
    }

    public void ClearEnemyInputOverride()
    {
        _host.DummyState.EnemyInput = default;
        _host.DummyState.PreviousEnemyInput = default;
        _host.DummyState.EnemyInputOverrideActive = false;
    }

    public void SetLocalPlayerName(string displayName)
    {
        _host.LocalPlayer.SetDisplayName(displayName);
    }

    public void SetLocalPlayerBadgeMask(ulong badgeMask)
    {
        _host.LocalPlayer.SetBadgeMask(badgeMask);
    }

    public void SetLocalPlayerChatBubble(int frameIndex)
    {
        _host.LocalPlayer.TriggerChatBubble(frameIndex);
    }

    public bool TrySetNetworkPlayerName(byte slot, string displayName)
    {
        switch (slot)
        {
            case SimulationConstants.LocalPlayerSlot:
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
            case SimulationConstants.LocalPlayerSlot:
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
            case SimulationConstants.LocalPlayerSlot:
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

    public bool TrySetNetworkPlayerTeam(byte slot, PlayerTeam team, bool respawnLivePlayerImmediately = false)
    {
        var changesTeam = TryGetNetworkPlayer(slot, out var configuredPlayer)
            && configuredPlayer.Team != team;
        if (changesTeam)
        {
            _host.TryDropCarriedIntel(configuredPlayer);
            _host.ClearDominationsForPlayer(configuredPlayer);
        }

        if (!TrySetNetworkPlayerConfiguredTeam(slot, team))
        {
            return false;
        }

        if (changesTeam)
        {
            _host.ClearLastToDieSniperMarksTargeting(slot);
            configuredPlayer.ResetLastToDieSniperDynamicState();
        }

        if (slot == SimulationConstants.LocalPlayerSlot)
        {
            if (_host.FriendlyDummyEnabled && !IsNetworkPlayerAwaitingJoin(SimulationConstants.LocalPlayerSlot))
            {
                var friendlySpawn = _host.FindFriendlyDummySpawnNearLocalPlayer();
                _host.FriendlyDummy.SetClassDefinition(_host.LocalState.FriendlyDummyClassDefinition);
                _host.SpawnPlayerResolved(_host.FriendlyDummy, GetNetworkPlayerConfiguredTeam(SimulationConstants.LocalPlayerSlot), friendlySpawn.X, friendlySpawn.Y);
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
        _host.SyncExperimentalGameplayLoadout(slot, player);
        if (player.IsAlive && player.Team != team && !respawnLivePlayerImmediately)
        {
            PrepareNetworkPlayerTeamChangeRespawn(slot, player, team);
            return true;
        }

        _host.SpawnPlayerResolved(player, team, _host.ReserveSpawn(player, team, slot), playRespawnSound: true);
        return true;
    }

    internal void PrepareNetworkPlayerTeamChangeRespawn(byte slot, PlayerEntity player, PlayerTeam team)
    {
        var wasInSpawnRoom = player.IsInSpawnRoom;
        _host.RemoveOwnedSpyArtifacts(player.Id);
        player.ClearMedicHealingTarget();
        foreach (var otherPlayer in _host.EnumerateSimulatedPlayers())
        {
            if (otherPlayer.MedicHealTargetId == player.Id)
            {
                otherPlayer.ClearMedicHealingTarget();
            }
        }

        player.Kill();
        player.SetPendingRespawnTeam(team);
        _host.SetNetworkPlayerDeathCam(slot, null);
        var respawnTicks = _host.MatchRules.Mode == GameModeKind.Arena
            ? 0
            : wasInSpawnRoom
                ? 1
                : _host.MatchSettings.RespawnTicks;
        TrySetNetworkPlayerRespawnTicks(slot, respawnTicks);
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

        _host.PlayerRegistry.PendingTeamSelections.Add(slot);
        return true;
    }

    public bool TryApplyNetworkPlayerClassSelection(byte slot, PlayerClass playerClass)
    {
        return CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(playerClass, out var binding)
            && TryApplyNetworkPlayerClassSelection(slot, binding.ClassId);
    }

    public bool TryForceNetworkPlayerClassSelectionAndRespawn(byte slot, PlayerClass playerClass)
    {
        return CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(playerClass, out var binding)
            && TryForceNetworkPlayerClassSelectionAndRespawn(slot, binding.ClassId);
    }

    public bool TryForceNetworkPlayerClassSelectionAndRespawn(byte slot, string gameplayClassId)
    {
        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        var definition = _host.ResolveMapForcedClassDefinition(slot, CharacterClassCatalog.GetDefinition(gameplayClassId));
        if (!_host.CanApplyNetworkPlayerClassLimit(slot, definition)
            || !TrySetNetworkPlayerClassDefinition(slot, definition)
            || !TryGetOrEnsurePlayableNetworkPlayer(slot, out var player))
        {
            return false;
        }

        player.SetClassDefinition(definition);
        _host.SyncExperimentalGameplayLoadout(slot, player);
        ConsumePendingNetworkPlayerTeamSelection(slot);
        return TryForceRespawnNetworkPlayer(slot, playRespawnSound: false);
    }

    public bool TryApplyNetworkPlayerClassSelection(byte slot, string gameplayClassId)
    {
        var resolvedDefinition = _host.ResolveMapForcedClassDefinition(slot, CharacterClassCatalog.GetDefinition(gameplayClassId));
        gameplayClassId = resolvedDefinition.GameplayClassId;
        if (IsNetworkPlayerAwaitingJoin(slot))
        {
            return TryCompleteNetworkPlayerJoinState(slot, gameplayClassId);
        }

        if (slot == SimulationConstants.LocalPlayerSlot)
        {
            return _host.TrySetLocalClass(gameplayClassId);
        }

        var definition = resolvedDefinition;
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
        TrySetNetworkPlayerTeam(SimulationConstants.LocalPlayerSlot, team);
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
        TrySetNetworkPlayerClassDefinition(SimulationConstants.LocalPlayerSlot, definition);
        _host.LocalPlayer.SetClassDefinition(definition);
        _host.SyncExperimentalGameplayLoadout(SimulationConstants.LocalPlayerSlot, _host.LocalPlayer);
    }

    public bool TrySetNetworkPlayerInput(byte slot, PlayerInputSnapshot input)
        => TrySetNetworkPlayerInput(slot, input, InputButtons.None);

    /// <summary>
    /// Sets a network input frame and optionally declares one-shot buttons as
    /// explicit rising edges. Protocol-64 commands use this seam so two
    /// adjacent jumps cannot be collapsed into one held-button transition.
    /// With explicit presses, held state cannot create a second press when its
    /// reliable command arrives on a different simulation tick.
    /// </summary>
    public bool TrySetNetworkPlayerInput(
        byte slot,
        PlayerInputSnapshot input,
        InputButtons forcedPressedButtons,
        bool requireExplicitPresses = false)
    {
        if (slot == SimulationConstants.LocalPlayerSlot)
        {
            SetLocalInput(input);
            _host.PlayerRegistry.ForcedPressedButtons[slot] = (forcedPressedButtons, requireExplicitPresses);
            return true;
        }

        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        EnsureAdditionalNetworkPlayer(slot);
        _host.PlayerRegistry.Inputs[slot] = input;
        _host.PlayerRegistry.ForcedPressedButtons[slot] = (forcedPressedButtons, requireExplicitPresses);
        return true;
    }

    public bool TrySetNetworkPlayerSpawnOverride(byte slot, float x, float y)
    {
        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        _host.PlayerRegistry.SpawnOverrides[slot] = new SpawnPoint(x, y);
        return true;
    }

    public bool TryClearNetworkPlayerSpawnOverride(byte slot)
    {
        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        _host.PlayerRegistry.SpawnOverrides.Remove(slot);
        return true;
    }

    public bool TryClearNetworkPlayerInputOverride(byte slot)
    {
        if (slot == SimulationConstants.LocalPlayerSlot)
        {
            SetLocalInput(default);
            _host.PlayerRegistry.ForcedPressedButtons.Remove(slot);
            return true;
        }

        _host.PlayerRegistry.Inputs.Remove(slot);
        _host.PlayerRegistry.PreviousInputs.Remove(slot);
        _host.PlayerRegistry.ForcedPressedButtons.Remove(slot);
        return IsPlayableNetworkPlayerSlot(slot);
    }

    public PlayerTeam GetNetworkPlayerConfiguredTeam(byte slot)
    {
        return slot switch
        {
            SimulationConstants.LocalPlayerSlot => _host.LocalPlayerTeam,
            _ when _host.PlayerRegistry.Teams.TryGetValue(slot, out var team) => team,
            _ => GetDefaultNetworkPlayerTeam(slot),
        };
    }

    public bool TrySetNetworkPlayerConfiguredTeam(byte slot, PlayerTeam team)
    {
        switch (slot)
        {
            case SimulationConstants.LocalPlayerSlot:
                _host.LocalPlayerTeam = team;
                return true;
            default:
                if (!IsPlayableNetworkPlayerSlot(slot))
                {
                    return false;
                }

                _host.PlayerRegistry.Teams[slot] = team;
                return true;
        }
    }

    public CharacterClassDefinition GetNetworkPlayerClassDefinition(byte slot)
    {
        return slot switch
        {
            SimulationConstants.LocalPlayerSlot => _host.LocalState.PlayerClassDefinition,
            _ when _host.PlayerRegistry.ClassDefinitions.TryGetValue(slot, out var definition) => definition,
            _ => CharacterClassCatalog.Scout,
        };
    }

    public bool TrySetNetworkPlayerClassDefinition(byte slot, CharacterClassDefinition definition)
    {
        switch (slot)
        {
            case SimulationConstants.LocalPlayerSlot:
                _host.LocalState.PlayerClassDefinition = definition;
                return true;
            default:
                if (!IsPlayableNetworkPlayerSlot(slot))
                {
                    return false;
                }

                _host.PlayerRegistry.ClassDefinitions[slot] = definition;
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

    public bool TrySetNetworkPlayerGameplayPrimaryItem(byte slot, string itemId, bool refillAmmo = true)
    {
        return TryGetOrEnsurePlayableNetworkPlayer(slot, out var player)
            && player.TrySelectGameplayPrimaryItem(itemId, refillAmmo);
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
            weaponDefinition = runtimeRegistry.CreatePrimaryWeaponDefinition(runtimeRegistry.GetRequiredItem(normalizedItemId));
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

    internal bool TryGetOrEnsurePlayableNetworkPlayer(byte slot, out PlayerEntity player)
    {
        if (slot != SimulationConstants.LocalPlayerSlot && IsPlayableNetworkPlayerSlot(slot))
        {
            EnsureAdditionalNetworkPlayer(slot);
        }

        return TryGetNetworkPlayer(slot, out player);
    }

    internal bool TryApplyNetworkPlayerClassChange(byte slot, CharacterClassDefinition definition, bool enforceClassLimit = true)
    {
        definition = _host.ResolveMapForcedClassDefinition(slot, definition);
        if ((enforceClassLimit && !_host.CanApplyNetworkPlayerClassLimit(slot, definition))
            || !TrySetNetworkPlayerClassDefinition(slot, definition)
            || !TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        var pendingTeamSelection = HasPendingNetworkPlayerTeamSelection(slot);
        ConsumePendingNetworkPlayerTeamSelection(slot);

        if (player.IsAlive)
        {
            var wasInSpawnRoom = player.IsInSpawnRoom;
            var classChangeCreatesRemains = !player.IsInSpawnRoom;
            _host.RemoveOwnedSpyArtifacts(player.Id);
            _host.KillPlayer(
                player,
                weaponSpriteName: "DeadKL",
                killFeedMessage: player.IsInSpawnRoom ? null : player.DisplayName + SimulationConstants.ClassChangeKillFeedSuffix,
                createDeathCam: false,
                spawnRemains: classChangeCreatesRemains,
                forceCorpseRemains: classChangeCreatesRemains,
                recordKillFeed: !player.IsInSpawnRoom);
            if (pendingTeamSelection && _host.MatchRules.Mode != GameModeKind.Arena)
            {
                TrySetNetworkPlayerRespawnTicks(slot, wasInSpawnRoom ? 1 : _host.MatchSettings.RespawnTicks);
            }
        }

        player.SetClassDefinition(definition);
        _host.SyncExperimentalGameplayLoadout(slot, player);
        return true;
    }

    internal bool HasPendingNetworkPlayerTeamSelection(byte slot)
    {
        return _host.PlayerRegistry.PendingTeamSelections.Contains(slot);
    }

    internal void ConsumePendingNetworkPlayerTeamSelection(byte slot)
    {
        _host.PlayerRegistry.PendingTeamSelections.Remove(slot);
    }
}
