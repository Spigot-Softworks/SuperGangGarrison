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

    public static SimpleLevel CreateScoutPrototypeLevel()
    {
        return CreateImportedLevel("Truefort") ?? CreateFallbackPrototypeLevel("Prototype");
    }

    public static SimpleLevel? CreateImportedLevel(string levelName, int mapAreaIndex = 1)
    {
        var normalizedName = NormalizeLevelName(levelName);
        var catalog = GetAvailableSourceLevels();
        var levelSpec = catalog.FirstOrDefault(entry => NameComparer.Equals(entry.Name, normalizedName));
        if (string.IsNullOrEmpty(levelSpec.Name))
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

        return new SimpleLevel(
            name: importedRoom.Name,
            mode: levelSpec.Mode,
            bounds: bounds,
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
    }

    public static IReadOnlyList<LevelCatalogEntry> GetAvailableSourceLevels()
    {
        if (_cachedCatalog is not null)
        {
            return _cachedCatalog;
        }

        var entries = new List<LevelCatalogEntry>();
        var mapsDirectory = ProjectSourceLocator.FindDirectory(ContentRoot.GetPath("Rooms", "Maps"));
        if (mapsDirectory is not null)
        {
            foreach (var mapFile in Directory.EnumerateFiles(mapsDirectory, "*.xml"))
            {
                var mapName = Path.GetFileNameWithoutExtension(mapFile);
                if (string.IsNullOrWhiteSpace(mapName)
                    || mapName.Equals("_resources.list", StringComparison.OrdinalIgnoreCase)
                    || mapName.Equals("CustomMapRoom", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var mode = DetectMode(mapFile);
                var collisionMaskPath = FindCollisionMaskPath(mapName);
                entries.Add(new LevelCatalogEntry(mapName, mode, mapFile, collisionMaskPath));
            }
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

    private static SimpleLevel CreateFallbackPrototypeLevel(string levelName)
    {
        var bounds = new WorldBounds(2400f, 1400f);
        var spawn = new SpawnPoint(220f, 320f);
        var floorY = MathF.Min(bounds.Height - 40f, spawn.Y + 360f);
        return new SimpleLevel(
            name: levelName,
            mode: GameModeKind.CaptureTheFlag,
            bounds: bounds,
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

        if (metadata.RoomObjects.Any(marker => marker.Type == RoomObjectType.ControlPoint))
        {
            return GameModeKind.ControlPoint;
        }

        if (metadata.IntelBases.Count > 0)
        {
            return GameModeKind.CaptureTheFlag;
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

    private static string? FindCollisionMaskPath(string mapName)
    {
        var collisionDirectory = ContentRoot.GetPath("Sprites", "Collision Maps");
        if (collisionDirectory is null)
        {
            return null;
        }

        var targetFolderName = $"{mapName}S.images";
        var folder = Directory.EnumerateDirectories(collisionDirectory)
            .FirstOrDefault(candidate => NameComparer.Equals(Path.GetFileName(candidate), targetFolderName));
        if (folder is null)
        {
            return null;
        }

        var imagePath = Path.Combine(folder, "image 0.png");
        return File.Exists(imagePath) ? imagePath : null;
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
            || trimmed.StartsWith("gen_", StringComparison.OrdinalIgnoreCase))
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
