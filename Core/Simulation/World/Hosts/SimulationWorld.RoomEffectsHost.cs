namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IRoomEffectsHost
{
    PlayerDeathSystem IRoomEffectsHost.PlayerDeaths => PlayerDeaths;
    WorldEffectsSystem IRoomEffectsHost.WorldEffects => WorldEffects;
    CombatSystem IRoomEffectsHost.Combat => Combat;
    bool IRoomEffectsHost.ClientPredictionMode => ClientPredictionMode;
}
