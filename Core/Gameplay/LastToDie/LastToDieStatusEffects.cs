namespace OpenGarrison.Core.LastToDie;

public enum LastToDieStatusEffectKind : byte
{
    Bleed = 1,
    Poison = 2,
    Slow = 3,
    Stun = 4,
    BeneficialBuff = 5,
}

public readonly record struct LastToDieStatusEffectId(string Value)
{
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>
/// Stable status channels used to keep unrelated perk effects independent.
/// Reapplying one channel from the same source refreshes that channel; effects
/// from different channels or sources remain independently attributed.
/// </summary>
public static class LastToDieStatusEffectIds
{
    public static readonly LastToDieStatusEffectId SpyBlunderbussBleed = Id("spy.blunderbuss.bleed");
    public static readonly LastToDieStatusEffectId SpyRubberBulletsSlow = Id("spy.rubber-bullets.slow");
    public static readonly LastToDieStatusEffectId SpyLuckyStrikeStun = Id("spy.lucky-strike.stun");
    public static readonly LastToDieStatusEffectId MedicExsanguinationBleed = Id("medic.exsanguination.bleed");
    public static readonly LastToDieStatusEffectId MedicExsanguinationSlow = Id("medic.exsanguination.slow");
    public static readonly LastToDieStatusEffectId MedicNeurotoxinStun = Id("medic.neurotoxin.stun");
    public static readonly LastToDieStatusEffectId SniperTranqPoison = Id("sniper.tranq.poison");
    public static readonly LastToDieStatusEffectId SniperTranqSlow = Id("sniper.tranq.slow");
    public static readonly LastToDieStatusEffectId SniperPoisonTip = Id("sniper.poison-tip.poison");
    public static readonly LastToDieStatusEffectId SniperGuardian = Id("sniper.guardian");

    private static LastToDieStatusEffectId Id(string suffix) => new($"ltd.status.{suffix}");
}

/// <summary>
/// A normalized, tick-based status request. Damage and movement fields are
/// intentionally explicit so one field never changes meaning by effect kind.
/// </summary>
public readonly record struct LastToDieStatusEffectSpec(
    LastToDieStatusEffectId Id,
    LastToDieStatusEffectKind Kind,
    int DurationTicks,
    float DamagePerSecond = 0f,
    float MovementSpeedMultiplier = 1f,
    float HealingPerSecond = 0f,
    float EvasionChance = 0f,
    float OutgoingDamageMultiplier = 1f,
    int StackCount = 1)
{
    public static LastToDieStatusEffectSpec Bleed(
        LastToDieStatusEffectId id,
        int durationTicks,
        float damagePerSecond)
        => new(id, LastToDieStatusEffectKind.Bleed, durationTicks, damagePerSecond);

    public static LastToDieStatusEffectSpec Poison(
        LastToDieStatusEffectId id,
        int durationTicks,
        float damagePerSecond)
        => new(id, LastToDieStatusEffectKind.Poison, durationTicks, damagePerSecond);

    public static LastToDieStatusEffectSpec Slow(
        LastToDieStatusEffectId id,
        int durationTicks,
        float movementSpeedMultiplier,
        float outgoingDamageMultiplier = 1f,
        int stackCount = 1)
        => new(
            id,
            LastToDieStatusEffectKind.Slow,
            durationTicks,
            MovementSpeedMultiplier: movementSpeedMultiplier,
            OutgoingDamageMultiplier: outgoingDamageMultiplier,
            StackCount: stackCount);

    public static LastToDieStatusEffectSpec Stun(
        LastToDieStatusEffectId id,
        int durationTicks)
        => new(id, LastToDieStatusEffectKind.Stun, durationTicks);

    public static LastToDieStatusEffectSpec BeneficialBuff(
        LastToDieStatusEffectId id,
        int durationTicks,
        float healingPerSecond,
        float evasionChance)
        => new(
            id,
            LastToDieStatusEffectKind.BeneficialBuff,
            durationTicks,
            HealingPerSecond: healingPerSecond,
            EvasionChance: evasionChance);
}

public sealed record LastToDieActiveStatusEffectSnapshot(
    LastToDieStatusEffectId Id,
    LastToDieStatusEffectKind Kind,
    int TargetPlayerId,
    int? SourcePlayerId,
    int RemainingTicks,
    float DamagePerSecond,
    float MovementSpeedMultiplier,
    float HealingPerSecond = 0f,
    float EvasionChance = 0f,
    float OutgoingDamageMultiplier = 1f,
    int StackCount = 1);
