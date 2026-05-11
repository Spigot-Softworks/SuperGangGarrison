#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace OpenGarrison.Client;

public interface INetworkClientMessageTransport : IDisposable
{
    bool HasPendingMessages { get; }
    bool IsLoopbackConnection { get; }
    string RemoteDescription { get; }

    bool TryReceive(out byte[] payload);
    bool TryConsumeDisconnectReason(out string reason);
    void Send(byte[] payload);
}

public interface IPlaybackMessageTransport : INetworkClientMessageTransport
{
    bool IsPaused { get; }
    float PlaybackRate { get; }
    int CurrentTick { get; }
    int TotalTicks { get; }
    string PlaybackDisplayName { get; }
    string PlaybackServerName { get; }
    string PlaybackMapName { get; }
    DateTime? PlaybackDateUtc { get; }

    void SetPaused(bool paused);
    void TogglePaused();
    void SetPlaybackRate(float playbackRate);
}

internal sealed class UdpNetworkClientMessageTransport : INetworkClientMessageTransport
{
    private const int SioUdpConnReset = -1744830452;

    private readonly UdpClient _udpClient;
    private readonly IPEndPoint _serverEndPoint;

    private UdpNetworkClientMessageTransport(UdpClient udpClient, IPEndPoint serverEndPoint)
    {
        _udpClient = udpClient;
        _serverEndPoint = serverEndPoint;
    }

    public bool HasPendingMessages => _udpClient.Available > 0;

    public bool IsLoopbackConnection
    {
        get
        {
            var address = _serverEndPoint.Address;
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            return IPAddress.IsLoopback(address);
        }
    }

    public string RemoteDescription => $"{_serverEndPoint.Address}:{_serverEndPoint.Port}";

    public static bool TryConnect(string host, int port, out INetworkClientMessageTransport? transport, out string error)
    {
        transport = null;
        error = string.Empty;

        try
        {
            var addresses = Dns.GetHostAddresses(host);
            var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault();
            if (address is null)
            {
                error = $"could not resolve host {host}";
                return false;
            }

            var serverEndPoint = new IPEndPoint(address, port);
            var udpClient = new UdpClient(0);
            udpClient.Client.Blocking = false;
            TryDisableUdpConnectionReset(udpClient.Client);
            transport = new UdpNetworkClientMessageTransport(udpClient, serverEndPoint);
            return true;
        }
        catch (SocketException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryReceive(out byte[] payload)
    {
        payload = [];
        IPEndPoint remoteEndPoint = new(IPAddress.Any, 0);
        var receivedPayload = _udpClient.Receive(ref remoteEndPoint);
        if (!EndpointsEqual(remoteEndPoint, _serverEndPoint))
        {
            return false;
        }

        payload = receivedPayload;
        return true;
    }

    public void Send(byte[] payload)
    {
        _udpClient.Send(payload, payload.Length, _serverEndPoint);
    }

    public bool TryConsumeDisconnectReason(out string reason)
    {
        reason = string.Empty;
        return false;
    }

    public void Dispose()
    {
        _udpClient.Dispose();
    }

    private static bool EndpointsEqual(IPEndPoint left, IPEndPoint right)
    {
        return left.Address.Equals(right.Address) && left.Port == right.Port;
    }

    private static void TryDisableUdpConnectionReset(Socket socket)
    {
        try
        {
            socket.IOControl((IOControlCode)SioUdpConnReset, [0, 0, 0, 0], null);
        }
        catch (PlatformNotSupportedException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }
}
