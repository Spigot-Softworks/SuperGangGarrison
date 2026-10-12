namespace OpenGarrison.Protocol;

public static partial class ProtocolCodec
{
    private static void WriteDamageableZoneStates(BinaryWriter writer, IReadOnlyList<SnapshotDamageableZoneState> states)
    {
        var count = Math.Min(states.Count, 1024);
        writer.Write((ushort)count);
        for (var i = 0; i < count; i++)
        {
            writer.Write(states[i].Index);
            writer.Write(states[i].Health);
            writer.Write(states[i].Revision);
            writer.Write(states[i].StateAgeTicks);
        }
    }

    private static IReadOnlyList<SnapshotDamageableZoneState> ReadDamageableZoneStates(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        if (count > 1024) throw new InvalidDataException("Too many damageable zones in snapshot.");
        var states = new SnapshotDamageableZoneState[count];
        for (var i = 0; i < count; i++) states[i] = new(reader.ReadUInt16(), reader.ReadSingle(), reader.ReadUInt32(), reader.ReadInt32());
        return states;
    }
}
