using System;
using System.Collections.Generic;
using System.Globalization;
using SixLabors.ImageSharp;

namespace OpenGarrison.Core;

/// <summary>
/// Imports GG2's "foreground" and "foreground_scale" entities (scenery images, often animated
/// GIFs such as dm_fortress's grass) as foreground sprites. GG2 draws them from the entity's
/// top-left corner at xscale/yscale, steps animations by animationspeed frames per 30 Hz tick,
/// and fades them while they cover a player unless fade is false.
/// </summary>
internal sealed class Gg2ForegroundMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public const string ForegroundEntityType = "foreground";
    public const string ForegroundScaleEntityType = "foreground_scale";
    public const float Gg2TicksPerSecond = 30f;

    public Gg2ForegroundMapEntityRuntimeImporter(string entityType)
    {
        EntityType = entityType;
    }

    public string EntityType { get; }

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!args.Type.Equals(EntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var properties = args.Properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!properties.TryGetValue("resource", out var rawResource)
            || string.IsNullOrWhiteSpace(rawResource))
        {
            return false;
        }

        var resourceName = rawResource.Trim();
        if (!context.Resources.TryGetValue(resourceName, out var resource)
            || !CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out var bytes)
            || !TryGetImageDimensions(bytes, out var pixelWidth, out var pixelHeight))
        {
            return false;
        }

        // foreground_scale stores one "scale" (mirrored into xscale/yscale by the GG2 editor);
        // foreground stretches with xscale/yscale. Prefer the explicit axes.
        var uniformScale = ParseFloat(properties, "scale", 1f);
        var xScale = HasKey(properties, "xscale") ? Normalize(args.XScale) : Normalize(uniformScale);
        var yScale = HasKey(properties, "yscale") ? Normalize(args.YScale) : Normalize(uniformScale);
        var width = pixelWidth * xScale;
        var height = pixelHeight * yScale;

        var depth = ParseFloat(properties, "depth", -2f);
        var animationSpeed = ParseFloat(properties, "animationspeed", 0f);
        var opacity = Math.Clamp(ParseFloat(properties, "opacity", 1f), 0f, 1f);
        var configuration = new ForegroundSpriteConfiguration(
            resourceName,
            depth < 0f ? ForegroundSpriteLayerKind.Fg : ForegroundSpriteLayerKind.Bg,
            0,
            MathF.Max(xScale, yScale),
            false,
            ForegroundSpriteMetadata.DefaultOutsideOpacity,
            ForegroundSpriteMetadata.DefaultInsideOpacity,
            ForegroundSpriteBoundaryKind.Box,
            false,
            MapSpriteTileAnchor.TopLeft,
            0f,
            0f)
        {
            AnimationFramesPerSecond = MathF.Max(0f, animationSpeed) * Gg2TicksPerSecond,
            NoCoverFade = properties.TryGetValue("fade", out var fade) && IsFalse(fade),
            FixedOpacity = opacity,
        };

        context.RoomObjects.Add(new RoomObjectMarker(
            RoomObjectType.ForegroundSprite,
            args.X,
            args.Y,
            width,
            height,
            string.Empty,
            SourceName: EntityType,
            ForegroundSprite: configuration));
        return true;
    }

    internal static bool TryGetImageDimensions(byte[] bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (bytes.Length == 0)
        {
            return false;
        }

        try
        {
            var info = Image.Identify(bytes);
            width = info.Width;
            height = info.Height;
            return width > 0 && height > 0;
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool HasKey(IReadOnlyDictionary<string, string> properties, string key)
    {
        return properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);
    }

    private static float ParseFloat(IReadOnlyDictionary<string, string> properties, string key, float fallback)
    {
        return properties.TryGetValue(key, out var value)
            && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && float.IsFinite(parsed)
            ? parsed
            : fallback;
    }

    private static float Normalize(float scale)
    {
        return MathF.Abs(scale) > 0.0001f ? MathF.Abs(scale) : 1f;
    }

    private static bool IsFalse(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Equals("false", StringComparison.OrdinalIgnoreCase) || trimmed == "0";
    }
}
