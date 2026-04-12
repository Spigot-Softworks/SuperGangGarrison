using System;
using System.Collections.Generic;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public static class GameplaySpriteAssetMigration
{
    public static GameplaySpriteAssetDefinition CreateDefinitionFromGameMakerSprite(
        string spriteId,
        IReadOnlyList<string> framePaths,
        GameMakerSpriteAsset sourceSprite,
        int? frameWidth = null,
        int? frameHeight = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteId);
        ArgumentNullException.ThrowIfNull(framePaths);
        ArgumentNullException.ThrowIfNull(sourceSprite);

        return new GameplaySpriteAssetDefinition(
            Id: spriteId.Trim(),
            FramePaths: framePaths,
            FrameWidth: frameWidth,
            FrameHeight: frameHeight,
            OriginX: sourceSprite.OriginX,
            OriginY: sourceSprite.OriginY,
            Mask: ToGameplayMask(sourceSprite.Mask));
    }

    public static GameplaySpriteAssetDefinition ImportDefinitionFromGameMakerMetadata(
        string spriteId,
        string metadataPath,
        IReadOnlyList<string>? framePaths = null,
        int? frameWidth = null,
        int? frameHeight = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataPath);
        if (!GameMakerAssetManifestImporter.TryImportSpriteMetadata(metadataPath, out var sourceSprite))
        {
            throw new InvalidOperationException($"GameMaker sprite metadata could not be imported from \"{metadataPath}\".");
        }

        return CreateDefinitionFromGameMakerSprite(
            spriteId,
            framePaths ?? sourceSprite.FramePaths,
            sourceSprite,
            frameWidth,
            frameHeight);
    }

    public static GameplaySpriteMaskDefinition ToGameplayMask(GameMakerSpriteMask sourceMask)
    {
        ArgumentNullException.ThrowIfNull(sourceMask);

        return new GameplaySpriteMaskDefinition(
            Separate: sourceMask.Separate,
            Shape: sourceMask.Shape,
            BoundsMode: sourceMask.BoundsMode,
            Left: sourceMask.Left,
            Top: sourceMask.Top,
            Right: sourceMask.Right,
            Bottom: sourceMask.Bottom);
    }
}
