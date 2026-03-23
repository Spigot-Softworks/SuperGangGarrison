using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    private OpenGarrison.Server.PluginCommandRegistry _pluginCommandRegistry = null!;
    private OpenGarrison.Server.PluginHost? _pluginHost;
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
                _sessionManager.PruneTimedOutClients();
                PumpIncomingPackets();
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
        while (_udp.Available > 0)
        {
            try
            {
                IPEndPoint remoteEndPoint = new(IPAddress.Any, 0);
                var payload = _udp.Receive(ref remoteEndPoint);
                if (!ProtocolCodec.TryDeserialize(payload, out var message) || message is null)
                {
                    continue;
                }

                switch (message)
                {
                    case ServerStatusRequestMessage:
                        SendServerStatus(remoteEndPoint);
                        break;
                    case HelloMessage hello:
                        HandleHello(hello, remoteEndPoint);
                        break;
                    case PasswordSubmitMessage passwordSubmit:
                        var passwordClient = FindClient(_clientsBySlot, remoteEndPoint);
                        if (passwordClient is null)
                        {
                            break;
                        }

                        passwordClient.LastSeen = _clock.Elapsed;
                        _sessionManager.HandlePasswordSubmit(passwordClient, passwordSubmit);
                        break;
                    case ChatSubmitMessage chatSubmit:
                        var chatClient = FindClient(_clientsBySlot, remoteEndPoint);
                        if (chatClient is null)
                        {
                            break;
                        }

                        chatClient.LastSeen = _clock.Elapsed;
                        if (!chatClient.IsAuthorized && _passwordRequired)
                        {
                            break;
                        }

                        BroadcastChat(chatClient, chatSubmit.Text);
                        break;
                    case SnapshotAckMessage snapshotAck:
                        var ackClient = FindClient(_clientsBySlot, remoteEndPoint);
                        if (ackClient is null)
                        {
                            break;
                        }

                        ackClient.LastSeen = _clock.Elapsed;
                        ackClient.AcknowledgeSnapshot(snapshotAck.Frame);
                        break;
                    case InputStateMessage input:
                        var client = FindClient(_clientsBySlot, remoteEndPoint);
                        if (client is null)
                        {
                            break;
                        }

                        client.LastSeen = _clock.Elapsed;
                        if (!client.IsAuthorized && _passwordRequired)
                        {
                            break;
                        }
                        client.TrySetLatestInput(input.Sequence, ToCoreInput(input));

                        if (input.ChatBubbleFrameIndex >= 0)
                        {
                            _world.TryTriggerNetworkPlayerChatBubble(client.Slot, input.ChatBubbleFrameIndex);
                        }
                        break;
                    case ControlCommandMessage command:
                        var controlClient = FindClient(_clientsBySlot, remoteEndPoint);
                        if (controlClient is null)
                        {
                            break;
                        }

                        controlClient.LastSeen = _clock.Elapsed;
                        if (!controlClient.IsAuthorized && _passwordRequired)
                        {
                            break;
                        }
                        _sessionManager.HandleControlCommand(controlClient, command);
                        break;
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset || ex.ErrorCode == WsaConnReset)
            {
                Console.WriteLine("[server] ignoring UDP connection reset from disconnected client");
            }
        }
    }

    private void HandleHello(HelloMessage hello, IPEndPoint remoteEndPoint)
    {
        _pluginHost?.NotifyHelloReceived(new HelloReceivedEvent(hello.Name, remoteEndPoint.ToString(), hello.Version));
        if (hello.Version != ProtocolVersion.Current)
        {
            Console.WriteLine($"[server] rejected client {remoteEndPoint} due to protocol mismatch client={hello.Version} server={ProtocolVersion.Current}");
            SendMessage(remoteEndPoint, new ConnectionDeniedMessage("Protocol mismatch."));
            return;
        }

        var existingClient = FindClient(_clientsBySlot, remoteEndPoint);
        if (existingClient is not null)
        {
            existingClient.Name = hello.Name;
            existingClient.LastSeen = _clock.Elapsed;
            _sessionManager.ApplyClientName(existingClient.Slot, hello.Name);
            var existingMapMetadata = GetCurrentMapMetadata();
            SendMessage(remoteEndPoint, new WelcomeMessage(
                _serverName,
                ProtocolVersion.Current,
                _config.TicksPerSecond,
                _world.Level.Name,
                existingClient.Slot,
                existingMapMetadata.IsCustomMap,
                existingMapMetadata.MapDownloadUrl,
                existingMapMetadata.MapContentHash));
            if (_passwordRequired && !existingClient.IsAuthorized)
            {
                SendMessage(remoteEndPoint, new PasswordRequestMessage());
                existingClient.LastPasswordRequestSentAt = _clock.Elapsed;
            }
            Console.WriteLine($"[server] client refreshed {remoteEndPoint} slot={existingClient.Slot} name=\"{hello.Name}\" version={hello.Version}");
            return;
        }

        if (GetHelloRateLimitReason(remoteEndPoint) is { } rateLimitReason)
        {
            Console.WriteLine($"[server] rejected client {remoteEndPoint}; {rateLimitReason}");
            SendMessage(remoteEndPoint, new ConnectionDeniedMessage(rateLimitReason));
            return;
        }

        var assignedSlot = FindAvailableSlot(_clientsBySlot, _maxTotalClients, _maxSpectatorClients, _maxPlayableClients);
        if (assignedSlot == 0)
        {
            Console.WriteLine($"[server] rejected client {remoteEndPoint}; server is full");
            SendMessage(remoteEndPoint, new ConnectionDeniedMessage("Server is full."));
            return;
        }

        var client = new ClientSession(assignedSlot, remoteEndPoint, hello.Name, _clock.Elapsed)
        {
            IsAuthorized = !_passwordRequired,
        };
        _clientsBySlot[assignedSlot] = client;
        _sessionManager.ApplyClientName(assignedSlot, hello.Name);
        if (SimulationWorld.IsPlayableNetworkPlayerSlot(assignedSlot))
        {
            _world.TryPrepareNetworkPlayerJoin(assignedSlot);
        }

        var mapMetadata = GetCurrentMapMetadata();
        var welcome = new WelcomeMessage(
            _serverName,
            ProtocolVersion.Current,
            _config.TicksPerSecond,
            _world.Level.Name,
            assignedSlot,
            mapMetadata.IsCustomMap,
            mapMetadata.MapDownloadUrl,
            mapMetadata.MapContentHash);
        SendMessage(remoteEndPoint, welcome);
        if (_passwordRequired && !client.IsAuthorized)
        {
            SendMessage(remoteEndPoint, new PasswordRequestMessage());
            client.LastPasswordRequestSentAt = _clock.Elapsed;
        }

        _helloRateLimiter.Reset(remoteEndPoint);
        _passwordRateLimiter.Reset(remoteEndPoint);
        Console.WriteLine($"[server] client connected {remoteEndPoint} slot={assignedSlot} name=\"{hello.Name}\" version={hello.Version}");
        LogServerEvent(
            "client_connected",
            ("slot", assignedSlot),
            ("player_name", hello.Name),
            ("endpoint", remoteEndPoint.ToString()),
            ("is_authorized", client.IsAuthorized),
            ("is_spectator", IsSpectatorSlot(assignedSlot)),
            ("version", hello.Version));
        _pluginHost?.NotifyClientConnected(new ClientConnectedEvent(
            assignedSlot,
            hello.Name,
            remoteEndPoint.ToString(),
            client.IsAuthorized,
            IsSpectatorSlot(assignedSlot)));
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

    private void AddConsoleStatusSummary(List<string> lines)
    {
        var activePlayableCount = _world.EnumerateActiveNetworkPlayers().Count();
        var spectatorCount = _clientsBySlot.Keys.Count(IsSpectatorSlot);
        var uptime = FormatDuration(_clock.Elapsed);
        var lobbyValue = _useLobbyServer ? $"enabled {_lobbyHost}:{_lobbyPort}" : "disabled";
        var passwordValue = _passwordRequired ? "required" : "off";
        lines.Add(
            $"[server] status | name={_serverName} | port={_port} | tickrate={_config.TicksPerSecond} | players={activePlayableCount}/{_maxPlayableClients} | spectators={spectatorCount} | map={_world.Level.Name} area={_world.Level.MapAreaIndex}/{_world.Level.MapAreaCount} | mode={_world.MatchRules.Mode} | phase={_world.MatchState.Phase} | score={_world.RedCaps}-{_world.BlueCaps} | lobby={lobbyValue} | password={passwordValue} | uptime={uptime}");
    }

    private void AddConsoleRulesSummary(List<string> lines)
    {
        var autoBalanceValue = _autoBalanceEnabled ? "enabled" : "disabled";
        var respawnSeconds = _respawnSecondsOverride ?? 5;
        lines.Add(
            $"[server] rules | timeLimit={_world.MatchRules.TimeLimitMinutes} | capLimit={_world.MatchRules.CapLimit} | respawn={respawnSeconds} | autoBalance={autoBalanceValue}");
    }

    private void AddConsoleLobbySummary(List<string> lines)
    {
        var lobbyValue = _useLobbyServer ? "enabled" : "disabled";
        lines.Add($"[server] lobby | enabled={lobbyValue} | host={_lobbyHost} | port={_lobbyPort}");
    }

    private void AddConsoleMapSummary(List<string> lines)
    {
        var winner = _world.MatchState.WinnerTeam?.ToString() ?? "none";
        lines.Add(
            $"[server] map | name={_world.Level.Name} | area={_world.Level.MapAreaIndex}/{_world.Level.MapAreaCount} | mode={_world.MatchRules.Mode} | phase={_world.MatchState.Phase} | winner={winner} | imported={_world.Level.ImportedFromSource}");
        lines.Add($"[server] world | bounds={_world.Bounds.Width}x{_world.Bounds.Height}");
    }

    private void AddConsoleRotationSummary(List<string> lines)
    {
        var rotation = _mapRotationManager.MapRotation;
        var currentIndex = rotation.Count == 0 ? 0 : Math.Clamp(_mapRotationManager.CurrentRotationIndex + 1, 1, rotation.Count);
        var source = string.IsNullOrWhiteSpace(_mapRotationFile) ? "stock" : _mapRotationFile!;
        var entries = rotation.Count == 0 ? _world.Level.Name : string.Join(", ", rotation);
        lines.Add($"[server] rotation | source={source} | current={currentIndex}/{Math.Max(1, rotation.Count)} | entries={entries}");
    }

    private void AddConsolePlayersSummary(List<string> lines)
    {
        if (_clientsBySlot.Count == 0)
        {
            lines.Add("[server] players | count=0");
            return;
        }

        lines.Add($"[server] players | count={_clientsBySlot.Count}");
        foreach (var client in _clientsBySlot.Values.OrderBy(entry => entry.Slot))
        {
            var role = "Spectator";
            if (!IsSpectatorSlot(client.Slot))
            {
                role = _world.TryGetNetworkPlayer(client.Slot, out var player)
                    ? player.Team.ToString()
                    : "Unassigned";
            }

            var connectedFor = FormatDuration(_clock.Elapsed - client.ConnectedAt);
            var authorized = client.IsAuthorized ? "yes" : "pending";
            lines.Add(
                $"[server] player | slot={client.Slot} | name={client.Name} | role={role} | authorized={authorized} | endpoint={client.EndPoint} | connected={connectedFor}");
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return duration.TotalHours >= 1d
            ? duration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
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

    private void BroadcastChat(ClientSession client, string text)
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

        var team = SimulationWorld.IsPlayableNetworkPlayerSlot(client.Slot) && _world.TryGetNetworkPlayer(client.Slot, out var player)
            ? (byte)player.Team
            : (byte)0;
        var chatEvent = new ChatReceivedEvent(
            client.Slot,
            client.Name,
            sanitized,
            team == 0 ? null : (PlayerTeam)team);
        if (_pluginHost?.TryHandleChatMessage(chatEvent) == true)
        {
            return;
        }

        LogServerEvent(
            "chat_received",
            ("slot", client.Slot),
            ("player_name", client.Name),
            ("team", team == 0 ? null : ((PlayerTeam)team).ToString()),
            ("text", sanitized));
        _pluginHost?.NotifyChatReceived(chatEvent);
        var relay = new ChatRelayMessage(team, client.Name, sanitized);
        foreach (var session in _clientsBySlot.Values)
        {
            SendMessage(session.EndPoint, relay);
        }

        Console.WriteLine($"[chat] {client.Name}: {sanitized}");
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
        _pluginCommandRegistry = new OpenGarrison.Server.PluginCommandRegistry();
        RegisterBuiltInCommands();
        _pluginHost = new OpenGarrison.Server.PluginHost(
            _pluginCommandRegistry,
            new OpenGarrison.Server.ServerReadOnlyStateView(() => _serverName, () => _world, () => _clientsBySlot),
            new OpenGarrison.Server.ServerAdminOperations(
                Console.WriteLine,
                SendMessage,
                () => _clientsBySlot,
                () => _sessionManager,
                () => _world,
                () => _mapRotationManager,
                () => _snapshotBroadcaster,
                evt => _pluginHost?.NotifyMapChanging(evt),
                evt => _pluginHost?.NotifyMapChanged(evt)),
            Path.Combine(RuntimePaths.ApplicationRoot, "Plugins"),
            Path.Combine(RuntimePaths.ConfigDirectory, "plugins"),
            Path.Combine(RuntimePaths.ApplicationRoot, "Maps"),
            Console.WriteLine);
    }

    private OpenGarrisonServerCommandContext CreateCommandContext()
    {
        return new OpenGarrisonServerCommandContext(
            new OpenGarrison.Server.ServerReadOnlyStateView(() => _serverName, () => _world, () => _clientsBySlot),
            new OpenGarrison.Server.ServerAdminOperations(
                Console.WriteLine,
                SendMessage,
                () => _clientsBySlot,
                () => _sessionManager,
                () => _world,
                () => _mapRotationManager,
                () => _snapshotBroadcaster,
                evt => _pluginHost?.NotifyMapChanging(evt),
                evt => _pluginHost?.NotifyMapChanged(evt)));
    }

    private void RegisterBuiltInCommands()
    {
        _pluginCommandRegistry.RegisterBuiltIn(
            "help",
            "Show server and plugin commands.",
            "help",
            (context, _, _) => Task.FromResult<IReadOnlyList<string>>(BuildHelpLines()),
            "?");
        _pluginCommandRegistry.RegisterBuiltIn(
            "status",
            "Show overall server status.",
            "status",
            (context, _, _) =>
            {
                var lines = new List<string>();
                AddConsoleStatusSummary(lines);
                AddConsoleRulesSummary(lines);
                AddConsoleLobbySummary(lines);
                AddConsoleMapSummary(lines);
                return Task.FromResult<IReadOnlyList<string>>(lines);
            },
            "info");
        _pluginCommandRegistry.RegisterBuiltIn(
            "players",
            "List connected players.",
            "players",
            (context, _, _) =>
            {
                var lines = new List<string>();
                AddConsolePlayersSummary(lines);
                return Task.FromResult<IReadOnlyList<string>>(lines);
            },
            "who");
        _pluginCommandRegistry.RegisterBuiltIn(
            "map",
            "Show current map details.",
            "map",
            (context, _, _) =>
            {
                var lines = new List<string>();
                AddConsoleMapSummary(lines);
                return Task.FromResult<IReadOnlyList<string>>(lines);
            },
            "level");
        _pluginCommandRegistry.RegisterBuiltIn(
            "rules",
            "Show match rules.",
            "rules",
            (context, _, _) =>
            {
                var lines = new List<string>();
                AddConsoleRulesSummary(lines);
                return Task.FromResult<IReadOnlyList<string>>(lines);
            });
        _pluginCommandRegistry.RegisterBuiltIn(
            "caplimit",
            "Set the capture limit.",
            "caplimit <1-255>",
            (context, arguments, _) =>
            {
                if (!TryParseBoundedInt(arguments, min: 1, max: 255, out var capLimit))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: caplimit <1-255>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TrySetCapLimit(capLimit)
                    ? [$"[server] cap limit set to {capLimit}."]
                    : ["[server] unable to set cap limit."]);
            },
            "cap");
        _pluginCommandRegistry.RegisterBuiltIn(
            "lobby",
            "Show lobby registration state.",
            "lobby",
            (context, _, _) =>
            {
                var lines = new List<string>();
                AddConsoleLobbySummary(lines);
                return Task.FromResult<IReadOnlyList<string>>(lines);
            });
        _pluginCommandRegistry.RegisterBuiltIn(
            "rotation",
            "Show the active map rotation.",
            "rotation",
            (context, _, _) =>
            {
                var lines = new List<string>();
                AddConsoleRotationSummary(lines);
                return Task.FromResult<IReadOnlyList<string>>(lines);
            },
            "maps");
        _pluginCommandRegistry.RegisterBuiltIn(
            "plugins",
            "List loaded server plugins.",
            "plugins",
            (context, _, _) => Task.FromResult<IReadOnlyList<string>>(BuildPluginLines()));
        _pluginCommandRegistry.RegisterBuiltIn(
            "say",
            "Broadcast a system chat message.",
            "say <text>",
            (context, arguments, _) =>
            {
                if (string.IsNullOrWhiteSpace(arguments))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: say <text>"]);
                }

                context.AdminOperations.BroadcastSystemMessage(arguments);
                return Task.FromResult<IReadOnlyList<string>>(["[server] system message sent."]);
            });
        _pluginCommandRegistry.RegisterBuiltIn(
            "kick",
            "Disconnect a player slot.",
            "kick <slot> [reason]",
            (context, arguments, _) =>
            {
                if (!TryParseSlotAndOptionalArgument(arguments, out var slot, out var reason))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: kick <slot> [reason]"]);
                }

                var finalReason = string.IsNullOrWhiteSpace(reason) ? "Kicked by admin." : reason;
                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TryDisconnect(slot, finalReason)
                    ? [$"[server] kicked slot {slot}."]
                    : [$"[server] no client at slot {slot}."]);
            });
        _pluginCommandRegistry.RegisterBuiltIn(
            "spectate",
            "Move a player to spectator.",
            "spectate <slot>",
            (context, arguments, _) =>
            {
                if (!TryParseSlot(arguments, out var slot))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: spectate <slot>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TryMoveToSpectator(slot)
                    ? [$"[server] moved slot {slot} to spectator."]
                    : [$"[server] unable to move slot {slot} to spectator."]);
            });
        _pluginCommandRegistry.RegisterBuiltIn(
            "team",
            "Set a player's team.",
            "team <slot> <red|blue>",
            (context, arguments, _) =>
            {
                if (!TryParseSlotAndRequiredArgument(arguments, out var slot, out var teamText)
                    || !TryParseTeam(teamText, out var team))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: team <slot> <red|blue>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TrySetTeam(slot, team)
                    ? [$"[server] slot {slot} set to {team}."]
                    : [$"[server] unable to set team for slot {slot}."]);
            });
        _pluginCommandRegistry.RegisterBuiltIn(
            "class",
            "Set a player's class.",
            "class <slot> <scout|engineer|pyro|soldier|demoman|heavy|sniper|medic|spy|quote>",
            (context, arguments, _) =>
            {
                if (!TryParseSlotAndRequiredArgument(arguments, out var slot, out var classText)
                    || !Enum.TryParse<PlayerClass>(classText, ignoreCase: true, out var playerClass))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: class <slot> <class>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TrySetClass(slot, playerClass)
                    ? [$"[server] slot {slot} class set to {playerClass}."]
                    : [$"[server] unable to set class for slot {slot}."]);
            });
        _pluginCommandRegistry.RegisterBuiltIn(
            "kill",
            "Kill a playable slot's current character.",
            "kill <slot>",
            (context, arguments, _) =>
            {
                if (!TryParseSlot(arguments, out var slot))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: kill <slot>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TryForceKill(slot)
                    ? [$"[server] killed slot {slot}."]
                    : [$"[server] unable to kill slot {slot}."]);
            });
        _pluginCommandRegistry.RegisterBuiltIn(
            "changemap",
            "Change to another map.",
            "changemap <mapName> [area]",
            (context, arguments, _) =>
            {
                if (!TryParseMapChangeArguments(arguments, out var levelName, out var areaIndex))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: changemap <mapName> [area]"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TryChangeMap(levelName, areaIndex, preservePlayerStats: false)
                    ? [$"[server] changed map to {levelName} area {areaIndex}."]
                    : [$"[server] unable to change map to {levelName} area {areaIndex}."]);
            },
            "mapchange");
    }

    private static bool TryParseBoundedInt(string text, int min, int max, out int value)
    {
        value = 0;
        var trimmed = text.Trim();
        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value >= min
            && value <= max;
    }

    private static bool TryParseSlot(string text, out byte slot)
    {
        slot = 0;
        var trimmed = text.Trim();
        return byte.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out slot) && slot > 0;
    }

    private static bool TryParseSlotAndOptionalArgument(string arguments, out byte slot, out string argument)
    {
        slot = 0;
        argument = string.Empty;
        var parts = arguments.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !TryParseSlot(parts[0], out slot))
        {
            return false;
        }

        argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        return true;
    }

    private static bool TryParseSlotAndRequiredArgument(string arguments, out byte slot, out string argument)
    {
        slot = 0;
        argument = string.Empty;
        var parts = arguments.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !TryParseSlot(parts[0], out slot))
        {
            return false;
        }

        argument = parts[1].Trim();
        return argument.Length > 0;
    }

    private static bool TryParseTeam(string text, out PlayerTeam team)
    {
        team = default;
        var normalized = text.Trim();
        if (normalized.Equals("red", StringComparison.OrdinalIgnoreCase))
        {
            team = PlayerTeam.Red;
            return true;
        }

        if (normalized.Equals("blue", StringComparison.OrdinalIgnoreCase) || normalized.Equals("blu", StringComparison.OrdinalIgnoreCase))
        {
            team = PlayerTeam.Blue;
            return true;
        }

        return false;
    }

    private static bool TryParseMapChangeArguments(string arguments, out string levelName, out int areaIndex)
    {
        levelName = string.Empty;
        areaIndex = 1;
        var parts = arguments.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        levelName = parts[0].Trim();
        if (levelName.Length == 0)
        {
            return false;
        }

        if (parts.Length < 2)
        {
            return true;
        }

        return int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out areaIndex) && areaIndex >= 1;
    }

    private IReadOnlyList<string> BuildHelpLines()
    {
        var lines = new List<string>
        {
            "[server] commands:",
        };
        foreach (var command in _pluginCommandRegistry.GetPrimaryCommands())
        {
            var ownerSuffix = command.IsBuiltIn ? string.Empty : $" [plugin:{command.OwnerId}]";
            lines.Add($"[server]   {command.Name} - {command.Description} ({command.Usage}){ownerSuffix}");
        }

        lines.Add("[server] shutdown is handled directly by the host console/admin pipe.");
        return lines;
    }

    private IReadOnlyList<string> BuildPluginLines()
    {
        var pluginIds = _pluginHost?.LoadedPluginIds ?? [];
        if (pluginIds.Count == 0)
        {
            return ["[server] plugins | count=0"];
        }

        return
        [
            $"[server] plugins | count={pluginIds.Count}",
            .. pluginIds.Select(pluginId => $"[server] plugin | id={pluginId}")
        ];
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
