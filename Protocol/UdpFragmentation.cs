using System.Buffers.Binary;
using System.Collections.Generic;

namespace OpenGarrison.Protocol;

/// <summary>Application-level UDP fragmentation for legacy protocol datagrams.</summary>
public static class UdpFragmentation
{
    public const int RecommendedDatagramBytes = 1200;
    public const int HeaderBytes = 32;
    public const int MaxDatagramBytes = 1400;
    public const int MaxMessageBytes = 16 * 1024 * 1024;
    public const int MaxChunkCount = ushort.MaxValue;
    public const int MaxNackIndices = (RecommendedDatagramBytes - HeaderBytes) / sizeof(ushort);
    public const int MaxRepairChunksPerNack = 8;
    private const uint Magic = 0x3144474F; // "OGD1"
    private const byte Version = 1;
    private const byte DataKind = 1;
    private const byte NackKind = 2;
    private const byte MtuProbeKind = 3;
    private const byte MtuAckKind = 4;

    public readonly record struct DecodedFrame(
        bool IsNack,
        bool IsMtuProbe,
        bool IsMtuAck,
        ulong MessageId,
        int TotalLength,
        int ChunkSize,
        int ChunkIndex,
        int ChunkCount,
        ushort Generation,
        uint Checksum,
        byte[] Body);

    public static bool IsFramed(ReadOnlySpan<byte> payload)
        => payload.Length >= sizeof(uint) && BinaryPrimitives.ReadUInt32LittleEndian(payload) == Magic;

    public static IReadOnlyList<byte[]> Fragment(byte[] payload, ulong messageId, int datagramLimit = RecommendedDatagramBytes, ushort generation = 0)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (messageId == 0) throw new ArgumentOutOfRangeException(nameof(messageId));
        if (datagramLimit is < (HeaderBytes + 1) or > MaxDatagramBytes) throw new ArgumentOutOfRangeException(nameof(datagramLimit));
        if (payload.Length == 0 || payload.Length > MaxMessageBytes) throw new ArgumentOutOfRangeException(nameof(payload));
        if (payload.Length <= datagramLimit) return [payload];

        var chunkSize = datagramLimit - HeaderBytes;
        var chunkCount = (payload.Length + chunkSize - 1) / chunkSize;
        if (chunkCount > MaxChunkCount) throw new ArgumentOutOfRangeException(nameof(payload));
        var checksum = Checksum(payload);
        var frames = new byte[chunkCount][];
        for (var index = 0; index < chunkCount; index++)
        {
            var offset = index * chunkSize;
            var count = Math.Min(chunkSize, payload.Length - offset);
            var frame = new byte[HeaderBytes + count];
            WriteHeader(frame, DataKind, messageId, payload.Length, chunkSize, index, chunkCount, checksum, generation);
            payload.AsSpan(offset, count).CopyTo(frame.AsSpan(HeaderBytes));
            frames[index] = frame;
        }

