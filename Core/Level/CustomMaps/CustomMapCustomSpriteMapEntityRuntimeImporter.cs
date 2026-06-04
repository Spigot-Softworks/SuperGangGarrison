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

        var (width, height) = CustomMapCustomSpriteMetadata.ResolveWorldDimensions(
            pixelWidth,
            pixelHeight,
            configuration.Scale);
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
            CustomMapSprite: configuration with { }));
        return true;
    }
}
