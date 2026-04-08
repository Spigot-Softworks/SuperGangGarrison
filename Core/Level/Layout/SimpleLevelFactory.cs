using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenGarrison.Core;

public static class SimpleLevelFactory
{
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;
    private static IReadOnlyList<LevelCatalogEntry>? _cachedCatalog;

    public readonly record struct LevelCatalogEntry(
        string Name,
        GameModeKind Mode,
        string RoomSourcePath,
        string? CollisionMaskSourcePath);

    public static SimpleLevel CreateScoutPrototypeLevel(float mapScale = 1f)
    {
        return CreateImportedLevel("Truefort", mapScale: mapScale) ?? CreateFallbackPrototypeLevel("Prototype", mapScale);
    }

    public static SimpleLevel? CreateImportedLevel(string levelName, int mapAreaIndex = 1, float mapScale = 1f)
    {
        var catalog = GetAvailableSourceLevels();
        if (!TryFindCatalogEntry(catalog, levelName, out var levelSpec))
        {
            return null;
        }

        var isCustomMap = Path.GetExtension(levelSpec.RoomSourcePath).Equals(".png", StringComparison.OrdinalIgnoreCase);
        GameMakerRoomMetadata? importedRoom;
        IReadOnlyList<LevelSolid> importedSolids;
        if (isCustomMap)
        {
            var customMap = CustomMapPngImporter.Import(levelSpec.RoomSourcePath);
            if (customMap is null)
            {
                return null;
            }

            importedRoom = customMap.Room;
            importedSolids = customMap.Solids;
        }
        else
        {
            importedRoom = GameMakerRoomMetadataImporter.Import(levelSpec.RoomSourcePath);
            if (importedRoom is null)
            {
                return null;
            }

            var importedSolidsPath = levelSpec.CollisionMaskSourcePath;
            importedSolids = importedSolidsPath is null
                ? []
                : GameMakerCollisionMaskImporter.Import(importedSolidsPath, importedRoom.Bounds);
        }

        if (importedRoom is null)
        {
            return null;
        }

        var bounds = importedRoom.Bounds;
        var mapAreaCount = Math.Max(1, importedRoom.AreaBoundaries.Count + 1);
        var clampedAreaIndex = Math.Clamp(mapAreaIndex, 1, mapAreaCount);
        var areaFilter = BuildMapAreaFilter(clampedAreaIndex, importedRoom.AreaBoundaries);
        var redSpawns = FilterByArea(importedRoom.RedSpawns, areaFilter);
        if (redSpawns.Count == 0)
        {
            redSpawns = importedRoom.RedSpawns;
        }
        var blueSpawns = FilterByArea(importedRoom.BlueSpawns, areaFilter);
        if (blueSpawns.Count == 0)
        {
            blueSpawns = importedRoom.BlueSpawns;
        }
        if (isCustomMap && (redSpawns.Count == 0 || blueSpawns.Count == 0))
        {
            return null;
        }

        if (redSpawns.Count == 0)
        {
            redSpawns = [new SpawnPoint(220f, 320f)];
        }
        if (blueSpawns.Count == 0)
        {
            blueSpawns = [new SpawnPoint(bounds.Width - 220f, 320f)];
        }
        var spawn = redSpawns[0];
        var intelBases = FilterByArea(importedRoom.IntelBases, areaFilter);
        var roomObjects = FilterByArea(importedRoom.RoomObjects, areaFilter);
        var floorY = FindFloorBelowSpawn(importedSolids, spawn)
            ?? MathF.Min(bounds.Height - 40f, spawn.Y + 360f);
        if (isCustomMap && importedSolids.Count == 0)
        {
            return null;
        }

        var solids = importedSolids.Count > 0 ? importedSolids : CreateFallbackSolids(bounds, spawn, floorY);

        var level = new SimpleLevel(
            name: levelSpec.Name,
            mode: levelSpec.Mode,
            bounds: bounds,
            mapScale: 1f,
            backgroundAssetName: importedRoom.PrimaryBackgroundAssetName,
            mapAreaIndex: clampedAreaIndex,
            mapAreaCount: mapAreaCount,
            localSpawn: spawn,
            redSpawns: redSpawns,
            blueSpawns: blueSpawns,
            intelBases: intelBases,
            roomObjects: roomObjects,
            floorY: floorY,
            solids: solids,
            importedFromSource: true,
            areaTransitionMarkers: importedRoom.AreaTransitionMarkers,
            unsupportedSourceEntities: importedRoom.UnsupportedEntities);
        return SimpleLevelScaling.ApplyUniformScale(level, mapScale);
    }

