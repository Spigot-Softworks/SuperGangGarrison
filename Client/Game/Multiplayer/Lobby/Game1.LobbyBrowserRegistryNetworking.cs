#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

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

            if (!IsCompatibleLobbyRegistryEntry(entry))
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(entry.Name) ? entry.Host : entry.Name.Trim();
            var lobbyEntry = AddLobbyBrowserEntry(
                FormatLobbyDisplayName(name, entry.IsPrivate),
                CreateRegistryNetworkEndpoint(entry),
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

    private static bool IsCompatibleLobbyRegistryEntry(LobbyRegistryServerEntry entry)
    {
        if (entry.ProtocolVersion != ProtocolVersion.Current)
        {
            return false;
        }

        var entryChannel = ApplicationBuildInfo.NormalizeReleaseChannel(entry.ReleaseChannel);
        var currentChannel = ApplicationBuildInfo.ReleaseChannel;
        if (!string.Equals(entryChannel, currentChannel, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var currentBuildVersion = ApplicationBuildInfo.BuildVersion;
        var currentCompatibilityKey = ApplicationBuildInfo.CreateCompatibilityKey(
            ProtocolVersion.Current,
            currentBuildVersion,
            currentChannel);
        var entryCompatibilityKey = ApplicationBuildInfo.NormalizeCompatibilityKey(entry.CompatibilityKey);
        if (!string.IsNullOrWhiteSpace(entryCompatibilityKey))
        {
            return string.Equals(entryCompatibilityKey, currentCompatibilityKey, StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrWhiteSpace(entry.BuildVersion))
        {
            return string.Equals(currentChannel, ApplicationBuildInfo.DefaultReleaseChannel, StringComparison.OrdinalIgnoreCase);
        }

        var entryBuildVersion = ApplicationBuildInfo.NormalizeBuildVersion(entry.BuildVersion);
        return string.Equals(entryBuildVersion, currentBuildVersion, StringComparison.OrdinalIgnoreCase);
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
        }
    }

    private static NetworkEndpoint CreateRegistryNetworkEndpoint(LobbyRegistryServerEntry entry)
    {
        var host = entry.Host.Trim();
        var webSocketUrl = entry.WebSocketUrl;
        if (OperatingSystem.IsBrowser()
            && string.IsNullOrWhiteSpace(webSocketUrl)
            && entry.WebSocketPort is > 0 and <= 65535
            && TryCreateBrowserPublicWebSocketUrl(host, out var browserWebSocketUrl))
        {
            webSocketUrl = browserWebSocketUrl;
        }

        return new NetworkEndpoint(host, entry.UdpPort, entry.WebSocketPort, webSocketUrl);
    }

    private static bool TryCreateBrowserPublicWebSocketUrl(string host, out string webSocketUrl)
    {
        webSocketUrl = string.Empty;
        if (!IPAddress.TryParse(host, out var address)
            || address.AddressFamily != AddressFamily.InterNetwork
            || !IsPublicIPv4Address(address))
        {
            return false;
        }

        var octets = address.GetAddressBytes();
        var sslipHost = string.Join('-', octets) + ".sslip.io";
        webSocketUrl = $"wss://{sslipHost}/opengarrison/ws";
        return true;
    }

    private static bool IsPublicIPv4Address(IPAddress address)
    {
        var octets = address.GetAddressBytes();
        if (octets.Length != 4)
        {
            return false;
        }

        return octets[0] switch
        {
            0 or 10 or 127 => false,
            169 when octets[1] == 254 => false,
            172 when octets[1] is >= 16 and <= 31 => false,
            192 when octets[1] == 168 => false,
            100 when octets[1] is >= 64 and <= 127 => false,
            _ => true,
        };
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

        var requestUri = AddLobbyRegistryCompatibilityQuery(ResolveLobbyRegistryRequestUri(endpoint));
        var response = await httpClient.GetFromJsonAsync<LobbyRegistryResponse>(requestUri).ConfigureAwait(false);
        return response?.Servers ?? [];
    }

    private static Uri AddLobbyRegistryCompatibilityQuery(Uri uri)
    {
        var releaseChannel = ApplicationBuildInfo.ReleaseChannel;
        var buildVersion = ApplicationBuildInfo.BuildVersion;
        var compatibilityKey = ApplicationBuildInfo.CreateCompatibilityKey(
            ProtocolVersion.Current,
            buildVersion,
            releaseChannel);
        var compatibilityQuery =
            $"protocolVersion={ProtocolVersion.Current}"
            + $"&releaseChannel={Uri.EscapeDataString(releaseChannel)}"
            + $"&buildVersion={Uri.EscapeDataString(buildVersion)}"
            + $"&compatibilityKey={Uri.EscapeDataString(compatibilityKey)}";

        if (!uri.IsAbsoluteUri)
        {
            var text = uri.ToString();
            var separator = text.Contains('?', StringComparison.Ordinal) ? '&' : '?';
            return new Uri(text + separator + compatibilityQuery, UriKind.Relative);
        }

        var builder = new UriBuilder(uri);
        var existingQuery = builder.Query;
        builder.Query = string.IsNullOrWhiteSpace(existingQuery)
            ? compatibilityQuery
            : existingQuery.TrimStart('?') + "&" + compatibilityQuery;
        return builder.Uri;
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

        [JsonPropertyName("buildVersion")]
        public string BuildVersion { get; set; } = string.Empty;

        [JsonPropertyName("releaseChannel")]
        public string ReleaseChannel { get; set; } = string.Empty;

        [JsonPropertyName("compatibilityKey")]
        public string CompatibilityKey { get; set; } = string.Empty;

        [JsonIgnore]
        public bool HasStatusMetadata =>
            !string.IsNullOrWhiteSpace(Map)
            || !string.IsNullOrWhiteSpace(Mode)
            || Players > 0
            || MaxPlayers > 0
            || Spectators > 0
            || ProtocolVersion > 0
            || !string.IsNullOrWhiteSpace(BuildVersion)
            || !string.IsNullOrWhiteSpace(ReleaseChannel)
            || !string.IsNullOrWhiteSpace(CompatibilityKey);
    }
}
