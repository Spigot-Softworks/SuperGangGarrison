#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const string LegacyLobbyHost = "OpenGarrison.game-host.org";

    private void StartLobbyBrowserRegistryRequest()
    {
        _lobbyBrowserRegistryRequestTask = LoadLobbyRegistryEntriesAsync(LobbyRegistryEndpoint);
    }

    private void UpdateLobbyBrowserRegistryState()
    {
        var requestTask = _lobbyBrowserRegistryRequestTask;
        if (requestTask is null || !requestTask.IsCompleted)
        {
            return;
        }

        _lobbyBrowserRegistryRequestTask = null;
        if (!requestTask.IsCompletedSuccessfully)
        {
            if (_lobbyBrowserOpen && _lobbyBrowserEntries.Count == 0)
            {
                _menuStatusMessage = "Server registry unavailable. Use Manual.";
            }

            return;
        }

        foreach (var entry in requestTask.Result)
        {
            if (string.IsNullOrWhiteSpace(entry.Host))
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(entry.Name) ? entry.Host : entry.Name.Trim();
            var lobbyEntry = AddLobbyBrowserEntry(
                FormatLobbyDisplayName(name, entry.IsPrivate),
                new NetworkEndpoint(entry.Host.Trim(), entry.UdpPort, entry.WebSocketPort, entry.WebSocketUrl),
                entry.IsPrivate,
                isLobbyEntry: true);
            if (lobbyEntry is not null)
            {
                ApplyLobbyRegistryMetadata(lobbyEntry, entry);
            }
        }

        if (_lobbyBrowserSelectedIndex < 0 && _lobbyBrowserEntries.Count > 0)
        {
            _lobbyBrowserSelectedIndex = 0;
        }

        if (_lobbyBrowserOpen && _lobbyBrowserEntries.Count > 0)
        {
            _menuStatusMessage = string.Empty;
        }
    }

    private static void ApplyLobbyRegistryMetadata(LobbyBrowserEntry lobbyEntry, LobbyRegistryServerEntry registryEntry)
    {
        lobbyEntry.CanJoinDirectly = lobbyEntry.Endpoint.TryResolveForCurrentRuntime(out _, out _, out _);
        lobbyEntry.HasTimedOut = false;
        lobbyEntry.StatusText = lobbyEntry.CanJoinDirectly ? "Online" : "Unsupported";

        if (registryEntry.HasStatusMetadata)
        {
            lobbyEntry.HasResponse = true;
            lobbyEntry.ServerName = string.IsNullOrWhiteSpace(registryEntry.Name)
                ? lobbyEntry.DisplayName
                : registryEntry.Name.Trim();
            lobbyEntry.LevelName = string.IsNullOrWhiteSpace(registryEntry.Map)
                ? "-"
                : registryEntry.Map.Trim();
            lobbyEntry.ModeLabel = string.IsNullOrWhiteSpace(registryEntry.Mode)
                ? "-"
                : registryEntry.Mode.Trim();
            lobbyEntry.PlayerCount = Math.Max(0, registryEntry.Players);
            lobbyEntry.MaxPlayerCount = Math.Max(0, registryEntry.MaxPlayers);
            lobbyEntry.SpectatorCount = Math.Max(0, registryEntry.Spectators);
            lobbyEntry.PingMilliseconds = -1;
        }
    }

    private static async Task<List<LobbyRegistryServerEntry>> LoadLobbyRegistryEntriesAsync(string endpoint)
    {
        using var fallbackClient = OperatingSystem.IsBrowser() ? null : new HttpClient();
        var httpClient = OperatingSystem.IsBrowser()
            ? ClientRuntimeBootstrap.GetBrowserHttpClient()
            : fallbackClient;
        if (httpClient is null)
        {
            return [];
        }

        var requestUri = ResolveLobbyRegistryRequestUri(endpoint);
        var response = await httpClient.GetFromJsonAsync<LobbyRegistryResponse>(requestUri).ConfigureAwait(false);
        return response?.Servers ?? [];
    }

    private static Uri ResolveLobbyRegistryRequestUri(string endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri;
        }

        var baseAddress = ClientRuntimeBootstrap.GetBrowserBaseAddress();
        return baseAddress is null
            ? new Uri(endpoint, UriKind.Relative)
            : new Uri(baseAddress, endpoint);
    }

    private static string ResolveLobbyRegistryEndpoint(string lobbyHost, int lobbyPort)
    {
        if (string.IsNullOrWhiteSpace(lobbyHost))
        {
            lobbyHost = OpenGarrisonPreferencesDocument.DefaultLobbyHost;
        }

        lobbyHost = lobbyHost.Trim();
        if (string.Equals(lobbyHost, LegacyLobbyHost, StringComparison.OrdinalIgnoreCase))
        {
            lobbyHost = OpenGarrisonPreferencesDocument.DefaultLobbyHost;
        }

        if (Uri.TryCreate(lobbyHost, UriKind.Absolute, out var configuredUri))
        {
            return configuredUri.ToString();
        }

        if (OperatingSystem.IsBrowser())
        {
            return DefaultLobbyRegistryPath;
        }

        var builder = new UriBuilder(Uri.UriSchemeHttps, lobbyHost)
        {
            Path = DefaultLobbyRegistryPath,
        };
        if (lobbyPort is 80 or 443)
        {
            builder.Port = lobbyPort;
        }

        return builder.Uri.ToString();
    }

    private sealed class LobbyRegistryResponse
    {
        [JsonPropertyName("servers")]
        public List<LobbyRegistryServerEntry> Servers { get; set; } = [];
    }

    private sealed class LobbyRegistryServerEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("host")]
        public string Host { get; set; } = string.Empty;

        [JsonPropertyName("udpPort")]
        public int UdpPort { get; set; }

        [JsonPropertyName("webSocketPort")]
        public int WebSocketPort { get; set; }

        [JsonPropertyName("webSocketUrl")]
        public string WebSocketUrl { get; set; } = string.Empty;

        [JsonPropertyName("private")]
        public bool IsPrivate { get; set; }

        [JsonPropertyName("map")]
        public string Map { get; set; } = string.Empty;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = string.Empty;

        [JsonPropertyName("players")]
        public int Players { get; set; }

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonPropertyName("spectators")]
        public int Spectators { get; set; }

        [JsonPropertyName("protocolVersion")]
        public int ProtocolVersion { get; set; }

        [JsonIgnore]
        public bool HasStatusMetadata =>
            !string.IsNullOrWhiteSpace(Map)
            || !string.IsNullOrWhiteSpace(Mode)
            || Players > 0
            || MaxPlayers > 0
            || Spectators > 0
            || ProtocolVersion > 0;
    }
}
