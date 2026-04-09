using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server.Plugins;

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
        Func<bool> autoBalanceEnabledGetter,
        Func<int> respawnSecondsGetter,
        IOpenGarrisonServerCvarRegistry cvarRegistry,
        IOpenGarrisonServerScheduler scheduler,
        Func<byte, OpenGarrisonServerAdminIdentity> adminIdentityResolver,
        GameplayOwnershipService? gameplayOwnershipService,
        MapRotationManager mapRotationManager,
        string? mapRotationFile,
        ServerSessionManager sessionManager,
        SnapshotBroadcaster snapshotBroadcaster,
        Action<MapChangeTransition> applyMapTransition,
        Action<IPEndPoint, IProtocolMessage> sendMessage,
        Action<byte, string, string, string, string, PluginMessagePayloadFormat, ushort> sendPluginMessageToClient,
        Action<string, string, string, string, PluginMessagePayloadFormat, ushort> broadcastPluginMessage,
        Action<string> log,
        string pluginsDirectory,
        string pluginConfigRoot,
        string mapsDirectory,
        ServerBanService? banService = null)
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
            () => gameplayOwnershipService,
            () => mapRotationManager,
            () => snapshotBroadcaster,
            applyMapTransition,
            banService);
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
            autoBalanceEnabledGetter,
            respawnSecondsGetter,
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
            () => pluginHost?.LoadedPluginIds ?? Array.Empty<string>(),
            cvarRegistry,
            scheduler)
            .RegisterAll();

        pluginHost = new PluginHost(
            () => world,
            commandRegistry,
            serverState,
            adminOperations,
            cvarRegistry,
            scheduler,
            adminIdentityResolver,
            sendPluginMessageToClient,
            broadcastPluginMessage,
            pluginsDirectory,
            pluginConfigRoot,
            mapsDirectory,
            log);

        return new ServerPluginRuntime(commandRegistry, pluginHost, serverState, adminOperations);
    }
}
