using System.IO.Compression;
using System.Text.Json;

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

        return false;
    }

    public static T? Deserialize<T>(string path, JsonSerializerOptions options)
    {
        using var stream = OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, options);
    }

    private static Stream OpenRead(string path)
    {
        var stream = File.OpenRead(path);
        return path.EndsWith(GzipExtension, StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(stream, CompressionMode.Decompress)
            : stream;
    }
}
