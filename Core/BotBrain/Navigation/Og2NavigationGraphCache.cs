using System.IO.Compression;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Persistent cache for the runtime-generated OG2 alpha graph.
///
/// The graph is immutable after construction, so a compact binary snapshot is
/// safe to reuse across diagnostic processes. The cache key includes the
/// generator fingerprint, map geometry fingerprint, contact-class selection,
/// and sweep settings. Steering/runtime changes therefore reuse the graph;
/// generator or map changes naturally miss the cache.
/// </summary>
internal static class Og2NavigationGraphCache
{
    private const uint Magic = 0x32474F47; // "GOG2"
    private const int LegacyFormatVersion = 3;
    private const int FormatVersion = 4;
    private const byte BrotliCompression = 1;
    private const string CacheDirectoryName = "botbrain-og2-nav";
    private const string ShippedDirectoryName = "BotBrainOg2Nav";
    private const long MaxUncompressedCacheBytes = 512L * 1024L * 1024L;

    public static bool Enabled =>
        Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_PERSISTENT_CACHE")
            is not ("0" or "false" or "FALSE");

    public static string BuildKey(
        SimpleLevel level,
        string? sweepTicksOverride = null,
        string? contactGraphOverride = null)
    {
        ArgumentNullException.ThrowIfNull(level);

        // The production graph is class/team agnostic. Diagnostic class lists
        // select which routes are validated; they must not create a different
        // cache identity or cause a live client to rebuild the same topology.
        var classes = "all-movement-signatures-v1";
        var sweepTicks = sweepTicksOverride
            ?? Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_SWEEP_TICKS")
            ?? "default";
        var contactGraph = contactGraphOverride
            ?? Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_GRAPH")
            ?? "0";
        var configuredExtendedClasses = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_EXTENDED_CONTACT_CLASSES");
        var extendedClasses = string.IsNullOrWhiteSpace(configuredExtendedClasses)
            ? "none"
            : string.Join(',', configuredExtendedClasses
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.TryParse<PlayerClass>(value, true, out var playerClass)
                    ? ((int)playerClass).ToString(CultureInfo.InvariantCulture)
                    : value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        var levelFingerprint = ComputeGraphGeometryFingerprint(level);

        // Logical-objective attachment is generator behavior for every
        // control-point/KOTH graph, not only maps that also happen to contain
        // a moving platform. Keep CTF cache keys stable while ensuring a
        // graph built before this objective contract cannot be reused.
        var hasLogicalObjective = level.RoomObjects.Any(roomObject =>
            roomObject.Type is RoomObjectType.CaptureZone
                or RoomObjectType.ArenaControlPoint
                or RoomObjectType.ControlPoint);
        var dynamicObjectiveAttachment = hasLogicalObjective
            ? "logical-objective-walk-attach-v1"
            : null;
        var keyParts = new List<string>
        {
            "og2-alpha",
            Og2NavigationGraphBuilder.GeneratorFingerprint,
            "graph-geometry-v1",
            level.MapAreaIndex.ToString(CultureInfo.InvariantCulture),
            ((int)level.Mode).ToString(CultureInfo.InvariantCulture),
            contactGraph,
            classes,
            sweepTicks,
            extendedClasses,
        };
        if (dynamicObjectiveAttachment is not null)
        {
            keyParts.Add(dynamicObjectiveAttachment);
        }

        keyParts.Add(levelFingerprint);
        return string.Join('|', keyParts);
    }

    public static string GetPath(SimpleLevel level, string key)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        var fileName = $"og2nav.{digest[..20]}.og2nav.bin";
        return RuntimePaths.GetConfigPath(Path.Combine(CacheDirectoryName, fileName));
    }

