using OpenGarrison.Core;
using System.IO;
using System.Text;

namespace OpenGarrison.BotAI;

internal static class OriginalClientBotNavMeshStore
{
    private const string RelativeContentDirectory = "Core/Content/BotNav/Original";
    private static readonly IReadOnlyDictionary<string, string> MeshFileNameByLevelName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Conflict"] = "ctf_conflict.pts",
        ["Truefort"] = "ctf_truefort.pts",
        ["Waterway"] = "ctf_waterway.pts",
        ["Corinth"] = "koth_corinth.pts",
        ["Harvest"] = "koth_harvest.pts",
    };

    public static bool TryResolveMeshPath(SimpleLevel level, out string meshPath)
    {
        meshPath = string.Empty;
        if (!MeshFileNameByLevelName.TryGetValue(level.Name, out var fileName))
        {
            return false;
        }

        var runtimePath = ContentRoot.GetPath("BotNav", "Original", fileName);
        if (File.Exists(runtimePath))
        {
            meshPath = runtimePath;
            return true;
        }

        var projectContentPath = ProjectSourceLocator.FindFile(Path.Combine(RelativeContentDirectory, fileName));
        if (!string.IsNullOrWhiteSpace(projectContentPath) && File.Exists(projectContentPath))
        {
            meshPath = projectContentPath;
            return true;
        }

        var sourceContentPath = ProjectSourceLocator.FindFile(Path.Combine(ContentRoot.Path, "BotNav", "Original", fileName));
        if (!string.IsNullOrWhiteSpace(sourceContentPath) && File.Exists(sourceContentPath))
        {
            meshPath = sourceContentPath;
            return true;
        }

        return false;
    }

    public static ClientBotNavPoints Load(SimpleLevel level, string meshPath)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentException.ThrowIfNullOrWhiteSpace(meshPath);

        var navPoints = ReadOriginalGg2NavMesh(meshPath);
        var levelFingerprint = BotNavigationLevelFingerprint.Compute(level);
        var nodes = new BotNavigationNode[navPoints.Count];
        var edges = new List<BotNavigationEdge>();
        for (var pointId = 0; pointId < navPoints.Count; pointId += 1)
        {
            var point = navPoints[pointId];
            nodes[pointId] = new BotNavigationNode
            {
                Id = pointId,
                X = point.X,
                Y = point.Y,
                SurfaceId = pointId,
                Kind = BotNavigationNodeKind.Surface,
                RequiresGroundSupport = true,
                ReverseOnlyBlockedFromNodeIds = point.Blocked,
            };

            for (var edgeIndex = 0; edgeIndex < point.Outgoing.Length; edgeIndex += 1)
            {
                var toNodeId = point.Outgoing[edgeIndex];
                if (toNodeId < 0 || toNodeId >= navPoints.Count)
                {
                    throw new InvalidDataException($"original nav mesh '{meshPath}' contains invalid edge {pointId}->{toNodeId}");
                }

                var targetPoint = navPoints[toNodeId];
                edges.Add(new BotNavigationEdge
                {
                    FromNodeId = pointId,
                    ToNodeId = toNodeId,
                    Kind = BotNavigationTraversalKind.Walk,
                    Cost = MathF.Max(6f, DistanceBetween(point.X, point.Y, targetPoint.X, targetPoint.Y)),
                });
            }
        }

        var asset = new BotNavigationAsset
        {
            FormatVersion = BotNavigationAssetStore.CurrentFormatVersion,
            LevelName = level.Name,
            MapAreaIndex = level.MapAreaIndex,
            LevelFingerprint = levelFingerprint,
            BuildStrategy = BotNavigationBuildStrategy.ModernClientBotPointGraph,
            Nodes = nodes,
            Edges = edges,
        };
        return ClientBotNavPoints.Build(asset, level);
    }

    private static List<OriginalGg2NavPoint> ReadOriginalGg2NavMesh(string path)
    {
        var payload = File.ReadAllText(path).Trim();
        var topLevelEntries = ReadGameMakerSerializedList(HexToBytes(payload));
        var points = new List<OriginalGg2NavPoint>(topLevelEntries.Count);
        for (var index = 0; index < topLevelEntries.Count; index += 1)
        {
            if (topLevelEntries[index] is not string pointPayload)
            {
                throw new InvalidDataException($"original navpoint {index} payload is not a string");
            }

            var pointEntries = ReadGameMakerSerializedList(HexToBytes(pointPayload));
            if (pointEntries.Count < 2)
            {
                throw new InvalidDataException($"original navpoint {index} has only {pointEntries.Count} entries");
            }

            var x = (float)ReadSerializedReal(pointEntries[0], $"navpoint {index} x");
            var y = (float)ReadSerializedReal(pointEntries[1], $"navpoint {index} y");
            var outgoing = Array.Empty<int>();
            if (pointEntries.Count >= 3)
            {
                var outgoingPayload = ReadSerializedString(pointEntries[2], $"navpoint {index} outgoing");
                var outgoingEntries = ReadGameMakerSerializedList(HexToBytes(outgoingPayload));
                outgoing = outgoingEntries
                    .Select(static value => (int)Math.Round(ReadSerializedReal(value, "outgoing node id")))
                    .OrderBy(static value => value)
                    .ToArray();
            }

            var blocked = Array.Empty<int>();
            if (pointEntries.Count >= 4)
            {
                var blockedPayload = ReadSerializedString(pointEntries[3], $"navpoint {index} blocked");
                var blockedEntries = ReadGameMakerSerializedList(HexToBytes(blockedPayload));
                blocked = blockedEntries
                    .Select(static value => (int)Math.Round(ReadSerializedReal(value, "blocked node id")))
                    .OrderBy(static value => value)
                    .ToArray();
            }

            points.Add(new OriginalGg2NavPoint(x, y, outgoing, blocked));
        }

        return points;
    }

    private static List<object> ReadGameMakerSerializedList(byte[] bytes)
    {
        if (bytes.Length < 8)
        {
            throw new InvalidDataException("serialized ds_list too short");
        }

        if (bytes[0] != 0x2D || bytes[1] != 0x01)
        {
            throw new InvalidDataException("serialized ds_list header mismatch");
        }

        var count = BitConverter.ToInt32(bytes, 4);
        var values = new List<object>(count);
        var offset = 8;
        for (var index = 0; index < count; index += 1)
        {
            if ((offset + 8) > bytes.Length)
            {
                throw new InvalidDataException($"serialized ds_list truncated at item {index}");
            }

            var itemType = BitConverter.ToInt32(bytes, offset);
            switch (itemType)
            {
                case 0:
                    if ((offset + 16) > bytes.Length)
                    {
                        throw new InvalidDataException($"serialized real truncated at item {index}");
                    }

                    values.Add(ReadGameMakerReal(bytes, offset + 8));
                    offset += 16;
                    break;

                case 1:
                    if ((offset + 16) > bytes.Length)
                    {
                        throw new InvalidDataException($"serialized string header truncated at item {index}");
                    }

                    var stringLength = BitConverter.ToInt32(bytes, offset + 12);
                    if (stringLength < 0 || (offset + 16 + stringLength) > bytes.Length)
                    {
                        throw new InvalidDataException($"serialized string payload truncated at item {index}");
                    }

                    values.Add(Encoding.ASCII.GetString(bytes, offset + 16, stringLength));
                    offset += 16 + stringLength;
                    break;

                default:
                    throw new InvalidDataException($"unsupported GameMaker ds_list item type {itemType} at index {index}");
            }
        }

        return values;
    }

    private static double ReadGameMakerReal(byte[] bytes, int offset)
    {
        var lowWord = BitConverter.ToUInt32(bytes, offset);
        var highWord = BitConverter.ToUInt32(bytes, offset + 4);
        var bits = ((ulong)lowWord << 32) | highWord;
        return BitConverter.Int64BitsToDouble((long)bits);
    }

    private static byte[] HexToBytes(string hex)
    {
        if (hex.Length % 2 != 0)
        {
            throw new InvalidDataException("hex payload length must be even");
        }

        var bytes = new byte[hex.Length / 2];
        for (var index = 0; index < bytes.Length; index += 1)
        {
            bytes[index] = Convert.ToByte(hex.Substring(index * 2, 2), 16);
        }

        return bytes;
    }

    private static double ReadSerializedReal(object value, string description)
    {
        if (value is double number)
        {
            return number;
        }

        throw new InvalidDataException($"{description} is not a real");
    }

    private static string ReadSerializedString(object value, string description)
    {
        if (value is string text)
        {
            return text;
        }

        throw new InvalidDataException($"{description} is not a string");
    }

    private static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private readonly record struct OriginalGg2NavPoint(float X, float Y, int[] Outgoing, int[] Blocked);
}
