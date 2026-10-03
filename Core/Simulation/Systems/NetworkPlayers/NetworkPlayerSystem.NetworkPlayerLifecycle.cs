namespace OpenGarrison.Core;

internal sealed partial class NetworkPlayerSystem
{
    public bool TryPrepareNetworkPlayerJoin(byte slot)
    {
        return TryPrepareNetworkPlayerJoinState(slot);
    }

    public void ForceKillLocalPlayer()
    {
        if (_host.LocalPlayer.IsAlive)
        {
            _host.PlayerDeaths.KillPlayer(_host.LocalPlayer);
        }
    }

    public bool ForceKillNetworkPlayer(byte slot)
    {
        if (!TryGetNetworkPlayer(slot, out var player) || !player.IsAlive)
        {
            return false;
        }

        _host.PlayerDeaths.KillPlayer(player);
        return true;
    }

    public bool TrySetNetworkPlayerAutomaticRespawnSuppressed(byte slot, bool suppressed)
    {
        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        if (suppressed)
        {
            _host.PlayerRegistry.AutomaticRespawnSuppressedSlots.Add(slot);
        }
        else
        {
            _host.PlayerRegistry.AutomaticRespawnSuppressedSlots.Remove(slot);
        }

        return true;
    }

    public bool IsNetworkPlayerAutomaticRespawnSuppressed(PlayerEntity player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return TryGetPlayerNetworkSlot(player, out var slot)
            && _host.PlayerRegistry.AutomaticRespawnSuppressedSlots.Contains(slot);
    }

    public void ForceRespawnLocalPlayer()
    {
        TryForceRespawnNetworkPlayer(SimulationConstants.LocalPlayerSlot);
    }

    public void PrepareLocalPlayerJoin()
    {
        TryPrepareNetworkPlayerJoinState(SimulationConstants.LocalPlayerSlot);
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
        TryCompleteNetworkPlayerJoinState(SimulationConstants.LocalPlayerSlot, gameplayClassId);
    }

    public bool TryReleaseNetworkPlayerSlot(byte slot)
    {
        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        if (slot != SimulationConstants.LocalPlayerSlot)
        {
            EnsureAdditionalNetworkPlayer(slot);
        }

        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        _host.LastToDieRules.TryFailLastToDieSpyAfterlifeOnDisconnect(slot, player);
        TryClearNetworkPlayerInputOverride(slot);
        _host.ObjectiveRules.TryDropCarriedIntel(player);
        _host.ReadyUp.TrySetNetworkPlayerReady(slot, ready: false);
        _host.Projectiles.RemoveOwnedSpyArtifacts(player.Id);
        _host.Projectiles.RemoveOwnedSentries(player.Id);
        _host.Projectiles.RemoveOwnedMines(player.Id);
        _host.Projectiles.RemoveOwnedProjectiles(player.Id);
        _host.LastToDieRules.ClearLastToDieStatusEffectsForReleasedPlayer(player.Id);
        _host.LastToDieRules.ClearLastToDieSniperMarksTargeting(slot);
        _host.LastToDieState.PerkRuntimesBySlot.Remove(slot);
        _host.LastToDieState.LegacyGameplaySettingsBySlot.Remove(slot);
        player.ClearLastToDieWeaponProfile();
        player.ClearLastToDiePerkModifiers();
        _host.CombatFeedback.ClearDominationsForPlayer(player);
        TrySetNetworkPlayerAwaitingJoin(slot, true);
        TrySetNetworkPlayerRespawnTicks(slot, 0);
        _host.PlayerDeaths.SetNetworkPlayerDeathCam(slot, null);
        TrySetNetworkPlayerClassDefinition(slot, CharacterClassCatalog.Scout);
        TrySetNetworkPlayerConfiguredTeam(slot, GetDefaultNetworkPlayerTeam(slot));
        ConsumePendingNetworkPlayerTeamSelection(slot);
        _host.PlayerRegistry.SpawnOverrides.Remove(slot);
        _host.PlayerRegistry.AutomaticRespawnSuppressedSlots.Remove(slot);
        _host.PlayerRegistry.MapSpawnClassBehaviorBypassSlots.Remove(slot);
        _host.PlayerRegistry.MovementSpeedScaleOverrides.Remove(slot);
        _host.PlayerRegistry.GravityScaleOverrides.Remove(slot);
        _host.PlayerRegistry.MaxHealthOverrides.Remove(slot);
        _host.PlayerRegistry.BotSlots.Remove(slot);
        player.SetClassDefinition(GetNetworkPlayerClassDefinition(slot));
        _host.ExperimentalRules.SyncExperimentalGameplayLoadout(slot, player);
        _host.ServerTuning.ApplyServerGameplayTuning(slot, player);
        player.SetDisplayName(GetNetworkPlayerDefaultName(slot));
        player.SetBadgeMask(0);
        player.ResetRoundStats();
        player.ClearMedicHealingTarget();
        foreach (var otherPlayer in _host.EnumerateSimulatedPlayers())
        {
            if (otherPlayer.MedicHealTargetId == player.Id)
            {
                otherPlayer.ClearMedicHealingTarget();
            }
        }

        player.Kill();
        SetNetworkPlayerEnabled(slot, slot == SimulationConstants.LocalPlayerSlot);
        return true;
    }

