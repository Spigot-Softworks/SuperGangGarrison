#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const long LobbyBrowserQueryTimeoutMilliseconds = 1500;
    private const long LobbyBrowserDetailsQueryTimeoutMilliseconds = 2500;
    private const long LobbyBrowserLobbyConnectTimeoutMilliseconds = 2500;
    private const long LobbyBrowserLobbyReadTimeoutMilliseconds = 6000;
    private const string DefaultLobbyRegistryPath = "servers.json";
    private const string LobbyProtocolUuidString = "71eb5496-492b-b186-4770-06ccb30d3f8f";

    private static readonly byte[] LobbyProtocolUuidBytes = ParseProtocolUuid(LobbyProtocolUuidString);

    private readonly List<LobbyBrowserEntry> _lobbyBrowserEntries = new();
    private UdpClient? _lobbyBrowserClient;
    private TcpClient? _lobbyBrowserLobbyClient;
    private Task? _lobbyBrowserLobbyConnectTask;
    private Task<List<LobbyRegistryServerEntry>>? _lobbyBrowserRegistryRequestTask;
    private readonly List<byte> _lobbyBrowserLobbyPending = new();
    private readonly byte[] _lobbyBrowserLobbyScratch = new byte[4096];
    private int _lobbyBrowserLobbyExpectedServers = -1;
    private int _lobbyBrowserLobbyServersRead;
    private long _lobbyBrowserLobbyStartedAtMilliseconds;
    private bool _lobbyBrowserLobbyHandshakeSent;
    private LobbyBrowserMode _lobbyBrowserMode = LobbyBrowserMode.Join;
    private LobbyBrowserPage _lobbyBrowserPage = LobbyBrowserPage.List;
    private LobbyBrowserEntry? _lobbyBrowserDetailsEntry;
    private ServerDetailsResponseMessage? _lobbyBrowserDetailsResponse;
    private string _lobbyBrowserDetailsStatus = string.Empty;
    private bool _lobbyBrowserDetailsRequestInFlight;
    private long _lobbyBrowserDetailsRequestStartedAtMilliseconds;
    private INetworkClientMessageTransport? _lobbyBrowserDetailsTransport;

    private string LobbyServerHost => string.IsNullOrWhiteSpace(_clientSettings.LobbyHost)
        ? OpenGarrisonPreferencesDocument.DefaultLobbyHost
        : _clientSettings.LobbyHost.Trim();

    private int LobbyServerPort => _clientSettings.LobbyPort > 0
        ? _clientSettings.LobbyPort
        : OpenGarrisonPreferencesDocument.DefaultLobbyPort;

    private string LobbyRegistryEndpoint => ResolveLobbyRegistryEndpoint(_clientSettings.LobbyHost, _clientSettings.LobbyPort);

    private static byte[] ParseProtocolUuid(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
        {
            return Array.Empty<byte>();
        }

        var cleaned = uuid.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        if (cleaned.Length != 32)
        {
            return Array.Empty<byte>();
        }

        var bytes = new byte[16];
        for (var index = 0; index < bytes.Length; index += 1)
        {
            var hex = cleaned.Substring(index * 2, 2);
            if (!byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                return Array.Empty<byte>();
            }

            bytes[index] = value;
        }

        return bytes;
    }

    private sealed class LobbyBrowserEntry(string displayName, NetworkEndpoint endpoint)
    {
        public string DisplayName { get; set; } = displayName;
        public NetworkEndpoint Endpoint { get; set; } = endpoint;
        public string Host => Endpoint.Host;
        public int Port => Endpoint.QueryPort;
        public string AddressLabel => Endpoint.AddressLabel;
        public IPEndPoint? QueryEndPoint { get; set; }
        public long QueryStartedAtMilliseconds { get; set; }
        public bool HasResponse { get; set; }
        public bool CanJoinDirectly { get; set; }
        public bool HasTimedOut { get; set; }
        public string StatusText { get; set; } = "Querying...";
        public string ServerName { get; set; } = string.Empty;
        public string LevelName { get; set; } = "-";
        public string ModeLabel { get; set; } = "-";
        public int PlayerCount { get; set; }
        public int MaxPlayerCount { get; set; }
        public int SpectatorCount { get; set; }
        public int PingMilliseconds { get; set; } = -1;
        public string PingLabel => PingMilliseconds >= 0 ? $"{PingMilliseconds} ms" : "-";
        public bool IsPrivate { get; set; }
        public bool IsLobbyEntry { get; set; }
    }

    private enum LobbyBrowserMode
    {
        Join,
        Watch,
    }

    private enum LobbyBrowserPage
    {
        List,
        Details,
    }

    private readonly record struct LobbyBrowserTarget(string DisplayName, NetworkEndpoint Endpoint);
}
