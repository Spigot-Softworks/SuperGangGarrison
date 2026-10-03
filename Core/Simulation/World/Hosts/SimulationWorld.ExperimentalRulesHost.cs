namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IExperimentalRulesHost
{
    bool IExperimentalRulesHost.ApplyPlayerDamageWithContext(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost, bool civvieUmbrellaUseLiveAttackerCriticalBoost, PlayerDamageTraits additionalTraits, bool? attackerWasGrounded, bool? targetWasGrounded, int sourceEntityId, ulong attackId, int attackerPlayerIdOverride)
        => Combat.ApplyPlayerDamageWithContext(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost, civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits, attackerWasGrounded, targetWasGrounded, sourceEntityId, attackId, attackerPlayerIdOverride);
    WorldBounds IExperimentalRulesHost.Bounds => Bounds;
    CombatSystem IExperimentalRulesHost.Combat => Combat;
    CombatRuntimeState IExperimentalRulesHost.CombatRuntime => CombatRuntime;
    DamageRulesSystem IExperimentalRulesHost.DamageRules => DamageRules;
    ExperimentalGameplaySettings IExperimentalRulesHost.ExperimentalGameplaySettings { get => ExperimentalGameplaySettings; set => ExperimentalGameplaySettings = value; }
    CombatResolver IExperimentalRulesHost.GeometryResolver => GeometryResolver;
    LastToDieRulesSystem IExperimentalRulesHost.LastToDieRules => LastToDieRules;
    PlayerEntity IExperimentalRulesHost.LocalPlayer => LocalPlayer;
    MapLogicSystem IExperimentalRulesHost.MapLogic => MapLogic;
    MatchSettingsState IExperimentalRulesHost.MatchSettings => MatchSettings;
    NetworkPlayerSystem IExperimentalRulesHost.NetworkPlayers => NetworkPlayers;
    ObjectiveRulesSystem IExperimentalRulesHost.ObjectiveRules => ObjectiveRules;
    ObjectiveStateStore IExperimentalRulesHost.Objectives => Objectives;
    PickupSystem IExperimentalRulesHost.Pickups => Pickups;
    PlayerDeathSystem IExperimentalRulesHost.PlayerDeaths => PlayerDeaths;
    SimulationRandomStreams IExperimentalRulesHost.Randoms => Randoms;
    int IExperimentalRulesHost.ReflectEnemyExplosiveProjectiles(PlayerEntity player, float aimRadians, float poofX, float poofY, bool radial, float radialRadius)
        => AirblastRules.ReflectEnemyExplosiveProjectiles(player, aimRadians, poofX, poofY, radial, radialRadius);
    PlayerDamageResolution IExperimentalRulesHost.ResolvePlayerDamageWithContext(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost, bool civvieUmbrellaUseLiveAttackerCriticalBoost, PlayerDamageTraits additionalTraits, bool? attackerWasGrounded, bool? targetWasGrounded, int sourceEntityId, ulong attackId, int attackerPlayerIdOverride)
        => Combat.ResolvePlayerDamageWithContext(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost, civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits, attackerWasGrounded, targetWasGrounded, sourceEntityId, attackId, attackerPlayerIdOverride);
    IReadOnlyList<RocketProjectileEntity> IExperimentalRulesHost.Rockets => Rockets;
    ServerTuningSystem IExperimentalRulesHost.ServerTuning => ServerTuning;
    void IExperimentalRulesHost.SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float directHitDamage, float burnDamagePerTick)
        => Projectiles.SpawnFlame(owner, x, y, velocityX, velocityY, directHitDamage, burnDamagePerTick);
    void IExperimentalRulesHost.SpawnRocket(PlayerEntity owner, float x, float y, float speed, float directionRadians, RocketCombatDefinition? rocketCombat, float directHitHealAmount, bool explodeImmediately, bool canGrantExperimentalInstantReloadOnHit, float knockbackScale, bool canIgniteTargets, bool enableExperimentalStingerTracking, bool enableExperimentalCaveatTracking, float experimentalVisualScale, int experimentalTrackingLockTicksRemaining, bool isBallistic, float ballisticGravityPerTick, bool suppressSmokeTrail, string? killFeedWeaponSpriteNameOverride)
        => Projectiles.SpawnRocket(owner, x, y, speed, directionRadians, rocketCombat, directHitHealAmount, explodeImmediately, canGrantExperimentalInstantReloadOnHit, knockbackScale, canIgniteTargets, enableExperimentalStingerTracking, enableExperimentalCaveatTracking, experimentalVisualScale, experimentalTrackingLockTicksRemaining, isBallistic, ballisticGravityPerTick, suppressSmokeTrail, killFeedWeaponSpriteNameOverride);
    void IExperimentalRulesHost.SpawnShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit, bool forceGibOnKill, string? killFeedWeaponSpriteNameOverride, int? sourceSentryId, bool applyExperimentalEngineerSentryPerkEffects, float playerKnockbackScale, float? playerSlowMovementMultiplier, int playerSlowRefreshTicks, float? playerKnockbackImpulse, float playerKnockbackAirborneVerticalScale, float playerKnockbackGroundedVerticalScale, bool isBoomstickPellet)
        => Projectiles.SpawnShot(owner, x, y, velocityX, velocityY, damagePerHit, forceGibOnKill, killFeedWeaponSpriteNameOverride, sourceSentryId, applyExperimentalEngineerSentryPerkEffects, playerKnockbackScale, playerSlowMovementMultiplier, playerSlowRefreshTicks, playerKnockbackImpulse, playerKnockbackAirborneVerticalScale, playerKnockbackGroundedVerticalScale, isBoomstickPellet);
    StructureSystem IExperimentalRulesHost.Structures => Structures;
    SupportRulesSystem IExperimentalRulesHost.SupportRules => SupportRules;
    WorldEffectsSystem IExperimentalRulesHost.WorldEffects => WorldEffects;
    WorldObjectStore IExperimentalRulesHost.WorldObjects => WorldObjects;
}
