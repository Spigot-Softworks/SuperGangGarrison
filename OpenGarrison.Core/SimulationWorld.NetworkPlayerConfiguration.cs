namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public void SetLocalInput(PlayerInputSnapshot input)
    {
        _localInput = input;
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
                if (IsPlayableNetworkPlayerSlot(slot))
                {
                    EnsureAdditionalNetworkPlayer(slot);
                }

                if (!TryGetNetworkPlayer(slot, out var player))
                {
                    return false;
                }

                player.SetDisplayName(displayName);
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

    public bool TrySetNetworkPlayerTeam(byte slot, PlayerTeam team)
    {
        if (TryGetNetworkPlayer(slot, out var configuredPlayer) && configuredPlayer.Team != team)
        {
            ClearDominationsForPlayer(configuredPlayer);
        }

        if (!TrySetNetworkPlayerConfiguredTeam(slot, team))
        {
            return false;
        }

        if (slot == LocalPlayerSlot)
        {
            for (var index = 0; index < NetworkPlayerSlots.Count; index += 1)
            {
                var otherSlot = NetworkPlayerSlots[index];
                if (otherSlot == LocalPlayerSlot || !IsNetworkPlayerEnabled(otherSlot) || !TryGetNetworkPlayer(otherSlot, out var otherPlayer))
                {
                    continue;
                }

                otherPlayer.SetClassDefinition(GetNetworkPlayerClassDefinition(otherSlot));
                if (IsNetworkPlayerAwaitingJoin(otherSlot))
                {
                    otherPlayer.Kill();
                }
                else
                {
                    var otherTeam = GetNetworkPlayerConfiguredTeam(otherSlot);
                    SpawnPlayerResolved(otherPlayer, otherTeam, ReserveSpawn(otherPlayer, otherTeam));
                }
            }

            if (FriendlyDummyEnabled && !IsNetworkPlayerAwaitingJoin(LocalPlayerSlot))
            {
                var friendlySpawn = FindFriendlyDummySpawnNearLocalPlayer();
                FriendlyDummy.SetClassDefinition(_friendlyDummyClassDefinition);
                SpawnPlayerResolved(FriendlyDummy, GetNetworkPlayerConfiguredTeam(LocalPlayerSlot), friendlySpawn.X, friendlySpawn.Y);
            }

            return true;
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
        SpawnPlayerResolved(player, team, ReserveSpawn(player, team));
        return true;
    }

    public bool TryApplyNetworkPlayerClassSelection(byte slot, PlayerClass playerClass)
    {
        if (IsNetworkPlayerAwaitingJoin(slot))
        {
            return TryCompleteNetworkPlayerJoinState(slot, playerClass);
        }

        if (slot == LocalPlayerSlot)
        {
            return TrySetLocalClass(playerClass);
        }

        var definition = CharacterClassCatalog.GetDefinition(playerClass);
        if (!TryGetNetworkPlayer(slot, out var player) || definition.Id == GetNetworkPlayerClassDefinition(slot).Id)
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
        var definition = CharacterClassCatalog.GetDefinition(playerClass);
        TrySetNetworkPlayerClassDefinition(LocalPlayerSlot, definition);
        LocalPlayer.SetClassDefinition(definition);
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

    private bool TryApplyNetworkPlayerClassChange(byte slot, CharacterClassDefinition definition)
    {
        if (!TrySetNetworkPlayerClassDefinition(slot, definition) || !TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        if (player.IsAlive)
        {
            RemoveOwnedSpyArtifacts(player.Id);
            KillPlayer(
                player,
                weaponSpriteName: "DeadKL",
                killFeedMessage: player.DisplayName + ClassChangeKillFeedSuffix,
                createDeathCam: false,
                spawnRemains: !player.IsInSpawnRoom);
        }

        player.SetClassDefinition(definition);
        return true;
    }
}
