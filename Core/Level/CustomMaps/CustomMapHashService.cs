using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

    public static string ComputePackageSha256(string manifestPath)
    {
        try
        {
            var files = CustomMapPackageImporter.GetPackageContentFiles(manifestPath);
            if (files.Count == 0)
            {
                return string.Empty;
            }

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var file in files)
            {
                var relativePathBytes = Encoding.UTF8.GetBytes(file.RelativePath.Replace('\\', '/'));
                hash.AppendData(relativePathBytes);
                hash.AppendData([0]);

                if (Path.GetFullPath(file.FullPath).Equals(Path.GetFullPath(manifestPath), StringComparison.OrdinalIgnoreCase))
                {
                    var canonicalManifestBytes = Encoding.UTF8.GetBytes(CanonicalizeJson(File.ReadAllText(file.FullPath)));
                    hash.AppendData(canonicalManifestBytes);
                }
                else
                {
                    using var stream = File.OpenRead(file.FullPath);
                    var buffer = new byte[81920];
                    int bytesRead;
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        hash.AppendData(buffer, 0, bytesRead);
                    }
                }

                hash.AppendData([0xff]);
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
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

    public static bool FileMatchesHash(string filePath, CustomMapHashValue expectedHash)
    {
        return expectedHash.HasValue && FileMatchesHash(filePath, $"{FormatHashAlgorithm(expectedHash.Algorithm)}:{expectedHash.Value}");
    }

    public static bool PackageMatchesHash(string manifestPath, CustomMapHashValue expectedHash)
    {
        if (!expectedHash.HasValue || expectedHash.Algorithm != CustomMapHashAlgorithm.Sha256)
        {
            return false;
        }

        var currentHash = ComputePackageSha256(manifestPath);
        return currentHash.Length > 0
            && string.Equals(currentHash, expectedHash.Value, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatHashAlgorithm(CustomMapHashAlgorithm algorithm)
    {
        return algorithm switch
        {
            CustomMapHashAlgorithm.Md5 => "md5",
            CustomMapHashAlgorithm.Sha256 => "sha256",
            _ => string.Empty,
        };
    }

    private static string CanonicalizeJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var builder = new StringBuilder();
        AppendCanonicalJson(builder, document.RootElement);
        return builder.ToString();
    }

    private static void AppendCanonicalJson(StringBuilder builder, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                var firstProperty = true;
                foreach (var property in element.EnumerateObject().OrderBy(static property => property.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty)
                    {
                        builder.Append(',');
                    }

                    builder.Append(JsonSerializer.Serialize(property.Name));
                    builder.Append(':');
                    AppendCanonicalJson(builder, property.Value);
                    firstProperty = false;
                }

                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                var firstItem = true;
                foreach (var item in element.EnumerateArray())
                {
                    if (!firstItem)
                    {
                        builder.Append(',');
                    }

                    AppendCanonicalJson(builder, item);
                    firstItem = false;
                }

                builder.Append(']');
                break;
            case JsonValueKind.String:
                builder.Append(JsonSerializer.Serialize(element.GetString()));
                break;
            case JsonValueKind.Number:
                builder.Append(element.GetRawText());
                break;
            case JsonValueKind.True:
                builder.Append("true");
                break;
            case JsonValueKind.False:
                builder.Append("false");
                break;
            default:
                builder.Append("null");
                break;
        }
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
