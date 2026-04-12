using System.Net.Http;
using OpenGarrison.Core;

namespace OpenGarrison.ClientShared;

public static class ClientRuntimeBootstrap
{
    private static Uri? _browserBaseAddress;
    private static BrowserBootstrapAssetCatalog? _browserBootstrapAssetCatalog;
    private static BrowserAtlasManifest? _browserBootstrapAtlasManifest;
    private static BrowserGameplayAtlasManifest? _browserStockGameplayAtlasManifest;
    private static BrowserGameMakerAtlasManifest? _browserGameMakerAtlasManifest;
    private static GameMakerAssetManifest? _browserRuntimeAssetManifest;
    private static HttpClient? _browserHttpClient;

    public static void InitializeContentRoot(string rootDirectory = "Content")
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("The content root directory must not be null or whitespace.", nameof(rootDirectory));
        }

        ContentRoot.Initialize(rootDirectory);
    }

    public static void InitializeBrowserBaseAddress(string? baseAddress)
    {
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            return;
        }

        _browserBaseAddress = new Uri(baseAddress, UriKind.Absolute);
    }

    public static Uri? GetBrowserBaseAddress()
    {
        return _browserBaseAddress;
    }

    public static void SetBrowserBootstrapAssetCatalog(BrowserBootstrapAssetCatalog? assetCatalog)
    {
        _browserBootstrapAssetCatalog = assetCatalog;
    }

    public static BrowserBootstrapAssetCatalog? GetBrowserBootstrapAssetCatalog()
    {
        return _browserBootstrapAssetCatalog;
    }

    public static void SetBrowserBootstrapAtlasManifest(BrowserAtlasManifest? atlasManifest)
    {
        _browserBootstrapAtlasManifest = atlasManifest;
    }

    public static BrowserAtlasManifest? GetBrowserBootstrapAtlasManifest()
    {
        return _browserBootstrapAtlasManifest;
    }

    public static void SetBrowserStockGameplayAtlasManifest(BrowserGameplayAtlasManifest? atlasManifest)
    {
        _browserStockGameplayAtlasManifest = atlasManifest;
    }

    public static BrowserGameplayAtlasManifest? GetBrowserStockGameplayAtlasManifest()
    {
        return _browserStockGameplayAtlasManifest;
    }

    public static void SetBrowserGameMakerAtlasManifest(BrowserGameMakerAtlasManifest? atlasManifest)
    {
        _browserGameMakerAtlasManifest = atlasManifest;
    }

    public static BrowserGameMakerAtlasManifest? GetBrowserGameMakerAtlasManifest()
    {
        return _browserGameMakerAtlasManifest;
    }

    public static void SetBrowserRuntimeAssetManifest(GameMakerAssetManifest? assetManifest)
    {
        _browserRuntimeAssetManifest = assetManifest;
    }

    public static GameMakerAssetManifest? GetBrowserRuntimeAssetManifest()
    {
        return _browserRuntimeAssetManifest;
    }

    public static void InitializeBrowserHttpClient(HttpClient? httpClient)
    {
        if (httpClient is null)
        {
            return;
        }

        _browserHttpClient = httpClient;
    }

    public static HttpClient? GetBrowserHttpClient()
    {
        return _browserHttpClient;
    }

    public static IAssetBinarySource? CreateGameplayPackAssetBinarySource(string packId, HttpClient? httpClient = null, string? packDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);

        if (OperatingSystem.IsBrowser())
        {
            httpClient ??= _browserHttpClient;
            if (httpClient is null)
            {
                throw new ArgumentNullException(nameof(httpClient), "Browser gameplay asset loading requires an HttpClient.");
            }

            return new HttpAssetBinarySource(httpClient, GameplayPackAssetPathUtility.GetPackContentRoot(packId));
        }

        var resolvedPackDirectory = string.IsNullOrWhiteSpace(packDirectory)
            ? GameplayModPackDirectoryLoader.FindPackDirectory(packId)
            : packDirectory;

        return string.IsNullOrWhiteSpace(resolvedPackDirectory)
            ? null
            : new FileSystemAssetBinarySource(resolvedPackDirectory);
    }

    public static GameplayPackSpriteAssetService? CreateGameplayPackSpriteAssetService(string packId, HttpClient? httpClient = null, string? packDirectory = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            httpClient ??= _browserHttpClient;
        }

        var assetBinarySource = CreateGameplayPackAssetBinarySource(packId, httpClient, packDirectory);
        if (assetBinarySource is null)
        {
            return null;
        }

        GameplaySpriteDefinitionHttpReader? spriteDefinitionReader = null;
        if (OperatingSystem.IsBrowser())
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            spriteDefinitionReader = new GameplaySpriteDefinitionHttpReader(httpClient);
        }

        return new GameplayPackSpriteAssetService(packId, assetBinarySource, spriteDefinitionReader);
    }
}
