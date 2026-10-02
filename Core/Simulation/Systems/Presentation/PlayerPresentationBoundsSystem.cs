namespace OpenGarrison.Core;

/// <summary>
/// Player hit bounds taken from the presentation sprite mask (GG2 collided with
/// the drawn sprite, not a fixed box), with a per-frame cache. The sprite asset
/// cache is process-wide and lives here rather than on the world.
/// </summary>
internal sealed partial class PlayerPresentationBoundsSystem
{
    private readonly IPlayerPresentationBoundsHost _host;

    public PlayerPresentationBoundsSystem(IPlayerPresentationBoundsHost host)
    {
        _host = host;
    }
}
