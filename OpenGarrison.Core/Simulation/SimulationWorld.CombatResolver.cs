namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private CombatResolver Combat => _combatResolver ??= new CombatResolver(this);
    private CombatResolver? _combatResolver;

    private sealed partial class CombatResolver
    {
        private readonly SimulationWorld _world;

        public CombatResolver(SimulationWorld world)
        {
            _world = world;
        }

        private SimpleLevel Level => _world.Level;

        private List<SentryEntity> _sentries => _world._sentries;

        private List<GeneratorState> _generators => _world._generators;

        private IEnumerable<PlayerEntity> EnumerateSimulatedPlayers()
        {
            return _world.EnumerateSimulatedPlayers();
        }
    }
}
