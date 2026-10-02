namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IPlayerRemainsHost
{
    int IPlayerRemainsHost.AllocateEntityId()
        => AllocateEntityId();
    bool IPlayerRemainsHost.ClientPredictionMode => ClientPredictionMode;
    EntityStore IPlayerRemainsHost.EntityStore => EntityStore;
    bool IPlayerRemainsHost.LocalGoreEffectsEnabled => LocalGoreEffectsEnabled;
    PresentationEventLog IPlayerRemainsHost.PresentationEvents => PresentationEvents;
    SimulationRandomStreams IPlayerRemainsHost.Randoms => Randoms;
    void IPlayerRemainsHost.RegisterVisualEffect(string effectName, float x, float y, float directionDegrees, int count, bool normalizeDirection)
        => WorldEffects.RegisterVisualEffect(effectName, x, y, directionDegrees, count, normalizeDirection);
    void IPlayerRemainsHost.RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId)
        => WorldEffects.RegisterWorldSoundEvent(soundName, x, y, sourcePlayerId);
    int IPlayerRemainsHost.ScaleBloodDropLifetimeTicks()
        => ScaleBloodDropLifetimeTicks();
    void IPlayerRemainsHost.SpawnDeadBody(PlayerEntity player, DeadBodyAnimationKind animationKind, PlayerEntity? killer, string? weaponSpriteName, SentryEntity? knockbackOriginSentry)
        => PlayerDeaths.SpawnDeadBody(player, animationKind, killer, weaponSpriteName, knockbackOriginSentry);
    WorldObjectStore IPlayerRemainsHost.WorldObjects => WorldObjects;
}
