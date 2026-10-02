namespace OpenGarrison.Core;

/// <summary>
/// Who can damage whom (self damage, round-end and confusion friendly fire),
/// server damage scaling, and healing feedback events.
/// </summary>
internal sealed partial class DamageRulesSystem
{
    private readonly IDamageRulesHost _host;

    public DamageRulesSystem(IDamageRulesHost host)
    {
        _host = host;
    }
}
