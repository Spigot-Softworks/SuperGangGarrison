using System.Globalization;

namespace OpenGarrison.Core;

/// <summary>A server-owned round outcome driven by a map logic condition.</summary>
public sealed record MapRoundEndDefinition(
    int NodeIndex, PlayerTeam? Winner, float DelaySeconds, float CountdownSeconds,
    bool RequireContinuousSignal, bool ShowCountdown, string TickSound, int OnCompleteNodeIndex);

public static class MapRoundEndMetadata
{
    public const string EntityType = "logicRoundEnd";
    public const string WinnerPropertyKey = "winnerTeam";
    public const string DelayPropertyKey = "countdownDelay";
    public const string ContinuousPropertyKey = "requireContinuousSignal";
    public const string ShowPropertyKey = "showCountdown";
    public const string TickSoundPropertyKey = "tickSound";
    public const string OnCompletePropertyKey = "onComplete";
    public const string DefaultTickSound = "CountDown1Snd";
    public const string DefaultProperties = "logicInput=;winnerTeam=red;countdownDelay=0;countdownSeconds=8;requireContinuousSignal=true;showCountdown=true;tickSound=CountDown1Snd;onComplete=;logicKey=";

    public static bool IsEntityType(string? type) =>
        string.Equals(type, EntityType, StringComparison.OrdinalIgnoreCase);

    public static PlayerTeam? ParseWinner(string? value) => value?.ToLowerInvariant() switch
    {
        "blue" => PlayerTeam.Blue,
        "draw" => null,
        _ => PlayerTeam.Red,
    };

    public static string CycleWinner(string? value) => value?.ToLowerInvariant() switch
    {
        "red" => "blue",
        "blue" => "draw",
        _ => "red",
    };

    public static float ParseSeconds(string? value, float fallback = 0f) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && float.IsFinite(seconds)
            ? Math.Clamp(seconds, 0f, 3600f) : fallback;

    public static IReadOnlyList<MapRoundEndDefinition> Import(IReadOnlyList<MapImportedEntity> entities, MapLogicGraph graph)
    {
        var result = new List<MapRoundEndDefinition>();
        foreach (var entity in entities)
        {
            if (!IsEntityType(entity.Type) || entity.Properties is not { } properties) continue;
            string Read(string key, string fallback = "") => properties.TryGetValue(key, out var value) ? value : fallback;
            if (!graph.TryGetNodeIndex(Read(MapLogicMetadata.LogicKeyPropertyKey), out var nodeIndex)) continue;
            result.Add(new(nodeIndex, ParseWinner(Read(WinnerPropertyKey)),
                ParseSeconds(Read(DelayPropertyKey)), ParseSeconds(Read(MapLogicMetadata.CountdownSecondsPropertyKey), 8f),
                !Read(ContinuousPropertyKey, "true").Equals("false", StringComparison.OrdinalIgnoreCase),
                !Read(ShowPropertyKey, "true").Equals("false", StringComparison.OrdinalIgnoreCase),
                Read(TickSoundPropertyKey, DefaultTickSound).Trim(),
                MapLogicGraphImporter.ResolveLogicSignalNodeIndex(graph, Read(OnCompletePropertyKey))));
        }
        return result;
    }
}
