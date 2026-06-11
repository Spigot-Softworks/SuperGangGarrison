using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Core;

public static class LevelCatalogCollisions
{
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    public static IReadOnlyList<SimpleLevelFactory.LevelCatalogEntry> FindEntriesByName(
        IReadOnlyList<SimpleLevelFactory.LevelCatalogEntry> catalog,
        string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return Array.Empty<SimpleLevelFactory.LevelCatalogEntry>();
        }

        var trimmedName = mapName.Trim();
        return catalog
            .Where(entry => NameComparer.Equals(entry.Name, trimmedName))
            .ToArray();
    }

    public static IReadOnlyList<SimpleLevelFactory.LevelCatalogEntry> FindStockEntriesByName(
        IReadOnlyList<SimpleLevelFactory.LevelCatalogEntry> catalog,
        string mapName)
    {
        return FindEntriesByName(catalog, mapName)
            .Where(entry => !entry.IsCustomMap)
            .ToArray();
    }

    public static bool HasDuplicateMapName(
        IReadOnlyList<SimpleLevelFactory.LevelCatalogEntry> catalog,
        string mapName)
    {
        return FindEntriesByName(catalog, mapName).Count >= 2;
    }

    public static bool ShouldWarnAboutMapNameCollision(
        IReadOnlyList<SimpleLevelFactory.LevelCatalogEntry> catalog,
        string mapName,
        string? currentMapSourcePath = null)
    {
        var stockMatches = FindStockEntriesByName(catalog, mapName);
        if (stockMatches.Count == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(currentMapSourcePath))
        {
            foreach (var entry in FindEntriesByName(catalog, mapName))
            {
                if (SourcePathsReferToSameMap(entry.RoomSourcePath, currentMapSourcePath))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static string BuildCollisionWarningMessage(
        IReadOnlyList<SimpleLevelFactory.LevelCatalogEntry> catalog,
        string mapName)
    {
        var trimmedName = mapName.Trim();
        var stockName = FindStockEntriesByName(catalog, trimmedName).FirstOrDefault().Name;
        var displayName = string.IsNullOrWhiteSpace(stockName) ? trimmedName : stockName;
        return $"A built-in map is already named \"{displayName}\".\nSave this map under a different name first.";
    }

    private static bool SourcePathsReferToSameMap(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
