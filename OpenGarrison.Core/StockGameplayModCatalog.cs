using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public static class StockGameplayModCatalog
{
    public static GameplayModPackDefinition Definition { get; } = CreateDefinition();

    public static string GetClassId(PlayerClass playerClass)
    {
        return playerClass switch
        {
            PlayerClass.Engineer => "engineer",
            PlayerClass.Pyro => "pyro",
            PlayerClass.Soldier => "soldier",
            PlayerClass.Demoman => "demoman",
            PlayerClass.Heavy => "heavy",
            PlayerClass.Sniper => "sniper",
            PlayerClass.Medic => "medic",
            PlayerClass.Spy => "spy",
            PlayerClass.Quote => "quote",
            _ => "scout",
        };
    }

    public static GameplayClassDefinition GetClassDefinition(PlayerClass playerClass)
    {
        return Definition.Classes[GetClassId(playerClass)];
    }

    public static GameplayClassLoadoutDefinition GetDefaultLoadout(PlayerClass playerClass)
    {
        var classDefinition = GetClassDefinition(playerClass);
        return classDefinition.Loadouts[classDefinition.DefaultLoadoutId];
    }

    public static GameplayItemDefinition GetPrimaryItem(PlayerClass playerClass)
    {
        var loadout = GetDefaultLoadout(playerClass);
        return Definition.Items[loadout.PrimaryItemId];
    }

    public static GameplayItemDefinition? GetSecondaryItem(PlayerClass playerClass)
    {
        var loadout = GetDefaultLoadout(playerClass);
        return loadout.SecondaryItemId is null
            ? null
            : Definition.Items[loadout.SecondaryItemId];
    }

    public static GameplayItemDefinition? GetUtilityItem(PlayerClass playerClass)
    {
        var loadout = GetDefaultLoadout(playerClass);
        return loadout.UtilityItemId is null
            ? null
            : Definition.Items[loadout.UtilityItemId];
    }

    private static GameplayModPackDefinition CreateDefinition()
    {
        var items = new Dictionary<string, GameplayItemDefinition>(StringComparer.Ordinal)
        {
            ["weapon.scattergun"] = CreateWeaponItem(
                "weapon.scattergun",
                "Scattergun",
                BuiltInGameplayBehaviorIds.PelletGun,
                maxAmmo: 6,
                ammoPerUse: 1,
                projectilesPerUse: 6,
                useDelaySourceTicks: 20,
                reloadSourceTicks: 15,
                spreadDegrees: 7f,
                minProjectileSpeed: 11f,
                additionalProjectileSpeed: 4f,
                worldSpriteName: "ScattergunS",
                recoilSpriteName: "ScattergunFS",
                reloadSpriteName: "ScattergunFRS",
                hudSpriteName: "ScattergunAmmoS",
                weaponOffsetX: -5f,
                weaponOffsetY: -4f),
            ["weapon.shotgun"] = CreateWeaponItem(
                "weapon.shotgun",
                "Shotgun",
                BuiltInGameplayBehaviorIds.PelletGun,
                maxAmmo: 8,
                ammoPerUse: 1,
                projectilesPerUse: 4,
                useDelaySourceTicks: 20,
                reloadSourceTicks: 15,
                spreadDegrees: 5f,
                minProjectileSpeed: 11f,
                additionalProjectileSpeed: 4f,
                worldSpriteName: "ShotgunS",
                recoilSpriteName: "ShotgunFS",
                reloadSpriteName: "ShotgunFRS",
                hudSpriteName: "ShotgunAmmoS",
                weaponOffsetX: -5f,
                weaponOffsetY: -2f),
            ["weapon.flamethrower"] = CreateWeaponItem(
                "weapon.flamethrower",
                "Flamethrower",
                BuiltInGameplayBehaviorIds.Flamethrower,
                maxAmmo: 200,
                ammoPerUse: 2,
                projectilesPerUse: 1,
                useDelaySourceTicks: 1,
                reloadSourceTicks: 0,
                spreadDegrees: 5f,
                minProjectileSpeed: 5f,
                additionalProjectileSpeed: 5f,
                autoReloads: false,
                ammoRegenPerTick: 1,
                worldSpriteName: "FlamethrowerS",
                recoilSpriteName: "FlamethrowerFS",
                hudSpriteName: "GasAmmoS",
                weaponOffsetX: -11f,
                weaponOffsetY: 4f,
                recoilDurationSourceTicks: 3,
                loopRecoilWhileActive: true),
            ["weapon.rocketlauncher"] = CreateWeaponItem(
                "weapon.rocketlauncher",
                "Rocket Launcher",
                BuiltInGameplayBehaviorIds.RocketLauncher,
                maxAmmo: 4,
                ammoPerUse: 1,
                projectilesPerUse: 1,
                useDelaySourceTicks: 30,
                reloadSourceTicks: 22,
                minProjectileSpeed: 13f,
                worldSpriteName: "RocketlauncherS",
                recoilSpriteName: "RocketlauncherFS",
                reloadSpriteName: "RocketlauncherFRS",
                hudSpriteName: "Rocketclip",
                weaponOffsetX: -15f,
                weaponOffsetY: -10f,
                useAmmoCountForHudFrame: true,
                blueTeamAmmoHudFrameOffset: 5),
            ["weapon.minelauncher"] = CreateWeaponItem(
                "weapon.minelauncher",
                "Mine Launcher",
                BuiltInGameplayBehaviorIds.MineLauncher,
                maxAmmo: 8,
                ammoPerUse: 1,
                projectilesPerUse: 1,
                useDelaySourceTicks: 26,
                reloadSourceTicks: 15,
                minProjectileSpeed: 12f,
                worldSpriteName: "MinegunS",
                recoilSpriteName: "MinegunFS",
                reloadSpriteName: "MinegunFRS",
                hudSpriteName: "MinegunAmmoS",
                weaponOffsetX: -3f,
                weaponOffsetY: -2f),
            ["weapon.minigun"] = CreateWeaponItem(
                "weapon.minigun",
                "Minigun",
                BuiltInGameplayBehaviorIds.Minigun,
                maxAmmo: 200,
                ammoPerUse: 4,
                projectilesPerUse: 1,
                useDelaySourceTicks: 2,
                reloadSourceTicks: 0,
                spreadDegrees: 7f,
                minProjectileSpeed: 12f,
                additionalProjectileSpeed: 1f,
                autoReloads: false,
                ammoRegenPerTick: 1,
                worldSpriteName: "MinigunS",
                recoilSpriteName: "MinigunFS",
                hudSpriteName: "MinigunAmmoS",
                weaponOffsetX: -11f,
                recoilDurationSourceTicks: 3,
                loopRecoilWhileActive: true),
            ["weapon.rifle"] = CreateWeaponItem(
                "weapon.rifle",
                "Rifle",
                BuiltInGameplayBehaviorIds.Rifle,
                maxAmmo: 1,
                ammoPerUse: 0,
                projectilesPerUse: 1,
                useDelaySourceTicks: 40,
                reloadSourceTicks: 0,
                worldSpriteName: "RifleS",
                recoilSpriteName: "RifleFS",
                reloadSpriteName: "RifleFRS",
                weaponOffsetX: -5f,
                weaponOffsetY: -8f,
                recoilDurationSourceTicks: 15,
                scopedRecoilDurationSourceTicks: 60),
            ["weapon.medigun"] = CreateWeaponItem(
                "weapon.medigun",
                "Medigun",
                BuiltInGameplayBehaviorIds.Medigun,
                maxAmmo: 40,
                ammoPerUse: 0,
                projectilesPerUse: 1,
                useDelaySourceTicks: 3,
                reloadSourceTicks: 0,
                autoReloads: false,
                worldSpriteName: "MedigunS",
                recoilSpriteName: "MedigunFS",
                reloadSpriteName: "MedigunFRS",
                hudSpriteName: "NeedleAmmoS",
                weaponOffsetX: -7f,
                recoilDurationSourceTicks: 4,
                reloadDurationSourceTicks: PlayerEntity.MedicNeedleRefillTicksDefault),
            ["weapon.revolver"] = CreateWeaponItem(
                "weapon.revolver",
                "Revolver",
                BuiltInGameplayBehaviorIds.Revolver,
                maxAmmo: 6,
                ammoPerUse: 1,
                projectilesPerUse: 1,
                useDelaySourceTicks: 18,
                reloadSourceTicks: 45,
                spreadDegrees: 1f,
                minProjectileSpeed: 20f,
                refillsAllAtOnce: true,
                worldSpriteName: "RevolverS",
                recoilSpriteName: "RevolverFS",
                reloadSpriteName: "RevolverFRS",
                hudSpriteName: "RevolverAmmoS",
                weaponOffsetX: -3f,
                weaponOffsetY: -6f),
            ["weapon.blade"] = CreateWeaponItem(
                "weapon.blade",
                "Blade",
                BuiltInGameplayBehaviorIds.Blade,
                maxAmmo: 100,
                ammoPerUse: 0,
                projectilesPerUse: 1,
                useDelaySourceTicks: 5,
                reloadSourceTicks: 0,
                spreadDegrees: 4f,
                minProjectileSpeed: 10f,
                additionalProjectileSpeed: 2f,
                autoReloads: false,
                ammoRegenPerTick: 1,
                worldSpriteName: "BladeS",
                hudSpriteName: "BladeAmmoS",
                weaponOffsetX: -3f,
                weaponOffsetY: -6f,
                blueTeamHudFrameOffset: 1),

            ["ability.engineer-pda"] = CreateAbilityItem("ability.engineer-pda", "Engineer PDA", GameplayEquipmentSlot.Secondary, BuiltInGameplayBehaviorIds.EngineerPda),
            ["ability.pyro-airblast"] = CreateAbilityItem("ability.pyro-airblast", "Compression Blast", GameplayEquipmentSlot.Secondary, BuiltInGameplayBehaviorIds.PyroAirblast),
            ["ability.demoman-detonate"] = CreateAbilityItem("ability.demoman-detonate", "Mine Detonator", GameplayEquipmentSlot.Secondary, BuiltInGameplayBehaviorIds.DemomanDetonate),
            ["ability.heavy-sandvich"] = CreateAbilityItem(
                "ability.heavy-sandvich",
                "Sandvich",
                GameplayEquipmentSlot.Secondary,
                BuiltInGameplayBehaviorIds.HeavySandvich,
                new GameplayItemPresentationDefinition(
                    HudSpriteName: "SandwichHudS",
                    BlueTeamHudFrameOffset: 1)),
            ["ability.sniper-scope"] = CreateAbilityItem("ability.sniper-scope", "Scope", GameplayEquipmentSlot.Secondary, BuiltInGameplayBehaviorIds.SniperScope),
            ["ability.medic-needlegun"] = CreateAbilityItem("ability.medic-needlegun", "Needlegun", GameplayEquipmentSlot.Secondary, BuiltInGameplayBehaviorIds.MedicNeedlegun),
            ["ability.medic-uber"] = CreateAbilityItem("ability.medic-uber", "UberCharge", GameplayEquipmentSlot.Utility, BuiltInGameplayBehaviorIds.MedicUber),
            ["ability.spy-cloak"] = CreateAbilityItem("ability.spy-cloak", "Invisibility Watch", GameplayEquipmentSlot.Secondary, BuiltInGameplayBehaviorIds.SpyCloak),
            ["ability.quote-blade-throw"] = CreateAbilityItem("ability.quote-blade-throw", "Blade Throw", GameplayEquipmentSlot.Secondary, BuiltInGameplayBehaviorIds.QuoteBladeThrow),
        };

        var classes = new Dictionary<string, GameplayClassDefinition>(StringComparer.Ordinal)
        {
            ["scout"] = CreateClass(
                "scout",
                "Scout",
                new GameplayClassMovementDefinition(100, -6f, -10f, 7f, 24f, 1.4f, LegacyMovementModel.DefaultJumpStrength, 1, 8),
                CreateLoadout("scout.stock", "Stock", "weapon.scattergun")),
            ["engineer"] = CreateClass(
                "engineer",
                "Engineer",
                new GameplayClassMovementDefinition(120, -6f, -10f, 7f, 24f, 1f, LegacyMovementModel.DefaultJumpStrength, 0, 12),
                CreateLoadout("engineer.stock", "Stock", "weapon.shotgun", secondaryItemId: "ability.engineer-pda")),
            ["pyro"] = CreateClass(
                "pyro",
                "Pyro",
                new GameplayClassMovementDefinition(120, -7f, -6f, 8f, 24f, 1.1f, LegacyMovementModel.DefaultJumpStrength, 0, 9),
                CreateLoadout("pyro.stock", "Stock", "weapon.flamethrower", secondaryItemId: "ability.pyro-airblast")),
            ["soldier"] = CreateClass(
                "soldier",
                "Soldier",
                new GameplayClassMovementDefinition(175, -6f, -8f, 7f, 24f, 0.9f, LegacyMovementModel.DefaultJumpStrength, 0, 15),
                CreateLoadout("soldier.stock", "Stock", "weapon.rocketlauncher")),
            ["demoman"] = CreateClass(
                "demoman",
                "Demoman",
                new GameplayClassMovementDefinition(120, -7f, -10f, 8f, 24f, 1f, LegacyMovementModel.DefaultJumpStrength, 0, 10),
                CreateLoadout("demoman.stock", "Stock", "weapon.minelauncher", secondaryItemId: "ability.demoman-detonate")),
            ["heavy"] = CreateClass(
                "heavy",
                "Heavy",
                new GameplayClassMovementDefinition(200, -9f, -12f, 10f, 24f, 0.8f, LegacyMovementModel.DefaultJumpStrength, 0, 11),
                CreateLoadout("heavy.stock", "Stock", "weapon.minigun", secondaryItemId: "ability.heavy-sandvich")),
            ["sniper"] = CreateClass(
                "sniper",
                "Sniper",
                new GameplayClassMovementDefinition(120, -6f, -8f, 7f, 24f, 0.9f, LegacyMovementModel.DefaultJumpStrength, 0, 12),
                CreateLoadout("sniper.stock", "Stock", "weapon.rifle", secondaryItemId: "ability.sniper-scope")),
            ["medic"] = CreateClass(
                "medic",
                "Medic",
                new GameplayClassMovementDefinition(120, -7f, -8f, 8f, 24f, 1.09f, LegacyMovementModel.DefaultJumpStrength, 0, 10),
                CreateLoadout("medic.stock", "Stock", "weapon.medigun", secondaryItemId: "ability.medic-needlegun", utilityItemId: "ability.medic-uber")),
            ["spy"] = CreateClass(
                "spy",
                "Spy",
                new GameplayClassMovementDefinition(100, -6f, -10f, 7f, 24f, 1.08f, LegacyMovementModel.DefaultJumpStrength, 0, 10),
                CreateLoadout("spy.stock", "Stock", "weapon.revolver", secondaryItemId: "ability.spy-cloak")),
            ["quote"] = CreateClass(
                "quote",
                "Quote",
                new GameplayClassMovementDefinition(140, -7f, -12f, 8f, 12f, 1.07f, LegacyMovementModel.DefaultJumpStrength, 0, 16),
                CreateLoadout("quote.stock", "Stock", "weapon.blade", secondaryItemId: "ability.quote-blade-throw")),
        };

        return new GameplayModPackDefinition(
            Id: "stock.gg2",
            DisplayName: "Stock OpenGarrison Gameplay",
            Version: new Version(1, 0, 0),
            Items: items,
            Classes: classes);
    }

    private static GameplayClassDefinition CreateClass(
        string id,
        string displayName,
        GameplayClassMovementDefinition movement,
        GameplayClassLoadoutDefinition defaultLoadout)
    {
        return new GameplayClassDefinition(
            Id: id,
            DisplayName: displayName,
            Movement: movement,
            Loadouts: new Dictionary<string, GameplayClassLoadoutDefinition>(StringComparer.Ordinal)
            {
                [defaultLoadout.Id] = defaultLoadout,
            },
            DefaultLoadoutId: defaultLoadout.Id);
    }

    private static GameplayClassLoadoutDefinition CreateLoadout(
        string id,
        string displayName,
        string primaryItemId,
        string? secondaryItemId = null,
        string? utilityItemId = null)
    {
        return new GameplayClassLoadoutDefinition(
            Id: id,
            DisplayName: displayName,
            PrimaryItemId: primaryItemId,
            SecondaryItemId: secondaryItemId,
            UtilityItemId: utilityItemId);
    }

    private static GameplayItemDefinition CreateAbilityItem(
        string id,
        string displayName,
        GameplayEquipmentSlot slot,
        string behaviorId,
        GameplayItemPresentationDefinition? presentation = null)
    {
        return new GameplayItemDefinition(
            Id: id,
            DisplayName: displayName,
            Slot: slot,
            BehaviorId: behaviorId,
            Ammo: new GameplayItemAmmoDefinition(),
            Presentation: presentation ?? new GameplayItemPresentationDefinition());
    }

    private static GameplayItemDefinition CreateWeaponItem(
        string id,
        string displayName,
        string behaviorId,
        int maxAmmo,
        int ammoPerUse,
        int projectilesPerUse,
        int useDelaySourceTicks,
        int reloadSourceTicks,
        float spreadDegrees = 0f,
        float minProjectileSpeed = 0f,
        float additionalProjectileSpeed = 0f,
        bool autoReloads = true,
        int ammoRegenPerTick = 0,
        bool refillsAllAtOnce = false,
        string? worldSpriteName = null,
        string? recoilSpriteName = null,
        string? reloadSpriteName = null,
        string? hudSpriteName = null,
        float weaponOffsetX = 0f,
        float weaponOffsetY = 0f,
        int recoilDurationSourceTicks = -1,
        int reloadDurationSourceTicks = -1,
        int scopedRecoilDurationSourceTicks = 0,
        bool loopRecoilWhileActive = false,
        int blueTeamHudFrameOffset = 1,
        bool useAmmoCountForHudFrame = false,
        int blueTeamAmmoHudFrameOffset = 0)
    {
        return new GameplayItemDefinition(
            Id: id,
            DisplayName: displayName,
            Slot: GameplayEquipmentSlot.Primary,
            BehaviorId: behaviorId,
            Ammo: new GameplayItemAmmoDefinition(
                MaxAmmo: maxAmmo,
                AmmoPerUse: ammoPerUse,
                ProjectilesPerUse: projectilesPerUse,
                UseDelaySourceTicks: useDelaySourceTicks,
                ReloadSourceTicks: reloadSourceTicks,
                SpreadDegrees: spreadDegrees,
                MinProjectileSpeed: minProjectileSpeed,
                AdditionalProjectileSpeed: additionalProjectileSpeed,
                AutoReloads: autoReloads,
                AmmoRegenPerTick: ammoRegenPerTick,
                RefillsAllAtOnce: refillsAllAtOnce),
            Presentation: new GameplayItemPresentationDefinition(
                WorldSpriteName: worldSpriteName,
                RecoilSpriteName: recoilSpriteName,
                ReloadSpriteName: reloadSpriteName,
                HudSpriteName: hudSpriteName,
                WeaponOffsetX: weaponOffsetX,
                WeaponOffsetY: weaponOffsetY,
                RecoilDurationSourceTicks: recoilDurationSourceTicks >= 0 ? recoilDurationSourceTicks : useDelaySourceTicks,
                ReloadDurationSourceTicks: reloadDurationSourceTicks >= 0 ? reloadDurationSourceTicks : reloadSourceTicks,
                ScopedRecoilDurationSourceTicks: scopedRecoilDurationSourceTicks,
                LoopRecoilWhileActive: loopRecoilWhileActive,
                BlueTeamHudFrameOffset: blueTeamHudFrameOffset,
                UseAmmoCountForHudFrame: useAmmoCountForHudFrame,
                BlueTeamAmmoHudFrameOffset: blueTeamAmmoHudFrameOffset));
    }
}
