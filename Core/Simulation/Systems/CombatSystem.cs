namespace OpenGarrison.Core;

/// <summary>
/// Applies combat damage, records ordered damage outcomes, and resolves the
/// entity-local part of a death. The system owns health mutation, modifiers,
/// assists, friendly-fire admission, and the damage-event queue. It receives
/// the remaining simulation concerns through <see cref="ICombatSystemHost"/>: mode/perk
/// modifiers, decision interceptors, presentation feedback, and the shared
/// random source. Level geometry, projectile queries, networking, rendering,
/// kill-feed/respawn consequences, and mode-specific death consequences stay
/// in the world coordinator because moving them here would couple this system
/// to the whole world rather than to entity state.
/// </summary>
public sealed class CombatSystem
{
    private const float AssistTrackingSourceTicks = 210f;
    public const float ExplosiveSplashRadiusMultiplier = 1.2f;
    public const float ExplosiveSplashMinimumDamage = 25f;

    private readonly EntityStore _entities;
    private readonly ICombatSystemHost _host;
    private readonly List<WorldDamageEvent> _pendingDamageEvents = new();

    public CombatSystem(EntityStore entities)
        : this(entities, new DetachedSimulationHost())
    {
    }

    internal CombatSystem(EntityStore entities, ICombatSystemHost host)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public IReadOnlyList<WorldDamageEvent> PendingDamageEvents => _pendingDamageEvents;

    public IReadOnlyList<WorldDamageEvent> DrainPendingDamageEvents()
    {
        if (_pendingDamageEvents.Count == 0)
        {
            return [];
        }

        var damageEvents = _pendingDamageEvents.ToArray();
        _pendingDamageEvents.Clear();
        return damageEvents;
    }

    public void ClearPendingDamageEvents() => _pendingDamageEvents.Clear();

    public bool CanDamagePlayer(PlayerTeam attackerTeam, int attackerId, PlayerEntity target)
    {
        return _host.CanTeamDamagePlayer(attackerTeam, attackerId, target);
    }

    /// <summary>
    /// Runs the entity-independent death admission check. The world invokes
    /// its mode, networking, and presentation consequences only after this
    /// returns <see langword="true"/>.
    /// </summary>
    internal bool TryBeginPlayerDeath(
        PlayerEntity player,
        bool gibbed,
        PlayerEntity? killer,
        string? weaponSpriteName)
    {
        return !ShouldCancelDeath(player, gibbed, killer, weaponSpriteName);
    }

    /// <summary>Applies damage after resolving the target and attacker IDs through EntityStore.</summary>
    public bool TryApplyPlayerDamage(int targetEntityId, int damage, int attackerEntityId = -1)
    {
        if (_entities.Get(targetEntityId) is not PlayerEntity target)
        {
            return false;
        }

        var attacker = attackerEntityId > 0
            ? _entities.Get(attackerEntityId) as PlayerEntity
            : null;
        if (attacker is not null && !CanDamagePlayer(attacker.Team, attacker.Id, target))
        {
            return false;
        }

        return ApplyPlayerDamageWithContext(target, damage, attacker);
    }

    public static float ResolveExplosiveSplashRadius(float baseRadius)
    {
        return float.IsFinite(baseRadius)
            ? MathF.Max(0f, baseRadius) * ExplosiveSplashRadiusMultiplier
            : 0f;
    }

    public static float ResolveExplosiveSplashDamage(float maximumDamage, float distanceFactor)
        => ResolveExplosiveSplashDamage(maximumDamage, distanceFactor, ExplosiveSplashMinimumDamage);

    public static float ResolveExplosiveSplashDamage(
        float maximumDamage,
        float distanceFactor,
        float minimumDamage)
    {
        if (!float.IsFinite(maximumDamage)
            || !float.IsFinite(distanceFactor)
            || maximumDamage <= 0f
            || distanceFactor <= 0f)
        {
            return 0f;
        }

        return MathF.Max(
            MathF.Max(0f, minimumDamage),
            maximumDamage * Math.Clamp(distanceFactor, 0f, 1f));
    }

    public static float ResolveExplosiveSplashDamageAtDistance(
        float maximumDamage,
        float distance,
        float blastRadius,
        float minimumDamage = ExplosiveSplashMinimumDamage)
    {
        if (!float.IsFinite(distance)
            || !float.IsFinite(blastRadius)
            || blastRadius <= 0f)
        {
            return 0f;
        }

        return ResolveExplosiveSplashDamage(
            maximumDamage,
            1f - (distance / blastRadius),
            minimumDamage);
    }

    internal void RegisterDamageEvent(
        PlayerEntity? attacker,
        DamageTargetKind targetKind,
        int targetEntityId,
        float x,
        float y,
        int amount,
        bool wasFatal,
        PlayerEntity? playerTarget = null,
        DamageEventFlags flags = DamageEventFlags.None,
        int assistPlayerIdOverride = -1,
        int attackerPlayerIdOverride = -1)
    {
        if (amount <= 0 && !flags.HasFlag(DamageEventFlags.Evaded))
        {
            return;
        }

        var attackerPlayerId = attackerPlayerIdOverride > 0
            ? attackerPlayerIdOverride
            : attacker?.Id ?? -1;
        var assistedByPlayerId = ResolveDamageEventAssistPlayerId(
            attacker,
            playerTarget,
            targetKind,
            wasFatal,
            assistPlayerIdOverride);
        _pendingDamageEvents.Add(new WorldDamageEvent(
            amount,
            attackerPlayerId,
            assistedByPlayerId,
            targetKind,
            targetEntityId,
            x,
            y,
            wasFatal,
            flags,
            SourceFrame: (ulong)_host.Frame));
        if (targetKind == DamageTargetKind.Player
            && attacker is not null
            && playerTarget is not null)
        {
            _host.TryRegisterBuffBannerDamage(attacker, playerTarget, amount);
        }
    }

