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
        if (File.Exists(mapPath))
        {
            if (!expectedHash.HasValue)
            {
                CacheLocator(normalizedLevelName, mapDownloadUrl, expectedHash);
                return true;
            }

            if (CustomMapHashService.FileMatchesHash(mapPath, expectedHash.Value))
            {
                CacheLocator(normalizedLevelName, mapDownloadUrl, expectedHash);
                return true;
            }
        }

        var downloadUrl = ResolveDownloadUrl(normalizedLevelName, mapDownloadUrl);
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            error = $"Missing map {normalizedLevelName}. Server did not provide a download URL.";
            return false;
        }

        if (!TryDownloadMap(downloadUrl, mapPath, expectedHash, out error))
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

    private static bool TryDownloadMap(string mapDownloadUrl, string mapPath, CustomMapHashValue expectedHash, out string error)
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
            using var response = HttpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
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
                if (!CustomMapHashService.FileMatchesHash(tempPath, expectedHash.Value))
                {
                    error = "Downloaded map hash does not match the server hash.";
                    return false;
                }
            }

            File.Move(tempPath, mapPath, overwrite: true);
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
