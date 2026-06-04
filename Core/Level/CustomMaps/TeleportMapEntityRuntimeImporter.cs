using System;

namespace OpenGarrison.Core;

internal sealed class TeleportExitMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => TeleportMetadata.TeleportExitEntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!args.Type.Equals(EntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var size = TeleportMetadata.ExitMarkerSize;
        var (left, top) = CustomMapEntityPlacementAnchor.ToTopLeft(
            args.X,
            args.Y,
            size,
            size,
            context.UseCenterOrigin);
        context.RoomObjects.Add(new RoomObjectMarker(
            RoomObjectType.TeleportExit,
            left,
            top,
            size,
            size,
            "spawnS",
            SourceName: EntityType));
        return true;
    }
}

internal sealed class TeleportZoneMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => TeleportMetadata.TeleportEntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!args.Type.Equals(EntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var (width, height) = TeleportMetadata.ResolveZoneDimensions(args.XScale, args.YScale);
        var (left, top) = CustomMapEntityPlacementAnchor.ToTopLeft(
            args.X,
            args.Y,
            width,
            height,
            context.UseCenterOrigin);
        var teleportZone = TeleportMetadata.ParseZoneConfiguration(args.Properties, context.RoomObjects);
        context.RoomObjects.Add(new RoomObjectMarker(
            RoomObjectType.TeleportZone,
            left,
            top,
            width,
            height,
            "sprite64",
            SourceName: EntityType,
            TeleportZone: teleportZone));
        return true;
    }
}
