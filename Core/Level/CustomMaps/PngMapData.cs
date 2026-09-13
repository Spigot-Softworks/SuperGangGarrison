using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace OpenGarrison.Core;

/// <summary>The common PNG metadata reader for the editor, runtime and exporter.</summary>
internal static class PngMapData
{
    internal const string Keyword = "OpenGarrisonLevelData";
    private const int MaximumChunkBytes = 64 * 1024 * 1024;
    private const int MaximumTextCharacters = 64 * 1024 * 1024;
    internal sealed record Chunk(string Type, byte[] Data, byte[] Crc);

    internal static IEnumerable<Chunk> ReadChunks(Stream stream)
    {
        byte[] signature = new byte[8];
        stream.ReadExactly(signature);
        if (!signature.AsSpan().SequenceEqual(new byte[] {137, 80, 78, 71, 13, 10, 26, 10}))
            throw new InvalidDataException("The selected image is not a PNG.");
        long total = 0;
        while (true)
        {
            byte[] header = new byte[8];
            stream.ReadExactly(header);
            var length = BinaryPrimitives.ReadInt32BigEndian(header);
            if (length < 0 || length > MaximumChunkBytes || (total += length) > 512L * 1024 * 1024)
                throw new InvalidDataException("PNG data exceeds the supported size.");
            var data = new byte[length];
            stream.ReadExactly(data);
            var crc = new byte[4];
            stream.ReadExactly(crc);
            var type = Encoding.ASCII.GetString(header, 4, 4);
            yield return new Chunk(type, data, crc);
            if (type == "IEND") yield break;
        }
    }

    internal static bool TryText(Chunk chunk, out string keyword, out string text)
    {
        keyword = text = string.Empty;
        if (chunk.Type is not ("tEXt" or "zTXt" or "iTXt")) return false;
        var data = chunk.Data;
        var separator = Array.IndexOf(data, (byte)0);
        if (separator < 1 || separator > 79) throw new InvalidDataException("Invalid PNG text header.");
        keyword = Encoding.Latin1.GetString(data, 0, separator);
        var offset = separator + 1;
        var compressed = false;
        var encoding = Encoding.Latin1;
        if (chunk.Type == "zTXt")
        {
            if (offset >= data.Length || data[offset++] != 0) throw new InvalidDataException("Unsupported PNG text compression.");
            compressed = true;
        }
        else if (chunk.Type == "iTXt")
        {
            if (offset + 2 > data.Length || data[offset] > 1 || data[offset + 1] != 0)
                throw new InvalidDataException("Invalid PNG international text header.");
            compressed = data[offset] == 1;
            offset += 2;
            for (var i = 0; i < 2; i++)
            {
                var end = Array.IndexOf(data, (byte)0, offset);
                if (end < 0) throw new InvalidDataException("Invalid PNG international text header.");
                offset = end + 1;
            }
            encoding = Encoding.UTF8;
        }
        if (!compressed) text = encoding.GetString(data, offset, data.Length - offset);
        else
        {
            using var source = new MemoryStream(data, offset, data.Length - offset, writable: false);
            using var zip = new ZLibStream(source, CompressionMode.Decompress);
            using var reader = new StreamReader(zip, encoding, detectEncodingFromByteOrderMarks: false);
            var builder = new StringBuilder();
            char[] buffer = new char[8192];
            int count;
            while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (builder.Length + count > MaximumTextCharacters) throw new InvalidDataException("PNG map text is too large.");
                builder.Append(buffer, 0, count);
            }
            text = builder.ToString();
        }
        return true;
    }

    internal static bool IsMapText(string keyword, string text) =>
        keyword.Equals(Keyword, StringComparison.OrdinalIgnoreCase)
        || text.Contains("{ENTITIES}", StringComparison.OrdinalIgnoreCase)
        || text.Contains("{WALKMASK}", StringComparison.OrdinalIgnoreCase);

    internal static string Read(Stream stream, out string warning)
    {
        var legacy = new StringBuilder();
        var authoritative = new List<string>();
        foreach (var chunk in ReadChunks(stream))
        {
            if (!TryText(chunk, out var keyword, out var text)) continue;
            if (legacy.Length + text.Length > MaximumTextCharacters) throw new InvalidDataException("PNG map text is too large.");
            if (keyword.Equals(Keyword, StringComparison.OrdinalIgnoreCase) && IsComplete(text)) authoritative.Add(text);
            legacy.Append(text);
        }
        warning = string.Empty;
        if (authoritative.Count > 0)
        {
            var selected = authoritative[^1];
            if (authoritative.Count > 1 || legacy.Length > selected.Length)
                warning = "Loaded the newest saved map data; older embedded metadata was also present.";
            return selected;
        }
        return legacy.ToString();
    }

    private static bool IsComplete(string text)
    {
        try
        {
        var entities = Section(text, "ENTITIES");
        var mask = Section(text, "WALKMASK");
        return entities.Length > 0 && mask.Length > 0
            && EmbeddedWalkmaskDecoder.TryDecodeSolidCells(mask, out _, out _, out _)
            && CustomMapBuilderPngImporter.TryDecodeEntities(entities, out _, out _);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException) { return false; }
    }

    internal static string Section(string text, string name)
    {
        var marker = "{" + name + "}";
        var start = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return string.Empty;
        start += marker.Length;
        var end = text.IndexOf("{END " + name + "}", start, StringComparison.OrdinalIgnoreCase);
        return end < 0 ? string.Empty : text[start..end];
    }
}