    private static string GetShippedPath(SimpleLevel level, string key)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        var fileName = $"og2nav.{digest[..20]}.og2nav.bin";
        return ContentRoot.GetPath(ShippedDirectoryName, fileName);
    }

    public static bool TryLoad(SimpleLevel level, string key, out NavGraph graph, out string path)
    {
        path = GetShippedPath(level, key);
        graph = null!;
        if (File.Exists(path) && TryLoadFile(level, key, path, out graph))
        {
            return true;
        }

        path = GetPath(level, key);
        if (!Enabled || !File.Exists(path))
        {
            return false;
        }

        return TryLoadFile(level, key, path, out graph);
    }

    internal static void SaveShipped(SimpleLevel level, string key, NavGraph graph, out string path)
    {
        path = GetShippedPath(level, key);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{path}.{Environment.ProcessId}.tmp";
        try
        {
            var legacySnapshot = SerializeLegacySnapshot(key, graph);
            using (var stream = File.Create(temporaryPath))
            {
                WriteCompressedSnapshot(stream, legacySnapshot);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDeleteTemporary(temporaryPath);
        }
    }

    public static void Save(SimpleLevel level, string key, NavGraph graph, out string path)
    {
        path = GetPath(level, key);
        if (!Enabled)
        {
            return;
        }

        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{path}.{Environment.ProcessId}.tmp";
        try
        {
            var legacySnapshot = SerializeLegacySnapshot(key, graph);
            using (var stream = File.Create(temporaryPath))
            {
                WriteCompressedSnapshot(stream, legacySnapshot);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDeleteTemporary(temporaryPath);
        }
    }

    private static bool TryLoadFile(SimpleLevel level, string key, string path, out NavGraph graph)
    {
        graph = null!;
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            if (reader.ReadUInt32() != Magic)
            {
                return false;
            }

            var version = reader.ReadInt32();
            if (version == LegacyFormatVersion)
            {
                return TryReadLegacyGraph(reader, level, key, out graph);
            }

            if (version != FormatVersion || reader.ReadByte() != BrotliCompression)
            {
                return false;
            }

            var uncompressedLength = reader.ReadInt64();
            if (uncompressedLength is < 0 or > MaxUncompressedCacheBytes)
            {
                return false;
            }

            using var decompressed = new MemoryStream((int)Math.Min(uncompressedLength, int.MaxValue));
            using (var brotli = new BrotliStream(stream, CompressionMode.Decompress, leaveOpen: true))
            {
                brotli.CopyTo(decompressed);
            }

            if (decompressed.Length != uncompressedLength)
            {
                return false;
            }

            decompressed.Position = 0;
            using var payloadReader = new BinaryReader(decompressed, Encoding.UTF8, leaveOpen: false);
            if (payloadReader.ReadUInt32() != Magic
                || payloadReader.ReadInt32() != LegacyFormatVersion)
            {
                return false;
            }

            return TryReadLegacyGraph(payloadReader, level, key, out graph);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or EndOfStreamException
            or FormatException
            or NotSupportedException)
        {
            graph = null!;
            return false;
        }
    }

    internal static Og2NavigationGraphCacheCompactionResult CompactPersistentCache()
    {
        var directory = RuntimePaths.GetConfigPath(CacheDirectoryName);
        if (!Directory.Exists(directory))
        {
            return new Og2NavigationGraphCacheCompactionResult(0, 0, 0, 0, 0, 0, 0, 0);
        }

        var scanned = 0;
        var compressed = 0;
        var skipped = 0;
        var failed = 0;
        var pruned = 0;
        long bytesBefore = 0;
        long bytesAfter = 0;
        long bytesPruned = 0;

        foreach (var path in Directory.EnumerateFiles(directory, "*.og2nav.bin", SearchOption.TopDirectoryOnly))
        {
            scanned += 1;
            try
            {
                var snapshot = File.ReadAllBytes(path);
                if (snapshot.Length < sizeof(uint) + sizeof(int))
                {
                    skipped += 1;
                    continue;
                }

                using var headerStream = new MemoryStream(snapshot, writable: false);
                using var headerReader = new BinaryReader(headerStream, Encoding.UTF8, leaveOpen: false);
                if (headerReader.ReadUInt32() != Magic)
                {
                    skipped += 1;
                    continue;
                }

                var version = headerReader.ReadInt32();
                if (version is 1 or 2)
                {
                    File.Delete(path);
                    pruned += 1;
                    bytesPruned += snapshot.Length;
                    continue;
                }

                if (version == FormatVersion)
                {
                    if (!TryReadCompressedSnapshot(snapshot, out var currentSnapshot)
                        || !TryReadSnapshotKey(currentSnapshot, out var currentKey)
                        || !IsCurrentGeneratorKey(currentKey))
                    {
                        File.Delete(path);
                        pruned += 1;
                        bytesPruned += snapshot.Length;
                        continue;
                    }

                    skipped += 1;
                    continue;
                }

                if (version != LegacyFormatVersion)
                {
                    skipped += 1;
                    continue;
                }

                if (!TryReadSnapshotKey(snapshot, out var legacyKey))
                {
                    skipped += 1;
                    continue;
                }

                if (!IsCurrentGeneratorKey(legacyKey))
                {
                    File.Delete(path);
                    pruned += 1;
                    bytesPruned += snapshot.Length;
                    continue;
                }

                var temporaryPath = $"{path}.{Environment.ProcessId}.compact.tmp";
                try
                {
                    using (var temporaryStream = File.Create(temporaryPath))
                    {
                        WriteCompressedSnapshot(temporaryStream, snapshot);
                    }

                    var compressedSnapshot = File.ReadAllBytes(temporaryPath);
                    if (!HasVerifiedRoundTrip(snapshot, compressedSnapshot))
                    {
                        failed += 1;
                        continue;
                    }

                    File.Move(temporaryPath, path, overwrite: true);
                    compressed += 1;
                    bytesBefore += snapshot.Length;
                    bytesAfter += compressedSnapshot.Length;
                }
                finally
                {
                    TryDeleteTemporary(temporaryPath);
                }
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or EndOfStreamException
                or NotSupportedException)
            {
                failed += 1;
            }
        }

        return new Og2NavigationGraphCacheCompactionResult(
            scanned,
            compressed,
            skipped,
            failed,
            pruned,
            bytesBefore,
            bytesAfter,
            bytesPruned);
    }

    private static bool TryReadLegacyGraph(
        BinaryReader reader,
        SimpleLevel level,
        string key,
        out NavGraph graph)
    {
        graph = null!;
        if (!string.Equals(reader.ReadString(), key, StringComparison.Ordinal))
        {
            return false;
        }

        var nodeCount = ReadCount(reader, 2_000_000);
        var nodes = new NavNode[nodeCount];
            for (var index = 0; index < nodes.Length; index += 1)
            {
                var x = reader.ReadSingle();
                var y = reader.ReadSingle();
                var kind = (NavNodeKind)reader.ReadByte();
                var surfaceId = reader.ReadBoolean() ? (int?)reader.ReadInt32() : null;
                if (!float.IsFinite(x) || !float.IsFinite(y))
                {
                    return false;
                }

                nodes[index] = new NavNode(x, y, kind, surfaceId);
            }

            var adjacency = new List<NavEdge>[nodeCount];
            for (var nodeIndex = 0; nodeIndex < adjacency.Length; nodeIndex += 1)
            {
                var edgeCount = ReadCount(reader, 500_000);
                var edges = new List<NavEdge>(edgeCount);
                for (var edgeIndex = 0; edgeIndex < edgeCount; edgeIndex += 1)
                {
                    var toNode = reader.ReadInt32();
                    if (toNode < 0 || toNode >= nodeCount)
                    {
                        return false;
                    }

                    var kind = (NavEdgeKind)reader.ReadByte();
                    var cost = reader.ReadSingle();
                    var completion = ReadCompletion(reader);
                    var jumpTriggerTick = reader.ReadInt32();
                    var probeTicks = reader.ReadInt32();
                    var probeMoveDirectionX = reader.ReadSingle();
                    var probeVariantAttempts = reader.ReadInt32();
                    var probeVariantSuccesses = reader.ReadInt32();
                    var supportedClassMask = reader.ReadInt32();
                    var supportedTeamMask = reader.ReadInt32();
                    var requiresGroundedContinuation = reader.ReadBoolean();
                    var requiresCarryingIntel = reader.ReadBoolean();
                    var launchRecipe = ReadLaunchRecipe(reader);
                    var carryingRequirement = reader.ReadSByte() switch
                    {
                        -1 => (bool?)null,
                        0 => false,
                        1 => true,
                        _ => throw new InvalidDataException("Invalid carrying-intel requirement."),
                    };
                    var isOg2Contact = reader.ReadBoolean();

                    edges.Add(new NavEdge(
                        toNode,
                        kind,
                        cost,
                        completion,
                        jumpTriggerTick,
                        probeTicks,
                        probeMoveDirectionX,
                        probeVariantAttempts,
                        probeVariantSuccesses,
                        supportedClassMask,
                        supportedTeamMask,
                        requiresGroundedContinuation,
                        requiresCarryingIntel,
                        launchRecipe,
                        carryingRequirement,
                        isOg2Contact));
                }

                adjacency[nodeIndex] = edges;
            }

        graph = new NavGraph(nodes, adjacency, level.Name, level.Mode, BuildSpawnAnchors(level), isOg2Alpha: true);
        return true;
    }

    private static byte[] SerializeLegacySnapshot(string key, NavGraph graph)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(LegacyFormatVersion);
            writer.Write(key);
            writer.Write(graph.NodeCount);
            for (var nodeIndex = 0; nodeIndex < graph.NodeCount; nodeIndex += 1)
            {
                var node = graph.GetNode(nodeIndex);
                writer.Write(node.X);
                writer.Write(node.Y);
                writer.Write((byte)node.Kind);
                writer.Write(node.SurfaceId.HasValue);
                if (node.SurfaceId.HasValue)
                {
                    writer.Write(node.SurfaceId.Value);
                }
            }

            for (var nodeIndex = 0; nodeIndex < graph.NodeCount; nodeIndex += 1)
            {
                var edges = graph.GetEdges(nodeIndex);
                writer.Write(edges.Length);
                for (var edgeIndex = 0; edgeIndex < edges.Length; edgeIndex += 1)
                {
                    var edge = edges[edgeIndex];
                    writer.Write(edge.ToNode);
                    writer.Write((byte)edge.Kind);
                    writer.Write(edge.Cost);
                    WriteCompletion(writer, edge.Completion);
                    writer.Write(edge.JumpTriggerTick);
                    writer.Write(edge.ProbeTicks);
                    writer.Write(edge.ProbeMoveDirectionX);
                    writer.Write(edge.ProbeVariantAttempts);
                    writer.Write(edge.ProbeVariantSuccesses);
                    writer.Write(edge.SupportedClassMask);
                    writer.Write(edge.SupportedTeamMask);
                    writer.Write(edge.RequiresGroundedContinuation);
                    writer.Write(edge.RequiresCarryingIntel);
                    WriteLaunchRecipe(writer, edge.LaunchRecipe);
                    writer.Write(edge.CarryingIntelRequirement switch
                    {
                        null => (sbyte)-1,
                        false => (sbyte)0,
                        true => (sbyte)1,
                    });
                    writer.Write(edge.IsOg2Contact);
                }
            }
        }

        return stream.ToArray();
    }

    private static void WriteCompressedSnapshot(Stream destination, byte[] legacySnapshot)
    {
        using (var writer = new BinaryWriter(destination, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(FormatVersion);
            writer.Write(BrotliCompression);
            writer.Write((long)legacySnapshot.Length);
            writer.Flush();
        }

        using var brotli = new BrotliStream(destination, CompressionLevel.Optimal, leaveOpen: true);
        brotli.Write(legacySnapshot, 0, legacySnapshot.Length);
    }

    private static bool HasVerifiedRoundTrip(byte[] expected, byte[] compressed)
    {
        using var source = new MemoryStream(compressed, writable: false);
        using var reader = new BinaryReader(source, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != Magic
            || reader.ReadInt32() != FormatVersion
            || reader.ReadByte() != BrotliCompression)
        {
            return false;
        }

        var expectedLength = reader.ReadInt64();
        if (expectedLength != expected.Length)
        {
            return false;
        }

        using var decompressed = new MemoryStream((int)Math.Min(expectedLength, int.MaxValue));
        using (var brotli = new BrotliStream(source, CompressionMode.Decompress, leaveOpen: true))
        {
            brotli.CopyTo(decompressed);
        }

        return decompressed.Length == expected.Length
            && decompressed.TryGetBuffer(out var actualBuffer)
            && expected.AsSpan().SequenceEqual(actualBuffer.AsSpan(0, expected.Length));
    }

    private static bool TryReadCompressedSnapshot(byte[] compressed, out byte[] legacySnapshot)
    {
        legacySnapshot = [];
        try
        {
            using var source = new MemoryStream(compressed, writable: false);
            using var reader = new BinaryReader(source, Encoding.UTF8, leaveOpen: true);
            if (reader.ReadUInt32() != Magic
                || reader.ReadInt32() != FormatVersion
                || reader.ReadByte() != BrotliCompression)
            {
                return false;
            }

            var uncompressedLength = reader.ReadInt64();
            if (uncompressedLength is < 0 or > MaxUncompressedCacheBytes)
            {
                return false;
            }

            using var decompressed = new MemoryStream((int)Math.Min(uncompressedLength, int.MaxValue));
            using (var brotli = new BrotliStream(source, CompressionMode.Decompress, leaveOpen: true))
            {
                brotli.CopyTo(decompressed);
            }

            if (decompressed.Length != uncompressedLength)
            {
                return false;
            }

            legacySnapshot = decompressed.ToArray();
            return true;
        }
        catch (Exception ex) when (ex is IOException
            or InvalidDataException
            or EndOfStreamException
            or FormatException
            or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryReadSnapshotKey(byte[] snapshot, out string key)
    {
        key = string.Empty;
        try
        {
            using var stream = new MemoryStream(snapshot, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            if (reader.ReadUInt32() != Magic
                || reader.ReadInt32() != LegacyFormatVersion)
            {
                return false;
            }

            key = reader.ReadString();
            return !string.IsNullOrWhiteSpace(key);
        }
        catch (Exception ex) when (ex is IOException
            or InvalidDataException
            or EndOfStreamException
            or FormatException)
        {
            return false;
        }
    }

    private static bool IsCurrentGeneratorKey(string key) =>
        key.StartsWith(
            $"og2-alpha|{Og2NavigationGraphBuilder.GeneratorFingerprint}|",
            StringComparison.Ordinal);

    private static string ComputeGraphGeometryFingerprint(SimpleLevel level)
    {
        var builder = new StringBuilder();
        AppendFingerprint(builder, level.Mode.ToString());
        AppendFingerprint(builder, level.MapAreaIndex);
        AppendFingerprint(builder, level.MapScale);
        AppendFingerprint(builder, level.Bounds.Width);
        AppendFingerprint(builder, level.Bounds.Height);
        AppendFingerprint(builder, level.FloorY);
        AppendFingerprint(builder, level.Solids.Count);
        foreach (var solid in level.Solids.OrderBy(static solid => solid.Left).ThenBy(static solid => solid.Top))
        {
            AppendFingerprint(builder, solid.Left);
            AppendFingerprint(builder, solid.Top);
            AppendFingerprint(builder, solid.Width);
            AppendFingerprint(builder, solid.Height);
        }

        AppendFingerprint(builder, level.RoomObjects.Count);
        foreach (var roomObject in level.RoomObjects
                     .OrderBy(static roomObject => roomObject.Type)
                     .ThenBy(static roomObject => roomObject.Left)
                     .ThenBy(static roomObject => roomObject.Top))
        {
            AppendFingerprint(builder, roomObject.Type.ToString());
            AppendFingerprint(builder, roomObject.Left);
            AppendFingerprint(builder, roomObject.Top);
            AppendFingerprint(builder, roomObject.Width);
            AppendFingerprint(builder, roomObject.Height);
            AppendFingerprint(builder, roomObject.Team?.ToString() ?? string.Empty);
            AppendFingerprint(builder, roomObject.Value);
        }

        AppendFingerprint(builder, level.RedSpawns.Count);
        foreach (var spawn in level.RedSpawns.OrderBy(static spawn => spawn.X).ThenBy(static spawn => spawn.Y))
        {
            AppendFingerprint(builder, spawn.X);
            AppendFingerprint(builder, spawn.Y);
        }

        AppendFingerprint(builder, level.BlueSpawns.Count);
        foreach (var spawn in level.BlueSpawns.OrderBy(static spawn => spawn.X).ThenBy(static spawn => spawn.Y))
        {
            AppendFingerprint(builder, spawn.X);
            AppendFingerprint(builder, spawn.Y);
        }

        AppendFingerprint(builder, level.IntelBases.Count);
        foreach (var intelBase in level.IntelBases.OrderBy(static intel => intel.Team).ThenBy(static intel => intel.X).ThenBy(static intel => intel.Y))
        {
            AppendFingerprint(builder, intelBase.Team.ToString());
            AppendFingerprint(builder, intelBase.X);
            AppendFingerprint(builder, intelBase.Y);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static void AppendFingerprint(StringBuilder builder, string value)
    {
        builder.Append(value);
        builder.Append('\n');
    }

    private static void AppendFingerprint(StringBuilder builder, int value) =>
        AppendFingerprint(builder, value.ToString(CultureInfo.InvariantCulture));

    private static void AppendFingerprint(StringBuilder builder, float value) =>
        AppendFingerprint(builder, value.ToString("R", CultureInfo.InvariantCulture));

    private static NavSpawnAnchor[] BuildSpawnAnchors(SimpleLevel level) =>
        level.RedSpawns
            .Select(spawn => new NavSpawnAnchor(spawn.X, spawn.Y, PlayerTeam.Red))
            .Concat(level.BlueSpawns.Select(spawn => new NavSpawnAnchor(spawn.X, spawn.Y, PlayerTeam.Blue)))
            .ToArray();

    private static NavEdgeCompletion ReadCompletion(BinaryReader reader)
    {
        var minX = reader.ReadSingle();
        var maxX = reader.ReadSingle();
        var minY = reader.ReadSingle();
        var maxY = reader.ReadSingle();
        var surfaceCount = ReadCount(reader, 100_000);
        var surfaceIds = new int[surfaceCount];
        for (var index = 0; index < surfaceIds.Length; index += 1)
        {
            surfaceIds[index] = reader.ReadInt32();
        }

        return new NavEdgeCompletion(minX, maxX, minY, maxY, surfaceIds, reader.ReadBoolean());
    }

    private static void WriteCompletion(BinaryWriter writer, NavEdgeCompletion completion)
    {
        writer.Write(completion.MinX);
        writer.Write(completion.MaxX);
        writer.Write(completion.MinY);
        writer.Write(completion.MaxY);
        writer.Write(completion.AcceptedSurfaceIds.Length);
        foreach (var surfaceId in completion.AcceptedSurfaceIds)
        {
            writer.Write(surfaceId);
        }

        writer.Write(completion.AllowsAirborneObjective);
    }

    private static NavEdgeLaunchRecipe ReadLaunchRecipe(BinaryReader reader) => new(
        reader.ReadBoolean(),
        reader.ReadInt32(),
        reader.ReadSingle(),
        reader.ReadSingle(),
        reader.ReadSingle(),
        reader.ReadSingle(),
        reader.ReadSingle(),
        reader.ReadSingle(),
        reader.ReadSingle(),
        reader.ReadBoolean(),
        (NavEdgeAirControlMode)reader.ReadByte(),
        reader.ReadInt32(),
        reader.ReadInt32());

    private static void WriteLaunchRecipe(BinaryWriter writer, NavEdgeLaunchRecipe recipe)
    {
        writer.Write(recipe.StartGrounded);
        writer.Write(recipe.LaunchTick);
        writer.Write(recipe.LaunchMinX);
        writer.Write(recipe.LaunchMaxX);
        writer.Write(recipe.LaunchMinY);
        writer.Write(recipe.LaunchMaxY);
        writer.Write(recipe.LaunchMinHorizontalSpeed);
        writer.Write(recipe.LaunchMaxHorizontalSpeed);
        writer.Write(recipe.ExpectedMoveDirectionX);
        writer.Write(recipe.JumpStartsGrounded);
        writer.Write((byte)recipe.AirControlMode);
        writer.Write(recipe.AirControlHoldTicks);
        writer.Write(recipe.PreLaunchBrakeTicks);
    }

    private static int ReadCount(BinaryReader reader, int maximum)
    {
        var count = reader.ReadInt32();
        return count >= 0 && count <= maximum
            ? count
            : throw new InvalidDataException($"Invalid graph cache count: {count}.");
    }

    private static string FormatToken(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_');
        }

        return builder.Length == 0 ? "map" : builder.ToString();
    }

    private static void TryDeleteTemporary(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal readonly record struct Og2NavigationGraphCacheCompactionResult(
    int Scanned,
    int Compressed,
    int Skipped,
    int Failed,
    int Pruned,
    long BytesBefore,
    long BytesAfter,
    long BytesPruned);
