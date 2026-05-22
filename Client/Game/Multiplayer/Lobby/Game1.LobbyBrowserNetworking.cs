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
        if (!entry.Endpoint.HasUdpEndpoint)
        {
            entry.CanJoinDirectly = entry.Endpoint.TryResolveForCurrentRuntime(out _, out _, out _);
            entry.HasTimedOut = false;
            entry.StatusText = entry.CanJoinDirectly ? "Ready" : "No endpoint";
            return;
        }

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

            entry.QueryEndPoint = new IPEndPoint(address, entry.Endpoint.UdpPort);
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
        UpdateLobbyBrowserRegistryState();
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
                    entry.ModeLabel = FormatGameModeLabel(status.GameMode);
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
            if (entry.HasResponse || entry.HasTimedOut || entry.CanJoinDirectly)
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

        if (_lobbyBrowserOpen && _lobbyBrowserPage == LobbyBrowserPage.List)
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

    private void OpenLobbyBrowserDetails(LobbyBrowserEntry entry)
    {
        _lobbyBrowserDetailsEntry = entry;
        _lobbyBrowserDetailsResponse = null;
        _lobbyBrowserDetailsStatus = "Loading server details...";
        _lobbyBrowserPage = LobbyBrowserPage.Details;
        RefreshLobbyBrowserDetails();
    }

    private void CloseLobbyBrowserDetails()
    {
        ClearLobbyBrowserDetails();
        _lobbyBrowserPage = LobbyBrowserPage.List;
    }

    private void RefreshLobbyBrowserDetails()
    {
        _lobbyBrowserDetailsTransport?.Dispose();
        _lobbyBrowserDetailsTransport = null;
        _lobbyBrowserDetailsRequestInFlight = false;
        _lobbyBrowserDetailsResponse = null;

        var entry = _lobbyBrowserDetailsEntry;
        if (entry is null)
        {
            _lobbyBrowserDetailsStatus = "No server selected.";
            return;
        }

        if (!entry.Endpoint.TryResolveForCurrentRuntime(out var host, out var port, out _))
        {
            _lobbyBrowserDetailsStatus = "Server cannot be reached from this runtime.";
            return;
        }

        if (!NetworkClientMessageTransportRegistry.TryConnect(host, port, out var transport, out var error) || transport is null)
        {
            _lobbyBrowserDetailsStatus = string.IsNullOrWhiteSpace(error)
                ? "Could not open details connection."
                : $"Details failed: {error}";
            return;
        }

        _lobbyBrowserDetailsTransport = transport;
        _lobbyBrowserDetailsRequestInFlight = true;
        _lobbyBrowserDetailsRequestStartedAtMilliseconds = Environment.TickCount64;
        _lobbyBrowserDetailsStatus = "Loading server details...";
        transport.Send(ProtocolCodec.Serialize(new ServerDetailsRequestMessage()));
    }

    private void UpdateLobbyBrowserDetailsState()
    {
        if (_lobbyBrowserPage != LobbyBrowserPage.Details
            || !_lobbyBrowserDetailsRequestInFlight
            || _lobbyBrowserDetailsTransport is null)
        {
            return;
        }

        while (_lobbyBrowserDetailsTransport.HasPendingMessages)
        {
            if (!_lobbyBrowserDetailsTransport.TryReceive(out var payload))
            {
                break;
            }

            if (!ProtocolCodec.TryDeserialize(payload, out var message)
                || message is not ServerDetailsResponseMessage details)
            {
                continue;
            }

            _lobbyBrowserDetailsResponse = details;
            _lobbyBrowserDetailsStatus = string.Empty;
            _lobbyBrowserDetailsRequestInFlight = false;
            _lobbyBrowserDetailsTransport.Dispose();
            _lobbyBrowserDetailsTransport = null;
            if (_lobbyBrowserDetailsEntry is { } entry)
            {
                entry.HasResponse = true;
                entry.HasTimedOut = false;
                entry.ServerName = details.ServerName;
                entry.LevelName = details.LevelName;
                entry.ModeLabel = FormatGameModeLabel(details.GameMode);
                entry.PlayerCount = details.PlayerCount;
                entry.MaxPlayerCount = details.MaxPlayerCount;
                entry.SpectatorCount = details.SpectatorCount;
                entry.StatusText = "Online";
            }

            return;
        }

        if (_lobbyBrowserDetailsTransport.TryConsumeDisconnectReason(out var disconnectReason))
        {
            _lobbyBrowserDetailsRequestInFlight = false;
            _lobbyBrowserDetailsTransport.Dispose();
            _lobbyBrowserDetailsTransport = null;
            _lobbyBrowserDetailsStatus = string.IsNullOrWhiteSpace(disconnectReason)
                ? "Server details unavailable."
                : disconnectReason;
            return;
        }

        if (Environment.TickCount64 - _lobbyBrowserDetailsRequestStartedAtMilliseconds >= LobbyBrowserDetailsQueryTimeoutMilliseconds)
        {
            _lobbyBrowserDetailsRequestInFlight = false;
            _lobbyBrowserDetailsTransport.Dispose();
            _lobbyBrowserDetailsTransport = null;
            _lobbyBrowserDetailsStatus = "Server details timed out.";
        }
    }

    private void ClearLobbyBrowserDetails()
    {
        _lobbyBrowserDetailsTransport?.Dispose();
        _lobbyBrowserDetailsTransport = null;
        _lobbyBrowserDetailsEntry = null;
        _lobbyBrowserDetailsResponse = null;
        _lobbyBrowserDetailsStatus = string.Empty;
        _lobbyBrowserDetailsRequestInFlight = false;
        _lobbyBrowserDetailsRequestStartedAtMilliseconds = 0;
    }

    private static string FormatGameModeLabel(byte gameMode)
    {
        return gameMode switch
        {
            (byte)GameModeKind.Arena => "Arena",
            (byte)GameModeKind.ControlPoint => "CP",
            (byte)GameModeKind.Generator => "Gen",
            (byte)GameModeKind.KingOfTheHill => "KOTH",
            (byte)GameModeKind.DoubleKingOfTheHill => "DKOTH",
            (byte)GameModeKind.TeamDeathmatch => "TDM",
            _ => "CTF",
        };
    }

    private static bool EndpointsEqual(IPEndPoint left, IPEndPoint right)
    {
        return left.Address.Equals(right.Address) && left.Port == right.Port;
    }

}
