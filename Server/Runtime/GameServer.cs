using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The server owns disposables for the duration of Run() and shuts them down in the startup lifecycle finally block.")]
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
    private readonly Uri? _registryUrl;
    private readonly string? _registryToken;
    private readonly string? _publicHost;
    private readonly string _lobbyHost;
    private readonly int _lobbyPort;
    private readonly string _protocolUuidString;
    private readonly int _lobbyHeartbeatSeconds;
    private readonly int _lobbyResolveSeconds;
    private readonly string? _requestedMap;
    private readonly string? _mapRotationFile;
    private readonly string _eventLogPath;
    private readonly OpenGarrisonHostSettings _hostGameplayDefaults;
    private readonly IReadOnlyList<string> _stockMapRotation;
    private bool _mapRotationShuffleEnabled;
    private readonly int _maxPlayableClients;
    private readonly int _maxTotalClients;
    private readonly int _maxSpectatorClients;
    private readonly int _autoBalanceDelaySeconds;
    private readonly int _autoBalanceNewPlayerGraceSeconds;
    private bool _autoBalanceEnabled;
    private bool _botAutofillEnabled;
    private int _botAutofillMinPlayers;
    private int _botAutofillPerTeam;
    private int _botAutofillLastTick;
    private bool _secondaryAbilitiesEnabled;
    private bool _randomSpreadEnabled;
    private bool _sniperAimIndicatorEnabled = true;
    private bool _competitiveReadyUpEnabled;
    private int _competitiveSetupSeconds;
    private readonly HashSet<byte> _competitiveReadyButtonDownSlots = new();
    private readonly int? _timeLimitMinutesOverride;
    private readonly int? _capLimitOverride;
    private readonly int? _respawnSecondsOverride;
    private readonly int _webSocketPort;
    private readonly string? _webSocketCertificatePath;
    private readonly string? _webSocketCertificatePassword;
    private readonly string? _publicWebSocketUrl;
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
    private int _nextClientUserId = 1;

    private UdpClient _udp = null!;
    private OpenGarrison.Server.IServerMessageTransport _messageTransport = null!;
    private OpenGarrison.Server.WebSocketServerHost? _webSocketHost;
    private bool _mapDownloadEndpointAvailable;
    private LobbyServerRegistrar? _lobbyRegistrar;
    private HttpServerRegistryHeartbeat? _httpRegistryHeartbeat;
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
    private ServerDemoRecorder _demoRecorder = null!;
    private ServerAdminSessionManager _adminSessionManager = null!;
    private ServerScheduler _scheduler = null!;
    private IOpenGarrisonServerCvarRegistry _cvarRegistry = null!;
    private ServerAdminChatRouter _adminChatRouter = null!;
    private ServerBanService _banService = null!;
    private ServerBotManager _botManager = null!;

    public GameServer(
        SimulationConfig config,
        int port,
        string serverName,
        string? serverPassword,
        string? rconPassword,
        bool useLobbyServer,
        Uri? registryUrl,
        string? registryToken,
        string? publicHost,
        string lobbyHost,
        int lobbyPort,
        string protocolUuidString,
        int lobbyHeartbeatSeconds,
        int lobbyResolveSeconds,
        string? requestedMap,
        string? mapRotationFile,
        string eventLogPath,
        IReadOnlyList<string> stockMapRotation,
        bool mapRotationShuffleEnabled,
        int maxPlayableClients,
        int maxTotalClients,
        int maxSpectatorClients,
        int autoBalanceDelaySeconds,
        int autoBalanceNewPlayerGraceSeconds,
        bool autoBalanceEnabled,
        bool secondaryAbilitiesEnabled,
        bool randomSpreadEnabled,
        bool competitiveReadyUpEnabled,
        int competitiveSetupSeconds,
        int? timeLimitMinutesOverride,
        int? capLimitOverride,
        int? respawnSecondsOverride,
        bool botAutofillEnabled,
        int botAutofillMinPlayers,
        int botAutofillPerTeam,
        int webSocketPort,
        string? webSocketCertificatePath,
        string? webSocketCertificatePassword,
        string? publicWebSocketUrl,
        double clientTimeoutSeconds,
        double passwordTimeoutSeconds,
        double passwordRetrySeconds,
        ulong transientEventReplayTicks,
        bool persistentGameplayOwnershipEnabled,
        PersistentGameplayOwnershipIdentityMode persistentGameplayOwnershipIdentityMode,
        string persistentGameplayOwnershipFile,
        OpenGarrisonHostSettings hostGameplayDefaults)
    {
        _config = config;
        _port = port;
        _serverName = serverName;
        _serverPassword = serverPassword;
        _rconPassword = rconPassword;
        _useLobbyServer = useLobbyServer;
        _registryUrl = registryUrl;
        _registryToken = registryToken;
        _publicHost = publicHost;
        _lobbyHost = lobbyHost;
        _lobbyPort = lobbyPort;
        _protocolUuidString = protocolUuidString;
        _lobbyHeartbeatSeconds = lobbyHeartbeatSeconds;
        _lobbyResolveSeconds = lobbyResolveSeconds;
        _requestedMap = requestedMap;
        _mapRotationFile = mapRotationFile;
        _eventLogPath = eventLogPath;
        _hostGameplayDefaults = hostGameplayDefaults ?? new OpenGarrisonHostSettings();
        _stockMapRotation = stockMapRotation;
        _mapRotationShuffleEnabled = mapRotationShuffleEnabled;
        _maxPlayableClients = maxPlayableClients;
        _maxTotalClients = maxTotalClients;
        _maxSpectatorClients = maxSpectatorClients;
        _autoBalanceDelaySeconds = autoBalanceDelaySeconds;
        _autoBalanceNewPlayerGraceSeconds = autoBalanceNewPlayerGraceSeconds;
        _autoBalanceEnabled = autoBalanceEnabled;
        _secondaryAbilitiesEnabled = secondaryAbilitiesEnabled;
        _randomSpreadEnabled = randomSpreadEnabled;
        _competitiveReadyUpEnabled = competitiveReadyUpEnabled;
        _competitiveSetupSeconds = Math.Clamp(competitiveSetupSeconds, 0, 120);
        _timeLimitMinutesOverride = timeLimitMinutesOverride;
        _capLimitOverride = capLimitOverride;
        _respawnSecondsOverride = respawnSecondsOverride;
        _botAutofillEnabled = botAutofillEnabled;
        _botAutofillMinPlayers = Math.Clamp(botAutofillMinPlayers, 0, SimulationWorld.MaxPlayableNetworkPlayers);
        _botAutofillPerTeam = Math.Clamp(botAutofillPerTeam, 0, SimulationWorld.MaxPlayableNetworkPlayers / 2);
        _webSocketPort = webSocketPort;
        _webSocketCertificatePath = webSocketCertificatePath;
        _webSocketCertificatePassword = webSocketCertificatePassword;
        _publicWebSocketUrl = publicWebSocketUrl;
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

    private int AllocateClientUserId()
    {
        var userId = _nextClientUserId;
        _nextClientUserId += 1;
        return userId;
    }
}
