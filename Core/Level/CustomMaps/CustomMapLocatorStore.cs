using System;
using System.IO;
using System.Linq;

namespace OpenGarrison.Core;

public readonly record struct CustomMapLocatorMetadata(
    string SourceUrl,
    string Md5Hash,
    string Sha256Hash);

public static class CustomMapLocatorStore
{
    public static CustomMapLocatorMetadata? TryReadMapMetadata(string levelName)
    {
        if (!TryNormalizeLevelName(levelName, out var normalizedLevelName))
        {
            return null;
        }

        var locatorPath = GetLocatorPath(normalizedLevelName);
        if (!File.Exists(locatorPath))
        {
            return null;
        }

        var lines = File.ReadAllLines(locatorPath)
            .Select(static line => line.Trim())
            .ToArray();
        if (lines.Length == 0)
        {
            return null;
        }

        var sourceUrl = lines[0];
        var md5Hash = string.Empty;
        var sha256Hash = string.Empty;
        for (var index = 1; index < lines.Length; index += 1)
        {
            var line = lines[index];
            if (line.Length == 0)
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (value.Length == 0)
            {
                continue;
            }

            if (key.Equals("md5", StringComparison.OrdinalIgnoreCase))
            {
                md5Hash = CustomMapHashService.ParseHash(value).Algorithm == CustomMapHashAlgorithm.Md5
                    ? CustomMapHashService.NormalizeHash(value)
                    : md5Hash;
            }
            else if (key.Equals("sha256", StringComparison.OrdinalIgnoreCase))
            {
                sha256Hash = CustomMapHashService.ParseHash(value).Algorithm == CustomMapHashAlgorithm.Sha256
                    ? CustomMapHashService.NormalizeHash(value)
                    : sha256Hash;
            }
        }

        if (string.IsNullOrWhiteSpace(sourceUrl) && md5Hash.Length == 0 && sha256Hash.Length == 0)
        {
            return null;
        }

        return new CustomMapLocatorMetadata(sourceUrl, md5Hash, sha256Hash);
    }

    public static string? TryReadMapUrl(string levelName)
    {
        var metadata = TryReadMapMetadata(levelName);
        return metadata is { SourceUrl.Length: > 0 } ? metadata.Value.SourceUrl : null;
    }

    public static void WriteMapUrl(string levelName, string mapUrl)
    {
        var existing = TryReadMapMetadata(levelName);
        WriteMapMetadata(levelName, new CustomMapLocatorMetadata(
            mapUrl.Trim(),
            existing?.Md5Hash ?? string.Empty,
            existing?.Sha256Hash ?? string.Empty));
    }

    public static void WriteMapMetadata(string levelName, CustomMapLocatorMetadata metadata)
    {
        if (!TryNormalizeLevelName(levelName, out var normalizedLevelName))
        {
            return;
        }

        var trimmedUrl = metadata.SourceUrl.Trim();
        var md5Hash = CustomMapHashService.ParseHash(metadata.Md5Hash).Algorithm == CustomMapHashAlgorithm.Md5
            ? CustomMapHashService.NormalizeHash(metadata.Md5Hash)
            : string.Empty;
        var sha256Hash = CustomMapHashService.ParseHash(metadata.Sha256Hash).Algorithm == CustomMapHashAlgorithm.Sha256
            ? CustomMapHashService.NormalizeHash(metadata.Sha256Hash)
            : string.Empty;
        if (trimmedUrl.Length == 0 && md5Hash.Length == 0 && sha256Hash.Length == 0)
        {
            return;
        }

        var locatorPath = GetLocatorPath(normalizedLevelName);
        Directory.CreateDirectory(Path.GetDirectoryName(locatorPath)!);
        using var writer = new StreamWriter(locatorPath, append: false);
        writer.WriteLine(trimmedUrl);
        if (md5Hash.Length > 0)
        {
            writer.WriteLine($"md5={md5Hash}");
        }

        if (sha256Hash.Length > 0)
        {
            writer.WriteLine($"sha256={sha256Hash}");
        }
    }

    public static string GetMapPath(string levelName)
    {
        var normalized = TryNormalizeLevelName(levelName, out var safeLevelName)
            ? safeLevelName
            : levelName.Trim();
        return Path.Combine(RuntimePaths.MapsDirectory, $"{normalized}.png");
    }

    private static string GetLocatorPath(string normalizedLevelName)
    {
        return Path.Combine(RuntimePaths.MapsDirectory, $"{normalizedLevelName}.locator");
    }

    public static bool TryNormalizeLevelName(string? levelName, out string normalizedLevelName)
    {
        normalizedLevelName = string.Empty;
        if (string.IsNullOrWhiteSpace(levelName))
        {
            return false;
        }

        var trimmed = levelName.Trim();
        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || trimmed.Contains(Path.DirectorySeparatorChar)
            || trimmed.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        normalizedLevelName = trimmed;
        return true;
    }
}
