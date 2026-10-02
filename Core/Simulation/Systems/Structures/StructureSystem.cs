namespace OpenGarrison.Core;

internal sealed partial class StructureSystem
{
    private readonly IStructureHost _host;

    public StructureSystem(IStructureHost host)
    {
        _host = host;
    }
}
