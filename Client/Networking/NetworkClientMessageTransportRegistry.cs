#nullable enable

using System;

namespace OpenGarrison.Client;

public delegate bool ConnectNetworkClientMessageTransport(
    string host,
    int port,
    out INetworkClientMessageTransport? transport,
    out string error);

public static class NetworkClientMessageTransportRegistry
{
    private static ConnectNetworkClientMessageTransport? _browserTransportFactory;

    public static void SetBrowserTransportFactory(ConnectNetworkClientMessageTransport? factory)
    {
        _browserTransportFactory = factory;
    }

    internal static bool TryConnect(string host, int port, out INetworkClientMessageTransport? transport, out string error)
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

        return UdpNetworkClientMessageTransport.TryConnect(host, port, out transport, out error);
    }
}
