using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client;

public sealed class GameMakerRuntimeAssetCache : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly GameMakerAssetManifest _manifest;
    private readonly Func<string, bool> _fileExists;
    private readonly Dictionary<string, LoadedGameMakerSprite> _sprites = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> _backgrounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SoundEffect> _sounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missingSoundPaths = new(OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal);
    private readonly Dictionary<string, Task<LoadedGameMakerSprite?>> _pendingBrowserSprites = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<Texture2D?>> _pendingBrowserBackgrounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<SoundEffect?>> _pendingBrowserSounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failedBrowserSprites = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failedBrowserBackgrounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failedBrowserSounds = new(StringComparer.OrdinalIgnoreCase);
    private BrowserAtlasTextureCache? _browserAtlasTextureCache;
    private BrowserGameMakerAtlasSpriteResolver? _browserGameMakerAtlasResolver;
    private bool _disposed;

    public GameMakerRuntimeAssetCache(GraphicsDevice graphicsDevice, GameMakerAssetManifest manifest)
        : this(graphicsDevice, manifest, File.Exists)
    {
    }

    internal GameMakerRuntimeAssetCache(
        GraphicsDevice graphicsDevice,
        GameMakerAssetManifest manifest,
        Func<string, bool> fileExists)
    {
        _graphicsDevice = graphicsDevice;
        _manifest = manifest;
        _fileExists = fileExists;

        var atlasManifest = ClientRuntimeBootstrap.GetBrowserGameMakerAtlasManifest();
        if (atlasManifest is not null)
        {
            _browserAtlasTextureCache = new BrowserAtlasTextureCache(graphicsDevice);
            _browserGameMakerAtlasResolver = new BrowserGameMakerAtlasSpriteResolver(atlasManifest, _browserAtlasTextureCache);
        }
    }

    public LoadedGameMakerSprite? GetSprite(string spriteName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_sprites.TryGetValue(spriteName, out var cached))
        {
            return cached;
        }

        if (!_manifest.Sprites.TryGetValue(spriteName, out var spriteAsset))
        {
            return null;
        }

        var loadStartTimestamp = ClientAssetLoadDiagnostics.StartTimestamp();
        var atlasStartTimestamp = ClientAssetLoadDiagnostics.StartTimestamp();
        var atlasSprite = TryGetAtlasSprite(spriteName);
        var atlasMilliseconds = atlasStartTimestamp > 0L
            ? ClientAssetLoadDiagnostics.GetElapsedMilliseconds(atlasStartTimestamp)
            : 0d;
        if (atlasSprite is not null)
        {
            if (loadStartTimestamp > 0L)
            {
                ClientAssetLoadDiagnostics.RecordOnce(
                    "sprite",
                    spriteName,
                    FormattableString.Invariant($"source=atlas atlasMs={atlasMilliseconds:F3} frames={atlasSprite.Frames.Count}"),
                    loadStartTimestamp);
            }

            return atlasSprite;
        }

        if (ShouldRequireRuntimeAtlas())
        {
            if (loadStartTimestamp > 0L)
            {
                ClientAssetLoadDiagnostics.RecordOnce(
                    "sprite",
                    spriteName,
                    FormattableString.Invariant($"source=atlas result=missing atlasMs={atlasMilliseconds:F3}"),
                    loadStartTimestamp);
            }

            var reason = ClientRuntimeBootstrap.GetBrowserGameMakerAtlasManifest() is null
                ? "the runtime GameMaker atlas manifest was not loaded"
                : "the sprite was not present in the runtime GameMaker atlas";
            throw new InvalidOperationException(
                $"Packaged runtime sprite \"{spriteName}\" could not be loaded from the atlas because {reason}. Packaged desktop builds must not fall back to loose GameMaker sprite frames.");
        }

        if (OperatingSystem.IsBrowser())
        {
            return TryGetBrowserSprite(spriteName, spriteAsset);
        }

        var framePaths = spriteAsset.FramePaths;
        var looseLoadStartTimestamp = ClientAssetLoadDiagnostics.StartTimestamp();
        {
            var metadataDirectory = Path.GetDirectoryName(spriteAsset.MetadataPath) ?? string.Empty;
            var imagesDirectory = Path.Combine(metadataDirectory, $"{spriteAsset.Name}.images");
            if (Directory.Exists(imagesDirectory))
            {
                var discoveredFramePaths = Directory
                    .GetFiles(imagesDirectory, "*.png", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => ExtractTrailingNumber(Path.GetFileNameWithoutExtension(path)))
                    .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (discoveredFramePaths.Length > framePaths.Count)
                {
                    framePaths = discoveredFramePaths;
                }
            }
        }

        if (framePaths.Count == 0)
        {
            if (loadStartTimestamp > 0L)
            {
                ClientAssetLoadDiagnostics.RecordOnce(
                    "sprite",
                    spriteName,
                    FormattableString.Invariant($"source=loose result=no-frames atlasMs={atlasMilliseconds:F3} looseMs={ClientAssetLoadDiagnostics.GetElapsedMilliseconds(looseLoadStartTimestamp):F3}"),
                    loadStartTimestamp);
            }

            return null;
        }

        var frames = new LoadedSpriteFrame[framePaths.Count];
        for (var frameIndex = 0; frameIndex < framePaths.Count; frameIndex += 1)
        {
            var framePath = framePaths[frameIndex];
            if (!File.Exists(framePath))
            {
                if (loadStartTimestamp > 0L)
                {
                    ClientAssetLoadDiagnostics.RecordOnce(
                        "sprite",
                        spriteName,
                        FormattableString.Invariant($"source=loose result=missing-frame frame={frameIndex} atlasMs={atlasMilliseconds:F3} looseMs={ClientAssetLoadDiagnostics.GetElapsedMilliseconds(looseLoadStartTimestamp):F3}"),
                        loadStartTimestamp);
                }

                return null;
            }

            using var stream = File.OpenRead(framePath);
            frames[frameIndex] = new LoadedSpriteFrame(Texture2D.FromStream(_graphicsDevice, stream));
        }

        cached = new LoadedGameMakerSprite(frames, new Point(spriteAsset.OriginX, spriteAsset.OriginY));
        _sprites[spriteName] = cached;
        if (loadStartTimestamp > 0L)
        {
            ClientAssetLoadDiagnostics.RecordOnce(
                "sprite",
                spriteName,
                FormattableString.Invariant($"source=loose atlasMs={atlasMilliseconds:F3} looseMs={ClientAssetLoadDiagnostics.GetElapsedMilliseconds(looseLoadStartTimestamp):F3} frames={frames.Length}"),
                loadStartTimestamp);
        }

        return cached;
    }

    public bool ContainsSprite(string spriteName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _manifest.Sprites.ContainsKey(spriteName);
    }

    private static int ExtractTrailingNumber(string fileNameWithoutExtension)
    {
        var trailingDigits = new string(fileNameWithoutExtension.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(trailingDigits, out var value)
            ? value
            : int.MaxValue;
    }

    public Texture2D? GetBackground(string backgroundName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_backgrounds.TryGetValue(backgroundName, out var cached))
        {
            return cached;
        }

        if (OperatingSystem.IsBrowser())
        {
            if (TryGetBrowserBackgroundByPath(backgroundName, out var directBackground))
            {
                _backgrounds[backgroundName] = directBackground;
                return directBackground;
            }

            if (!_manifest.Backgrounds.TryGetValue(backgroundName, out var browserBackgroundAsset))
            {
                return null;
            }

            return TryGetBrowserBackground(backgroundName, browserBackgroundAsset);
        }

        if (File.Exists(backgroundName))
        {
            using var directStream = File.OpenRead(backgroundName);
            cached = Texture2D.FromStream(_graphicsDevice, directStream);
            _backgrounds[backgroundName] = cached;
            return cached;
        }

        if (!_manifest.Backgrounds.TryGetValue(backgroundName, out var backgroundAsset))
        {
            return null;
        }

        if (File.Exists(backgroundAsset.ImagePath))
        {
            using var stream = File.OpenRead(backgroundAsset.ImagePath);
            cached = Texture2D.FromStream(_graphicsDevice, stream);
        }
        else if (TryReadPackagedBackgroundBytes(backgroundAsset.ImagePath, out var packagedBytes))
        {
            using var stream = new MemoryStream(packagedBytes, writable: false);
            cached = Texture2D.FromStream(_graphicsDevice, stream);
        }
        else
        {
            return null;
        }

        _backgrounds[backgroundName] = cached;
        return cached;
    }

    internal static bool TryReadPackagedBackgroundBytes(
        string imagePath,
        out byte[] bytes,
        string? bundlePath = null)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return false;
        }

        var entryPath = BrowserAssetBundleLoader.NormalizeRelativePath(imagePath);
        if (!entryPath.StartsWith("Content/Backgrounds/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        bundlePath ??= Path.Combine(AppContext.BaseDirectory,
            BrowserDistributionPaths.RuntimeAssetBundlePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(bundlePath))
        {
            return false;
        }

        try
        {
            using var archive = ZipFile.OpenRead(bundlePath);
            var entry = archive.Entries.FirstOrDefault(candidate =>
                string.Equals(candidate.FullName, entryPath, StringComparison.OrdinalIgnoreCase));
            if (entry is null || entry.Length is <= 0 or > 64 * 1024 * 1024)
            {
                return false;
            }

            using var entryStream = entry.Open();
            using var buffer = new MemoryStream(checked((int)entry.Length));
            entryStream.CopyTo(buffer);
            bytes = buffer.ToArray();
            return bytes.Length == entry.Length;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public SoundEffect? GetSound(string soundName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_sounds.TryGetValue(soundName, out var cached))
        {
            return cached;
        }

        if (!_manifest.Sounds.TryGetValue(soundName, out var soundAsset))
        {
            return null;
        }

        if (OperatingSystem.IsBrowser())
        {
            return TryGetBrowserSound(soundName, soundAsset);
        }

        var audioPath = ResolveDesktopSoundAssetPath(soundAsset.AudioPath, ContentRoot.Path);
        if (_missingSoundPaths.Contains(audioPath))
        {
            return null;
        }

        var loadStartTimestamp = ClientAssetLoadDiagnostics.StartTimestamp();
        if (!_fileExists(audioPath))
        {
            _missingSoundPaths.Add(audioPath);
            if (loadStartTimestamp > 0L)
            {
                ClientAssetLoadDiagnostics.RecordOnce(
                    "sound",
                    soundName,
                    FormattableString.Invariant($"path=\"{audioPath}\" result=missing-file"),
                    loadStartTimestamp);
            }

            return null;
        }

        var readStartTimestamp = ClientAssetLoadDiagnostics.StartTimestamp();
        var decodeStartTimestamp = 0L;
        var readMilliseconds = 0d;
        var decodeMilliseconds = 0d;
        var readCompleted = false;
        var decodeCompleted = false;
        try
        {
            var soundBytes = File.ReadAllBytes(audioPath);
            readCompleted = true;
            if (readStartTimestamp > 0L)
            {
                readMilliseconds = ClientAssetLoadDiagnostics.GetElapsedMilliseconds(readStartTimestamp);
            }

            decodeStartTimestamp = ClientAssetLoadDiagnostics.StartTimestamp();
            cached = SoundDecodeUtility.LoadSoundEffect(soundBytes, audioPath);
            decodeCompleted = true;
            if (decodeStartTimestamp > 0L)
            {
                decodeMilliseconds = ClientAssetLoadDiagnostics.GetElapsedMilliseconds(decodeStartTimestamp);
            }

            _sounds[soundName] = cached;
            if (loadStartTimestamp > 0L)
            {
                ClientAssetLoadDiagnostics.RecordOnce(
                    "sound",
                    soundName,
                    FormattableString.Invariant($"path=\"{audioPath}\" readMs={readMilliseconds:F3} decodeMs={decodeMilliseconds:F3} bytes={soundBytes.Length}"),
                    loadStartTimestamp);
            }

            return cached;
        }
        catch (Exception exception)
        {
            if (loadStartTimestamp > 0L)
            {
                if (!readCompleted)
                {
                    readMilliseconds = ClientAssetLoadDiagnostics.GetElapsedMilliseconds(readStartTimestamp);
                }

                if (decodeStartTimestamp > 0L && !decodeCompleted)
                {
                    decodeMilliseconds = ClientAssetLoadDiagnostics.GetElapsedMilliseconds(decodeStartTimestamp);
                }

                ClientAssetLoadDiagnostics.RecordOnce(
                    "sound",
                    soundName,
                    FormattableString.Invariant($"path=\"{audioPath}\" readMs={readMilliseconds:F3} decodeMs={decodeMilliseconds:F3} result=failed error={exception.GetType().Name}"),
                    loadStartTimestamp);
            }

            return null;
        }
    }

    internal bool IsDesktopSoundPermanentlyUnavailable(string soundName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (OperatingSystem.IsBrowser() || _sounds.ContainsKey(soundName))
        {
            return false;
        }

        if (!_manifest.Sounds.TryGetValue(soundName, out var soundAsset))
        {
            return true;
        }

        var audioPath = ResolveDesktopSoundAssetPath(soundAsset.AudioPath, ContentRoot.Path);
        return _missingSoundPaths.Contains(audioPath);
    }

    internal static string ResolveDesktopSoundAssetPath(string audioPath, string contentRoot)
    {
        if (Path.IsPathRooted(audioPath))
        {
            return audioPath;
        }

        var relativePath = audioPath.Replace('\\', '/');
        const string packagedContentPrefix = "Content/";
        if (relativePath.StartsWith(packagedContentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = relativePath[packagedContentPrefix.Length..];
        }
        else if (string.Equals(relativePath, "Content", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = string.Empty;
        }

        var platformRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(contentRoot, platformRelativePath));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var sprite in _sprites.Values)
        {
            foreach (var frame in sprite.Frames)
            {
                frame.Dispose();
            }
        }

        foreach (var background in _backgrounds.Values)
        {
            background.Dispose();
        }

        foreach (var sound in _sounds.Values)
        {
            sound.Dispose();
        }

        _sprites.Clear();
        _backgrounds.Clear();
        _sounds.Clear();
        _missingSoundPaths.Clear();
        _pendingBrowserSprites.Clear();
        _pendingBrowserBackgrounds.Clear();
        _pendingBrowserSounds.Clear();
        _failedBrowserSprites.Clear();
        _failedBrowserBackgrounds.Clear();
        _failedBrowserSounds.Clear();
        _browserGameMakerAtlasResolver = null;
        _browserAtlasTextureCache?.Dispose();
        _browserAtlasTextureCache = null;
    }

    private LoadedGameMakerSprite? TryGetAtlasSprite(string spriteName)
    {
        if (_browserGameMakerAtlasResolver?.CanResolve(spriteName) != true)
        {
            return null;
        }

        var pendingTask = _browserGameMakerAtlasResolver.LoadSpriteAsync(spriteName);
        if (OperatingSystem.IsBrowser() && !pendingTask.IsCompletedSuccessfully)
        {
            return null;
        }

        var sprite = pendingTask.GetAwaiter().GetResult();
        if (sprite is not null)
        {
            _sprites[spriteName] = sprite;
        }

        return sprite;
    }

    private bool ShouldRequireRuntimeAtlas()
        => !OperatingSystem.IsBrowser() && !_manifest.ImportedFromSource;

    private LoadedGameMakerSprite? TryGetBrowserSprite(string spriteName, GameMakerSpriteAsset spriteAsset)
    {
        if (_failedBrowserSprites.Contains(spriteName))
        {
            LogCriticalBrowserRuntimeSprite(spriteName, "previously-failed", $"metadata={spriteAsset.MetadataPath}");
            return null;
        }

        if (!_pendingBrowserSprites.TryGetValue(spriteName, out var pendingTask))
        {
            LogCriticalBrowserRuntimeSprite(spriteName, "begin-load", $"metadata={spriteAsset.MetadataPath}");
            pendingTask = LoadBrowserSpriteAsync(spriteAsset);
            _pendingBrowserSprites[spriteName] = pendingTask;
        }

        if (!pendingTask.IsCompleted)
        {
            return null;
        }

        _pendingBrowserSprites.Remove(spriteName);
        try
        {
            var sprite = pendingTask.GetAwaiter().GetResult();
            if (sprite is null)
            {
                LogCriticalBrowserRuntimeSprite(spriteName, "resolved-null", $"metadata={spriteAsset.MetadataPath}");
                _failedBrowserSprites.Add(spriteName);
                return null;
            }

            LogCriticalBrowserRuntimeSprite(spriteName, "loaded", $"frames={sprite.Frames.Count} metadata={spriteAsset.MetadataPath}");
            _sprites[spriteName] = sprite;
            return sprite;
        }
        catch (Exception ex)
        {
            if (_failedBrowserSprites.Add(spriteName))
            {
                Console.WriteLine($"Browser runtime sprite load failed for \"{spriteName}\": {ex.Message}");
            }

            return null;
        }
    }

    private SoundEffect? TryGetBrowserSound(string soundName, GameMakerSoundAsset soundAsset)
    {
        if (_failedBrowserSounds.Contains(soundName))
        {
            return null;
        }

        if (TryGetCachedBrowserSound(soundAsset, out var cachedSound))
        {
            _sounds[soundName] = cachedSound;
            return cachedSound;
        }

        if (!_pendingBrowserSounds.TryGetValue(soundName, out var pendingTask))
        {
            pendingTask = LoadBrowserSoundAsync(soundAsset);
            _pendingBrowserSounds[soundName] = pendingTask;
        }

        if (!pendingTask.IsCompleted)
        {
            return null;
        }

        _pendingBrowserSounds.Remove(soundName);
        try
        {
            var sound = pendingTask.GetAwaiter().GetResult();
            if (sound is null)
            {
                _failedBrowserSounds.Add(soundName);
                return null;
            }

            _sounds[soundName] = sound;
            return sound;
        }
        catch (Exception ex)
        {
            if (_failedBrowserSounds.Add(soundName))
            {
                Console.WriteLine($"Browser runtime sound load failed for \"{soundName}\": {ex.Message}");
            }

            return null;
        }
    }

    private static bool TryGetCachedBrowserSound(GameMakerSoundAsset soundAsset, out SoundEffect sound)
    {
        sound = null!;
        if (!TryGetCachedBrowserSoundBytes(soundAsset.AudioPath, out var bytes)
            || bytes.Length == 0)
        {
            return false;
        }

        try
        {
            sound = SoundDecodeUtility.LoadSoundEffect(bytes, soundAsset.AudioPath);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Browser runtime cached sound decode failed for \"{soundAsset.Name}\": {ex.Message}");
            return false;
        }
    }

    private static bool TryGetCachedBrowserSoundBytes(string soundPath, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(soundPath))
        {
            return false;
        }

        if (BrowserContentCatalog.TryGetBinaryForPath(soundPath, out var directBytes)
            && directBytes.Length > 0)
        {
            bytes = directBytes;
            return true;
        }

        if (!TryNormalizeBrowserAssetPath(soundPath, out var relativePath))
        {
            return false;
        }

        if (BrowserContentCatalog.TryGetBinary(relativePath, out var cachedBytes)
            && cachedBytes.Length > 0)
        {
            bytes = cachedBytes;
            return true;
        }

        return false;
    }

    private Texture2D? TryGetBrowserBackground(string backgroundName, GameMakerBackgroundAsset backgroundAsset)
    {
        if (_failedBrowserBackgrounds.Contains(backgroundName))
        {
            return null;
        }

        if (!_pendingBrowserBackgrounds.TryGetValue(backgroundName, out var pendingTask))
        {
            pendingTask = LoadBrowserBackgroundAsync(backgroundAsset);
            _pendingBrowserBackgrounds[backgroundName] = pendingTask;
        }

        if (!pendingTask.IsCompleted)
        {
            return null;
        }

        _pendingBrowserBackgrounds.Remove(backgroundName);
        try
        {
            var background = pendingTask.GetAwaiter().GetResult();
            if (background is null)
            {
                _failedBrowserBackgrounds.Add(backgroundName);
                return null;
            }

            _backgrounds[backgroundName] = background;
            return background;
        }
        catch (Exception ex)
        {
            if (_failedBrowserBackgrounds.Add(backgroundName))
            {
                Console.WriteLine($"Browser runtime background load failed for \"{backgroundName}\": {ex.Message}");
            }

            return null;
        }
    }

    private async Task<LoadedGameMakerSprite?> LoadBrowserSpriteAsync(GameMakerSpriteAsset spriteAsset)
    {
        var framePaths = spriteAsset.FramePaths;
        if (framePaths.Count == 0)
        {
            LogCriticalBrowserRuntimeSprite(spriteAsset.Name, "no-frame-paths", $"metadata={spriteAsset.MetadataPath}");
            return null;
        }

        var frameByteTasks = framePaths
            .Select(TryGetBrowserBytesAsync)
            .ToArray();
        var frameBytes = await Task.WhenAll(frameByteTasks).ConfigureAwait(false);

        await Task.Yield();

        var frames = new LoadedSpriteFrame[framePaths.Count];
        for (var frameIndex = 0; frameIndex < framePaths.Count; frameIndex += 1)
        {
            var bytes = frameBytes[frameIndex];
            if (bytes is null || bytes.Length == 0)
            {
                LogCriticalBrowserRuntimeSprite(spriteAsset.Name, "missing-frame-bytes", $"framePath={framePaths[frameIndex]} frameIndex={frameIndex}");
                return null;
            }

            frames[frameIndex] = TextureDecodeUtility.LoadSpriteFrame(_graphicsDevice, bytes, applyLegacyChromaKey: true);
        }

        return new LoadedGameMakerSprite(frames, new Point(spriteAsset.OriginX, spriteAsset.OriginY));
    }

    private async Task<Texture2D?> LoadBrowserBackgroundAsync(GameMakerBackgroundAsset backgroundAsset)
    {
        var bytes = await TryGetBrowserBytesAsync(backgroundAsset.ImagePath).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        await Task.Yield();
        return TextureDecodeUtility.LoadTexture(_graphicsDevice, bytes, applyLegacyChromaKey: false);
    }

    private static async Task<SoundEffect?> LoadBrowserSoundAsync(GameMakerSoundAsset soundAsset)
    {
        var relativePath = soundAsset.AudioPath.Replace('\\', '/');
        var bytes = await BrowserAssetFetchUtility.TryGetBytesAsync(relativePath).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        await Task.Yield();
        return SoundDecodeUtility.LoadSoundEffect(bytes, soundAsset.AudioPath);
    }

    private bool TryGetBrowserBackgroundByPath(string backgroundPath, out Texture2D texture)
    {
        texture = null!;
        if (!OperatingSystem.IsBrowser() || string.IsNullOrWhiteSpace(backgroundPath))
        {
            return false;
        }

        if (BrowserContentCatalog.TryGetBinaryForPath(backgroundPath, out var cachedBytes)
            && cachedBytes.Length > 0)
        {
            texture = TextureDecodeUtility.LoadTexture(_graphicsDevice, cachedBytes, applyLegacyChromaKey: false);
            return true;
        }

        if (!TryNormalizeBrowserAssetPath(backgroundPath, out var relativePath))
        {
            return false;
        }

        if (_failedBrowserBackgrounds.Contains(backgroundPath))
        {
            return false;
        }

        if (!_pendingBrowserBackgrounds.TryGetValue(backgroundPath, out var pendingTask))
        {
            pendingTask = LoadBrowserBackgroundByRelativePathAsync(relativePath);
            _pendingBrowserBackgrounds[backgroundPath] = pendingTask;
        }

        if (!pendingTask.IsCompleted)
        {
            return false;
        }

        _pendingBrowserBackgrounds.Remove(backgroundPath);
        try
        {
            texture = pendingTask.GetAwaiter().GetResult()!;
            if (texture is null)
            {
                _failedBrowserBackgrounds.Add(backgroundPath);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            if (_failedBrowserBackgrounds.Add(backgroundPath))
            {
                Console.WriteLine($"Browser runtime background load failed for \"{backgroundPath}\": {ex.Message}");
            }

            return false;
        }
    }

    private static bool TryNormalizeBrowserAssetPath(string path, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalizedPath = path.Replace('\\', '/');
        const string contentMarker = "Content/";
        var contentIndex = normalizedPath.IndexOf(contentMarker, StringComparison.OrdinalIgnoreCase);
        if (contentIndex < 0)
        {
            return false;
        }

        relativePath = normalizedPath[contentIndex..];
        return relativePath.Length > 0;
    }

    private static async Task<byte[]?> TryGetBrowserBytesAsync(string relativePath)
    {
        if (BrowserContentCatalog.TryGetBinary(relativePath, out var cachedBytes))
        {
            return cachedBytes;
        }

        var httpClient = ClientRuntimeBootstrap.GetBrowserHttpClient();
        if (httpClient is null)
        {
            return null;
        }

        try
        {
            using var response = await httpClient.GetAsync(relativePath, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            if (bytes.Length > 0)
            {
                BrowserContentCatalog.AddOrUpdateBinaryAssets(
                [
                    new KeyValuePair<string, byte[]>(relativePath, bytes),
                ]);
            }

            return bytes;
        }
        catch
        {
            return null;
        }
    }

    private async Task<Texture2D?> LoadBrowserBackgroundByRelativePathAsync(string relativePath)
    {
        var bytes = await BrowserAssetFetchUtility.TryGetBytesAsync(relativePath).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        await Task.Yield();
        return TextureDecodeUtility.LoadTexture(_graphicsDevice, bytes, applyLegacyChromaKey: false);
    }

    private static void LogCriticalBrowserRuntimeSprite(string spriteName, string state, string details)
    {
        if (!OperatingSystem.IsBrowser() || !CriticalBrowserSpriteNames.Contains(spriteName))
        {
            return;
        }

        Console.WriteLine($"Browser runtime sprite {spriteName} {state}: {details}");
    }

    private static readonly HashSet<string> CriticalBrowserSpriteNames = new(StringComparer.Ordinal)
    {
        "TeamSelectS",
        "ClassSelectS",
        "TeamDoorS",
        "DoorTopLightUpS",
        "TVLightUpS",
        "ClassSelectPortraitS",
        "ClassSelectSpritesS",
        "TimerHudS",
        "TimerS",
    };
}

public sealed record LoadedGameMakerSprite(IReadOnlyList<LoadedSpriteFrame> Frames, Point Origin);
