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
        if (amount <= 0)
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

    private bool ApplyPlayerDamage(
        PlayerEntity target,
        int damage,
        PlayerEntity? attacker,
        float spyRevealAlpha = 0f,
        DamageEventFlags damageFlags = DamageEventFlags.None)
    {
        if (damage <= 0 || !target.IsAlive)
        {
            return false;
        }

        var healthBefore = target.Health;
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
        ApplyExperimentalDamageRewards(attacker, target, appliedDamage);
        ApplyExperimentalDamageTakenRewards(target, attacker, appliedDamage);
        TryRegisterCombatComboHit(attacker, target, appliedDamage);
        return died;
    }

    private bool ApplyPlayerContinuousDamage(
        PlayerEntity target,
        float damage,
        PlayerEntity? attacker,
        float spyRevealAlpha = 0f,
        DamageEventFlags damageFlags = DamageEventFlags.None)
    {
        if (damage <= 0f || !target.IsAlive)
        {
            return false;
        }

        var healthBefore = target.Health;
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
        ApplyExperimentalDamageRewards(attacker, target, appliedDamage);
        ApplyExperimentalDamageTakenRewards(target, attacker, appliedDamage);
        TryRegisterCombatComboHit(attacker, target, appliedDamage);
        return died;
    }

    private bool ApplySentryDamage(SentryEntity target, int damage, PlayerEntity? attacker)
    {
        if (damage <= 0)
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
