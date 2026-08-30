using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Server.LastToDie;

partial class GameServer
{
    private LastToDieNetworkSession? _lastToDieNetworkSession;
    private ulong _lastToDieLoadedStageInstanceId;
    private ulong _lastToDieFailedStageInstanceId;
    private readonly Dictionary<byte, int> _lastToDieObservedKillsBySlot = [];
    private readonly HashSet<byte> _lastToDieEnemySpawnPreparedWhileDead = [];
    private long _lastToDieReturnToLobbyFrame;
    private bool _lastToDieBidirectionalEnemySpawnsEnabled;
    private LastToDieRandom _lastToDieEnemySpawnRandom = new(0UL, 0x535041574EUL);
    private static readonly PlayerClass[] LastToDieEnemyClassCycle =
    [
        PlayerClass.Scout,
        PlayerClass.Soldier,
        PlayerClass.Pyro,
        PlayerClass.Demoman,
        PlayerClass.Heavy,
        PlayerClass.Engineer,
        PlayerClass.Sniper,
        PlayerClass.Spy,
        PlayerClass.Medic,
    ];

    private bool IsLastToDieHosted => _gameplayVariant == GameplayVariantKind.LastToDie;

    private void InitializeGameplayVariantRuntime()
    {
        if (!IsLastToDieHosted)
        {
            return;
        }

        _autoBalanceEnabled = false;
        _switchTeamsAfterRoundEnd = false;
        _teamShuffleAfterWins = 0;
        _botAutofillEnabled = false;
        _competitiveReadyUpEnabled = false;
        _world.SetCompetitiveReadyUpEnabled(false);
        _botManager.SeekEnemyPlayersAfterOwningControlPoint = true;
        _world.ConfigureExperimentalGameplaySettings(
            _world.ExperimentalGameplaySettings with
            {
                EnableSecondaryAbilities = true,
                EnableCapturedPointHealingAura = true,
                EnableEnemyHealthPackDrops = true,
                EnemyHealthPackDropChance = ExperimentalGameplaySettings.DefaultEnemyHealthPackDropChance,
            });

        var seed = _lastToDieSeed ?? unchecked((ulong)Random.Shared.NextInt64());
        _world.ConfigureLastToDieCombatSeed(seed);
        _lastToDieEnemySpawnRandom = new LastToDieRandom(
            seed ^ 0x4C5444535041574EUL,
            sequence: 0x535041574EUL);
        var director = LastToDieServerDirector.CreateFirstSlice(
            _stockMapRotation,
            _lastToDieDifficulty,
            seed,
            _config.TicksPerSecond,
            maximumPlayers: _maxPlayableClients);
        var controller = new LastToDieProtocolController(director);
        _lastToDieNetworkSession = new LastToDieNetworkSession(
            controller,
            () => _world.Frame,
            _outboundMessaging.SendMessage,
            _config.TicksPerSecond,
            _world.ConsumeLastToDieSpyAfterlifeDisconnectFailure,
            (client, reason) => _sessionManager.RemoveClient(client.Slot, reason));
        _sessionManager.ConfigurePlayableClientLifecyclePolicy(
            slot => _lastToDieNetworkSession?.ShouldRetainPlayableSlot(slot) == true,
            slot => _lastToDieNetworkSession?.CanAcceptGameplayInput(slot) == true);

        Console.WriteLine(
            $"[ltd] authoritative run initialized run={director.Director.RunId} " +
            $"difficulty={_lastToDieDifficulty} seed={seed} slots={_maxPlayableClients}");
    }

    private bool TryBuildLastToDieConsoleCommandResponse(
        string commandText,
        out List<string> responseLines)
    {
        var parts = commandText.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0
            || !string.Equals(parts[0], "ltd_win", StringComparison.OrdinalIgnoreCase))
        {
            responseLines = [];
            return false;
        }

        if (parts.Length != 1)
        {
            responseLines = ["[ltd] usage: ltd_win"];
            return true;
        }