    internal void MarkPendingFatalPlayerDamageEventGibbed(int playerId)
    {
        for (var index = _pendingDamageEvents.Count - 1; index >= 0; index -= 1)
        {
            var damageEvent = _pendingDamageEvents[index];
            if (!damageEvent.WasFatal
                || damageEvent.TargetKind != DamageTargetKind.Player
                || damageEvent.TargetEntityId != playerId)
            {
                continue;
            }

            _pendingDamageEvents[index] = damageEvent with
            {
                Flags = damageEvent.Flags | DamageEventFlags.Gibbed,
            };
            return;
        }
    }

    internal void MarkPendingFatalPlayerDamageEventPrevented(int playerId)
    {
        for (var index = _pendingDamageEvents.Count - 1; index >= 0; index -= 1)
        {
            var damageEvent = _pendingDamageEvents[index];
            if (!damageEvent.WasFatal
                || damageEvent.TargetKind != DamageTargetKind.Player
                || damageEvent.TargetEntityId != playerId)
            {
                continue;
            }

            _pendingDamageEvents[index] = damageEvent with { WasFatal = false };
            return;
        }
    }

    internal bool ApplyPlayerDamage(
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
        bool civvieUmbrellaCriticalBoost = false)
    {
        return ApplyPlayerDamageWithContext(
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
            attackerWasGrounded: attacker?.IsGrounded,
            targetWasGrounded: target.IsGrounded);
    }

