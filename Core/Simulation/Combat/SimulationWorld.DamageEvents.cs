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
        DamageEventFlags flags = DamageEventFlags.None)
    {
        if (amount <= 0 && !flags.HasFlag(DamageEventFlags.Evaded))
        {
            return;
        }

        var attackerPlayerId = attacker?.Id ?? -1;
        var assistedByPlayerId = ResolveDamageEventAssistPlayerId(attacker, playerTarget, targetKind, wasFatal);
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
        if (damage <= 0 || !target.IsAlive)
        {
            return false;
        }

        if (allowCivvieUmbrellaShield
            && TryAbsorbCivvieUmbrellaDamage(
                target,
                attacker,
                damageFlags,
                civvieUmbrellaThreatSourceX,
                civvieUmbrellaThreatSourceY,
                civvieUmbrellaDrainTicks,
                civvieUmbrellaCriticalBoost))
        {
            return false;
        }

        damage = ApplyExperimentalOutgoingDamageMultiplier(attacker, target, damage);
        damage = ApplyExperimentalIncomingDamageMultiplier(target, attacker, damage);
        damage = ScaleConfiguredDamage(damage);
        damage = target.AbsorbExperimentalShieldDamage(damage);
        if (damage <= 0)
        {
            return false;
        }

        if (TryConvertExperimentalSelfDamageToHealing(target, attacker, damage))
        {
            return false;
        }

        var healthBefore = target.Health;
        if (TryRegisterExperimentalGhostDashEvade(target, attacker, damageFlags))
        {
            return false;
        }

        if (TryEvadeExperimentalDamage(target, attacker, damage, damageFlags))
        {
            return false;
        }

        if (TryPreventExperimentalFatalDamage(target, damage))
        {
            RegisterDamageEvent(
                attacker,
                DamageTargetKind.Player,
                target.Id,
                target.X,
                target.Y,
                Math.Max(0, healthBefore - target.Health),
                wasFatal: false,
                target,
                damageFlags);
            return false;
        }

        var wouldBeFatal = damage >= target.Health;
        if (ShouldCancelDamage(
                DamageTargetKind.Player,
                target.Id,
                target.Id,
                target.Team,
                attacker,
                damage,
                wouldBeFatal,
                target.X,
                target.Y))
        {
            return false;
        }

        if (TryAbsorbPracticeCombatDummyDamage(target, damage, attacker, damageFlags))
        {
            return false;
        }

        if (wouldBeFatal && ShouldCancelDeath(target, gibbed: false, attacker, weaponSpriteName: null))
        {
            return false;
        }

        var died = target.ApplyDamage(damage, spyRevealAlpha);
        var appliedDamage = Math.Max(0, healthBefore - target.Health);
        RegisterPlayerDamageDealer(target, attacker, appliedDamage);
        RegisterDamageEvent(
            attacker,
            DamageTargetKind.Player,
            target.Id,
            target.X,
            target.Y,
            appliedDamage,
            died,
            target,
            damageFlags);
        ApplyExperimentalDamageRewards(attacker, target, appliedDamage, allowOsmosisHealOwnedSentries);
        ApplyExperimentalDamageTakenRewards(target, attacker, appliedDamage);
        if (attacker is not null)
        {
            ApplyExperimentalEngineerFriendlyFireRetaliation(attacker, target, appliedDamage);
        }
        TryRegisterCombatComboHit(attacker, target, appliedDamage);
        return died;
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
        if (damage <= 0f || !target.IsAlive)
        {
            return false;
        }

        if (allowCivvieUmbrellaShield
            && TryAbsorbCivvieUmbrellaDamage(
                target,
                attacker,
                damageFlags,
                civvieUmbrellaThreatSourceX,
                civvieUmbrellaThreatSourceY,
                civvieUmbrellaDrainTicks,
                civvieUmbrellaCriticalBoost))
        {
            return false;
        }

        damage = ApplyExperimentalOutgoingDamageMultiplier(attacker, target, damage);
        damage = ApplyExperimentalIncomingDamageMultiplier(target, attacker, damage);
        damage = ScaleConfiguredDamage(damage);
        damage = target.AbsorbExperimentalShieldDamage(damage);
        if (damage <= 0f)
        {
            return false;
        }

        if (TryConvertExperimentalSelfDamageToHealing(target, attacker, damage))
        {
            return false;
        }

        var healthBefore = target.Health;
        if (TryRegisterExperimentalGhostDashEvade(target, attacker, damageFlags))
        {
            return false;
        }

        if (TryEvadeExperimentalDamage(target, attacker, damage, damageFlags))
        {
            return false;
        }

        if (TryPreventExperimentalFatalDamage(target, (int)MathF.Ceiling(damage)))
        {
            RegisterDamageEvent(
                attacker,
                DamageTargetKind.Player,
                target.Id,
                target.X,
                target.Y,
                Math.Max(0, healthBefore - target.Health),
                wasFatal: false,
                target,
                damageFlags);
            return false;
        }

        var roundedDamage = Math.Max(1, (int)MathF.Ceiling(damage));
        var wouldBeFatal = damage >= target.Health;
        if (ShouldCancelDamage(
                DamageTargetKind.Player,
                target.Id,
                target.Id,
                target.Team,
                attacker,
                roundedDamage,
                wouldBeFatal,
                target.X,
                target.Y))
        {
            return false;
        }

        if (TryAbsorbPracticeCombatDummyContinuousDamage(target, damage, attacker, damageFlags))
        {
            return false;
        }

        if (wouldBeFatal && ShouldCancelDeath(target, gibbed: false, attacker, weaponSpriteName: null))
        {
            return false;
        }

        var died = target.ApplyContinuousDamage(damage, spyRevealAlpha);
        var appliedDamage = Math.Max(0, healthBefore - target.Health);
        RegisterPlayerDamageDealer(target, attacker, appliedDamage);
        RegisterDamageEvent(
            attacker,
            DamageTargetKind.Player,
            target.Id,
            target.X,
            target.Y,
            appliedDamage,
            died,
            target,
            damageFlags);
        ApplyExperimentalDamageRewards(attacker, target, appliedDamage, allowOsmosisHealOwnedSentries);
        ApplyExperimentalDamageTakenRewards(target, attacker, appliedDamage);
        if (attacker is not null)
        {
            ApplyExperimentalEngineerFriendlyFireRetaliation(attacker, target, appliedDamage);
        }
        TryRegisterCombatComboHit(attacker, target, appliedDamage);
        return died;
    }

    private bool TryAbsorbCivvieUmbrellaDamage(
        PlayerEntity target,
        PlayerEntity? attacker,
        DamageEventFlags damageFlags,
        float? threatSourceX = null,
        float? threatSourceY = null,
        int? drainTicks = null,
        bool criticalBoost = false)
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
        var isCriticalBoosted = criticalBoost || attacker.IsKritzCritBoosted;
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
            criticalBoost || attacker.IsKritzCritBoosted);
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
        bool wasFatal)
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
