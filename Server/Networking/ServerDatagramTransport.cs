using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal enum ServerTransportKind
{
    Udp = 1,
    WebTransport = 2,
}

internal readonly struct ServerTransportPeer : IEquatable<ServerTransportPeer>
{
    public ServerTransportPeer(ServerTransportKind kind, ulong id, string description, IPEndPoint? udpEndPoint)
    {
        if (id == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Server transport peer id must not be zero.");
        }

        Kind = kind;
        Id = id;
        Description = string.IsNullOrWhiteSpace(description) ? $"peer:{id}" : description;
        UdpEndPoint = udpEndPoint;
    }

    public ServerTransportKind Kind { get; }
    public ulong Id { get; }
    public string Description { get; }
    public IPEndPoint? UdpEndPoint { get; }

    public bool IsLoopback
    {
        get
        {
            var address = UdpEndPoint?.Address;
            if (address is null)
            {
                return false;
            }

            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            return IPAddress.IsLoopback(address);
        }
    }

    public bool Equals(ServerTransportPeer other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is ServerTransportPeer other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override string ToString()
    {
        return Description;
    }

    public static ServerTransportPeer FromUdpEndPoint(IPEndPoint endPoint)
    {
        ArgumentNullException.ThrowIfNull(endPoint);
        return new ServerTransportPeer(ServerTransportKind.Udp, CreateUdpPeerId(endPoint), endPoint.ToString(), endPoint);
    }

    public static ServerTransportPeer FromWebTransportSession(long sessionId, string description)
    {
        return new ServerTransportPeer(ServerTransportKind.WebTransport, unchecked((ulong)sessionId), description, udpEndPoint: null);
    }

    public static bool operator ==(ServerTransportPeer left, ServerTransportPeer right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ServerTransportPeer left, ServerTransportPeer right)
    {
        return !left.Equals(right);
    }

    private static ulong CreateUdpPeerId(IPEndPoint endPoint)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offsetBasis;
        var addressBytes = endPoint.Address.GetAddressBytes();
        for (var index = 0; index < addressBytes.Length; index += 1)
        {
            hash = (hash ^ addressBytes[index]) * prime;
        }

        hash = (hash ^ (byte)(endPoint.Port & 0xff)) * prime;
        hash = (hash ^ (byte)((endPoint.Port >> 8) & 0xff)) * prime;
        return hash == 0 ? 1 : hash;
    }
}

internal readonly record struct ServerDatagram(ServerTransportPeer RemotePeer, byte[] Payload);

internal interface IServerDatagramTransport
{
    bool HasPendingDatagrams { get; }

    ServerDatagram Receive();
    void Send(ServerTransportPeer remotePeer, byte[] payload);
}

internal sealed class UdpServerDatagramTransport(UdpClient udp) : IServerDatagramTransport
{
    public bool HasPendingDatagrams => udp.Available > 0;

    public ServerDatagram Receive()
    {
        IPEndPoint remoteEndPoint = new(IPAddress.Any, 0);
        var payload = udp.Receive(ref remoteEndPoint);
        return new ServerDatagram(ServerTransportPeer.FromUdpEndPoint(remoteEndPoint), payload);
    }

    public void Send(ServerTransportPeer remotePeer, byte[] payload)
    {
        var remoteEndPoint = remotePeer.UdpEndPoint
            ?? throw new InvalidOperationException($"Peer {remotePeer} cannot be addressed by UDP.");
        udp.Send(payload, payload.Length, remoteEndPoint);
    }
}

internal sealed class CompositeServerDatagramTransport(UdpClient udp) : IServerDatagramTransport
{
    private const int MaxInboundWebTransportPayloadBytes = 64 * 1024;

    private readonly UdpServerDatagramTransport _udpTransport = new(udp);
    private readonly ConcurrentQueue<ServerDatagram> _inboundDatagrams = new();
    private readonly ConcurrentDictionary<ulong, WebTransportPeerConnection> _webTransportConnections = new();

    public bool HasPendingDatagrams => !_inboundDatagrams.IsEmpty || _udpTransport.HasPendingDatagrams;

    public ServerDatagram Receive()
    {
        return _inboundDatagrams.TryDequeue(out var datagram)
            ? datagram
            : _udpTransport.Receive();
    }

