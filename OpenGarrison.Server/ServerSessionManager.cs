using System;
using System.Collections.Generic;
using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using static ServerHelpers;

sealed class ServerSessionManager
{
    private readonly SimulationWorld _world;
    private readonly Dictionary<byte, ClientSession> _clientsBySlot;
    private readonly int _maxPlayableClients;
    private readonly int _maxTotalClients;
    private readonly int _maxSpectatorClients;
    private readonly Func<TimeSpan> _nowProvider;
    private readonly string? _serverPassword;
    private readonly bool _passwordRequired;
    private readonly double _clientTimeoutSeconds;
    private readonly double _passwordTimeoutSeconds;
    private readonly double _passwordRetrySeconds;
    private readonly Func<IPEndPoint, string?> _getPasswordRateLimitReason;
    private readonly Action<IPEndPoint> _recordPasswordFailure;
    private readonly Action<IPEndPoint> _clearPasswordFailures;
    private readonly Action<IPEndPoint, IProtocolMessage> _sendMessage;
    private readonly Action<string> _log;
    private readonly Action<ClientSession, string> _clientRemoved;
    private readonly Action<ClientSession> _passwordAccepted;
    private readonly Action<ClientSession, PlayerTeam> _playerTeamChanged;
    private readonly Action<ClientSession, PlayerClass> _playerClassChanged;

    public ServerSessionManager(
        SimulationWorld world,
        Dictionary<byte, ClientSession> clientsBySlot,
        int maxPlayableClients,
        int maxTotalClients,
        int maxSpectatorClients,
        Func<TimeSpan> nowProvider,
        string? serverPassword,
        bool passwordRequired,
        double clientTimeoutSeconds,
        double passwordTimeoutSeconds,
        double passwordRetrySeconds,
        Func<IPEndPoint, string?> getPasswordRateLimitReason,
        Action<IPEndPoint> recordPasswordFailure,
        Action<IPEndPoint> clearPasswordFailures,
        Action<IPEndPoint, IProtocolMessage> sendMessage,
        Action<string> log,
        Action<ClientSession, string>? clientRemoved = null,
        Action<ClientSession>? passwordAccepted = null,
        Action<ClientSession, PlayerTeam>? playerTeamChanged = null,
        Action<ClientSession, PlayerClass>? playerClassChanged = null)
    {
        _world = world;
        _clientsBySlot = clientsBySlot;
        _maxPlayableClients = maxPlayableClients;
        _maxTotalClients = maxTotalClients;
        _maxSpectatorClients = maxSpectatorClients;
        _nowProvider = nowProvider;
        _serverPassword = serverPassword;
        _passwordRequired = passwordRequired;
        _clientTimeoutSeconds = clientTimeoutSeconds;
        _passwordTimeoutSeconds = passwordTimeoutSeconds;
        _passwordRetrySeconds = passwordRetrySeconds;
        _getPasswordRateLimitReason = getPasswordRateLimitReason;
        _recordPasswordFailure = recordPasswordFailure;
        _clearPasswordFailures = clearPasswordFailures;
        _sendMessage = sendMessage;
        _log = log;
        _clientRemoved = clientRemoved ?? ((_, _) => { });
        _passwordAccepted = passwordAccepted ?? (_ => { });
        _playerTeamChanged = playerTeamChanged ?? ((_, _) => { });
        _playerClassChanged = playerClassChanged ?? ((_, _) => { });
    }

    public void ApplyClientName(byte slot, string name)
    {
        _world.TrySetNetworkPlayerName(slot, name);
    }

    public void PreparePlayableClientInputsForNextTick()
    {
        for (var index = 0; index < SimulationWorld.NetworkPlayerSlots.Count; index += 1)
        {
            var slot = SimulationWorld.NetworkPlayerSlots[index];
            if (_clientsBySlot.TryGetValue(slot, out var client)
                && client.IsAuthorized
                && client.TryGetInputForNextTick(out var input))
            {
                _world.TrySetNetworkPlayerInput(slot, input);
            }
            else
            {
                _world.TryClearNetworkPlayerInputOverride(slot);
            }
        }
    }

