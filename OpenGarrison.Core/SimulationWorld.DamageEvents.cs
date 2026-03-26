namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void RegisterDamageEvent(
        PlayerEntity? attacker,
        DamageTargetKind targetKind,
        int targetEntityId,
        float x,
        float y,
        int amount,
        bool wasFatal)
    {
        if (amount <= 0)
        {
            return;
        }

        var attackerPlayerId = attacker?.Id ?? -1;
        var assistedByPlayerId = attacker is null
            ? -1
            : FindHealingMedicPlayerId(attacker.Id);
        _pendingDamageEvents.Add(new WorldDamageEvent(
            amount,
            attackerPlayerId,
            assistedByPlayerId,
            targetKind,
            targetEntityId,
            x,
            y,
            wasFatal,
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

    private bool ApplyPlayerDamage(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha = 0f)
    {
        if (damage <= 0 || !target.IsAlive)
        {
            return false;
        }

        var healthBefore = target.Health;
        var died = target.ApplyDamage(damage, spyRevealAlpha);
        RegisterDamageEvent(
            attacker,
            DamageTargetKind.Player,
            target.Id,
            target.X,
            target.Y,
            Math.Max(0, healthBefore - target.Health),
            died);
        return died;
    }

    private bool ApplyPlayerContinuousDamage(PlayerEntity target, float damage, PlayerEntity? attacker, float spyRevealAlpha = 0f)
    {
        if (damage <= 0f || !target.IsAlive)
        {
            return false;
        }

        var healthBefore = target.Health;
        var died = target.ApplyContinuousDamage(damage, spyRevealAlpha);
        RegisterDamageEvent(
            attacker,
            DamageTargetKind.Player,
            target.Id,
            target.X,
            target.Y,
            Math.Max(0, healthBefore - target.Health),
            died);
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
}
