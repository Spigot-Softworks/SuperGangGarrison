using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGarrison.GameplayModding;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenGarrison.Core;

public static class MeleeHitboxMaskCatalog
{
    private static readonly ConcurrentDictionary<string, MeleeHitboxMask> Cache = new(StringComparer.Ordinal);
    private static BrowserAtlasManifest? _browserStockAtlasManifest;

    public static void SetBrowserStockAtlasManifest(BrowserAtlasManifest? manifest)
    {
        _browserStockAtlasManifest = manifest;
        Cache.Clear();
    }

    public static MeleeHitboxMask? GetOrLoad(string? spriteId, int frameIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(spriteId) || frameIndex < 0)
        {
            return null;
        }

        var normalizedId = spriteId.Trim();
        var cacheKey = frameIndex == 0 ? normalizedId : $"{normalizedId}#{frameIndex}";
        if (Cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // Browser atlas bytes can arrive after the first request. Do not cache a
        // missing mask; retry once its content becomes available.
        var loaded = Load(normalizedId, frameIndex);
        return loaded is null ? null : Cache.GetOrAdd(cacheKey, loaded);
    }

    private static MeleeHitboxMask? Load(string spriteId, int frameIndex)
    {
        if (!TryResolveSprite(spriteId, out var sprite, out var packDirectoryName))
        {
            return null;
        }

        if (sprite.FramePaths.Count == 0)
        {
            // Compact packaged runtime definitions intentionally omit source
            // frame paths. Their alpha data lives in the generated atlas.
            return TryLoadFromStockAtlas(spriteId, packDirectoryName, sprite, frameIndex);
        }

        if (frameIndex >= sprite.FramePaths.Count)
        {
            return null;
        }

        var packDirectory = GameplayModPackDirectoryLoader.FindPackDirectory(packDirectoryName);
        var frameRelativePath = sprite.FramePaths[frameIndex].Replace('/', Path.DirectorySeparatorChar);
        var framePath = string.IsNullOrWhiteSpace(packDirectory)
            ? string.Empty
            : Path.Combine(packDirectory, frameRelativePath);
        Image<Rgba32>? image = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(framePath) && File.Exists(framePath))
            {
                image = Image.Load<Rgba32>(framePath);
            }
            else if (!string.IsNullOrWhiteSpace(framePath)
                && BrowserContentCatalog.TryGetBinaryForPath(framePath, out var bytes)
                && bytes.Length > 0)
            {
                image = Image.Load<Rgba32>(bytes);
            }
            else
            {
                var contentRelative = Path.Combine("Gameplay", packDirectoryName, frameRelativePath);
                if (BrowserContentCatalog.TryGetBinary(contentRelative.Replace('\\', '/'), out bytes)
                    && bytes.Length > 0)
                {
                    image = Image.Load<Rgba32>(bytes);
                }
            }

            if (image is null)
            {
                return TryLoadFromStockAtlas(spriteId, packDirectoryName, sprite, frameIndex);
            }

            if (image.Width <= 0 || image.Height <= 0)
            {
                return null;
            }

            var alphaSamples = new byte[image.Width * image.Height];
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y += 1)
                {
                    var row = accessor.GetRowSpan(y);
                    var destination = y * accessor.Width;
                    for (var x = 0; x < row.Length; x += 1)
                    {
                        alphaSamples[destination + x] = row[x].A;
                    }
                }
            });

            return new MeleeHitboxMask(
                image.Width,
                image.Height,
                sprite.OriginX,
                sprite.OriginY,
                alphaSamples);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or UnknownImageFormatException
            or InvalidImageContentException)
        {
            return null;
        }
        finally
        {
            image?.Dispose();
        }
    }

    private static MeleeHitboxMask? TryLoadFromStockAtlas(
        string spriteId,
        string packDirectoryName,
        GameplaySpriteAssetDefinition sprite,
        int frameIndex)
    {
        if (!string.Equals(packDirectoryName, StockGameplayModCatalog.StockPackDirectoryName, StringComparison.Ordinal))
        {
            return null;
        }

        var manifest = _browserStockAtlasManifest ?? TryReadStockAtlasManifest();
        if (manifest is null
            || !manifest.Sprites.TryGetValue(spriteId, out var atlasSprite)
            || frameIndex >= atlasSprite.Frames.Count)
        {
            return null;
        }

        var frame = atlasSprite.Frames[frameIndex];
        var page = manifest.Atlases.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, frame.AtlasId, StringComparison.Ordinal));
        if (page is null || frame.Width <= 0 || frame.Height <= 0
            || frame.X < 0 || frame.Y < 0
            || (long)frame.X + frame.Width > page.Width
            || (long)frame.Y + frame.Height > page.Height)
        {
            return null;
        }

        var pageFileName = Path.GetFileName(page.ImagePath.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(pageFileName))
        {
            return null;
        }

        var pagePath = ContentRoot.GetPath("Browser", "Atlases", pageFileName);
        Image<Rgba32>? atlas = null;
        try
        {
            if (File.Exists(pagePath))
            {
                atlas = Image.Load<Rgba32>(pagePath);
            }
            else if (BrowserContentCatalog.TryGetBinary(page.ImagePath, out var bytes)
                && bytes.Length > 0)
            {
                atlas = Image.Load<Rgba32>(bytes);
            }

            if (atlas is null
                || (long)frame.X + frame.Width > atlas.Width
                || (long)frame.Y + frame.Height > atlas.Height)
            {
                return null;
            }

            var alphaSamples = new byte[checked(frame.Width * frame.Height)];
            atlas.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < frame.Height; y += 1)
                {
                    var row = accessor.GetRowSpan(frame.Y + y);
                    var destination = y * frame.Width;
                    for (var x = 0; x < frame.Width; x += 1)
                    {
                        alphaSamples[destination + x] = row[frame.X + x].A;
                    }
                }
            });

            return new MeleeHitboxMask(
                frame.Width, frame.Height, sprite.OriginX, sprite.OriginY, alphaSamples);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or UnknownImageFormatException
            or InvalidImageContentException)
        {
            return null;
        }
        finally
        {
            atlas?.Dispose();
        }
    }

    private static BrowserAtlasManifest? TryReadStockAtlasManifest()
    {
        var path = ContentRoot.GetPath("Browser", "Manifests", "stock-pack-atlas-manifest.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize(
                File.ReadAllBytes(path), MeleeHitboxAtlasJsonContext.Default.BrowserGameplayAtlasManifest);
            return document?.Manifest;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            return null;
        }
    }

    private static bool TryResolveSprite(
        string spriteId,
        out GameplaySpriteAssetDefinition sprite,
        out string packDirectoryName)
    {
        sprite = null!;
        packDirectoryName = StockGameplayModCatalog.StockPackDirectoryName;
        if (StockGameplayModCatalog.Definition.Assets.Sprites.TryGetValue(spriteId, out sprite!))
        {
            return true;
        }

        foreach (var modPack in CharacterClassCatalog.RuntimeRegistry.ModPacks)
        {
            if (modPack.Assets.Sprites.TryGetValue(spriteId, out sprite!))
            {
                packDirectoryName = modPack.Id;
                return true;
            }
        }

        return false;
    }
}

[JsonSerializable(typeof(BrowserGameplayAtlasManifest))]
internal partial class MeleeHitboxAtlasJsonContext : JsonSerializerContext
{
}
