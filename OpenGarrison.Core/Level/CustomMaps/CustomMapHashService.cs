using System;
using System.IO;
using System.Security.Cryptography;

namespace OpenGarrison.Core;

public enum CustomMapHashAlgorithm
{
    None = 0,
    Md5 = 1,
    Sha256 = 2,
}

public readonly record struct CustomMapHashValue(CustomMapHashAlgorithm Algorithm, string Value)
{
    public bool HasValue => Algorithm != CustomMapHashAlgorithm.None && Value.Length > 0;
}

public static class CustomMapHashService
{
#pragma warning disable CA5351 // MD5 is required for legacy OpenGarrison custom-map compatibility.
    public static string ComputeMd5(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = MD5.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
#pragma warning restore CA5351

    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string NormalizeHash(string? hash)
    {
        return ParseHash(hash).Value;
    }

    public static CustomMapHashValue ParseHash(string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return default;
        }

        var normalized = hash.Trim().ToLowerInvariant();
        if (normalized.StartsWith("md5:", StringComparison.Ordinal))
        {
            normalized = normalized["md5:".Length..].Trim();
            return IsHexHash(normalized, 32)
                ? new CustomMapHashValue(CustomMapHashAlgorithm.Md5, normalized)
                : default;
        }

        if (normalized.StartsWith("sha256:", StringComparison.Ordinal))
        {
            normalized = normalized["sha256:".Length..].Trim();
            return IsHexHash(normalized, 64)
                ? new CustomMapHashValue(CustomMapHashAlgorithm.Sha256, normalized)
                : default;
        }

        if (IsHexHash(normalized, 32))
        {
            return new CustomMapHashValue(CustomMapHashAlgorithm.Md5, normalized);
        }

        if (IsHexHash(normalized, 64))
        {
            return new CustomMapHashValue(CustomMapHashAlgorithm.Sha256, normalized);
        }

        return default;
    }

    public static bool FileMatchesHash(string filePath, string? expectedHash)
    {
        var parsedHash = ParseHash(expectedHash);
        if (!parsedHash.HasValue)
        {
            return false;
        }

        var currentHash = parsedHash.Algorithm switch
        {
            CustomMapHashAlgorithm.Md5 => ComputeMd5(filePath),
            CustomMapHashAlgorithm.Sha256 => ComputeSha256(filePath),
            _ => string.Empty,
        };
        return string.Equals(currentHash, parsedHash.Value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHexHash(string value, int expectedLength)
    {
        if (value.Length != expectedLength)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index += 1)
        {
            if (!Uri.IsHexDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }
}
