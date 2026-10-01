#nullable enable

using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using System.IO;

namespace OpenGarrison.Client;

public partial class Game1
{
    private BrowserBootstrapResources _browserBootstrapResources => _gameplayManager.Bootstrap.BrowserBootstrapResources;

    public LoadedSpriteFrame? LoadSpriteFrameFromPath(string path)
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
            return TextureDecodeUtility.LoadSpriteFrame(GraphicsDevice, browserBytes, applyLegacyChromaKey: false);
        }

        if (OperatingSystem.IsBrowser() || !File.Exists(path))
        {
            return null;
        }

        return TextureDecodeUtility.LoadSpriteFrame(GraphicsDevice, File.ReadAllBytes(path), applyLegacyChromaKey: false);
    }

    private void StartBrowserBootstrapAssetPreloadIfNeeded()
    {
        if (!OperatingSystem.IsBrowser() || _browserBootstrapResources.AssetsTask is not null)
        {
            return;
        }

        var preloadedAssets = ClientRuntimeBootstrap.GetBrowserBootstrapAssetCatalog();
        if (preloadedAssets is not null)
        {
            _browserBootstrapResources.Assets = preloadedAssets;
            _browserBootstrapResources.AssetsApplied = true;
            return;
        }

        var browserHttpClient = ClientRuntimeBootstrap.GetBrowserHttpClient();
        _browserBootstrapResources.AssetsTask = browserHttpClient is null
            ? BrowserBootstrapAssetCatalog.LoadDefaultAsync()
            : BrowserBootstrapAssetCatalog.LoadDefaultAsync(browserHttpClient);
    }

    private void PollBrowserBootstrapAssetPreload()
    {
        if (!OperatingSystem.IsBrowser()
            || _browserBootstrapResources.AssetsApplied
            || _browserBootstrapResources.AssetsTask?.IsCompleted != true)
        {
            return;
        }

        try
        {
            _browserBootstrapResources.Assets = _browserBootstrapResources.AssetsTask.GetAwaiter().GetResult();
            _browserBootstrapResources.AssetsApplied = true;
            EnsureBrowserBootstrapAtlasResolver();
            RefreshBrowserSpriteFontsIfPossible();
            LoadMenuPlaqueTextures();
            LoadGameplayLoadoutMenuTextures();
            LoadMenuBitmapFont();
            AddConsoleLine("browser bootstrap assets ready");
        }
        catch (Exception ex)
        {
            _browserBootstrapResources.AssetsApplied = true;
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
        if (_browserBootstrapResources.AtlasResolver?.CanResolve(relativePath) == true)
        {
            var pendingFrame = _browserBootstrapResources.AtlasResolver.LoadFrameAsync(relativePath);
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

        if (_browserBootstrapResources.Assets is not null && _browserBootstrapResources.Assets.TryGetBinary(relativePath, out var bytes))
        {
            frame = TextureDecodeUtility.LoadSpriteFrame(GraphicsDevice, bytes, applyLegacyChromaKey: false);
            return true;
        }

        return false;
    }

    private bool TryGetBrowserContentText(string path, out string text)
    {
        text = string.Empty;
        return OperatingSystem.IsBrowser()
            && _browserBootstrapResources.Assets is not null
            && TryGetBrowserContentRelativePath(path, out var relativePath)
            && _browserBootstrapResources.Assets.TryGetText(relativePath, out text);
    }

    public bool CanLoadSpriteFrameFromPath(string path)
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
        if (_browserBootstrapResources.AtlasResolver?.CanResolve(relativePath) == true)
        {
            return true;
        }

        if (OperatingSystem.IsBrowser())
        {
            if ((_browserBootstrapResources.Assets?.TryGetBinary(relativePath, out _) ?? false)
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

        // Atlas manifests use paths rooted at "Content/", even when the
        // desktop content directory is resolved to an absolute path.
        relativePath = "Content/" + normalizedPath[(markerIndex + marker.Length)..].TrimStart('/');
        return relativePath.Length > 0;
    }

    private void EnsureBrowserBootstrapAtlasResolver()
    {
        if (_browserBootstrapResources.AtlasResolver is not null)
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

        _browserBootstrapResources.AtlasTextureCache ??= new BrowserAtlasTextureCache(GraphicsDevice);
        _browserBootstrapResources.AtlasResolver = new BrowserBootstrapAtlasTextureResolver(bootstrapAtlasManifest, _browserBootstrapResources.AtlasTextureCache);
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
