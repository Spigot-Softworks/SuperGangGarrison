namespace OpenGarrison.Core;

/// <summary>
/// Map logic: the logic graph, activators, player/intel/score/damage triggers,
/// damageable zones, spritesheet playback and foreground jungle sprites.
/// State lives on the level and in <see cref="MapRuntimeState"/>; this system owns the rules.
/// </summary>
internal sealed partial class MapLogicSystem
{
    private readonly IMapLogicHost _host;

    public MapLogicSystem(IMapLogicHost host)
    {
        _host = host;
    }
}
