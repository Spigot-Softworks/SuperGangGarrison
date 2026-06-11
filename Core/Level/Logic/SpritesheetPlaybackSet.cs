namespace OpenGarrison.Core;

public readonly record struct SpritesheetPlaybackEntry(
    int RoomObjectIndex,
    int StartInputNodeIndex,
    int StopInputNodeIndex,
    int NextFrameInputNodeIndex,
    SpritesheetConfiguration Configuration);

public sealed class SpritesheetPlaybackSet
{
    public static SpritesheetPlaybackSet Empty { get; } = new(Array.Empty<SpritesheetPlaybackEntry>());

    public SpritesheetPlaybackSet(IReadOnlyList<SpritesheetPlaybackEntry> entries)
    {
        Entries = entries ?? Array.Empty<SpritesheetPlaybackEntry>();
    }

    public IReadOnlyList<SpritesheetPlaybackEntry> Entries { get; }

    public bool HasEntries => Entries.Count > 0;
}
