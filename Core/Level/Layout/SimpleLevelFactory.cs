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
        string? CollisionMaskSourcePath,
        CustomMapSourceKind SourceKind,
        bool IsCustomMap);

    public static SimpleLevel CreateScoutPrototypeLevel(float mapScale = 1f)
    {
        return CreateImportedLevel("Truefort", mapScale: mapScale) ?? CreateFallbackPrototypeLevel("Prototype", mapScale);
    }

    public static SimpleLevel? CreateImportedLevel(string levelName, int mapAreaIndex = 1, float mapScale = 1f)
    {
        var catalog = GetAvailableSourceLevels();
        if (!TryFindCatalogEntry(catalog, levelName, out var levelSpec))
        {
            ClearCachedCatalog();
            catalog = GetAvailableSourceLevels();
            if (!TryFindCatalogEntry(catalog, levelName, out levelSpec))
            {
                return null;
            }
        }

        var usesCustomMapRuntimeFormat = levelSpec.SourceKind is CustomMapSourceKind.LegacyPng or CustomMapSourceKind.Package;
        GameMakerRoomMetadata? importedRoom;
        IReadOnlyList<LevelSolid> importedSolids;
        if (levelSpec.SourceKind == CustomMapSourceKind.Package)
        {
            var customMap = CustomMapPackageImporter.Import(levelSpec.RoomSourcePath);
            if (customMap is null)
            {
                return null;
            }

            importedRoom = customMap.Room;
            importedSolids = customMap.Solids;
        }
        else if (levelSpec.SourceKind == CustomMapSourceKind.LegacyPng)
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
        if (usesCustomMapRuntimeFormat && (redSpawns.Count == 0 || blueSpawns.Count == 0))
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
        if (usesCustomMapRuntimeFormat && importedSolids.Count == 0)
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
            unsupportedSourceEntities: importedRoom.UnsupportedEntities,
            customMapVisuals: importedRoom.CustomMapVisuals,
            controlPointSettings: importedRoom.ControlPointSettings,
            scrSettings: importedRoom.ScrSettings,
            showControlPoints: importedRoom.ShowControlPoints,
            logicGraph: importedRoom.LogicGraph,
            logicActivators: importedRoom.LogicActivators,
            logicScoreTriggers: importedRoom.LogicScoreTriggers,
            spritesheetPlaybackSet: importedRoom.SpritesheetPlaybackSet);
        return SimpleLevelScaling.ApplyUniformScale(level, mapScale);
    }

    public static IReadOnlyList<LevelCatalogEntry> GetAvailableSourceLevels()
    {
        if (_cachedCatalog is not null)
        {
            return _cachedCatalog;
        }

        var entries = new List<LevelCatalogEntry>();
        foreach (var definition in OpenGarrisonStockMapCatalog.SourceDefinitions)
        {
            var stockMapSource = FindStockMapSource(definition);
            if (stockMapSource.RoomSourcePath is null)
            {
                continue;
            }

            entries.Add(new LevelCatalogEntry(
                definition.LevelName,
                definition.Mode,
                stockMapSource.RoomSourcePath,
                stockMapSource.CollisionMaskSourcePath,
                ResolveSourceKind(stockMapSource.RoomSourcePath),
                IsCustomMap: false));
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
        if (metadata.ExplicitGameMode is GameModeKind explicitMode)
        {
            return explicitMode;
        }

        if (metadata.LogicScoreTriggers.HasTriggers)
        {
            return GameModeKind.Scr;
        }

        if (metadata.Name.StartsWith("scr_", StringComparison.OrdinalIgnoreCase))
        {
            return GameModeKind.Scr;
        }

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
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        var discoveredCustomMapNames = new HashSet<string>(NameComparer);
        foreach (var customMapsDirectory in RuntimePaths.MapSearchDirectories)
        {
            if (!Directory.Exists(customMapsDirectory))
            {
                continue;
            }

            foreach (var mapFile in Directory
                .EnumerateFiles(customMapsDirectory, "*.png", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                var mapName = Path.GetFileNameWithoutExtension(mapFile);
                if (string.IsNullOrWhiteSpace(mapName)
                    || !discoveredCustomMapNames.Add(mapName))
                {
                    continue;
                }

                var imported = CustomMapPngImporter.Import(mapFile);
                if (imported is null)
                {
                    continue;
                }

                entries.Add(new LevelCatalogEntry(
                    mapName,
                    DetectMode(imported.Room),
                    mapFile,
                    null,
                    CustomMapSourceKind.LegacyPng,
                    IsCustomMap: true));
            }
        }

        foreach (var customMapsDirectory in RuntimePaths.MapSearchDirectories)
        {
            if (!Directory.Exists(customMapsDirectory))
            {
                continue;
            }

            foreach (var packageDirectory in Directory
                .EnumerateDirectories(customMapsDirectory, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (!CustomMapPackageImporter.TryFindManifestInDirectory(packageDirectory, out var manifestPath))
                {
                    continue;
                }

                var mapName = Path.GetFileNameWithoutExtension(manifestPath);
                if (string.IsNullOrWhiteSpace(mapName)
                    || !discoveredCustomMapNames.Add(mapName))
                {
                    continue;
                }

                var imported = CustomMapPackageImporter.Import(manifestPath);
                if (imported is null)
                {
                    continue;
                }

                entries.Add(new LevelCatalogEntry(
                    mapName,
                    DetectMode(imported.Room),
                    manifestPath,
                    null,
                    CustomMapSourceKind.Package,
                    IsCustomMap: true));
            }
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

    private static (string? RoomSourcePath, string? CollisionMaskSourcePath) FindStockMapSource(OpenGarrisonStockMapDefinition definition)
    {
        var packageManifestPath = FindStockMapPackageSourcePath(definition);
        if (!string.IsNullOrWhiteSpace(packageManifestPath))
        {
            return (packageManifestPath, null);
        }

        var roomFileName = $"{definition.LevelName}.xml";
        var collisionSpriteName = $"{definition.LevelName}S.images";
        var runtimeRoomPath = ContentRoot.GetPath("Rooms", "Maps", roomFileName);
        var runtimeCollisionPath = ContentRoot.GetPath("Sprites", "Collision Maps", collisionSpriteName, "image 0.png");
        if ((File.Exists(runtimeRoomPath) || BrowserContentCatalog.TryGetBinaryForPath(runtimeRoomPath, out _))
            && (File.Exists(runtimeCollisionPath) || BrowserContentCatalog.TryGetBinaryForPath(runtimeCollisionPath, out _)))
        {
            return (runtimeRoomPath, runtimeCollisionPath);
        }

        var projectRoomPath = ProjectSourceLocator.FindFile(Path.Combine("Core", "Content", "Rooms", "Maps", roomFileName));
        var projectCollisionPath = ProjectSourceLocator.FindFile(Path.Combine("Core", "Content", "Sprites", "Collision Maps", collisionSpriteName, "image 0.png"));
        if (!string.IsNullOrWhiteSpace(projectRoomPath)
            && !string.IsNullOrWhiteSpace(projectCollisionPath)
            && File.Exists(projectRoomPath)
            && File.Exists(projectCollisionPath))
        {
            return (projectRoomPath, projectCollisionPath);
        }

        var sourceRoomPath = ProjectSourceLocator.FindFile(Path.Combine(ContentRoot.Path, "Rooms", "Maps", roomFileName));
        var sourceCollisionPath = ProjectSourceLocator.FindFile(Path.Combine(ContentRoot.Path, "Sprites", "Collision Maps", collisionSpriteName, "image 0.png"));
        if (!string.IsNullOrWhiteSpace(sourceRoomPath)
            && !string.IsNullOrWhiteSpace(sourceCollisionPath)
            && File.Exists(sourceRoomPath)
            && File.Exists(sourceCollisionPath))
        {
            return (sourceRoomPath, sourceCollisionPath);
        }

        return (FindStockMapPngSourcePath(definition), null);
    }

    private static CustomMapSourceKind ResolveSourceKind(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return CustomMapSourceKind.Package;
        }

        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return CustomMapSourceKind.LegacyPng;
        }

        return CustomMapSourceKind.StockRoom;
    }

    private static string? FindStockMapPackageSourcePath(OpenGarrisonStockMapDefinition definition)
    {
        foreach (var relativePath in EnumerateStockMapPackageManifestRelativePaths(definition))
        {
            var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var runtimePath = ContentRoot.GetPath(parts);
            if (IsReadablePackageManifest(runtimePath))
            {
                return runtimePath;
            }

            var projectContentPath = ProjectSourceLocator.FindFile(Path.Combine("Core", "Content", relativePath));
            if (!string.IsNullOrWhiteSpace(projectContentPath)
                && IsReadablePackageManifest(projectContentPath))
            {
                return projectContentPath;
            }

            var sourceContentPath = ProjectSourceLocator.FindFile(Path.Combine(ContentRoot.Path, relativePath));
            if (!string.IsNullOrWhiteSpace(sourceContentPath)
                && IsReadablePackageManifest(sourceContentPath))
            {
                return sourceContentPath;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateStockMapPackageManifestRelativePaths(OpenGarrisonStockMapDefinition definition)
    {
        yield return Path.Combine("StockMaps", definition.LevelName, $"{definition.LevelName}.json");
        yield return Path.Combine("StockMaps", definition.IniKey, $"{definition.IniKey}.json");
    }

    private static bool IsReadablePackageManifest(string path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && CustomMapPackageImporter.TryReadManifest(path, out _, out _);
    }

    private static string? FindStockMapPngSourcePath(OpenGarrisonStockMapDefinition definition)
    {
        var fileName = $"{definition.IniKey}.png";
        var runtimePath = ContentRoot.GetPath("StockMaps", fileName);
        if (OperatingSystem.IsBrowser())
        {
            return runtimePath;
        }

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
