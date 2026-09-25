using System.Collections.Concurrent;
using OpenGarrison.GameplayModding;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenGarrison.Core;

public static class MeleeHitboxMaskCatalog
{
    private static readonly ConcurrentDictionary<string, MeleeHitboxMask?> Cache = new(StringComparer.Ordinal);

    public static MeleeHitboxMask? GetOrLoad(string? spriteId)
    {
        if (string.IsNullOrWhiteSpace(spriteId))
        {
            return null;
        }

        return Cache.GetOrAdd(spriteId.Trim(), Load);
    }

    private static MeleeHitboxMask? Load(string spriteId)
    {
        if (!TryResolveSprite(spriteId, out var sprite, out var packDirectoryName))
        {
            return null;
        }

        if (sprite.FramePaths.Count == 0)
        {
            return null;
        }

        var packDirectory = GameplayModPackDirectoryLoader.FindPackDirectory(packDirectoryName);
        if (string.IsNullOrWhiteSpace(packDirectory))
        {
            return null;
        }

        var frameRelativePath = sprite.FramePaths[0].Replace('/', Path.DirectorySeparatorChar);
        var framePath = Path.Combine(packDirectory, frameRelativePath);
        Image<Rgba32>? image = null;
        try
        {
            if (File.Exists(framePath))
            {
                image = Image.Load<Rgba32>(framePath);
            }
            else if (BrowserContentCatalog.TryGetBinaryForPath(framePath, out var bytes)
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

            if (image is null || image.Width <= 0 || image.Height <= 0)
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