        var session = _lastToDieNetworkSession;
        if (!IsLastToDieHosted || session is null)
        {
            responseLines = ["[ltd] ltd_win is only available during Last to Die."];
            return true;
        }

        if (!session.TryTriggerStageVictoryForTesting(out var error))
        {
            responseLines = [string.IsNullOrWhiteSpace(error)
                ? "[ltd] could not trigger victory from the current run state."
                : $"[ltd] could not trigger victory: {error}"];
            return true;
        }

        responseLines = ["[ltd] stage victory triggered."];
        return true;
    }

    private void SynchronizeLastToDieClients()
    {
        var session = _lastToDieNetworkSession;
        if (session is null)
        {
            return;
        }

        var reboundSlots = session.SynchronizeAuthorizedClients(
            _clientsBySlot.Values,
            (client, reason) =>
            {
                _outboundMessaging.SendMessage(
                    client.Peer,
                    new OpenGarrison.Protocol.ConnectionDeniedMessage(reason));
                _sessionManager.RemoveClient(client.Slot, "Last to Die roster rejected the connection");
            });
        if (reboundSlots.Count == 0)
        {
            return;
        }

        var snapshot = session.Controller.Director.CreateSnapshot();
        var canHydrate = snapshot.Phase == LastToDiePhase.Playing
            || snapshot.Phase == LastToDiePhase.LoadingStage
                && snapshot.StageInstanceId == _lastToDieLoadedStageInstanceId;
        if (!canHydrate)
        {
            return;
        }

        var reboundSlotSet = reboundSlots.ToHashSet();
        foreach (var participant in session.GetParticipants()
                     .Where(participant => reboundSlotSet.Contains(participant.Slot)))
        {
            var shouldRespawn = snapshot.Phase == LastToDiePhase.LoadingStage || participant.IsAlive;
            ConfigureLastToDieParticipant(participant, shouldRespawn, refillHealth: shouldRespawn);
        }
    }

    private void AdvanceLastToDieAfterSimulationTick()
    {
        var session = _lastToDieNetworkSession;
        if (session is null)
        {
            return;
        }

        var directorSnapshot = session.Controller.Director.CreateSnapshot();
        if (directorSnapshot.Phase == LastToDiePhase.LoadingStage
            && directorSnapshot.StageInstanceId != 0
            && directorSnapshot.StageInstanceId != _lastToDieLoadedStageInstanceId
            && directorSnapshot.StageInstanceId != _lastToDieFailedStageInstanceId)
        {
            TryCommitLastToDieStageWorld(session, directorSnapshot);
        }

        if (session.Controller.Director.Phase == LastToDiePhase.Playing)
        {
            ObserveLastToDiePlayingWorld(session);
        }

        var phase = session.Controller.Director.Phase;
        DeactivateCompletedLastToDieStageWorld(phase);
        if (phase == LastToDiePhase.Won)
        {
            if (_lastToDieReturnToLobbyFrame == 0)
            {
                _lastToDieReturnToLobbyFrame = checked(
                    _world.Frame + (3L * Math.Max(1, _config.TicksPerSecond)));
            }
            else if (_world.Frame >= _lastToDieReturnToLobbyFrame
                     && session.Controller.Director.TryReturnToLobby(out _))
            {
                _lastToDieReturnToLobbyFrame = 0;
                _lastToDieObservedKillsBySlot.Clear();
            }
        }
        else
        {
            _lastToDieReturnToLobbyFrame = 0;
        }

        session.Tick();
    }

    private void DeactivateCompletedLastToDieStageWorld(LastToDiePhase phase)
    {
        if (!ShouldDeactivateLastToDieStageWorld(phase)
            || _botManager.BotSlots.Count == 0)
        {
            return;
        }

        // Reward and terminal screens are authoritative pause boundaries. Do
        // not leave the completed stage's bots firing, respawning, or emitting
        // audio beneath those menus. The next LoadingStage commit creates the
        // new stage roster from scratch.
        foreach (var slot in _botManager.BotSlots.Keys.ToArray())
        {
            _botManager.TryRemoveBot(slot);
        }

        _lastToDieBidirectionalEnemySpawnsEnabled = false;
        _lastToDieEnemySpawnPreparedWhileDead.Clear();
    }

    internal static bool ShouldDeactivateLastToDieStageWorld(LastToDiePhase phase)
        => phase is LastToDiePhase.RewardChoice
            or LastToDiePhase.Won
            or LastToDiePhase.Lost
            or LastToDiePhase.Lobby
            or LastToDiePhase.SurvivorChoice;

    private bool TryCommitLastToDieStageWorld(
        LastToDieNetworkSession session,
        LastToDieRunSnapshot directorSnapshot)
    {
        var previousLevelName = _world.Level.Name;
        var previousAreaIndex = _world.Level.MapAreaIndex;
        var previousAreaCount = _world.Level.MapAreaCount;
        var previousMode = _world.MatchRules.Mode;
        if (!_world.TryLoadLevel(
                directorSnapshot.CurrentMap,
                mapAreaIndex: 1,
                preservePlayerStats: false))
        {
            _lastToDieFailedStageInstanceId = directorSnapshot.StageInstanceId;
            Console.WriteLine(
                $"[ltd] failed to load stage={directorSnapshot.StageInstanceId} map={directorSnapshot.CurrentMap}; " +
                "the run remains in its loading barrier.");
            return false;
        }

        _mapRotationManager.AlignExternalMapChange(_world.Level.Name);
        ConfigureLastToDieParticipants(session);
        ConfigureLastToDieEnemies(directorSnapshot.EnemyCount);
        var botNavigationPreloaded = PreloadBotNavigationForCurrentLevel(
            out var botNavigationPreloadMs,
            out var botNavigationWarmup);
        _mapBotSpawnController.Reset();

        var transition = new MapChangeTransition(
            previousLevelName,
            previousAreaIndex,
            previousAreaCount,
            _world.Level.Name,
            _world.Level.MapAreaIndex,
            PreservePlayerStats: false,
            WinnerTeam: null,
            previousMode);
        _eventReporter.ApplyMapTransition(transition);
        _demoRecorder.HandleMapTransition(transition);

        // This reset invalidates every ordinary delta baseline. The snapshot
        // broadcaster immediately following this hook must therefore send a
        // full world snapshot for the newly committed stage.
        _snapshotBroadcaster.ResetTransientEvents();
        var baselineFrame = checked((ulong)_world.Frame);
        if (!session.TryOpenStageBarrier(
                directorSnapshot.StageInstanceId,
                baselineFrame,
                out var barrierError))
        {
            _lastToDieFailedStageInstanceId = directorSnapshot.StageInstanceId;
            Console.WriteLine(
                $"[ltd] failed to open stage={directorSnapshot.StageInstanceId} barrier: {barrierError}");
            return false;
        }

        _lastToDieLoadedStageInstanceId = directorSnapshot.StageInstanceId;
        Console.WriteLine(
            $"[ltd] committed stage={directorSnapshot.StageInstanceId} map={_world.Level.Name} " +
            $"baseline={baselineFrame} navPreloaded={botNavigationPreloaded} " +
            $"navPreloadMs={botNavigationPreloadMs:0.###} " +
            $"navSource={botNavigationWarmup.Source} navSourcePath=\"{botNavigationWarmup.Path}\"");
        return true;
    }

    private void ConfigureLastToDieParticipants(LastToDieNetworkSession session)
    {
        _lastToDieObservedKillsBySlot.Clear();
        foreach (var participant in session.GetParticipants().Where(participant => participant.IsConnected))
        {
            ConfigureLastToDieParticipant(participant, respawn: true, refillHealth: true);
        }
    }

    private void ConfigureLastToDieParticipant(
        LastToDieNetworkParticipant participant,
        bool respawn,
        bool refillHealth)
    {
        if (string.IsNullOrWhiteSpace(participant.SurvivorId))
        {
            return;
        }

        if (!respawn)
        {
            _lastToDieObservedKillsBySlot[participant.Slot] = 0;
            return;
        }

        var survivors = LastToDieSurvivorCatalog.CreateStock();
        var survivor = survivors.GetRequired(new LastToDieSurvivorId(participant.SurvivorId));
        _world.TrySetNetworkPlayerAutomaticRespawnSuppressed(participant.Slot, suppressed: true);
        _world.TrySetNetworkPlayerTeam(
            participant.Slot,
            PlayerTeam.Red,
            respawnLivePlayerImmediately: true);
        _world.TryForceNetworkPlayerClassSelectionAndRespawn(
            participant.Slot,
            survivor.GameplayClassId);
        _world.TryMoveNetworkPlayerToLastToDieObjectiveSpawn(participant.Slot);
        if (!_world.TryGetNetworkPlayer(participant.Slot, out var player))
        {
            return;
        }

        var baseMaximumHealth = _lastToDieDifficulty == LastToDieDifficulty.Hardcore
            ? 25
            : player.ClassDefinition.MaxHealth;
        _world.TryConfigureLastToDiePlayerBuild(
            participant.Slot,
            participant.OwnedPerkIds.Select(perkId => new LastToDiePerkId(perkId)),
            baseMaximumHealth,
            refillHealth,
            resetDynamicState: true);
        _world.TryRestoreLastToDieSniperConquistadorStacks(
            participant.Slot,
            participant.ConquistadorStacks);
        _lastToDieObservedKillsBySlot[participant.Slot] = Math.Max(0, player.Kills);
    }

    private void ConfigureLastToDieEnemies(int enemyCount)
    {
        foreach (var slot in _botManager.BotSlots.Keys.ToArray())
        {
            _botManager.TryRemoveBot(slot);
        }

        var remaining = Math.Clamp(enemyCount, 0, SimulationWorld.MaxPlayableNetworkPlayers - 2);
        var spawnSides = BuildLastToDieEnemySpawnSides(remaining, _lastToDieEnemySpawnRandom);
        _lastToDieBidirectionalEnemySpawnsEnabled = spawnSides.Length >= 3;
        _lastToDieEnemySpawnPreparedWhileDead.Clear();
        var classIndex = 0;
        for (var slotNumber = 3;
             slotNumber <= SimulationWorld.MaxPlayableNetworkPlayers && remaining > 0;
             slotNumber += 1)
        {
            var playerClass = LastToDieEnemyClassCycle[classIndex % LastToDieEnemyClassCycle.Length];
            if (_botManager.TryAddBot(
                    (byte)slotNumber,
                    PlayerTeam.Blue,
                    playerClass,
                    string.Empty))
            {
                var spawnSide = spawnSides[classIndex];
                if (!_world.TryMoveNetworkPlayerToLastToDieEnemySpawn(
                        (byte)slotNumber,
                        spawnSide))
                {
                    Console.WriteLine(
                        $"[ltd] could not place enemy slot={slotNumber} at its requested " +
                        $"{spawnSide}-side ingress; keeping its default spawn.");
                }

                if (_lastToDieDifficulty == LastToDieDifficulty.Hardcore)
                {
                    _world.TrySetNetworkPlayerMaxHealthOverride(
                        (byte)slotNumber,
                        25,
                        refillHealth: true);
                }

                remaining -= 1;
                classIndex += 1;
            }
        }

        if (remaining > 0)
        {
            Console.WriteLine($"[ltd] could not allocate {remaining} of {enemyCount} requested enemy slots.");
        }
    }

    private void PrepareLastToDieEnemySpawnsBeforeSimulationTick()
    {
        if (!_lastToDieBidirectionalEnemySpawnsEnabled
            || _lastToDieNetworkSession?.Controller.Director.Phase != LastToDiePhase.Playing)
        {
            return;
        }

        foreach (var slot in _botManager.BotSlots.Keys)
        {
            if (!_world.TryGetNetworkPlayer(slot, out var enemy))
            {
                _lastToDieEnemySpawnPreparedWhileDead.Remove(slot);
                continue;
            }

            if (enemy.IsAlive)
            {
                _lastToDieEnemySpawnPreparedWhileDead.Remove(slot);
                continue;
            }

            if (_lastToDieEnemySpawnPreparedWhileDead.Contains(slot))
            {
                continue;
            }

            var spawnSide = _lastToDieEnemySpawnRandom.NextInt32(2) == 0
                ? PlayerTeam.Red
                : PlayerTeam.Blue;
            if (_world.TryConfigureNetworkPlayerLastToDieEnemySpawn(
                    slot,
                    spawnSide,
                    repositionAlivePlayer: false))
            {
                _lastToDieEnemySpawnPreparedWhileDead.Add(slot);
            }
        }
    }

    internal static PlayerTeam[] BuildLastToDieEnemySpawnSides(
        int enemyCount,
        LastToDieRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var count = Math.Max(0, enemyCount);
        var sides = Enumerable.Repeat(PlayerTeam.Blue, count).ToArray();
        if (count < 3)
        {
            return sides;
        }

        // Guarantee pressure from both directions, then shuffle which enemy
        // receives each side assignment using the run-seeded LTD stream.
        sides[0] = PlayerTeam.Red;
        sides[1] = PlayerTeam.Blue;
        for (var index = 2; index < sides.Length; index += 1)
        {
            sides[index] = random.NextInt32(2) == 0
                ? PlayerTeam.Red
                : PlayerTeam.Blue;
        }

        random.Shuffle(sides);
        return sides;
    }

    private void ObserveLastToDiePlayingWorld(LastToDieNetworkSession session)
    {
        var director = session.Controller.Director;
        var directorSnapshot = director.CreateSnapshot();
        var playersById = directorSnapshot.Players.ToDictionary(player => player.PlayerId);
        var stateChanged = false;

        foreach (var participant in session.GetParticipants().Where(participant => participant.IsConnected))
        {
            if (!playersById.TryGetValue(participant.PlayerId, out var directorPlayer)
                || !_world.TryGetNetworkPlayer(participant.Slot, out var worldPlayer))
            {
                continue;
            }

            if (directorPlayer.IsAlive != worldPlayer.IsAlive
                && director.TrySetPlayerAlive(participant.PlayerId, worldPlayer.IsAlive, out _))
            {
                stateChanged = true;
            }

            var observedKills = Math.Max(0, worldPlayer.Kills);
            var previousKills = _lastToDieObservedKillsBySlot.GetValueOrDefault(participant.Slot);
            if (observedKills > previousKills
                && director.TryRecordKills(
                    participant.PlayerId,
                    observedKills - previousKills,
                    _world.Frame,
                    out _))
            {
                stateChanged = true;
            }

            _lastToDieObservedKillsBySlot[participant.Slot] = observedKills;

            var conquistadorStacks = _world.TryGetLastToDieSniperConquistadorStacks(
                participant.Slot,
                out var worldConquistadorStacks)
                ? worldConquistadorStacks
                : 0;
            if (directorPlayer.ConquistadorStacks != conquistadorStacks
                && director.TrySetPlayerConquistadorStacks(
                    participant.PlayerId,
                    conquistadorStacks,
                    out _))
            {
                stateChanged = true;
            }
        }

        var revisionBeforeAdvance = director.StructuralRevision;
        var phaseBeforeAdvance = director.Phase;
        var winner = _world.MatchState.IsEnded ? _world.MatchState.WinnerTeam : null;
        director.TryAdvancePlayingState(
            _world.Frame,
            redObjectiveWon: winner == PlayerTeam.Red,
            blueObjectiveWon: winner == PlayerTeam.Blue,
            anyAfterlifeWindowActive: session.GetParticipants().Any(participant =>
                participant.IsConnected
                && _world.IsLastToDieSpyAfterlifeWindowActive(participant.Slot))
                || session.HasActiveReconnectGrace(),
            out _);
        stateChanged |= revisionBeforeAdvance != director.StructuralRevision
            || phaseBeforeAdvance != director.Phase;

        if (stateChanged)
        {
            session.BroadcastSnapshots();
        }
    }
}
