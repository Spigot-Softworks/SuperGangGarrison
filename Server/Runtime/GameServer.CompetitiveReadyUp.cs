using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Core;

partial class GameServer
{
    private void ProcessCompetitiveReadyUpBeforeSimulationTick()
    {
        if (!_world.ReadyUp.CompetitiveReadyUpEnabled)
        {
            _competitiveReadyButtonDownSlots.Clear();
            return;
        }

        var playableSlots = GetCompetitiveReadyUpPlayableSlots();
        ProcessCompetitiveReadyButtonEdges(playableSlots);
        _world.ReadyUp.AdvanceCompetitiveReadyUp(playableSlots);
    }

    private List<byte> GetCompetitiveReadyUpPlayableSlots()
    {
        var slots = new List<byte>(_clientsBySlot.Count);
        foreach (var client in _clientsBySlot.Values)
        {
            if (!client.IsAuthorized
                || client.IsWatchOnly
                || !NetworkPlayerSystem.IsPlayableNetworkPlayerSlot(client.Slot)
                || _world.NetworkPlayers.IsNetworkPlayerAwaitingJoin(client.Slot))
            {
                continue;
            }

            slots.Add(client.Slot);
        }

        slots.Sort();
        return slots;
    }

    private void ProcessCompetitiveReadyButtonEdges(IReadOnlyCollection<byte> playableSlots)
    {
        _competitiveReadyButtonDownSlots.RemoveWhere(slot => !playableSlots.Contains(slot));
        foreach (var slot in playableSlots)
        {
            if (!_clientsBySlot.TryGetValue(slot, out var client))
            {
                continue;
            }

            var readyDown = client.LatestAppliedInput.ReadyUp;
            var wasDown = _competitiveReadyButtonDownSlots.Contains(slot);
            if (readyDown)
            {
                _competitiveReadyButtonDownSlots.Add(slot);
                if (!wasDown)
                {
                    _world.ReadyUp.TryToggleNetworkPlayerReady(slot);
                }

                continue;
            }

            _competitiveReadyButtonDownSlots.Remove(slot);
        }
    }
}
