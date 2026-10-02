namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="ExplosionRulesSystem"/> needs from the world coordinator.
/// </summary>
internal interface IExplosionRulesHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    IReadOnlyList<BubbleProjectileEntity> Bubbles { get; }
    bool ClientPredictionMode { get; }
    CombatRuntimeState CombatRuntime { get; }
    ExperimentalGameplaySettings ExperimentalGameplaySettings { get; }
    IReadOnlyList<MineProjectileEntity> Mines { get; }
    SimulationRandomStreams Randoms { get; }
    IReadOnlyList<RocketProjectileEntity> Rockets { get; }
    WorldObjectStore WorldObjects { get; }

    int ApplyExperimentalAirshotDamageMultiplier(
        PlayerEntity? attacker,
        PlayerEntity target,
        int baseDamage,
        out DamageEventFlags damageFlags);
    void ApplyExplosiveDamageToDamageableZones(
        float originX,
        float originY,
        float blastRadius,
        float damage,
        float splashThresholdFactor = 0f,
        int excludeRoomObjectIndex = -1,
        PlayerTeam? damagingTeam = null,
        float minimumSplashDamage = 0f);
    int ApplyHealingWithFeedback(PlayerEntity target, float healing, string? soundName = null, float soundX = 0f, float soundY = 0f);
    bool ApplyPlayerContinuousDamageWithContext(
        PlayerEntity target,
        float damage,
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
        bool? targetWasGrounded = null);
    bool ApplyPlayerDamageWithContext(
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
    bool ApplySentryDamage(SentryEntity target, int damage, PlayerEntity? attacker);
    void AwardHealingPoints(PlayerEntity healer, int healedAmount);
    void DestroyJumpPad(JumpPadEntity pad);
    void DestroySentry(SentryEntity sentry, PlayerEntity? attacker = null);
    void GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom);
    ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player);
    bool IsExperimentalPracticePowerOwner(PlayerEntity? player);
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
    void RegisterBloodEffect(float x, float y, float directionDegrees, int count = 1);
    void RegisterCombatTrace(
        float originX,
        float originY,
        float directionX,
        float directionY,
        float distance,
        bool hitCharacter,
        PlayerTeam team = PlayerTeam.Red,
        bool isSniperTracer = false,
        bool isCritical = false);
    void RegisterVisualEffect(
        string effectName,
        float x,
        float y,
        float directionDegrees = 0f,
        int count = 1,
        bool normalizeDirection = true);
    void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId = -1);
    void RemoveBubbleAt(int index);
    void RemoveMineAt(int index);
    void RemoveRocketAt(int index);
    bool TryApplyDamageableZoneDamage(int roomObjectIndex, float damage, PlayerTeam? damagingTeam = null);
    void TryApplyExperimentalSoldierRocketHitReloadReward(PlayerEntity? attacker, RocketProjectileEntity rocket, bool hitEnemyPlayer);
    bool TryDamageGenerator(PlayerTeam targetTeam, float damage, PlayerEntity? attacker = null);
}