    internal bool ApplyPlayerDamageWithContext(
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
        int attackerPlayerIdOverride = -1)
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
            attackerPlayerIdOverride).WasFatal;

    internal PlayerDamageResolution ResolvePlayerDamageWithContext(
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
        int attackerPlayerIdOverride = -1)
    {
        var traits = PlayerDamageTraits.CanEvade
            | PlayerDamageTraits.CanApplyOnHitEffects
            | PlayerDamageTraits.CanReflect
            | additionalTraits;
        if (civvieUmbrellaCriticalBoost)
        {
            traits |= PlayerDamageTraits.Critical;
        }

        return ResolvePlayerDamage(
            target,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                damage,
                attacker,
                spyRevealAlpha,
                damageFlags,
                traits,
                allowOsmosisHealOwnedSentries,
                new PlayerDamageUmbrellaOptions(
                    allowCivvieUmbrellaShield,
                    civvieUmbrellaThreatSourceX,
                    civvieUmbrellaThreatSourceY,
                    civvieUmbrellaDrainTicks,
                    civvieUmbrellaCriticalBoost,
                    civvieUmbrellaUseLiveAttackerCriticalBoost),
                SourceEntityId: sourceEntityId,
                AttackId: attackId,
                AttackerWasGrounded: attackerWasGrounded ?? attacker?.IsGrounded,
                TargetWasGrounded: targetWasGrounded ?? target.IsGrounded,
                AttackerPlayerIdOverride: attackerPlayerIdOverride));
    }

    internal PlayerDamageResolution ResolvePlayerDamage(
        PlayerEntity target,
        in PlayerDamageRequest request)
    {
        return request.ApplicationKind switch
        {
            PlayerDamageApplicationKind.Instant => ResolveInstantPlayerDamage(target, request),
            PlayerDamageApplicationKind.Continuous => ResolveContinuousPlayerDamage(target, request),
            _ => new PlayerDamageResolution(
                PlayerDamageDisposition.Rejected,
                request.Amount,
                request.Amount,
                request.Amount,
                request.Amount,
                request.Amount,
                target.Health,
                target.Health,
                0,
                WasFatal: false,
                request.EventFlags,
                request.Traits),
        };
    }

    private PlayerDamageResolution ResolveInstantPlayerDamage(
        PlayerEntity target,
        in PlayerDamageRequest request)
    {
        var requestedDamage = request.Amount;
        var damageAfterOutgoingModifiers = requestedDamage;
        var damageAfterIncomingModifiers = requestedDamage;
        var damageAfterServerScaling = requestedDamage;
        var damageAfterShield = requestedDamage;
        var healthBefore = target.Health;
        var damageFlags = ResolvePlayerDamageEventFlags(request);
        var damageTraits = request.Traits;

        PlayerDamageResolution Finish(
            PlayerDamageDisposition disposition,
            int appliedHealthDamage = 0,
            bool wasFatal = false)
            => new(
                disposition,
                requestedDamage,
                damageAfterOutgoingModifiers,
                damageAfterIncomingModifiers,
                damageAfterServerScaling,
                damageAfterShield,
                healthBefore,
                target.Health,
                appliedHealthDamage,
                wasFatal,
                damageFlags,
                damageTraits);

        var damage = (int)request.Amount;
        if (damage <= 0 || !target.IsAlive)
        {
            return Finish(PlayerDamageDisposition.Rejected);
        }

        if (target.IsLastToDieSpyAfterlifeIncomingDamageImmune
            || target.IsLastToDieMedicHailMaryInvulnerable
            || target.IsLastToDieSecondChanceInvulnerable)
        {
            return Finish(PlayerDamageDisposition.Invulnerable);
        }

        if (request.Traits.HasFlag(PlayerDamageTraits.DirectProjectile)
            && target.IsLastToDieSpyInfiltrateProjectileImmune)
        {
            return Finish(PlayerDamageDisposition.Invulnerable);
        }

        if (request.Umbrella.AllowBlock
            && TryAbsorbCivvieUmbrellaDamage(
                target,
                request.Attacker,
                damageFlags,
                request.Umbrella.ThreatSourceX,
                request.Umbrella.ThreatSourceY,
                request.Umbrella.DrainTicks,
                request.Umbrella.CriticalBoost,
                request.Umbrella.UseLiveAttackerCriticalBoost))
        {
            return Finish(PlayerDamageDisposition.UmbrellaBlocked);
        }

        damage = _host.ApplyExperimentalOutgoingDamageMultiplier(request.Attacker, target, damage);
        damage = _host.ApplyLastToDieOutgoingDamageMultiplier(
            request.Attacker,
            target,
            damage,
            request.Traits,
            request.AttackerWasGrounded,
            request.TargetWasGrounded);
        damageAfterOutgoingModifiers = damage;
        if (request.Traits.HasFlag(PlayerDamageTraits.CanEvade)
            && TryRegisterExperimentalGhostDashEvade(target, request.Attacker, damageFlags))
        {
            return Finish(PlayerDamageDisposition.GhostEvaded);
        }

        if (request.Traits.HasFlag(PlayerDamageTraits.CanEvade)
            && TryEvadePlayerDamage(target, request.Attacker, damage, damageFlags))
        {
            return Finish(PlayerDamageDisposition.Evaded);
        }

        damage = _host.ApplyExperimentalIncomingDamageMultiplier(target, request.Attacker, damage);
        damage = _host.ApplyLastToDieIncomingDamageMultiplier(target, damage, damageTraits);
        damageAfterIncomingModifiers = damage;
        damage = _host.ScaleConfiguredDamage(damage);
        damageAfterServerScaling = damage;
        damage = target.AbsorbExperimentalShieldDamage(damage);
        damageAfterShield = damage;
        if (damage <= 0)
        {
            return Finish(PlayerDamageDisposition.FullyShielded);
        }

        if (request.Traits.HasFlag(PlayerDamageTraits.ExecuteAfterDefenses))
        {
            damage = target.Health;
        }

        if (_host.TryConvertExperimentalSelfDamageToHealing(target, request.Attacker, damage))
        {
            return Finish(PlayerDamageDisposition.ConvertedToHealing);
        }

        if (_host.TryPreventExperimentalFatalDamage(target, damage))
        {
            var fatalPreventedDamage = Math.Max(0, healthBefore - target.Health);
            var fatalPreventedLinkedMedic = _host.ResolveLastToDieMedicLinkedOnHit(
                request.Attacker,
                target,
                fatalPreventedDamage,
                request.Traits);
            var fatalPreventedAssistPlayerIdOverride = request.AssistPlayerIdOverride > 0
                ? request.AssistPlayerIdOverride
                : _host.ResolveLastToDieMedicLinkedAssistPlayerId(request.Attacker, fatalPreventedLinkedMedic);
            RegisterDamageEvent(
                request.Attacker,
                DamageTargetKind.Player,
                target.Id,
                target.X,
                target.Y,
                fatalPreventedDamage,
                wasFatal: false,
                target,
                damageFlags,
                fatalPreventedAssistPlayerIdOverride,
                request.AttackerPlayerIdOverride);
            _host.ApplyLastToDieDamageRewards(request.Attacker, target, fatalPreventedDamage, request.Traits);
            _host.ApplyLastToDieMedicLinkedOnHitEffects(
                request.Attacker,
                target,
                fatalPreventedLinkedMedic);
            _host.ApplyLastToDieDamageTakenEffects(target, request.Attacker, fatalPreventedDamage, request.Traits);
            return Finish(PlayerDamageDisposition.FatalPrevented, fatalPreventedDamage);
        }

        var martyrFatalPrevented = target.LastToDieMedicMartyrProtectedLinkActive
            && damage >= target.Health;
        if (martyrFatalPrevented)
        {
            damage = Math.Max(0, target.Health - 1);
            if (damage == 0)
            {
                return Finish(PlayerDamageDisposition.FatalPrevented);
            }
        }

        var wouldBeFatal = damage >= target.Health;
        if (ShouldCancelDamage(
                DamageTargetKind.Player,
                target.Id,
                target.Id,
                target.Team,
                request.Attacker,
                damage,
                wouldBeFatal,
                target.X,
                target.Y))
        {
            return Finish(PlayerDamageDisposition.DamageCancelled);
        }

        if (_host.TryAbsorbPracticeCombatDummyDamage(target, damage, request.Attacker, damageFlags))
        {
            return Finish(PlayerDamageDisposition.PracticeDummyRecorded);
        }

        if (wouldBeFatal && ShouldCancelDeath(
                target,
                request.GibOnFatal,
                request.Attacker,
                request.FatalWeaponSpriteName))
        {
            return Finish(PlayerDamageDisposition.DeathCancelled);
        }

        var died = target.ApplyDamage(damage, request.SpyRevealAlpha);
        var appliedDamage = Math.Max(0, healthBefore - target.Health);
        RegisterPlayerDamageDealer(target, request.Attacker, appliedDamage);
        var linkedMedic = _host.ResolveLastToDieMedicLinkedOnHit(
            request.Attacker,
            target,
            appliedDamage,
            request.Traits);
        var assistPlayerIdOverride = request.AssistPlayerIdOverride > 0
            ? request.AssistPlayerIdOverride
            : _host.ResolveLastToDieMedicLinkedAssistPlayerId(request.Attacker, linkedMedic);
        RegisterDamageEvent(
            request.Attacker,
            DamageTargetKind.Player,
            target.Id,
            target.X,
            target.Y,
            appliedDamage,
            died,
            target,
            damageFlags,
            assistPlayerIdOverride,
            request.AttackerPlayerIdOverride);
        _host.ApplyExperimentalDamageRewards(
            request.Attacker,
            target,
            appliedDamage,
            request.AllowOsmosisHealOwnedSentries);
        _host.ApplyLastToDieDamageRewards(request.Attacker, target, appliedDamage, request.Traits);
        _host.ApplyLastToDieMedicLinkedOnHitEffects(
            request.Attacker,
            target,
            linkedMedic);
        _host.ApplyLastToDieDamageTakenEffects(target, request.Attacker, appliedDamage, request.Traits);
        if (!request.Traits.HasFlag(PlayerDamageTraits.Reflected))
        {
            _host.ApplyExperimentalDamageTakenRewards(target, request.Attacker, appliedDamage);
        }
        if (request.Attacker is not null)
        {
            _host.ApplyExperimentalEngineerFriendlyFireRetaliation(request.Attacker, target, appliedDamage);
        }
        _host.TryRegisterCombatComboHit(request.Attacker, target, appliedDamage);
        return Finish(
            martyrFatalPrevented
                ? PlayerDamageDisposition.FatalPrevented
                : appliedDamage > 0
                    ? PlayerDamageDisposition.Applied
                    : PlayerDamageDisposition.Invulnerable,
            appliedDamage,
            died);
    }

    internal bool ApplyPlayerContinuousDamage(
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
        bool civvieUmbrellaCriticalBoost = false)
    {
        return ApplyPlayerContinuousDamageWithContext(
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
            attackerWasGrounded: attacker?.IsGrounded,
            targetWasGrounded: target.IsGrounded);
    }

    internal bool ApplyPlayerContinuousDamageWithContext(
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
        bool? targetWasGrounded = null)
    {
        var traits = PlayerDamageTraits.CanEvade
            | PlayerDamageTraits.CanApplyOnHitEffects
            | PlayerDamageTraits.CanReflect
            | additionalTraits;
        if (civvieUmbrellaCriticalBoost)
        {
            traits |= PlayerDamageTraits.Critical;
        }

        return ResolvePlayerDamage(
            target,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Continuous,
                damage,
                attacker,
                spyRevealAlpha,
                damageFlags,
                traits,
                allowOsmosisHealOwnedSentries,
                new PlayerDamageUmbrellaOptions(
                    allowCivvieUmbrellaShield,
                    civvieUmbrellaThreatSourceX,
                    civvieUmbrellaThreatSourceY,
                    civvieUmbrellaDrainTicks,
                    civvieUmbrellaCriticalBoost,
                    civvieUmbrellaUseLiveAttackerCriticalBoost),
                AttackerWasGrounded: attackerWasGrounded ?? attacker?.IsGrounded,
                TargetWasGrounded: targetWasGrounded ?? target.IsGrounded)).WasFatal;
    }

    private PlayerDamageResolution ResolveContinuousPlayerDamage(
        PlayerEntity target,
        in PlayerDamageRequest request)
    {
        var requestedDamage = request.Amount;
        var damageAfterOutgoingModifiers = requestedDamage;
        var damageAfterIncomingModifiers = requestedDamage;
        var damageAfterServerScaling = requestedDamage;
        var damageAfterShield = requestedDamage;
        var healthBefore = target.Health;
        var damageFlags = ResolvePlayerDamageEventFlags(request);
        var damageTraits = request.Traits;

        PlayerDamageResolution Finish(
            PlayerDamageDisposition disposition,
            int appliedHealthDamage = 0,
            bool wasFatal = false)
            => new(
                disposition,
                requestedDamage,
                damageAfterOutgoingModifiers,
                damageAfterIncomingModifiers,
                damageAfterServerScaling,
                damageAfterShield,
                healthBefore,
                target.Health,
                appliedHealthDamage,
                wasFatal,
                damageFlags,
                damageTraits);

        var damage = request.Amount;
        if (damage <= 0f || !target.IsAlive)
        {
            return Finish(PlayerDamageDisposition.Rejected);
        }

        if (target.IsLastToDieSpyAfterlifeIncomingDamageImmune
            || target.IsLastToDieMedicHailMaryInvulnerable
            || target.IsLastToDieSecondChanceInvulnerable)
        {
            return Finish(PlayerDamageDisposition.Invulnerable);
        }

        if (request.Traits.HasFlag(PlayerDamageTraits.DirectProjectile)
            && target.IsLastToDieSpyInfiltrateProjectileImmune)
        {
            return Finish(PlayerDamageDisposition.Invulnerable);
        }

        if (request.Umbrella.AllowBlock
            && TryAbsorbCivvieUmbrellaDamage(
                target,
                request.Attacker,
                damageFlags,
                request.Umbrella.ThreatSourceX,
                request.Umbrella.ThreatSourceY,
                request.Umbrella.DrainTicks,
                request.Umbrella.CriticalBoost,
                request.Umbrella.UseLiveAttackerCriticalBoost))
        {
            return Finish(PlayerDamageDisposition.UmbrellaBlocked);
        }

        damage = _host.ApplyExperimentalOutgoingDamageMultiplier(request.Attacker, target, damage);
        damage = _host.ApplyLastToDieOutgoingDamageMultiplier(
            request.Attacker,
            target,
            damage,
            request.Traits,
            request.AttackerWasGrounded,
            request.TargetWasGrounded);
        damageAfterOutgoingModifiers = damage;
        if (request.Traits.HasFlag(PlayerDamageTraits.CanEvade)
            && TryRegisterExperimentalGhostDashEvade(target, request.Attacker, damageFlags))
        {
            return Finish(PlayerDamageDisposition.GhostEvaded);
        }

        if (request.Traits.HasFlag(PlayerDamageTraits.CanEvade)
            && TryEvadePlayerDamage(target, request.Attacker, damage, damageFlags))
        {
            return Finish(PlayerDamageDisposition.Evaded);
        }

        damage = _host.ApplyExperimentalIncomingDamageMultiplier(target, request.Attacker, damage);
        damage = _host.ApplyLastToDieIncomingDamageMultiplier(target, damage, damageTraits);
        damageAfterIncomingModifiers = damage;
        damage = _host.ScaleConfiguredDamage(damage);
        damageAfterServerScaling = damage;
        damage = target.AbsorbExperimentalShieldDamage(damage);
        damageAfterShield = damage;
        if (damage <= 0f)
        {
            return Finish(PlayerDamageDisposition.FullyShielded);
        }

        if (request.Traits.HasFlag(PlayerDamageTraits.ExecuteAfterDefenses))
        {
            damage = target.Health;
        }

        if (_host.TryConvertExperimentalSelfDamageToHealing(target, request.Attacker, damage))
        {
            return Finish(PlayerDamageDisposition.ConvertedToHealing);
        }

        if (_host.TryPreventExperimentalFatalDamage(target, (int)MathF.Ceiling(damage)))
        {
            var fatalPreventedDamage = Math.Max(0, healthBefore - target.Health);
            var fatalPreventedLinkedMedic = _host.ResolveLastToDieMedicLinkedOnHit(
                request.Attacker,
                target,
                fatalPreventedDamage,
                request.Traits);
            var fatalPreventedAssistPlayerIdOverride = request.AssistPlayerIdOverride > 0
                ? request.AssistPlayerIdOverride
                : _host.ResolveLastToDieMedicLinkedAssistPlayerId(request.Attacker, fatalPreventedLinkedMedic);
            RegisterDamageEvent(
                request.Attacker,
                DamageTargetKind.Player,
                target.Id,
                target.X,
                target.Y,
                fatalPreventedDamage,
                wasFatal: false,
                target,
                damageFlags,
                fatalPreventedAssistPlayerIdOverride,
                request.AttackerPlayerIdOverride);
            _host.ApplyLastToDieDamageRewards(request.Attacker, target, fatalPreventedDamage, request.Traits);
            _host.ApplyLastToDieMedicLinkedOnHitEffects(
                request.Attacker,
                target,
                fatalPreventedLinkedMedic);
            _host.ApplyLastToDieDamageTakenEffects(target, request.Attacker, fatalPreventedDamage, request.Traits);
            return Finish(PlayerDamageDisposition.FatalPrevented, fatalPreventedDamage);
        }

        var projectedWholeDamage = (int)(target.ContinuousDamageAccumulator + damage);
        var martyrFatalPrevented = target.LastToDieMedicMartyrProtectedLinkActive
            && projectedWholeDamage >= target.Health;
        if (martyrFatalPrevented && target.Health <= 1)
        {
            return Finish(PlayerDamageDisposition.FatalPrevented);
        }

        var roundedDamage = martyrFatalPrevented
            ? target.Health - 1
            : Math.Max(1, (int)MathF.Ceiling(damage));
        var wouldBeFatal = !martyrFatalPrevented && damage >= target.Health;
        if (ShouldCancelDamage(
                DamageTargetKind.Player,
                target.Id,
                target.Id,
                target.Team,
                request.Attacker,
                roundedDamage,
                wouldBeFatal,
                target.X,
                target.Y))
        {
            return Finish(PlayerDamageDisposition.DamageCancelled);
        }

        if (_host.TryAbsorbPracticeCombatDummyContinuousDamage(target, damage, request.Attacker, damageFlags))
        {
            return Finish(PlayerDamageDisposition.PracticeDummyRecorded);
        }

        if (wouldBeFatal && ShouldCancelDeath(target, gibbed: false, request.Attacker, weaponSpriteName: null))
        {
            return Finish(PlayerDamageDisposition.DeathCancelled);
        }

        var died = martyrFatalPrevented
            ? target.ApplyContinuousDamageCapped(
                damage,
                maximumHealthDamage: target.Health - 1,
                request.SpyRevealAlpha)
            : target.ApplyContinuousDamage(damage, request.SpyRevealAlpha);
        var appliedDamage = Math.Max(0, healthBefore - target.Health);
        RegisterPlayerDamageDealer(target, request.Attacker, appliedDamage);
        var linkedMedic = _host.ResolveLastToDieMedicLinkedOnHit(
            request.Attacker,
            target,
            appliedDamage,
            request.Traits);
        var assistPlayerIdOverride = request.AssistPlayerIdOverride > 0
            ? request.AssistPlayerIdOverride
            : _host.ResolveLastToDieMedicLinkedAssistPlayerId(request.Attacker, linkedMedic);
        RegisterDamageEvent(
            request.Attacker,
            DamageTargetKind.Player,
            target.Id,
            target.X,
            target.Y,
            appliedDamage,
            died,
            target,
            damageFlags,
            assistPlayerIdOverride,
            request.AttackerPlayerIdOverride);
        _host.ApplyExperimentalDamageRewards(
            request.Attacker,
            target,
            appliedDamage,
            request.AllowOsmosisHealOwnedSentries);
        _host.ApplyLastToDieDamageRewards(request.Attacker, target, appliedDamage, request.Traits);
        _host.ApplyLastToDieMedicLinkedOnHitEffects(
            request.Attacker,
            target,
            linkedMedic);
        _host.ApplyLastToDieDamageTakenEffects(target, request.Attacker, appliedDamage, request.Traits);
        if (!request.Traits.HasFlag(PlayerDamageTraits.Reflected))
        {
            _host.ApplyExperimentalDamageTakenRewards(target, request.Attacker, appliedDamage);
        }
        if (request.Attacker is not null)
        {
            _host.ApplyExperimentalEngineerFriendlyFireRetaliation(request.Attacker, target, appliedDamage);
        }
        _host.TryRegisterCombatComboHit(request.Attacker, target, appliedDamage);
        var disposition = martyrFatalPrevented
            ? PlayerDamageDisposition.FatalPrevented
            : appliedDamage > 0
                ? PlayerDamageDisposition.Applied
                : target.IsUbered
                    || target.IsLastToDieMedicHailMaryInvulnerable
                    || target.IsLastToDieSecondChanceInvulnerable
                    || target.IsExperimentalGhostDashing
                    ? PlayerDamageDisposition.Invulnerable
                    : PlayerDamageDisposition.Accumulated;
        return Finish(disposition, appliedDamage, died);
    }

    private static DamageEventFlags ResolvePlayerDamageEventFlags(in PlayerDamageRequest request)
    {
        var flags = request.EventFlags;
        if (request.Traits.HasFlag(PlayerDamageTraits.Periodic))
        {
            flags |= DamageEventFlags.StatusTick;
        }
        if (request.Traits.HasFlag(PlayerDamageTraits.Critical))
        {
            flags |= DamageEventFlags.Critical;
        }

        return flags;
    }

    internal bool TryAbsorbCivvieUmbrellaDamage(
        PlayerEntity target,
        PlayerEntity? attacker,
        DamageEventFlags damageFlags,
        float? threatSourceX = null,
        float? threatSourceY = null,
        int? drainTicks = null,
        bool criticalBoost = false,
        bool useLiveAttackerCriticalBoost = true)
    {
        if (attacker is null
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team
            || !target.IsCivvieUmbrellaActive
            || target.IsCivvieUmbrellaBroken
            || target.CivvieUmbrellaChargeTicks <= 0)
        {
            return false;
        }

        var resolvedThreatSourceX = threatSourceX ?? attacker.X;
        var resolvedThreatSourceY = threatSourceY ?? attacker.Y;
        var resolvedDrainTicks = drainTicks ?? PlayerEntity.CivvieUmbrellaImpactDrain;
        var isCriticalBoosted = criticalBoost
            || (useLiveAttackerCriticalBoost && attacker.IsKritzCritBoosted);
        resolvedDrainTicks = PlayerEntity.ScaleCivvieUmbrellaDrainForCriticalBoost(resolvedDrainTicks, isCriticalBoosted);
        if (!IsCivvieUmbrellaFrontThreat(target, resolvedThreatSourceX, resolvedThreatSourceY)
            || !target.TryAbsorbCivvieUmbrellaHit(resolvedDrainTicks))
        {
            return false;
        }

        var effectPosition = GetCivvieUmbrellaBlockEffectPosition(target);
        RegisterDamageEvent(
            attacker,
            DamageTargetKind.Player,
            target.Id,
            effectPosition.X,
            effectPosition.Y,
            amount: 0,
            wasFatal: false,
            target,
            damageFlags | DamageEventFlags.Evaded | DamageEventFlags.CivvieUmbrellaBlock);
        return true;
    }

    internal bool TryAbsorbCivvieUmbrellaProjectileContact(
        PlayerEntity target,
        int ownerId,
        float hitX,
        float hitY,
        DamageEventFlags damageFlags = DamageEventFlags.None,
        bool criticalBoost = false)
    {
        var attacker = _host.FindPlayerById(ownerId);
        if (attacker is null
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team
            || !target.IsCivvieUmbrellaActive
            || target.IsCivvieUmbrellaBroken
            || target.CivvieUmbrellaChargeTicks <= 0
            || !IsCivvieUmbrellaFrontThreat(target, hitX, hitY))
        {
            return false;
        }

        var resolvedDrainTicks = PlayerEntity.ScaleCivvieUmbrellaDrainForCriticalBoost(
            PlayerEntity.CivvieUmbrellaImpactDrain,
            criticalBoost);
        if (!target.TryAbsorbCivvieUmbrellaHit(resolvedDrainTicks))
        {
            return false;
        }

        _host.RegisterImpactEffect(hitX, hitY, 0f);
        RegisterDamageEvent(
            attacker,
            DamageTargetKind.Player,
            target.Id,
            hitX,
            hitY,
            amount: 0,
            wasFatal: false,
            target,
            damageFlags | DamageEventFlags.Evaded | DamageEventFlags.CivvieUmbrellaBlock);
        return true;
    }

    private (float X, float Y) GetCivvieUmbrellaBlockEffectPosition(PlayerEntity target)
    {
        var aimRadians = DegreesToRadians(target.AimDirectionDegrees);
        var aimWorldX = target.X + DeterministicMath.Cos(aimRadians) * 128f;
        var aimWorldY = target.Y + DeterministicMath.Sin(aimRadians) * 128f;
        return _host.GetCivvieUmbrellaTip(target, aimWorldX, aimWorldY);
    }

    private static bool IsCivvieUmbrellaFrontThreat(PlayerEntity target, float threatSourceX, float threatSourceY)
    {
        var deltaX = threatSourceX - target.X;
        var deltaY = threatSourceY - target.Y;
        if ((deltaX * deltaX) + (deltaY * deltaY) < 0.0001f)
        {
            return true;
        }

        var aimRadians = DegreesToRadians(target.AimDirectionDegrees);
        var forwardX = DeterministicMath.Cos(aimRadians);
        var forwardY = DeterministicMath.Sin(aimRadians);
        var length = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        var threatDirX = deltaX / length;
        var threatDirY = deltaY / length;
        return ((threatDirX * forwardX) + (threatDirY * forwardY)) > 0f;
    }

    private bool TryRegisterExperimentalGhostDashEvade(
        PlayerEntity target,
        PlayerEntity? attacker,
        DamageEventFlags damageFlags)
    {
        if (!target.IsExperimentalGhostDashing
            || attacker is null
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team)
        {
            return false;
        }

        RegisterDamageEvent(
            attacker,
            DamageTargetKind.Player,
            target.Id,
            target.X,
            target.Y,
            amount: 0,
            wasFatal: false,
            target,
            damageFlags | DamageEventFlags.Evaded | DamageEventFlags.GhostDash);
        return true;
    }

    private bool TryEvadePlayerDamage(
        PlayerEntity target,
        PlayerEntity? attacker,
        float damage,
        DamageEventFlags damageFlags)
    {
        var experimentalEvasionChance = _host.GetExperimentalTotalEvasionChance(target);
        var lastToDieEvasionChance = _host.GetLastToDieEvasionChance(target);
        var totalEvasionChance = Math.Clamp(
            1f - ((1f - experimentalEvasionChance) * (1f - lastToDieEvasionChance)),
            0f,
            0.95f);
        if (damage <= 0f
            || attacker is null
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team
            || totalEvasionChance <= 0f)
        {
            return false;
        }

        var evaded = lastToDieEvasionChance > 0f
            ? !_host.RollLastToDieEvasion(target, totalEvasionChance)
            : _host.NextDouble() < totalEvasionChance;
        if (!evaded)
        {
            return false;
        }

        RegisterDamageEvent(
            attacker,
            DamageTargetKind.Player,
            target.Id,
            target.X,
            target.Y,
            amount: 0,
            wasFatal: false,
            target,
            damageFlags | DamageEventFlags.Evaded);
        return true;
    }

    internal bool ApplySentryDamage(SentryEntity target, int damage, PlayerEntity? attacker)
    {
        if (damage <= 0)
        {
            return false;
        }

        damage = _host.ApplyExperimentalIncomingSentryDamageMultiplier(target, damage);
        damage = _host.ScaleConfiguredDamage(damage);
        if (damage <= 0)
        {
            return false;
        }

        var wouldBeFatal = damage >= target.Health;
        if (ShouldCancelDamage(
                DamageTargetKind.Sentry,
                target.Id,
                -1,
                target.Team,
                attacker,
                damage,
                wouldBeFatal,
                target.X,
                target.Y))
        {
            return false;
        }

        var healthBefore = target.Health;
        var destroyed = target.ApplyDamage(damage);
        RegisterDamageEvent(
            attacker,
            DamageTargetKind.Sentry,
            target.Id,
            target.X,
            target.Y,
            Math.Max(0, healthBefore - target.Health),
            destroyed);
        return destroyed;
    }

    internal bool ApplyGeneratorDamage(GeneratorState target, float damage, PlayerEntity? attacker)
    {
        if (damage <= 0f || target.IsDestroyed)
        {
            return false;
        }

        damage = _host.ScaleConfiguredDamage(damage);
        if (damage <= 0f)
        {
            return false;
        }

        var roundedDamage = Math.Max(1, (int)MathF.Ceiling(damage));
        var wouldBeFatal = damage >= target.Health;
        if (ShouldCancelDamage(
                DamageTargetKind.Generator,
                (int)target.Team,
                -1,
                target.Team,
                attacker,
                roundedDamage,
                wouldBeFatal,
                target.Marker.CenterX,
                target.Marker.CenterY))
        {
            return false;
        }

        var healthBefore = target.Health;
        var destroyed = target.ApplyDamage(damage);
        RegisterDamageEvent(
            attacker,
            DamageTargetKind.Generator,
            (int)target.Team,
            target.Marker.CenterX,
            target.Marker.CenterY,
            Math.Max(0, healthBefore - target.Health),
            destroyed);
        return destroyed;
    }

    internal PlayerEntity? ResolveAssistPlayer(PlayerEntity victim, PlayerEntity killer)
    {
        if (ReferenceEquals(victim, killer) || killer.Team == victim.Team)
        {
            return null;
        }

        var assistantId = victim.LastDamageDealerPlayerId != killer.Id
            ? victim.LastDamageDealerPlayerId
            : victim.SecondToLastDamageDealerPlayerId;
        var remainingTicks = victim.LastDamageDealerPlayerId != killer.Id
            ? victim.LastDamageDealerAssistTicksRemaining
            : victim.SecondToLastDamageDealerAssistTicksRemaining;
        var assistant = assistantId.HasValue ? _host.FindPlayerById(assistantId.Value) : null;
        if (remainingTicks <= 0 || assistant is null
            || assistant.Id == killer.Id || assistant.Id == victim.Id
            || assistant.Team != killer.Team)
        {
            return null;
        }

        return assistant;
    }

    internal int ResolveAssistPlayerId(PlayerEntity victim, PlayerEntity killer)
        => ResolveAssistPlayer(victim, killer)?.Id ?? -1;

    private void RegisterPlayerDamageDealer(PlayerEntity target, PlayerEntity? attacker, int appliedDamage)
    {
        if (appliedDamage <= 0
            || attacker is null
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team)
        {
            return;
        }

        target.RegisterDamageDealer(
            attacker.Id,
            _host.GetSimulationTicksFromSourceTicks(AssistTrackingSourceTicks));
    }

    private int ResolveDamageEventAssistPlayerId(
        PlayerEntity? attacker,
        PlayerEntity? playerTarget,
        DamageTargetKind targetKind,
        bool wasFatal,
        int assistPlayerIdOverride = -1)
    {
        if (attacker is null)
        {
            return -1;
        }

        if (targetKind == DamageTargetKind.Player
            && playerTarget is not null
            && (ReferenceEquals(attacker, playerTarget) || attacker.Team == playerTarget.Team))
        {
            return -1;
        }

        if (targetKind == DamageTargetKind.Player && wasFatal && playerTarget is not null)
        {
            return ResolveAssistPlayerId(playerTarget, attacker);
        }

        if (assistPlayerIdOverride > 0)
        {
            return playerTarget is not null
                && assistPlayerIdOverride != attacker.Id
                && assistPlayerIdOverride != playerTarget.Id
                    ? assistPlayerIdOverride
                    : -1;
        }

        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (player.ClassId == PlayerClass.Medic
                && player.IsAlive
                && player.MedicHealTargetId == attacker.Id)
            {
                return player.Id;
            }
        }

        return -1;
    }

    private static float DegreesToRadians(float degrees) => degrees * (MathF.PI / 180f);

    private bool ShouldCancelDamage(
        DamageTargetKind targetKind,
        int targetEntityId,
        int targetPlayerId,
        PlayerTeam? targetTeam,
        PlayerEntity? attacker,
        int amount,
        bool wouldBeFatal,
        float x,
        float y)
    {
        return _host.ShouldCancelDamage(
            targetKind,
            targetEntityId,
            targetPlayerId,
            targetTeam,
            attacker,
            amount,
            wouldBeFatal,
            x,
            y);
    }

    private bool ShouldCancelDeath(
        PlayerEntity player,
        bool gibbed,
        PlayerEntity? killer,
        string? weaponSpriteName)
    {
        return _host.ShouldCancelDeath(player, gibbed, killer, weaponSpriteName);
    }
}

