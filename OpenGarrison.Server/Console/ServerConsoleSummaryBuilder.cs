using System.Globalization;
using OpenGarrison.Core;
using static ServerHelpers;

namespace OpenGarrison.Server;

internal sealed class ServerConsoleSummaryBuilder(
    SimulationConfig config,
    int port,
    Func<string> serverNameGetter,
    Func<SimulationWorld> worldGetter,
    Func<IReadOnlyDictionary<byte, ClientSession>> clientsGetter,
    Func<TimeSpan> uptimeGetter,
    int maxPlayableClients,
    bool useLobbyServer,
    string lobbyHost,
    int lobbyPort,
    bool passwordRequired,
    bool autoBalanceEnabled,
    int? respawnSecondsOverride,
    Func<MapRotationManager> mapRotationManagerGetter,
    string? mapRotationFile)
{
    public void AddStatusSummary(List<string> lines)
    {
        var world = worldGetter();
        var clients = clientsGetter();
        var activePlayableCount = world.EnumerateActiveNetworkPlayers().Count();
        var spectatorCount = clients.Keys.Count(IsSpectatorSlot);
        var uptime = FormatDuration(uptimeGetter());
        var lobbyValue = useLobbyServer ? $"enabled {lobbyHost}:{lobbyPort}" : "disabled";
        var passwordValue = passwordRequired ? "required" : "off";
        lines.Add(
            $"[server] status | name={serverNameGetter()} | port={port} | tickrate={config.TicksPerSecond} | players={activePlayableCount}/{maxPlayableClients} | spectators={spectatorCount} | map={world.Level.Name} area={world.Level.MapAreaIndex}/{world.Level.MapAreaCount} | mode={world.MatchRules.Mode} | phase={world.MatchState.Phase} | score={world.RedCaps}-{world.BlueCaps} | lobby={lobbyValue} | password={passwordValue} | uptime={uptime}");
    }

    public void AddRulesSummary(List<string> lines)
    {
        var world = worldGetter();
        var autoBalanceValue = autoBalanceEnabled ? "enabled" : "disabled";
        var respawnSeconds = respawnSecondsOverride ?? 5;
        lines.Add(
            $"[server] rules | timeLimit={world.MatchRules.TimeLimitMinutes} | capLimit={world.MatchRules.CapLimit} | respawn={respawnSeconds} | autoBalance={autoBalanceValue}");
    }

    public void AddLobbySummary(List<string> lines)
    {
        var lobbyValue = useLobbyServer ? "enabled" : "disabled";
        lines.Add($"[server] lobby | enabled={lobbyValue} | host={lobbyHost} | port={lobbyPort}");
    }

    public void AddMapSummary(List<string> lines)
    {
        var world = worldGetter();
        var winner = world.MatchState.WinnerTeam?.ToString() ?? "none";
        lines.Add(
            $"[server] map | name={world.Level.Name} | area={world.Level.MapAreaIndex}/{world.Level.MapAreaCount} | mode={world.MatchRules.Mode} | phase={world.MatchState.Phase} | winner={winner} | imported={world.Level.ImportedFromSource}");
        lines.Add($"[server] world | bounds={world.Bounds.Width}x{world.Bounds.Height}");
    }

    public void AddRotationSummary(List<string> lines)
    {
        var world = worldGetter();
        var mapRotationManager = mapRotationManagerGetter();
        var rotation = mapRotationManager.MapRotation;
        var currentIndex = rotation.Count == 0 ? 0 : Math.Clamp(mapRotationManager.CurrentRotationIndex + 1, 1, rotation.Count);
        var source = string.IsNullOrWhiteSpace(mapRotationFile) ? "stock" : mapRotationFile!;
        var entries = rotation.Count == 0 ? world.Level.Name : string.Join(", ", rotation);
        lines.Add($"[server] rotation | source={source} | current={currentIndex}/{Math.Max(1, rotation.Count)} | entries={entries}");
    }

    public void AddPlayersSummary(List<string> lines)
    {
        var world = worldGetter();
        var clients = clientsGetter();
        if (clients.Count == 0)
        {
            lines.Add("[server] players | count=0");
            return;
        }

        lines.Add($"[server] players | count={clients.Count}");
        foreach (var client in clients.Values.OrderBy(entry => entry.Slot))
        {
            var role = "Spectator";
            if (!IsSpectatorSlot(client.Slot))
            {
                role = world.TryGetNetworkPlayer(client.Slot, out var player)
                    ? player.Team.ToString()
                    : "Unassigned";
            }

            var connectedFor = FormatDuration(uptimeGetter() - client.ConnectedAt);
            var authorized = client.IsAuthorized ? "yes" : "pending";
            lines.Add(
                $"[server] player | slot={client.Slot} | name={client.Name} | role={role} | authorized={authorized} | endpoint={client.EndPoint} | connected={connectedFor}");
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return duration.TotalHours >= 1d
            ? duration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }
}
