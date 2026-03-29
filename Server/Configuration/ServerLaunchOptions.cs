using System;
using System.IO;
using OpenGarrison.Core;

sealed class ServerLaunchOptions
{
    private const string DefaultLobbyHost = "OpenGarrison.game-host.org";
    private const int DefaultLobbyPort = 29942;

    private ServerLaunchOptions()
    {
    }

    public string ResolvedConfigPath { get; private init; } = string.Empty;
    public ServerSettings Settings { get; private init; } = new();
    public int Port { get; private init; }
    public string ServerName { get; private init; } = "My Server";
    public string? ServerPassword { get; private init; }
    public bool UseLobbyServer { get; private init; }
    public string LobbyHost { get; private init; } = DefaultLobbyHost;
    public int LobbyPort { get; private init; } = DefaultLobbyPort;
    public string? RequestedMap { get; private init; }
    public string? MapRotationFile { get; private init; }
    public string EventLogPath { get; private init; } = string.Empty;
    public IReadOnlyList<string> StockMapRotation { get; private init; } = Array.Empty<string>();
    public int TickRate { get; private init; } = SimulationConfig.DefaultTicksPerSecond;
    public int MaxPlayableClients { get; private init; }
    public int MaxTotalClients { get; private init; }
    public int MaxSpectatorClients { get; private init; }
    public bool AutoBalanceEnabled { get; private init; }
    public int? TimeLimitMinutesOverride { get; private init; }
    public int? CapLimitOverride { get; private init; }
    public int? RespawnSecondsOverride { get; private init; }

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
        var useLobbyServer = settings.UseLobbyServer;
        var lobbyHost = string.IsNullOrWhiteSpace(settings.LobbyHost) ? DefaultLobbyHost : settings.LobbyHost;
        var lobbyPort = settings.LobbyPort > 0 ? settings.LobbyPort : DefaultLobbyPort;
        string? requestedMap = string.IsNullOrWhiteSpace(settings.RequestedMap) ? null : settings.RequestedMap;
        string? mapRotationFile = string.IsNullOrWhiteSpace(settings.MapRotationFile) ? null : settings.MapRotationFile;
        var eventLogPath = PersistentServerEventLog.GetDefaultPath(now);
        var stockMapRotation = OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(settings.HostDefaults.StockMapRotation);
        var tickRate = SimulationConfig.NormalizeTicksPerSecond(settings.TickRate);
        int? timeLimitMinutesOverride = settings.TimeLimitMinutes > 0 ? Math.Clamp(settings.TimeLimitMinutes, 1, 255) : null;
        int? capLimitOverride = settings.CapLimit > 0 ? Math.Clamp(settings.CapLimit, 1, 255) : null;
        int? respawnSecondsOverride = settings.RespawnSeconds >= 0 ? Math.Clamp(settings.RespawnSeconds, 0, 255) : null;
        var autoBalanceEnabled = settings.AutoBalanceEnabled;

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
                lobbyHost = args[index + 1];
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

            if (index == 0 && int.TryParse(arg, out var firstPort))
            {
                port = firstPort;
            }
        }

        return new ServerLaunchOptions
        {
            ResolvedConfigPath = resolvedConfigPath,
            Settings = settings,
            Port = port,
            ServerName = serverName,
            ServerPassword = serverPassword,
            UseLobbyServer = useLobbyServer,
            LobbyHost = lobbyHost,
            LobbyPort = lobbyPort,
            RequestedMap = requestedMap,
            MapRotationFile = mapRotationFile,
            EventLogPath = eventLogPath,
            StockMapRotation = stockMapRotation,
            TickRate = tickRate,
            MaxPlayableClients = maxPlayableClients,
            MaxTotalClients = maxTotalClients,
            MaxSpectatorClients = maxSpectatorClients,
            AutoBalanceEnabled = autoBalanceEnabled,
            TimeLimitMinutesOverride = timeLimitMinutesOverride,
            CapLimitOverride = capLimitOverride,
            RespawnSecondsOverride = respawnSecondsOverride,
        };
    }
}
