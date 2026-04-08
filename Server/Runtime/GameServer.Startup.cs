using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using OpenGarrison.Core;
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
        InitializeGameplayOwnershipService();
        InitializePluginRuntime();
        InitializeIncomingPacketPump();
        StartAndAnnounceServer(timerResolution.IsActive, eventLog);
        RunMainLoop(cancellationToken);
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
    }

    private OpenGarrison.Server.ServerRuntimeBootstrap CreateRuntimeBootstrap(PersistentServerEventLog eventLog)
    {
        return OpenGarrison.Server.ServerRuntimeBootstrapFactory.Create(
            _config,
            _udp,
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
    }

    private void StartAndAnnounceServer(bool highResolutionTimerEnabled, PersistentServerEventLog eventLog)
    {
        _pluginHost?.LoadPlugins();
        _pluginHost?.NotifyServerStarting();

        Console.WriteLine($"OG2.Server booting at {_config.TicksPerSecond} ticks/sec.");
        Console.WriteLine($"Protocol version: {ProtocolVersion.Current}");
        Console.WriteLine($"UDP bind: 0.0.0.0:{_port}");
        Console.WriteLine($"Name: {_serverName}");
        Console.WriteLine($"Max players: {_maxPlayableClients}");
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
        Console.WriteLine($"Level: {_world.Level.Name} area={_world.Level.MapAreaIndex}/{_world.Level.MapAreaCount} imported={_world.Level.ImportedFromSource} mode={_world.MatchRules.Mode}");
        Console.WriteLine($"World bounds: {_world.Bounds.Width}x{_world.Bounds.Height}");
        Console.WriteLine($"Event log: {eventLog.FilePath}");
        Console.WriteLine(_passwordRequired ? "[server] password required" : "[server] no password set");
        if (_useLobbyServer)
        {
            Console.WriteLine($"[server] lobby registration enabled host={_lobbyHost}:{_lobbyPort}");
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

    private void RunMainLoop(CancellationToken cancellationToken)
    {
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
                    _sessionManager.PreparePlayableClientInputsForNextTick,
                    () =>
                    {
                        _autoBalancer.Tick(now, 1, _autoBalanceEnabled);
                        if (_mapRotationManager.TryApplyPendingMapChange(out var transition))
                        {
                            _eventReporter.ApplyMapTransition(transition);
                            _snapshotBroadcaster.ResetTransientEvents();
                        }
                    },
                    _snapshotBroadcaster.BroadcastSnapshot);
                _lobbyRegistrar?.Tick(now, BuildLobbyServerName(_serverName, _world, _clientsBySlot, _passwordRequired, _maxPlayableClients));
                if (ticks > 0)
                {
                    _eventReporter.PublishGameplayEvents(_snapshotBroadcaster.LastCapturedTransientEvents);
                }

                if (ticks > 0 && _world.Frame % _config.TicksPerSecond == 0)
                {
                    var activePlayableCount = _world.EnumerateActiveNetworkPlayers().Count();
                    Console.WriteLine(
                        $"[server] frame={_world.Frame} clients={_clientsBySlot.Count} " +
                        $"mode={_world.MatchRules.Mode} phase={_world.MatchState.Phase} hp={_world.LocalPlayer.Health}/{_world.LocalPlayer.MaxHealth} " +
                        $"ammo={_world.LocalPlayer.CurrentShells}/{_world.LocalPlayer.MaxShells} pos=({_world.LocalPlayer.X:F1},{_world.LocalPlayer.Y:F1}) " +
                        $"activePlayable={activePlayableCount} spectators={_clientsBySlot.Keys.Count(IsSpectatorSlot)} caps={_world.RedCaps}-{_world.BlueCaps}");
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
            _pluginHost?.NotifyServerStopped();
            _pluginHost?.ShutdownPlugins();
            Console.WriteLine("[server] shutdown complete.");
        }
    }

    private void InitializePluginRuntime()
    {
        _scheduler = new ServerScheduler(() => _clock.Elapsed, Console.WriteLine);
        _adminSessionManager = new ServerAdminSessionManager(_rconPassword, () => _clock.Elapsed);
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
            _respawnSecondsOverride,
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
            _eventReporter.ApplyMapTransition,
            _outboundMessaging.SendMessage,
            _outboundMessaging.SendPluginMessage,
            _outboundMessaging.BroadcastPluginMessage,
            Console.WriteLine,
            Path.Combine(RuntimePaths.ApplicationRoot, "Plugins"),
            Path.Combine(RuntimePaths.ConfigDirectory, "plugins"),
            Path.Combine(RuntimePaths.ApplicationRoot, "Maps"));
        _pluginCommandRegistry = pluginRuntime.CommandRegistry;
        _pluginHost = pluginRuntime.PluginHost;
        _serverState = pluginRuntime.ServerState;
        _adminOperations = pluginRuntime.AdminOperations;
        _adminChatRouter = new ServerAdminChatRouter(
            _adminSessionManager,
            () => _pluginHost,
            (slot, text) => _adminOperations.SendSystemMessage(slot, text));
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
            "sv_caplimit",
            "Current capture limit.",
            _capLimitOverride ?? _world.MatchRules.CapLimit,
            () => _world.MatchRules.CapLimit,
            value => _world.SetCapLimit(value),
            minValue: 1,
            maxValue: 255);
        registry.RegisterBoolean(
            "sv_autobalance",
            "Enable or disable server auto-balance.",
            _autoBalanceEnabled,
            () => _autoBalanceEnabled,
            value => _autoBalanceEnabled = value);
        registry.RegisterString(
            "sv_map",
            "Current loaded map level name.",
            _world.Level.Name,
            () => _world.Level.Name);
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
        return registry;
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
            _connectionRateLimiter.GetHelloRateLimitReason,
            _connectionRateLimiter.ResetConnectionAttemptLimits,
            _eventReporter.GetCurrentMapMetadata,
            _outboundMessaging.SendMessage,
            _outboundMessaging.SendServerStatus,
            _outboundMessaging.BroadcastChat,
            _eventReporter.WriteEvent,
            Console.WriteLine);
        _incomingPacketPump = new OpenGarrison.Server.ServerIncomingPacketPump(
            _udp,
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
