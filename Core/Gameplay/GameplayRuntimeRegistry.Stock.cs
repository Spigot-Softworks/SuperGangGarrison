using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class GameplayRuntimeRegistry
{
    public static GameplayRuntimeRegistry CreateStock()
    {
        if (OperatingSystem.IsBrowser() && BrowserGameplayModCatalog.HasDefinitions)
        {
            return CreateStock(BrowserGameplayModCatalog.GetDefinitions());
        }

        return CreateStock(GameplayModPackDirectoryLoader.LoadAllFromContentRoot());
    }

    public static GameplayRuntimeRegistry CreateStock(IEnumerable<GameplayModPackDefinition> discoveredModPacks)
    {
        var registry = new GameplayRuntimeRegistry();
        registry.RegisterBuiltInBehaviorBindings();

        var stockModPack = discoveredModPacks.FirstOrDefault(static pack =>
            string.Equals(pack.Id, StockGameplayModCatalog.Definition.Id, StringComparison.Ordinal))
            ?? StockGameplayModCatalog.Definition;
        registry.RegisterModPack(stockModPack, CreateClassBindingsFromRuntimeMetadata(stockModPack));

        foreach (var modPack in discoveredModPacks)
        {
            if (string.Equals(modPack.Id, stockModPack.Id, StringComparison.Ordinal))
            {
                continue;
            }

            registry.RegisterModPack(modPack, CreateClassBindingsFromRuntimeMetadata(modPack));
        }

        return registry;
    }

    private void RegisterBuiltInBehaviorBindings()
    {
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.PelletGun, PrimaryWeaponKind.PelletGun));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Flamethrower, PrimaryWeaponKind.FlameThrower));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.RocketLauncher, PrimaryWeaponKind.RocketLauncher));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.MineLauncher, PrimaryWeaponKind.MineLauncher));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.GrenadeLauncher, PrimaryWeaponKind.GrenadeLauncher));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Minigun, PrimaryWeaponKind.Minigun));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Rifle, PrimaryWeaponKind.Rifle));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Medigun, PrimaryWeaponKind.Medigun));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.MedigunCrit, PrimaryWeaponKind.Medigun));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Revolver, PrimaryWeaponKind.Revolver));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Blade, PrimaryWeaponKind.Blade));
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(
            BuiltInGameplayBehaviorIds.ScoutNailgun,
            PrimaryWeaponKind.Custom,
            Executor: new DelegateGameplayPrimaryWeaponExecutor(static context =>
                context.World.ExecuteScoutNailgunPrimaryWeapon(context))));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.EngineerPda, static context => context.World.ExecuteEngineerPdaAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.PyroAirblast, static context => context.World.ExecutePyroAirblastAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.DemomanDetonate, static context => context.World.ExecuteDemomanDetonateAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.HeavySandvich, static context => context.World.ExecuteHeavySandvichAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.SniperScope, static context => context.World.ExecuteSniperScopeAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.SniperBinoculars, static context => context.World.ExecuteSniperBinocularsAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.MedicNeedlegun, static context => context.World.ExecuteMedicNeedlegunAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.MedicKritzBeam, static context => context.World.ExecuteMedicKritzBeamAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.MedicKritzHealNeedles, static context => context.World.ExecuteMedicKritzHealNeedlesAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.MedicUber, static context => context.World.ExecuteMedicUberAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.SpyCloak, static context => context.World.ExecuteSpyCloakAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.SpySuperjump, static context => context.World.ExecuteSpySuperjumpAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.QuoteBladeThrow, static context => context.World.ExecuteQuoteBladeThrowAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.CivvieUmbrella, static context => context.World.ExecuteCivvieUmbrellaAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.CivvieTaunt, static context => context.World.ExecuteCivvieTauntAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.CivviePogo, static context => context.World.ExecuteCivviePogoAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.ScoutTaunt, static context => context.World.ExecuteScoutTauntAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.ScoutNailgunToggle, static context => context.World.ExecuteScoutNailgunToggleAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.SoldierSecondaryToggle, static context => context.World.ExecuteSoldierSecondaryToggleAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.EngineerJumpPad, static context => context.World.ExecuteEngineerJumpPadAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.HeavyGhostDash, static context => context.World.ExecuteHeavyGhostDashAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.ExperimentalSoldierSecondary, static context => context.World.ExecuteExperimentalSoldierSecondaryAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.ExperimentalLtdPassive, static context => context.World.ExecuteExperimentalLtdPassiveAbility(context));
        RegisterGameplayAbilityExecutor(BuiltInGameplayBehaviorIds.ExperimentalLtdRage, static context => context.World.ExecuteExperimentalLtdRageAbility(context));
    }

    private static GameplayClassRuntimeBinding[] CreateClassBindingsFromRuntimeMetadata(GameplayModPackDefinition modPack)
    {
        ArgumentNullException.ThrowIfNull(modPack);
        var bindings = new List<GameplayClassRuntimeBinding>();
        foreach (var gameplayClass in modPack.Classes.Values)
        {
            var runtime = gameplayClass.Runtime;
            if (runtime is null)
            {
                continue;
            }

            var bindsLegacyPlayerClass = !string.IsNullOrWhiteSpace(runtime.PlayerClass);
            var basePlayerClassName = string.IsNullOrWhiteSpace(runtime.BasePlayerClass)
                ? (bindsLegacyPlayerClass ? runtime.PlayerClass : nameof(PlayerClass.Scout))
                : runtime.BasePlayerClass;
            var botGraphPlayerClassName = string.IsNullOrWhiteSpace(runtime.BotGraphPlayerClass)
                ? basePlayerClassName
                : runtime.BotGraphPlayerClass;
            if (!Enum.TryParse<PlayerClass>(basePlayerClassName, ignoreCase: true, out var basePlayerClass))
            {
                throw new InvalidOperationException($"Gameplay class \"{gameplayClass.Id}\" in mod pack \"{modPack.Id}\" declares unsupported base player class \"{basePlayerClassName}\".");
            }

            if (!Enum.TryParse<PlayerClass>(botGraphPlayerClassName, ignoreCase: true, out var botGraphPlayerClass))
            {
                throw new InvalidOperationException($"Gameplay class \"{gameplayClass.Id}\" in mod pack \"{modPack.Id}\" declares unsupported bot graph player class \"{botGraphPlayerClassName}\".");
            }

            var playerClass = basePlayerClass;
            if (bindsLegacyPlayerClass && !Enum.TryParse<PlayerClass>(runtime.PlayerClass, ignoreCase: true, out playerClass))
            {
                throw new InvalidOperationException($"Gameplay class \"{gameplayClass.Id}\" in mod pack \"{modPack.Id}\" declares unsupported player class slot \"{runtime.PlayerClass}\".");
            }

            bindings.Add(new GameplayClassRuntimeBinding(
                playerClass,
                modPack.Id,
                gameplayClass.Id,
                runtime.SupportsExperimentalAcquiredWeapon,
                runtime.PrimaryWeaponKillFeedSprite,
                basePlayerClass,
                botGraphPlayerClass,
                bindsLegacyPlayerClass));
        }

        return bindings.ToArray();
    }
}
