namespace OpenGarrison.Core.LastToDie;

/// <summary>
/// Shared runtime effects for all-class Last to Die perks. Keeping these
/// together makes the stacking rules explicit and avoids adding one-off
/// state to the offline gameplay settings model.
/// </summary>
public sealed record LastToDieUniversalModifiers
{
    public int MaximumHealthBonus { get; init; }

    public int? BaseMaximumHealthOverride { get; init; }

    public float PlayerScale { get; init; } = 1f;

    public float MeleeScale { get; init; } = 1f;

    public float ProjectileScale { get; init; } = 1f;

    public float ExplosionScale { get; init; } = 1f;

    public float MovementSpeedMultiplier { get; init; } = 1f;

    public float FireSpeedMultiplier { get; init; } = 1f;

    public float ReloadSpeedMultiplier { get; init; } = 1f;

    public float OutgoingDamageMultiplier { get; init; } = 1f;

    public float BulletDamageTakenMultiplier { get; init; } = 1f;

    public float ExplosionDamageTakenMultiplier { get; init; } = 1f;

    public float KnockbackReceivedMultiplier { get; init; } = 1f;

    public float RegenerationPerSecond { get; init; }

    public float EvasionChance { get; init; }

    public float ReflectionFraction { get; init; }

    public int MaximumHealthPerRunKill { get; init; }

    public int HealPerKill { get; init; }

    public bool RageDisabled { get; init; }

    public bool InfiniteAmmo { get; init; }

    public bool InfiniteSlayWorks { get; init; }

    public bool SecondChance { get; init; }

    public bool FatalBravado { get; init; }

    public bool Mimic { get; init; }

    public bool Triage { get; init; }

    public bool Reinforcements { get; init; }

    public bool FireCrew { get; init; }

    public bool FreezingArmor { get; init; }

    public bool BlazingArmor { get; init; }

    public bool Fortify { get; init; }

    public bool Ragnarok { get; init; }

    public bool FightOrFlight { get; init; }

    public bool Sinister { get; init; }

    public bool LethalTango { get; init; }

    public bool ImmovableObject { get; init; }

    public bool Colossus { get; init; }

    public bool DefenseBattery { get; init; }

    public bool BattleMaster { get; init; }

    public bool LuckyDraw { get; init; }

    public static LastToDieUniversalModifiers FromPerks(IEnumerable<LastToDiePerkId> perks)
    {
        ArgumentNullException.ThrowIfNull(perks);
        var owned = perks.ToHashSet();
        bool Has(LastToDiePerkId id) => owned.Contains(id);

        var troopersBlessing = Has(LastToDiePerkIds.Rare.TroopersBlessing);
        var speedBooster = Has(LastToDiePerkIds.Rare.SpeedBooster);
        var sleightOfHand = Has(LastToDiePerkIds.Rare.SleightOfHand);
        var zergRush = Has(LastToDiePerkIds.Ultra.ZergRush);
        var immovableObject = Has(LastToDiePerkIds.Ultra.ImmovableObject);
        var spikedArmor = Has(LastToDiePerkIds.Rare.SpikedArmor);
        var fatalBravado = Has(LastToDiePerkIds.Ultra.FatalBravado);

        return new LastToDieUniversalModifiers
        {
            MaximumHealthBonus = (Has(LastToDiePerkIds.Rare.Colossus) ? 150 : 0)
                + (Has(LastToDiePerkIds.Rare.HealthBooster) ? 50 : 0)
                + (immovableObject ? 60 : 0)
                - (zergRush ? 50 : 0),
            BaseMaximumHealthOverride = Has(LastToDiePerkIds.Ultra.GutsAndGlory) ? 50 : null,
            PlayerScale = (zergRush ? 0.3f : 1f)
                * (Has(LastToDiePerkIds.Rare.Colossus) ? 1.3f : 1f),
            MeleeScale = Has(LastToDiePerkIds.Rare.Colossus) ? 1.3f : 1f,
            ProjectileScale = Has(LastToDiePerkIds.Rare.Colossus) ? 1.3f : 1f,
            ExplosionScale = Has(LastToDiePerkIds.Rare.Colossus) ? 1.3f : 1f,
            MovementSpeedMultiplier = (troopersBlessing ? 1.15f : 1f)
                * (speedBooster ? 1.15f : 1f),
            FireSpeedMultiplier = (troopersBlessing ? 1.15f : 1f)
                * (speedBooster ? 1.15f : 1f)
                * (sleightOfHand ? 1.25f : 1f)
                * (zergRush ? 2f : 1f),
            ReloadSpeedMultiplier = (troopersBlessing ? 1.15f : 1f)
                * (speedBooster ? 1.15f : 1f)
                * (sleightOfHand ? 1.25f : 1f)
                * (zergRush ? 2f : 1f),
            OutgoingDamageMultiplier = Has(LastToDiePerkIds.Rare.PowerBooster) ? 1.15f : 1f,
            BulletDamageTakenMultiplier = immovableObject ? 0.8f : 1f,
            ExplosionDamageTakenMultiplier = immovableObject ? 0.8f : 1f,
            KnockbackReceivedMultiplier = immovableObject ? 0.4f : 1f,
            RegenerationPerSecond = troopersBlessing ? 5f : 0f,
            EvasionChance = Has(LastToDiePerkIds.Ultra.FightOrFlight) ? 0.3f : 0f,
            ReflectionFraction = spikedArmor ? 0.2f : 0f,
            MaximumHealthPerRunKill = (Has(LastToDiePerkIds.Ultra.GutsAndGlory) ? 2 : 0)
                + (Has(LastToDiePerkIds.Ultra.HeartOfBravery) ? 1 : 0),
            HealPerKill = Has(LastToDiePerkIds.Ultra.InfiniteSlayWorks) ? 10 : 0,
            RageDisabled = zergRush,
            InfiniteAmmo = Has(LastToDiePerkIds.Ultra.InfiniteSlayWorks),
            InfiniteSlayWorks = Has(LastToDiePerkIds.Ultra.InfiniteSlayWorks),
            SecondChance = Has(LastToDiePerkIds.Ultra.SecondChance),
            FatalBravado = fatalBravado,
            Mimic = Has(LastToDiePerkIds.Rare.Mimic),
            Triage = Has(LastToDiePerkIds.Rare.Triage),
            Reinforcements = Has(LastToDiePerkIds.Rare.Reinforcements),
            FireCrew = Has(LastToDiePerkIds.Rare.FireCrew),
            FreezingArmor = Has(LastToDiePerkIds.Rare.FreezingArmor),
            BlazingArmor = Has(LastToDiePerkIds.Rare.BlazingArmor),
            Fortify = Has(LastToDiePerkIds.Rare.Fortify),
            Ragnarok = Has(LastToDiePerkIds.Rare.Ragnarok),
            FightOrFlight = Has(LastToDiePerkIds.Ultra.FightOrFlight),
            Sinister = Has(LastToDiePerkIds.Ultra.Sinister),
            LethalTango = Has(LastToDiePerkIds.Ultra.LethalTango),
            ImmovableObject = immovableObject,
            Colossus = Has(LastToDiePerkIds.Rare.Colossus),
            DefenseBattery = Has(LastToDiePerkIds.Ultra.DefenseBattery),
            BattleMaster = Has(LastToDiePerkIds.Ultra.BattleMaster),
            LuckyDraw = Has(LastToDiePerkIds.Ultra.LuckyDraw),
        };
    }
}