    public bool TrySetNetworkPlayerRespawnTicks(byte slot, int ticks)
    {
        switch (slot)
        {
            case SimulationConstants.LocalPlayerSlot:
                _host.LocalPlayerRespawnTicks = ticks;
                return true;
            default:
                if (!IsPlayableNetworkPlayerSlot(slot))
                {
                    return false;
                }

                _host.PlayerRegistry.RespawnTicks[slot] = ticks;
                return true;
        }
    }

    public bool TrySetNetworkPlayerAwaitingJoin(byte slot, bool awaitingJoin)
    {
        switch (slot)
        {
            case SimulationConstants.LocalPlayerSlot:
                _host.LocalState.PlayerAwaitingJoin = awaitingJoin;
                return true;
            default:
                if (!IsPlayableNetworkPlayerSlot(slot))
                {
                    return false;
                }

                _host.PlayerRegistry.AwaitingJoin[slot] = awaitingJoin;
                return true;
        }
    }

    internal bool TryForceRespawnNetworkPlayer(byte slot, bool playRespawnSound = true)
    {
        if (slot != SimulationConstants.LocalPlayerSlot)
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
        _host.PlayerDeaths.SetNetworkPlayerDeathCam(slot, null);
        ConsumePendingNetworkPlayerTeamSelection(slot);

        var team = GetNetworkPlayerConfiguredTeam(slot);
        player.SetClassDefinition(GetNetworkPlayerClassDefinition(slot));
        if (!_host.Spawns.SpawnPlayerResolved(player, team, _host.Spawns.ReserveSpawn(player, team, slot), playRespawnSound: playRespawnSound))
        {
            return false;
        }

        _host.ExperimentalRules.SyncExperimentalGameplayLoadout(slot, player);
        return true;
    }

    internal bool TryPrepareNetworkPlayerJoinState(byte slot)
    {
        if (!IsPlayableNetworkPlayerSlot(slot))
        {
            return false;
        }

        if (slot != SimulationConstants.LocalPlayerSlot)
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
        _host.PlayerDeaths.SetNetworkPlayerDeathCam(slot, null);
        ConsumePendingNetworkPlayerTeamSelection(slot);

        _host.CombatFeedback.ClearDominationsForPlayer(player);
        _host.LastToDieRules.ClearLastToDieStatusEffectsForTarget(player.Id);
        player.ClearMedicHealingTarget();
        player.Kill();

        if (slot == SimulationConstants.LocalPlayerSlot && _host.FriendlyDummyEnabled)
        {
            _host.FriendlyDummy.ClearMedicHealingTarget();
            _host.FriendlyDummy.Kill();
        }

        return true;
    }

    internal bool TryCompleteNetworkPlayerJoinState(byte slot, PlayerClass playerClass)
    {
        return TryCompleteNetworkPlayerJoinState(slot, CharacterClassCatalog.GetDefinition(playerClass).GameplayClassId);
    }

    internal bool TryCompleteNetworkPlayerJoinState(byte slot, string gameplayClassId)
    {
        var definition = _host.ClassRules.ResolveMapForcedClassDefinition(slot, CharacterClassCatalog.GetDefinition(gameplayClassId));
        if (slot != SimulationConstants.LocalPlayerSlot)
        {
            EnsureAdditionalNetworkPlayer(slot);
        }

        if (!_host.ClassRules.CanApplyNetworkPlayerClassLimit(slot, definition)
            || !TrySetNetworkPlayerClassDefinition(slot, definition)
            || !TryGetNetworkPlayer(slot, out var player))
        {
            return false;
        }

        player.SetClassDefinition(definition);
        _host.ExperimentalRules.SyncExperimentalGameplayLoadout(slot, player);
        ConsumePendingNetworkPlayerTeamSelection(slot);
        return TryForceRespawnNetworkPlayer(slot, playRespawnSound: false);
    }
}
