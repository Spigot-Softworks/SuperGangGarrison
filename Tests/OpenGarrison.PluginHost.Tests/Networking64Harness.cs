using System.Buffers.Binary;

namespace OpenGarrison.PluginHost.Tests;

internal enum Networking64DeliveryMode : byte
{
    ReliableOrdered = 1,
    ReliableUnordered = 2,
    LastWins = 3,
}

internal enum Networking64DecodeFailureKind
{
    None,
    NeedMoreData,
    InvalidMagic,
    UnsupportedProtocolVersion,
    InvalidDeliveryMode,
    FrameTooLarge,
    InvalidLength,
}

internal enum Networking64TransportEventKind
{
    Accepted,
    Dropped,
    Duplicated,
    Truncated,
    Backpressured,
    StreamReset,
}

internal sealed record Networking64Frame(
    int StreamId,
    ulong Sequence,
    Networking64DeliveryMode Delivery,
    byte[] Payload);

internal sealed record Networking64TransportPacket(
    int StreamId,
    ulong Sequence,
    byte[] Bytes);

internal sealed record Networking64TransportEvent(
    Networking64TransportEventKind Kind,
    int StreamId,
    ulong Sequence,
    int AffectedPacketCount = 1);

internal sealed class Networking64FaultScript
{
    public HashSet<ulong> DropSequences { get; } = [];

    public HashSet<ulong> DuplicateSequences { get; } = [];

    public Dictionary<ulong, int> TruncateToBytes { get; } = [];
}

internal static class Networking64FrameCodec
{
    public const byte ProtocolVersion = 64;
    public const int HeaderLength = 20;
    public const int MaxPayloadLength = 64 * 1024;

    private const ushort Magic = 0x6434;

    public static byte[] Encode(Networking64Frame frame)
    {
        if (frame.Payload.Length > MaxPayloadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(frame), "The test frame exceeds the protocol-64 payload limit.");
        }

        var encoded = new byte[HeaderLength + frame.Payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(0, 2), Magic);
        encoded[2] = ProtocolVersion;
        encoded[3] = (byte)frame.Delivery;
        BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(4, 4), frame.StreamId);
        BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(8, 8), frame.Sequence);
        BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(16, 4), frame.Payload.Length);
        frame.Payload.CopyTo(encoded.AsSpan(HeaderLength));
        return encoded;
    }

    public static bool TryDecode(
        ReadOnlySpan<byte> encoded,
        out Networking64Frame? frame,
        out Networking64DecodeFailureKind failure)
    {
        frame = null;
        failure = Networking64DecodeFailureKind.None;

        if (encoded.Length < HeaderLength)
        {
            failure = Networking64DecodeFailureKind.NeedMoreData;
            return false;
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(encoded[..2]) != Magic)
        {
            failure = Networking64DecodeFailureKind.InvalidMagic;
            return false;
        }

        if (encoded[2] != ProtocolVersion)
        {
            failure = Networking64DecodeFailureKind.UnsupportedProtocolVersion;
            return false;
        }

        var delivery = (Networking64DeliveryMode)encoded[3];
        if (!Enum.IsDefined(delivery))
        {
            failure = Networking64DecodeFailureKind.InvalidDeliveryMode;
            return false;
        }

        var streamId = BinaryPrimitives.ReadInt32LittleEndian(encoded.Slice(4, 4));
        var sequence = BinaryPrimitives.ReadUInt64LittleEndian(encoded.Slice(8, 8));
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(encoded.Slice(16, 4));
        if (payloadLength < 0)
        {
            failure = Networking64DecodeFailureKind.InvalidLength;
            return false;
        }

        if (payloadLength > MaxPayloadLength)
        {
            failure = Networking64DecodeFailureKind.FrameTooLarge;
            return false;
        }

        var frameLength = HeaderLength + payloadLength;
        if (encoded.Length < frameLength)
        {
            failure = Networking64DecodeFailureKind.NeedMoreData;
            return false;
        }

        frame = new Networking64Frame(
            streamId,
            sequence,
            delivery,
            encoded.Slice(HeaderLength, payloadLength).ToArray());
        return true;
    }
}

