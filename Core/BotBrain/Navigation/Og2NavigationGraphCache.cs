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
    private const int FormatVersion = 1;
    private const string CacheDirectoryName = "botbrain-og2-nav";

    public static bool Enabled =>
        Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_PERSISTENT_CACHE")
            is not ("0" or "false" or "FALSE");

    public static string BuildKey(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        var configuredClasses = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_CLASSES");
        var classes = string.IsNullOrWhiteSpace(configuredClasses)
            ? "default"
            : string.Join(',', configuredClasses
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.TryParse<PlayerClass>(value, true, out var playerClass)
                    ? ((int)playerClass).ToString(CultureInfo.InvariantCulture)
                    : value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        var sweepTicks = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_SWEEP_TICKS") ?? "default";
        var contactGraph = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_GRAPH") ?? "0";
        var levelFingerprint = BotNavigationAssetStore.ComputeLevelFingerprint(level);

        return string.Join('|',
            "og2-alpha",
            Og2NavigationGraphBuilder.GeneratorFingerprint,
            FormatToken(level.Name),
            level.MapAreaIndex.ToString(CultureInfo.InvariantCulture),
            ((int)level.Mode).ToString(CultureInfo.InvariantCulture),
            contactGraph,
            classes,
            sweepTicks,
            levelFingerprint);
    }

    public static string GetPath(SimpleLevel level, string key)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        var fileName = $"{FormatToken(level.Name)}.a{Math.Max(1, level.MapAreaIndex).ToString(CultureInfo.InvariantCulture)}.{digest[..20]}.og2nav.bin";
        return RuntimePaths.GetConfigPath(Path.Combine(CacheDirectoryName, fileName));
    }

    public static bool TryLoad(SimpleLevel level, string key, out NavGraph graph, out string path)
    {
        path = GetPath(level, key);
        graph = null!;
        if (!Enabled || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            if (reader.ReadUInt32() != Magic
                || reader.ReadInt32() != FormatVersion
                || !string.Equals(reader.ReadString(), key, StringComparison.Ordinal))
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
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or EndOfStreamException
            or FormatException)
        {
            graph = null!;
            return false;
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
            using (var stream = File.Create(temporaryPath))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
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

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDeleteTemporary(temporaryPath);
        }
    }

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

        return new NavEdgeCompletion(minX, maxX, minY, maxY, surfaceIds);
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
