#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const long LobbyBrowserQueryTimeoutMilliseconds = 1500;
    private const long LobbyBrowserLobbyConnectTimeoutMilliseconds = 2500;
    private const long LobbyBrowserLobbyReadTimeoutMilliseconds = 6000;
    private const string LobbyServerHost = "OpenGarrison.game-host.org";
    private const int LobbyServerPort = 29942;
    private const string LobbyProtocolUuidString = "71eb5496-492b-b186-4770-06ccb30d3f8f";

    private static readonly byte[] LobbyProtocolUuidBytes = ParseProtocolUuid(LobbyProtocolUuidString);

    private readonly List<LobbyBrowserEntry> _lobbyBrowserEntries = new();
    private UdpClient? _lobbyBrowserClient;
    private TcpClient? _lobbyBrowserLobbyClient;
    private Task? _lobbyBrowserLobbyConnectTask;
    private readonly List<byte> _lobbyBrowserLobbyPending = new();
    private readonly byte[] _lobbyBrowserLobbyScratch = new byte[4096];
    private int _lobbyBrowserLobbyExpectedServers = -1;
    private int _lobbyBrowserLobbyServersRead;
    private long _lobbyBrowserLobbyStartedAtMilliseconds;
    private bool _lobbyBrowserLobbyHandshakeSent;

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

    private sealed class LobbyBrowserEntry(string displayName, string host, int port)
    {
        public string DisplayName { get; set; } = displayName;
        public string Host { get; } = host;
        public int Port { get; } = port;
        public string AddressLabel => $"{Host}:{Port}";
        public IPEndPoint? QueryEndPoint { get; set; }
        public long QueryStartedAtMilliseconds { get; set; }
        public bool HasResponse { get; set; }
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

    private readonly record struct LobbyBrowserTarget(string DisplayName, string Host, int Port);
}
