#nullable enable

using System;
using System.Threading;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void SelectLobbyBrowserSource(LobbyBrowserSource source)
    {
        if ((IsRestrictedBrowserEdition || OpenGarrison.ClientShared.ClientDistribution.IsGg2Only)
            && source != LobbyBrowserSource.Gg2)
            return;
        if (_lobbyBrowserMode != LobbyBrowserMode.Join || _lobbyBrowserSource == source)
        {
            return;
        }

        _lobbyBrowserSource = source;
        RefreshLobbyBrowser();
    }

    private void StartLegacyGg2LobbyRequest()
    {
        CancelLegacyGg2LobbyRequest();
        _legacyGg2LobbyRequestCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        _legacyGg2LobbyRequestTask = OperatingSystem.IsBrowser()
            ? LegacyGg2LobbyClient.FetchBrowserAsync(
                OpenGarrison.ClientShared.ClientDistribution.RoomServiceOrigin,
                _legacyGg2LobbyRequestCancellation.Token)
            : LegacyGg2LobbyClient.FetchAsync(
                cancellationToken: _legacyGg2LobbyRequestCancellation.Token);
    }

    private void CancelLegacyGg2LobbyRequest()
    {
        _legacyGg2LobbyRequestCancellation?.Cancel();
        _legacyGg2LobbyRequestCancellation?.Dispose();
        _legacyGg2LobbyRequestCancellation = null;
        _legacyGg2LobbyRequestTask = null;
    }

    private void UpdateLegacyGg2LobbyRequest()
    {
        var task = _legacyGg2LobbyRequestTask;
        if (task is null || !task.IsCompleted)
        {
            return;
        }

        _legacyGg2LobbyRequestTask = null;
        _legacyGg2LobbyRequestCancellation?.Dispose();
        _legacyGg2LobbyRequestCancellation = null;
        if (_lobbyBrowserSource != LobbyBrowserSource.Gg2 || !_lobbyBrowserOpen)
        {
            return;
        }

        if (task.IsFaulted || task.IsCanceled)
        {
            _menuStatusMessage = task.IsCanceled
                ? "GG2 lobby request timed out."
                : $"GG2 lobby unavailable: {task.Exception?.GetBaseException().Message}";
            return;
        }

        foreach (var server in task.Result)
        {
            if (server.Port is < 1 or > 65535) continue;
            var entry = new LobbyBrowserEntry(server.Name, NetworkEndpoint.ForUdp(server.Host, server.Port))
            {
                IsLegacyGg2 = true,
                IsLobbyEntry = true,
                IsPrivate = server.IsPrivate,
                HasResponse = true,
                CanJoinDirectly = server.IsCompatible && !server.IsPrivate,
                HasTimedOut = false,
                StatusText = server.IsPrivate ? "Password required"
                    : server.IsCompatible ? "Online" : "Different protocol",
                ServerName = server.Name,
                LevelName = server.Map,
                ModeLabel = GetLegacyGg2ModeLabel(server.Map),
                PlayerCount = server.Players,
                BotCount = server.Bots,
                MaxPlayerCount = server.Slots,
                VersionLabel = string.IsNullOrEmpty(server.Version) ? "-" : server.Version,
            };
            _lobbyBrowserEntries.Add(entry);
        }

        _lobbyBrowserSelectedIndex = _lobbyBrowserEntries.FindIndex(entry => entry.CanJoinDirectly);
        _lobbyBrowserScrollOffset = 0;
        _menuStatusMessage = _lobbyBrowserEntries.Count == 0
            ? "No GG2 servers advertised."
            : string.Empty;
    }

    private static string GetLegacyGg2ModeLabel(string map)
    {
        var separator = map.IndexOf('_');
        return separator > 0 ? map[..separator].ToUpperInvariant() : "GG2";
    }
}
