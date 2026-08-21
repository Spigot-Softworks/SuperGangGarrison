using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

/// <summary>
/// Extensible registry for map entity types used by the modern builder and runtime import.
/// Register additional <see cref="ICustomMapEntityRuntimeImporter"/> implementations to add new entity kinds.
/// </summary>
public static class CustomMapEntityRuntimeRegistry
{
    public const string EntitySchemaMetadataKey = "entity_schema";
    public const string ModernEntitySchemaValue = "modern";

    private static readonly List<ICustomMapEntityRuntimeImporter> Importers = [];

    static CustomMapEntityRuntimeRegistry()
    {
        Register(new SpawnMapEntityRuntimeImporter());
        Register(new LegacyTeamSpawnMapEntityRuntimeImporter());
        Register(new BarrierMapEntityRuntimeImporter());
        Register(new DirectionalWallMapEntityRuntimeImporter());
        Register(new ControlPointMapEntityRuntimeImporter());
        Register(new TeleportExitMapEntityRuntimeImporter());
        Register(new TeleportZoneMapEntityRuntimeImporter());
        Register(new PlayerTriggerMapEntityRuntimeImporter());
        Register(new DamageableMapEntityRuntimeImporter());
        Register(new PushBlockMapEntityRuntimeImporter());
        Register(new CatapultMapEntityRuntimeImporter());
        Register(new AreaMapEntityRuntimeImporter());
        Register(new CustomMapCustomSpriteMapEntityRuntimeImporter());
        Register(new ForegroundSpriteMapEntityRuntimeImporter());
        Register(new SpritesheetMapEntityRuntimeImporter());
        Register(new HealthPackMapEntityRuntimeImporter());
        Register(new JumpPadMapEntityRuntimeImporter());
        Register(new BotSpawnMapEntityRuntimeImporter());
        Register(new GameplayMessageMapEntityRuntimeImporter());
        Register(new GameplaySoundMapEntityRuntimeImporter());
        Register(new SpawnClassBehaviorMapEntityRuntimeImporter());
        Register(new LogicMapEntityRuntimeImporter());
    }

    public static IReadOnlyList<ICustomMapEntityRuntimeImporter> RegisteredImporters => Importers;

    public static void Register(ICustomMapEntityRuntimeImporter importer)
    {
        ArgumentNullException.ThrowIfNull(importer);
        Importers.Add(importer);
    }

    public static bool IsModernEntityType(string type)
    {
        foreach (var importer in Importers)
        {
            if (type.Equals(importer.EntityType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsModernEntities(IReadOnlyList<CustomMapBuilderEntity> entities)
    {
        foreach (var entity in entities)
        {
            if (IsModernEntityType(entity.Type))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryImport(
        string type,
        float x,
        float y,
        float xScale,
        float yScale,
        IReadOnlyDictionary<string, string> properties,
        CustomMapEntityImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var args = new CustomMapEntityImportArgs(type, x, y, xScale, yScale, properties);
        foreach (var importer in Importers)
        {
            if (importer.TryImport(args, context))
            {
                return true;
            }
        }

        return false;
    }
}

public interface ICustomMapEntityRuntimeImporter
{
    string EntityType { get; }

    bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context);
}

public readonly record struct CustomMapEntityImportArgs(
    string Type,
    float X,
    float Y,
    float XScale,
    float YScale,
    IReadOnlyDictionary<string, string> Properties);

public sealed class CustomMapEntityImportContext
{
    public IList<SpawnPoint> RedSpawns { get; init; } = [];
    public IList<SpawnPoint> BlueSpawns { get; init; } = [];
    public List<RoomObjectMarker> RoomObjects { get; init; } = [];
    public IList<HealthPackSpawnMarker> HealthPackSpawns { get; init; } = [];
    public IList<JumpPadSpawnMarker> JumpPadSpawns { get; init; } = [];
    public IList<BotSpawnMarker> BotSpawns { get; init; } = [];
    public IList<GameplayMessageMarker> GameplayMessages { get; init; } = [];
    public IList<GameplaySoundMarker> GameplaySounds { get; init; } = [];
    public IList<SpawnClassBehaviorMarker> SpawnClassBehaviors { get; init; } = [];

    /// <summary>
    /// When true, entity X/Y are sprite-origin coordinates and room-object markers are shifted to top-left.
    /// </summary>
    public bool UseCenterOrigin { get; init; }

    public IReadOnlyDictionary<string, CustomMapBuilderResource> Resources { get; init; } =
        new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase);
}

internal sealed class HealthPackMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => HealthPackMetadata.HealthPackEntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!HealthPackMetadata.IsHealthPackEntityType(args.Type))
        {
            return false;
        }

        context.HealthPackSpawns.Add(HealthPackSpawnMarker.FromProperties(args.X, args.Y, args.Properties));
        return true;
    }
}

internal sealed class JumpPadMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => JumpPadMetadata.EntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!JumpPadMetadata.IsJumpPadEntityType(args.Type))
        {
            return false;
        }

        // The builder entry is intentionally neutral-only. Treat missing team
        // as neutral for hand-authored package/PNG entities, but ignore a
        // malformed team override rather than silently creating a team pad.
        if (!JumpPadMetadata.IsNeutral(args.Properties))
        {
            return true;
        }

        context.JumpPadSpawns.Add(new JumpPadSpawnMarker(args.X, args.Y));
        return true;
    }
}

