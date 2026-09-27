using System.IO;
using System.Security.Cryptography;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>Receives and verifies GG2's raw PNG map transfer before joining.</summary>
internal static class LegacyGg2MapCache
{
    private const uint MaxMapBytes = 64 * 1024 * 1024;

    internal static string EnsureMapAvailable(
        BinaryReader reader,
        Action<byte[]> send,
        string advertisedMapName,
        string advertisedMd5,
        string? cacheDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(send);
        if (string.IsNullOrWhiteSpace(advertisedMapName)
            || advertisedMapName.Any(character => character is '/' or '\\' || char.IsControl(character)))
        {
            throw new InvalidDataException("GG2 server advertised an invalid map name.");
        }

        if (advertisedMd5.Length != 32 || !advertisedMd5.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("GG2 server advertised an invalid map MD5.");
        }

        var hash = advertisedMd5.ToLowerInvariant();
        var levelName = $"gg2_{hash}";
        var directory = cacheDirectory ?? Path.Combine(RuntimePaths.UserDataRoot, "gg2-maps");
        var mapPath = Path.Combine(directory, levelName + ".png");
        if (File.Exists(mapPath)
            && new FileInfo(mapPath).Length <= MaxMapBytes
            && HasExpectedHash(mapPath, hash)
            && TryRegisterLevel(levelName, mapPath, advertisedMapName))
        {
            return levelName;
        }

        send([LegacyGg2Wire.DownloadMap]);
        var length = reader.ReadUInt32();
        if (length == 0 || length > MaxMapBytes)
        {
            throw new InvalidDataException($"GG2 map transfer has an invalid size ({length} bytes).");
        }

        var bytes = reader.ReadBytes(checked((int)length));
        if (bytes.Length != length)
        {
            throw new EndOfStreamException("GG2 map transfer ended early.");
        }

        var receivedHash = Convert.ToHexStringLower(MD5.HashData(bytes));
        if (!string.Equals(receivedHash, hash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("GG2 map transfer did not match the server's MD5.");
        }

        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Guid.NewGuid():N}.download");
        try
        {
            File.WriteAllBytes(tempPath, bytes);
            if (!TryRegisterLevel(levelName, tempPath, advertisedMapName))
            {
                throw new InvalidDataException("GG2 map PNG has no usable walkmask or player spawns.");
            }

            File.Move(tempPath, mapPath, overwrite: true);
            SimpleLevelFactory.RegisterExternalLegacyPngLevel(levelName, mapPath, advertisedMapName);
            return levelName;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static bool HasExpectedHash(string path, string expectedHash)
    {
        using var stream = File.OpenRead(path);
        return string.Equals(Convert.ToHexStringLower(MD5.HashData(stream)), expectedHash, StringComparison.Ordinal);
    }

    private static bool TryRegisterLevel(string levelName, string mapPath, string sourceMapName)
    {
        SimpleLevelFactory.RegisterExternalLegacyPngLevel(levelName, mapPath, sourceMapName);
        return SimpleLevelFactory.CreateImportedLevel(levelName) is not null;
    }
}
