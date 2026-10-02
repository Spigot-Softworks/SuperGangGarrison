namespace OpenGarrison.Core;

/// <summary>
/// What a player leaves behind: gibs and their kicks and landing splats, blood
/// drops, and dead-body effects.
/// </summary>
internal sealed partial class PlayerRemainsSystem
{
    private readonly IPlayerRemainsHost _host;

    public PlayerRemainsSystem(IPlayerRemainsHost host)
    {
        _host = host;
    }
}
