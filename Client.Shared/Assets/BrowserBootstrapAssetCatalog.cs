using System.Net.Http;
using System.Text;

namespace OpenGarrison.ClientShared;

public sealed class BrowserBootstrapAssetCatalog(
    IReadOnlyDictionary<string, byte[]> binaryAssets,
    IReadOnlyDictionary<string, string> textAssets)
{
    private readonly IReadOnlyDictionary<string, byte[]> _binaryAssets = binaryAssets;
    private readonly IReadOnlyDictionary<string, string> _textAssets = textAssets;

    public static BrowserBootstrapAssetCatalog Empty { get; } =
        new BrowserBootstrapAssetCatalog(
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    public bool TryGetBinary(string relativePath, out byte[] bytes)
        => _binaryAssets.TryGetValue(NormalizeRelativePath(relativePath), out bytes!);

    public bool TryGetText(string relativePath, out string text)
        => _textAssets.TryGetValue(NormalizeRelativePath(relativePath), out text!);

    public IReadOnlyDictionary<string, byte[]> GetBinaryAssets() => _binaryAssets;

    public IReadOnlyDictionary<string, string> GetTextAssets() => _textAssets;

    public static IReadOnlyList<string> DefaultBinaryAssetPaths => DefaultBinaryPaths;

    public static IReadOnlyList<string> DefaultTextAssetPaths => DefaultTextPaths;

    public static Task<BrowserBootstrapAssetCatalog> LoadDefaultAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return Task.FromResult(Empty);
        }

        return LoadAsync(DefaultBinaryPaths, DefaultTextPaths, null, cancellationToken);
    }

    public static Task<BrowserBootstrapAssetCatalog> LoadDefaultAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return LoadAsync(DefaultBinaryPaths, DefaultTextPaths, httpClient, cancellationToken);
    }

    private static async Task<BrowserBootstrapAssetCatalog> LoadAsync(
        IReadOnlyList<string> binaryPaths,
        IReadOnlyList<string> textPaths,
        HttpClient? providedHttpClient,
        CancellationToken cancellationToken)
    {
        using var ownedHttpClient = providedHttpClient is null ? new HttpClient() : null;
        var httpClient = providedHttpClient ?? ownedHttpClient!;
        var browserBaseAddress = ClientRuntimeBootstrap.GetBrowserBaseAddress();

        var bundledAssets = await BrowserAssetBundleLoader.TryLoadAsync(
                httpClient,
                BrowserDistributionPaths.BootstrapBundlePath,
                browserBaseAddress,
                cancellationToken)
            .ConfigureAwait(false);
        if (bundledAssets.Count > 0)
        {
            return CreateCatalogFromBundleAssets(bundledAssets, binaryPaths, textPaths);
        }

        var binaryTasks = binaryPaths
            .Select(relativePath => LoadBinaryAssetAsync(httpClient, browserBaseAddress, relativePath, cancellationToken))
            .ToArray();
        var textTasks = textPaths
            .Select(relativePath => LoadTextAssetAsync(httpClient, browserBaseAddress, relativePath, cancellationToken))
            .ToArray();

        var binaryResults = await Task.WhenAll(binaryTasks).ConfigureAwait(false);
        var textResults = await Task.WhenAll(textTasks).ConfigureAwait(false);

        var binaryAssets = binaryResults
            .Where(static result => result.Bytes is not null && result.Bytes.Length > 0)
            .ToDictionary(
                static result => result.Path,
                static result => result.Bytes!,
                StringComparer.OrdinalIgnoreCase);
        var textAssets = textResults
            .Where(static result => !string.IsNullOrEmpty(result.Text))
            .ToDictionary(
                static result => result.Path,
                static result => result.Text!,
                StringComparer.OrdinalIgnoreCase);

        return new BrowserBootstrapAssetCatalog(binaryAssets, textAssets);
    }

    private static BrowserBootstrapAssetCatalog CreateCatalogFromBundleAssets(
        IReadOnlyDictionary<string, byte[]> bundledAssets,
        IReadOnlyList<string> binaryPaths,
        IReadOnlyList<string> textPaths)
    {
        var binaryAssets = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var relativePath in binaryPaths)
        {
            var normalizedPath = NormalizeRelativePath(relativePath);
            if (bundledAssets.TryGetValue(normalizedPath, out var bytes) && bytes.Length > 0)
            {
                binaryAssets[normalizedPath] = bytes;
            }
        }

        var textAssets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relativePath in textPaths)
        {
            var normalizedPath = NormalizeRelativePath(relativePath);
            if (bundledAssets.TryGetValue(normalizedPath, out var bytes) && bytes.Length > 0)
            {
                textAssets[normalizedPath] = Encoding.UTF8.GetString(bytes);
            }
        }

        return new BrowserBootstrapAssetCatalog(binaryAssets, textAssets);
    }

    private static async Task<(string Path, byte[]? Bytes)> LoadBinaryAssetAsync(
        HttpClient httpClient,
        Uri? browserBaseAddress,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizeRelativePath(relativePath);
        try
        {
            var bytes = await httpClient.GetByteArrayAsync(
                    ResolveRequestUri(browserBaseAddress, normalizedPath),
                    cancellationToken)
                .ConfigureAwait(false);
            return (normalizedPath, bytes);
        }
        catch
        {
            return (normalizedPath, null);
        }
    }

    private static async Task<(string Path, string? Text)> LoadTextAssetAsync(
        HttpClient httpClient,
        Uri? browserBaseAddress,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizeRelativePath(relativePath);
        try
        {
            var text = await httpClient.GetStringAsync(
                    ResolveRequestUri(browserBaseAddress, normalizedPath),
                    cancellationToken)
                .ConfigureAwait(false);
            return (normalizedPath, text);
        }
        catch
        {
            return (normalizedPath, null);
        }
    }

    private static string NormalizeRelativePath(string relativePath)
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

    private static readonly string[] BrowserPracticeNavigationLevelNames =
    [
        "Conflict",
        "Eiger",
        "Gallery",
        "Harvest",
        "Montane",
        "Valley",
        "Waterway",
    ];

    private static readonly IReadOnlyList<string> DefaultBinaryPaths = CreateDefaultBinaryPaths();

    private static readonly string[] DefaultTextPaths =
    [
        "Content/Sprites/Menu/Fonts/MenuBuildFontAtlas.json",
        "Content/Sprites/Menu/Fonts/MenuFontAtlas.json",
    ];

    private static List<string> CreateDefaultBinaryPaths()
    {
        var binaryPaths = new List<string>
        {
            "Content/Sprites/Menu/Plaques/MenuPlaque.png",
            "Content/Sprites/Menu/Plaques/MenuPlaqueTall.png",
            "Content/Sprites/Menu/Plaques/MenuTextBoxTop.png",
            "Content/Sprites/Menu/Plaques/MenuTextBoxMiddle.png",
            "Content/Sprites/Menu/Plaques/MenuTextBoxBottom.png",
            "Content/Sprites/Menu/Plaques/MenuTextBoxSolo.png",
            "Content/Sprites/Menu/Plaques/LTD_MenuPlaque.png",
            "Content/Sprites/Menu/Plaques/LTD_MenuTextBoxSolo.png",
            "Content/Sprites/Menu/RandomizerLoadout/LoadoutStrip.png",
            "Content/Sprites/Menu/RandomizerLoadout/LoadoutSelectionStrip.png",
            "Content/Sprites/Menu/RandomizerLoadout/LoadoutBackgroundBar.png",
            "Content/Sprites/Menu/RandomizerLoadout/DescriptionBoardS.png",
            "Content/Sprites/Menu/RandomizerLoadout/SelectionS.chunk0.png",
            "Content/Sprites/Menu/RandomizerLoadout/SelectionS.chunk1.png",
            "Content/Sprites/Menu/RandomizerLoadout/SelectionS.chunk2.png",
            "Content/Sprites/Menu/RandomizerLoadout/SelectionS.chunk3.png",
            "Content/Sprites/Menu/RandomizerLoadout/SelectionS2.png",
            "Content/Sprites/Menu/RandomizerLoadout/ScrollerS.png",
            "Content/Sprites/Menu/RandomizerLoadout/PageS.png",
            "Content/Sprites/Menu/RandomizerLoadout/BackS.png",
            "Content/Sprites/Menu/Fonts/MenuBuildFontAtlas.png",
            "Content/Sprites/Menu/Fonts/MenuFontAtlas.png",
            "Content/Sprites/Menu/LastToDie/last2die.png",
            "Content/Sprites/Menu/LastToDie/ltd_buff.png",
            "Content/Sprites/InGameElements/CustomBubble/big_bubble.png",
            "Content/Sprites/Menu/Title/background.png",
            "Content/Sprites/Menu/Title/background-4x3.png",
            "Content/Sprites/Menu/Title/background-5x4.png",
        };

        for (var index = 1; index <= 6; index += 1)
        {
            binaryPaths.Add($"Content/Sounds/Music/menumusic{index}.ogg");
        }

        binaryPaths.Add("Content/Sounds/Music/faucetmusic.ogg");
        binaryPaths.Add("Content/Sounds/Music/ingamemusic.ogg");
        binaryPaths.Add("Content/Sounds/Music/ingame_l2d.ogg");
        binaryPaths.Add("Content/Sounds/Music/ltdgameover.fixed.ogg");
        binaryPaths.Add("Content/Sounds/Music/menu-l2d.fixed.ogg");
        binaryPaths.Add("Content/Sounds/message.ogg");

        // Browser practice/Last To Die rely on shipped point-graph assets, and the browser runtime
        // cannot safely block on async fetches once gameplay startup begins.
        foreach (var levelName in BrowserPracticeNavigationLevelNames)
        {
            binaryPaths.Add($"Content/BotNav/{levelName}.a1.modern.botnav.json");
        }

        return binaryPaths;
    }
}
