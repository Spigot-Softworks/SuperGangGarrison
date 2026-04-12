using System.Net.Http;
using System.Text.Json;

namespace OpenGarrison.Core;

public static class BrowserGameMakerAssetManifestLoader
{
    private const string BrowserManifestPath = "Content/_gamemaker-asset-manifest.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<GameMakerAssetManifest> LoadAsync(
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        var json = await httpClient.GetStringAsync(BrowserManifestPath, cancellationToken).ConfigureAwait(false);
        var document = JsonSerializer.Deserialize<BrowserGameMakerAssetManifestDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Browser GameMaker asset manifest \"{BrowserManifestPath}\" could not be deserialized.");
        return document.ToManifest();
    }
}
