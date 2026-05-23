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
        ServerBotManager botManager,
        ServerDemoRecorder demoRecorder,
        Action<MapChangeTransition> applyMapTransition,
        Action<ServerTransportPeer, IProtocolMessage> sendMessage,
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
            () => botManager,
            broadcastSystemChatMessage: message => demoRecorder.RecordBroadcastMessage(message),
            applyMapTransition: applyMapTransition,
            banService: banService,
            demoRecordingStatusGetter: demoRecorder.GetStatusLine,
            demoRecordingStarter: requestedPath =>
            {
                var success = demoRecorder.TryStart(requestedPath, out var status, out var error);
                return new OpenGarrisonServerDemoRecordingResult(success, status, error);
            },
            demoRecordingStopper: () =>
            {
                var success = demoRecorder.TryStop(out var status, out var error);
                return new OpenGarrisonServerDemoRecordingResult(success, status, error);
            },
            beforeTeamChange: (slot, team) => pluginHost?.BeforeTeamChange(slot, team) ?? OpenGarrisonServerDecisionResult.Continue,
            beforeClassChange: (slot, playerClass) => pluginHost?.BeforeClassChange(slot, playerClass) ?? OpenGarrisonServerDecisionResult.Continue,
            beforeLoadoutChange: (slot, loadoutId) => pluginHost?.BeforeLoadoutChange(slot, loadoutId) ?? OpenGarrisonServerDecisionResult.Continue,
            beforeMapChange: (levelName, areaIndex, preservePlayerStats) => pluginHost?.BeforeMapChange(levelName, areaIndex, preservePlayerStats) ?? OpenGarrisonServerDecisionResult.Continue);
        var consoleSummaryBuilder = new ServerConsoleSummaryBuilder(
            config,
            port,
            () => serverName,
            () => world,
            () => clientsBySlot,
            () => snapshotBroadcaster.Metrics,
            uptimeGetter,
            maxPlayableClients,
            useLobbyServer,
            lobbyHost,
            lobbyPort,
            passwordRequired,
            autoBalanceEnabledGetter,
            respawnSecondsGetter,
            () => mapRotationManager,
            mapRotationFile,
            demoRecorder.GetStatusLine);
        new ServerBuiltInCommandRegistrar(
            commandRegistry,
            consoleSummaryBuilder.AddStatusSummary,
            consoleSummaryBuilder.AddRulesSummary,
            consoleSummaryBuilder.AddLobbySummary,
            consoleSummaryBuilder.AddMapSummary,
            consoleSummaryBuilder.AddRotationSummary,
            consoleSummaryBuilder.AddPlayersSummary,
            demoRecorder.GetStatusLine,
            requestedPath =>
            {
                var success = demoRecorder.TryStart(requestedPath, out var status, out var error);
                return (success, status, error);
            },
            () =>
            {
                var success = demoRecorder.TryStop(out var status, out var error);
                return (success, status, error);
            },
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