        return frames;
    }

    public static byte[] CreateNack(ulong messageId, int totalLength, int chunkSize, int chunkCount, uint checksum, ReadOnlySpan<ushort> missingIndices, ushort generation = 0)
    {
        if (messageId == 0 || totalLength is <= 0 or > MaxMessageBytes || chunkSize is <= 0 or > (MaxDatagramBytes - HeaderBytes)
            || chunkCount is <= 0 or > MaxChunkCount || missingIndices.Length is <= 0 or > MaxNackIndices)
            throw new ArgumentOutOfRangeException(nameof(missingIndices));
        var frame = new byte[HeaderBytes + missingIndices.Length * sizeof(ushort)];
        WriteHeader(frame, NackKind, messageId, totalLength, chunkSize, 0, chunkCount, checksum, generation);
        for (var i = 0; i < missingIndices.Length; i++)
        {
            if (missingIndices[i] >= chunkCount) throw new ArgumentOutOfRangeException(nameof(missingIndices));
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(HeaderBytes + i * sizeof(ushort)), missingIndices[i]);
        }

        return frame;
    }

    public static byte[] CreateMtuProbe(ulong nonce, int datagramBytes)
    {
        if (nonce == 0 || datagramBytes is <= HeaderBytes or > MaxDatagramBytes)
            throw new ArgumentOutOfRangeException(nameof(datagramBytes));
        var frame = new byte[datagramBytes];
        WriteHeader(frame, MtuProbeKind, nonce, datagramBytes, 1, 0, 1, 0);
        return frame;
    }

    public static byte[] CreateMtuAck(ulong nonce, int datagramBytes)
    {
        if (nonce == 0 || datagramBytes is <= HeaderBytes or > MaxDatagramBytes)
            throw new ArgumentOutOfRangeException(nameof(datagramBytes));
        var frame = new byte[HeaderBytes];
        WriteHeader(frame, MtuAckKind, nonce, datagramBytes, 1, 0, 1, 0);
        return frame;
    }

    public static bool TryDecode(ReadOnlySpan<byte> frame, out DecodedFrame decoded)
    {
        decoded = default;
        if (frame.Length < HeaderBytes || BinaryPrimitives.ReadUInt32LittleEndian(frame) != Magic || frame[4] != Version)
            return false;
        var kind = frame[5];
        if (kind is not (DataKind or NackKind or MtuProbeKind or MtuAckKind) || BinaryPrimitives.ReadUInt16LittleEndian(frame[6..]) != 0)
            return false;
        var messageId = BinaryPrimitives.ReadUInt64LittleEndian(frame[8..]);
        var totalLength = BinaryPrimitives.ReadInt32LittleEndian(frame[16..]);
        var chunkSize = BinaryPrimitives.ReadUInt16LittleEndian(frame[20..]);
        var chunkIndex = BinaryPrimitives.ReadUInt16LittleEndian(frame[22..]);
        var chunkCount = BinaryPrimitives.ReadUInt16LittleEndian(frame[24..]);
        var generation = BinaryPrimitives.ReadUInt16LittleEndian(frame[26..]);
        var checksum = BinaryPrimitives.ReadUInt32LittleEndian(frame[28..]);
        if (messageId == 0 || chunkSize == 0 || chunkCount == 0 || chunkCount > MaxChunkCount)
            return false;

        var body = frame[HeaderBytes..];
        if (kind is MtuProbeKind or MtuAckKind)
        {
            if (totalLength is <= HeaderBytes or > MaxDatagramBytes || chunkSize != 1 || chunkIndex != 0 || chunkCount != 1 || checksum != 0)
                return false;
            if (kind == MtuProbeKind ? frame.Length != totalLength : body.Length != 0) return false;
        }
        else if (totalLength <= 0 || totalLength > MaxMessageBytes || chunkSize > MaxDatagramBytes - HeaderBytes
            || (totalLength + chunkSize - 1) / chunkSize != chunkCount)
        {
            return false;
        }
        else if (kind == DataKind)
        {
            if (chunkIndex >= chunkCount) return false;
            var expected = chunkIndex == chunkCount - 1 ? totalLength - chunkIndex * chunkSize : chunkSize;
            if (body.Length != expected || expected <= 0) return false;
        }
        else
        {
            if (chunkIndex != 0 || body.Length == 0 || body.Length % sizeof(ushort) != 0
                || body.Length > RecommendedDatagramBytes - HeaderBytes)
                return false;
            for (var offset = 0; offset < body.Length; offset += sizeof(ushort))
                if (BinaryPrimitives.ReadUInt16LittleEndian(body[offset..]) >= chunkCount) return false;
        }

        decoded = new DecodedFrame(kind == NackKind, kind == MtuProbeKind, kind == MtuAckKind,
            messageId, totalLength, chunkSize, chunkIndex, chunkCount, generation, checksum, body.ToArray());
        return true;
    }

    public static uint Checksum(ReadOnlySpan<byte> payload)
    {
        var hash = 2166136261u;
        foreach (var value in payload) hash = (hash ^ value) * 16777619u;
        return hash;
    }

    private static void WriteHeader(Span<byte> frame, byte kind, ulong id, int totalLength, int chunkSize, int index, int count, uint checksum, ushort generation = 0)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(frame, Magic);
        frame[4] = Version;
        frame[5] = kind;
        BinaryPrimitives.WriteUInt16LittleEndian(frame[6..], 0);
        BinaryPrimitives.WriteUInt64LittleEndian(frame[8..], id);
        BinaryPrimitives.WriteInt32LittleEndian(frame[16..], totalLength);
        BinaryPrimitives.WriteUInt16LittleEndian(frame[20..], checked((ushort)chunkSize));
        BinaryPrimitives.WriteUInt16LittleEndian(frame[22..], checked((ushort)index));
        BinaryPrimitives.WriteUInt16LittleEndian(frame[24..], checked((ushort)count));
        BinaryPrimitives.WriteUInt16LittleEndian(frame[26..], generation);
        BinaryPrimitives.WriteUInt32LittleEndian(frame[28..], checksum);
    }
}

/// <summary>Bounded, per-destination UDP payload ceiling discovery. Probe replies must match both nonce and size.</summary>
public sealed class UdpPathMtuDiscovery
{
    public const int InitialDatagramBytes = UdpFragmentation.RecommendedDatagramBytes;
    public const int SmallerConfirmationDatagramBytes = 548;
    public const int MaxPeers = 256;
    public const long PeerLifetimeMilliseconds = 60_000;
    public const long ProbeTimeoutMilliseconds = 1_500;
    public const long ProbeIntervalMilliseconds = 5_000;
    public const long RevalidationIntervalMilliseconds = 1_000;
    private static readonly int[] LargerCandidates = [1240, 1280, 1320, 1360, UdpFragmentation.MaxDatagramBytes];
    private readonly Dictionary<string, PeerState> _peers = new(StringComparer.Ordinal);
    private ulong _nextNonce = unchecked((ulong)Random.Shared.NextInt64(1, long.MaxValue));

    public int GetDatagramLimit(string peerKey, long nowMilliseconds)
    {
        var state = GetOrCreate(peerKey, nowMilliseconds);
        state.LastSeenMilliseconds = nowMilliseconds;
        return state.NeedsRevalidationFallback
            ? InitialDatagramBytes
            : state.ConfirmedDatagramBytes;
    }

