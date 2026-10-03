namespace OpenGarrison.Core;

/// <summary>
/// Level loads, round restarts, pending map changes and the match settings that
/// reset with them. Resets every store and system that holds per-map or
/// per-round state, in one place.
/// </summary>
internal sealed class MapLifecycleSystem
{
    internal const int DefaultCapLimit = 5;
    internal const int DefaultTeamDeathmatchKillLimit = 30;
    private const int ArenaPointUnlockTicksDefault = 1800;
    private const int PendingMapChangeTicks = 300;

    private readonly IMapLifecycleHost _host;

    internal MapLifecycleSystem(IMapLifecycleHost host)
    {
        _host = host;
    }

    public void ConfigureMatchDefaults(int? timeLimitMinutes = null, int? capLimit = null, int? respawnSeconds = null)
    {
        if (timeLimitMinutes.HasValue)
        {
            _host.MatchSettings.TimeLimitMinutes = Math.Clamp(timeLimitMinutes.Value, 1, 255);
        }

        if (capLimit.HasValue)
        {
            _host.MatchSettings.CapLimit = Math.Clamp(capLimit.Value, 1, 255);
        }

        if (respawnSeconds.HasValue)
        {
            SetRespawnSeconds(respawnSeconds.Value);
        }

        _host.MatchRules = CreateDefaultMatchRules(_host.Level.Mode);
        _host.MatchState = CreateInitialMatchState(_host.MatchRules);
    }

    public void SetTimeLimitMinutes(int timeLimitMinutes)
    {
        _host.MatchSettings.TimeLimitMinutes = Math.Clamp(timeLimitMinutes, 1, 255);
        var previousTimeLimitTicks = _host.MatchRules.TimeLimitTicks;
        var nextTimeLimitTicks = _host.MatchSettings.TimeLimitMinutes * _host.Config.TicksPerSecond * 60;
        var elapsedTicks = Math.Max(0, previousTimeLimitTicks - _host.MatchState.TimeRemainingTicks);
        var nextRemainingTicks = Math.Max(0, nextTimeLimitTicks - elapsedTicks);
        var nextPhase = !_host.MatchState.IsEnded && nextRemainingTicks > 0
            ? MatchPhase.Running
            : _host.MatchState.Phase;

        _host.MatchRules = _host.MatchRules with
        {
            TimeLimitMinutes = _host.MatchSettings.TimeLimitMinutes,
            TimeLimitTicks = nextTimeLimitTicks,
        };
        _host.MatchState = _host.MatchState with
        {
            Phase = nextPhase,
            TimeRemainingTicks = nextRemainingTicks,
            WinnerTeam = nextPhase == MatchPhase.Running ? null : _host.MatchState.WinnerTeam,
        };
    }

    public void SetCapLimit(int capLimit)
    {
        _host.MatchSettings.CapLimit = Math.Clamp(capLimit, 1, 255);
        _host.MatchRules = _host.MatchRules with
        {
            CapLimit = _host.MatchSettings.CapLimit,
        };
    }

    public void SetRespawnSeconds(int respawnSeconds)
    {
        _host.MatchSettings.RespawnSeconds = Math.Clamp(respawnSeconds, 0, 255);
        _host.MatchSettings.RespawnTicks = Math.Max(1, _host.MatchSettings.RespawnSeconds * _host.Config.TicksPerSecond);
    }

    public bool TryLoadLevel(string levelName)
    {
        return TryLoadLevel(levelName, mapAreaIndex: 1, preservePlayerStats: false, mapScale: _host.MatchSettings.MapScale);
    }

