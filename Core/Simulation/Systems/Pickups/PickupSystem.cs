namespace OpenGarrison.Core;

internal sealed partial class PickupSystem
{
    private readonly IPickupHost _host;

    public PickupSystem(IPickupHost host)
    {
        _host = host;
    }
}
