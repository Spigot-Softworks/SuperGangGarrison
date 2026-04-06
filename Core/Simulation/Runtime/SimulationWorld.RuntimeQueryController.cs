namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed class RuntimeQueryController
    {
        private readonly SimulationWorld _world;

        public RuntimeQueryController(SimulationWorld world)
        {
            _world = world;
        }

        public int CountPlayers(PlayerTeam team)
        {
            var count = 0;
            foreach (var player in _world.EnumerateSimulatedPlayers())
            {
                if (!_world.TryGetNetworkPlayerSlot(player, out var slot) || _world.IsNetworkPlayerAwaitingJoin(slot))
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
            foreach (var player in _world.EnumerateSimulatedPlayers())
            {
                if (_world.TryGetNetworkPlayerSlot(player, out var slot) && _world.IsNetworkPlayerAwaitingJoin(slot))
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
            var captureZones = _world.Level.GetRoomObjects(RoomObjectType.CaptureZone);
            if (captureZones.Count == 0)
            {
                return 0;
            }

            var count = 0;
            foreach (var player in _world.EnumerateSimulatedPlayers())
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
}
