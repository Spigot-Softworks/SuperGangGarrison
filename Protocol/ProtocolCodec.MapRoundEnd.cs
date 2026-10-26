namespace OpenGarrison.Protocol;

public static partial class ProtocolCodec
{
    private static void WriteMapRoundEndStates(BinaryWriter writer, IReadOnlyList<SnapshotMapRoundEndState> states)
    {
        if (states.Count > ushort.MaxValue) throw new InvalidDataException("Too many round end countdowns.");
        writer.Write((ushort)states.Count);
        foreach (var state in states)
        {
            writer.Write(state.Index);
            writer.Write(state.RemainingTicks);
        }
    }

    private static IReadOnlyList<SnapshotMapRoundEndState> ReadMapRoundEndStates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        if (reader.BaseStream.Length - reader.BaseStream.Position < count * 8L)
            throw new InvalidDataException("Truncated round end countdown states.");
        var states = new SnapshotMapRoundEndState[count];
        for (var index = 0; index < count; index++)
        {
            var nodeIndex = reader.ReadInt32();
            var ticks = reader.ReadInt32();
            if (nodeIndex < 0 || ticks < 0) throw new InvalidDataException("Invalid round end countdown state.");
            states[index] = new(nodeIndex, ticks);
        }
        return states;
    }
}