    /// <summary>Returns a non-blocking probe when this peer's bounded discovery timer is due.</summary>
    public bool TryCreateProbe(string peerKey, long nowMilliseconds, out byte[]? packet)
    {
        var state = GetOrCreate(peerKey, nowMilliseconds);
        state.LastSeenMilliseconds = nowMilliseconds;
        packet = null;
        if (state.PendingNonce != 0)
        {
            if (nowMilliseconds - state.PendingSinceMilliseconds < ProbeTimeoutMilliseconds) return false;
            var missedSize = state.PendingDatagramBytes;
            state.PendingNonce = 0;
            state.PendingDatagramBytes = 0;
            if (missedSize > InitialDatagramBytes
                && missedSize == state.ConfirmedDatagramBytes)
            {
                // The confirmed ceiling itself stopped answering probes. Keep
                // application traffic at the known-good base size while we
                // confirm that the smaller path is still reachable.
                state.NeedsRevalidationFallback = true;
                state.MissedBaseProbes = 0;
                state.NextLargerCandidateRetryAtMilliseconds = 0;
                state.NextLargerCandidate = 0;
                state.NextProbeAtMilliseconds = nowMilliseconds;
            }
            else if (missedSize == InitialDatagramBytes)
            {
                state.MissedBaseProbes++;
                state.NextProbeAtMilliseconds = state.MissedBaseProbes >= 2
                    ? nowMilliseconds
                    : nowMilliseconds + (state.NeedsRevalidationFallback
                        ? ProbeTimeoutMilliseconds
                        : ProbeIntervalMilliseconds);
            }
            else if (missedSize == SmallerConfirmationDatagramBytes)
            {
                // The smaller confirmation failed too; keep the prior confirmed ceiling.
                state.MissedBaseProbes = 0;
                state.NextProbeAtMilliseconds = nowMilliseconds + (state.NeedsRevalidationFallback
                    ? ProbeTimeoutMilliseconds
                    : PeerLifetimeMilliseconds);
            }
            else
            {
                // A failed upward probe never changes the confirmed path ceiling.
                state.NextLargerCandidate = LargerCandidates.Length;
                if (state.ConfirmedDatagramBytes > InitialDatagramBytes)
                {
                    // Recheck the last known-good size immediately. If it
                    // answers, keep using it; if it also fails, fall back to
                    // the confirmed base size without waiting for another
                    // discovery cycle.
                    state.NextLargerCandidateRetryAtMilliseconds = nowMilliseconds + PeerLifetimeMilliseconds;
                    state.NextProbeAtMilliseconds = nowMilliseconds;
                }
                else
                {
                    state.NextLargerCandidateRetryAtMilliseconds = 0;
                    state.NextProbeAtMilliseconds = nowMilliseconds + PeerLifetimeMilliseconds;
                }
            }
        }
        if (nowMilliseconds < state.NextProbeAtMilliseconds) return false;

        var size = state.MissedBaseProbes >= 2
            ? SmallerConfirmationDatagramBytes
            : state.NeedsRevalidationFallback
                ? InitialDatagramBytes
                : state.NextLargerCandidate < LargerCandidates.Length
                    ? LargerCandidates[state.NextLargerCandidate]
                    : state.ConfirmedDatagramBytes > InitialDatagramBytes
                        ? state.ConfirmedDatagramBytes
                        : InitialDatagramBytes;
        var nonce = NextNonce();
        state.PendingNonce = nonce;
        state.PendingSinceMilliseconds = nowMilliseconds;
        state.PendingDatagramBytes = size;
        packet = UdpFragmentation.CreateMtuProbe(nonce, size);
        return true;
    }

    /// <summary>Accepts only an ACK for this peer's active probe. The size result is direction-specific.</summary>
    public bool TryAcceptAck(string peerKey, UdpFragmentation.DecodedFrame frame, long nowMilliseconds, out int confirmedDatagramBytes)
    {
        confirmedDatagramBytes = InitialDatagramBytes;
        if (!frame.IsMtuAck || !_peers.TryGetValue(peerKey, out var state)
            || state.PendingNonce == 0 || frame.MessageId != state.PendingNonce
            || frame.TotalLength != state.PendingDatagramBytes
            || nowMilliseconds - state.PendingSinceMilliseconds > ProbeTimeoutMilliseconds)
            return false;

        var confirmedSize = state.PendingDatagramBytes;
        state.PendingNonce = 0;
        state.PendingDatagramBytes = 0;
        state.LastSeenMilliseconds = nowMilliseconds;
        if (confirmedSize == SmallerConfirmationDatagramBytes)
        {
            state.ConfirmedDatagramBytes = SmallerConfirmationDatagramBytes;
            state.MissedBaseProbes = 0;
            state.NeedsRevalidationFallback = false;
            state.NextLargerCandidateRetryAtMilliseconds = 0;
            state.NextLargerCandidate = LargerCandidates.Length;
            state.NextProbeAtMilliseconds = nowMilliseconds + PeerLifetimeMilliseconds;
        }
        else if (confirmedSize == InitialDatagramBytes)
        {
            state.MissedBaseProbes = 0;
            if (state.NeedsRevalidationFallback)
            {
                state.ConfirmedDatagramBytes = InitialDatagramBytes;
                state.NeedsRevalidationFallback = false;
                state.NextLargerCandidateRetryAtMilliseconds = 0;
                state.NextLargerCandidate = 0;
            }
            else
            {
                state.ConfirmedDatagramBytes = Math.Max(state.ConfirmedDatagramBytes, InitialDatagramBytes);
                if (state.ConfirmedDatagramBytes == InitialDatagramBytes)
                {
                    state.NextLargerCandidateRetryAtMilliseconds = 0;
                    state.NextLargerCandidate = 0;
                }
            }
            state.NextProbeAtMilliseconds = nowMilliseconds + ProbeIntervalMilliseconds;
        }
        else if (confirmedSize == state.ConfirmedDatagramBytes)
        {
            // This is a successful same-size revalidation, not a new upward
            // step. Keep the ceiling and schedule the next light probe sooner
            // than the broader candidate discovery cadence.
            if (state.NextLargerCandidateRetryAtMilliseconds != 0
                && nowMilliseconds >= state.NextLargerCandidateRetryAtMilliseconds)
            {
                state.NextLargerCandidate = FindNextLargerCandidateIndex(confirmedSize);
                state.NextLargerCandidateRetryAtMilliseconds = 0;
            }

            state.NextProbeAtMilliseconds = nowMilliseconds + RevalidationIntervalMilliseconds;
        }
        else
        {
            state.ConfirmedDatagramBytes = confirmedSize;
            state.NextLargerCandidate++;
            state.NextLargerCandidateRetryAtMilliseconds = 0;
            state.NextProbeAtMilliseconds = nowMilliseconds + RevalidationIntervalMilliseconds;
        }
        confirmedDatagramBytes = state.ConfirmedDatagramBytes;
        return true;
    }

