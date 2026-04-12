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
        return $"{GetPackContentRoot(packId)}/{NormalizePath(relativePath)}";
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Trim('/', '\\').Replace('\\', '/');
    }
}
