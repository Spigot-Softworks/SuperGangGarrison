namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float AssistTrackingSourceTicks = 120f;

    private void RegisterDamageEvent(
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
            SourceFrame: (ulong)Frame));
    }

    private int FindHealingMedicPlayerId(int targetPlayerId)
    {
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (player.ClassId == PlayerClass.Medic
                && player.IsAlive
                && player.MedicHealTargetId == targetPlayerId)
            {
                return player.Id;
            }
        }

        return -1;
    }

    private void MarkPendingFatalPlayerDamageEventGibbed(int playerId)
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

    private void MarkPendingFatalPlayerDamageEventPrevented(int playerId)
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

    private bool ApplyPlayerDamage(
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

    private bool ApplyPlayerDamageWithContext(
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

    private PlayerDamageResolution ResolvePlayerDamageWithContext(
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
            || target.IsLastToDieMedicHailMaryInvulnerable)
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

        damage = ApplyExperimentalOutgoingDamageMultiplier(request.Attacker, target, damage);
        damage = ApplyLastToDieOutgoingDamageMultiplier(
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

        damage = ApplyExperimentalIncomingDamageMultiplier(target, request.Attacker, damage);
        damage = ApplyLastToDieIncomingDamageMultiplier(target, damage, damageTraits);
        damageAfterIncomingModifiers = damage;
        damage = ScaleConfiguredDamage(damage);
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

        if (TryConvertExperimentalSelfDamageToHealing(target, request.Attacker, damage))
        {
            return Finish(PlayerDamageDisposition.ConvertedToHealing);
        }

        if (TryPreventExperimentalFatalDamage(target, damage))
        {
            var fatalPreventedDamage = Math.Max(0, healthBefore - target.Health);
            var fatalPreventedLinkedMedic = ResolveLastToDieMedicLinkedOnHit(
                request.Attacker,
                target,
                fatalPreventedDamage,
                request.Traits);
            var fatalPreventedAssistPlayerIdOverride = request.AssistPlayerIdOverride > 0
                ? request.AssistPlayerIdOverride
                : ResolveLastToDieMedicLinkedAssistPlayerId(request.Attacker, fatalPreventedLinkedMedic);
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
            ApplyLastToDieDamageRewards(request.Attacker, target, fatalPreventedDamage, request.Traits);
            ApplyLastToDieMedicLinkedOnHitEffects(
                request.Attacker,
                target,
                fatalPreventedLinkedMedic);
            ApplyLastToDieDamageTakenEffects(target, request.Attacker, fatalPreventedDamage, request.Traits);
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

        if (TryAbsorbPracticeCombatDummyDamage(target, damage, request.Attacker, damageFlags))
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
        var linkedMedic = ResolveLastToDieMedicLinkedOnHit(
            request.Attacker,
            target,
            appliedDamage,
            request.Traits);
        var assistPlayerIdOverride = request.AssistPlayerIdOverride > 0
            ? request.AssistPlayerIdOverride
            : ResolveLastToDieMedicLinkedAssistPlayerId(request.Attacker, linkedMedic);
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
        ApplyExperimentalDamageRewards(
            request.Attacker,
            target,
            appliedDamage,
            request.AllowOsmosisHealOwnedSentries);
        ApplyLastToDieDamageRewards(request.Attacker, target, appliedDamage, request.Traits);
        ApplyLastToDieMedicLinkedOnHitEffects(
            request.Attacker,
            target,
            linkedMedic);
        ApplyLastToDieDamageTakenEffects(target, request.Attacker, appliedDamage, request.Traits);
        if (!request.Traits.HasFlag(PlayerDamageTraits.Reflected))
        {
            ApplyExperimentalDamageTakenRewards(target, request.Attacker, appliedDamage);
        }
        if (request.Attacker is not null)
        {
            ApplyExperimentalEngineerFriendlyFireRetaliation(request.Attacker, target, appliedDamage);
        }
        TryRegisterCombatComboHit(request.Attacker, target, appliedDamage);
        return Finish(
            martyrFatalPrevented
                ? PlayerDamageDisposition.FatalPrevented
                : appliedDamage > 0
                    ? PlayerDamageDisposition.Applied
                    : PlayerDamageDisposition.Invulnerable,
            appliedDamage,
            died);
    }

    private bool ApplyPlayerContinuousDamage(
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

    private bool ApplyPlayerContinuousDamageWithContext(
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
            || target.IsLastToDieMedicHailMaryInvulnerable)
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

        damage = ApplyExperimentalOutgoingDamageMultiplier(request.Attacker, target, damage);
        damage = ApplyLastToDieOutgoingDamageMultiplier(
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

        damage = ApplyExperimentalIncomingDamageMultiplier(target, request.Attacker, damage);
        damage = ApplyLastToDieIncomingDamageMultiplier(target, damage, damageTraits);
        damageAfterIncomingModifiers = damage;
        damage = ScaleConfiguredDamage(damage);
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

        if (TryConvertExperimentalSelfDamageToHealing(target, request.Attacker, damage))
        {
            return Finish(PlayerDamageDisposition.ConvertedToHealing);
        }

        if (TryPreventExperimentalFatalDamage(target, (int)MathF.Ceiling(damage)))
        {
            var fatalPreventedDamage = Math.Max(0, healthBefore - target.Health);
            var fatalPreventedLinkedMedic = ResolveLastToDieMedicLinkedOnHit(
                request.Attacker,
                target,
                fatalPreventedDamage,
                request.Traits);
            var fatalPreventedAssistPlayerIdOverride = request.AssistPlayerIdOverride > 0
                ? request.AssistPlayerIdOverride
                : ResolveLastToDieMedicLinkedAssistPlayerId(request.Attacker, fatalPreventedLinkedMedic);
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
            ApplyLastToDieDamageRewards(request.Attacker, target, fatalPreventedDamage, request.Traits);
            ApplyLastToDieMedicLinkedOnHitEffects(
                request.Attacker,
                target,
                fatalPreventedLinkedMedic);
            ApplyLastToDieDamageTakenEffects(target, request.Attacker, fatalPreventedDamage, request.Traits);
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

        if (TryAbsorbPracticeCombatDummyContinuousDamage(target, damage, request.Attacker, damageFlags))
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
        var linkedMedic = ResolveLastToDieMedicLinkedOnHit(
            request.Attacker,
            target,
            appliedDamage,
            request.Traits);
        var assistPlayerIdOverride = request.AssistPlayerIdOverride > 0
            ? request.AssistPlayerIdOverride
            : ResolveLastToDieMedicLinkedAssistPlayerId(request.Attacker, linkedMedic);
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
        ApplyExperimentalDamageRewards(
            request.Attacker,
            target,
            appliedDamage,
            request.AllowOsmosisHealOwnedSentries);
        ApplyLastToDieDamageRewards(request.Attacker, target, appliedDamage, request.Traits);
        ApplyLastToDieMedicLinkedOnHitEffects(
            request.Attacker,
            target,
            linkedMedic);
        ApplyLastToDieDamageTakenEffects(target, request.Attacker, appliedDamage, request.Traits);
        if (!request.Traits.HasFlag(PlayerDamageTraits.Reflected))
        {
            ApplyExperimentalDamageTakenRewards(target, request.Attacker, appliedDamage);
        }
        if (request.Attacker is not null)
        {
            ApplyExperimentalEngineerFriendlyFireRetaliation(request.Attacker, target, appliedDamage);
        }
        TryRegisterCombatComboHit(request.Attacker, target, appliedDamage);
        var disposition = martyrFatalPrevented
            ? PlayerDamageDisposition.FatalPrevented
            : appliedDamage > 0
                ? PlayerDamageDisposition.Applied
                : target.IsUbered
                    || target.IsLastToDieMedicHailMaryInvulnerable
                    || target.IsExperimentalGhostDashing
                    ? PlayerDamageDisposition.Invulnerable
                    : PlayerDamageDisposition.Accumulated;
        return Finish(disposition, appliedDamage, died);
    }

    private static DamageEventFlags ResolvePlayerDamageEventFlags(
        in PlayerDamageRequest request)
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

    private bool TryAbsorbCivvieUmbrellaDamage(
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

        var (effectX, effectY) = GetCivvieUmbrellaBlockEffectPosition(target);
        RegisterDamageEvent(
            attacker,
            DamageTargetKind.Player,
            target.Id,
            effectX,
            effectY,
            amount: 0,
            wasFatal: false,
            target,
            damageFlags | DamageEventFlags.Evaded | DamageEventFlags.CivvieUmbrellaBlock);
        return true;
    }

    private bool TryAbsorbCivvieUmbrellaProjectileContact(
        PlayerEntity target,
        int ownerId,
        float hitX,
        float hitY,
        DamageEventFlags damageFlags = DamageEventFlags.None,
        bool criticalBoost = false)
    {
        var attacker = FindPlayerById(ownerId);
        if (attacker is null
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team
            || !target.IsCivvieUmbrellaActive
            || target.IsCivvieUmbrellaBroken
            || target.CivvieUmbrellaChargeTicks <= 0)
        {
            return false;
        }

        if (!IsCivvieUmbrellaFrontThreat(target, hitX, hitY))
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

        RegisterImpactEffect(hitX, hitY, 0f);
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
        var aimWorldX = target.X + MathF.Cos(aimRadians) * 128f;
        var aimWorldY = target.Y + MathF.Sin(aimRadians) * 128f;
        var tip = WeaponHandler.GetCivvieUmbrellaTip(target, aimWorldX, aimWorldY);
        return (tip.X, tip.Y);
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
        var forwardX = MathF.Cos(aimRadians);
        var forwardY = MathF.Sin(aimRadians);
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

    private bool ApplySentryDamage(SentryEntity target, int damage, PlayerEntity? attacker)
    {
        if (damage <= 0)
        {
            return false;
        }

        damage = ApplyExperimentalIncomingSentryDamageMultiplier(target, damage);
        damage = ScaleConfiguredDamage(damage);
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

    private bool ApplyGeneratorDamage(GeneratorState target, float damage, PlayerEntity? attacker)
    {
        if (damage <= 0f || target.IsDestroyed)
        {
            return false;
        }

        damage = ScaleConfiguredDamage(damage);
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

    private void RegisterPlayerDamageDealer(PlayerEntity target, PlayerEntity? attacker, int appliedDamage)
    {
        if (appliedDamage <= 0
            || attacker is null
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team)
        {
            return;
        }

        target.RegisterDamageDealer(attacker.Id, GetSimulationTicksFromSourceTicks(AssistTrackingSourceTicks));
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

        if (assistPlayerIdOverride > 0)
        {
            return playerTarget is not null
                && assistPlayerIdOverride != attacker.Id
                && assistPlayerIdOverride != playerTarget.Id
                    ? assistPlayerIdOverride
                    : -1;
        }

        if (targetKind == DamageTargetKind.Player
            && wasFatal
            && playerTarget is not null)
        {
            return ResolveAssistPlayerId(playerTarget, attacker);
        }

        return FindHealingMedicPlayerId(attacker.Id);
    }

    private int ResolveAssistPlayerId(PlayerEntity victim, PlayerEntity killer)
    {
        var assistingPlayer = ResolveAssistPlayer(victim, killer);
        return assistingPlayer?.Id ?? -1;
    }

    private PlayerEntity? ResolveAssistPlayer(PlayerEntity victim, PlayerEntity killer)
    {
        if (ReferenceEquals(victim, killer) || killer.Team == victim.Team)
        {
            return null;
        }

        var healingMedic = FindHealingMedicPlayer(killer.Id);
        if (healingMedic is not null
            && healingMedic.Id != killer.Id
            && healingMedic.Id != victim.Id
            && healingMedic.Team == killer.Team)
        {
            return healingMedic;
        }

        if (!victim.SecondToLastDamageDealerPlayerId.HasValue)
        {
            return null;
        }

        var assistant = FindPlayerById(victim.SecondToLastDamageDealerPlayerId.Value);
        if (assistant is null
            || assistant.Id == killer.Id
            || assistant.Id == victim.Id
            || !assistant.IsAlive
            || assistant.Team != killer.Team)
        {
            return null;
        }

        return assistant;
    }
}
