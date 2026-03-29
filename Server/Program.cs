using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenGarrison.Core;
using OpenGarrison.Server;

const string protocolUuidString = "71eb5496-492b-b186-4770-06ccb30d3f8f";
const int lobbyHeartbeatSeconds = 30;
const int lobbyResolveSeconds = 600;
const double clientTimeoutSeconds = 5;
const double passwordTimeoutSeconds = 30;
const double passwordRetrySeconds = 2;
const ulong transientEventReplayTicks = 6;
const int autoBalanceDelaySeconds = 10;
const int autoBalanceNewPlayerGraceSeconds = 60;

var launchOptions = ServerLaunchOptions.Load(args);
launchOptions.Settings.Save(launchOptions.ResolvedConfigPath);
var sessionPath = HostedServerSessionInfo.GetDefaultPath();
var pipeName = $"opengarrison-hosted-server-{Environment.ProcessId}";

var config = new SimulationConfig
{
    TicksPerSecond = launchOptions.TickRate,
    EnableLocalDummies = false,
};

var server = new GameServer(
    config,
    launchOptions.Port,
    launchOptions.ServerName,
    launchOptions.ServerPassword,
    launchOptions.UseLobbyServer,
    launchOptions.LobbyHost,
    launchOptions.LobbyPort,
    protocolUuidString,
    lobbyHeartbeatSeconds,
    lobbyResolveSeconds,
    launchOptions.RequestedMap,
    launchOptions.MapRotationFile,
    launchOptions.EventLogPath,
    launchOptions.StockMapRotation,
    launchOptions.MaxPlayableClients,
    launchOptions.MaxTotalClients,
    launchOptions.MaxSpectatorClients,
    autoBalanceDelaySeconds,
    autoBalanceNewPlayerGraceSeconds,
    launchOptions.AutoBalanceEnabled,
    launchOptions.TimeLimitMinutesOverride,
    launchOptions.CapLimitOverride,
    launchOptions.RespawnSecondsOverride,
    clientTimeoutSeconds,
    passwordTimeoutSeconds,
    passwordRetrySeconds,
    transientEventReplayTicks);

using var shutdownCts = new CancellationTokenSource();
var sessionInfo = new HostedServerSessionInfo
{
    ProcessId = Environment.ProcessId,
    Port = launchOptions.Port,
    ServerName = launchOptions.ServerName,
    PipeName = pipeName,
    ConfigPath = launchOptions.ResolvedConfigPath,
    WorkingDirectory = Directory.GetCurrentDirectory(),
    LaunchMode = Environment.GetEnvironmentVariable("OPENGARRISON_LAUNCH_MODE") ?? "direct",
    StartedAtUtc = DateTimeOffset.UtcNow,
};
sessionInfo.Save(sessionPath);
ConsoleCancelEventHandler cancelHandler = (_, e) =>
{
    e.Cancel = true;
    if (!shutdownCts.IsCancellationRequested)
    {
        Console.WriteLine("[server] shutdown requested via Ctrl+C.");
        shutdownCts.Cancel();
    }
};
Console.CancelKeyPress += cancelHandler;
var shutdownCommandTask = Task.Run(() => ListenForShutdownCommands(server, shutdownCts));
using var adminPipeHost = new HostedServerAdminPipeHost(
    pipeName,
    server.ExecuteAdminCommandAsync,
    () =>
    {
        if (!shutdownCts.IsCancellationRequested)
        {
            Console.WriteLine("[server] shutdown requested.");
            shutdownCts.Cancel();
        }
    },
    shutdownCts.Token);

try
{
    server.Run(shutdownCts.Token);
}
finally
{
    shutdownCts.Cancel();
    HostedServerSessionInfo.Delete(sessionPath);
    Console.CancelKeyPress -= cancelHandler;
    try
    {
        shutdownCommandTask.Wait(250);
    }
    catch (AggregateException)
    {
    }
}

return;

static void ListenForShutdownCommands(GameServer server, CancellationTokenSource shutdownCts)
{
    try
    {
        while (!shutdownCts.IsCancellationRequested)
        {
            var line = Console.ReadLine();
            if (!ServerConsoleCommandProcessor.TryProcessLine(
                    line,
                    Console.IsInputRedirected,
                    shutdownCts,
                    Console.WriteLine,
                    server.EnqueueConsoleCommand))
            {
                break;
            }
        }
    }
    catch (InvalidOperationException)
    {
    }
}
