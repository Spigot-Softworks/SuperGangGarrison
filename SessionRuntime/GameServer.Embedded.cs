using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Server;
using OpenGarrison.Server.Plugins;
using OpenGarrison.SessionRuntime;

partial class GameServer
{
    internal void ConfigureEmbeddedAdmissions(Func<ServerTransportPeer, ManagedRoomRuntime.Participant?> admission)
        => _embeddedAdmission = admission;
    internal void RemoveEmbeddedPeer(ServerTransportPeer peer)
    {
        if (_clientsBySlot is null) return;
        foreach (var client in _clientsBySlot.Values.Where(client => client.Peer.Id == peer.Id).ToArray())
            _sessionManager.RemoveClient(client.Slot, "room connection closed");
    }
    internal SimulationWorld EmbeddedWorld => _world;
    internal IReadOnlyList<string> EmbeddedJukeboxCommand(string command) => _serverAudio?.Execute(command) ?? [];
    private PersistentServerEventLog? _embeddedEventLog;

    internal GameServer(EmbeddedSessionOptions options) : this(
        config: new SimulationConfig { TicksPerSecond = options.TickRate, EnableLocalDummies = false },
        port: 0, serverName: options.Name, serverPassword: null, rconPassword: null,
        useLobbyServer: false, registryUrl: null, registryToken: null, publicHost: null,
        buildVersion: ApplicationBuildInfo.BuildVersion, releaseChannel: "stable", compatibilityKey: "",
        lobbyHost: "", lobbyPort: 0, protocolUuidString: "71eb5496-492b-b186-4770-06ccb30d3f8f",
        lobbyHeartbeatSeconds: 30, lobbyResolveSeconds: 600,
        requestedMap: ClassicStockMapCatalog.GetPreferredLevelName(options.Map, options.PreferClassicMaps), mapRotationFile: null, eventLogPath: PersistentServerEventLog.DisabledPath,
        stockMapRotation: OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(new OpenGarrisonHostSettings().StockMapRotation)
            .Select(map => ClassicStockMapCatalog.GetPreferredLevelName(map, options.PreferClassicMaps)).ToArray(),
        mapRotationShuffleEnabled: false, mapRotationAdvanceMode: default,
        mapRotationRounds: 1, mapRotationMinutes: 15,
        maxPlayableClients: options.MaximumPlayers, maxTotalClients: options.MaximumPlayers, maxSpectatorClients: 0,
        autoBalanceDelaySeconds: 10, autoBalanceNewPlayerGraceSeconds: 60, autoBalanceEnabled: false,
        switchTeamsAfterRoundEnd: false, teamShuffleAfterWins: 0, secondaryAbilitiesEnabled: options.SpecialAbilities,
        randomSpreadEnabled: true, competitiveReadyUpEnabled: false, competitiveSetupSeconds: 0,
        timeLimitMinutesOverride: options.TimeLimitMinutes, capLimitOverride: options.CaptureLimit,
        respawnSecondsOverride: options.RespawnSeconds, botAutofillEnabled: false, botAutofillMinPlayers: 0, botAutofillPerTeam: 0,
        webSocketPort: 0, webSocketCertificatePath: null, webSocketCertificatePassword: null,
        publicWebSocketUrl: null, quicPort: 0, publicQuicUrl: null, relayHostUrl: null,
        clientTimeoutSeconds: 30, passwordTimeoutSeconds: 30, passwordRetrySeconds: 2,
        transientEventReplayTicks: (ulong)options.TickRate * 2,
        persistentGameplayOwnershipEnabled: false, persistentGameplayOwnershipIdentityMode: PersistentGameplayOwnershipIdentityMode.Disabled,
        persistentGameplayOwnershipFile: "", hostGameplayDefaults: new OpenGarrisonHostSettings
        { AutoBalanceEnabled = false, HlxEnabled = false, SecondaryAbilitiesEnabled = options.SpecialAbilities },
        gameplayVariant: options.LastToDie ? GameplayVariantKind.LastToDie : GameplayVariantKind.Standard,
        lastToDieDifficulty: options.Difficulty, lastToDieSeed: options.Seed,
        serverManagementConfiguration: new ServerManagementConfiguration())
    { }

    internal void StartEmbedded(IServerMessageTransport transport, EmbeddedSessionOptions options)
    {
        _messageTransport = transport;
        _embeddedEventLog = new PersistentServerEventLog(PersistentServerEventLog.DisabledPath);
        ApplyRuntimeBootstrap(CreateRuntimeBootstrap(_embeddedEventLog));
        // The browser has a virtual filesystem, but no separate server configuration.
        _serverAudio = new ServerAudioService(new ServerAudioSettings(), _clientsBySlot, _world,
            _outboundMessaging.SendMessage, () => _clock.Elapsed.TotalSeconds, Console.WriteLine,
            requireVoiceChannelJoin: options.LastToDie);
        ApplyHostGameplayDefaults();
        _world.ConfigureMatchDefaults(timeLimitMinutes: options.TimeLimitMinutes,
            capLimit: options.CaptureLimit, respawnSeconds: options.RespawnSeconds);
        InitializeGameplayVariantRuntime();
        InitializePluginRuntime();
        InitializeIncomingPacketPump();
        if (!options.LastToDie)
        {
            var map = ClassicStockMapCatalog.GetPreferredLevelName(options.Map, options.PreferClassicMaps);
            if ((!string.Equals(map, _world.Level.Name, StringComparison.OrdinalIgnoreCase)
                || options.MapArea != _world.Level.MapAreaIndex)
                && !_world.TryLoadLevel(map, options.MapArea, preservePlayerStats: false))
                throw new InvalidOperationException("The selected Practice map area could not load.");
            _world.ConfigureMatchDefaults(timeLimitMinutes: options.TimeLimitMinutes,
                capLimit: options.CaptureLimit, respawnSeconds: options.RespawnSeconds);
            var nextSlot = (byte)(options.MaximumPlayers + 1);
            foreach (var (team, count) in new[] { (PlayerTeam.Red, options.RedBots), (PlayerTeam.Blue, options.BlueBots) })
                for (var index = 0; index < count; index++)
                    if (!_botManager.TryAddBot(nextSlot++, team, (PlayerClass)(index % 9 + 1), ""))
                        throw new InvalidOperationException("The Practice bots could not spawn.");
        }
    }

    internal void AdvanceEmbedded(double elapsedSeconds)
    {
        ProcessPendingConsoleCommands();
        _connectionRateLimiter.Prune();
        PumpIncomingPackets();
        _sessionManager.PruneTimedOutClients();
        _sessionManager.RefreshPasswordRequests();
        SynchronizeLastToDieClients();
        _serverAudio?.Tick();
        _scheduler.RunDueTasks();
        var now = _clock.Elapsed;
        var ticks = ShouldPauseLastToDieAuthoritativeSimulation() ? 0
            : AdvanceAuthoritativeSimulation(elapsedSeconds, now, Math.Max(1, _config.TicksPerSecond / 10));
        if (ticks > 0)
        {
            _outboundMessaging.BroadcastProtocol64State(unchecked((uint)_world.Frame));
            _eventReporter.PublishGameplayEvents(_snapshotBroadcaster.LastCapturedTransientEvents);
        }
    }

    internal void StopEmbedded()
    {
        _serverAudio?.Dispose();
        _pluginHost?.ShutdownPlugins();
        _embeddedEventLog?.Dispose();
    }
}
