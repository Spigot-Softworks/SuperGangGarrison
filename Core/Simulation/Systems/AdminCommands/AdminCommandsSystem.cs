namespace OpenGarrison.Core;

/// <summary>
/// Server admin commands that act on a network player: noclip, freeze, stun,
/// teleport, explode, respawn overrides, free jump pads and input inspection.
/// </summary>
internal sealed partial class AdminCommandsSystem
{
    private readonly IAdminCommandsHost _host;

    public AdminCommandsSystem(IAdminCommandsHost host)
    {
        _host = host;
    }
}