internal sealed class Networking64InMemoryTransport
{
    private readonly Queue<Networking64TransportPacket> _pendingPackets = [];

    public Networking64InMemoryTransport(int maxPendingPackets, Networking64FaultScript? faultScript = null)
    {
        if (maxPendingPackets <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPendingPackets));
        }

        MaxPendingPackets = maxPendingPackets;
        FaultScript = faultScript ?? new Networking64FaultScript();
    }

    public int MaxPendingPackets { get; }

    public Networking64FaultScript FaultScript { get; }

    public int PendingPacketCount => _pendingPackets.Count;

    public List<Networking64TransportEvent> Events { get; } = [];

    public bool TrySend(Networking64Frame frame)
    {
        if (FaultScript.DropSequences.Contains(frame.Sequence))
        {
            Events.Add(new Networking64TransportEvent(
                Networking64TransportEventKind.Dropped,
                frame.StreamId,
                frame.Sequence));
            return true;
        }

        var copies = FaultScript.DuplicateSequences.Contains(frame.Sequence) ? 2 : 1;
        if (_pendingPackets.Count + copies > MaxPendingPackets)
        {
            Events.Add(new Networking64TransportEvent(
                Networking64TransportEventKind.Backpressured,
                frame.StreamId,
                frame.Sequence,
                copies));
            return false;
        }

        var encoded = Networking64FrameCodec.Encode(frame);
        var packetBytes = encoded;
        if (FaultScript.TruncateToBytes.TryGetValue(frame.Sequence, out var requestedLength))
        {
            var truncatedLength = Math.Clamp(requestedLength, 0, encoded.Length);
            packetBytes = encoded[..truncatedLength];
            Events.Add(new Networking64TransportEvent(
                Networking64TransportEventKind.Truncated,
                frame.StreamId,
                frame.Sequence));
        }

        for (var copy = 0; copy < copies; copy += 1)
        {
            _pendingPackets.Enqueue(new Networking64TransportPacket(
                frame.StreamId,
                frame.Sequence,
                packetBytes.ToArray()));
        }

        Events.Add(new Networking64TransportEvent(
            Networking64TransportEventKind.Accepted,
            frame.StreamId,
            frame.Sequence,
            copies));

        if (copies > 1)
        {
            Events.Add(new Networking64TransportEvent(
                Networking64TransportEventKind.Duplicated,
                frame.StreamId,
                frame.Sequence,
                copies));
        }

        return true;
    }

    public void ReorderPendingPackets()
    {
        var packets = _pendingPackets.ToArray();
        Array.Reverse(packets);
        _pendingPackets.Clear();
        foreach (var packet in packets)
        {
            _pendingPackets.Enqueue(packet);
        }
    }

    public int ResetStream(int streamId)
    {
        var retainedPackets = _pendingPackets
            .Where(packet => packet.StreamId != streamId)
            .ToArray();
        var removedPacketCount = _pendingPackets.Count - retainedPackets.Length;

        _pendingPackets.Clear();
        foreach (var packet in retainedPackets)
        {
            _pendingPackets.Enqueue(packet);
        }

        Events.Add(new Networking64TransportEvent(
            Networking64TransportEventKind.StreamReset,
            streamId,
            Sequence: 0,
            AffectedPacketCount: removedPacketCount));
        return removedPacketCount;
    }

    public bool TryReceive(out Networking64TransportPacket packet)
    {
        if (_pendingPackets.Count > 0)
        {
            packet = _pendingPackets.Dequeue();
            return true;
        }

        packet = null!;
        return false;
    }
}

internal sealed class Networking64FrameReader
{
    private readonly List<byte> _buffer = [];

    public int BufferedByteCount => _buffer.Count;

    public void Append(ReadOnlySpan<byte> bytes)
    {
        for (var index = 0; index < bytes.Length; index += 1)
        {
            _buffer.Add(bytes[index]);
        }
    }

