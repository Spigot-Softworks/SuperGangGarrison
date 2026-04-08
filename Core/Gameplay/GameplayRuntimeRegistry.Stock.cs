using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class GameplayRuntimeRegistry
{
    public static GameplayRuntimeRegistry CreateStock()
    {
        return CreateStock(GameplayModPackDirectoryLoader.LoadAllFromContentRoot());
    }

    public static GameplayRuntimeRegistry CreateStock(IEnumerable<GameplayModPackDefinition> discoveredModPacks)
    {
        var registry = new GameplayRuntimeRegistry();
        registry.RegisterBuiltInBehaviorBindings();

        var stockModPack = discoveredModPacks.FirstOrDefault(static pack =>
            string.Equals(pack.Id, StockGameplayModCatalog.Definition.Id, StringComparison.Ordinal))
            ?? StockGameplayModCatalog.Definition;
        registry.RegisterModPack(stockModPack, CreateStockClassBindings(stockModPack.Id));

        foreach (var modPack in discoveredModPacks)
        {
            if (string.Equals(modPack.Id, stockModPack.Id, StringComparison.Ordinal))
            {
                continue;
            }

            registry.RegisterModPack(modPack, []);
        }

        return registry;
    }

    private void RegisterBuiltInBehaviorBindings()
    {
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.PelletGun, PrimaryWeaponKind.PelletGun, FireSoundName: "ShotgunSnd"));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Flamethrower, PrimaryWeaponKind.FlameThrower));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.RocketLauncher, PrimaryWeaponKind.RocketLauncher, FireSoundName: "RocketSnd"));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.MineLauncher, PrimaryWeaponKind.MineLauncher, FireSoundName: "MinegunSnd"));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Minigun, PrimaryWeaponKind.Minigun, FireSoundName: "ChaingunSnd"));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Rifle, PrimaryWeaponKind.Rifle, FireSoundName: "SniperSnd"));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Medigun, PrimaryWeaponKind.Medigun));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Revolver, PrimaryWeaponKind.Revolver, FireSoundName: "RevolverSnd"));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Blade, PrimaryWeaponKind.Blade));
        RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.EngineerPda, GameplaySecondaryAbilityActionKind.EngineerPda));
        RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.PyroAirblast, GameplaySecondaryAbilityActionKind.PyroAirblast));
        RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.DemomanDetonate, GameplaySecondaryAbilityActionKind.DemomanDetonate, UsesHeldInput: true));
        RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.HeavySandvich, GameplaySecondaryAbilityActionKind.HeavySandvich));
        RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.SniperScope, GameplaySecondaryAbilityActionKind.SniperScope));
        RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.MedicNeedlegun, GameplaySecondaryAbilityActionKind.MedicNeedlegun));
        RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.SpyCloak, GameplaySecondaryAbilityActionKind.SpyCloak));
        RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.QuoteBladeThrow, GameplaySecondaryAbilityActionKind.QuoteBladeThrow, UsesHeldInput: true));
        RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.Flamethrower, GameplaySecondaryAbilityActionKind.PyroAirblast));
        RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.MineLauncher, GameplaySecondaryAbilityActionKind.DemomanDetonate, UsesHeldInput: true));
        RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.Rifle, GameplaySecondaryAbilityActionKind.SniperScope));
        RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.Medigun, GameplaySecondaryAbilityActionKind.MedicNeedlegun));
        RegisterUtilityAbilityBehavior(new GameplayUtilityAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.MedicUber, GameplayUtilityAbilityActionKind.MedicUber));
    }

    private static GameplayClassRuntimeBinding[] CreateStockClassBindings(string modPackId)
    {
        return
        [
            new GameplayClassRuntimeBinding(PlayerClass.Scout, modPackId, "scout", SupportsExperimentalAcquiredWeapon: true, "ScatterKL"),
            new GameplayClassRuntimeBinding(PlayerClass.Engineer, modPackId, "engineer", SupportsExperimentalAcquiredWeapon: true, "ShotgunKL"),
            new GameplayClassRuntimeBinding(PlayerClass.Pyro, modPackId, "pyro", SupportsExperimentalAcquiredWeapon: true, "FlameKL"),
            new GameplayClassRuntimeBinding(PlayerClass.Soldier, modPackId, "soldier", SupportsExperimentalAcquiredWeapon: true, "RocketKL"),
            new GameplayClassRuntimeBinding(PlayerClass.Demoman, modPackId, "demoman", SupportsExperimentalAcquiredWeapon: true, "MineKL"),
            new GameplayClassRuntimeBinding(PlayerClass.Heavy, modPackId, "heavy", SupportsExperimentalAcquiredWeapon: true, "MinigunKL"),
            new GameplayClassRuntimeBinding(PlayerClass.Sniper, modPackId, "sniper", SupportsExperimentalAcquiredWeapon: true, "RifleKL"),
            new GameplayClassRuntimeBinding(PlayerClass.Medic, modPackId, "medic", SupportsExperimentalAcquiredWeapon: true, "NeedleKL"),
            new GameplayClassRuntimeBinding(PlayerClass.Spy, modPackId, "spy", SupportsExperimentalAcquiredWeapon: true, "RevolverKL"),
            new GameplayClassRuntimeBinding(PlayerClass.Quote, modPackId, "quote", SupportsExperimentalAcquiredWeapon: false, "BladeKL"),
        ];
    }
}
