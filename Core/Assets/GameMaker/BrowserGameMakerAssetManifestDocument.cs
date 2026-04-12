using System.Text.Json.Serialization;

namespace OpenGarrison.Core;

public sealed class BrowserGameMakerAssetManifestDocument
{
    [JsonPropertyName("sprites")]
    public Dictionary<string, BrowserGameMakerSpriteAssetDocument> Sprites { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("backgrounds")]
    public Dictionary<string, BrowserGameMakerBackgroundAssetDocument> Backgrounds { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("sounds")]
    public Dictionary<string, BrowserGameMakerSoundAssetDocument> Sounds { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public static BrowserGameMakerAssetManifestDocument FromManifest(GameMakerAssetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return new BrowserGameMakerAssetManifestDocument
        {
            Sprites = manifest.Sprites.ToDictionary(
                static pair => pair.Key,
                static pair => BrowserGameMakerSpriteAssetDocument.FromAsset(pair.Value),
                StringComparer.OrdinalIgnoreCase),
            Backgrounds = manifest.Backgrounds.ToDictionary(
                static pair => pair.Key,
                static pair => BrowserGameMakerBackgroundAssetDocument.FromAsset(pair.Value),
                StringComparer.OrdinalIgnoreCase),
            Sounds = manifest.Sounds.ToDictionary(
                static pair => pair.Key,
                static pair => BrowserGameMakerSoundAssetDocument.FromAsset(pair.Value),
                StringComparer.OrdinalIgnoreCase),
        };
    }

    public GameMakerAssetManifest ToManifest()
    {
        return new GameMakerAssetManifest(
            sourceRootPath: null,
            sprites: Sprites.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ToAsset(pair.Key),
                StringComparer.OrdinalIgnoreCase),
            backgrounds: Backgrounds.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ToAsset(pair.Key),
                StringComparer.OrdinalIgnoreCase),
            sounds: Sounds.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ToAsset(pair.Key),
                StringComparer.OrdinalIgnoreCase));
    }

    internal static string NormalizeAssetPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalizedPath = path.Replace('\\', '/');
        const string contentMarker = "Content/";
        var contentIndex = normalizedPath.IndexOf(contentMarker, StringComparison.OrdinalIgnoreCase);
        if (contentIndex >= 0)
        {
            return normalizedPath[contentIndex..];
        }

        return normalizedPath.TrimStart('/');
    }
}

public sealed class BrowserGameMakerSpriteAssetDocument
{
    [JsonPropertyName("metadataPath")]
    public string MetadataPath { get; init; } = string.Empty;

    [JsonPropertyName("framePaths")]
    public string[] FramePaths { get; init; } = [];

    [JsonPropertyName("originX")]
    public int OriginX { get; init; }

    [JsonPropertyName("originY")]
    public int OriginY { get; init; }

    [JsonPropertyName("preload")]
    public bool Preload { get; init; }

    [JsonPropertyName("transparent")]
    public bool Transparent { get; init; }

    [JsonPropertyName("mask")]
    public BrowserGameMakerSpriteMaskDocument Mask { get; init; } = new();

    public static BrowserGameMakerSpriteAssetDocument FromAsset(GameMakerSpriteAsset asset)
    {
        return new BrowserGameMakerSpriteAssetDocument
        {
            MetadataPath = BrowserGameMakerAssetManifestDocument.NormalizeAssetPath(asset.MetadataPath),
            FramePaths = asset.FramePaths.Select(BrowserGameMakerAssetManifestDocument.NormalizeAssetPath).ToArray(),
            OriginX = asset.OriginX,
            OriginY = asset.OriginY,
            Preload = asset.Preload,
            Transparent = asset.Transparent,
            Mask = BrowserGameMakerSpriteMaskDocument.FromMask(asset.Mask),
        };
    }

