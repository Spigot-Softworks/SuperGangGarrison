using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class CustomMapSyncService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    internal readonly record struct CustomMapSyncResult(bool Success, string Error)
    {
        public static CustomMapSyncResult Ok { get; } = new(true, string.Empty);
        public static CustomMapSyncResult Fail(string error) => new(false, error);
    }

    public static bool EnsureMapAvailable(
        string levelName,
        bool isCustomMap,
        string mapDownloadUrl,
        string mapContentHash,
        out string error)
    {
        return EnsureMapAvailable(
            levelName,
            isCustomMap,
            mapDownloadUrl,
            mapContentHash,
            serverDownloadBaseUri: null,
            out error);
    }

    public static Task<CustomMapSyncResult> EnsureMapAvailableAsync(
        string levelName,
        bool isCustomMap,
        string mapDownloadUrl,
        string mapContentHash,
        Uri? serverDownloadBaseUri)
    {
        var httpClient = OperatingSystem.IsBrowser()
            ? ClientRuntimeBootstrap.GetBrowserHttpClient() ?? HttpClient
            : HttpClient;
        return EnsureMapAvailableAsync(
            levelName,
            isCustomMap,
            mapDownloadUrl,
            mapContentHash,
            serverDownloadBaseUri,
            httpClient);
    }

    internal static async Task<CustomMapSyncResult> EnsureMapAvailableAsync(
        string levelName,
        bool isCustomMap,
        string mapDownloadUrl,
        string mapContentHash,
        Uri? serverDownloadBaseUri,
        HttpClient httpClient)
    {
        if (!isCustomMap)
        {
            return CustomMapSyncResult.Ok;
        }

        if (!CustomMapLocatorStore.TryNormalizeLevelName(levelName, out var normalizedLevelName))
        {
            return CustomMapSyncResult.Fail($"Invalid custom map name: {levelName}");
        }

        var expectedHash = CustomMapHashService.ParseHash(mapContentHash);
        var downloadUrl = ResolveDownloadUrl(normalizedLevelName, mapDownloadUrl, serverDownloadBaseUri);
        var mapPath = CustomMapLocatorStore.GetMapPath(normalizedLevelName);
        var hasExpectedHash = expectedHash.HasValue;
        if (File.Exists(mapPath)
            && (!hasExpectedHash || CustomMapHashService.FileMatchesHash(mapPath, expectedHash)))
        {
            CacheLocator(normalizedLevelName, downloadUrl, expectedHash);
            return CustomMapSyncResult.Ok;
        }

        if (CustomMapLocatorStore.TryGetPackageManifestPath(normalizedLevelName, out var packageManifestPath)
            && (!hasExpectedHash || CustomMapHashService.PackageMatchesHash(packageManifestPath, expectedHash)))
        {
            CacheLocator(normalizedLevelName, downloadUrl, expectedHash);
            return CustomMapSyncResult.Ok;
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return CustomMapSyncResult.Fail($"Missing map {normalizedLevelName}. Server did not provide a download URL.");
        }

        var isPackageDownload = ShouldDownloadPackage(downloadUrl, expectedHash);
        var result = isPackageDownload
            ? await TryDownloadPackageAsync(httpClient, normalizedLevelName, downloadUrl, expectedHash).ConfigureAwait(false)
            : await TryDownloadLegacyMapAsync(httpClient, downloadUrl, mapPath, expectedHash).ConfigureAwait(false);
        if (!result.Success)
        {
            return result;
        }

        CacheLocator(normalizedLevelName, downloadUrl, expectedHash);
        return CustomMapSyncResult.Ok;
    }

    public static bool EnsureMapAvailable(
        string levelName,
        bool isCustomMap,
        string mapDownloadUrl,
        string mapContentHash,
        Uri? serverDownloadBaseUri,
        out string error)
    {
        return EnsureMapAvailable(
            levelName,
            isCustomMap,
            mapDownloadUrl,
            mapContentHash,
            serverDownloadBaseUri,
            HttpClient,
            out error);
    }

    internal static bool EnsureMapAvailable(
        string levelName,
        bool isCustomMap,
        string mapDownloadUrl,
        string mapContentHash,
        HttpClient httpClient,
        out string error)
    {
        return EnsureMapAvailable(
            levelName,
            isCustomMap,
            mapDownloadUrl,
            mapContentHash,
            serverDownloadBaseUri: null,
            httpClient,
            out error);
    }

    internal static bool EnsureMapAvailable(
        string levelName,
        bool isCustomMap,
        string mapDownloadUrl,
        string mapContentHash,
        Uri? serverDownloadBaseUri,
        HttpClient httpClient,
        out string error)
    {
        error = string.Empty;
        if (!isCustomMap)
        {
            return true;
        }

        if (!CustomMapLocatorStore.TryNormalizeLevelName(levelName, out var normalizedLevelName))
        {
            error = $"Invalid custom map name: {levelName}";
            return false;
        }

        var expectedHash = CustomMapHashService.ParseHash(mapContentHash);
        var downloadUrl = ResolveDownloadUrl(normalizedLevelName, mapDownloadUrl, serverDownloadBaseUri);
        var mapPath = CustomMapLocatorStore.GetMapPath(normalizedLevelName);
        var hasExpectedHash = expectedHash.HasValue;
        if (File.Exists(mapPath)
            && (!hasExpectedHash || CustomMapHashService.FileMatchesHash(mapPath, expectedHash)))
        {
            CacheLocator(normalizedLevelName, downloadUrl, expectedHash);
            return true;
        }

        if (CustomMapLocatorStore.TryGetPackageManifestPath(normalizedLevelName, out var packageManifestPath)
            && (!hasExpectedHash || CustomMapHashService.PackageMatchesHash(packageManifestPath, expectedHash)))
        {
            CacheLocator(normalizedLevelName, downloadUrl, expectedHash);
            return true;
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            error = $"Missing map {normalizedLevelName}. Server did not provide a download URL.";
            return false;
        }

        var isPackageDownload = ShouldDownloadPackage(downloadUrl, expectedHash);
        if (isPackageDownload)
        {
            if (!TryDownloadPackage(httpClient, normalizedLevelName, downloadUrl, expectedHash, out error))
            {
                return false;
            }
        }
        else if (!TryDownloadLegacyMap(httpClient, downloadUrl, mapPath, expectedHash, out error))
        {
            return false;
        }

        CacheLocator(normalizedLevelName, downloadUrl, expectedHash);
        return true;
    }

    internal static bool TryCreateServerDownloadBaseUri(string host, int port, out Uri baseUri)
    {
        baseUri = null!;
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var trimmedHost = host.Trim();
        if (Uri.TryCreate(trimmedHost, UriKind.Absolute, out var absoluteUri)
            && (absoluteUri.Scheme == "ws"
                || absoluteUri.Scheme == "wss"
                || absoluteUri.Scheme == Uri.UriSchemeHttp
                || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            var builder = new UriBuilder(absoluteUri)
            {
                Scheme = absoluteUri.Scheme == "wss"
                    ? Uri.UriSchemeHttps
                    : absoluteUri.Scheme == "ws"
                        ? Uri.UriSchemeHttp
                        : absoluteUri.Scheme,
                Path = string.Empty,
                Query = string.Empty,
                Fragment = string.Empty,
            };
            baseUri = EnsureTrailingSlash(builder.Uri);
            return true;
        }

        if (port is <= 0 or > 65535)
        {
            return false;
        }

        if (!Uri.TryCreate($"{Uri.UriSchemeHttp}://{trimmedHost}:{port}/", UriKind.Absolute, out var createdBaseUri)
            || createdBaseUri is null)
        {
            baseUri = null!;
            return false;
        }

        baseUri = createdBaseUri;
        return true;
    }

    private static string ResolveDownloadUrl(string levelName, string mapDownloadUrl, Uri? serverDownloadBaseUri)
    {
        if (!string.IsNullOrWhiteSpace(mapDownloadUrl))
        {
            var trimmedUrl = mapDownloadUrl.Trim();
            if (Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var absoluteUri))
            {
                return absoluteUri.ToString();
            }

            if (serverDownloadBaseUri is not null
                && Uri.TryCreate(serverDownloadBaseUri, trimmedUrl, out var resolvedUri))
            {
                return resolvedUri.ToString();
            }

            return trimmedUrl;
        }

        return CustomMapLocatorStore.TryReadMapUrl(levelName) ?? string.Empty;
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var text = uri.ToString();
        return text.Length > 0 && text[^1] == '/'
            ? uri
            : new Uri($"{text}/", UriKind.Absolute);
    }

    private static bool ShouldDownloadPackage(string mapDownloadUrl, CustomMapHashValue expectedHash)
    {
        if (Uri.TryCreate(mapDownloadUrl, UriKind.Absolute, out var uri))
        {
            var extension = Path.GetExtension(uri.AbsolutePath);
            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return expectedHash.Algorithm == CustomMapHashAlgorithm.Sha256;
    }

    private static bool TryDownloadLegacyMap(
        HttpClient httpClient,
        string mapDownloadUrl,
        string mapPath,
        CustomMapHashValue expectedHash,
        out string error)
    {
        error = string.Empty;
        if (!Uri.TryCreate(mapDownloadUrl, UriKind.Absolute, out var mapUri))
        {
            error = $"Invalid map download URL: {mapDownloadUrl}";
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(mapPath)!);
        var tempPath = mapPath + ".download";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, mapUri);
            using var response = httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                error = $"Map download failed ({(int)response.StatusCode} {response.ReasonPhrase}).";
                return false;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!string.IsNullOrWhiteSpace(mediaType)
                && !mediaType.Contains("png", StringComparison.OrdinalIgnoreCase))
            {
                error = $"Map download returned unsupported content type: {mediaType}.";
                return false;
            }

            using (var networkStream = response.Content.ReadAsStream())
            using (var fileStream = File.Create(tempPath))
            {
                networkStream.CopyTo(fileStream);
            }

            if (expectedHash.HasValue)
            {
                if (!CustomMapHashService.FileMatchesHash(tempPath, expectedHash))
                {
                    error = "Downloaded map hash does not match the server hash.";
                    return false;
                }
            }

            File.Move(tempPath, mapPath, overwrite: true);
            var packageDirectory = CustomMapLocatorStore.GetPackageDirectory(Path.GetFileNameWithoutExtension(mapPath));
            if (Directory.Exists(packageDirectory))
            {
                Directory.Delete(packageDirectory, recursive: true);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Map download failed: {ex.Message}";
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }
    }

    private static async Task<CustomMapSyncResult> TryDownloadLegacyMapAsync(
        HttpClient httpClient,
        string mapDownloadUrl,
        string mapPath,
        CustomMapHashValue expectedHash)
    {
        if (!Uri.TryCreate(mapDownloadUrl, UriKind.Absolute, out var mapUri))
        {
            return CustomMapSyncResult.Fail($"Invalid map download URL: {mapDownloadUrl}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(mapPath)!);
        var tempPath = mapPath + ".download";
        try
        {
            using var response = await httpClient.GetAsync(mapUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return CustomMapSyncResult.Fail($"Map download failed ({(int)response.StatusCode} {response.ReasonPhrase}).");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!string.IsNullOrWhiteSpace(mediaType)
                && !mediaType.Contains("png", StringComparison.OrdinalIgnoreCase))
            {
                return CustomMapSyncResult.Fail($"Map download returned unsupported content type: {mediaType}.");
            }

            var payload = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            File.WriteAllBytes(tempPath, payload);

            if (expectedHash.HasValue
                && !CustomMapHashService.FileMatchesHash(tempPath, expectedHash))
            {
                return CustomMapSyncResult.Fail("Downloaded map hash does not match the server hash.");
            }

            File.Move(tempPath, mapPath, overwrite: true);
            var packageDirectory = CustomMapLocatorStore.GetPackageDirectory(Path.GetFileNameWithoutExtension(mapPath));
            if (Directory.Exists(packageDirectory))
            {
                Directory.Delete(packageDirectory, recursive: true);
            }

            return CustomMapSyncResult.Ok;
        }
        catch (Exception ex)
        {
            return CustomMapSyncResult.Fail($"Map download failed: {ex.Message}");
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static bool TryDownloadPackage(
        HttpClient httpClient,
        string levelName,
        string manifestDownloadUrl,
        CustomMapHashValue expectedHash,
        out string error)
    {
        error = string.Empty;
        if (!Uri.TryCreate(manifestDownloadUrl, UriKind.Absolute, out var manifestUri))
        {
            error = $"Invalid map package manifest URL: {manifestDownloadUrl}";
            return false;
        }

        var finalDirectory = CustomMapLocatorStore.GetPackageDirectory(levelName);
        var tempDirectory = Path.Combine(RuntimePaths.MapsDirectory, $".{levelName}.{Guid.NewGuid():N}.download");
        var backupDirectory = Path.Combine(RuntimePaths.MapsDirectory, $".{levelName}.{Guid.NewGuid():N}.old");
        var finalManifestPath = CustomMapLocatorStore.GetPackageManifestPath(levelName);
        try
        {
            Directory.CreateDirectory(tempDirectory);
            var tempManifestPath = Path.Combine(tempDirectory, $"{levelName}.json");
            if (!TryDownloadToFile(httpClient, manifestUri, tempManifestPath, IsSupportedManifestContentType, "Map package manifest", out error))
            {
                return false;
            }

            if (!CustomMapPackageImporter.TryReadManifest(tempManifestPath, out var manifest, out error))
            {
                return false;
            }

            foreach (var imageReference in CustomMapPackageImporter.GetReferencedImagePaths(manifest))
            {
                if (!CustomMapPackageImporter.TryResolvePackageImagePath(
                    tempDirectory,
                    imageReference,
                    requireFileExists: false,
                    out var imageOutputPath,
                    out var normalizedRelativePath))
                {
                    error = $"Package image reference is invalid: {imageReference}";
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(imageOutputPath)!);
                var imageUri = new Uri(manifestUri, normalizedRelativePath);
                if (!TryDownloadToFile(httpClient, imageUri, imageOutputPath, IsSupportedPngContentType, "Map package image", out error))
                {
                    return false;
                }
            }

            if (expectedHash.HasValue
                && !CustomMapHashService.PackageMatchesHash(tempManifestPath, expectedHash))
            {
                error = "Downloaded map package hash does not match the server hash.";
                return false;
            }

            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            if (File.Exists(finalManifestPath))
            {
                if (Directory.Exists(backupDirectory))
                {
                    Directory.Delete(backupDirectory, recursive: true);
                }

                Directory.Move(finalDirectory, backupDirectory);
            }
            else if (Directory.Exists(finalDirectory))
            {
                Directory.Move(finalDirectory, backupDirectory);
            }

            try
            {
                Directory.Move(tempDirectory, finalDirectory);
            }
            catch
            {
                if (Directory.Exists(backupDirectory) && !Directory.Exists(finalDirectory))
                {
                    Directory.Move(backupDirectory, finalDirectory);
                }

                throw;
            }

            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, recursive: true);
            }

            var legacyMapPath = CustomMapLocatorStore.GetMapPath(levelName);
            if (File.Exists(legacyMapPath))
            {
                File.Delete(legacyMapPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Map package download failed: {ex.Message}";
            return false;
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
            TryDeleteDirectory(backupDirectory);
        }
    }

    private static async Task<CustomMapSyncResult> TryDownloadPackageAsync(
        HttpClient httpClient,
        string levelName,
        string manifestDownloadUrl,
        CustomMapHashValue expectedHash)
    {
        if (!Uri.TryCreate(manifestDownloadUrl, UriKind.Absolute, out var manifestUri))
        {
            return CustomMapSyncResult.Fail($"Invalid map package manifest URL: {manifestDownloadUrl}");
        }

        var finalDirectory = CustomMapLocatorStore.GetPackageDirectory(levelName);
        var tempDirectory = Path.Combine(RuntimePaths.MapsDirectory, $".{levelName}.{Guid.NewGuid():N}.download");
        var backupDirectory = Path.Combine(RuntimePaths.MapsDirectory, $".{levelName}.{Guid.NewGuid():N}.old");
        var finalManifestPath = CustomMapLocatorStore.GetPackageManifestPath(levelName);
        try
        {
            Directory.CreateDirectory(tempDirectory);
            var tempManifestPath = Path.Combine(tempDirectory, $"{levelName}.json");
            var manifestResult = await TryDownloadToFileAsync(
                    httpClient,
                    manifestUri,
                    tempManifestPath,
                    IsSupportedManifestContentType,
                    "Map package manifest")
                .ConfigureAwait(false);
            if (!manifestResult.Success)
            {
                return manifestResult;
            }

            if (!CustomMapPackageImporter.TryReadManifest(tempManifestPath, out var manifest, out var error))
            {
                return CustomMapSyncResult.Fail(error);
            }

            foreach (var imageReference in CustomMapPackageImporter.GetReferencedImagePaths(manifest))
            {
                if (!CustomMapPackageImporter.TryResolvePackageImagePath(
                    tempDirectory,
                    imageReference,
                    requireFileExists: false,
                    out var imageOutputPath,
                    out var normalizedRelativePath))
                {
                    return CustomMapSyncResult.Fail($"Package image reference is invalid: {imageReference}");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(imageOutputPath)!);
                var imageUri = new Uri(manifestUri, normalizedRelativePath);
                var imageResult = await TryDownloadToFileAsync(
                        httpClient,
                        imageUri,
                        imageOutputPath,
                        IsSupportedPngContentType,
                        "Map package image")
                    .ConfigureAwait(false);
                if (!imageResult.Success)
                {
                    return imageResult;
                }
            }

            if (expectedHash.HasValue
                && !CustomMapHashService.PackageMatchesHash(tempManifestPath, expectedHash))
            {
                return CustomMapSyncResult.Fail("Downloaded map package hash does not match the server hash.");
            }

            Directory.CreateDirectory(RuntimePaths.MapsDirectory);
            if (File.Exists(finalManifestPath))
            {
                if (Directory.Exists(backupDirectory))
                {
                    Directory.Delete(backupDirectory, recursive: true);
                }

                Directory.Move(finalDirectory, backupDirectory);
            }
            else if (Directory.Exists(finalDirectory))
            {
                Directory.Move(finalDirectory, backupDirectory);
            }

            try
            {
                Directory.Move(tempDirectory, finalDirectory);
            }
            catch
            {
                if (Directory.Exists(backupDirectory) && !Directory.Exists(finalDirectory))
                {
                    Directory.Move(backupDirectory, finalDirectory);
                }

                throw;
            }

            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, recursive: true);
            }

            var legacyMapPath = CustomMapLocatorStore.GetMapPath(levelName);
            if (File.Exists(legacyMapPath))
            {
                File.Delete(legacyMapPath);
            }

            return CustomMapSyncResult.Ok;
        }
        catch (Exception ex)
        {
            return CustomMapSyncResult.Fail($"Map package download failed: {ex.Message}");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
            TryDeleteDirectory(backupDirectory);
        }
    }

    private static bool TryDownloadToFile(
        HttpClient httpClient,
        Uri uri,
        string outputPath,
        Func<string?, bool> contentTypeValidator,
        string label,
        out string error)
    {
        error = string.Empty;
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            error = $"{label} download failed ({(int)response.StatusCode} {response.ReasonPhrase}).";
            return false;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!contentTypeValidator(mediaType))
        {
            error = $"{label} download returned unsupported content type: {mediaType}.";
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var networkStream = response.Content.ReadAsStream();
        using var fileStream = File.Create(outputPath);
        networkStream.CopyTo(fileStream);
        return true;
    }

    private static async Task<CustomMapSyncResult> TryDownloadToFileAsync(
        HttpClient httpClient,
        Uri uri,
        string outputPath,
        Func<string?, bool> contentTypeValidator,
        string label)
    {
        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return CustomMapSyncResult.Fail($"{label} download failed ({(int)response.StatusCode} {response.ReasonPhrase}).");
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!contentTypeValidator(mediaType))
        {
            return CustomMapSyncResult.Fail($"{label} download returned unsupported content type: {mediaType}.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var payload = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        File.WriteAllBytes(outputPath, payload);
        return CustomMapSyncResult.Ok;
    }

    private static bool IsSupportedManifestContentType(string? mediaType)
    {
        return string.IsNullOrWhiteSpace(mediaType)
            || mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
            || mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedPngContentType(string? mediaType)
    {
        return string.IsNullOrWhiteSpace(mediaType)
            || mediaType.Contains("png", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void CacheLocator(string levelName, string mapDownloadUrl, CustomMapHashValue expectedHash)
    {
        if (string.IsNullOrWhiteSpace(mapDownloadUrl) && !expectedHash.HasValue)
        {
            return;
        }

        var existing = CustomMapLocatorStore.TryReadMapMetadata(levelName);
        var sourceUrl = string.IsNullOrWhiteSpace(mapDownloadUrl)
            ? existing?.SourceUrl ?? string.Empty
            : mapDownloadUrl.Trim();
        var md5Hash = expectedHash.Algorithm == CustomMapHashAlgorithm.Md5
            ? expectedHash.Value
            : existing?.Md5Hash ?? string.Empty;
        var sha256Hash = expectedHash.Algorithm == CustomMapHashAlgorithm.Sha256
            ? expectedHash.Value
            : existing?.Sha256Hash ?? string.Empty;
        CustomMapLocatorStore.WriteMapMetadata(levelName, new CustomMapLocatorMetadata(sourceUrl, md5Hash, sha256Hash));
    }
}
