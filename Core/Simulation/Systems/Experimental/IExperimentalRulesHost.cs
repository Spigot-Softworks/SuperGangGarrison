namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="ExperimentalRulesSystem"/> needs from the world coordinator.
/// Experimental perk settings are still resolved by <see cref="LastToDieRulesSystem"/>
/// (Last-To-Die runs carry their own legacy settings per slot), so this host
/// only reads them; it does not own them.
/// </summary>
internal interface IExperimentalRulesHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    WorldBounds Bounds { get; }
    ExperimentalGameplaySettings ExperimentalGameplaySettings { get; set; }
    SimulationRandomStreams Randoms { get; }
    IReadOnlyList<RocketProjectileEntity> Rockets { get; }
    WorldObjectStore WorldObjects { get; }
    PlayerEntity LocalPlayer { get; }
    CombatRuntimeState CombatRuntime { get; }
    MatchSettingsState MatchSettings { get; }
    ObjectiveStateStore Objectives { get; }

    int ApplyHealingWithFeedback(PlayerEntity target, float healing, string? soundName = null, float soundX = 0f, float soundY = 0f);
    bool ApplySentryDamage(SentryEntity target, int damage, PlayerEntity? attacker);
    void DestroySentry(SentryEntity sentry, PlayerEntity? attacker = null);
    void FlushExperimentalEngineerEssenceExtractorHealing(PlayerEntity engineer);
    bool IsLastToDieDroneSentry(SentryEntity sentry);
    bool IsProjectileSpawnBlocked(float originX, float originY, float targetX, float targetY, PlayerTeam shotTeam);
    int ReflectEnemyExplosiveProjectiles(
        PlayerEntity player,
        float aimRadians,
        float poofX,
        float poofY,
        bool radial = false,
        float radialRadius = SimulationConstants.PyroAirblastDistance);
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
    void RegisterImpactEffect(float x, float y, float directionDegrees);
    void RegisterVisualEffect(
        string effectName,
        float x,
        float y,
        float directionDegrees = 0f,
        int count = 1,
        bool normalizeDirection = true);
    void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId = -1);
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
    RifleHitResult ResolveRifleHit(PlayerEntity attacker, float directionX, float directionY, float maxDistance);
    RifleHitResult ResolveRifleHit(
        PlayerEntity attacker,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float maxDistance);
    void SpawnFlame(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float directHitDamage = FlameProjectileEntity.DirectHitDamage,
        float burnDamagePerTick = FlameProjectileEntity.BurnDamagePerTick);
    void SpawnRocket(
        PlayerEntity owner,
        float x,
        float y,
        float speed,
        float directionRadians,
        RocketCombatDefinition? rocketCombat = null,
        float directHitHealAmount = 0f,
        bool explodeImmediately = false,
        bool canGrantExperimentalInstantReloadOnHit = true,
        float knockbackScale = 1f,
        bool canIgniteTargets = false,
        bool enableExperimentalStingerTracking = false,
        bool enableExperimentalCaveatTracking = false,
        float experimentalVisualScale = 1f,
        int experimentalTrackingLockTicksRemaining = 0,
        bool isBallistic = false,
        float ballisticGravityPerTick = 0f,
        bool suppressSmokeTrail = false,
        string? killFeedWeaponSpriteNameOverride = null);
    void SpawnShot(
        PlayerEntity owner,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float damagePerHit = ShotProjectileEntity.DamagePerHit,
        bool forceGibOnKill = false,
        string? killFeedWeaponSpriteNameOverride = null,
        int? sourceSentryId = null,
        bool applyExperimentalEngineerSentryPerkEffects = false,
        float playerKnockbackScale = 1f,
        float? playerSlowMovementMultiplier = null,
        int playerSlowRefreshTicks = 0,
        float? playerKnockbackImpulse = null,
        float playerKnockbackAirborneVerticalScale = 1f,
        float playerKnockbackGroundedVerticalScale = 1f,
        bool isBoomstickPellet = false);
    bool TryApplyDamageableZoneDamage(int roomObjectIndex, float damage, PlayerTeam? damagingTeam = null);
    bool TryDamageGenerator(PlayerTeam targetTeam, float damage, PlayerEntity? attacker = null);
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
    ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player);
    bool IsLastToDieGameplaySettingEnabled(Func<ExperimentalGameplaySettings, bool> selector);
    bool TryGetLastToDieLegacyGameplaySettings(byte slot, out ExperimentalGameplaySettings settings);
    bool TryGetPlayerNetworkSlot(PlayerEntity player, out byte slot);
    void ApplyNetworkPlayerMaxHealthOverride(byte slot, PlayerEntity player, bool refillHealth);
    void ClearTemporaryHealthPacks();
    void ClearDroppedWeapons();
    bool IsNetworkPlayerEnabled(byte slot);
    bool TryGetNetworkPlayer(byte slot, out PlayerEntity player);
}
