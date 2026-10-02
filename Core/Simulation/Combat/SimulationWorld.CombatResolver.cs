namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    internal CombatResolver GeometryResolver => _combatResolver ??= new CombatResolver(this);
    private CombatResolver? _combatResolver;
}
