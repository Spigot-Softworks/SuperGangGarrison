using System;

namespace OpenGarrison.Core;

internal sealed class AreaMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => AreaExtensionMetadata.AreaEntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!args.Type.Equals(EntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var properties = args.Properties;
        if (!properties.TryGetValue(AreaExtensionMetadata.ExtendsPropertyKey, out var extendsRef)
            || string.IsNullOrWhiteSpace(extendsRef))
        {
            return false;
        }

        if (!AreaExtensionMetadata.TryResolveParentRoomObjectIndex(
                context.RoomObjects,
                extendsRef,
                out var parentRoomObjectIndex,
                out var kind))
        {
            return false;
        }

        var (width, height) = AreaExtensionMetadata.ResolveZoneDimensions(args.XScale, args.YScale);
        var (left, top) = CustomMapEntityPlacementAnchor.ToTopLeft(
            EntityType,
            args.X,
            args.Y,
            width,
            height,
            context.UseCenterOrigin);
        context.RoomObjects.Add(new RoomObjectMarker(
            RoomObjectType.AreaExtension,
            left,
            top,
            width,
            height,
            string.Empty,
            SourceName: EntityType,
            AreaExtension: new AreaExtensionConfiguration(parentRoomObjectIndex, kind)));
        return true;
    }
}