    public void ClearPeer(string peerKey) => _peers.Remove(peerKey);

    private static int FindNextLargerCandidateIndex(int confirmedDatagramBytes)
    {
        for (var index = 0; index < LargerCandidates.Length; index++)
        {
            if (LargerCandidates[index] > confirmedDatagramBytes)
            {
                return index;
            }
        }

        return LargerCandidates.Length;
    }

    private PeerState GetOrCreate(string peerKey, long nowMilliseconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerKey);
        Expire(nowMilliseconds);
        if (_peers.TryGetValue(peerKey, out var state)) return state;
        if (_peers.Count >= MaxPeers)
        {
            string? oldestPeer = null;
            var oldestSeen = long.MaxValue;
            foreach (var pair in _peers)
                if (pair.Value.LastSeenMilliseconds < oldestSeen) { oldestPeer = pair.Key; oldestSeen = pair.Value.LastSeenMilliseconds; }
            if (oldestPeer is not null) _peers.Remove(oldestPeer);
        }
        state = new PeerState(nowMilliseconds);
        _peers.Add(peerKey, state);
        return state;
    }

    private void Expire(long nowMilliseconds)
    {
        var expired = new List<string>();
        foreach (var pair in _peers)
            if (nowMilliseconds - pair.Value.LastSeenMilliseconds >= PeerLifetimeMilliseconds) expired.Add(pair.Key);
        foreach (var peer in expired) _peers.Remove(peer);
    }

    private ulong NextNonce()
    {
        _nextNonce = unchecked(_nextNonce + 1);
        if (_nextNonce == 0) _nextNonce = 1;
        return _nextNonce;
    }

    private sealed class PeerState(long now)
    {
        public int ConfirmedDatagramBytes { get; set; } = InitialDatagramBytes;
        public int PendingDatagramBytes { get; set; }
        public ulong PendingNonce { get; set; }
        public long PendingSinceMilliseconds { get; set; }
        public long NextProbeAtMilliseconds { get; set; } = now;
        public long LastSeenMilliseconds { get; set; } = now;
        public int MissedBaseProbes { get; set; }
        public int NextLargerCandidate { get; set; } = LargerCandidates.Length;
        public long NextLargerCandidateRetryAtMilliseconds { get; set; }
        public bool NeedsRevalidationFallback { get; set; }
    }
}

/// <summary>Bounded peer-isolated receiver state. Times are monotonic milliseconds.</summary>
public sealed class UdpFragmentReassembler
{
    public const long AssemblyLifetimeMilliseconds = 5000;
    public const long NackIntervalMilliseconds = 120;
    public const int MaxAssemblies = 2048;
    public const int MaxAssembliesPerPeer = 128;
    public const int MaxBufferedBytes = 32 * 1024 * 1024;
    public const int MaxBufferedBytesPerPeer = 16 * 1024 * 1024;
    public const int MaxCompletedIds = 2048;

    private readonly Dictionary<(string Peer, ulong Id), Assembly> _assemblies = new();
    private readonly Dictionary<(string Peer, ulong Id), long> _completed = new();
    private readonly Queue<((string Peer, ulong Id) Key, long CompletedAt)> _completedOrder = new();
    private readonly long _assemblyLifetimeMilliseconds;
    private readonly int _maxAssemblies;
    private readonly int _maxAssembliesPerPeer;
    private readonly int _maxBufferedBytes;
    private readonly int _maxBufferedBytesPerPeer;
    private readonly int _maxCompletedIds;
    private int _bufferedBytes;

    public UdpFragmentReassembler(
        long assemblyLifetimeMilliseconds = AssemblyLifetimeMilliseconds,
        int maxAssemblies = MaxAssemblies,
        int maxAssembliesPerPeer = MaxAssembliesPerPeer,
        int maxBufferedBytes = MaxBufferedBytes,
        int maxBufferedBytesPerPeer = MaxBufferedBytesPerPeer,
        int maxCompletedIds = MaxCompletedIds)
    {
        if (assemblyLifetimeMilliseconds is <= 0 or > AssemblyLifetimeMilliseconds
            || maxAssemblies <= 0 || maxAssembliesPerPeer <= 0
            || maxBufferedBytes <= 0 || maxBufferedBytesPerPeer <= 0
            || maxCompletedIds <= 0)
            throw new ArgumentOutOfRangeException(nameof(assemblyLifetimeMilliseconds));
        _assemblyLifetimeMilliseconds = assemblyLifetimeMilliseconds;
        _maxAssemblies = maxAssemblies;
        _maxAssembliesPerPeer = maxAssembliesPerPeer;
        _maxBufferedBytes = maxBufferedBytes;
        _maxBufferedBytesPerPeer = maxBufferedBytesPerPeer;
        _maxCompletedIds = maxCompletedIds;
    }

    public int BufferedBytes => _bufferedBytes;

