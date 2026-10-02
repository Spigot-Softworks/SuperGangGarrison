namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    /// <summary>
    /// Presentation-only sweep through the same solids and team blockers as
    /// authoritative projectile collision. It never damages or moves an entity.
    /// </summary>
    public (float X, float Y) ClampProjectilePresentationPath(
        PlayerTeam team, float startX, float startY, float endX, float endY, float collisionBackoff = 0f)
        => GeometryResolver.ClampProjectilePresentationPath(team, startX, startY, endX, endY, collisionBackoff);
}
