using System.Text.Json.Serialization;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed class BrowserGameplayModPackDefinitionDocument
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("items")]
    public Dictionary<string, GameplayItemDefinition> Items { get; init; } = new(StringComparer.Ordinal);

    [JsonPropertyName("classes")]
    public Dictionary<string, GameplayClassDefinition> Classes { get; init; } = new(StringComparer.Ordinal);

    [JsonPropertyName("assets")]
    public BrowserGameplayModPackAssetCatalogDocument Assets { get; init; } = new();

    public static BrowserGameplayModPackDefinitionDocument FromDefinition(GameplayModPackDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new BrowserGameplayModPackDefinitionDocument
        {
            Id = definition.Id,
            DisplayName = definition.DisplayName,
            Version = definition.Version.ToString(),
            Items = definition.Items.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.Ordinal),
            Classes = definition.Classes.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.Ordinal),
            Assets = BrowserGameplayModPackAssetCatalogDocument.FromCatalog(definition.Assets),
        };
    }

    public GameplayModPackDefinition ToDefinition()
    {
        if (!System.Version.TryParse(Version, out var parsedVersion))
        {
            throw new InvalidOperationException($"Browser gameplay mod pack version \"{Version}\" is invalid for pack \"{Id}\".");
        }

        return new GameplayModPackDefinition(
            Id: Id,
            DisplayName: DisplayName,
            Version: parsedVersion,
            Items: Items,
            Classes: Classes,
            Assets: Assets.ToCatalog());
    }
}

public sealed class BrowserGameplayModPackAssetCatalogDocument
{
    [JsonPropertyName("sprites")]
    public Dictionary<string, BrowserGameplaySpriteAssetDocument> Sprites { get; init; } = new(StringComparer.Ordinal);

    public static BrowserGameplayModPackAssetCatalogDocument FromCatalog(GameplayModPackAssetCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return new BrowserGameplayModPackAssetCatalogDocument
        {
            Sprites = catalog.Sprites.ToDictionary(
                static pair => pair.Key,
                static pair => BrowserGameplaySpriteAssetDocument.FromDefinition(pair.Value),
                StringComparer.Ordinal),
        };
    }

    public GameplayModPackAssetCatalog ToCatalog()
    {
        return new GameplayModPackAssetCatalog(
            Sprites.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ToDefinition(),
                StringComparer.Ordinal));
    }
}

public sealed class BrowserGameplaySpriteAssetDocument
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("frameWidth")]
    public int? FrameWidth { get; init; }

    [JsonPropertyName("frameHeight")]
    public int? FrameHeight { get; init; }

    [JsonPropertyName("originX")]
    public int OriginX { get; init; }

    [JsonPropertyName("originY")]
    public int OriginY { get; init; }

    [JsonPropertyName("mask")]
    public GameplaySpriteMaskDefinition? Mask { get; init; }

    public static BrowserGameplaySpriteAssetDocument FromDefinition(GameplaySpriteAssetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new BrowserGameplaySpriteAssetDocument
        {
            Id = definition.Id,
            FrameWidth = definition.FrameWidth,
            FrameHeight = definition.FrameHeight,
            OriginX = definition.OriginX,
            OriginY = definition.OriginY,
            Mask = definition.Mask,
        };
    }

    public GameplaySpriteAssetDefinition ToDefinition()
    {
        return new GameplaySpriteAssetDefinition(
            Id: Id,
            FramePaths: [],
            FrameWidth: FrameWidth,
            FrameHeight: FrameHeight,
            OriginX: OriginX,
            OriginY: OriginY,
            Mask: Mask);
    }
}
