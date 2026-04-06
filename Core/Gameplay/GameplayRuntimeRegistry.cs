using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed class GameplayRuntimeRegistry
{
    private readonly Dictionary<string, GameplayModPackDefinition> _modPacks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameplayItemDefinition> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameplayClassDefinition> _classes = new(StringComparer.Ordinal);
    private readonly Dictionary<PlayerClass, GameplayClassRuntimeBinding> _classBindings = new();
    private readonly Dictionary<string, PrimaryWeaponKind> _primaryWeaponKindsByBehaviorId = new(StringComparer.Ordinal);

    public static GameplayRuntimeRegistry CreateStock()
    {
        var registry = new GameplayRuntimeRegistry();
        registry.RegisterPrimaryWeaponBehavior(BuiltInGameplayBehaviorIds.PelletGun, PrimaryWeaponKind.PelletGun);
        registry.RegisterPrimaryWeaponBehavior(BuiltInGameplayBehaviorIds.Flamethrower, PrimaryWeaponKind.FlameThrower);
        registry.RegisterPrimaryWeaponBehavior(BuiltInGameplayBehaviorIds.RocketLauncher, PrimaryWeaponKind.RocketLauncher);
        registry.RegisterPrimaryWeaponBehavior(BuiltInGameplayBehaviorIds.MineLauncher, PrimaryWeaponKind.MineLauncher);
        registry.RegisterPrimaryWeaponBehavior(BuiltInGameplayBehaviorIds.Minigun, PrimaryWeaponKind.Minigun);
        registry.RegisterPrimaryWeaponBehavior(BuiltInGameplayBehaviorIds.Rifle, PrimaryWeaponKind.Rifle);
        registry.RegisterPrimaryWeaponBehavior(BuiltInGameplayBehaviorIds.Medigun, PrimaryWeaponKind.Medigun);
        registry.RegisterPrimaryWeaponBehavior(BuiltInGameplayBehaviorIds.Revolver, PrimaryWeaponKind.Revolver);
        registry.RegisterPrimaryWeaponBehavior(BuiltInGameplayBehaviorIds.Blade, PrimaryWeaponKind.Blade);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(behaviorId);
        _primaryWeaponKindsByBehaviorId[behaviorId] = weaponKind;
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
        GameplayEquipmentSlot equippedSlot = GameplayEquipmentSlot.Primary,
        string? secondaryItemOverrideId = null,
        string? acquiredItemId = null)
    {
        var binding = GetRequiredClassBinding(playerClass);
        var loadout = GetDefaultLoadout(playerClass);
        var primaryItemId = loadout.PrimaryItemId;
        var secondaryItemId = secondaryItemOverrideId ?? loadout.SecondaryItemId;
        var utilityItemId = loadout.UtilityItemId;

        var equippedItemId = equippedSlot switch
        {
            GameplayEquipmentSlot.Secondary => acquiredItemId ?? secondaryItemId ?? primaryItemId,
            GameplayEquipmentSlot.Utility => utilityItemId ?? primaryItemId,
            _ => primaryItemId,
        };

        return new GameplayPlayerLoadoutState(
            ModPackId: binding.ModPackId,
            ClassId: binding.ClassId,
            LoadoutId: loadout.Id,
            PrimaryItemId: primaryItemId,
            SecondaryItemId: secondaryItemId,
            UtilityItemId: utilityItemId,
            EquippedSlot: equippedSlot,
            EquippedItemId: equippedItemId,
            AcquiredItemId: acquiredItemId);
    }

    public GameplayClassLoadoutDefinition GetDefaultLoadout(PlayerClass playerClass)
    {
        var gameplayClass = GetClassDefinition(playerClass);
        return gameplayClass.Loadouts[gameplayClass.DefaultLoadoutId];
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
        if (_primaryWeaponKindsByBehaviorId.TryGetValue(behaviorId, out var kind))
        {
            return kind;
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
}

public sealed record GameplayClassRuntimeBinding(
    PlayerClass PlayerClass,
    string ModPackId,
    string ClassId,
    bool SupportsExperimentalAcquiredWeapon,
    string PrimaryWeaponKillFeedSprite);
