using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed record GameplayPackAssetFile(string Path, string Sha256, long Size);

/// <summary>
/// The downloadable files of a plugin mod pack: every sprite frame PNG and sound file, with
/// per-file hashes and one content hash over all of them. Servers publish it so clients
/// without the plugin can download the pack's art and sounds, the way custom maps are downloaded.
/// </summary>
public sealed record GameplayPackAssetManifest(string PackId, string ContentHash, IReadOnlyList<GameplayPackAssetFile> Files)
{
    public const string RoutePrefix = "/opengarrison/plugin-packs";
    public const string ManifestFileName = "manifest.json";
    public const int MaxFiles = 4096;
    public const long MaxTotalBytes = 64L * 1024 * 1024;

    /// <summary>The only file types a pack can publish: sprite frames and sounds.</summary>
    public static readonly IReadOnlyList<string> DownloadableExtensions = [".png", ".wav", ".ogg"];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryCreate(
        string packId,
        string packDirectory,
        IEnumerable<GameplaySpriteAssetDefinition> sprites,
        out GameplayPackAssetManifest manifest,
        out string errorMessage)
    {
        return TryCreate(packId, packDirectory, sprites, [], out manifest, out errorMessage);
    }

    public static bool TryCreate(
        string packId,
        string packDirectory,
        GameplayModPackAssetCatalog assets,
        out GameplayPackAssetManifest manifest,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(assets);
        return TryCreate(packId, packDirectory, assets.Sprites.Values, assets.SoundsOrEmpty.Values, out manifest, out errorMessage);
    }

    public static bool TryCreate(
        string packId,
        string packDirectory,
        IEnumerable<GameplaySpriteAssetDefinition> sprites,
        IEnumerable<GameplaySoundAssetDefinition> sounds,
        out GameplayPackAssetManifest manifest,
        out string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        ArgumentException.ThrowIfNullOrWhiteSpace(packDirectory);
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(sounds);
        manifest = null!;
        var relativePaths = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var sprite in sprites)
        {
            foreach (var framePath in sprite.FramePaths)
            {
                if (!TryNormalizeRelativePath(framePath, out var normalizedPath)
                    || !normalizedPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = $"Sprite \"{sprite.Id}\" frame \"{framePath}\" is not a downloadable pack-relative PNG path.";
                    return false;
                }

                relativePaths.Add(normalizedPath);
            }
        }

        foreach (var sound in sounds)
        {
            if (!TryNormalizeRelativePath(sound.Path, out var normalizedPath)
                || normalizedPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"Sound \"{sound.Id}\" file \"{sound.Path}\" is not a downloadable pack-relative WAV or OGG path.";
                return false;
            }

            relativePaths.Add(normalizedPath);
        }

        if (relativePaths.Count > MaxFiles)
        {
            errorMessage = $"Pack \"{packId}\" has {relativePaths.Count} asset files; the limit is {MaxFiles}.";
            return false;
        }

        var files = new List<GameplayPackAssetFile>(relativePaths.Count);
        long totalBytes = 0;
        foreach (var relativePath in relativePaths)
        {
            if (!TryResolveContainedPath(packDirectory, relativePath, out var fullPath) || !File.Exists(fullPath))
            {
                errorMessage = $"Pack \"{packId}\" asset file \"{relativePath}\" was not found.";
                return false;
            }

            var bytes = File.ReadAllBytes(fullPath);
            totalBytes += bytes.LongLength;
            if (totalBytes > MaxTotalBytes)
            {
                errorMessage = $"Pack \"{packId}\" asset files exceed {MaxTotalBytes} bytes.";
                return false;
            }

            files.Add(new GameplayPackAssetFile(relativePath, Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.LongLength));
        }

        manifest = new GameplayPackAssetManifest(packId, ComputeContentHash(packDirectory, files.Select(static file => file.Path)), files);
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// SHA-256 over every file, sorted by path: path bytes, 0x00, content, 0xFF per file
    /// (the custom map package scheme). Returned as "sha256:&lt;hex&gt;".
    /// </summary>
    public static string ComputeContentHash(string rootDirectory, IEnumerable<string> relativePaths)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var relativePath in relativePaths.OrderBy(static path => path, StringComparer.Ordinal))
        {
            if (!TryResolveContainedPath(rootDirectory, relativePath, out var fullPath))
            {
                throw new InvalidOperationException($"Pack file \"{relativePath}\" escapes its directory.");
            }

            hash.AppendData(Encoding.UTF8.GetBytes(relativePath));
            hash.AppendData([0x00]);
            hash.AppendData(File.ReadAllBytes(fullPath));
            hash.AppendData([0xFF]);
        }

        return "sha256:" + Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>Relative URL of the manifest on the server's HTTP endpoint.</summary>
    public static string BuildManifestUrl(string packId, string contentHash)
    {
        return $"{RoutePrefix}/{Uri.EscapeDataString(packId)}/{GetHashHex(contentHash)}/{ManifestFileName}";
    }

    public static string GetHashHex(string contentHash)
    {
        return contentHash.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? contentHash["sha256:".Length..]
            : contentHash;
    }

    public static bool IsValidHashHex(string? hashHex)
    {
        return hashHex is { Length: 64 } && hashHex.All(Uri.IsHexDigit);
    }

    /// <summary>
    /// Accepts only plain pack-relative PNG, WAV or OGG paths ("assets/kano/KanoRedS/0.png"):
    /// no rooted paths, drive letters, empty, "." or ".." segments.
    /// </summary>
    public static bool TryNormalizeRelativePath(string? relativePath, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var candidate = relativePath.Trim().Replace('\\', '/');
        if (candidate.StartsWith('/')
            || candidate.Contains(':', StringComparison.Ordinal)
            || !DownloadableExtensions.Any(extension => candidate.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var segments = candidate.Split('/');
        if (segments.Any(static segment => segment.Length == 0 || segment == "." || segment == ".."))
        {
            return false;
        }

        normalizedPath = candidate;
        return true;
    }

    public static bool TryResolveContainedPath(string rootDirectory, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (!TryNormalizeRelativePath(relativePath, out var normalizedPath))
        {
            return false;
        }

        var root = Path.GetFullPath(rootDirectory);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root, normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    public byte[] SerializeToUtf8Json()
    {
        return JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> utf8Json, out GameplayPackAssetManifest manifest, out string errorMessage)
    {
        manifest = null!;
        try
        {
            var parsed = JsonSerializer.Deserialize<GameplayPackAssetManifest>(utf8Json, JsonOptions);
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.PackId) || parsed.Files is null)
            {
                errorMessage = "Pack manifest is empty.";
                return false;
            }

            if (parsed.Files.Count > MaxFiles || parsed.Files.Sum(static file => Math.Max(0L, file.Size)) > MaxTotalBytes)
            {
                errorMessage = "Pack manifest exceeds the download limits.";
                return false;
            }

            foreach (var file in parsed.Files)
            {
                if (!TryNormalizeRelativePath(file.Path, out var normalizedPath)
                    || !string.Equals(normalizedPath, file.Path, StringComparison.Ordinal)
                    || !IsValidHashHex(file.Sha256)
                    || file.Size < 0)
                {
                    errorMessage = $"Pack manifest lists an invalid file \"{file.Path}\".";
                    return false;
                }
            }

            manifest = parsed;
            errorMessage = string.Empty;
            return true;
        }
        catch (JsonException ex)
        {
            errorMessage = $"Pack manifest is invalid: {ex.Message}";
            return false;
        }
    }
}
