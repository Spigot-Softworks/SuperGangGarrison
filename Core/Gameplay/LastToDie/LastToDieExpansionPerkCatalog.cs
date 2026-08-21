namespace OpenGarrison.Core.LastToDie;

public static class LastToDiePerkIds
{
    public static class Spy
    {
        public static readonly LastToDiePerkId Blunderbuss1 = Id("spy.blunderbuss.1");
        public static readonly LastToDiePerkId Blunderbuss2 = Id("spy.blunderbuss.2");
        public static readonly LastToDiePerkId Blunderbuss3 = Id("spy.blunderbuss.3");
        public static readonly LastToDiePerkId Rejuvenation = Id("spy.rejuvenation");
        public static readonly LastToDiePerkId ChameleonShell = Id("spy.chameleon-shell");
        public static readonly LastToDiePerkId Multistab = Id("spy.multistab");
        public static readonly LastToDiePerkId SpringLoaded = Id("spy.spring-loaded");
        public static readonly LastToDiePerkId Instastab = Id("spy.instastab");
        public static readonly LastToDiePerkId Healstab = Id("spy.healstab");
        public static readonly LastToDiePerkId Shroud = Id("spy.shroud");
        public static readonly LastToDiePerkId RogueCommander = Id("spy.rogue-commander");
        public static readonly LastToDiePerkId HealingHarness = Id("spy.healing-harness");
        public static readonly LastToDiePerkId Deadly = Id("spy.deadly");
        public static readonly LastToDiePerkId Professional = Id("spy.professional");
        public static readonly LastToDiePerkId Infiltrate = Id("spy.infiltrate");
        public static readonly LastToDiePerkId Executioner = Id("spy.executioner");
        public static readonly LastToDiePerkId Agent = Id("spy.agent");
        public static readonly LastToDiePerkId DoubleJump = Id("spy.double-jump");
        public static readonly LastToDiePerkId Afterlife = Id("spy.afterlife");
        public static readonly LastToDiePerkId Grounded = Id("spy.grounded");
        public static readonly LastToDiePerkId Acrobat = Id("spy.acrobat");
        public static readonly LastToDiePerkId Ricochet = Id("spy.ricochet");
        public static readonly LastToDiePerkId RubberBullets = Id("spy.rubber-bullets");
        public static readonly LastToDiePerkId LuckyStrike = Id("spy.lucky-strike");
        public static readonly LastToDiePerkId Vampire = Id("spy.vampire");
    }

    public static class Medic
    {
        public static readonly LastToDiePerkId TraumaSurgeon = Id("medic.trauma-surgeon");
        public static readonly LastToDiePerkId CombatMedic = Id("medic.combat-medic");
        public static readonly LastToDiePerkId StimulantDrip = Id("medic.stimulant-drip");
        public static readonly LastToDiePerkId Overcharged = Id("medic.overcharged");
        public static readonly LastToDiePerkId FieldCommander = Id("medic.field-commander");
        public static readonly LastToDiePerkId Exsanguination = Id("medic.exsanguination");
        public static readonly LastToDiePerkId KritPower = Id("medic.krit-power");
        public static readonly LastToDiePerkId VitalityTrinket = Id("medic.vitality-trinket");
        public static readonly LastToDiePerkId Stoic = Id("medic.stoic");
        public static readonly LastToDiePerkId AgilityDrive = Id("medic.agility-drive");
        public static readonly LastToDiePerkId RejuvenationRay = Id("medic.rejuvenation-ray");
        public static readonly LastToDiePerkId Homeostasis = Id("medic.homeostasis");
        public static readonly LastToDiePerkId Javelin = Id("medic.javelin");
        public static readonly LastToDiePerkId HailMary = Id("medic.hail-mary");
        public static readonly LastToDiePerkId ModifiedSpring = Id("medic.modified-spring");
        public static readonly LastToDiePerkId Neurotoxin = Id("medic.neurotoxin");
        public static readonly LastToDiePerkId SupportRelay = Id("medic.support-relay");
        public static readonly LastToDiePerkId SpikedVest = Id("medic.spiked-vest");
        public static readonly LastToDiePerkId IronWill = Id("medic.iron-will");
        public static readonly LastToDiePerkId Martyr = Id("medic.martyr");
    }

