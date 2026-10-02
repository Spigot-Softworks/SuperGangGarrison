namespace OpenGarrison.Core;

/// <summary>
/// Pyro airblast, civvie umbrella airblast and the Thundergunner: pushing
/// players, mines, gibs and bodies, and reflecting or destroying enemy projectiles.
/// </summary>
internal sealed partial class AirblastRulesSystem
{
    private readonly IAirblastRulesHost _host;

    public AirblastRulesSystem(IAirblastRulesHost host)
    {
        _host = host;
    }
}
