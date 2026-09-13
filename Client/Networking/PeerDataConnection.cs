using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text.Json;

namespace OpenGarrison.Client;

public interface IPeerDataConnection : IDisposable
{
    bool IsOpen { get; }
    void ApplySignal(string signal);
    bool TrySend(byte[] packet);
}

public static class PeerDataConnectionFactory
{
    public static Func<bool, JsonElement, Action<string>, Action<byte[]>, IPeerDataConnection>? BrowserFactory { get; set; }
    public static IPeerDataConnection Create(bool offerer, JsonElement iceServers, Action<string> signal, Action<byte[]> receive)
    {
#if BROWSER_KNI
        return (BrowserFactory ?? throw new InvalidOperationException("Browser peer networking is unavailable."))(offerer, iceServers, signal, receive);
#else
        return new NativePeerDataConnection(offerer, iceServers, signal, receive);
#endif
    }
}

/// <summary>Bounded fragmentation for reliable ordered data channels, including large map snapshots.</summary>
public sealed class PeerPacketFraming
{
    public const int ChunkPayloadBytes = 16000;
    private const uint Magic = 0x3150474f;
    private uint _nextId;
    private uint _receivingId;
    private byte[]? _assembling;
    private int _received;
    private readonly Action<byte[]> _receive;
    public PeerPacketFraming(Action<byte[]> receive) => _receive = receive;

    public void Send(byte[] packet, Action<byte[]> send)
    {
        if (packet.Length is < 1 or > 4 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(packet));
        var id = ++_nextId;
        for (var offset = 0; offset < packet.Length; offset += ChunkPayloadBytes)
        {
            var count = Math.Min(ChunkPayloadBytes, packet.Length - offset);
            var chunk = new byte[16 + count];
            BinaryPrimitives.WriteUInt32LittleEndian(chunk, Magic);
            BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), id);
            BinaryPrimitives.WriteInt32LittleEndian(chunk.AsSpan(8), packet.Length);
            BinaryPrimitives.WriteInt32LittleEndian(chunk.AsSpan(12), offset);
            packet.AsSpan(offset, count).CopyTo(chunk.AsSpan(16));
            send(chunk);
        }
    }
    public void Receive(byte[] chunk)
    {
        if (chunk.Length is < 17 or > ChunkPayloadBytes + 16 || BinaryPrimitives.ReadUInt32LittleEndian(chunk) != Magic) return;
        var id = BinaryPrimitives.ReadUInt32LittleEndian(chunk.AsSpan(4));
        var length = BinaryPrimitives.ReadInt32LittleEndian(chunk.AsSpan(8));
        var offset = BinaryPrimitives.ReadInt32LittleEndian(chunk.AsSpan(12));
        if (length is < 1 or > 4 * 1024 * 1024 || offset < 0 || (long)offset + chunk.Length - 16 > length) return;
        if (offset == 0) { _receivingId = id; _received = 0; _assembling = new byte[length]; }
        if (_assembling is null || id != _receivingId || offset != _received || length != _assembling.Length) return;
        chunk.AsSpan(16).CopyTo(_assembling.AsSpan(offset));
        _received += chunk.Length - 16;
        if (_received == length)
        {
            var packet = _assembling;
            _assembling = null;
            _receive(packet);
        }
    }
}
