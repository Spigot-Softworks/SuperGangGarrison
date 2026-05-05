using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using System.IO;

namespace OpenGarrison.Client;

internal static class BrowserAssetFetchUtility
{
    public static async Task<byte[]?> TryGetBytesAsync(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (BrowserContentCatalog.TryGetBinary(relativePath, out var cachedBytes))
        {
            return cachedBytes;
        }

        if (!OperatingSystem.IsBrowser())
        {
            var localPath = TryResolveLocalContentPath(relativePath);
            if (localPath is null || !File.Exists(localPath))
            {
                return null;
            }

            return File.ReadAllBytes(localPath);
        }

        var httpClient = ClientRuntimeBootstrap.GetBrowserHttpClient();
        if (httpClient is null)
        {
            return null;
        }

        try
        {
            using var response = await httpClient.GetAsync(relativePath, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            if (bytes.Length > 0)
            {
                BrowserContentCatalog.AddOrUpdateBinaryAssets(
                [
                    new KeyValuePair<string, byte[]>(relativePath, bytes),
                ]);
            }

            return bytes;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryResolveLocalContentPath(string relativePath)
    {
        var normalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
        const string contentPrefix = "Content/";
        if (normalizedPath.StartsWith(contentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = normalizedPath[contentPrefix.Length..];
        }

        return string.IsNullOrWhiteSpace(normalizedPath)
            ? null
            : ContentRoot.GetPath(normalizedPath);
    }
}