    public bool IsCurrentNack(string peerKey, ReadOnlySpan<byte> packet, long nowMilliseconds)
    {
        Expire(nowMilliseconds);
        if (!UdpFragmentation.TryDecode(packet, out var frame) || !frame.IsNack
            || !_assemblies.TryGetValue((peerKey, frame.MessageId), out var assembly)) return false;
        return frame.Generation == assembly.Generation
            && frame.TotalLength == assembly.TotalLength
            && frame.ChunkSize == assembly.ChunkSize
            && frame.ChunkCount == assembly.ChunkCount
            && frame.Checksum == assembly.Checksum;
    }

    public bool TryAccept(string peerKey, ReadOnlySpan<byte> packet, long nowMilliseconds, out byte[]? completeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerKey);
        completeMessage = null;
        if (!UdpFragmentation.TryDecode(packet, out var frame) || frame.IsNack || frame.IsMtuProbe || frame.IsMtuAck) return false;
        var key = (peerKey, frame.MessageId);
        Expire(nowMilliseconds);
        if (_completed.ContainsKey(key)) return true;
        if (!_assemblies.TryGetValue(key, out var assembly))
        {
            var peerCount = 0;
            var peerBytes = 0;
            foreach (var existing in _assemblies)
                if (existing.Key.Peer == peerKey) { peerCount++; peerBytes += existing.Value.TotalLength; }
            if (_assemblies.Count >= _maxAssemblies || peerCount >= _maxAssembliesPerPeer
                || _bufferedBytes + frame.TotalLength > _maxBufferedBytes
                || peerBytes + frame.TotalLength > _maxBufferedBytesPerPeer) return false;
            assembly = new Assembly(frame.MessageId, frame.TotalLength, frame.ChunkSize, frame.ChunkCount, frame.Generation, frame.Checksum, nowMilliseconds);
            _assemblies.Add(key, assembly);
            _bufferedBytes += frame.TotalLength;
        }
        else if (assembly.TotalLength != frame.TotalLength || assembly.Checksum != frame.Checksum)
        {
            Remove(key, assembly);
            return false;
        }
        else if (frame.Generation != assembly.Generation)
        {
            if (unchecked((short)(frame.Generation - assembly.Generation)) <= 0) return true;
            assembly = new Assembly(frame.MessageId, frame.TotalLength, frame.ChunkSize, frame.ChunkCount, frame.Generation, frame.Checksum, nowMilliseconds);
            _assemblies[key] = assembly;
        }
        else if (assembly.ChunkSize != frame.ChunkSize || assembly.ChunkCount != frame.ChunkCount)
        {
            Remove(key, assembly);
            return false;
        }

        var offset = frame.ChunkIndex * assembly.ChunkSize;
        if (!assembly.Received[frame.ChunkIndex])
        {
            frame.Body.CopyTo(assembly.Payload, offset);
            assembly.Received[frame.ChunkIndex] = true;
            assembly.ReceivedCount++;
        }
        assembly.LastActivityMilliseconds = nowMilliseconds;
        if (assembly.ReceivedCount != assembly.ChunkCount) return true;
        if (UdpFragmentation.Checksum(assembly.Payload) != assembly.Checksum)
        {
            Remove(key, assembly);
            return false;
        }

