using System.Globalization;

namespace OpenGarrison.Core;

public static class EmbeddedWalkmaskDecoder
{
    public static bool TryGetDimensions(string walkmaskSection, out int width, out int height)
    {
        if (!TryReadWalkmask(walkmaskSection, out _, out _, out var dimensions))
        {
            width = 0;
            height = 0;
            return false;
        }

        width = dimensions.Width;
        height = dimensions.Height;
        return true;
    }

    public static bool TryDecodeSolidCells(string walkmaskSection, out int width, out int height, out bool[] cells)
    {
        width = 0;
        height = 0;
        cells = Array.Empty<bool>();
        if (!TryReadWalkmask(walkmaskSection, out var lines, out var firstContentLine, out var dimensions))
        {
            return false;
        }

        width = dimensions.Width;
        height = dimensions.Height;
        cells = new bool[width * height];
        var packed = string.Concat(lines.Skip(firstContentLine + 2));
        if (packed.Length == 0)
        {
            cells = Array.Empty<bool>();
            width = 0;
            height = 0;
            return false;
        }

        var trailingUnusedBits = Math.Max(0, (packed.Length * 6) - (width * height));
        var packedIndex = packed.Length - 1 - (trailingUnusedBits / 6);
        if (packedIndex < 0)
        {
            cells = Array.Empty<bool>();
            width = 0;
            height = 0;
            return false;
        }

        var bitMask = 1 << (trailingUnusedBits % 6);
        var packedValue = Math.Max(0, packed[packedIndex] - 32);
        for (var row = height - 1; row >= 0; row -= 1)
        {
            for (var column = width - 1; column >= 0; column -= 1)
            {
                if (bitMask == 64)
                {
                    packedIndex -= 1;
                    if (packedIndex < 0)
                    {
                        cells = Array.Empty<bool>();
                        width = 0;
                        height = 0;
                        return false;
                    }

                    packedValue = Math.Max(0, packed[packedIndex] - 32);
                    bitMask = 1;
                }

                if ((packedValue & bitMask) != 0)
                {
                    cells[(row * width) + column] = true;
                }

                bitMask <<= 1;
            }
        }

        return true;
    }

    private readonly record struct WalkmaskDimensions(int Width, int Height);

    private static bool TryReadWalkmask(
        string walkmaskSection,
        out string[] lines,
        out int firstContentLine,
        out WalkmaskDimensions dimensions)
    {
        lines = walkmaskSection
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n');
        firstContentLine = 0;
        while (firstContentLine < lines.Length && string.IsNullOrWhiteSpace(lines[firstContentLine]))
        {
            firstContentLine += 1;
        }

        if (firstContentLine + 2 >= lines.Length
            || !int.TryParse(lines[firstContentLine].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
            || !int.TryParse(lines[firstContentLine + 1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
            || width <= 0
            || height <= 0)
        {
            dimensions = default;
            return false;
        }

        dimensions = new WalkmaskDimensions(width, height);
        return true;
    }
}