/// <summary>Mode, perk, and server-configuration adjustments to a damage amount.</summary>
internal interface ICombatDamageModifiers
{
    int ScaleConfiguredDamage(int damage);
    float ScaleConfiguredDamage(float damage);
    int ApplyExperimentalOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, int damage);
    float ApplyExperimentalOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, float damage);
    int ApplyExperimentalIncomingDamageMultiplier(PlayerEntity target, PlayerEntity? attacker, int damage);
    float ApplyExperimentalIncomingDamageMultiplier(PlayerEntity target, PlayerEntity? attacker, float damage);
    int ApplyLastToDieOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, int damage, PlayerDamageTraits traits, bool? attackerWasGrounded, bool? targetWasGrounded);
    float ApplyLastToDieOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, float damage, PlayerDamageTraits traits, bool? attackerWasGrounded, bool? targetWasGrounded);
    int ApplyLastToDieIncomingDamageMultiplier(PlayerEntity target, int damage, PlayerDamageTraits traits);
    float ApplyLastToDieIncomingDamageMultiplier(PlayerEntity target, float damage, PlayerDamageTraits traits);
    int ApplyExperimentalIncomingSentryDamageMultiplier(SentryEntity target, int damage);
    float GetExperimentalTotalEvasionChance(PlayerEntity target);
    float GetLastToDieEvasionChance(PlayerEntity target);
    bool RollLastToDieEvasion(PlayerEntity target, float totalEvasionChance);
}

