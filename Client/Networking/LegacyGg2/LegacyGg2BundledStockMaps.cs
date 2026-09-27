using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class LegacyGg2BundledStockMaps
{
    internal static bool TryRegister(string mapName, out string levelName, string? directory = null)
    {
        levelName = string.Empty;
        if (string.IsNullOrWhiteSpace(mapName)
            || !mapName.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'))
        {
            return false;
        }

        var normalizedMapName = mapName.ToLowerInvariant();
        var mapPath = Path.Combine(directory ?? ContentRoot.GetPath("StockMaps", "Gg2"), normalizedMapName + ".png");
        if (!File.Exists(mapPath))
        {
            return false;
        }

        levelName = "gg2_stock_" + normalizedMapName;
        SimpleLevelFactory.RegisterExternalLegacyPngLevel(levelName, mapPath, mapName);
        return SimpleLevelFactory.CreateImportedLevel(levelName) is not null;
    }
}
