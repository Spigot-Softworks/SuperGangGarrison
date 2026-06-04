using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class CustomMapSyncService
{
    private static readonly char[] SchemePrefixSeparators = ['/', '?', '#'];
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    internal readonly record struct CustomMapSyncResult(bool Success, string Error)
    {
        public static CustomMapSyncResult Ok { get; } = new(true, string.Empty);
        public static CustomMapSyncResult Fail(string error) => new(false, error);
    }

    internal readonly record struct CustomMapSyncProgress(string Message, double? Progress);

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
        Uri? serverDownloadBaseUri,
        IProgress<CustomMapSyncProgress>? progress = null)
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
            httpClient,
            progress);
    }

    internal static async Task<CustomMapSyncResult> EnsureMapAvailableAsync(
        string levelName,
        bool isCustomMap,
        string mapDownloadUrl,
        string mapContentHash,
        Uri? serverDownloadBaseUri,
        HttpClient httpClient,
        IProgress<CustomMapSyncProgress>? progress = null)
    {
        if (!isCustomMap)
        {
            ReportProgress(progress, "Loading map...", 1d);
            return CustomMapSyncResult.Ok;
        }

        ReportProgress(progress, "Checking custom map...", null);

        if (!CustomMapLocatorStore.TryNormalizeLevelName(levelName, out var normalizedLevelName))
        {
            return CustomMapSyncResult.Fail($"Invalid custom map name: {levelName}");
        }

        var expectedHash = CustomMapHashService.ParseHash(mapContentHash);
        var downloadUrl = ResolveDownloadUrl(normalizedLevelName, mapDownloadUrl, serverDownloadBaseUri, out var downloadUrlError);
        var mapPath = CustomMapLocatorStore.GetMapPath(normalizedLevelName);
        var hasExpectedHash = expectedHash.HasValue;
        if (File.Exists(mapPath)
            && (!hasExpectedHash || CustomMapHashService.FileMatchesHash(mapPath, expectedHash)))
        {
            if (string.IsNullOrWhiteSpace(downloadUrlError))
            {
                CacheLocator(normalizedLevelName, downloadUrl, expectedHash);
            }

            ReportProgress(progress, "Loading map...", 1d);
            return CustomMapSyncResult.Ok;
        }

        if (CustomMapLocatorStore.TryGetPackageManifestPath(normalizedLevelName, out var packageManifestPath)
            && (!hasExpectedHash || CustomMapHashService.PackageMatchesHash(packageManifestPath, expectedHash)))
        {
            if (string.IsNullOrWhiteSpace(downloadUrlError))
            {
                CacheLocator(normalizedLevelName, downloadUrl, expectedHash);
            }

            ReportProgress(progress, "Loading map...", 1d);
            return CustomMapSyncResult.Ok;
        }

        if (!string.IsNullOrWhiteSpace(downloadUrlError))
        {
            return CustomMapSyncResult.Fail(downloadUrlError);
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return CustomMapSyncResult.Fail($"Missing map {normalizedLevelName}. Server did not provide a download URL.");
        }

        var isPackageDownload = ShouldDownloadPackage(downloadUrl, expectedHash);
        ReportProgress(progress, "Downloading custom map...", null);
        var result = isPackageDownload
            ? await TryDownloadPackageAsync(httpClient, normalizedLevelName, downloadUrl, expectedHash, progress).ConfigureAwait(false)
            : await TryDownloadLegacyMapAsync(httpClient, downloadUrl, mapPath, expectedHash, progress).ConfigureAwait(false);
        if (!result.Success)
        {
            return result;
        }

        ReportProgress(progress, "Loading map...", 1d);
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
        var downloadUrl = ResolveDownloadUrl(normalizedLevelName, mapDownloadUrl, serverDownloadBaseUri, out var downloadUrlError);
        var mapPath = CustomMapLocatorStore.GetMapPath(normalizedLevelName);
        var hasExpectedHash = expectedHash.HasValue;
        if (File.Exists(mapPath)
            && (!hasExpectedHash || CustomMapHashService.FileMatchesHash(mapPath, expectedHash)))
        {
            if (string.IsNullOrWhiteSpace(downloadUrlError))
            {
                CacheLocator(normalizedLevelName, downloadUrl, expectedHash);
            }

            return true;
        }

        if (CustomMapLocatorStore.TryGetPackageManifestPath(normalizedLevelName, out var packageManifestPath)
            && (!hasExpectedHash || CustomMapHashService.PackageMatchesHash(packageManifestPath, expectedHash)))
        {
            if (string.IsNullOrWhiteSpace(downloadUrlError))
            {
                CacheLocator(normalizedLevelName, downloadUrl, expectedHash);
            }

            return true;
        }

        if (!string.IsNullOrWhiteSpace(downloadUrlError))
        {
            error = downloadUrlError;
            return false;
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

    private static string ResolveDownloadUrl(
        string levelName,
        string mapDownloadUrl,
        Uri? serverDownloadBaseUri,
        out string error)
    {
        error = string.Empty;

        if (!string.IsNullOrWhiteSpace(mapDownloadUrl))
        {
            var trimmedUrl = mapDownloadUrl.Trim();
            return ResolveDownloadUrlValue(trimmedUrl, serverDownloadBaseUri, out error);
        }

        var cachedUrl = CustomMapLocatorStore.TryReadMapUrl(levelName);
        return string.IsNullOrWhiteSpace(cachedUrl)
            ? string.Empty
            : ResolveDownloadUrlValue(cachedUrl.Trim(), null, out error);
    }

    private static string ResolveDownloadUrlValue(string trimmedUrl, Uri? serverDownloadBaseUri, out string error)
    {
        error = string.Empty;

        if (Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var absoluteUri))
        {
            if (IsSupportedDownloadUri(absoluteUri))
            {
                return absoluteUri.ToString();
            }

            error = FormatUnsupportedDownloadUrlScheme(absoluteUri.Scheme);
            return string.Empty;
        }

        if (TryGetSchemeLikePrefix(trimmedUrl, out var scheme))
        {
            error = FormatUnsupportedDownloadUrlScheme(scheme);
            return string.Empty;
        }

        if (serverDownloadBaseUri is not null
            && Uri.TryCreate(serverDownloadBaseUri, trimmedUrl, out var resolvedUri))
        {
            if (IsSupportedDownloadUri(resolvedUri))
            {
                return resolvedUri.ToString();
            }

            error = FormatUnsupportedDownloadUrlScheme(resolvedUri.Scheme);
            return string.Empty;
        }

        return trimmedUrl;
    }

    private static bool TryGetSchemeLikePrefix(string value, out string scheme)
    {
        scheme = string.Empty;
        var colonIndex = value.IndexOf(':');
        if (colonIndex <= 0)
        {
            return false;
        }

        var separatorIndex = value.IndexOfAny(SchemePrefixSeparators);
        if (separatorIndex >= 0 && separatorIndex < colonIndex)
        {
            return false;
        }

        scheme = value[..colonIndex].Trim();
        return scheme.Length > 0;
    }

    private static bool TryCreateSupportedDownloadUri(
        string value,
        string invalidLabel,
        out Uri uri,
        out string error)
    {
        error = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var createdUri))
        {
            uri = null!;
            error = $"Invalid {invalidLabel}: {value}";
            return false;
        }

        if (!IsSupportedDownloadUri(createdUri))
        {
            uri = null!;
            error = FormatUnsupportedDownloadUrlScheme(createdUri.Scheme);
            return false;
        }

        uri = createdUri;
        return true;
    }

    private static bool IsSupportedDownloadUri(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedDownloadUrl(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            && IsSupportedDownloadUri(uri);
    }

    private static string FormatUnsupportedDownloadUrlScheme(string scheme)
    {
        var displayScheme = string.IsNullOrWhiteSpace(scheme) ? "(empty)" : scheme;
        return $"Unsupported map download URL scheme: {displayScheme}. Server must provide an http or https map download URL.";
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
        if (Uri.TryCreate(mapDownloadUrl, UriKind.Absolute, out var uri)
            && IsSupportedDownloadUri(uri))
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
        if (!TryCreateSupportedDownloadUri(mapDownloadUrl, "map download URL", out var mapUri, out error))
        {
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
        CustomMapHashValue expectedHash,
        IProgress<CustomMapSyncProgress>? progress)
    {
        if (!TryCreateSupportedDownloadUri(mapDownloadUrl, "map download URL", out var mapUri, out var error))
        {
            return CustomMapSyncResult.Fail(error);
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

            using (var networkStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var fileStream = File.Create(tempPath))
            {
                await CopyToFileWithProgressAsync(
                        networkStream,
                        fileStream,
                        response.Content.Headers.ContentLength,
                        "Downloading custom map...",
                        progress)
                    .ConfigureAwait(false);
            }

            ReportProgress(progress, "Verifying custom map...", null);
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
        if (!TryCreateSupportedDownloadUri(manifestDownloadUrl, "map package manifest URL", out var manifestUri, out error))
        {
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
        CustomMapHashValue expectedHash,
        IProgress<CustomMapSyncProgress>? progress)
    {
        if (!TryCreateSupportedDownloadUri(manifestDownloadUrl, "map package manifest URL", out var manifestUri, out var packageError))
        {
            return CustomMapSyncResult.Fail(packageError);
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
                    "Map package manifest",
                    progress)
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
                        "Map package image",
                        progress)
                    .ConfigureAwait(false);
                if (!imageResult.Success)
                {
                    return imageResult;
                }
            }

            ReportProgress(progress, "Verifying custom map package...", null);
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
        string label,
        IProgress<CustomMapSyncProgress>? progress)
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
        using (var networkStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
        using (var fileStream = File.Create(outputPath))
        {
            await CopyToFileWithProgressAsync(
                    networkStream,
                    fileStream,
                    response.Content.Headers.ContentLength,
                    $"Downloading {label.ToLowerInvariant()}...",
                    progress)
                .ConfigureAwait(false);
        }

        return CustomMapSyncResult.Ok;
    }

    private static async Task CopyToFileWithProgressAsync(
        Stream source,
        Stream destination,
        long? totalBytes,
        string message,
        IProgress<CustomMapSyncProgress>? progress)
    {
        var buffer = new byte[81920];
        long copiedBytes = 0;
        ReportProgress(progress, message, totalBytes.HasValue && totalBytes.Value > 0 ? 0d : null);

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
            if (bytesRead <= 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
            copiedBytes += bytesRead;
            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                ReportProgress(progress, message, Math.Clamp(copiedBytes / (double)totalBytes.Value, 0d, 1d));
            }
        }

        ReportProgress(progress, message, totalBytes.HasValue && totalBytes.Value > 0 ? 1d : null);
    }

    private static void ReportProgress(IProgress<CustomMapSyncProgress>? progress, string message, double? value)
    {
        progress?.Report(new CustomMapSyncProgress(message, value.HasValue ? Math.Clamp(value.Value, 0d, 1d) : null));
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
        var existingSourceUrl = existing?.SourceUrl ?? string.Empty;
        var sourceUrl = string.IsNullOrWhiteSpace(mapDownloadUrl)
            ? IsSupportedDownloadUrl(existingSourceUrl)
                ? existingSourceUrl.Trim()
                : string.Empty
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
