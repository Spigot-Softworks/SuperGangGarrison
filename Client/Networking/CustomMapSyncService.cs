using System;
using System.IO;
using System.Net.Http;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class CustomMapSyncService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

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
        var mapPath = CustomMapLocatorStore.GetMapPath(normalizedLevelName);
        var hasExpectedHash = expectedHash.HasValue;
        if (File.Exists(mapPath)
            && (!hasExpectedHash || CustomMapHashService.FileMatchesHash(mapPath, expectedHash)))
        {
            CacheLocator(normalizedLevelName, mapDownloadUrl, expectedHash);
            return true;
        }

        if (CustomMapLocatorStore.TryGetPackageManifestPath(normalizedLevelName, out var packageManifestPath)
            && (!hasExpectedHash || CustomMapHashService.PackageMatchesHash(packageManifestPath, expectedHash)))
        {
            CacheLocator(normalizedLevelName, mapDownloadUrl, expectedHash);
            return true;
        }

        var downloadUrl = ResolveDownloadUrl(normalizedLevelName, mapDownloadUrl);
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

    private static string ResolveDownloadUrl(string levelName, string mapDownloadUrl)
    {
        if (!string.IsNullOrWhiteSpace(mapDownloadUrl))
        {
            return mapDownloadUrl.Trim();
        }

        return CustomMapLocatorStore.TryReadMapUrl(levelName) ?? string.Empty;
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
