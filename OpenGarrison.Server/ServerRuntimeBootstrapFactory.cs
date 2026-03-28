using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using OpenGarrison.Core;

namespace OpenGarrison.Server;

internal static class ServerRuntimeBootstrapFactory
{
    public static ServerRuntimeBootstrap Create(
        SimulationConfig config,
        UdpClient udp,
        int port,
        byte[] protocolUuidBytes,
        bool useLobbyServer,
        string lobbyHost,
        int lobbyPort,
        int lobbyHeartbeatSeconds,
        int lobbyResolveSeconds,
        string? requestedMap,
        string? mapRotationFile,
        IReadOnlyList<string> stockMapRotation,
        int maxNewHelloAttemptsPerWindow,
        TimeSpan helloAttemptWindow,
        TimeSpan helloCooldown,
        int maxPasswordFailuresPerWindow,
        TimeSpan passwordFailureWindow,
        TimeSpan passwordCooldown,
        int maxPlayableClients,
        int maxTotalClients,
        int maxSpectatorClients,
        int autoBalanceDelaySeconds,
        int autoBalanceNewPlayerGraceSeconds,
        bool autoBalanceEnabled,
        int? timeLimitMinutesOverride,
        int? capLimitOverride,
        int? respawnSecondsOverride,
        string? serverPassword,
        bool passwordRequired,
        double clientTimeoutSeconds,
        double passwordTimeoutSeconds,
        double passwordRetrySeconds,
        ulong transientEventReplayTicks,
        Func<PluginHost?> pluginHostGetter,
        string serverName,
        Action<string, (string Key, object? Value)[]> writeEvent,
        Action<string> log)
    {
        LobbyServerRegistrar? lobbyRegistrar = null;
        if (useLobbyServer)
        {
            lobbyRegistrar = new LobbyServerRegistrar(
                udp,
                lobbyHost,
                lobbyPort,
                protocolUuidBytes,
                port,
                TimeSpan.FromSeconds(lobbyHeartbeatSeconds),
                TimeSpan.FromSeconds(lobbyResolveSeconds));
        }

        var world = new SimulationWorld(config);
        if (timeLimitMinutesOverride.HasValue || capLimitOverride.HasValue || respawnSecondsOverride.HasValue)
        {
            world.ConfigureMatchDefaults(
                timeLimitMinutes: timeLimitMinutesOverride,
                capLimit: capLimitOverride,
                respawnSeconds: respawnSecondsOverride);
        }

        world.AutoRestartOnMapChange = false;
        var mapRotationManager = new MapRotationManager(world, requestedMap, mapRotationFile, stockMapRotation, log);
        world.DespawnEnemyDummy();
        world.TryPrepareNetworkPlayerJoin(SimulationWorld.LocalPlayerSlot);

        var simulator = new FixedStepSimulator(world);
        var clock = Stopwatch.StartNew();
        var previous = clock.Elapsed;
        var clientsBySlot = new Dictionary<byte, ClientSession>();
        var connectionRateLimiter = new ServerConnectionRateLimiter(
            maxNewHelloAttemptsPerWindow,
            helloAttemptWindow,
            helloCooldown,
            maxPasswordFailuresPerWindow,
            passwordFailureWindow,
            passwordCooldown,
            () => clock.Elapsed);
        var mapMetadataResolver = new ServerMapMetadataResolver(world);
        var eventReporter = new ServerRuntimeEventReporter(world, pluginHostGetter, writeEvent, mapMetadataResolver);
        eventReporter.ResetObservedGameplayState();
        var outboundMessaging = new ServerOutboundMessaging(
            udp,
            serverName,
            world,
            clientsBySlot,
            maxPlayableClients,
            pluginHostGetter,
            eventReporter.WriteEvent,
            log);
        var sessionManager = new ServerSessionManager(
            world,
            clientsBySlot,
            maxPlayableClients,
            maxTotalClients,
            maxSpectatorClients,
            () => clock.Elapsed,
            serverPassword,
            passwordRequired,
            clientTimeoutSeconds,
            passwordTimeoutSeconds,
            passwordRetrySeconds,
            connectionRateLimiter.GetPasswordRateLimitReason,
            connectionRateLimiter.RecordPasswordFailure,
            connectionRateLimiter.ClearPasswordFailures,
            outboundMessaging.SendMessage,
            log,
            eventReporter.NotifyClientDisconnected,
            eventReporter.NotifyPasswordAccepted,
            eventReporter.NotifyPlayerTeamChanged,
            eventReporter.NotifyPlayerClassChanged);
        var autoBalancer = new AutoBalancer(
            world,
            config,
            clientsBySlot,
            autoBalanceDelaySeconds,
            autoBalanceNewPlayerGraceSeconds,
            passwordRequired,
            outboundMessaging.SendMessage,
            log);
        var snapshotBroadcaster = new SnapshotBroadcaster(
            world,
            config,
            clientsBySlot,
            transientEventReplayTicks,
            mapMetadataResolver,
            outboundMessaging.SendSnapshotPayload);

        return new ServerRuntimeBootstrap(
            lobbyRegistrar,
            world,
            simulator,
            clock,
            previous,
            clientsBySlot,
            connectionRateLimiter,
            eventReporter,
            outboundMessaging,
            sessionManager,
            autoBalancer,
            snapshotBroadcaster,
            mapRotationManager);
    }
}
