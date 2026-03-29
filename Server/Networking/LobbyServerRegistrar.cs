using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

sealed class LobbyServerRegistrar
{
    private readonly UdpClient _udp;
    private readonly string _host;
    private readonly int _port;
    private readonly byte[] _protocolUuid;
    private readonly int _hostingPort;
    private readonly TimeSpan _heartbeatInterval;
    private readonly TimeSpan _resolveInterval;
    private TimeSpan _lastSent = TimeSpan.MinValue;
    private TimeSpan _lastResolve = TimeSpan.MinValue;
    private IPEndPoint? _endpoint;

    public LobbyServerRegistrar(
        UdpClient udp,
        string host,
        int port,
        byte[] protocolUuid,
        int hostingPort,
        TimeSpan heartbeatInterval,
        TimeSpan resolveInterval)
    {
        _udp = udp;
        _host = host;
        _port = port;
        _protocolUuid = protocolUuid;
        _hostingPort = hostingPort;
        _heartbeatInterval = heartbeatInterval;
        _resolveInterval = resolveInterval;
    }

    public void Tick(TimeSpan now, string mapServerName)
    {
        if (_protocolUuid.Length != 16)
        {
            return;
        }

        if (_endpoint is null || now - _lastResolve >= _resolveInterval)
        {
            ResolveEndpoint(now);
        }

        if (_endpoint is null || now - _lastSent < _heartbeatInterval)
        {
            return;
        }

        var payload = BuildPayload(mapServerName);
        _udp.Send(payload, payload.Length, _endpoint);
        _lastSent = now;
    }

    private void ResolveEndpoint(TimeSpan now)
    {
        _lastResolve = now;
        try
        {
            var addresses = Dns.GetHostAddresses(_host);
            var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault();
            _endpoint = address is null ? null : new IPEndPoint(address, _port);
        }
        catch
        {
            _endpoint = null;
        }
    }

    private byte[] BuildPayload(string mapServerName)
    {
        var nameBytes = Encoding.UTF8.GetBytes(mapServerName);
        if (nameBytes.Length > byte.MaxValue)
        {
            nameBytes = nameBytes[..byte.MaxValue];
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)4);
        writer.Write((byte)8);
        writer.Write((byte)15);
        writer.Write((byte)16);
        writer.Write((byte)23);
        writer.Write((byte)42);
        writer.Write((byte)128);
        writer.Write(_protocolUuid, 0, 16);
        writer.Write((ushort)_hostingPort);
        writer.Write((byte)nameBytes.Length);
        writer.Write(nameBytes);
        writer.Flush();
        return stream.ToArray();
    }
}
