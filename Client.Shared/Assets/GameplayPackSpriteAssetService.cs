#nullable enable

using OpenGarrison.GameplayModding;

namespace OpenGarrison.ClientShared;

public sealed class GameplayPackSpriteAssetService(string packId, IAssetBinarySource assetSource, GameplaySpriteDefinitionHttpReader? spriteDefinitionReader = null)
{
    private readonly string _packId = packId;
    private readonly IAssetBinarySource _assetSource = assetSource;
    private readonly GameplaySpriteDefinitionHttpReader? _spriteDefinitionReader = spriteDefinitionReader;

    public string PackId => _packId;

    public LoadedGameplaySpriteAsset LoadRegisteredSprite(GameplaySpriteAssetDefinition spriteDefinition)
    {
        ArgumentNullException.ThrowIfNull(spriteDefinition);

        var loadedDefinition = LoadedGameplaySpriteDefinition.FromPackAsset(_packId, spriteDefinition);
        var sourceImages = GameplaySpriteBinaryLoader.LoadSourceImages(_assetSource, spriteDefinition);
        return new LoadedGameplaySpriteAsset(loadedDefinition, sourceImages);
    }

    public async Task<LoadedGameplaySpriteAsset> LoadRegisteredSpriteAsync(
        GameplaySpriteAssetDefinition spriteDefinition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spriteDefinition);

        var loadedDefinition = LoadedGameplaySpriteDefinition.FromPackAsset(_packId, spriteDefinition);
        var sourceImages = await GameplaySpriteBinaryLoader.LoadSourceImagesAsync(
                _assetSource,
                spriteDefinition,
                cancellationToken)
            .ConfigureAwait(false);
        return new LoadedGameplaySpriteAsset(loadedDefinition, sourceImages);
    }

    public async Task<LoadedGameplaySpriteAsset?> TryLoadAsync(string spriteName, CancellationToken cancellationToken = default)
    {
        if (_spriteDefinitionReader is null)
        {
            throw new InvalidOperationException("This gameplay sprite asset service was created without a sprite definition reader.");
        }

        var loadedDefinition = await _spriteDefinitionReader.TryLoadAsync(_packId, spriteName, cancellationToken);
        if (loadedDefinition is null)
        {
            return null;
        }

        var sourceImages = await GameplaySpriteBinaryLoader.LoadSourceImagesAsync(
            _assetSource,
            loadedDefinition.Definition,
            cancellationToken).ConfigureAwait(false);
        return new LoadedGameplaySpriteAsset(loadedDefinition, sourceImages);
    }
}

public sealed record LoadedGameplaySpriteAsset(
    LoadedGameplaySpriteDefinition Definition,
    LoadedGameplaySpriteSourceSet SourceSet);
