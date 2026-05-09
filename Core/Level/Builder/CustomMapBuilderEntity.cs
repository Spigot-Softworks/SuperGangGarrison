using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace OpenGarrison.Core;

public sealed record CustomMapBuilderEntity(
    string Type,
    float X,
    float Y,
    float XScale,
    float YScale,
    IReadOnlyDictionary<string, string> Properties)
{
    public static CustomMapBuilderEntity Create(
        string type,
        float x,
        float y,
        IReadOnlyDictionary<string, string>? properties = null,
        float xScale = 1f,
        float yScale = 1f) => new(type, x, y, xScale, yScale, properties ?? EmptyProperties);

    public CustomMapBuilderEntity NormalizeForEditing()
    {
        var normalizedProperties = new Dictionary<string, string>(
            Properties ?? EmptyProperties,
            StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = Type.Trim(),
            ["x"] = X.ToString(CultureInfo.InvariantCulture),
            ["y"] = Y.ToString(CultureInfo.InvariantCulture),
        };

        var normalizedXScale = NormalizeScale(XScale);
        var normalizedYScale = NormalizeScale(YScale);
        if (Math.Abs(normalizedXScale - 1f) > float.Epsilon)
        {
            normalizedProperties["xscale"] = normalizedXScale.ToString(CultureInfo.InvariantCulture);
        }

        if (Math.Abs(normalizedYScale - 1f) > float.Epsilon)
        {
            normalizedProperties["yscale"] = normalizedYScale.ToString(CultureInfo.InvariantCulture);
        }

        return this with
        {
            Type = Type.Trim(),
            XScale = normalizedXScale,
            YScale = normalizedYScale,
            Properties = new ReadOnlyDictionary<string, string>(normalizedProperties),
        };
    }

    private static IReadOnlyDictionary<string, string> EmptyProperties { get; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private static float NormalizeScale(float scale)
    {
        return float.IsFinite(scale) && Math.Abs(scale) > 0f ? scale : 1f;
    }
}
