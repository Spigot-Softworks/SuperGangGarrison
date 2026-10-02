namespace OpenGarrison.Core;

/// <summary>
/// Player death and respawn: killing a player (credit, assists, rewards, intel drop,
/// kill feed, remains), death cams, respawn timers and dead bodies.
/// </summary>
internal sealed partial class PlayerDeathSystem
{
    private readonly IPlayerDeathHost _host;

    public PlayerDeathSystem(IPlayerDeathHost host)
    {
        _host = host;
    }
}
