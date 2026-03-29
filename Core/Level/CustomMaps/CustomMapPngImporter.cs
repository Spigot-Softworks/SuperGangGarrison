using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace OpenGarrison.Core;

public static class CustomMapPngImporter
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public sealed record Result(GameMakerRoomMetadata Room, IReadOnlyList<LevelSolid> Solids);

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

        if (!TryDecodeWalkmask(walkmaskSection, out var solids, out var bounds))
        {
            return null;
        }

        var room = BuildRoomMetadata(Path.GetFileNameWithoutExtension(pngPath), pngPath, bounds, entitiesSection);
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

    private static bool TryDecodeWalkmask(string walkmaskSection, out IReadOnlyList<LevelSolid> solids, out WorldBounds bounds)
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
        var x = width - 1;
        var y = height - 1;
        foreach (var character in packed)
        {
            var value = Math.Max(0, character - 32);
            for (var bit = 0; bit < 6 && y >= 0; bit += 1)
            {
                if ((value & (1 << bit)) != 0)
                {
                    cells[(y * width) + x] = true;
                }

                x -= 1;
                if (x >= 0)
                {
                    continue;
                }

                x = width - 1;
                y -= 1;
            }

            if (y < 0)
            {
                break;
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

                decodedSolids.Add(new LevelSolid(start, row, column - start, 1f));
            }
        }

        bounds = new WorldBounds(width, height);
        solids = decodedSolids;
        return true;
    }

    private static GameMakerRoomMetadata BuildRoomMetadata(string mapName, string pngPath, WorldBounds bounds, string entitiesSection)
    {
        var redSpawns = new List<SpawnPoint>();
        var blueSpawns = new List<SpawnPoint>();
        var intelBases = new List<IntelBaseMarker>();
        var roomObjects = new List<RoomObjectMarker>();
        var areaTransitionMarkers = new List<AreaTransitionMarker>();
        var unsupportedEntities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                    if (TryCreateRoomObject(entityType, x, y, out var marker))
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

    private static bool TryCreateRoomObject(string entityType, float x, float y, out RoomObjectMarker marker)
    {
        marker = entityType.ToLowerInvariant() switch
        {
            "spawnroom" => new RoomObjectMarker(RoomObjectType.SpawnRoom, x, y, 42f, 42f, "sprite64", SourceName: entityType),
            "cabinets" or "healingcabinet" or "medcabinet" => new RoomObjectMarker(RoomObjectType.HealingCabinet, x, y, 32f, 48f, "sprite74", SourceName: entityType),
            "killbox" => new RoomObjectMarker(RoomObjectType.KillBox, x, y, 42f, 42f, "sprite64", SourceName: entityType),
            "fragbox" => new RoomObjectMarker(RoomObjectType.FragBox, x, y, 42f, 42f, "sprite64", SourceName: entityType),
            "redteamgate" => new RoomObjectMarker(RoomObjectType.TeamGate, x, y, 6f, 60f, "sprite45", PlayerTeam.Red, entityType),
            "blueteamgate" => new RoomObjectMarker(RoomObjectType.TeamGate, x, y, 6f, 60f, "sprite45", PlayerTeam.Blue, entityType),
            "redteamgate2" => new RoomObjectMarker(RoomObjectType.TeamGate, x, y, 60f, 6f, "sprite44", PlayerTeam.Red, entityType),
            "blueteamgate2" => new RoomObjectMarker(RoomObjectType.TeamGate, x, y, 60f, 6f, "sprite44", PlayerTeam.Blue, entityType),
            "redintelgate" => new RoomObjectMarker(RoomObjectType.IntelGate, x, y, 6f, 60f, "sprite45", PlayerTeam.Red, entityType),
            "blueintelgate" => new RoomObjectMarker(RoomObjectType.IntelGate, x, y, 6f, 60f, "sprite45", PlayerTeam.Blue, entityType),
            "redintelgate2" => new RoomObjectMarker(RoomObjectType.IntelGate, x, y, 60f, 6f, "sprite44", PlayerTeam.Red, entityType),
            "blueintelgate2" => new RoomObjectMarker(RoomObjectType.IntelGate, x, y, 60f, 6f, "sprite44", PlayerTeam.Blue, entityType),
            "intelgatevertical" => new RoomObjectMarker(RoomObjectType.IntelGate, x, y, 6f, 60f, "sprite45", SourceName: entityType),
            "intelgatehorizontal" => new RoomObjectMarker(RoomObjectType.IntelGate, x, y, 60f, 6f, "sprite44", SourceName: entityType),
            "playerwall" => new RoomObjectMarker(RoomObjectType.PlayerWall, x, y, 6f, 60f, "sprite45", SourceName: entityType),
            "playerwallhorizontal" or "playerwall_horizontal" => new RoomObjectMarker(RoomObjectType.PlayerWall, x, y, 60f, 6f, "sprite44", SourceName: entityType),
            "bulletwall" => new RoomObjectMarker(RoomObjectType.BulletWall, x, y, 6f, 60f, "sprite45", SourceName: entityType),
            "bulletwallhorizontal" or "bulletwall_horizontal" => new RoomObjectMarker(RoomObjectType.BulletWall, x, y, 60f, 6f, "sprite44", SourceName: entityType),
            "controlpoint" or "controlpoint1" or "controlpoint2" or "controlpoint3" or "controlpoint4" or "controlpoint5"
                => new RoomObjectMarker(RoomObjectType.ControlPoint, x, y, 42f, 42f, "ControlPointNeutralS", SourceName: entityType),
            "capturepoint" => new RoomObjectMarker(RoomObjectType.CaptureZone, x, y, 42f, 42f, string.Empty, SourceName: entityType),
            "setupgate" => new RoomObjectMarker(RoomObjectType.ControlPointSetupGate, x, y, 60f, 6f, "sprite44", SourceName: entityType),
            "arenacontrolpoint" => new RoomObjectMarker(RoomObjectType.ArenaControlPoint, x, y, 42f, 42f, "ControlPointNeutralS", SourceName: entityType),
            "generatorred" => new RoomObjectMarker(RoomObjectType.Generator, x, y, 40f, 40f, "GeneratorS", PlayerTeam.Red, entityType),
            "generatorblue" => new RoomObjectMarker(RoomObjectType.Generator, x, y, 40f, 40f, "GeneratorS", PlayerTeam.Blue, entityType),
            _ => default,
        };

        return marker.Type != default || entityType.Equals("spawnroom", StringComparison.OrdinalIgnoreCase);
    }
}
