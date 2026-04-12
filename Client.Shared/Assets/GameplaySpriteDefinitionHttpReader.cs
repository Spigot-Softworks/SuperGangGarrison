#nullable enable

using System.Net.Http;
using System.Net.Http.Json;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.ClientShared;

public sealed class GameplaySpriteDefinitionHttpReader(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<LoadedGameplaySpriteDefinition?> TryLoadAsync(string packId, string spriteName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        ArgumentException.ThrowIfNullOrWhiteSpace(spriteName);

        var definitionPath = GameplayPackAssetPathUtility.GetSpriteDefinitionPath(packId, spriteName);
        var definition = await _httpClient.GetFromJsonAsync<GameplaySpriteAssetDefinition>(definitionPath, cancellationToken);
        return definition is null
            ? null
            : LoadedGameplaySpriteDefinition.FromPackAsset(packId, definition);
    }
}

public sealed record LoadedGameplaySpriteDefinition(
    string PackId,
    string DefinitionPath,
    GameplaySpriteAssetDefinition Definition,
    string? FirstFrameContentPath)
{
    public static LoadedGameplaySpriteDefinition FromPackAsset(string packId, GameplaySpriteAssetDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        ArgumentNullException.ThrowIfNull(definition);

        var firstFrameContentPath = definition.FramePaths.Count > 0
            ? GameplayPackAssetPathUtility.BuildPackAssetPath(packId, definition.FramePaths[0])
            : null;
        return new LoadedGameplaySpriteDefinition(
            packId,
            GameplayPackAssetPathUtility.GetSpriteDefinitionPath(packId, definition.Id),
            definition,
            firstFrameContentPath);
    }
}