internal sealed class BotSpawnMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => BotSpawnMetadata.BotSpawnEntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!BotSpawnMetadata.IsBotSpawnEntityType(args.Type))
        {
            return false;
        }

        context.BotSpawns.Add(BotSpawnMetadata.FromProperties(args.X, args.Y, args.Properties));
        return true;
    }
}

internal sealed class GameplayMessageMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => GameplayMessageMetadata.EntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!GameplayMessageMetadata.IsGameplayMessageEntityType(args.Type))
        {
            return false;
        }

        context.GameplayMessages.Add(GameplayMessageMetadata.FromProperties(
            args.X,
            args.Y,
            args.XScale,
            args.YScale,
            args.Properties));
        return true;
    }
}

internal sealed class SpawnClassBehaviorMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => SpawnClassBehaviorMetadata.EntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!SpawnClassBehaviorMetadata.IsSpawnClassBehaviorEntityType(args.Type))
        {
            return false;
        }

        context.SpawnClassBehaviors.Add(SpawnClassBehaviorMetadata.FromProperties(args.X, args.Y, args.Properties));
        return true;
    }
}

internal sealed class PushBlockMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => PushBlockMetadata.EntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!PushBlockMetadata.IsPushBlockEntityType(args.Type))
        {
            return false;
        }

        context.RoomObjects.Add(PushBlockMetadata.CreateMarker(
            args.X,
            args.Y,
            args.XScale,
            args.YScale,
            args.Properties,
            context.UseCenterOrigin));
        return true;
    }
}

internal sealed class CatapultMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => CatapultMetadata.EntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!CatapultMetadata.IsCatapultEntityType(args.Type))
        {
            return false;
        }

        context.RoomObjects.Add(CatapultMetadata.CreateMarker(
            args.X,
            args.Y,
            args.XScale,
            args.YScale,
            args.Properties,
            context.UseCenterOrigin));
        return true;
    }
}

internal sealed class GameplaySoundMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => GameplaySoundMetadata.EntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!GameplaySoundMetadata.IsGameplaySoundEntityType(args.Type))
        {
            return false;
        }

        context.GameplaySounds.Add(GameplaySoundMetadata.FromProperties(args.X, args.Y, args.Properties));
        return true;
    }
}