    public GameMakerSpriteAsset ToAsset(string name)
    {
        return new GameMakerSpriteAsset(
            Name: name,
            MetadataPath: MetadataPath,
            FramePaths: FramePaths,
            OriginX: OriginX,
            OriginY: OriginY,
            Preload: Preload,
            Transparent: Transparent,
            Mask: Mask.ToMask());
    }
}

public sealed class BrowserGameMakerSpriteMaskDocument
{
    [JsonPropertyName("separate")]
    public bool Separate { get; init; }

    [JsonPropertyName("shape")]
    public string Shape { get; init; } = string.Empty;

    [JsonPropertyName("boundsMode")]
    public string BoundsMode { get; init; } = string.Empty;

    [JsonPropertyName("left")]
    public int? Left { get; init; }

    [JsonPropertyName("top")]
    public int? Top { get; init; }

    [JsonPropertyName("right")]
    public int? Right { get; init; }

    [JsonPropertyName("bottom")]
    public int? Bottom { get; init; }

    public static BrowserGameMakerSpriteMaskDocument FromMask(GameMakerSpriteMask mask)
    {
        return new BrowserGameMakerSpriteMaskDocument
        {
            Separate = mask.Separate,
            Shape = mask.Shape,
            BoundsMode = mask.BoundsMode,
            Left = mask.Left,
            Top = mask.Top,
            Right = mask.Right,
            Bottom = mask.Bottom,
        };
    }

    public GameMakerSpriteMask ToMask()
    {
        return new GameMakerSpriteMask(Separate, Shape, BoundsMode, Left, Top, Right, Bottom);
    }
}

public sealed class BrowserGameMakerBackgroundAssetDocument
{
    [JsonPropertyName("metadataPath")]
    public string MetadataPath { get; init; } = string.Empty;

    [JsonPropertyName("imagePath")]
    public string ImagePath { get; init; } = string.Empty;

    [JsonPropertyName("preload")]
    public bool Preload { get; init; }

    [JsonPropertyName("transparent")]
    public bool Transparent { get; init; }

    public static BrowserGameMakerBackgroundAssetDocument FromAsset(GameMakerBackgroundAsset asset)
    {
        return new BrowserGameMakerBackgroundAssetDocument
        {
            MetadataPath = BrowserGameMakerAssetManifestDocument.NormalizeAssetPath(asset.MetadataPath),
            ImagePath = BrowserGameMakerAssetManifestDocument.NormalizeAssetPath(asset.ImagePath),
            Preload = asset.Preload,
            Transparent = asset.Transparent,
        };
    }

    public GameMakerBackgroundAsset ToAsset(string name)
    {
        return new GameMakerBackgroundAsset(name, MetadataPath, ImagePath, Preload, Transparent);
    }
}

public sealed class BrowserGameMakerSoundAssetDocument
{
    [JsonPropertyName("metadataPath")]
    public string MetadataPath { get; init; } = string.Empty;

    [JsonPropertyName("audioPath")]
    public string AudioPath { get; init; } = string.Empty;

    [JsonPropertyName("fileType")]
    public string FileType { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    [JsonPropertyName("pan")]
    public float Pan { get; init; }

    [JsonPropertyName("volume")]
    public float Volume { get; init; }

    [JsonPropertyName("preload")]
    public bool Preload { get; init; }

    public static BrowserGameMakerSoundAssetDocument FromAsset(GameMakerSoundAsset asset)
    {
        return new BrowserGameMakerSoundAssetDocument
        {
            MetadataPath = BrowserGameMakerAssetManifestDocument.NormalizeAssetPath(asset.MetadataPath),
            AudioPath = BrowserGameMakerAssetManifestDocument.NormalizeAssetPath(asset.AudioPath),
            FileType = asset.FileType,
            Kind = asset.Kind,
            Pan = asset.Pan,
            Volume = asset.Volume,
            Preload = asset.Preload,
        };
    }

    public GameMakerSoundAsset ToAsset(string name)
    {
        return new GameMakerSoundAsset(name, MetadataPath, AudioPath, FileType, Kind, Pan, Volume, Preload);
    }
}
