using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text;

namespace OpenGarrison.Core;

public static class CustomMapBuilderPngImporter
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static CustomMapBuilderDocument? Import(string pngPath) => Import(pngPath, out _);

    public static CustomMapBuilderDocument? Import(string pngPath, out string warning)
    {
        warning = string.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(pngPath);
        if (!TryExtractLevelData(pngPath, out var levelData, out warning))
        {
            return null;
        }

        var walkmaskSection = ExtractSection(levelData, "{WALKMASK}", "{END WALKMASK}");
        var entitiesSection = ExtractSection(levelData, "{ENTITIES}", "{END ENTITIES}");
        if (string.IsNullOrWhiteSpace(walkmaskSection) || string.IsNullOrWhiteSpace(entitiesSection))
        {
            throw new InvalidDataException("The PNG contains incomplete map metadata.");
        }

        if (!TryDecodeEntities(entitiesSection.Trim(), out var metadata, out var entities))
        {
            throw new InvalidDataException("The PNG entity data could not be parsed.");
        }

        var walkmaskScale = CustomMapBuilderDocument.ResolveWalkmaskScale(metadata);
        var visualScale = CustomMapBuilderDocument.ResolveVisualScale(metadata, walkmaskScale);

        var resources = CustomMapBuilderResourceCodec.DecodeResourcesFromMetadata(metadata);
        var layers = CustomMapBuilderParallaxLayers.DecodeFromMetadata(metadata, resources);
        foreach (var key in metadata.Keys.ToArray())
        {
            if (resources.ContainsKey(key) || key.Equals("bg_foreground", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("bg_layer", StringComparison.OrdinalIgnoreCase)
                || (key.StartsWith("layer", StringComparison.OrdinalIgnoreCase) && (key.EndsWith("xfactor") || key.EndsWith("yfactor"))))
                metadata.Remove(key);
        }
        return new CustomMapBuilderDocument(
            Name: Path.GetFileNameWithoutExtension(pngPath),
            BackgroundImagePath: pngPath,
            WalkmaskImagePath: string.Empty,
            Scale: walkmaskScale,
            VisualScale: visualScale,
            Metadata: new ReadOnlyDictionary<string, string>(metadata),
            Entities: entities,
            Resources: resources,
            ParallaxLayers: layers,
            EmbeddedWalkmaskSection: walkmaskSection);
    }

    internal static bool TryDecodeEntities(
        string entitiesSection,
        out Dictionary<string, string> metadata,
        out IReadOnlyList<CustomMapBuilderEntity> entities)
    {
        var trimmed = entitiesSection.Trim();
        if (trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '['))
        {
            return TryDecodeGgonEntities(trimmed, out metadata, out entities);
        }

        return TryDecodeLegacyLineEntities(trimmed, out metadata, out entities);
    }

    private static bool TryDecodeGgonEntities(
        string entitiesSection,
        out Dictionary<string, string> metadata,
        out IReadOnlyList<CustomMapBuilderEntity> entities)
    {
        metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var decodedEntities = new List<CustomMapBuilderEntity>();
        if (!GgonParser.TryParse(entitiesSection, out var root) || !TryEnumerateList(root, out var items))
        {
            entities = Array.Empty<CustomMapBuilderEntity>();
            return false;
        }

        foreach (var item in items)
        {
            if (item is not GgonValue.Map map)
            {
                continue;
            }

            var properties = DecodeScalarMap(map);
            if (!properties.TryGetValue("type", out var type) || string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            if (type.Equals("meta", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var pair in properties)
                {
                    metadata[pair.Key] = pair.Value;
                }

                continue;
            }

            if (!TryParseFloat(properties, "x", out var x) || !TryParseFloat(properties, "y", out var y))
            {
                continue;
            }

            var xScale = TryParseFloat(properties, "xscale", out var parsedXScale) ? parsedXScale : 1f;
            var yScale = TryParseFloat(properties, "yscale", out var parsedYScale) ? parsedYScale : 1f;
            decodedEntities.Add(CustomMapBuilderEntity.Create(type, x, y, properties, xScale, yScale).NormalizeForEditing());
        }

        metadata.TryAdd("type", "meta");
        metadata.TryAdd("background", CustomMapBuilderDocument.DefaultBackgroundColor);
        metadata.TryAdd("void", CustomMapBuilderDocument.DefaultVoidColor);
        entities = decodedEntities;
        return true;
    }

    private static bool TryDecodeLegacyLineEntities(
        string entitiesSection,
        out Dictionary<string, string> metadata,
        out IReadOnlyList<CustomMapBuilderEntity> entities)
    {
        metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var decodedEntities = new List<CustomMapBuilderEntity>();
        var lines = entitiesSection
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var index = 0; index + 2 < lines.Length; index += 3)
        {
            var type = lines[index].Trim();
            if (!float.TryParse(lines[index + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(lines[index + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
            {
                continue;
            }

            decodedEntities.Add(CustomMapBuilderEntity.Create(type, x, y).NormalizeForEditing());
        }

        metadata.TryAdd("type", "meta");
        metadata.TryAdd("background", CustomMapBuilderDocument.DefaultBackgroundColor);
        metadata.TryAdd("void", CustomMapBuilderDocument.DefaultVoidColor);
        entities = decodedEntities;
        return true;
    }

    private static float GetLegacyDefaultParallaxFactor(int index)
    {
        return 10f - index;
    }

    private static Dictionary<string, string> DecodeScalarMap(GgonValue.Map map)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in map.Entries)
        {
            if (pair.Value is GgonValue.Scalar scalar)
            {
                values[pair.Key] = scalar.Value;
            }
        }

        return values;
    }

    private static bool TryParseFloat(Dictionary<string, string> values, string key, out float value)
    {
        value = 0f;
        return values.TryGetValue(key, out var text)
            && float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static bool TryEnumerateList(GgonValue value, out IReadOnlyList<GgonValue> items)
    {
        if (value is GgonValue.List list)
        {
            items = list.Items;
            return true;
        }

        if (value is GgonValue.Map map
            && map.Entries.TryGetValue("length", out var lengthValue)
            && lengthValue is GgonValue.Scalar lengthScalar
            && int.TryParse(lengthScalar.Value, out var length)
            && length >= 0)
        {
            var values = new List<GgonValue>(length);
            for (var index = 0; index < length; index += 1)
            {
                if (!map.Entries.TryGetValue(index.ToString(System.Globalization.CultureInfo.InvariantCulture), out var item))
                {
                    items = Array.Empty<GgonValue>();
                    return false;
                }

                values.Add(item);
            }

            items = values;
            return true;
        }

        items = Array.Empty<GgonValue>();
        return false;
    }

    private static bool TryExtractLevelData(string pngPath, out string levelData, out string warning)
    {
        warning = string.Empty;
        if (!File.Exists(pngPath)) { levelData = string.Empty; return false; }
        using var stream = File.OpenRead(pngPath);
        levelData = PngMapData.Read(stream, out warning);
        return levelData.Contains("{ENTITIES}", StringComparison.OrdinalIgnoreCase) || levelData.Contains("{WALKMASK}", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractSection(string input, string startMarker, string endMarker)
    {
        var start = input.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return string.Empty;
        }

        start += startMarker.Length;
        var end = input.IndexOf(endMarker, start, StringComparison.OrdinalIgnoreCase);
        return end < 0 ? string.Empty : input[start..end];
    }
}
