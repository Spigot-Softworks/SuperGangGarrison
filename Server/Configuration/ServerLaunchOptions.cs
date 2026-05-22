using System;
using System.IO;
using OpenGarrison.Core;

sealed class ServerLaunchOptions
{
    private const string DefaultLobbyHost = OpenGarrisonPreferencesDocument.DefaultLobbyHost;
    private const int DefaultLobbyPort = OpenGarrisonPreferencesDocument.DefaultLobbyPort;
    private const string LegacyLobbyHost = "OpenGarrison.game-host.org";

    private ServerLaunchOptions()
    {
    }

    public string ResolvedConfigPath { get; private init; } = string.Empty;
    public ServerSettings Settings { get; private init; } = new();
    public int Port { get; private init; }
    public string ServerName { get; private init; } = "My Server";
    public string? ServerPassword { get; private init; }
    public string? RconPassword { get; private init; }
    public bool UseLobbyServer { get; private init; }
    public bool UseLegacyLobbyServer { get; private init; }
    public string LobbyHost { get; private init; } = DefaultLobbyHost;
    public int LobbyPort { get; private init; } = DefaultLobbyPort;
    public Uri? RegistryUrl { get; private init; }
    public string? RegistryToken { get; private init; }
    public string? PublicHost { get; private init; }
    public string? RequestedMap { get; private init; }
    public string? MapRotationFile { get; private init; }
    public string EventLogPath { get; private init; } = string.Empty;
    public IReadOnlyList<string> StockMapRotation { get; private init; } = Array.Empty<string>();
    public int TickRate { get; private init; } = SimulationConfig.DefaultTicksPerSecond;
    public int MaxPlayableClients { get; private init; }
    public int MaxTotalClients { get; private init; }
    public int MaxSpectatorClients { get; private init; }
    public bool AutoBalanceEnabled { get; private init; }
    public bool SecondaryAbilitiesEnabled { get; private init; }
    public bool RandomSpreadEnabled { get; private init; }
    public int? TimeLimitMinutesOverride { get; private init; }
    public int? CapLimitOverride { get; private init; }
    public int? RespawnSecondsOverride { get; private init; }
    public int WebSocketPort { get; private init; }
    public string? WebSocketCertificatePath { get; private init; }
    public string? WebSocketCertificatePassword { get; private init; }
    public string? PublicWebSocketUrl { get; private init; }

    public static ServerLaunchOptions Load(string[] args)
    {
        return Load(args, ServerSettings.Load, DateTimeOffset.Now);
    }

    internal static ServerLaunchOptions Load(string[] args, Func<string?, ServerSettings> loadSettings)
    {
        return Load(args, loadSettings, DateTimeOffset.Now);
    }

