using System.Globalization;
using System.Linq;

namespace OpenGarrison.Core;

public static class GarrisonBuilderQuickTestNaming
{
    public const string FolderPrefix = "_garrison_quicktest_";

    public static string SanitizeDocumentName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return "map";
        }

        var chars = trimmed
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '_')
            .ToArray();
        var sanitized = new string(chars).Trim('_');
        return sanitized.Length == 0 ? "map" : sanitized;
    }

    /// <summary>
    /// True for the builder's quick test copies of maps. They are working files, so map lists
    /// (Practice, hosting, menu backgrounds) leave them out.
    /// </summary>
    public static bool IsQuickTestLevel(string? levelName)
    {
        return levelName is not null
            && levelName.TrimStart().StartsWith(FolderPrefix, System.StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildQuickTestLevelName(string? documentName)
    {
        return FolderPrefix + SanitizeDocumentName(documentName);
    }
}
