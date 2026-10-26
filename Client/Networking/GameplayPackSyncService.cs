#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Downloads plugin pack art a server advertises (see <see cref="GameplayReplicatedAssetPack"/>)
/// into a per-hash cache, the same way custom maps are fetched. Every file is checked
/// against the manifest's size and SHA-256, and the whole set against the advertised content
/// hash, before the folder is published; a pack already cached under its hash is reused.
/// </summary>
public sealed partial class GameplayPackSyncService(HttpClient httpClient, string cacheRoot)
{
    private const string CompleteMarkerFileName = ".complete";
    private const int MaxManifestBytes = 4 * 1024 * 1024;

    public static string DefaultCacheRoot => Path.Combine(RuntimePaths.UserDataRoot, "PluginPacks");

    public string GetCacheDirectory(GameplayReplicatedAssetPack pack)
    {
        return Path.Combine(cacheRoot, GetSafeDirectoryName(pack.PackId), GameplayPackAssetManifest.GetHashHex(pack.ContentHash).ToLowerInvariant());
    }

    public bool TryGetCached(GameplayReplicatedAssetPack pack, out string directory)
    {
        directory = GetCacheDirectory(pack);
        return File.Exists(Path.Combine(directory, CompleteMarkerFileName));
    }

    public async Task<string> EnsureAvailableAsync(GameplayReplicatedAssetPack pack, Uri? serverBaseUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var hashHex = GameplayPackAssetManifest.GetHashHex(pack.ContentHash);
        if (!GameplayPackAssetManifest.IsValidHashHex(hashHex))
        {
            throw new InvalidDataException($"Pack \"{pack.PackId}\" has an invalid content hash.");
        }

        if (TryGetCached(pack, out var cachedDirectory))
        {
            return cachedDirectory;
        }

        var manifestUri = ResolveUri(pack.ManifestUrl, serverBaseUri);
        var manifestBytes = await DownloadAsync(manifestUri, MaxManifestBytes, cancellationToken).ConfigureAwait(false);
        if (!GameplayPackAssetManifest.TryDeserialize(manifestBytes, out var manifest, out var manifestError))
        {
            throw new InvalidDataException(manifestError);
        }

        if (!string.Equals(manifest.PackId, pack.PackId, StringComparison.Ordinal)
            || !string.Equals(GameplayPackAssetManifest.GetHashHex(manifest.ContentHash), hashHex, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Pack \"{pack.PackId}\" manifest does not match the advertised content.");
        }

        var finalDirectory = GetCacheDirectory(pack);
        var stagingDirectory = finalDirectory + ".download-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(stagingDirectory);
            foreach (var file in manifest.Files)
            {
                if (!GameplayPackAssetManifest.TryResolveContainedPath(stagingDirectory, file.Path, out var targetPath))
                {
                    throw new InvalidDataException($"Pack file \"{file.Path}\" escapes the pack folder.");
                }

                var fileUri = new Uri(manifestUri, EscapeRelativePath(file.Path));
                var bytes = await DownloadAsync(fileUri, file.Size, cancellationToken).ConfigureAwait(false);
                if (bytes.LongLength != file.Size
                    || !string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Pack file \"{file.Path}\" does not match its manifest hash.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await File.WriteAllBytesAsync(targetPath, bytes, cancellationToken).ConfigureAwait(false);
            }

            var contentHash = GameplayPackAssetManifest.ComputeContentHash(stagingDirectory, manifest.Files.Select(static file => file.Path));
            if (!string.Equals(GameplayPackAssetManifest.GetHashHex(contentHash), hashHex, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Pack \"{pack.PackId}\" files do not match the advertised content hash.");
            }

            await File.WriteAllTextAsync(Path.Combine(stagingDirectory, CompleteMarkerFileName), pack.ContentHash, cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(Path.GetDirectoryName(finalDirectory)!);
            if (Directory.Exists(finalDirectory))
            {
                Directory.Delete(finalDirectory, recursive: true);
            }

            Directory.Move(stagingDirectory, finalDirectory);
            return finalDirectory;
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private async Task<byte[]> DownloadAsync(Uri uri, long maxBytes, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is { } declaredLength && declaredLength > maxBytes)
        {
            throw new InvalidDataException($"\"{uri}\" is larger than expected.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidDataException($"\"{uri}\" is larger than expected.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static Uri ResolveUri(string url, Uri? serverBaseUri)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute;
        }

        if (serverBaseUri is null || !url.StartsWith('/'))
        {
            throw new InvalidDataException($"Pack download URL \"{url}\" can't be resolved without the server's address.");
        }

        return new Uri(serverBaseUri, url);
    }

    private static string EscapeRelativePath(string relativePath)
    {
        return string.Join('/', relativePath.Split('/').Select(Uri.EscapeDataString));
    }

    private static string GetSafeDirectoryName(string packId)
    {
        var safe = UnsafeDirectoryCharacters().Replace(packId, "_").Trim('.');
        return string.IsNullOrEmpty(safe) ? "pack" : safe;
    }

    [GeneratedRegex(@"[^A-Za-z0-9._-]")]
    private static partial Regex UnsafeDirectoryCharacters();
}
