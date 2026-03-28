using OpenGarrison.Core;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class ServerRuntimeEventReporter(
    SimulationWorld world,
    Func<PluginHost?> pluginHostGetter,
    Action<string, (string Key, object? Value)[]> writeEvent,
    ServerMapMetadataResolver mapMetadataResolver)
{
    private int _lastObservedRedCaps;
    private int _lastObservedBlueCaps;
    private MatchPhase _lastObservedMatchPhase;
    private int _lastObservedKillFeedCount;
    private readonly Dictionary<int, int> _lastObservedPlayerCapsById = new();

    public void ResetObservedGameplayState()
    {
        _lastObservedRedCaps = world.RedCaps;
        _lastObservedBlueCaps = world.BlueCaps;
        _lastObservedMatchPhase = world.MatchState.Phase;
        _lastObservedKillFeedCount = world.KillFeed.Count;
        _lastObservedPlayerCapsById.Clear();
        foreach (var (_, player) in world.EnumerateActiveNetworkPlayers())
        {
            _lastObservedPlayerCapsById[player.Id] = player.Caps;
        }
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

    public void PublishGameplayEvents()
    {
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
}
