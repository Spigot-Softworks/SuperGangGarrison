using System.IO.Compression;
using System.Text.Json;
using OpenGarrison.Core;

namespace OpenGarrison.Core.BotBrain;

internal static class BotBrainJsonAssetIO
{
    private const string GzipExtension = ".gz";

    public static bool TryResolveReadablePath(string path, out string readablePath)
    {
        readablePath = path;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (File.Exists(path))
        {
            return true;
        }

        if (TryGetCatalogBytes(path, out _))
        {
            return true;
        }

        if (path.EndsWith(GzipExtension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var compressedPath = path + GzipExtension;
        if (File.Exists(compressedPath))
        {
            readablePath = compressedPath;
            return true;
        }

        if (TryGetCatalogBytes(compressedPath, out _))
        {
            readablePath = compressedPath;
            return true;
        }

        return false;
    }

    public static bool TryGetReadableMetadata(string path, out string cacheKey, out long lastWriteTicks, out long length)
    {
        cacheKey = GetCacheKey(path);
        lastWriteTicks = 0;
        length = 0;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (TryGetCatalogBytes(path, out var bytes))
        {
            length = bytes.Length;
            return true;
        }

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            return false;
        }

        cacheKey = Path.GetFullPath(path);
        lastWriteTicks = fileInfo.LastWriteTimeUtc.Ticks;
        length = fileInfo.Length;
        return true;
    }

    public static string GetCacheKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return TryGetCatalogRelativePath(path, out var relativePath)
            ? relativePath
            : Path.GetFullPath(path);
    }

    public static T? Deserialize<T>(string path, JsonSerializerOptions options)
    {
        using var stream = OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, options);
    }

    private static Stream OpenRead(string path)
    {
        if (TryGetCatalogBytes(path, out var bytes))
        {
            var memoryStream = new MemoryStream(bytes, writable: false);
            return path.EndsWith(GzipExtension, StringComparison.OrdinalIgnoreCase)
                ? new GZipStream(memoryStream, CompressionMode.Decompress)
                : memoryStream;
        }

        var stream = File.OpenRead(path);
        return path.EndsWith(GzipExtension, StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(stream, CompressionMode.Decompress)
            : stream;
    }

    private static bool TryGetCatalogBytes(string path, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (BrowserContentCatalog.TryGetBinaryForPath(path, out bytes))
        {
            return true;
        }

        if (!TryGetCatalogRelativePath(path, out var relativePath))
        {
            return false;
        }

        return BrowserContentCatalog.TryGetBinary(relativePath, out bytes);
    }

    private static bool TryGetCatalogRelativePath(string path, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var isRootedPath = Path.IsPathRooted(path);
        var normalizedPath = path.Replace('\\', '/').TrimStart('/');
        if (BrowserContentCatalog.TryGetBinary(normalizedPath, out _))
        {
            relativePath = normalizedPath;
            return true;
        }

        const string contentMarker = "Content/";
        var contentIndex = normalizedPath.IndexOf(contentMarker, StringComparison.OrdinalIgnoreCase);
        if (contentIndex >= 0)
        {
            relativePath = normalizedPath[contentIndex..];
            return relativePath.Length > 0;
        }

        if (isRootedPath || normalizedPath.Contains(':'))
        {
            return false;
        }

        relativePath = $"{contentMarker}{normalizedPath}";
        return normalizedPath.Length > 0;
    }
}
