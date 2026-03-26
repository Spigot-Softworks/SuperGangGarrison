namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public void ConfigureMatchDefaults(int? timeLimitMinutes = null, int? capLimit = null, int? respawnSeconds = null)
    {
        if (timeLimitMinutes.HasValue)
        {
            _configuredTimeLimitMinutes = Math.Clamp(timeLimitMinutes.Value, 1, 255);
        }

        if (capLimit.HasValue)
        {
            _configuredCapLimit = Math.Clamp(capLimit.Value, 1, 255);
        }

        if (respawnSeconds.HasValue)
        {
            var clampedSeconds = Math.Clamp(respawnSeconds.Value, 0, 255);
            _configuredRespawnTicks = Math.Max(1, clampedSeconds * Config.TicksPerSecond);
        }

        MatchRules = CreateDefaultMatchRules(Level.Mode);
        MatchState = CreateInitialMatchState(MatchRules);
    }

    public void SetCapLimit(int capLimit)
    {
        _configuredCapLimit = Math.Clamp(capLimit, 1, 255);
        MatchRules = MatchRules with
        {
            CapLimit = _configuredCapLimit,
        };
    }

    public bool TryLoadLevel(string levelName)
    {
        return TryLoadLevel(levelName, mapAreaIndex: 1, preservePlayerStats: false);
    }

    public bool TryLoadLevel(string levelName, int mapAreaIndex, bool preservePlayerStats)
    {
        var nextLevel = SimpleLevelFactory.CreateImportedLevel(levelName, mapAreaIndex);
        if (nextLevel is null)
        {
            return false;
        }

        Level = nextLevel;
        MatchRules = CreateDefaultMatchRules(Level.Mode);
        ResetModeStateForNewMap();
        RestartCurrentRound(preservePlayerStats);
        return true;
    }

    public bool ApplyPendingMapChange(string levelName, int mapAreaIndex, bool preservePlayerStats)
    {
        if (!_mapChangeReady)
        {
            return false;
        }

        if (!TryLoadLevel(levelName, mapAreaIndex, preservePlayerStats))
        {
            RestartCurrentRound(preservePlayerStats: false);
            return false;
        }

        _mapChangeReady = false;
        return true;
    }

    private bool AdvancePendingMapChange()
    {
        if (_pendingMapChangeTicks < 0)
        {
            return false;
        }

        if (_pendingMapChangeTicks == 0)
        {
            if (_autoRestartOnMapChange)
            {
                RestartCurrentRound(preservePlayerStats: false);
                return false;
            }

            _mapChangeReady = true;
            return false;
        }

        _pendingMapChangeTicks -= 1;
        return false;
    }

    private void QueuePendingMapChange()
    {
        if (_pendingMapChangeTicks >= 0)
        {
            return;
        }

        _pendingMapChangeTicks = PendingMapChangeTicks;
        _mapChangeReady = false;
    }

    private void RestartCurrentRound(bool preservePlayerStats)
    {
        _pendingMapChangeTicks = -1;
        _mapChangeReady = false;
        if (MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            RedCaps = 0;
            BlueCaps = 0;
        }

        if (!preservePlayerStats)
        {
            ClearAllDominations();
            LocalPlayer.ResetRoundStats();
            EnemyPlayer.ResetRoundStats();
            FriendlyDummy.ResetRoundStats();
            foreach (var player in _additionalNetworkPlayersBySlot.Values)
            {
                player.ResetRoundStats();
            }
        }

        MatchState = CreateInitialMatchState(MatchRules);
        RedIntel = CreateIntelState(PlayerTeam.Red);
        BlueIntel = CreateIntelState(PlayerTeam.Blue);
        ResetModeStateForNewRound();
        TrySetNetworkPlayerRespawnTicks(LocalPlayerSlot, 0);
        _enemyDummyRespawnTicks = 0;
        LocalDeathCam = null;
        _killFeedTrimTicks = 0;
        _combatTraces.Clear();
        _killFeed.Clear();
        _pendingSoundEvents.Clear();
        _pendingVisualEvents.Clear();
        _pendingDamageEvents.Clear();
        _nextRedSpawnIndex = 0;
        _nextBlueSpawnIndex = 0;
        ClearDynamicEntities();
        RespawnPlayersForNewRound();
    }

    private void ResetModeStateForNewMap()
    {
        RedCaps = 0;
        BlueCaps = 0;

        if (MatchRules.Mode != GameModeKind.ControlPoint)
        {
            _controlPoints.Clear();
            _controlPointZones.Clear();
            _controlPointSetupMode = false;
            _controlPointSetupTicksRemaining = 0;
        }
        if (MatchRules.Mode != GameModeKind.Generator)
        {
            _generators.Clear();
        }
        UpdateControlPointSetupGates();

        _arenaRedConsecutiveWins = 0;
        _arenaBlueConsecutiveWins = 0;

        ResetModeStateForNewRound();
    }

    private void ResetModeStateForNewRound()
    {
        _arenaPointTeam = null;
        _arenaCappingTeam = null;
        _arenaCappingTicks = 0f;
        _arenaCappers = 0;
        _arenaUnlockTicksRemaining = MatchRules.Mode == GameModeKind.Arena ? ArenaPointUnlockTicksDefault : 0;

        if (MatchRules.Mode == GameModeKind.ControlPoint)
        {
            ResetControlPointStateForNewRound();
        }

        if (MatchRules.Mode == GameModeKind.Generator)
        {
            ResetGeneratorStateForNewRound();
        }
        else
        {
            _generators.Clear();
        }
    }

    private void ClearDynamicEntities()
    {
        RemoveEntities(_shots);
        RemoveEntities(_bubbles);
        RemoveEntities(_blades);
        RemoveEntities(_needles);
        RemoveEntities(_revolverShots);
        RemoveEntities(_stabAnimations);
        RemoveEntities(_stabMasks);
        RemoveEntities(_flames);
        RemoveEntities(_rockets);
        RemoveEntities(_mines);
        RemoveEntities(_sentries);
        RemoveEntities(_playerGibs);
        RemoveEntities(_bloodDrops);
        RemoveEntities(_deadBodies);
        RemoveEntities(_sentryGibs);
    }

    private void RemoveEntities<T>(List<T> entities) where T : SimulationEntity
    {
        for (var index = 0; index < entities.Count; index += 1)
        {
            _entities.Remove(entities[index].Id);
        }

        entities.Clear();
    }

    public void ResetPlayersToAwaitingJoinForFreshMap()
    {
        for (var index = 0; index < NetworkPlayerSlots.Count; index += 1)
        {
            var slot = NetworkPlayerSlots[index];
            if (slot != LocalPlayerSlot && !IsNetworkPlayerEnabled(slot))
            {
                continue;
            }

            if (!TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            TryDropCarriedIntel(player);
            TrySetNetworkPlayerAwaitingJoin(slot, true);
            TrySetNetworkPlayerRespawnTicks(slot, 0);
            SetNetworkPlayerDeathCam(slot, null);
            player.ClearMedicHealingTarget();
            player.Kill();
        }
    }

}
