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
    private PersistentServerEventLog? _eventLog;
    private AutoBalancer _autoBalancer = null!;
    private SnapshotBroadcaster _snapshotBroadcaster = null!;
    private MapRotationManager _mapRotationManager = null!;
    private EndpointRateLimiter _helloRateLimiter = null!;
    private EndpointRateLimiter _passwordRateLimiter = null!;
    private string _cachedMapMetadataLevelName = string.Empty;
    private bool _cachedIsCustomMap;
    private string _cachedMapDownloadUrl = string.Empty;
    private string _cachedMapContentHash = string.Empty;
    private int _lastObservedRedCaps;
    private int _lastObservedBlueCaps;
    private MatchPhase _lastObservedMatchPhase;
    private int _lastObservedKillFeedCount;
    private readonly Dictionary<int, int> _lastObservedPlayerCapsById = new();

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
        _udp = udp;
        _udp.Client.Blocking = false;
        TryDisableUdpConnectionReset(_udp.Client);

        if (_useLobbyServer)
        {
            _lobbyRegistrar = new LobbyServerRegistrar(
                _udp,
                _lobbyHost,
                _lobbyPort,
                _protocolUuidBytes,
                _port,
                TimeSpan.FromSeconds(_lobbyHeartbeatSeconds),
                TimeSpan.FromSeconds(_lobbyResolveSeconds));
        }

        _world = new SimulationWorld(_config);
        if (_timeLimitMinutesOverride.HasValue || _capLimitOverride.HasValue || _respawnSecondsOverride.HasValue)
        {
            _world.ConfigureMatchDefaults(
                timeLimitMinutes: _timeLimitMinutesOverride,
                capLimit: _capLimitOverride,
                respawnSeconds: _respawnSecondsOverride);
        }
        _world.AutoRestartOnMapChange = false;
        _mapRotationManager = new MapRotationManager(_world, _requestedMap, _mapRotationFile, _stockMapRotation, Console.WriteLine);
        _world.DespawnEnemyDummy();
        _world.TryPrepareNetworkPlayerJoin(SimulationWorld.LocalPlayerSlot);
        ResetObservedGameplayState();

        _simulator = new FixedStepSimulator(_world);
        _clock = Stopwatch.StartNew();
        _previous = _clock.Elapsed;
        _clientsBySlot = new Dictionary<byte, ClientSession>();
        _helloRateLimiter = new EndpointRateLimiter(MaxNewHelloAttemptsPerWindow, HelloAttemptWindow, HelloCooldown, () => _clock.Elapsed);
        _passwordRateLimiter = new EndpointRateLimiter(MaxPasswordFailuresPerWindow, PasswordFailureWindow, PasswordCooldown, () => _clock.Elapsed);

        _sessionManager = new ServerSessionManager(
            _world,
            _clientsBySlot,
            _maxPlayableClients,
            _maxTotalClients,
            _maxSpectatorClients,
            () => _clock.Elapsed,
            _serverPassword,
            _passwordRequired,
            _clientTimeoutSeconds,
            _passwordTimeoutSeconds,
            _passwordRetrySeconds,
            GetPasswordRateLimitReason,
            RecordPasswordFailure,
            ClearPasswordFailures,
            SendMessage,
            Console.WriteLine,
            OnClientRemoved,
            OnPasswordAccepted,
            OnPlayerTeamChanged,
            OnPlayerClassChanged);
        _autoBalancer = new AutoBalancer(
            _world,
            _config,
            _clientsBySlot,
            _autoBalanceDelaySeconds,
            _autoBalanceNewPlayerGraceSeconds,
            _passwordRequired,
            SendMessage,
            Console.WriteLine);
        _snapshotBroadcaster = new SnapshotBroadcaster(
            _world,
            _config,
            _clientsBySlot,
            _transientEventReplayTicks,
            SendSnapshotPayload);
        _eventLog = new PersistentServerEventLog(_eventLogPath, Console.WriteLine);
        InitializePluginRuntime();
        InitializeIncomingPacketPump();
        _pluginHost?.LoadPlugins();
        _pluginHost?.NotifyServerStarting();

        Console.WriteLine($"OpenGarrison.Server booting at {_config.TicksPerSecond} ticks/sec.");
        Console.WriteLine($"Protocol version: {ProtocolVersion.Current}");
        Console.WriteLine($"UDP bind: 0.0.0.0:{_port}");
        Console.WriteLine($"Name: {_serverName}");
        Console.WriteLine($"Max players: {_maxPlayableClients}");
        if (timerResolution.IsActive)
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
        Console.WriteLine($"Event log: {_eventLog?.FilePath ?? _eventLogPath}");
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
        LogServerEvent(
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

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ProcessPendingConsoleCommands();
                _helloRateLimiter.Prune();
                _passwordRateLimiter.Prune();
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
                            NotifyMapTransition(transition);
                            ResetObservedGameplayState();
                            _snapshotBroadcaster.ResetTransientEvents();
                        }
                    },
                    _snapshotBroadcaster.BroadcastSnapshot);
                _lobbyRegistrar?.Tick(now, BuildLobbyServerName(_serverName, _world, _clientsBySlot, _passwordRequired, _maxPlayableClients));
                if (ticks > 0)
                {
                    PublishGameplayEvents();
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
            LogServerEvent(
                "server_stopping",
                ("server_name", _serverName),
                ("port", _port),
                ("uptime_seconds", _clock?.Elapsed.TotalSeconds ?? 0d),
                ("frame", _world?.Frame ?? 0L));
            _pluginHost?.NotifyServerStopping();
            NotifyClientsOfShutdown();
            _pluginHost?.NotifyServerStopped();
            _pluginHost?.ShutdownPlugins();
            _eventLog?.Dispose();
            _eventLog = null;
            Console.WriteLine("[server] shutdown complete.");
        }
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

    private void SendServerStatus(IPEndPoint remoteEndPoint)
    {
        var playerCount = _clientsBySlot.Count;
        var spectatorCount = _clientsBySlot.Keys.Count(IsSpectatorSlot);
        SendMessage(
            remoteEndPoint,
            new ServerStatusResponseMessage(
                _serverName,
                _world.Level.Name,
                (byte)_world.MatchRules.Mode,
                playerCount - spectatorCount,
                _maxPlayableClients,
                spectatorCount));
    }

    private void BroadcastChat(ClientSession client, string text, bool teamOnly)
    {
        var sanitized = text.Trim();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return;
        }

        if (sanitized.Length > 120)
        {
            sanitized = sanitized[..120];
        }

        var team = TryGetClientChatTeam(client) is { } resolvedTeam
            ? (byte)resolvedTeam
            : (byte)0;
        var chatEvent = new ChatReceivedEvent(
            client.Slot,
            client.Name,
            sanitized,
            team == 0 ? null : (PlayerTeam)team,
            teamOnly);
        if (_pluginHost?.TryHandleChatMessage(chatEvent) == true)
        {
            return;
        }

        LogServerEvent(
            "chat_received",
            ("slot", client.Slot),
            ("player_name", client.Name),
            ("team", team == 0 ? null : ((PlayerTeam)team).ToString()),
            ("team_only", teamOnly),
            ("text", sanitized));
        _pluginHost?.NotifyChatReceived(chatEvent);
        var relay = new ChatRelayMessage(team, client.Name, sanitized, teamOnly);
        foreach (var session in _clientsBySlot.Values)
        {
            if (teamOnly)
            {
                var sessionTeam = TryGetClientChatTeam(session);
                if (team == 0)
                {
                    if (session.Slot != client.Slot)
                    {
                        continue;
                    }
                }
                else if (sessionTeam != (PlayerTeam)team)
                {
                    continue;
                }
            }

            SendMessage(session.EndPoint, relay);
        }

        Console.WriteLine(teamOnly
            ? $"[team chat] {client.Name}: {sanitized}"
            : $"[chat] {client.Name}: {sanitized}");
    }

    private PlayerTeam? TryGetClientChatTeam(ClientSession client)
    {
        return SimulationWorld.IsPlayableNetworkPlayerSlot(client.Slot)
            && _world.TryGetNetworkPlayer(client.Slot, out var player)
            ? player.Team
            : null;
    }

    private void SendMessage(IPEndPoint remoteEndPoint, IProtocolMessage message)
    {
        var payload = ProtocolCodec.Serialize(message);
        SendPayload(remoteEndPoint, payload);
    }

    private void SendSnapshotPayload(IPEndPoint remoteEndPoint, SnapshotMessage snapshot, byte[] payload)
    {
        SendPayload(remoteEndPoint, payload);
    }

    private void SendPayload(IPEndPoint remoteEndPoint, byte[] payload)
    {
        _udp.Send(payload, payload.Length, remoteEndPoint);
    }

    private string? GetHelloRateLimitReason(IPEndPoint remoteEndPoint)
    {
        if (_passwordRateLimiter.IsLimited(remoteEndPoint, out var passwordRetryAfter))
        {
            return BuildRetryMessage("Too many password attempts", passwordRetryAfter);
        }

        if (!_helloRateLimiter.TryConsume(remoteEndPoint, out var helloRetryAfter))
        {
            return BuildRetryMessage("Too many connection attempts", helloRetryAfter);
        }

        return null;
    }

    private string? GetPasswordRateLimitReason(IPEndPoint remoteEndPoint)
    {
        if (!_passwordRateLimiter.IsLimited(remoteEndPoint, out var retryAfter))
        {
            return null;
        }

        return BuildRetryMessage("Too many password attempts", retryAfter);
    }

    private void RecordPasswordFailure(IPEndPoint remoteEndPoint)
    {
        _passwordRateLimiter.TryConsume(remoteEndPoint, out _);
    }

    private void ClearPasswordFailures(IPEndPoint remoteEndPoint)
    {
        _passwordRateLimiter.Reset(remoteEndPoint);
    }

    private void NotifyClientsOfShutdown()
    {
        if (_clientsBySlot is null || _clientsBySlot.Count == 0)
        {
            return;
        }

        foreach (var client in _clientsBySlot.Values)
        {
            try
            {
                SendMessage(client.EndPoint, new ConnectionDeniedMessage("Server shutting down."));
            }
            catch
            {
            }
        }
    }

    private static string BuildRetryMessage(string prefix, TimeSpan retryAfter)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        return $"{prefix}. Try again in {seconds}s.";
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
            SendMessage,
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
            GetHelloRateLimitReason,
            remoteEndPoint =>
            {
                _helloRateLimiter.Reset(remoteEndPoint);
                _passwordRateLimiter.Reset(remoteEndPoint);
            },
            GetCurrentMapMetadata,
            SendMessage,
            SendServerStatus,
            BroadcastChat,
            (eventName, fields) => LogServerEvent(eventName, fields),
            Console.WriteLine);
        _incomingPacketPump = new OpenGarrison.Server.ServerIncomingPacketPump(
            _udp,
            messageDispatcher,
            WsaConnReset,
            Console.WriteLine);
    }

    private void ResetObservedGameplayState()
    {
        _lastObservedRedCaps = _world.RedCaps;
        _lastObservedBlueCaps = _world.BlueCaps;
        _lastObservedMatchPhase = _world.MatchState.Phase;
        _lastObservedKillFeedCount = _world.KillFeed.Count;
        _lastObservedPlayerCapsById.Clear();
        foreach (var (_, player) in _world.EnumerateActiveNetworkPlayers())
        {
            _lastObservedPlayerCapsById[player.Id] = player.Caps;
        }
    }

    private void LogServerEvent(string eventName, params (string Key, object? Value)[] fields)
    {
        _eventLog?.Write(eventName, fields);
    }

    private void PublishPlayerCapEvents()
    {
        var activePlayerIds = new HashSet<int>();
        foreach (var (slot, player) in _world.EnumerateActiveNetworkPlayers())
        {
            activePlayerIds.Add(player.Id);
            var previousCaps = _lastObservedPlayerCapsById.GetValueOrDefault(player.Id, player.Caps);
            if (player.Caps > previousCaps)
            {
                for (var capsAwarded = previousCaps; capsAwarded < player.Caps; capsAwarded += 1)
                {
                    LogServerEvent(
                        "player_cap_awarded",
                        ("frame", _world.Frame),
                        ("slot", slot),
                        ("player_id", player.Id),
                        ("player_name", player.DisplayName),
                        ("team", player.Team),
                        ("caps_total", capsAwarded + 1),
                        ("mode", _world.MatchRules.Mode),
                        ("red_caps", _world.RedCaps),
                        ("blue_caps", _world.BlueCaps));
                }
            }

            _lastObservedPlayerCapsById[player.Id] = player.Caps;
        }

        if (_lastObservedPlayerCapsById.Count == activePlayerIds.Count)
        {
            return;
        }

        var stalePlayerIds = _lastObservedPlayerCapsById.Keys.Where(playerId => !activePlayerIds.Contains(playerId)).ToArray();
        for (var index = 0; index < stalePlayerIds.Length; index += 1)
        {
            _lastObservedPlayerCapsById.Remove(stalePlayerIds[index]);
        }
    }

    private void OnClientRemoved(ClientSession client, string reason)
    {
        LogServerEvent(
            "client_disconnected",
            ("slot", client.Slot),
            ("player_name", client.Name),
            ("endpoint", client.EndPoint.ToString()),
            ("reason", reason),
            ("was_authorized", client.IsAuthorized));
        _pluginHost?.NotifyClientDisconnected(new ClientDisconnectedEvent(
            client.Slot,
            client.Name,
            client.EndPoint.ToString(),
            reason,
            client.IsAuthorized));
    }

    private void OnPasswordAccepted(ClientSession client)
    {
        LogServerEvent(
            "password_accepted",
            ("slot", client.Slot),
            ("player_name", client.Name),
            ("endpoint", client.EndPoint.ToString()));
        _pluginHost?.NotifyPasswordAccepted(new PasswordAcceptedEvent(
            client.Slot,
            client.Name,
            client.EndPoint.ToString()));
    }

    private void OnPlayerTeamChanged(ClientSession client, PlayerTeam team)
    {
        LogServerEvent(
            "player_team_changed",
            ("slot", client.Slot),
            ("player_name", client.Name),
            ("team", team));
        _pluginHost?.NotifyPlayerTeamChanged(new PlayerTeamChangedEvent(client.Slot, client.Name, team));
    }

    private void OnPlayerClassChanged(ClientSession client, PlayerClass playerClass)
    {
        LogServerEvent(
            "player_class_changed",
            ("slot", client.Slot),
            ("player_name", client.Name),
            ("player_class", playerClass));
        _pluginHost?.NotifyPlayerClassChanged(new PlayerClassChangedEvent(client.Slot, client.Name, playerClass));
    }

    private void NotifyMapTransition(MapChangeTransition transition)
    {
        LogServerEvent(
            "map_changing",
            ("current_level_name", transition.CurrentLevelName),
            ("current_area_index", transition.CurrentAreaIndex),
            ("current_area_count", transition.CurrentAreaCount),
            ("next_level_name", transition.NextLevelName),
            ("next_area_index", transition.NextAreaIndex),
            ("preserve_player_stats", transition.PreservePlayerStats),
            ("winner_team", transition.WinnerTeam?.ToString()));
        _pluginHost?.NotifyMapChanging(new MapChangingEvent(
            transition.CurrentLevelName,
            transition.CurrentAreaIndex,
            transition.CurrentAreaCount,
            transition.NextLevelName,
            transition.NextAreaIndex,
            transition.PreservePlayerStats,
            transition.WinnerTeam));
        LogServerEvent(
            "map_changed",
            ("level_name", _world.Level.Name),
            ("area_index", _world.Level.MapAreaIndex),
            ("area_count", _world.Level.MapAreaCount),
            ("mode", _world.MatchRules.Mode));
        _pluginHost?.NotifyMapChanged(new MapChangedEvent(
            _world.Level.Name,
            _world.Level.MapAreaIndex,
            _world.Level.MapAreaCount,
            _world.MatchRules.Mode));
    }

    private (bool IsCustomMap, string MapDownloadUrl, string MapContentHash) GetCurrentMapMetadata()
    {
        var levelName = _world.Level.Name;
        if (string.Equals(_cachedMapMetadataLevelName, levelName, StringComparison.OrdinalIgnoreCase))
        {
            return (_cachedIsCustomMap, _cachedMapDownloadUrl, _cachedMapContentHash);
        }

        _cachedMapMetadataLevelName = levelName;
        if (CustomMapDescriptorResolver.TryResolve(levelName, out var descriptor))
        {
            _cachedIsCustomMap = true;
            _cachedMapDownloadUrl = descriptor.SourceUrl;
            _cachedMapContentHash = descriptor.ContentHash;
        }
        else
        {
            _cachedIsCustomMap = false;
            _cachedMapDownloadUrl = string.Empty;
            _cachedMapContentHash = string.Empty;
        }

        return (_cachedIsCustomMap, _cachedMapDownloadUrl, _cachedMapContentHash);
    }

    private void PublishGameplayEvents()
    {
        PublishPlayerCapEvents();

        if (_world.RedCaps != _lastObservedRedCaps || _world.BlueCaps != _lastObservedBlueCaps)
        {
            LogServerEvent(
                "score_changed",
                ("frame", _world.Frame),
                ("mode", _world.MatchRules.Mode),
                ("red_caps", _world.RedCaps),
                ("blue_caps", _world.BlueCaps),
                ("previous_red_caps", _lastObservedRedCaps),
                ("previous_blue_caps", _lastObservedBlueCaps));
            _pluginHost?.NotifyScoreChanged(new ScoreChangedEvent(_world.RedCaps, _world.BlueCaps, _world.MatchRules.Mode));
            _lastObservedRedCaps = _world.RedCaps;
            _lastObservedBlueCaps = _world.BlueCaps;
        }

        var killFeed = _world.KillFeed;
        if (killFeed.Count < _lastObservedKillFeedCount)
        {
            _lastObservedKillFeedCount = 0;
        }

        for (var index = _lastObservedKillFeedCount; index < killFeed.Count; index += 1)
        {
            var entry = killFeed[index];
            LogServerEvent(
                "kill",
                ("frame", _world.Frame),
                ("killer_name", entry.KillerName),
                ("killer_team", entry.KillerTeam),
                ("weapon_sprite_name", entry.WeaponSpriteName),
                ("victim_name", entry.VictimName),
                ("victim_team", entry.VictimTeam),
                ("message_text", entry.MessageText));
            _pluginHost?.NotifyKillFeedEntry(new KillFeedEvent(
                entry.KillerName,
                entry.KillerTeam,
                entry.WeaponSpriteName,
                entry.VictimName,
                entry.VictimTeam,
                entry.MessageText));
        }

        _lastObservedKillFeedCount = killFeed.Count;

        if (_lastObservedMatchPhase != MatchPhase.Ended && _world.MatchState.Phase == MatchPhase.Ended)
        {
            LogServerEvent(
                "round_ended",
                ("frame", _world.Frame),
                ("mode", _world.MatchRules.Mode),
                ("winner_team", _world.MatchState.WinnerTeam?.ToString()),
                ("red_caps", _world.RedCaps),
                ("blue_caps", _world.BlueCaps));
            _pluginHost?.NotifyRoundEnded(new RoundEndedEvent(
                _world.MatchRules.Mode,
                _world.MatchState.WinnerTeam,
                _world.RedCaps,
                _world.BlueCaps,
                _world.Frame));
        }

        _lastObservedMatchPhase = _world.MatchState.Phase;
    }
}