    internal static ServerLaunchOptions Load(string[] args, Func<string?, ServerSettings> loadSettings, DateTimeOffset now)
    {
        string? configPath = null;
        for (var index = 0; index < args.Length; index += 1)
        {
            if ((string.Equals(args[index], "--config", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(args[index], "-c", StringComparison.OrdinalIgnoreCase))
                && index + 1 < args.Length)
            {
                configPath = args[index + 1];
                break;
            }
        }

        var resolvedConfigPath = string.IsNullOrWhiteSpace(configPath)
            ? RuntimePaths.GetConfigPath(ServerSettings.DefaultFileName)
            : configPath;
        var settings = loadSettings(resolvedConfigPath);

        var maxPlayableClients = Math.Clamp(settings.MaxPlayableClients, 1, SimulationWorld.MaxPlayableNetworkPlayers);
        var maxTotalClients = Math.Clamp(settings.MaxTotalClients, maxPlayableClients, SimulationWorld.MaxPlayableNetworkPlayers);
        var maxSpectatorClients = Math.Clamp(settings.MaxSpectatorClients, 0, SimulationWorld.MaxPlayableNetworkPlayers);
        var port = settings.Port;
        var serverName = settings.ServerName;
        string? serverPassword = string.IsNullOrWhiteSpace(settings.Password) ? null : settings.Password;
        string? rconPassword = string.IsNullOrWhiteSpace(settings.RconPassword) ? null : settings.RconPassword;
        var useLobbyServer = settings.UseLobbyServer;
        var lobbyHost = NormalizeLobbyHost(settings.LobbyHost);
        var lobbyPort = settings.LobbyPort > 0 ? settings.LobbyPort : DefaultLobbyPort;
        string? registryUrlOverride = null;
        string? registryToken = Environment.GetEnvironmentVariable("OPENGARRISON_REGISTRY_TOKEN");
        string? publicHost = Environment.GetEnvironmentVariable("OPENGARRISON_PUBLIC_HOST");
        string? requestedMap = string.IsNullOrWhiteSpace(settings.RequestedMap) ? null : settings.RequestedMap;
        string? mapRotationFile = string.IsNullOrWhiteSpace(settings.MapRotationFile) ? null : settings.MapRotationFile;
        var eventLogPath = PersistentServerEventLog.GetDefaultPath(now);
        var stockMapRotation = OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(settings.HostDefaults.StockMapRotation);
        var tickRate = SimulationConfig.NormalizeTicksPerSecond(settings.TickRate);
        int? timeLimitMinutesOverride = settings.TimeLimitMinutes > 0 ? Math.Clamp(settings.TimeLimitMinutes, 1, 255) : null;
        int? capLimitOverride = settings.CapLimit > 0 ? Math.Clamp(settings.CapLimit, 1, 255) : null;
        int? respawnSecondsOverride = settings.RespawnSeconds >= 0 ? Math.Clamp(settings.RespawnSeconds, 0, 255) : null;
        var autoBalanceEnabled = settings.AutoBalanceEnabled;
        var secondaryAbilitiesEnabled = settings.SecondaryAbilitiesEnabled;
        var randomSpreadEnabled = settings.RandomSpreadEnabled;
        var webSocketPort = port;
        var webSocketPortExplicitlySet = false;
        string? webSocketCertificatePath = null;
        string? webSocketCertificatePassword = null;
        string? publicWebSocketUrl = Environment.GetEnvironmentVariable("OPENGARRISON_PUBLIC_WEBSOCKET_URL");

        for (var index = 0; index < args.Length; index += 1)
        {
            var arg = args[index];
            if (string.Equals(arg, "--config", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-c", StringComparison.OrdinalIgnoreCase))
            {
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--map", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                requestedMap = args[index + 1];
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--port", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                if (int.TryParse(args[index + 1], out var parsedPort))
                {
                    port = parsedPort;
                    if (!webSocketPortExplicitlySet)
                    {
                        webSocketPort = port;
                    }
                }
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--name", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                serverName = args[index + 1];
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--password", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                serverPassword = args[index + 1];
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--rcon-password", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                rconPassword = args[index + 1];
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--max-players", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Length
                && int.TryParse(args[index + 1], out var parsedMaxPlayers))
            {
                maxPlayableClients = Math.Clamp(parsedMaxPlayers, 1, SimulationWorld.MaxPlayableNetworkPlayers);
                maxTotalClients = maxPlayableClients;
                maxSpectatorClients = maxPlayableClients;
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--slots", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Length
                && int.TryParse(args[index + 1], out var parsedSlots))
            {
                maxPlayableClients = Math.Clamp(parsedSlots, 1, SimulationWorld.MaxPlayableNetworkPlayers);
                maxTotalClients = maxPlayableClients;
                maxSpectatorClients = maxPlayableClients;
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--map-rotation", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                mapRotationFile = args[index + 1];
                index += 1;
                continue;
            }

            if ((string.Equals(arg, "--event-log", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "--event-log-file", StringComparison.OrdinalIgnoreCase))
                && index + 1 < args.Length)
            {
                eventLogPath = Path.GetFullPath(args[index + 1]);
                index += 1;
                continue;
            }

            if ((string.Equals(arg, "--tickrate", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "--tick-rate", StringComparison.OrdinalIgnoreCase))
                && index + 1 < args.Length)
            {
                if (int.TryParse(args[index + 1], out var parsedTickRate))
                {
                    tickRate = SimulationConfig.NormalizeTicksPerSecond(parsedTickRate);
                }

                index += 1;
                continue;
            }

            if (string.Equals(arg, "--time-limit", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Length
                && int.TryParse(args[index + 1], out var parsedTimeLimit))
            {
                timeLimitMinutesOverride = Math.Clamp(parsedTimeLimit, 1, 255);
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--cap-limit", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Length
                && int.TryParse(args[index + 1], out var parsedCapLimit))
            {
                capLimitOverride = Math.Clamp(parsedCapLimit, 1, 255);
                index += 1;
                continue;
            }

            if ((string.Equals(arg, "--respawn", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "--respawn-seconds", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "--respawn-time", StringComparison.OrdinalIgnoreCase))
                && index + 1 < args.Length
                && int.TryParse(args[index + 1], out var parsedRespawnSeconds))
            {
                respawnSecondsOverride = Math.Clamp(parsedRespawnSeconds, 0, 255);
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--auto-balance", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--autobalance", StringComparison.OrdinalIgnoreCase))
            {
                autoBalanceEnabled = true;
                continue;
            }

            if (string.Equals(arg, "--no-auto-balance", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--no-autobalance", StringComparison.OrdinalIgnoreCase))
            {
                autoBalanceEnabled = false;
                continue;
            }

            if (string.Equals(arg, "--secondary-abilities", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--secondaryabilities", StringComparison.OrdinalIgnoreCase))
            {
                secondaryAbilitiesEnabled = true;
                continue;
            }

            if (string.Equals(arg, "--no-secondary-abilities", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--no-secondaryabilities", StringComparison.OrdinalIgnoreCase))
            {
                secondaryAbilitiesEnabled = false;
                continue;
            }

            if (string.Equals(arg, "--random-spread", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--randomspread", StringComparison.OrdinalIgnoreCase))
            {
                randomSpreadEnabled = true;
                continue;
            }

            if (string.Equals(arg, "--no-random-spread", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--no-randomspread", StringComparison.OrdinalIgnoreCase))
            {
                randomSpreadEnabled = false;
                continue;
            }

            if (string.Equals(arg, "--lobby", StringComparison.OrdinalIgnoreCase))
            {
                useLobbyServer = true;
                continue;
            }

            if (string.Equals(arg, "--no-lobby", StringComparison.OrdinalIgnoreCase))
            {
                useLobbyServer = false;
                continue;
            }

            if (string.Equals(arg, "--lobby-host", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                lobbyHost = NormalizeLobbyHost(args[index + 1]);
                useLobbyServer = true;
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--lobby-port", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                if (int.TryParse(args[index + 1], out var parsedLobbyPort) && parsedLobbyPort > 0 && parsedLobbyPort <= 65535)
                {
                    lobbyPort = parsedLobbyPort;
                    useLobbyServer = true;
                }
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--registry-url", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                registryUrlOverride = args[index + 1];
                useLobbyServer = true;
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--registry-token", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                registryToken = args[index + 1];
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--public-host", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                publicHost = args[index + 1];
                index += 1;
                continue;
            }

            if (string.Equals(arg, "--no-websocket", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--disable-websocket", StringComparison.OrdinalIgnoreCase))
            {
                webSocketPort = 0;
                webSocketPortExplicitlySet = true;
                continue;
            }

            if ((string.Equals(arg, "--websocket-port", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "--ws-port", StringComparison.OrdinalIgnoreCase))
                && index + 1 < args.Length)
            {
                if (int.TryParse(args[index + 1], out var parsedWebSocketPort) && parsedWebSocketPort > 0 && parsedWebSocketPort <= 65535)
                {
                    webSocketPort = parsedWebSocketPort;
                    webSocketPortExplicitlySet = true;
                }

                index += 1;
                continue;
            }

            if ((string.Equals(arg, "--websocket-cert", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "--websocket-certificate", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "--ws-cert", StringComparison.OrdinalIgnoreCase))
                && index + 1 < args.Length)
            {
                webSocketCertificatePath = Path.GetFullPath(args[index + 1]);
                index += 1;
                continue;
            }

            if ((string.Equals(arg, "--websocket-cert-password", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "--websocket-certificate-password", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "--ws-cert-password", StringComparison.OrdinalIgnoreCase))
                && index + 1 < args.Length)
            {
                webSocketCertificatePassword = args[index + 1];
                index += 1;
                continue;
            }

            if ((string.Equals(arg, "--public-websocket-url", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "--websocket-url", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "--ws-url", StringComparison.OrdinalIgnoreCase))
                && index + 1 < args.Length)
            {
                publicWebSocketUrl = NormalizePublicWebSocketUrl(args[index + 1]);
                index += 1;
                continue;
            }

            if (index == 0 && int.TryParse(arg, out var firstPort))
            {
                port = firstPort;
                if (!webSocketPortExplicitlySet)
                {
                    webSocketPort = port;
                }
            }
        }

        var registryUrl = useLobbyServer
            ? ResolveRegistryUrl(registryUrlOverride, lobbyHost, lobbyPort)
            : null;
        var useLegacyLobbyServer = useLobbyServer
            && registryUrlOverride is null
            && !LooksLikeHttpRegistryEndpoint(lobbyHost);

        return new ServerLaunchOptions
        {
            ResolvedConfigPath = resolvedConfigPath,
            Settings = settings,
            Port = port,
            ServerName = serverName,
            ServerPassword = serverPassword,
            RconPassword = rconPassword,
            UseLobbyServer = useLobbyServer,
            UseLegacyLobbyServer = useLegacyLobbyServer,
            LobbyHost = lobbyHost,
            LobbyPort = lobbyPort,
            RegistryUrl = registryUrl,
            RegistryToken = string.IsNullOrWhiteSpace(registryToken) ? null : registryToken.Trim(),
            PublicHost = string.IsNullOrWhiteSpace(publicHost) ? null : publicHost.Trim(),
            RequestedMap = requestedMap,
            MapRotationFile = mapRotationFile,
            EventLogPath = eventLogPath,
            StockMapRotation = stockMapRotation,
            TickRate = tickRate,
            MaxPlayableClients = maxPlayableClients,
            MaxTotalClients = maxTotalClients,
            MaxSpectatorClients = maxSpectatorClients,
            AutoBalanceEnabled = autoBalanceEnabled,
            SecondaryAbilitiesEnabled = secondaryAbilitiesEnabled,
            RandomSpreadEnabled = randomSpreadEnabled,
            TimeLimitMinutesOverride = timeLimitMinutesOverride,
            CapLimitOverride = capLimitOverride,
            RespawnSecondsOverride = respawnSecondsOverride,
            WebSocketPort = webSocketPort,
            WebSocketCertificatePath = webSocketCertificatePath,
            WebSocketCertificatePassword = webSocketCertificatePassword,
            PublicWebSocketUrl = NormalizePublicWebSocketUrl(publicWebSocketUrl),
        };
    }

    private static string? NormalizePublicWebSocketUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != "ws" && uri.Scheme != "wss")
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return null;
        }

        return uri.ToString();
    }

    private static string NormalizeLobbyHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultLobbyHost;
        }

        var trimmed = value.Trim();
        return string.Equals(trimmed, LegacyLobbyHost, StringComparison.OrdinalIgnoreCase)
            ? DefaultLobbyHost
            : trimmed;
    }

    private static Uri? ResolveRegistryUrl(string? overrideUrl, string lobbyHost, int lobbyPort)
    {
        var candidate = string.IsNullOrWhiteSpace(overrideUrl) ? lobbyHost : overrideUrl.Trim();
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var absoluteUri)
            && (absoluteUri.Scheme == Uri.UriSchemeHttps || absoluteUri.Scheme == Uri.UriSchemeHttp))
        {
            return absoluteUri;
        }

        if (string.IsNullOrWhiteSpace(overrideUrl) && !LooksLikeHttpRegistryEndpoint(lobbyHost))
        {
            return null;
        }

        var builder = new UriBuilder(Uri.UriSchemeHttps, lobbyHost)
        {
            Path = "/API/og2servers.php",
        };
        if (lobbyPort is > 0 and not 443)
        {
            builder.Port = lobbyPort;
        }

        return builder.Uri;
    }

    private static bool LooksLikeHttpRegistryEndpoint(string lobbyHost)
    {
        if (Uri.TryCreate(lobbyHost, UriKind.Absolute, out var uri))
        {
            return uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp;
        }

        return lobbyHost.Contains('/', StringComparison.Ordinal);
    }
}