        completeMessage = assembly.Payload;
        Remove(key, assembly);
        AddCompleted(key, nowMilliseconds);
        return true;
    }

    public IReadOnlyList<(string PeerKey, byte[] Packet)> GetDueNacks(long nowMilliseconds)
    {
        Expire(nowMilliseconds);
        var result = new List<(string PeerKey, byte[] Packet)>();
        foreach (var assembly in _assemblies.Values)
        {
            if (nowMilliseconds - assembly.LastNackMilliseconds < NackIntervalMilliseconds) continue;
            var missing = new ushort[Math.Min(UdpFragmentation.MaxRepairChunksPerNack, assembly.ChunkCount - assembly.ReceivedCount)];
            var count = 0;
            for (var index = 0; index < assembly.ChunkCount && count < missing.Length; index++)
                if (!assembly.Received[index]) missing[count++] = (ushort)index;
            if (count == 0) continue;
            result.Add((FindPeer(assembly), UdpFragmentation.CreateNack(assembly.MessageId, assembly.TotalLength, assembly.ChunkSize,
                assembly.ChunkCount, assembly.Checksum, missing.AsSpan(0, count), assembly.Generation)));
            assembly.LastNackMilliseconds = nowMilliseconds;
        }
        return result;
    }

    public void ClearPeer(string peerKey)
    {
        foreach (var pair in _assemblies)
            if (pair.Key.Peer == peerKey) _bufferedBytes -= pair.Value.TotalLength;
        var keys = new List<(string Peer, ulong Id)>();
        foreach (var key in _assemblies.Keys) if (key.Peer == peerKey) keys.Add(key);
        foreach (var key in keys) _assemblies.Remove(key);
        var completedKeys = new List<(string Peer, ulong Id)>();
        foreach (var key in _completed.Keys) if (key.Peer == peerKey) completedKeys.Add(key);
        foreach (var key in completedKeys) _completed.Remove(key);
        var retainedCompleted = new Queue<((string Peer, ulong Id) Key, long CompletedAt)>();
        while (_completedOrder.TryDequeue(out var item))
            if (item.Key.Peer != peerKey) retainedCompleted.Enqueue(item);
        while (retainedCompleted.TryDequeue(out var item)) _completedOrder.Enqueue(item);
    }

    private void Expire(long nowMilliseconds)
    {
        var expired = new List<(string Peer, ulong Id)>();
        foreach (var pair in _assemblies)
            if (nowMilliseconds - pair.Value.CreatedAtMilliseconds >= _assemblyLifetimeMilliseconds) expired.Add(pair.Key);
        foreach (var key in expired) Remove(key, _assemblies[key]);
        while (_completedOrder.TryPeek(out var item) && nowMilliseconds - item.CompletedAt >= _assemblyLifetimeMilliseconds)
        {
            _completedOrder.Dequeue();
            if (_completed.TryGetValue(item.Key, out var completedAt) && completedAt == item.CompletedAt)
                _completed.Remove(item.Key);
        }
    }

    private void AddCompleted((string Peer, ulong Id) key, long nowMilliseconds)
    {
        _completed[key] = nowMilliseconds;
        _completedOrder.Enqueue((key, nowMilliseconds));
        while (_completed.Count > _maxCompletedIds)
        {
            var oldest = _completedOrder.Dequeue();
            if (_completed.TryGetValue(oldest.Key, out var completedAt) && completedAt == oldest.CompletedAt)
                _completed.Remove(oldest.Key);
        }
    }

    private void Remove((string Peer, ulong Id) key, Assembly assembly)
    {
        _assemblies.Remove(key);
        _bufferedBytes -= assembly.TotalLength;
    }

    private string FindPeer(Assembly target)
    {
        foreach (var pair in _assemblies) if (ReferenceEquals(pair.Value, target)) return pair.Key.Peer;
        return string.Empty;
    }

    private sealed class Assembly(ulong messageId, int totalLength, int chunkSize, int chunkCount, ushort generation, uint checksum, long now)
    {
        public ulong MessageId { get; } = messageId;
        public int TotalLength { get; } = totalLength;
        public int ChunkSize { get; } = chunkSize;
        public int ChunkCount { get; } = chunkCount;
        public ushort Generation { get; } = generation;
        public uint Checksum { get; } = checksum;
        public byte[] Payload { get; } = new byte[totalLength];
        public bool[] Received { get; } = new bool[chunkCount];
        public int ReceivedCount { get; set; }
        public long LastActivityMilliseconds { get; set; } = now;
        public long CreatedAtMilliseconds { get; } = now;
        public long LastNackMilliseconds { get; set; } = now;
    }
}

/// <summary>Short-lived, bounded cache for selective retransmission to the original peer.</summary>
public sealed class UdpFragmentSendCache
{
    public const long CacheLifetimeMilliseconds = 5000;
    public const int MaxMessagesPerPeer = 128;
    public const int MaxRepairRatePeers = 512;
    public const int MaxCachedBytes = 32 * 1024 * 1024;
    public const long MinRepairIntervalMilliseconds = 120;
    private readonly Dictionary<(string Peer, ulong Id), CachedMessage> _messages = new();
    private readonly Dictionary<string, long> _lastRepairByPeer = new(StringComparer.Ordinal);
    private int _cachedBytes;
    private ulong _nextId = unchecked((ulong)Random.Shared.NextInt64(1, long.MaxValue));

    public int CachedBytes => _cachedBytes;

