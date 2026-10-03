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
    CombatSystem Combat { get; }
    CombatRuntimeState CombatRuntime { get; }
    DamageRulesSystem DamageRules { get; }
    ExperimentalGameplaySettings ExperimentalGameplaySettings { get; set; }
    CombatResolver GeometryResolver { get; }
    LastToDieRulesSystem LastToDieRules { get; }
    PlayerEntity LocalPlayer { get; }
    MapLogicSystem MapLogic { get; }
    MatchSettingsState MatchSettings { get; }
    NetworkPlayerSystem NetworkPlayers { get; }
    ObjectiveRulesSystem ObjectiveRules { get; }
    ObjectiveStateStore Objectives { get; }
    PickupSystem Pickups { get; }
    PlayerDeathSystem PlayerDeaths { get; }
    SimulationRandomStreams Randoms { get; }
    IReadOnlyList<RocketProjectileEntity> Rockets { get; }
    ServerTuningSystem ServerTuning { get; }
    StructureSystem Structures { get; }
    SupportRulesSystem SupportRules { get; }
    WorldEffectsSystem WorldEffects { get; }
    WorldObjectStore WorldObjects { get; }

    int ReflectEnemyExplosiveProjectiles(
        PlayerEntity player,
        float aimRadians,
        float poofX,
        float poofY,
        bool radial = false,
        float radialRadius = SimulationConstants.PyroAirblastDistance);
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
}
