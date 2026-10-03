namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool QueryHasLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam? team = null)
    {
        return team.HasValue
            ? GeometryResolver.HasDirectLineOfSight(originX, originY, targetX, targetY, team.Value)
            : GeometryResolver.HasObstacleLineOfSight(originX, originY, targetX, targetY);
    }
}
