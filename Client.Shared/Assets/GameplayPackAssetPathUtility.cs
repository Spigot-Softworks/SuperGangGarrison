using OpenGarrison.Core;

namespace OpenGarrison.ClientShared;

public static class GameplayPackAssetPathUtility
{
    public static string GetPackContentRoot(string packId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        if (packId.IndexOfAny(['/', '\\']) >= 0 || packId is "." or "..")
        {
            throw new ArgumentException("Gameplay pack IDs must be a single directory name.", nameof(packId));
        }

        return $"{NormalizePath(ContentRoot.Path)}/Gameplay/{packId}";
    }

    public static string GetSpriteDefinitionPath(string packId, string spriteName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteName);
        return BuildPackAssetPath(packId, $"sprites/{spriteName}.json");
    }

    public static string BuildPackAssetPath(string packId, string relativePath)
    {
        return $"{GetPackContentRoot(packId)}/{NormalizePackRelativePath(relativePath)}";
    }

    public static string NormalizePackRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalizedPath = relativePath.Trim().Replace('\\', '/');
        var pathSegments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (normalizedPath.StartsWith("Content/", StringComparison.OrdinalIgnoreCase)
            || Path.IsPathRooted(normalizedPath)
            || normalizedPath.StartsWith('/')
            || normalizedPath.Contains(':', StringComparison.Ordinal)
            || pathSegments.Any(static segment => segment is "." or ".."))
        {
            throw new ArgumentException("Gameplay asset paths must be relative to their pack.", nameof(relativePath));
        }

        return normalizedPath;
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Trim('/', '\\').Replace('\\', '/');
    }
}
