using System.Net;
using System.Net.Http;
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
        try
        {
            var normalizedPath = GameplayPackAssetPathUtility.NormalizePackRelativePath(assetPath);
            fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, normalizedPath));
            var baseDirectoryPrefix = _baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(baseDirectoryPrefix, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            fullPath = string.Empty;
            return false;
        }
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
        var normalizedPath = GameplayPackAssetPathUtility.NormalizePackRelativePath(assetPath);
        return $"{_packBaseUrl}/{normalizedPath}";
    }

    private static string NormalizeBaseUrl(string packBaseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packBaseUrl);
        return packBaseUrl.TrimEnd('/', '\\');
    }
}
