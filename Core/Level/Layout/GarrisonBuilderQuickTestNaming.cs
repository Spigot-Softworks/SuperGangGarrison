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

    public static string BuildQuickTestLevelName(string? documentName)
    {
        return FolderPrefix + SanitizeDocumentName(documentName);
    }
}
