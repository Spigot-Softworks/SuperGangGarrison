using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

/// <summary>
/// A map-author rectangle marking an indoor area. Presentation only: ambient weather
/// treats it like a roof (it stops at the top edge and never appears inside), and the
/// simulation never reads it.
/// </summary>
public readonly record struct IndoorRegionMarker(float X, float Y, float Width, float Height)
{
    public float Left => X;

    public float Top => Y;

    public float Right => X + Width;

    public float Bottom => Y + Height;

    public bool Contains(float x, float y) => x >= X && x < Right && y >= Y && y < Bottom;

    public IndoorRegionMarker Scale(float scale) => new(X * scale, Y * scale, Width * scale, Height * scale);
}

public static class IndoorRegionMetadata
{
    public const string EntityType = "indoorRegion";

    /// <summary>Unscaled box size; the builder's xscale/yscale multiply it, as for kill boxes.</summary>
    public const float BaseSize = 42f;

    public static bool IsIndoorRegionEntityType(string? type)
    {
        return !string.IsNullOrWhiteSpace(type)
            && type.Equals(EntityType, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Entity X/Y is the top-left corner, matching the other effect boxes.</summary>
    public static IndoorRegionMarker FromEntity(float x, float y, float xScale, float yScale)
    {
        var width = BaseSize * (MathF.Abs(xScale) <= 0f ? 1f : MathF.Abs(xScale));
        var height = BaseSize * (MathF.Abs(yScale) <= 0f ? 1f : MathF.Abs(yScale));
        return new IndoorRegionMarker(x, y, width, height);
    }
}

internal sealed class IndoorRegionMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => IndoorRegionMetadata.EntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!IndoorRegionMetadata.IsIndoorRegionEntityType(args.Type))
        {
            return false;
        }

        var region = IndoorRegionMetadata.FromEntity(args.X, args.Y, args.XScale, args.YScale);
        var (left, top) = CustomMapEntityPlacementAnchor.ToTopLeft(
            args.Type,
            region.X,
            region.Y,
            region.Width,
            region.Height,
            context.UseCenterOrigin);
        context.IndoorRegions.Add(region with { X = left, Y = top });
        return true;
    }
}
