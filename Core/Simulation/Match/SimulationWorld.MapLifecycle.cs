namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public void ConfigureMatchDefaults(int? timeLimitMinutes = null, int? capLimit = null, int? respawnSeconds = null)
    {
        if (timeLimitMinutes.HasValue)
        {
            MatchSettings.TimeLimitMinutes = Math.Clamp(timeLimitMinutes.Value, 1, 255);
        }

        if (capLimit.HasValue)
        {
            MatchSettings.CapLimit = Math.Clamp(capLimit.Value, 1, 255);
        }

        if (respawnSeconds.HasValue)
        {
            SetRespawnSeconds(respawnSeconds.Value);
        }

        MatchRules = CreateDefaultMatchRules(Level.Mode);
        MatchState = CreateInitialMatchState(MatchRules);
    }

    public void SetTimeLimitMinutes(int timeLimitMinutes)
    {
        MatchSettings.TimeLimitMinutes = Math.Clamp(timeLimitMinutes, 1, 255);
        var previousTimeLimitTicks = MatchRules.TimeLimitTicks;
        var nextTimeLimitTicks = MatchSettings.TimeLimitMinutes * Config.TicksPerSecond * 60;
        var elapsedTicks = Math.Max(0, previousTimeLimitTicks - MatchState.TimeRemainingTicks);
        var nextRemainingTicks = Math.Max(0, nextTimeLimitTicks - elapsedTicks);
        var nextPhase = !MatchState.IsEnded && nextRemainingTicks > 0
            ? MatchPhase.Running
            : MatchState.Phase;

        MatchRules = MatchRules with
        {
            TimeLimitMinutes = MatchSettings.TimeLimitMinutes,
            TimeLimitTicks = nextTimeLimitTicks,
        };
        MatchState = MatchState with
        {
            Phase = nextPhase,
            TimeRemainingTicks = nextRemainingTicks,
            WinnerTeam = nextPhase == MatchPhase.Running ? null : MatchState.WinnerTeam,
        };
    }

    public void SetCapLimit(int capLimit)
    {
        MatchSettings.CapLimit = Math.Clamp(capLimit, 1, 255);
        MatchRules = MatchRules with
        {
            CapLimit = MatchSettings.CapLimit,
        };
    }

    public void SetRespawnSeconds(int respawnSeconds)
    {
        MatchSettings.RespawnSeconds = Math.Clamp(respawnSeconds, 0, 255);
        MatchSettings.RespawnTicks = Math.Max(1, MatchSettings.RespawnSeconds * Config.TicksPerSecond);
    }

    public bool TryLoadLevel(string levelName)
    {
        return TryLoadLevel(levelName, mapAreaIndex: 1, preservePlayerStats: false, mapScale: MatchSettings.MapScale);
    }

    public bool TryLoadLevel(string levelName, int mapAreaIndex, bool preservePlayerStats, float? mapScale = null)
    {
        var nextLevel = SimpleLevelFactory.CreateImportedLevel(levelName, mapAreaIndex, mapScale ?? MatchSettings.MapScale);
        if (nextLevel is null)
        {
            return false;
        }

        Level = nextLevel;
        MatchSettings.MapScale = Level.MapScale;
        var mapContentHash = CustomMapDescriptorResolver.TryResolve(Level.Name, out var descriptor)
            ? descriptor.ContentHash
            : string.Empty;
        ConfigureSessionPresentationSeed(Level.Name, mapContentHash);
        MatchRules = CreateDefaultMatchRules(Level.Mode);
        ApplyScrLevelMatchSettings();
        RebuildForegroundJungleSpriteCache();
        ResetModeStateForNewMap();
        RestartCurrentRound(preservePlayerStats);
        return true;
    }

    public bool ApplyPendingMapChange(string levelName, int mapAreaIndex, bool preservePlayerStats)
    {
        if (!Lifecycle.MapChangeReady)
        {
            return false;
        }

        if (!TryLoadLevel(levelName, mapAreaIndex, preservePlayerStats))
        {
            RestartCurrentRound(preservePlayerStats: false);
            return false;
        }

        Lifecycle.MapChangeReady = false;
        return true;
    }

    public void RestartCurrentRoundForMapRotation(bool preservePlayerStats)
    {
        RestartCurrentRound(preservePlayerStats);
    }

    public void ConfigureSessionPresentationSeed(string levelName, string mapContentHash)
    {
        SessionPresentationSeed = SessionPresentationRules.DerivePresentationSeed(
            levelName,
            mapContentHash,
            Config.TicksPerSecond);
    }

    private bool AdvancePendingMapChange()
    {
        if (Lifecycle.PendingMapChangeTicks < 0)
        {
            return false;
        }

        if (Lifecycle.PendingMapChangeTicks == 0)
        {
            if (Lifecycle.AutoRestartOnMapChange)
            {
                if (MatchState.WinnerTeam == PlayerTeam.Red
                    && Level.MapAreaIndex < Level.MapAreaCount
                    && TryLoadLevel(
                        Level.Name,
                        Level.MapAreaIndex + 1,
                        preservePlayerStats: true))
                {
                    return false;
                }

                RestartCurrentRound(preservePlayerStats: false);
                return false;
            }

            Lifecycle.MapChangeReady = true;
            return false;
        }

        Lifecycle.PendingMapChangeTicks -= 1;
        return false;
    }

    private void QueuePendingMapChange()
    {
        if (Lifecycle.PendingMapChangeTicks >= 0)
        {
            return;
        }

        Lifecycle.PendingMapChangeTicks = PendingMapChangeTicks;
        Lifecycle.MapChangeReady = false;
    }

    private void RestartCurrentRound(bool preservePlayerStats, bool enterCompetitiveSkirmish = true)
    {
        Lifecycle.PendingMapChangeTicks = -1;
        Lifecycle.MapChangeReady = false;
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
            foreach (var player in PlayerRegistry.PlayersBySlot.Values)
            {
                player.ResetRoundStats();
            }
        }

        MatchState = CreateInitialMatchState(MatchRules);
        RedIntel = CreateIntelState(PlayerTeam.Red);
        BlueIntel = CreateIntelState(PlayerTeam.Blue);
        ResetModeStateForNewRound();
        FinalizeScrRoundStart();
        if (MapRuntime.LogicActivatorStartApplied.Length > 0)
        {
            Array.Clear(MapRuntime.LogicActivatorStartApplied, 0, MapRuntime.LogicActivatorStartApplied.Length);
        }

        MapRuntime.LogicControlPointInputSignature = 0;

        TrySetNetworkPlayerRespawnTicks(LocalPlayerSlot, 0);
        DummyState.EnemyRespawnTicks = 0;
        LocalDeathCam = null;
        PresentationEvents.ClearForRoundRestart();
        Combat.ClearPendingDamageEvents();
        Projectiles.ClearPendingRocketSpawnEvents();
        CombatRuntime.CivvieMoneyTrailTracker.Clear();
        Lifecycle.NextRedSpawnIndex = 0;
        Lifecycle.NextBlueSpawnIndex = 0;
        ClearDynamicEntities();
        ResetMovingPlatformsForLevel();
        ResetHealthPackSpawnsForLevel();
        ResetJumpPadSpawnsForLevel();
        RespawnPlayersForNewRound();
        if (ReadyUpState.Enabled
            && enterCompetitiveSkirmish
            && !ReadyUpState.SuppressSkirmishOnNextRoundRestart)
        {
            BeginCompetitiveSkirmish(clearReadyPlayers: true);
        }
    }

    private void ResetModeStateForNewMap()
    {
        RedCaps = 0;
        BlueCaps = 0;

        if (!IsControlPointMode(MatchRules.Mode)
            && !IsKothMode(MatchRules.Mode)
            && !Level.ShouldSimulateControlPoints)
        {
            Objectives.ControlPoints.Clear();
        }
        if (!IsKothMode(MatchRules.Mode))
        {
            Objectives.Koth.Clear();
        }
        if (MatchRules.Mode != GameModeKind.Generator)
        {
            WorldObjects.Generators.Clear();
        }
        UpdateControlPointSetupGates();

        Objectives.Arena.ResetWinStreaks();

        ResetModeStateForNewRound();
    }

    private void ResetModeStateForNewRound()
    {
        ResetTeleportTracking();
        Objectives.Arena.ResetForNewRound(MatchRules.Mode == GameModeKind.Arena ? ArenaPointUnlockTicksDefault : 0);

        if (IsControlPointMode(MatchRules.Mode)
            || IsKothMode(MatchRules.Mode)
            || Level.ShouldSimulateControlPoints)
        {
            ResetControlPointStateForNewRound();
        }

        if (IsVipModeActive)
        {
            ResetVipStateForNewRound();
        }
        else
        {
            ClearVipState();
        }

        if (!IsKothMode(MatchRules.Mode))
        {
            Objectives.Koth.Clear();
        }

        if (MatchRules.Mode == GameModeKind.Generator)
        {
            ResetGeneratorStateForNewRound();
        }
        else
        {
            WorldObjects.Generators.Clear();
        }
    }

    private void ClearDynamicEntities()
    {
        Projectiles.RemoveAllProjectiles(Shots);
        Projectiles.RemoveAllProjectiles(Bubbles);
        Projectiles.RemoveAllProjectiles(Blades);
        Projectiles.RemoveAllProjectiles(Needles);
        Projectiles.RemoveAllProjectiles(RevolverShots);
        Projectiles.RemoveAllProjectiles(StabAnimations);
        Projectiles.RemoveAllProjectiles(StabMasks);
        Projectiles.RemoveAllProjectiles(Flames);
        Projectiles.RemoveAllProjectiles(Rockets);
        Projectiles.RemoveAllProjectiles(Mines);
        WorldObjects.RemoveRoundScopedObjects();
        Projectiles.ClearPendingNewRocketIds();
        ClientSnapshots.PredictedProjectileIds.Clear();
        ClientSnapshots.TerminatedProjectileIds.Clear();
        ClientSnapshots.TerminatedProjectileExpiryFrames.Clear();
        ClientSnapshots.ProcessedImmediateRocketSpawnEventIds.Clear();
        ClientSnapshots.ProcessedGibSpawnEventIds.Clear();
        ClientSnapshots.PresentedGibDeathCountsByPlayerId.Clear();
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
            TrySetNetworkPlayerReady(slot, ready: false);
            TrySetNetworkPlayerAwaitingJoin(slot, true);
            TrySetNetworkPlayerRespawnTicks(slot, 0);
            SetNetworkPlayerDeathCam(slot, null);
            player.ClearMedicHealingTarget();
            player.Kill();
        }
    }

}