    public void HandleControlCommand(ClientSession client, ControlCommandMessage command)
    {
        if (_passwordRequired && !client.IsAuthorized)
        {
            _sendMessage(client.EndPoint, new ControlAckMessage(command.Sequence, command.Kind, false));
            return;
        }

        var previousSequence = command.Kind switch
        {
            ControlCommandKind.SelectTeam => client.LastTeamCommandSequence,
            ControlCommandKind.SelectClass => client.LastClassCommandSequence,
            ControlCommandKind.Spectate => client.LastSpectateCommandSequence,
            _ => 0u,
        };
        if (previousSequence == command.Sequence)
        {
            _sendMessage(client.EndPoint, new ControlAckMessage(command.Sequence, command.Kind, true));
            return;
        }

        var accepted = command.Kind switch
        {
            ControlCommandKind.SelectTeam => ApplyRequestedTeam(client, command.Value),
            ControlCommandKind.SelectClass => ApplyRequestedClass(client.Slot, command.Value),
            ControlCommandKind.Spectate => ApplyRequestedSpectate(client),
            _ => false,
        };

        if (accepted)
        {
            if (command.Kind == ControlCommandKind.SelectTeam)
            {
                client.LastTeamCommandSequence = command.Sequence;
            }
            else if (command.Kind == ControlCommandKind.SelectClass)
            {
                client.LastClassCommandSequence = command.Sequence;
            }
            else if (command.Kind == ControlCommandKind.Spectate)
            {
                client.LastSpectateCommandSequence = command.Sequence;
            }
        }

        _sendMessage(client.EndPoint, new ControlAckMessage(command.Sequence, command.Kind, accepted));
    }

    public void HandlePasswordSubmit(ClientSession client, PasswordSubmitMessage passwordSubmit)
    {
        if (!_passwordRequired)
        {
            client.IsAuthorized = true;
            _clearPasswordFailures(client.EndPoint);
            _sendMessage(client.EndPoint, new PasswordResultMessage(true, string.Empty));
            _passwordAccepted(client);
            return;
        }

        if (_getPasswordRateLimitReason(client.EndPoint) is { } rateLimitReason)
        {
            _sendMessage(client.EndPoint, new PasswordResultMessage(false, rateLimitReason));
            RemoveClient(client.Slot, "password rate limited");
            return;
        }

        if (string.Equals(passwordSubmit.Password, _serverPassword, StringComparison.Ordinal))
        {
            client.IsAuthorized = true;
            _clearPasswordFailures(client.EndPoint);
            _sendMessage(client.EndPoint, new PasswordResultMessage(true, string.Empty));
            _log($"[server] client authorized slot={client.Slot} endpoint={client.EndPoint}");
            _passwordAccepted(client);
            return;
        }

        _recordPasswordFailure(client.EndPoint);
        _sendMessage(client.EndPoint, new PasswordResultMessage(false, "Incorrect password."));
        RemoveClient(client.Slot, "bad password");
    }

    public void RemoveClient(byte slot, string reason)
    {
        if (!_clientsBySlot.Remove(slot, out var removedClient))
        {
            return;
        }

        _log($"[server] client removed slot={slot} endpoint={removedClient.EndPoint} reason={reason}");
        _clientRemoved(removedClient, reason);
        if (SimulationWorld.IsPlayableNetworkPlayerSlot(slot))
        {
            _world.TryReleaseNetworkPlayerSlot(slot);
        }
    }

    public bool TryMoveClientToSpectator(byte slot)
    {
        return _clientsBySlot.TryGetValue(slot, out var client)
            && ApplyRequestedSpectate(client);
    }

    public bool TrySetClientTeam(byte slot, PlayerTeam team)
    {
        return _clientsBySlot.TryGetValue(slot, out var client)
            && ApplyRequestedTeam(client, (byte)team);
    }

    public bool TrySetClientClass(byte slot, PlayerClass playerClass)
    {
        return _clientsBySlot.ContainsKey(slot)
            && ApplyRequestedClass(slot, (byte)playerClass);
    }

    public void PruneTimedOutClients()
    {
        if (_clientsBySlot.Count == 0)
        {
            return;
        }

        var now = _nowProvider();
        var staleSlots = new List<byte>();
        foreach (var entry in _clientsBySlot)
        {
            if ((now - entry.Value.LastSeen).TotalSeconds >= GetEffectiveClientTimeoutSeconds(entry.Value))
            {
                staleSlots.Add(entry.Key);
            }
        }

        foreach (var slot in staleSlots)
        {
            RemoveClient(slot, "timeout");
        }
    }

