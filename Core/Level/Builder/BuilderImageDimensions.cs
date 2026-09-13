using System.Runtime.CompilerServices;
using SixLabors.ImageSharp;

namespace OpenGarrison.Core;

internal static class BuilderImageDimensions
{
    private sealed record Dimensions(int Width, int Height);
    private static readonly ConditionalWeakTable<byte[], Dimensions> Cache = new();
    internal static bool TryGet(byte[] bytes, out int width, out int height)
    {
        var dimensions = Cache.GetValue(bytes, static data =>
        {
            try
            {
                BuilderImageValidation.Validate(data);
                var info = Image.Identify(data);
                return new Dimensions(info.Width, info.Height);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnknownImageFormatException or InvalidImageContentException)
            { return new Dimensions(0, 0); }
        });
        width = dimensions.Width; height = dimensions.Height;
        return width > 0 && height > 0;
    }
}
