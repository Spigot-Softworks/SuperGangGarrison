using System.Net;
using System.Net.Http;
using OpenGarrison.Core;

namespace OpenGarrison.ClientShared;

public interface IAssetBinarySource
{
    byte[]? TryReadAllBytes(string assetPath);
    Task<byte[]?> TryReadAllBytesAsync(string assetPath, CancellationToken cancellationToken = default);
}

public sealed class FileSystemAssetBinarySource(string baseDirectory) : IAssetBinarySource
{
    private readonly string _baseDirectory = Path.GetFullPath(baseDirectory);

    public byte[]? TryReadAllBytes(string assetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);

        if (!TryResolvePath(assetPath, out var fullPath) || !File.Exists(fullPath))
        {
            return null;
        }

        return File.ReadAllBytes(fullPath);
    }

    public Task<byte[]?> TryReadAllBytesAsync(string assetPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(TryReadAllBytes(assetPath));
    }

    private bool TryResolvePath(string assetPath, out string fullPath)
    {
        if (GameplayPackAssetPathUtility.IsContentRootRelativePath(assetPath))
        {
            var normalizedPath = assetPath.Replace('\\', '/').TrimStart('/');
            const string contentPrefix = "Content/";
            fullPath = Path.GetFullPath(ContentRoot.GetPath(normalizedPath[contentPrefix.Length..]));
            var fullContentRoot = Path.GetFullPath(ContentRoot.Path);
            return fullPath.StartsWith(fullContentRoot, StringComparison.OrdinalIgnoreCase);
        }

        fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, assetPath));
        return fullPath.StartsWith(_baseDirectory, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class HttpAssetBinarySource(HttpClient httpClient, string packBaseUrl) : IAssetBinarySource
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _packBaseUrl = NormalizeBaseUrl(packBaseUrl);

    public byte[]? TryReadAllBytes(string assetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);

        if (OperatingSystem.IsBrowser())
        {
            return null;
        }

        try
        {
            return TryReadAllBytesAsync(assetPath).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    public async Task<byte[]?> TryReadAllBytesAsync(string assetPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);

        var assetUrl = BuildAssetUrl(assetPath);
        if (OperatingSystem.IsBrowser()
            && OpenGarrison.Core.BrowserContentCatalog.TryGetBinary(assetUrl, out var cachedBytes))
        {
            return cachedBytes;
        }

        using var response = await _httpClient.GetAsync(
            assetUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (OperatingSystem.IsBrowser() && bytes.Length > 0)
        {
            OpenGarrison.Core.BrowserContentCatalog.AddOrUpdateBinaryAssets(
            [
                new KeyValuePair<string, byte[]>(assetUrl, bytes),
            ]);
        }

        return bytes;
    }

    private string BuildAssetUrl(string assetPath)
    {
        var normalizedPath = assetPath.TrimStart('/').Replace('\\', '/');
        return GameplayPackAssetPathUtility.IsContentRootRelativePath(normalizedPath)
            ? normalizedPath
            : $"{_packBaseUrl}/{normalizedPath}";
    }

    private static string NormalizeBaseUrl(string packBaseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packBaseUrl);
        return packBaseUrl.TrimEnd('/', '\\');
    }
}
