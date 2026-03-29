using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed class GameMakerAssetManifest
{
    public GameMakerAssetManifest(
        string? sourceRootPath,
        IReadOnlyDictionary<string, GameMakerSpriteAsset> sprites,
        IReadOnlyDictionary<string, GameMakerBackgroundAsset> backgrounds,
        IReadOnlyDictionary<string, GameMakerSoundAsset> sounds)
    {
        SourceRootPath = sourceRootPath;
        Sprites = sprites;
        Backgrounds = backgrounds;
        Sounds = sounds;
    }

    public string? SourceRootPath { get; }

    public IReadOnlyDictionary<string, GameMakerSpriteAsset> Sprites { get; }

    public IReadOnlyDictionary<string, GameMakerBackgroundAsset> Backgrounds { get; }

    public IReadOnlyDictionary<string, GameMakerSoundAsset> Sounds { get; }

    public bool ImportedFromSource => SourceRootPath is not null;
}
