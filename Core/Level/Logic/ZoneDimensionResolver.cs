using System;

namespace OpenGarrison.Core;

internal static class ZoneDimensionResolver
{
    public static (float Width, float Height) Resolve(
        float defaultWidth,
        float defaultHeight,
        float minExtent,
        float xScale,
        float yScale)
    {
        var width = defaultWidth * MathF.Abs(xScale <= 0f ? 1f : xScale);
        var height = defaultHeight * MathF.Abs(yScale <= 0f ? 1f : yScale);
        return (MathF.Max(minExtent, width), MathF.Max(minExtent, height));
    }
}