    public IReadOnlyList<byte[]> CacheAndFragment(
        string peerKey,
        byte[] payload,
        long nowMilliseconds,
        int datagramLimit = UdpFragmentation.RecommendedDatagramBytes,
        bool isSnapshot = false,
        bool cacheForRepairs = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerKey);
        ArgumentNullException.ThrowIfNull(payload);
        if (datagramLimit is < (UdpFragmentation.HeaderBytes + 1) or > UdpFragmentation.MaxDatagramBytes)
            throw new ArgumentOutOfRangeException(nameof(datagramLimit));
        Expire(nowMilliseconds);
        if (payload.Length <= datagramLimit) return [payload];
        if (payload.Length > UdpFragmentation.MaxMessageBytes) throw new ArgumentOutOfRangeException(nameof(payload));
        var id = NextId();
        var frames = UdpFragmentation.Fragment(payload, id, datagramLimit).ToArray();
        if (!cacheForRepairs) return frames;
        var message = new CachedMessage(id, payload, frames, datagramLimit, isSnapshot) { CreatedAt = nowMilliseconds };
        _messages[(peerKey, id)] = message;
        _cachedBytes += payload.Length;
        EnforceBounds(peerKey);
        return frames;
    }

    public IReadOnlyList<byte[]> TryRepair(string peerKey, ReadOnlySpan<byte> packet, long nowMilliseconds, int datagramLimit = UdpFragmentation.RecommendedDatagramBytes)
    {
        Expire(nowMilliseconds);
        ExpireRepairRateState(nowMilliseconds);
        if (!UdpFragmentation.TryDecode(packet, out var nack) || !nack.IsNack
            || !_messages.TryGetValue((peerKey, nack.MessageId), out var message)
            || message.Payload.Length != nack.TotalLength || message.Checksum != nack.Checksum
            || nack.Generation > message.Generation)
            return Array.Empty<byte[]>();
        if (_lastRepairByPeer.TryGetValue(peerKey, out var last) && nowMilliseconds - last < MinRepairIntervalMilliseconds)
            return Array.Empty<byte[]>();
        var oldGenerationNack = nack.Generation < message.Generation;
        if (!oldGenerationNack && (nack.ChunkSize != message.ChunkSize || nack.ChunkCount != message.Frames.Length))
            return Array.Empty<byte[]>();
        var mustRefragment = message.DatagramLimit > datagramLimit || oldGenerationNack;
        if (message.DatagramLimit > datagramLimit)
        {
            message.Generation = unchecked((ushort)(message.Generation + 1));
            if (message.Generation == 0) message.Generation = 1;
            message.DatagramLimit = datagramLimit;
            message.Frames = UdpFragmentation.Fragment(message.Payload, message.Id, datagramLimit, message.Generation).ToArray();
        }

        var repairs = new List<byte[]>();
        if (mustRefragment)
        {
            for (var index = 0; index < Math.Min(UdpFragmentation.MaxRepairChunksPerNack, message.Frames.Length); index++) repairs.Add(message.Frames[index]);
        }
        else
        {
            for (var offset = 0; offset < nack.Body.Length; offset += sizeof(ushort))
            {
                var index = BinaryPrimitives.ReadUInt16LittleEndian(nack.Body.AsSpan(offset));
                if (repairs.Any(frame => UdpFragmentation.TryDecode(frame, out var queued) && queued.ChunkIndex == index)) continue;
                repairs.Add(message.Frames[index]);
                if (repairs.Count == UdpFragmentation.MaxRepairChunksPerNack) break;
            }
        }
        if (repairs.Count > 0)
        {
            _lastRepairByPeer[peerKey] = nowMilliseconds;
            if (_lastRepairByPeer.Count > MaxRepairRatePeers)
            {
                string? oldestPeer = null;
                var oldestTime = long.MaxValue;
                foreach (var pair in _lastRepairByPeer)
                    if (pair.Value < oldestTime) { oldestPeer = pair.Key; oldestTime = pair.Value; }
                if (oldestPeer is not null) _lastRepairByPeer.Remove(oldestPeer);
            }
        }
        return repairs;
    }

    public void ClearPeer(string peerKey)
    {
        var keys = new List<(string Peer, ulong Id)>();
        foreach (var pair in _messages) if (pair.Key.Peer == peerKey) keys.Add(pair.Key);
        foreach (var key in keys) Remove(key);
        _lastRepairByPeer.Remove(peerKey);
    }

    public bool IsSnapshotFrame(string peerKey, ReadOnlySpan<byte> packet)
        => UdpFragmentation.TryDecode(packet, out var frame)
            && !frame.IsNack && !frame.IsMtuProbe && !frame.IsMtuAck
            && _messages.TryGetValue((peerKey, frame.MessageId), out var message)
            && message.IsSnapshot;

    public bool IsCurrentFrame(string peerKey, ReadOnlySpan<byte> packet, long nowMilliseconds)
    {
        Expire(nowMilliseconds);
        return UdpFragmentation.TryDecode(packet, out var frame)
            && !frame.IsNack && !frame.IsMtuProbe && !frame.IsMtuAck
            && _messages.TryGetValue((peerKey, frame.MessageId), out var message)
            && frame.Generation == message.Generation
            && frame.TotalLength == message.Payload.Length
            && frame.Checksum == message.Checksum
            && frame.ChunkCount == message.Frames.Length
            && frame.ChunkSize == message.ChunkSize;
    }

    private ulong NextId()
    {
        _nextId = unchecked(_nextId + 1);
        if (_nextId == 0) _nextId = 1;
        return _nextId;
    }

    private void Expire(long nowMilliseconds)
    {
        var expired = new List<(string Peer, ulong Id)>();
        foreach (var pair in _messages)
            if (nowMilliseconds - pair.Value.CreatedAt >= CacheLifetimeMilliseconds) expired.Add(pair.Key);
        foreach (var key in expired) Remove(key);
    }

    private void ExpireRepairRateState(long nowMilliseconds)
    {
        var stale = new List<string>();
        foreach (var pair in _lastRepairByPeer)
            if (nowMilliseconds - pair.Value >= CacheLifetimeMilliseconds) stale.Add(pair.Key);
        foreach (var peer in stale) _lastRepairByPeer.Remove(peer);
    }

    private void EnforceBounds(string peerKey)
    {
        while (_cachedBytes > MaxCachedBytes || CountPeer(peerKey) > MaxMessagesPerPeer)
        {
            (string Peer, ulong Id)? oldestKey = null;
            long oldestTime = long.MaxValue;
            foreach (var pair in _messages)
            {
                if (_cachedBytes <= MaxCachedBytes && pair.Key.Peer != peerKey) continue;
                if (pair.Value.CreatedAt < oldestTime) { oldestTime = pair.Value.CreatedAt; oldestKey = pair.Key; }
            }
            if (oldestKey is null) break;
            Remove(oldestKey.Value);
        }
    }

    private int CountPeer(string peerKey)
    {
        var count = 0;
        foreach (var key in _messages.Keys) if (key.Peer == peerKey) count++;
        return count;
    }

    private void Remove((string Peer, ulong Id) key)
    {
        if (_messages.Remove(key, out var message)) _cachedBytes -= message.Payload.Length;
    }

    private sealed class CachedMessage
    {
        public CachedMessage(ulong id, byte[] payload, byte[][] frames, int datagramLimit, bool isSnapshot)
        {
            Id = id;
            Payload = payload;
            Frames = frames;
            DatagramLimit = datagramLimit;
            IsSnapshot = isSnapshot;
            if (frames.Length > 0 && UdpFragmentation.TryDecode(frames[0], out var frame)) Generation = frame.Generation;
        }

        public ulong Id { get; }
        public byte[] Payload { get; }
        public byte[][] Frames { get; set; }
        public int DatagramLimit { get; set; }
        public bool IsSnapshot { get; }
        public ushort Generation { get; set; }
        public long CreatedAt { get; init; } = Environment.TickCount64;
        public int ChunkSize => Frames.Length == 0 ? 0 : Frames[0].Length - UdpFragmentation.HeaderBytes;
        public uint Checksum => UdpFragmentation.Checksum(Payload);
    }
}

