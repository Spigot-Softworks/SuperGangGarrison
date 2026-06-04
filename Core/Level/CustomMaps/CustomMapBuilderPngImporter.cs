using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text;

namespace OpenGarrison.Core;

public static class CustomMapBuilderPngImporter
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static CustomMapBuilderDocument? Import(string pngPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pngPath);
        if (!TryExtractLevelData(pngPath, out var levelData))
        {
            return null;
        }

        var walkmaskSection = ExtractSection(levelData, "{WALKMASK}", "{END WALKMASK}");
        var entitiesSection = ExtractSection(levelData, "{ENTITIES}", "{END ENTITIES}");
        if (string.IsNullOrWhiteSpace(walkmaskSection) || string.IsNullOrWhiteSpace(entitiesSection))
        {
            return null;
        }

        if (!TryDecodeEntities(entitiesSection.Trim(), out var metadata, out var entities))
        {
            return null;
        }

        var walkmaskScale = CustomMapBuilderDocument.ResolveWalkmaskScale(metadata);
        var visualScale = CustomMapBuilderDocument.ResolveVisualScale(metadata, walkmaskScale);

        var resources = CustomMapBuilderResourceCodec.DecodeResourcesFromMetadata(metadata);
        return new CustomMapBuilderDocument(
            Name: Path.GetFileNameWithoutExtension(pngPath),
            BackgroundImagePath: pngPath,
            WalkmaskImagePath: string.Empty,
            Scale: walkmaskScale,
            VisualScale: visualScale,
            Metadata: new ReadOnlyDictionary<string, string>(metadata),
            Entities: entities,
            Resources: resources,
            ParallaxLayers: CustomMapBuilderParallaxLayers.DecodeFromMetadata(metadata, resources),
            EmbeddedWalkmaskSection: walkmaskSection.Trim());
    }

    private static bool TryDecodeEntities(
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
        return decodedEntities.Count > 0;
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
        return decodedEntities.Count > 0;
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

    private static bool TryExtractLevelData(string pngPath, out string levelData)
    {
        if (!File.Exists(pngPath))
        {
            levelData = string.Empty;
            return false;
        }

        using var stream = File.OpenRead(pngPath);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var signature = reader.ReadBytes(PngSignature.Length);
        if (!signature.SequenceEqual(PngSignature))
        {
            levelData = string.Empty;
            return false;
        }

        var builder = new StringBuilder();
        while (stream.Position + 8 <= stream.Length)
        {
            var lengthBytes = reader.ReadBytes(4);
            if (lengthBytes.Length < 4)
            {
                break;
            }

            var dataLength = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
            var chunkType = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var chunkData = reader.ReadBytes(Math.Max(0, dataLength));
            _ = reader.ReadUInt32();
            switch (chunkType)
            {
                case "tEXt":
                    AppendTextChunk(builder, chunkData);
                    break;
                case "zTXt":
                    AppendCompressedTextChunk(builder, chunkData);
                    break;
                case "iTXt":
                    AppendInternationalTextChunk(builder, chunkData);
                    break;
            }
        }

        levelData = builder.ToString();
        return levelData.Length > 0;
    }

    private static void AppendTextChunk(StringBuilder builder, byte[] chunkData)
    {
        var separatorIndex = Array.IndexOf(chunkData, (byte)0);
        if (separatorIndex >= 0 && separatorIndex < chunkData.Length - 1)
        {
            builder.Append(Encoding.Latin1.GetString(chunkData, separatorIndex + 1, chunkData.Length - separatorIndex - 1));
        }
    }

    private static void AppendCompressedTextChunk(StringBuilder builder, byte[] chunkData)
    {
        var separatorIndex = Array.IndexOf(chunkData, (byte)0);
        if (separatorIndex < 0 || separatorIndex >= chunkData.Length - 2)
        {
            return;
        }

        using var compressedStream = new MemoryStream(chunkData, separatorIndex + 2, chunkData.Length - separatorIndex - 2, writable: false);
        using var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress);
        using var reader = new StreamReader(zlibStream, Encoding.Latin1);
        builder.Append(reader.ReadToEnd());
    }

    private static void AppendInternationalTextChunk(StringBuilder builder, byte[] chunkData)
    {
        var index = Array.IndexOf(chunkData, (byte)0);
        if (index < 0 || index + 5 >= chunkData.Length)
        {
            return;
        }

        var compressed = chunkData[index + 1] == 1;
        index += 3;
        while (index < chunkData.Length && chunkData[index] != 0)
        {
            index += 1;
        }

        index += 1;
        while (index < chunkData.Length && chunkData[index] != 0)
        {
            index += 1;
        }

        index += 1;
        if (index >= chunkData.Length)
        {
            return;
        }

        if (compressed)
        {
            using var compressedStream = new MemoryStream(chunkData, index, chunkData.Length - index, writable: false);
            using var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress);
            using var reader = new StreamReader(zlibStream, Encoding.UTF8);
            builder.Append(reader.ReadToEnd());
        }
        else
        {
            builder.Append(Encoding.UTF8.GetString(chunkData, index, chunkData.Length - index));
        }
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
