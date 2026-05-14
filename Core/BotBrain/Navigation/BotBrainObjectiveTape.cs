using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.Core.BotBrain;

public sealed class BotBrainObjectiveTapeAsset
{
    public int FormatVersion { get; set; } = 1;

    public string LevelName { get; set; } = string.Empty;

    public int MapAreaIndex { get; set; }

    public List<BotBrainObjectiveTapeEntry> Tapes { get; set; } = [];
}

public sealed class BotBrainObjectiveTapeEntry
{
    public string Name { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlayerTeam Team { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlayerClass PlayerClass { get; set; }

    public List<BotBrainObjectiveTapeSegment> Segments { get; set; } = [];
}

public sealed class BotBrainObjectiveTapeSegment
{
    public bool RequiresCarryingIntel { get; set; }

    public string Source { get; set; } = string.Empty;

    public float? EntryX { get; set; }

    public float? EntryY { get; set; }

    public float? EntryRadius { get; set; }

    public float? ExitX { get; set; }

    public float? ExitY { get; set; }

    public float? ExitRadius { get; set; }

    public List<BotBrainObjectiveTapeSample> Samples { get; set; } = [];

    public List<BotBrainObjectiveTapeAction> Actions { get; set; } = [];
}

public sealed class BotBrainObjectiveTapeAction
{
    public string Kind { get; set; } = string.Empty;

    public int Direction { get; set; }

    public int Ticks { get; set; }
}

public sealed class BotBrainObjectiveTapeSample
{
    public int Tick { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public float Bottom { get; set; }

    public float HorizontalSpeed { get; set; }

    public float VerticalSpeed { get; set; }

    public bool IsGrounded { get; set; }

    public float MoveDirection { get; set; }

    public bool Jump { get; set; }

    public bool DropDown { get; set; }

    public bool IsCarryingIntel { get; set; }
}

public static class BotBrainObjectiveTapeStore
{
    private const string TapeDirectoryName = "BotBrainTapes";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static bool TryLoad(SimpleLevel level, out BotBrainObjectiveTapeAsset asset)
    {
        ArgumentNullException.ThrowIfNull(level);

        asset = null!;
        var path = ResolvePath(level.Name, level.MapAreaIndex);
        if (!File.Exists(path))
        {
            return TryLoadFromAuthoredCorridors(level, out asset);
        }

        try
        {
            asset = JsonSerializer.Deserialize<BotBrainObjectiveTapeAsset>(File.ReadAllText(path), SerializerOptions)!;
        }
        catch
        {
            return false;
        }

        if (asset is not null
            && string.Equals(asset.LevelName, level.Name, StringComparison.OrdinalIgnoreCase)
            && asset.MapAreaIndex == level.MapAreaIndex)
        {
            AddMirroredCaptureTheFlagTapes(level, asset);
            return true;
        }

        return TryLoadFromAuthoredCorridors(level, out asset);
    }

    private static bool TryLoadFromAuthoredCorridors(SimpleLevel level, out BotBrainObjectiveTapeAsset asset)
    {
        if (!level.Name.Equals("Eiger", StringComparison.OrdinalIgnoreCase))
        {
            asset = null!;
            return false;
        }

        if (TryBuildFromAuthoredCorridors(level, out asset))
        {
            if (level.Mode == GameModeKind.CaptureTheFlag)
            {
                KeepOnlyCarrierReturnSegments(asset);
            }

            AddMirroredCaptureTheFlagTapes(level, asset);
            return true;
        }

        return false;
    }

    private static void KeepOnlyCarrierReturnSegments(BotBrainObjectiveTapeAsset asset)
    {
        foreach (var tape in asset.Tapes)
        {
            tape.Segments = tape.Segments
                .Where(static segment => segment.RequiresCarryingIntel)
                .ToList();
        }

        asset.Tapes = asset.Tapes
            .Where(static tape => tape.Segments.Count > 0)
            .ToList();
    }

    private static void AddMirroredCaptureTheFlagTapes(SimpleLevel level, BotBrainObjectiveTapeAsset asset)
    {
        if (level.Mode != GameModeKind.CaptureTheFlag
            || !level.GetIntelBase(PlayerTeam.Red).HasValue
            || !level.GetIntelBase(PlayerTeam.Blue).HasValue)
        {
            return;
        }

        var redBase = level.GetIntelBase(PlayerTeam.Red)!.Value;
        var blueBase = level.GetIntelBase(PlayerTeam.Blue)!.Value;
        var mirrorSumX = redBase.X + blueBase.X;
        var allowMirrorCompetition = string.Equals(level.Name, "Truefort", StringComparison.OrdinalIgnoreCase);
        var sourceTapes = asset.Tapes.ToArray();
        foreach (var source in sourceTapes)
        {
            var mirroredSegments = source.Segments
                .Select(segment => new BotBrainObjectiveTapeSegment
                {
                    RequiresCarryingIntel = segment.RequiresCarryingIntel,
                    Source = segment.Source,
                    EntryX = MirrorNullableX(segment.EntryX, mirrorSumX),
                    EntryY = segment.EntryY,
                    EntryRadius = segment.EntryRadius,
                    ExitX = MirrorNullableX(segment.ExitX, mirrorSumX),
                    ExitY = segment.ExitY,
                    ExitRadius = segment.ExitRadius,
                    Samples = segment.Samples.Select(sample => new BotBrainObjectiveTapeSample
                    {
                        Tick = sample.Tick,
                        X = mirrorSumX - sample.X,
                        Y = sample.Y,
                        Bottom = sample.Bottom,
                        HorizontalSpeed = -sample.HorizontalSpeed,
                        VerticalSpeed = sample.VerticalSpeed,
                        IsGrounded = sample.IsGrounded,
                        MoveDirection = -sample.MoveDirection,
                        Jump = sample.Jump,
                        DropDown = sample.DropDown,
                        IsCarryingIntel = sample.IsCarryingIntel,
                    }).ToList(),
                    Actions = segment.Actions.Select(action => new BotBrainObjectiveTapeAction
                    {
                        Kind = action.Kind,
                        Direction = -action.Direction,
                        Ticks = action.Ticks,
                    }).ToList(),
                })
                .Where(static segment => segment.Samples.Count >= 2)
                .ToList();
            if (mirroredSegments.Count == 0)
            {
                continue;
            }

            var mirroredTeam = source.Team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
            var hasNativeCoverage = sourceTapes
                .Where(tape => tape.Team == mirroredTeam
                    && tape.PlayerClass == source.PlayerClass
                    && !tape.Name.Contains(".Mirrored", StringComparison.OrdinalIgnoreCase))
                .SelectMany(tape => tape.Segments)
                .Select(segment => segment.RequiresCarryingIntel)
                .Distinct()
                .Count(carryingIntel => mirroredSegments.Any(segment => segment.RequiresCarryingIntel == carryingIntel))
                == mirroredSegments
                    .Select(segment => segment.RequiresCarryingIntel)
                    .Distinct()
                    .Count();
            if (hasNativeCoverage && !allowMirrorCompetition)
            {
                continue;
            }

            var mirroredName = $"{source.Name}.Mirrored{mirroredTeam}";
            if (asset.Tapes.Any(tape => tape.Name.Equals(mirroredName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            asset.Tapes.Add(new BotBrainObjectiveTapeEntry
            {
                Name = mirroredName,
                Team = mirroredTeam,
                PlayerClass = source.PlayerClass,
                Segments = mirroredSegments,
            });
        }
    }

    private static float? MirrorNullableX(float? x, float mirrorSumX) =>
        x.HasValue ? mirrorSumX - x.Value : null;

    private static bool TryBuildFromAuthoredCorridors(SimpleLevel level, out BotBrainObjectiveTapeAsset asset)
    {
        asset = null!;
        if (!BotNavigationAuthoredCorridorStore.TryLoad(level, out var corridorAsset))
        {
            return false;
        }

        asset = new BotBrainObjectiveTapeAsset
        {
            LevelName = level.Name,
            MapAreaIndex = level.MapAreaIndex,
        };
        foreach (var corridor in corridorAsset.Corridors)
        {
            var segments = BuildSegmentsFromCorridor(level, corridor);
            if (segments.Count == 0)
            {
                continue;
            }

            asset.Tapes.Add(new BotBrainObjectiveTapeEntry
            {
                Name = corridor.Name,
                Team = corridor.Team,
                PlayerClass = corridor.PlayerClass,
                Segments = segments,
            });
        }

        return asset.Tapes.Count > 0;
    }

    private static List<BotBrainObjectiveTapeSegment> BuildSegmentsFromCorridor(
        SimpleLevel level,
        BotNavigationAuthoredCorridorEntry corridor)
    {
        if (corridor.Waypoints.Count < 2)
        {
            return [];
        }

        if (level.Mode == GameModeKind.CaptureTheFlag
            && TryBuildCaptureTheFlagSegments(level, corridor, out var ctfSegments))
        {
            return ctfSegments;
        }

        var samples = BuildSamplesFromCorridor(corridor, 0, corridor.Waypoints.Count, carryingIntel: false);
        return samples.Count >= 2
            ?
            [
                new BotBrainObjectiveTapeSegment
                {
                    RequiresCarryingIntel = false,
                    Samples = samples,
                },
            ]
            : [];
    }

    private static bool TryBuildCaptureTheFlagSegments(
        SimpleLevel level,
        BotNavigationAuthoredCorridorEntry corridor,
        out List<BotBrainObjectiveTapeSegment> segments)
    {
        segments = [];
        var opposingTeam = corridor.Team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        var enemyBase = level.GetIntelBase(opposingTeam);
        var ownBase = level.GetIntelBase(corridor.Team);
        if (!enemyBase.HasValue || !ownBase.HasValue || corridor.Waypoints.Count < 2)
        {
            return false;
        }

        var first = corridor.Waypoints[0];
        var last = corridor.Waypoints[^1];
        var startsNearOwnBase = DistanceSquared(first.X, first.Y, ownBase.Value.X, ownBase.Value.Y) <= 900f * 900f;
        var startsNearEnemyBase = DistanceSquared(first.X, first.Y, enemyBase.Value.X, enemyBase.Value.Y) <= 900f * 900f;
        var endsNearOwnBase = DistanceSquared(last.X, last.Y, ownBase.Value.X, ownBase.Value.Y) <= 900f * 900f;
        var endsNearEnemyBase = DistanceSquared(last.X, last.Y, enemyBase.Value.X, enemyBase.Value.Y) <= 900f * 900f;

        if (startsNearOwnBase && endsNearEnemyBase)
        {
            AddSegment(corridor, 0, corridor.Waypoints.Count, carryingIntel: false, segments);
            return segments.Count > 0;
        }

        if (startsNearEnemyBase && endsNearOwnBase)
        {
            AddSegment(corridor, 0, corridor.Waypoints.Count, carryingIntel: true, segments);
            return segments.Count > 0;
        }

        if (TryFindCaptureTheFlagSplit(level, corridor, out var splitIndex))
        {
            AddSegment(corridor, 0, splitIndex + 1, carryingIntel: false, segments);
            AddSegment(corridor, splitIndex, corridor.Waypoints.Count, carryingIntel: true, segments);
            return segments.Count > 0;
        }

        return false;
    }

    private static void AddSegment(
        BotNavigationAuthoredCorridorEntry corridor,
        int startIndex,
        int endIndex,
        bool carryingIntel,
        List<BotBrainObjectiveTapeSegment> segments)
    {
        var samples = BuildSamplesFromCorridor(corridor, startIndex, endIndex, carryingIntel);
        if (samples.Count >= 2)
        {
            segments.Add(new BotBrainObjectiveTapeSegment
            {
                RequiresCarryingIntel = carryingIntel,
                Samples = samples,
            });
        }
    }

    private static bool TryFindCaptureTheFlagSplit(
        SimpleLevel level,
        BotNavigationAuthoredCorridorEntry corridor,
        out int splitIndex)
    {
        splitIndex = -1;
        var opposingTeam = corridor.Team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        var enemyBase = level.GetIntelBase(opposingTeam);
        var ownBase = level.GetIntelBase(corridor.Team);
        if (!enemyBase.HasValue || !ownBase.HasValue)
        {
            return false;
        }

        var bestEnemyDistance = float.PositiveInfinity;
        var bestEnemyIndex = -1;
        for (var i = 0; i < corridor.Waypoints.Count; i += 1)
        {
            var waypoint = corridor.Waypoints[i];
            var enemyDistance = DistanceSquared(waypoint.X, waypoint.Y, enemyBase.Value.X, enemyBase.Value.Y);
            if (enemyDistance < bestEnemyDistance)
            {
                bestEnemyDistance = enemyDistance;
                bestEnemyIndex = i;
            }
        }

        if (bestEnemyIndex <= 0 || bestEnemyIndex >= corridor.Waypoints.Count - 2)
        {
            return false;
        }

        var last = corridor.Waypoints[^1];
        var endsNearOwnBase = DistanceSquared(last.X, last.Y, ownBase.Value.X, ownBase.Value.Y) <= 900f * 900f;
        var reachesEnemyBase = bestEnemyDistance <= 240f * 240f;
        if (!reachesEnemyBase || !endsNearOwnBase)
        {
            return false;
        }

        splitIndex = bestEnemyIndex;
        return true;
    }

    private static List<BotBrainObjectiveTapeSample> BuildSamplesFromCorridor(
        BotNavigationAuthoredCorridorEntry corridor,
        int startIndex,
        int endIndex,
        bool carryingIntel)
    {
        var samples = new List<BotBrainObjectiveTapeSample>(Math.Max(0, endIndex - startIndex));
        for (var i = startIndex; i < endIndex; i += 1)
        {
            var waypoint = corridor.Waypoints[i];
            var previous = i > 0 ? corridor.Waypoints[i - 1] : waypoint;
            var next = i + 1 < corridor.Waypoints.Count ? corridor.Waypoints[i + 1] : waypoint;
            var dx = next.X - waypoint.X;
            var dy = next.Y - waypoint.Y;
            var previousDy = waypoint.Y - previous.Y;
            samples.Add(new BotBrainObjectiveTapeSample
            {
                Tick = (i - startIndex) * 6,
                X = waypoint.X,
                Y = waypoint.Y,
                Bottom = waypoint.Y + 24f,
                IsGrounded = waypoint.IsGrounded,
                MoveDirection = MathF.Abs(dx) <= 8f ? 0f : dx > 0f ? 1f : -1f,
                Jump = waypoint.IsGrounded && (!next.IsGrounded || dy < -8f || previousDy < -8f),
                DropDown = false,
                IsCarryingIntel = carryingIntel,
            });
        }

        return samples;
    }

    private static float DistanceSquared(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return (dx * dx) + (dy * dy);
    }

    public static string UpsertTape(SimpleLevel level, BotBrainObjectiveTapeEntry tape)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(tape);

        var path = ResolvePath(level.Name, level.MapAreaIndex);
        BotBrainObjectiveTapeAsset asset;
        if (File.Exists(path))
        {
            asset = JsonSerializer.Deserialize<BotBrainObjectiveTapeAsset>(File.ReadAllText(path), SerializerOptions)
                ?? new BotBrainObjectiveTapeAsset();
        }
        else
        {
            asset = new BotBrainObjectiveTapeAsset();
        }

        asset.LevelName = level.Name;
        asset.MapAreaIndex = level.MapAreaIndex;
        var existingIndex = asset.Tapes.FindIndex(existing =>
            string.Equals(existing.Name, tape.Name, StringComparison.OrdinalIgnoreCase)
            && existing.Team == tape.Team
            && existing.PlayerClass == tape.PlayerClass);
        if (existingIndex >= 0)
        {
            asset.Tapes[existingIndex] = tape;
        }
        else
        {
            asset.Tapes.Add(tape);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(asset, SerializerOptions));
        return path;
    }

    public static string ResolvePath(string levelName, int mapAreaIndex)
    {
        var fileName = $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex).ToString(CultureInfo.InvariantCulture)}.botbrain-tapes.json";
        return ContentRoot.GetPath(TapeDirectoryName, fileName);
    }

    private static string SanitizeFileToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unnamed";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Trim())
        {
            builder.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? char.ToLowerInvariant(c) : '_');
        }

        return builder.Length == 0 ? "unnamed" : builder.ToString();
    }
}
