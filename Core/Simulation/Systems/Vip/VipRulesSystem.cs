namespace OpenGarrison.Core;

/// <summary>
/// VIP rules: VIP assignment and practice VIP, warmup, death penalties,
/// capture permissions and the VIP-death win condition. State lives in
/// <see cref="VipState"/>; this system owns the rules.
/// </summary>
internal sealed partial class VipRulesSystem
{
    private readonly IVipRulesHost _host;

    public VipRulesSystem(IVipRulesHost host)
    {
        _host = host;
    }
}
