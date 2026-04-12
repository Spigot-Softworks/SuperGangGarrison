using System.Text.RegularExpressions;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class BrowserPluginFileSystem
{
    public static bool Exists(string path)
    {
        return TryGetRelativePath(path, out var relativePath)
            && BrowserContentCatalog.TryGetBinary(relativePath, out _);
    }

    public static bool TryReadAllBytes(string path, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        return TryGetRelativePath(path, out var relativePath)
            && BrowserContentCatalog.TryGetBinary(relativePath, out bytes);
    }

    public static bool TryReadAllText(string path, out string text)
    {
        text = string.Empty;
        return TryGetRelativePath(path, out var relativePath)
            && BrowserContentCatalog.TryGetText(relativePath, out text);
    }

    public static IReadOnlyList<string> EnumerateFiles(string directoryPath, string searchPattern)
    {
        if (!TryGetRelativePath(directoryPath, out var relativeDirectory))
        {
            return [];
        }

        var normalizedDirectory = relativeDirectory.TrimEnd('/');
        var directoryPrefix = normalizedDirectory.Length == 0 ? string.Empty : normalizedDirectory + "/";
        var matcher = CreateSearchPatternMatcher(searchPattern);
        return BrowserContentCatalog.GetBinaryPaths(directoryPrefix)
            .Where(path =>
            {
                var remainingPath = path[directoryPrefix.Length..];
                return remainingPath.Length > 0 && remainingPath.IndexOf('/') < 0;
            })
            .Where(path => matcher.IsMatch(Path.GetFileName(path)))
            .Select(path => Path.Combine(directoryPath, Path.GetFileName(path)))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryGetRelativePath(string path, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalizedPath = path.Replace('\\', '/').Trim();
        var segments = normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < segments.Length; index += 1)
        {
            if (!string.Equals(segments[index], "Plugins", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            relativePath = string.Join('/', segments.Skip(index));
            return relativePath.Length > 0;
        }

        if (normalizedPath.StartsWith("Plugins/", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = normalizedPath;
            return true;
        }

        return false;
    }

    private static Regex CreateSearchPatternMatcher(string searchPattern)
    {
        var normalizedPattern = string.IsNullOrWhiteSpace(searchPattern) ? "*" : searchPattern.Trim();
        var regexPattern = "^" + Regex.Escape(normalizedPattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
