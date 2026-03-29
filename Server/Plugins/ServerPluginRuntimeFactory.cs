using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal static class ServerPluginRuntimeFactory
{
    public static ServerPluginRuntime Create(
        SimulationConfig config,
        int port,
        string serverName,
        Dictionary<byte, ClientSession> clientsBySlot,
        SimulationWorld world,
        Func<TimeSpan> uptimeGetter,
        int maxPlayableClients,
        bool useLobbyServer,
        string lobbyHost,
        int lobbyPort,
        bool passwordRequired,
        bool autoBalanceEnabled,
        int? respawnSecondsOverride,
        MapRotationManager mapRotationManager,
        string? mapRotationFile,
        ServerSessionManager sessionManager,
        SnapshotBroadcaster snapshotBroadcaster,
        Action<MapChangeTransition> applyMapTransition,
        Action<IPEndPoint, IProtocolMessage> sendMessage,
        Action<string> log,
        string pluginsDirectory,
        string pluginConfigRoot,
        string mapsDirectory)
    {
        var commandRegistry = new PluginCommandRegistry();
        var serverState = new ServerReadOnlyStateView(() => serverName, () => world, () => clientsBySlot);
        PluginHost? pluginHost = null;
        var adminOperations = new ServerAdminOperations(
            log,
            sendMessage,
            () => clientsBySlot,
            () => sessionManager,
            () => world,
            () => mapRotationManager,
            () => snapshotBroadcaster,
            applyMapTransition);
        var consoleSummaryBuilder = new ServerConsoleSummaryBuilder(
            config,
            port,
            () => serverName,
            () => world,
            () => clientsBySlot,
            uptimeGetter,
            maxPlayableClients,
            useLobbyServer,
            lobbyHost,
            lobbyPort,
            passwordRequired,
            autoBalanceEnabled,
            respawnSecondsOverride,
            () => mapRotationManager,
            mapRotationFile);
        new ServerBuiltInCommandRegistrar(
            commandRegistry,
            consoleSummaryBuilder.AddStatusSummary,
            consoleSummaryBuilder.AddRulesSummary,
            consoleSummaryBuilder.AddLobbySummary,
            consoleSummaryBuilder.AddMapSummary,
            consoleSummaryBuilder.AddRotationSummary,
            consoleSummaryBuilder.AddPlayersSummary,
            () => pluginHost?.LoadedPluginIds ?? Array.Empty<string>())
            .RegisterAll();

        pluginHost = new PluginHost(
            commandRegistry,
            serverState,
            adminOperations,
            pluginsDirectory,
            pluginConfigRoot,
            mapsDirectory,
            log);

        return new ServerPluginRuntime(commandRegistry, pluginHost, serverState, adminOperations);
    }
}
