using System.Net.Http;
using System.Text.Json;
using OpenGarrison.Core;

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

    private static async Task<T> LoadAsync<T>(HttpClient httpClient, string relativePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        using var response = await httpClient.GetAsync(relativePath, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return value ?? throw new InvalidOperationException($"Browser atlas manifest \"{relativePath}\" could not be deserialized as {typeof(T).Name}.");
    }
}