    public static class Sniper
    {
        public static readonly LastToDiePerkId FiftyCal = Id("sniper.fifty-cal");
        public static readonly LastToDiePerkId Overcharged = Id("sniper.overcharged");
        public static readonly LastToDiePerkId Fmj = Id("sniper.fmj");
        public static readonly LastToDiePerkId GreasedBolt = Id("sniper.greased-bolt");
        public static readonly LastToDiePerkId Ghost = Id("sniper.ghost");
        public static readonly LastToDiePerkId Spotted = Id("sniper.spotted");
        public static readonly LastToDiePerkId Guardian = Id("sniper.guardian");
        public static readonly LastToDiePerkId TranqDarts = Id("sniper.tranq-darts");
        public static readonly LastToDiePerkId PoisonTip = Id("sniper.poison-tip");
        public static readonly LastToDiePerkId Decapitator = Id("sniper.decapitator");
        public static readonly LastToDiePerkId LightMarksman = Id("sniper.light-marksman");
        public static readonly LastToDiePerkId MenageATrois = Id("sniper.menage-a-trois");
        public static readonly LastToDiePerkId ExtremeConditioning = Id("sniper.extreme-conditioning");
        public static readonly LastToDiePerkId Mechanica = Id("sniper.mechanica");
        public static readonly LastToDiePerkId Zen = Id("sniper.zen");
        public static readonly LastToDiePerkId Overkiller = Id("sniper.overkiller");
        public static readonly LastToDiePerkId ExplosiveTip = Id("sniper.explosive-tip");
        public static readonly LastToDiePerkId Conquistador = Id("sniper.conquistador");
    }

    private static LastToDiePerkId Id(string suffix) => new($"ltd.perk.{suffix}");
}

public static class LastToDieExpansionPerkCatalog
{
    public static LastToDiePerkCatalog Create(LastToDieSurvivorCatalog survivors)
        => new(survivors, CreateDefinitions());

