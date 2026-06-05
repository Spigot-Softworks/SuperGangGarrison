using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

internal sealed class DamageableMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => DamageableMetadata.DamageableEntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!args.Type.Equals(EntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var properties = new Dictionary<string, string>(args.Properties ?? EmptyProperties, StringComparer.OrdinalIgnoreCase);
        var mapEntityId = MapLogicMetadata.EnsureMapEntityId(properties);
        properties[MapLogicMetadata.MapEntityIdPropertyKey] = mapEntityId;
        var (width, height) = DamageableMetadata.ResolveZoneDimensions(args.XScale, args.YScale);
        var (left, top) = CustomMapEntityPlacementAnchor.ToTopLeft(
            EntityType,
            args.X,
            args.Y,
            width,
            height,
            context.UseCenterOrigin);
        var configuration = DamageableMetadata.ParseZoneConfiguration(properties);
        context.RoomObjects.Add(new RoomObjectMarker(
            RoomObjectType.DamageableZone,
            left,
            top,
            width,
            height,
            string.Empty,
            SourceName: mapEntityId,
            DamageableZone: configuration));
        return true;
    }

    private static readonly Dictionary<string, string> EmptyProperties =
        new(StringComparer.OrdinalIgnoreCase);
}