    public void Send(ServerTransportPeer remotePeer, byte[] payload)
    {
        if (remotePeer.Kind == ServerTransportKind.Udp)
        {
            _udpTransport.Send(remotePeer, payload);
            return;
        }

        if (_webTransportConnections.TryGetValue(remotePeer.Id, out var connection))
        {
            connection.QueueOutboundPayload(payload);
        }
    }

    public async Task RunWebTransportPeerAsync(long sessionId, string description, PipeReader reader, PipeWriter writer, Action<string> log, CancellationToken cancellationToken)
    {
        var peer = ServerTransportPeer.FromWebTransportSession(sessionId, description);
        var connection = new WebTransportPeerConnection(peer, reader, writer, _inboundDatagrams, log);
        if (!_webTransportConnections.TryAdd(peer.Id, connection))
        {
            throw new InvalidOperationException($"A WebTransport peer with id {peer.Id} is already connected.");
        }

        try
        {
            await connection.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _webTransportConnections.TryRemove(peer.Id, out _);
            connection.Dispose();
        }
    }

    private sealed class WebTransportPeerConnection : IDisposable
    {
        private const int LengthPrefixBytes = 4;

        private readonly ServerTransportPeer _peer;
        private readonly Stream _readStream;
        private readonly Stream _writeStream;
        private readonly ConcurrentQueue<ServerDatagram> _inboundDatagrams;
        private readonly Channel<byte[]> _outboundPayloads = Channel.CreateUnbounded<byte[]>();
        private readonly Action<string> _log;
        private int _disposed;

        public WebTransportPeerConnection(ServerTransportPeer peer, PipeReader reader, PipeWriter writer, ConcurrentQueue<ServerDatagram> inboundDatagrams, Action<string> log)
        {
            _peer = peer;
            _readStream = reader.AsStream(leaveOpen: true);
            _writeStream = writer.AsStream(leaveOpen: true);
            _inboundDatagrams = inboundDatagrams;
            _log = log;
        }

        public void QueueOutboundPayload(byte[] payload)
        {
            if (payload.Length == 0 || Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            _outboundPayloads.Writer.TryWrite(payload);
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var linkedToken = linkedCts.Token;
            var readerTask = RunReaderLoopAsync(linkedToken);
            var writerTask = RunWriterLoopAsync(linkedToken);
            var completedTask = await Task.WhenAny(readerTask, writerTask).ConfigureAwait(false);
            linkedCts.Cancel();

            try
            {
                await completedTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _log($"[server] WebTransport peer {_peer} closed with error: {ex.Message}");
            }

            try
            {
                await Task.WhenAll(readerTask, writerTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
            {
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _outboundPayloads.Writer.TryComplete();
            try
            {
                _readStream.Dispose();
            }
            catch
            {
            }

            try
            {
                _writeStream.Dispose();
            }
            catch
            {
            }
        }

        private async Task RunReaderLoopAsync(CancellationToken cancellationToken)
        {
            var prefixBuffer = new byte[LengthPrefixBytes];
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!await TryReadExactlyAsync(prefixBuffer, cancellationToken).ConfigureAwait(false))
                {
                    break;
                }

                var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(prefixBuffer);
                if (payloadLength <= 0 || payloadLength > MaxInboundWebTransportPayloadBytes)
                {
                    throw new InvalidOperationException($"Invalid WebTransport payload length {payloadLength} for peer {_peer}.");
                }

                var payload = new byte[payloadLength];
                if (!await TryReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false))
                {
                    break;
                }

                _inboundDatagrams.Enqueue(new ServerDatagram(_peer, payload));
            }
        }

        private async Task RunWriterLoopAsync(CancellationToken cancellationToken)
        {
            await foreach (var payload in _outboundPayloads.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var frame = new byte[LengthPrefixBytes + payload.Length];
                BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0, LengthPrefixBytes), payload.Length);
                payload.CopyTo(frame, LengthPrefixBytes);
                await _writeStream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                await _writeStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private async ValueTask<bool> TryReadExactlyAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            var bytesRead = 0;
            while (bytesRead < buffer.Length)
            {
                var read = await _readStream.ReadAsync(buffer.AsMemory(bytesRead, buffer.Length - bytesRead), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return false;
                }

                bytesRead += read;
            }

            return true;
        }
    }
}
