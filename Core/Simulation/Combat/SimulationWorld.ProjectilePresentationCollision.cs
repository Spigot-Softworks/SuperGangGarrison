namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    /// <summary>
    /// Presentation-only sweep through the same solids and team blockers as
    /// authoritative projectile collision. It never damages or moves an entity.
    /// </summary>
    public (float X, float Y) ClampProjectilePresentationPath(
        PlayerTeam team, float startX, float startY, float endX, float endY, float collisionBackoff = 0f)
    {
        var dx = endX - startX;
        var dy = endY - startY;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (!float.IsFinite(distance) || distance <= 0.0001f)
            return (startX, startY);
        var directionX = dx / distance;
        var directionY = dy / distance;
        var hitDistance = GeometryResolver.GetProjectilePresentationEnvironmentHit(team, startX, startY, directionX, directionY, distance);
        if (!hitDistance.HasValue)
            return (endX, endY);
        var travel = MathF.Max(0f, hitDistance.Value - MathF.Max(0f, collisionBackoff));
        return (startX + directionX * travel, startY + directionY * travel);
    }
}
