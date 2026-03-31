using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace OpenGarrison.Core;

public static class CustomMapPngImporter
{
    private const float DefaultCustomMapScale = 6f;
    private const float DefaultMoveBoxPushPowerPerTick = 5f;
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
        if (!File.Exists(pngPath))
        {
            return null;
        }

        var levelData = ExtractLevelData(pngPath);
        if (string.IsNullOrWhiteSpace(levelData))
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

        var mapScale = ResolveMapScale(metadata);
        if (!TryDecodeWalkmask(walkmaskSection, mapScale, out var solids, out var bounds))
        {
            return null;
        }

        var room = BuildRoomMetadata(Path.GetFileNameWithoutExtension(pngPath), pngPath, bounds, entities);
        return new Result(room, solids);
    }

    private static string ExtractLevelData(string pngPath)
    {
        using var stream = File.OpenRead(pngPath);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var signature = reader.ReadBytes(PngSignature.Length);
        if (!signature.SequenceEqual(PngSignature))
        {
            return string.Empty;
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

        if (builder.Length > 0)
        {
            return builder.ToString();
        }

        return string.Empty;
    }

    private static void AppendTextChunk(StringBuilder builder, byte[] chunkData)
    {
        var separatorIndex = Array.IndexOf(chunkData, (byte)0);
        if (separatorIndex < 0 || separatorIndex >= chunkData.Length - 1)
        {
            return;
        }

        builder.Append(Encoding.UTF8.GetString(chunkData, separatorIndex + 1, chunkData.Length - separatorIndex - 1));
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
        using var reader = new StreamReader(zlibStream, Encoding.UTF8);
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

        return input[start..end].Trim();
    }

    private static bool TryDecodeWalkmask(string walkmaskSection, float mapScale, out IReadOnlyList<LevelSolid> solids, out WorldBounds bounds)
    {
        solids = Array.Empty<LevelSolid>();
        var lines = walkmaskSection
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 3
            || !int.TryParse(lines[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
            || !int.TryParse(lines[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
            || width <= 0
            || height <= 0)
        {
            bounds = default;
            return false;
        }

        var cells = new bool[width * height];
        var packed = string.Concat(lines.Skip(2));
        if (packed.Length == 0)
        {
            bounds = default;
            return false;
        }

        var trailingUnusedBits = Math.Max(0, (packed.Length * 6) - (width * height));
        var packedIndex = packed.Length - 1 - (trailingUnusedBits / 6);
        if (packedIndex < 0)
        {
            bounds = default;
            return false;
        }

        var bitMask = 1 << (trailingUnusedBits % 6);
        var packedValue = Math.Max(0, packed[packedIndex] - 32);
        for (var row = height - 1; row >= 0; row -= 1)
        {
            for (var column = width - 1; column >= 0; column -= 1)
            {
                if (bitMask == 64)
                {
                    packedIndex -= 1;
                    if (packedIndex < 0)
                    {
                        bounds = default;
                        return false;
                    }

                    packedValue = Math.Max(0, packed[packedIndex] - 32);
                    bitMask = 1;
                }

                if ((packedValue & bitMask) != 0)
                {
                    cells[(row * width) + column] = true;
                }

                bitMask <<= 1;
            }
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
        IReadOnlyList<ImportedEntity> entities)
    {
        var redSpawns = new List<SpawnPoint>();
        var blueSpawns = new List<SpawnPoint>();
        var intelBases = new List<IntelBaseMarker>();
        var roomObjects = new List<RoomObjectMarker>();
        var areaTransitionMarkers = new List<AreaTransitionMarker>();
        var unsupportedEntities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < entities.Count; index += 1)
        {
            var entity = entities[index];
            var entityType = entity.Type.Trim();
            var x = entity.X;
            var y = entity.Y;

            switch (entityType.ToLowerInvariant())
            {
                case "redspawn":
                    redSpawns.Add(new SpawnPoint(x, y));
                    break;
                case "bluespawn":
                    blueSpawns.Add(new SpawnPoint(x, y));
                    break;
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
                    if (TryCreateRoomObject(entity, out var marker))
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
        };
    }

    private static bool TryCreateRoomObject(ImportedEntity entity, out RoomObjectMarker marker)
    {
        var entityType = entity.Type;
        var x = entity.X;
        var y = entity.Y;
        var xScale = MathF.Abs(entity.XScale) <= 0f ? 1f : MathF.Abs(entity.XScale);
        var yScale = MathF.Abs(entity.YScale) <= 0f ? 1f : MathF.Abs(entity.YScale);
        var resetMoveStatus = ResolveResetMoveStatus(entity.Properties);
        var moveBoxImpulse = ResolveMoveBoxImpulse(entity.Properties);

        marker = entityType.ToLowerInvariant() switch
        {
            "spawnroom" => CreateMarker(RoomObjectType.SpawnRoom, x, y, 42f, 42f, "sprite64", xScale, yScale, sourceName: entityType),
            "cabinets" or "healingcabinet" or "medcabinet" => CreateMarker(RoomObjectType.HealingCabinet, x, y, 32f, 48f, "sprite74", xScale, yScale, sourceName: entityType),
            "killbox" or "pitfall" => CreateMarker(RoomObjectType.KillBox, x, y, 42f, 42f, "sprite64", xScale, yScale, sourceName: entityType),
            "fragbox" => CreateMarker(RoomObjectType.FragBox, x, y, 42f, 42f, "sprite64", xScale, yScale, sourceName: entityType),
            "redteamgate" => CreateMarker(RoomObjectType.TeamGate, x, y, 6f, 60f, "sprite45", xScale, yScale, PlayerTeam.Red, entityType),
            "blueteamgate" => CreateMarker(RoomObjectType.TeamGate, x, y, 6f, 60f, "sprite45", xScale, yScale, PlayerTeam.Blue, entityType),
            "redteamgate2" => CreateMarker(RoomObjectType.TeamGate, x, y, 60f, 6f, "sprite44", xScale, yScale, PlayerTeam.Red, entityType),
            "blueteamgate2" => CreateMarker(RoomObjectType.TeamGate, x, y, 60f, 6f, "sprite44", xScale, yScale, PlayerTeam.Blue, entityType),
            "redintelgate" => CreateMarker(RoomObjectType.IntelGate, x, y, 6f, 60f, "sprite45", xScale, yScale, PlayerTeam.Red, entityType),
            "blueintelgate" => CreateMarker(RoomObjectType.IntelGate, x, y, 6f, 60f, "sprite45", xScale, yScale, PlayerTeam.Blue, entityType),
            "redintelgate2" => CreateMarker(RoomObjectType.IntelGate, x, y, 60f, 6f, "sprite44", xScale, yScale, PlayerTeam.Red, entityType),
            "blueintelgate2" => CreateMarker(RoomObjectType.IntelGate, x, y, 60f, 6f, "sprite44", xScale, yScale, PlayerTeam.Blue, entityType),
            "intelgatevertical" => CreateMarker(RoomObjectType.IntelGate, x, y, 6f, 60f, "sprite45", xScale, yScale, sourceName: entityType),
            "intelgatehorizontal" => CreateMarker(RoomObjectType.IntelGate, x, y, 60f, 6f, "sprite44", xScale, yScale, sourceName: entityType),
            "playerwall" => CreateMarker(RoomObjectType.PlayerWall, x, y, 6f, 60f, "sprite45", xScale, yScale, sourceName: entityType),
            "playerwallhorizontal" or "playerwall_horizontal" => CreateMarker(RoomObjectType.PlayerWall, x, y, 60f, 6f, "sprite44", xScale, yScale, sourceName: entityType),
            "leftdoor" => CreateMarker(RoomObjectType.PlayerWall, x, y, 6f, 60f, "sprite45", xScale, yScale, sourceName: entityType),
            "rightdoor" => CreateMarker(RoomObjectType.PlayerWall, x, y, 6f, 60f, "sprite45", xScale, yScale, sourceName: entityType),
            "dropdownplatform" => CreateMarker(RoomObjectType.DropdownPlatform, x, y, 60f, 6f, "sprite44", xScale, yScale, sourceName: entityType, value: resetMoveStatus),
            "bulletwall" => CreateMarker(RoomObjectType.BulletWall, x, y, 6f, 60f, "sprite45", xScale, yScale, sourceName: entityType),
            "bulletwallhorizontal" or "bulletwall_horizontal" => CreateMarker(RoomObjectType.BulletWall, x, y, 60f, 6f, "sprite44", xScale, yScale, sourceName: entityType),
            "moveboxup" => CreateMarker(RoomObjectType.MoveBoxUp, x, y, 42f, 42f, "sprite64", xScale, yScale, sourceName: entityType, value: moveBoxImpulse),
            "moveboxdown" => CreateMarker(RoomObjectType.MoveBoxDown, x, y, 42f, 42f, "sprite64", xScale, yScale, sourceName: entityType, value: moveBoxImpulse),
            "moveboxleft" => CreateMarker(RoomObjectType.MoveBoxLeft, x, y, 42f, 42f, "sprite64", xScale, yScale, sourceName: entityType, value: moveBoxImpulse),
            "moveboxright" => CreateMarker(RoomObjectType.MoveBoxRight, x, y, 42f, 42f, "sprite64", xScale, yScale, sourceName: entityType, value: moveBoxImpulse),
            "controlpoint" or "controlpoint1" or "controlpoint2" or "controlpoint3" or "controlpoint4" or "controlpoint5"
                => CreateMarker(RoomObjectType.ControlPoint, x, y, 42f, 42f, "ControlPointNeutralS", xScale, yScale, sourceName: entityType),
            "kothcontrolpoint" => CreateMarker(RoomObjectType.ControlPoint, x, y, 42f, 42f, "ControlPointNeutralS", xScale, yScale, sourceName: entityType),
            "kothredcontrolpoint" => CreateMarker(RoomObjectType.ControlPoint, x, y, 42f, 42f, "ControlPointRedS", xScale, yScale, sourceName: entityType),
            "kothbluecontrolpoint" => CreateMarker(RoomObjectType.ControlPoint, x, y, 42f, 42f, "ControlPointBlueS", xScale, yScale, sourceName: entityType),
            "capturepoint" => CreateMarker(RoomObjectType.CaptureZone, x, y, 42f, 42f, string.Empty, xScale, yScale, sourceName: entityType),
            "setupgate" => CreateMarker(RoomObjectType.ControlPointSetupGate, x, y, 60f, 6f, "sprite44", xScale, yScale, sourceName: entityType),
            "arenacontrolpoint" => CreateMarker(RoomObjectType.ArenaControlPoint, x, y, 42f, 42f, "ControlPointNeutralS", xScale, yScale, sourceName: entityType),
            "generatorred" => CreateMarker(RoomObjectType.Generator, x, y, 40f, 40f, "GeneratorS", xScale, yScale, PlayerTeam.Red, entityType),
            "generatorblue" => CreateMarker(RoomObjectType.Generator, x, y, 40f, 40f, "GeneratorS", xScale, yScale, PlayerTeam.Blue, entityType),
            _ => default,
        };

        return marker.Type != default || entityType.Equals("spawnroom", StringComparison.OrdinalIgnoreCase);
    }

    private static RoomObjectMarker CreateMarker(
        RoomObjectType type,
        float x,
        float y,
        float width,
        float height,
        string spriteName,
        float xScale,
        float yScale,
        PlayerTeam? team = null,
        string sourceName = "",
        float value = 0f)
    {
        return new RoomObjectMarker(type, x, y, width * xScale, height * yScale, spriteName, team, sourceName, value);
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
                continue;
            }

            if (entityType.Equals("meta", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var entry in entityMap.Entries)
                {
                    if (entry.Value is GgonValue.Scalar scalar)
                    {
                        decodedMetadata[entry.Key] = scalar.Value;
                    }
                }

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

    private static float ResolveMapScale(IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.TryGetValue("scale", out var scaleText)
            && float.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
            && scale > 0f)
        {
            return scale;
        }

        return DefaultCustomMapScale;
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
}
