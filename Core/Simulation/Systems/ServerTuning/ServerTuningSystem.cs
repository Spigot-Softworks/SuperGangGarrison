namespace OpenGarrison.Core;

/// <summary>
/// Server gameplay tuning: match-wide scale settings (map, player, movement, gravity,
/// damage, projectile speed, speed clamps) and per-slot overrides, and applying them
/// to players. Settings live in <see cref="MatchSettings"/> and the player registry.
/// </summary>
internal sealed partial class ServerTuningSystem
{
    private readonly IServerTuningHost _host;

    public ServerTuningSystem(IServerTuningHost host)
    {
        _host = host;
    }
}
