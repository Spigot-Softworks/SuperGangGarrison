using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO.Compression;
using System.Text;

namespace OpenGarrison.Core;

public static class CustomMapPngImporter
{
    private const float DefaultCustomMapScale = 6f;
    private const float DefaultMoveBoxPushPowerPerTick = 5f;
    private const float DefaultMovingPlatformWidth = 60f;
    private const float DefaultMovingPlatformHeight = 6f;
    private const float DefaultMovingPlatformTravelTop = 60f;
    private const float DefaultMovingPlatformTravelLeft = 0f;
    private const float DefaultMovingPlatformSpeed = 3f;
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public sealed record Result(GameMakerRoomMetadata Room, IReadOnlyList<LevelSolid> Solids);

    private readonly record struct ImportedEntity(
        string Type,
        float X,
        float Y,
        float XScale,
        float YScale,
        IReadOnlyDictionary<string, string> Properties);

    public static Result? Import(string pngPath)
    {
        if (!TryExtractLevelData(pngPath, out var levelData))
        {
            return null;
        }
        return ImportLevelData(Path.GetFileNameWithoutExtension(pngPath), pngPath, levelData);
    }

    public static Result? ImportLevelData(
        string mapName,
        string backgroundImagePath,
        string levelData,
        IReadOnlyDictionary<string, CustomMapBuilderResource>? packageResources = null)
    {
        if (string.IsNullOrWhiteSpace(mapName)
            || string.IsNullOrWhiteSpace(backgroundImagePath)
            || string.IsNullOrWhiteSpace(levelData))
        {
            return null;
        }

        var walkmaskSection = ExtractSection(levelData, "{WALKMASK}", "{END WALKMASK}");
        var entitiesSection = ExtractSection(levelData, "{ENTITIES}", "{END ENTITIES}");
        if (string.IsNullOrWhiteSpace(walkmaskSection))
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(entitiesSection))
        {
            return null;
        }

        if (!TryDecodeEntities(entitiesSection, out var entities, out var metadata))
        {
            return null;
        }

        var walkmaskScale = CustomMapBuilderDocument.ResolveWalkmaskScale(metadata);
        var visualScale = CustomMapBuilderDocument.ResolveVisualScale(metadata, walkmaskScale);
        var mapScale = ResolveMapScale(walkmaskScale, entities, walkmaskSection);
        if (!TryDecodeWalkmask(walkmaskSection, mapScale, out var solids, out var bounds))
        {
            return null;
        }

