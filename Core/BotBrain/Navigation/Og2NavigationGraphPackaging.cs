using System.IO.Compression;
using System.Text;

namespace OpenGarrison.Core.BotBrain;

/// <summary>Build-time conversion for runtimes that do not support BrotliStream.</summary>
public static class Og2NavigationGraphPackaging
{
    public static byte[] EncodeForBrowser(byte[] snapshot)
    {
        using var source = new MemoryStream(snapshot, writable: false);
        using var reader = new BinaryReader(source, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != 0x32474F47) throw new InvalidDataException("Invalid navigation graph header.");
        var version = reader.ReadInt32();
        byte[] legacy;
        if (version == 3) legacy = snapshot;
        else if (version == 4)
        {
            var compression = reader.ReadByte();
            var length = reader.ReadInt64();
            if (length is < 8 or > 512L * 1024L * 1024L) throw new InvalidDataException("Invalid navigation graph length.");
            using Stream decoder = compression switch
            {
                1 => new BrotliStream(source, CompressionMode.Decompress),
                2 => new GZipStream(source, CompressionMode.Decompress),
                _ => throw new InvalidDataException("Unknown navigation graph compression."),
            };
            using var decoded = new MemoryStream((int)length);
            decoder.CopyTo(decoded);
            if (decoded.Length != length) throw new InvalidDataException("Incomplete navigation graph.");
            legacy = decoded.ToArray();
        }
        else throw new InvalidDataException("Unsupported navigation graph format.");

        using var payload = new BinaryReader(new MemoryStream(legacy, writable: false));
        if (payload.ReadUInt32() != 0x32474F47 || payload.ReadInt32() != 3)
            throw new InvalidDataException("Invalid navigation graph payload.");
        using var output = new MemoryStream();
        using (var header = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
        {
            header.Write(0x32474F47u);
            header.Write(4);
            header.Write((byte)2); // gzip; browser runtime has no BrotliStream support.
            header.Write((long)legacy.Length);
        }
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true)) gzip.Write(legacy);
        return output.ToArray();
    }
}
