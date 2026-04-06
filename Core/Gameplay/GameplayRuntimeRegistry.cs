using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed class GameplayRuntimeRegistry
{
    private readonly Dictionary<string, GameplayModPackDefinition> _modPacks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameplayItemDefinition> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameplayClassDefinition> _classes = new(StringComparer.Ordinal);
    private readonly Dictionary<PlayerClass, GameplayClassRuntimeBinding> _classBindings = new();
    private readonly Dictionary<string, GameplayPrimaryWeaponRuntimeBinding> _primaryWeaponBindingsByBehaviorId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameplaySecondaryAbilityRuntimeBinding> _secondaryAbilityBindingsByBehaviorId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameplayUtilityAbilityRuntimeBinding> _utilityAbilityBindingsByBehaviorId = new(StringComparer.Ordinal);

    public static GameplayRuntimeRegistry CreateStock()
    {
        var registry = new GameplayRuntimeRegistry();
        registry.RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.PelletGun, PrimaryWeaponKind.PelletGun, FireSoundName: "ShotgunSnd"));
        registry.RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Flamethrower, PrimaryWeaponKind.FlameThrower));
        registry.RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.RocketLauncher, PrimaryWeaponKind.RocketLauncher, FireSoundName: "RocketSnd"));
        registry.RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.MineLauncher, PrimaryWeaponKind.MineLauncher, FireSoundName: "MinegunSnd"));
        registry.RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Minigun, PrimaryWeaponKind.Minigun, FireSoundName: "ChaingunSnd"));
        registry.RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Rifle, PrimaryWeaponKind.Rifle, FireSoundName: "SniperSnd"));
        registry.RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Medigun, PrimaryWeaponKind.Medigun));
        registry.RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Revolver, PrimaryWeaponKind.Revolver, FireSoundName: "RevolverSnd"));
        registry.RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(BuiltInGameplayBehaviorIds.Blade, PrimaryWeaponKind.Blade));
        registry.RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.EngineerPda, GameplaySecondaryAbilityActionKind.EngineerPda));
        registry.RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.PyroAirblast, GameplaySecondaryAbilityActionKind.PyroAirblast));
        registry.RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.DemomanDetonate, GameplaySecondaryAbilityActionKind.DemomanDetonate, UsesHeldInput: true));
        registry.RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.HeavySandvich, GameplaySecondaryAbilityActionKind.HeavySandvich));
        registry.RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.SniperScope, GameplaySecondaryAbilityActionKind.SniperScope));
        registry.RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.MedicNeedlegun, GameplaySecondaryAbilityActionKind.MedicNeedlegun));
        registry.RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.SpyCloak, GameplaySecondaryAbilityActionKind.SpyCloak));
        registry.RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.QuoteBladeThrow, GameplaySecondaryAbilityActionKind.QuoteBladeThrow, UsesHeldInput: true));
        registry.RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.Flamethrower, GameplaySecondaryAbilityActionKind.PyroAirblast));
        registry.RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.MineLauncher, GameplaySecondaryAbilityActionKind.DemomanDetonate, UsesHeldInput: true));
        registry.RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.Rifle, GameplaySecondaryAbilityActionKind.SniperScope));
        registry.RegisterSecondaryAbilityBehavior(new GameplaySecondaryAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.Medigun, GameplaySecondaryAbilityActionKind.MedicNeedlegun));
        registry.RegisterUtilityAbilityBehavior(new GameplayUtilityAbilityRuntimeBinding(BuiltInGameplayBehaviorIds.MedicUber, GameplayUtilityAbilityActionKind.MedicUber));

        registry.RegisterModPack(
            StockGameplayModCatalog.Definition,
            [
                new GameplayClassRuntimeBinding(PlayerClass.Scout, StockGameplayModCatalog.Definition.Id, "scout", SupportsExperimentalAcquiredWeapon: true, "ScatterKL"),
                new GameplayClassRuntimeBinding(PlayerClass.Engineer, StockGameplayModCatalog.Definition.Id, "engineer", SupportsExperimentalAcquiredWeapon: true, "ShotgunKL"),
                new GameplayClassRuntimeBinding(PlayerClass.Pyro, StockGameplayModCatalog.Definition.Id, "pyro", SupportsExperimentalAcquiredWeapon: true, "FlameKL"),
                new GameplayClassRuntimeBinding(PlayerClass.Soldier, StockGameplayModCatalog.Definition.Id, "soldier", SupportsExperimentalAcquiredWeapon: true, "RocketKL"),
                new GameplayClassRuntimeBinding(PlayerClass.Demoman, StockGameplayModCatalog.Definition.Id, "demoman", SupportsExperimentalAcquiredWeapon: true, "MineKL"),
                new GameplayClassRuntimeBinding(PlayerClass.Heavy, StockGameplayModCatalog.Definition.Id, "heavy", SupportsExperimentalAcquiredWeapon: true, "MinigunKL"),
                new GameplayClassRuntimeBinding(PlayerClass.Sniper, StockGameplayModCatalog.Definition.Id, "sniper", SupportsExperimentalAcquiredWeapon: true, "RifleKL"),
                new GameplayClassRuntimeBinding(PlayerClass.Medic, StockGameplayModCatalog.Definition.Id, "medic", SupportsExperimentalAcquiredWeapon: true, "NeedleKL"),
                new GameplayClassRuntimeBinding(PlayerClass.Spy, StockGameplayModCatalog.Definition.Id, "spy", SupportsExperimentalAcquiredWeapon: true, "RevolverKL"),
                new GameplayClassRuntimeBinding(PlayerClass.Quote, StockGameplayModCatalog.Definition.Id, "quote", SupportsExperimentalAcquiredWeapon: false, "BladeKL"),
            ]);

        return registry;
    }

    public IReadOnlyCollection<GameplayModPackDefinition> ModPacks => _modPacks.Values;

    public void RegisterPrimaryWeaponBehavior(string behaviorId, PrimaryWeaponKind weaponKind)
    {
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(behaviorId, weaponKind));
    }

    public void RegisterPrimaryWeaponBehavior(GameplayPrimaryWeaponRuntimeBinding binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.BehaviorId);
        _primaryWeaponBindingsByBehaviorId[binding.BehaviorId] = binding;
    }

    public void RegisterSecondaryAbilityBehavior(GameplaySecondaryAbilityRuntimeBinding binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.BehaviorId);
        _secondaryAbilityBindingsByBehaviorId[binding.BehaviorId] = binding;
    }

    public void RegisterUtilityAbilityBehavior(GameplayUtilityAbilityRuntimeBinding binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.BehaviorId);
        _utilityAbilityBindingsByBehaviorId[binding.BehaviorId] = binding;
    }

    public void RegisterModPack(GameplayModPackDefinition modPack, IReadOnlyList<GameplayClassRuntimeBinding> classBindings)
    {
        ArgumentNullException.ThrowIfNull(modPack);
        ArgumentNullException.ThrowIfNull(classBindings);
        _modPacks[modPack.Id] = modPack;

        foreach (var item in modPack.Items)
        {
            _items[item.Key] = item.Value;
        }

        foreach (var gameplayClass in modPack.Classes)
        {
            _classes[gameplayClass.Key] = gameplayClass.Value;
        }

        for (var index = 0; index < classBindings.Count; index += 1)
        {
            _classBindings[classBindings[index].PlayerClass] = classBindings[index];
        }
    }

    public GameplayModPackDefinition GetRequiredModPack(string modPackId)
    {
        if (_modPacks.TryGetValue(modPackId, out var modPack))
        {
            return modPack;
        }

        throw new KeyNotFoundException($"Gameplay mod pack \"{modPackId}\" is not registered.");
    }

    public GameplayClassDefinition GetRequiredClass(string classId)
    {
        if (_classes.TryGetValue(classId, out var gameplayClass))
        {
            return gameplayClass;
        }

        throw new KeyNotFoundException($"Gameplay class \"{classId}\" is not registered.");
    }

    public GameplayItemDefinition GetRequiredItem(string itemId)
    {
        if (_items.TryGetValue(itemId, out var item))
        {
            return item;
        }

        throw new KeyNotFoundException($"Gameplay item \"{itemId}\" is not registered.");
    }

    public GameplayClassRuntimeBinding GetRequiredClassBinding(PlayerClass playerClass)
    {
        if (_classBindings.TryGetValue(playerClass, out var binding))
        {
            return binding;
        }

        throw new KeyNotFoundException($"No gameplay runtime binding registered for player class \"{playerClass}\".");
    }

    public GameplayClassDefinition GetClassDefinition(PlayerClass playerClass)
    {
        var binding = GetRequiredClassBinding(playerClass);
        return GetRequiredClass(binding.ClassId);
    }

    public GameplayPlayerLoadoutState CreatePlayerLoadoutState(
        PlayerClass playerClass,
        string? loadoutId = null,
        GameplayEquipmentSlot equippedSlot = GameplayEquipmentSlot.Primary,
        string? secondaryItemOverrideId = null,
        string? acquiredItemId = null)
    {
        if (TryCreateValidatedPlayerLoadoutState(playerClass, loadoutId, equippedSlot, secondaryItemOverrideId, acquiredItemId, out var loadoutState))
        {
            return loadoutState;
        }

        return CreateFallbackPlayerLoadoutState(playerClass);
    }

    public bool TryCreateValidatedPlayerLoadoutState(
        PlayerClass playerClass,
        string? loadoutId,
        GameplayEquipmentSlot equippedSlot,
        string? secondaryItemOverrideId,
        string? acquiredItemId,
        out GameplayPlayerLoadoutState loadoutState)
    {
        var binding = GetRequiredClassBinding(playerClass);
        var loadout = ResolveValidatedLoadout(playerClass, loadoutId);
        var primaryItemId = loadout.PrimaryItemId;
        var secondaryItemId = ResolveValidatedSecondaryItemId(playerClass, loadout, secondaryItemOverrideId);
        var utilityItemId = loadout.UtilityItemId;
        var validatedAcquiredItemId = ResolveValidatedAcquiredItemId(playerClass, acquiredItemId);
        var validatedEquippedSlot = ResolveValidatedEquippedSlot(
            equippedSlot,
            primaryItemId,
            secondaryItemId,
            utilityItemId,
            validatedAcquiredItemId);

        var equippedItemId = validatedEquippedSlot switch
        {
            GameplayEquipmentSlot.Secondary => validatedAcquiredItemId ?? secondaryItemId ?? primaryItemId,
            GameplayEquipmentSlot.Utility => utilityItemId ?? primaryItemId,
            _ => primaryItemId,
        };

        loadoutState = new GameplayPlayerLoadoutState(
            ModPackId: binding.ModPackId,
            ClassId: binding.ClassId,
            LoadoutId: loadout.Id,
            PrimaryItemId: primaryItemId,
            SecondaryItemId: secondaryItemId,
            UtilityItemId: utilityItemId,
            EquippedSlot: validatedEquippedSlot,
            EquippedItemId: equippedItemId,
            AcquiredItemId: validatedAcquiredItemId);
        return true;
    }

    public GameplayClassLoadoutDefinition GetDefaultLoadout(PlayerClass playerClass)
    {
        var gameplayClass = GetClassDefinition(playerClass);
        return gameplayClass.Loadouts[gameplayClass.DefaultLoadoutId];
    }

    public bool TryGetLoadout(PlayerClass playerClass, string? loadoutId, out GameplayClassLoadoutDefinition loadout)
    {
        var gameplayClass = GetClassDefinition(playerClass);
        if (!string.IsNullOrWhiteSpace(loadoutId)
            && gameplayClass.Loadouts.TryGetValue(loadoutId, out var resolvedLoadout))
        {
            loadout = resolvedLoadout;
            return true;
        }

        loadout = null!;
        return false;
    }

    public GameplayClassLoadoutDefinition GetRequiredLoadout(PlayerClass playerClass, string loadoutId)
    {
        if (TryGetLoadout(playerClass, loadoutId, out var loadout))
        {
            return loadout;
        }

        throw new KeyNotFoundException($"Gameplay loadout \"{loadoutId}\" is not registered for player class \"{playerClass}\".");
    }

    public bool CanUseLoadout(PlayerClass playerClass, string? loadoutId)
    {
        return string.IsNullOrWhiteSpace(loadoutId)
            || TryGetLoadout(playerClass, loadoutId, out _);
    }

    public GameplayItemDefinition GetPrimaryItem(PlayerClass playerClass)
    {
        return GetRequiredItem(GetDefaultLoadout(playerClass).PrimaryItemId);
    }

    public GameplayItemDefinition? GetSecondaryItem(PlayerClass playerClass)
    {
        var itemId = GetDefaultLoadout(playerClass).SecondaryItemId;
        return itemId is null ? null : GetRequiredItem(itemId);
    }

    public GameplayItemDefinition? GetUtilityItem(PlayerClass playerClass)
    {
        var itemId = GetDefaultLoadout(playerClass).UtilityItemId;
        return itemId is null ? null : GetRequiredItem(itemId);
    }

    public bool SupportsExperimentalAcquiredWeapon(PlayerClass playerClass)
    {
        return GetRequiredClassBinding(playerClass).SupportsExperimentalAcquiredWeapon;
    }

    public string GetPrimaryWeaponKillFeedSprite(PlayerClass playerClass)
    {
        return GetRequiredClassBinding(playerClass).PrimaryWeaponKillFeedSprite;
    }

    public PrimaryWeaponDefinition CreatePrimaryWeaponDefinition(GameplayItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new PrimaryWeaponDefinition(
            DisplayName: item.DisplayName,
            Kind: ResolvePrimaryWeaponKind(item.BehaviorId),
            MaxAmmo: item.Ammo.MaxAmmo,
            AmmoPerShot: item.Ammo.AmmoPerUse,
            ProjectilesPerShot: item.Ammo.ProjectilesPerUse,
            ReloadDelayTicks: item.Ammo.UseDelaySourceTicks,
            AmmoReloadTicks: item.Ammo.ReloadSourceTicks,
            SpreadDegrees: item.Ammo.SpreadDegrees,
            MinShotSpeed: item.Ammo.MinProjectileSpeed,
            AdditionalRandomShotSpeed: item.Ammo.AdditionalProjectileSpeed,
            AutoReloads: item.Ammo.AutoReloads,
            AmmoRegenPerTick: item.Ammo.AmmoRegenPerTick,
            RefillsAllAtOnce: item.Ammo.RefillsAllAtOnce);
    }

    public bool TryGetPrimaryWeaponBinding(string? behaviorId, out GameplayPrimaryWeaponRuntimeBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(behaviorId)
            && _primaryWeaponBindingsByBehaviorId.TryGetValue(behaviorId, out binding))
        {
            return true;
        }

        binding = default;
        return false;
    }

    public GameplayPrimaryWeaponRuntimeBinding GetRequiredPrimaryWeaponBinding(string behaviorId)
    {
        if (TryGetPrimaryWeaponBinding(behaviorId, out var binding))
        {
            return binding;
        }

        throw new InvalidOperationException($"Gameplay behavior id \"{behaviorId}\" is not registered as a primary weapon behavior.");
    }

    public bool TryGetSecondaryAbilityBinding(string? behaviorId, out GameplaySecondaryAbilityRuntimeBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(behaviorId)
            && _secondaryAbilityBindingsByBehaviorId.TryGetValue(behaviorId, out binding))
        {
            return true;
        }

        binding = default;
        return false;
    }

    public bool TryGetUtilityAbilityBinding(string? behaviorId, out GameplayUtilityAbilityRuntimeBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(behaviorId)
            && _utilityAbilityBindingsByBehaviorId.TryGetValue(behaviorId, out binding))
        {
            return true;
        }

        binding = default;
        return false;
    }

    public CharacterClassDefinition CreateCharacterClassDefinition(PlayerClass playerClass)
    {
        var gameplayClass = GetClassDefinition(playerClass);
        var movement = gameplayClass.Movement;
        var primaryWeapon = CreatePrimaryWeaponDefinition(GetPrimaryItem(playerClass));
        var width = movement.CollisionRight - movement.CollisionLeft;
        var height = movement.CollisionBottom - movement.CollisionTop;

        return new CharacterClassDefinition(
            Id: playerClass,
            DisplayName: gameplayClass.DisplayName,
            PrimaryWeapon: primaryWeapon,
            MaxHealth: movement.MaxHealth,
            Width: width,
            Height: height,
            CollisionLeft: movement.CollisionLeft,
            CollisionTop: movement.CollisionTop,
            CollisionRight: movement.CollisionRight,
            CollisionBottom: movement.CollisionBottom,
            RunPower: movement.RunPower,
            JumpStrength: movement.JumpStrength,
            MaxRunSpeed: LegacyMovementModel.GetMaxRunSpeed(movement.RunPower),
            GroundAcceleration: LegacyMovementModel.GetContinuousRunDrive(movement.RunPower),
            GroundDeceleration: LegacyMovementModel.GetContinuousRunDrive(movement.RunPower),
            Gravity: LegacyMovementModel.GetGravityPerSecondSquared(),
            JumpSpeed: LegacyMovementModel.GetJumpSpeed(movement.JumpStrength),
            MaxAirJumps: movement.MaxAirJumps,
            TauntLengthFrames: movement.TauntLengthFrames);
    }

    private PrimaryWeaponKind ResolvePrimaryWeaponKind(string behaviorId)
    {
        if (_primaryWeaponBindingsByBehaviorId.TryGetValue(behaviorId, out var binding))
        {
            return binding.WeaponKind;
        }

        throw new InvalidOperationException($"Gameplay behavior id \"{behaviorId}\" is not registered as a primary weapon behavior.");
    }

    public bool TryResolvePrimaryWeaponItemId(PrimaryWeaponDefinition weaponDefinition, out string itemId)
    {
        ArgumentNullException.ThrowIfNull(weaponDefinition);

        foreach (var item in _items.Values)
        {
            if (item.Slot != GameplayEquipmentSlot.Primary)
            {
                continue;
            }

            if (CreatePrimaryWeaponDefinition(item) != weaponDefinition)
            {
                continue;
            }

            itemId = item.Id;
            return true;
        }

        itemId = string.Empty;
        return false;
    }

    public bool CanUseSecondaryOverrideItem(PlayerClass playerClass, string? secondaryItemId)
    {
        return CanUseSecondaryOverrideItem(playerClass, GetDefaultLoadout(playerClass), secondaryItemId);
    }

    public bool CanUseSecondaryOverrideItem(PlayerClass playerClass, string? loadoutId, string? secondaryItemId)
    {
        return CanUseSecondaryOverrideItem(playerClass, ResolveValidatedLoadout(playerClass, loadoutId), secondaryItemId);
    }

    private bool CanUseSecondaryOverrideItem(PlayerClass playerClass, GameplayClassLoadoutDefinition loadout, string? secondaryItemId)
    {
        if (string.IsNullOrWhiteSpace(secondaryItemId))
        {
            return true;
        }

        var defaultSecondaryItemId = loadout.SecondaryItemId;
        if (string.Equals(defaultSecondaryItemId, secondaryItemId, StringComparison.Ordinal))
        {
            return true;
        }

        return SupportsExperimentalAcquiredWeapon(playerClass)
            && TryGetItem(secondaryItemId, out var secondaryItem)
            && secondaryItem.Slot == GameplayEquipmentSlot.Primary;
    }

    public bool CanUseAcquiredItem(PlayerClass playerClass, string? acquiredItemId)
    {
        return string.IsNullOrWhiteSpace(acquiredItemId)
            || (SupportsExperimentalAcquiredWeapon(playerClass)
                && TryGetItem(acquiredItemId, out var acquiredItem)
                && acquiredItem.Slot == GameplayEquipmentSlot.Primary);
    }

    public bool CanEquipSlot(
        PlayerClass playerClass,
        string? loadoutId,
        GameplayEquipmentSlot equippedSlot,
        string? secondaryItemOverrideId = null,
        string? acquiredItemId = null)
    {
        var loadout = ResolveValidatedLoadout(playerClass, loadoutId);
        var secondaryItemId = ResolveValidatedSecondaryItemId(playerClass, loadout, secondaryItemOverrideId);
        var utilityItemId = loadout.UtilityItemId;
        var validatedAcquiredItemId = ResolveValidatedAcquiredItemId(playerClass, acquiredItemId);
        return ResolveValidatedEquippedSlot(
            equippedSlot,
            loadout.PrimaryItemId,
            secondaryItemId,
            utilityItemId,
            validatedAcquiredItemId) == equippedSlot;
    }

    private bool TryGetItem(string? itemId, out GameplayItemDefinition item)
    {
        if (!string.IsNullOrWhiteSpace(itemId)
            && _items.TryGetValue(itemId, out var resolvedItem))
        {
            item = resolvedItem;
            return true;
        }

        item = null!;
        return false;
    }

    private GameplayPlayerLoadoutState CreateFallbackPlayerLoadoutState(PlayerClass playerClass)
    {
        var binding = GetRequiredClassBinding(playerClass);
        var loadout = GetDefaultLoadout(playerClass);
        return new GameplayPlayerLoadoutState(
            ModPackId: binding.ModPackId,
            ClassId: binding.ClassId,
            LoadoutId: loadout.Id,
            PrimaryItemId: loadout.PrimaryItemId,
            SecondaryItemId: loadout.SecondaryItemId,
            UtilityItemId: loadout.UtilityItemId,
            EquippedSlot: GameplayEquipmentSlot.Primary,
            EquippedItemId: loadout.PrimaryItemId,
            AcquiredItemId: null);
    }

    private GameplayClassLoadoutDefinition ResolveValidatedLoadout(PlayerClass playerClass, string? loadoutId)
    {
        return TryGetLoadout(playerClass, loadoutId, out var loadout)
            ? loadout
            : GetDefaultLoadout(playerClass);
    }

    private string? ResolveValidatedSecondaryItemId(PlayerClass playerClass, GameplayClassLoadoutDefinition loadout, string? secondaryItemOverrideId)
    {
        return CanUseSecondaryOverrideItem(playerClass, loadout, secondaryItemOverrideId)
            ? (string.IsNullOrWhiteSpace(secondaryItemOverrideId) ? loadout.SecondaryItemId : secondaryItemOverrideId)
            : loadout.SecondaryItemId;
    }

    private string? ResolveValidatedAcquiredItemId(PlayerClass playerClass, string? acquiredItemId)
    {
        return CanUseAcquiredItem(playerClass, acquiredItemId) && !string.IsNullOrWhiteSpace(acquiredItemId)
            ? acquiredItemId
            : null;
    }

    private static GameplayEquipmentSlot ResolveValidatedEquippedSlot(
        GameplayEquipmentSlot requestedSlot,
        string primaryItemId,
        string? secondaryItemId,
        string? utilityItemId,
        string? acquiredItemId)
    {
        return requestedSlot switch
        {
            GameplayEquipmentSlot.Secondary when !string.IsNullOrWhiteSpace(acquiredItemId) || !string.IsNullOrWhiteSpace(secondaryItemId)
                => GameplayEquipmentSlot.Secondary,
            GameplayEquipmentSlot.Utility when !string.IsNullOrWhiteSpace(utilityItemId)
                => GameplayEquipmentSlot.Utility,
            _ => GameplayEquipmentSlot.Primary,
        };
    }
}

public sealed record GameplayClassRuntimeBinding(
    PlayerClass PlayerClass,
    string ModPackId,
    string ClassId,
    bool SupportsExperimentalAcquiredWeapon,
    string PrimaryWeaponKillFeedSprite);

public readonly record struct GameplayPrimaryWeaponRuntimeBinding(
    string BehaviorId,
    PrimaryWeaponKind WeaponKind,
    string? FireSoundName = null);

public enum GameplaySecondaryAbilityActionKind
{
    None = 0,
    EngineerPda = 1,
    PyroAirblast = 2,
    DemomanDetonate = 3,
    HeavySandvich = 4,
    SniperScope = 5,
    MedicNeedlegun = 6,
    SpyCloak = 7,
    QuoteBladeThrow = 8,
}

public readonly record struct GameplaySecondaryAbilityRuntimeBinding(
    string BehaviorId,
    GameplaySecondaryAbilityActionKind ActionKind,
    bool UsesHeldInput = false);

public enum GameplayUtilityAbilityActionKind
{
    None = 0,
    MedicUber = 1,
}

public readonly record struct GameplayUtilityAbilityRuntimeBinding(
    string BehaviorId,
    GameplayUtilityAbilityActionKind ActionKind);
