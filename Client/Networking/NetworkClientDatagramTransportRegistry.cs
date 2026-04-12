#nullable enable

using System;

namespace OpenGarrison.Client;

public delegate bool ConnectNetworkClientDatagramTransport(
    string host,
    int port,
    out INetworkClientDatagramTransport? transport,
    out string error);

public static class NetworkClientDatagramTransportRegistry
{
    private static ConnectNetworkClientDatagramTransport? _browserTransportFactory;

    public static void SetBrowserTransportFactory(ConnectNetworkClientDatagramTransport? factory)
    {
        _browserTransportFactory = factory;
    }

    internal static bool TryConnect(string host, int port, out INetworkClientDatagramTransport? transport, out string error)
    {
        if (OperatingSystem.IsBrowser())
        {
            if (_browserTransportFactory is null)
            {
                transport = null;
                error = "Browser networking transport is not initialized.";
                return false;
            }

            return _browserTransportFactory(host, port, out transport, out error);
        }

        return UdpNetworkClientDatagramTransport.TryConnect(host, port, out transport, out error);
    }
}
