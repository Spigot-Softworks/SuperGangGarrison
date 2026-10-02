namespace OpenGarrison.Core;

internal sealed partial class NetworkPlayerSystem
{
    private readonly INetworkPlayerHost _host;

    public NetworkPlayerSystem(INetworkPlayerHost host)
    {
        _host = host;
    }
}
