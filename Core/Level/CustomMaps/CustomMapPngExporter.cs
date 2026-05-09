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

        var levelData = BuildLevelData(normalized);
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        WritePngWithLevelData(normalized.BackgroundImagePath, outputPath, levelData);
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
        using var image = Image.Load<Rgba32>(walkmaskImagePath);
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
            document.EmbeddedWalkmaskSection.Trim(),
            Environment.NewLine,
            "{END WALKMASK}");
    }

    private static void WritePngWithLevelData(string backgroundImagePath, string outputPath, string levelData)
    {
        using var input = new MemoryStream(File.ReadAllBytes(backgroundImagePath), writable: false);
        using var output = File.Create(outputPath);
        var signature = new byte[PngSignature.Length];
        if (input.Read(signature) != signature.Length || !signature.SequenceEqual(PngSignature))
        {
            throw new InvalidOperationException($"Background image \"{backgroundImagePath}\" is not a PNG file.");
        }

        output.Write(signature);
        var inserted = false;
        Span<byte> lengthBuffer = stackalloc byte[4];
        Span<byte> chunkTypeBuffer = stackalloc byte[4];
        Span<byte> crcBuffer = stackalloc byte[4];
        while (input.Position + 8 <= input.Length)
        {
            input.ReadExactly(lengthBuffer);
            var dataLength = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
            input.ReadExactly(chunkTypeBuffer);
            if (dataLength < 0)
            {
                throw new InvalidOperationException($"PNG chunk length is invalid in \"{backgroundImagePath}\".");
            }

            var chunkData = new byte[dataLength];
            input.ReadExactly(chunkData);
            input.ReadExactly(crcBuffer);

            var chunkType = Encoding.ASCII.GetString(chunkTypeBuffer);
            if (chunkType == "IEND")
            {
                WriteTextChunk(output, "OpenGarrisonLevelData", levelData);
                inserted = true;
            }

            if (!IsExistingLevelDataTextChunk(chunkType, chunkData))
            {
                output.Write(lengthBuffer);
                output.Write(chunkTypeBuffer);
                output.Write(chunkData);
                output.Write(crcBuffer);
            }

            if (chunkType == "IEND")
            {
                break;
            }
        }

        if (!inserted)
        {
            throw new InvalidOperationException($"Background image \"{backgroundImagePath}\" did not contain an IEND chunk.");
        }
    }

    private static bool IsExistingLevelDataTextChunk(string chunkType, byte[] chunkData)
    {
        return chunkType == "tEXt"
            && Encoding.UTF8.GetString(chunkData).Contains("{WALKMASK}", StringComparison.OrdinalIgnoreCase)
            && Encoding.UTF8.GetString(chunkData).Contains("{ENTITIES}", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteTextChunk(Stream output, string keyword, string value)
    {
        var keywordBytes = Encoding.ASCII.GetBytes(keyword);
        var valueBytes = Encoding.UTF8.GetBytes(value);
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