    public bool TryLoadLevel(string levelName, int mapAreaIndex, bool preservePlayerStats, float? mapScale = null)
    {
        var nextLevel = SimpleLevelFactory.CreateImportedLevel(levelName, mapAreaIndex, mapScale ?? _host.MatchSettings.MapScale);
        if (nextLevel is null)
        {
            return false;
        }

        _host.Level = nextLevel;
        _host.MatchSettings.MapScale = _host.Level.MapScale;
        var mapContentHash = CustomMapDescriptorResolver.TryResolve(_host.Level.Name, out var descriptor)
            ? descriptor.ContentHash
            : string.Empty;
        ConfigureSessionPresentationSeed(_host.Level.Name, mapContentHash);
        _host.MatchRules = CreateDefaultMatchRules(_host.Level.Mode);
        _host.ObjectiveRules.ApplyScrLevelMatchSettings();
        _host.MapLogic.RebuildForegroundJungleSpriteCache();
        ResetModeStateForNewMap();
        RestartCurrentRound(preservePlayerStats);
        return true;
    }

    public bool ApplyPendingMapChange(string levelName, int mapAreaIndex, bool preservePlayerStats)
    {
        if (!_host.Lifecycle.MapChangeReady)
        {
            return false;
        }

        if (!TryLoadLevel(levelName, mapAreaIndex, preservePlayerStats))
        {
            RestartCurrentRound(preservePlayerStats: false);
            return false;
        }

        _host.Lifecycle.MapChangeReady = false;
        return true;
    }

    public void RestartCurrentRoundForMapRotation(bool preservePlayerStats)
    {
        RestartCurrentRound(preservePlayerStats);
    }

    public void ConfigureSessionPresentationSeed(string levelName, string mapContentHash)
    {
        _host.SessionPresentationSeed = SessionPresentationRules.DerivePresentationSeed(
            levelName,
            mapContentHash,
            _host.Config.TicksPerSecond);
    }

    internal bool AdvancePendingMapChange()
    {
        if (_host.Lifecycle.PendingMapChangeTicks < 0)
        {
            return false;
        }

        if (_host.Lifecycle.PendingMapChangeTicks == 0)
        {
            if (_host.Lifecycle.AutoRestartOnMapChange)
            {
                if (_host.MatchState.WinnerTeam == PlayerTeam.Red
                    && _host.Level.MapAreaIndex < _host.Level.MapAreaCount
                    && TryLoadLevel(
                        _host.Level.Name,
                        _host.Level.MapAreaIndex + 1,
                        preservePlayerStats: true))
                {
                    return false;
                }

                RestartCurrentRound(preservePlayerStats: false);
                return false;
            }

            _host.Lifecycle.MapChangeReady = true;
            return false;
        }

        _host.Lifecycle.PendingMapChangeTicks -= 1;
        return false;
    }

    internal void QueuePendingMapChange()
    {
        if (_host.Lifecycle.PendingMapChangeTicks >= 0)
        {
            return;
        }

        _host.Lifecycle.PendingMapChangeTicks = PendingMapChangeTicks;
        _host.Lifecycle.MapChangeReady = false;
    }

