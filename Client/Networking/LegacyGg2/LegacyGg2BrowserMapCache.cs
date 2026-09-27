#nullable enable

using System.Net.Http;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>Registers GG2 PNG maps in the browser's in-memory content catalog.</summary>
internal static class LegacyGg2BrowserMapCache
{
    private const int MaxMapBytes = 64 * 1024 * 1024;

    internal static bool IsGg2LevelName(string levelName)
        => levelName.StartsWith("gg2_stock_", StringComparison.OrdinalIgnoreCase)
            || levelName.StartsWith("gg2_", StringComparison.OrdinalIgnoreCase)
                && levelName.Length == 36 && levelName.AsSpan(4).ToString().All(Uri.IsHexDigit);

    internal static async Task<CustomMapSyncService.CustomMapSyncResult> EnsureMapAvailableAsync(
        string levelName,
        string mapDownloadUrl,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        if (!IsGg2LevelName(levelName))
            return CustomMapSyncService.CustomMapSyncResult.Fail("Invalid GG2 map name.");

        var isStock = levelName.StartsWith("gg2_stock_", StringComparison.OrdinalIgnoreCase);
        var mapName = isStock ? levelName["gg2_stock_".Length..] : GetMapNameFromUrl(mapDownloadUrl);
        if (string.IsNullOrWhiteSpace(mapName)
            || mapName.Any(character => character is '/' or '\\' || char.IsControl(character)))
            return CustomMapSyncService.CustomMapSyncResult.Fail("Invalid GG2 map name.");

        var path = ContentRoot.GetPath("Gg2Maps", levelName + ".png");
        if (BrowserContentCatalog.TryGetBinaryForPath(path, out _)
            && TryRegister(levelName, path, mapName))
            return CustomMapSyncService.CustomMapSyncResult.Ok;

        byte[]? bytes;
        if (isStock)
        {
            bytes = await BrowserAssetFetchUtility.TryGetBytesAsync(
                $"Content/StockMaps/Gg2/{mapName}.png");
        }
        else
        {
            if (!Uri.TryCreate(mapDownloadUrl, UriKind.Absolute, out var mapUri)
                || mapUri.Scheme != Uri.UriSchemeHttps && mapUri.Scheme != Uri.UriSchemeHttp
                || !SameOrigin(mapUri, ClientDistribution.RoomServiceOrigin))
                return CustomMapSyncService.CustomMapSyncResult.Fail("GG2 map URL is outside the gateway.");
            using var response = await httpClient.GetAsync(mapUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return CustomMapSyncService.CustomMapSyncResult.Fail($"GG2 map download failed ({(int)response.StatusCode}).");
            if (response.Content.Headers.ContentLength is > MaxMapBytes)
                return CustomMapSyncService.CustomMapSyncResult.Fail("GG2 map exceeds the download limit.");
            bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        if (bytes is null || bytes.Length is 0 or > MaxMapBytes)
            return CustomMapSyncService.CustomMapSyncResult.Fail("GG2 map download was empty or too large.");
        if (!isStock && !string.Equals(
                LegacyGg2Md5.ComputeHex(bytes), levelName[4..], StringComparison.OrdinalIgnoreCase))
            return CustomMapSyncService.CustomMapSyncResult.Fail("GG2 map did not match the advertised MD5.");

        BrowserContentCatalog.AddOrUpdateBinaryAssets(
        [
            new KeyValuePair<string, byte[]>($"Content/Gg2Maps/{levelName}.png", bytes),
        ]);
        return TryRegister(levelName, path, mapName)
            ? CustomMapSyncService.CustomMapSyncResult.Ok
            : CustomMapSyncService.CustomMapSyncResult.Fail("GG2 map has no usable walkmask or player spawns.");
    }

    private static bool TryRegister(string levelName, string path, string mapName)
    {
        SimpleLevelFactory.RegisterExternalLegacyPngLevel(levelName, path, mapName);
        return SimpleLevelFactory.CreateImportedLevel(levelName) is not null;
    }

    private static string GetMapNameFromUrl(string mapDownloadUrl)
    {
        if (!Uri.TryCreate(mapDownloadUrl, UriKind.Absolute, out var uri)) return string.Empty;
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("name=", StringComparison.Ordinal))
                return Uri.UnescapeDataString(part[5..]);
        }
        return string.Empty;
    }

    private static bool SameOrigin(Uri left, Uri right)
        => string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
            && left.Port == right.Port
            && left.Scheme == right.Scheme;
}
