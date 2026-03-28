#nullable enable

using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void StartLobbyBrowserLobbyRequest()
    {
        CloseLobbyBrowserLobbyClient();
        _lobbyBrowserLobbyExpectedServers = -1;
        _lobbyBrowserLobbyServersRead = 0;
        _lobbyBrowserLobbyPending.Clear();
        _lobbyBrowserLobbyHandshakeSent = false;
        _lobbyBrowserLobbyStartedAtMilliseconds = Environment.TickCount64;

        if (LobbyProtocolUuidBytes.Length != 16)
        {
            return;
        }

        _lobbyBrowserLobbyClient = new TcpClient();
        try
        {
            _lobbyBrowserLobbyConnectTask = _lobbyBrowserLobbyClient.ConnectAsync(LobbyServerHost, LobbyServerPort);
        }
        catch
        {
            CloseLobbyBrowserLobbyClient();
        }
    }

    private void UpdateLobbyBrowserLobbyState()
    {
        if (_lobbyBrowserLobbyClient is null)
        {
            return;
        }

        if (_lobbyBrowserLobbyConnectTask is not null)
        {
            if (!_lobbyBrowserLobbyConnectTask.IsCompleted)
            {
                if (Environment.TickCount64 - _lobbyBrowserLobbyStartedAtMilliseconds
                    > LobbyBrowserLobbyConnectTimeoutMilliseconds)
                {
                    CloseLobbyBrowserLobbyClient();
                }
                return;
            }

            if (_lobbyBrowserLobbyConnectTask.IsFaulted || _lobbyBrowserLobbyConnectTask.IsCanceled)
            {
                CloseLobbyBrowserLobbyClient();
                return;
            }

            _lobbyBrowserLobbyConnectTask = null;
        }

        var socket = _lobbyBrowserLobbyClient.Client;
        if (socket is null || !socket.Connected)
        {
            CloseLobbyBrowserLobbyClient();
            return;
        }

        if (!_lobbyBrowserLobbyHandshakeSent)
        {
            if (!TrySendLobbyBrowserHandshake(socket))
            {
                CloseLobbyBrowserLobbyClient();
                return;
            }

            _lobbyBrowserLobbyHandshakeSent = true;
        }

        if (Environment.TickCount64 - _lobbyBrowserLobbyStartedAtMilliseconds
            > LobbyBrowserLobbyReadTimeoutMilliseconds)
        {
            CloseLobbyBrowserLobbyClient();
            return;
        }

        try
        {
            var available = socket.Available;
            while (available > 0)
            {
                var read = socket.Receive(
                    _lobbyBrowserLobbyScratch,
                    0,
                    Math.Min(_lobbyBrowserLobbyScratch.Length, available),
                    SocketFlags.None);
                if (read <= 0)
                {
                    CloseLobbyBrowserLobbyClient();
                    return;
                }

                for (var index = 0; index < read; index += 1)
                {
                    _lobbyBrowserLobbyPending.Add(_lobbyBrowserLobbyScratch[index]);
                }

                available = socket.Available;
            }
        }
        catch (SocketException)
        {
            CloseLobbyBrowserLobbyClient();
            return;
        }

        ParseLobbyBrowserLobbyData();

        if (_lobbyBrowserLobbyExpectedServers >= 0
            && _lobbyBrowserLobbyServersRead >= _lobbyBrowserLobbyExpectedServers)
        {
            CloseLobbyBrowserLobbyClient();
        }
    }

    private void ParseLobbyBrowserLobbyData()
    {
        var offset = 0;
        if (_lobbyBrowserLobbyExpectedServers < 0)
        {
            if (_lobbyBrowserLobbyPending.Count - offset < 1)
            {
                return;
            }

            _lobbyBrowserLobbyExpectedServers = _lobbyBrowserLobbyPending[offset];
            offset += 1;
            if (_lobbyBrowserLobbyExpectedServers == 0)
            {
                _lobbyBrowserLobbyPending.Clear();
                CloseLobbyBrowserLobbyClient();
                return;
            }
        }

        while (_lobbyBrowserLobbyExpectedServers >= 0
            && _lobbyBrowserLobbyServersRead < _lobbyBrowserLobbyExpectedServers)
        {
            if (_lobbyBrowserLobbyPending.Count - offset < 1)
            {
                break;
            }

            var nameLength = _lobbyBrowserLobbyPending[offset];
            var entryLength = 1 + nameLength + 6;
            if (_lobbyBrowserLobbyPending.Count - offset < entryLength)
            {
                break;
            }

            offset += 1;
            var nameBytes = _lobbyBrowserLobbyPending.GetRange(offset, nameLength).ToArray();
            offset += nameLength;

            var ip0 = _lobbyBrowserLobbyPending[offset++];
            var ip1 = _lobbyBrowserLobbyPending[offset++];
            var ip2 = _lobbyBrowserLobbyPending[offset++];
            var ip3 = _lobbyBrowserLobbyPending[offset++];
            var port = (_lobbyBrowserLobbyPending[offset] << 8) | _lobbyBrowserLobbyPending[offset + 1];
            offset += 2;

            var name = Encoding.UTF8.GetString(nameBytes);
            var isPrivate = false;
            if (name.StartsWith("!private!", StringComparison.OrdinalIgnoreCase))
            {
                name = name[9..];
                isPrivate = true;
            }

            name = name.Trim();
            if (name.Contains('#'))
            {
                name = "Tornado";
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Unknown server";
            }

            var host = $"{ip0}.{ip1}.{ip2}.{ip3}";
            AddLobbyBrowserEntry(FormatLobbyDisplayName(name, isPrivate), host, port, isPrivate, isLobbyEntry: true);

            _lobbyBrowserLobbyServersRead += 1;
        }

        if (offset > 0)
        {
            _lobbyBrowserLobbyPending.RemoveRange(0, offset);
        }
    }

    private void CloseLobbyBrowserLobbyClient()
    {
        _lobbyBrowserLobbyConnectTask = null;
        if (_lobbyBrowserLobbyClient is not null)
        {
            _lobbyBrowserLobbyClient.Dispose();
            _lobbyBrowserLobbyClient = null;
        }
    }

    private static bool TrySendLobbyBrowserHandshake(Socket socket)
    {
        if (LobbyProtocolUuidBytes.Length != 16)
        {
            return false;
        }

        Span<byte> payload = stackalloc byte[17];
        payload[0] = 128;
        LobbyProtocolUuidBytes.AsSpan().CopyTo(payload[1..]);

        try
        {
            socket.Send(payload);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private void AddLobbyBrowserEntry(string displayName, string host, int port, bool isPrivate, bool isLobbyEntry)
    {
        if (string.IsNullOrWhiteSpace(host) || port <= 0)
        {
            return;
        }

        var existing = _lobbyBrowserEntries.FirstOrDefault(entry =>
            entry.Host.Equals(host, StringComparison.OrdinalIgnoreCase) && entry.Port == port);
        if (existing is not null)
        {
            if (isLobbyEntry)
            {
                existing.IsLobbyEntry = true;
                existing.IsPrivate = isPrivate;
                if (existing.DisplayName is "Manual target" or "Recent")
                {
                    existing.DisplayName = displayName;
                }
            }

            return;
        }

        var entry = new LobbyBrowserEntry(displayName, host, port)
        {
            IsPrivate = isPrivate,
            IsLobbyEntry = isLobbyEntry,
        };
        _lobbyBrowserEntries.Add(entry);
        if (_lobbyBrowserSelectedIndex < 0)
        {
            _lobbyBrowserSelectedIndex = _lobbyBrowserEntries.Count - 1;
        }

        QueryLobbyBrowserEntry(entry);
    }

    private static string FormatLobbyDisplayName(string name, bool isPrivate)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Unknown server";
        }

        return isPrivate ? $"{name} (Private)" : name;
    }
}