    public bool TryRead(
        out Networking64Frame? frame,
        out Networking64DecodeFailureKind failure)
    {
        frame = null;
        failure = Networking64DecodeFailureKind.None;

        if (_buffer.Count < Networking64FrameCodec.HeaderLength)
        {
            failure = Networking64DecodeFailureKind.NeedMoreData;
            return false;
        }

        var header = _buffer.Take(Networking64FrameCodec.HeaderLength).ToArray();
        var declaredPayloadLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(16, 4));
        if (declaredPayloadLength < 0)
        {
            failure = Networking64DecodeFailureKind.InvalidLength;
            _buffer.RemoveRange(0, Networking64FrameCodec.HeaderLength);
            return false;
        }

        if (declaredPayloadLength > Networking64FrameCodec.MaxPayloadLength)
        {
            failure = Networking64DecodeFailureKind.FrameTooLarge;
            _buffer.RemoveRange(0, Networking64FrameCodec.HeaderLength);
            return false;
        }

        var frameLength = Networking64FrameCodec.HeaderLength + declaredPayloadLength;
        if (_buffer.Count < frameLength)
        {
            failure = Networking64DecodeFailureKind.NeedMoreData;
            return false;
        }

        var encodedFrame = _buffer.Take(frameLength).ToArray();
        _buffer.RemoveRange(0, frameLength);

        if (Networking64FrameCodec.TryDecode(encodedFrame, out frame, out failure))
        {
            return true;
        }

        frame = null;
        return false;
    }

    public void Reset() => _buffer.Clear();
}

internal sealed class Networking64ReliableOrderedReceiver
{
    private readonly SortedDictionary<ulong, Networking64Frame> _pendingFrames = [];
    private ulong _nextSequence;

    public Networking64ReliableOrderedReceiver(ulong firstSequence = 1)
    {
        _nextSequence = firstSequence;
    }

    public int DuplicateCount { get; private set; }

    public int BufferedOutOfOrderCount { get; private set; }

    public ulong NextSequence => _nextSequence;

    public IReadOnlyList<ulong> MissingSequences
    {
        get
        {
            if (_pendingFrames.Count == 0)
            {
                return [];
            }

            var highestSequence = _pendingFrames.Keys.Max();
            var missing = new List<ulong>();
            for (var sequence = _nextSequence; sequence < highestSequence; sequence += 1)
            {
                if (!_pendingFrames.ContainsKey(sequence))
                {
                    missing.Add(sequence);
                }
            }

            return missing;
        }
    }

    public IReadOnlyList<Networking64Frame> Accept(Networking64Frame frame)
    {
        if (frame.Sequence < _nextSequence || _pendingFrames.ContainsKey(frame.Sequence))
        {
            DuplicateCount += 1;
            return [];
        }

        if (frame.Sequence > _nextSequence)
        {
            _pendingFrames.Add(frame.Sequence, frame);
            BufferedOutOfOrderCount += 1;
            return [];
        }

        var delivered = new List<Networking64Frame> { frame };
        _nextSequence += 1;
        while (_pendingFrames.TryGetValue(_nextSequence, out var pendingFrame))
        {
            _pendingFrames.Remove(_nextSequence);
            delivered.Add(pendingFrame);
            _nextSequence += 1;
        }

        return delivered;
    }
}

internal sealed class Networking64ReliableUnorderedReceiver
{
    private readonly HashSet<ulong> _seenSequences = [];

    public int DuplicateCount { get; private set; }

    public IReadOnlyList<Networking64Frame> Accept(Networking64Frame frame)
    {
        if (!_seenSequences.Add(frame.Sequence))
        {
            DuplicateCount += 1;
            return [];
        }

        return [frame];
    }
}

internal sealed class Networking64LastWinsMailbox<T>
{
    private T? _value;
    private bool _hasValue;
    private ulong _latestSequence;

    public bool HasValue => _hasValue;

    public ulong LatestSequence => _latestSequence;

    public bool TryReplace(ulong sequence, T value)
    {
        if (_hasValue && sequence <= _latestSequence)
        {
            return false;
        }

        _latestSequence = sequence;
        _value = value;
        _hasValue = true;
        return true;
    }

    public bool TryTake(out T value)
    {
        if (!_hasValue)
        {
            value = default!;
            return false;
        }

        value = _value!;
        _value = default;
        _hasValue = false;
        return true;
    }
}
