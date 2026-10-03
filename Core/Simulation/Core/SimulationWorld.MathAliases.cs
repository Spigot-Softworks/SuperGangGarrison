namespace OpenGarrison.Core;

// Short names for the shared math helpers, kept for the remaining world partials.
public sealed partial class SimulationWorld
{
    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }
}
