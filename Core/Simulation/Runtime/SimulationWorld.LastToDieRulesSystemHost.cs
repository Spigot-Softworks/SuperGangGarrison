using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ILastToDieHost
{
    LastToDieState ILastToDieHost.LastToDieState => LastToDieState;
    IReadOnlyList<byte> ILastToDieHost.NetworkPlayerSlots => NetworkPlayerSlots;
    bool ILastToDieHost.ClientPredictionMode => ClientPredictionMode;
    ExperimentalGameplaySettings ILastToDieHost.ExperimentalGameplaySettings => ExperimentalGameplaySettings;
    MatchRules ILastToDieHost.MatchRules => MatchRules;
    MatchSettingsState ILastToDieHost.MatchSettings => MatchSettings;
    ObjectiveStateStore ILastToDieHost.Objectives => Objectives;
    IReadOnlyList<ControlPointState> ILastToDieHost.ControlPoints => ControlPoints;
    IReadOnlyList<NeedleProjectileEntity> ILastToDieHost.Needles => Needles;

    bool ILastToDieHost.TryGetPlayerNetworkSlot(PlayerEntity player, out byte slot) => NetworkPlayerRules.TryGetPlayerNetworkSlot(player, out slot);
    bool ILastToDieHost.TryGetNetworkPlayerSlot(PlayerEntity player, out byte slot) => NetworkPlayerRules.TryGetNetworkPlayerSlot(player, out slot);
    bool ILastToDieHost.TryGetNetworkPlayer(byte slot, out PlayerEntity player) => NetworkPlayerRules.TryGetNetworkPlayer(slot, out player);
    void ILastToDieHost.SyncExperimentalGameplayLoadout(byte slot, PlayerEntity player) => SyncExperimentalGameplayLoadout(slot, player);
    bool ILastToDieHost.TrySetNetworkPlayerAutomaticRespawnSuppressed(byte slot, bool suppressed)
        => NetworkPlayerRules.TrySetNetworkPlayerAutomaticRespawnSuppressed(slot, suppressed);
    bool ILastToDieHost.TrySetNetworkPlayerMaxHealthOverride(byte slot, int? maxHealth, bool refillHealth)
        => ServerTuning.TrySetNetworkPlayerMaxHealthOverride(slot, maxHealth, refillHealth);
    bool ILastToDieHost.TrySetNetworkPlayerScale(byte slot, float scale) => ServerTuning.TrySetNetworkPlayerScale(slot, scale);

    PlayerDamageResolution ILastToDieHost.ResolvePlayerDamage(PlayerEntity target, in PlayerDamageRequest request) => ResolvePlayerDamage(target, request);
    PlayerDamageResolution ILastToDieHost.ResolvePlayerDamageWithContext(
        PlayerEntity target,
        int damage,
        PlayerEntity? attacker,
        float spyRevealAlpha,
        DamageEventFlags damageFlags,
        bool allowOsmosisHealOwnedSentries,
        bool allowCivvieUmbrellaShield,
        float? civvieUmbrellaThreatSourceX,
        float? civvieUmbrellaThreatSourceY,
        int? civvieUmbrellaDrainTicks,
        bool civvieUmbrellaCriticalBoost,
        bool civvieUmbrellaUseLiveAttackerCriticalBoost,
        PlayerDamageTraits additionalTraits,
        bool? attackerWasGrounded,
        bool? targetWasGrounded,
        int sourceEntityId,
        ulong attackId,
        int attackerPlayerIdOverride)
        => ResolvePlayerDamageWithContext(
            target,
            damage,
            attacker,
            spyRevealAlpha,
            damageFlags,
            allowOsmosisHealOwnedSentries,
            allowCivvieUmbrellaShield,
            civvieUmbrellaThreatSourceX,
            civvieUmbrellaThreatSourceY,
            civvieUmbrellaDrainTicks,
            civvieUmbrellaCriticalBoost,
            civvieUmbrellaUseLiveAttackerCriticalBoost,
            additionalTraits,
            attackerWasGrounded,
            targetWasGrounded,
            sourceEntityId,
            attackId,
            attackerPlayerIdOverride);
    void ILastToDieHost.MarkPendingFatalPlayerDamageEventPrevented(int playerId) => MarkPendingFatalPlayerDamageEventPrevented(playerId);
    void ILastToDieHost.KillPlayer(
        PlayerEntity player,
        bool gibbed,
        PlayerEntity? killer,
        string? weaponSpriteName,
        DeadBodyAnimationKind deadBodyAnimationKind,
        string? deathCamMessage,
        SentryEntity? deathCamSentry,
        string? killFeedMessage,
        bool createDeathCam,
        bool spawnRemains,
        bool forceCorpseRemains,
        bool recordKillFeed,
        int assistingPlayerIdOverride,
        bool completingLastToDieSpyAfterlifeDeath)
        => PlayerDeaths.KillPlayer(
            player,
            gibbed,
            killer,
            weaponSpriteName,
            deadBodyAnimationKind,
            deathCamMessage,
            deathCamSentry,
            killFeedMessage,
            createDeathCam,
            spawnRemains,
            forceCorpseRemains,
            recordKillFeed,
            assistingPlayerIdOverride,
            completingLastToDieSpyAfterlifeDeath);
    bool ILastToDieHost.CanMedicHealTarget(PlayerEntity medic, PlayerEntity target) => SupportRules.CanMedicHealTarget(medic, target);
    int ILastToDieHost.ApplyHealingWithFeedback(PlayerEntity target, float healing, string? soundName, float soundX, float soundY)
        => DamageRules.ApplyHealingWithFeedback(target, healing, soundName, soundX, soundY);
    void ILastToDieHost.AwardHealingPoints(PlayerEntity healer, int healedAmount) => Scorekeeping.AwardHealingPoints(healer, healedAmount);

    void ILastToDieHost.GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom)
        => PresentationBounds.GetCachedPlayerPresentationHitBounds(player, out left, out top, out right, out bottom);
    bool ILastToDieHost.HasObstacleLineOfSight(float originX, float originY, float targetX, float targetY)
        => HasObstacleLineOfSight(originX, originY, targetX, targetY);
    void ILastToDieHost.RemoveNeedleAt(int index) => RemoveNeedleAt(index);
    void ILastToDieHost.TryDropCarriedIntel(PlayerEntity player) => ObjectiveRules.TryDropCarriedIntel(player);
    bool ILastToDieHost.IsPlayerInControlPointCaptureZone(PlayerEntity player, int controlPointIndex)
        => ObjectiveRules.IsPlayerInControlPointCaptureZone(player, controlPointIndex);
}
