using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

internal sealed class CustomMapCustomSpriteMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => CustomMapCustomSpriteMetadata.CustomSpriteEntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!args.Type.Equals(EntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var properties = args.Properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var configuration = CustomMapCustomSpriteMetadata.ParseConfiguration(properties);
        var pixelWidth = 42;
        var pixelHeight = 42;
        if (configuration.HasImage
            && properties.TryGetValue(CustomMapCustomSpriteMetadata.ImagePropertyKey, out var resourceName)
            && context.Resources.TryGetValue(resourceName, out var resource)
            && CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out var bytes)
            && CustomMapCustomSpriteMetadata.TryParsePngDimensions(bytes, out var decodedWidth, out var decodedHeight))
        {
            pixelWidth = decodedWidth;
            pixelHeight = decodedHeight;
        }

        var xScale = NormalizeEntityScale(args.XScale);
        var yScale = NormalizeEntityScale(args.YScale);
        var drawScale = configuration.Scale * MathF.Max(xScale, yScale);
        var (baseWidth, baseHeight) = CustomMapCustomSpriteMetadata.ResolveWorldDimensions(
            pixelWidth,
            pixelHeight,
            configuration.Scale,
            configuration);
        var width = baseWidth * xScale;
        var height = baseHeight * yScale;
        var (left, top) = CustomMapEntityPlacementAnchor.ToTopLeft(
            args.X,
            args.Y,
            width,
            height,
            useCenterOrigin: true);
        context.RoomObjects.Add(new RoomObjectMarker(
            RoomObjectType.CustomMapSprite,
            left,
            top,
            width,
            height,
            string.Empty,
            SourceName: EntityType,
            CustomMapSprite: configuration with { Scale = drawScale }));
        return true;
    }

    private static float NormalizeEntityScale(float scale)
    {
        return MathF.Abs(scale) > 0.0001f ? MathF.Abs(scale) : 1f;
    }
}