    public static IReadOnlyList<LastToDiePerkDefinition> CreateDefinitions()
    {
        var spy = LastToDieSurvivorCatalog.SpyId;
        var medic = LastToDieSurvivorCatalog.MedicId;
        var sniper = LastToDieSurvivorCatalog.SniperId;
        var agentAndRubber = new[] { LastToDiePerkIds.Spy.Agent, LastToDiePerkIds.Spy.RubberBullets };
        var blunderbussRanks = new[]
        {
            LastToDiePerkIds.Spy.Blunderbuss1,
            LastToDiePerkIds.Spy.Blunderbuss2,
            LastToDiePerkIds.Spy.Blunderbuss3,
        };

        return
        [
            Perk(LastToDiePerkIds.Spy.Blunderbuss1, spy, "Blunderbuss", "Single-shot 13-pellet revolver conversion with bleed and +30% reload speed.", excludes: agentAndRubber, tags: ["revolver", "weapon-profile", "bleed"]),
            Perk(LastToDiePerkIds.Spy.Blunderbuss2, spy, "Blunderbuss II", "Two-shell clip with stronger bleed, damage, and knockback.", 2, [LastToDiePerkIds.Spy.Blunderbuss1], agentAndRubber, ["revolver", "weapon-profile", "bleed"]),
            Perk(LastToDiePerkIds.Spy.Blunderbuss3, spy, "Blunderbuss III", "Double pellets, wider spread, and faster reload.", 3, [LastToDiePerkIds.Spy.Blunderbuss1, LastToDiePerkIds.Spy.Blunderbuss2], agentAndRubber, ["revolver", "weapon-profile"]),
            Perk(LastToDiePerkIds.Spy.Rejuvenation, spy, "Rejuvenation", "Move faster and regenerate health while cloaked.", tags: ["cloak", "movement", "healing"]),
            Perk(LastToDiePerkIds.Spy.ChameleonShell, spy, "Chameleon Shell", "Gain damage resistance while cloaked.", tags: ["cloak", "resistance"]),
            Perk(LastToDiePerkIds.Spy.Multistab, spy, "Multistab", "Backstabs affect nearby enemies without the normal damage cap.", tags: ["backstab", "multi-hit"]),
            Perk(LastToDiePerkIds.Spy.SpringLoaded, spy, "Spring Loaded", "Backstabs reset jump boots.", tags: ["backstab", "jump-boots"]),
            Perk(LastToDiePerkIds.Spy.Instastab, spy, "Instastab", "Greatly accelerates the backstab animation.", tags: ["backstab"]),
            Perk(LastToDiePerkIds.Spy.Healstab, spy, "Healstab", "Stabbing an ally heals them.", tags: ["backstab", "healing"]),
            Perk(LastToDiePerkIds.Spy.Shroud, spy, "Shroud", "Gain evasion while cloaked and briefly after uncloaking.", tags: ["cloak", "evasion"]),
            Perk(LastToDiePerkIds.Spy.RogueCommander, spy, "Rogue Commander", "Metered cloak, cloaked capture, and an uncloaked damage/resistance ramp.", tags: ["cloak", "objective", "damage", "resistance"]),
            Perk(LastToDiePerkIds.Spy.HealingHarness, spy, "Healing Harness", "Jump boots heal and extinguish flames.", tags: ["jump-boots", "healing"]),
            Perk(LastToDiePerkIds.Spy.Deadly, spy, "Deadly", "Revolver shots gain a critical-hit chance.", tags: ["revolver", "critical"]),
            Perk(LastToDiePerkIds.Spy.Professional, spy, "The Professional", "Hold M2 to fire your revolver instead of backstabbing while cloaked.", tags: ["revolver", "cloak"]),
            Perk(LastToDiePerkIds.Spy.Infiltrate, spy, "Infiltrate", "Use the perk utility to dash with projectile immunity.", tags: ["utility", "movement", "immunity"]),
            Perk(LastToDiePerkIds.Spy.Executioner, spy, "Executioner", "Revolver shots crit enemies below the health threshold.", tags: ["revolver", "critical"]),
            Perk(LastToDiePerkIds.Spy.Agent, spy, "Agent", "Increase revolver clip size to nine.", excludes: blunderbussRanks, tags: ["revolver", "ammo"]),
            Perk(LastToDiePerkIds.Spy.DoubleJump, spy, "Double Jump", "Jump boots gain a second use and charge faster.", tags: ["jump-boots", "movement"]),
            Perk(LastToDiePerkIds.Spy.Afterlife, spy, "Afterlife", "Enter a temporary kill-to-resurrect ghost state on death.", tags: ["death", "resurrection"]),
            Perk(LastToDiePerkIds.Spy.Grounded, spy, "Grounded", "Deal bonus damage from the ground to airborne enemies.", tags: ["damage", "stance"]),
            Perk(LastToDiePerkIds.Spy.Acrobat, spy, "Acrobat", "Deal bonus damage while airborne to grounded enemies.", tags: ["damage", "stance"]),
            Perk(LastToDiePerkIds.Spy.Ricochet, spy, "Ricochet", "Revolver bullets bounce between enemies.", tags: ["revolver", "multi-hit"]),
            Perk(LastToDiePerkIds.Spy.RubberBullets, spy, "Rubber Bullets", "Revolver bullets launch and slow enemies.", excludes: blunderbussRanks, tags: ["revolver", "slow", "knockback"]),
            Perk(LastToDiePerkIds.Spy.LuckyStrike, spy, "Lucky Strike", "Every third revolver shot stuns.", tags: ["revolver", "stun"]),
            Perk(LastToDiePerkIds.Spy.Vampire, spy, "Vampire", "Heal for a fraction of damage dealt.", tags: ["healing", "damage-reward"]),

            Perk(LastToDiePerkIds.Medic.TraumaSurgeon, medic, "Trauma Surgeon", "Healing increases as the target loses health.", tags: ["medigun", "healing"]),
            Perk(LastToDiePerkIds.Medic.CombatMedic, medic, "Combat Medic", "Gain damage and resistance below half health.", tags: ["damage", "resistance"]),
            Perk(LastToDiePerkIds.Medic.StimulantDrip, medic, "Stimulant Drip", "The heal target gains offense, reload speed, and resistance.", tags: ["medigun", "link"]),
            Perk(LastToDiePerkIds.Medic.Overcharged, medic, "Overcharged", "Build Uber twice as fast.", tags: ["uber"]),
            Perk(LastToDiePerkIds.Medic.FieldCommander, medic, "Field Commander", "Capture while Ubered.", tags: ["uber", "objective"]),
            Perk(LastToDiePerkIds.Medic.Exsanguination, medic, "Exsanguination", "The Medic and heal target inflict bleed and slow.", tags: ["link", "bleed", "slow"]),
            Perk(LastToDiePerkIds.Medic.KritPower, medic, "Krit Power", "Increase Kritzkrieg critical damage.", tags: ["kritzkrieg", "critical"]),
            Perk(LastToDiePerkIds.Medic.VitalityTrinket, medic, "Vitality Trinket", "Increase maximum health.", tags: ["health"]),
            Perk(LastToDiePerkIds.Medic.Stoic, medic, "Stoic", "Held Ubercharge grants scaling evasion.", tags: ["uber", "evasion"]),
            Perk(LastToDiePerkIds.Medic.AgilityDrive, medic, "Agility Drive", "The Medic and heal target gain movement and evasion.", tags: ["medigun", "link", "movement", "evasion"]),
            Perk(LastToDiePerkIds.Medic.RejuvenationRay, medic, "Rejuvenation Ray", "Uber trades invincibility for greatly increased healing.", tags: ["uber", "healing"]),
            Perk(LastToDiePerkIds.Medic.Homeostasis, medic, "Homeostasis", "Self-heal for a fraction of healing done.", tags: ["healing", "healing-reward"]),
            Perk(LastToDiePerkIds.Medic.Javelin, medic, "Javelin", "Kritzkrieg M2 projectiles explode after a fuse.", tags: ["kritzkrieg", "projectile", "explosion"]),
            Perk(LastToDiePerkIds.Medic.HailMary, medic, "Hail Mary", "Kritzkrieg M2 grants brief ally invulnerability.", tags: ["kritzkrieg", "projectile", "invulnerability"]),
            Perk(LastToDiePerkIds.Medic.ModifiedSpring, medic, "Modified Spring", "Needleguns fire and reload twice as fast.", tags: ["needlegun", "attack-speed", "reload-speed"]),
            Perk(LastToDiePerkIds.Medic.Neurotoxin, medic, "Neurotoxin", "Kritzkrieg M2 stuns and deals bonus damage to stunned enemies.", tags: ["kritzkrieg", "projectile", "stun"]),
            Perk(LastToDiePerkIds.Medic.SupportRelay, medic, "Support Relay", "Healing weapons restore missing target ammo.", tags: ["medigun", "kritzkrieg", "ammo"]),
            Perk(LastToDiePerkIds.Medic.SpikedVest, medic, "Spiked Vest", "Gain resistance and reflect damage.", tags: ["resistance", "reflect"]),
            Perk(LastToDiePerkIds.Medic.IronWill, medic, "Iron Will", "Increase health regeneration at low health.", tags: ["healing", "health-threshold"]),
            Perk(LastToDiePerkIds.Medic.Martyr, medic, "Martyr", "Protect a heal target from death and redirect bot threat.", tags: ["medigun", "link", "fatal-protection", "threat"]),

            Perk(LastToDiePerkIds.Sniper.FiftyCal, sniper, ".50 cal", "Slower rifle shots gib the first target and pierce another.", tags: ["rifle", "pierce", "execute"]),
            Perk(LastToDiePerkIds.Sniper.Overcharged, sniper, "Overcharged", "Reach full rifle and Huntsman charge faster.", tags: ["rifle", "huntsman", "charge"]),
            Perk(LastToDiePerkIds.Sniper.Fmj, sniper, "FMJ", "Rifle shots pass through solid geometry.", tags: ["rifle", "geometry"]),
            Perk(LastToDiePerkIds.Sniper.GreasedBolt, sniper, "Greased Bolt", "Increase rifle rate of fire.", tags: ["rifle", "attack-speed"]),
            Perk(LastToDiePerkIds.Sniper.Ghost, sniper, "Ghost", "Cloak with the perk utility and empower the next shot.", tags: ["utility", "cloak", "damage"]),
            Perk(LastToDiePerkIds.Sniper.Spotted, sniper, "Spotted", "Mark one target for increased subsequent damage.", tags: ["mark", "damage"]),
            Perk(LastToDiePerkIds.Sniper.Guardian, sniper, "Guardian", "Shots grant allies healing and evasion.", tags: ["rifle", "huntsman", "healing", "evasion"]),
            Perk(LastToDiePerkIds.Sniper.TranqDarts, sniper, "Tranq Darts", "Shots deal less damage but poison, slow, and weaken enemies.", tags: ["rifle", "huntsman", "poison", "slow"]),
            Perk(LastToDiePerkIds.Sniper.PoisonTip, sniper, "Poison Tip", "Arrows apply charge-scaled poison.", tags: ["huntsman", "poison"]),
            Perk(LastToDiePerkIds.Sniper.Decapitator, sniper, "Decapitator", "Fully charged headshots execute and arrows carry heads.", tags: ["rifle", "huntsman", "headshot", "execute"]),
            Perk(LastToDiePerkIds.Sniper.LightMarksman, sniper, "Light Marksman", "Rifle loses scope/charge for higher base damage and fire rate.", tags: ["rifle", "weapon-profile"]),
            Perk(LastToDiePerkIds.Sniper.MenageATrois, sniper, "Menage A Trois", "Fully charged Huntsman shots fire a three-arrow volley.", tags: ["huntsman", "volley"]),
            Perk(LastToDiePerkIds.Sniper.ExtremeConditioning, sniper, "Extreme Conditioning", "Move faster without rifle charge slowdown.", tags: ["rifle", "movement"]),
            Perk(LastToDiePerkIds.Sniper.Mechanica, sniper, "Mechanica", "Fully charged rifle and Huntsman shots pierce without a target limit.", tags: ["rifle", "huntsman", "pierce"]),
            Perk(LastToDiePerkIds.Sniper.Zen, sniper, "Zen", "Regenerate health while scoped.", tags: ["scope", "healing"]),
            Perk(LastToDiePerkIds.Sniper.Overkiller, sniper, "Overkiller", "Damage has a chance to instantly kill enemies.", tags: ["execute"]),
            Perk(LastToDiePerkIds.Sniper.ExplosiveTip, sniper, "Explosive Tip", "Detonate Huntsman arrows manually or at end of life.", tags: ["huntsman", "explosion"]),
            Perk(LastToDiePerkIds.Sniper.Conquistador, sniper, "Conquistador", "Kills grant stacking damage until death.", tags: ["kill-reward", "damage"]),
        ];
    }

    private static LastToDiePerkDefinition Perk(
        LastToDiePerkId id,
        LastToDieSurvivorId survivor,
        string name,
        string description,
        int rank = 1,
        IReadOnlyList<LastToDiePerkId>? requires = null,
        IReadOnlyList<LastToDiePerkId>? excludes = null,
        IReadOnlyList<string>? tags = null)
        => new(id, survivor, name, description, rank, requires, excludes, tags);
}
