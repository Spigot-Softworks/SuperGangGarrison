namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public void AdvanceOneTick()
    {
        if (AdvancePendingMapChange())
        {
            Frame += 1;
            return;
        }

        AdvancePrePlayerSimulationPhase();
        AdvancePlayerSimulationPhase();
        AdvancePostPlayerSimulationPhase();
        _previousLocalInput = _localInput;
        Frame += 1;
    }

    private void AdvanceNetworkPlayerChatBubbleState(byte slot)
    {
        if (TryGetNetworkPlayer(slot, out var player))
        {
            player.AdvanceChatBubbleState();
        }
    }

    private int CountPlayers(PlayerTeam team)
    {
        var count = 0;
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!TryGetNetworkPlayerSlot(player, out var slot) || IsNetworkPlayerAwaitingJoin(slot))
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

    private int CountAlivePlayers(PlayerTeam team)
    {
        var count = 0;
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (TryGetNetworkPlayerSlot(player, out var slot) && IsNetworkPlayerAwaitingJoin(slot))
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

    private int CountPlayersInArenaCaptureZone(PlayerTeam team)
    {
        var captureZones = Level.GetRoomObjects(RoomObjectType.CaptureZone);
        if (captureZones.Count == 0)
        {
            return 0;
        }

        var count = 0;
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive || player.Team != team)
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
