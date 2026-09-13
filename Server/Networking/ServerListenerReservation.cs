using System.Net;
using System.Net.Sockets;

namespace OpenGarrison.Server;

// Hold the actual UDP socket through startup, rather than probing and releasing
// it. Simultaneous servers cannot select the same port, including across users.
internal sealed class ServerListenerReservation : IDisposable
{
    public UdpClient Udp { get; }
    public int Port { get; }
    public int HttpPort { get; }
    private TcpListener? _tcp;

    private ServerListenerReservation(UdpClient udp, TcpListener tcp, int port, int httpPort)
        => (Udp, _tcp, Port, HttpPort) = (udp, tcp, port, httpPort);

    public static ServerListenerReservation Acquire(int requestedPort, int webSocketPort, bool loopbackOnly = false)
    {
        if (requestedPort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(requestedPort));
        var address = loopbackOnly ? IPAddress.Loopback : IPAddress.Any;
        var fixedHttpPort = webSocketPort > 0 && webSocketPort != requestedPort ? webSocketPort : 0;
        for (var port = requestedPort; port <= 65535; port++)
        {
            var udp = new UdpClient(AddressFamily.InterNetwork);
            TcpListener? tcp = null;
            var udpBound = false;
            try
            {
                udp.ExclusiveAddressUse = true;
                udp.Client.Bind(new IPEndPoint(address, port));
                udpBound = true;
                var httpPort = fixedHttpPort > 0 ? fixedHttpPort : port;
                tcp = new TcpListener(address, httpPort) { ExclusiveAddressUse = true };
                tcp.Start();
                return new ServerListenerReservation(udp, tcp, port, httpPort);
            }
            catch (SocketException ex) when (ex.SocketErrorCode is SocketError.AddressAlreadyInUse or SocketError.AccessDenied)
            {
                udp.Dispose();
                tcp?.Stop();
                if (udpBound && fixedHttpPort > 0)
                    throw new IOException($"Configured HTTP/WebSocket port {fixedHttpPort} is unavailable.", ex);
            }
            catch { udp.Dispose(); tcp?.Stop(); throw; }
        }
        throw new IOException($"No available UDP/TCP port at or above {requestedPort}.");
    }

    public void ReleaseHttpReservation() { _tcp?.Stop(); _tcp = null; }
    public void Dispose() { ReleaseHttpReservation(); Udp.Dispose(); }
}
