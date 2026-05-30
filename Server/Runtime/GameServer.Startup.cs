using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using OpenGarrison.Server.Plugins;
using static ServerHelpers;

partial class GameServer
{
    public void Run(CancellationToken cancellationToken)
    {
        using var udp = new UdpClient(_port);
        using var timerResolution = WindowsTimerResolutionScope.Create1Millisecond();
        using var eventLog = new PersistentServerEventLog(_eventLogPath, Console.WriteLine);
        InitializeUdpTransport(udp);
        ApplyRuntimeBootstrap(CreateRuntimeBootstrap(eventLog));
        InitializeWebSocketHost();
        InitializeGameplayOwnershipService();
        InitializePluginRuntime();
        InitializeHttpRegistryHeartbeat();
        InitializeIncomingPacketPump();
        StartAndAnnounceServer(timerResolution.IsActive, eventLog);
        RunMainLoop(eventLog, cancellationToken);
    }

    private static void TryDisableUdpConnectionReset(Socket socket)
    {
        try
        {
            socket.IOControl((IOControlCode)SioUdpConnReset, [0, 0, 0, 0], null);
        }
        catch (PlatformNotSupportedException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private void InitializeUdpTransport(UdpClient udp)
    {
        _udp = udp;
        _udp.Client.Blocking = false;
        TryDisableUdpConnectionReset(_udp.Client);
        _messageTransport = new OpenGarrison.Server.CompositeServerMessageTransport(_udp);
    }

    private void InitializeWebSocketHost()
    {
        var enableWebSocket = _webSocketPort > 0;
        var httpPort = ResolveMapDownloadPort();
        if (httpPort <= 0)
        {
            return;
        }

        try
        {
            _webSocketHost = new OpenGarrison.Server.WebSocketServerHost(
                httpPort,
                enableWebSocket ? _webSocketCertificatePath : null,
                enableWebSocket ? _webSocketCertificatePassword : null,
                (OpenGarrison.Server.CompositeServerMessageTransport)_messageTransport,
                Console.WriteLine,
                enableWebSocket: enableWebSocket,
                enableMapDownloads: true);
            _webSocketHost.Start();
            _mapDownloadEndpointAvailable = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[server] failed to start HTTP listener: {ex.Message}");
            _webSocketHost?.Dispose();
            _webSocketHost = null;
            _mapDownloadEndpointAvailable = false;
        }
    }

    private OpenGarrison.Server.ServerRuntimeBootstrap CreateRuntimeBootstrap(PersistentServerEventLog eventLog)
    {
        return OpenGarrison.Server.ServerRuntimeBootstrapFactory.Create(
            _config,
            _udp,
            _messageTransport,
            _port,
            _protocolUuidBytes,
            _useLobbyServer,
            _lobbyHost,
            _lobbyPort,
            _lobbyHeartbeatSeconds,
            _lobbyResolveSeconds,
            _requestedMap,
            _mapRotationFile,
            _stockMapRotation,
            _mapRotationShuffleEnabled,
            MaxNewHelloAttemptsPerWindow,
            HelloAttemptWindow,
            HelloCooldown,
            MaxPasswordFailuresPerWindow,
            PasswordFailureWindow,
            PasswordCooldown,
            _maxPlayableClients,
            _maxTotalClients,
            _maxSpectatorClients,
            _autoBalanceDelaySeconds,
            _autoBalanceNewPlayerGraceSeconds,
            _autoBalanceEnabled,
            _secondaryAbilitiesEnabled,
            _randomSpreadEnabled,
            _competitiveReadyUpEnabled,
            _competitiveSetupSeconds,
            _timeLimitMinutesOverride,
            _capLimitOverride,
            _respawnSecondsOverride,
            _serverPassword,
            _passwordRequired,
            _clientTimeoutSeconds,
            _passwordTimeoutSeconds,
            _passwordRetrySeconds,
            _transientEventReplayTicks,
            () => _adminChatRouter,
            () => _pluginHost,
            BuildCustomMapDownloadUrl,
            _serverName,
            eventLog.Write,
            Console.WriteLine);
    }

    private void ApplyRuntimeBootstrap(OpenGarrison.Server.ServerRuntimeBootstrap runtime)
    {
        _lobbyRegistrar = runtime.LobbyRegistrar;
        _world = runtime.World;
        _simulator = runtime.Simulator;
        _clock = runtime.Clock;
        _previous = runtime.Previous;
        _clientsBySlot = runtime.ClientsBySlot;
        _connectionRateLimiter = runtime.ConnectionRateLimiter;
        _eventReporter = runtime.EventReporter;
        _outboundMessaging = runtime.OutboundMessaging;
        _sessionManager = runtime.SessionManager;
        _autoBalancer = runtime.AutoBalancer;
        _snapshotBroadcaster = runtime.SnapshotBroadcaster;
        _mapRotationManager = runtime.MapRotationManager;
        _botManager = runtime.BotManager;
        _demoRecorder = runtime.DemoRecorder;
    }

    private void StartAndAnnounceServer(bool highResolutionTimerEnabled, PersistentServerEventLog eventLog)
    {
        _pluginHost?.LoadPlugins();
        _pluginHost?.NotifyServerStarting();
        CharacterClassCatalog.RuntimeRegistry.SealAbilityDefinitions();

        Console.WriteLine($"OG2.Server booting at {_config.TicksPerSecond} ticks/sec.");
        Console.WriteLine($"Protocol version: {ProtocolVersion.Current}");
        Console.WriteLine($"UDP bind: 0.0.0.0:{_port}");
        Console.WriteLine(_webSocketPort <= 0 || _webSocketHost is null
            ? "[server] WebSocket: disabled"
            : $"[server] WebSocket: {(_webSocketCertificatePath is null ? "ws" : "wss")}://0.0.0.0:{_webSocketPort}/opengarrison/ws");
        Console.WriteLine(_mapDownloadEndpointAvailable
            ? $"[server] custom map downloads: enabled on port {ResolveMapDownloadPort()}"
            : "[server] custom map downloads: unavailable");
        Console.WriteLine($"Name: {_serverName}");
        Console.WriteLine($"Max players: {_maxPlayableClients}");
        Console.WriteLine(_botAutofillEnabled
            ? $"Bot autofill: enabled (min players {_botAutofillMinPlayers}, per team {_botAutofillPerTeam})"
            : "Bot autofill: disabled");
        if (highResolutionTimerEnabled)
        {
            Console.WriteLine("[server] high-resolution timer enabled (1 ms).");
        }

        if (_timeLimitMinutesOverride.HasValue)
        {
            Console.WriteLine($"Time limit: {_timeLimitMinutesOverride.Value} minutes");
        }

        if (_capLimitOverride.HasValue)
        {
            Console.WriteLine($"Cap limit: {_capLimitOverride.Value}");
        }

        if (_respawnSecondsOverride.HasValue)
        {
            Console.WriteLine($"Respawn: {_respawnSecondsOverride.Value} seconds");
        }

        Console.WriteLine($"Auto-balance: {(_autoBalanceEnabled ? "Enabled" : "Disabled")}");
        Console.WriteLine($"Random bullet spread: {(_randomSpreadEnabled ? "Enabled" : "Disabled")}");
        Console.WriteLine($"Special abilities: {(_secondaryAbilitiesEnabled ? "Enabled" : "Disabled")}");
        Console.WriteLine(_competitiveReadyUpEnabled
            ? $"Competitive ready-up: enabled (setup {_competitiveSetupSeconds} seconds)"
            : "Competitive ready-up: disabled");
        Console.WriteLine($"Level: {_world.Level.Name} area={_world.Level.MapAreaIndex}/{_world.Level.MapAreaCount} imported={_world.Level.ImportedFromSource} mode={_world.MatchRules.Mode}");
        Console.WriteLine($"World bounds: {_world.Bounds.Width}x{_world.Bounds.Height}");
        var botNavigationPreloaded = PreloadBotNavigationForCurrentLevel(out var botNavigationPreloadMs);
        var botNavigationDiagnostic = BotNavigationAssetStore.GetLoadDiagnostic(_world.Level);
        Console.WriteLine(
            "[botbrain] startup-nav " +
            $"level={_world.Level.Name} area={_world.Level.MapAreaIndex} " +
            $"preloaded={botNavigationPreloaded} preloadMs={botNavigationPreloadMs:0.###} " +
            $"expectedFingerprint={TrimDiagnosticFingerprint(botNavigationDiagnostic.ExpectedFingerprint)} " +
            $"shipped={botNavigationDiagnostic.ShippedStatus} shippedPath=\"{botNavigationDiagnostic.ShippedPath}\" " +
            $"runtimeCache={botNavigationDiagnostic.RuntimeCacheStatus}");
        Console.WriteLine($"Event log: {eventLog.FilePath}");
        Console.WriteLine(_passwordRequired ? "[server] password required" : "[server] no password set");
        if (_useLobbyServer)
        {
            Console.WriteLine($"[server] lobby registration enabled host={_lobbyHost}:{_lobbyPort}");
        }

        if (_httpRegistryHeartbeat is not null)
        {
            Console.WriteLine($"[server] HTTP registry enabled url={_registryUrl}");
        }

        Console.WriteLine("[server] type \"help\" for commands. Type \"shutdown\" to stop.");
        foreach (var line in BuildConsoleCommandResponse("status", CreateConsoleIdentity(), OpenGarrisonServerCommandSource.Console))
        {
            Console.WriteLine(line);
        }

        foreach (var line in BuildConsoleCommandResponse("rotation", CreateConsoleIdentity(), OpenGarrisonServerCommandSource.Console))
        {
            Console.WriteLine(line);
        }

        Console.WriteLine("Waiting for a UDP hello packet. Pass a different port as the first CLI argument to override 8190.");
        _eventReporter.WriteEvent(
            "server_started",
            ("server_name", _serverName),
            ("port", _port),
            ("tick_rate", _config.TicksPerSecond),
            ("max_playable_clients", _maxPlayableClients),
            ("max_total_clients", _maxTotalClients),
            ("max_spectator_clients", _maxSpectatorClients),
            ("password_required", _passwordRequired),
            ("use_lobby_server", _useLobbyServer),
            ("map_name", _world.Level.Name),
            ("map_area_index", _world.Level.MapAreaIndex),
            ("map_area_count", _world.Level.MapAreaCount),
            ("mode", _world.MatchRules.Mode));
        _pluginHost?.NotifyServerStarted();
    }

    private static string TrimDiagnosticFingerprint(string fingerprint)
    {
        return string.IsNullOrWhiteSpace(fingerprint)
            ? string.Empty
            : fingerprint[..Math.Min(12, fingerprint.Length)];
    }

    private int ResolveMapDownloadPort()
    {
        return _webSocketPort > 0 ? _webSocketPort : _port;
    }

    private string BuildCustomMapDownloadUrl(CustomMapDescriptor descriptor)
    {
        return _mapDownloadEndpointAvailable
            ? ServerMapDownloadEndpoint.BuildAdvertisedDownloadUrl(
                descriptor,
                _publicWebSocketUrl,
                _publicHost,
                ResolveMapDownloadPort(),
                preferHttps: _webSocketPort > 0 && !string.IsNullOrWhiteSpace(_webSocketCertificatePath))
            : descriptor.SourceUrl;
    }

    private bool PreloadBotNavigationForCurrentLevel(out double elapsedMilliseconds)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var loaded = BotNavigationAssetStore.TryLoadCachedGraph(_world.Level, out _);
        elapsedMilliseconds = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        return loaded;
    }

    private void RunMainLoop(PersistentServerEventLog eventLog, CancellationToken cancellationToken)
    {
        var maxSimulationTicksPerAdvance = Math.Max(1, (int)Math.Ceiling(_config.TicksPerSecond / 10d));
        var simulationBacklogDropCount = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ProcessPendingConsoleCommands();
                _connectionRateLimiter.Prune();
                PumpIncomingPackets();
                _sessionManager.PruneTimedOutClients();
                _sessionManager.RefreshPasswordRequests();

                var now = _clock.Elapsed;
                var elapsedSeconds = (now - _previous).TotalSeconds;
                _previous = now;
                _scheduler.RunDueTasks();
                _pluginHost?.NotifyServerHeartbeat(now);

                var ticks = ServerSimulationBatch.Advance(
                    _simulator,
                    elapsedSeconds,
                    () =>
                    {
                        _sessionManager.PreparePlayableClientInputsForNextTick();
                        ProcessCompetitiveReadyUpBeforeSimulationTick();
                        _botManager.FeedBotInputsBeforeSimulationAdvance();
                    },
                    () =>
                    {
                        _autoBalancer.Tick(now, 1, _autoBalanceEnabled);
                        if (_mapRotationManager.TryApplyPendingMapChange(out var transition))
                        {
                            var botNavigationPreloaded = PreloadBotNavigationForCurrentLevel(out var botNavigationPreloadMs);
                            Console.WriteLine(
                                "[botbrain] map-nav " +
                                $"level={_world.Level.Name} area={_world.Level.MapAreaIndex} " +
                                $"preloaded={botNavigationPreloaded} preloadMs={botNavigationPreloadMs:0.###}");
                            var restoredBotCount = _botManager.ReactivateBotsAfterMapChange();
                            _eventReporter.ApplyMapTransition(transition);
                            _demoRecorder.HandleMapTransition(transition);
                            _snapshotBroadcaster.ResetTransientEvents();
                            if (restoredBotCount > 0)
                            {
                                Console.WriteLine($"[server] restored {restoredBotCount} server bots after map change.");
                            }
                        }
                        // Update bot reactions/emotes AFTER simulation advances
                        _botManager.AdvanceBotReactions();
                    },
                    _snapshotBroadcaster.BroadcastSnapshot,
                    maxSimulationTicksPerAdvance);
                if (_simulator.DroppedSimulationBacklogOnLastAdvance)
                {
                    simulationBacklogDropCount += 1;
                }
                _lobbyRegistrar?.Tick(now, BuildLobbyServerName(_serverName, _world, _clientsBySlot, _passwordRequired, _maxPlayableClients));
                _httpRegistryHeartbeat?.Tick(now);
                if (ticks > 0)
                {
                    _eventReporter.PublishGameplayEvents(_snapshotBroadcaster.LastCapturedTransientEvents);
                }

                if (ticks > 0 && _world.Frame % (_config.TicksPerSecond * 5) == 0)
                {
                    var activePlayableCount = _world.EnumerateActiveNetworkPlayers().Count();
                    Console.WriteLine(
                        $"[server] frame={_world.Frame} clients={_clientsBySlot.Count} " +
                        $"mode={_world.MatchRules.Mode} phase={_world.MatchState.Phase} hp={_world.LocalPlayer.Health}/{_world.LocalPlayer.MaxHealth} " +
                        $"ammo={_world.LocalPlayer.CurrentShells}/{_world.LocalPlayer.MaxShells} pos=({_world.LocalPlayer.X:F1},{_world.LocalPlayer.Y:F1}) " +
                        $"activePlayable={activePlayableCount} spectators={_clientsBySlot.Keys.Count(IsSpectatorSlot)} caps={_world.RedCaps}-{_world.BlueCaps}");

                    var snapshotMetrics = _snapshotBroadcaster.Metrics;
                    if (snapshotMetrics.HasMeasurements)
                    {
                        eventLog.Write(
                            "server_snapshot_metrics",
                            ("frame", snapshotMetrics.Frame),
                            ("client_count", snapshotMetrics.ClientCount),
                            ("average_target_payload_bytes", snapshotMetrics.AverageTargetPayloadBytes),
                            ("average_full_payload_bytes", snapshotMetrics.AverageFullPayloadBytes),
                            ("average_sent_uncompressed_bytes", snapshotMetrics.AverageSentUncompressedBytes),
                            ("average_sent_payload_bytes", snapshotMetrics.AverageSentPayloadBytes),
                            ("max_sent_payload_bytes", snapshotMetrics.MaxSentPayloadBytes),
                            ("payload_over_target_count", snapshotMetrics.PayloadOverTargetCount),
                            ("budgeted_client_count", snapshotMetrics.BudgetedClientCount),
                            ("baseline_hit_count", snapshotMetrics.BaselineHitCount),
                            ("baseline_miss_count", snapshotMetrics.BaselineMissCount),
                            ("average_serialize_passes", snapshotMetrics.AverageSerializePasses),
                            ("snapshot_full_player_bytes", snapshotMetrics.AverageSnapshotFullPlayerBytes),
                            ("snapshot_player_movement_bytes", snapshotMetrics.AverageSnapshotPlayerMovementBytes),
                            ("snapshot_player_status_bytes", snapshotMetrics.AverageSnapshotPlayerStatusBytes),
                            ("snapshot_player_extended_status_bytes", snapshotMetrics.AverageSnapshotPlayerExtendedStatusBytes),
                            ("snapshot_player_chat_bubble_bytes", snapshotMetrics.AverageSnapshotPlayerChatBubbleBytes),
                            ("snapshot_projectile_bytes", snapshotMetrics.AverageSnapshotProjectileBytes),
                            ("snapshot_sentry_bytes", snapshotMetrics.AverageSnapshotSentryBytes),
                            ("snapshot_event_bytes", snapshotMetrics.AverageSnapshotEventBytes),
                            ("snapshot_removal_bytes", snapshotMetrics.AverageSnapshotRemovalBytes),
                            ("snapshot_world_bytes", snapshotMetrics.AverageSnapshotWorldBytes),
                            ("snapshot_envelope_bytes", snapshotMetrics.AverageSnapshotEnvelopeBytes),
                            ("shared_capture_ms", snapshotMetrics.SharedCaptureMilliseconds),
                            ("per_client_ms", snapshotMetrics.PerClientMilliseconds),
                            ("total_ms", snapshotMetrics.TotalMilliseconds),
                            ("simulation_backlog_drop_count", simulationBacklogDropCount),
                            ("shared_capture_allocated_bytes", snapshotMetrics.SharedCaptureAllocatedBytes),
                            ("per_client_allocated_bytes", snapshotMetrics.PerClientAllocatedBytes),
                            ("total_allocated_bytes", snapshotMetrics.TotalAllocatedBytes),
                            ("average_snapshot_history_count", snapshotMetrics.AverageSnapshotHistoryCount),
                            ("max_snapshot_history_count", snapshotMetrics.MaxSnapshotHistoryCount));
                    }

                    var botMetrics = _botManager.Metrics;
                    if (botMetrics.HasMeasurements)
                    {
                        eventLog.Write(
                            "server_bot_perf",
                            ("frame", _world.Frame),
                            ("controlled_bot_count", botMetrics.ControlledBotCount),
                            ("active_input_count", botMetrics.ActiveInputCount),
                            ("zero_input_count", botMetrics.ZeroInputCount),
                            ("botbrain_active_controller_count", botMetrics.BotBrainActiveControllerCount),
                            ("botbrain_navigation_loaded_count", botMetrics.BotBrainNavigationLoadedCount),
                            ("botbrain_navigation_missing_count", botMetrics.BotBrainNavigationMissingCount),
                            ("botbrain_objective_tape_loaded_count", botMetrics.BotBrainObjectiveTapeLoadedCount),
                            ("botbrain_active_path_count", botMetrics.BotBrainActivePathCount),
                            ("sample_count", botMetrics.SampleCount),
                            ("last_build_input_ms", botMetrics.LastBuildInputMilliseconds),
                            ("average_build_input_ms", botMetrics.AverageBuildInputMilliseconds),
                            ("max_build_input_ms", botMetrics.MaxBuildInputMilliseconds),
                            ("last_apply_input_ms", botMetrics.LastApplyInputMilliseconds),
                            ("average_apply_input_ms", botMetrics.AverageApplyInputMilliseconds),
                            ("max_apply_input_ms", botMetrics.MaxApplyInputMilliseconds));
                    }

                    // Check bot autofill every 5 seconds
                    if (_botAutofillEnabled && _world.Frame - _botAutofillLastTick >= (long)_config.TicksPerSecond * 5)
                    {
                        ApplyBotAutofill();
                    }
                }

                Thread.Sleep(1);
            }
        }
        finally
        {
            _eventReporter.WriteEvent(
                "server_stopping",
                ("server_name", _serverName),
                ("port", _port),
                ("uptime_seconds", _clock?.Elapsed.TotalSeconds ?? 0d),
                ("frame", _world?.Frame ?? 0L));
            _pluginHost?.NotifyServerStopping();
            _outboundMessaging.NotifyClientsOfShutdown();
            _demoRecorder.TryStop(out _, out _);
            _demoRecorder.Dispose();
            _httpRegistryHeartbeat?.Remove();
            _httpRegistryHeartbeat?.Dispose();
            _httpRegistryHeartbeat = null;
            _webSocketHost?.Dispose();
            _webSocketHost = null;
            _pluginHost?.NotifyServerStopped();
            _pluginHost?.ShutdownPlugins();
            Console.WriteLine("[server] shutdown complete.");
        }
    }

    private void InitializePluginRuntime()
    {
        _scheduler = new ServerScheduler(() => _clock.Elapsed, Console.WriteLine);
        _adminSessionManager = new ServerAdminSessionManager(_rconPassword, () => _clock.Elapsed);
        _banService = new ServerBanService(RuntimePaths.GetConfigPath("server-bans.json"), log: Console.WriteLine);
        _cvarRegistry = CreateServerCvarRegistry();
        var pluginRuntime = OpenGarrison.Server.ServerPluginRuntimeFactory.Create(
            _config,
            _port,
            _serverName,
            _clientsBySlot,
            _world,
            () => _clock.Elapsed,
            _maxPlayableClients,
            _useLobbyServer,
            _lobbyHost,
            _lobbyPort,
            _passwordRequired,
            () => _autoBalanceEnabled,
            () => _world.ConfiguredRespawnSeconds,
            _cvarRegistry,
            _scheduler,
            slot => _clientsBySlot.TryGetValue(slot, out var client)
                ? ServerAdminSessionManager.GetClientIdentity(client)
                : OpenGarrisonServerAdminIdentity.CreateUnauthenticated(slot),
            _gameplayOwnershipService,
            _mapRotationManager,
            _mapRotationFile,
            _sessionManager,
            _snapshotBroadcaster,
            _botManager,
            _demoRecorder,
            _eventReporter.ApplyMapTransition,
            _outboundMessaging.SendMessage,
            _outboundMessaging.SendPluginMessage,
            _outboundMessaging.BroadcastPluginMessage,
            Console.WriteLine,
            Path.Combine(RuntimePaths.ApplicationRoot, "Plugins"),
            Path.Combine(RuntimePaths.ConfigDirectory, "plugins"),
            Path.Combine(RuntimePaths.ApplicationRoot, "Maps"),
            _banService);
        _pluginCommandRegistry = pluginRuntime.CommandRegistry;
        _pluginHost = pluginRuntime.PluginHost;
        _world.GameplayAbilityInputInterceptor = abilityEvent => _pluginHost?.TryNotifyGameplayAbilityInput(abilityEvent) ?? false;
        _world.SpawnDecisionInterceptor = request => ToWorldDecision(_pluginHost?.BeforeSpawn(request));
        _world.DamageDecisionInterceptor = request => ToWorldDecision(_pluginHost?.BeforeDamage(request));
        _world.DeathDecisionInterceptor = request => ToWorldDecision(_pluginHost?.BeforeDeath(request));
        _world.PickupDecisionInterceptor = request => ToWorldDecision(_pluginHost?.BeforePickup(request));
        _world.ScoreDecisionInterceptor = request => ToWorldDecision(_pluginHost?.BeforeScore(request));
        _world.RoundEndDecisionInterceptor = request => ToWorldDecision(_pluginHost?.BeforeRoundEnd(request));
        _serverState = pluginRuntime.ServerState;
        _adminOperations = pluginRuntime.AdminOperations;
        _adminChatRouter = new ServerAdminChatRouter(
            _adminSessionManager,
            () => _pluginHost,
            (slot, text) => _adminOperations.SendSystemMessage(slot, text));
    }

    private static WorldDecisionResult ToWorldDecision(OpenGarrisonServerDecisionResult? decision)
    {
        return decision is { IsCancelled: true } cancelled
            ? WorldDecisionResult.Cancel(cancelled.Reason)
            : WorldDecisionResult.Continue;
    }

    private void ApplyBotAutofill()
    {
        _botAutofillLastTick = (int)_world.Frame;

        var (humanRedCount, humanBlueCount) = GetConnectedHumanTeamCounts();
        var (targetRedBots, targetBlueBots) = ComputeBotAutofillTargets(
            humanRedCount,
            humanBlueCount,
            _botAutofillMinPlayers,
            _botAutofillPerTeam);

        var removedRed = _botManager.TrimAutofillTeam(PlayerTeam.Red, targetRedBots);
        var removedBlue = _botManager.TrimAutofillTeam(PlayerTeam.Blue, targetBlueBots);
        var addedRed = _botManager.FillAutofillTeam(PlayerTeam.Red, targetRedBots, requestedClass: null);
        var addedBlue = _botManager.FillAutofillTeam(PlayerTeam.Blue, targetBlueBots, requestedClass: null);
        var totalDelta = removedRed + removedBlue + addedRed + addedBlue;
        if (totalDelta == 0)
        {
            return;
        }

        var currentRedBots = _botManager.BotSlots.Values.Count(state => state.Team == PlayerTeam.Red);
        var currentBlueBots = _botManager.BotSlots.Values.Count(state => state.Team == PlayerTeam.Blue);
        Console.WriteLine(
            $"[server] autofill: humans={humanRedCount + humanBlueCount} " +
            $"targets=R{targetRedBots}/B{targetBlueBots} " +
            $"added=R{addedRed}/B{addedBlue} removed=R{removedRed}/B{removedBlue} " +
            $"current=R{currentRedBots}/B{currentBlueBots}.");
    }

    private (int RedCount, int BlueCount) GetConnectedHumanTeamCounts()
    {
        var redCount = 0;
        var blueCount = 0;
        foreach (var entry in _clientsBySlot)
        {
            if (IsSpectatorSlot(entry.Key))
            {
                continue;
            }

            var team = _world.GetNetworkPlayerConfiguredTeam(entry.Key);
            if (team == PlayerTeam.Red)
            {
                redCount += 1;
            }
            else if (team == PlayerTeam.Blue)
            {
                blueCount += 1;
            }
        }

        return (redCount, blueCount);
    }

    internal static (int RedBots, int BlueBots) ComputeBotAutofillTargets(
        int humanRedCount,
        int humanBlueCount,
        int minimumTotalPlayers,
        int maxBotsPerTeam)
    {
        var clampedHumanRedCount = Math.Max(0, humanRedCount);
        var clampedHumanBlueCount = Math.Max(0, humanBlueCount);
        var clampedMinimumTotalPlayers = Math.Max(0, minimumTotalPlayers);
        var clampedMaxBotsPerTeam = Math.Max(0, maxBotsPerTeam);
        var desiredTotalBots = Math.Max(
            0,
            clampedMinimumTotalPlayers - (clampedHumanRedCount + clampedHumanBlueCount));
        desiredTotalBots = Math.Min(
            desiredTotalBots,
            clampedMaxBotsPerTeam * 2);

        var targetRedBots = 0;
        var targetBlueBots = 0;
        for (var index = 0; index < desiredTotalBots; index += 1)
        {
            if (targetRedBots >= clampedMaxBotsPerTeam && targetBlueBots >= clampedMaxBotsPerTeam)
            {
                break;
            }

            if (targetRedBots >= clampedMaxBotsPerTeam)
            {
                targetBlueBots += 1;
                continue;
            }

            if (targetBlueBots >= clampedMaxBotsPerTeam)
            {
                targetRedBots += 1;
                continue;
            }

            var redTotal = clampedHumanRedCount + targetRedBots;
            var blueTotal = clampedHumanBlueCount + targetBlueBots;
            if (redTotal <= blueTotal)
            {
                targetRedBots += 1;
            }
            else
            {
                targetBlueBots += 1;
            }
        }

        return (targetRedBots, targetBlueBots);
    }

    private void InitializeHttpRegistryHeartbeat()
    {
        if (_registryUrl is null)
        {
            return;
        }

        _httpRegistryHeartbeat = new HttpServerRegistryHeartbeat(
            _registryUrl,
            _registryToken,
            CreateServerRegistrySnapshot,
            Console.WriteLine);
    }

    private ServerRegistrySnapshot CreateServerRegistrySnapshot()
    {
        var players = _world.EnumerateActiveNetworkPlayers().Count();
        var spectators = _clientsBySlot.Keys.Count(IsSpectatorSlot);
        return new ServerRegistrySnapshot(
            _serverName,
            _publicHost,
            _port,
            _webSocketHost is null ? 0 : _webSocketPort,
            _publicWebSocketUrl,
            _passwordRequired,
            _world.Level.Name,
            _world.MatchRules.Mode.ToString(),
            players,
            _maxPlayableClients,
            spectators);
    }

    private ServerCvarRegistry CreateServerCvarRegistry()
    {
        var registry = new ServerCvarRegistry();
        registry.RegisterString(
            "sv_rcon_password",
            "Remote admin password for private !gt_* sessions.",
            _rconPassword ?? string.Empty,
            () => _adminSessionManager.RconPassword,
            value =>
            {
                _adminSessionManager.RconPassword = value;
                _rconPassword = _adminSessionManager.RconPassword;
                return null;
            },
            isProtected: true);
        registry.RegisterInteger(
            "sv_timelimit",
            "Current match time limit in minutes.",
            _timeLimitMinutesOverride ?? _world.MatchRules.TimeLimitMinutes,
            () => _world.MatchRules.TimeLimitMinutes,
            value => _world.SetTimeLimitMinutes(value),
            minValue: 1,
            maxValue: 255);
        registry.RegisterInteger(
            "sv_caplimit",
            "Current capture limit.",
            _capLimitOverride ?? _world.MatchRules.CapLimit,
            () => _world.MatchRules.CapLimit,
            value => _world.SetCapLimit(value),
            minValue: 1,
            maxValue: 255);
        registry.RegisterInteger(
            "sv_respawnseconds",
            "Current respawn time in seconds.",
            _respawnSecondsOverride ?? _world.ConfiguredRespawnSeconds,
            () => _world.ConfiguredRespawnSeconds,
            value => _world.SetRespawnSeconds(value),
            minValue: 0,
            maxValue: 255);
        registry.RegisterFloat(
            "sv_player_scale",
            "Global live player collision and render scale.",
            _world.ConfiguredPlayerScale,
            () => _world.ConfiguredPlayerScale,
            value => _world.SetPlayerScale(value),
            minValue: PlayerEntity.MinPlayerScale,
            maxValue: PlayerEntity.MaxPlayerScale);
        registry.RegisterFloat(
            "sv_map_scale",
            "Current map scale. Reloads the active map safely when changed.",
            _world.ConfiguredMapScale,
            () => _world.ConfiguredMapScale,
            value => _world.SetMapScale(value),
            minValue: 0.25f,
            maxValue: 4f);
        registry.RegisterFloat(
            "sv_movement_speed_scale",
            "Global player movement speed multiplier.",
            _world.ConfiguredMovementSpeedScale,
            () => _world.ConfiguredMovementSpeedScale,
            value => _world.SetMovementSpeedScale(value),
            minValue: 0.1f,
            maxValue: 4f);
        registry.RegisterFloat(
            "sv_projectile_speed_scale",
            "Global projectile launch speed multiplier.",
            _world.ConfiguredProjectileSpeedScale,
            () => _world.ConfiguredProjectileSpeedScale,
            value => _world.SetProjectileSpeedScale(value),
            minValue: 0.1f,
            maxValue: 4f);
        registry.RegisterFloat(
            "sv_damage_scale",
            "Global damage multiplier for player and structure damage.",
            _world.ConfiguredDamageScale,
            () => _world.ConfiguredDamageScale,
            value => _world.SetDamageScale(value),
            minValue: 0f,
            maxValue: 10f);
        registry.RegisterFloat(
            "sv_gravity_scale",
            "Global gravity multiplier for player and ballistic projectile gravity.",
            _world.ConfiguredGravityScale,
            () => _world.ConfiguredGravityScale,
            value => _world.SetGravityScale(value),
            minValue: 0f,
            maxValue: 4f);
        registry.RegisterFloat(
            "sv_horizontal_speed_clamp",
            "Base horizontal movement speed clamp in source units per tick.",
            _world.ConfiguredHorizontalSpeedClampPerTick,
            () => _world.ConfiguredHorizontalSpeedClampPerTick,
            value => _world.SetHorizontalSpeedClampPerTick(value),
            minValue: 1f,
            maxValue: 60f);
        registry.RegisterFloat(
            "sv_vertical_speed_clamp",
            "Base vertical movement speed clamp in source units per tick.",
            _world.ConfiguredVerticalSpeedClampPerTick,
            () => _world.ConfiguredVerticalSpeedClampPerTick,
            value => _world.SetVerticalSpeedClampPerTick(value),
            minValue: 1f,
            maxValue: 60f);
        registry.RegisterBoolean(
            "sv_roundendff",
            "Enable same-team player damage during ended-round humiliation.",
            _world.RoundEndFriendlyFireEnabled,
            () => _world.RoundEndFriendlyFireEnabled,
            value => _world.SetRoundEndFriendlyFire(value));
        registry.RegisterBoolean(
            "sv_autobalance",
            "Enable or disable server auto-balance.",
            _autoBalanceEnabled,
            () => _autoBalanceEnabled,
            value => _autoBalanceEnabled = value);
        registry.RegisterBoolean(
            "sv_randomspread",
            "Enable or disable random bullet spread.",
            _randomSpreadEnabled,
            () => _world.RandomSpreadEnabled,
            value =>
            {
                _randomSpreadEnabled = value;
                _world.RandomSpreadEnabled = value;
            });
        registry.RegisterBoolean(
            "sv_sniper_aim_indicator",
            "Enable or disable sniper aim indicator visibility.",
            _sniperAimIndicatorEnabled,
            () => _sniperAimIndicatorEnabled,
            value =>
            {
                _sniperAimIndicatorEnabled = value;
                _world.SniperAimIndicatorEnabled = value;
            });
        registry.RegisterBoolean(
            "sv_specialabilities",
            "Enable or disable class special abilities, modular gameplay abilities, and ability-owned alternate weapons on the server.",
            _secondaryAbilitiesEnabled,
            () => _world.ExperimentalGameplaySettings.EnableSecondaryAbilities,
            SetSpecialAbilitiesEnabled);
        registry.RegisterBoolean(
            "sv_special_abilities",
            "Alias for sv_specialabilities.",
            _secondaryAbilitiesEnabled,
            () => _world.ExperimentalGameplaySettings.EnableSecondaryAbilities,
            SetSpecialAbilitiesEnabled);
        registry.RegisterBoolean(
            "sv_secondaryabilities",
            "Legacy alias for sv_specialabilities.",
            _secondaryAbilitiesEnabled,
            () => _world.ExperimentalGameplaySettings.EnableSecondaryAbilities,
            SetSpecialAbilitiesEnabled);
        registry.RegisterBoolean(
            "sv_secondary_abilities",
            "Legacy alias for sv_specialabilities.",
            _secondaryAbilitiesEnabled,
            () => _world.ExperimentalGameplaySettings.EnableSecondaryAbilities,
            SetSpecialAbilitiesEnabled);
        registry.RegisterBoolean(
            "sv_competitive_readyup",
            "Start each round in skirmish until a majority of active players ready up with F4.",
            _competitiveReadyUpEnabled,
            () => _world.CompetitiveReadyUpEnabled,
            value =>
            {
                _competitiveReadyUpEnabled = value;
                _world.SetCompetitiveReadyUpEnabled(value);
                if (!value)
                {
                    _competitiveReadyButtonDownSlots.Clear();
                }
            });
        registry.RegisterInteger(
            "sv_competitive_setup_seconds",
            "Spawn-door setup duration after competitive ready-up completes.",
            _competitiveSetupSeconds,
            () => _world.CompetitiveSetupSeconds,
            value =>
            {
                _competitiveSetupSeconds = Math.Clamp(value, 0, 120);
                _world.SetCompetitiveSetupSeconds(_competitiveSetupSeconds);
            },
            minValue: 0,
            maxValue: 120);
        registry.RegisterString(
            "sv_map",
            "Current loaded map level name.",
            _world.Level.Name,
            () => _world.Level.Name);
        registry.RegisterBoolean(
            "sv_map_rotation_shuffle",
            "Enable randomized map selection when advancing through the map rotation.",
            _mapRotationShuffleEnabled,
            () => _mapRotationManager.MapRotationShuffleEnabled,
            value =>
            {
                _mapRotationShuffleEnabled = value;
                _mapRotationManager.MapRotationShuffleEnabled = value;
            });
        registry.RegisterInteger(
            "sv_maxplayers",
            "Configured playable slot count.",
            _maxPlayableClients,
            () => _maxPlayableClients);
        registry.RegisterInteger(
            "sv_tickrate",
            "Server simulation tick rate.",
            _config.TicksPerSecond,
            () => _config.TicksPerSecond);
        registry.RegisterBoolean(
            "sv_bot_autofill",
            "Enable automatic bot filling to maintain minimum player count.",
            _botAutofillEnabled,
            () => _botAutofillEnabled,
            value => _botAutofillEnabled = value);
        registry.RegisterInteger(
            "sv_bot_autofill_min_players",
            "Minimum total players before bots are automatically added.",
            _botAutofillMinPlayers,
            () => _botAutofillMinPlayers,
            value => _botAutofillMinPlayers = value,
            minValue: 0,
            maxValue: 24);
        registry.RegisterInteger(
            "sv_bot_autofill_per_team",
            "Target bot count per team when autofilling.",
            _botAutofillPerTeam,
            () => _botAutofillPerTeam,
            value => _botAutofillPerTeam = value,
            minValue: 0,
            maxValue: 12);
        registry.EnableRuntimeProtectionPersistence(RuntimePaths.GetConfigPath("server-cvar-policy.json"));
        return registry;
    }

    private void SetSpecialAbilitiesEnabled(bool value)
    {
        _secondaryAbilitiesEnabled = value;
        _world.ConfigureExperimentalGameplaySettings(
            _world.ExperimentalGameplaySettings with
            {
                EnableSecondaryAbilities = value,
                EnableSoldierShotgunSecondaryWeapon = value,
            });
    }

    private void InitializeGameplayOwnershipService()
    {
        var ownershipStorePath = ResolvePersistentGameplayOwnershipPath(_persistentGameplayOwnershipFile);
        _gameplayOwnershipService = new GameplayOwnershipService(
            () => _world,
            new GameplayOwnershipIdentityResolver(_persistentGameplayOwnershipIdentityMode),
            new JsonGameplayOwnershipRepository(ownershipStorePath),
            Console.WriteLine);
        _sessionManager.SetGameplayOwnershipService(_gameplayOwnershipService);
        Console.WriteLine(_gameplayOwnershipService.DescribeConfiguration(ownershipStorePath));
        if (_persistentGameplayOwnershipEnabled
            && _persistentGameplayOwnershipIdentityMode == PersistentGameplayOwnershipIdentityMode.PlayerNameAndBadge)
        {
            Console.WriteLine("[server] ownership identity mode PlayerNameAndBadge is server-local and weak-trust. Use only until a stronger account identity exists.");
        }
    }

    private void InitializeIncomingPacketPump()
    {
        var messageDispatcher = new OpenGarrison.Server.ServerIncomingMessageDispatcher(
            _config,
            _serverName,
            _passwordRequired,
            _maxPlayableClients,
            _maxTotalClients,
            _maxSpectatorClients,
            _clientsBySlot,
            _sessionManager,
            _world,
            () => _clock.Elapsed,
            () => _pluginHost,
            AllocateClientUserId,
            _connectionRateLimiter.GetHelloRateLimitReason,
            _connectionRateLimiter.ResetConnectionAttemptLimits,
            _eventReporter.GetCurrentMapMetadata,
            _outboundMessaging.SendMessage,
            _outboundMessaging.SendServerStatus,
            _outboundMessaging.SendServerDetails,
            _outboundMessaging.BroadcastChat,
            _eventReporter.WriteEvent,
            Console.WriteLine,
            slot => !_botManager.BotSlots.ContainsKey(slot),
            _banService,
            receiveCustomBubbleUpload: _outboundMessaging.ReceiveCustomBubbleUpload,
            receiveCustomBubbleClear: _outboundMessaging.ReceiveCustomBubbleClear,
            sendCustomBubbleStates: _outboundMessaging.SendCustomBubbleStatesToClient);
        _incomingPacketPump = new OpenGarrison.Server.ServerIncomingPacketPump(
            _messageTransport,
            messageDispatcher,
            WsaConnReset,
            Console.WriteLine);
    }

    private static string ResolvePersistentGameplayOwnershipPath(string configuredPath)
    {
        var fileName = string.IsNullOrWhiteSpace(configuredPath)
            ? "gameplay-ownership.json"
            : configuredPath.Trim();
        return Path.IsPathRooted(fileName)
            ? fileName
            : RuntimePaths.GetConfigPath(fileName);
    }
}