    public void RefreshPasswordRequests()
    {
        if (!_passwordRequired || _clientsBySlot.Count == 0)
        {
            return;
        }

        var now = _nowProvider();
        var toRemove = new List<byte>();
        foreach (var entry in _clientsBySlot)
        {
            var client = entry.Value;
            if (client.IsAuthorized)
            {
                continue;
            }

            if ((now - client.ConnectedAt).TotalSeconds >= _passwordTimeoutSeconds)
            {
                _sendMessage(client.EndPoint, new ConnectionDeniedMessage("Password entry timed out."));
                toRemove.Add(entry.Key);
                continue;
            }

            if ((now - client.LastPasswordRequestSentAt).TotalSeconds >= _passwordRetrySeconds)
            {
                _sendMessage(client.EndPoint, new PasswordRequestMessage());
                client.LastPasswordRequestSentAt = now;
            }
        }

        foreach (var slot in toRemove)
        {
            RemoveClient(slot, "password timeout");
        }
    }

    private double GetEffectiveClientTimeoutSeconds(ClientSession client)
    {
        var address = client.EndPoint.Address;
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return IPAddress.IsLoopback(address)
            ? Math.Max(_clientTimeoutSeconds, 30d)
            : _clientTimeoutSeconds;
    }

    private bool ApplyRequestedTeamForSlot(byte slot, byte requestedTeam)
    {
        if (requestedTeam > (byte)PlayerTeam.Blue)
        {
            return false;
        }

        var team = (PlayerTeam)requestedTeam;
        if (!_world.TrySetNetworkPlayerTeam(slot, team))
        {
            return false;
        }

        if (_clientsBySlot.TryGetValue(slot, out var client))
        {
            _playerTeamChanged(client, team);
        }

        return true;
    }

    private bool ApplyRequestedClass(byte slot, byte requestedClass)
    {
        if (!Enum.IsDefined(typeof(PlayerClass), (int)requestedClass))
        {
            return false;
        }

        var playerClass = (PlayerClass)requestedClass;
        if (!_world.TryApplyNetworkPlayerClassSelection(slot, playerClass))
        {
            return false;
        }

        if (_clientsBySlot.TryGetValue(slot, out var client))
        {
            _playerClassChanged(client, playerClass);
        }

        return true;
    }

    private bool TryMoveClientToSlot(ClientSession client, byte newSlot)
    {
        if (client.Slot == newSlot)
        {
            return true;
        }

        if (_clientsBySlot.ContainsKey(newSlot))
        {
            return false;
        }

        if (SimulationWorld.IsPlayableNetworkPlayerSlot(client.Slot))
        {
            _world.TryReleaseNetworkPlayerSlot(client.Slot);
        }

        _clientsBySlot.Remove(client.Slot);
        client.Slot = newSlot;
        _clientsBySlot[newSlot] = client;

        if (SimulationWorld.IsPlayableNetworkPlayerSlot(newSlot))
        {
            _world.TryPrepareNetworkPlayerJoin(newSlot);
            _world.TrySetNetworkPlayerName(newSlot, client.Name);
        }

        _sendMessage(client.EndPoint, new SessionSlotChangedMessage(newSlot));
        _log($"[server] client moved endpoint={client.EndPoint} slot={newSlot}");
        return true;
    }

    private bool ApplyRequestedTeam(ClientSession client, byte requestedTeam)
    {
        if (requestedTeam > (byte)PlayerTeam.Blue)
        {
            return false;
        }

        if (IsSpectatorSlot(client.Slot))
        {
            var playableSlot = FindAvailablePlayableSlot(_clientsBySlot, _maxPlayableClients);
            if (playableSlot == 0 || !TryMoveClientToSlot(client, playableSlot))
            {
                return false;
            }
        }

        var team = (PlayerTeam)requestedTeam;
        return ApplyRequestedTeamForSlot(client.Slot, requestedTeam);
    }

    private bool ApplyRequestedSpectate(ClientSession client)
    {
        if (IsSpectatorSlot(client.Slot))
        {
            return true;
        }

        var spectatorSlot = FindAvailableSpectatorSlot(_clientsBySlot, _maxTotalClients, _maxSpectatorClients, client.Slot);
        return spectatorSlot != 0 && TryMoveClientToSlot(client, spectatorSlot);
    }
}