/// <summary>Bounded queue that emits at most one repair datagram per peer every 20 ms.</summary>
public sealed class UdpRepairPacer
{
    public const int MaxQueuedDatagrams = 512;
    public const int MaxQueuedPerPeer = 32;
    public const int MaxTrackedPeers = 512;
    public const long PeerStateLifetimeMilliseconds = 5000;
    public const long PerPeerIntervalMilliseconds = 20;
    public const long QueuedPacketLifetimeMilliseconds = 5000;
    private readonly Queue<(string PeerKey, byte[] Packet, long QueuedAtMilliseconds)> _queue = new();
    private readonly HashSet<(string PeerKey, ulong MessageId, int Generation, int ChunkIndex)> _queuedChunks = new();
    private readonly HashSet<(string PeerKey, ulong MessageId, int Generation)> _queuedNacks = new();
    private readonly Dictionary<string, long> _nextSendByPeer = new(StringComparer.Ordinal);

    public int Count => _queue.Count;

    public int Enqueue(string peerKey, IEnumerable<byte[]> packets, long? nowMilliseconds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerKey);
        ArgumentNullException.ThrowIfNull(packets);
        var now = nowMilliseconds ?? Environment.TickCount64;
        RemoveExpired(now);
        var peerQueued = 0;
        foreach (var item in _queue) if (item.PeerKey == peerKey) peerQueued++;
        var added = 0;
        foreach (var packet in packets)
        {
            if (_queue.Count >= MaxQueuedDatagrams || peerQueued >= MaxQueuedPerPeer) break;
            if (UdpFragmentation.TryDecode(packet, out var frame))
            {
                if (frame.IsNack)
                {
                    if (!_queuedNacks.Add((peerKey, frame.MessageId, frame.Generation))) continue;
                }
                else if (!frame.IsMtuAck && !frame.IsMtuProbe
                    && !_queuedChunks.Add((peerKey, frame.MessageId, frame.Generation, frame.ChunkIndex))) continue;
            }
            _queue.Enqueue((peerKey, packet, now));
            peerQueued++;
            added++;
        }
        return added;
    }

    public bool TryDequeueDue(long nowMilliseconds, out string peerKey, out byte[]? packet)
    {
        RemoveExpired(nowMilliseconds);
        var stalePeers = new List<string>();
        foreach (var pair in _nextSendByPeer)
            if (nowMilliseconds - pair.Value >= PeerStateLifetimeMilliseconds) stalePeers.Add(pair.Key);
        foreach (var stalePeer in stalePeers) _nextSendByPeer.Remove(stalePeer);
        peerKey = string.Empty;
        packet = null;
        var checks = _queue.Count;
        while (checks-- > 0)
        {
            var item = _queue.Dequeue();
            if (!_nextSendByPeer.TryGetValue(item.PeerKey, out var nextSend) || nowMilliseconds >= nextSend)
            {
                RemoveQueuedIdentity(item.PeerKey, item.Packet);
                _nextSendByPeer[item.PeerKey] = nowMilliseconds + PerPeerIntervalMilliseconds;
                if (_nextSendByPeer.Count > MaxTrackedPeers)
                {
                    string? oldestPeer = null;
                    var oldestTime = long.MaxValue;
                    foreach (var pair in _nextSendByPeer)
                        if (pair.Value < oldestTime) { oldestPeer = pair.Key; oldestTime = pair.Value; }
                    if (oldestPeer is not null) _nextSendByPeer.Remove(oldestPeer);
                }
                peerKey = item.PeerKey;
                packet = item.Packet;
                return true;
            }
            _queue.Enqueue(item);
        }
        return false;
    }

    public void ClearPeer(string peerKey)
    {
        var retained = new Queue<(string PeerKey, byte[] Packet, long QueuedAtMilliseconds)>();
        while (_queue.TryDequeue(out var item))
        {
            if (item.PeerKey != peerKey) { retained.Enqueue(item); continue; }
            RemoveQueuedIdentity(item.PeerKey, item.Packet);
        }
        while (retained.TryDequeue(out var item)) _queue.Enqueue(item);
        _nextSendByPeer.Remove(peerKey);
    }

    private void RemoveExpired(long nowMilliseconds)
    {
        var retained = new Queue<(string PeerKey, byte[] Packet, long QueuedAtMilliseconds)>();
        while (_queue.TryDequeue(out var item))
        {
            if (nowMilliseconds - item.QueuedAtMilliseconds >= QueuedPacketLifetimeMilliseconds)
            {
                RemoveQueuedIdentity(item.PeerKey, item.Packet);
                continue;
            }
            retained.Enqueue(item);
        }
        while (retained.TryDequeue(out var item)) _queue.Enqueue(item);
    }

    private void RemoveQueuedIdentity(string peerKey, ReadOnlySpan<byte> packet)
    {
        if (!UdpFragmentation.TryDecode(packet, out var decoded)) return;
        if (decoded.IsNack) _queuedNacks.Remove((peerKey, decoded.MessageId, decoded.Generation));
        else if (!decoded.IsMtuAck && !decoded.IsMtuProbe)
            _queuedChunks.Remove((peerKey, decoded.MessageId, decoded.Generation, decoded.ChunkIndex));
    }
}
