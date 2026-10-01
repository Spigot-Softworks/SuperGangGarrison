using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

/// <summary>
/// Inert host for systems constructed without a <see cref="SimulationWorld"/>,
/// such as isolated unit tests. Queries find nothing, rules leave damage
/// unchanged, and world consequences are dropped. When an entity store is
/// supplied, players are looked up in it and allocated entity IDs avoid the
/// IDs it already holds. Tests override individual members
/// to supply the behavior under test.
/// </summary>
internal class DetachedSimulationHost :
    ICombatSystemHost,
    IProjectileSystemHost,
    IMovementSystemHost,
    ISnapshotSystemHost
{
    private readonly EntityStore? _entities;
    private SimpleLevel? _level;
    private int _nextEntityId = 1;

    public DetachedSimulationHost(EntityStore? entities = null)
    {
        _entities = entities;
    }

    // World state
    public virtual SimpleLevel Level => _level ??= SimpleLevelFactory.CreateScoutPrototypeLevel(1f);
    public virtual SimulationConfig Config { get; } = new();
    public virtual long Frame => 0L;
    public virtual int GetSimulationTicksFromSourceTicks(float sourceTicks) => Math.Max(0, (int)MathF.Round(sourceTicks));

    // Players
    public virtual IEnumerable<PlayerEntity> EnumerateSimulatedPlayers()
        => _entities is null ? [] : _entities.All().OfType<PlayerEntity>();
    public virtual PlayerEntity? FindPlayerById(int playerId) => _entities?.Get(playerId) as PlayerEntity;
    public virtual bool CanTeamDamagePlayer(PlayerTeam attackerTeam, int attackerId, PlayerEntity target)
        => target.IsAlive && (attackerId == target.Id || attackerTeam != target.Team);

    // Random source: never rolls an evade.
    public virtual int Next(int maximumExclusive) => 0;
    public virtual float NextSingle() => 0f;
    public virtual double NextDouble() => 1d;

    // Structures
    public virtual IReadOnlyList<SentryEntity> Sentries => [];
    public virtual IReadOnlyList<JumpPadEntity> JumpPads => [];
    public virtual IReadOnlyList<GeneratorState> Generators => [];
    public virtual void DestroyJumpPad(JumpPadEntity jumpPad) { }

    // Presentation
    public virtual void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId) { }
    public virtual void RegisterSoundEvent(PlayerEntity player, string soundName) { }
    public virtual void RegisterVisualEffect(string effectName, float x, float y, float direction, int count, bool normalizeDirection) { }
    public virtual void RegisterImpactEffect(float x, float y, float direction) { }
    public virtual void RegisterBloodEffect(float x, float y, float direction, int count) { }
    public virtual void RegisterCombatTrace(float x, float y, float directionX, float directionY, float distance, bool hitCharacter, PlayerTeam team, bool isSniperTracer, bool isCritical) { }
    public virtual void RegisterStuckArrowEffect(float x, float y, float directionX, float directionY, ArrowProjectileEntity arrow) { }
    public virtual void RegisterExplosionTraces(float x, float y) { }

    // Experimental and Last-To-Die queries
    public virtual ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player) => new();
    public virtual bool IsExperimentalPracticePowerOwner(PlayerEntity player) => false;
    public virtual bool IsExperimentalEngineerPerkOwner(PlayerEntity? player) => false;

    // Combat: damage modifiers
    public virtual int ScaleConfiguredDamage(int damage) => damage;
    public virtual float ScaleConfiguredDamage(float damage) => damage;
    public virtual int ApplyExperimentalOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, int damage) => damage;
    public virtual float ApplyExperimentalOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, float damage) => damage;
    public virtual int ApplyExperimentalIncomingDamageMultiplier(PlayerEntity target, PlayerEntity? attacker, int damage) => damage;
    public virtual float ApplyExperimentalIncomingDamageMultiplier(PlayerEntity target, PlayerEntity? attacker, float damage) => damage;
    public virtual int ApplyLastToDieOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, int damage, PlayerDamageTraits traits, bool? attackerWasGrounded, bool? targetWasGrounded) => damage;
    public virtual float ApplyLastToDieOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, float damage, PlayerDamageTraits traits, bool? attackerWasGrounded, bool? targetWasGrounded) => damage;
    public virtual int ApplyLastToDieIncomingDamageMultiplier(PlayerEntity target, int damage, PlayerDamageTraits traits) => damage;
    public virtual float ApplyLastToDieIncomingDamageMultiplier(PlayerEntity target, float damage, PlayerDamageTraits traits) => damage;
    public virtual int ApplyExperimentalIncomingSentryDamageMultiplier(SentryEntity target, int damage) => damage;
    public virtual float GetExperimentalTotalEvasionChance(PlayerEntity target) => 0f;
    public virtual float GetLastToDieEvasionChance(PlayerEntity target) => 0f;
    public virtual bool RollLastToDieEvasion(PlayerEntity target, float totalEvasionChance) => false;

    // Combat: interceptors
    public virtual bool ShouldCancelDamage(DamageTargetKind targetKind, int targetEntityId, int targetPlayerId, PlayerTeam? targetTeam, PlayerEntity? attacker, int amount, bool wouldBeFatal, float x, float y) => false;
    public virtual bool ShouldCancelDeath(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName) => false;
    public virtual bool TryPreventExperimentalFatalDamage(PlayerEntity target, int damage) => false;
    public virtual bool TryConvertExperimentalSelfDamageToHealing(PlayerEntity target, PlayerEntity? attacker, float damage) => false;
    public virtual bool TryAbsorbPracticeCombatDummyDamage(PlayerEntity target, int damage, PlayerEntity? attacker, DamageEventFlags flags) => false;
    public virtual bool TryAbsorbPracticeCombatDummyContinuousDamage(PlayerEntity target, float damage, PlayerEntity? attacker, DamageEventFlags flags) => false;

    // Combat: consequences
    public virtual void ApplyExperimentalDamageRewards(PlayerEntity? attacker, PlayerEntity target, int damage, bool allowOsmosisHealOwnedSentries) { }
    public virtual void ApplyExperimentalDamageTakenRewards(PlayerEntity target, PlayerEntity? attacker, int damage) { }
    public virtual void ApplyLastToDieDamageRewards(PlayerEntity? attacker, PlayerEntity target, int damage, PlayerDamageTraits traits) { }
    public virtual void ApplyLastToDieDamageTakenEffects(PlayerEntity target, PlayerEntity? attacker, int damage, PlayerDamageTraits traits) { }
    public virtual PlayerEntity? ResolveLastToDieMedicLinkedOnHit(PlayerEntity? attacker, PlayerEntity target, int damage, PlayerDamageTraits traits) => null;
    public virtual int ResolveLastToDieMedicLinkedAssistPlayerId(PlayerEntity? attacker, PlayerEntity? linkedMedic) => -1;
    public virtual void ApplyLastToDieMedicLinkedOnHitEffects(PlayerEntity? attacker, PlayerEntity target, PlayerEntity? linkedMedic) { }
    public virtual void ApplyExperimentalEngineerFriendlyFireRetaliation(PlayerEntity attacker, PlayerEntity target, int damage) { }
    public virtual void TryRegisterCombatComboHit(PlayerEntity? attacker, PlayerEntity target, int damage) { }
    public virtual void TryRegisterBuffBannerDamage(PlayerEntity attacker, PlayerEntity target, int damage) { }
    public virtual (float X, float Y) GetCivvieUmbrellaTip(PlayerEntity target, float aimWorldX, float aimWorldY) => (target.X, target.Y);

    // Snapshots
    public virtual byte FirstSpectatorSlot => 128;
    public virtual bool AreSpecialAbilitiesEnabled => false;
    public virtual bool IsPlayableNetworkPlayerSlot(byte slot) => slot >= 1 && slot <= 40;
    public virtual bool IsNetworkPlayerAwaitingJoin(byte slot) => false;
    public virtual PlayerTeam GetNetworkPlayerConfiguredTeam(byte slot) => PlayerTeam.Red;
    public virtual int GetNetworkPlayerRespawnTicks(byte slot) => 0;
    public virtual bool IsNetworkPlayerReady(byte slot) => false;

    // Movement
    public virtual IEnumerable<ArrowProjectileEntity> EnumerateArrowProjectiles() => [];
    public virtual bool TryFindWhippingCordTerrainContact(PlayerEntity player, GameplayItemDefinition item, float aimWorldX, float aimWorldY, out float contactX, out float contactY)
    {
        contactX = 0f;
        contactY = 0f;
        return false;
    }
    public virtual bool IsWhippingCordTerrainLatchPathClear(PlayerEntity player, float originX, float originY, float directionX, float directionY, float distance) => true;

    // Projectiles: spawn context
    public virtual float GravityScale => 1f;
    public virtual bool IsClientPredictionMode => false;
    public virtual int? AuthoritativeLocalPlayerId => null;
    public virtual int LocalPlayerId => 0;
    public virtual int LocalProjectileTerminationSuppressionTicks => 12;
    public virtual HashSet<int> ClientPredictedProjectileIds { get; } = new();
    public virtual int AllocateEntityId()
    {
        while (_entities?.Contains(_nextEntityId) == true)
        {
            _nextEntityId += 1;
        }

        return _nextEntityId++;
    }
    public virtual void SuppressProjectileRespawn(int projectileId, int ticks) { }

    // Projectiles: hit queries
    public virtual ShotHitResult? GetNearestShotHit(ShotProjectileEntity shot, float directionX, float directionY, float distance) => null;
    public virtual ShotHitResult? GetNearestNeedleHit(NeedleProjectileEntity needle, float directionX, float directionY, float distance) => null;
    public virtual ShotHitResult? GetNearestMedicHealNeedleHit(MedicHealNeedleProjectileEntity needle, float directionX, float directionY, float distance) => null;
    public virtual ShotHitResult? GetNearestRevolverHit(RevolverProjectileEntity shot, float directionX, float directionY, float distance) => null;
    public virtual ShotHitResult? GetNearestBladeHit(BladeProjectileEntity blade, float directionX, float directionY, float distance) => null;
    public virtual ShotHitResult? GetNearestStabHit(StabMaskEntity mask, float directionX, float directionY) => null;
    public virtual ShotHitResult? GetNearestHealstabHit(StabMaskEntity mask, float directionX, float directionY) => null;
    public virtual bool HasStabChainLineOfSight(float x1, float y1, float x2, float y2) => true;
    public virtual RocketHitResult? GetNearestRocketHit(RocketProjectileEntity rocket, float directionX, float directionY, float distance) => null;
    public virtual MineHitResult? GetNearestMineHit(MineProjectileEntity mine, float directionX, float directionY, float distance) => null;
    public virtual GrenadeEnvironmentHit? GetNearestGrenadeEnvironmentHit(GrenadeProjectileEntity grenade, float directionX, float directionY, float distance) => null;
    public virtual PlayerEntity? GetNearestGrenadePlayerHit(GrenadeProjectileEntity grenade, float directionX, float directionY, float distance) => null;
    public virtual bool TryGetGrenadeDamageableZoneContact(GrenadeProjectileEntity grenade, float directionX, float directionY, float maxDistance, out float hitX, out float hitY, out int roomObjectIndex)
    {
        hitX = 0f;
        hitY = 0f;
        roomObjectIndex = -1;
        return false;
    }
    public virtual FlameHitResult? GetNearestFlameHit(FlameProjectileEntity flame, float directionX, float directionY, float distance) => null;
    public virtual ShotHitResult? GetNearestFlareHit(FlareProjectileEntity flare, float directionX, float directionY, float distance) => null;
    public virtual ShotHitResult? GetNearestFlareHit(FlareProjectileEntity flare, float directionX, float directionY, float distance, bool includePlayers) => null;
    public virtual ShotHitResult? GetNearestFlarePlayerHit(FlareProjectileEntity flare, float directionX, float directionY, float distance, ShotHitResult? blockingHit) => null;
    public virtual bool IsProjectilePathBlocked(float x1, float y1, float x2, float y2, PlayerTeam team) => false;
    public virtual bool TryInterceptWithCivilDefenseTurret(PlayerTeam team, float x, float y, float directionX, float directionY, float distance) => false;
    public virtual void GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom)
    {
        left = player.Left;
        top = player.Top;
        right = player.Right;
        bottom = player.Bottom;
    }
    public virtual float GetExplosionDistanceToPlayer(PlayerEntity player, float x, float y) => 0f;

    // Projectiles: impact targets
    public virtual void KillPlayer(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName, DeadBodyAnimationKind deadBodyAnimationKind) { }
    public virtual void DestroySentry(SentryEntity sentry, PlayerEntity? attacker) { }
    public virtual bool ApplySentryDamage(SentryEntity sentry, int damage, PlayerEntity? attacker) => false;
    public virtual bool ApplyGeneratorDamage(GeneratorState generator, float damage, PlayerEntity? attacker) => false;
    public virtual bool TryDamageGenerator(PlayerTeam team, float damage, PlayerEntity? attacker) => false;
    public virtual void ApplyJumpPadDamage(JumpPadEntity jumpPad, int damage) { }
    public virtual void ApplyExplosiveDamageToJumpPads(float x, float y, float radius, float damage, PlayerTeam team, float minimumDamage) { }
    public virtual void ApplyExplosiveDamageToDamageableZones(float x, float y, float radius, float damage, float splashThresholdFactor, int excludeRoomObjectIndex, PlayerTeam? damagingTeam, float minimumSplashDamage) { }
    public virtual bool TryHandleProjectileDamageableZoneHit(in ShotHitResult hit, float damage, PlayerTeam team) => false;
    public virtual bool TryApplyDamageableZoneDamage(int roomObjectIndex, float damage, PlayerTeam? team) => false;
    public virtual bool BlocksProjectileDamageableZone(int roomObjectIndex) => false;
    public virtual int ApplyHealingWithFeedback(PlayerEntity player, float healing, string? soundName, float x, float y) => 0;

    // Projectiles: explosions
    public virtual void ExplodeRocket(RocketProjectileEntity rocket, PlayerEntity? directHitPlayer, SentryEntity? directHitSentry, GeneratorState? directHitGenerator, int damageableZoneIndex) { }
    public virtual void ApplyDeadBodyExplosionImpulse(float x, float y, float radius, float impulse, float? falloff) { }
    public virtual void ApplyPlayerGibExplosionImpulse(float x, float y, float radius, float impulse, float? falloff) { }
    public virtual void ApplyMineExplosionImpulse(PlayerEntity player, float x, float y, float factor) { }
    public virtual bool ShouldSkipFriendlyExplosionBoost(PlayerEntity player, PlayerTeam team, int ownerId) => false;
    public virtual bool ShouldIgnoreFriendlyGroundedBlast(PlayerEntity player, PlayerTeam team, int ownerId) => false;

    // Projectiles: gameplay rules
    public virtual float ExperimentalSoldierStingerTurnRateRadians => 0f;
    public virtual float ExperimentalEngineerCaveatTurnRateRadians => 0f;
    public virtual string? GetKillFeedWeaponSprite(PlayerEntity? owner) => null;
    public virtual int ApplyExperimentalAirshotDamageMultiplier(PlayerEntity? owner, PlayerEntity target, int damage, out DamageEventFlags flags)
    {
        flags = DamageEventFlags.None;
        return damage;
    }
    public virtual void ApplyExperimentalSentryPlayerHit(SentryEntity sentry, PlayerEntity owner, PlayerEntity target, int damage, PlayerDamageTraits additionalTraits, bool criticalBoost, bool useLiveAttackerCriticalBoost, float? threatSourceX, float? threatSourceY, BulletKnockbackPayload? knockbackPayload, float? impactDirectionX, float? impactDirectionY) { }
    public virtual void ApplyExperimentalSentryDamageRewards(SentryEntity sentry, PlayerEntity owner, int damage) { }
    public virtual bool TryResolveExperimentalEngineerRocketTrackingDirection(RocketProjectileEntity rocket, PlayerEntity player, out float directionRadians)
    {
        directionRadians = 0f;
        return false;
    }
    public virtual void TrySpawnExperimentalDemoknightDecapitationRemains(PlayerEntity player, float directionX, float directionY) { }
    public virtual void ApplyMedicHealNeedleTeammateHit(PlayerEntity? medic, PlayerEntity target, MedicHealNeedleProjectileEntity needle) { }
    public virtual void ExplodeBoomstickPellet(ShotProjectileEntity shot) { }
    public virtual bool TryApplyLastToDieSniperGuardian(PlayerEntity sniper, PlayerEntity target) => false;
    public virtual void TryApplyLastToDieSniperStatusPayload(PlayerEntity owner, PlayerEntity target, bool tranquilize, float poison) { }
    public virtual bool TryApplyLastToDieStatusEffect(int targetId, int sourceId, LastToDieStatusEffectSpec spec) => false;
    public virtual LastToDieMedicKritzM2Payload CaptureLastToDieMedicKritzM2Payload(PlayerEntity owner) => default;
    public virtual bool TryExplodeLastToDieSniperArrow(ArrowProjectileEntity arrow, float x, float y) => false;
    public virtual bool TryExplodeLastToDieMedicJavelin(MedicHealNeedleProjectileEntity needle) => false;
}
