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
using OpenGarrison.Server;
using OpenGarrison.Server.Plugins;
using static ServerHelpers;

sealed partial class GameServer
{
    private sealed record PendingConsoleCommand(
        string Command,
        bool EchoToConsole,
        OpenGarrisonServerAdminIdentity Identity,
        OpenGarrisonServerCommandSource Source,
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
    private string? _rconPassword;
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
    private bool _autoBalanceEnabled;
    private readonly int? _timeLimitMinutesOverride;
    private readonly int? _capLimitOverride;
    private readonly int? _respawnSecondsOverride;
    private readonly double _clientTimeoutSeconds;
    private readonly double _passwordTimeoutSeconds;
    private readonly double _passwordRetrySeconds;
    private readonly ulong _transientEventReplayTicks;
    private readonly bool _persistentGameplayOwnershipEnabled;
    private readonly PersistentGameplayOwnershipIdentityMode _persistentGameplayOwnershipIdentityMode;
    private readonly string _persistentGameplayOwnershipFile;
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
    private GameplayOwnershipService? _gameplayOwnershipService;
    private AutoBalancer _autoBalancer = null!;
    private SnapshotBroadcaster _snapshotBroadcaster = null!;
    private MapRotationManager _mapRotationManager = null!;
    private OpenGarrison.Server.ServerConnectionRateLimiter _connectionRateLimiter = null!;
    private ServerAdminSessionManager _adminSessionManager = null!;
    private ServerScheduler _scheduler = null!;
    private IOpenGarrisonServerCvarRegistry _cvarRegistry = null!;
    private ServerAdminChatRouter _adminChatRouter = null!;

    public GameServer(
        SimulationConfig config,
        int port,
        string serverName,
        string? serverPassword,
        string? rconPassword,
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
        ulong transientEventReplayTicks,
        bool persistentGameplayOwnershipEnabled,
        PersistentGameplayOwnershipIdentityMode persistentGameplayOwnershipIdentityMode,
        string persistentGameplayOwnershipFile)
    {
        _config = config;
        _port = port;
        _serverName = serverName;
        _serverPassword = serverPassword;
        _rconPassword = rconPassword;
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
        _persistentGameplayOwnershipEnabled = persistentGameplayOwnershipEnabled;
        _persistentGameplayOwnershipIdentityMode = persistentGameplayOwnershipEnabled
            ? persistentGameplayOwnershipIdentityMode
            : PersistentGameplayOwnershipIdentityMode.Disabled;
        _persistentGameplayOwnershipFile = persistentGameplayOwnershipFile;
        _passwordRequired = !string.IsNullOrWhiteSpace(serverPassword);
        _protocolUuidBytes = ParseProtocolUuid(protocolUuidString);
    }

    public void EnqueueConsoleCommand(string command)
    {
        if (!string.IsNullOrWhiteSpace(command))
        {
            _pendingConsoleCommands.Enqueue(new PendingConsoleCommand(
                command.Trim(),
                EchoToConsole: true,
                CreateConsoleIdentity(),
                OpenGarrisonServerCommandSource.Console,
                Completion: null));
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
        _pendingConsoleCommands.Enqueue(new PendingConsoleCommand(
            command.Trim(),
            echoToConsole,
            CreateAdminPipeIdentity(),
            OpenGarrisonServerCommandSource.AdminPipe,
            tcs));
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
            var lines = BuildConsoleCommandResponse(request.Command, request.Identity, request.Source);
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

    private List<string> BuildConsoleCommandResponse(
        string command,
        OpenGarrisonServerAdminIdentity identity,
        OpenGarrisonServerCommandSource source)
    {
        var normalized = command.Trim();
        if (normalized.Length == 0)
        {
            return [];
        }

        if (_pluginCommandRegistry.TryExecute(normalized, CreateCommandContext(identity, source), CancellationToken.None, out var responseLines))
        {
            return responseLines.ToList();
        }

        return [$"[server] unknown command \"{normalized}\". Type help for commands."];
    }

    private OpenGarrisonServerCommandContext CreateCommandContext(
        OpenGarrisonServerAdminIdentity identity,
        OpenGarrisonServerCommandSource source)
    {
        return new OpenGarrisonServerCommandContext(
            _serverState,
            _adminOperations,
            _cvarRegistry,
            _scheduler,
            identity,
            source);
    }

    private OpenGarrisonServerAdminIdentity CreateConsoleIdentity()
    {
        return _adminSessionManager is null
            ? new OpenGarrisonServerAdminIdentity("Console", OpenGarrisonServerAdminAuthority.HostConsole, OpenGarrisonServerAdminPermissions.FullAccess)
            : _adminSessionManager.ConsoleIdentity;
    }

    private OpenGarrisonServerAdminIdentity CreateAdminPipeIdentity()
    {
        return _adminSessionManager is null
            ? new OpenGarrisonServerAdminIdentity("AdminPipe", OpenGarrisonServerAdminAuthority.AdminPipe, OpenGarrisonServerAdminPermissions.FullAccess)
            : _adminSessionManager.AdminPipeIdentity;
    }
}
