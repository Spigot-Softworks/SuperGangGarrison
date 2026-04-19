using OpenGarrison.Core;
using System.IO;

namespace OpenGarrison.BotAI;

internal static class ClientBotNavMeshResolver
{
    public static bool TryResolveCacheKey(SimpleLevel level, out string cacheKey, out string? originalMeshPath)
    {
        ArgumentNullException.ThrowIfNull(level);

        if (OriginalClientBotNavMeshStore.TryResolveMeshPath(level, out var meshPath))
        {
            var fileInfo = new FileInfo(meshPath);
            cacheKey = $"{level.Name}|{level.MapAreaIndex}|clientbot-navpoints-original|{fileInfo.Name}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}";
            originalMeshPath = meshPath;
            return true;
        }

        cacheKey = $"{level.Name}|{level.MapAreaIndex}|clientbot-navpoints|{level.Bounds.Width}x{level.Bounds.Height}|{level.Solids.Count}";
        originalMeshPath = null;
        return false;
    }
}