    public static IReadOnlyList<LevelCatalogEntry> GetAvailableSourceLevels()
    {
        if (_cachedCatalog is not null)
        {
            return _cachedCatalog;
        }

        var entries = new List<LevelCatalogEntry>();
        foreach (var definition in OpenGarrisonStockMapCatalog.Definitions)
        {
            var stockMapPath = FindStockMapSourcePath(definition);
            if (stockMapPath is null)
            {
                continue;
            }

            entries.Add(new LevelCatalogEntry(definition.LevelName, definition.Mode, stockMapPath, null));
        }

        AppendCustomMapEntries(entries);

        _cachedCatalog = entries
            .OrderBy(entry => entry.Name, NameComparer)
            .ToArray();
        return _cachedCatalog;
    }

    public static void ClearCachedCatalog()
    {
        _cachedCatalog = null;
    }

    private static SimpleLevel CreateFallbackPrototypeLevel(string levelName, float mapScale)
    {
        var bounds = new WorldBounds(2400f, 1400f);
        var spawn = new SpawnPoint(220f, 320f);
        var floorY = MathF.Min(bounds.Height - 40f, spawn.Y + 360f);
        var level = new SimpleLevel(
            name: levelName,
            mode: GameModeKind.CaptureTheFlag,
            bounds: bounds,
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: spawn,
            redSpawns: [spawn],
            blueSpawns: [new SpawnPoint(bounds.Width - 220f, 320f)],
            intelBases: [],
            roomObjects: [],
            floorY: floorY,
            solids: CreateFallbackSolids(bounds, spawn, floorY),
            importedFromSource: false);
        return SimpleLevelScaling.ApplyUniformScale(level, mapScale);
    }

    private static LevelSolid[] CreateFallbackSolids(WorldBounds bounds, SpawnPoint spawn, float floorY)
    {
        return
        [
            new LevelSolid(0f, floorY, bounds.Width, MathF.Max(40f, bounds.Height - floorY)),
            new LevelSolid(MathF.Max(180f, spawn.X - 180f), floorY - 160f, 320f, 40f),
            new LevelSolid(MathF.Max(420f, spawn.X + 280f), floorY - 280f, 280f, 40f),
            new LevelSolid(MathF.Min(bounds.Width - 460f, spawn.X + 760f), floorY - 420f, 260f, 40f),
            new LevelSolid(MathF.Min(bounds.Width - 220f, spawn.X + 1220f), floorY - 300f, 120f, 300f),
        ];
    }

    private static float? FindFloorBelowSpawn(IReadOnlyList<LevelSolid> solids, SpawnPoint spawn)
    {
        var spawnX = spawn.X;
        var solidBelowSpawn = solids
            .Where(solid =>
                spawnX >= solid.Left
                && spawnX <= solid.Right
                && solid.Top >= spawn.Y)
            .OrderBy(solid => solid.Top)
            .Cast<LevelSolid?>()
            .FirstOrDefault();

        return solidBelowSpawn?.Top;
    }

    private static GameModeKind DetectMode(string roomFilePath)
    {
        var metadata = GameMakerRoomMetadataImporter.Import(roomFilePath);
        if (metadata is null)
        {
            return GameModeKind.CaptureTheFlag;
        }

        return DetectMode(metadata);
    }

    private static GameModeKind DetectMode(GameMakerRoomMetadata metadata)
    {
        if (metadata.RoomObjects.Any(marker => marker.Type == RoomObjectType.Generator))
        {
            return GameModeKind.Generator;
        }

        if (metadata.RoomObjects.Any(marker => marker.Type == RoomObjectType.ArenaControlPoint))
        {
            return GameModeKind.Arena;
        }

        if (metadata.RoomObjects.Any(static marker => marker.IsRedKothControlPoint())
            && metadata.RoomObjects.Any(static marker => marker.IsBlueKothControlPoint()))
        {
            return GameModeKind.DoubleKingOfTheHill;
        }

        if (metadata.RoomObjects.Any(static marker => marker.IsSingleKothControlPoint()))
        {
            return GameModeKind.KingOfTheHill;
        }

        if (metadata.RoomObjects.Any(marker => marker.Type == RoomObjectType.ControlPoint))
        {
            return GameModeKind.ControlPoint;
        }

        if (metadata.IntelBases.Count > 0)
        {
            return GameModeKind.CaptureTheFlag;
        }

        if (metadata.Name.StartsWith("tdm_", StringComparison.OrdinalIgnoreCase))
        {
            return GameModeKind.TeamDeathmatch;
        }

        return GameModeKind.CaptureTheFlag;
    }

