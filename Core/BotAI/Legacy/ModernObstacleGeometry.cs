using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

internal static class ModernObstacleGeometry
{
    public static LevelSolid[] BuildStaticObstacles(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        var solids = new List<LevelSolid>(level.Solids.Count + level.RoomObjects.Count);
        for (var index = 0; index < level.Solids.Count; index += 1)
        {
            solids.Add(level.Solids[index]);
        }

        for (var index = 0; index < level.RoomObjects.Count; index += 1)
        {
            var roomObject = level.RoomObjects[index];
            if (!IsStaticPlayerObstacle(roomObject, level))
            {
                continue;
            }

            solids.Add(new LevelSolid(roomObject.Left, roomObject.Top, roomObject.Width, roomObject.Height));
        }

        return solids.Count == 0 ? Array.Empty<LevelSolid>() : solids.ToArray();
    }

    public static LevelSolid[] BuildRuntimePlayerObstacles(SimpleLevel level, PlayerTeam team, bool carryingIntel)
    {
        ArgumentNullException.ThrowIfNull(level);
        var solids = new List<LevelSolid>(level.Solids.Count + level.RoomObjects.Count);
        for (var index = 0; index < level.Solids.Count; index += 1)
        {
            solids.Add(level.Solids[index]);
        }

        foreach (var gate in level.GetBlockingTeamGates(team, carryingIntel))
        {
            solids.Add(new LevelSolid(gate.Left, gate.Top, gate.Width, gate.Height));
        }

        foreach (var wall in level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            solids.Add(new LevelSolid(wall.Left, wall.Top, wall.Width, wall.Height));
        }

        return solids.Count == 0 ? Array.Empty<LevelSolid>() : solids.ToArray();
    }

    private static bool IsStaticPlayerObstacle(RoomObjectMarker roomObject, SimpleLevel level)
    {
        return roomObject.Type switch
        {
            RoomObjectType.PlayerWall => true,
            RoomObjectType.ControlPointSetupGate => level.ControlPointSetupGatesActive,
            _ => false,
        };
    }
}