    internal void RestartCurrentRound(bool preservePlayerStats, bool enterCompetitiveSkirmish = true)
    {
        _host.Lifecycle.PendingMapChangeTicks = -1;
        _host.Lifecycle.MapChangeReady = false;
        if (_host.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            _host.RedCaps = 0;
            _host.BlueCaps = 0;
        }

        if (!preservePlayerStats)
        {
            _host.CombatFeedback.ClearAllDominations();
            _host.LocalPlayer.ResetRoundStats();
            _host.EnemyPlayer.ResetRoundStats();
            _host.FriendlyDummy.ResetRoundStats();
            foreach (var player in _host.PlayerRegistry.PlayersBySlot.Values)
            {
                player.ResetRoundStats();
            }
        }

        _host.MatchState = CreateInitialMatchState(_host.MatchRules);
        _host.ObjectiveRules.RedIntel = _host.ObjectiveRules.CreateIntelState(PlayerTeam.Red);
        _host.ObjectiveRules.BlueIntel = _host.ObjectiveRules.CreateIntelState(PlayerTeam.Blue);
        ResetModeStateForNewRound();
        _host.ObjectiveRules.FinalizeScrRoundStart();
        if (_host.MapRuntime.LogicActivatorStartApplied.Length > 0)
        {
            Array.Clear(_host.MapRuntime.LogicActivatorStartApplied, 0, _host.MapRuntime.LogicActivatorStartApplied.Length);
        }

        _host.MapRuntime.LogicControlPointInputSignature = 0;

        _host.NetworkPlayers.TrySetNetworkPlayerRespawnTicks(SimulationConstants.LocalPlayerSlot, 0);
        _host.DummyState.EnemyRespawnTicks = 0;
        _host.LocalDeathCam = null;
        _host.PresentationEvents.ClearForRoundRestart();
        _host.Combat.ClearPendingDamageEvents();
        _host.Projectiles.ClearPendingRocketSpawnEvents();
        _host.CombatRuntime.CivvieMoneyTrailTracker.Clear();
        _host.Lifecycle.NextRedSpawnIndex = 0;
        _host.Lifecycle.NextBlueSpawnIndex = 0;
        ClearDynamicEntities();
        _host.Movement.ResetMovingPlatformsForLevel();
        _host.Pickups.ResetHealthPackSpawnsForLevel();
        _host.Structures.ResetJumpPadSpawnsForLevel();
        _host.Spawns.RespawnPlayersForNewRound();
        if (_host.ReadyUpState.Enabled
            && enterCompetitiveSkirmish
            && !_host.ReadyUpState.SuppressSkirmishOnNextRoundRestart)
        {
            _host.ReadyUp.BeginCompetitiveSkirmish(clearReadyPlayers: true);
        }
    }

    private void ResetModeStateForNewMap()
    {
        _host.RedCaps = 0;
        _host.BlueCaps = 0;

        if (!ObjectiveRulesSystem.IsControlPointMode(_host.MatchRules.Mode)
            && !ObjectiveRulesSystem.IsKothMode(_host.MatchRules.Mode)
            && !_host.Level.ShouldSimulateControlPoints)
        {
            _host.Objectives.ControlPoints.Clear();
        }
        if (!ObjectiveRulesSystem.IsKothMode(_host.MatchRules.Mode))
        {
            _host.Objectives.Koth.Clear();
        }
        if (_host.MatchRules.Mode != GameModeKind.Generator)
        {
            _host.WorldObjects.Generators.Clear();
        }
        _host.ObjectiveRules.UpdateControlPointSetupGates();

        _host.Objectives.Arena.ResetWinStreaks();

        ResetModeStateForNewRound();
    }

    internal void ResetModeStateForNewRound()
    {
        _host.Movement.ResetTeleportTracking();
        _host.Objectives.Arena.ResetForNewRound(_host.MatchRules.Mode == GameModeKind.Arena ? ArenaPointUnlockTicksDefault : 0);

        if (ObjectiveRulesSystem.IsControlPointMode(_host.MatchRules.Mode)
            || ObjectiveRulesSystem.IsKothMode(_host.MatchRules.Mode)
            || _host.Level.ShouldSimulateControlPoints)
        {
            _host.ObjectiveRules.ResetControlPointStateForNewRound();
        }

        if (_host.VipRules.IsVipModeActive)
        {
            _host.VipRules.ResetVipStateForNewRound();
        }
        else
        {
            _host.VipRules.ClearVipState();
        }

        if (!ObjectiveRulesSystem.IsKothMode(_host.MatchRules.Mode))
        {
            _host.Objectives.Koth.Clear();
        }

        if (_host.MatchRules.Mode == GameModeKind.Generator)
        {
            _host.ObjectiveRules.ResetGeneratorStateForNewRound();
        }
        else
        {
            _host.WorldObjects.Generators.Clear();
        }
    }

