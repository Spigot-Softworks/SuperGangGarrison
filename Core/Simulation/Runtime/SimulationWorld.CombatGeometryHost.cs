namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ICombatGeometryHost
{
    DamageRulesSystem ICombatGeometryHost.DamageRules => DamageRules;
    IEnumerable<PlayerEntity> ICombatGeometryHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    long ICombatGeometryHost.Frame => Frame;
    SimpleLevel ICombatGeometryHost.Level => Level;
    MapLogicSystem ICombatGeometryHost.MapLogic => MapLogic;
    PlayerPresentationBoundsSystem ICombatGeometryHost.PresentationBounds => PresentationBounds;
    ProjectileSystem ICombatGeometryHost.Projectiles => Projectiles;
    WorldObjectStore ICombatGeometryHost.WorldObjects => WorldObjects;
}
