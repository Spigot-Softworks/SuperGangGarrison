using System.IO;

namespace OpenGarrison.Protocol;

public static partial class ProtocolCodec
{
    private static AudioPacket ReadAudioPacket(BinaryReader reader)
    {
        var sequence = reader.ReadUInt32();
        var count = reader.ReadByte();
        if (count is < 1 or > AudioWireFormat.HistoryFrames)
            throw new InvalidDataException("Invalid audio history length.");
        var frames = new byte[count][];
        for (var i = 0; i < count; i++)
        {
            var length = reader.ReadUInt16();
            if (length is < 1 or > AudioWireFormat.MaxFrameBytes)
                throw new InvalidDataException("Invalid audio frame size.");
            frames[i] = reader.ReadBytes(length);
            if (frames[i].Length != length) throw new EndOfStreamException();
        }
        return new AudioPacket(sequence, frames);
    }

    private static void WriteAudioPacket(BinaryWriter writer, AudioPacket packet)
    {
        if (!AudioWireFormat.IsValid(packet)) throw new InvalidDataException("Invalid audio packet.");
        writer.Write(packet.Sequence);
        writer.Write((byte)packet.Frames.Length);
        foreach (var frame in packet.Frames)
        {
            writer.Write((ushort)frame.Length);
            writer.Write(frame);
        }
    }

    private static AudioRelayMessage ReadAudioRelay(BinaryReader reader)
    {
        var slot = reader.ReadByte();
        var streamId = reader.ReadUInt32();
        var name = ReadString(reader, AudioWireFormat.MaxNameBytes);
        var team = reader.ReadByte();
        if (streamId == 0 || team > 2) throw new InvalidDataException("Invalid audio identity.");
        return new AudioRelayMessage(slot, streamId, slot == 0 ? AudioWireFormat.JukeboxName : name, team, ReadAudioPacket(reader));
    }

    private static void WriteAudioRelay(BinaryWriter writer, AudioRelayMessage message)
    {
        if (message.StreamId == 0 || message.Team > 2) throw new InvalidDataException("Invalid audio identity.");
        writer.Write(message.SpeakerSlot);
        writer.Write(message.StreamId);
        WriteString(writer, message.IsJukebox ? AudioWireFormat.JukeboxName : message.SpeakerName, AudioWireFormat.MaxNameBytes, nameof(message.SpeakerName));
        writer.Write(message.Team);
        WriteAudioPacket(writer, message.Packet);
    }
}
