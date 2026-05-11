using OpenGarrison.Core;

namespace OpenGarrison.ClientShared;

public static class GameplayPackAssetPathUtility
{
    public static string GetPackContentRoot(string packId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        return $"{NormalizePath(ContentRoot.Path)}/Gameplay/{packId}";
    }

    public static string GetSpriteDefinitionPath(string packId, string spriteName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteName);
        return BuildPackAssetPath(packId, $"sprites/{spriteName}.json");
    }

    public static string BuildPackAssetPath(string packId, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalizedPath = NormalizePath(relativePath);
        return IsContentRootRelativePath(normalizedPath)
            ? normalizedPath
            : $"{GetPackContentRoot(packId)}/{normalizedPath}";
    }

    public static bool IsContentRootRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return NormalizePath(path).StartsWith("Content/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Trim('/', '\\').Replace('\\', '/');
    }
}
