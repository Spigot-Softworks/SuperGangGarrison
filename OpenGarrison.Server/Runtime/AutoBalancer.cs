using System;
using System.Collections.Generic;
using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

sealed class AutoBalancer
{
    private readonly SimulationWorld _world;
    private readonly SimulationConfig _config;
    private readonly Dictionary<byte, ClientSession> _clientsBySlot;
    private readonly int _autoBalanceDelaySeconds;
    private readonly int _autoBalanceNewPlayerGraceSeconds;
    private readonly bool _passwordRequired;
    private readonly Action<IPEndPoint, IProtocolMessage> _sendMessage;
    private readonly Action<string> _log;
    private int _autoBalanceCountdownTicks = -1;
    private PlayerTeam? _autoBalanceTeam;

    public AutoBalancer(
        SimulationWorld world,
        SimulationConfig config,
        Dictionary<byte, ClientSession> clientsBySlot,
        int autoBalanceDelaySeconds,
        int autoBalanceNewPlayerGraceSeconds,
        bool passwordRequired,
        Action<IPEndPoint, IProtocolMessage> sendMessage,
        Action<string> log)
    {
        _world = world;
        _config = config;
        _clientsBySlot = clientsBySlot;
        _autoBalanceDelaySeconds = autoBalanceDelaySeconds;
        _autoBalanceNewPlayerGraceSeconds = autoBalanceNewPlayerGraceSeconds;
        _passwordRequired = passwordRequired;
        _sendMessage = sendMessage;
        _log = log;
    }

    public void Tick(TimeSpan now, int ticksElapsed, bool autoBalanceEnabled)
    {
        if (!autoBalanceEnabled || _world.MatchRules.Mode == GameModeKind.Arena || _world.MatchState.IsEnded)
        {
            _autoBalanceCountdownTicks = -1;
            _autoBalanceTeam = null;
            return;
        }

        var redCount = 0;
        var blueCount = 0;
        foreach (var entry in _world.EnumerateActiveNetworkPlayers())
        {
            if (entry.Player.Team == PlayerTeam.Red)
            {
                redCount += 1;
            }
            else if (entry.Player.Team == PlayerTeam.Blue)
            {
                blueCount += 1;
            }
        }

        PlayerTeam? targetTeam = null;
        if (redCount >= blueCount + 2)
        {
            targetTeam = PlayerTeam.Red;
        }
        else if (blueCount >= redCount + 2)
        {
            targetTeam = PlayerTeam.Blue;
        }

        if (!targetTeam.HasValue)
        {
            _autoBalanceCountdownTicks = -1;
            _autoBalanceTeam = null;
            return;
        }

        if (_autoBalanceTeam != targetTeam)
        {
            _autoBalanceTeam = targetTeam;
            _autoBalanceCountdownTicks = _autoBalanceDelaySeconds * _config.TicksPerSecond;
            _log($"[server] auto-balance pending: {targetTeam.Value} has {Math.Max(redCount, blueCount)} players.");
            SendAutoBalanceNotice(new AutoBalanceNoticeMessage(
                AutoBalanceNoticeKind.Pending,
                string.Empty,
                (byte)targetTeam.Value,
                (byte)(targetTeam.Value == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red),
                _autoBalanceDelaySeconds));
            return;
        }

        if (_autoBalanceCountdownTicks > 0)
        {
            _autoBalanceCountdownTicks = Math.Max(0, _autoBalanceCountdownTicks - ticksElapsed);
            return;
        }

        var candidateSlot = (byte)0;
        PlayerEntity? candidate = null;
        var bestScore = int.MaxValue;
        foreach (var entry in _world.EnumerateActiveNetworkPlayers())
        {
            if (entry.Player.Team != targetTeam || !entry.Player.IsAlive)
            {
                continue;
            }

            if (_clientsBySlot.TryGetValue(entry.Slot, out var candidateClient))
            {
                var connectedSeconds = (now - candidateClient.ConnectedAt).TotalSeconds;
                if (connectedSeconds < _autoBalanceNewPlayerGraceSeconds)
                {
                    continue;
                }
            }

            var score = entry.Player.Kills + (entry.Player.Caps * 2) + entry.Player.HealPoints;
            if (score < bestScore)
            {
                bestScore = score;
                candidateSlot = entry.Slot;
                candidate = entry.Player;
            }
        }

        if (candidateSlot == 0 || candidate is null)
        {
            _autoBalanceCountdownTicks = _config.TicksPerSecond * 2;
            return;
        }

        var newTeam = targetTeam == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        _world.TrySetNetworkPlayerTeam(candidateSlot, newTeam);
        _world.ForceKillNetworkPlayer(candidateSlot);
        _log($"[server] auto-balance moved \"{candidate.DisplayName}\" slot={candidateSlot} to {newTeam}.");
        SendAutoBalanceNotice(new AutoBalanceNoticeMessage(
            AutoBalanceNoticeKind.Applied,
            candidate.DisplayName,
            (byte)targetTeam.Value,
            (byte)newTeam,
            0));

        _autoBalanceCountdownTicks = -1;
        _autoBalanceTeam = null;
    }

    private void SendAutoBalanceNotice(AutoBalanceNoticeMessage notice)
    {
        foreach (var client in _clientsBySlot.Values)
        {
            if (!client.IsAuthorized && _passwordRequired)
            {
                continue;
            }

            _sendMessage(client.EndPoint, notice);
        }
    }
}
