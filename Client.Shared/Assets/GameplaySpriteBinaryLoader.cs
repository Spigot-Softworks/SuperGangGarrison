#nullable enable

using OpenGarrison.GameplayModding;

namespace OpenGarrison.ClientShared;

public static class GameplaySpriteBinaryLoader
{
    public static LoadedGameplaySpriteSourceSet LoadSourceImages(IAssetBinarySource assetSource, GameplaySpriteAssetDefinition spriteDefinition)
    {
        ArgumentNullException.ThrowIfNull(assetSource);
        ArgumentNullException.ThrowIfNull(spriteDefinition);

        var sourceImages = new List<LoadedGameplaySpriteSourceImage>(spriteDefinition.FramePaths.Count);
        foreach (var framePath in spriteDefinition.FramePaths)
        {
            var frameBytes = assetSource.TryReadAllBytes(framePath);
            if (frameBytes is null)
            {
                throw new FileNotFoundException(
                    $"Gameplay sprite asset \"{spriteDefinition.Id}\" frame path could not be loaded: {framePath}",
                    framePath);
            }

            sourceImages.Add(new LoadedGameplaySpriteSourceImage(framePath, frameBytes));
        }

        return new LoadedGameplaySpriteSourceSet(spriteDefinition, sourceImages);
    }

    public static async Task<LoadedGameplaySpriteSourceSet> LoadSourceImagesAsync(
        IAssetBinarySource assetSource,
        GameplaySpriteAssetDefinition spriteDefinition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assetSource);
        ArgumentNullException.ThrowIfNull(spriteDefinition);

        var frameReadTasks = spriteDefinition.FramePaths
            .Select(async framePath => new
            {
                FramePath = framePath,
                Bytes = await assetSource.TryReadAllBytesAsync(framePath, cancellationToken).ConfigureAwait(false),
            })
            .ToArray();
        var frameResults = await Task.WhenAll(frameReadTasks).ConfigureAwait(false);

        var sourceImages = new List<LoadedGameplaySpriteSourceImage>(spriteDefinition.FramePaths.Count);
        foreach (var frameResult in frameResults)
        {
            var frameBytes = frameResult.Bytes;
            if (frameBytes is null)
            {
                throw new FileNotFoundException(
                    $"Gameplay sprite asset \"{spriteDefinition.Id}\" frame path could not be loaded: {frameResult.FramePath}",
                    frameResult.FramePath);
            }

            sourceImages.Add(new LoadedGameplaySpriteSourceImage(frameResult.FramePath, frameBytes));
        }

        return new LoadedGameplaySpriteSourceSet(spriteDefinition, sourceImages);
    }
}

public sealed record LoadedGameplaySpriteSourceSet(
    GameplaySpriteAssetDefinition Definition,
    IReadOnlyList<LoadedGameplaySpriteSourceImage> SourceImages);

public sealed record LoadedGameplaySpriteSourceImage(
    string FramePath,
    byte[] Bytes);
