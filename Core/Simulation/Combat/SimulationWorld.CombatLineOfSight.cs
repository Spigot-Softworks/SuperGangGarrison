namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public void WarmCombatSpatialIndices()
    {
        GeometryResolver.WarmSpatialIndices();
    }
}