/// <summary>Checks that may cancel, absorb, or convert damage before it is applied.</summary>
internal interface ICombatDamageInterceptors
{
    bool ShouldCancelDamage(DamageTargetKind targetKind, int targetEntityId, int targetPlayerId, PlayerTeam? targetTeam, PlayerEntity? attacker, int amount, bool wouldBeFatal, float x, float y);
    bool ShouldCancelDeath(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName);
    bool TryPreventExperimentalFatalDamage(PlayerEntity target, int damage);
    bool TryConvertExperimentalSelfDamageToHealing(PlayerEntity target, PlayerEntity? attacker, float damage);
    bool TryAbsorbPracticeCombatDummyDamage(PlayerEntity target, int damage, PlayerEntity? attacker, DamageEventFlags flags);
    bool TryAbsorbPracticeCombatDummyContinuousDamage(PlayerEntity target, float damage, PlayerEntity? attacker, DamageEventFlags flags);
}

/// <summary>Mode and perk consequences that follow applied damage.</summary>
internal interface ICombatDamageConsequences
{
    void ApplyExperimentalDamageRewards(PlayerEntity? attacker, PlayerEntity target, int damage, bool allowOsmosisHealOwnedSentries);
    void ApplyExperimentalDamageTakenRewards(PlayerEntity target, PlayerEntity? attacker, int damage);
    void ApplyLastToDieDamageRewards(PlayerEntity? attacker, PlayerEntity target, int damage, PlayerDamageTraits traits);
    void ApplyLastToDieDamageTakenEffects(PlayerEntity target, PlayerEntity? attacker, int damage, PlayerDamageTraits traits);
    PlayerEntity? ResolveLastToDieMedicLinkedOnHit(PlayerEntity? attacker, PlayerEntity target, int damage, PlayerDamageTraits traits);
    int ResolveLastToDieMedicLinkedAssistPlayerId(PlayerEntity? attacker, PlayerEntity? linkedMedic);
    void ApplyLastToDieMedicLinkedOnHitEffects(PlayerEntity? attacker, PlayerEntity target, PlayerEntity? linkedMedic);
    void ApplyExperimentalEngineerFriendlyFireRetaliation(PlayerEntity attacker, PlayerEntity target, int damage);
    void TryRegisterCombatComboHit(PlayerEntity? attacker, PlayerEntity target, int damage);
    void TryRegisterBuffBannerDamage(PlayerEntity attacker, PlayerEntity target, int damage);
}

/// <summary>Everything <see cref="CombatSystem"/> needs from the world.</summary>
internal interface ICombatSystemHost :
    ISimulationWorldState,
    ISimulationPlayerDirectory,
    ISimulationRandomSource,
    ISimulationPresentationEvents,
    ICombatDamageModifiers,
    ICombatDamageInterceptors,
    ICombatDamageConsequences
{
    (float X, float Y) GetCivvieUmbrellaTip(PlayerEntity target, float aimWorldX, float aimWorldY);
}