        var decodedResources = MergeDecodedResources(
            CustomMapBuilderResourceCodec.DecodeResourcesFromMetadata(metadata),
            packageResources);
        var visuals = DecodeVisualMetadata(metadata, visualScale);
        var useCenterOrigin = CustomMapEntityPlacementAnchor.UsesCenterOrigin(metadata);
        var room = BuildRoomMetadata(
            mapName.Trim(),
            backgroundImagePath,
            bounds,
            entities,
            visuals,
            useCenterOrigin,
            metadata,
            decodedResources);
        return new Result(room, solids);
    }

    private static bool TryExtractLevelData(string pngPath, out string levelData)
    {
        Stream? stream = null;
        if (File.Exists(pngPath))
        {
            stream = File.OpenRead(pngPath);
        }
        else if (OperatingSystem.IsBrowser() && BrowserContentCatalog.TryGetBinaryForPath(pngPath, out var bytes))
        {
            stream = new MemoryStream(bytes, writable: false);
        }

        if (stream is null)
        {
            levelData = string.Empty;
            return false;
        }

        using (stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var signature = reader.ReadBytes(PngSignature.Length);
            if (!signature.SequenceEqual(PngSignature))
            {
                levelData = string.Empty;
                return false;
            }

            var builder = new StringBuilder();
            while (stream.Position + 8 <= stream.Length)
            {
                var lengthBytes = reader.ReadBytes(4);
                if (lengthBytes.Length < 4)
                {
                    break;
                }

                var dataLength = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
                var chunkType = Encoding.ASCII.GetString(reader.ReadBytes(4));
                var chunkData = reader.ReadBytes(Math.Max(0, dataLength));
                _ = reader.ReadUInt32();

                switch (chunkType)
                {
                    case "tEXt":
                        AppendTextChunk(builder, chunkData);
                        break;
                    case "zTXt":
                        AppendCompressedTextChunk(builder, chunkData);
                        break;
                    case "iTXt":
                        AppendInternationalTextChunk(builder, chunkData);
                        break;
                }
            }

            levelData = builder.Length > 0 ? builder.ToString() : string.Empty;
            return levelData.Length > 0;
        }
    }

    private static void AppendTextChunk(StringBuilder builder, byte[] chunkData)
    {
        var separatorIndex = Array.IndexOf(chunkData, (byte)0);
        if (separatorIndex < 0 || separatorIndex >= chunkData.Length - 1)
        {
            return;
        }

        builder.Append(Encoding.Latin1.GetString(chunkData, separatorIndex + 1, chunkData.Length - separatorIndex - 1));
    }

    private static void AppendCompressedTextChunk(StringBuilder builder, byte[] chunkData)
    {
        var separatorIndex = Array.IndexOf(chunkData, (byte)0);
        if (separatorIndex < 0 || separatorIndex >= chunkData.Length - 2)
        {
            return;
        }

        using var compressedStream = new MemoryStream(chunkData, separatorIndex + 2, chunkData.Length - separatorIndex - 2, writable: false);
        using var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress);
        using var reader = new StreamReader(zlibStream, Encoding.Latin1);
        builder.Append(reader.ReadToEnd());
    }

    private static void AppendInternationalTextChunk(StringBuilder builder, byte[] chunkData)
    {
        var index = Array.IndexOf(chunkData, (byte)0);
        if (index < 0 || index + 5 >= chunkData.Length)
        {
            return;
        }

        var compressed = chunkData[index + 1] == 1;
        index += 3;
        while (index < chunkData.Length && chunkData[index] != 0)
        {
            index += 1;
        }

        index += 1;
        while (index < chunkData.Length && chunkData[index] != 0)
        {
            index += 1;
        }

        index += 1;
        if (index >= chunkData.Length)
        {
            return;
        }

        if (compressed)
        {
            using var compressedStream = new MemoryStream(chunkData, index, chunkData.Length - index, writable: false);
            using var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress);
            using var reader = new StreamReader(zlibStream, Encoding.UTF8);
            builder.Append(reader.ReadToEnd());
        }
        else
        {
            builder.Append(Encoding.UTF8.GetString(chunkData, index, chunkData.Length - index));
        }
    }

    private static string ExtractSection(string input, string startMarker, string endMarker)
    {
        var start = input.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return string.Empty;
        }

        start += startMarker.Length;
        var end = input.IndexOf(endMarker, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
        {
            return string.Empty;
        }

        // Walkmask sections use spaces as valid packed bits, so callers must receive the raw section text.
        return input[start..end];
    }

    private static bool TryDecodeWalkmask(string walkmaskSection, float mapScale, out IReadOnlyList<LevelSolid> solids, out WorldBounds bounds)
    {
        solids = Array.Empty<LevelSolid>();
        if (!EmbeddedWalkmaskDecoder.TryDecodeSolidCells(walkmaskSection, out var width, out var height, out var cells))
        {
            bounds = default;
            return false;
        }

        var decodedSolids = new List<LevelSolid>();
        for (var row = 0; row < height; row += 1)
        {
            var column = 0;
            while (column < width)
            {
                if (!cells[(row * width) + column])
                {
                    column += 1;
                    continue;
                }

                var start = column;
                while (column < width && cells[(row * width) + column])
                {
                    column += 1;
                }

                decodedSolids.Add(new LevelSolid(start * mapScale, row * mapScale, (column - start) * mapScale, mapScale));
            }
        }

        bounds = new WorldBounds(width * mapScale, height * mapScale);
        solids = decodedSolids;
        return true;
    }

    private static GameMakerRoomMetadata BuildRoomMetadata(
        string mapName,
        string pngPath,
        WorldBounds bounds,
        IReadOnlyList<ImportedEntity> entities,
        CustomMapVisualMetadata visuals,
        bool useCenterOrigin,
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyDictionary<string, CustomMapBuilderResource> decodedResources)
    {
        var redSpawns = new List<SpawnPoint>();
        var blueSpawns = new List<SpawnPoint>();
        var intelBases = new List<IntelBaseMarker>();
        var roomObjects = new List<RoomObjectMarker>();
        var movingPlatforms = new List<MovingPlatformMarker>();
        var areaTransitionMarkers = new List<AreaTransitionMarker>();
        var unsupportedEntities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var importOrder = entities
            .OrderBy(entity => AreaExtensionMetadata.IsAreaEntityType(entity.Type) ? 1 : 0)
            .ToArray();
        for (var index = 0; index < importOrder.Length; index += 1)
        {
            var entity = importOrder[index];
            var entityType = entity.Type.Trim();
            var normalizedEntityType = NormalizeEntityTypeName(entityType);
            var x = entity.X;
            var y = entity.Y;

            var importContext = new CustomMapEntityImportContext
            {
                RedSpawns = redSpawns,
                BlueSpawns = blueSpawns,
                RoomObjects = roomObjects,
                UseCenterOrigin = useCenterOrigin,
                Resources = decodedResources,
            };
            if (CustomMapEntityRuntimeRegistry.TryImport(
                    entityType,
                    x,
                    y,
                    entity.XScale,
                    entity.YScale,
                    entity.Properties,
                    importContext))
            {
                continue;
            }

            switch (normalizedEntityType)
            {
                case "redintel":
                    intelBases.Add(new IntelBaseMarker(PlayerTeam.Red, x, y));
                    break;
                case "blueintel":
                    intelBases.Add(new IntelBaseMarker(PlayerTeam.Blue, x, y));
                    break;
                case "nextareao":
                    areaTransitionMarkers.Add(new AreaTransitionMarker(x, y, AreaTransitionDirection.Next, entityType));
                    break;
                case "previousareao":
                    areaTransitionMarkers.Add(new AreaTransitionMarker(x, y, AreaTransitionDirection.Previous, entityType));
                    break;
                default:
                    if (TryCreateMovingPlatform(entity, out var movingPlatform))
                    {
                        movingPlatforms.Add(movingPlatform);
                    }
                    else if (CustomMapRoomObjectFactory.TryCreate(
                            entityType,
                            x,
                            y,
                            entity.XScale,
                            entity.YScale,
                            entity.Properties,
                            out var marker,
                            useCenterOrigin))
                    {
                        roomObjects.Add(marker);
                    }
                    else
                    {
                        unsupportedEntities.Add(entityType);
                    }
                    break;
            }
        }

        var totalControlPoints = ForwardSpawnMetadata.CountMapControlPoints(roomObjects);
        ForwardSpawnMetadata.ApplyForwardSpawnControlPointLinks(redSpawns, blueSpawns, totalControlPoints);

        var mapEntities = ToMapImportedEntities(entities);
        var logicGraph = MapLogicGraphImporter.BuildFromEntities(mapEntities, roomObjects);
        var logicActivators = MapLogicActivatorImporter.BuildFromEntities(mapEntities, roomObjects, logicGraph);
        var logicScoreTriggers = MapLogicScoreTriggerImporter.BuildFromEntities(mapEntities, logicGraph);
        var spritesheetPlaybackSet = SpritesheetPlaybackImporter.BuildFromRoomObjects(roomObjects, logicGraph);
        MapLogicRuntimePatch.ApplySpawnLogicSignals(redSpawns, mapEntities, logicGraph);
        MapLogicRuntimePatch.ApplyControlPointLogicLocks(roomObjects, mapEntities, logicGraph);
        MapTeleportRuntimePatch.ApplyExitLinks(roomObjects, mapEntities);
        MapDamageableRuntimePatch.ApplyHealWhenLinks(roomObjects, mapEntities, logicGraph);
        visuals = AttachCustomSpriteResources(visuals, roomObjects, decodedResources);

        return new GameMakerRoomMetadata(
            mapName,
            bounds,
            pngPath,
            redSpawns,
            blueSpawns,
            intelBases,
            roomObjects,
            AreaTransitionMetadata.BuildAreaBoundaries(areaTransitionMarkers))
        {
            AreaTransitionMarkers = areaTransitionMarkers.ToArray(),
            UnsupportedEntities = unsupportedEntities
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            CustomMapVisuals = visuals,
            MovingPlatforms = movingPlatforms.ToArray(),
            ControlPointSettings = new CustomMapControlPointSettings(
                ControlPointMapSettingsMetadata.ParseOverrideInitialCps(metadata)),
            ScrSettings = ScrMapSettingsMetadata.ParseScrSettings(metadata),
            ShowControlPoints = ScrMapSettingsMetadata.ParseShowControlPoints(metadata),
            LogicGraph = logicGraph,
            LogicActivators = logicActivators,
            LogicScoreTriggers = logicScoreTriggers,
            SpritesheetPlaybackSet = spritesheetPlaybackSet,
            ExplicitGameMode = MapGameModeMetadata.TryReadGameMode(metadata),
        };
    }

    private static CustomMapVisualMetadata DecodeVisualMetadata(IReadOnlyDictionary<string, string> metadata, float mapScale)
    {
        var visualResources = new Dictionary<string, CustomMapVisualResource>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in CustomMapBuilderResourceCodec.DecodeResourcesFromMetadata(metadata).Values)
        {
            if (CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out var resourceBytes))
            {
                visualResources[resource.Name] = new CustomMapVisualResource(resource.Name, resourceBytes);
            }
        }

        var parallaxLayers = new List<CustomMapParallaxLayer>();
        for (var index = 0; index < 7; index += 1)
        {
            var key = $"bg_layer{index}";
            if (!visualResources.TryGetValue(key, out var resource)
                && (!metadata.TryGetValue(key, out var resourceName)
                    || !visualResources.TryGetValue(resourceName.Trim(), out resource)))
            {
                continue;
            }

            parallaxLayers.Add(new CustomMapParallaxLayer(
                index,
                ResolveParallaxFactor(metadata, $"layer{index}xfactor", 10f - index),
                ResolveParallaxFactor(metadata, $"layer{index}yfactor", 10f - index),
                resource));
        }

        CustomMapVisualResource? foreground = null;
        if (visualResources.TryGetValue("bg_foreground", out var foregroundResource))
        {
            foreground = foregroundResource;
        }

        if (parallaxLayers.Count == 0
            && foreground is null
            && visualResources.Count == 0
            && !metadata.TryGetValue("background", out _)
            && !metadata.TryGetValue("void", out _))
        {
            return CustomMapVisualMetadata.Empty;
        }

        return new CustomMapVisualMetadata(
            ImageScale: mapScale,
            BackgroundColor: metadata.TryGetValue("background", out var backgroundColor) ? backgroundColor : null,
            VoidColor: metadata.TryGetValue("void", out var voidColor) ? voidColor : null,
            ParallaxLayers: parallaxLayers,
            Foreground: foreground,
            Resources: visualResources,
            SpriteResources: EmptySpriteResources,
            ForegroundSpriteJungleHitMasks: CustomMapVisualMetadata.Empty.ForegroundSpriteJungleHitMasks);
    }

    private static readonly IReadOnlyDictionary<string, CustomMapVisualResource> EmptySpriteResources =
        new Dictionary<string, CustomMapVisualResource>(StringComparer.OrdinalIgnoreCase);

    private static CustomMapVisualMetadata AttachCustomSpriteResources(
        CustomMapVisualMetadata visuals,
        IReadOnlyList<RoomObjectMarker> roomObjects,
        IReadOnlyDictionary<string, CustomMapBuilderResource> decodedResources)
    {
        var spriteResources = new Dictionary<string, CustomMapVisualResource>(StringComparer.OrdinalIgnoreCase);
        var jungleHitMasks = new Dictionary<int, ForegroundSpriteHitMask>();
        for (var index = 0; index < roomObjects.Count; index += 1)
        {
            var marker = roomObjects[index];
            if (marker.Type == RoomObjectType.CustomMapSprite)
            {
                TryAttachSpriteResource(marker.CustomMapSprite.ImageResourceName, spriteResources, decodedResources);
                continue;
            }

            if (marker.Type == RoomObjectType.Spritesheet)
            {
                TryAttachSpriteResource(marker.Spritesheet.ImageResourceName, spriteResources, decodedResources);
                continue;
            }

            if (marker.Type != RoomObjectType.ForegroundSprite)
            {
                continue;
            }

            var resourceName = marker.ForegroundSprite.ImageResourceName;
            if (!TryAttachSpriteResource(resourceName, spriteResources, decodedResources, out var bytes))
            {
                continue;
            }

            if (!marker.ForegroundSprite.Jungle
                || marker.ForegroundSprite.Boundary != ForegroundSpriteBoundaryKind.Pixel
                || !ForegroundSpriteMetadata.TryBuildHitMask(bytes, out var hitMask)
                || hitMask is null)
            {
                continue;
            }

            jungleHitMasks[index] = hitMask;
        }

        if (spriteResources.Count == 0 && jungleHitMasks.Count == 0)
        {
            return visuals;
        }

        return visuals with
        {
            SpriteResources = spriteResources.Count == 0 ? visuals.SpriteResources : spriteResources,
            ForegroundSpriteJungleHitMasks = jungleHitMasks.Count == 0
                ? visuals.ForegroundSpriteJungleHitMasks
                : jungleHitMasks,
        };
    }

    private static IReadOnlyDictionary<string, CustomMapBuilderResource> MergeDecodedResources(
        IReadOnlyDictionary<string, CustomMapBuilderResource> decodedFromMetadata,
        IReadOnlyDictionary<string, CustomMapBuilderResource>? packageResources)
    {
        if (packageResources is null || packageResources.Count == 0)
        {
            return decodedFromMetadata;
        }

        var merged = new Dictionary<string, CustomMapBuilderResource>(
            decodedFromMetadata,
            StringComparer.OrdinalIgnoreCase);
        foreach (var resource in packageResources.Values)
        {
            merged[resource.Name] = resource;
        }

        return new ReadOnlyDictionary<string, CustomMapBuilderResource>(merged);
    }

    private static bool TryAttachSpriteResource(
        string resourceName,
        IDictionary<string, CustomMapVisualResource> spriteResources,
        IReadOnlyDictionary<string, CustomMapBuilderResource> decodedResources)
    {
        return TryAttachSpriteResource(resourceName, spriteResources, decodedResources, out _);
    }

    private static bool TryAttachSpriteResource(
        string resourceName,
        IDictionary<string, CustomMapVisualResource> spriteResources,
        IReadOnlyDictionary<string, CustomMapBuilderResource> decodedResources,
        out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(resourceName)
            || spriteResources.ContainsKey(resourceName)
            || !decodedResources.TryGetValue(resourceName, out var resource)
            || !CustomMapBuilderResourceCodec.TryGetResourceBytes(resource, out bytes))
        {
            return false;
        }

        spriteResources[resourceName] = new CustomMapVisualResource(resourceName, bytes);
        return true;
    }

    private static float ResolveParallaxFactor(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        float fallback)
    {
        return metadata.TryGetValue(key, out var rawValue)
            && float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool TryCreateMovingPlatform(ImportedEntity entity, out MovingPlatformMarker marker)
    {
        marker = default;
        var entityType = entity.Type;
        if (!NormalizeEntityTypeName(entityType).Equals("movingplatform", StringComparison.Ordinal))
        {
            return false;
        }

        var platformScale = ResolvePositiveFloat(entity.Properties, "scale", 1f);
        var travelX = ResolveFloat(entity.Properties, "left", DefaultMovingPlatformTravelLeft);
        var travelY = -ResolveFloat(entity.Properties, "top", DefaultMovingPlatformTravelTop);
        var upSpeed = ResolvePositiveFloat(entity.Properties, "upspeed", DefaultMovingPlatformSpeed);
        var downSpeed = ResolvePositiveFloat(entity.Properties, "downspeed", DefaultMovingPlatformSpeed);
        var triggerMode = (int)Math.Clamp(ResolveFloat(entity.Properties, "trigger", 0f), 0f, 2f);
        var resetMovementState = ResolveResetMoveStatus(entity.Properties) > 0.5f;
        var resourceName = entity.Properties.TryGetValue("resource", out var rawResource)
            ? rawResource.Trim()
            : string.Empty;

        marker = new MovingPlatformMarker(
            entity.X,
            entity.Y,
            DefaultMovingPlatformWidth * platformScale,
            DefaultMovingPlatformHeight * platformScale,
            travelX,
            travelY,
            upSpeed,
            downSpeed,
            triggerMode,
            resetMovementState,
            resourceName,
            entityType);
        return true;
    }

    private static bool TryDecodeEntities(
        string entitiesSection,
        out IReadOnlyList<ImportedEntity> entities,
        out IReadOnlyDictionary<string, string> metadata)
    {
        var trimmed = entitiesSection.Trim();
        if (!string.IsNullOrEmpty(trimmed) && (trimmed[0] == '{' || trimmed[0] == '['))
        {
            return TryDecodeGgonEntities(trimmed, out entities, out metadata);
        }

        return TryDecodeLegacyEntities(entitiesSection, out entities, out metadata);
    }

    private static bool TryDecodeLegacyEntities(
        string entitiesSection,
        out IReadOnlyList<ImportedEntity> entities,
        out IReadOnlyDictionary<string, string> metadata)
    {
        metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var decodedEntities = new List<ImportedEntity>();
        var lines = entitiesSection
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var index = 0; index + 2 < lines.Length; index += 3)
        {
            var entityType = lines[index].Trim();
            if (!float.TryParse(lines[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(lines[index + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                continue;
            }

            decodedEntities.Add(new ImportedEntity(
                entityType,
                x,
                y,
                1f,
                1f,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
        }

        entities = decodedEntities;
        return true;
    }

    private static bool TryDecodeGgonEntities(
        string entitiesSection,
        out IReadOnlyList<ImportedEntity> entities,
        out IReadOnlyDictionary<string, string> metadata)
    {
        entities = Array.Empty<ImportedEntity>();
        metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!GgonParser.TryParse(entitiesSection, out var root))
        {
            return false;
        }

        if (!TryEnumerateList(root, out var items))
        {
            return false;
        }

        var decodedEntities = new List<ImportedEntity>(items.Count);
        var decodedMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < items.Count; index += 1)
        {
            if (items[index] is not GgonValue.Map entityMap)
            {
                continue;
            }

            if (!TryGetString(entityMap, "type", out var entityType))
            {
                CopyUntypedMetadataMap(entityMap, decodedMetadata);
                continue;
            }

            if (entityType.Equals("meta", StringComparison.OrdinalIgnoreCase))
            {
                CopyScalarMetadata(entityMap, decodedMetadata);
                continue;
            }

            if (!TryGetFloat(entityMap, "x", out var x) || !TryGetFloat(entityMap, "y", out var y))
            {
                continue;
            }

            var xScale = TryGetFloat(entityMap, "xscale", out var parsedXScale) ? parsedXScale : 1f;
            var yScale = TryGetFloat(entityMap, "yscale", out var parsedYScale) ? parsedYScale : 1f;
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entityMap.Entries)
            {
                if (entry.Value is GgonValue.Scalar scalar)
                {
                    properties[entry.Key] = scalar.Value;
                }
            }

            decodedEntities.Add(new ImportedEntity(entityType, x, y, xScale, yScale, properties));
        }

        entities = decodedEntities;
        metadata = decodedMetadata;
        return true;
    }

    private static void CopyUntypedMetadataMap(
        GgonValue.Map entityMap,
        IDictionary<string, string> metadata)
    {
        if (!entityMap.Entries.Keys.Any(IsVisualMetadataKey))
        {
            return;
        }

        CopyScalarMetadata(entityMap, metadata);
    }

    private static void CopyScalarMetadata(
        GgonValue.Map entityMap,
        IDictionary<string, string> metadata)
    {
        foreach (var entry in entityMap.Entries)
        {
            if (entry.Value is GgonValue.Scalar scalar)
            {
                metadata[entry.Key] = scalar.Value;
            }
        }
    }

    private static bool IsVisualMetadataKey(string key)
    {
        return key.Equals("background", StringComparison.OrdinalIgnoreCase)
            || key.Equals("void", StringComparison.OrdinalIgnoreCase)
            || key.Equals("scale", StringComparison.OrdinalIgnoreCase)
            || key.Equals("walkmaskScale", StringComparison.OrdinalIgnoreCase)
            || key.Equals("visualScale", StringComparison.OrdinalIgnoreCase)
            || key.Equals("bg_foreground", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("bg_layer", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("xfactor", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("yfactor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryEnumerateList(GgonValue value, out IReadOnlyList<GgonValue> items)
    {
        if (value is GgonValue.List list)
        {
            items = list.Items;
            return true;
        }

        if (value is GgonValue.Map map
            && TryGetString(map, "length", out var lengthText)
            && int.TryParse(lengthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length)
            && length >= 0)
        {
            var values = new List<GgonValue>(length);
            for (var index = 0; index < length; index += 1)
            {
                if (!map.Entries.TryGetValue(index.ToString(CultureInfo.InvariantCulture), out var item))
                {
                    items = Array.Empty<GgonValue>();
                    return false;
                }

                values.Add(item);
            }

            items = values;
            return true;
        }

        items = Array.Empty<GgonValue>();
        return false;
    }

    private static float ResolveMetadataScale(IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.TryGetValue("scale", out var scaleText)
            && float.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
            && scale > 0f)
        {
            return scale;
        }

        return DefaultCustomMapScale;
    }

    private static float ResolveMapScale(
        float resolvedScale,
        IReadOnlyList<ImportedEntity> entities,
        string walkmaskSection)
    {
        return ShouldUseDefaultScaleForGg2Compatibility(entities, walkmaskSection, resolvedScale)
            ? DefaultCustomMapScale
            : resolvedScale;
    }

    private static bool ShouldUseDefaultScaleForGg2Compatibility(
        IReadOnlyList<ImportedEntity> entities,
        string walkmaskSection,
        float resolvedScale)
    {
        if (resolvedScale >= DefaultCustomMapScale
            || !EmbeddedWalkmaskDecoder.TryGetDimensions(walkmaskSection, out var walkmaskWidth, out var walkmaskHeight)
            || entities.Count == 0)
        {
            return false;
        }

        var scaledWidth = walkmaskWidth * resolvedScale;
        var scaledHeight = walkmaskHeight * resolvedScale;
        var defaultWidth = walkmaskWidth * DefaultCustomMapScale;
        var defaultHeight = walkmaskHeight * DefaultCustomMapScale;
        var hasEntityOutsideScaledBounds = false;
        for (var index = 0; index < entities.Count; index += 1)
        {
            var entity = entities[index];
            if (entity.X < 0f || entity.Y < 0f)
            {
                continue;
            }

            if (entity.X > scaledWidth || entity.Y > scaledHeight)
            {
                hasEntityOutsideScaledBounds = true;
            }

            if (entity.X > defaultWidth || entity.Y > defaultHeight)
            {
                return false;
            }
        }

        return hasEntityOutsideScaledBounds;
    }

    private static float ResolveResetMoveStatus(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("resetMoveStatus", out var rawValue)
            && float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue))
        {
            return parsedValue != 0f ? 1f : 0f;
        }

        return 1f;
    }

    private static float ResolveFloat(IReadOnlyDictionary<string, string> properties, string key, float fallback)
    {
        return properties.TryGetValue(key, out var rawValue)
            && float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue)
            && float.IsFinite(parsedValue)
            ? parsedValue
            : fallback;
    }

    private static float ResolvePositiveFloat(IReadOnlyDictionary<string, string> properties, string key, float fallback)
    {
        var value = ResolveFloat(properties, key, fallback);
        return value > 0f ? value : fallback;
    }

    private static float ResolveMoveBoxImpulse(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("speed", out var rawValue)
            && float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue)
            && parsedValue > 0f)
        {
            return parsedValue * LegacyMovementModel.SourceTicksPerSecond;
        }

        return DefaultMoveBoxPushPowerPerTick * LegacyMovementModel.SourceTicksPerSecond;
    }

    private static bool TryGetString(GgonValue.Map map, string key, out string value)
    {
        if (map.Entries.TryGetValue(key, out var rawValue) && rawValue is GgonValue.Scalar scalar)
        {
            value = scalar.Value;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetFloat(GgonValue.Map map, string key, out float value)
    {
        if (TryGetString(map, key, out var text)
            && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0f;
        return false;
    }

    private static string NormalizeEntityTypeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static MapImportedEntity[] ToMapImportedEntities(IReadOnlyList<ImportedEntity> entities)
    {
        var mapped = new MapImportedEntity[entities.Count];
        for (var index = 0; index < entities.Count; index += 1)
        {
            var entity = entities[index];
            mapped[index] = new MapImportedEntity(
                entity.Type,
                entity.X,
                entity.Y,
                entity.Properties);
        }

        return mapped;
    }
}
