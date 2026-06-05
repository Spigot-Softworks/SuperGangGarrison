using System.Collections.ObjectModel;
using System.Text;

namespace OpenGarrison.Core;

public static class BrowserContentCatalog
{
    private static readonly object Sync = new();
    private static IReadOnlyDictionary<string, byte[]> _binaryAssets =
        new ReadOnlyDictionary<string, byte[]>(new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase));

    public static void SetBinaryAssets(IEnumerable<KeyValuePair<string, byte[]>> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var normalized = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, bytes) in assets)
        {
            if (string.IsNullOrWhiteSpace(path) || bytes is null || bytes.Length == 0)
            {
                continue;
            }

            normalized[NormalizeRelativePath(path)] = bytes;
        }

        lock (Sync)
        {
            _binaryAssets = new ReadOnlyDictionary<string, byte[]>(normalized);
        }
    }

    public static void AddOrUpdateBinaryAssets(IEnumerable<KeyValuePair<string, byte[]>> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        lock (Sync)
        {
            var merged = new Dictionary<string, byte[]>(_binaryAssets, StringComparer.OrdinalIgnoreCase);
            foreach (var (path, bytes) in assets)
            {
                if (string.IsNullOrWhiteSpace(path) || bytes is null || bytes.Length == 0)
                {
                    continue;
                }

                merged[NormalizeRelativePath(path)] = bytes;
            }

            _binaryAssets = new ReadOnlyDictionary<string, byte[]>(merged);
        }
    }

    public static bool TryGetBinary(string relativePath, out byte[] bytes)
    {
        var normalizedPath = NormalizeRelativePath(relativePath);
        lock (Sync)
        {
            return _binaryAssets.TryGetValue(normalizedPath, out bytes!);
        }
    }

    public static bool TryGetBinaryForPath(string path, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (!TryGetCatalogRelativePath(path, out var relativePath))
        {
            return false;
        }

        return TryGetBinary(relativePath, out bytes);
    }

    public static bool TryGetText(string relativePath, out string text)
    {
        text = string.Empty;
        if (!TryGetBinary(relativePath, out var bytes) || bytes.Length == 0)
        {
            return false;
        }

        text = Encoding.UTF8.GetString(bytes);
        return true;
    }

    public static IReadOnlyList<string> GetBinaryPaths(string? prefix = null)
    {
        lock (Sync)
        {
            IEnumerable<string> paths = _binaryAssets.Keys;
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                var normalizedPrefix = NormalizeRelativePath(prefix);
                if (!normalizedPrefix.EndsWith('/'))
                {
                    normalizedPrefix += '/';
                }

                paths = paths.Where(path => path.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase));
            }

            return paths
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    private static bool TryGetCatalogRelativePath(string path, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalizedPath = path.Replace('\\', '/');
        if (TryGetContentRelativePath(normalizedPath, out relativePath))
        {
            return true;
        }

        return TryGetMarkerRelativePath(normalizedPath, "Plugins", out relativePath);
    }

    private static bool TryGetContentRelativePath(string normalizedPath, out string relativePath)
    {
        relativePath = string.Empty;
        var normalizedRoot = ContentRoot.Path.Replace('\\', '/').TrimEnd('/');
        var marker = normalizedRoot + "/";
        var markerIndex = normalizedPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return false;
        }

        relativePath = normalizedPath.Substring(markerIndex).TrimStart('/');
        return relativePath.Length > 0;
    }

    private static bool TryGetMarkerRelativePath(string normalizedPath, string markerName, out string relativePath)
    {
        relativePath = string.Empty;
        var segments = normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < segments.Length; index += 1)
        {
            if (!string.Equals(segments[index], markerName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            relativePath = string.Join('/', segments.Skip(index));
            return relativePath.Length > 0;
        }

        return false;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/').TrimStart('/');
    }
}