    private static void AppendCustomMapEntries(List<LevelCatalogEntry> entries)
    {
        var customMapsDirectory = Path.Combine(RuntimePaths.ApplicationRoot, "Maps");
        if (!Directory.Exists(customMapsDirectory))
        {
            return;
        }

        foreach (var mapFile in Directory.EnumerateFiles(customMapsDirectory, "*.png"))
        {
            var mapName = Path.GetFileNameWithoutExtension(mapFile);
            if (string.IsNullOrWhiteSpace(mapName))
            {
                continue;
            }

            var imported = CustomMapPngImporter.Import(mapFile);
            if (imported is null)
            {
                continue;
            }

            entries.Add(new LevelCatalogEntry(mapName, DetectMode(imported.Room), mapFile, null));
        }
    }

    private static bool TryFindCatalogEntry(IReadOnlyList<LevelCatalogEntry> catalog, string levelName, out LevelCatalogEntry entry)
    {
        var trimmedLevelName = levelName.Trim();
        foreach (var candidate in catalog)
        {
            if (NameComparer.Equals(candidate.Name, trimmedLevelName))
            {
                entry = candidate;
                return true;
            }
        }

        var normalizedName = NormalizeLevelName(levelName);
        foreach (var candidate in catalog)
        {
            if (NameComparer.Equals(NormalizeLevelName(candidate.Name), normalizedName))
            {
                entry = candidate;
                return true;
            }
        }

        entry = default;
        return false;
    }

    private static string? FindStockMapSourcePath(OpenGarrisonStockMapDefinition definition)
    {
        var fileName = $"{definition.IniKey}.png";
        var runtimePath = ContentRoot.GetPath("StockMaps", fileName);
        if (File.Exists(runtimePath))
        {
            return runtimePath;
        }

        var projectContentPath = ProjectSourceLocator.FindFile(Path.Combine("Core", "Content", "StockMaps", fileName));
        if (!string.IsNullOrWhiteSpace(projectContentPath) && File.Exists(projectContentPath))
        {
            return projectContentPath;
        }

        var sourceContentPath = ProjectSourceLocator.FindFile(Path.Combine(ContentRoot.Path, "StockMaps", fileName));
        if (!string.IsNullOrWhiteSpace(sourceContentPath) && File.Exists(sourceContentPath))
        {
            return sourceContentPath;
        }

        return null;
    }

    private static string NormalizeLevelName(string levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
        {
            return string.Empty;
        }

        var trimmed = levelName.Trim();
        if (trimmed.StartsWith("ctf_", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("arena_", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("cp_", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("gen_", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("koth_", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("dkoth_", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("tdm_", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[(trimmed.IndexOf('_') + 1)..];
        }

        return trimmed;
    }

    private static Func<float, bool> BuildMapAreaFilter(int mapAreaIndex, IReadOnlyList<float> boundaries)
    {
        if (boundaries.Count == 0)
        {
            return _ => true;
        }

        var totalAreas = boundaries.Count + 1;
        var clampedIndex = Math.Clamp(mapAreaIndex, 1, totalAreas);
        if (clampedIndex == 1)
        {
            var upper = boundaries[0];
            return y => y <= 0f || y <= upper;
        }

        if (clampedIndex < totalAreas)
        {
            var lower = boundaries[clampedIndex - 2];
            var upper = boundaries[clampedIndex - 1];
            return y => y <= 0f || (y >= lower && y <= upper);
        }

        var finalLower = boundaries[^1];
        return y => y <= 0f || y >= finalLower;
    }

    private static IReadOnlyList<T> FilterByArea<T>(IReadOnlyList<T> source, Func<float, bool> includeY)
        where T : struct
    {
        if (source.Count == 0)
        {
            return source;
        }

        if (typeof(T) == typeof(SpawnPoint))
        {
            return source
                .Cast<SpawnPoint>()
                .Where(spawn => includeY(spawn.Y))
                .Cast<T>()
                .ToArray();
        }

        if (typeof(T) == typeof(IntelBaseMarker))
        {
            return source
                .Cast<IntelBaseMarker>()
                .Where(marker => includeY(marker.Y))
                .Cast<T>()
                .ToArray();
        }

        if (typeof(T) == typeof(RoomObjectMarker))
        {
            return source
                .Cast<RoomObjectMarker>()
                .Where(marker => includeY(marker.Y))
                .Cast<T>()
                .ToArray();
        }

        return source;
    }
}