    private void ClearDynamicEntities()
    {
        _host.Projectiles.RemoveAllProjectiles(_host.Projectiles.Shots);
        _host.Projectiles.RemoveAllProjectiles(_host.Projectiles.Bubbles);
        _host.Projectiles.RemoveAllProjectiles(_host.Projectiles.Blades);
        _host.Projectiles.RemoveAllProjectiles(_host.Projectiles.Needles);
        _host.Projectiles.RemoveAllProjectiles(_host.Projectiles.RevolverShots);
        _host.Projectiles.RemoveAllProjectiles(_host.Projectiles.StabAnimations);
        _host.Projectiles.RemoveAllProjectiles(_host.Projectiles.StabMasks);
        _host.Projectiles.RemoveAllProjectiles(_host.Projectiles.Flames);
        _host.Projectiles.RemoveAllProjectiles(_host.Projectiles.Rockets);
        _host.Projectiles.RemoveAllProjectiles(_host.Projectiles.Mines);
        _host.WorldObjects.RemoveRoundScopedObjects();
        _host.Projectiles.ClearPendingNewRocketIds();
        _host.ClientSnapshots.PredictedProjectileIds.Clear();
        _host.ClientSnapshots.TerminatedProjectileIds.Clear();
        _host.ClientSnapshots.TerminatedProjectileExpiryFrames.Clear();
        _host.ClientSnapshots.ProcessedImmediateRocketSpawnEventIds.Clear();
        _host.ClientSnapshots.ProcessedGibSpawnEventIds.Clear();
        _host.ClientSnapshots.PresentedGibDeathCountsByPlayerId.Clear();
    }

    public void ResetPlayersToAwaitingJoinForFreshMap()
    {
        for (var index = 0; index < SimulationConstants.NetworkPlayerSlots.Count; index += 1)
        {
            var slot = SimulationConstants.NetworkPlayerSlots[index];
            if (slot != SimulationConstants.LocalPlayerSlot && !_host.NetworkPlayers.IsNetworkPlayerEnabled(slot))
            {
                continue;
            }

            if (!_host.NetworkPlayers.TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            _host.ObjectiveRules.TryDropCarriedIntel(player);
            _host.ReadyUp.TrySetNetworkPlayerReady(slot, ready: false);
            _host.NetworkPlayers.TrySetNetworkPlayerAwaitingJoin(slot, true);
            _host.NetworkPlayers.TrySetNetworkPlayerRespawnTicks(slot, 0);
            _host.PlayerDeaths.SetNetworkPlayerDeathCam(slot, null);
            player.ClearMedicHealingTarget();
            player.Kill();
        }
    }

    public void SetMapScale(float scale)
    {
        var nextScale = float.Clamp(scale, 0.25f, 4f);
        if (MathF.Abs(_host.MatchSettings.MapScale - nextScale) <= 0.0001f)
        {
            return;
        }

        var previousScale = _host.MatchSettings.MapScale;
        _host.MatchSettings.MapScale = nextScale;
        if (TryLoadLevel(_host.Level.Name, _host.Level.MapAreaIndex, preservePlayerStats: false, mapScale: nextScale))
        {
            return;
        }

        if (!_host.Level.ImportedFromSource)
        {
            _host.Level = SimpleLevelFactory.CreateScoutPrototypeLevel(nextScale);
            _host.MatchRules = CreateDefaultMatchRules(_host.Level.Mode);
            ResetModeStateForNewMap();
            RestartCurrentRound(preservePlayerStats: false);
            return;
        }

        if (!TryLoadLevel(_host.Level.Name, _host.Level.MapAreaIndex, preservePlayerStats: false, mapScale: previousScale))
        {
            _host.MatchSettings.MapScale = previousScale;
        }
    }

    internal MatchRules CreateDefaultMatchRules(GameModeKind mode)
    {
        var settings = _host.MatchSettings;
        var timeLimitTicks = settings.TimeLimitMinutes * _host.Config.TicksPerSecond * 60;
        var capLimit = mode == GameModeKind.TeamDeathmatch && settings.CapLimit == DefaultCapLimit
            ? DefaultTeamDeathmatchKillLimit
            : settings.CapLimit;
        return new MatchRules(mode, settings.TimeLimitMinutes, timeLimitTicks, capLimit);
    }

    internal static MatchState CreateInitialMatchState(MatchRules rules)
    {
        return new MatchState(MatchPhase.Running, rules.TimeLimitTicks, null);
    }
}
