using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

internal sealed class ForegroundSpriteMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => ForegroundSpriteMetadata.ForegroundSpriteEntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!args.Type.Equals(EntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var properties = args.Properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var configuration = ForegroundSpriteMetadata.ParseConfiguration(properties);
        var pixelWidth = 42;
        var pixelHeight = 42;
        if (configuration.HasImage
            && properties.TryGetValue(ForegroundSpriteMetadata.ImagePropertyKey, out var resourceName)
            && context.Resources.TryGetValue(resourceName, out var resource)
            && CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out var bytes)
            && ForegroundSpriteMetadata.TryParsePngDimensions(bytes, out var decodedWidth, out var decodedHeight))
        {
            pixelWidth = decodedWidth;
            pixelHeight = decodedHeight;
        }

        var (width, height) = ForegroundSpriteMetadata.ResolveWorldDimensions(
            pixelWidth,
            pixelHeight,
            configuration.Scale,
            configuration);
        var (left, top) = CustomMapEntityPlacementAnchor.ToTopLeft(
            args.X,
            args.Y,
            width,
            height,
            useCenterOrigin: true);
        context.RoomObjects.Add(new RoomObjectMarker(
            RoomObjectType.ForegroundSprite,
            left,
            top,
            width,
            height,
            string.Empty,
            SourceName: EntityType,
            ForegroundSprite: configuration with { }));
        return true;
    }
}