internal sealed class SpawnMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => "spawn";

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!args.Type.Equals(EntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var team = ReadProperty(args.Properties, "team", "red");
        var forward = ReadBoolProperty(args.Properties, "forward", false);
        SpawnPoint spawn;
        if (forward)
        {
            var linkedIndex = ForwardSpawnMetadata.ParseLinkedControlPointIndex(args.Properties);
            var useCondition = ForwardSpawnMetadata.TryParseUseCondition(
                ReadProperty(args.Properties, ForwardSpawnMetadata.UseWhenPropertyKey, ForwardSpawnMetadata.DefaultUseWhenValue),
                out var parsedCondition)
                ? parsedCondition
                : ForwardSpawnUseCondition.ObjectiveOwnedByTeam;
            spawn = new SpawnPoint(
                args.X,
                args.Y,
                SpawnPointRole.Forward,
                linkedIndex,
                useCondition,
                ForwardSpawnMetadata.ParsePriority(args.Properties));
        }
        else
        {
            spawn = new SpawnPoint(args.X, args.Y);
        }

        if (team.Equals("neutral", StringComparison.OrdinalIgnoreCase))
        {
            context.RedSpawns.Add(spawn);
            context.BlueSpawns.Add(spawn);
        }
        else if (team.Equals("blue", StringComparison.OrdinalIgnoreCase))
        {
            context.BlueSpawns.Add(spawn);
        }
        else
        {
            context.RedSpawns.Add(spawn);
        }

        return true;
    }

    private static string ReadProperty(IReadOnlyDictionary<string, string> properties, string key, string fallback)
    {
        return properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }

    private static bool ReadBoolProperty(IReadOnlyDictionary<string, string> properties, string key, bool fallback)
    {
        if (!properties.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class LegacyTeamSpawnMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => "legacyTeamSpawn";

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!LegacyTeamSpawnRuntimeImport.TryCreateSpawnPoint(args.Type, args.X, args.Y, out var result))
        {
            return false;
        }

        switch (result.Team)
        {
            case PlayerTeam.Blue:
                context.BlueSpawns.Add(result.SpawnPoint);
                break;
            default:
                context.RedSpawns.Add(result.SpawnPoint);
                break;
        }

        return true;
    }
}

internal sealed class BarrierMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => "barrier";

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!args.Type.Equals(EntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var floor = BarrierConfiguration.IsFloorOrientation(args.Properties);
        var properties = BarrierLegacyPropertyMigration.EnsureModernBarrierProperties(args.Properties);
        var configuration = BarrierConfiguration.FromProperties(properties);
        if (!configuration.Targets.BlocksAnyPlayerMovement()
            && !configuration.Targets.BlocksAnyProjectile())
        {
            return true;
        }
        var (width, height) = BarrierConfiguration.ResolveDimensions(args.XScale, args.YScale, floor);
        var (x, y) = CustomMapEntityPlacementAnchor.ToTopLeft(args.X, args.Y, width, height, context.UseCenterOrigin);
        context.RoomObjects.Add(BarrierConfiguration.CreateMarker(
            x,
            y,
            args.XScale,
            args.YScale,
            configuration,
            floor));
        return true;
    }
}

internal sealed class DirectionalWallMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => "directionalWall";

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!args.Type.Equals(EntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var configuration = DirectionalWallConfiguration.FromProperties(args.Properties);
        if (!configuration.AffectsPlayers && !configuration.AffectsProjectiles)
        {
            return true;
        }

        var (width, height) = BarrierConfiguration.ResolveDimensions(args.XScale, args.YScale, configuration.UsesFloorShape);
        var (x, y) = CustomMapEntityPlacementAnchor.ToTopLeft(args.X, args.Y, width, height, context.UseCenterOrigin);
        context.RoomObjects.Add(DirectionalWallConfiguration.CreateMarker(
            x,
            y,
            args.XScale,
            args.YScale,
            configuration));
        return true;
    }
}

internal sealed class LogicMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => MapLogicMetadata.CpTriggerEntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!MapLogicMetadata.IsLogicEntityType(args.Type)
            || args.Type.Equals(MapLogicMetadata.PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}

internal sealed class ControlPointMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => "controlPoint";

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!args.Type.Equals(EntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var index = ReadIndex(args.Properties);
        var sourceName = $"controlPoint{index}";
        if (!CustomMapRoomObjectFactory.TryCreate(
                sourceName,
                args.X,
                args.Y,
                args.XScale,
                args.YScale,
                args.Properties,
                out var marker,
                context.UseCenterOrigin))
        {
            return false;
        }

        context.RoomObjects.Add(marker);
        return true;
    }

    private static int ReadIndex(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("index", out var rawValue)
            && int.TryParse(rawValue, out var index))
        {
            return Math.Clamp(index, 1, 5);
        }

        return 1;
    }
}
