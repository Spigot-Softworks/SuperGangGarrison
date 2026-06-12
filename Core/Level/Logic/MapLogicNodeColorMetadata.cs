using System.Globalization;

namespace OpenGarrison.Core;

public static class MapLogicNodeColorMetadata
{
    public const string PropertyKey = "logicColor";

    public const byte DefaultRed = 48;
    public const byte DefaultGreen = 168;
    public const byte DefaultBlue = 156;

    public static readonly string[] VgaPaletteHex =
    [
        "#000000",
        "#0000AA",
        "#00AA00",
        "#00AAAA",
        "#AA0000",
        "#AA00AA",
        "#AA5500",
        "#AAAAAA",
        "#555555",
        "#5555FF",
        "#55FF55",
        "#55FFFF",
        "#FF5555",
        "#FF55FF",
        "#FFFF55",
        "#FFFFFF",
    ];

    public static bool SupportsRecolor(string? entityType)
    {
        if (!MapLogicMetadata.IsLogicEntityType(entityType))
        {
            return false;
        }

        return !entityType!.Equals(MapLogicMetadata.AreaEntityType, StringComparison.OrdinalIgnoreCase)
            && !DamageableMetadata.IsDamageableEntityType(entityType);
    }

    public static bool TryResolveFillColor(
        IReadOnlyDictionary<string, string>? properties,
        out byte red,
        out byte green,
        out byte blue)
    {
        red = DefaultRed;
        green = DefaultGreen;
        blue = DefaultBlue;
        if (properties is null
            || !properties.TryGetValue(PropertyKey, out var raw)
            || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return TryParseColor(raw, out red, out green, out blue);
    }

    public static bool TryParseColor(string? raw, out byte red, out byte green, out byte blue)
    {
        red = DefaultRed;
        green = DefaultGreen;
        blue = DefaultBlue;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length != 6
            || !byte.TryParse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out red)
            || !byte.TryParse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out green)
            || !byte.TryParse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out blue))
        {
            red = DefaultRed;
            green = DefaultGreen;
            blue = DefaultBlue;
            return false;
        }

        return true;
    }

    public static string FormatColor(byte red, byte green, byte blue)
    {
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    public static void SetColor(IDictionary<string, string> properties, byte red, byte green, byte blue)
    {
        properties[PropertyKey] = FormatColor(red, green, blue);
    }

    public static void ClearColor(IDictionary<string, string> properties)
    {
        properties.Remove(PropertyKey);
    }
}
