using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="LastToDieRulesSystem"/> needs from the world coordinator.
/// Wide because Last-To-Die perks touch damage, healing, network slots and
/// objectives; each member is a capability, never a back-reference to the world.
/// </summary>
internal interface ILastToDieHost : ISimulationWorldState, ISimulationPlayerDirectory, ISimulationPresentationEvents
{
    LastToDieState LastToDieState { get; }
    IReadOnlyList<byte> NetworkPlayerSlots { get; }
    bool ClientPredictionMode { get; }
    ExperimentalGameplaySettings ExperimentalGameplaySettings { get; }
    MatchRules MatchRules { get; }
    MatchSettingsState MatchSettings { get; }
    ObjectiveStateStore Objectives { get; }
    IReadOnlyList<ControlPointState> ControlPoints { get; }
    IReadOnlyList<NeedleProjectileEntity> Needles { get; }

    // Network players.
    bool TryGetPlayerNetworkSlot(PlayerEntity player, out byte slot);
    bool TryGetNetworkPlayerSlot(PlayerEntity player, out byte slot);
    bool TryGetNetworkPlayer(byte slot, out PlayerEntity player);
    void SyncExperimentalGameplayLoadout(byte slot, PlayerEntity player);
    bool TrySetNetworkPlayerAutomaticRespawnSuppressed(byte slot, bool suppressed);
    bool TrySetNetworkPlayerMaxHealthOverride(byte slot, int? maxHealth, bool refillHealth = true);
    bool TrySetNetworkPlayerScale(byte slot, float scale);

    // Damage, death and healing.
    PlayerDamageResolution ResolvePlayerDamage(PlayerEntity target, in PlayerDamageRequest request);
    PlayerDamageResolution ResolvePlayerDamageWithContext(
        PlayerEntity target,
        int damage,
        PlayerEntity? attacker,
        float spyRevealAlpha = 0f,
        DamageEventFlags damageFlags = DamageEventFlags.None,
        bool allowOsmosisHealOwnedSentries = true,
        bool allowCivvieUmbrellaShield = true,
        float? civvieUmbrellaThreatSourceX = null,
        float? civvieUmbrellaThreatSourceY = null,
        int? civvieUmbrellaDrainTicks = null,
        bool civvieUmbrellaCriticalBoost = false,
        bool civvieUmbrellaUseLiveAttackerCriticalBoost = true,
        PlayerDamageTraits additionalTraits = PlayerDamageTraits.None,
        bool? attackerWasGrounded = null,
        bool? targetWasGrounded = null,
        int sourceEntityId = 0,
        ulong attackId = 0,
        int attackerPlayerIdOverride = -1);
    void MarkPendingFatalPlayerDamageEventPrevented(int playerId);
    void KillPlayer(
        PlayerEntity player,
        bool gibbed = false,
        PlayerEntity? killer = null,
        string? weaponSpriteName = null,
        DeadBodyAnimationKind deadBodyAnimationKind = DeadBodyAnimationKind.Default,
        string? deathCamMessage = null,
        SentryEntity? deathCamSentry = null,
        string? killFeedMessage = null,
        bool createDeathCam = true,
        bool spawnRemains = true,
        bool forceCorpseRemains = false,
        bool recordKillFeed = true,
        int assistingPlayerIdOverride = -1,
        bool completingLastToDieSpyAfterlifeDeath = false);
    bool CanMedicHealTarget(PlayerEntity medic, PlayerEntity target);
    int ApplyHealingWithFeedback(PlayerEntity target, float healing, string? soundName = null, float soundX = 0f, float soundY = 0f);
    void AwardHealingPoints(PlayerEntity healer, int healedAmount);

    // Geometry, projectiles and objectives.
    void GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom);
    bool HasObstacleLineOfSight(float originX, float originY, float targetX, float targetY);
    void RemoveNeedleAt(int index);
    void TryDropCarriedIntel(PlayerEntity player);
    bool IsPlayerInControlPointCaptureZone(PlayerEntity player, int controlPointIndex);
}
