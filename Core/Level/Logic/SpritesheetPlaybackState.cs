namespace OpenGarrison.Core;

public sealed class SpritesheetPlaybackState
{
    public SpritesheetPlaybackState(int roomObjectCount)
    {
        var length = Math.Max(0, roomObjectCount);
        CurrentFrame = new int[length];
        IsPlaying = new bool[length];
        PlaybackDirection = new int[length];
        FrameAccumulator = new float[length];
        Completed = new bool[length];
        for (var index = 0; index < length; index += 1)
        {
            PlaybackDirection[index] = 1;
        }
    }

    public int[] CurrentFrame { get; }

    public bool[] IsPlaying { get; }

    public int[] PlaybackDirection { get; }

    public float[] FrameAccumulator { get; }

    public bool[] Completed { get; }

    public void ResetFromConfiguration(SpritesheetPlaybackSet playbackSet)
    {
        Array.Clear(CurrentFrame, 0, CurrentFrame.Length);
        Array.Clear(FrameAccumulator, 0, FrameAccumulator.Length);
        Array.Clear(Completed, 0, Completed.Length);
        Array.Fill(IsPlaying, false);
        Array.Fill(PlaybackDirection, 1);
        for (var index = 0; index < playbackSet.Entries.Count; index += 1)
        {
            var entry = playbackSet.Entries[index];
            if (entry.RoomObjectIndex < 0 || entry.RoomObjectIndex >= IsPlaying.Length)
            {
                continue;
            }

            IsPlaying[entry.RoomObjectIndex] = entry.Configuration.Autostart;
            CurrentFrame[entry.RoomObjectIndex] = 0;
            PlaybackDirection[entry.RoomObjectIndex] = 1;
        }
    }
}
