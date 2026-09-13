using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace OpenGarrison.Core;

/// <summary>Reject excessive or malformed assets before allocating decoded pixels.</summary>
public static class BuilderImageValidation
{
    public const int MaximumFileBytes = 64 * 1024 * 1024;
    public static byte[] ReadFile(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > MaximumFileBytes) throw new InvalidDataException($"Image '{path}' exceeds 64 MB.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    public static void Validate(byte[] bytes)
    {
        if (bytes.Length > MaximumFileBytes) throw new InvalidDataException("Image exceeds 64 MB.");
        var info = Image.Identify(new DecoderOptions { MaxFrames = 2 }, bytes);
        if (info.Width <= 0 || info.Height <= 0 || info.Width > 16384 || info.Height > 16384 || (long)info.Width * info.Height > 32L * 1024 * 1024)
            throw new InvalidDataException("Image exceeds the supported dimensions (16,384 per side, 32 million pixels).");
    }

    public static void ValidateDecoded(byte[] bytes)
    {
        Validate(bytes);
        using var image = Image.Load(new DecoderOptions { MaxFrames = 1 }, bytes);
    }
}
