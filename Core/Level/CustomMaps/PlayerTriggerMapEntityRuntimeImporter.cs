using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

internal sealed class PlayerTriggerMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => PlayerTriggerMetadata.PlayerTriggerEntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!args.Type.Equals(EntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var properties = args.Properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var logicKey = MapLogicMetadata.EnsureLogicKey(properties);
        var (width, height) = PlayerTriggerMetadata.ResolveZoneDimensions(args.XScale, args.YScale);
        var (left, top) = CustomMapEntityPlacementAnchor.ToTopLeft(
            EntityType,
            args.X,
            args.Y,
            width,
            height,
            context.UseCenterOrigin);
        var zone = PlayerTriggerMetadata.ParseZoneConfiguration(properties);
        context.RoomObjects.Add(new RoomObjectMarker(
            RoomObjectType.PlayerTriggerZone,
            left,
            top,
            width,
            height,
            string.Empty,
            SourceName: logicKey,
            PlayerTriggerZone: zone));
        return true;
    }
}
