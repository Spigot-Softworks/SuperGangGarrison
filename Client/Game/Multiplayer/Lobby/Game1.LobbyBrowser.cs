#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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

    public readonly List<LobbyBrowserEntry> _lobbyBrowserEntries = new();
    private UdpClient? _lobbyBrowserClient;
    private TcpClient? _lobbyBrowserLobbyClient;
    private Task? _lobbyBrowserLobbyConnectTask;
    public Task<List<LobbyRegistryServerEntry>>? _lobbyBrowserRegistryRequestTask;
    private Task<IReadOnlyList<LegacyGg2LobbyServer>>? _legacyGg2LobbyRequestTask;
    private CancellationTokenSource? _legacyGg2LobbyRequestCancellation;
    private readonly List<byte> _lobbyBrowserLobbyPending = new();
    private readonly byte[] _lobbyBrowserLobbyScratch = new byte[4096];
    private int _lobbyBrowserLobbyExpectedServers = -1;
    private int _lobbyBrowserLobbyServersRead;
    public long _lobbyBrowserLobbyStartedAtMilliseconds;
    private bool _lobbyBrowserLobbyHandshakeSent;
    public LobbyBrowserMode _lobbyBrowserMode = LobbyBrowserMode.Join;
    public LobbyBrowserSource _lobbyBrowserSource = LobbyBrowserSource.Sgg;
    public LobbyBrowserPage _lobbyBrowserPage = LobbyBrowserPage.List;
    public LobbyBrowserEntry? _lobbyBrowserDetailsEntry;
    public int _lobbyBrowserScrollOffset;
    private ServerDetailsResponseMessage? _lobbyBrowserDetailsResponse;
    public string _lobbyBrowserDetailsStatus = string.Empty;
    private bool _lobbyBrowserDetailsRequestInFlight;
    private long _lobbyBrowserDetailsRequestStartedAtMilliseconds;
    private INetworkClientMessageTransport? _lobbyBrowserDetailsTransport;

    public string LobbyServerHost => string.IsNullOrWhiteSpace(_clientSettings.LobbyHost)
        ? OpenGarrisonPreferencesDocument.DefaultLobbyHost
        : _clientSettings.LobbyHost.Trim();

    public int LobbyServerPort => _clientSettings.LobbyPort > 0
        ? _clientSettings.LobbyPort
        : OpenGarrisonPreferencesDocument.DefaultLobbyPort;

    public string LobbyRegistryEndpoint => ResolveLobbyRegistryEndpoint(_clientSettings.LobbyHost, _clientSettings.LobbyPort);

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

    public sealed class LobbyBrowserEntry(string displayName, NetworkEndpoint endpoint)
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
        public bool IsLegacyGg2 { get; set; }
        public string VersionLabel { get; set; } = "-";
        public int BotCount { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsLobbyEntry { get; set; }
    }

    public enum LobbyBrowserMode
    {
        Join,
        Watch,
    }

    public enum LobbyBrowserSource
    {
        Sgg,
        Gg2,
    }

    public enum LobbyBrowserPage
    {
        List,
        Details,
    }

    public readonly record struct LobbyBrowserTarget(string DisplayName, NetworkEndpoint Endpoint);
}
