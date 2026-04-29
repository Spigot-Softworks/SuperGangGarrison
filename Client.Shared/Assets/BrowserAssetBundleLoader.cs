using System.IO.Compression;
using System.Net.Http;

namespace OpenGarrison.ClientShared;

public static class BrowserAssetBundleLoader
{
    public static async Task<IReadOnlyDictionary<string, byte[]>> TryLoadAsync(
        HttpClient httpClient,
        string relativePath,
        Uri? browserBaseAddress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        try
        {
            var archiveBytes = await httpClient.GetByteArrayAsync(
                    ResolveRequestUri(browserBaseAddress, NormalizeRelativePath(relativePath)),
                    cancellationToken)
                .ConfigureAwait(false);
            return ReadArchive(archiveBytes);
        }
        catch
        {
            return EmptyAssets;
        }
    }

    public static IReadOnlyDictionary<string, byte[]> ReadArchive(byte[] archiveBytes)
    {
        ArgumentNullException.ThrowIfNull(archiveBytes);

        if (archiveBytes.Length == 0)
        {
            return EmptyAssets;
        }

        using var stream = new MemoryStream(archiveBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        var assets = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FullName) || entry.FullName.EndsWith('/'))
            {
                continue;
            }

            using var entryStream = entry.Open();
            using var entryBuffer = new MemoryStream((int)Math.Max(0, entry.Length));
            entryStream.CopyTo(entryBuffer);
            assets[NormalizeRelativePath(entry.FullName)] = entryBuffer.ToArray();
        }

        return assets.Count == 0 ? EmptyAssets : assets;
    }

    public static string NormalizeRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return relativePath.Replace('\\', '/').TrimStart('/');
    }

    private static Uri ResolveRequestUri(Uri? browserBaseAddress, string normalizedPath)
    {
        if (Uri.TryCreate(normalizedPath, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri;
        }

        if (browserBaseAddress is not null)
        {
            return new Uri(browserBaseAddress, normalizedPath);
        }

        return new Uri(normalizedPath, UriKind.Relative);
    }

    private static readonly IReadOnlyDictionary<string, byte[]> EmptyAssets =
        new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
}
