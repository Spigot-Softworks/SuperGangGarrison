#nullable enable

using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using System.IO;

namespace OpenGarrison.Client;

public partial class Game1
{
    private Task<BrowserBootstrapAssetCatalog>? _browserBootstrapAssetsTask;
    private BrowserBootstrapAssetCatalog? _browserBootstrapAssets;
    private bool _browserBootstrapAssetsApplied;
    private BrowserAtlasTextureCache? _browserAtlasTextureCache;
    private BrowserBootstrapAtlasTextureResolver? _browserBootstrapAtlasResolver;

    private LoadedSpriteFrame? LoadSpriteFrameFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (TryLoadBrowserFrame(path, out var browserFrame))
        {
            return browserFrame;
        }

        if (OperatingSystem.IsBrowser()
            && BrowserContentCatalog.TryGetBinaryForPath(path, out var browserBytes)
            && browserBytes.Length > 0)
        {
            return new LoadedSpriteFrame(TextureDecodeUtility.LoadTexture(GraphicsDevice, browserBytes, applyLegacyChromaKey: false));
        }

        if (OperatingSystem.IsBrowser() || !File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        return new LoadedSpriteFrame(Texture2D.FromStream(GraphicsDevice, stream));
    }

    private void StartBrowserBootstrapAssetPreloadIfNeeded()
    {
        if (!OperatingSystem.IsBrowser() || _browserBootstrapAssetsTask is not null)
        {
            return;
        }

        var preloadedAssets = ClientRuntimeBootstrap.GetBrowserBootstrapAssetCatalog();
        if (preloadedAssets is not null)
        {
            _browserBootstrapAssets = preloadedAssets;
            _browserBootstrapAssetsApplied = true;
            return;
        }

        var browserHttpClient = ClientRuntimeBootstrap.GetBrowserHttpClient();
        _browserBootstrapAssetsTask = browserHttpClient is null
            ? BrowserBootstrapAssetCatalog.LoadDefaultAsync()
            : BrowserBootstrapAssetCatalog.LoadDefaultAsync(browserHttpClient);
    }

    private void PollBrowserBootstrapAssetPreload()
    {
        if (!OperatingSystem.IsBrowser()
            || _browserBootstrapAssetsApplied
            || _browserBootstrapAssetsTask?.IsCompleted != true)
        {
            return;
        }

        try
        {
            _browserBootstrapAssets = _browserBootstrapAssetsTask.GetAwaiter().GetResult();
            _browserBootstrapAssetsApplied = true;
            EnsureBrowserBootstrapAtlasResolver();
            RefreshBrowserSpriteFontsIfPossible();
            LoadMenuPlaqueTextures();
            LoadGameplayLoadoutMenuTextures();
            LoadMenuBitmapFont();
            AddConsoleLine("browser bootstrap assets ready");
        }
        catch (Exception ex)
        {
            _browserBootstrapAssetsApplied = true;
            AddConsoleLine($"browser bootstrap asset preload failed: {ex.Message}");
        }
    }

    private bool TryLoadBrowserFrame(string path, out LoadedSpriteFrame? frame)
    {
        frame = null;
        if (!TryGetBrowserContentRelativePath(path, out var relativePath))
        {
            return false;
        }

        return TryLoadBrowserFrameByRelativePath(relativePath, out frame);
    }
    private bool TryLoadBrowserFrameByRelativePath(string relativePath, out LoadedSpriteFrame? frame)
    {
        frame = null;
        EnsureBrowserBootstrapAtlasResolver();
        if (_browserBootstrapAtlasResolver?.CanResolve(relativePath) == true)
        {
            var pendingFrame = _browserBootstrapAtlasResolver.LoadFrameAsync(relativePath);
            if (OperatingSystem.IsBrowser() && !pendingFrame.IsCompletedSuccessfully)
            {
                return false;
            }

            frame = pendingFrame.GetAwaiter().GetResult();
            if (frame is not null)
            {
                return true;
            }
        }

        if (_browserBootstrapAssets is not null && _browserBootstrapAssets.TryGetBinary(relativePath, out var bytes))
        {
            using var stream = new MemoryStream(bytes, writable: false);
            frame = new LoadedSpriteFrame(Texture2D.FromStream(GraphicsDevice, stream));
            return true;
        }

        return false;
    }

    private bool TryGetBrowserContentText(string path, out string text)
    {
        text = string.Empty;
        return OperatingSystem.IsBrowser()
            && _browserBootstrapAssets is not null
            && TryGetBrowserContentRelativePath(path, out var relativePath)
            && _browserBootstrapAssets.TryGetText(relativePath, out text);
    }

    private bool CanLoadSpriteFrameFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (File.Exists(path))
        {
            return true;
        }

        if (!TryGetBrowserContentRelativePath(path, out var relativePath))
        {
            return false;
        }

        EnsureBrowserBootstrapAtlasResolver();
        if (_browserBootstrapAtlasResolver?.CanResolve(relativePath) == true)
        {
            return true;
        }

        if (OperatingSystem.IsBrowser())
        {
            if ((_browserBootstrapAssets?.TryGetBinary(relativePath, out _) ?? false)
                || BrowserContentCatalog.TryGetBinary(relativePath, out _)
                || BrowserContentCatalog.TryGetBinaryForPath(path, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetBrowserContentRelativePath(string path, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalizedPath = path.Replace('\\', '/');
        var normalizedRoot = ContentRoot.Path.Replace('\\', '/').TrimEnd('/');
        var marker = normalizedRoot + "/";
        var markerIndex = normalizedPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return false;
        }

        relativePath = normalizedPath.Substring(markerIndex).TrimStart('/');
        return relativePath.Length > 0;
    }

    private void EnsureBrowserBootstrapAtlasResolver()
    {
        if (_browserBootstrapAtlasResolver is not null)
        {
            return;
        }

        if (GraphicsDevice is null)
        {
            return;
        }

        var bootstrapAtlasManifest = ClientRuntimeBootstrap.GetBrowserBootstrapAtlasManifest();
        if (bootstrapAtlasManifest is null)
        {
            return;
        }

        _browserAtlasTextureCache ??= new BrowserAtlasTextureCache(GraphicsDevice);
        _browserBootstrapAtlasResolver = new BrowserBootstrapAtlasTextureResolver(bootstrapAtlasManifest, _browserAtlasTextureCache);
    }

    private static void InitializeLocalDistributionAtlasManifestsIfPresent()
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        ClientRuntimeBootstrap.SetBrowserBootstrapAtlasManifest(
            BrowserAtlasManifestLoader.TryLoadBootstrapFromFile());
        ClientRuntimeBootstrap.SetBrowserStockGameplayAtlasManifest(
            BrowserAtlasManifestLoader.TryLoadStockGameplayFromFile());
        ClientRuntimeBootstrap.SetBrowserGameMakerAtlasManifest(
            BrowserAtlasManifestLoader.TryLoadGameMakerFromFile());
    }
}
