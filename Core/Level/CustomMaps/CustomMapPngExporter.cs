using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenGarrison.Core;

public static class CustomMapPngExporter
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static void Export(CustomMapBuilderDocument document, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var normalized = document.NormalizeForEditing();
        if (string.IsNullOrWhiteSpace(normalized.BackgroundImagePath))
        {
            throw new InvalidOperationException("A background PNG is required before exporting a custom map.");
        }

        if (string.IsNullOrWhiteSpace(normalized.WalkmaskImagePath)
            && string.IsNullOrWhiteSpace(normalized.EmbeddedWalkmaskSection))
        {
            throw new InvalidOperationException("A walkmask image or embedded walkmask section is required before exporting a custom map.");
        }

        BuilderImageValidation.ValidateDecoded(BuilderImageValidation.ReadFile(normalized.BackgroundImagePath));
        var levelData = BuildLevelData(normalized);
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        foreach (var resource in normalized.Resources.Values)
        {
            if (resource.Kind == CustomMapBuilderResourceKind.MessageSound)
                throw new InvalidOperationException("Maps with sounds must be saved as a JSON package; legacy PNG cannot preserve audio.");
            if (!CustomMapBuilderResourceCodec.IsSupportedImage(CustomMapBuilderResourceCodec.GetResourceBytes(resource)))
                throw new InvalidOperationException($"Resource '{resource.Name}' is missing or invalid.");
            BuilderImageValidation.ValidateDecoded(CustomMapBuilderResourceCodec.GetResourceBytes(resource));
        }
        MapFileTransaction.Write(outputPath, temporary =>
        {
            WritePngWithLevelData(normalized.BackgroundImagePath, temporary, levelData);
            var reopened = CustomMapBuilderPngImporter.Import(temporary)
                ?? throw new InvalidDataException("The saved PNG could not be verified.");
            if (BuildEntitiesSection(reopened) != BuildEntitiesSection(normalized)
                || !EmbeddedWalkmaskDecoder.TryDecodeSolidCells(reopened.EmbeddedWalkmaskSection, out _, out _, out _))
                throw new InvalidDataException("The saved PNG did not preserve the map data.");
        });
    }

    public static string BuildLevelData(CustomMapBuilderDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var normalized = document.NormalizeForEditing();
        return string.Concat(
            BuildEntitiesSection(normalized),
            Environment.NewLine,
            BuildWalkmaskSection(normalized));
    }

    public static string BuildEntitiesSection(CustomMapBuilderDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var normalized = document.NormalizeForEditing();
        var values = new List<IReadOnlyDictionary<string, string>>
        {
            normalized.BuildExportMetadata(),
        };

        foreach (var entity in normalized.Entities)
        {
            values.Add(entity.Properties);
        }

        var builder = new StringBuilder();
        builder.AppendLine("{ENTITIES}");
        AppendGgonList(builder, values);
        builder.AppendLine();
        builder.Append("{END ENTITIES}");
        return builder.ToString();
    }

    public static string BuildWalkmaskSection(string walkmaskImagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(walkmaskImagePath);
        Image<Rgba32>? loadedImage = null;
        if (File.Exists(walkmaskImagePath))
        {
            var bytes = BuilderImageValidation.ReadFile(walkmaskImagePath);
            BuilderImageValidation.Validate(bytes);
            loadedImage = Image.Load<Rgba32>(bytes);
        }
        else if (BrowserContentCatalog.TryGetBinaryForPath(walkmaskImagePath, out var walkmaskBytes)
            && walkmaskBytes.Length > 0)
        {
            BuilderImageValidation.Validate(walkmaskBytes);
            loadedImage = Image.Load<Rgba32>(walkmaskBytes);
        }

        if (loadedImage is null)
        {
            throw new FileNotFoundException($"Walkmask image \"{walkmaskImagePath}\" was not found.", walkmaskImagePath);
        }

        using var image = loadedImage;
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new InvalidOperationException($"Walkmask image \"{walkmaskImagePath}\" is empty.");
        }

        var builder = new StringBuilder();
        builder.AppendLine("{WALKMASK}");
        builder.AppendLine(image.Width.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine(image.Height.ToString(CultureInfo.InvariantCulture));

        var charFill = 0;
        var packedValue = 0;
        for (var y = 0; y < image.Height; y += 1)
        {
            for (var x = 0; x < image.Width; x += 1)
            {
                packedValue <<= 1;
                if (image[x, y].A > 0)
                {
                    packedValue += 1;
                }

                charFill += 1;
                if (charFill == 6)
                {
                    builder.Append((char)(packedValue + 32));
                    packedValue = 0;
                    charFill = 0;
                }
            }
        }

        if (charFill > 0)
        {
            for (; charFill < 6; charFill += 1)
            {
                packedValue <<= 1;
            }

            builder.Append((char)(packedValue + 32));
        }

        builder.AppendLine();
        builder.Append("{END WALKMASK}");
        return builder.ToString();
    }

    private static string BuildWalkmaskSection(CustomMapBuilderDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document.WalkmaskImagePath))
        {
            return BuildWalkmaskSection(document.WalkmaskImagePath);
        }

        if (string.IsNullOrWhiteSpace(document.EmbeddedWalkmaskSection))
        {
            throw new InvalidOperationException("A walkmask image or embedded walkmask section is required before exporting a custom map.");
        }

        return string.Concat(
            "{WALKMASK}",
            Environment.NewLine,
            document.EmbeddedWalkmaskSection,
            Environment.NewLine,
            "{END WALKMASK}");
    }

    private static void WritePngWithLevelData(string backgroundImagePath, string outputPath, string levelData)
    {
        using var input = File.OpenRead(backgroundImagePath);
        using var output = File.Create(outputPath);
        output.Write(PngSignature);
        foreach (var chunk in PngMapData.ReadChunks(input))
        {
            if (PngMapData.TryText(chunk, out var keyword, out var text) && PngMapData.IsMapText(keyword, text)) continue;
            if (chunk.Type == "IEND") WriteTextChunk(output, PngMapData.Keyword, levelData);
            WriteChunk(output, chunk.Type, chunk.Data);
        }
    }

    private static void WriteTextChunk(Stream output, string keyword, string value)
    {
        var keywordBytes = Encoding.ASCII.GetBytes(keyword);
        var unicode = value.Any(static character => character > 255);
        if (unicode)
        {
            var header = Encoding.ASCII.GetBytes(keyword + "\0\0\0\0\0");
            WriteChunk(output, "iTXt", header.Concat(Encoding.UTF8.GetBytes(value)).ToArray());
            return;
        }
        var valueBytes = Encoding.Latin1.GetBytes(value);
        var data = new byte[keywordBytes.Length + 1 + valueBytes.Length];
        Buffer.BlockCopy(keywordBytes, 0, data, 0, keywordBytes.Length);
        Buffer.BlockCopy(valueBytes, 0, data, keywordBytes.Length + 1, valueBytes.Length);
        WriteChunk(output, "tEXt", data);
    }

    private static void WriteChunk(Stream output, string chunkType, byte[] data)
    {
        Span<byte> lengthBuffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, data.Length);
        output.Write(lengthBuffer);

        var typeBytes = Encoding.ASCII.GetBytes(chunkType);
        output.Write(typeBytes);
        output.Write(data);

        Span<byte> crcBuffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBuffer, ComputeCrc(typeBytes, data));
        output.Write(crcBuffer);
    }

    private static void AppendGgonList(StringBuilder builder, List<IReadOnlyDictionary<string, string>> values)
    {
        builder.Append('[');
        for (var index = 0; index < values.Count; index += 1)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            AppendGgonMap(builder, values[index]);
        }

        builder.Append(']');
    }

    private static void AppendGgonMap(StringBuilder builder, IReadOnlyDictionary<string, string> map)
    {
        builder.Append('{');
        var first = true;
        foreach (var pair in map.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!first)
            {
                builder.Append(',');
            }

            AppendGgonToken(builder, pair.Key);
            builder.Append(':');
            AppendGgonToken(builder, pair.Value);
            first = false;
        }

        builder.Append('}');
    }

    private static void AppendGgonToken(StringBuilder builder, string value)
    {
        if (CanUseUnquotedGgonToken(value))
        {
            builder.Append(value);
            return;
        }

        builder.Append('\'');
        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '\\' => "\\\\",
                '\'' => "\\'",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\0' => "\\0",
                _ => character.ToString(),
            });
        }

        builder.Append('\'');
    }

    private static bool CanUseUnquotedGgonToken(string value)
    {
        return value.Length > 0
            && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or '-' or '+');
    }

    private static uint ComputeCrc(byte[] typeBytes, byte[] data)
    {
        var crc = 0xffffffffu;
        crc = UpdateCrc(crc, typeBytes);
        crc = UpdateCrc(crc, data);
        return crc ^ 0xffffffffu;
    }

    private static uint UpdateCrc(uint crc, byte[] bytes)
    {
        foreach (var value in bytes)
        {
            crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index += 1)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit += 1)
            {
                value = (value & 1) != 0 ? 0xedb88320u ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
