#nullable enable

using OpenGarrison.GameplayModding;

namespace OpenGarrison.ClientShared;

public sealed class GameplayPackSpriteAssetService(
    string packId,
    IAssetBinarySource assetSource,
    GameplaySpriteDefinitionHttpReader? spriteDefinitionReader = null,
    IReadOnlyDictionary<string, GameplaySpriteAssetDefinition>? spriteDefinitions = null)
{
    private readonly string _packId = packId;
    private readonly IAssetBinarySource _assetSource = assetSource;
    private readonly GameplaySpriteDefinitionHttpReader? _spriteDefinitionReader = spriteDefinitionReader;
    private readonly IReadOnlyDictionary<string, GameplaySpriteAssetDefinition> _spriteDefinitions =
        spriteDefinitions is null
            ? new Dictionary<string, GameplaySpriteAssetDefinition>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, GameplaySpriteAssetDefinition>(spriteDefinitions, StringComparer.OrdinalIgnoreCase);

    public string PackId => _packId;

    public LoadedGameplaySpriteAsset LoadRegisteredSprite(GameplaySpriteAssetDefinition spriteDefinition)
    {
        ArgumentNullException.ThrowIfNull(spriteDefinition);
        EnsureSpriteDefinitionHasSourceFrames(spriteDefinition);

        var loadedDefinition = LoadedGameplaySpriteDefinition.FromPackAsset(_packId, spriteDefinition);
        var sourceImages = GameplaySpriteBinaryLoader.LoadSourceImages(_assetSource, spriteDefinition);
        return new LoadedGameplaySpriteAsset(loadedDefinition, sourceImages);
    }

    public async Task<LoadedGameplaySpriteAsset> LoadRegisteredSpriteAsync(
        GameplaySpriteAssetDefinition spriteDefinition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spriteDefinition);
        EnsureSpriteDefinitionHasSourceFrames(spriteDefinition);

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
        if (_spriteDefinitions.TryGetValue(spriteName, out var registeredDefinition))
        {
            if (registeredDefinition.FramePaths.Count > 0)
            {
                return await LoadRegisteredSpriteAsync(registeredDefinition, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        if (_spriteDefinitionReader is null)
        {
            return null;
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

    private static void EnsureSpriteDefinitionHasSourceFrames(GameplaySpriteAssetDefinition spriteDefinition)
    {
        if (spriteDefinition.FramePaths.Count == 0)
        {
            throw new InvalidOperationException(
                $"Gameplay sprite asset \"{spriteDefinition.Id}\" does not include source frame paths in this runtime payload. Atlas resolution should have satisfied this request before raw sprite loading.");
        }
    }
}

public sealed record LoadedGameplaySpriteAsset(
    LoadedGameplaySpriteDefinition Definition,
    LoadedGameplaySpriteSourceSet SourceSet);
