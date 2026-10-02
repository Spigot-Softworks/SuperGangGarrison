namespace OpenGarrison.Core;

internal sealed class PlayerCountQueries
{
    private readonly IPlayerCountHost _host;

    public PlayerCountQueries(IPlayerCountHost host)
    {
        _host = host;
    }

    public int CountPlayers(PlayerTeam team)
    {
        var count = 0;
        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (!_host.TryGetNetworkPlayerSlot(player, out var slot) || _host.IsNetworkPlayerAwaitingJoin(slot))
            {
                continue;
            }

            if (player.Team == team)
            {
                count += 1;
            }
        }

        return count;
    }

    public int CountAlivePlayers(PlayerTeam team)
    {
        var count = 0;
        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (_host.TryGetNetworkPlayerSlot(player, out var slot) && _host.IsNetworkPlayerAwaitingJoin(slot))
            {
                continue;
            }

            if (player.Team == team && player.IsAlive)
            {
                count += 1;
            }
        }

        return count;
    }

    public int CountPlayersInArenaCaptureZone(PlayerTeam team)
    {
        var captureZones = _host.Level.GetRoomObjects(RoomObjectType.CaptureZone);
        if (captureZones.Count == 0)
        {
            return 0;
        }

        var count = 0;
        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive
                || player.Team != team
                || !_host.CanPlayerContributeToControlPoint(player))
            {
                continue;
            }

            for (var index = 0; index < captureZones.Count; index += 1)
            {
                var captureZone = captureZones[index];
                if (player.IntersectsMarker(captureZone.CenterX, captureZone.CenterY, captureZone.Width, captureZone.Height))
                {
                    count += 1;
                    break;
                }
            }
        }

        return count;
    }
}
