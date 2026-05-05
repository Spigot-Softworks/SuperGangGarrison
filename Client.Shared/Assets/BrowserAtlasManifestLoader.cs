using System.Net.Http;
using System.Text.Json;
using OpenGarrison.Core;
using System.IO;

namespace OpenGarrison.ClientShared;

public static class BrowserAtlasManifestLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static Task<BrowserAtlasManifest> LoadBootstrapAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        return LoadAsync<BrowserAtlasManifest>(httpClient, BrowserDistributionPaths.BrowserBootstrapAtlasManifestPath, cancellationToken);
    }

    public static Task<BrowserGameplayAtlasManifest> LoadStockGameplayAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        return LoadAsync<BrowserGameplayAtlasManifest>(httpClient, BrowserDistributionPaths.BrowserStockGameplayAtlasManifestPath, cancellationToken);
    }

    public static Task<BrowserGameMakerAtlasManifest> LoadGameMakerAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        return LoadAsync<BrowserGameMakerAtlasManifest>(httpClient, BrowserDistributionPaths.BrowserGameMakerAtlasManifestPath, cancellationToken);
    }

    public static BrowserAtlasManifest? TryLoadBootstrapFromFile()
    {
        return TryLoadFromFile<BrowserAtlasManifest>(BrowserDistributionPaths.BrowserBootstrapAtlasManifestPath);
    }

    public static BrowserGameplayAtlasManifest? TryLoadStockGameplayFromFile()
    {
        return TryLoadFromFile<BrowserGameplayAtlasManifest>(BrowserDistributionPaths.BrowserStockGameplayAtlasManifestPath);
    }

    public static BrowserGameMakerAtlasManifest? TryLoadGameMakerFromFile()
    {
        return TryLoadFromFile<BrowserGameMakerAtlasManifest>(BrowserDistributionPaths.BrowserGameMakerAtlasManifestPath);
    }

    private static async Task<T> LoadAsync<T>(HttpClient httpClient, string relativePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        using var response = await httpClient.GetAsync(relativePath, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return value ?? throw new InvalidOperationException($"Browser atlas manifest \"{relativePath}\" could not be deserialized as {typeof(T).Name}.");
    }

    private static T? TryLoadFromFile<T>(string relativePath) where T : class
    {
        var localPath = TryResolveLocalContentPath(relativePath);
        if (localPath is null || !File.Exists(localPath))
        {
            return null;
        }

        using var stream = File.OpenRead(localPath);
        var value = JsonSerializer.Deserialize<T>(stream, JsonOptions);
        return value ?? throw new InvalidOperationException($"Local atlas manifest \"{localPath}\" could not be deserialized as {typeof(T).Name}.");
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
