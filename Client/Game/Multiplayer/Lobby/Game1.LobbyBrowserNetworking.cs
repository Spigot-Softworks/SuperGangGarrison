#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void EnsureLobbyBrowserClient()
    {
        if (_lobbyBrowserClient is not null)
        {
            return;
        }

        _lobbyBrowserClient = new UdpClient(0);
        _lobbyBrowserClient.Client.Blocking = false;
    }

    private void QueryLobbyBrowserEntry(LobbyBrowserEntry entry)
    {
        if (_lobbyBrowserClient is null)
        {
            entry.HasTimedOut = true;
            entry.StatusText = "Browser unavailable";
            return;
        }

        try
        {
            var addresses = Dns.GetHostAddresses(entry.Host);
            var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault();
            if (address is null)
            {
                entry.HasTimedOut = true;
                entry.StatusText = "Resolve failed";
                return;
            }

            entry.QueryEndPoint = new IPEndPoint(address, entry.Port);
            entry.QueryStartedAtMilliseconds = Environment.TickCount64;
            var payload = ProtocolCodec.Serialize(new ServerStatusRequestMessage());
            _lobbyBrowserClient.Send(payload, payload.Length, entry.QueryEndPoint);
        }
        catch
        {
            entry.HasTimedOut = true;
            entry.StatusText = "Resolve failed";
        }
    }

    private void UpdateLobbyBrowserResponses()
    {
        UpdateLobbyBrowserLobbyState();

        if (_lobbyBrowserClient is null)
        {
            return;
        }

        while (_lobbyBrowserClient.Available > 0)
        {
            try
            {
                IPEndPoint remoteEndPoint = new(IPAddress.Any, 0);
                var payload = _lobbyBrowserClient.Receive(ref remoteEndPoint);
                if (!ProtocolCodec.TryDeserialize(payload, out var message) || message is not ServerStatusResponseMessage status)
                {
                    continue;
                }

                for (var index = 0; index < _lobbyBrowserEntries.Count; index += 1)
                {
                    var entry = _lobbyBrowserEntries[index];
                    if (entry.QueryEndPoint is null || !EndpointsEqual(entry.QueryEndPoint, remoteEndPoint))
                    {
                        continue;
                    }

                    entry.HasResponse = true;
                    entry.HasTimedOut = false;
                    entry.ServerName = status.ServerName;
                    entry.LevelName = status.LevelName;
                    entry.ModeLabel = status.GameMode switch
                    {
                        (byte)GameModeKind.Arena => "Arena",
                        (byte)GameModeKind.ControlPoint => "CP",
                        (byte)GameModeKind.Generator => "Gen",
                        _ => "CTF",
                    };
                    entry.PlayerCount = status.PlayerCount;
                    entry.MaxPlayerCount = status.MaxPlayerCount;
                    entry.SpectatorCount = status.SpectatorCount;
                    entry.PingMilliseconds = (int)Math.Max(0, Environment.TickCount64 - entry.QueryStartedAtMilliseconds);
                    entry.StatusText = "Online";
                    if (entry.DisplayName is "Manual target" or "Recent")
                    {
                        entry.DisplayName = FormatLobbyDisplayName(status.ServerName, entry.IsPrivate);
                    }
                    else if (entry.IsLobbyEntry && !string.IsNullOrWhiteSpace(status.ServerName))
                    {
                        entry.DisplayName = FormatLobbyDisplayName(status.ServerName, entry.IsPrivate);
                    }

                    break;
                }
            }
            catch (SocketException)
            {
                break;
            }
        }

        var pendingCount = 0;
        for (var index = 0; index < _lobbyBrowserEntries.Count; index += 1)
        {
            var entry = _lobbyBrowserEntries[index];
            if (entry.HasResponse || entry.HasTimedOut)
            {
                continue;
            }

            if (Environment.TickCount64 - entry.QueryStartedAtMilliseconds >= LobbyBrowserQueryTimeoutMilliseconds)
            {
                entry.HasTimedOut = true;
                entry.StatusText = "No response";
            }
            else
            {
                pendingCount += 1;
            }
        }

        if (_lobbyBrowserOpen)
        {
            if (_lobbyBrowserEntries.Count == 0 && _lobbyBrowserLobbyClient is not null)
            {
                _menuStatusMessage = "Contacting lobby server...";
            }
            else
            {
                _menuStatusMessage = pendingCount > 0
                    ? $"Refreshing server list... ({pendingCount} pending)"
                    : string.Empty;
            }
        }
    }

    private static bool EndpointsEqual(IPEndPoint left, IPEndPoint right)
    {
        return left.Address.Equals(right.Address) && left.Port == right.Port;
    }

}
