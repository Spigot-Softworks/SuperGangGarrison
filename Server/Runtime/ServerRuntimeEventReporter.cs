using OpenGarrison.Core;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class ServerRuntimeEventReporter(
    SimulationWorld world,
    Func<PluginHost?> pluginHostGetter,
    Action<string, (string Key, object? Value)[]> writeEvent,
    ServerMapMetadataResolver mapMetadataResolver)
{
    private readonly Dictionary<int, bool> _lastObservedPlayerAliveById = new();
    private int _lastObservedRedCaps;
    private int _lastObservedBlueCaps;
    private MatchPhase _lastObservedMatchPhase;
    private int _lastObservedKillFeedCount;
    private readonly Dictionary<int, int> _lastObservedPlayerCapsById = new();
    private readonly Dictionary<int, bool> _lastObservedSentryBuiltById = new();
    private readonly HashSet<int> _observedSpawnedPlayerIds = new();
    private bool _lastObservedRedIntelAtBase;
    private bool _lastObservedRedIntelDropped;
    private float _lastObservedRedIntelX;
    private float _lastObservedRedIntelY;
    private bool _lastObservedBlueIntelAtBase;
    private bool _lastObservedBlueIntelDropped;
    private float _lastObservedBlueIntelX;
    private float _lastObservedBlueIntelY;
    private readonly Dictionary<int, (PlayerTeam? Team, PlayerTeam? CappingTeam, int Cappers, int ProgressTicks, bool IsLocked)> _lastObservedControlPointStates = new();
    private readonly Dictionary<PlayerTeam, bool> _lastObservedGeneratorDestroyedStates = new();

    public void ResetObservedGameplayState()
    {
        _lastObservedRedCaps = world.RedCaps;
        _lastObservedBlueCaps = world.BlueCaps;
        _lastObservedMatchPhase = world.MatchState.Phase;
        _lastObservedKillFeedCount = world.KillFeed.Count;
        _lastObservedPlayerAliveById.Clear();
        _lastObservedPlayerCapsById.Clear();
        _lastObservedSentryBuiltById.Clear();
        _observedSpawnedPlayerIds.Clear();
        _lastObservedControlPointStates.Clear();
        _lastObservedGeneratorDestroyedStates.Clear();
        foreach (var (_, player) in world.EnumerateActiveNetworkPlayers())
        {
            _lastObservedPlayerAliveById[player.Id] = player.IsAlive;
            _lastObservedPlayerCapsById[player.Id] = player.Caps;
            if (player.IsAlive)
            {
                _observedSpawnedPlayerIds.Add(player.Id);
            }
        }

        foreach (var sentry in world.Sentries)
        {
            _lastObservedSentryBuiltById[sentry.Id] = sentry.IsBuilt;
        }

        for (var index = 0; index < world.ControlPoints.Count; index += 1)
        {
            var point = world.ControlPoints[index];
            _lastObservedControlPointStates[point.Index] = (
                point.Team,
                point.CappingTeam,
                point.Cappers,
                (int)MathF.Round(point.CappingTicks),
                point.IsLocked);
        }

        for (var index = 0; index < world.Generators.Count; index += 1)
        {
            var generator = world.Generators[index];
            _lastObservedGeneratorDestroyedStates[generator.Team] = generator.IsDestroyed;
        }

        _lastObservedRedIntelAtBase = world.RedIntel.IsAtBase;
        _lastObservedRedIntelDropped = world.RedIntel.IsDropped;
        _lastObservedRedIntelX = world.RedIntel.X;
        _lastObservedRedIntelY = world.RedIntel.Y;
        _lastObservedBlueIntelAtBase = world.BlueIntel.IsAtBase;
        _lastObservedBlueIntelDropped = world.BlueIntel.IsDropped;
        _lastObservedBlueIntelX = world.BlueIntel.X;
        _lastObservedBlueIntelY = world.BlueIntel.Y;
    }

    public void WriteEvent(string eventName, params (string Key, object? Value)[] fields)
    {
        writeEvent(eventName, fields);
    }

    public void ApplyMapTransition(MapChangeTransition transition)
    {
        NotifyMapTransition(transition);
        ResetObservedGameplayState();
    }

    public void PublishGameplayEvents(SnapshotTransientEvents transientEvents)
    {
        PublishDamageEvents(transientEvents);
        PublishSpawnEvents();
        PublishBuildableEvents();
        PublishIntelEvents();
        PublishControlPointEvents();
        PublishPlayerCapEvents();

        if (world.RedCaps != _lastObservedRedCaps || world.BlueCaps != _lastObservedBlueCaps)
        {
            WriteEvent(
                "score_changed",
                ("frame", world.Frame),
                ("mode", world.MatchRules.Mode),
                ("red_caps", world.RedCaps),
                ("blue_caps", world.BlueCaps),
                ("previous_red_caps", _lastObservedRedCaps),
                ("previous_blue_caps", _lastObservedBlueCaps));
            pluginHostGetter()?.NotifyScoreChanged(new ScoreChangedEvent(world.RedCaps, world.BlueCaps, world.MatchRules.Mode));
            _lastObservedRedCaps = world.RedCaps;
            _lastObservedBlueCaps = world.BlueCaps;
        }

        var killFeed = world.KillFeed;
        if (killFeed.Count < _lastObservedKillFeedCount)
        {
            _lastObservedKillFeedCount = 0;
        }

        for (var index = _lastObservedKillFeedCount; index < killFeed.Count; index += 1)
        {
            var entry = killFeed[index];
            WriteEvent(
                "kill",
                ("frame", world.Frame),
                ("killer_name", entry.KillerName),
                ("killer_team", entry.KillerTeam),
                ("weapon_sprite_name", entry.WeaponSpriteName),
                ("victim_name", entry.VictimName),
                ("victim_team", entry.VictimTeam),
                ("message_text", entry.MessageText));
            pluginHostGetter()?.NotifyKillFeedEntry(new KillFeedEvent(
                entry.KillerName,
                entry.KillerTeam,
                entry.WeaponSpriteName,
                entry.VictimName,
                entry.VictimTeam,
                entry.MessageText));
        }

        _lastObservedKillFeedCount = killFeed.Count;

        if (_lastObservedMatchPhase != MatchPhase.Ended && world.MatchState.Phase == MatchPhase.Ended)
        {
            WriteEvent(
                "round_ended",
                ("frame", world.Frame),
                ("mode", world.MatchRules.Mode),
                ("winner_team", world.MatchState.WinnerTeam?.ToString()),
                ("red_caps", world.RedCaps),
                ("blue_caps", world.BlueCaps));
            pluginHostGetter()?.NotifyRoundEnded(new RoundEndedEvent(
                world.MatchRules.Mode,
                world.MatchState.WinnerTeam,
                world.RedCaps,
                world.BlueCaps,
                world.Frame));
        }

        _lastObservedMatchPhase = world.MatchState.Phase;
    }

    public void NotifyClientDisconnected(ClientSession client, string reason)
    {
        WriteEvent(
            "client_disconnected",
            ("slot", client.Slot),
            ("player_name", client.Name),
            ("endpoint", client.EndPoint.ToString()),
            ("reason", reason),
            ("was_authorized", client.IsAuthorized));
        pluginHostGetter()?.NotifyClientDisconnected(new ClientDisconnectedEvent(
            client.Slot,
            client.Name,
            client.EndPoint.ToString(),
            reason,
            client.IsAuthorized));
    }

    public void NotifyPasswordAccepted(ClientSession client)
    {
        WriteEvent(
            "password_accepted",
            ("slot", client.Slot),
            ("player_name", client.Name),
            ("endpoint", client.EndPoint.ToString()));
        pluginHostGetter()?.NotifyPasswordAccepted(new PasswordAcceptedEvent(
            client.Slot,
            client.Name,
            client.EndPoint.ToString()));
    }

    public void NotifyPlayerTeamChanged(ClientSession client, PlayerTeam team)
    {
        WriteEvent(
            "player_team_changed",
            ("slot", client.Slot),
            ("player_name", client.Name),
            ("team", team));
        pluginHostGetter()?.NotifyPlayerTeamChanged(new PlayerTeamChangedEvent(client.Slot, client.Name, team));
    }

    public void NotifyPlayerClassChanged(ClientSession client, PlayerClass playerClass)
    {
        WriteEvent(
            "player_class_changed",
            ("slot", client.Slot),
            ("player_name", client.Name),
            ("player_class", playerClass));
        pluginHostGetter()?.NotifyPlayerClassChanged(new PlayerClassChangedEvent(client.Slot, client.Name, playerClass));
    }

    public void NotifyMapTransition(MapChangeTransition transition)
    {
        WriteEvent(
            "map_changing",
            ("current_level_name", transition.CurrentLevelName),
            ("current_area_index", transition.CurrentAreaIndex),
            ("current_area_count", transition.CurrentAreaCount),
            ("next_level_name", transition.NextLevelName),
            ("next_area_index", transition.NextAreaIndex),
            ("preserve_player_stats", transition.PreservePlayerStats),
            ("winner_team", transition.WinnerTeam?.ToString()));
        pluginHostGetter()?.NotifyMapChanging(new MapChangingEvent(
            transition.CurrentLevelName,
            transition.CurrentAreaIndex,
            transition.CurrentAreaCount,
            transition.NextLevelName,
            transition.NextAreaIndex,
            transition.PreservePlayerStats,
            transition.WinnerTeam));
        WriteEvent(
            "map_changed",
            ("level_name", world.Level.Name),
            ("area_index", world.Level.MapAreaIndex),
            ("area_count", world.Level.MapAreaCount),
            ("mode", world.MatchRules.Mode));
        pluginHostGetter()?.NotifyMapChanged(new MapChangedEvent(
            world.Level.Name,
            world.Level.MapAreaIndex,
            world.Level.MapAreaCount,
            world.MatchRules.Mode));
    }

    public (bool IsCustomMap, string MapDownloadUrl, string MapContentHash) GetCurrentMapMetadata()
    {
        return mapMetadataResolver.GetCurrentMapMetadata();
    }

    private void PublishPlayerCapEvents()
    {
        var activePlayerIds = new HashSet<int>();
        foreach (var (slot, player) in world.EnumerateActiveNetworkPlayers())
        {
            activePlayerIds.Add(player.Id);
            var previousCaps = _lastObservedPlayerCapsById.GetValueOrDefault(player.Id, player.Caps);
            if (player.Caps > previousCaps)
            {
                for (var capsAwarded = previousCaps; capsAwarded < player.Caps; capsAwarded += 1)
                {
                    WriteEvent(
                        "player_cap_awarded",
                        ("frame", world.Frame),
                        ("slot", slot),
                        ("player_id", player.Id),
                        ("player_name", player.DisplayName),
                        ("team", player.Team),
                        ("caps_total", capsAwarded + 1),
                        ("mode", world.MatchRules.Mode),
                        ("red_caps", world.RedCaps),
                        ("blue_caps", world.BlueCaps));
                }
            }

            _lastObservedPlayerCapsById[player.Id] = player.Caps;
        }

        if (_lastObservedPlayerCapsById.Count == activePlayerIds.Count)
        {
            return;
        }

        var stalePlayerIds = _lastObservedPlayerCapsById.Keys.Where(playerId => !activePlayerIds.Contains(playerId)).ToArray();
        for (var index = 0; index < stalePlayerIds.Length; index += 1)
        {
            _lastObservedPlayerCapsById.Remove(stalePlayerIds[index]);
        }
    }

    private void PublishDamageEvents(SnapshotTransientEvents transientEvents)
    {
        var pluginHost = pluginHostGetter();
        for (var index = 0; index < transientEvents.DamageEvents.Length; index += 1)
        {
            var damageEvent = transientEvents.DamageEvents[index];
            var attacker = FindPlayerById(damageEvent.AttackerPlayerId);
            var assistant = FindPlayerById(damageEvent.AssistedByPlayerId);
            var victim = damageEvent.TargetKind == (byte)DamageTargetKind.Player
                ? FindPlayerById(damageEvent.TargetEntityId)
                : null;

            pluginHost?.NotifyDamage(new OpenGarrisonServerDamageEvent(
                world.Frame,
                damageEvent.Amount,
                (DamageTargetKind)damageEvent.TargetKind,
                damageEvent.TargetEntityId,
                damageEvent.WasFatal,
                damageEvent.AttackerPlayerId,
                attacker?.DisplayName ?? string.Empty,
                attacker?.Team,
                damageEvent.AssistedByPlayerId,
                assistant?.DisplayName ?? string.Empty,
                assistant?.Team,
                victim?.Id ?? -1,
                victim?.DisplayName ?? string.Empty,
                victim?.Team,
                damageEvent.X,
                damageEvent.Y,
                DamageEventFlags.None));

            if (!damageEvent.WasFatal || victim is null)
            {
                continue;
            }

            var latestKillFeedEntry = FindLatestKillFeedEntryForVictim(victim.Id);
            pluginHost?.NotifyDeath(new OpenGarrisonServerDeathEvent(
                world.Frame,
                victim.Id,
                victim.DisplayName,
                victim.Team,
                damageEvent.AttackerPlayerId,
                attacker?.DisplayName ?? string.Empty,
                attacker?.Team,
                damageEvent.AssistedByPlayerId,
                assistant?.DisplayName ?? string.Empty,
                assistant?.Team,
                latestKillFeedEntry?.WeaponSpriteName ?? string.Empty,
                latestKillFeedEntry?.MessageText ?? string.Empty));

            if (assistant is not null && attacker is not null)
            {
                pluginHost?.NotifyAssist(new OpenGarrisonServerAssistEvent(
                    world.Frame,
                    assistant.Id,
                    assistant.DisplayName,
                    assistant.Team,
                    attacker.Id,
                    attacker.DisplayName,
                    attacker.Team,
                    victim.Id,
                    victim.DisplayName,
                    victim.Team,
                    latestKillFeedEntry?.WeaponSpriteName ?? string.Empty));
            }
        }
    }

    private void PublishSpawnEvents()
    {
        var activePlayerIds = new HashSet<int>();
        var pluginHost = pluginHostGetter();
        foreach (var (slot, player) in world.EnumerateActiveNetworkPlayers())
        {
            activePlayerIds.Add(player.Id);
            var wasAlive = _lastObservedPlayerAliveById.GetValueOrDefault(player.Id, player.IsAlive);
            if (!wasAlive && player.IsAlive)
            {
                var isRespawn = _observedSpawnedPlayerIds.Contains(player.Id);
                pluginHost?.NotifyPlayerSpawned(new OpenGarrisonServerPlayerSpawnEvent(
                    world.Frame,
                    slot,
                    player.Id,
                    player.DisplayName,
                    player.Team,
                    player.ClassId,
                    player.X,
                    player.Y,
                    isRespawn));
                _observedSpawnedPlayerIds.Add(player.Id);
            }

            _lastObservedPlayerAliveById[player.Id] = player.IsAlive;
        }

        var staleIds = _lastObservedPlayerAliveById.Keys.Where(playerId => !activePlayerIds.Contains(playerId)).ToArray();
        for (var index = 0; index < staleIds.Length; index += 1)
        {
            _lastObservedPlayerAliveById.Remove(staleIds[index]);
            _observedSpawnedPlayerIds.Remove(staleIds[index]);
        }
    }

    private void PublishBuildableEvents()
    {
        var pluginHost = pluginHostGetter();
        var activeSentryIds = new HashSet<int>();
        foreach (var sentry in world.Sentries)
        {
            activeSentryIds.Add(sentry.Id);
            var wasBuilt = _lastObservedSentryBuiltById.GetValueOrDefault(sentry.Id, false);
            if (!wasBuilt && sentry.IsBuilt)
            {
                var owner = FindPlayerById(sentry.OwnerPlayerId);
                pluginHost?.NotifyBuild(new OpenGarrisonServerBuildableEvent(
                    world.Frame,
                    OpenGarrisonServerBuildableKind.Sentry,
                    sentry.Id,
                    sentry.OwnerPlayerId,
                    owner?.DisplayName ?? string.Empty,
                    sentry.Team,
                    sentry.X,
                    sentry.Y));
            }

            _lastObservedSentryBuiltById[sentry.Id] = sentry.IsBuilt;
        }

        var removedSentryIds = _lastObservedSentryBuiltById.Keys.Where(id => !activeSentryIds.Contains(id)).ToArray();
        for (var index = 0; index < removedSentryIds.Length; index += 1)
        {
            pluginHost?.NotifyDestroy(new OpenGarrisonServerBuildableEvent(
                world.Frame,
                OpenGarrisonServerBuildableKind.Sentry,
                removedSentryIds[index],
                0,
                string.Empty,
                null,
                0f,
                0f));
            _lastObservedSentryBuiltById.Remove(removedSentryIds[index]);
        }

        for (var index = 0; index < world.Generators.Count; index += 1)
        {
            var generator = world.Generators[index];
            var wasDestroyed = _lastObservedGeneratorDestroyedStates.GetValueOrDefault(generator.Team, generator.IsDestroyed);
            if (!wasDestroyed && generator.IsDestroyed)
            {
                pluginHost?.NotifyDestroy(new OpenGarrisonServerBuildableEvent(
                    world.Frame,
                    OpenGarrisonServerBuildableKind.Generator,
                    (int)generator.Team,
                    0,
                    string.Empty,
                    generator.Team,
                    generator.Marker.CenterX,
                    generator.Marker.CenterY));
            }

            _lastObservedGeneratorDestroyedStates[generator.Team] = generator.IsDestroyed;
        }
    }

    private void PublishIntelEvents()
    {
        PublishIntelEventsForState(
            world.RedIntel,
            ref _lastObservedRedIntelAtBase,
            ref _lastObservedRedIntelDropped,
            ref _lastObservedRedIntelX,
            ref _lastObservedRedIntelY);
        PublishIntelEventsForState(
            world.BlueIntel,
            ref _lastObservedBlueIntelAtBase,
            ref _lastObservedBlueIntelDropped,
            ref _lastObservedBlueIntelX,
            ref _lastObservedBlueIntelY);
    }

    private void PublishIntelEventsForState(
        TeamIntelligenceState intel,
        ref bool wasAtBase,
        ref bool wasDropped,
        ref float previousX,
        ref float previousY)
    {
        var pluginHost = pluginHostGetter();
        var actingTeam = intel.Team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        if ((wasAtBase || wasDropped) && intel.IsCarried)
        {
            var actor = FindPlayerCarryingEnemyIntel(actingTeam);
            pluginHost?.NotifyIntelEvent(new OpenGarrisonServerIntelEvent(
                world.Frame,
                OpenGarrisonServerIntelEventKind.PickedUp,
                intel.Team,
                actingTeam,
                actor?.Id ?? -1,
                actor?.DisplayName ?? string.Empty,
                intel.X,
                intel.Y));
        }
        else if (!wasDropped && !wasAtBase && intel.IsDropped)
        {
            var actor = FindClosestPlayer(previousX, previousY, actingTeam);
            pluginHost?.NotifyIntelEvent(new OpenGarrisonServerIntelEvent(
                world.Frame,
                OpenGarrisonServerIntelEventKind.Dropped,
                intel.Team,
                actingTeam,
                actor?.Id ?? -1,
                actor?.DisplayName ?? string.Empty,
                intel.X,
                intel.Y));
        }
        else if (wasDropped && intel.IsAtBase)
        {
            var kind = intel.Team == PlayerTeam.Red
                ? world.BlueCaps > _lastObservedBlueCaps
                    ? OpenGarrisonServerIntelEventKind.Captured
                    : OpenGarrisonServerIntelEventKind.Returned
                : world.RedCaps > _lastObservedRedCaps
                    ? OpenGarrisonServerIntelEventKind.Captured
                    : OpenGarrisonServerIntelEventKind.Returned;
            pluginHost?.NotifyIntelEvent(new OpenGarrisonServerIntelEvent(
                world.Frame,
                kind,
                intel.Team,
                actingTeam,
                -1,
                string.Empty,
                intel.X,
                intel.Y));
        }

        wasAtBase = intel.IsAtBase;
        wasDropped = intel.IsDropped;
        previousX = intel.X;
        previousY = intel.Y;
    }

    private void PublishControlPointEvents()
    {
        var pluginHost = pluginHostGetter();
        for (var index = 0; index < world.ControlPoints.Count; index += 1)
        {
            var point = world.ControlPoints[index];
            var currentState = (
                point.Team,
                point.CappingTeam,
                point.Cappers,
                (int)MathF.Round(point.CappingTicks),
                point.IsLocked);
            var previousState = _lastObservedControlPointStates.GetValueOrDefault(point.Index, currentState);
            if (!Equals(previousState, currentState))
            {
                pluginHost?.NotifyControlPointStateChanged(new OpenGarrisonServerControlPointStateEvent(
                    world.Frame,
                    point.Index,
                    point.Team,
                    point.CappingTeam,
                    point.Cappers,
                    point.CapTimeTicks <= 0 ? 0f : Math.Clamp(point.CappingTicks / point.CapTimeTicks, 0f, 1f),
                    point.IsLocked,
                    point.Marker.CenterX,
                    point.Marker.CenterY));
            }

            _lastObservedControlPointStates[point.Index] = currentState;
        }
    }

    private PlayerEntity? FindPlayerById(int playerId)
    {
        if (playerId <= 0)
        {
            return null;
        }

        foreach (var (_, player) in world.EnumerateActiveNetworkPlayers())
        {
            if (player.Id == playerId)
            {
                return player;
            }
        }

        return null;
    }

    private KillFeedEntry? FindLatestKillFeedEntryForVictim(int victimPlayerId)
    {
        for (var index = world.KillFeed.Count - 1; index >= 0; index -= 1)
        {
            var entry = world.KillFeed[index];
            if (entry.VictimPlayerId == victimPlayerId)
            {
                return entry;
            }
        }

        return null;
    }

    private PlayerEntity? FindPlayerCarryingEnemyIntel(PlayerTeam team)
    {
        foreach (var (_, player) in world.EnumerateActiveNetworkPlayers())
        {
            if (player.Team == team && player.IsCarryingIntel)
            {
                return player;
            }
        }

        return null;
    }

    private PlayerEntity? FindClosestPlayer(float x, float y, PlayerTeam? team = null)
    {
        PlayerEntity? closest = null;
        var closestDistanceSquared = float.MaxValue;
        foreach (var (_, player) in world.EnumerateActiveNetworkPlayers())
        {
            if (team.HasValue && player.Team != team.Value)
            {
                continue;
            }

            var deltaX = player.X - x;
            var deltaY = player.Y - y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared >= closestDistanceSquared)
            {
                continue;
            }

            closest = player;
            closestDistanceSquared = distanceSquared;
        }

        return closest;
    }
}
