#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static readonly HashSet<string> SuperGangGarrisonMapDisplayNames = new(
        ["Coldfront", "Kulay", "Harvest", "Docking", "Conflict"],
        StringComparer.OrdinalIgnoreCase);

    internal static bool IsSuperGangGarrisonMap(string levelName, string displayName)
    {
        if (ClassicStockMapCatalog.TryGetVariant(levelName, out _))
        {
            return false;
        }

        return SuperGangGarrisonMapDisplayNames.Contains(displayName);
    }

    internal static IReadOnlyList<string> GetSuperGangGarrisonShowcaseMapNames()
    {
        return OpenGarrisonStockMapCatalog.Definitions
            .Where(static definition => IsSuperGangGarrisonMap(definition.LevelName, definition.DisplayName))
            .Select(static definition => definition.LevelName)
            .ToArray();
    }
}
