namespace OpenGarrison.Core;

// The world is the host for its extracted systems. Capabilities shared by
// several systems are implemented here; system-specific members live beside
// the world code they forward to.
public sealed partial class SimulationWorld :
    ICombatSystemHost,
    IProjectileSystemHost,
    IMovementSystemHost,
    ISnapshotSystemHost
{
    SimpleLevel ISimulationWorldState.Level => Level;
    SimulationConfig ISimulationWorldState.Config => Config;
    long ISimulationWorldState.Frame => Frame;
    int ISimulationWorldState.GetSimulationTicksFromSourceTicks(float sourceTicks) => Projectiles.GetSimulationTicksFromSourceTicks(sourceTicks);

    IEnumerable<PlayerEntity> ISimulationPlayerDirectory.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    PlayerEntity? ISimulationPlayerDirectory.FindPlayerById(int playerId) => FindPlayerById(playerId);
    bool ISimulationPlayerDirectory.CanTeamDamagePlayer(PlayerTeam attackerTeam, int attackerId, PlayerEntity target)
        => DamageRules.CanTeamDamagePlayer(attackerTeam, attackerId, target);

    int ISimulationRandomSource.Next(int maximumExclusive) => Randoms.Gameplay.Next(maximumExclusive);
    float ISimulationRandomSource.NextSingle() => Randoms.Gameplay.NextSingle();
    double ISimulationRandomSource.NextDouble() => Randoms.Gameplay.NextDouble();

    IReadOnlyList<SentryEntity> ISimulationStructures.Sentries => WorldObjects.Sentries;
    IReadOnlyList<JumpPadEntity> ISimulationStructures.JumpPads => WorldObjects.JumpPads;
    IReadOnlyList<GeneratorState> ISimulationStructures.Generators => WorldObjects.Generators;
    void ISimulationStructures.DestroyJumpPad(JumpPadEntity jumpPad) => Structures.DestroyJumpPad(jumpPad);

    void ISimulationPresentationEvents.RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId)
        => WorldEffects.RegisterWorldSoundEvent(soundName, x, y, sourcePlayerId);
    void ISimulationPresentationEvents.RegisterSoundEvent(PlayerEntity player, string soundName)
        => WorldEffects.RegisterSoundEvent(player, soundName);
    void ISimulationPresentationEvents.RegisterVisualEffect(string effectName, float x, float y, float direction, int count, bool normalizeDirection)
        => WorldEffects.RegisterVisualEffect(effectName, x, y, direction, count, normalizeDirection);
    void ISimulationPresentationEvents.RegisterImpactEffect(float x, float y, float direction)
        => WorldEffects.RegisterImpactEffect(x, y, direction);
    void ISimulationPresentationEvents.RegisterBloodEffect(float x, float y, float direction, int count)
        => WorldEffects.RegisterBloodEffect(x, y, direction, count);
    void ISimulationPresentationEvents.RegisterCombatTrace(float x, float y, float directionX, float directionY, float distance, bool hitCharacter, PlayerTeam team, bool isSniperTracer, bool isCritical)
        => WorldEffects.RegisterCombatTrace(x, y, directionX, directionY, distance, hitCharacter, team, isSniperTracer, isCritical);
    void ISimulationPresentationEvents.RegisterStuckArrowEffect(float x, float y, float directionX, float directionY, ArrowProjectileEntity arrow)
        => WorldEffects.RegisterStuckArrowEffect(x, y, directionX, directionY, arrow);
    void ISimulationPresentationEvents.RegisterExplosionTraces(float x, float y)
        => ExplosionRules.RegisterExplosionTraces(x, y);

    ExperimentalGameplaySettings ISimulationExperimentalRules.GetLastToDieGameplaySettings(PlayerEntity? player)
        => LastToDieRules.GetLastToDieGameplaySettings(player);
    bool ISimulationExperimentalRules.IsExperimentalPracticePowerOwner(PlayerEntity player)
        => ExperimentalRules.IsExperimentalPracticePowerOwner(player);
    bool ISimulationExperimentalRules.IsExperimentalEngineerPerkOwner(PlayerEntity? player)
        => ExperimentalRules.IsExperimentalEngineerPerkOwner(player);
}
