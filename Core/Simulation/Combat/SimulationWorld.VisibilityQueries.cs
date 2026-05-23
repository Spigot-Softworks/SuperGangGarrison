namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool QueryHasLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam? team = null)
    {
        return team.HasValue
            ? HasDirectLineOfSight(originX, originY, targetX, targetY, team.Value)
            : HasObstacleLineOfSight(originX, originY, targetX, targetY);
    }
}
