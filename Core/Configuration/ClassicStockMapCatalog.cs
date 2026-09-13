namespace OpenGarrison.Core;

/// <summary>Explicit map identities keep original and redrawn geometry distinct over the network.</summary>
public static class ClassicStockMapCatalog
{
    public readonly record struct Variant(string ReplacedLevelName, string LevelName, string DisplayName,
        GameModeKind Mode, string RelativeSourcePath);

    public static IReadOnlyList<Variant> Variants { get; } =
    [
        new("cp_coldfront_js", "gg2_cp_coldfront_v7", "Coldfront", GameModeKind.ControlPoint,
            "StockMaps/Classic/cp_coldfront_v7/cp_coldfront_v7.json"),
        new("Kulay", "gg2_dkoth_kulay", "Kulay", GameModeKind.DoubleKingOfTheHill,
            "StockMaps/Classic/dkoth_kulay/dkoth_kulay.json"),
        new("Harvest", "gg2_koth_harvest", "Harvest", GameModeKind.KingOfTheHill,
            "StockMaps/Classic/koth_harvest.png"),
        new("Docking", "gg2_cp_docking_v2", "Docking", GameModeKind.ControlPoint,
            "StockMaps/Classic/cp_docking_v2.png"),
        new("Conflict", "gg2_ctf_conflict", "Conflict", GameModeKind.CaptureTheFlag,
            "StockMaps/Classic/ctf_conflict.png"),
    ];

    public static string GetPreferredLevelName(string levelName, bool preferClassicMaps)
    {
        if (!preferClassicMaps) return levelName;
        var canonicalName = OpenGarrisonStockMapCatalog.TryGetDefinition(levelName, out var definition)
            ? definition.LevelName : levelName;
        foreach (var variant in Variants)
            if (variant.ReplacedLevelName.Equals(canonicalName, StringComparison.OrdinalIgnoreCase))
                return variant.LevelName;
        return levelName;
    }

    public static bool TryGetVariant(string levelName, out Variant variant)
    {
        foreach (var candidate in Variants)
            if (candidate.LevelName.Equals(levelName, StringComparison.OrdinalIgnoreCase))
            { variant = candidate; return true; }
        variant = default;
        return false;
    }
}
