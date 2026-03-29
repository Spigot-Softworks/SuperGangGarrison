using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server.Plugins;
using static ServerHelpers;

sealed class GameServer
{
    private sealed record PendingConsoleCommand(
        string Command,
        bool EchoToConsole,
        TaskCompletionSource<IReadOnlyList<string>>? Completion);

    private const int WsaConnReset = 10054;
    private const int SioUdpConnReset = -1744830452;
    private const int MaxNewHelloAttemptsPerWindow = 8;
    private const int MaxPasswordFailuresPerWindow = 3;
    private static readonly TimeSpan HelloAttemptWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HelloCooldown = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan PasswordFailureWindow = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PasswordCooldown = TimeSpan.FromSeconds(10);

    private readonly SimulationConfig _config;
    private readonly int _port;
    private readonly string _serverName;
    private readonly string? _serverPassword;
    private readonly bool _useLobbyServer;
    private readonly string _lobbyHost;
    private readonly int _lobbyPort;
    private readonly string _protocolUuidString;
    private readonly int _lobbyHeartbeatSeconds;
    private readonly int _lobbyResolveSeconds;
    private readonly string? _requestedMap;
    private readonly string? _mapRotationFile;
    private readonly string _eventLogPath;
    private readonly IReadOnlyList<string> _stockMapRotation;
    private readonly int _maxPlayableClients;
    private readonly int _maxTotalClients;
    private readonly int _maxSpectatorClients;
    private readonly int _autoBalanceDelaySeconds;
    private readonly int _autoBalanceNewPlayerGraceSeconds;
    private readonly bool _autoBalanceEnabled;
    private readonly int? _timeLimitMinutesOverride;
    private readonly int? _capLimitOverride;
    private readonly int? _respawnSecondsOverride;
    private readonly double _clientTimeoutSeconds;
    private readonly double _passwordTimeoutSeconds;
    private readonly double _passwordRetrySeconds;
    private readonly ulong _transientEventReplayTicks;
    private readonly bool _passwordRequired;
    private readonly byte[] _protocolUuidBytes;
    private readonly ConcurrentQueue<PendingConsoleCommand> _pendingConsoleCommands = new();

    private UdpClient _udp = null!;
    private LobbyServerRegistrar? _lobbyRegistrar;
    private SimulationWorld _world = null!;
    private FixedStepSimulator _simulator = null!;
    private Stopwatch _clock = null!;
    private TimeSpan _previous;
    private Dictionary<byte, ClientSession> _clientsBySlot = null!;
    private ServerSessionManager _sessionManager = null!;
    private OpenGarrison.Server.Plugins.IOpenGarrisonServerReadOnlyState _serverState = null!;
    private OpenGarrison.Server.Plugins.IOpenGarrisonServerAdminOperations _adminOperations = null!;
    private OpenGarrison.Server.PluginCommandRegistry _pluginCommandRegistry = null!;
    private OpenGarrison.Server.PluginHost? _pluginHost;
    private OpenGarrison.Server.ServerIncomingPacketPump _incomingPacketPump = null!;
    private OpenGarrison.Server.ServerRuntimeEventReporter _eventReporter = null!;
    private OpenGarrison.Server.ServerOutboundMessaging _outboundMessaging = null!;
    private AutoBalancer _autoBalancer = null!;
    private SnapshotBroadcaster _snapshotBroadcaster = null!;
    private MapRotationManager _mapRotationManager = null!;
    private OpenGarrison.Server.ServerConnectionRateLimiter _connectionRateLimiter = null!;

