namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private int[] _teleportZoneIndexByPlayerId = [];

    private void ResetTeleportTracking()
    {
        if (_teleportZoneIndexByPlayerId.Length > 0)
        {
            Array.Fill(_teleportZoneIndexByPlayerId, -1);
        }
    }

    private void EnsureTeleportTrackingCapacity(int playerId)
    {
        if (playerId < 0)
        {
            return;
        }

        var requiredLength = playerId + 1;
        if (_teleportZoneIndexByPlayerId.Length >= requiredLength)
        {
            return;
        }

        var expanded = new int[requiredLength];
        for (var index = 0; index < expanded.Length; index += 1)
        {
            expanded[index] = -1;
        }

        if (_teleportZoneIndexByPlayerId.Length > 0)
        {
            Array.Copy(_teleportZoneIndexByPlayerId, expanded, _teleportZoneIndexByPlayerId.Length);
        }

        _teleportZoneIndexByPlayerId = expanded;
    }

    private void ApplyTeleportZones(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return;
        }

        EnsureTeleportTrackingCapacity(player.Id);
        var zoneIndex = FindTeleportZoneIndexContainingPlayer(player);
        var previousZoneIndex = _teleportZoneIndexByPlayerId[player.Id];
        _teleportZoneIndexByPlayerId[player.Id] = zoneIndex;
        if (zoneIndex < 0 || zoneIndex == previousZoneIndex)
        {
            return;
        }

        if (!AreaExtensionMetadata.TryGetEffectiveTeleportZone(Level.RoomObjects, zoneIndex, out var zone)
            || !zone.TeleportZone.HasExit)
        {
            return;
        }

        player.TeleportTo(zone.TeleportZone.ExitX, zone.TeleportZone.ExitY);
    }

    private int FindTeleportZoneIndexContainingPlayer(PlayerEntity player)
    {
        for (var index = 0; index < Level.RoomObjects.Count; index += 1)
        {
            if (!Level.IsRoomObjectActive(index))
            {
                continue;
            }

            var marker = Level.RoomObjects[index];
            if (marker.Type is not (RoomObjectType.TeleportZone or RoomObjectType.AreaExtension))
            {
                continue;
            }

            if (!AreaExtensionMetadata.TryGetEffectiveTeleportZone(Level.RoomObjects, index, out var teleportZone))
            {
                continue;
            }

            if (!TeleportMetadata.AllowsTeam(teleportZone.TeleportZone.TeamFilter, player.Team))
            {
                continue;
            }

            if (!teleportZone.TeleportZone.HasExit)
            {
                continue;
            }

            if (AreaExtensionMetadata.IsPointInsideMarker(player.X, player.Y, marker))
            {
                return index;
            }
        }

        return -1;
    }
}
