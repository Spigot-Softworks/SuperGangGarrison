using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

internal static class ModernObstacleGeometry
{
    public static LevelSolid[] BuildStaticObstacles(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        return level.Solids.Count == 0 ? Array.Empty<LevelSolid>() : level.Solids.ToArray();
    }
}