    public GameServer(
        SimulationConfig config,
        int port,
        string serverName,
        string? serverPassword,
        bool useLobbyServer,
        string lobbyHost,
        int lobbyPort,
        string protocolUuidString,
        int lobbyHeartbeatSeconds,
        int lobbyResolveSeconds,
        string? requestedMap,
        string? mapRotationFile,
        string eventLogPath,
        IReadOnlyList<string> stockMapRotation,
        int maxPlayableClients,
        int maxTotalClients,
        int maxSpectatorClients,
        int autoBalanceDelaySeconds,
        int autoBalanceNewPlayerGraceSeconds,
        bool autoBalanceEnabled,
        int? timeLimitMinutesOverride,
        int? capLimitOverride,
        int? respawnSecondsOverride,
        double clientTimeoutSeconds,
        double passwordTimeoutSeconds,
        double passwordRetrySeconds,
        ulong transientEventReplayTicks)
    {
        _config = config;
        _port = port;
        _serverName = serverName;
        _serverPassword = serverPassword;
        _useLobbyServer = useLobbyServer;
        _lobbyHost = lobbyHost;
        _lobbyPort = lobbyPort;
        _protocolUuidString = protocolUuidString;
        _lobbyHeartbeatSeconds = lobbyHeartbeatSeconds;
        _lobbyResolveSeconds = lobbyResolveSeconds;
        _requestedMap = requestedMap;
        _mapRotationFile = mapRotationFile;
        _eventLogPath = eventLogPath;
        _stockMapRotation = stockMapRotation;
        _maxPlayableClients = maxPlayableClients;
        _maxTotalClients = maxTotalClients;
        _maxSpectatorClients = maxSpectatorClients;
        _autoBalanceDelaySeconds = autoBalanceDelaySeconds;
        _autoBalanceNewPlayerGraceSeconds = autoBalanceNewPlayerGraceSeconds;
        _autoBalanceEnabled = autoBalanceEnabled;
        _timeLimitMinutesOverride = timeLimitMinutesOverride;
        _capLimitOverride = capLimitOverride;
        _respawnSecondsOverride = respawnSecondsOverride;
        _clientTimeoutSeconds = clientTimeoutSeconds;
        _passwordTimeoutSeconds = passwordTimeoutSeconds;
        _passwordRetrySeconds = passwordRetrySeconds;
        _transientEventReplayTicks = transientEventReplayTicks;
        _passwordRequired = !string.IsNullOrWhiteSpace(serverPassword);
        _protocolUuidBytes = ParseProtocolUuid(protocolUuidString);
    }

    public void Run(CancellationToken cancellationToken)
    {
        using var udp = new UdpClient(_port);
        using var timerResolution = WindowsTimerResolutionScope.Create1Millisecond();
        using var eventLog = new PersistentServerEventLog(_eventLogPath, Console.WriteLine);
        InitializeUdpTransport(udp);
        ApplyRuntimeBootstrap(CreateRuntimeBootstrap(eventLog));
        InitializePluginRuntime();
        InitializeIncomingPacketPump();
        StartAndAnnounceServer(timerResolution.IsActive, eventLog);
        RunMainLoop(cancellationToken);
    }

    public void EnqueueConsoleCommand(string command)
    {
        if (!string.IsNullOrWhiteSpace(command))
        {
            _pendingConsoleCommands.Enqueue(new PendingConsoleCommand(command.Trim(), EchoToConsole: true, Completion: null));
        }
    }

    public Task<IReadOnlyList<string>> ExecuteAdminCommandAsync(string command, bool echoToConsole, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (cancellationToken.IsCancellationRequested)
        {
            tcs.TrySetCanceled(cancellationToken);
            return tcs.Task;
        }

        cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        _pendingConsoleCommands.Enqueue(new PendingConsoleCommand(command.Trim(), echoToConsole, tcs));
        return tcs.Task;
    }

    private void PumpIncomingPackets()
    {
        _incomingPacketPump.PumpAvailablePackets();
    }

    private void ProcessPendingConsoleCommands()
    {
        while (_pendingConsoleCommands.TryDequeue(out var request))
        {
            var lines = BuildConsoleCommandResponse(request.Command);
            if (request.EchoToConsole)
            {
                foreach (var line in lines)
                {
                    Console.WriteLine(line);
                }
            }

            request.Completion?.TrySetResult(lines);
        }
    }

    private List<string> BuildConsoleCommandResponse(string command)
    {
        var normalized = command.Trim();
        if (normalized.Length == 0)
        {
            return [];
        }

        if (_pluginCommandRegistry.TryExecute(normalized, CreateCommandContext(), CancellationToken.None, out var responseLines))
        {
            return responseLines.ToList();
        }

        return [$"[server] unknown command \"{normalized}\". Type help for commands."];
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
        foreach (var line in BuildConsoleCommandResponse("status"))
        {
            Console.WriteLine(line);
        }

        foreach (var line in BuildConsoleCommandResponse("rotation"))
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
                    _eventReporter.PublishGameplayEvents();
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
            _autoBalanceEnabled,
            _respawnSecondsOverride,
            _mapRotationManager,
            _mapRotationFile,
            _sessionManager,
            _snapshotBroadcaster,
            _eventReporter.ApplyMapTransition,
            _outboundMessaging.SendMessage,
            Console.WriteLine,
            Path.Combine(RuntimePaths.ApplicationRoot, "Plugins"),
            Path.Combine(RuntimePaths.ConfigDirectory, "plugins"),
            Path.Combine(RuntimePaths.ApplicationRoot, "Maps"));
        _pluginCommandRegistry = pluginRuntime.CommandRegistry;
        _pluginHost = pluginRuntime.PluginHost;
        _serverState = pluginRuntime.ServerState;
        _adminOperations = pluginRuntime.AdminOperations;
    }

    private OpenGarrisonServerCommandContext CreateCommandContext()
    {
        return new OpenGarrisonServerCommandContext(
            _serverState,
            _adminOperations);
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
}
