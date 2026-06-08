namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool TryPrepareNetworkPlayerJoin(byte slot)
    {
        return TryPrepareNetworkPlayerJoinState(slot);
    }

    public void ForceKillLocalPlayer()
    {
        if (LocalPlayer.IsAlive)
        {
            KillPlayer(LocalPlayer);
        }
    }

    public bool ForceKillNetworkPlayer(byte slot)
    {
        if (!TryGetNetworkPlayer(slot, out var player) || !player.IsAlive)
        {
            return false;
        }

        KillPlayer(player);
        return true;
    }

    public void ForceRespawnLocalPlayer()
    {
        TryForceRespawnNetworkPlayer(LocalPlayerSlot);
    }

    public void PrepareLocalPlayerJoin()
    {
        TryPrepareNetworkPlayerJoinState(LocalPlayerSlot);
    }

    public void CompleteLocalPlayerJoin(PlayerClass playerClass)
    {
        if (CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(playerClass, out var binding))
        {
            CompleteLocalPlayerJoin(binding.ClassId);
        }
    }

    public void CompleteLocalPlayerJoin(string gameplayClassId)
    {
        TryCompleteNetworkPlayerJoinState(LocalPlayerSlot, gameplayClassId);
    }

    public bool TryReleaseNetworkPlayerSlot(byte slot)
    {
        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        if (slot != LocalPlayerSlot)
        {
            EnsureAdditionalNetworkPlayer(slot);
        }

        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        TryClearNetworkPlayerInputOverride(slot);
        TryDropCarriedIntel(player);
        TrySetNetworkPlayerReady(slot, ready: false);
        RemoveOwnedSpyArtifacts(player.Id);
        RemoveOwnedSentries(player.Id);
        RemoveOwnedMines(player.Id);
        RemoveOwnedProjectiles(player.Id);
        ClearDominationsForPlayer(player);
        TrySetNetworkPlayerAwaitingJoin(slot, true);
        TrySetNetworkPlayerRespawnTicks(slot, 0);
        SetNetworkPlayerDeathCam(slot, null);
        TrySetNetworkPlayerClassDefinition(slot, CharacterClassCatalog.Scout);
        TrySetNetworkPlayerConfiguredTeam(slot, GetDefaultNetworkPlayerTeam(slot));
        ConsumePendingNetworkPlayerTeamSelection(slot);
        _networkPlayerSpawnOverrides.Remove(slot);
        _networkPlayerMovementSpeedScaleOverrides.Remove(slot);
        _networkPlayerGravityScaleOverrides.Remove(slot);
        _networkPlayerMaxHealthOverrides.Remove(slot);
        player.SetClassDefinition(GetNetworkPlayerClassDefinition(slot));
        SyncExperimentalGameplayLoadout(slot, player);
        ApplyServerGameplayTuning(slot, player);
        player.SetDisplayName(GetNetworkPlayerDefaultName(slot));
        player.SetBadgeMask(0);
        player.ResetRoundStats();
        player.ClearMedicHealingTarget();
        foreach (var otherPlayer in EnumerateSimulatedPlayers())
        {
            if (otherPlayer.MedicHealTargetId == player.Id)
            {
                otherPlayer.ClearMedicHealingTarget();
            }
        }

        player.Kill();
        SetNetworkPlayerEnabled(slot, slot == LocalPlayerSlot);
        return true;
    }

    public bool TrySetNetworkPlayerRespawnTicks(byte slot, int ticks)
    {
        switch (slot)
        {
            case LocalPlayerSlot:
                LocalPlayerRespawnTicks = ticks;
                return true;
            default:
                if (!IsPlayableNetworkPlayerSlot(slot))
                {
                    return false;
                }

                _additionalNetworkPlayerRespawnTicks[slot] = ticks;
                return true;
        }
    }

    public bool TrySetNetworkPlayerAwaitingJoin(byte slot, bool awaitingJoin)
    {
        switch (slot)
        {
            case LocalPlayerSlot:
                _localPlayerAwaitingJoin = awaitingJoin;
                return true;
            default:
                if (!IsPlayableNetworkPlayerSlot(slot))
                {
                    return false;
                }

                _additionalNetworkPlayerAwaitingJoin[slot] = awaitingJoin;
                return true;
        }
    }

    private bool TryForceRespawnNetworkPlayer(byte slot, bool playRespawnSound = true)
    {
        if (slot != LocalPlayerSlot)
        {
            EnsureAdditionalNetworkPlayer(slot);
        }

        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        SetNetworkPlayerEnabled(slot, true);
        TrySetNetworkPlayerAwaitingJoin(slot, false);
        TrySetNetworkPlayerRespawnTicks(slot, 0);
        SetNetworkPlayerDeathCam(slot, null);
        ConsumePendingNetworkPlayerTeamSelection(slot);

        var team = GetNetworkPlayerConfiguredTeam(slot);
        player.SetClassDefinition(GetNetworkPlayerClassDefinition(slot));
        if (!SpawnPlayerResolved(player, team, ReserveSpawn(player, team, slot), playRespawnSound: playRespawnSound))
        {
            return false;
        }

        SyncExperimentalGameplayLoadout(slot, player);
        return true;
    }

    private bool TryPrepareNetworkPlayerJoinState(byte slot)
    {
        if (slot != LocalPlayerSlot)
        {
            EnsureAdditionalNetworkPlayer(slot);
        }

        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        SetNetworkPlayerEnabled(slot, true);
        TrySetNetworkPlayerAwaitingJoin(slot, true);
        TrySetNetworkPlayerRespawnTicks(slot, 0);
        SetNetworkPlayerDeathCam(slot, null);
        ConsumePendingNetworkPlayerTeamSelection(slot);

        ClearDominationsForPlayer(player);
        player.ClearMedicHealingTarget();
        player.Kill();

        if (slot == LocalPlayerSlot && FriendlyDummyEnabled)
        {
            FriendlyDummy.ClearMedicHealingTarget();
            FriendlyDummy.Kill();
        }

        return true;
    }

    private bool TryCompleteNetworkPlayerJoinState(byte slot, PlayerClass playerClass)
    {
        return TryCompleteNetworkPlayerJoinState(slot, CharacterClassCatalog.GetDefinition(playerClass).GameplayClassId);
    }

    private bool TryCompleteNetworkPlayerJoinState(byte slot, string gameplayClassId)
    {
        var definition = CharacterClassCatalog.GetDefinition(gameplayClassId);
        if (slot != LocalPlayerSlot)
        {
            EnsureAdditionalNetworkPlayer(slot);
        }

        if (!TrySetNetworkPlayerClassDefinition(slot, definition) || !TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        player.SetClassDefinition(definition);
        SyncExperimentalGameplayLoadout(slot, player);
        ConsumePendingNetworkPlayerTeamSelection(slot);
        return TryForceRespawnNetworkPlayer(slot, playRespawnSound: false);
    }
}
