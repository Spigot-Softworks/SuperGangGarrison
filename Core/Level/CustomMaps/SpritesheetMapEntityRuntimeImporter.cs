using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

internal sealed class SpritesheetMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => SpritesheetMetadata.SpritesheetEntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!args.Type.Equals(EntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var properties = args.Properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var configuration = SpritesheetMetadata.ParseConfiguration(properties);
        var (width, height) = (42f, 42f);
        if (configuration.HasImage
            && properties.TryGetValue(SpritesheetMetadata.ImagePropertyKey, out var resourceName)
            && context.Resources.TryGetValue(resourceName, out var resource)
            && CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out var bytes)
            && SpritesheetMetadata.TryParsePngDimensions(bytes, out var imageWidth, out var imageHeight))
        {
            (width, height) = SpritesheetMetadata.ResolveWorldDimensions(
                imageWidth,
                imageHeight,
                configuration.Scale,
                configuration);
        }

        var xScale = NormalizeEntityScale(args.XScale);
        var yScale = NormalizeEntityScale(args.YScale);
        width *= xScale;
        height *= yScale;

        var (left, top) = CustomMapEntityPlacementAnchor.ToTopLeft(
            args.X,
            args.Y,
            width,
            height,
            useCenterOrigin: true);
        context.RoomObjects.Add(new RoomObjectMarker(
            RoomObjectType.Spritesheet,
            left,
            top,
            width,
            height,
            string.Empty,
            SourceName: EntityType,
            Spritesheet: configuration with { }));
        return true;
    }

    private static float NormalizeEntityScale(float scale)
    {
        return MathF.Abs(scale) > 0.0001f ? MathF.Abs(scale) : 1f;
    }
}
