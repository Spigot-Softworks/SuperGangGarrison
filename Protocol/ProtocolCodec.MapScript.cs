namespace OpenGarrison.Protocol;

public static partial class ProtocolCodec
{
    private static void WriteMapScriptObjectStates(BinaryWriter writer, IReadOnlyList<SnapshotMapScriptObjectState> states)
    {
        if (states.Count > ushort.MaxValue) throw new InvalidDataException("Too many scripted map objects.");
        writer.Write((ushort)states.Count);
        foreach (var item in states) { writer.Write(item.Index); writer.Write(item.Active); }
    }

    private static SnapshotMapScriptObjectState[] ReadMapScriptObjectStates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        if (count * 5 > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("Truncated scripted map objects.");
        var states = new SnapshotMapScriptObjectState[count];
        for (var index = 0; index < count; index++)
        {
            var objectIndex = reader.ReadInt32();
            if (objectIndex < 0) throw new InvalidDataException("Invalid scripted map object index.");
            states[index] = new(objectIndex, reader.ReadBoolean());
        }
        return states;
    }
}
