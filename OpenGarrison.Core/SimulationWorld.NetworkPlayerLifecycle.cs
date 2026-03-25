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
        TryCompleteNetworkPlayerJoinState(LocalPlayerSlot, playerClass);
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
        player.SetClassDefinition(GetNetworkPlayerClassDefinition(slot));
        player.SetDisplayName(GetNetworkPlayerDefaultName(slot));
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

    private bool TryForceRespawnNetworkPlayer(byte slot)
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

        var team = GetNetworkPlayerConfiguredTeam(slot);
        player.SetClassDefinition(GetNetworkPlayerClassDefinition(slot));
        SpawnPlayerResolved(player, team, ReserveSpawn(player, team));
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
        var definition = CharacterClassCatalog.GetDefinition(playerClass);
        if (slot != LocalPlayerSlot)
        {
            EnsureAdditionalNetworkPlayer(slot);
        }

        if (!TrySetNetworkPlayerClassDefinition(slot, definition) || !TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        player.SetClassDefinition(definition);
        return TryForceRespawnNetworkPlayer(slot);
    }
}
