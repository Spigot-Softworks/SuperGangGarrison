using System.Text.Json;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class FirstPlayMessageCatalog
{
    // Exact Builder entities from cp_coldfront_js_message.ogmap, reordered only.
    // Embedded in the client so every distribution uses them on every map.
    public static IReadOnlyList<GameplayMessageMarker> Messages { get; } = Load();

    private static IReadOnlyList<GameplayMessageMarker> Load()
    {
        using var stream = typeof(FirstPlayMessageCatalog).Assembly.GetManifestResourceStream("OpenGarrison.FirstPlayMessages.json")
            ?? throw new InvalidOperationException("Missing first-play message templates.");
        using var json = JsonDocument.Parse(stream);
        return Array.AsReadOnly(json.RootElement.EnumerateArray().Select(entity =>
            GameplayMessageMetadata.FromProperties(entity.GetProperty("X").GetSingle(), entity.GetProperty("Y").GetSingle(),
                entity.GetProperty("XScale").GetSingle(), entity.GetProperty("YScale").GetSingle(),
                entity.GetProperty("Properties").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!))).ToArray());
    }
}

internal sealed class FirstPlayHintSequence(bool alreadyShown)
{
    public bool Started { get; private set; } = alreadyShown;
    public bool Finished { get; private set; } = alreadyShown;
    public bool Visible { get; private set; }
    public int Index { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public GameplayMessageMarker Marker => FirstPlayMessageCatalog.Messages[Index];

    // Returns true exactly once, when a playable session actually starts the tips.
    public bool Update(float deltaSeconds, bool canPresent)
    {
        Visible = false;
        if (Finished || !canPresent) return false;
        var justStarted = !Started;
        Started = true;
        if (!justStarted && float.IsFinite(deltaSeconds)) ElapsedSeconds += Math.Clamp(deltaSeconds, 0f, 0.1f);
        var endSeconds = Marker.OnEndEffects.HasFlag(GameplayMessageOnEndEffects.FadeOut) ? Marker.OnEndSeconds : 0f;
        if (ElapsedSeconds >= Marker.DurationSeconds + endSeconds)
        {
            ElapsedSeconds = 0f;
            if (Index + 1 == FirstPlayMessageCatalog.Messages.Count) Finished = true;
            else Index++;
        }
        Visible = !Finished;
        return justStarted;
    }

    public void LeaveSession()
    {
        // Once started, reconnecting or launching a different mode must not replay it.
        if (Started) Finished = true;
        Visible = false;
    }
}
